using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // The Mechs tab's Energy column reads pawn.needs.energy, the mech energy need, which an android does not
    // have. The resulting NullReferenceException aborts the whole table, so one android in the list blanked
    // every column of every row. Androids get their own power need drawn in the same bar instead.
    [HarmonyPatch(typeof(PawnColumnWorker_Energy), nameof(PawnColumnWorker_Energy.DoCell))]
    public static class PawnColumnWorker_Energy_DoCell_Patch
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (pawn.needs?.energy != null)
            {
                return true;
            }
            Need need = pawn.needs?.AllNeeds.FirstOrDefault(n => n is VREAndroids.Need_ReactorPower);
            if (need != null)
            {
                Widgets.FillableBar(rect.ContractedBy(4f), need.CurLevelPercentage,
                    PawnColumnWorker_Energy.EnergyBarTex, BaseContent.ClearTex, doBorder: false);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, need.CurLevelPercentage.ToStringPercent());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            return false;
        }
    }
}
