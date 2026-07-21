using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // "Mechlike" subroutine: an android carrying VREA_MechOversight is treated by the mech control system as
    // if it were one of the mechanitor's mechs. It gets a CompOverseerSubject so a mechanitor can
    // connect/disconnect it (the vanilla ControlMech / DisconnectMech float-menu options, which only gate on
    // IsColonyMech), it counts against bandwidth (5, from the gene's BandwidthCost stat offset), and it
    // shows up in the mechs tab and control groups. All of that hangs off making IsColonyMech return true
    // for such androids.
    internal static class MechOversightUtil
    {
        private static readonly FieldInfo CompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
        private static readonly FieldInfo OverseerSubjectField = AccessTools.Field(typeof(Pawn), "overseerSubject");

        private static GeneDef gene;
        private static MentalStateDef dormant;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            gene = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_MechOversight");
            dormant = DefDatabase<MentalStateDef>.GetNamedSilentFail("VREA_AwaitingOverseer");
        }

        public static MentalStateDef AwaitingOverseerDef
        {
            get
            {
                Resolve();
                return dormant;
            }
        }

        // Scoped opt-out of the IsColonyMech override. An oversight android should read as a colony mech to
        // the mechanitor control system, but NOT to systems that assume a mech's race data - most notably
        // work types, which vanilla derives from RaceProps.mechEnabledWorkTypes (empty on a humanlike, so
        // every work type would be disabled).
        // Depth-counted rather than a bool so nested calls can't clear it early.
        [ThreadStatic] private static int suppressColonyMechDepth;

        public static bool SuppressColonyMech => suppressColonyMechDepth > 0;

        public static void PushSuppressColonyMech()
        {
            suppressColonyMechDepth++;
        }

        public static void PopSuppressColonyMech()
        {
            if (suppressColonyMechDepth > 0)
            {
                suppressColonyMechDepth--;
            }
        }

        // Pawn.OverseerSubject only ever looks the comp up when RaceProps.IsMechanoid, so for a humanlike
        // android it returns null even though the comp is present. Resolve it ourselves and prime the
        // backing field so later reads short-circuit past that guard.
        public static CompOverseerSubject ResolveOverseerSubject(Pawn pawn)
        {
            CompOverseerSubject comp = pawn.GetComp<CompOverseerSubject>();
            if (comp != null)
            {
                OverseerSubjectField?.SetValue(pawn, comp);
            }
            return comp;
        }

        public static bool IsOversightAndroid(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn == null || !pawn.IsAndroid())
            {
                return false;
            }
            Resolve();
            return gene != null && pawn.HasActiveGene(gene);
        }

        // Cut every link a mechanitor holds on this pawn: control-group membership, the Overseer relation
        // and the bandwidth it was costing. Deliberately sweeps all mechanitors instead of asking the pawn
        // for its overseer - by the time this runs the relation may already be gone while the control-group
        // membership lingers, which is exactly the "still stuck in Group 1" case it exists to fix.
        public static void DisconnectFromAllMechanitors(Pawn pawn)
        {
            foreach (Pawn mechanitor in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                Pawn_MechanitorTracker tracker = mechanitor.mechanitor;
                if (tracker == null)
                {
                    continue;
                }
                bool wasControlling = tracker.ControlledPawns.Contains(pawn) || tracker.OverseenPawns.Contains(pawn);
                tracker.UnassignPawnFromAnyControlGroup(pawn);
                mechanitor.relations.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, pawn);
                if (wasControlling)
                {
                    tracker.Notify_BandwidthChanged();
                }
            }
        }

        // Give the android a CompOverseerSubject at runtime (androids share the Human ThingDef, so the comp
        // can't just be declared in XML without also landing on every colonist). Feral behaviour is disabled:
        // a disconnected oversight android goes dormant (see Gene_MechOversight) instead of going feral.
        public static void EnsureOverseerSubject(Pawn pawn)
        {
            if (CompsField == null || pawn.GetComp<CompOverseerSubject>() != null)
            {
                return;
            }
            CompOverseerSubject comp = new CompOverseerSubject { parent = pawn };
            comp.Initialize(new CompProperties_OverseerSubject
            {
                delayUntilFeralCheck = int.MaxValue,
                feralMtbDays = int.MaxValue
            });
            List<ThingComp> list = (List<ThingComp>)CompsField.GetValue(pawn);
            if (list == null)
            {
                list = new List<ThingComp>();
                CompsField.SetValue(pawn, list);
            }
            list.Add(comp);
        }
    }

    // THE key patch: Pawn.OverseerSubject only resolves the comp when RaceProps.IsMechanoid, so a humanlike
    // android reported null even with the comp attached - which made EverControllable false and every
    // mechanitor see "Cannot control: Never controllable".
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.OverseerSubject), MethodType.Getter)]
    public static class Pawn_OverseerSubject_Patch
    {
        public static void Postfix(Pawn __instance, ref CompOverseerSubject __result)
        {
            if (__result == null && MechOversightUtil.IsOversightAndroid(__instance))
            {
                __result = MechOversightUtil.ResolveOverseerSubject(__instance);
            }
        }
    }

    // Oversight androids never go feral - they go dormant instead - so drop the comp's red "Danger: may go
    // feral" / "Uncontrolled" warning and keep only the "Overseer: ..." line. The dormant mental state
    // already explains the state in the pawn's inspect pane.
    [HarmonyPatch(typeof(CompOverseerSubject), nameof(CompOverseerSubject.CompInspectStringExtra))]
    public static class CompOverseerSubject_CompInspectStringExtra_Patch
    {
        public static void Postfix(CompOverseerSubject __instance, ref string __result)
        {
            if (__result.NullOrEmpty() || !(__instance.parent is Pawn pawn)
                || !MechOversightUtil.IsOversightAndroid(pawn))
            {
                return;
            }
            int lineBreak = __result.IndexOf('\n');
            if (lineBreak >= 0)
            {
                __result = __result.Substring(0, lineBreak);
            }
        }
    }

    // Make oversight androids read as colony mechs so the whole mechanitor control system accepts them.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonyMech), MethodType.Getter)]
    public static class Pawn_IsColonyMech_Patch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || MechOversightUtil.SuppressColonyMech || !MechOversightUtil.IsOversightAndroid(__instance))
            {
                return;
            }
            // A dormant "awaiting overseer" android must still read as a colony mech so a mechanitor can
            // connect to it (otherwise its own dormant mental state would disqualify it).
            if (__instance.Faction == Faction.OfPlayer
                && (__instance.MentalStateDef == null || __instance.MentalStateDef == MechOversightUtil.AwaitingOverseerDef))
            {
                __result = __instance.HostFaction == null || __instance.IsSlave;
            }
        }
    }

    // Add the overseer-subject comp on spawn/load - a reliable point where the pawn's genes are set and its
    // comp list is built (PawnGenerator's early AddAndRemoveDynamicComponents can run before genes exist).
    // The gene's PostAdd covers runtime reprogramming.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_MechOversight_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            if (MechOversightUtil.IsOversightAndroid(__instance))
            {
                MechOversightUtil.EnsureOverseerSubject(__instance);
                // Drop any work-type list cached before the colony-mech suppression existed, so an android
                // from an older save recomputes its (normal, humanlike) work types.
                __instance.Notify_DisabledWorkTypesChanged();
            }
        }
    }

    // An oversight android keeps its normal humanlike work types and skills. Vanilla disables every work
    // type not in RaceProps.mechEnabledWorkTypes for a colony mech, and a humanlike race lists none - which
    // wiped out all of the android's work and skills (and, knock-on, made skill-derived stats read as
    // "disabled" and throw consistency errors). Suppress the colony-mech override for the duration.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Pawn_GetDisabledWorkTypes_Patch
    {
        public static void Prefix(Pawn __instance, out bool __state)
        {
            __state = MechOversightUtil.IsOversightAndroid(__instance);
            if (__state)
            {
                MechOversightUtil.PushSuppressColonyMech();
            }
        }

        // Finalizer so the suppression is released even if the original throws.
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                MechOversightUtil.PopSuppressColonyMech();
            }
        }
    }

    // A mechlike android only takes direct orders while it is inside its mechanitor's command range, the
    // same leash mechs operate under. In range it behaves normally (so it can still be told to equip
    // weapons, wear apparel, etc.); out of range - or with no overseer at all, where it is dormant anyway -
    // right-click commands are refused.
    [HarmonyPatch(typeof(FloatMenuMakerMap), "ShouldGenerateFloatMenuForPawn")]
    public static class FloatMenuMakerMap_ShouldGenerateFloatMenuForPawn_MechOversight_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(Pawn pawn, ref AcceptanceReport __result)
        {
            if (!__result || !MechOversightUtil.IsOversightAndroid(pawn))
            {
                return;
            }
            if (!MechanitorUtility.InMechanitorCommandRange(pawn, pawn.Position))
            {
                __result = new AcceptanceReport("OutOfCommandRange".Translate());
            }
        }
    }

    // An escorting android has to actually engage. JobGiver_AIFightEnemy.TryGiveJob (which the escort's
    // JobGiver_AIDefendOverseer calls into) bails out at the very top for any pawn that IsColonist whose
    // hostilityResponse is not "Attack" - and an android is a colonist whose default response is Flee. So a
    // weaponless android would just stand next to its overseer instead of charging the pirate. Mechs skip
    // this because they are not colonists; force the same behaviour on an escorting android by presenting
    // its hostility response as Attack for the duration of the call.
    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
    public static class JobGiver_AIFightEnemy_TryGiveJob_Patch
    {
        public static void Prefix(Pawn pawn, out HostilityResponseMode __state)
        {
            __state = pawn?.playerSettings?.hostilityResponse ?? HostilityResponseMode.Flee;
            if (pawn?.playerSettings != null && MechOversightUtil.IsOversightAndroid(pawn)
                && pawn.GetOverseer() != null && pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort)
            {
                pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
            }
        }

        public static void Finalizer(Pawn pawn, HostilityResponseMode __state)
        {
            if (pawn?.playerSettings != null)
            {
                pawn.playerSettings.hostilityResponse = __state;
            }
        }
    }

    // While escorting, an overseen android should fight like a mech - defending its overseer using
    // JobGiver_AIDefendOverseer's acquisition radius - instead of obeying its own colonist attack/flee
    // setting. That setting comes from JobGiver_ConfigurableHostilityResponse, which lives in the *constant*
    // think tree and so preempts the escort subtree every tick; it also only reacts at very short range,
    // which is why detection looked far shorter than a mech's. Mechs have no configurable hostility response
    // at all, so suppress it while escorting (other work modes keep it).
    [HarmonyPatch(typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob")]
    public static class JobGiver_ConfigurableHostilityResponse_TryGiveJob_Patch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn != null && MechOversightUtil.IsOversightAndroid(pawn) && pawn.GetOverseer() != null
                && pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    // Waking an android up when its work mode changes. Vanilla's SetWorkModeForPawn rouses a dormant mech
    // through CompCanBeDormant.WakeUp() and by ending a MechCharge job - an android has neither, and its
    // forced-sleep LayDown job is not something CheckForJobOverride will beat, so switching from sleep to
    // work/escort left it lying there asleep. Ending the parked job makes the think tree re-evaluate at
    // once (and re-issue a dormant job immediately if the new mode is also a resting one).
    [HarmonyPatch(typeof(MechanitorControlGroup), "SetWorkModeForPawn")]
    public static class MechanitorControlGroup_SetWorkModeForPawn_Patch
    {
        public static void Postfix(Pawn pawn)
        {
            if (pawn != null && MechOversightUtil.IsOversightAndroid(pawn) && pawn.CurJobDef == JobDefOf.LayDown)
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    // Self-heal: a control group must never keep an android that no longer has the mechlike gene (most often
    // because it awakened). Gene_MechOversight.PostRemove disconnects on awakening, but that does not help
    // an android that awakened in an OLD save before this existed - its stale group membership was saved and
    // reloads intact. Filtering the group's read-list (and unassigning as we go) fixes both going forward
    // and on load, the moment the group is next inspected.
    [HarmonyPatch(typeof(MechanitorControlGroup), nameof(MechanitorControlGroup.MechsForReading), MethodType.Getter)]
    public static class MechanitorControlGroup_MechsForReading_Patch
    {
        public static void Postfix(MechanitorControlGroup __instance, ref List<Pawn> __result)
        {
            if (__result == null)
            {
                return;
            }
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                Pawn p = __result[i];
                if (p != null && p.IsAndroid() && !MechOversightUtil.IsOversightAndroid(p))
                {
                    __instance.TryUnassign(p);
                    MechOversightUtil.DisconnectFromAllMechanitors(p);
                    __result.RemoveAt(i);
                }
            }
        }
    }

    // The mechs tab lists pawns by RaceProps.IsMechanoid (not IsColonyMech), so oversight androids would be
    // filtered out. Add them back in.
    [HarmonyPatch(typeof(MainTabWindow_Mechs), "Pawns", MethodType.Getter)]
    public static class MainTabWindow_Mechs_Pawns_Patch
    {
        public static void Postfix(ref IEnumerable<Pawn> __result)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            IEnumerable<Pawn> androids = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .Where(p => MechOversightUtil.IsOversightAndroid(p) && p.OverseerSubject != null);
            __result = __result.Concat(androids);
        }
    }
}
