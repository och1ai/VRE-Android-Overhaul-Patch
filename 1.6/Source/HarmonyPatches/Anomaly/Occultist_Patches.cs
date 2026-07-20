using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The "occultist protocols" subroutine is the ONLY thing that lets an android touch the anomalous:
    //  - take part in psychic rituals (including being the subject of an infused death refusal), and
    //  - capture, contain, feed/tend and study entities (research).
    // Every android without the subroutine is blocked from all of the above. These patches are safe to
    // load without the Anomaly DLC (the types exist in the base assembly); they simply never fire, since
    // the guard below short-circuits when Anomaly is inactive and the gene does not exist.
    internal static class OccultistUtil
    {
        private static GeneDef gene;
        private static bool resolved;

        private static GeneDef Gene
        {
            get
            {
                if (!resolved)
                {
                    gene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Occultist");
                    resolved = true;
                }
                return gene;
            }
        }

        // True when this pawn is an android that may NOT do anomalous work / rituals.
        public static bool BlockedAndroid(Pawn pawn)
        {
            if (!ModsConfig.AnomalyActive || pawn == null || !pawn.IsAndroid())
            {
                return false;
            }
            return Gene == null || !pawn.HasActiveGene(Gene);
        }
    }

    // Psychic rituals: reject any android that lacks the occultist subroutine from every role - invoker,
    // participant, target, and the infused-death-refusal subject alike. On top of that, NO android (not
    // even an occultist or an awakened one) may ever be the subject of a psychophagy or chronophagy - there
    // is no organic mind or lifespan to devour.
    [HarmonyPatch]
    public static class PsychicRitualRoleDef_PawnCanDo_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PsychicRitualRoleDef), "PawnCanDo", new Type[]
            {
                typeof(PsychicRitualRoleDef.Context), typeof(Pawn), typeof(TargetInfo),
                typeof(PsychicRitualRoleDef.Reason).MakeByRefType()
            });
        }

        public static void Postfix(PsychicRitualRoleDef __instance, Pawn pawn, ref bool __result)
        {
            if (!__result || pawn == null || !pawn.IsAndroid())
            {
                return;
            }
            if (__instance.defName == "PsychophagyTarget" || __instance.defName == "ChronophagyTarget"
                || OccultistUtil.BlockedAndroid(pawn))
            {
                __result = false;
            }
        }
    }

    // Studying / researching entities (both the normal study interaction and the dark study one).
    [HarmonyPatch(typeof(WorkGiver_StudyBase), nameof(WorkGiver_StudyBase.PotentialWorkThingsGlobal))]
    public static class WorkGiver_StudyBase_PotentialWorkThingsGlobal_Patch
    {
        public static void Postfix(Pawn pawn, ref IEnumerable<Thing> __result)
        {
            if (OccultistUtil.BlockedAndroid(pawn))
            {
                __result = Enumerable.Empty<Thing>();
            }
        }
    }

    // Capturing an entity and hauling it onto a holding platform (containment).
    [HarmonyPatch(typeof(WorkGiver_TakeEntityToHoldingPlatform), nameof(WorkGiver_TakeEntityToHoldingPlatform.HasJobOnThing))]
    public static class WorkGiver_TakeEntityToHoldingPlatform_HasJobOnThing_Patch
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && OccultistUtil.BlockedAndroid(pawn))
            {
                __result = false;
            }
        }
    }

    // Feeding / tending / executing entities on a holding platform (WorkGiver_EntityOnPlatform base,
    // shared by tend and execute).
    [HarmonyPatch(typeof(WorkGiver_EntityOnPlatform), nameof(WorkGiver_EntityOnPlatform.ShouldSkip))]
    public static class WorkGiver_EntityOnPlatform_ShouldSkip_Patch
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && OccultistUtil.BlockedAndroid(pawn))
            {
                __result = true;
            }
        }
    }

    // Suppressing a contained entity's activity level. This is a warden job on the entity, so it slipped
    // past the entity-workgiver gates above - a non-occultist android could still walk up and suppress an
    // anomaly. Block it too.
    [HarmonyPatch(typeof(WorkGiver_Warden_SuppressActivity), nameof(WorkGiver_Warden_SuppressActivity.JobOnThing))]
    public static class WorkGiver_Warden_SuppressActivity_JobOnThing_Patch
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && OccultistUtil.BlockedAndroid(pawn))
            {
                __result = null;
            }
        }
    }
}
