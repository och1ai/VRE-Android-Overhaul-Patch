using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Charging a battery android: it walks to a powered android stand and draws from the grid, with the
    // mech charger's mote, cable pulse and sustainer for feedback. The stand's own tick still tops up
    // anything parked on it (see below), which is what covers an android too flat to run a job at all.
    public static class AndroidCharging
    {
        // Matches the vanilla mechanoid default: go recharge below 30%, top all the way back up.
        public const float ChargeThreshold = 0.3f;

        // A full charge from empty takes about half a day parked on a stand - much slower than plugging
        // in properly, since nothing is driving the transfer.
        public const float ChargePerDay = 2f;

        // The charge job for this android, or null if it cannot use one: no rechargeable core, or no
        // powered stand it can reach and reserve. Deliberately does not test the charge level - the two
        // callers disagree about that, one waiting for the threshold and one charging on command.
        public static Job ChargeJobFor(Pawn pawn)
        {
            if (OverhaulDefOf.ChargeAndroid == null || !pawn.GetPowerCore().CanRecharge())
            {
                return null;
            }
            Building_AndroidStand stand = FindStandFor(pawn);
            return stand == null ? null : JobMaker.MakeJob(OverhaulDefOf.ChargeAndroid, stand);
        }

        public static Building_AndroidStand FindStandFor(Pawn pawn)
        {
            // Its own stand first, then claim any unowned one - the same order the original uses when
            // sending an android to reformat its memory.
            foreach (Building_AndroidStand stand in Building_AndroidStand.stands)
            {
                if (Usable(stand, pawn) && stand.CompAssignableToPawn.AssignedPawns.Contains(pawn))
                {
                    return stand;
                }
            }
            foreach (Building_AndroidStand stand in Building_AndroidStand.stands)
            {
                if (Usable(stand, pawn) && !stand.CompAssignableToPawn.AssignedPawns.Any())
                {
                    stand.CompAssignableToPawn.TryAssignPawn(pawn);
                    return stand;
                }
            }
            return null;
        }

        private static bool Usable(Building_AndroidStand stand, Pawn pawn)
        {
            return stand.compPower != null && stand.compPower.PowerOn
                && stand.CannotUseNowReason(pawn) == null
                && pawn.CanReserveAndReach(stand, PathEndMode.OnCell, Danger.Deadly);
        }
    }

    // Sends an android with a low, rechargeable core to a powered stand. Androids on a reactor power
    // themselves and never use this.
    public class JobGiver_ChargeAndroid : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            if (!pawn.GetPowerCore().CanRecharge())
            {
                return 0f;
            }
            Need need = pawn.needs?.TryGetNeed<Need_ReactorPower>();
            if (need == null || need.CurLevelPercentage > AndroidCharging.ChargeThreshold)
            {
                return 0f;
            }
            // High priority: a low battery means imminent shutdown, so the android breaks off work to
            // charge. It still won't override a player's draft orders.
            return 950f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            Need need = pawn.needs?.TryGetNeed<Need_ReactorPower>();
            if (need == null || need.CurLevelPercentage > AndroidCharging.ChargeThreshold)
            {
                return null;
            }
            return AndroidCharging.ChargeJobFor(pawn);
        }
    }

    // The "recharge" work mode a mechanitor can put a mechlike android in. Unlike the job giver above this
    // is an order, so it holds no threshold: the android goes and tops up whatever its level. A reactor
    // android has nothing to plug into, and one with no stand available is better off powering down than
    // idling at full drain, so both fall through to the dormant behaviour.
    public class JobGiver_AndroidRecharge : JobGiver_AndroidDormant
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return AndroidCharging.ChargeJobFor(pawn) ?? base.TryGiveJob(pawn);
        }
    }

    // Walk to the stand, stand on it and draw from the grid until full. Modelled on the vanilla mech
    // charger, down to its motes and sounds.
    //
    // The fork's version also produces toxic waste while charging and refuses a stand that is full of it;
    // the stand's waste system is not ported (see PORTING.md §5), so those two lines are left out.
    public class JobDriver_ChargeAndroid : JobDriver
    {
        public Building_AndroidStand Stand => job.targetA.Thing as Building_AndroidStand;

        // What the stand draws from the grid while actively charging, in place of its idle standby draw.
        public const float ChargingPowerConsumption = 200f;

        // A fully drained battery recharges in about three in-game hours, so a low android only needs a
        // short pit stop before going back to work.
        public const int FullChargeTicks = 7500;

        private Mote moteCharging;
        private Mote moteCablePulse;
        private Sustainer sustainerCharging;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(() => Stand?.compPower == null || !Stand.compPower.PowerOn);
            this.FailOn(() => !pawn.GetPowerCore().CanRecharge());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            Toil toil = ToilMaker.MakeToil();
            toil.initAction = delegate
            {
                toil.actor.pather.StopDead();
                SoundDefOf.MechChargerStart.PlayOneShot(Stand);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            toil.tickIntervalAction = delegate(int delta)
            {
                toil.actor.Rotation = Rot4.South;
                Hediff_AndroidReactor core = pawn.GetPowerCore();
                if (core == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                core.Energy = Mathf.Min(1f, core.Energy + (1f / FullChargeTicks) * delta);
                if (core.Energy >= 1f)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            toil.tickAction = delegate
            {
                if (moteCharging == null || moteCharging.Destroyed)
                {
                    moteCharging = MoteMaker.MakeAttachedOverlay(pawn, ThingDefOf.Mote_MechCharging, Vector3.zero);
                }
                moteCharging?.Maintain();
                if (moteCablePulse == null || moteCablePulse.Destroyed)
                {
                    moteCablePulse = MoteMaker.MakeInteractionOverlay(ThingDefOf.Mote_ChargingCablesPulse, Stand,
                        new TargetInfo(pawn.Position, Map));
                }
                moteCablePulse?.Maintain();
                if (sustainerCharging == null || sustainerCharging.Ended)
                {
                    sustainerCharging = SoundDefOf.MechChargerCharging.TrySpawnSustainer(
                        SoundInfo.InMap(Stand, MaintenanceType.PerTick));
                }
                sustainerCharging.Maintain();
                if (Stand?.compPower != null)
                {
                    Stand.compPower.PowerOutput = -ChargingPowerConsumption;
                }
            };
            AddFinishAction(delegate
            {
                if (Stand?.compPower != null)
                {
                    // The fork restores basePowerConsumption, which is private in the real assembly; the
                    // public PowerConsumption is the same figure with the def's modifiers applied.
                    Stand.compPower.PowerOutput = -Stand.compPower.Props.PowerConsumption;
                }
                if (sustainerCharging != null)
                {
                    sustainerCharging.End();
                    sustainerCharging = null;
                }
            });
            yield return toil;
        }
    }

    // A slow trickle for anything simply parked on a powered stand rather than plugged in by the charge
    // job - which is the only way an android that is already too flat to run a job ever comes back up,
    // once a colonist has hauled it there.
    [HarmonyPatch(typeof(Building_AndroidStand), nameof(Building_AndroidStand.Tick))]
    public static class Building_AndroidStand_Tick_Patch
    {
        private const int ChargeInterval = 60;

        public static void Postfix(Building_AndroidStand __instance)
        {
            if (!__instance.IsHashIntervalTick(ChargeInterval) || __instance.compPower == null
                || !__instance.compPower.PowerOn)
            {
                return;
            }
            Pawn occupant = __instance.CurOccupant;
            Hediff_AndroidReactor core = occupant?.GetPowerCore();
            if (core == null || !core.CanRecharge() || core.Energy >= 1f)
            {
                return;
            }
            // CurOccupant is any android standing still on the cell, so it also matches the one running
            // the charge job. That job does its own charging, mote and grid draw; two of them at once
            // would double the rate and fight over the stand's power output.
            if (occupant.CurJobDef == OverhaulDefOf.ChargeAndroid)
            {
                return;
            }
            core.Energy = Mathf.Min(1f, core.Energy
                + (AndroidCharging.ChargePerDay / GenDate.TicksPerDay) * ChargeInterval);
            // Draw from the grid while actually charging, on top of the stand's idle draw.
            __instance.compPower.PowerOutput = -__instance.compPower.Props.PowerConsumption * 2f;
        }
    }

    // Mechanoid-style energy readout in the inspect pane: "Power: 62% (-33% / day)".
    [HarmonyPatch(typeof(Pawn), "GetInspectString")]
    public static class Pawn_GetInspectString_Power_Patch
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!__instance.IsAndroid())
            {
                return;
            }
            Hediff_AndroidReactor core = __instance.GetPowerCore();
            Need need = __instance.needs?.TryGetNeed<Need_ReactorPower>();
            if (core == null || need == null)
            {
                return;
            }
            string line;
            if (core.CanRecharge() && core.Severity >= 1f)
            {
                // Flat and trickle-charging on its own - the android equivalent of a mech's "Dormant
                // self-charging" line.
                line = "VREAOverhaul.AndroidEnergy".Translate() + ": " + need.CurLevelPercentage.ToStringPercent()
                    + " (+" + "PerDay".Translate(Hediff_AndroidBattery.SlowRechargePerDay.ToStringPercent()) + ")"
                    + "\n" + "VREAOverhaul.AndroidDormantCharging".Translate();
            }
            else
            {
                float perDay = core.PowerEfficiencyDrainMultiplier
                    / (core.CanRecharge() ? Hediff_AndroidBattery.LifespanDays : GenDate.DaysPerYear * 2f);
                line = "VREAOverhaul.AndroidEnergy".Translate() + ": " + need.CurLevelPercentage.ToStringPercent()
                    + " (-" + "PerDay".Translate(perDay.ToStringPercent()) + ")";
            }
            __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
        }
    }
}
