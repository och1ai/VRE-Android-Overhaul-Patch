using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android without the ideological subroutine can never be given an ideoligion. This blocks every
    // vector that would assign one - conversion, social interactions, pawn generation, the colony
    // ideoligion being applied on load - at the single choke point they all funnel through. Clearing to
    // null (dropping an ideoligion) is always allowed, and androids that DO carry the subroutine, as well
    // as every non-android, are unaffected.
    [HarmonyPatch(typeof(Pawn_IdeoTracker), "SetIdeo")]
    public static class Pawn_IdeoTracker_SetIdeo_Patch
    {
        public static bool Prefix(Pawn ___pawn, Ideo ideo)
        {
            return ideo == null || IdeoCapability.CanHoldIdeoligion(___pawn);
        }
    }

    // The convert ability can't target an android with no way to hold beliefs. Refusing at targeting time
    // means the moral guide never walks over to preach at a machine that would reject it anyway.
    [HarmonyPatch(typeof(CompAbilityEffect_Convert), "Valid")]
    public static class CompAbilityEffect_Convert_Valid_Patch
    {
        public static void Postfix(LocalTargetInfo target, bool throwMessages, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            Pawn pawn = target.Pawn;
            if (pawn != null && pawn.IsAndroid() && !IdeoCapability.CanHoldIdeoligion(pawn))
            {
                if (throwMessages)
                {
                    Messages.Message("VREAOverhaul.CannotConvertAndroid".Translate(pawn.Named("PAWN")), pawn,
                        MessageTypeDefOf.RejectInput, historical: false);
                }
                __result = false;
            }
        }
    }
}
