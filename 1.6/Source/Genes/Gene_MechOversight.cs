using RimWorld;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // "Mechlike" subroutine: the standard mechanoid routine that delegates part of the android's
    // decision-making to a supervising mechanitor. The android is treated as a colony mech (see
    // MechOversight_Patches), but on its own it has no operator: with no overseer it stands dormant -
    // powered down and frozen, the same way the solar-flare-vulnerability gene freezes an android - until a
    // mechanitor connects to it. It never goes feral; there is nothing to rebel, only something to idle.
    public class Gene_MechOversight : Gene
    {
        public OverlayHandle? overlayPowerOff;

        public override void PostAdd()
        {
            base.PostAdd();
            // The comp has to exist as soon as the gene is applied (e.g. reprogramming an already-spawned
            // android), not only on spawn.
            if (pawn != null)
            {
                MechOversightUtil.EnsureOverseerSubject(pawn);
            }
        }

        // Losing the subroutine (most often by awakening) severs the link: an awakened mind is nobody's
        // remote-controlled machine.
        public override void PostRemove()
        {
            base.PostRemove();
            if (pawn == null)
            {
                return;
            }
            if (pawn.Spawned && overlayPowerOff.HasValue)
            {
                pawn.Map.overlayDrawer.Disable(pawn, ref overlayPowerOff);
            }
            // Disconnect explicitly, and do NOT rely on GetOverseer: by the time PostRemove runs the gene
            // change may already have severed the Overseer relation (a non-mech can't be overseen) while
            // never unassigning the android from the control group - so it lingers in "Group 1". Sweep every
            // player mechanitor and unassign this pawn from all their groups, dropping any leftover relation
            // too.
            MechOversightUtil.DisconnectFromAllMechanitors(pawn);
            // Drop the dormant state so it doesn't stay frozen waiting for an overseer it no longer needs.
            if (pawn.MentalStateDef == MechOversightUtil.AwaitingOverseerDef)
            {
                pawn.mindState?.mentalStateHandler?.CurState?.RecoverFromState();
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (!pawn.Spawned)
            {
                return;
            }
            MechOversightUtil.EnsureOverseerSubject(pawn);

            // No mechanitor has taken oversight -> stand dormant until one does.
            if (pawn.GetOverseer() == null)
            {
                MentalStateDef dormant = MechOversightUtil.AwaitingOverseerDef;
                if (dormant != null && pawn.MentalStateDef != dormant)
                {
                    if (pawn.InMentalState)
                    {
                        pawn.mindState.mentalStateHandler.CurState.RecoverFromState();
                    }
                    pawn.mindState.mentalStateHandler.TryStartMentalState(dormant, null, forced: true,
                        forceWake: false, causedByMood: false, null, transitionSilently: true);
                }
                if (overlayPowerOff is null)
                {
                    overlayPowerOff = pawn.Map.overlayDrawer.Enable(pawn, OverlayTypes.PowerOff);
                }
            }
            // With an overseer, MentalState_AwaitingOverseer recovers itself and clears the overlay.
        }
    }

    // The dormant state itself: frozen in place, recovering the instant a mechanitor connects.
    public class MentalState_AwaitingOverseer : MentalState
    {
        private const int FrozenStanceTicks = 999999999;

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override void PreStart()
        {
            base.PreStart();
            Freeze();
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (pawn.GetOverseer() != null)
            {
                RecoverFromState();
                return;
            }
            if (pawn.Spawned && pawn.stances != null && !(pawn.stances.curStance is Stance_Stand))
            {
                Freeze();
            }
        }

        private void Freeze()
        {
            if (pawn.stances != null)
            {
                pawn.stances.SetStance(new Stance_Stand(FrozenStanceTicks, pawn.Position + pawn.Rotation.FacingCell, null));
            }
        }

        public override void PostEnd()
        {
            base.PostEnd();
            if (!pawn.Spawned)
            {
                return;
            }
            Gene_MechOversight gene = pawn.genes?.GetFirstGeneOfType<Gene_MechOversight>();
            if (gene != null)
            {
                pawn.Map.overlayDrawer.Disable(pawn, ref gene.overlayPowerOff);
            }
            if (pawn.stances != null)
            {
                pawn.stances.SetStance(new Stance_Mobile());
            }
        }
    }
}
