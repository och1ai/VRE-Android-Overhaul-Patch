using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The "occultist" subroutine is the ONLY thing that lets an android touch the anomalous:
    //  - take part in psychic rituals (including being the subject of an infused death refusal), and
    //  - capture, contain, feed/tend, suppress, harvest and study entities.
    // Every android without the subroutine is blocked from all of the above. These patches are safe to
    // load without the Anomaly DLC (the types exist in the base assembly); they simply never fire, since
    // the guard below short-circuits when Anomaly is inactive and the gene does not exist.
    internal static class OccultistUtil
    {
        private static GeneDef gene;
        private static bool resolved;

        private static GeneDef Gene
        {
            get
            {
                if (!resolved)
                {
                    gene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Occultist");
                    resolved = true;
                }
                return gene;
            }
        }

        // True when this pawn is an android that may NOT do anomalous work / rituals.
        public static bool BlockedAndroid(Pawn pawn)
        {
            if (!ModsConfig.AnomalyActive || pawn == null || !pawn.IsAndroid())
            {
                return false;
            }
            return Gene == null || !pawn.HasActiveGene(Gene);
        }

        // Same check, but it also records WHY the work giver produced nothing. This is vanilla's own idiom:
        // when a work giver returns no job, the float-menu builder drops the option entirely unless a
        // JobFailReason was set, in which case it renders "Cannot suppress prisoner: garry has no occultist
        // subroutine" - the refusal names both the work and the cause instead of a bare, contextless line.
        public static bool Deny(Pawn pawn)
        {
            if (!BlockedAndroid(pawn))
            {
                return false;
            }
            JobFailReason.Is("VREAOverhaul.NoOccultistSubroutine".Translate(pawn.Named("PAWN")));
            return true;
        }
    }

    // Psychic rituals: reject any android that lacks the occultist subroutine from every role - invoker,
    // participant, target, and the infused-death-refusal subject alike. On top of that, NO android (not
    // even an occultist or an awakened one) may ever be the subject of a psychophagy or chronophagy - there
    // is no organic mind or lifespan to devour.
    [HarmonyPatch]
    public static class PsychicRitualRoleDef_PawnCanDo_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PsychicRitualRoleDef), "PawnCanDo", new Type[]
            {
                typeof(PsychicRitualRoleDef.Context), typeof(Pawn), typeof(TargetInfo),
                typeof(PsychicRitualRoleDef.Reason).MakeByRefType()
            });
        }

        public static void Postfix(PsychicRitualRoleDef __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || pawn == null || !pawn.IsAndroid())
            {
                return;
            }
            if (__instance.defName == "PsychophagyTarget" || __instance.defName == "ChronophagyTarget"
                || OccultistUtil.BlockedAndroid(pawn))
            {
                __result = false;
            }
        }
    }

    // All entity work an android may not do: studying (normal and dark), tending, executing and harvesting
    // bioferrite from a contained entity.
    //
    // The gate sits on each concrete work giver's HasJobOnThing rather than on the coarse
    // WorkGiver_StudyBase.PotentialWorkThingsGlobal / WorkGiver_EntityOnPlatform.ShouldSkip it used to,
    // because those two make the work giver skip the target outright - which silently deletes the
    // right-click option instead of showing it greyed out with a reason. HasJobOnThing is late enough to
    // explain itself and still early enough that the work scanner never auto-assigns the job.
    [HarmonyPatch]
    public static class EntityWorkGiver_HasJobOnThing_Patch
    {
        private static readonly Type[] GatedWorkGivers =
        {
            typeof(WorkGiver_StudyInteract),
            typeof(WorkGiver_DarkStudyInteract),
            typeof(WorkGiver_TendEntity),
            typeof(WorkGiver_ExecuteEntity),
            typeof(WorkGiver_ExtractBioferrite)
        };

        private static IEnumerable<MethodBase> Gated()
        {
            return GatedWorkGivers
                .Select(t => (MethodBase)AccessTools.DeclaredMethod(t, "HasJobOnThing",
                    new[] { typeof(Pawn), typeof(Thing), typeof(bool) }))
                .Where(m => m != null);
        }

        // An empty TargetMethods() throws out of the mod constructor and aborts the whole patch run, so the
        // class is skipped outright if the game ever stops declaring these overrides.
        public static bool Prepare()
        {
            if (Gated().Any())
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the entity work-giver methods to gate; "
                + "androids without the occultist subroutine will be able to handle entities. Nothing else is affected.");
            return false;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return Gated();
        }

        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && OccultistUtil.Deny(pawn))
            {
                __result = false;
            }
        }
    }

    // Hauling a downed entity onto a holding platform. Blocked WITHOUT a JobFailReason on purpose: capture
    // is offered twice in the right-click menu (once by this work giver, once by the dedicated provider
    // below), and explaining it in both places would print the same refusal line twice. This one drops out
    // quietly; the provider is the one that speaks.
    [HarmonyPatch(typeof(WorkGiver_TakeEntityToHoldingPlatform), nameof(WorkGiver_TakeEntityToHoldingPlatform.HasJobOnThing))]
    public static class WorkGiver_TakeEntityToHoldingPlatform_HasJobOnThing_Patch
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && OccultistUtil.BlockedAndroid(pawn))
            {
                __result = false;
            }
        }
    }

    // Capturing a downed entity is offered by its own float-menu provider, not by the work giver above, so
    // gating the work giver alone would leave the right-click "Capture" working. Vanilla's provider already
    // has a refusal format for this ("Cannot capture X: no path"), so the block reuses it verbatim.
    [HarmonyPatch(typeof(FloatMenuOptionProvider_CaptureEntity), nameof(FloatMenuOptionProvider_CaptureEntity.GetOptionsFor))]
    public static class FloatMenuOptionProvider_CaptureEntity_GetOptionsFor_Patch
    {
        public static void Postfix(Thing clickedThing, FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            Pawn pawn = context?.FirstSelectedPawn;
            if (!OccultistUtil.BlockedAndroid(pawn))
            {
                return;
            }
            // Only ever replace options vanilla actually offered - never invent one for a thing it decided
            // was not capturable in the first place.
            if (__result == null || !__result.Any())
            {
                return;
            }
            string label = "CannotGenericWorkCustom".Translate("CaptureLower".Translate(clickedThing)) + ": "
                + "VREAOverhaul.NoOccultistSubroutine".Translate(pawn.Named("PAWN")).CapitalizeFirst();
            __result = new List<FloatMenuOption> { new FloatMenuOption(label, null) };
        }
    }

    // Suppressing a contained entity's activity level. This one is a warden job on the entity rather than
    // an entity work giver, so it needs its own gate.
    [HarmonyPatch(typeof(WorkGiver_Warden_SuppressActivity), nameof(WorkGiver_Warden_SuppressActivity.JobOnThing))]
    public static class WorkGiver_Warden_SuppressActivity_JobOnThing_Patch
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && OccultistUtil.Deny(pawn))
            {
                __result = null;
            }
        }
    }
}
