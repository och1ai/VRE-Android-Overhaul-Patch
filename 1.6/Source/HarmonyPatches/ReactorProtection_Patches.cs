using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;
using VREAndroids;

namespace VREAndroidsOverhaul
{
    // A reactor cannot be wounded or destroyed.
    //
    // The reactor is a solid added part, which lives on the android's stomach and is lost the moment that
    // part is destroyed. An android's whole power supply would then hang on a handful of hit points on an
    // internal organ, so the reactor is made unreachable instead: damage that would land on the part
    // carrying it lands on the part around it. It can still be removed deliberately - surgery adds no
    // injury - and it still runs dry. The battery has no part of its own and needs nothing here.
    public static class ReactorProtection
    {
        public static bool IsProtected(Pawn pawn, BodyPartRecord part)
        {
            if (part == null || pawn?.health?.hediffSet == null)
            {
                return false;
            }
            return pawn.health.hediffSet.hediffs.Any(h =>
                h.Part == part && h is Hediff_AndroidReactor && !(h is Hediff_AndroidBattery));
        }

        // The nearest enclosing part that does not carry a reactor; null when there is none.
        public static BodyPartRecord Shelter(Pawn pawn, BodyPartRecord part)
        {
            BodyPartRecord outer = part.parent;
            while (outer != null && IsProtected(pawn, outer))
            {
                outer = outer.parent;
            }
            return outer;
        }
    }

    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    public static class HediffSet_AddDirect_ReactorProtection_Patch
    {
        public static bool Prefix(HediffSet __instance, Hediff hediff)
        {
            if (!(hediff is Hediff_Injury) && !(hediff is Hediff_MissingPart))
            {
                return true;
            }
            Pawn pawn = __instance.pawn;
            if (pawn == null || !ReactorProtection.IsProtected(pawn, hediff.Part))
            {
                return true;
            }
            BodyPartRecord shelter = ReactorProtection.Shelter(pawn, hediff.Part);
            if (shelter == null || hediff is Hediff_MissingPart)
            {
                return false;
            }
            hediff.Part = shelter;
            return true;
        }
    }
}
