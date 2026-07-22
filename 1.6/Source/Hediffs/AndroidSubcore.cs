using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The subcore installed in every android's brain: a shielded core holding what the android IS, the way
    // a Westworld host's brain does. It is never shown in the health list - it is not a wound or an implant
    // the player manages - and sitting in the brain is exactly what makes losing the head permanent.
    //
    // Its whole point is the distinction between DESTROYED and KILLED. While the subcore survives, an
    // android's "death" is a recoverable destruction: no grief, no tales, no funeral, relationships kept.
    // Losing the subcore - the head or torso taken off, or the corpse destroyed - is the real, permanent
    // death, and only then does the colony mourn.
    public class Hediff_AndroidSubcore : HediffWithComps
    {
        public AndroidPersonaData personaData = new AndroidPersonaData();

        public override bool ShouldRemove => false;

        public override bool Visible => false;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            personaData.CopyFromPawn(pawn);
            base.Notify_PawnDied(dinfo, culprit);
        }

        public override void Notify_PawnKilled()
        {
            personaData.CopyFromPawn(pawn);
            base.Notify_PawnKilled();
        }

        // Pops the subcore out as an item carrying the stored persona, and removes the implant so the same
        // body cannot yield a second core. Returns the spawned item (null if it could not be placed).
        public AndroidSubcore SpawnSubcore(ThingPlaceMode placeMode = ThingPlaceMode.Near)
        {
            if (!personaData.ContainsData)
            {
                personaData.CopyFromPawn(pawn);
            }
            ThingDef def = SubcoreDefOf.SubcoreItem;
            if (def == null)
            {
                return null;
            }
            AndroidSubcore subcore = (AndroidSubcore)ThingMaker.MakeThing(def);
            subcore.personaData = personaData;
            Pawn corePawn = pawn;
            Map map = corePawn.MapHeld;
            IntVec3 pos = corePawn.PositionHeld;
            corePawn.health.RemoveHediff(this);
            if (map != null && pos.IsValid)
            {
                GenPlace.TryPlaceThing(subcore, pos, map, placeMode);
            }
            // The persona now lives in the popped subcore item, so the body left behind is an empty,
            // identity-less husk: disown it from the colony so it no longer counts as a colonist (it drops
            // off the colonist bar and the empty shell is not grieved for).
            if (corePawn.Faction != null)
            {
                corePawn.SetFactionDirect(null);
                Find.ColonistBar?.MarkColonistsDirty();
            }
            // Pulling the core tears the head open, spraying the android's fluid (nothing if bloodless).
            BlowOffHead(corePawn);
            return subcore;
        }

        // The blood an android sprays when its core is pulled: none if it runs dry, neutroamine if it runs
        // on neutroamine, otherwise ordinary red.
        private static ThingDef BloodFor(Pawn pawn)
        {
            GeneDef bloodless = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Bloodless");
            if (bloodless != null && pawn.HasActiveGene(bloodless))
            {
                return null;
            }
            GeneDef neutro = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_NeutroCirculation");
            if (neutro != null && pawn.HasActiveGene(neutro))
            {
                return DefDatabase<ThingDef>.GetNamedSilentFail("VREA_Filth_Neutroamine") ?? ThingDefOf.Filth_Blood;
            }
            return ThingDefOf.Filth_Blood;
        }

        private static void BlowOffHead(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }
            Map map = pawn.MapHeld;
            IntVec3 pos = pawn.PositionHeld;
            ThingDef blood = BloodFor(pawn);
            if (blood != null && map != null && pos.IsValid)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pos, 1.6f, true))
                {
                    if (cell.InBounds(map) && Rand.Chance(0.6f))
                    {
                        FilthMaker.TryMakeFilth(cell, map, blood, pawn.LabelShort, Rand.RangeInclusive(1, 3));
                    }
                }
            }
            BodyPartRecord head = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def == BodyPartDefOf.Head);
            if (head != null)
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, head);
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref personaData, "personaData");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && personaData == null)
            {
                personaData = new AndroidPersonaData();
            }
        }
    }

    // Defs this subsystem adds, resolved by name so nothing depends on a DefOf the original does not have.
    [StaticConstructorOnStartup]
    public static class SubcoreDefOf
    {
        public static readonly ThingDef SubcoreItem = DefDatabase<ThingDef>.GetNamedSilentFail("VREA_AndroidSubcore");
        public static readonly JobDef ExtractSubcoreJob = DefDatabase<JobDef>.GetNamedSilentFail("VREA_ExtractSubcore");
        public static readonly DesignationDef ExtractSubcoreDesignation =
            DefDatabase<DesignationDef>.GetNamedSilentFail("VREA_ExtractSubcoreDesignation");
    }

    // Gene classification helpers the fork keeps on its Utils; the overlay cannot add to the original's.
    public static class AndroidGeneUtil
    {
        public static bool IsSkinColorGene(GeneDef g) => g.skinColorBase.HasValue || g.skinColorOverride.HasValue;

        public static bool IsHairColorGene(GeneDef g) => g.hairColorOverride.HasValue;

        public static bool IsBodyTypeGene(GeneDef g) => g.bodyType.HasValue;
    }

    public static class AndroidDeath
    {
        // Set while forcing the REAL death of an android whose subcore is gone, so the normal
        // "recoverable destruction" suppression is bypassed for that one moment.
        public static bool forcingRealDeath;

        private static HediffDef subcoreDef;
        private static bool resolved;

        public static HediffDef SubcoreDef
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    subcoreDef = DefDatabase<HediffDef>.GetNamedSilentFail("VREA_AndroidSubcoreImplant");
                }
                return subcoreDef;
            }
        }

        public static bool HasSubcore(this Pawn pawn, out Hediff_AndroidSubcore subcore)
        {
            subcore = pawn?.health?.hediffSet?.hediffs.OfType<Hediff_AndroidSubcore>().FirstOrDefault();
            return subcore != null;
        }

        // Set true while a deliberate subcore extraction is destroying an android, so the death letter
        // stays quiet - the player ordered the extraction and does not need a "destroyed" notice.
        public static bool extractingSubcore;

        // The permanent death of an android known only by a stored subcore (no body): if the body it came
        // from still exists, run the full grief through it so friends and lovers actually mourn; otherwise
        // just tell the player their android is gone for good.
        public static void RealDeathFromData(AndroidPersonaData data)
        {
            if (data == null || !data.ContainsData)
            {
                return;
            }
            if (data.sourcePawn != null && !data.sourcePawn.Discarded && data.sourcePawn.relations != null)
            {
                RealDeath(data.sourcePawn);
                return;
            }
            if (data.faction != Faction.OfPlayer)
            {
                return;
            }
            Find.LetterStack.ReceiveLetter("VREAOverhaul.AndroidKilled".Translate() + ": " + data.ShortName,
                "VREAOverhaul.AndroidKilledNoBodyDesc".Translate(data.name.ToStringFull), LetterDefOf.NegativeEvent);
        }

        public static void EnsureSubcore(Pawn pawn)
        {
            if (pawn == null || !pawn.IsAndroid() || pawn.health == null || SubcoreDef == null)
            {
                return;
            }
            if (HasSubcore(pawn, out _))
            {
                return;
            }
            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            if (brain != null)
            {
                pawn.health.AddHediff(SubcoreDef, brain);
            }
        }

        // The permanent death of an android whose subcore has just been destroyed: friends and lovers now
        // grieve as for any real death, and the player is told it was killed for good. Pass sendLetter:
        // false when a kill notice was already shown for this death, so it is not doubled.
        public static void RealDeath(Pawn pawn, bool sendLetter = true)
        {
            if (pawn == null)
            {
                return;
            }
            try
            {
                forcingRealDeath = true;
                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(pawn, null, PawnDiedOrDownedThoughtsKind.Died);
            }
            finally
            {
                forcingRealDeath = false;
            }
            if (sendLetter && (pawn.Faction == Faction.OfPlayer || PawnUtility.ShouldSendNotificationAbout(pawn)))
            {
                Find.LetterStack.ReceiveLetter("VREAOverhaul.AndroidKilled".Translate() + ": " + pawn.LabelShortCap,
                    "VREAOverhaul.AndroidKilledDesc".Translate(pawn.Named("PAWN")), LetterDefOf.NegativeEvent);
            }
        }
    }

    // Every android carries a subcore. Installed on spawn rather than from a gene so it also reaches
    // androids in saves made before this existed.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_Subcore_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            AndroidDeath.EnsureSubcore(__instance);
        }
    }
}
