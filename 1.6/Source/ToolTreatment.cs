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

        // True only when it is one of THIS mod's precepts doing the treating. The original already applies
        // every tool consequence for its own "androids: tools" precept, so the overlay's patches must fire
        // only for the cases it added - otherwise effects that accumulate (the slave headcount) would be
        // applied twice. In practice all these precepts share one issue and an ideoligion can hold only
        // one, but the check costs nothing and does not rely on that.
        public static bool TreatedAsToolByOverlayPreceptOnly(Ideo ideo, Pawn android)
        {
            if (!IdeoTreatsAndroidAsTool(ideo, android))
            {
                return false;
            }
            return tools == null || !ideo.HasPrecept(tools);
        }

        public static bool TreatedAsToolByColonyOverlayPreceptOnly(Pawn android)
        {
            return TreatedAsToolByOverlayPreceptOnly(Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo, android);
        }
    }
}
