using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android resting on an android stand is drawn upright, which the original does by overriding
    // GetPosture while a pawn is being rendered. Two things go wrong with how it flags "being rendered":
    //  - it only sets the flag around RenderPawnAt, but in 1.6 the body's pose is worked out earlier, in
    //    ParallelPreRenderPawnAt, where the flag is off. A resting android (lying down, not charging) then
    //    gets a lying pose there and an upright one at draw time, and the two disagree - it comes out
    //    flipped/inverted on the stand;
    //  - the flag is a plain static, and the pre-draw pass runs on worker threads for many pawns at once, so
    //    one pawn's flag flips another's mid-draw (the android flickers between standing and lying).
    // This keeps its own per-thread flag, covers both entry points, and answers for the posture first.
    [HarmonyPatch(typeof(PawnUtility), "GetPosture")]
    [HarmonyBefore(VREAndroidsOverhaulMod.OriginalHarmonyId)]
    public static class PawnUtility_GetPosture_StandPosture_Patch
    {
        [ThreadStatic] public static bool rendering;

        [HarmonyPriority(int.MaxValue)]
        public static bool Prefix(Pawn p, ref PawnPosture __result)
        {
            if (!rendering || p?.pather == null || p.pather.Moving || !p.Spawned)
            {
                return true;
            }
            List<Thing> things = p.Position.GetThingList(p.Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_AndroidStand)
                {
                    __result = PawnPosture.Standing;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    public static class PawnRenderer_RenderPawnAt_StandPosture_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = PawnUtility_GetPosture_StandPosture_Patch.rendering;
            PawnUtility_GetPosture_StandPosture_Patch.rendering = true;
        }

        public static void Finalizer(bool __state)
        {
            PawnUtility_GetPosture_StandPosture_Patch.rendering = __state;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelPreRenderPawnAt")]
    public static class PawnRenderer_ParallelPreRenderPawnAt_StandPosture_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = PawnUtility_GetPosture_StandPosture_Patch.rendering;
            PawnUtility_GetPosture_StandPosture_Patch.rendering = true;
        }

        public static void Finalizer(bool __state)
        {
            PawnUtility_GetPosture_StandPosture_Patch.rendering = __state;
        }
    }
}
