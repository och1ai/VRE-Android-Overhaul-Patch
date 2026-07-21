using HarmonyLib;
using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // 7c: the original's "psychically deaf" gene is retuned into "psychically dull" (0.5 sensitivity) by
    // Patches/PsychicSensitivity.xml, and awakening removes it entirely so an awakened android reaches a
    // full 1.0.
    //
    // That removal deliberately does NOT use the def's <removeWhenAwakened> flag. Utils.IsAwakened() is
    // defined as "carries no gene flagged removeWhenAwakened", i.e. those genes ARE the awakened marker -
    // flagging one that awakened androids in existing saves already carry would silently un-awaken every
    // one of them. Stripping it from code keeps that marker untouched.
    internal static class PsychicDullness
    {
        public const string GeneDefName = "VREA_PsychicallyDeaf";

        public static void Strip(Pawn pawn)
        {
            if (pawn?.genes == null)
            {
                return;
            }
            GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(GeneDefName);
            if (def == null)
            {
                return;
            }
            Gene gene = pawn.genes.GetGene(def);
            if (gene != null)
            {
                pawn.genes.RemoveGene(gene);
            }
        }
    }

    // Every awakening path in the original funnels through this one method.
    [HarmonyPatch(typeof(Gene_SyntheticBody), nameof(Gene_SyntheticBody.Awaken))]
    public static class Gene_SyntheticBody_Awaken_Patch
    {
        public static void Postfix(Gene_SyntheticBody __instance)
        {
            PsychicDullness.Strip(__instance.pawn);
        }
    }

    // One-shot migration for androids that awakened before this patch existed: they still carry the gene,
    // so they would sit at 0.5 forever. Runs once per save, then records itself as done.
    public class GameComponent_PsychicDullnessMigration : GameComponent
    {
        private bool migrated;

        public GameComponent_PsychicDullnessMigration(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref migrated, "vreaOverhaul_psychicDullnessMigrated", defaultValue: false);
        }

        public override void FinalizeInit()
        {
            if (migrated)
            {
                return;
            }
            migrated = true;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (pawn.IsAndroid() && pawn.IsAwakened())
                {
                    PsychicDullness.Strip(pawn);
                }
            }
        }
    }
}
