using RimWorld;
using System.Collections.Generic;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // An android follows an ideoligion only if something in it can hold beliefs: the ideological
    // subroutine, or the sentience it gains by awakening. Everything else - a base android running stock
    // firmware - has no ideoligion at all, which is why the colony's precepts about androids matter more
    // than the android's own.
    public static class IdeoCapability
    {
        private static GeneDef gene;
        private static bool resolved;

        public static GeneDef Gene
        {
            get
            {
                if (!resolved)
                {
                    gene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Ideological");
                    resolved = true;
                }
                return gene;
            }
        }

        public static bool CanHoldIdeoligion(Pawn pawn)
        {
            if (pawn == null || !pawn.IsAndroid() || pawn.IsAwakened())
            {
                return true;
            }
            return Gene != null && pawn.HasActiveGene(Gene);
        }

        // Reconciles an android's ideoligion with the subroutine: with the gene it is given one (its
        // faction's, or any existing one) if it has none; without the gene its ideoligion is cleared. An
        // awakened android keeps whatever it believes even if the subroutine is stripped. No-op without
        // Ideology.
        public static void SyncIdeo(Pawn android)
        {
            if (!ModsConfig.IdeologyActive || android?.ideo == null || Gene == null)
            {
                return;
            }
            bool hasGene = android.genes?.GetGene(Gene) != null;
            if (hasGene)
            {
                if (android.Ideo == null)
                {
                    Ideo ideo = android.Faction?.ideos?.PrimaryIdeo;
                    if (ideo == null && Find.IdeoManager != null)
                    {
                        List<Ideo> all = Find.IdeoManager.IdeosListForReading;
                        if (all.Count > 0)
                        {
                            ideo = all.RandomElement();
                        }
                    }
                    if (ideo != null)
                    {
                        android.ideo.SetIdeo(ideo);
                    }
                }
            }
            else if (!android.IsAwakened() && android.Ideo != null)
            {
                android.ideo.SetIdeo(null);
            }
        }
    }

    // Belief-modelling subroutine. Adding it gives the android an ideoligion, removing it takes it away
    // again; roles and conversion then work through the normal Ideology UI.
    public class Gene_Ideological : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            IdeoCapability.SyncIdeo(pawn);
        }

        public override void PostRemove()
        {
            base.PostRemove();
            IdeoCapability.SyncIdeo(pawn);
        }
    }

    // Androids in a save from before the subroutine existed are holding an ideoligion they should not
    // have (or missing one they should). Reconciled once, then recorded as done.
    public class GameComponent_IdeoCapabilityMigration : GameComponent
    {
        private bool migrated;

        public GameComponent_IdeoCapabilityMigration(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref migrated, "vreaOverhaul_ideoCapabilityMigrated", defaultValue: false);
        }

        public override void FinalizeInit()
        {
            if (migrated || !ModsConfig.IdeologyActive)
            {
                return;
            }
            migrated = true;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (pawn.IsAndroid())
                {
                    IdeoCapability.SyncIdeo(pawn);
                }
            }
        }
    }
}
