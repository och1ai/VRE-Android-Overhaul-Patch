using VREAndroids;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // Marks a dead android's corpse so a colonist will go extract its subcore, mirroring the
    // mechlink/cortical-stack extraction flow.
    public class Designator_ExtractSubcore : Designator
    {
        protected override DesignationDef Designation => SubcoreDefOf.ExtractSubcoreDesignation;

        public Designator_ExtractSubcore()
        {
            defaultLabel = "VREA.DesignatorExtractSubcore".Translate();
            defaultDesc = "VREA.DesignatorExtractSubcoreDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("Items/PersonaSubcore");
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Claim;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map))
            {
                return false;
            }
            return CorpsesInCell(c).Any()
                ? (AcceptanceReport)true
                : (AcceptanceReport)"VREA.MessageMustDesignateSubcore".Translate();
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            foreach (Thing item in CorpsesInCell(c))
            {
                DesignateThing(item);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (Map.designationManager.DesignationOn(t, Designation) != null)
            {
                return false;
            }
            return t is Corpse corpse && AndroidDeath.HasSubcore(corpse.InnerPawn, out _)
                ? (AcceptanceReport)true
                : (AcceptanceReport)false;
        }

        public override void DesignateThing(Thing t)
        {
            Map.designationManager.AddDesignation(new Designation(t, Designation));
        }

        private IEnumerable<Thing> CorpsesInCell(IntVec3 c)
        {
            if (c.Fogged(Map))
            {
                yield break;
            }
            List<Thing> thingList = c.GetThingList(Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (CanDesignateThing(thingList[i]).Accepted)
                {
                    yield return thingList[i];
                }
            }
        }
    }
}
