using VREAndroids;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VREAndroidsOverhaul
{
    public class WorkGiver_ExtractSubcore : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation item in pawn.Map.designationManager.SpawnedDesignationsOfDef(SubcoreDefOf.ExtractSubcoreDesignation))
            {
                if (item.target.HasThing)
                {
                    yield return item.target.Thing;
                }
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(SubcoreDefOf.ExtractSubcoreDesignation);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Map.designationManager.DesignationOn(t, SubcoreDefOf.ExtractSubcoreDesignation) == null)
            {
                return false;
            }
            if (t.IsForbidden(pawn))
            {
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            return t is Corpse corpse && AndroidDeath.HasSubcore(corpse.InnerPawn, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(SubcoreDefOf.ExtractSubcoreJob, t);
        }
    }
}
