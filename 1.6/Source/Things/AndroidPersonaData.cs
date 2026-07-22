using VREAndroids;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // A single stored skill, so a subcore can carry skill levels without holding a live SkillRecord.
    public class StoredSkill : IExposable
    {
        public SkillDef def;
        public int level;
        public float xpSinceLastLevel;
        public Passion passion;

        public StoredSkill() { }

        public StoredSkill(SkillRecord record)
        {
            def = record.def;
            level = record.levelInt;
            xpSinceLastLevel = record.xpSinceLastLevel;
            passion = record.passion;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref level, "level", 0);
            Scribe_Values.Look(ref xpSinceLastLevel, "xpSinceLastLevel", 0f);
            Scribe_Values.Look(ref passion, "passion", Passion.None);
        }
    }

    // The identity an android subcore carries: who the android is (name, personality, skills, memories,
    // relationships, ideology, life records) and what it looks like, plus the android hardware/subroutine
    // genes that defined the body. This is the android equivalent of an Altered Carbon cortical stack -
    // it lets a body be reprinted or resurrected later as the same person. For a fresh (never awakened)
    // drone most of this is empty, exactly as a factory android would carry no history.
    public class AndroidPersonaData : IExposable
    {
        public NameTriple name;
        public Gender gender;
        public BackstoryDef childhood;
        public BackstoryDef adulthood;
        public BodyTypeDef bodyType;
        public HeadTypeDef headType;
        public HairDef hairDef;
        public BeardDef beard;
        public TattooDef bodyTattoo;
        public TattooDef faceTattoo;
        public Color hairColor = Color.white;
        public Color? skinColorOverride;
        public List<Trait> traits = new List<Trait>();
        public List<StoredSkill> skills = new List<StoredSkill>();
        public List<GeneDef> androidGenes = new List<GeneDef>();
        public string xenotypeName;
        public XenotypeIconDef iconDef;
        public Faction faction;
        // The (now dead) pawn this persona was taken from, kept so that destroying an extracted subcore
        // can still make its friends and lovers grieve.
        public Pawn sourcePawn;

        // Biological age chosen in the designer / carried by the original; reapplied to the printed body.
        public long biologicalAgeTicks;

        // Full persona (mirrors what a cortical stack preserves).
        public List<Thought_Memory> memories = new List<Thought_Memory>();
        public List<DirectPawnRelation> relations = new List<DirectPawnRelation>();
        public bool everSeenByPlayer;
        public Ideo ideo;
        public float certainty;
        public List<Ideo> previousIdeos = new List<Ideo>();
        public int joinTick;
        public DefMap<RecordDef, float> records;

        public bool ContainsData => name != null;

        // NameTriple's constructor calls Trim() on first and last, so a null there (common for android
        // names, which have no surname) throws. Build one defensively.
        private static NameTriple MakeNameTriple(string first, string nick, string last)
        {
            return new NameTriple(first ?? "", nick, last ?? "");
        }

        public string ShortName => name != null ? name.ToStringShort : "VREA.UnknownPersona".Translate().ToString();

        // The stored persona's name is tinted by allegiance, the way a cortical stack colours its
        // sleeve's name: red for a hostile android, blue for one of your own or a friendly.
        public Color NameColor =>
            (faction != null && faction != Faction.OfPlayer && faction.HostileTo(Faction.OfPlayer))
                ? new Color(0.9f, 0.35f, 0.35f)
                : new Color(0.45f, 0.75f, 1f);

        // Convenience: the stored short name, already colourized for rich-text labels.
        public string ColoredShortName => ShortName.Colorize(NameColor);

        // Snapshots an android's current identity, memory and appearance into this subcore.
        public void CopyFromPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }
            // Non-awakened androids are shown with a NameSingle, but their real full name is stashed on
            // the synthetic-body gene (and restored when they awaken). Prefer that so the core always
            // carries the android's true identity; fall back to whatever name the pawn does have.
            NameTriple resolvedName = pawn.Name as NameTriple;
            if (resolvedName == null)
            {
                if (pawn.genes?.GetGene(VREA_DefOf.VREA_SyntheticBody) is Gene_SyntheticBody bodyGene
                    && bodyGene.storedTripleName != null)
                {
                    resolvedName = bodyGene.storedTripleName;
                }
                else if (pawn.Name is NameSingle nameSingle)
                {
                    resolvedName = MakeNameTriple(nameSingle.Name, null, null);
                }
            }
            if (resolvedName != null)
            {
                name = MakeNameTriple(resolvedName.First, resolvedName.Nick, resolvedName.Last);
            }
            gender = pawn.gender;
            faction = pawn.Faction;
            sourcePawn = pawn;
            biologicalAgeTicks = pawn.ageTracker?.AgeBiologicalTicks ?? 0;
            if (pawn.story != null)
            {
                childhood = pawn.story.Childhood;
                adulthood = pawn.story.Adulthood;
                bodyType = pawn.story.bodyType;
                headType = pawn.story.headType;
                hairDef = pawn.story.hairDef;
                hairColor = pawn.story.HairColor;
                skinColorOverride = pawn.story.skinColorOverride;
                beard = pawn.style?.beardDef;
                bodyTattoo = pawn.style?.BodyTattoo;
                faceTattoo = pawn.style?.FaceTattoo;
                traits = pawn.story.traits?.allTraits
                    .Where(t => t.sourceGene == null && t.suppressedByGene == null)
                    .Select(t => new Trait(t.def, t.Degree, t.ScenForced))
                    .ToList() ?? new List<Trait>();
            }
            skills = pawn.skills?.skills.Select(s => new StoredSkill(s)).ToList() ?? new List<StoredSkill>();
            if (pawn.genes != null)
            {
                // Capture the component genes plus the appearance genes (skin/hair colour, melanin, body
                // shape). The colour genes are clones of vanilla genes, not AndroidGeneDefs, so a plain
                // "is AndroidGeneDef" check would drop them and a reprint would lose the android's colours.
                androidGenes = pawn.genes.GenesListForReading
                    .Where(g => g.def is AndroidGeneDef || g.def.IsAndroidGene()
                        || AndroidGeneUtil.IsSkinColorGene(g.def) || AndroidGeneUtil.IsHairColorGene(g.def)
                        || g.def.bodyType != null || g.def.endogeneCategory == EndogeneCategory.Melanin)
                    .Select(g => g.def)
                    .Distinct()
                    .ToList();
                xenotypeName = pawn.genes.xenotypeName;
                iconDef = pawn.genes.iconDef;
            }
            // Memories / thoughts.
            memories = pawn.needs?.mood?.thoughts?.memories?.Memories?.ToList() ?? new List<Thought_Memory>();
            // Relationships (the overseer link is a live mechanitor bond, never carried by the core).
            if (pawn.relations != null)
            {
                everSeenByPlayer = pawn.relations.everSeenByPlayer;
                relations = pawn.relations.DirectRelations
                    .Where(r => r.def != PawnRelationDefOf.Overseer)
                    .ToList();
            }
            // Ideology.
            if (ModsConfig.IdeologyActive && pawn.ideo != null)
            {
                ideo = pawn.Ideo;
                certainty = pawn.ideo.Certainty;
                previousIdeos = pawn.ideo.PreviousIdeos?.ToList() ?? new List<Ideo>();
                joinTick = pawn.ideo.joinTick;
            }
            // Life records (kills, social interactions, time spent, etc.).
            records = new DefMap<RecordDef, float>();
            if (pawn.records != null)
            {
                foreach (RecordDef def in DefDatabase<RecordDef>.AllDefsListForReading)
                {
                    records[def] = pawn.records.GetValue(def);
                }
            }
        }

        // Applies this stored identity, memory and appearance onto a (freshly grown) body. Used by the
        // printer when reprinting or resurrecting an android from its subcore. Genes are intentionally
        // left to the printer, which regrows the body with the stored android genes.
        public void OverwritePawn(Pawn pawn)
        {
            if (pawn == null || !ContainsData)
            {
                return;
            }
            pawn.Name = MakeNameTriple(name.First, name.Nick, name.Last);
            // A reprint keeps the original android's exact form (gender, skin, face and hair) rather than
            // whatever the fresh body happened to roll.
            pawn.gender = gender;
            if (pawn.story != null)
            {
                if (childhood != null)
                {
                    pawn.story.Childhood = childhood;
                }
                pawn.story.Adulthood = adulthood;
                if (bodyType != null)
                {
                    pawn.story.bodyType = bodyType;
                }
                if (headType != null)
                {
                    pawn.story.headType = headType;
                }
                pawn.story.hairDef = hairDef;
                pawn.story.HairColor = hairColor;
                pawn.story.skinColorOverride = skinColorOverride;
                if (pawn.style != null)
                {
                    pawn.style.beardDef = beard ?? BeardDefOf.NoBeard;
                    if (ModsConfig.IdeologyActive)
                    {
                        pawn.style.BodyTattoo = bodyTattoo ?? TattooDefOf.NoTattoo_Body;
                        pawn.style.FaceTattoo = faceTattoo ?? TattooDefOf.NoTattoo_Face;
                    }
                }
                if (pawn.story.traits != null)
                {
                    foreach (Trait trait in pawn.story.traits.allTraits.ToList())
                    {
                        if (trait.sourceGene == null)
                        {
                            pawn.story.traits.RemoveTrait(trait);
                        }
                    }
                    foreach (Trait trait in traits)
                    {
                        if (!pawn.story.traits.HasTrait(trait.def))
                        {
                            pawn.story.traits.GainTrait(new Trait(trait.def, trait.Degree, trait.ScenForced));
                        }
                    }
                }
            }
            if (pawn.skills != null)
            {
                foreach (StoredSkill stored in skills)
                {
                    SkillRecord record = pawn.skills.GetSkill(stored.def);
                    if (record != null)
                    {
                        record.levelInt = stored.level;
                        record.xpSinceLastLevel = stored.xpSinceLastLevel;
                        record.passion = stored.passion;
                    }
                }
            }
            if (biologicalAgeTicks > 0 && pawn.ageTracker != null)
            {
                pawn.ageTracker.AgeBiologicalTicks = biologicalAgeTicks;
            }
            RestoreMemories(pawn);
            RestoreRelations(pawn);
            RestoreIdeology(pawn);
            RestoreRecords(pawn);
            // The body was generated with a random look; force it to rebuild with the restored gender,
            // face, hair and beard.
            pawn.Drawer?.renderer?.renderTree?.SetDirty();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private void RestoreMemories(Pawn pawn)
        {
            MemoryThoughtHandler handler = pawn.needs?.mood?.thoughts?.memories;
            if (handler == null || memories == null)
            {
                return;
            }
            for (int i = handler.Memories.Count - 1; i >= 0; i--)
            {
                handler.RemoveMemory(handler.Memories[i]);
            }
            foreach (Thought_Memory memory in memories)
            {
                if (memory?.def != null)
                {
                    handler.TryGainMemory(memory, memory.otherPawn);
                }
            }
        }

        private void RestoreRelations(Pawn pawn)
        {
            if (pawn.relations == null || relations == null)
            {
                return;
            }
            pawn.relations.everSeenByPlayer = everSeenByPlayer;
            foreach (DirectPawnRelation relation in relations)
            {
                if (relation?.def == null || relation.otherPawn == null || relation.otherPawn == pawn)
                {
                    continue;
                }
                if (!pawn.relations.DirectRelationExists(relation.def, relation.otherPawn))
                {
                    pawn.relations.AddDirectRelation(relation.def, relation.otherPawn);
                }
            }
            // The persona has moved into this freshly printed body, so strip the old source body's
            // relationships. Otherwise both the old (now dead) body and the reprint linger as separate
            // relations on everyone else - e.g. two "lover" entries, one marked Dead.
            if (sourcePawn != null && sourcePawn != pawn && sourcePawn.relations != null)
            {
                sourcePawn.relations.ClearAllRelations();
            }
        }

        private void RestoreIdeology(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn.ideo == null || ideo == null)
            {
                return;
            }
            pawn.ideo.SetIdeo(ideo);
            pawn.ideo.OffsetCertainty(certainty - pawn.ideo.Certainty);
            pawn.ideo.joinTick = joinTick;
        }

        private void RestoreRecords(Pawn pawn)
        {
            if (records == null || pawn.records == null)
            {
                return;
            }
            foreach (RecordDef def in DefDatabase<RecordDef>.AllDefsListForReading)
            {
                float value = records[def];
                if (value != 0f)
                {
                    pawn.records.AddTo(def, value);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref name, "name");
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Defs.Look(ref childhood, "childhood");
            Scribe_Defs.Look(ref adulthood, "adulthood");
            Scribe_Defs.Look(ref bodyType, "bodyType");
            Scribe_Defs.Look(ref headType, "headType");
            Scribe_Defs.Look(ref hairDef, "hairDef");
            Scribe_Defs.Look(ref beard, "beard");
            Scribe_Defs.Look(ref bodyTattoo, "bodyTattoo");
            Scribe_Defs.Look(ref faceTattoo, "faceTattoo");
            Scribe_Values.Look(ref hairColor, "hairColor", Color.white);
            Scribe_Values.Look(ref skinColorOverride, "skinColorOverride");
            Scribe_Collections.Look(ref traits, "traits", LookMode.Deep);
            Scribe_Collections.Look(ref skills, "skills", LookMode.Deep);
            Scribe_Collections.Look(ref androidGenes, "androidGenes", LookMode.Def);
            Scribe_Values.Look(ref xenotypeName, "xenotypeName");
            Scribe_Defs.Look(ref iconDef, "iconDef");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref sourcePawn, "sourcePawn");
            Scribe_Values.Look(ref biologicalAgeTicks, "biologicalAgeTicks", 0L);
            Scribe_Collections.Look(ref memories, "memories", LookMode.Deep);
            Scribe_Collections.Look(ref relations, "relations", LookMode.Deep);
            Scribe_Values.Look(ref everSeenByPlayer, "everSeenByPlayer");
            Scribe_Deep.Look(ref records, "records");
            if (ModsConfig.IdeologyActive)
            {
                Scribe_References.Look(ref ideo, "ideo");
                Scribe_Values.Look(ref certainty, "certainty");
                Scribe_Collections.Look(ref previousIdeos, "previousIdeos", LookMode.Reference);
                Scribe_Values.Look(ref joinTick, "joinTick");
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                traits ??= new List<Trait>();
                skills ??= new List<StoredSkill>();
                androidGenes ??= new List<GeneDef>();
                memories ??= new List<Thought_Memory>();
                relations ??= new List<DirectPawnRelation>();
                previousIdeos ??= new List<Ideo>();
            }
        }
    }
}
