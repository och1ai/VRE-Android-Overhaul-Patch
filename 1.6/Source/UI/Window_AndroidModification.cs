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

        // The installed blood type, power source and chassis show in the selected list but are locked:
        // this station can reprogram subroutines and swap other hardware, but the body's fixed power
        // core (reactor/battery), blood system and chassis can only be chosen when the body is printed.
        protected override bool IsGeneLocked(GeneDef geneDef)
        {
            return geneDef.IsBloodGene() || geneDef.IsPowerGene() || geneDef.IsChassisGene();
        }

        public override bool GeneValidator(GeneDef x)
        {
            // Blood type, power source and chassis are fixed once the body is built. Show only the
            // option this android actually has (locked) and hide the alternatives, so the hardware
            // list mirrors the selected list instead of offering a swap.
            if (x.IsBloodGene() || x.IsPowerGene() || x.IsChassisGene())
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
