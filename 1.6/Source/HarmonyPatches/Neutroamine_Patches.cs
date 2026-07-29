using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Adds the "extract neutroamine" surgery, the mirror of vanilla's extract-hemogen-pack. It has to be an
    // implied def rather than XML because its recipeUsers is every humanlike race in the load order, which
    // XML cannot enumerate - the same reason the original generates its administer-neutroamine recipe in
    // code. A postfix is still pre-resolve, so the def lands in the database in time.
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class DefGenerator_ExtractNeutroamine_Patch
    {
        public static void Postfix()
        {
            if (DefDatabase<RecipeDef>.GetNamedSilentFail("VREA_ExtractNeutroamine") != null)
            {
                return;
            }
            List<ThingDef> humanlikes = DefDatabase<ThingDef>.AllDefs
                .Where(x => x.race?.Humanlike ?? false).ToList();
            RecipeDef recipe = new RecipeDef
            {
                defName = "VREA_ExtractNeutroamine",
                label = "VREA.ExtractNeutroamine".Translate(),
                description = "VREA.ExtractNeutroamineDesc".Translate(),
                jobString = "VREA.ExtractingNeutroamine".Translate(),
                workerClass = typeof(Recipe_ExtractNeutroamine),
                workAmount = 500,
                targetsBodyPart = false,
                anesthetize = false,
                hideBodyPartNames = true,
                surgerySuccessChanceFactor = 99999f,
                recipeUsers = humanlikes,
                workSkill = SkillDefOf.Crafting,
                workSkillLearnFactor = 2f,
                effectWorking = VREA_DefOf.ButcherMechanoid,
                soundWorking = VREA_DefOf.Recipe_Machining,
            };
            // Vanilla keeps the icon field private and only exposes the UIIconThing getter, so it has to be
            // set reflectively. Losing it costs the bill its neutroamine icon and nothing else, so a miss
            // here is not worth failing the whole recipe over.
            FieldInfo uiIconThing = AccessTools.Field(typeof(RecipeDef), "uiIconThing");
            if (uiIconThing != null)
            {
                uiIconThing.SetValue(recipe, VREA_DefOf.Neutroamine);
            }
            else
            {
                Log.Warning("[VRE-Android Overhaul] Could not set the extract-neutroamine bill icon; the "
                    + "surgery works, it just shows no icon.");
            }
            DefGenerator.AddImpliedDef(recipe);
        }
    }

    // The two remaining places the original hardcodes a 100-neutroamine reservoir. Both are ordinary methods
    // with the constant appearing exactly once, so a one-instruction swap is enough and leaves the rest of
    // the original's logic alone. The float-menu option decides how much neutroamine to carry and the
    // repointed job driver divides by the same figure, so the pair stays consistent.
    [HarmonyPatch]
    public static class FloatMenuOptionProvider_RefuelWithNeutroamine_Patch
    {
        // The reservoir figure is not in GetSingleOptionFor itself but in the option's click action, which
        // the compiler lifted into a display class. Its generated name carries an index that changes
        // whenever the original is recompiled, so it is found by shape - the one lambda of that method -
        // rather than hard-coded.
        public static MethodBase TargetMethod()
        {
            foreach (Type nested in typeof(FloatMenuOptionProvider_RefuelWithNeutroamine)
                .GetNestedTypes(AccessTools.all))
            {
                foreach (MethodInfo method in nested.GetMethods(AccessTools.all))
                {
                    if (method.Name.StartsWith("<GetSingleOptionFor>b__"))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        public static bool Prepare() => NeutroamineReservoir.Found(TargetMethod(),
            "the refuel-with-neutroamine float menu option");

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => NeutroamineReservoir.Swap(instructions, 100f, AndroidNeutroamine.PerFullReservoir,
                "the refuel-with-neutroamine float menu option");
    }

    // The casket restores 1/100th of a reservoir per unit of fuel it burns; at a 40-unit reservoir that is
    // 1/40th. Without this the casket's configured 40-unit target fills less than half an android.
    [HarmonyPatch]
    public static class Building_NeutroCasket_TickInterval_Patch
    {
        public static MethodBase TargetMethod() => AccessTools.Method(
            typeof(Building_NeutroCasket), "TickInterval", new[] { typeof(int) });

        public static bool Prepare() => NeutroamineReservoir.Found(TargetMethod(),
            "the neutro casket's tick");

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => NeutroamineReservoir.Swap(instructions, -0.01f, -1f / AndroidNeutroamine.PerFullReservoir,
                "the neutro casket's refill rate");
    }

    public static class NeutroamineReservoir
    {
        // Harmony aborts the whole mod's patching run on an undefined target method, so an optional patch on
        // the original's code has to be gated rather than left to throw.
        public static bool Found(MethodBase target, string where)
        {
            if (target != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find " + where + ", so it keeps the original's "
                + "100-neutroamine reservoir. Refuelling will be inconsistent with the rest of the mod, but "
                + "nothing else is affected.");
            return false;
        }

        public static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions,
            float from, float to, string where)
        {
            bool swapped = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (!swapped && instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float value && Mathf.Approximately(value, from))
                {
                    swapped = true;
                    // Carry any labels/blocks over so branch targets survive.
                    yield return new CodeInstruction(OpCodes.Ldc_R4, to)
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
                Log.Warning("[VRE-Android Overhaul] " + where + " no longer holds the reservoir size where "
                    + "expected, so it keeps the original's 100. Refuelling will be inconsistent with the "
                    + "rest of the mod, but nothing else is affected.");
            }
        }
    }
}
