using VREAndroids;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VREAndroidsOverhaul
{
    public class JobDriver_ExtractSubcore : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            Toil extract = Toils_General.Wait(300);
            extract.WithProgressBarToilDelay(TargetIndex.A);
            extract.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            extract.FailOnCannotTouch(TargetIndex.A, PathEndMode.ClosestTouch);
            extract.activeSkill = () => SkillDefOf.Crafting;
            yield return extract;
            yield return new Toil
            {
                initAction = delegate
                {
                    if (TargetThingA is Corpse corpse && AndroidDeath.HasSubcore(corpse.InnerPawn, out Hediff_AndroidSubcore hediff))
                    {
                        hediff.SpawnSubcore(ThingPlaceMode.Near);
                        Map.designationManager.TryRemoveDesignationOn(corpse, SubcoreDefOf.ExtractSubcoreDesignation);
                        if (pawn.skills != null)
                        {
                            pawn.skills.Learn(SkillDefOf.Crafting, 35f);
                        }
                    }
                }
            };
        }
    }
}
