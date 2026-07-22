using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A "machine" android - emotionless (no emotion simulators, not awakened) and without the ideological
    // subroutine - has neither relationships nor an ideoligion. The original already hides the relations
    // list for an emotionless android, but that leaves the Social tab as a mostly empty card with gaps
    // where the relations and ideoligion sections used to be.
    //
    // For an android with nothing in either section, draw the interaction log across the whole card
    // instead. An android that carries the ideological subroutine keeps the normal card, since it has an
    // ideoligion and a role to show.
    [HarmonyPatch(typeof(SocialCardUtility), "DrawSocialCard")]
    public static class SocialCardUtility_DrawSocialCard_Patch
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (!LogOnly(pawn))
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

        private static bool LogOnly(Pawn pawn)
        {
            return pawn != null && pawn.IsAndroid() && pawn.Emotionless()
                && !IdeoCapability.CanHoldIdeoligion(pawn);
        }
    }
}
