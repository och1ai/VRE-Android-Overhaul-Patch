using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VREAndroidsOverhaul
{
    // A save stores every thing, gene, hediff and need with the class it was running as, and rebuilds it
    // from that stored name - not from the def. So repointing a def only reaches things created *after*
    // the overlay was added: an assembler already standing in a colony keeps opening the original's window
    // forever, and an android already alive keeps the original's power gene, reactor and needs, no matter
    // how correct Patches/*.xml is. Nothing is logged, and it reads exactly like "the feature was never
    // ported" - which is the whole reason an overlay needs this.
    //
    // BackCompatibility.GetBackCompatibleType is the hook the game itself uses to rename a saved class, so
    // the overlay's rewrites are declared here and swapped in as the save is read. Its converter chain is a
    // private hardcoded list that mods cannot join, hence the postfix.
    //
    // Every replacement is either a subclass of the class it replaces, so the saved fields still load, or -
    // for the assembler - a class whose ExposeData already tolerates the nodes it will not find. Entries on
    // a class the overlay does not own are matched on the node's def as well, so nothing outside this mod
    // is ever remapped.
    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType))]
    public static class BackCompatibility_GetBackCompatibleType_Patch
    {
        private sealed class Rewrite
        {
            public string savedClass;
            // null matches on the saved class alone; otherwise the node's def has to match too.
            public string defName;
            public Type replacement;
        }

        private static readonly List<Rewrite> Rewrites = new List<Rewrite>
        {
            new Rewrite
            {
                savedClass = "VREAndroids.Building_AndroidCreationStation",
                defName = "VREA_AndroidCreationStation",
                replacement = typeof(Building_AndroidCreationStation),
            },
            new Rewrite
            {
                savedClass = "VREAndroids.UnfinishedAndroid",
                defName = "VREA_UnfinishedAndroid",
                replacement = typeof(UnfinishedAndroid),
            },
            new Rewrite
            {
                savedClass = "VREAndroids.Hediff_AndroidReactor",
                defName = "VREA_Reactor",
                replacement = typeof(Hediff_AndroidReactorCore),
            },
            new Rewrite
            {
                savedClass = "VREAndroids.Need_ReactorPower",
                defName = "VREA_ReactorPower",
                replacement = typeof(Need_AndroidPower),
            },
            new Rewrite
            {
                savedClass = "VREAndroids.Need_MemorySpace",
                defName = "VREA_MemorySpace",
                replacement = typeof(Need_AndroidMemory),
            },
            // The original leaves the power gene on the vanilla Gene class, so this one has to be matched on
            // its def: remapping Verse.Gene on the class name alone would rewrite every gene in the game.
            new Rewrite
            {
                savedClass = "Verse.Gene",
                defName = "VREA_Power",
                replacement = typeof(Gene_AndroidPower),
            },
        };

        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Postfix(string providedClassName, XmlNode node, ref Type __result)
        {
            for (int i = 0; i < Rewrites.Count; i++)
            {
                Rewrite rewrite = Rewrites[i];
                if (rewrite.savedClass != providedClassName)
                {
                    continue;
                }
                if (rewrite.defName != null && node?["def"]?.InnerText != rewrite.defName)
                {
                    continue;
                }
                __result = rewrite.replacement;
                // Worth one line each: it is the only sign that a colony predating the overlay is being
                // carried over rather than silently running the original's code.
                if (Reported.Add(rewrite.savedClass))
                {
                    Log.Message("[VRE-Android Overhaul] Loading " + rewrite.savedClass
                        + " saved before the overhaul as " + rewrite.replacement.Name + ".");
                }
                return;
            }
        }
    }
}
