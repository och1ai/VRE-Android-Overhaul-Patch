using RimWorld;
using VREAndroids;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VREAndroidsOverhaul
{
    [HotSwappable]
    public class WorkGiver_CreateAndroid : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VREA_DefOf.VREA_AndroidCreationStation);
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (pawn.CurJob?.def != VREA_DefOf.VREA_CreateAndroid && thing is Building_AndroidCreationStation station 
                && pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn)))
            {
                // Once the ingredients are delivered (the unfinished android exists), the printer
                // gestates on its own - a crafter is only needed to bring the materials.
                if (station.unfinishedAndroid != null)
                {
                    return null;
                }
                if (!station.ReadyForAssembling(pawn, out var failReason))
                    JobFailReason.Is(failReason);
                else
                {
                    var chosen = new List<ThingCount>();
                    var requiredIngredients = station.RequiredIngredients().ToList();
                    if (!AndroidCreationUtility.TryFindBestFixedIngredients(requiredIngredients, pawn, station, chosen))
                        JobFailReason.Is("VREA.MissingMaterials".Translate(string.Join(", ", requiredIngredients.Select(x => x.ToString()))));
                    else if (!AddPendingInput(station, pawn, chosen))
                        JobFailReason.Is("VREA.MissingMaterials".Translate(station.pendingInput.LabelCap));
                    else if (chosen.Any(x => !pawn.CanReserveAndReach(x.Thing, PathEndMode.ClosestTouch, MaxPathDanger(pawn))))
                        JobFailReason.Is("VREA.MissingMaterials".Translate(string.Join(", ", requiredIngredients.Select(x => x.ToString()))));
                    else
                    {
                        var job = JobMaker.MakeJob(VREA_DefOf.VREA_CreateAndroid);
                        job.targetA = station;
                        job.targetQueueB = new List<LocalTargetInfo>(chosen.Count);
                        job.countQueue = new List<int>(chosen.Count);
                        for (var i = 0; i < chosen.Count; i++)
                        {
                            job.targetQueueB.Add(chosen[i].Thing);
                            job.countQueue.Add(chosen[i].Count);
                        }
                        return job;
                    }
                }
            }
            return null;
        }

        // Resurrect/reprint also require the specific body/subcore to be hauled to the printer.
        private bool AddPendingInput(Building_AndroidCreationStation station, Pawn pawn, List<ThingCount> chosen)
        {
            if (station.pendingInput == null)
            {
                return true;
            }
            if (station.pendingInput.Destroyed
                || !pawn.CanReserveAndReach(station.pendingInput, PathEndMode.ClosestTouch, MaxPathDanger(pawn)))
            {
                return false;
            }
            chosen.Add(new ThingCount(station.pendingInput, 1));
            return true;
        }
    }

    // Sends a crafter to complete an assembly cycle once the assembler pauses at a cycle boundary. Only the
    // last cycle finishes on its own; each earlier cycle waits for a crafter here before the next begins.
    [HotSwappable]
    public class WorkGiver_CompleteAndroidCycle : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VREA_DefOf.VREA_AndroidCreationStation);
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Building_AndroidCreationStation station && station.awaitingCycleCompletion
                && (station.compPower == null || station.compPower.PowerOn)
                && !t.IsBurning()
                && pawn.CanReserveAndReach(t, PathEndMode.InteractionCell, MaxPathDanger(pawn), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return HasJobOnThing(pawn, t, forced) ? JobMaker.MakeJob(OverhaulDefOf.CompleteAndroidCycle, t) : null;
        }
    }
}
