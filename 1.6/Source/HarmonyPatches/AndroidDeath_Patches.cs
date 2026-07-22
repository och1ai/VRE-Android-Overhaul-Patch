using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android whose subcore survives has been DESTROYED, not killed: the chassis is wreckage but the
    // person is still in there. Everything the game does to mark a death - grief, tales, the letter,
    // severing relationships, funeral rites - is held back until the subcore itself is gone.

    // A brain hit alone leaves a recoverable destruction. Losing the whole HEAD (decapitation) or the
    // TORSO takes the shielded subcore with it, so strip it before the death notice runs and the death
    // reads as a true, irrecoverable kill.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_Patch
    {
        public static void Prefix(Pawn __instance)
        {
            if (AndroidDeath.forcingRealDeath || __instance == null || !__instance.IsAndroid())
            {
                return;
            }
            if (AndroidDeath.HasSubcore(__instance, out Hediff_AndroidSubcore subcore) && HeadOrTorsoDestroyed(__instance))
            {
                __instance.health.RemoveHediff(subcore);
            }
        }

        private static bool HeadOrTorsoDestroyed(Pawn pawn)
        {
            bool headPresent = false;
            bool torsoPresent = false;
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == "Head")
                {
                    headPresent = true;
                }
                else if (part.def == BodyPartDefOf.Torso)
                {
                    torsoPresent = true;
                }
            }
            return !headPresent || !torsoPresent;
        }

        public static void Postfix(Pawn __instance)
        {
            if (AndroidDeath.forcingRealDeath || __instance == null || !__instance.IsAndroid())
            {
                return;
            }
            if (!AndroidDeath.HasSubcore(__instance, out _))
            {
                // NotifyPlayerOfKilled already posted the "android killed" letter for this death, so only
                // the grief is added here - not a second letter.
                AndroidDeath.RealDeath(__instance, sendLetter: false);
            }
        }
    }

    // Destroying the corpse - rotting away, shredded, burned - of an android that still held its subcore
    // is the moment it is really and permanently dead, so the grief and the kill notice fire now rather
    // than at the recoverable destruction earlier.
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.Destroy))]
    public static class Corpse_Destroy_Patch
    {
        public static void Prefix(Corpse __instance)
        {
            Pawn inner = __instance.InnerPawn;
            if (inner != null && inner.IsAndroid() && AndroidDeath.HasSubcore(inner, out _))
            {
                AndroidDeath.RealDeath(inner);
            }
        }
    }

    // The death letter itself: a mechanoid-style destruction notice while the subcore is intact, the grim
    // permanent one once it is gone.
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.NotifyPlayerOfKilled))]
    public static class Pawn_HealthTracker_NotifyPlayerOfKilled_Patch
    {
        public static bool Prefix(Pawn ___pawn)
        {
            if (___pawn == null || !___pawn.IsAndroid())
            {
                return true;
            }
            bool hasSubcore = AndroidDeath.HasSubcore(___pawn, out _);
            TaggedString label = (hasSubcore ? "VREAOverhaul.AndroidDestroyed" : "VREAOverhaul.AndroidKilled")
                .Translate() + ": " + ___pawn.LabelShortCap;
            TaggedString text = (hasSubcore ? "VREAOverhaul.AndroidDestroyedDesc" : "VREAOverhaul.AndroidKilledDesc")
                .Translate(___pawn.Named("PAWN"));
            LetterDef letterDef = hasSubcore ? LetterDefOf.NeutralEvent : LetterDefOf.NegativeEvent;
            if (___pawn.Faction == Faction.OfPlayer)
            {
                Find.LetterStack.ReceiveLetter(label, text, letterDef, ___pawn);
            }
            else
            {
                Messages.Message(text, ___pawn, MessageTypeDefOf.PawnDeath);
            }
            return false;
        }
    }

    // No grief for a destruction the colony can undo - no "my friend died", no witnessed-death trauma.
    // Only RealDeath, which sets the flag, gets through.
    [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), "TryGiveThoughts",
        new System.Type[] { typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind) })]
    public static class PawnDiedOrDownedThoughtsUtility_TryGiveThoughts_Patch
    {
        public static bool Prefix(Pawn victim)
        {
            return AndroidDeath.forcingRealDeath || victim == null || !victim.IsAndroid();
        }
    }

    // Relationships are kept through a recoverable destruction, so a resurrected android walks back into
    // the same friendships and marriages it left.
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "Notify_PawnKilled")]
    public static class Pawn_RelationsTracker_Notify_PawnKilled_Patch
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return AndroidDeath.forcingRealDeath || ___pawn == null || !___pawn.IsAndroid()
                || !AndroidDeath.HasSubcore(___pawn, out _);
        }
    }

    // No death tales either. Most importantly this skips the "killed a colonist" tale, so whoever took the
    // android down is not socially judged for wrecking what is really a repairable machine.
    [HarmonyPatch(typeof(TaleUtility), nameof(TaleUtility.Notify_PawnDied))]
    public static class TaleUtility_Notify_PawnDied_Patch
    {
        public static bool Prefix(Pawn victim)
        {
            return AndroidDeath.forcingRealDeath || victim == null || !victim.IsAndroid()
                || !AndroidDeath.HasSubcore(victim, out _);
        }
    }

    // Funerals and eulogies are for the dead, and a merely destroyed android is not dead - holding a
    // funeral for one that is about to walk back out of the assembler makes no sense. Death rites are
    // limited to androids truly killed (subcore gone) that actually followed an ideoligion.
    internal static class AndroidFuneral
    {
        public static bool ShouldGetDeathRites(Pawn pawn)
        {
            if (pawn == null || !pawn.IsAndroid())
            {
                return true;
            }
            if (AndroidDeath.HasSubcore(pawn, out _))
            {
                return false;
            }
            return pawn.Ideo != null;
        }
    }

    [HarmonyPatch(typeof(RitualObligationTrigger_MemberDied),
        nameof(RitualObligationTrigger_MemberDied.Notify_MemberDied))]
    public static class RitualObligationTrigger_MemberDied_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn p)
        {
            return AndroidFuneral.ShouldGetDeathRites(p);
        }
    }

    [HarmonyPatch(typeof(RitualObligationTrigger_MemberCorpseDestroyed),
        nameof(RitualObligationTrigger_MemberCorpseDestroyed.Notify_MemberCorpseDestroyed))]
    public static class RitualObligationTrigger_MemberCorpseDestroyed_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn p)
        {
            return AndroidFuneral.ShouldGetDeathRites(p);
        }
    }
}
