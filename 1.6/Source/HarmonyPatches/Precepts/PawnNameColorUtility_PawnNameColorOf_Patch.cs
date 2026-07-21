using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // When the colony's ideoligion treats an android as a mere tool, its colonist-bar name is tinted a cold
    // blue - the android analogue of the amber slave colour.
    [HarmonyPatch(typeof(PawnNameColorUtility), nameof(PawnNameColorUtility.PawnNameColorOf))]
    public static class PawnNameColorUtility_PawnNameColorOf_Patch
    {
        public static readonly Color ToolAndroidColor = new Color(0.4f, 0.65f, 1f);

        public static void Postfix(Pawn pawn, ref Color __result)
        {
            if (pawn == null)
            {
                return;
            }
            if (ToolTreatment.IsTreatedAsToolByColony(pawn))
            {
                __result = ToolAndroidColor;
                return;
            }
            // A mechlike android is still a colonist in the bar, not a mech: vanilla would tint it with the
            // "uncontrolled player mech" colour whenever it has no overseer or no bandwidth. Keep it white.
            if (MechOversightUtil.IsOversightAndroid(pawn) && pawn.Faction == Faction.OfPlayer)
            {
                __result = Color.white;
            }
        }
    }
}
