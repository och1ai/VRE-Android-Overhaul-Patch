using System.Linq;
using VEF.Genes;
using Verse;

namespace VREAndroidsOverhaul
{
    // The original generates the skill and aptitude subroutines in code and gives them a Subroutine frame
    // for the xenogene case only. When such a gene sits as an endogene - which is how an android's own
    // components are shown in several panels - there is no endogene frame, so it falls back to the plain
    // germline gene frame and the subroutine looks like an ordinary gene. Copy the xenogene frame across
    // wherever the endogene one is missing. Frames are read from the extension at draw time, so doing it
    // once at startup is enough.
    [StaticConstructorOnStartup]
    public static class GeneBackgrounds
    {
        static GeneBackgrounds()
        {
            int fixedCount = 0;
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefs)
            {
                GeneExtension extension = gene.modExtensions?.OfType<GeneExtension>().FirstOrDefault();
                if (extension != null && extension.backgroundPathEndogenes.NullOrEmpty()
                    && !extension.backgroundPathXenogenes.NullOrEmpty()
                    && extension.backgroundPathXenogenes.Contains("GeneBackground_Subroutine"))
                {
                    extension.backgroundPathEndogenes = extension.backgroundPathXenogenes;
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
            {
                Log.Message("[VRE-Android Overhaul] gave " + fixedCount + " subroutine genes their endogene frame.");
            }
        }
    }
}
