using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace VREAndroidsOverhaul
{
    // The control-group gizmo's hover text lists "Assigned mechs" with each one's energy, but only for mechs
    // that have an energy need - a mechlike android has a power need instead, so it vanished from the list
    // even while it was in the group. The text is built inside a lambda in GizmoOnGUI, so the patch targets
    // that compiler-generated method and rebuilds the list section with androids included, showing their
    // name and charge in the same format.
    [HarmonyPatch]
    public static class MechanitorControlGroupGizmo_Tooltip_Patch
    {
        // The tooltip lambda captures only the gizmo's own fields, so the compiler put it on the gizmo
        // class itself, named after GizmoOnGUI with a build-specific number. It is the one parameterless
        // lambda returning a string.
        private static MethodBase TargetMethod()
        {
            return typeof(MechanitorControlGroupGizmo).GetMethods(AccessTools.all).FirstOrDefault(m =>
                m.ReturnType == typeof(string) && m.GetParameters().Length == 0 && m.Name.Contains("GizmoOnGUI>b__"));
        }

        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        public static void Postfix(object __instance, ref string __result)
        {
            MechanitorControlGroup group = FindGroup(__instance);
            if (group == null || !group.MechsForReading.Any(MechOversightUtil.IsOversightAndroid))
            {
                return;
            }
            string header = "AssignedMechs".Translate().Colorize(ColoredText.TipSectionTitleColor) + "\n";
            int headerAt = __result.IndexOf(header);
            if (headerAt < 0)
            {
                return;
            }
            int start = headerAt + header.Length;
            // The list is the last section unless the gizmo is disabled, which appends a blank-line-separated
            // reason after it.
            int end = __result.IndexOf("\n\n", start);
            string tail = end < 0 ? "" : __result.Substring(end);
            IEnumerable<string> entries = group.MechsForReading.Select(EntryFor).Where(e => e != null);
            __result = __result.Substring(0, start) + entries.ToLineList(" - ") + tail;
        }

        private static string EntryFor(Pawn mech)
        {
            Need need = mech.needs?.energy;
            if (need != null)
            {
                return mech.LabelCap + " (" + need.CurLevelPercentage.ToStringPercent() + " " + "EnergyLower".Translate() + ")";
            }
            if (MechOversightUtil.IsOversightAndroid(mech))
            {
                need = mech.needs?.AllNeeds.FirstOrDefault(n => n is VREAndroids.Need_ReactorPower);
                return need == null ? mech.LabelCap
                    : mech.LabelCap + " (" + need.CurLevelPercentage.ToStringPercent() + " " + "VREAOverhaul.ChargeLower".Translate() + ")";
            }
            return null;
        }

        private static MechanitorControlGroup FindGroup(object instance)
        {
            return Traverse.Create(instance).Field("controlGroup").GetValue<MechanitorControlGroup>();
        }
    }
}
