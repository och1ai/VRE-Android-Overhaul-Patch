using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Declares which power-core hediff a power-source gene installs, and on which body part. A core with
    // no part (the battery) is a whole-body system whose charge no single organ can knock out; a core with
    // a part (the reactor) is a physical, destructible, replaceable implant.
    public class PowerCoreExtension : DefModExtension
    {
        public BodyPartDef part;
        public HediffDef coreHediff;
    }

    public static class PowerCoreUtil
    {
        public const string ExclusionTag = "AndroidPower";

        // The android's installed power core, battery or reactor.
        public static Hediff_AndroidReactor GetPowerCore(this Pawn pawn)
        {
            return pawn?.health?.hediffSet?.hediffs.OfType<Hediff_AndroidReactor>().FirstOrDefault();
        }

        public static bool CanRecharge(this Hediff_AndroidReactor core)
        {
            return core is Hediff_AndroidBattery;
        }

        private static PowerCoreExtension ActiveExtension(Pawn pawn, GeneDef geneOverride)
        {
            if (geneOverride != null)
            {
                return geneOverride.GetModExtension<PowerCoreExtension>();
            }
            if (pawn?.genes == null)
            {
                return null;
            }
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active)
                {
                    PowerCoreExtension ext = gene.def.GetModExtension<PowerCoreExtension>();
                    if (ext?.coreHediff != null)
                    {
                        return ext;
                    }
                }
            }
            return null;
        }

        // Reconciles the android's power core with its active power gene: strips any core that doesn't
        // match and installs the gene's core. Never overwrites a manual implant on the same part.
        // geneOverride is passed from a gene's PostAdd, where that gene may not be flagged Active yet.
        public static void SyncPowerCore(Pawn pawn, GeneDef geneOverride = null)
        {
            if (pawn?.health == null)
            {
                return;
            }
            PowerCoreExtension ext = ActiveExtension(pawn, geneOverride);
            if (ext?.coreHediff == null)
            {
                return;
            }
            List<Hediff> stale = pawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_AndroidReactor && h.def != ext.coreHediff).ToList();
            foreach (Hediff h in stale)
            {
                pawn.health.RemoveHediff(h);
            }
            if (ext.part == null)
            {
                if (!pawn.health.hediffSet.hediffs.Any(h => h.def == ext.coreHediff))
                {
                    pawn.health.AddHediff(ext.coreHediff);
                }
                return;
            }
            foreach (BodyPartRecord record in pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def == ext.part).ToList())
            {
                bool alreadyHasCore = pawn.health.hediffSet.hediffs.Any(h => h.Part == record && h.def == ext.coreHediff);
                bool hasManualAddedPart = pawn.health.hediffSet.hediffs
                    .Any(h => h.Part == record && h is Hediff_AddedPart && !(h is Hediff_AndroidReactor));
                if (!alreadyHasCore && !hasManualAddedPart)
                {
                    pawn.health.AddHediff(ext.coreHediff, record);
                }
            }
        }
    }

    // Power-source hardware gene (reactor powered / battery powered). Installing the core here rather than
    // from the body gene means it does not depend on gene application order, and it works for an android
    // reprogrammed after it was built.
    public class Gene_AndroidPower : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            // Pass our own def: this gene may not be flagged Active yet during PostAdd.
            PowerCoreUtil.SyncPowerCore(pawn, def);
        }
    }

    // Belt and braces for the core actually matching the gene. The original's body gene installs a reactor
    // as one of its body-part counterparts regardless of which power gene the android has, and gene
    // application order is not guaranteed, so a battery android can end up briefly carrying both. Re-syncing
    // on spawn settles it once the pawn is fully built, and covers androids from older saves.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_PowerCore_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance.IsAndroid())
            {
                PowerCoreUtil.SyncPowerCore(__instance);
            }
        }
    }

    // A rechargeable battery: the cheap power source. Drains in about three days of operation and is
    // topped up at a powered android stand. Subclasses the original's reactor hediff so that everything
    // already written against it - the power need, the shutdown at zero charge, the repair job - keeps
    // working; only the drain and recharge behaviour differ.
    public class Hediff_AndroidBattery : Hediff_AndroidReactor
    {
        public const int TickRate = 60;

        // A flat android trickle-charges its own cells at ~+1%/day, so it eventually comes back online
        // even if nobody helps - though hauling it to a stand is enormously faster.
        public const float SlowRechargePerDay = 0.01f;

        public const float LifespanDays = 3f;

        // A spent battery is not a spent reactor: no toxic wastepack, nothing to extract. Deliberately
        // does not call base, which is what would drop the waste.
        public override void PostRemoved()
        {
        }

        public override void TickInterval(int delta)
        {
            // Deliberately NOT calling base: the reactor's own drain curve is a two-year one.
            if (!pawn.IsHashIntervalTick(TickRate, delta))
            {
                return;
            }
            if (Severity >= 1f || MechOversightUtil.IsDormantForPower(pawn))
            {
                // Flat, or parked in a low-power work mode: trickle up instead of draining.
                Energy = Mathf.Min(1f, Energy + (SlowRechargePerDay / GenDate.TicksPerDay) * TickRate);
                return;
            }
            float drain = (1f / (GenDate.TicksPerDay * LifespanDays)) * PowerEfficiencyDrainMultiplier * TickRate;
            Energy = Mathf.Max(0f, Energy - drain);
        }
    }

    // The reactor, with one behaviour added: an android parked asleep in a low-power work mode by its
    // mechanitor stops spending charge entirely (it has nothing to plug into, so "recharge" for a reactor
    // just means idling).
    public class Hediff_AndroidReactorCore : Hediff_AndroidReactor
    {
        public override void TickInterval(int delta)
        {
            if (MechOversightUtil.IsDormantForPower(pawn))
            {
                return;
            }
            base.TickInterval(delta);
        }
    }

    // The original's power need reads the reactor hediff by def, so a battery would leave the bar empty
    // and frozen. This resolves whichever core is installed. Repointed onto the NeedDef by
    // Patches/PowerCores.xml.
    public class Need_AndroidPower : Need_ReactorPower
    {
        public Need_AndroidPower(Pawn pawn) : base(pawn)
        {
        }

        public override float CurLevel
        {
            get
            {
                Hediff_AndroidReactor core = pawn.GetPowerCore();
                return core != null ? core.Energy : base.CurLevel;
            }
            set
            {
                Hediff_AndroidReactor core = pawn.GetPowerCore();
                if (core == null)
                {
                    base.CurLevel = value;
                    return;
                }
                if (core.pawn == null)
                {
                    core.pawn = pawn;
                }
                core.Energy = value;
                curLevelInt = core.Energy;
            }
        }

        public override void SetInitialLevel()
        {
            Hediff_AndroidReactor core = pawn.GetPowerCore();
            if (core != null)
            {
                curLevelInt = core.Energy;
                return;
            }
            base.SetInitialLevel();
        }
    }
}
