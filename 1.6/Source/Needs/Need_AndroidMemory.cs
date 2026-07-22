using RimWorld;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Working memory is optional hardware now, so the memory need is only live when the android actually
    // has something to maintain: the memory-recharging hardware (permanently), or the
    // component-overheating hardware while it is overheating - heat scrambles the drive, so the need
    // appears only for as long as it is hot.
    //
    // Repointed onto the original's NeedDef by Patches/MemoryRework.xml.
    public class Need_AndroidMemory : Need_MemorySpace
    {
        // Recovery while there is no active memory system: the drive simply sits topped up.
        private const float IdleRecoveryPerDay = 300f;

        // Heat scrambling costs twice the normal drain on top of the base drain, i.e. 3x in total.
        private const float OverheatingExtraDrainPerDay = 300f;

        public Need_AndroidMemory(Pawn pawn) : base(pawn)
        {
        }

        public bool Overheating
        {
            get
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("VREA_Overheating");
                return def != null && (pawn.health?.hediffSet?.HasHediff(def) ?? false);
            }
        }

        public bool HasMemoryHardware => HasGene("VREA_MemoryProcessing");

        public bool HeatScrambling => Overheating && HasGene("VREA_ComponentOverheating");

        public bool MemoryActive => HasMemoryHardware || HeatScrambling;

        private bool HasGene(string defName)
        {
            GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            return def != null && pawn.HasActiveGene(def);
        }

        public override void NeedInterval()
        {
            if (!MemoryActive)
            {
                // No active memory system (e.g. an overheating-hardware android that has cooled back down):
                // memory tops itself back up and never forces a reformat.
                curLevelInt = Mathf.Min(1f, curLevelInt + (1f / GenDate.TicksPerDay) * IdleRecoveryPerDay);
                return;
            }
            if (Overheating)
            {
                StatDef drainStat = DefDatabase<StatDef>.GetNamedSilentFail("VREA_MemorySpaceDrainMultiplier");
                float multiplier = drainStat != null ? pawn.GetStatValue(drainStat) : 1f;
                curLevelInt = Mathf.Max(0f,
                    curLevelInt - (1f / GenDate.TicksPerDay) * OverheatingExtraDrainPerDay * multiplier);
            }
            // The base drain, and with it the reformatting reboot when the drive fills up.
            base.NeedInterval();
        }
    }
}
