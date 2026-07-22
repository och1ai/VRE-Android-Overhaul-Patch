using VREAndroids;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VREAndroidsOverhaul
{
    // Small drawing helpers for the android designer, adapted from Altered Carbon's UIHelper but
    // trimmed to what we need and using vanilla widgets / float menus instead of AC's custom windows.
    public static class VREA_UIHelper
    {
        // Narrow label gutter so the controls sit closer to their labels and don't crowd the right edge
        // of the (scrolled) controls column.
        public const float LabelWidth = 100f;
        public const float ButtonWidth = 150f;
        public const float ButtonHeight = 30f;
        public const float RowGap = 5f;

        // A labelled row: returns the rect for the control area to the right of the label and advances pos.
        public static Rect LabelRow(ref Vector2 pos, string label, out Rect controlRect, float controlWidth)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(pos.x, pos.y, LabelWidth, ButtonHeight);
            Widgets.Label(labelRect, label);
            controlRect = new Rect(labelRect.xMax + 10f, pos.y, controlWidth, ButtonHeight);
            pos.y += ButtonHeight + RowGap;
            Text.Anchor = TextAnchor.UpperLeft;
            return labelRect;
        }

        public static void Separator(ref Vector2 pos, float width, string label)
        {
            pos.y += 14f;
            GUI.color = Widgets.SeparatorLabelColor;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(pos.x, pos.y, width, 24f), label);
            pos.y += 24f;
            GUI.color = Widgets.SeparatorLineColor;
            Widgets.DrawLineHorizontal(pos.x, pos.y, width);
            pos.y += 12f;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // < current label > cycler that also opens a float menu of all options on the centre button.
        public static void Cycler<T>(ref Vector2 pos, string label, IList<T> list, T current,
            Func<T, string> labelGetter, Action<T> onSelect)
        {
            if (list == null || list.Count == 0)
            {
                return;
            }
            LabelRow(ref pos, label, out Rect row, ButtonWidth * 2f);
            int index = Mathf.Max(0, list.IndexOf(current));
            var left = new Rect(row.x, row.y, ButtonHeight, ButtonHeight);
            var centre = new Rect(left.xMax + 2f, row.y, row.width - (ButtonHeight * 2f) - 4f, ButtonHeight);
            var right = new Rect(centre.xMax + 2f, row.y, ButtonHeight, ButtonHeight);
            Widgets.DrawHighlight(row);
            if (Widgets.ButtonText(left, "<"))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                onSelect(list[(index - 1 + list.Count) % list.Count]);
            }
            if (Widgets.ButtonText(centre, labelGetter(list[index])))
            {
                var opts = list.Select(x => new FloatMenuOption(labelGetter(x), () => onSelect(x))).ToList();
                if (opts.Any())
                {
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
            if (Widgets.ButtonText(right, ">"))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                onSelect(list[(index + 1) % list.Count]);
            }
        }

        // A stylist-station-style grid of style items (hair, beards): each cell shows the item's icon
        // tinted by the given colour, highlights the current pick, and selects on click. At most maxRows
        // rows are shown at once; the rest scroll. Advances pos below the (capped) grid.
        public static void StyleItemGrid(ref Vector2 pos, ref Vector2 scroll, string label, float width,
            IList<StyleItemDef> items, Color tint, Func<StyleItemDef, bool> isSelected, Action<StyleItemDef> onSelect,
            float itemSize = 48f, int maxRows = 3)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(pos.x, pos.y, LabelWidth, ButtonHeight), label);
            Text.Anchor = TextAnchor.UpperLeft;

            const float gap = 4f;
            const float barW = 16f;
            float stride = itemSize + gap;
            float gridX = pos.x + LabelWidth + 10f;
            float gridW = width - LabelWidth - 10f;
            // The scrollbar lives in a fixed gutter on the LEFT of the grid (right after the label) so it
            // never sits against the tall controls-column scrollbar on the far right. Cells fill the rest.
            float cellsX = gridX + barW + 4f;
            float cellsW = gridW - barW - 4f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((cellsW + gap) / stride));
            int totalRows = Mathf.CeilToInt(items.Count / (float)perRow);
            float viewH = Mathf.Min(totalRows, maxRows) * stride - gap;
            float contentH = totalRows * stride - gap;
            bool needsScroll = contentH > viewH + 0.5f;
            float maxScroll = Mathf.Max(0f, contentH - viewH);
            scroll.y = Mathf.Clamp(scroll.y, 0f, maxScroll);

            // Mouse wheel anywhere over the grid.
            var gridRect = new Rect(gridX, pos.y, gridW, viewH);
            if (needsScroll && Mouse.IsOver(gridRect) && Event.current.type == EventType.ScrollWheel)
            {
                scroll.y = Mathf.Clamp(scroll.y + Event.current.delta.y * 20f, 0f, maxScroll);
                Event.current.Use();
            }
            if (needsScroll)
            {
                var barRect = new Rect(gridX, pos.y, barW, viewH);
                scroll.y = GUI.VerticalScrollbar(barRect, scroll.y, viewH, 0f, contentH);
            }

            var cellsRect = new Rect(cellsX, pos.y, cellsW, viewH);
            Widgets.BeginGroup(cellsRect);
            for (int i = 0; i < items.Count; i++)
            {
                var cell = new Rect((i % perRow) * stride, (i / perRow) * stride - scroll.y, itemSize, itemSize);
                if (cell.yMax < 0f || cell.y > viewH)
                {
                    continue; // culled: outside the visible rows
                }
                Widgets.DrawHighlight(cell);
                if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                    TooltipHandler.TipRegion(cell, items[i].LabelCap);
                }
                GUI.color = tint;
                Widgets.DefIcon(cell, items[i], null, 1.25f);
                GUI.color = Color.white;
                if (isSelected != null && isSelected(items[i]))
                {
                    Widgets.DrawBox(cell, 2);
                }
                if (Widgets.ButtonInvisible(cell))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    onSelect(items[i]);
                }
            }
            Widgets.EndGroup();
            pos.y += viewH + RowGap;
        }

        // Row of clickable colour swatches (one per option); wraps to new rows as needed.
        public static void ColorSwatches<T>(ref Vector2 pos, string label, IList<T> options,
            Func<T, Color> colorGetter, Func<T, bool> isSelected, Action<T> onSelect, int perRow = 8)
        {
            LabelRow(ref pos, label, out Rect row, ButtonWidth * 2f);
            float x = row.x;
            float y = row.y;
            const float size = 26f;
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0 && i % perRow == 0)
                {
                    x = row.x;
                    y += size + 3f;
                    pos.y += size + 3f;
                }
                var swatch = new Rect(x, y, size, size);
                if (isSelected != null && isSelected(options[i]))
                {
                    Widgets.DrawBoxSolid(swatch.ExpandedBy(2f), Color.white);
                }
                Widgets.DrawBoxSolid(swatch, colorGetter(options[i]));
                if (Widgets.ButtonInvisible(swatch))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    onSelect(options[i]);
                }
                x += size + 4f;
            }
        }
    }
}
