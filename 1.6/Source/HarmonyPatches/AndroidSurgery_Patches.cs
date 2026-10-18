using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android regenerates (see AndroidRepair), so the original's surgery list - install and remove entries
    // for every limb and organ - only fills the health tab with options nobody needs, and the part items they
    // install are legacy now. What is left is the power core, the one thing repair never rebuilds:
    //  - installing a part is refused unless it is the reactor, and the reactor only on reactor-powered
    //    androids (a battery is chosen by gene, not swapped by surgery);
    //  - removing a part is offered only on the reactor.
    [HarmonyPatch]
    public static class Recipe_InstallAndroidPart_AvailableOnNow_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Recipe_InstallAndroidPart), nameof(RecipeWorker.AvailableOnNow));
            foreach (System.Type type in typeof(Recipe_InstallAndroidPart).AllSubclasses())
            {
                MethodInfo method = type.GetMethod(nameof(RecipeWorker.AvailableOnNow), AccessTools.allDeclared);
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        public static void Postfix(RecipeWorker __instance, Thing thing, ref bool __result)
        {
            if (!__result || !(thing is Pawn pawn) || !pawn.IsAndroid())
            {
                return;
            }
            if (!(__instance is Recipe_InstallReactor) || pawn.ActivePowerGene()?.def != OverhaulDefOf.ReactorPowered)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_RemoveArtificialBodyPart), nameof(Recipe_RemoveArtificialBodyPart.GetPartsToApplyOn))]
    public static class Recipe_RemoveArtificialBodyPart_GetPartsToApplyOn_Patch
    {
        public static void Postfix(Pawn pawn, ref IEnumerable<BodyPartRecord> __result)
        {
            __result = __result.Where(part =>
                pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h is Hediff_AndroidReactor));
        }
    }
}
