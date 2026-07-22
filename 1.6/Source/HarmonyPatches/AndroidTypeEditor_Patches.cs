using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // The androidtype editor starts a new type by auto-selecting every component that cannot be removed
    // from an android, i.e. every gene flagged isCoreComponent. That was right when there was exactly one
    // power source and one blood type; now that each is a mutually exclusive CHOICE between core
    // components, it selects all of them at once - a new android would start with a reactor AND a battery,
    // neutroamine AND hemogenic AND bloodless.
    //
    // So after the window sets its defaults, trim each exclusion group down to the one the stock android
    // is built with: a reactor, and neutroamine blood.
    [HarmonyPatch]
    public static class Window_CreateAndroidBase_Ctor_Patch
    {
        private const string BloodTag = "AndroidBlood";
        private const string PowerTag = "AndroidPower";

        public static MethodBase TargetMethod()
        {
            return AccessTools.Constructor(typeof(Window_CreateAndroidBase), new[] { typeof(Action) });
        }

        public static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not find the androidtype editor constructor; a new "
                + "androidtype will start with every power source and blood type selected at once. Nothing "
                + "else is affected.");
            return false;
        }

        public static void Postfix(Window_CreateAndroidBase __instance)
        {
            List<GeneDef> selected = __instance.SelectedGenes;
            if (selected == null)
            {
                return;
            }
            KeepOnly(selected, PowerTag, "VREA_Power");
            KeepOnly(selected, BloodTag, "VREA_NeutroCirculation");
        }

        // Drops every selected gene carrying the exclusion tag, then puts back the default one.
        private static void KeepOnly(List<GeneDef> selected, string exclusionTag, string keepDefName)
        {
            GeneDef keep = DefDatabase<GeneDef>.GetNamedSilentFail(keepDefName);
            selected.RemoveAll(g => g.exclusionTags != null && g.exclusionTags.Contains(exclusionTag));
            if (keep != null)
            {
                selected.Add(keep);
            }
        }
    }
}
