using RimWorld;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // Opinion worker for the "respected (only awakened)" precept: the esteem applies only to androids that
    // have actually awakened. Unawakened androids get nothing (they are treated as tools instead).
    public class ThoughtWorker_Precept_Android_Social_OnlyAwakened : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn p, Pawn otherPawn)
        {
            if (!ModsConfig.BiotechActive || !ModsConfig.IdeologyActive)
            {
                return ThoughtState.Inactive;
            }
            return otherPawn.IsAndroid() && otherPawn.IsAwakened();
        }
    }
}
