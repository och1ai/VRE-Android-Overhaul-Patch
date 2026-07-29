using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The overhaul rewrote the androidtype editor's base class (Window_CreateAndroidBase): the drone
    // default icon instead of the vanilla blank "Basic" face, one blood type and one power source
    // preselected rather than every core component at once, and the appearance genes hidden because they
    // now belong to the android designer. The overlay ships that rewrite as its own class, so only the
    // windows deriving from OUR base get it.
    //
    // Three places open an editor, and all three built the ORIGINAL's window - so the rewrite only ever
    // showed up on the one path that goes through the designer. These patches repoint each of them at the
    // overlay's equivalent, which is what actually places the new UI everywhere it belongs:
    //
    //   - the "android editor" button on the starting-pawns page,
    //   - the "android editor..." option on a pawn's character card,
    //   - the behaviourist station, when a colonist is loaded into it for modification.
    //
    // The original's windows are otherwise unchanged from the fork's, so each overlay window is that same
    // class sitting on the rewritten base.

    // The androidtype editor starts a new type by auto-selecting every component that cannot be removed
    // from an android, i.e. every gene flagged isCoreComponent. That was right when there was exactly one
    // power source and one blood type; now that each is a mutually exclusive CHOICE between core
    // components, it selects all of them at once - a new android would start with a reactor AND a battery,
    // neutroamine AND hemogenic AND bloodless.
    //
    // So after the window sets its defaults, trim each exclusion group down to the one the stock android
    // is built with: a reactor, and neutroamine blood.
    //
    // With the three entry points below repointed, nothing we know of opens the original's editor any more
    // and this should never fire. It stays as the safety net for any path that still reaches it.
    [HarmonyPatch]
    public static class Window_CreateAndroidBase_Ctor_Patch
    {
        private const string BloodTag = "AndroidBlood";
        private const string PowerTag = "AndroidPower";

        public static MethodBase TargetMethod()
        {
            return AccessTools.Constructor(typeof(VREAndroids.Window_CreateAndroidBase), new[] { typeof(Action) });
        }

        public static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the androidtype editor constructor; a new "
                + "androidtype will start with every power source and blood type selected at once. Nothing "
                + "else is affected.");
            return false;
        }

        public static void Postfix(VREAndroids.Window_CreateAndroidBase __instance)
        {
            List<GeneDef> selected = __instance.SelectedGenes;
            if (selected == null)
            {
                return;
            }
            KeepOnly(selected, PowerTag, "VREA_Power");
            KeepOnly(selected, BloodTag, "VREA_NeutroCirculation");
        }

        // Drops every selected gene carrying the exclusion tag, then puts back the default one.
        private static void KeepOnly(List<GeneDef> selected, string exclusionTag, string keepDefName)
        {
            GeneDef keep = DefDatabase<GeneDef>.GetNamedSilentFail(keepDefName);
            selected.RemoveAll(g => g.exclusionTags != null && g.exclusionTags.Contains(exclusionTag));
            if (keep != null)
            {
                selected.Add(keep);
            }
        }
    }

    // The "android editor..." option on a pawn's character card. Here the window is built inside the
    // option's own lambda, i.e. a compiler-generated closure whose name we would have to hard-code, so this
    // one restates the option instead of swapping an instruction. It is two lines and the option's label
    // and default priority are the original's.
    [HarmonyPatch]
    public static class CharacterCard_AndroidEditorOption_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(VREAndroids.CharacterCardUtility_LifestageAndXenotypeOptions_Patch),
                "AddAndroidEditorOption");
        }

        public static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the character card's android editor option; "
                + "it will open the unmodified androidtype editor. Nothing else is affected.");
            return false;
        }

        public static bool Prefix(Pawn pawn, List<FloatMenuOption> list, Action randomizeCallback)
        {
            list.Add(new FloatMenuOption("VREA.AndroidEditor".Translate() + "...", delegate
            {
                Find.WindowStack.Add(new Window_CreateAndroidXenotype(StartingPawnUtility.PawnIndex(pawn), delegate
                {
                    CharacterCardUtility.cachedCustomXenotypes = null;
                    randomizeCallback();
                }));
            }));
            return false;
        }
    }

    // The "android editor" button along the bottom of the starting-pawns page. Here the window is built in
    // the method itself, so swap that one instruction rather than restating the button's layout maths - the
    // original names its first parameter __instance, which Harmony reserves and would pass as null to a
    // prefix on a static method like this one.
    [HarmonyPatch]
    public static class StartingPawns_AndroidEditorButton_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(VREAndroids.Page_ConfigureStartingPawns_DrawXenotypeEditorButton_Patch),
                "AddAndroidEditorButton");
        }

        public static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the starting-pawns android editor button; it "
                + "will open the unmodified androidtype editor. Nothing else is affected.");
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return AndroidEditorEntryPoints.SwapXenotypeEditor(instructions, "the starting-pawns page");
        }
    }

    // Shared plumbing for repointing a construction of the original's standalone androidtype editor at the
    // overlay's.
    public static class AndroidEditorEntryPoints
    {
        // Takes the same two arguments the stock constructor does and leaves one window on the stack, so
        // the surrounding IL is unchanged.
        public static Window MakeXenotypeEditor(int generationRequestIndex, Action callback)
        {
            return new Window_CreateAndroidXenotype(generationRequestIndex, callback);
        }

        public static IEnumerable<CodeInstruction> SwapXenotypeEditor(
            IEnumerable<CodeInstruction> instructions, string where)
        {
            MethodInfo replacement = AccessTools.Method(
                typeof(AndroidEditorEntryPoints), nameof(MakeXenotypeEditor));
            bool swapped = false;
            foreach (CodeInstruction instruction in instructions)
            {
                // Each of these methods builds exactly one window, so matching on the type is enough - and
                // it does not depend on reflection handing back the same ConstructorInfo Harmony read.
                if (!swapped && instruction.opcode == OpCodes.Newobj
                    && instruction.operand is ConstructorInfo ctor
                    && ctor.DeclaringType == typeof(VREAndroids.Window_CreateAndroidXenotype))
                {
                    swapped = true;
                    // Keep any labels/blocks on the original instruction so branch targets survive.
                    yield return new CodeInstruction(OpCodes.Call, replacement)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };
                    continue;
                }
                yield return instruction;
            }
            if (!swapped)
            {
                Log.Warning("[VRE-Android Overhaul] " + where + " no longer builds the androidtype editor "
                    + "where expected; it will open the unmodified one. Nothing else is affected.");
            }
        }
    }

    // The behaviourist station opens the modification editor the moment a colonist is loaded into it, at
    // the tail of TryAcceptPawn - the rest of which is pawn-transfer bookkeeping we have no reason to
    // restate. So swap just the one instruction that builds the window.
    //
    // Closing the stock window and opening ours instead is not an option: its Close() calls
    // CancelModification() whenever no project is queued yet, which ejects the pawn the method just loaded.
    // The window must never be constructed in the first place.
    [HarmonyPatch]
    public static class BehavioristStation_ModificationWindow_Patch
    {
        private static readonly ConstructorInfo StockWindowCtor =
            AccessTools.Constructor(typeof(VREAndroids.Window_AndroidModification),
                new[] { typeof(VREAndroids.Building_AndroidBehavioristStation), typeof(Pawn), typeof(Action) });

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(VREAndroids.Building_AndroidBehavioristStation), "TryAcceptPawn");
        }

        public static bool Prepare()
        {
            if (TargetMethod() != null && StockWindowCtor != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the behaviourist station's accept-pawn "
                + "method; it will open the unmodified androidtype editor. Nothing else is affected.");
            return false;
        }

        // Takes the same three arguments the stock constructor does and leaves one window on the stack, so
        // the surrounding IL is unchanged.
        public static Window MakeWindow(Building_AndroidBehavioristStation station, Pawn android, Action callback)
        {
            return new Window_AndroidModification(station, android, callback);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(
                typeof(BehavioristStation_ModificationWindow_Patch), nameof(MakeWindow));
            bool swapped = false;
            foreach (CodeInstruction instruction in instructions)
            {
                // The method builds exactly one window, so matching on the type is enough - and it does not
                // depend on reflection handing back the same ConstructorInfo instance Harmony read.
                if (!swapped && instruction.opcode == OpCodes.Newobj
                    && instruction.operand is ConstructorInfo ctor
                    && ctor.DeclaringType == typeof(VREAndroids.Window_AndroidModification))
                {
                    swapped = true;
                    // Keep any labels/blocks on the original instruction so branch targets survive.
                    yield return new CodeInstruction(OpCodes.Call, replacement)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };
                    continue;
                }
                yield return instruction;
            }
            if (!swapped)
            {
                Log.Warning("[VRE-Android Overhaul] The behaviourist station no longer builds its editor "
                    + "window where expected; it will open the unmodified one. Nothing else is affected.");
            }
        }
    }
}
