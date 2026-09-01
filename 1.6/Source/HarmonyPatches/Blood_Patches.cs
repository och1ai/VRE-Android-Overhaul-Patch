using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Three circulatory options instead of one. The original only ever intercepts bleeding for its own
    // neutroamine gene; the other two both need code. A bloodless frame must never bleed, the coagulation
    // subroutine slows bleeding down, and a hemogenic one has to have its bleed rate computed for it,
    // because vanilla's own rate is always zero on an android (see Hediff_Injury_BleedRate_Patch).
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

    // Per-wound bleeding: zero on a dry frame, computed for a hemogenic one, and slowed by the coagulation
    // subroutine. A postfix, so the original's own neutroamine handling (which computes its own rate in a
    // prefix) still runs first and is scaled correctly rather than being overwritten.
    [HarmonyPatch(typeof(Hediff_Injury), "BleedRate", MethodType.Getter)]
    public static class Hediff_Injury_BleedRate_Patch
    {
        // How much the coagulation subroutine cuts a wound's bleed rate.
        public const float CoagulationBleedFactor = 0.35f;

        [HarmonyPriority(int.MinValue)]
        public static void Postfix(Hediff_Injury __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (BloodTypes.IsBloodless(pawn))
            {
                __result = 0f;
                return;
            }
            if (BloodTypes.IsHemogenic(pawn))
            {
                __result = HemogenicBleedRate(__instance);
            }
            if (__result > 0f && BloodTypes.HasCoagulation(pawn))
            {
                __result *= CoagulationBleedFactor;
            }
        }

        // A hemogenic android does NOT simply fall through to working vanilla logic, which is what this
        // file first assumed. Vanilla zeroes a wound's bleed rate whenever the injured part carries a
        // directly-added part whose hediff is not flagged organicAddedBodypart - and EVERY part of an
        // android is exactly that: the original's Gene_SyntheticBody installs a Hediff_AndroidPart on all
        // of them, off VREA_AndroidBodyPartBase, which sets addedPartProps and leaves organicAddedBodypart
        // at its default false.
        //
        // So a hemogenic android splattered blood - that filth comes from the damage worker, not from
        // here - while its bleed rate stayed 0, and with it the health tab's bleeding line, the
        // bleeding-to-death timer and any blood loss at all.
        //
        // The original already had to solve this for its own neutroamine androids, with a replacement rate
        // that skips the added-part check. Call that same public helper instead of restating the formula:
        // hemogenic then bleeds at exactly the rate neutroamine leaks, and any retune of it carries over.
        // It drops two vanilla guards along the way - the solid-part check and BleedingStoppedDueToAge -
        // which is already true of every neutroamine android in the original, so the two blood types stay
        // comparable. (The solid-part one is moot regardless: android parts declare solid=false.)
        private static float HemogenicBleedRate(Hediff_Injury injury)
        {
            return VREAndroids.Hediff_Injury_BleedRate_Patch.BleedRate(injury);
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
