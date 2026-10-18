using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace VREAndroidsOverhaul
{
    // The colonist bar tags anyone in a fight with a red "attacking" icon. For an android acting as a mech -
    // escorting or hunting on its mechanitor's orders - that reads like a colonist who has gone into a
    // self-defence fight, which is not what is happening, so it is left off. DrawIcons asks for the current
    // job in several places; every one of them gets the combat job masked out for such an android (the rest
    // of the method then behaves as for a pawn that is simply busy), and only while that bar slot is drawn.
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawIcons")]
    public static class ColonistBarColonistDrawer_DrawIcons_AttackIcon_Patch
    {
        [System.ThreadStatic] private static Pawn drawing;

        public static void Prefix(Pawn colonist)
        {
            drawing = colonist;
        }

        public static void Finalizer()
        {
            drawing = null;
        }

        public static Job MaskCombatJob(Job job)
        {
            if (job == null || drawing == null)
            {
                return job;
            }
            JobDef def = job.def;
            if ((def == JobDefOf.AttackMelee || def == JobDefOf.AttackStatic || def == JobDefOf.Wait_Combat)
                && MechOversightUtil.IsOversightAndroid(drawing) && MechOversightUtil.FightsOnOrders(drawing))
            {
                return null;
            }
            return job;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.CurJob));
            MethodInfo mask = AccessTools.Method(typeof(ColonistBarColonistDrawer_DrawIcons_AttackIcon_Patch), nameof(MaskCombatJob));
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(getter))
                {
                    yield return new CodeInstruction(OpCodes.Call, mask);
                }
            }
        }
    }

    // The same thing floats over the pawn on the map: the red "colonist attacking" mote that vanilla throws
    // when a colonist with a configurable hostility response starts a fight on their own initiative. An
    // android defending its mechanitor is following orders, not reacting, so no mote.
    [HarmonyPatch(typeof(MoteMaker), nameof(MoteMaker.MakeColonistActionOverlay))]
    public static class MoteMaker_MakeColonistActionOverlay_AttackMote_Patch
    {
        public static bool Prefix(Pawn pawn, ThingDef moteDef)
        {
            return !(moteDef == ThingDefOf.Mote_ColonistAttacking
                && MechOversightUtil.IsOversightAndroid(pawn) && MechOversightUtil.FightsOnOrders(pawn));
        }
    }
}
