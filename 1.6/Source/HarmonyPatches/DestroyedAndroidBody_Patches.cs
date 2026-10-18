using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A destroyed android - its subcore still intact - is wreckage the colony can rebuild, not a dead
    // body. Seeing one lying about is only distressing to someone whose ideoligion holds androids to be
    // people: an "equal" precept, or the awakened-only one for an android that has awakened. Everyone else
    // looks at a broken machine. This is the vanilla "observed a corpse" history event, which is what the
    // mood thoughts and precept reactions to a body on the floor hang off.
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.GiveObservedHistoryEvent))]
    public static class Corpse_GiveObservedHistoryEvent_Android_Patch
    {
        public static void Postfix(Corpse __instance, Pawn observer, ref HistoryEventDef __result)
        {
            if (__result == null)
            {
                return;
            }
            Pawn inner = __instance.InnerPawn;
            if (inner == null || !inner.IsAndroid() || !AndroidDeath.HasSubcore(inner, out _))
            {
                return;
            }
            if (!SeesAndroidsAsEquals(observer, inner))
            {
                __result = null;
            }
        }

        private static bool SeesAndroidsAsEquals(Pawn observer, Pawn android)
        {
            Ideo ideo = observer?.Ideo;
            if (ideo == null)
            {
                return false;
            }
            if (HasPrecept(ideo, "VRE_Androids_Equal"))
            {
                return true;
            }
            return android.IsAwakened() && HasPrecept(ideo, "VRE_Androids_EqualOnlyAwakened");
        }

        private static bool HasPrecept(Ideo ideo, string defName)
        {
            PreceptDef def = DefDatabase<PreceptDef>.GetNamedSilentFail(defName);
            return def != null && ideo.HasPrecept(def);
        }
    }
}
