using VREAndroids;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    public enum PrintMode
    {
        Print,
        Resurrect,
        Reprint
    }

    [StaticConstructorOnStartup]
    public class Building_AndroidCreationStation : Building, IThingHolder, IBillGiver
    {
        public CompPowerTrader compPower;

        public CustomXenotype curAndroidProject;

        public UnfinishedAndroid unfinishedAndroid;

        public float currentWorkAmountDone;

        public float totalWorkAmount;

        // Cycle progress. Each cycle grows on its own, but between cycles a crafter must come and complete
        // the cycle before the next starts; the final cycle finishes automatically with no colonist.
        public int cyclesCompleted;
        public bool awaitingCycleCompletion;

        // One printing cycle is 4 days, like a mech-gestator gestation cycle. A fresh print / reprint takes
        // two cycles (8 days); a resurrection only takes one (4 days).
        public const int TicksPerCycle = 240000;
        public const int PrintCycles = 2;
        public const int ResurrectCycles = 1;
        public const int ReprintCycles = 2;
        public const int GestationTicks = PrintCycles * TicksPerCycle;

        private static int CyclesForMode(PrintMode mode) => mode switch
        {
            PrintMode.Resurrect => ResurrectCycles,
            PrintMode.Reprint => ReprintCycles,
            _ => PrintCycles,
        };

        private static int TotalTicksForMode(PrintMode mode) => CyclesForMode(mode) * TicksPerCycle;

        // What the current print will produce, and the inputs for the resurrect/reprint modes.
        public PrintMode printMode;
        public AndroidPersonaData reprintPersona;
        // The look/name/age/ideoligion chosen in the appearance designer for a fresh print.
        public AndroidPersonaData curDesign;
        // The specific body/subcore a queued resurrect/reprint still needs a colonist to haul in.
        public Thing pendingInput;
        private ThingOwner innerContainer;

        // Standing "resurrect android" orders, using the vanilla bill system so the UI matches every other
        // crafting station. While a bill should run and the printer is idle, it auto-queues the next
        // available dead android body for resurrection (colonists never "do" the bill themselves - the def
        // has no recipes, so it is never a WorkGiver_DoBill target; the printer processes it on its own).
        public BillStack billStack;
        private Bill activeResurrectBill;
        private string activeResurrectBillId;

        private Effecter printingEffecter;

        public Building_AndroidCreationStation()
        {
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
            billStack = new BillStack(this);
        }

        // ---- IBillGiver (BillStack storage + the vanilla bills UI; the printer never lets colonists work
        // these bills, so most members are minimal). ----
        public BillStack BillStack => billStack;
        public IEnumerable<IntVec3> IngredientStackCells => GenAdj.CellsOccupiedBy(this);
        public bool CurrentlyUsableForBills() => compPower == null || compPower.PowerOn;
        public bool UsableForBillsAfterFueling() => true;
        public void Notify_BillDeleted(Bill bill)
        {
            if (activeResurrectBill == bill)
            {
                activeResurrectBill = null;
            }
        }

        public List<ThingDefCount> requiredItems;
        public IEnumerable<IngredientCount> RequiredIngredients()
        {
            var ingredientCountList = new List<IngredientCount>();
            foreach (var data in requiredItems)
                ingredientCountList.Add(new ThingDefCountClass(data.ThingDef, data.Count).ToIngredientCount());
            return ingredientCountList;
        }

        // The printer is occupied whenever a print of any mode has been queued.
        public bool IsBusy => curAndroidProject != null || unfinishedAndroid != null || pendingInput != null;

        // A print has been queued but its materials/body have not been delivered yet.
        public bool HasPendingJob => unfinishedAndroid == null && (curAndroidProject != null || pendingInput != null);

        public Corpse HeldCorpse => innerContainer.OfType<Corpse>().FirstOrDefault();

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }

        public void DoWork(Pawn crafter, int delta, out bool workDone)
        {
            var workAmount = crafter.GetStatValue(StatDefOf.WorkSpeedGlobal) * delta;
            currentWorkAmountDone += workAmount;
            workDone = currentWorkAmountDone >= totalWorkAmount;
            unfinishedAndroid.workLeft = totalWorkAmount - currentWorkAmountDone;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compPower = this.TryGetComp<CompPowerTrader>();
            // An in-progress print that was tucked inside the printer during a minify is re-spawned at the
            // printer's centre wherever it is placed next.
            if (unfinishedAndroid != null && !unfinishedAndroid.Spawned)
            {
                if (innerContainer.Contains(unfinishedAndroid))
                {
                    innerContainer.Remove(unfinishedAndroid);
                }
                GenSpawn.Spawn(unfinishedAndroid, this.OccupiedRect().CenterCell, map);
            }
        }

        // The printing effect (welding sparks + the machine-hum sustainer) is maintained here in Tick(),
        // which runs every single tick - NOT in TickInterval(delta), which the engine batches (delta > 1)
        // when the building is off-camera. A PerTick sustainer maintained only on those batched calls kept
        // expiring and respawning, which is exactly what made the sound restart/overlap ("starts a thousand
        // times"). The mech gestator and the fabrication table maintain their working sound every tick for
        // the same reason. One effecter, spawned once (attached) and ticked every tick, through the whole
        // print including the brief cycle-completion pauses, so nothing breaks.
        protected override void Tick()
        {
            base.Tick();
            if (unfinishedAndroid != null && (compPower == null || compPower.PowerOn))
            {
                if (printingEffecter == null)
                {
                    printingEffecter = OverhaulDefOf.AndroidAssembling.SpawnAttached(this, this.Map, 1f);
                }
                printingEffecter.EffectTick(this, this);
            }
            else
            {
                StopPrintingEffect();
            }
        }

        // Once a print is under way (the unfinished android exists), the printer builds it on its own -
        // like a mech gestator, no crafter has to stand and work it. Growth only advances while powered.
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            // When idle and powered, standing resurrect bills auto-queue the next available android body.
            if (unfinishedAndroid == null && !HasPendingJob && this.IsHashIntervalTick(120)
                && (compPower == null || compPower.PowerOn))
            {
                TryStartResurrectBill();
            }
            // No growth while a print is paused at a cycle boundary (awaiting a crafter) or unpowered.
            if (unfinishedAndroid == null || awaitingCycleCompletion || (compPower != null && !compPower.PowerOn))
            {
                return;
            }
            // Grow only up to the end of the current cycle, then pause until a crafter (a human or a
            // fabricor) comes and completes it. Every cycle - including the last - needs that completion.
            float cycleBoundary = Mathf.Min((cyclesCompleted + 1) * TicksPerCycle, totalWorkAmount);
            currentWorkAmountDone += delta;
            if (currentWorkAmountDone >= cycleBoundary)
            {
                currentWorkAmountDone = cycleBoundary;
                awaitingCycleCompletion = true;
            }
        }

        public int TotalCycles => Mathf.Max(1, Mathf.RoundToInt(totalWorkAmount / TicksPerCycle));

        // Called by a crafter's "complete assembly cycle" job at a cycle boundary. Every cycle needs a
        // crafter (human or fabricor); completing the last one finishes the android, while completing an
        // earlier one assembles and reveals the actual body on the machine before the next cycle grows.
        public void CompleteCycle(Pawn crafter)
        {
            if (!awaitingCycleCompletion)
            {
                return;
            }
            cyclesCompleted++;
            awaitingCycleCompletion = false;
            if (cyclesCompleted >= TotalCycles)
            {
                FinishAndroidProject();
                return;
            }
            if (PawnBeingAssembled == null && (printMode == PrintMode.Print || printMode == PrintMode.Reprint))
            {
                try
                {
                    // The gene churn during generation briefly downs/undowns the body; mute those notices
                    // until the android is actually born (spawned) at the end of the last cycle.
                    ForkCompat.suppressAndroidNotifications = true;
                    Pawn android = printMode == PrintMode.Reprint
                        ? GenerateReprintedPawn(reprintPersona)
                        : GenerateAndroidPawn(curAndroidProject, curDesign);
                    innerContainer.TryAddOrTransfer(android, canMergeWithExistingStacks: false);
                    // While it is only a body being assembled inside the machine it must NOT count as a
                    // colonist (the colonist bar lists live player-faction pawns even when contained). Drop
                    // its faction until it is "born" (spawned) at the end of the last cycle.
                    android.SetFactionDirect(null);
                    Find.ColonistBar?.MarkColonistsDirty();
                }
                finally
                {
                    ForkCompat.suppressAndroidNotifications = false;
                }
            }
        }

        // Dev: jump to the end of the current cycle and complete it, exactly as a crafter would.
        public void DevCompleteCycle()
        {
            if (unfinishedAndroid == null)
            {
                return;
            }
            currentWorkAmountDone = Mathf.Min((cyclesCompleted + 1) * TicksPerCycle, totalWorkAmount);
            awaitingCycleCompletion = true;
            CompleteCycle(null);
        }

        // Dev: run every remaining cycle through to the finished android.
        public void DevCompleteAllCycles()
        {
            int guard = 0;
            while (unfinishedAndroid != null && guard++ < 16)
            {
                DevCompleteCycle();
            }
        }

        // The live body shown on the machine: the corpse being resurrected, or - once past the first cycle -
        // the freshly assembled android. Null during the first assembly cycle of a print (shows the shell).
        public Pawn HeldAndroid => innerContainer.OfType<Pawn>().FirstOrDefault();
        public Pawn PawnBeingAssembled
        {
            get
            {
                Corpse corpse = HeldCorpse;
                if (corpse?.InnerPawn != null)
                {
                    return corpse.InnerPawn;
                }
                return cyclesCompleted >= 1 ? HeldAndroid : null;
            }
        }

        private void StopPrintingEffect()
        {
            printingEffecter?.Cleanup();
            printingEffecter = null;
        }

        // Exact mech-gestator forming-bar look/size: orange fill, transparent track.
        private static readonly Material AssemblyBarFilledMat =
            SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.98f, 0.46f, 0f));
        private static readonly Material AssemblyBarUnfilledMat =
            SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f, 0f, 0f, 0f));

        // Progress bar is OFF for now: the mech gestator's art has an inset slot to hold the bar, but the
        // assembler texture doesn't yet. Once that slot is added to the art, flip this back to true - the
        // bar code below is kept working and tuned (orange, gestator size, bottom-right at +0.9 / -1.25).
        private static readonly bool DrawProgressBar = false;

        // A small progress bar tucked into the bottom-right of the assembler, same size as a mech
        // gestator's forming bar. Drawn with the vanilla GenDraw.DrawFillableBar.
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (!DrawProgressBar || unfinishedAndroid == null)
            {
                return;
            }
            GenDraw.FillableBarRequest r = default;
            r.center = new Vector3(drawLoc.x + 0.9f, AltitudeLayer.MetaOverlays.AltitudeFor(), drawLoc.z - 1.25f);
            r.size = new Vector2(0.7f, 0.13f);
            r.fillPercent = PrintProgress;
            r.filledMat = AssemblyBarFilledMat;
            r.unfilledMat = AssemblyBarUnfilledMat;
            r.margin = 0f;
            r.rotation = Rot4.North;
            GenDraw.DrawFillableBar(r);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            StopPrintingEffect();
            if (unfinishedAndroid != null && unfinishedAndroid.Spawned)
            {
                if (mode == DestroyMode.Vanish)
                {
                    // Minify: carry the in-progress print inside the printer so it travels with it and is
                    // re-spawned (centred) wherever it is placed next, instead of being left on the ground.
                    unfinishedAndroid.DeSpawn();
                    innerContainer.TryAddOrTransfer(unfinishedAndroid, canMergeWithExistingStacks: false);
                }
                else
                {
                    // Deconstructed/destroyed: abort the print and return the delivered materials.
                    CancelPrint();
                }
            }
            base.DeSpawn(mode);
        }

        public float PrintProgress => totalWorkAmount > 0f ? Mathf.Clamp01(currentWorkAmountDone / totalWorkAmount) : 0f;

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (unfinishedAndroid != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                // Mirror the mech-gestator readout, but printing-flavoured: what's being made, then the
                // current cycle's time left and how many cycles remain.
                text += ModeInspectLine();
                int total = TotalCycles;
                int remaining = Mathf.Max(0, total - cyclesCompleted);
                int ticksLeftThisCycle = Mathf.Max(0, TicksPerCycle - Mathf.RoundToInt(currentWorkAmountDone % TicksPerCycle));
                text += "\n" + "VREA.CurrentPrintingCycle".Translate() + ": " + ticksLeftThisCycle.ToStringTicksToPeriod();
                text += "\n" + "VREA.PrintingCyclesRemaining".Translate() + ": " + remaining
                    + " (" + "OfLower".Translate() + " " + total + ")";
                if (awaitingCycleCompletion)
                {
                    text += "\n" + "VREA.AwaitingCycleCompletion".Translate();
                }
                else if (compPower != null && !compPower.PowerOn)
                {
                    text += " (" + "VREA.PrintingPaused".Translate() + ")";
                }
            }
            return text;
        }

        // The "what's being produced" line of the inspect readout, by mode.
        private string ModeInspectLine()
        {
            switch (printMode)
            {
                case PrintMode.Resurrect:
                    var corpse = HeldCorpse;
                    return corpse?.InnerPawn != null
                        ? "VREA.ResurrectingAndroid".Translate(corpse.InnerPawn.LabelShortCap)
                        : "VREA.ResurrectingAndroid".Translate("");
                case PrintMode.Reprint:
                    return "VREA.ReprintingAndroid".Translate(reprintPersona?.name?.ToStringShort ?? "");
                default:
                    return "VREA.PrintingAndroid".Translate(curAndroidProject?.name ?? "");
            }
        }

        public bool ReadyForAssembling(Pawn crafter, out string failReason)
        {
            failReason = null;
            if (!compPower.PowerOn) failReason = "NoPower".Translate();
            if (curAndroidProject is null && pendingInput is null) return false;
            return failReason is null;
        }

        // Queues a resurrection: a colonist must haul in the dead body (subcore still inside) plus the
        // steel, then the printer regrows it.
        public void QueueResurrect(Corpse corpse)
        {
            pendingInput = corpse;
            printMode = PrintMode.Resurrect;
            requiredItems = new List<ThingDefCount> { new ThingDefCount(ThingDefOf.Steel, 50) };
        }

        // Is this corpse a dead android that a resurrect bill can still restore? Only bodies that still
        // hold their subcore qualify - once extracted, the android can only come back via a reprint.
        public static bool CanResurrect(Corpse corpse) =>
            corpse?.InnerPawn != null && corpse.InnerPawn.IsAndroid() && ForkCompat.HasSubcore(corpse.InnerPawn, out _);

        // When idle, start the next active resurrect bill against the nearest available android body.
        private void TryStartResurrectBill()
        {
            if (IsBusy || billStack.Count == 0)
            {
                return;
            }
            Bill bill = billStack.Bills.FirstOrDefault(b => b.ShouldDoNow());
            if (bill == null)
            {
                return;
            }
            Corpse best = null;
            float bestDist = float.MaxValue;
            foreach (Thing thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (thing is Corpse corpse && CanResurrect(corpse) && !corpse.IsForbidden(Faction.OfPlayer)
                    && Map.reachability.CanReach(Position, corpse, Verse.AI.PathEndMode.ClosestTouch,
                        TraverseParms.For(TraverseMode.PassDoors)))
                {
                    float d = corpse.Position.DistanceToSquared(Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = corpse;
                    }
                }
            }
            if (best != null)
            {
                QueueResurrect(best);
                activeResurrectBill = bill;
            }
        }

        // Records a completed resurrection against the bill that ordered it (native repeat handling).
        private void NotifyResurrectBillCompleted()
        {
            if (activeResurrectBill == null)
            {
                return;
            }
            activeResurrectBill.Notify_IterationCompleted(null, new List<Thing>());
            activeResurrectBill = null;
        }

        // Queues a reprint from a recovered subcore: a colonist hauls in that subcore plus the standard
        // android materials, then the printer prints a fresh body carrying the stored identity.
        public void QueueReprint(AndroidSubcore subcore)
        {
            pendingInput = subcore;
            printMode = PrintMode.Reprint;
            requiredItems = ForkCompat.AndroidMaterialCost(subcore.personaData?.androidGenes, includeSubcore: false);
        }

        // Called by the delivery job once a colonist has brought everything a queued print needs. Routes
        // the delivered things by mode and starts the print.
        public void DeliverAndStart(List<Thing> placedThings)
        {
            switch (printMode)
            {
                case PrintMode.Resurrect:
                {
                    Corpse corpse = placedThings.OfType<Corpse>().FirstOrDefault();
                    if (corpse == null) { pendingInput = null; printMode = PrintMode.Print; return; }
                    corpse.DeSpawn();
                    innerContainer.TryAddOrTransfer(corpse, canMergeWithExistingStacks: false);
                    SpawnUnfinishedMarker();
                    StoreResources(placedThings.Where(t => t != corpse));
                    break;
                }
                case PrintMode.Reprint:
                {
                    AndroidSubcore subcore = placedThings.OfType<AndroidSubcore>().FirstOrDefault(s => s.HasData);
                    if (subcore == null) { pendingInput = null; printMode = PrintMode.Print; return; }
                    reprintPersona = subcore.personaData;
                    // Hand the persona to the printer and empty the item so consuming it is not a death.
                    subcore.personaData = null;
                    subcore.Destroy();
                    SpawnUnfinishedMarker();
                    StoreResources(placedThings.Where(t => t != subcore));
                    break;
                }
                default:
                    printMode = PrintMode.Print;
                    SpawnUnfinishedMarker();
                    StoreResources(placedThings);
                    break;
            }
            pendingInput = null;
        }

        private void StoreResources(IEnumerable<Thing> resources)
        {
            unfinishedAndroid.resources = new List<Thing>();
            foreach (Thing thing in resources)
            {
                unfinishedAndroid.resources.Add(thing);
                if (thing.Spawned)
                {
                    thing.DeSpawn();
                }
            }
        }

        // Spawns the visible work-in-progress placeholder on the printer and (re)starts the print clock.
        private void SpawnUnfinishedMarker()
        {
            unfinishedAndroid = ThingMaker.MakeThing(VREA_DefOf.VREA_UnfinishedAndroid) as UnfinishedAndroid;
            unfinishedAndroid.station = this;
            currentWorkAmountDone = 0;
            cyclesCompleted = 0;
            awaitingCycleCompletion = false;
            totalWorkAmount = TotalTicksForMode(printMode);
            GenSpawn.Spawn(unfinishedAndroid, this.OccupiedRect().CenterCell, Map);
        }

        // Builds and fully configures a fresh android for a print project, but does not spawn it.
        public Pawn GenerateAndroidPawn(CustomXenotype project, AndroidPersonaData design = null)
        {
            Pawn android = MakeBlankAndroid();
            android.genes.xenotypeName = project.name;
            android.genes.iconDef = project.IconDef;
            ClearAndroidGenes(android);
            foreach (GeneDef gene in project.genes.OrderByDescending(x => x.CanBeRemovedFromAndroid() is false).ToList())
            {
                android.genes.AddGene(gene, true);
            }
            ForkCompat.RemoveDuplicateGenes(android);
            ForkCompat.SyncBloodOrgans(android);
            ForkCompat.SyncPowerCore(android);
            ForkCompat.SyncAndroidIdeo(android);
            // The chosen type is authoritative for appearance: keep only the skin/hair/body/melanin genes
            // it actually lists, so exactly the picked colour and body genes remain (no stray melanin or
            // hair gene rides along - greyed-out - next to the chosen one).
            EnforceChosenAppearanceGenes(android, project.genes);
            // Apply the look/name/age/ideoligion chosen in the appearance designer, if any. The design's
            // story overrides pin the rendered colour to match the chosen colour genes above.
            design?.OverwritePawn(android);
            if (android.HasSubcore(out var subcore))
            {
                subcore.personaData.CopyFromPawn(android);
            }
            return android;
        }

        // A throwaway android with the given components applied, for the appearance designer's live
        // preview - mirrors GenerateAndroidPawn's setup minus the subcore snapshot.
        public Pawn MakeDesignAndroid(System.Collections.Generic.List<GeneDef> genes)
        {
            try
            {
                // The gene churn below briefly downs/undowns the throwaway body; mute the notices.
                ForkCompat.suppressAndroidNotifications = true;
                Pawn android = MakeBlankAndroid();
                ClearAndroidGenes(android);
                foreach (GeneDef gene in genes.OrderByDescending(x => x.CanBeRemovedFromAndroid() is false).ToList())
                {
                    android.genes.AddGene(gene, true);
                }
                ForkCompat.RemoveDuplicateGenes(android);
                ForkCompat.SyncBloodOrgans(android);
                ForkCompat.SyncPowerCore(android);
                ForkCompat.SyncAndroidIdeo(android);
                return android;
            }
            finally
            {
                ForkCompat.suppressAndroidNotifications = false;
            }
        }

        // Builds a fresh body from a stored persona (a reprint): rebuilds the stored hardware/subroutines
        // and restores the android's full identity.
        public Pawn GenerateReprintedPawn(AndroidPersonaData persona)
        {
            Pawn android = MakeBlankAndroid();
            ClearAndroidGenes(android);
            if (persona.androidGenes != null)
            {
                foreach (GeneDef gene in persona.androidGenes.OrderByDescending(x => x.CanBeRemovedFromAndroid() is false).ToList())
                {
                    android.genes.AddGene(gene, true);
                }
            }
            if (!persona.xenotypeName.NullOrEmpty()) android.genes.xenotypeName = persona.xenotypeName;
            if (persona.iconDef != null) android.genes.iconDef = persona.iconDef;
            ForkCompat.RemoveDuplicateGenes(android);
            ForkCompat.SyncBloodOrgans(android);
            ForkCompat.SyncPowerCore(android);
            ForkCompat.SyncAndroidIdeo(android);
            EnforceChosenAppearanceGenes(android, persona.androidGenes);
            persona.OverwritePawn(android);
            if (android.HasSubcore(out var subcore))
            {
                subcore.personaData = persona;
            }
            return android;
        }

        private Pawn MakeBlankAndroid()
        {
            var android = PawnGenerator.GeneratePawn(new PawnGenerationRequest(VREA_DefOf.VREA_AndroidBasic, Faction.OfPlayer,
                allowDowned: true, allowAddictions: false));
            android.apparel.WornApparel.Clear();
            android.equipment.AllEquipmentListForReading.Clear();
            android.inventory.innerContainer.Clear();
            // Printed androids come out as fully-formed adults so the game treats them as ordinary pawns,
            // but their chronological age stays 0 - they were just made.
            android.ageTracker.AgeBiologicalTicks = (long)(Rand.Range(20f, 50f) * GenDate.TicksPerYear);
            android.ageTracker.AgeChronologicalTicks = 0;
            return android;
        }

        private static void ClearAndroidGenes(Pawn android)
        {
            // Remove all android genes plus the appearance genes the generator rolls (skin/hair colour,
            // melanin and body-type genes). Melanin genes carry no skinColorBase, so they slip past the
            // colour-gene check - list them explicitly, or a stray one survives next to the chosen colour.
            foreach (var gene in android.genes.GenesListForReading
                .Where(g => g.def.IsAndroidGene() || g.def.bodyType != null
                    || ForkCompat.IsSkinColorGene(g.def) || ForkCompat.IsHairColorGene(g.def)
                    || g.def.endogeneCategory == EndogeneCategory.Melanin).ToList())
            {
                android.genes.RemoveGene(gene);
            }
        }

        // Keep only the appearance genes (skin/hair colour, melanin, body-type) that the chosen type/persona
        // actually lists. The blank body is generated with random colour and body genes, and some (melanin)
        // slip past the earlier clear; this pins the finished body to exactly the designer's picks.
        private static void EnforceChosenAppearanceGenes(Pawn android, IEnumerable<GeneDef> keepGenes)
        {
            var keep = keepGenes != null ? new HashSet<GeneDef>(keepGenes) : new HashSet<GeneDef>();
            foreach (var gene in android.genes.GenesListForReading
                .Where(g => (ForkCompat.IsSkinColorGene(g.def) || ForkCompat.IsHairColorGene(g.def)
                        || g.def.bodyType != null || g.def.endogeneCategory == EndogeneCategory.Melanin)
                    && !keep.Contains(g.def)).ToList())
            {
                android.genes.RemoveGene(gene);
            }
        }

        // The charge a battery android wakes with after being resurrected.
        private const float ResurrectBatteryCharge = 0.5f;

        // After a resurrection, restore the android's power. A battery is inherent, so it is re-installed
        // (in case the body lost it) and wakes with a partial charge. A reactor instead keeps whatever
        // charge it had - its hediff survives the death and resurrection - and if it was destroyed with
        // the body it is not regrown: the android stays unpowered (downed) until a new one is installed.
        private void RestorePowerAfterResurrection(Pawn android)
        {
            if (android.ActivePowerGene()?.def == OverhaulDefOf.BatteryPowered)
            {
                ForkCompat.SyncPowerCore(android);
                var core = android.GetPowerCore();
                if (core != null)
                {
                    core.Energy = ResurrectBatteryCharge;
                }
            }
        }

        public void FinishAndroidProject()
        {
            // Snapshot and clear state up front so that, if the finish ever throws, it cannot re-fire
            // every tick and spam broken androids.
            PrintMode mode = printMode;
            CustomXenotype project = curAndroidProject;
            AndroidPersonaData persona = reprintPersona;
            AndroidPersonaData design = curDesign;
            Corpse corpse = innerContainer.OfType<Corpse>().FirstOrDefault();
            // The android assembled during the later cycle(s) and shown on the machine, if any. It was
            // held faction-less so it wouldn't show as a colonist; give it back to the player before it's
            // spawned and "born".
            Pawn assembled = innerContainer.OfType<Pawn>().FirstOrDefault();
            if (assembled != null)
            {
                innerContainer.Remove(assembled);
                if (assembled.Faction != Faction.OfPlayer)
                {
                    assembled.SetFactionDirect(Faction.OfPlayer);
                }
            }
            UnfinishedAndroid finished = unfinishedAndroid;
            curAndroidProject = null;
            reprintPersona = null;
            curDesign = null;
            unfinishedAndroid = null;
            printMode = PrintMode.Print;
            currentWorkAmountDone = 0;
            totalWorkAmount = 0;
            cyclesCompleted = 0;
            awaitingCycleCompletion = false;
            StopPrintingEffect();
            finished?.Destroy();

            switch (mode)
            {
                case PrintMode.Resurrect:
                    if (corpse?.InnerPawn == null)
                    {
                        return;
                    }
                    Pawn dead = corpse.InnerPawn;
                    ResurrectionUtility.TryResurrect(dead);
                    if (!dead.Dead)
                    {
                        if (dead.Faction != Faction.OfPlayer)
                        {
                            dead.SetFaction(Faction.OfPlayer);
                        }
                        RestorePowerAfterResurrection(dead);
                    }
                    if (!dead.Spawned)
                    {
                        GenSpawn.Spawn(dead, Position, Map);
                    }
                    NotifyResurrectBillCompleted();
                    break;
                case PrintMode.Reprint:
                    if (assembled == null && persona == null)
                    {
                        return;
                    }
                    // Spawn the body assembled during the last cycle; regenerate only as a fallback.
                    GenSpawn.Spawn(assembled ?? GenerateReprintedPawn(persona), Position, Map);
                    break;
                default:
                    if (assembled == null && project == null)
                    {
                        return;
                    }
                    GenSpawn.Spawn(assembled ?? GenerateAndroidPawn(project, design), Position, Map);
                    break;
            }
        }

        // Aborts the current print, returning any consumed inputs to the map.
        public void CancelPrint()
        {
            StopPrintingEffect();
            // A cancelled resurrect leaves its bill active (uncounted) so it can retry later.
            activeResurrectBill = null;
            // A queued job whose inputs were never hauled just leaves the body/subcore where it is.
            pendingInput = null;
            // A half-assembled android body is scrapped rather than dropped as a live pawn.
            var assembled = innerContainer.OfType<Pawn>().ToList();
            foreach (var p in assembled)
            {
                innerContainer.Remove(p);
                p.Destroy();
            }
            if (innerContainer.Count > 0)
            {
                innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            }
            if (reprintPersona != null)
            {
                var subcore = (AndroidSubcore)ThingMaker.MakeThing(OverhaulDefOf.AndroidSubcore);
                subcore.personaData = reprintPersona;
                GenPlace.TryPlaceThing(subcore, Position, Map, ThingPlaceMode.Near);
                reprintPersona = null;
            }
            printMode = PrintMode.Print;
            currentWorkAmountDone = 0;
            totalWorkAmount = 0;
            cyclesCompleted = 0;
            awaitingCycleCompletion = false;
            if (unfinishedAndroid != null)
            {
                unfinishedAndroid.CancelProject();
            }
            else
            {
                curAndroidProject = null;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }
            if (!IsBusy)
            {
                yield return new FloatMenuOption("VREA.PrintAndroid".Translate(), delegate
                {
                    CallAndroidCreationWindow(selPawn);
                });
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }
            if (IsBusy)
            {
                yield return new Command_Action
                {
                    defaultLabel = "VREA.CancelAndroid".Translate(),
                    defaultDesc = "VREA.CancelAndroidDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/CancelAnAndroid"),
                    action = CancelPrint
                };
                if (DebugSettings.godMode)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Complete cycle",
                        action = DevCompleteCycle
                    };
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Complete all cycles",
                        action = DevCompleteAllCycles
                    };
                }
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "VREA.PrintAndroid".Translate(),
                defaultDesc = "VREA.PrintAndroidDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/CreateAnAndroid"),
                action = delegate
                {
                    CallAndroidCreationWindow(null);
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "VREA.ReprintAndroid".Translate(),
                defaultDesc = "VREA.ReprintAndroidDesc".Translate(),
                icon = OverhaulDefOf.AndroidSubcore.uiIcon,
                action = OpenReprintMenu
            };
        }

        // Lists the recovered subcores on the map to load for a reprint.
        private void OpenReprintMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (Thing thing in Map.listerThings.ThingsOfDef(OverhaulDefOf.AndroidSubcore))
            {
                if (thing is AndroidSubcore subcore && subcore.HasData)
                {
                    AndroidSubcore localSubcore = subcore;
                    options.Add(new FloatMenuOption(subcore.LabelCap, delegate { QueueReprint(localSubcore); },
                        iconThing: localSubcore, iconColor: Color.white));
                }
            }
            if (!options.Any())
            {
                options.Add(new FloatMenuOption("VREA.NoSubcores".Translate(), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void CallAndroidCreationWindow(Pawn creator)
        {
            // The appearance designer is the entry point now; the component picker is reached from inside
            // it via "create/edit type".
            Find.WindowStack.Add(new Window_AndroidDesign(this, creator));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref unfinishedAndroid, "unfinishedAndroid");
            Scribe_Deep.Look(ref curAndroidProject, "curAndroidProject");
            Scribe_Values.Look(ref currentWorkAmountDone, "currentWorkAmountDone");
            Scribe_Values.Look(ref totalWorkAmount, "totalWorkAmount");
            Scribe_Values.Look(ref cyclesCompleted, "cyclesCompleted");
            Scribe_Values.Look(ref awaitingCycleCompletion, "awaitingCycleCompletion");
            Scribe_Values.Look(ref printMode, "printMode");
            Scribe_References.Look(ref pendingInput, "pendingInput");
            Scribe_Deep.Look(ref reprintPersona, "reprintPersona");
            Scribe_Deep.Look(ref curDesign, "curDesign");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Collections.Look(ref requiredItems, "requiredItems", LookMode.Deep);
            Scribe_Deep.Look(ref billStack, "billStack", this);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                activeResurrectBillId = activeResurrectBill?.GetUniqueLoadID();
            }
            Scribe_Values.Look(ref activeResurrectBillId, "activeResurrectBillId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: false);
                billStack ??= new BillStack(this);
                billStack.billGiver = this;
                if (!activeResurrectBillId.NullOrEmpty())
                {
                    activeResurrectBill = billStack.Bills.FirstOrDefault(b => b.GetUniqueLoadID() == activeResurrectBillId);
                }
            }
        }
    }

    // The printer's Bills tab. Uses the vanilla bill listing (BillStack.DoListing) so it is pixel-identical
    // to every other crafting station / the mech gestator: an "Add bill" dropdown, per-bill rows with the
    // suspend/copy/delete buttons, repeat modes and the "Details..." dialog.
    public class ITab_AndroidBills : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(420f, 480f);
        private Vector2 scrollPos;
        private float viewHeight = 1000f;

        public ITab_AndroidBills()
        {
            size = WinSize;
            labelKey = "TabBills"; // vanilla "Bills" label, matching other stations
        }

        private Building_AndroidCreationStation Station => SelThing as Building_AndroidCreationStation;

        protected override void FillTab()
        {
            var station = Station;
            if (station == null)
            {
                return;
            }
            var rect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            station.billStack.DoListing(rect, RecipeOptionsMaker, ref scrollPos, ref viewHeight);
        }

        // The "Add bill" dropdown. Only the resurrect recipe is offered - it isn't a recipe user of the
        // printer (so colonists never try to work it), so we surface it here by hand. The option is built
        // exactly like a vanilla crafting-station recipe option: a recipe icon on the left, the ingredient
        // info window on hover, and the "i" info-card button on the right.
        private List<FloatMenuOption> RecipeOptionsMaker()
        {
            var station = Station;
            var opts = new List<FloatMenuOption>();
            RecipeDef recipe = OverhaulDefOf.ResurrectAndroid;
            string label = recipe.LabelCap;
            opts.Add(new FloatMenuOption(label, delegate
            {
                Bill bill = recipe.MakeNewBill();
                station.billStack.AddBill(bill);
            },
            iconTex: OverhaulDefOf.AndroidSubcore.uiIcon,
            iconColor: Color.white,
            mouseoverGuiAction: rect => BillUtility.DoBillInfoWindow(0, label, rect, recipe),
            extraPartWidth: 29f,
            extraPartOnGUI: rect => Widgets.InfoCardButton(rect.x + 5f, rect.y + (rect.height - 24f) / 2f, recipe)));
            return opts;
        }
    }
}
