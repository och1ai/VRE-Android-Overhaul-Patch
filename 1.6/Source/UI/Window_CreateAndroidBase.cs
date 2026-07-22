using RimWorld;
using VREAndroids;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Grammar;
using Verse.Sound;

namespace VREAndroidsOverhaul
{
    public abstract class Window_CreateAndroidBase : GeneCreationDialogBase
    {
        protected Action callback;

        protected List<GeneDef> selectedGenes = new List<GeneDef>();

        protected bool? selectedCollapsed = false;

        protected HashSet<GeneCategoryDef> matchingCategories = new HashSet<GeneCategoryDef>();

        protected Dictionary<GeneCategoryDef, bool> collapsedCategories = new Dictionary<GeneCategoryDef, bool>();

        protected bool hoveredAnyGene;

        protected GeneDef hoveredGene;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 1036), UI.screenHeight - 4);
        protected override List<GeneDef> SelectedGenes => selectedGenes;

        public List<ThingDefCount> requiredItems;

        public bool disableAndroidHardwareLimitation;

        // Blood hardware is mutually exclusive and can only be swapped while the body is being
        // built (creation), not at the behaviorist/modification station.
        protected virtual bool CanSwapBlood => false;

        // Power source (reactor / battery) is likewise a mutually-exclusive build-time choice.
        protected virtual bool CanSwapPower => false;

        // Chassis (reinforced / delicate) is a mutually-exclusive but fully optional build-time choice.
        protected virtual bool CanSwapChassis => false;

        // A locked component is shown in the selected list but cannot be toggled off or swapped (used
        // by the behaviorist station for the fixed blood/power hardware).
        protected virtual bool IsGeneLocked(GeneDef geneDef) => false;

        // "reactor powered" / "neutroamine blood or hemogenic blood" - the hardware a component needs,
        // formatted for a tooltip line, or null when it has no requirement.
        protected string RequiredHardwareLabel(GeneDef geneDef)
        {
            var req = geneDef.RequiredHardware();
            if (req == null)
            {
                return null;
            }
            return string.Join(" " + "VREA.Or".Translate() + " ", req.Select(x => x.LabelCap.Resolve()));
        }

        // First selected component whose required hardware is missing, or null if all requirements met.
        protected GeneDef FirstUnmetRequirementGene()
        {
            foreach (var g in selectedGenes)
            {
                if (!g.RequirementSatisfiedBy(selectedGenes))
                {
                    return g;
                }
            }
            return null;
        }

        // "incapable of social" - the components this gene is declared to clash with, for a tooltip line.
        protected string ConflictLabel(GeneDef geneDef)
        {
            var names = geneDef.ConflictsWith();
            if (names == null || names.Count == 0)
            {
                return null;
            }
            return string.Join(", ", names.Select(n => DefDatabase<GeneDef>.GetNamedSilentFail(n)?.LabelCap.Resolve() ?? n));
        }

        // First selected component that clashes with another selected component, or null if none do.
        protected GeneDef FirstConflictingGene()
        {
            foreach (var g in selectedGenes)
            {
                if (g.ConflictInSelection(selectedGenes) != null)
                {
                    return g;
                }
            }
            return null;
        }

        public Window_CreateAndroidBase(Action callback)
        {
            this.callback = callback;
            xenotypeName = GetAndroidTypeName();
            forcePause = true;
            absorbInputAroundWindow = true;
            alwaysUseFullBiostatsTableHeight = true;
            searchWidgetOffsetX = ButSize.x * 2f + 4f;
            foreach (GeneCategoryDef allDef in DefDatabase<GeneCategoryDef>.AllDefs)
            {
                collapsedCategories.Add(allDef, value: false);
            }
            // Auto-select the immutable core hardware, but pick exactly one blood type (neutroamine
            // by default) and one power source (reactor by default) rather than all of them.
            selectedGenes = Utils.AndroidGenesGenesInOrder
                .Where(x => x.CanBeRemovedFromAndroid() is false && x.IsBloodGene() is false && x.IsPowerGene() is false).ToList();
            selectedGenes.Add(OverhaulDefOf.NeutroCirculation);
            selectedGenes.Add(OverhaulDefOf.ReactorPowered);
            OnGenesChanged();
        }

        // Skin colour, hair colour and body shape are chosen ONLY in the android designer, never as picked
        // components, so those genes never show up in the androidtype (component) editor.
        public virtual bool GeneValidator(GeneDef x) =>
            !ForkCompat.IsSkinColorGene(x) && !ForkCompat.IsHairColorGene(x) && !ForkCompat.IsBodyTypeGene(x);

        public override void DoWindowContents(Rect rect)
        {
            Rect rect2 = rect;
            rect2.yMax -= ButSize.y + 4f;
            Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, 35f);
            Text.Font = GameFont.Medium;
            Widgets.Label(rect3, Header);
            Text.Font = GameFont.Small;
            DrawSearchRect(rect);
            rect2.yMin += 39f;
            float num = rect.width * 0.25f - Margin - 10f;
            float num2 = num - 24f - 10f;
            float num3 = Mathf.Max(AndroidStatsTable.HeightForBiostats(requiredItems), postXenotypeHeight);
            Rect rect4 = new Rect(rect2.x + Margin, rect2.y, rect2.width - Margin * 2f, rect2.height - num3 - 8f);
            DrawGenes(rect4);
            float num4 = rect4.yMax + 4f;
            Rect rect5 = new Rect(rect2.x + Margin + 10f, num4, rect.width * 0.75f - Margin * 3f - 10f, num3);
            rect5.yMax = rect4.yMax + num3 + 4f;
            AndroidStatsTable.Draw(rect5, gcx, met, requiredItems);
            string text = AndroidName().CapitalizeFirst() + ":";
            Rect rect6 = new Rect(rect5.xMax + Margin, num4, Text.CalcSize(text).x, Text.LineHeight);
            Widgets.Label(rect6, text);
            Rect rect7 = new Rect(rect6.xMin, rect6.y + Text.LineHeight, num, Text.LineHeight);
            rect7.xMax = rect2.xMax - Margin - 17f - num2 * 0.25f;
            string text2 = xenotypeName;
            xenotypeName = Widgets.TextField(rect7, xenotypeName, 40, VanillaGeneUI.ValidSymbolRegex);
            if (text2 != xenotypeName)
            {
                if (xenotypeName.Length > text2.Length && xenotypeName.Length > 3)
                {
                    xenotypeNameLocked = true;
                }
                else if (xenotypeName.Length == 0)
                {
                    xenotypeNameLocked = false;
                }
            }
            Rect rect8 = new Rect(rect7.xMax + 4f, rect7.yMax - 35f, 35f, 35f);
            VanillaGeneUI.DrawIconSelector(this, rect8);
            Rect rect9 = new Rect(rect7.x, rect7.yMax + 4f, num2 * 0.75f - 4f, 24f);
            if (Widgets.ButtonText(rect9, "Randomize".Translate()))
            {
                GUI.FocusControl(null);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                xenotypeName = GetAndroidTypeName();
            }
            Rect rect10 = new Rect(rect9.xMax + 4f, rect9.y, num2 * 0.25f, 24f);
            if (Widgets.ButtonText(rect10, "..."))
            {
                List<string> list = new List<string>();
                int num5 = 0;
                while (list.Count < 20)
                {
                    string text3 = GetAndroidTypeName();
                    if (text3.NullOrEmpty())
                    {
                        break;
                    }
                    if (list.Contains(text3) || text3 == xenotypeName)
                    {
                        num5++;
                        if (num5 >= 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        list.Add(text3);
                    }
                }
                List<FloatMenuOption> list2 = new List<FloatMenuOption>();
                for (int j = 0; j < list.Count; j++)
                {
                    string i = list[j];
                    list2.Add(new FloatMenuOption(i, delegate
                    {
                        xenotypeName = i;
                    }));
                }
                if (list2.Any())
                {
                    Find.WindowStack.Add(new FloatMenu(list2));
                }
            }
            Rect rect11 = new Rect(rect10.xMax + 10f, rect9.y, 24f, 24f);
            if (Widgets.ButtonImage(rect11, xenotypeNameLocked ? LockedTex : UnlockedTex))
            {
                xenotypeNameLocked = !xenotypeNameLocked;
                if (xenotypeNameLocked)
                {
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                }
                else
                {
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                }
            }
            if (Mouse.IsOver(rect11))
            {
                string text4 = "LockNameButtonDesc".Translate() + "\n\n" + (xenotypeNameLocked ? "LockNameOn" : "LockNameOff").Translate();
                TooltipHandler.TipRegion(rect11, text4);
            }
            postXenotypeHeight = rect11.yMax - num4;
            PostXenotypeOnGUI(rect6.xMin, rect9.y + 24f);
            Rect rect12 = rect;
            rect12.yMin = rect12.yMax - ButSize.y;
            DoBottomButtons(rect12);
        }

        private string GetAndroidTypeName()
        {
            var rootKeyword = VREA_DefOf.VREA_AndroidTypeNameMaker.RulesPlusIncludes
                .Where(x => x.keyword == "r_name").RandomElement().keyword;
            var request = default(GrammarRequest);
            request.Rules.Add(new Rule_String("TotalComplexityNumber", gcx.ToString()));
            request.Includes.Add(VREA_DefOf.VREA_AndroidTypeNameMaker);
            return GrammarResolver.Resolve(rootKeyword, request);
        }

        protected virtual TaggedString AndroidName()
        {
            return "VREA.AndroidName".Translate();
        }

        protected override void Accept()
        {
            AcceptInner();
            callback?.Invoke();
            Close();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            // base.PreOpen resets iconDef to the vanilla blank "Basic" face every time the window opens, so
            // override it here (after base) to start a new androidtype on the same symbol the stock basic
            // android xenotype uses (the drone), for consistency with the designer's default.
            iconDef = OverhaulDefOf.AndroidXenotypeIcon7;
        }

        public override void PostOpen()
        {
            if (!ModLister.CheckBiotech("xenotype creation"))
            {
                Close(doCloseSound: false);
            }
            else
            {
                base.PostOpen();
            }
        }

        protected override void DrawGenes(Rect rect)
        {
            hoveredAnyGene = false;
            GUI.BeginGroup(rect);
            float curY = 0f;
            DrawSection(new Rect(0f, 0f, rect.width, selectedHeight), selectedGenes, "VREA.SelectedComponents".Translate(), ref curY, ref selectedHeight, adding: false, rect, ref selectedCollapsed);
            if (!selectedCollapsed.Value)
            {
                curY += 10f;
            }
            float num = curY;
            Widgets.Label(0f, ref curY, rect.width, "VREA.Components".Translate().CapitalizeFirst());
            curY += 10f;
            float height = curY - num - 4f;
            if (Widgets.ButtonText(new Rect(rect.width - 150f - 16f, num, 150f, height), "CollapseAllCategories".Translate()))
            {
                SoundDefOf.TabClose.PlayOneShotOnCamera();
                foreach (GeneCategoryDef allDef in DefDatabase<GeneCategoryDef>.AllDefs)
                {
                    collapsedCategories[allDef] = true;
                }
            }
            if (Widgets.ButtonText(new Rect(rect.width - 300f - 4f - 16f, num, 150f, height), "ExpandAllCategories".Translate()))
            {
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                foreach (GeneCategoryDef allDef2 in DefDatabase<GeneCategoryDef>.AllDefs)
                {
                    collapsedCategories[allDef2] = false;
                }
            }
            float num2 = curY;
            Rect rect2 = new Rect(0f, curY, rect.width - 16f, scrollHeight);
            Widgets.BeginScrollView(new Rect(0f, curY, rect.width, rect.height - curY), ref scrollPosition, rect2);
            Rect containingRect = rect2;
            containingRect.y = curY + scrollPosition.y;
            containingRect.height = rect.height;
            bool? collapsed = null;
            DrawSection(rect, Utils.AndroidGenesGenesInOrder.Where(GeneValidator).ToList(), null, ref curY, ref unselectedHeight, adding: true, containingRect, ref collapsed);
            if (Event.current.type == EventType.Layout)
            {
                scrollHeight = curY - num2;
            }
            Widgets.EndScrollView();
            GUI.EndGroup();
            if (!hoveredAnyGene)
            {
                hoveredGene = null;
            }
        }

        private void DrawSection(Rect rect, List<GeneDef> genes, string label, ref float curY, ref float sectionHeight, bool adding, Rect containingRect, ref bool? collapsed)
        {
            float curX = 4f;
            if (!label.NullOrEmpty())
            {
                Rect rect2 = new Rect(0f, curY, rect.width, Text.LineHeight);
                rect2.xMax -= (adding ? 16f : (Text.CalcSize("ClickToAddOrRemove".Translate()).x + 4f));
                if (collapsed.HasValue)
                {
                    Rect position = new Rect(rect2.x, rect2.y + (rect2.height - 18f) / 2f, 18f, 18f);
                    GUI.DrawTexture(position, collapsed.Value ? TexButton.Reveal : TexButton.Collapse);
                    if (Widgets.ButtonInvisible(rect2))
                    {
                        collapsed = !collapsed;
                        if (collapsed.Value)
                        {
                            SoundDefOf.TabClose.PlayOneShotOnCamera();
                        }
                        else
                        {
                            SoundDefOf.TabOpen.PlayOneShotOnCamera();
                        }
                    }
                    if (Mouse.IsOver(rect2))
                    {
                        Widgets.DrawHighlight(rect2);
                    }
                    rect2.xMin += position.width;
                }
                Widgets.Label(rect2, label);
                if (!adding)
                {
                    Text.Anchor = TextAnchor.UpperRight;
                    GUI.color = ColoredText.SubtleGrayColor;
                    Widgets.Label(new Rect(rect2.xMax - 18f, curY, rect.width - rect2.width, Text.LineHeight), "ClickToAddOrRemove".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                curY += Text.LineHeight + 3f;
            }
            if (collapsed == true)
            {
                if (Event.current.type == EventType.Layout)
                {
                    sectionHeight = 0f;
                }
                return;
            }
            float num = curY;
            bool flag = false;
            float num2 = 34f + GeneSize.x + 8f;
            float num3 = rect.width - 16f;
            float num4 = num2 + 4f;
            float b = (num3 - num4 * Mathf.Floor(num3 / num4)) / 2f;
            Rect rect3 = new Rect(0f, curY, rect.width, sectionHeight);
            if (!adding)
            {
                Widgets.DrawRectFast(rect3, Widgets.MenuSectionBGFillColor);
            }
            curY += 4f;
            if (!genes.Any())
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(rect3, "(" + "NoneLower".Translate() + ")");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                GeneCategoryDef geneCategoryDef = null;
                int num5 = 0;
                for (int i = 0; i < genes.Count; i++)
                {
                    GeneDef geneDef = genes[i];
                    if ((adding && quickSearchWidget.filter.Active && (!matchingGenes.Contains(geneDef) || selectedGenes.Contains(geneDef)) && !matchingCategories.Contains(geneDef.displayCategory)))
                    {
                        continue;
                    }
                    bool flag2 = false;
                    if (curX + num2 > num3)
                    {
                        curX = 4f;
                        curY += GeneSize.y + 8f + 4f;
                        flag2 = true;
                    }
                    bool flag3 = quickSearchWidget.filter.Active && (matchingGenes.Contains(geneDef)
                        || matchingCategories.Contains(geneDef.displayCategory));
                    bool flag4 = collapsedCategories[geneDef.displayCategory] && !flag3;
                    if (adding && geneCategoryDef != geneDef.displayCategory)
                    {
                        if (!flag2 && flag)
                        {
                            curX = 4f;
                            curY += GeneSize.y + 8f + 4f;
                        }
                        geneCategoryDef = geneDef.displayCategory;
                        Rect rect4 = new Rect(curX, curY, rect.width - 8f, Text.LineHeight);
                        if (!flag3)
                        {
                            Rect position2 = new Rect(rect4.x, rect4.y + (rect4.height - 18f) / 2f, 18f, 18f);
                            GUI.DrawTexture(position2, flag4 ? TexButton.Reveal : TexButton.Collapse);
                            if (Widgets.ButtonInvisible(rect4))
                            {
                                collapsedCategories[geneDef.displayCategory] = !collapsedCategories[geneDef.displayCategory];
                                if (collapsedCategories[geneDef.displayCategory])
                                {
                                    SoundDefOf.TabClose.PlayOneShotOnCamera();
                                }
                                else
                                {
                                    SoundDefOf.TabOpen.PlayOneShotOnCamera();
                                }
                            }
                            if (num5 % 2 == 1)
                            {
                                Widgets.DrawLightHighlight(rect4);
                            }
                            if (Mouse.IsOver(rect4))
                            {
                                Widgets.DrawHighlight(rect4);
                            }
                            rect4.xMin += position2.width;
                        }
                        Widgets.Label(rect4, geneCategoryDef.LabelCap);
                        curY += rect4.height;
                        if (!flag4)
                        {
                            GUI.color = Color.grey;
                            Widgets.DrawLineHorizontal(curX, curY, rect.width - 8f);
                            GUI.color = Color.white;
                            curY += 10f;
                        }
                        num5++;
                    }
                    if (adding && flag4)
                    {
                        flag = false;
                        if (Event.current.type == EventType.Layout)
                        {
                            sectionHeight = curY - num;
                        }
                        continue;
                    }
                    curX = Mathf.Max(curX, b);
                    flag = true;
                    if (DrawGene(geneDef, !adding, ref curX, curY, num2, containingRect, flag3))
                    {
                        if (IsGeneLocked(geneDef))
                        {
                            // Fixed hardware (e.g. the android's built-in blood/power at the
                            // behaviorist station): shown for reference but not changeable.
                            SoundDefOf.ClickReject.PlayOneShotOnCamera();
                            break;
                        }
                        if (geneDef.IsBloodGene() && CanSwapBlood)
                        {
                            // Blood is mutually exclusive: switching to another type replaces the
                            // current one. Clicking the active type does nothing (one is required).
                            if (!selectedGenes.Contains(geneDef))
                            {
                                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                                selectedGenes.RemoveAll(g => g.IsBloodGene());
                                selectedGenes.Add(geneDef);
                                if (!xenotypeNameLocked)
                                {
                                    xenotypeName = GetAndroidTypeName();
                                }
                                OnGenesChanged();
                            }
                            break;
                        }
                        if (geneDef.IsChassisGene() && CanSwapChassis)
                        {
                            // Chassis is mutually exclusive but optional: clicking the active one
                            // removes it (leaving no chassis), clicking a different one replaces
                            // whatever chassis was selected.
                            if (selectedGenes.Contains(geneDef))
                            {
                                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                                selectedGenes.Remove(geneDef);
                            }
                            else
                            {
                                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                                selectedGenes.RemoveAll(g => g.IsChassisGene());
                                selectedGenes.Add(geneDef);
                            }
                            if (!xenotypeNameLocked)
                            {
                                xenotypeName = GetAndroidTypeName();
                            }
                            OnGenesChanged();
                            break;
                        }
                        if (geneDef.IsPowerGene() && CanSwapPower)
                        {
                            // Power source is mutually exclusive, same as blood: pick exactly one.
                            if (!selectedGenes.Contains(geneDef))
                            {
                                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                                selectedGenes.RemoveAll(g => g.IsPowerGene());
                                selectedGenes.Add(geneDef);
                                if (!xenotypeNameLocked)
                                {
                                    xenotypeName = GetAndroidTypeName();
                                }
                                OnGenesChanged();
                            }
                            break;
                        }
                        if (selectedGenes.Contains(geneDef))
                        {
                            if (geneDef.CanBeRemovedFromAndroid() || disableAndroidHardwareLimitation && geneDef.CanBeRemovedFromAndroidAwakened())
                            {
                                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                                selectedGenes.Remove(geneDef);
                            }
                        }
                        else
                        {
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                            selectedGenes.Add(geneDef);
                        }
                        if (!xenotypeNameLocked)
                        {
                            xenotypeName = GetAndroidTypeName();
                        }
                        OnGenesChanged();
                        break;
                    }
                }
            }
            if (!adding || flag)
            {
                curY += GeneSize.y + 12f;
            }
            if (Event.current.type == EventType.Layout)
            {
                sectionHeight = curY - num;
            }
        }

        private bool DrawGene(GeneDef geneDef, bool selectedSection, ref float curX, float curY, float packWidth, Rect containingRect, bool isMatch)
        {
            bool result = false;
            Rect rect = new Rect(curX, curY, packWidth, GeneSize.y + 8f);
            if (!containingRect.Overlaps(rect))
            {
                curX = rect.xMax + 4f;
                return false;
            }
            bool selected = !selectedSection && selectedGenes.Contains(geneDef);
            bool overridden = leftChosenGroups.Any((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(geneDef));
            Widgets.DrawOptionBackground(rect, selected);
            curX += 4f;
            DrawBiostats(geneDef.biostatCpx, geneDef.biostatMet, geneDef.biostatArc, ref curX, curY, 4f);
            Rect rect2 = new Rect(curX, curY + 4f, GeneSize.x, GeneSize.y);
            if (isMatch)
            {
                Widgets.DrawStrongHighlight(rect2.ExpandedBy(6f));
            }
            GeneUIUtility.DrawGeneDef(geneDef, rect2, GeneType.Xenogene, () => GeneTip(geneDef, selectedSection), doBackground: false, clickable: false, overridden);
            curX += GeneSize.x + 4f;
            if (Mouse.IsOver(rect))
            {
                hoveredGene = geneDef;
                hoveredAnyGene = true;
            }
            else if (hoveredGene != null && geneDef.ConflictsWith(hoveredGene))
            {
                Widgets.DrawLightHighlight(rect);
            }
            if (Widgets.ButtonInvisible(rect))
            {
                result = true;
            }
            curX = Mathf.Max(curX, rect.xMax + 4f);
            return result;
        }

        public static void DrawBiostats(int gcx, int met, int arc, ref float curX, float curY, float margin = 6f)
        {
            float num = GeneSize.y / 3f;
            float num2 = 0f;
            float num3 = Text.LineHeightOf(GameFont.Small);
            Rect iconRect = new Rect(curX, curY + margin + num2, num3, num3);
            VanillaGeneUI.DrawStat(iconRect, GeneUtility.GCXTex, gcx.ToString(), num3);
            Rect rect = new Rect(curX, iconRect.y, 38f, num3);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, "Complexity".Translate().Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + "VREA.ComplexityTotalDesc".Translate());
            }
            num2 += num;
            if (met != 0)
            {
                Rect iconRect2 = new Rect(curX, curY + margin + num2, num3, num3);
                if (met < 10)
                {
                    VanillaGeneUI.DrawStat(iconRect2, AndroidStatsTable.PowerEfficiencyIconTex, met.ToStringWithSign(), num3);
                }
                else
                {
                    GUI.DrawTexture(iconRect2, AndroidStatsTable.PowerEfficiencyIconTex.Texture);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(iconRect2.xMax - 6, iconRect2.y, num3 + 6, num3), met.ToStringWithSign());
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                Rect rect2 = new Rect(curX, iconRect2.y, 38f, num3);
                if (Mouse.IsOver(rect2))
                {
                    Widgets.DrawHighlight(rect2);
                    TooltipHandler.TipRegion(rect2, "VREA.PowerEfficiency".Translate().Colorize(ColoredText.TipSectionTitleColor) + "\n\n"
                        + "VREA.PowerEfficiencyTotalDesc".Translate());
                }
                num2 += num;
            }
            curX += 34f;
        }

        private string GeneTip(GeneDef geneDef, bool selectedSection)
        {
            string text = null;
            if (selectedSection)
            {
                if (leftChosenGroups.Any((GeneLeftChosenGroup x) => x.leftChosen == geneDef))
                {
                    text = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.leftChosen == geneDef));
                }
                else if (cachedOverriddenGenes.Contains(geneDef))
                {
                    text = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(geneDef)));
                }
                else if (randomChosenGroups.ContainsKey(geneDef))
                {
                    text = ("VREA.ComponentWillBeRandomChosen".Translate() + ":\n" + randomChosenGroups[geneDef].Select((GeneDef x) => x.label).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
                }
            }
            if (selectedGenes.Contains(geneDef) && geneDef.prerequisite != null && !selectedGenes.Contains(geneDef.prerequisite))
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n\n";
                }
                text += ("VREA.MessageComponentMissingPrerequisite".Translate(geneDef.label).CapitalizeFirst() + ": " + geneDef.prerequisite.LabelCap).Colorize(ColorLibrary.RedReadable);
            }
            // "Requires: reactor powered" - shown for any component that depends on specific hardware,
            // like the way Biotech sanguophage genes list their hemogenic requirement. Turns red when
            // the required hardware is not part of the current selection.
            string requiredLabel = RequiredHardwareLabel(geneDef);
            if (requiredLabel != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n\n";
                }
                string requiresLine = "VREA.ComponentRequires".Translate(requiredLabel);
                text += geneDef.RequirementSatisfiedBy(selectedGenes)
                    ? requiresLine.Colorize(ColoredText.TipSectionTitleColor)
                    : requiresLine.Colorize(ColorLibrary.RedReadable);
            }
            // "Conflicts with: incapable of social" - listed for any component that declares clashes,
            // and turned red when a clashing component is actually part of the current selection.
            string conflictLabel = ConflictLabel(geneDef);
            if (conflictLabel != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n\n";
                }
                string conflictsLine = "VREA.ComponentConflictsWith".Translate(conflictLabel);
                text += geneDef.ConflictInSelection(selectedGenes) == null
                    ? conflictsLine.Colorize(ColoredText.TipSectionTitleColor)
                    : conflictsLine.Colorize(ColorLibrary.RedReadable);
            }
            if (!text.NullOrEmpty())
            {
                text += "\n\n";
            }
            if (geneDef.CanBeRemovedFromAndroid() || disableAndroidHardwareLimitation && geneDef.CanBeRemovedFromAndroidAwakened())
            {
                return text + (selectedGenes.Contains(geneDef) ? "ClickToRemove" : "ClickToAdd").Translate().Colorize(ColoredText.SubtleGrayColor);
            }
            return text;
            static string GroupInfo(GeneLeftChosenGroup group)
            {
                if (group == null)
                {
                    return null;
                }
                return ("VREA.ComponentLeftmostActive".Translate() + ":\n  - " + group.leftChosen.LabelCap + " (" + "Active".Translate() + ")" + "\n" + group.overriddenGenes.Select((GeneDef x) => (x.label + " (" + "Suppressed".Translate() + ")").Colorize(ColorLibrary.RedReadable)).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
            }
        }

        protected override void PostXenotypeOnGUI(float curX, float curY)
        {

        }

        protected override void OnGenesChanged()
        {
            selectedGenes.SortGeneDefs();
            base.OnGenesChanged();
        }

        protected override void DoBottomButtons(Rect rect)
        {
            base.DoBottomButtons(rect);
            string text = null;
            if (leftChosenGroups.Any())
            {
                int num = leftChosenGroups.Sum((GeneLeftChosenGroup x) => x.overriddenGenes.Count);
                GeneLeftChosenGroup geneLeftChosenGroup = leftChosenGroups[0];
                text = "VREA.ComponentsConflict".Translate() + ": " + "GenesConflictDesc".Translate(geneLeftChosenGroup.leftChosen.Named("FIRST"), geneLeftChosenGroup.overriddenGenes[0].Named("SECOND")).CapitalizeFirst() + ((num > 1) ? (" +" + (num - 1)) : string.Empty);
            }
            else
            {
                GeneDef conflicting = FirstConflictingGene();
                GeneDef unmet = FirstUnmetRequirementGene();
                if (conflicting != null)
                {
                    text = "VREA.ComponentsConflict".Translate() + ": " + conflicting.LabelCap + " / " + conflicting.ConflictInSelection(selectedGenes).LabelCap;
                }
                else if (unmet != null)
                {
                    text = "VREA.ComponentMissingRequirement".Translate(unmet.LabelCap, RequiredHardwareLabel(unmet));
                }
                else if (met < -20)
                {
                    text = "VREA.TooLowEfficiency".Translate();
                }
            }
            if (text != null)
            {
                float x2 = Text.CalcSize(text).x;
                GUI.color = ColorLibrary.RedReadable;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.xMax - ButSize.x - x2 - 4f, rect.y, x2, rect.height), text);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        protected override bool WithinAcceptableBiostatLimits(bool showMessage)
        {
            if (met < AndroidStatsTable.AndroidStatRange.TrueMin)
            {
                if (showMessage)
                {
                    Messages.Message("VREA.EfficiencyTooLowToCreateAndroid".Translate(met.Named("AMOUNT"), AndroidStatsTable.AndroidStatRange.TrueMin.Named("MIN")), null, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            return true;
        }

        protected override bool CanAccept()
        {
            if (leftChosenGroups.Any())
            {
                Messages.Message("VREA.MessageConflictingComponentPresent".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            string text = xenotypeName;
            if (text != null && text.Trim().Length == 0)
            {
                Messages.Message("VREA.AndroidNameCannotBeEmpty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (!WithinAcceptableBiostatLimits(showMessage: true))
            {
                return false;
            }
            List<GeneDef> selectedGenes = SelectedGenes;
            foreach (GeneDef selectedGene in SelectedGenes)
            {
                if (selectedGene.prerequisite != null && !selectedGenes.Contains(selectedGene.prerequisite))
                {
                    Messages.Message("VREA.MessageComponentMissingPrerequisite".Translate(selectedGene.label).CapitalizeFirst() + ": " + selectedGene.prerequisite.LabelCap, null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                if (!selectedGene.RequirementSatisfiedBy(selectedGenes))
                {
                    Messages.Message("VREA.ComponentMissingRequirement".Translate(selectedGene.LabelCap, RequiredHardwareLabel(selectedGene)),
                        null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                var conflict = selectedGene.ConflictInSelection(selectedGenes);
                if (conflict != null)
                {
                    Messages.Message("VREA.ComponentsConflict".Translate() + ": " + selectedGene.LabelCap + " / " + conflict.LabelCap,
                        null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
            }
            return true;
        }
        protected abstract void AcceptInner();

        protected override void UpdateSearchResults()
        {
            quickSearchWidget.noResultsMatched = false;
            matchingGenes.Clear();
            matchingCategories.Clear();
            if (!quickSearchWidget.filter.Active)
            {
                return;
            }
            foreach (GeneDef item in GeneUtility.GenesInOrder)
            {
                if (!selectedGenes.Contains(item))
                {
                    if (quickSearchWidget.filter.Matches(item.label))
                    {
                        matchingGenes.Add(item);
                    }
                    if (quickSearchWidget.filter.Matches(item.displayCategory.label))
                    {
                        matchingCategories.Add(item.displayCategory);
                    }
                }
            }
            quickSearchWidget.noResultsMatched = !matchingGenes.Any() && !matchingCategories.Any();
        }
    }
}
