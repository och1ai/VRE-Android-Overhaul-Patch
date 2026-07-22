using VREAndroids;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // The work-in-progress android sitting in the printer: it holds the delivered materials and shows
    // the print progress. During a resurrection it instead renders the actual body being regrown. The
    // finished android is generated and spawned by the printer when the print completes.
    public class UnfinishedAndroid : ThingWithComps
    {
        public float workLeft = -10000f;
        public List<Thing> resources;
        public Building_AndroidCreationStation station;

        // Once the android body exists - the corpse being resurrected, or the body assembled from the
        // second cycle onward - it is shown on the machine instead of this placeholder shell.
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = station?.PawnBeingAssembled;
            if (pawn != null)
            {
                try
                {
                    pawn.Rotation = Rot4.South;
                    ForkCompat.forceStandingPawn = pawn;
                    pawn.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, drawLoc, flip);
                    pawn.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, drawLoc, flip);
                    pawn.DynamicDrawPhaseAt(DrawPhase.Draw, drawLoc, flip);
                    return;
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[VREAndroids] Error drawing resurrecting body: " + e, thingIDNumber ^ 0x51CE);
                }
                finally
                {
                    ForkCompat.forceStandingPawn = null;
                }
            }
            base.DrawAt(drawLoc, flip);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (station != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                text += "VREA.PrintingProgress".Translate((station.PrintProgress * 100f).ToString("F0"));
            }
            return text;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }
            yield return new Command_Action
            {
                defaultLabel = "VREA.CancelAndroid".Translate(),
                defaultDesc = "VREA.CancelAndroidDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/CancelAnAndroid"),
                action = delegate
                {
                    CancelProject();
                }
            };
        }

        public void CancelProject()
        {
            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    GenPlace.TryPlaceThing(resource, Position, Map, ThingPlaceMode.Near);
                }
            }
            if (station != null)
            {
                station.curAndroidProject = null;
                station.unfinishedAndroid = null;
            }
            this.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref resources, "requiredItems", LookMode.Deep);
            Scribe_References.Look(ref station, "station");
        }
    }
}
