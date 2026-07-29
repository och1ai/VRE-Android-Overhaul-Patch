using HarmonyLib;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // The body being regrown in the printer is drawn upright and facing front, as if the platform were
    // holding it up, rather than lying dead on the floor. UnfinishedAndroid.DrawAt flags the pawn around its
    // own draw call; this is what answers for it.
    //
    // The original already prefixes GetPosture, to stand androids up on a charging stand, but that branch
    // requires the pawn to be Spawned and the printer's body never is - so the two never contend. Ordered
    // ahead of it anyway, so the result does not depend on which prefix Harmony happens to run first.
    [HarmonyPatch(typeof(PawnUtility), "GetPosture")]
    [HarmonyBefore(VREAndroidsOverhaulMod.OriginalHarmonyId)]
    public static class PawnUtility_GetPosture_ForcedStanding_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn p, ref PawnPosture __result)
        {
            if (ForkCompat.forceStandingPawn == null || p != ForkCompat.forceStandingPawn)
            {
                return true;
            }
            __result = PawnPosture.Standing;
            return false;
        }
    }
}
