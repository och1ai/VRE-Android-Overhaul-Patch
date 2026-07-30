using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // When an android drops because its battery ran out, vanilla logs "a capacitor array caused X to fall
    // unconscious" - the battery is implemented as a hediff, and the combat log names it. Replaced with a
    // low-power collapse line that says what actually happened.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Pawn_HealthTracker_MakeDowned_LowPower_Patch
    {
        public static void Prefix(Pawn ___pawn, Hediff hediff)
        {
            LowPowerCollapse.suppressDownLog = ___pawn != null && ___pawn.IsAndroid()
                && hediff is Hediff_AndroidBattery;
        }

        public static void Postfix(Pawn ___pawn)
        {
            if (!LowPowerCollapse.suppressDownLog)
            {
                return;
            }
            // Cleared first, or the replacement entry below would be swallowed by the same filter.
            LowPowerCollapse.suppressDownLog = false;
            if (___pawn != null && ___pawn.Spawned && OverhaulDefOf.TransitionLowPower != null)
            {
                Find.BattleLog.Add(new BattleLogEntry_StateTransition(___pawn,
                    OverhaulDefOf.TransitionLowPower, null, null, null));
            }
        }
    }

    // Drops the vanilla "downed by hediff" entry while an android is collapsing from low power; the
    // postfix above logs a clearer one in its place.
    [HarmonyPatch(typeof(BattleLog), nameof(BattleLog.Add))]
    public static class BattleLog_Add_LowPower_Patch
    {
        public static bool Prefix(LogEntry entry)
        {
            return !(LowPowerCollapse.suppressDownLog && entry is BattleLogEntry_StateTransition);
        }
    }

    public static class LowPowerCollapse
    {
        // Set for the duration of one MakeDowned call, between the prefix and the postfix.
        public static bool suppressDownLog;
    }
}
