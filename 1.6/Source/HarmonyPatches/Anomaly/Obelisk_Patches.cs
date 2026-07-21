using HarmonyLib;
using RimWorld;
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

    // An android may not be ordered to force-activate a duplicator or mutator obelisk - there is nothing to
    // gain, since it can't be copied or transformed.
    //
    // The refusal is expressed as a rejected CanInteract report rather than by rewriting the float menu,
    // so vanilla keeps ownership of the presentation: CompInteractable disables its own option and appends
    // the reason ("Trigger mutation (garry is an android)"), and the activation-targeting path shows the
    // matching "Cannot trigger: ..." message. It also means the option is never invented - the obelisk
    // offers nothing at all below study level 2, so an unstudied obelisk still gives away nothing about
    // what it would eventually do.
    [HarmonyPatch(typeof(CompObeliskTriggerInteractor), nameof(CompObeliskTriggerInteractor.CanInteract))]
    public static class CompObeliskTriggerInteractor_CanInteract_Patch
    {
        public static void Postfix(CompObeliskTriggerInteractor __instance, Pawn activateBy, ref AcceptanceReport __result)
        {
            // Only ever turn an accepted report into a rejected one; never explain away a refusal vanilla
            // already made for its own (possibly spoiler-bearing) reasons.
            if (!__result.Accepted || activateBy == null || !activateBy.IsAndroid())
            {
                return;
            }
            bool transformsOrDuplicates = __instance.parent.GetComp<CompObelisk_Duplicator>() != null
                || __instance.parent.GetComp<CompObelisk_Mutator>() != null;
            if (!transformsOrDuplicates)
            {
                return;
            }
            __result = new AcceptanceReport("VREAOverhaul.IsAndroid".Translate(activateBy.Named("PAWN")).ToString());
        }
    }
}
