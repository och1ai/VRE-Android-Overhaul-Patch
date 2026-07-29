using System.Collections.Generic;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The right-click "refuel with neutroamine" job. Identical to the original's except for the reservoir
    // size, which the original hardcodes at 100 inside a lambda inside an iterator - too fragile a place to
    // transpile, so the job def's driverClass is repointed here instead (Patches/Neutroamine.xml).
    //
    // This has to move together with the float-menu option that sets job.count: the option decides how much
    // neutroamine to carry from the same reservoir figure this driver divides by.
    public class JobDriver_RefuelWithNeutroamine : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_General.Wait(120).WithProgressBarToilDelay(TargetIndex.A);
            Toil toil = ToilMaker.MakeToil();
            toil.initAction = delegate
            {
                Hediff neutroloss = pawn.health.hediffSet.GetFirstHediffOfDef(VREA_DefOf.VREA_NeutroLoss);
                if (neutroloss != null)
                {
                    pawn.carryTracker.CarriedThing.Destroy();
                    neutroloss.Severity -= job.count / AndroidNeutroamine.PerFullReservoir;
                    if (neutroloss.Severity <= 0.01f)
                    {
                        neutroloss.Severity = 0;
                    }
                }
            };
            yield return toil;
        }
    }
}
