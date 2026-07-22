using RimWorld;
using VREAndroids;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VREAndroidsOverhaul
{

    public class JobDriver_CreateAndroid : JobDriver
    {
        public Building_AndroidCreationStation Station => TargetA.Thing as Building_AndroidCreationStation;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            if (job.targetQueueB != null)
            {
                pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
                foreach (var target in job.GetTargetQueue(TargetIndex.B))
                {
                    pawn.Map.physicalInteractionReservationManager.Reserve(pawn, job, target.Thing);
                }
            }
            if (Station.unfinishedAndroid != null && !pawn.Reserve(Station.unfinishedAndroid, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                Thing thing = GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                return thing.Spawned && thing is Building_AndroidCreationStation station && station.ReadyForAssembling(pawn, out _) 
                ? JobCondition.Ongoing : JobCondition.Incompletable;
            });
            this.FailOn(() => Station.unfinishedAndroid != null && Station.unfinishedAndroid.Spawned is false 
            && pawn.carryTracker.CarriedThing != Station.unfinishedAndroid);
            this.FailOnBurningImmobile(TargetIndex.A);
            Toil gotoStation = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            if (job.targetQueueB != null)
            {
                yield return Toils_Jump.JumpIf(gotoStation, () => job.GetTargetQueue(TargetIndex.B).NullOrEmpty());
                foreach (Toil item in CollectIngredientsToils(TargetIndex.B, TargetIndex.A, TargetIndex.C))
                {
                    yield return item;
                }
            }
            else if (Station.unfinishedAndroid.Position != Station.Position)
            {
                job.SetTarget(TargetIndex.C, Station.unfinishedAndroid);
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
                job.count = 1;
                yield return Toils_Haul.StartCarryThing(TargetIndex.C);
                yield return Toils_Goto.GotoCell(Station.Position, PathEndMode.OnCell);
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.A, null, storageMode: false);
            }
            yield return gotoStation;
            var toil = ToilMaker.MakeToil();
            toil.initAction = () =>
            {
                if (job.targetQueueB != null && job.placedThings != null)
                {
                    Station.DeliverAndStart(job.placedThings.Select(x => x.thing).ToList());
                    pawn.Map.physicalInteractionReservationManager.ReleaseClaimedBy(pawn, job);
                    job.placedThings = null;
                }
                job.SetTarget(TargetIndex.C, Station.unfinishedAndroid);
            };
            // The crafter's job ends once the materials are delivered and the unfinished android exists;
            // from here the printer gestates it automatically (see Building_AndroidCreationStation.TickInterval).
            yield return toil;
        }

        public IEnumerable<Toil> CollectIngredientsToils(TargetIndex ingredientInd, TargetIndex billGiverInd, TargetIndex ingredientPlaceCellInd, bool subtractNumTakenFromJobCount = false, bool failIfStackCountLessThanJobCount = true)
        {
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(ingredientInd);
            yield return extract;
            Toil getToHaulTarget = Toils_Goto.GotoThing(ingredientInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(ingredientInd).FailOnSomeonePhysicallyInteracting(ingredientInd);
            yield return getToHaulTarget;
            yield return Toils_Haul.StartCarryThing(ingredientInd, putRemainderInQueue: true, subtractNumTakenFromJobCount, failIfStackCountLessThanJobCount);
            yield return JobDriver_DoBill.JumpToCollectNextIntoHandsForBill(getToHaulTarget, TargetIndex.B);
            yield return Toils_Goto.GotoThing(billGiverInd, PathEndMode.OnCell).FailOnDestroyedOrNull(ingredientInd);
            Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(billGiverInd, ingredientInd, ingredientPlaceCellInd);
            yield return findPlaceTarget;
            yield return PlaceHauledThingInCell(ingredientPlaceCellInd, findPlaceTarget, storageMode: false);
            yield return Toils_Jump.JumpIfHaveTargetInQueue(ingredientInd, extract);
        }

        public static Toil PlaceHauledThingInCell(TargetIndex cellInd, Toil nextToilOnPlaceFailOrIncomplete, bool storageMode, bool tryStoreInSameStorageIfSpotCantHoldWholeStack = false)
        {
            Toil toil = ToilMaker.MakeToil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                IntVec3 cell = curJob.GetTarget(cellInd).Cell;
                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error(string.Concat(actor, " tried to place hauled thing in cell but is not hauling anything."));
                }
                else
                {
                    SlotGroup slotGroup = actor.Map.haulDestinationManager.SlotGroupAt(cell);
                    if (slotGroup != null && slotGroup.Settings.AllowedToAccept(actor.carryTracker.CarriedThing))
                    {
                        actor.Map.designationManager.TryRemoveDesignationOn(actor.carryTracker.CarriedThing, DesignationDefOf.Haul);
                    }
                    Action<Thing, int> placedAction = delegate (Thing th, int added)
                    {
                        if (curJob.placedThings == null)
                        {
                            curJob.placedThings = new List<ThingCountClass>();
                        }
                        ThingCountClass thingCountClass = curJob.placedThings.Find((ThingCountClass x) => x.thing == th);
                        if (thingCountClass != null)
                        {
                            thingCountClass.Count += added;
                        }
                        else
                        {
                            curJob.placedThings.Add(new ThingCountClass(th, added));
                        }
                    };

                    if (!actor.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _, placedAction))
                    {
                        if (storageMode)
                        {
                            if (nextToilOnPlaceFailOrIncomplete != null && ((tryStoreInSameStorageIfSpotCantHoldWholeStack && StoreUtility.TryFindBestBetterStoreCellForIn(actor.carryTracker.CarriedThing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, cell.GetSlotGroup(actor.Map), out var foundCell)) || StoreUtility.TryFindBestBetterStoreCellFor(actor.carryTracker.CarriedThing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, out foundCell)))
                            {
                                if (actor.CanReserve(foundCell))
                                {
                                    actor.Reserve(foundCell, actor.CurJob);
                                }
                                actor.CurJob.SetTarget(cellInd, foundCell);
                                actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                            }
                            else
                            {
                                Job job = HaulAIUtility.HaulAsideJobFor(actor, actor.carryTracker.CarriedThing);
                                if (job != null)
                                {
                                    curJob.targetA = job.targetA;
                                    curJob.targetB = job.targetB;
                                    curJob.targetC = job.targetC;
                                    curJob.count = job.count;
                                    curJob.haulOpportunisticDuplicates = job.haulOpportunisticDuplicates;
                                    curJob.haulMode = job.haulMode;
                                    actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                                }
                                else
                                {
                                    Log.Error(string.Concat("Incomplete haul for ", actor, ": Could not find anywhere to put ", actor.carryTracker.CarriedThing, " near ", actor.Position, ". Destroying. This should never happen!"));
                                    actor.carryTracker.CarriedThing.Destroy();
                                }
                            }
                        }
                        else if (nextToilOnPlaceFailOrIncomplete != null)
                        {
                            actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                        }
                    }
                }
            };
            return toil;
        }
    }

    // A crafter walks to the paused assembler and works briefly to complete the current assembly cycle,
    // letting the next one begin.
    public class JobDriver_CompleteAndroidCycle : JobDriver
    {
        private const int WorkTicks = 600;
        private Building_AndroidCreationStation Station => TargetA.Thing as Building_AndroidCreationStation;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(() => Station == null || !Station.awaitingCycleCompletion);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil work = Toils_General.Wait(WorkTicks, TargetIndex.A);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return work;
            Toil finish = ToilMaker.MakeToil();
            finish.initAction = delegate { Station?.CompleteCycle(pawn); };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
