using RimWorld;
using VREAndroids;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    [HotSwappable]
    public class Window_AndroidCreation : Window_CreateAndroidBase
    {
        public Building_AndroidCreationStation station;

        public Pawn creator;
        public Window_AndroidCreation(Building_AndroidCreationStation station, Pawn creator, Action callback) : base(callback)
        {
            this.station = station;
            this.creator = creator;
        }

        protected override string Header => "VREA.CreateAndroidType".Translate();
        protected override string AcceptButtonLabel => "VREA.Confirm".Translate();

        // Blood type, power source and chassis can be chosen freely while building the body.
        protected override bool CanSwapBlood => true;
        protected override bool CanSwapPower => true;
        protected override bool CanSwapChassis => true;
        // Set by the designer: the component window is now a "pick the android type" sub-step that
        // returns its gene selection, rather than starting the print itself.
        public Action<CustomXenotype> onTypeResult;

        protected override void AcceptInner()
        {
            CustomXenotype customXenotype = new CustomXenotype();
            customXenotype.name = xenotypeName?.Trim();
            customXenotype.genes.AddRange(selectedGenes);
            customXenotype.inheritable = false;
            customXenotype.iconDef = iconDef;
            onTypeResult?.Invoke(customXenotype);
        }


        protected override TaggedString AndroidName()
        {
            return "VREA.AndroidtypeName".Translate();
        }
        protected override void DrawSearchRect(Rect rect)
        {
            base.DrawSearchRect(rect);
            if (Widgets.ButtonText(new Rect(rect.xMax - ButSize.x, rect.y, ButSize.x, ButSize.y), "VREA.SaveAndroidtype".Translate()))
            {
                CustomXenotype customXenotype = new CustomXenotype();
                customXenotype.name = xenotypeName?.Trim();
                customXenotype.genes.AddRange(selectedGenes);
                customXenotype.inheritable = false;
                customXenotype.iconDef = iconDef;
                Find.WindowStack.Add(new Dialog_AndroidProjectList_Save(customXenotype));
            }
            if (Widgets.ButtonText(new Rect(rect.xMax - ButSize.x * 2f - 4f, rect.y, ButSize.x, ButSize.y), "VREA.LoadAndroidtype".Translate()))
            {
                Find.WindowStack.Add(new Dialog_AndroidProjectList_Load(delegate (CustomXenotype xenotype)
                {
                    xenotypeName = xenotype.name;
                    xenotypeNameLocked = true;
                    selectedGenes.Clear();
                    selectedGenes = Utils.AndroidGenesGenesInOrder
                        .Where(x => x.CanBeRemovedFromAndroid() is false && x.IsBloodGene() is false).ToList();
                    selectedGenes.AddRange(xenotype.genes);
                    selectedGenes = selectedGenes.Distinct().ToList();
                    iconDef = xenotype.IconDef;
                    OnGenesChanged();
                }));
            }
        }

        protected override void OnGenesChanged()
        {
            base.OnGenesChanged();
            requiredItems = new List<ThingDefCount>
            {
                new ThingDefCount(OverhaulDefOf.AndroidSubcore, 1),
                new ThingDefCount(ThingDefOf.Plasteel, 125),
                new ThingDefCount(ThingDefOf.ComponentSpacer, 7),
            };
            // The chosen power source is the only source of the android's uranium cost: a reactor
            // needs uranium, a battery just a few components.
            if (SelectedGenes.Contains(OverhaulDefOf.BatteryPowered))
            {
                requiredItems.Add(new ThingDefCount(ThingDefOf.ComponentIndustrial, 3));
            }
            else
            {
                requiredItems.Add(new ThingDefCount(ThingDefOf.Uranium, 20));
            }
            // The chosen blood fills the android's reservoir up front, so it spawns full: neutroamine
            // blood needs 40 neutroamine, hemogenic blood 4 hemogen packs, bloodless needs none.
            if (SelectedGenes.Contains(OverhaulDefOf.NeutroCirculation))
            {
                requiredItems.Add(new ThingDefCount(VREA_DefOf.Neutroamine, 40));
            }
            else if (SelectedGenes.Contains(OverhaulDefOf.NormalBlood))
            {
                requiredItems.Add(new ThingDefCount(ThingDefOf.HemogenPack, 4));
            }
        }
    }
}
