using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // What actually kills an android.
    //
    // The original makes an android with an intact brain effectively immortal: its ShouldBeDead postfix
    // returns false for ANY death cause, and its required-capacity postfix nulls EVERY missing capacity.
    // Between them, nothing short of decapitation can end an android, which leaves the whole
    // destroyed/resurrect/reprint chain with almost no way to ever start.
    //
    // The overhaul's model is the fork's: an android dies like anyone else - a destroyed vital organ, a
    // destroyed torso, bleeding all the way out - and only two vanilla death causes are lifted, because
    // both describe a body that is merely broken rather than gone:
    //
    //   - the accumulated lethal damage threshold, which is a statistical "enough is enough" rule for
    //     flesh and has no meaning for a chassis that can be repaired part by part;
    //   - loss of CONSCIOUSNESS while the brain is still there, which for an android is being switched
    //     off, not dying - it can be recharged, repaired or hauled to a stand.
    //
    // Every other required capacity still kills. That death is then usually a *destruction* rather than a
    // kill, because the subcore survives it (AndroidDeath_Patches.cs), which is exactly what leaves a body
    // to resurrect at the assembler or a subcore to pull out and reprint from.
    //
    // Both of these restate a rule the original already had, with the exception added, so its two patches
    // are unpatched in VREAndroidsOverhaulMod.UnpatchOriginal first. Restating beats fighting for
    // priority: the original's postfixes both run at int.MinValue and would simply win.
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromLethalDamageThreshold))]
    public static class Pawn_HealthTracker_ShouldBeDeadFromLethalDamageThreshold_Overlay_Patch
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result && ___pawn.IsAndroid())
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromRequiredCapacity))]
    public static class Pawn_HealthTracker_ShouldBeDeadFromRequiredCapacity_Overlay_Patch
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn ___pawn, ref PawnCapacityDef __result)
        {
            if (__result == PawnCapacityDefOf.Consciousness && ___pawn.IsAndroid()
                && ___pawn.health.hediffSet.GetBrain() != null)
            {
                __result = null;
            }
        }
    }
}
