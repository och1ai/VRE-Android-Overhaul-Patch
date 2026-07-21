using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Psylinks and the golden cube, both keyed on awakening.
    //
    //  - A base android is psychically dull and machine-minded: no psylink, and the cube's pull means
    //    nothing to a subcore that cannot want things.
    //  - An awakened android has shed the dullness (see Patches/PsychicSensitivity.xml) and wants things of
    //    its own, so it may hold a psylink AND it is NOT immune to the cube. Awakening is meant to cost
    //    something, not just grant.
    internal static class PsychicAwakening
    {
        private static HediffDef psychicAmplifier;
        private static HediffDef cubeInterest, cubeWithdrawal, cubeComa;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            psychicAmplifier = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicAmplifier");
            cubeInterest = DefDatabase<HediffDef>.GetNamedSilentFail("CubeInterest");
            cubeWithdrawal = DefDatabase<HediffDef>.GetNamedSilentFail("CubeWithdrawal");
            cubeComa = DefDatabase<HediffDef>.GetNamedSilentFail("CubeComa");
        }

        // An android that has not awakened: the state both blocks below share.
        private static bool UnawakenedAndroid(Pawn pawn)
        {
            return pawn != null && pawn.IsAndroid() && !pawn.IsAwakened();
        }

        public static bool BlocksPsylink(Pawn pawn, HediffDef def)
        {
            Resolve();
            return def != null && def == psychicAmplifier && UnawakenedAndroid(pawn);
        }

        public static bool BlocksCube(Pawn pawn, HediffDef def)
        {
            Resolve();
            if (def == null || (def != cubeInterest && def != cubeWithdrawal && def != cubeComa))
            {
                return false;
            }
            return UnawakenedAndroid(pawn);
        }
    }

    // Runs after the original's own android hediff handler (which is priority int.MaxValue), so by the time
    // this is reached the amplifier is no longer being rejected by the settings blocklist.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new Type[]
    {
        typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult)
    })]
    [HarmonyAfter("VREAndroidsMod")]
    [HarmonyPriority(Priority.Low)]
    public static class Pawn_HealthTracker_AddHediff_Overlay_Patch
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff)
        {
            if (hediff == null)
            {
                return true;
            }
            return !PsychicAwakening.BlocksPsylink(___pawn, hediff.def)
                && !PsychicAwakening.BlocksCube(___pawn, hediff.def);
        }
    }

    // Same gate on the surgery side, so a psylink neuroformer cannot be queued on an android that would
    // silently refuse the hediff. The original's postfix decides availability for androids; this one only
    // takes availability away again.
    [HarmonyPatch(typeof(RecipeWorker), nameof(RecipeWorker.AvailableOnNow))]
    [HarmonyAfter("VREAndroidsMod")]
    [HarmonyPriority(Priority.Low)]
    public static class RecipeWorker_AvailableOnNow_Overlay_Patch
    {
        public static void Postfix(RecipeWorker __instance, Thing thing, ref bool __result)
        {
            if (__result && thing is Pawn pawn
                && PsychicAwakening.BlocksPsylink(pawn, __instance.recipe?.addsHediff))
            {
                __result = false;
            }
        }
    }

    // Gaining psylink levels (rituals, neuroformers, bestowing) goes through ChangeLevel, which the
    // original refuses outright for androids. Its prefix is replaced rather than fought: unpatching just
    // that one method lets the same guard be reinstated with the awakening exception.
    [HarmonyPatch]
    public static class Hediff_Psylink_ChangeLevel_Overlay_Patch
    {
        public static MethodBase Target()
        {
            return AccessTools.Method(typeof(Hediff_Psylink), "ChangeLevel", new Type[] { typeof(int) });
        }

        public static bool Prepare()
        {
            return Target() != null;
        }

        public static MethodBase TargetMethod()
        {
            return Target();
        }

        public static bool Prefix(Hediff_Psylink __instance)
        {
            return !PsychicAwakening.BlocksPsylink(__instance.pawn, __instance.def);
        }
    }
}
