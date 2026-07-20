using HarmonyLib;
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
            if (pawn != null && ToolTreatment.IsTreatedAsToolByColony(pawn))
            {
                __result = ToolAndroidColor;
            }
        }
    }
}
