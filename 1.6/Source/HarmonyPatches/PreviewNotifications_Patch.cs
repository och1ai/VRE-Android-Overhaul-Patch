using HarmonyLib;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // Mutes player notifications about a pawn while a throwaway designer-preview android is being
    // generated or edited. It is not a real colonist - the assembler and the designer build one, churn its
    // genes to show the player what the current loadout looks like, and throw it away - so the downed and
    // undowned notices that churn produces have nothing to do with the colony.
    //
    // ForkCompat.suppressAndroidNotifications is raised around each of those blocks; this is what reads it.
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.ShouldSendNotificationAbout))]
    public static class PawnUtility_ShouldSendNotificationAbout_Patch
    {
        public static bool Prefix(ref bool __result)
        {
            if (!ForkCompat.suppressAndroidNotifications)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
