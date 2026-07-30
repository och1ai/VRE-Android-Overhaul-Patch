using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Declares which synthetic circulatory organs a blood-type gene installs, and on which body parts:
    // neutroamine gets a neutropump and neutrofilters, hemogenic a hemopump and hemofilters, and a
    // bloodless frame has no fluid to move at all, so it runs solid-state internals instead. A blood type
    // that lists nothing for a part simply has no organ there. Mirrors PowerCoreExtension.
    public class BloodOrgansExtension : DefModExtension
    {
        public List<BloodOrgan> organs = new List<BloodOrgan>();

        public HediffDef GetOrgan(BodyPartDef part)
        {
            for (int i = 0; i < organs.Count; i++)
            {
                if (organs[i].part == part)
                {
                    return organs[i].hediff;
                }
            }
            return null;
        }
    }

    public class BloodOrgan
    {
        public BodyPartDef part;
        public HediffDef hediff;
    }

    public static class BloodOrganUtil
    {
        private static HashSet<BodyPartDef> cachedParts;
        private static HashSet<HediffDef> cachedHediffs;

        // Which parts and hediffs count as circulatory is derived from the extensions themselves rather
        // than listed anywhere, so adding a blood type is a pure def change.
        private static void BuildCache()
        {
            if (cachedParts != null)
            {
                return;
            }
            cachedParts = new HashSet<BodyPartDef>();
            cachedHediffs = new HashSet<HediffDef>();
            foreach (GeneDef geneDef in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                BloodOrgansExtension ext = geneDef.GetModExtension<BloodOrgansExtension>();
                if (ext?.organs == null)
                {
                    continue;
                }
                foreach (BloodOrgan organ in ext.organs)
                {
                    if (organ.part != null)
                    {
                        cachedParts.Add(organ.part);
                    }
                    if (organ.hediff != null)
                    {
                        cachedHediffs.Add(organ.hediff);
                    }
                }
            }
        }

        // Body parts whose synthetic organ depends on the android's blood type (heart, kidneys).
        public static bool IsBloodOrganPart(BodyPartDef part)
        {
            BuildCache();
            return cachedParts.Contains(part);
        }

        public static bool IsBloodOrganHediff(HediffDef def)
        {
            BuildCache();
            return cachedHediffs.Contains(def);
        }

        // The organ this android's blood type puts on the given part. Null both when the part is not a
        // circulatory one and when the blood type deliberately leaves it bare, so callers that need to
        // tell those apart must ask IsBloodOrganPart first.
        public static HediffDef OrganFor(BodyPartDef part, Pawn pawn)
        {
            if (!IsBloodOrganPart(part))
            {
                return null;
            }
            return pawn.ActiveBloodGene()?.def.GetModExtension<BloodOrgansExtension>()?.GetOrgan(part);
        }

        // Reconciles an android's circulatory organs with its active blood type: strips the organs that do
        // not belong to it and installs the ones that do. A part listed once applies to every record of it
        // (both kidneys get a neutrofilter); listed more than once, the organs map to successive records
        // (one kidney gets a fluid reprocessor, the other a heatsink). A manual implant is never
        // overwritten.
        //
        // geneOverride is passed from a gene's PostAdd, where that gene is not flagged Active yet.
        public static void SyncBloodOrgans(Pawn pawn, GeneDef geneOverride = null)
        {
            if (pawn?.health == null)
            {
                return;
            }
            BuildCache();
            GeneDef bloodGene = geneOverride ?? pawn.ActiveBloodGene()?.def;
            BloodOrgansExtension ext = bloodGene?.GetModExtension<BloodOrgansExtension>();
            HashSet<HediffDef> allowed = new HashSet<HediffDef>();
            if (ext?.organs != null)
            {
                foreach (BloodOrgan organ in ext.organs)
                {
                    if (organ.hediff != null)
                    {
                        allowed.Add(organ.hediff);
                    }
                }
            }
            // 1) Strip any circulatory organ that belongs to a different blood type. Matched by hediff
            //    rather than by body part record, so it holds however the organ got there.
            //
            //    The same pass drops duplicates on a part. The overlay needs that and the fork does not:
            //    the fork edits the body gene to skip circulatory parts, whereas here the original's body
            //    gene installs its counterpart on every part unconditionally - it has no "part already
            //    carries an added part" guard of its own - so an android whose blood gene happened to be
            //    applied first would end up wearing two pumps.
            HashSet<BodyPartRecord> kept = new HashSet<BodyPartRecord>();
            List<Hediff> stale = new List<Hediff>();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (!(hediff is Hediff_AndroidPart) || !IsBloodOrganHediff(hediff.def))
                {
                    continue;
                }
                // Short-circuits deliberately: an organ that does not belong here must not reserve the
                // part against the one that does.
                if (!allowed.Contains(hediff.def) || (hediff.Part != null && !kept.Add(hediff.Part)))
                {
                    stale.Add(hediff);
                }
            }
            foreach (Hediff hediff in stale)
            {
                pawn.health.RemoveHediff(hediff);
            }
            // 2) Install this blood type's organs on each circulatory part record.
            foreach (BodyPartDef partDef in cachedParts)
            {
                List<HediffDef> desired = new List<HediffDef>();
                if (ext?.organs != null)
                {
                    foreach (BloodOrgan organ in ext.organs)
                    {
                        if (organ.part == partDef && organ.hediff != null)
                        {
                            desired.Add(organ.hediff);
                        }
                    }
                }
                if (desired.Count == 0)
                {
                    continue;
                }
                List<BodyPartRecord> records = pawn.health.hediffSet.GetNotMissingParts()
                    .Where(p => p.def == partDef).ToList();
                for (int i = 0; i < records.Count; i++)
                {
                    HediffDef want = desired.Count == 1 ? desired[0] : (i < desired.Count ? desired[i] : null);
                    if (want == null)
                    {
                        continue;
                    }
                    BodyPartRecord record = records[i];
                    bool alreadyHasAddedPart = pawn.health.hediffSet.hediffs
                        .Any(h => h.Part == record && h is Hediff_AddedPart);
                    if (!alreadyHasAddedPart)
                    {
                        pawn.health.AddHediff(want, record);
                    }
                }
            }
        }
    }

    // Blood-type hardware gene. Installing the organs here rather than from the body gene means they do
    // not depend on the order genes are applied, and it works for an android reprogrammed after it was
    // built - swapping blood type swaps the organs with it.
    public class Gene_AndroidBlood : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            // Pass our own def: this gene is not flagged Active yet during PostAdd.
            BloodOrganUtil.SyncBloodOrgans(pawn, def);
        }
    }

    // The original's body gene installs a fixed android counterpart on every body part, heart and kidneys
    // included, so it builds every android with the neutroamine organs whatever blood it actually runs -
    // and it does so unconditionally, without checking whether the part already carries an implant. The
    // fork edits that loop to skip circulatory parts; an overlay cannot, so the organs are reconciled
    // straight afterwards instead. That costs nothing: removing a hediff does not drop its
    // spawnThingOnRemoved item - only the removal surgery does.
    //
    // This is what makes the end state independent of the order the genes are applied in. Run after the
    // body gene, the sync either corrects the organ it just installed (blood gene still to come, so the
    // blood gene's own PostAdd finishes the job) or discards the duplicate it stacked on top of the right
    // one (blood gene already applied).
    [HarmonyPatch(typeof(Gene_SyntheticBody), nameof(Gene_SyntheticBody.PostAdd))]
    public static class Gene_SyntheticBody_PostAdd_BloodOrgans_Patch
    {
        public static void Postfix(Gene_SyntheticBody __instance)
        {
            BloodOrganUtil.SyncBloodOrgans(__instance.pawn);
        }
    }

    // Androids in a save from before the organs were per blood type are carrying whichever ones the
    // original built them with - neutroamine organs on a hemogenic or bloodless frame. Reconciled once,
    // then recorded as done.
    public class GameComponent_BloodOrganMigration : GameComponent
    {
        private bool migrated;

        public GameComponent_BloodOrganMigration(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref migrated, "vreaOverhaul_bloodOrgansMigrated", defaultValue: false);
        }

        public override void FinalizeInit()
        {
            if (migrated)
            {
                return;
            }
            migrated = true;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (pawn.IsAndroid())
                {
                    BloodOrganUtil.SyncBloodOrgans(pawn);
                }
            }
        }
    }
}
