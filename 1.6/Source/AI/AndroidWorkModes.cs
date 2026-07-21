using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Vanilla's ThinkNode_ConditionalWorkMode requires RaceProps.IsMechanoid, so it can never match a
    // humanlike android. This is the same test for an overseen "mechlike" android: it matches when a
    // mechanitor has taken oversight and set the android's control group to this work mode.
    public class ThinkNode_ConditionalAndroidWorkMode : ThinkNode_Conditional
    {
        public MechWorkModeDef workMode;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalAndroidWorkMode copy = (ThinkNode_ConditionalAndroidWorkMode)base.DeepCopy(resolve);
            copy.workMode = workMode;
            return copy;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            if (!MechOversightUtil.IsOversightAndroid(pawn) || pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }
            // GetMechWorkMode null-chains through the overseer and control group, so an unassigned or
            // unclaimed android simply matches nothing and falls through to its normal behaviour.
            return pawn.GetOverseer() != null && pawn.GetMechWorkMode() == workMode;
        }
    }

    // The low-power work modes ("dormant self-charge" and, for a reactor android, "recharge"). The android
    // powers down where it stands; one that runs a real sleep cycle beds down like an organic instead.
    public class JobGiver_AndroidDormant : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return BedJobIfSleeper(pawn) ?? SleepInPlaceJob(pawn);
        }

        private static Job SleepInPlaceJob(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
            job.forceSleep = true;
            return job;
        }

        private static Job BedJobIfSleeper(Pawn pawn)
        {
            GeneDef sleepCycle = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_SleepNeed");
            if (sleepCycle == null || !pawn.HasActiveGene(sleepCycle))
            {
                return null;
            }
            Building_Bed bed = RestUtility.FindBedFor(pawn);
            if (bed == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            job.forceSleep = true;
            return job;
        }
    }
}
