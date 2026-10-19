using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The Mechs tab is written around mechanoids, and an android is not one, so each column that assumes a
    // mech need or comp has to be told what the android equivalent is.

    // Energy reads pawn.needs.energy, which an android lacks. The resulting NullReferenceException aborts the
    // whole table, so one android in the list blanked every column of every row. Androids get their power
    // need drawn in the same bar, as a value out of 100 like the mech energy figure.
    [HarmonyPatch(typeof(PawnColumnWorker_Energy), nameof(PawnColumnWorker_Energy.DoCell))]
    public static class PawnColumnWorker_Energy_DoCell_Patch
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (pawn.needs?.energy != null)
            {
                return true;
            }
            Need need = pawn.needs?.AllNeeds.FirstOrDefault(n => n is Need_ReactorPower);
            if (need != null)
            {
                Widgets.FillableBar(rect.ContractedBy(4f), need.CurLevelPercentage,
                    PawnColumnWorker_Energy.EnergyBarTex, BaseContent.ClearTex, doBorder: false);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, Mathf.RoundToInt(need.CurLevelPercentage * 100f) + " / 100");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            return false;
        }
    }

    // The auto repair checkbox is bound to the mech repair comp. Androids keep the same switch on their
    // synthetic body gene, the one the health tab and the repair job already read.
    [HarmonyPatch(typeof(PawnColumnWorker_AutoRepair), nameof(PawnColumnWorker_AutoRepair.DoCell))]
    public static class PawnColumnWorker_AutoRepair_DoCell_Patch
    {
        public static bool Prefix(PawnColumnWorker_AutoRepair __instance, Rect rect, Pawn pawn)
        {
            if (!MechOversightUtil.IsOversightAndroid(pawn))
            {
                return true;
            }
            Gene_SyntheticBody gene = pawn.genes?.GetFirstGeneOfType<Gene_SyntheticBody>();
            if (gene != null)
            {
                rect.xMin += (rect.width - 24f) / 2f;
                rect.yMin += (rect.height - 24f) / 2f;
                Widgets.Checkbox(rect.position, ref gene.autoRepair, 24f, disabled: false, __instance.def.paintable);
            }
            return false;
        }
    }

    // A name column entry for something that is not humanlike or an animal is "Name, kind", which for an
    // android reads "Zakhar, Colonist". The android's own title, the one its bio shows, is "colony android".
    [HarmonyPatch(typeof(PawnColumnWorker_Label), "GetLabel")]
    public static class PawnColumnWorker_Label_GetLabel_Patch
    {
        public static void Postfix(Pawn pawn, ref TaggedString __result)
        {
            if (MechOversightUtil.IsOversightAndroid(pawn) && pawn.Name != null && !pawn.Name.Numerical)
            {
                string title = pawn.story?.TitleCap;
                __result = title.NullOrEmpty()
                    ? pawn.Name.ToStringShort.CapitalizeFirst()
                    : pawn.Name.ToStringShort.CapitalizeFirst() + ", " + title.Colorize(ColoredText.SubtleGrayColor);
            }
        }
    }
}

namespace VREAndroidsOverhaul
{
    // The colonist bar along the top lists colonists. An android under a mechanitor's oversight is managed
    // as one of its mechs instead, so it is left off the bar. The bar sorts its pawn list just before adding
    // the entries, so the android is dropped from the list there, and only while the bar is rebuilding it.
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class ColonistBar_CheckRecacheEntries_HideMechanized_Patch
    {
        [System.ThreadStatic] public static bool Rebuilding;

        public static void Prefix()
        {
            Rebuilding = true;
        }

        public static void Finalizer()
        {
            Rebuilding = false;
        }
    }

    [HarmonyPatch(typeof(PlayerPawnsDisplayOrderUtility), nameof(PlayerPawnsDisplayOrderUtility.Sort))]
    public static class PlayerPawnsDisplayOrderUtility_Sort_HideMechanized_Patch
    {
        public static void Prefix(List<Pawn> pawns)
        {
            if (ColonistBar_CheckRecacheEntries_HideMechanized_Patch.Rebuilding)
            {
                pawns.RemoveAll(MechOversightUtil.IsOversightAndroid);
            }
        }
    }
}
