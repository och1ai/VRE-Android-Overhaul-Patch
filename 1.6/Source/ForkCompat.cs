using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Helpers the fork keeps on its own `Utils`. The overlay cannot add members to the original mod's
    // static class, so the ported UI and assembler files are redirected here instead. Everything is a
    // faithful copy of the fork's behaviour except where noted.
    public static class ForkCompat
    {
        public const string BloodTag = "AndroidBlood";
        public const string PowerTag = "AndroidPower";
        public const string ChassisTag = "AndroidChassis";

        private static bool HasTag(GeneDef g, string tag) => g?.exclusionTags != null && g.exclusionTags.Contains(tag);

        public static bool IsBloodGene(this GeneDef g) => HasTag(g, BloodTag);

        public static bool IsPowerGene(this GeneDef g) => HasTag(g, PowerTag);

        public static bool IsChassisGene(this GeneDef g) => HasTag(g, ChassisTag);

        public static bool IsSkinColorGene(GeneDef g) => g.skinColorBase.HasValue || g.skinColorOverride.HasValue;

        public static bool IsHairColorGene(GeneDef g) => g.hairColorOverride.HasValue;

        public static bool IsBodyTypeGene(GeneDef g) => g.bodyType.HasValue;

        public static Gene ActiveBloodGene(this Pawn pawn) => ActiveTagged(pawn, BloodTag);

        public static Gene ActivePowerGene(this Pawn pawn) => ActiveTagged(pawn, PowerTag);

        private static Gene ActiveTagged(Pawn pawn, string tag)
        {
            if (pawn?.genes == null)
            {
                return null;
            }
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active && HasTag(gene.def, tag))
                {
                    return gene;
                }
            }
            return null;
        }

        // Hardware requirements and conflicts are declared on the fork's extended AndroidGeneDef
        // (`requiresOneOf` / `conflictsWith`), which an overlay cannot add to the original's def class.
        // Until that data is carried by a DefModExtension of our own, nothing declares a requirement, so
        // these report "no requirement" and "no conflict" - the mutually exclusive groups are still
        // enforced by vanilla exclusionTags, which is the part that actually matters.
        public static List<GeneDef> RequiredHardware(this GeneDef geneDef) => null;

        public static bool RequirementSatisfiedBy(this GeneDef geneDef, List<GeneDef> selected) => true;

        public static GeneDef ConflictInSelection(this GeneDef geneDef, List<GeneDef> selected) => null;

        public static List<string> ConflictsWith(this GeneDef geneDef) => null;

        private static List<GeneDef> cachedSkinColorGenes;

        public static List<GeneDef> AllSkinColorAndroidGenes
        {
            get
            {
                if (cachedSkinColorGenes == null)
                {
                    cachedSkinColorGenes = DefDatabase<GeneDef>.AllDefsListForReading
                        .Where(g => g.IsAndroidGene() && IsSkinColorGene(g))
                        .OrderBy(g => g.skinColorBase.HasValue ? g.minMelanin : 2f)
                        .ToList();
                }
                return cachedSkinColorGenes;
            }
        }

        public static Color SkinColorOf(GeneDef g) =>
            g.skinColorOverride ?? g.skinColorBase ?? Color.white;

        // The standard materials to build an android body with the given components - the same cost the
        // creation window charges. Reprints supply their own subcore, so it is excluded there.
        public static List<ThingDefCount> AndroidMaterialCost(IEnumerable<GeneDef> genes, bool includeSubcore)
        {
            List<GeneDef> geneList = genes?.ToList() ?? new List<GeneDef>();
            List<ThingDefCount> items = new List<ThingDefCount>();
            if (includeSubcore && OverhaulDefOf.AndroidSubcore != null)
            {
                items.Add(new ThingDefCount(OverhaulDefOf.AndroidSubcore, 1));
            }
            items.Add(new ThingDefCount(ThingDefOf.Plasteel, 125));
            items.Add(new ThingDefCount(ThingDefOf.ComponentSpacer, 7));
            items.Add(geneList.Contains(OverhaulDefOf.BatteryPowered)
                ? new ThingDefCount(ThingDefOf.ComponentIndustrial, 3)
                : new ThingDefCount(ThingDefOf.Uranium, 20));
            if (geneList.Contains(OverhaulDefOf.NeutroCirculation) && OverhaulDefOf.Neutroamine != null)
            {
                items.Add(new ThingDefCount(OverhaulDefOf.Neutroamine, 40));
            }
            else if (geneList.Contains(OverhaulDefOf.NormalBlood))
            {
                items.Add(new ThingDefCount(ThingDefOf.HemogenPack, 4));
            }
            return items;
        }

        public static void RemoveDuplicateGenes(Pawn pawn)
        {
            if (pawn?.genes == null)
            {
                return;
            }
            HashSet<GeneDef> seen = new HashSet<GeneDef>();
            foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
            {
                if (!seen.Add(gene.def))
                {
                    pawn.genes.RemoveGene(gene);
                }
            }
        }

        public static void SyncAndroidIdeo(Pawn pawn) => IdeoCapability.SyncIdeo(pawn);

        public static void SyncPowerCore(Pawn pawn, GeneDef geneOverride = null) =>
            PowerCoreUtil.SyncPowerCore(pawn, geneOverride);

        // Per-blood-type circulatory organs are not ported yet (see PORTING.md); nothing to reconcile.
        public static void SyncBloodOrgans(Pawn pawn, GeneDef geneOverride = null)
        {
        }

        public static bool HasSubcore(Pawn pawn, out Hediff_AndroidSubcore subcore) =>
            AndroidDeath.HasSubcore(pawn, out subcore);

        // While a throwaway designer-preview android is being built/edited its gene churn briefly downs and
        // undowns it; this suppresses the resulting notices.
        public static bool suppressAndroidNotifications;

        // Set while a designer preview pawn is being drawn, so it is rendered standing rather than in
        // whatever posture its (throwaway) job implies. Consumed by the posture patch once ported.
        public static Pawn forceStandingPawn;
    }

    // GeneCreationDialogBase / GeneUIUtility keep these private; the fork builds against a publicized
    // assembly and calls them directly. Reflection keeps the ported UI byte-identical instead of
    // reimplementing vanilla drawing.
    public static class VanillaGeneUI
    {
        private static readonly System.Reflection.MethodInfo drawStat =
            HarmonyLib.AccessTools.Method(typeof(GeneUIUtility), "DrawStat");

        private static readonly System.Reflection.MethodInfo drawIconSelector =
            HarmonyLib.AccessTools.Method(typeof(GeneCreationDialogBase), "DrawIconSelector");

        private static readonly System.Reflection.FieldInfo validSymbolRegex =
            HarmonyLib.AccessTools.Field(typeof(GeneCreationDialogBase), "ValidSymbolRegex");

        public static void DrawStat(Rect rect, CachedTexture icon, string text, float width)
        {
            drawStat?.Invoke(null, new object[] { rect, icon, text, width });
        }

        public static void DrawIconSelector(GeneCreationDialogBase window, Rect rect)
        {
            drawIconSelector?.Invoke(window, new object[] { rect });
        }

        // Vanilla keeps this as a compiled Regex; fall back to the same pattern if it ever moves.
        public static System.Text.RegularExpressions.Regex ValidSymbolRegex =>
            validSymbolRegex?.GetValue(null) as System.Text.RegularExpressions.Regex
                ?? new System.Text.RegularExpressions.Regex("[\\p{L}0-9 \'\\-]*");
    }

    // Defs the ported files reach through the fork's DefOf. Resolved by name so a missing DLC or a renamed
    // def degrades instead of throwing at startup.
    [StaticConstructorOnStartup]
    public static class OverhaulDefOf
    {
        public static readonly ThingDef AndroidSubcore = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_AndroidSubcore");
        public static readonly ThingDef Neutroamine = DefDatabase<ThingDef>.GetNamedSilentFail("Neutroamine");
        public static readonly GeneDef BatteryPowered = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_BatteryPowered");
        // The original's "power" gene, retuned into "reactor powered" by Patches/PowerCores.xml.
        public static readonly GeneDef ReactorPowered = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Power");
        public static readonly GeneDef NeutroCirculation = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_NeutroCirculation");
        public static readonly GeneDef NormalBlood = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_NormalBlood");
        public static readonly GeneDef Ideological = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Ideological");
        public static readonly JobDef CompleteAndroidCycle = DefDatabase<JobDef>.GetNamedSilentFail("VREA_CompleteAndroidCycle");
        public static readonly RecipeDef ResurrectAndroid = DefDatabase<RecipeDef>.GetNamedSilentFail("VREA_ResurrectAndroid");
        public static readonly XenotypeIconDef AndroidXenotypeIcon7 =
            DefDatabase<XenotypeIconDef>.GetNamedSilentFail("VRE_AndroidXenotypeIcon7");
        public static readonly EffecterDef AndroidAssembling = DefDatabase<EffecterDef>.GetNamedSilentFail("VREA_AndroidAssembling");
    }
}
