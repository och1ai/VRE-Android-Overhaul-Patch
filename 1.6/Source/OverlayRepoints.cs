using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VREAndroidsOverhaul
{
    // Checks, at startup, that the overlay's def repoints actually took.
    //
    // Nearly everything this mod rewrites is installed by pointing one of the original's defs at one of our
    // classes from Patches/*.xml. That is the overlay's weakest link, because a PatchOperation whose xpath
    // stops matching fails *quietly*: the game still starts, most of the mod still works, and one whole
    // subsystem silently keeps running the original's code. Nothing in the mod notices, and the symptom
    // reaches the player as "this feature was never ported" rather than "a patch did not apply".
    //
    // So every class repoint is restated here as an assertion. Anything that did not take is named in the
    // log and then set from code. This runs after the DefDatabase is built and before any save is loaded,
    // which is early enough: all of these are read when the game instantiates something, not at def load.
    [StaticConstructorOnStartup]
    public static class OverlayRepoints
    {
        static OverlayRepoints()
        {
            List<string> repaired = new List<string>();
            List<string> absent = new List<string>();
            List<string> unrepairable = new List<string>();
            int verified = 0;

            void Repoint(string what, Def def, Func<bool> alreadyPointed, Action point)
            {
                if (def == null)
                {
                    absent.Add(what);
                    return;
                }
                verified++;
                if (alreadyPointed())
                {
                    return;
                }
                try
                {
                    point();
                    repaired.Add(what);
                }
                catch (Exception ex)
                {
                    Log.Error("[VRE-Android Overhaul] Could not set " + what + " from code: " + ex);
                }
            }

            // --- the assembler cluster (Patches/Assembler.xml) ---
            ThingDef assembler = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_AndroidCreationStation");
            Repoint("the assembler's building class", assembler,
                () => assembler.thingClass == typeof(Building_AndroidCreationStation),
                () => assembler.thingClass = typeof(Building_AndroidCreationStation));
            Repoint("the assembler's bills tab", assembler,
                () => assembler.inspectorTabs != null && assembler.inspectorTabs.Contains(typeof(ITab_AndroidBills)),
                () => AddInspectorTab(assembler, typeof(ITab_AndroidBills)));

            ThingDef unfinished = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_UnfinishedAndroid");
            Repoint("the unfinished android's class", unfinished,
                () => unfinished.thingClass == typeof(UnfinishedAndroid),
                () => unfinished.thingClass = typeof(UnfinishedAndroid));

            // Not class repoints, but the same silent failure - and the pair that broke the printer. Left on
            // the values UnfinishedBase hands down, the android-in-progress is drawn by the map mesh (so the
            // staged body DrawAt paints never appears at all) and is haulable (so a colonist carries the
            // half-built android off to a stockpile and the print stops).
            Repoint("the unfinished android's drawer type", unfinished,
                () => unfinished.drawerType == DrawerType.RealtimeOnly,
                () => unfinished.drawerType = DrawerType.RealtimeOnly);
            Repoint("the unfinished android's haulability", unfinished,
                () => !unfinished.alwaysHaulable,
                () => unfinished.alwaysHaulable = false);
            Repoint("the unfinished android's selectability", unfinished,
                () => !unfinished.selectable,
                () => unfinished.selectable = false);

            JobDef createJob = DefDatabase<JobDef>.GetNamedSilentFail("VREA_CreateAndroid");
            Repoint("the create-android job driver", createJob,
                () => createJob.driverClass == typeof(JobDriver_CreateAndroid),
                () => createJob.driverClass = typeof(JobDriver_CreateAndroid));

            WorkGiverDef createGiver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("VREA_CreateAndroid");
            Repoint("the create-android work giver", createGiver,
                () => createGiver.giverClass == typeof(WorkGiver_CreateAndroid),
                () => SetGiverClass(createGiver, typeof(WorkGiver_CreateAndroid)));

            // The polyanalyzer's output is a def reference rather than a class, but it is the same kind of
            // repoint and the same kind of silent failure - it already broke once this way.
            ThingDef polyanalyzer = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_SubcorePolyanalyzer");
            ThingDef subcore = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_AndroidSubcore");
            Repoint("the polyanalyzer's subcore output",
                subcore != null && polyanalyzer?.building != null ? polyanalyzer : null,
                () => polyanalyzer.building.subcoreScannerOutputDef == subcore,
                () => polyanalyzer.building.subcoreScannerOutputDef = subcore);

            // --- repair rework (Patches/RepairRework.xml) ---
            JobDef repairJob = DefDatabase<JobDef>.GetNamedSilentFail("VREA_RepairAndroid");
            Repoint("the repair job driver", repairJob,
                () => repairJob.driverClass == typeof(JobDriver_RepairAndroid),
                () => repairJob.driverClass = typeof(JobDriver_RepairAndroid));

            WorkGiverDef repairGiver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("VREA_RepairAndroid");
            Repoint("the repair work giver", repairGiver,
                () => repairGiver.giverClass == typeof(WorkGiver_RepairAndroid),
                () => SetGiverClass(repairGiver, typeof(WorkGiver_RepairAndroid)));

            // --- power cores (Patches/PowerCores.xml) ---
            GeneDef powerGene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Power");
            Repoint("the power gene's class", powerGene,
                () => powerGene.geneClass == typeof(Gene_AndroidPower),
                () => powerGene.geneClass = typeof(Gene_AndroidPower));

            HediffDef reactor = DefDatabase<HediffDef>.GetNamedSilentFail("VREA_Reactor");
            Repoint("the reactor hediff's class", reactor,
                () => reactor.hediffClass == typeof(Hediff_AndroidReactorCore),
                () => reactor.hediffClass = typeof(Hediff_AndroidReactorCore));

            NeedDef power = DefDatabase<NeedDef>.GetNamedSilentFail("VREA_ReactorPower");
            Repoint("the power need's class", power,
                () => power.needClass == typeof(Need_AndroidPower),
                () => power.needClass = typeof(Need_AndroidPower));

            // --- memory rework (Patches/MemoryRework.xml) ---
            NeedDef memory = DefDatabase<NeedDef>.GetNamedSilentFail("VREA_MemorySpace");
            Repoint("the memory need's class", memory,
                () => memory.needClass == typeof(Need_AndroidMemory),
                () => memory.needClass = typeof(Need_AndroidMemory));

            // The power-core extension carries data - which hediff, and the body part it sits in - so there
            // is nothing safe to reconstruct from code if its patch did not apply. Reported only.
            if (powerGene != null && !powerGene.HasModExtension<PowerCoreExtension>())
            {
                unrepairable.Add("the power gene's core extension");
            }

            if (absent.Any())
            {
                Log.Error("[VRE-Android Overhaul] The original mod no longer has the defs behind: "
                    + absent.ToCommaList() + ". Those parts of the overhaul are not installed.");
            }
            if (unrepairable.Any())
            {
                Log.Error("[VRE-Android Overhaul] These XML repoints did not apply and carry data that "
                    + "cannot be rebuilt from code: " + unrepairable.ToCommaList() + ".");
            }
            if (repaired.Any())
            {
                Log.Error("[VRE-Android Overhaul] " + repaired.Count + " of " + verified + " def repoints did "
                    + "not apply from XML and were set from code: " + repaired.ToCommaList()
                    + ". The overhaul is running, but the matching Patches/*.xml operation needs fixing.");
            }
            else
            {
                Log.Message("[VRE-Android Overhaul] all " + verified + " def repoints applied.");
            }
        }

        private static void AddInspectorTab(ThingDef def, Type tabType)
        {
            if (def.inspectorTabs == null)
            {
                def.inspectorTabs = new List<Type>();
            }
            def.inspectorTabs.Add(tabType);
            // ResolveReferences already built the instance list, so the tab has to be added to both.
            if (def.inspectorTabsResolved == null)
            {
                def.inspectorTabsResolved = new List<InspectTabBase>();
            }
            def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
        }

        private static void SetGiverClass(WorkGiverDef def, Type giverClass)
        {
            def.giverClass = giverClass;
            // Worker is built on first use and cached; drop it so the new class is what gets instantiated.
            AccessTools.Field(typeof(WorkGiverDef), "workerInt")?.SetValue(def, null);
        }
    }
}
