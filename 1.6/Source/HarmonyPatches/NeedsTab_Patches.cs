using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A non-awakened android reads like a machine, so its needs tab is trimmed to a mechanoid's: power and
    // memory, nothing else. Mood still exists underneath - the awakening mechanic runs on it - it is just
    // not shown. Once awakened, the android shows the full set of needs like any colonist.
    //
    // The memory bar is further hidden whenever there is no memory system to maintain, so an android built
    // without the memory hardware does not show a bar that never moves.
    [HarmonyPatch(typeof(Need), nameof(Need.ShowOnNeedList), MethodType.Getter)]
    [HarmonyAfter("VREAndroidsMod")]
    [HarmonyPriority(Priority.Low)]
    public static class Need_ShowOnNeedList_Patch
    {
        public static void Postfix(Need __instance, Pawn ___pawn, ref bool __result)
        {
            if (!__result || ___pawn == null || !___pawn.IsAndroid())
            {
                return;
            }
            if (__instance is Need_AndroidMemory memory && !memory.MemoryActive)
            {
                __result = false;
                return;
            }
            if (___pawn.IsAwakened())
            {
                return;
            }
            if (__instance.def.defName != "VREA_ReactorPower" && __instance.def.defName != "VREA_MemorySpace")
            {
                __result = false;
            }
        }
    }
}
