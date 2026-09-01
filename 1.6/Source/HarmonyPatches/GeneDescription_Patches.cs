using HarmonyLib;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // Component overheating enables the memory need, so vanilla's gene panel lists a flat
    // "Adds need: memory space" on it - which reads as though the gene fits a memory drive. It does not:
    // heat only scrambles whatever drive is there, and the need appears solely while the android is
    // actually overheating, then disappears again once it cools. Qualify that one line so the panel says
    // "Adds need: memory space (overheating)" and the temporary need is not confused with the memory
    // hardware that provides a permanent one.
    //
    // Patched on the private builder rather than the DescriptionFull property: the property caches its
    // result, so a postfix there would append the qualifier again on every call.
    [HarmonyPatch(typeof(GeneDef), "GetDescriptionFull")]
    public static class GeneDef_GetDescriptionFull_Patch
    {
        private const string OverheatingGene = "VREA_ComponentOverheating";
        private const string MemoryNeed = "VREA_MemorySpace";

        public static void Postfix(GeneDef __instance, ref string __result)
        {
            if (__instance.defName != OverheatingGene || __result.NullOrEmpty())
            {
                return;
            }
            NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(MemoryNeed);
            if (need == null || need.label.NullOrEmpty())
            {
                return;
            }
            // Qualify only the label inside vanilla's "adds need" line, never any other mention of it in
            // the description prose above.
            int lineStart = __result.IndexOf("AddsNeed".Translate().ToString(), System.StringComparison.Ordinal);
            if (lineStart < 0)
            {
                return;
            }
            int labelAt = __result.IndexOf(need.label, lineStart, System.StringComparison.OrdinalIgnoreCase);
            if (labelAt < 0)
            {
                return;
            }
            int labelEnd = labelAt + need.label.Length;
            __result = __result.Substring(0, labelEnd)
                + " (" + "VREAOverhaul.WhileOverheating".Translate() + ")"
                + __result.Substring(labelEnd);
        }
    }
}
