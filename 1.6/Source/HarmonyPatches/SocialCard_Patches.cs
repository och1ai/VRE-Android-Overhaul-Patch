using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A "machine" android - emotionless (no emotion simulators, not awakened) and without the ideological
    // subroutine - has neither relationships nor an ideoligion, so its Social tab has nothing to say. The
    // original already hides the relations list for an emotionless android, which left the tab as a mostly
    // empty card with gaps where the relations and ideoligion sections used to be.
    //
    // Rather than shrink that card down to the interaction log, the tab is taken away entirely - button
    // included - so a base android's inspect pane simply has no Social tab. An android that carries the
    // ideological subroutine, or emotion simulators, or has awakened keeps the normal tab.
    //
    // (This is a deliberate departure from the fork, which keeps a log-only card at a reduced height.)
    [HarmonyPatch(typeof(ITab_Pawn_Social), nameof(ITab_Pawn_Social.IsVisible), MethodType.Getter)]
    public static class ITab_Pawn_Social_IsVisible_Patch
    {
        public static void Postfix(ITab_Pawn_Social __instance, ref bool __result)
        {
            if (__result && SocialTabPawn(__instance).SocialTabLogOnly())
            {
                __result = false;
            }
        }

        private static readonly MethodInfo SelPawnGetter =
            AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "SelPawnForSocialInfo");

        // ITab_Pawn_Social keeps its own selected-pawn property private, and it is the one that resolves a
        // corpse or a caravan entry to the pawn the card is actually about.
        private static Pawn SocialTabPawn(ITab_Pawn_Social tab)
        {
            return SelPawnGetter?.Invoke(tab, null) as Pawn;
        }
    }

    // Kept as the rule for what such an android's card *would* contain, for any path that draws the card
    // without going through the tab: the interaction log alone, no relations and no ideoligion section.
    [HarmonyPatch(typeof(SocialCardUtility), "DrawSocialCard")]
    public static class SocialCardUtility_DrawSocialCard_Patch
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (!pawn.SocialTabLogOnly())
            {
                return true;
            }
            Widgets.BeginGroup(rect);
            Text.Font = GameFont.Small;
            float top = Prefs.DevMode ? 20f : 15f;
            Rect logRect = new Rect(0f, top, rect.width, rect.height - top).ContractedBy(10f);
            InteractionCardUtility.DrawInteractionsLog(logRect, pawn, Find.PlayLog.AllEntries, 12);
            Widgets.EndGroup();
            return false;
        }
    }
}
