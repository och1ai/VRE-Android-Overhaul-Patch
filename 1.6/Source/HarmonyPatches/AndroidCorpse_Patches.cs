using System.Collections.Generic;
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

    // An "extract subcore" toggle on a dead android's body, carrying the subcore item's own icon. It
    // queues the same designation the Orders designator does, so the recovery is discoverable straight
    // from the selected corpse - the way a mechlink or a cortical stack is pulled from a dead pawn -
    // instead of only from a menu the player has to know to go looking in.
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class ThingWithComps_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, ThingWithComps __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            DesignationDef designation = SubcoreDefOf.ExtractSubcoreDesignation;
            if (designation == null || SubcoreDefOf.SubcoreItem == null
                || !(__instance is Corpse corpse) || !corpse.Spawned || corpse.Map == null
                || !AndroidDeath.HasSubcore(corpse.InnerPawn, out _))
            {
                yield break;
            }
            yield return new Command_Toggle
            {
                defaultLabel = "VREA.DesignatorExtractSubcore".Translate(),
                defaultDesc = "VREA.DesignatorExtractSubcoreDesc".Translate(),
                icon = SubcoreDefOf.SubcoreItem.uiIcon,
                isActive = () => corpse.Map.designationManager.DesignationOn(corpse, designation) != null,
                toggleAction = delegate
                {
                    DesignationManager manager = corpse.Map.designationManager;
                    Designation existing = manager.DesignationOn(corpse, designation);
                    if (existing != null)
                    {
                        manager.RemoveDesignation(existing);
                    }
                    else
                    {
                        manager.AddDesignation(new Designation(corpse, designation));
                    }
                }
            };
        }
    }
}
