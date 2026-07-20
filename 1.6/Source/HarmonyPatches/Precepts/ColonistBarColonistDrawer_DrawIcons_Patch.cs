using HarmonyLib;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VREAndroidsOverhaul
{
    // Ideology marks a slave on its colonist-bar entry with a small icon (alongside the amber name). An
    // android the colony's ideoligion treats as a mere tool gets the same treatment: its chosen androidtype
    // symbol is added to that same icon row, tinted the same cold blue as its name.
    //
    // Vanilla builds the row inside DrawIcons from a private static list that is cleared at the top of the
    // method and drawn before it returns, so there is no seam for a prefix/postfix to append to. A
    // transpiler injects one call just before the row is laid out, which appends our icon like any other.
    //
    // Safety: Prepare() bails out (skipping the whole patch) if any of the private members can't be
    // resolved, the transpiler returns the original IL untouched if its anchor is missing, and the injected
    // hook swallows anything it throws. Worst case the icon simply doesn't appear.
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawIcons")]
    public static class ColonistBarColonistDrawer_DrawIcons_Patch
    {
        private static readonly FieldInfo IconsField =
            AccessTools.Field(typeof(ColonistBarColonistDrawer), "tmpIconsToDraw");

        // Anchor: the static field read that begins the row layout, immediately after every icon has been
        // collected. It is referenced exactly once in the method.
        private static readonly FieldInfo AreaWidthField =
            AccessTools.Field(typeof(ColonistBarColonistDrawer), "BaseIconAreaWidth");

        private static readonly Type IconDrawCallType =
            AccessTools.Inner(typeof(ColonistBarColonistDrawer), "IconDrawCall");

        private static readonly ConstructorInfo IconDrawCallCtor = IconDrawCallType == null
            ? null
            : AccessTools.Constructor(IconDrawCallType,
                new[] { typeof(Texture2D), typeof(string), typeof(Color?) });

        public static bool Prepare()
        {
            if (IconsField != null && AreaWidthField != null && IconDrawCallCtor != null)
            {
                return true;
            }
            Log.Warning("[VRE-Android Overhaul] Could not resolve the colonist-bar icon internals; tool-treated "
                + "androids will not show their androidtype symbol on the colonist bar. Nothing else is affected.");
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            MethodInfo hook = AccessTools.Method(typeof(ColonistBarColonistDrawer_DrawIcons_Patch),
                nameof(AddToolAndroidIcon));

            int anchor = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && (c.operand as FieldInfo) == AreaWidthField);
            if (anchor < 0 || hook == null)
            {
                Log.Warning("[VRE-Android Overhaul] Colonist-bar icon transpiler could not find its anchor; leaving "
                    + "DrawIcons untouched (tool androids just won't show their androidtype symbol).");
                return codes;
            }

            // DrawIcons is an instance method: arg0 = this, arg1 = rect, arg2 = colonist.
            var injected = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, hook)
            };
            // The anchor may be a branch target - move any labels onto the first injected instruction so
            // jumps still land in the right place.
            injected[0].labels.AddRange(codes[anchor].labels);
            codes[anchor].labels.Clear();
            codes.InsertRange(anchor, injected);
            return codes;
        }

        public static void AddToolAndroidIcon(Pawn colonist)
        {
            try
            {
                if (colonist == null || colonist.Dead || colonist.genes == null
                    || !ToolTreatment.IsTreatedAsToolByColony(colonist))
                {
                    return;
                }
                Texture2D icon = colonist.genes.XenotypeIcon;
                if (icon == null || !(IconsField.GetValue(null) is IList list))
                {
                    return;
                }
                list.Add(IconDrawCallCtor.Invoke(new object[]
                {
                    icon,
                    colonist.genes.XenotypeLabelCap,
                    // No tint - a null colour makes the draw loop fall back to plain white, like the
                    // slave/role icons. (The blue is kept for the name only.)
                    null
                }));
            }
            catch
            {
                // Cosmetic only - never let this break the colonist bar.
            }
        }
    }
}
