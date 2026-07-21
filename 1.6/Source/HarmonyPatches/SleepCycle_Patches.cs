using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The sleep-cycle subroutine gives an android a real Rest need: it tires, and it has to sleep.
    //
    // Rest has been taken off the original's excluded-needs list (Patches/SleepCycle_RestNeed.xml), which
    // would hand the need to EVERY android, so both halves of the original's rule are restated here for
    // androids that lack the subroutine - one on the "should this pawn have it" query, one on the actual
    // add. Both are needed: the query drives periodic re-evaluation, the add is what a fresh pawn goes
    // through.
    internal static class SleepCycle
    {
        private static GeneDef gene;
        private static bool resolved;

        private static GeneDef Gene
        {
            get
            {
                if (!resolved)
                {
                    gene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_SleepNeed");
                    resolved = true;
                }
                return gene;
            }
        }

        // True for an android that must NOT have the Rest need.
        public static bool RefusesRest(Pawn pawn, NeedDef need)
        {
            if (need != NeedDefOf.Rest || pawn == null || !pawn.IsAndroid())
            {
                return false;
            }
            return Gene == null || !pawn.HasActiveGene(Gene);
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Pawn_NeedsTracker_ShouldHaveNeed_Overlay_Patch
    {
        [HarmonyPriority(int.MinValue)]
        [HarmonyAfter("VREAndroidsMod")]
        public static void Postfix(Pawn ___pawn, NeedDef nd, ref bool __result)
        {
            if (__result && SleepCycle.RefusesRest(___pawn, nd))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "AddNeed")]
    public static class Pawn_NeedsTracker_AddNeed_Overlay_Patch
    {
        public static bool Prefix(Pawn ___pawn, NeedDef nd)
        {
            return !SleepCycle.RefusesRest(___pawn, nd);
        }
    }
}
