using VREAndroids;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // Surgery on a living android that pulls its subcore. The extraction tears the head open and
    // destroys the android, but drops the subcore (carrying the full persona) as an item and leaves an
    // empty chassis that can be fitted with new hardware or reprinted/resurrected later.
    public class Recipe_ExtractAndroidSubcore : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn && pawn.IsAndroid() && pawn.HasSubcore(out _);
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            BodyPartRecord head = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def == BodyPartDefOf.Head);
            if (head != null)
            {
                yield return head;
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!pawn.HasSubcore(out Hediff_AndroidSubcore hediff))
            {
                return;
            }
            // Snapshot the living android's full identity into the core before pulling it.
            hediff.personaData.CopyFromPawn(pawn);
            try
            {
                AndroidDeath.extractingSubcore = true;
                // Popping the subcore blows the head off, which kills the android outright.
                hediff.SpawnSubcore(ThingPlaceMode.Near);
                if (!pawn.Dead)
                {
                    pawn.Kill(null);
                }
            }
            finally
            {
                AndroidDeath.extractingSubcore = false;
            }
            if (billDoer != null)
            {
                billDoer.skills?.Learn(SkillDefOf.Crafting, 50f);
            }
        }
    }
}
