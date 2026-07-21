using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Androids are repaired like mechanoids: injuries, MISSING BODY PARTS and permanent damage (scars) are
    // all fixable, so nothing on an android is permanent. That is what retires the android parts economy -
    // a lost hand is rebuilt by the repair job instead of being crafted and installed.
    //
    // Repointed onto the original's job and work-giver defs by Patches/RepairRework.xml.
    public class JobDriver_RepairAndroid : JobDriver
    {
        protected int ticksToNextRepair;

        protected Pawn Patient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        protected int TicksPerHeal => 200;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Repair is open to every crafter, so a rare same-tick race for the same patient can slip past
            // the work giver; fail the reservation quietly instead of logging an error.
            return pawn.Reserve(Patient, job, 1, -1, null, errorOnFailed: false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Pure repair: no tending pass. Bleeding wounds are repaired away first, then injuries, missing
            // parts and scars. Speed scales with the repairer's crafting skill and work speed.
            Toil repairToil = Toils_General.Wait(int.MaxValue);
            repairToil.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A);
            repairToil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
            repairToil.AddPreInitAction(delegate
            {
                ticksToNextRepair = RepairInterval();
            });
            repairToil.handlingFacing = true;
            repairToil.tickIntervalAction = delegate (int delta)
            {
                ticksToNextRepair -= delta;
                if (ticksToNextRepair <= 0)
                {
                    RepairTick(Patient);
                    ticksToNextRepair = RepairInterval();
                }
                pawn.rotationTracker.FaceTarget(Patient);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.05f * delta);
                }
            };

            if (pawn != Patient)
            {
                repairToil.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            }
            repairToil.AddEndCondition(() => CanRepairAndroid(Patient) ? JobCondition.Ongoing : JobCondition.Succeeded);
            repairToil.activeSkill = () => SkillDefOf.Crafting;
            if (pawn != Patient)
            {
                yield return gotoToil;
            }
            yield return repairToil;
            AddFinishAction(delegate
            {
                if (Patient != null && Patient != pawn && Patient.CurJob != null
                    && (Patient.CurJob.def == JobDefOf.Wait || Patient.CurJob.def == JobDefOf.Wait_MaintainPosture))
                {
                    Patient.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });
        }

        // Ticks between each repair point, faster with higher crafting skill and work speed. Self-repair is
        // slower (0.7x), matching the self-repair tooltip.
        private int RepairInterval()
        {
            float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
            SkillRecord crafting = pawn.skills?.GetSkill(SkillDefOf.Crafting);
            float skillFactor = crafting != null ? Mathf.Lerp(0.5f, 2f, Mathf.Clamp01(crafting.Level / 20f)) : 1f;
            float factor = Mathf.Max(0.1f, speed * skillFactor);
            if (pawn == Patient)
            {
                factor *= 0.7f;
            }
            return Mathf.Max(1, Mathf.RoundToInt(TicksPerHeal / factor));
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            if (dinfo.Def.ExternalViolenceFor(pawn) && pawn.Faction != Faction.OfPlayer && pawn == Patient)
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

        public static bool CanRepairAndroid(Pawn android)
        {
            if (android.InMentalState || android.IsBurning() || android.IsAttacking())
            {
                return false;
            }
            return GetHediffToHeal(android) != null
                || GetMissingPartToRestore(android) != null
                || GetPermanentHediffToRemove(android) != null;
        }

        // Bleeding wounds are repaired first (so bleeding stops fastest), then the smallest injury.
        public static Hediff GetHediffToHeal(Pawn android)
        {
            Hediff bleeding = null;
            float maxBleed = 0f;
            Hediff smallest = null;
            float minSeverity = float.PositiveInfinity;
            foreach (Hediff hediff in android.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && !injury.IsPermanent())
                {
                    float bleed = injury.BleedRate;
                    if (bleed > maxBleed)
                    {
                        maxBleed = bleed;
                        bleeding = injury;
                    }
                    if (injury.Severity < minSeverity)
                    {
                        minSeverity = injury.Severity;
                        smallest = injury;
                    }
                }
            }
            return bleeding ?? smallest;
        }

        // The closest-to-core missing part that can be regrown (its parent still exists).
        public static Hediff_MissingPart GetMissingPartToRestore(Pawn android)
        {
            HediffSet hediffSet = android.health.hediffSet;
            foreach (Hediff hediff in hediffSet.hediffs)
            {
                if (hediff is Hediff_MissingPart missingPart && !missingPart.def.keepOnBodyPartRestoration
                    && missingPart.Part != null)
                {
                    BodyPartRecord parent = missingPart.Part.parent;
                    if (parent == null || hediffSet.GetNotMissingParts().Contains(parent))
                    {
                        return missingPart;
                    }
                }
            }
            return null;
        }

        // Permanent injuries (scars), so an android carries no permanent damage once repaired.
        public static Hediff GetPermanentHediffToRemove(Pawn android)
        {
            foreach (Hediff hediff in android.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && injury.IsPermanent())
                {
                    return injury;
                }
            }
            return null;
        }

        // Charge spent per repair step by a battery android. Reactor androids self-power repairs.
        public const float RepairEnergyCost = 0.01f;

        public static void RepairTick(Pawn android)
        {
            Hediff_AndroidReactor core = android.GetPowerCore();
            if (core != null && core.CanRecharge())
            {
                core.Energy -= RepairEnergyCost;
            }
            Hediff hediffToHeal = GetHediffToHeal(android);
            if (hediffToHeal != null)
            {
                // Each wound is fully repaired in a single pass (no progressive shrinking and no tending),
                // so its bleeding stops at once. Parts are handled afterwards.
                hediffToHeal.Heal(hediffToHeal.Severity + 1f);
                return;
            }
            Hediff_MissingPart missingPart = GetMissingPartToRestore(android);
            if (missingPart != null)
            {
                RestoreMissingPart(android, missingPart.Part);
                return;
            }
            Hediff permanent = GetPermanentHediffToRemove(android);
            if (permanent != null)
            {
                android.health.RemoveHediff(permanent);
            }
        }

        // Regrows a missing part and re-synthesizes the android counterparts across the whole restored
        // subtree, leaving any manually installed implant in place rather than overwriting it. RestorePart
        // only clears the top part for androids (the original's RestorePartRecursiveInt patch is
        // non-recursive), so the children are handled here.
        public static void RestoreMissingPart(Pawn android, BodyPartRecord part)
        {
            android.health.RestorePart(part);
            ReSynthesizeSubtree(android, part);
            android.health.hediffSet.DirtyCache();
        }

        private static void ReSynthesizeSubtree(Pawn android, BodyPartRecord part)
        {
            List<Hediff> hediffs = android.health.hediffSet.hediffs;
            bool hasAddedPart = false;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part != part)
                {
                    continue;
                }
                if (hediff is Hediff_MissingPart && !hediff.def.keepOnBodyPartRestoration)
                {
                    hediffs.RemoveAt(i);
                    hediff.PostRemoved();
                }
                else if (hediff is Hediff_AddedPart)
                {
                    hasAddedPart = true;
                }
            }
            if (!hasAddedPart)
            {
                HediffDef counterpart = CounterpartFor(part.def, android);
                // The reactor is the one component repair never regenerates: a removed or spent reactor
                // must be replaced with a crafted one via surgery. Otherwise repairing a reactor-less
                // android would hand out a free, fully-charged reactor. Everything else regenerates.
                if (counterpart != null && counterpart != AndroidRepairUtil.ReactorDef)
                {
                    android.health.AddHediff(counterpart, part);
                }
            }
            for (int i = 0; i < part.parts.Count; i++)
            {
                ReSynthesizeSubtree(android, part.parts[i]);
            }
        }

        private static HediffDef CounterpartFor(BodyPartDef part, Pawn android)
        {
            // The power core depends on which power gene the android carries, not on a fixed counterpart
            // table, so it is resolved from the gene's extension.
            HediffDef fromPowerGene = AndroidRepairUtil.PowerCoreFor(part, android);
            return fromPowerGene ?? part.GetAndroidCounterPart();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair", 0);
        }
    }

    internal static class AndroidRepairUtil
    {
        private static HediffDef reactor;
        private static JobDef repairJob;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            reactor = DefDatabase<HediffDef>.GetNamedSilentFail("VREA_Reactor");
            repairJob = DefDatabase<JobDef>.GetNamedSilentFail("VREA_RepairAndroid");
        }

        public static HediffDef ReactorDef
        {
            get
            {
                Resolve();
                return reactor;
            }
        }

        public static JobDef RepairJob
        {
            get
            {
                Resolve();
                return repairJob;
            }
        }

        // The core hediff this android's power gene installs, if this body part is where it lives.
        public static HediffDef PowerCoreFor(BodyPartDef part, Pawn android)
        {
            if (android?.genes == null)
            {
                return null;
            }
            foreach (Gene gene in android.genes.GenesListForReading)
            {
                if (!gene.Active)
                {
                    continue;
                }
                PowerCoreExtension ext = gene.def.GetModExtension<PowerCoreExtension>();
                if (ext?.part != null && ext.part == part)
                {
                    return ext.coreHediff;
                }
            }
            return null;
        }
    }

    public class WorkGiver_RepairAndroid : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn patient = t as Pawn;
            if (patient == null || !patient.IsAndroid(out Gene_SyntheticBody gene))
            {
                return false;
            }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Crafting) || !gene.autoRepair)
            {
                return false;
            }
            if (!GoodLayingStatusForRepair(patient, pawn, forced))
            {
                return false;
            }
            if (pawn != patient)
            {
                // Don't repair an android that is busy repairing itself.
                if (patient.CurJobDef == AndroidRepairUtil.RepairJob)
                {
                    return false;
                }
                if (patient.HostileTo(pawn) || t.IsForbidden(pawn))
                {
                    return false;
                }
                // Only one crafter repairs a given android at a time. This avoids several free crafters
                // being handed the same patient and then failing to reserve it.
                List<Pawn> factionPawns = patient.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
                for (int i = 0; i < factionPawns.Count; i++)
                {
                    Pawn other = factionPawns[i];
                    if (other != pawn && other.CurJobDef == AndroidRepairUtil.RepairJob
                        && other.CurJob.targetA.Thing == patient)
                    {
                        return false;
                    }
                }
                if (!pawn.CanReserve(t, 1, -1, null, forced)
                    || !pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Deadly))
                {
                    return false;
                }
            }
            return JobDriver_RepairAndroid.CanRepairAndroid(patient);
        }

        public static bool GoodLayingStatusForRepair(Pawn patient, Pawn doctor, bool forced)
        {
            if (patient == doctor)
            {
                // Self-repair only requires self-repair (self-tend) to be enabled.
                return patient.playerSettings != null && patient.playerSettings.selfTend;
            }
            // Auto-repair targets androids that are downed or resting in a bed/stand. Repairing an android
            // that is up and about is done on demand via the right-click order or by prioritizing the work
            // (both pass forced = true), which avoids idle androids being swarmed by every free crafter.
            return forced || patient.Downed || patient.InBed();
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(AndroidRepairUtil.RepairJob, t);
        }
    }
}
