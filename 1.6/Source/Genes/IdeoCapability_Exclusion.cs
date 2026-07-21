using System.Collections.Generic;
using Verse;

namespace VREAndroidsOverhaul
{
    // Belief modelling needs a working social model, so the ideological subroutine and the "social
    // incapable" aptitude are mutually exclusive.
    //
    // This has to be done from code rather than XML: the aptitude genes do not exist as defs on disk, they
    // are generated per skill at load time by the original's implied-gene pass, so there is nothing for a
    // PatchOperation to find. By the time a static constructor runs they are in the database.
    [StaticConstructorOnStartup]
    public static class IdeoCapability_Exclusion
    {
        private const string Tag = "AndroidSocialModel";

        static IdeoCapability_Exclusion()
        {
            GeneDef ideological = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_Ideological");
            GeneDef socialIncapable = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_AptitudeIncapable_Social");
            if (ideological == null || socialIncapable == null)
            {
                // Ideology absent (the gene never loads), or the aptitude gene is not generated under that
                // name any more. Either way the pair simply stays non-exclusive.
                return;
            }
            AddTag(ideological);
            AddTag(socialIncapable);
        }

        private static void AddTag(GeneDef def)
        {
            if (def.exclusionTags == null)
            {
                def.exclusionTags = new List<string>();
            }
            if (!def.exclusionTags.Contains(Tag))
            {
                def.exclusionTags.Add(Tag);
            }
        }
    }
}
