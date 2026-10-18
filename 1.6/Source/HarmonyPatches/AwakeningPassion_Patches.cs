using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The awakening letter raises a chosen skill's passion with the vanilla PassionExtension.IncrementPassion
    // - none to minor, minor to major. Vanilla Skills Expanded replaces that with a step through EVERY
    // passion it knows, ordered by learn rate, so with Alpha Skills loaded the step off "none" lands on
    // whichever of its passions sits next in that order. Biotech's own growth moments dodge this by having
    // VSE swap in a choice of the default passion; the android letter never got the same treatment.
    //
    // An awakening is always the plain one-flame passion (then two flames), so the letter and the dialog
    // that previews it both use the vanilla step.
    public static class AwakeningPassion
    {
        public static Passion Increment(Passion passion)
        {
            switch ((int)passion)
            {
                case 0:
                    return Passion.Minor;
                case 1:
                    return Passion.Major;
                default:
                    return passion;
            }
        }

        public static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(PassionExtension), nameof(PassionExtension.IncrementPassion));
            MethodInfo ours = AccessTools.Method(typeof(AwakeningPassion), nameof(Increment));
            foreach (CodeInstruction ins in instructions)
            {
                if (ins.Calls(original))
                {
                    yield return new CodeInstruction(OpCodes.Call, ours);
                }
                else
                {
                    yield return ins;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ChoiceLetter_AndroidAwakened), nameof(ChoiceLetter_AndroidAwakened.MakeChoices))]
    public static class ChoiceLetter_AndroidAwakened_MakeChoices_Passion_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return AwakeningPassion.Swap(instructions);
        }
    }

    [HarmonyPatch(typeof(Dialog_AndroidAwakenedChoices), "DrawPassionChoices")]
    public static class Dialog_AndroidAwakenedChoices_DrawPassionChoices_Passion_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return AwakeningPassion.Swap(instructions);
        }
    }
}
