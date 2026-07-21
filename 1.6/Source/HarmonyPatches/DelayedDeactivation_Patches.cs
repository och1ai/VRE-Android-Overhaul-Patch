using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    internal static class DeactivationReserve
    {
        private static Gene_DelayedDeactivation Of(Pawn pawn)
        {
            if (pawn == null || !pawn.IsAndroid() || pawn.genes == null)
            {
                return null;
            }
            GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_DelayedDeactivation");
            return def == null ? null : pawn.genes.GetGene(def) as Gene_DelayedDeactivation;
        }

        // Applies the reserve to a "this android is finished" verdict. Returns the verdict that should
        // actually stand.
        public static bool Apply(Pawn pawn, bool wouldStop)
        {
            Gene_DelayedDeactivation reserve = Of(pawn);
            if (reserve == null)
            {
                return wouldStop;
            }
            if (!wouldStop)
            {
                // Repaired back above the threshold: the capacitor recharges for next time.
                reserve.ResetCountdown();
                return false;
            }
            // A destroyed head is the one failure no reserve covers - there is nothing left to run on.
            if (pawn.health.hediffSet.GetBrain() == null)
            {
                reserve.ResetCountdown();
                return true;
            }
            // Nor does it cover simply being out of power: the original downs an unpowered android
            // outright, and reserve capacitors don't help a frame with no reactor in it.
            if (!pawn.health.hediffSet.hediffs.OfType<Hediff_AndroidReactor>().Any())
            {
                reserve.ResetCountdown();
                return true;
            }
            return reserve.RunReserveAndShouldDeactivate();
        }
    }

    // The original decides an android's downed state in a prefix that replaces the vanilla method
    // wholesale. Postfixes still run after a skipping prefix, so this reads its verdict and holds the
    // shutdown off while the reserve lasts.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDowned")]
    public static class Pawn_HealthTracker_ShouldBeDowned_Overlay_Patch
    {
        [HarmonyPriority(int.MinValue)]
        [HarmonyAfter("VREAndroidsMod")]
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            __result = DeactivationReserve.Apply(___pawn, __result);
        }
    }

    // Downing is only half the story: a destroyed torso kills outright through ShouldBeDead (the core-part
    // efficiency check), which never goes near ShouldBeDowned. The subroutine is meant to cover a critical
    // failure in ANY region but the head, so the same reserve has to gate death as well - otherwise the
    // android dies instantly the moment its torso is destroyed. Both paths share the one countdown, so it
    // stays up and alive for the full two hours and then goes down and dies together.
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Pawn_HealthTracker_ShouldBeDead_Overlay_Patch
    {
        [HarmonyPriority(int.MinValue)]
        [HarmonyAfter("VREAndroidsMod")]
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result)
            {
                __result = DeactivationReserve.Apply(___pawn, true);
            }
        }
    }

    // A red countdown in the inspect pane while the reserve is burning, so the player can see how long the
    // android has left rather than being surprised by it dropping.
    [HarmonyPatch(typeof(Pawn), "GetInspectString")]
    public static class Pawn_GetInspectString_Overlay_Patch
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!__instance.IsAndroid()
                || !(__instance.genes?.GetGene(DefDatabase<GeneDef>.GetNamedSilentFail("VREA_DelayedDeactivation"))
                    is Gene_DelayedDeactivation reserve)
                || !reserve.CountingDown)
            {
                return;
            }
            string warning = "VREAOverhaul.ShuttingDownIn".Translate(reserve.TicksLeft.ToStringTicksToPeriod())
                .CapitalizeFirst().Colorize(ColorLibrary.RedReadable);
            __result = __result.NullOrEmpty() ? warning : __result + "\n" + warning;
        }
    }
}
