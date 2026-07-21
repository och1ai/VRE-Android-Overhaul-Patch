using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Charging a battery android. Rather than adding a bespoke charge job and building, this reuses what
    // the original already has: the android stand is a Building_Bed, so the android simply lies down on a
    // powered one, and the stand tops it up while it is docked. That also means a flat android hauled to a
    // stand by a colonist charges without being able to run any job itself.
    public static class AndroidCharging
    {
        // Matches the vanilla mechanoid default: go recharge below 30%, top all the way back up.
        public const float ChargeThreshold = 0.3f;

        // A full charge from empty takes about half a day at a stand.
        public const float ChargePerDay = 2f;

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
            if (!pawn.GetPowerCore().CanRecharge())
            {
                return null;
            }
            Need need = pawn.needs?.TryGetNeed<Need_ReactorPower>();
            if (need == null || need.CurLevelPercentage > AndroidCharging.ChargeThreshold)
            {
                return null;
            }
            Building_AndroidStand stand = AndroidCharging.FindStandFor(pawn);
            if (stand == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, stand);
            job.forceSleep = true;
            return job;
        }
    }

    // The stand does the actual charging: any android docked on a powered stand with a rechargeable core
    // gains charge, whether it walked there itself or was hauled in flat.
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
