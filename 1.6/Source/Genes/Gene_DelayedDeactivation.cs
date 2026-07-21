using RimWorld;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // "Death delay" subroutine. When a critical failure that would normally drop the android happens
    // anywhere but the head, a hardened backup capacitor keeps it running for two hours before it finally
    // shuts down. A destroyed head bypasses the reserve entirely. The decision itself lives in
    // DelayedDeactivation_Patches; this gene only holds the countdown, so it survives saving and can be
    // shown in the inspect pane.
    public class Gene_DelayedDeactivation : Gene
    {
        // Absolute game tick at which the reserve runs out. -1 = not counting.
        public int deactivateAtTick = -1;

        public const int GraceTicks = 2 * GenDate.TicksPerHour;

        public bool CountingDown => deactivateAtTick >= 0;

        // Never report a negative remainder once the reserve has run out.
        public int TicksLeft => deactivateAtTick < 0
            ? 0
            : Mathf.Max(0, deactivateAtTick - Find.TickManager.TicksGame);

        public bool Expired => deactivateAtTick >= 0 && Find.TickManager.TicksGame >= deactivateAtTick;

        // ShouldBeDead / ShouldBeDowned are only consulted when something changes the pawn's health, so an
        // android whose reserve simply times out with no further damage would never be re-evaluated - the
        // countdown would run on into negative numbers and it would keep working. Once the reserve is
        // spent, poke the health tracker so the shutdown actually lands.
        public override void Tick()
        {
            base.Tick();
            if (Expired && pawn != null && !pawn.Dead && !pawn.Downed)
            {
                pawn.health.CheckForStateChange(null, null);
            }
        }

        // Starts the countdown the first time it is asked, and reports whether the reserve has run out.
        public bool RunReserveAndShouldDeactivate()
        {
            int now = Find.TickManager.TicksGame;
            if (deactivateAtTick < 0)
            {
                deactivateAtTick = now + GraceTicks;
                return false;
            }
            return now >= deactivateAtTick;
        }

        public void ResetCountdown()
        {
            deactivateAtTick = -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deactivateAtTick, "deactivateAtTick", -1);
        }
    }
}
