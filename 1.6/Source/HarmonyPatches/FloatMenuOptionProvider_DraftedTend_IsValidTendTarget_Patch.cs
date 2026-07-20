using HarmonyLib;
using VREAndroids;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // Androids are never tended (they are repaired instead), so the drafted-doctor "Tend"
    // float-menu option must not appear for them.
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedTend), "IsValidTendTarget")]
    public static class FloatMenuOptionProvider_DraftedTend_IsValidTendTarget_Patch
    {
        public static void Postfix(Pawn patient, ref bool __result)
        {
            if (patient.IsAndroid())
            {
                __result = false;
            }
        }
    }
}
