using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace VREAndroidsOverhaul
{
    // Bootstrap for the overlay patch. Loads after the original Vanilla Races Expanded - Android and
    // applies its own Harmony patches under a distinct id. The original's own patches (harmony id
    // "VREAndroidsMod") are left in place; where this overlay needs to change the original's behaviour it
    // selectively unpatches the specific target method and applies its own, rather than copying any of
    // the original's code.
    public class VREAndroidsOverhaulMod : Mod
    {
        public const string HarmonyId = "och1ai.vreandroid.overhaul";
        public const string OriginalHarmonyId = "VREAndroidsMod";

        public static Harmony harmony;

        public VREAndroidsOverhaulMod(ModContentPack content) : base(content)
        {
            harmony = new Harmony(HarmonyId);
            UnpatchOriginal();
            harmony.PatchAll();
            Log.Message("[VRE-Android Overhaul] patch assembly loaded (" + typeof(VREAndroidsOverhaulMod).Assembly.GetName().Version + ").");
        }

        // Methods where the original's own patch has to go before this mod's replacement can apply. Kept to
        // an explicit, minimal list: every entry is a behaviour the overhaul redefines outright, and the
        // replacement lives in this assembly next to a comment explaining what changed. Everything else the
        // original does is left running untouched.
        private static void UnpatchOriginal()
        {
            // Psylink level changes: the original refuses them for every android. Replaced by a guard that
            // makes the exception for awakened androids (Psylink_Patches.cs).
            Unpatch(AccessTools.Method(typeof(Hediff_Psylink), "ChangeLevel", new Type[] { typeof(int) }),
                HarmonyPatchType.Prefix, "psylink level changes");
        }

        private static void Unpatch(MethodBase method, HarmonyPatchType type, string what)
        {
            if (method == null)
            {
                Log.Warning("[VRE-Android Overhaul] Could not find the method behind " + what
                    + " to unpatch; the original mod's behaviour stays in effect there.");
                return;
            }
            try
            {
                harmony.Unpatch(method, type, OriginalHarmonyId);
            }
            catch (Exception ex)
            {
                Log.Warning("[VRE-Android Overhaul] Failed to unpatch " + what + ": " + ex.Message
                    + ". The original mod's behaviour stays in effect there.");
            }
        }
    }
}
