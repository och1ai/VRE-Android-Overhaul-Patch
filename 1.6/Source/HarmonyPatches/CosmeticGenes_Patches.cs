using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The component editor lists genes by category and then by each gene's own display order, which is
    // whatever number its mod picked, so body coverings (furskin, insectskin, scaleskin...) end up
    // scattered through the cosmetic section between ears and horns. Gather them into one block at the
    // top of it.
    [HarmonyPatch(typeof(Utils), nameof(Utils.AndroidGenesGenesInOrder), MethodType.Getter)]
    public static class Utils_AndroidGenesGenesInOrder_Coverings_Patch
    {
        private static bool arranged;

        public static void Postfix(List<GeneDef> __result)
        {
            if (arranged)
            {
                return;
            }
            arranged = true;
            List<GeneDef> coverings = __result
                .Where(g => g.fur != null && g.displayCategory?.defName == "Cosmetic")
                .OrderBy(g => g.label).ToList();
            if (coverings.Count < 2)
            {
                return;
            }
            int first = __result.FindIndex(g => g.displayCategory?.defName == "Cosmetic");
            __result.RemoveAll(coverings.Contains);
            __result.InsertRange(first, coverings);
        }
    }
}
