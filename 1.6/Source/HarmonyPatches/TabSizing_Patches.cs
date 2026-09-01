using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A non-awakened android gets a compact, mechanoid-style needs tab: just the visible need bars
    // (power + memory), with no mood/thoughts panel. The full humanlike tab is huge only because
    // pawn.needs.mood is non-null - it is kept for the awakening mechanic, never shown - so these two
    // patches shrink the card to what is actually drawn in it.
    [HarmonyPatch(typeof(NeedsCardUtility), nameof(NeedsCardUtility.GetSize))]
    public static class NeedsCardUtility_GetSize_Patch
    {
        public static void Postfix(Pawn pawn, ref Vector2 __result)
        {
            if (pawn.IsAndroid() && pawn.IsAwakened() is false)
            {
                int count = Mathf.Max(1, pawn.needs.AllNeeds.Count(n => n.ShowOnNeedList));
                __result = new Vector2(225f, count * Mathf.Min(70f, NeedsCardUtility.FullSize.y / count));
            }
        }
    }

    [HarmonyPatch(typeof(NeedsCardUtility), nameof(NeedsCardUtility.DoNeedsMoodAndThoughts))]
    public static class NeedsCardUtility_DoNeedsMoodAndThoughts_Patch
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (pawn.IsAndroid() && pawn.IsAwakened() is false)
            {
                NeedsCardUtility.DoNeeds(rect, pawn);
                return false;
            }
            return true;
        }
    }
}
