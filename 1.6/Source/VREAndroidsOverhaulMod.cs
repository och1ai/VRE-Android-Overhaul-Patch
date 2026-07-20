using HarmonyLib;
using Verse;

namespace VREAndroidsOverhaul
{
    // Bootstrap for the overlay patch. Loads after the original Vanilla Races Expanded - Android and
    // applies its own Harmony patches under a distinct id. The original's own patches (harmony id
    // "VREAndroidsMod") are left in place; where this overlay needs to change the original's behaviour it
    // will selectively unpatch the specific target method and apply its own, rather than copying any of
    // the original's code.
    public class VREAndroidsOverhaulMod : Mod
    {
        public const string HarmonyId = "och1ai.vreandroid.overhaul";
        public static Harmony harmony;

        public VREAndroidsOverhaulMod(ModContentPack content) : base(content)
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[VRE-Android Overhaul] patch assembly loaded (" + typeof(VREAndroidsOverhaulMod).Assembly.GetName().Version + ").");
        }
    }
}
