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
        public override bool ShouldRemove => false;

        public override bool Visible => false;
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

        public static bool HasSubcore(Pawn pawn, out Hediff subcore)
        {
            subcore = pawn?.health?.hediffSet?.hediffs.OfType<Hediff_AndroidSubcore>().FirstOrDefault();
            return subcore != null;
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
