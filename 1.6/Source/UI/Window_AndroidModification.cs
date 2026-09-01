using RimWorld;
using VREAndroids;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    [HotSwappable]
    public class Window_AndroidModification : Window_CreateAndroidBase
    {
        public Building_AndroidBehavioristStation station;

        public Pawn android;
        public Window_AndroidModification(Building_AndroidBehavioristStation station, Pawn android, Action callback) : base(callback)
        {
            this.station = station;
            this.android = android;
            this.selectedGenes = android.genes.GenesListForReading.Where(x => x.def.IsAndroidGene()).Select(x => x.def).ToList();
            forcePause = true;
            // Recompute biostats/conflicts for the android's actual components (the base ctor seeded a
            // default loadout before we swapped in this pawn's genes).
            OnGenesChanged();
        }
        protected override string Header => "VREA.ModifyAndroid".Translate();
        protected override string AcceptButtonLabel => "VREA.ModifyAndroid".Translate();
        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);
            if (station.curAndroidProject is null)
            {
                station.CancelModification();
            }
        }
        protected override void AcceptInner()
        {
            CustomXenotype customXenotype = new CustomXenotype();
            customXenotype.name = xenotypeName?.Trim();
            customXenotype.genes.AddRange(selectedGenes);
            customXenotype.inheritable = false;
            customXenotype.iconDef = iconDef;
            station.curAndroidProject = customXenotype;
            var genesToRemove = android.genes.GenesListForReading.Where(x => x.def.IsAndroidGene() 
            && selectedGenes.Contains(x.def) is false).ToList();
            var newGenesToAdd = selectedGenes.Where(x => android.genes.GenesListForReading.Select(y => y.def).Contains(x) is false).ToList();
            station.totalWorkAmount = (genesToRemove.Count * 2000) + (newGenesToAdd.Count * 2000);
            station.currentWorkAmountDone = 0;
            station.initModification = true;
        }

        // The behaviourist station REPROGRAMS an android; it does not rebuild it. Subroutines are software
        // and can be rewritten freely here - hardware is physical and is chosen when the body is printed,
        // so every hardware component shows in the selected list but is locked.
        //
        // This is wider than the fork, which locked only blood, power and chassis and let the rest of the
        // hardware be swapped from this chair. Installing or pulling a memory drive, a coagulation module
        // or a spacer chassis is surgery on a built body, not a reprogramming job, so the station now
        // refuses all of it: to change hardware, print a new body or use the operations tab.
        private static bool IsHardware(GeneDef geneDef)
        {
            return geneDef.displayCategory?.defName == HardwareCategory
                || geneDef.IsBloodGene() || geneDef.IsPowerGene() || geneDef.IsChassisGene();
        }

        private const string HardwareCategory = "VREA_Hardware";

        protected override bool IsGeneLocked(GeneDef geneDef)
        {
            return IsHardware(geneDef);
        }

        public override bool GeneValidator(GeneDef x)
        {
            // Hardware is fixed once the body is built. Show only the components this android actually
            // has (locked) and hide the alternatives, so the hardware list mirrors the selected list
            // instead of offering a swap that this station cannot perform.
            if (IsHardware(x))
            {
                return selectedGenes.Contains(x);
            }
            if (android.IsAwakened())
            {
                if (x is AndroidGeneDef geneDef && geneDef.removeWhenAwakened)
                {
                    return false;
                }
                else if (x == VREA_DefOf.VREA_AntiAwakeningProtocols)
                {
                    return false;
                }
            }
            return base.GeneValidator(x);
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
                    var currentBlood = android.genes.GenesListForReading.Select(g => g.def).FirstOrDefault(d => d.IsBloodGene());
                    selectedGenes = Utils.AndroidGenesGenesInOrder
                        .Where(x => x.CanBeRemovedFromAndroid() is false && x.IsBloodGene() is false).ToList();
                    if (currentBlood != null)
                    {
                        selectedGenes.Add(currentBlood);
                    }
                    selectedGenes.AddRange(xenotype.genes.Where(g => g.IsBloodGene() is false));
                    selectedGenes = selectedGenes.Distinct().ToList();
                    iconDef = xenotype.IconDef;
                    OnGenesChanged();
                }));
            }
        }
    }
}
