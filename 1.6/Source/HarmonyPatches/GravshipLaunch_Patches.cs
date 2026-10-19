using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI.Group;

namespace VREAndroidsOverhaul
{
    // A mechanized android reads as a colony mech to the mechanitor system. The gravship launch ritual
    // reads that flag too: it loads colony mechs on board as cargo, and it would do that to the same android
    // it has just taken as the copilot. An android taking part in the launch is a crew member, so it reads
    // as one for as long as the launch is being set up and while it belongs to the ritual.
    public static class GravshipCrewUtil
    {
        [ThreadStatic] private static HashSet<Pawn> settingUp;

        public static void Begin(RitualRoleAssignments assignments)
        {
            settingUp = assignments?.Participants == null
                ? null
                : new HashSet<Pawn>(assignments.Participants.Where(MechOversightUtil.IsOversightAndroid));
        }

        public static void End()
        {
            settingUp = null;
        }

        public static bool IsCrew(Pawn pawn)
        {
            if (settingUp != null && settingUp.Contains(pawn))
            {
                return true;
            }
            return pawn.GetLord()?.LordJob is LordJob_Ritual ritual && ritual.RoleFor(pawn) != null;
        }
    }

    [HarmonyPatch(typeof(RitualBehaviorWorker_GravshipLaunch), nameof(RitualBehaviorWorker_GravshipLaunch.TryExecuteOn))]
    public static class RitualBehaviorWorker_GravshipLaunch_TryExecuteOn_Crew_Patch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(RitualRoleAssignments assignments)
        {
            GravshipCrewUtil.Begin(assignments);
        }

        [HarmonyPriority(Priority.Last)]
        public static void Finalizer()
        {
            GravshipCrewUtil.End();
        }
    }

    // Vanilla Gravship Expanded records the destination tile for the launch ritual's lord right after the
    // ritual starts, and throws when no such lord exists. That left the launch dialog throwing every frame
    // instead of closing, so its failure is contained here.
    [HarmonyPatch]
    public static class VGE_GravshipLaunch_TryExecuteOn_Contain_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method("VanillaGravshipExpanded.RitualBehaviorWorker_GravshipLaunch_TryExecuteOn_Patch:Postfix");
        }

        public static bool Prepare()
        {
            return TargetMethod() != null;
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                Log.WarningOnce("[VRE-Android Overhaul] Vanilla Gravship Expanded could not record the launch "
                    + "target: " + __exception.GetType().Name, 7311905);
            }
            return null;
        }
    }
}
