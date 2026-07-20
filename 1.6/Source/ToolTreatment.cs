using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Whether the colony's (or a given) ideoligion treats an android as a mere tool: the original "androids:
    // tools" precept, or one of the overlay's awakened-only precepts while the android has not awakened.
    // Precepts are resolved by defName so this stays decoupled from any DefOf.
    public static class ToolTreatment
    {
        private static PreceptDef tools, respectedAwakened, equalAwakened;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            tools = DefDatabase<PreceptDef>.GetNamedSilentFail("VRE_Androids_Tools");
            respectedAwakened = DefDatabase<PreceptDef>.GetNamedSilentFail("VRE_Androids_RespectedOnlyAwakened");
            equalAwakened = DefDatabase<PreceptDef>.GetNamedSilentFail("VRE_Androids_EqualOnlyAwakened");
        }

        public static bool IdeoTreatsAndroidAsTool(Ideo ideo, Pawn android)
        {
            if (ideo == null || android == null || !android.IsAndroid())
            {
                return false;
            }
            Resolve();
            if (tools != null && ideo.HasPrecept(tools))
            {
                return true;
            }
            if (android.IsAwakened())
            {
                return false;
            }
            return (respectedAwakened != null && ideo.HasPrecept(respectedAwakened))
                || (equalAwakened != null && ideo.HasPrecept(equalAwakened));
        }

        public static bool IsTreatedAsToolByColony(Pawn android)
        {
            return IdeoTreatsAndroidAsTool(Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo, android);
        }
    }
}
