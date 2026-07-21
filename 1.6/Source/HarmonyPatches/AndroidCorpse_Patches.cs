using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android's chassis is metal, not meat. Corpse.IngestibleNow already reports mechanoid corpses as
    // non-edible (their race isn't flesh); androids are a humanlike xenotype whose race IS flesh, so their
    // corpses would otherwise read as edible food. Force them non-ingestible too, so nothing - pawns,
    // animals, nutrient paste dispensers, food stockpiles - treats an android body as food. Butchering it
    // at the android butcher table for plasteel/steel/neutroamine is unaffected; that path does not use
    // this property.
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.IngestibleNow), MethodType.Getter)]
    public static class Corpse_IngestibleNow_Patch
    {
        public static void Postfix(Corpse __instance, ref bool __result)
        {
            if (__result && __instance.InnerPawn != null && __instance.InnerPawn.IsAndroid())
            {
                __result = false;
            }
        }
    }
}
