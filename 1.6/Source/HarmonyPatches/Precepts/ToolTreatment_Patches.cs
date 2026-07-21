using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The original applies four consequences to an android its ideoligion regards as a mere tool: nobody
    // forms opinions about it, nobody grieves it, enslaving it is a formality rather than an event, and it
    // does not count towards the colony's slave headcount.
    //
    // Those four are hard-wired to the original's "androids: tools" precept. The overlay adds precepts that
    // treat only NON-awakened androids as tools ("respected (awakened)", "equal (awakened)"), so each
    // consequence needs the same treatment for that case. Every patch here fires only for the overlay's own
    // precepts - where the original's precept applies, the original's patch is already doing the work.

    // No opinions about a tool.
    [HarmonyPatch(typeof(ThoughtHandler), "GetSocialThoughts", new Type[] { typeof(Pawn), typeof(List<ISocialThought>) })]
    public static class ThoughtHandler_GetSocialThoughts_Overlay_Patch
    {
        public static bool Prefix(Pawn otherPawn, ThoughtHandler __instance)
        {
            if (__instance.pawn.IsAndroid())
            {
                return true;
            }
            return !ToolTreatment.TreatedAsToolByOverlayPreceptOnly(__instance.pawn.Ideo, otherPawn);
        }
    }

    // No grief, guilt or witness thoughts when a tool is destroyed.
    [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), "AppendThoughts_ForHumanlike")]
    public static class PawnDiedOrDownedThoughtsUtility_AppendThoughts_ForHumanlike_Overlay_Patch
    {
        public static bool Prefix(Pawn victim)
        {
            return !ToolTreatment.TreatedAsToolByColonyOverlayPreceptOnly(victim);
        }
    }

    // Enslaving a tool is a formality: it happens, but it is not the moral event vanilla treats it as (no
    // recruitment interaction, no ideoligion fallout).
    [HarmonyPatch(typeof(GenGuest), "EnslavePrisoner")]
    public static class GenGuest_EnslavePrisoner_Overlay_Patch
    {
        public static bool Prefix(Pawn warden, Pawn prisoner)
        {
            if (!ToolTreatment.TreatedAsToolByOverlayPreceptOnly(warden.Ideo, prisoner))
            {
                return true;
            }
            if (!prisoner.IsSlave)
            {
                prisoner.guest.SetGuestStatus(warden.Faction, GuestStatus.Slave);
                Messages.Message("MessagePrisonerEnslaved".Translate(prisoner, warden),
                    new LookTargets(prisoner, warden), MessageTypeDefOf.NeutralEvent);
                prisoner.apparel.UnlockAll();
            }
            return false;
        }
    }

    // A tool is not a slave for the purposes of the colony's slave count (and therefore slave rebellions).
    [HarmonyPatch(typeof(FactionUtility), "GetSlavesInFactionCount")]
    [HarmonyPriority(Priority.Last)]
    public static class FactionUtility_GetSlavesInFactionCount_Overlay_Patch
    {
        public static void Postfix(Faction faction, ref int __result)
        {
            Ideo ideo = Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo;
            if (ideo == null)
            {
                return;
            }
            int count = __result;
            foreach (Pawn pawn in PawnsFinder.AllMaps_SpawnedPawnsInFaction(faction))
            {
                if (pawn.IsSlave && ToolTreatment.TreatedAsToolByOverlayPreceptOnly(ideo, pawn))
                {
                    count--;
                }
            }
            __result = count;
        }
    }
}
