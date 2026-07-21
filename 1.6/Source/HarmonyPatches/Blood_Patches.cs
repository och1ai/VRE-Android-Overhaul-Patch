using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Three circulatory options instead of one. The original only ever intercepts bleeding for its own
    // neutroamine gene, so a hemogenic android needs no patching at all - it falls through to the vanilla
    // logic and bleeds red like anyone else. Only the two ends of the range need code: a bloodless frame
    // that must never bleed, and the coagulation subroutine that slows bleeding down.
    internal static class BloodTypes
    {
        private static GeneDef bloodless, coagulation, hemogenic;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            bloodless = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Bloodless");
            coagulation = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Coagulation");
            hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_NormalBlood");
        }

        public static bool IsBloodless(Pawn pawn)
        {
            Resolve();
            return bloodless != null && pawn != null && pawn.HasActiveGene(bloodless);
        }

        public static bool HasCoagulation(Pawn pawn)
        {
            Resolve();
            return coagulation != null && pawn != null && pawn.HasActiveGene(coagulation);
        }

        public static bool IsHemogenic(Pawn pawn)
        {
            Resolve();
            return hemogenic != null && pawn != null && pawn.HasActiveGene(hemogenic);
        }
    }

    // CanBleed is the vanilla gate behind the whole-body bleed rate and the "bleeding to death" timer.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "CanBleed", MethodType.Getter)]
    public static class Pawn_HealthTracker_CanBleed_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result && BloodTypes.IsBloodless(___pawn))
            {
                __result = false;
            }
        }
    }

    // The authoritative whole-body figure, belt-and-braces with CanBleed.
    [HarmonyPatch(typeof(HediffSet), "CalculateBleedRate")]
    public static class HediffSet_CalculateBleedRate_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(HediffSet __instance, ref float __result)
        {
            if (__result > 0f && BloodTypes.IsBloodless(__instance.pawn))
            {
                __result = 0f;
            }
        }
    }

    // Per-wound bleeding: zero on a dry frame, and slowed by the coagulation subroutine. A postfix, so the
    // original's own neutroamine handling (which computes its own rate in a prefix) still runs first and
    // is scaled correctly rather than being overwritten.
    [HarmonyPatch(typeof(Hediff_Injury), "BleedRate", MethodType.Getter)]
    public static class Hediff_Injury_BleedRate_Patch
    {
        // How much the coagulation subroutine cuts a wound's bleed rate.
        public const float CoagulationBleedFactor = 0.35f;

        [HarmonyPriority(int.MinValue)]
        public static void Postfix(Hediff_Injury __instance, ref float __result)
        {
            if (__result <= 0f)
            {
                return;
            }
            if (BloodTypes.IsBloodless(__instance.pawn))
            {
                __result = 0f;
            }
            else if (BloodTypes.HasCoagulation(__instance.pawn))
            {
                __result *= CoagulationBleedFactor;
            }
        }
    }

    // Blood loss accrual. The original already redirects this into neutro loss for a neutroamine android;
    // a bloodless one must accrue nothing at all.
    [HarmonyPatch(typeof(HediffGiver_Bleeding), "OnIntervalPassed")]
    public static class HediffGiver_Bleeding_OnIntervalPassed_Patch
    {
        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn pawn)
        {
            return !BloodTypes.IsBloodless(pawn);
        }
    }

    // A hemogenic android carries ordinary red blood, so the vanilla blood transfusion and hemogen
    // extraction surgeries work on it just like on a person. The original refuses every "administer
    // ingestible"-style recipe on androids, so this re-allows exactly those two.
    [HarmonyPatch(typeof(RecipeWorker), nameof(RecipeWorker.AvailableOnNow))]
    [HarmonyAfter("VREAndroidsMod")]
    [HarmonyPriority(Priority.Low)]
    public static class RecipeWorker_AvailableOnNow_Blood_Patch
    {
        public static void Postfix(RecipeWorker __instance, Thing thing, ref bool __result)
        {
            if (__result || !(thing is Pawn pawn) || !BloodTypes.IsHemogenic(pawn))
            {
                return;
            }
            string defName = __instance.recipe?.defName;
            if (defName == "BloodTransfusion" || defName == "ExtractHemogenPack")
            {
                __result = true;
            }
        }
    }
}
