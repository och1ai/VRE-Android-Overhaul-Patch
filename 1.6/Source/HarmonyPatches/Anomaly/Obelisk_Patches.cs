using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Androids are sealed synthetic frames: the Anomaly obelisks can't rewrite them. A twisted (mutator)
    // obelisk can't graft tentacles / fleshmass organs onto them, and a corrupted (duplicator) obelisk
    // can't copy them. Both the passive obelisk pulse and a deliberately triggered activation are covered,
    // because the block lives on the shared utility methods every path funnels through.

    // Twisted / mutator obelisk: refuse to mutate an android's body.
    [HarmonyPatch(typeof(FleshbeastUtility), nameof(FleshbeastUtility.TryGiveMutation))]
    public static class FleshbeastUtility_TryGiveMutation_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn != null && pawn.IsAndroid())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Corrupted / duplicator obelisk: an android cannot be duplicated.
    [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.TryDuplicatePawn))]
    public static class AnomalyUtility_TryDuplicatePawn_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn originalPawn, ref Pawn duplicatePawn, ref bool __result)
        {
            if (originalPawn != null && originalPawn.IsAndroid())
            {
                duplicatePawn = null;
                __result = false;
                return false;
            }
            return true;
        }
    }

    // An android may not be ordered to force-activate a duplicator or mutator obelisk (there is nothing to
    // gain - it can't be copied or transformed - so the option is disabled with an "is android" reason).
    [HarmonyPatch(typeof(CompObeliskTriggerInteractor), nameof(CompObeliskTriggerInteractor.CompFloatMenuOptions))]
    public static class CompObeliskTriggerInteractor_CompFloatMenuOptions_Patch
    {
        public static void Postfix(CompObeliskTriggerInteractor __instance, Pawn selPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (selPawn == null || !selPawn.IsAndroid())
            {
                return;
            }
            bool transformsOrDuplicates = __instance.parent.GetComp<CompObelisk_Duplicator>() != null
                || __instance.parent.GetComp<CompObelisk_Mutator>() != null;
            if (!transformsOrDuplicates)
            {
                return;
            }
            __result = new List<FloatMenuOption>
            {
                new FloatMenuOption("VREA.IsAndroid".Translate(selPawn.Named("PAWN")).CapitalizeFirst(), null)
            };
        }
    }
}
