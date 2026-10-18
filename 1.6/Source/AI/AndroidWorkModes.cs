using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Vanilla's ThinkNode_ConditionalWorkMode requires RaceProps.IsMechanoid, so it can never match a
    // humanlike android. This is the same test for an overseen "mechlike" android: it matches when a
    // mechanitor has taken oversight and set the android's control group to this work mode.
    public class ThinkNode_ConditionalAndroidWorkMode : ThinkNode_Conditional
    {
        public MechWorkModeDef workMode;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalAndroidWorkMode copy = (ThinkNode_ConditionalAndroidWorkMode)base.DeepCopy(resolve);
            copy.workMode = workMode;
            return copy;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            if (!MechOversightUtil.IsOversightAndroid(pawn) || pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }
            // GetMechWorkMode null-chains through the overseer and control group, so an unassigned or
            // unclaimed android simply matches nothing and falls through to its normal behaviour.
            return pawn.GetOverseer() != null && pawn.GetMechWorkMode() == workMode;
        }
    }

    // Work modes that other mods add to the mechanoid think tree (the "Engage" mode mod, WVC - Work Modes).
    // They only ever patch the "Mechanoid" tree, which a humanlike android never runs, and vanilla's
    // ThinkNode_ConditionalWorkMode rejects non-mechanoids anyway. Rather than hand-copying each mod's
    // branches (and having to track their settings and versions), this node borrows them: it finds every
    // work-mode branch in the mechanoid tree and, when the android's control group is set to that mode,
    // runs the branch's own job givers. Modes this overlay handles itself sit earlier in the tree and win;
    // "Work" is skipped so it still falls through to the colonist core. Branches built on mech-only needs
    // (energy, chargers) simply give no job to an android and fall through to the next giver.
    public class ThinkNode_AndroidBorrowedWorkModes : ThinkNode
    {
        private static Dictionary<MechWorkModeDef, List<ThinkNode>> branches;

        // Only run the branch's fighting givers (set when used in the constant tree, see AlertGuard).
        public bool combatOnly;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_AndroidBorrowedWorkModes copy = (ThinkNode_AndroidBorrowedWorkModes)base.DeepCopy(resolve);
            copy.combatOnly = combatOnly;
            return copy;
        }

        private static Dictionary<MechWorkModeDef, List<ThinkNode>> Branches
        {
            get
            {
                if (branches == null)
                {
                    branches = new Dictionary<MechWorkModeDef, List<ThinkNode>>();
                    ThinkTreeDef mechTree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Mechanoid");
                    if (mechTree != null)
                    {
                        Collect(mechTree.thinkRoot);
                    }
                }
                return branches;
            }
        }

        private static void Collect(ThinkNode node)
        {
            if (node is ThinkNode_ConditionalWorkMode modeNode)
            {
                if (modeNode.workMode != null)
                {
                    if (!branches.TryGetValue(modeNode.workMode, out List<ThinkNode> list))
                    {
                        branches[modeNode.workMode] = list = new List<ThinkNode>();
                    }
                    list.Add(modeNode);
                }
                return;
            }
            if (node.subNodes == null)
            {
                return;
            }
            for (int i = 0; i < node.subNodes.Count; i++)
            {
                Collect(node.subNodes[i]);
            }
        }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            if (!MechOversightUtil.IsOversightAndroid(pawn) || pawn.Faction != Faction.OfPlayer || pawn.GetOverseer() == null)
            {
                return ThinkResult.NoJob;
            }
            MechWorkModeDef mode = pawn.GetMechWorkMode();
            if (mode == null || mode == MechWorkModeDefOf.Work || !Branches.TryGetValue(mode, out List<ThinkNode> list))
            {
                return ThinkResult.NoJob;
            }
            for (int i = 0; i < list.Count; i++)
            {
                List<ThinkNode> subNodes = list[i].subNodes;
                for (int j = 0; j < subNodes.Count; j++)
                {
                    if (combatOnly && !(subNodes[j] is JobGiver_AIFightEnemy))
                    {
                        continue;
                    }
                    ThinkResult result = ThinkResult.NoJob;
                    try
                    {
                        result = subNodes[j].TryIssueJobPackage(pawn, jobParams);
                    }
                    catch (System.Exception ex)
                    {
                        // A giver written for a mech's components can trip over an android; skip it.
                        Log.ErrorOnce("[VRE-Android Overhaul] " + subNodes[j].GetType().Name + " failed for " + pawn.LabelShort
                            + " in work mode " + mode.defName + ": " + ex.Message, subNodes[j].GetType().GetHashCode() ^ mode.index);
                    }
                    if (result.IsValid)
                    {
                        // Reported as this node: the borrowed giver lives in another tree, and a job saves
                        // a key that has to resolve inside the tree it was issued from.
                        return new ThinkResult(result.Job, this, result.Tag, result.FromQueue);
                    }
                }
            }
            return ThinkResult.NoJob;
        }
    }

    // Threat check for an overseen android in a fighting mode (escort, engage...), run from the constant
    // think tree. The main tree is only consulted when the current job ends - up to several seconds into a
    // follow or wait - so an escorting android would not notice a raider until it was already on top of it
    // and fell back on the close-melee reaction, which reads as personal defence rather than guarding. The
    // constant tree is polled every half second and may interrupt, which is how it reacts promptly.
    // A fight this guard started is left alone until it finishes, so it does not keep re-issuing it.
    public class ThinkNode_AndroidAlertGuard : ThinkNode_Priority
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            if (!MechOversightUtil.IsOversightAndroid(pawn) || pawn.Faction != Faction.OfPlayer
                || !MechOversightUtil.FightsOnOrders(pawn) || pawn.Drafted)
            {
                return ThinkResult.NoJob;
            }
            Job current = pawn.CurJob;
            if (current != null && (current.playerForced || Contains(this, current.jobGiver)))
            {
                return ThinkResult.NoJob;
            }
            return base.TryIssueJobPackage(pawn, jobParams);
        }

        // The issuing giver is nested under the work-mode condition, so look through the whole subtree.
        private static bool Contains(ThinkNode root, ThinkNode target)
        {
            if (target == null || root.subNodes == null)
            {
                return false;
            }
            for (int i = 0; i < root.subNodes.Count; i++)
            {
                if (root.subNodes[i] == target || Contains(root.subNodes[i], target))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // Escort's defend step with a longer leash. Vanilla's JobGiver_AIDefendOverseer only picks targets within
    // 5 cells plus weapon range of the mechanitor - plenty for a ranged mech, but for an android with a melee
    // weapon (or fists) that is about six cells, so it trailed its overseer and only fought what had already
    // arrived. A wider radius lets it go out and meet a threat the way a mech with a gun does.
    public class JobGiver_AndroidDefendOverseer : JobGiver_AIDefendOverseer
    {
        private const float DefendRadius = 18f;

        protected override float GetFlagRadius(Pawn pawn)
        {
            return DefendRadius;
        }
    }

    // The low-power work modes ("dormant self-charge" and, for a reactor android, "recharge"). The android
    // powers down where it stands; one that runs a real sleep cycle beds down like an organic instead.
    public class JobGiver_AndroidDormant : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return BedJobIfSleeper(pawn) ?? SleepInPlaceJob(pawn);
        }

        private static Job SleepInPlaceJob(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
            job.forceSleep = true;
            return job;
        }

        private static Job BedJobIfSleeper(Pawn pawn)
        {
            GeneDef sleepCycle = DefDatabase<GeneDef>.GetNamedSilentFail("VREA_SleepNeed");
            if (sleepCycle == null || !pawn.HasActiveGene(sleepCycle))
            {
                return null;
            }
            Building_Bed bed = RestUtility.FindBedFor(pawn);
            if (bed == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            job.forceSleep = true;
            return job;
        }
    }
}
