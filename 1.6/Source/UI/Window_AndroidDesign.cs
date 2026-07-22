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
    // Altered-Carbon-style android designer - the first step of printing. The player picks (or builds)
    // an android type here and shapes the body: full name, gender, android age, head/body type, skin
    // colour (applied as the matching melanin gene so it persists), hair, and - with the ideological
    // subroutine - the ideoligion and conviction. A live throwaway android drives the preview.
    [HotSwappable]
    public class Window_AndroidDesign : Window
    {
        private readonly Building_AndroidCreationStation station;
        private readonly Pawn creator;

        private List<GeneDef> currentGenes;
        private string xenotypeName;
        private XenotypeIconDef iconDef;
        private bool typeChosen;
        private Pawn android;

        // The player's last-picked appearance genes (event-driven). These are the single body-type, skin-
        // colour and hair-colour genes baked into the printed body, preserved across type changes and
        // seeded to the closest match on the first preview so one of each is always present.
        private GeneDef chosenSkinGene;
        private GeneDef chosenHairGene;
        private GeneDef chosenBodyGene;

        // Preserved across type changes so re-picking the type doesn't wipe the chosen identity.
        private string firstName = "";
        private string nickName = "";
        private string lastName = "";
        private long bioAgeTicks;

        private Vector2 scrollPos;
        private Vector2 hairScroll;
        private Vector2 beardScroll;
        private Vector2 bodyTattooScroll;
        private Vector2 faceTattooScroll;
        private float lastContentHeight = 600f;

        // Wider than before: the right column now hosts the full vanilla character-card bio, which needs
        // ~520px (backstory column + skills column side by side) beside the controls.
        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 1040f), Mathf.Min(UI.screenHeight, 840f));

        public Window_AndroidDesign(Building_AndroidCreationStation station, Pawn creator)
        {
            this.station = station;
            this.creator = creator;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            currentGenes = DefaultTypeGenes();
            // Start already on the stock "basic android" type (its genes are the default set) so Start print
            // is enabled straight away; the player can still swap to a saved/new type.
            xenotypeName = DefDatabase<XenotypeDef>.GetNamedSilentFail("VREA_AndroidBasic")?.LabelCap
                ?? "VREA.AndroidName".Translate();
            // Match the base mod's basic-android xenotype icon (the drone), not the generic robot.
            iconDef = OverhaulDefOf.AndroidXenotypeIcon7;
            typeChosen = true;
            bioAgeTicks = (long)Rand.Range(20f, 50f) * GenDate.TicksPerYear;
            RebuildPreview();
        }

        private static List<GeneDef> DefaultTypeGenes()
        {
            var genes = Utils.AndroidGenesGenesInOrder
                .Where(x => x.CanBeRemovedFromAndroid() is false && x.IsBloodGene() is false && x.IsPowerGene() is false)
                .ToList();
            genes.Add(OverhaulDefOf.NeutroCirculation);
            genes.Add(OverhaulDefOf.ReactorPowered);
            return genes;
        }

        private void RebuildPreview()
        {
            Pawn built;
            try
            {
                built = station.MakeDesignAndroid(currentGenes);
            }
            catch (Exception e)
            {
                Log.Error("[VREA] Failed to build design preview: " + e);
                return; // keep whatever preview we already had so the window (and confirm flow) survive
            }
            android = built;
            // Stamp the chosen androidtype's name/icon onto the preview so the bio's xenotype badge shows
            // the picked type (and updates every time the type is changed), instead of a stale default.
            if (android.genes != null)
            {
                android.genes.xenotypeName = xenotypeName;
                android.genes.iconDef = iconDef;
            }
            android.Rotation = Rot4.South;
            android.ageTracker.AgeBiologicalTicks = bioAgeTicks;
            android.ageTracker.AgeChronologicalTicks = 0;
            // The generator can roll an odd (modded) head; default to a standard face.
            var standardHeads = StandardHeads();
            if (standardHeads.Any() && !standardHeads.Contains(android.story.headType))
            {
                android.story.headType = standardHeads.RandomElement();
            }
            // Body shape is gene-driven: keep the player's last pick across type changes (seed it the first
            // time from whatever the fresh body rolled) and force exactly that one body gene.
            if (BodyTypeGenes().Any())
            {
                if (chosenBodyGene == null)
                {
                    // Default to a Standard build rather than whatever the fresh body rolled (often Fat).
                    chosenBodyGene = BodyTypeGenes().FirstOrDefault(g => g.bodyType == GeneticBodyType.Standard)
                        ?? CurrentBodyGene();
                }
                ApplyBodyGene(chosenBodyGene);
            }
            // Always keep a concrete skin/hair colour gene selected so the printed body carries the
            // matching colour gene (not just a story override). Seed from the closest match the first
            // time; preserve the player's pick (and re-apply it) across type changes.
            if (chosenSkinGene == null && ForkCompat.AllSkinColorAndroidGenes.Any())
            {
                chosenSkinGene = ClosestColorGene(ForkCompat.AllSkinColorAndroidGenes, ForkCompat.SkinColorOf, android.story.SkinColor);
            }
            if (chosenSkinGene != null) android.story.skinColorOverride = ForkCompat.SkinColorOf(chosenSkinGene);
            if (chosenHairGene == null && Utils.HairColorAndroidGenes.Any())
            {
                chosenHairGene = ClosestColorGene(Utils.HairColorAndroidGenes, g => g.hairColorOverride.Value, android.story.HairColor);
            }
            if (chosenHairGene != null) android.story.HairColor = chosenHairGene.hairColorOverride.Value;
            // Seed only a first name from the generated body the first time (no nickname / last name),
            // then keep the player's edits.
            if (firstName.NullOrEmpty() && nickName.NullOrEmpty() && lastName.NullOrEmpty())
            {
                var nt = android.Name as NameTriple;
                firstName = (nt != null && !nt.First.NullOrEmpty()) ? nt.First : (nt?.Nick ?? android.LabelShortCap);
                nickName = "";
                lastName = "";
            }
            ApplyName();
            Refresh();
        }

        private void SetType(CustomXenotype xeno)
        {
            if (xeno?.genes == null)
            {
                return;
            }
            currentGenes = xeno.genes.ToList();
            xenotypeName = xeno.name;
            iconDef = xeno.IconDef;
            typeChosen = true;
            RebuildPreview();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(0, 0, inRect.width, 34f), "VREA.DesignAndroid".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (android == null)
            {
                Widgets.Label(new Rect(20f, 60f, inRect.width - 40f, 60f), "Preview could not be built.");
                return;
            }

            float bottomBar = 40f;
            var body = new Rect(0, 44f, inRect.width, inRect.height - 44f - bottomBar - 6f);

            // Right column holds the portrait (fixed size, centred) plus the vanilla bio card below it; it
            // must stay wide enough for the card's two columns while leaving the controls at least ~320px.
            float previewW = Mathf.Min(520f, body.width - 340f);
            DrawPreview(new Rect(body.xMax - previewW, body.y, previewW, body.height));

            var leftCol = new Rect(body.x, body.y, body.width - previewW - 20f, body.height);
            var viewRect = new Rect(0, 0, leftCol.width - 20f, lastContentHeight);
            Widgets.BeginScrollView(leftCol, ref scrollPos, viewRect);
            var pos = new Vector2(4f, 0f);
            DrawControls(ref pos, viewRect.width);
            lastContentHeight = pos.y + 10f;
            Widgets.EndScrollView();

            // An android can't be printed without a chosen type - the button stays greyed until one is set.
            var acceptRect = new Rect(inRect.width - 210f, inRect.height - bottomBar + 4f, 210f, bottomBar - 8f);
            if (!typeChosen)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                TooltipHandler.TipRegion(acceptRect, "VREA.SelectTypeFirst".Translate());
            }
            bool clickedStart = Widgets.ButtonText(acceptRect, "VREA.StartPrint".Translate());
            GUI.color = Color.white;
            if (clickedStart)
            {
                if (typeChosen)
                {
                    Confirm();
                }
                else
                {
                    Messages.Message("VREA.SelectTypeFirst".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        private void DrawControls(ref Vector2 pos, float width)
        {
            float sepW = width - 8f;

            // Android type: pick a saved type or build a new one right here. The button shows the chosen
            // type's icon and name once one is set, otherwise it prompts to select.
            VREA_UIHelper.Separator(ref pos, sepW, "VREA.AndroidType".Translate());
            // The type picker has no left label, so centre it under its (centred) section title as a
            // prominent primary action rather than leaving it stranded in the right control column.
            float typeBtnW = VREA_UIHelper.ButtonWidth * 2f;
            var typeRow = new Rect(pos.x + (width - typeBtnW) / 2f, pos.y,
                typeBtnW, VREA_UIHelper.ButtonHeight);
            pos.y += VREA_UIHelper.ButtonHeight + VREA_UIHelper.RowGap;
            bool hasIcon = typeChosen && iconDef?.Icon != null;
            string typeLabel = (typeChosen && !xenotypeName.NullOrEmpty()) ? xenotypeName : "VREA.SelectAndroidType".Translate();
            // Native button centring (identical to the Gender/Body/Hair cyclers), with the type icon laid
            // over the left edge - the short type name stays centred and clear of the icon.
            if (Widgets.ButtonText(typeRow, typeLabel))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("VREA.CreateNewType".Translate(), OpenComponentEditor),
                    new FloatMenuOption("VREA.LoadSavedType".Translate(), () =>
                        Find.WindowStack.Add(new Dialog_AndroidProjectList_Load(SetType))),
                }));
            }
            if (hasIcon)
            {
                GUI.DrawTexture(new Rect(typeRow.x + 6f, typeRow.y + (typeRow.height - 24f) / 2f, 24f, 24f), iconDef.Icon);
            }

            VREA_UIHelper.Separator(ref pos, sepW, "VREA.Identity".Translate());
            // The name fields carry tiny "First/Nickname/Last" captions drawn 13px above the row, which
            // otherwise collide with the separator line above - give them room to breathe.
            pos.y += 16f;
            VREA_UIHelper.LabelRow(ref pos, "VREA.FullName".Translate(), out Rect nameRow, VREA_UIHelper.ButtonWidth * 2f);
            float fieldGap = 6f;
            float fieldW = (nameRow.width - fieldGap * 2f) / 3f;
            firstName = LabeledField(new Rect(nameRow.x, nameRow.y, fieldW, nameRow.height), "VREA.NameFirst".Translate(), firstName);
            nickName = LabeledField(new Rect(nameRow.x + fieldW + fieldGap, nameRow.y, fieldW, nameRow.height), "VREA.NameNick".Translate(), nickName);
            lastName = LabeledField(new Rect(nameRow.x + (fieldW + fieldGap) * 2f, nameRow.y, fieldW, nameRow.height), "VREA.NameLast".Translate(), lastName);
            pos.y += 14f;
            ApplyName();

            VREA_UIHelper.Cycler(ref pos, "Gender".Translate(), new List<Gender> { Gender.Male, Gender.Female },
                android.gender, g => g.GetLabel().CapitalizeFirst(), g =>
                {
                    android.gender = g;
                    // Standard heads are gender-specific, so drop to a valid one for the new gender.
                    var heads = StandardHeads();
                    if (heads.Any() && !heads.Contains(android.story.headType))
                    {
                        android.story.headType = heads.First();
                    }
                    Refresh();
                });

            VREA_UIHelper.LabelRow(ref pos, "VREA.AndroidAge".Translate(), out Rect ageRow, VREA_UIHelper.ButtonWidth * 2f);
            int ageYears = android.ageTracker.AgeBiologicalYears;
            int newAge = (int)Widgets.HorizontalSlider(ageRow, ageYears, 18f, 100f,
                label: "VREA.YearsOld".Translate(ageYears), roundTo: 1f);
            if (newAge != ageYears)
            {
                bioAgeTicks = (long)newAge * GenDate.TicksPerYear;
                android.ageTracker.AgeBiologicalTicks = bioAgeTicks;
            }

            VREA_UIHelper.Separator(ref pos, sepW, "VREA.Appearance".Translate());
            // Only the standard human faces are offered - no ghoul/skull/stump or other special heads.
            VREA_UIHelper.Cycler(ref pos, "VREA.HeadShape".Translate(), StandardHeads(), android.story.headType,
                h => h.defName.Replace("_", " "), h => { android.story.headType = h; Refresh(); });
            // Body shape is applied as a body-type gene, so the choice is one and the same across every
            // menu and always forces that body on the printed android.
            var bodyGenes = BodyTypeGenes();
            if (bodyGenes.Any())
            {
                VREA_UIHelper.Cycler(ref pos, "VREA.BodyShape".Translate(), bodyGenes, chosenBodyGene ?? CurrentBodyGene(),
                    g => g.bodyType.Value.ToString(), ApplyBodyGene);
            }

            // Skin colour: swatches come from every android skin-colour gene (melanin tones + tints). The
            // pick is remembered as its gene (so it is printed as the matching colour gene) and also
            // applied as a story override so it always renders - even the tint genes that don't render on
            // their own - with no duplicate colour genes piling up on the finished body.
            var skinGenes = ForkCompat.AllSkinColorAndroidGenes;
            if (skinGenes.Any())
            {
                VREA_UIHelper.ColorSwatches(ref pos, "VREA.SkinColour".Translate(), skinGenes, ForkCompat.SkinColorOf,
                    g => g == chosenSkinGene,
                    g => { chosenSkinGene = g; android.story.skinColorOverride = ForkCompat.SkinColorOf(g); Refresh(); }, perRow: 10);
            }

            // Hair colour sits right under skin colour (and above the hair/beard style grids) so it isn't
            // buried below the tall style grids where the player can't find it.
            var hairColorGenes = Utils.HairColorAndroidGenes;
            if (hairColorGenes.Any())
            {
                VREA_UIHelper.ColorSwatches(ref pos, "VREA.HairColour".Translate(), hairColorGenes,
                    g => g.hairColorOverride.Value,
                    g => g == chosenHairGene,
                    g => { chosenHairGene = g; android.story.HairColor = g.hairColorOverride.Value; Refresh(); }, perRow: 10);
            }

            // Hair & beard: a visual grid of styles like the ideology stylist station, each tinted by the
            // chosen hair colour. A little vertical breathing room keeps the two grids from touching.
            const float gridMargin = 8f;
            var hairs = PermittedHairs();
            if (hairs.Any())
            {
                pos.y += gridMargin;
                VREA_UIHelper.StyleItemGrid(ref pos, ref hairScroll, "VREA.HairType".Translate(), width,
                    hairs.Cast<StyleItemDef>().ToList(), android.story.HairColor,
                    h => android.story.hairDef == h, h => { android.story.hairDef = (HairDef)h; Refresh(); });
                pos.y += gridMargin;
            }
            var beards = PermittedBeards();
            if (beards.Any())
            {
                pos.y += gridMargin;
                VREA_UIHelper.StyleItemGrid(ref pos, ref beardScroll, "VREA.BeardType".Translate(), width,
                    beards.Cast<StyleItemDef>().ToList(), android.story.HairColor,
                    b => android.style?.beardDef == b,
                    b => { if (android.style != null) android.style.beardDef = (BeardDef)b; Refresh(); });
                pos.y += gridMargin;
            }

            // Tattoos (Ideology only): body and face, each a visual grid like hair/beard, at the bottom.
            // Drawn in the skin colour so they read the way they will on the finished body.
            if (ModsConfig.IdeologyActive && android.style != null)
            {
                var bodyTattoos = PermittedTattoos(TattooType.Body);
                if (bodyTattoos.Any())
                {
                    pos.y += gridMargin;
                    VREA_UIHelper.StyleItemGrid(ref pos, ref bodyTattooScroll, "VREA.BodyTattoo".Translate(), width,
                        bodyTattoos.Cast<StyleItemDef>().ToList(), android.story.SkinColor,
                        t => android.style.BodyTattoo == t,
                        t => { android.style.BodyTattoo = (TattooDef)t; Refresh(); });
                    pos.y += gridMargin;
                }
                var faceTattoos = PermittedTattoos(TattooType.Face);
                if (faceTattoos.Any())
                {
                    pos.y += gridMargin;
                    VREA_UIHelper.StyleItemGrid(ref pos, ref faceTattooScroll, "VREA.FaceTattoo".Translate(), width,
                        faceTattoos.Cast<StyleItemDef>().ToList(), android.story.SkinColor,
                        t => android.style.FaceTattoo == t,
                        t => { android.style.FaceTattoo = (TattooDef)t; Refresh(); });
                    pos.y += gridMargin;
                }
            }

            if (ModsConfig.IdeologyActive && android.HasActiveGene(OverhaulDefOf.Ideological) && android.ideo != null)
            {
                VREA_UIHelper.Separator(ref pos, sepW, "VREA.Ideoligion".Translate());
                var ideos = Find.IdeoManager.IdeosListForReading.Where(i => !i.hidden).ToList();
                if (android.Ideo == null && ideos.Any())
                {
                    android.ideo.SetIdeo(ideos[0]);
                }
                VREA_UIHelper.Cycler(ref pos, "VREA.Ideoligion".Translate(), ideos, android.Ideo,
                    i => i.name, i => { android.ideo.SetIdeo(i); Refresh(); });
                VREA_UIHelper.LabelRow(ref pos, "VREA.Conviction".Translate(), out Rect convRow, VREA_UIHelper.ButtonWidth * 2f);
                float cert = Widgets.HorizontalSlider(convRow, android.ideo.Certainty, 0f, 1f, label: android.ideo.Certainty.ToStringPercent());
                CertaintyRef(android.ideo) = Mathf.Clamp01(cert);
            }
        }

        private void DrawPreview(Rect rect)
        {
            // Fixed-size portrait centred at the top of the (now wide) right column, so widening the column
            // for the bio card doesn't blow the portrait up to column width.
            const float portraitSize = 260f;
            var box = new Rect(rect.x + (rect.width - portraitSize) / 2f, rect.y, portraitSize, portraitSize);
            Widgets.DrawMenuSection(box);
            Widgets.DrawShadowAround(box);
            var inner = box.ContractedBy(6f);
            try
            {
                GUI.DrawTexture(inner, PortraitsCache.Get(android, inner.size, android.Rotation, default, 1.2f));
            }
            catch { /* a half-built preview body can momentarily fail to render; skip this frame */ }
            Widgets.InfoCardButton(box.x + 8f, box.yMax - 32f, android);
            // Randomize / rotate as text buttons under the preview.
            float btnY = box.yMax + 6f;
            float halfW = (portraitSize - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(box.x, btnY, halfW, 28f), "VREA.Randomize".Translate()))
            {
                RandomizeLook();
            }
            if (Widgets.ButtonText(new Rect(box.x + halfW + 6f, btnY, halfW, 28f), "VREA.Rotate".Translate()))
            {
                android.Rotation = android.Rotation.Rotated(RotationDirection.Clockwise);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            // The real vanilla character-card bio fills the space under the portrait (backstory, traits,
            // incapabilities and skills), spanning the full column width.
            float bioTop = btnY + 28f + 12f;
            DrawBio(new Rect(rect.x, bioTop, rect.width, rect.yMax - bioTop));
        }

        private void DrawBio(Rect rect)
        {
            // Draw the name ourselves so we can keep the card in showName:false mode - that suppresses the
            // vanilla rename pencil (which the card would otherwise draw at the top-right whenever dev mode
            // is on, even for an unspawned pawn).
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), android.Name.ToStringFull.CapitalizeFirst());
            Text.Font = GameFont.Small;
            var cardRect = new Rect(rect.x, rect.y + 38f, rect.width, rect.height - 38f);
            // Blank the faction just for the card draw: the preview is a design, not a colonist yet, so
            // colonist-only overlays other mods inject into the card (e.g. Vanilla Skills Expanded's
            // expertise star, gated on pawn.IsColonist) don't clutter it. Restored immediately after.
            var savedFaction = android.Faction;
            try
            {
                if (savedFaction != null) android.SetFactionDirect(null);
                CharacterCardUtility.DrawCharacterCard(cardRect, android, null, default, showName: false);
            }
            catch { /* a half-built preview body can momentarily fail to render; skip this frame */ }
            finally
            {
                if (savedFaction != null) android.SetFactionDirect(savedFaction);
            }
        }

        private void OpenComponentEditor()
        {
            var window = new Window_AndroidCreation(station, creator, null);
            window.onTypeResult = SetType;
            Find.WindowStack.Add(window);
        }

        private void RandomizeLook()
        {
            // Random uses only the standard face set (below); the selector still offers every head.
            var heads = StandardHeads();
            var bodies = PermittedBodyTypes();
            var hairs = PermittedHairs();
            if (heads.Any()) android.story.headType = heads.RandomElement();
            if (BodyTypeGenes().Any()) ApplyBodyGene(BodyTypeGenes().RandomElement());
            if (hairs.Any()) android.story.hairDef = hairs.RandomElement();
            if (Utils.HairColorAndroidGenes.Any())
            {
                chosenHairGene = Utils.HairColorAndroidGenes.RandomElement();
                android.story.HairColor = chosenHairGene.hairColorOverride.Value;
            }
            if (ForkCompat.AllSkinColorAndroidGenes.Any())
            {
                chosenSkinGene = ForkCompat.AllSkinColorAndroidGenes.RandomElement();
                android.story.skinColorOverride = ForkCompat.SkinColorOf(chosenSkinGene);
            }
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            Refresh();
        }

        private static string LabeledField(Rect rect, string label, string value)
        {
            var lblRect = new Rect(rect.x, rect.y - 13f, rect.width, 13f);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.grey;
            Widgets.Label(lblRect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return Widgets.TextField(rect, value);
        }

        private void ApplyName()
        {
            if (firstName.NullOrEmpty() && lastName.NullOrEmpty() && nickName.NullOrEmpty())
            {
                return;
            }
            var nick = nickName.NullOrEmpty() ? firstName : nickName;
            android.Name = new NameTriple(firstName ?? "", nick ?? "", lastName ?? "");
        }

        private void Refresh()
        {
            android.style?.Notify_StyleItemChanged();
            android.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(android);
        }

        private void Confirm()
        {
            ApplyName();
            var customXenotype = new CustomXenotype
            {
                name = xenotypeName?.Trim(),
                inheritable = false,
                iconDef = iconDef,
            };
            // Capture the component genes (hardware/subroutines + body shape) plus the single chosen skin
            // and hair colour gene, so the printed body carries exactly the colour genes the player picked
            // (head/body shape stays gene-driven; name/age/ideoligion ride the design snapshot).
            var geneDefs = android.genes.GenesListForReading
                .Where(g => (g.def.IsAndroidGene() || g.def.bodyType != null)
                    && !ForkCompat.IsSkinColorGene(g.def) && !ForkCompat.IsHairColorGene(g.def)
                    && g.def.endogeneCategory != EndogeneCategory.Melanin)
                .Select(g => g.def).ToList();
            if (chosenSkinGene != null) geneDefs.Add(chosenSkinGene);
            if (chosenHairGene != null) geneDefs.Add(chosenHairGene);
            customXenotype.genes.AddRange(geneDefs.Distinct());

            var design = new AndroidPersonaData();
            design.CopyFromPawn(android);
            design.sourcePawn = null;

            station.curDesign = design;
            station.curAndroidProject = customXenotype;
            station.printMode = PrintMode.Print;
            station.totalWorkAmount = Building_AndroidCreationStation.GestationTicks;
            station.currentWorkAmountDone = 0;
            station.requiredItems = ComputeRequiredItems();
            if (creator != null)
            {
                var job = new WorkGiver_CreateAndroid().JobOnThing(creator, station);
                if (job != null)
                {
                    creator.jobs.TryTakeOrderedJob(job);
                }
            }
            Close();
        }

        private List<ThingDefCount> ComputeRequiredItems()
        {
            var items = new List<ThingDefCount>
            {
                new ThingDefCount(OverhaulDefOf.AndroidSubcore, 1),
                new ThingDefCount(ThingDefOf.Plasteel, 125),
                new ThingDefCount(ThingDefOf.ComponentSpacer, 7),
            };
            if (currentGenes.Contains(OverhaulDefOf.BatteryPowered))
            {
                items.Add(new ThingDefCount(ThingDefOf.ComponentIndustrial, 3));
            }
            else
            {
                items.Add(new ThingDefCount(ThingDefOf.Uranium, 20));
            }
            if (currentGenes.Contains(OverhaulDefOf.NeutroCirculation))
            {
                items.Add(new ThingDefCount(VREA_DefOf.Neutroamine, 40));
            }
            else if (currentGenes.Contains(OverhaulDefOf.NormalBlood))
            {
                items.Add(new ThingDefCount(ThingDefOf.HemogenPack, 4));
            }
            return items;
        }

        // ---- option lists ----

        // The standard human faces (narrow/average, normal/pointy/wide jaw) for the current gender - and
        // only those. Ghoul/skull/stump heads are genderless (gender == None), so requiring an exact gender
        // match already drops them; the extra Ghoul guard also removes "Ghoul Narrow", whose name would
        // otherwise slip through the Narrow check.
        private List<HeadTypeDef> StandardHeads() => DefDatabase<HeadTypeDef>.AllDefs
            .Where(h => (h.modContentPack?.IsOfficialMod ?? false)
                && h.gender == android.gender
                && h.requiredGenes.NullOrEmpty()
                && (h.defName.Contains("Average") || h.defName.Contains("Narrow"))
                && !h.defName.Contains("Ghoul") && !h.defName.Contains("Skull") && !h.defName.Contains("Stump")).ToList();

        private List<BodyTypeDef> PermittedBodyTypes()
        {
            var list = new List<BodyTypeDef>
            {
                android.gender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male,
                BodyTypeDefOf.Thin, BodyTypeDefOf.Fat, BodyTypeDefOf.Hulk,
            };
            return list.Distinct().ToList();
        }

        private static List<GeneDef> bodyTypeGenes;
        // One gene per body shape. The mod clones vanilla body genes into VREA_ versions, so both carry the
        // same bodyType - group by shape and keep a single (android) gene each, or the list shows doubles.
        private static List<GeneDef> BodyTypeGenes() => bodyTypeGenes ??=
            DefDatabase<GeneDef>.AllDefs.Where(g => g.bodyType != null)
                .GroupBy(g => g.bodyType.Value)
                .Select(grp => grp.OrderByDescending(g => g.IsAndroidGene()).First())
                .ToList();

        private GeneDef CurrentBodyGene()
        {
            var active = android.genes.GenesListForReading.FirstOrDefault(x => x.def.bodyType != null)?.def;
            // Map whatever body gene the body rolled onto the single canonical gene for that shape, so the
            // cycler's selection lines up with the deduped list.
            if (active != null)
            {
                return BodyTypeGenes().FirstOrDefault(g => g.bodyType == active.bodyType) ?? active;
            }
            return BodyTypeGenes().FirstOrDefault();
        }

        private void ApplyBodyGene(GeneDef bodyGene)
        {
            chosenBodyGene = bodyGene; // remember the player's last body pick
            try
            {
                ForkCompat.suppressAndroidNotifications = true;
                foreach (var g in android.genes.GenesListForReading.Where(x => x.def.bodyType != null && x.def != bodyGene).ToList())
                {
                    android.genes.RemoveGene(g);
                }
                if (android.genes.GetGene(bodyGene) == null)
                {
                    android.genes.AddGene(bodyGene, false);
                }
            }
            finally
            {
                ForkCompat.suppressAndroidNotifications = false;
            }
            Refresh();
        }

        private List<HairDef> PermittedHairs() => DefDatabase<HairDef>.AllDefs
            .Where(h => h.modContentPack?.IsOfficialMod ?? false).ToList();

        // Official beards (NoBeard included, so the player can always go clean-shaven).
        private List<BeardDef> PermittedBeards() => DefDatabase<BeardDef>.AllDefs
            .Where(b => b.modContentPack?.IsOfficialMod ?? false).ToList();

        // Official tattoos of the given slot (the NoTattoo option is included, so "none" is selectable).
        private List<TattooDef> PermittedTattoos(TattooType type) => DefDatabase<TattooDef>.AllDefs
            .Where(t => t.tattooType == type && (t.modContentPack?.IsOfficialMod ?? false)).ToList();

        // The gene whose colour is nearest the target, so a fresh preview always lands on a real colour gene.
        private static GeneDef ClosestColorGene(IEnumerable<GeneDef> genes, Func<GeneDef, Color> colorOf, Color target)
            => genes.OrderBy(g =>
            {
                var c = colorOf(g);
                return Mathf.Abs(c.r - target.r) + Mathf.Abs(c.g - target.g) + Mathf.Abs(c.b - target.b);
            }).First();

        private static readonly HarmonyLib.AccessTools.FieldRef<Pawn_IdeoTracker, float> CertaintyRef =
            HarmonyLib.AccessTools.FieldRefAccess<Pawn_IdeoTracker, float>("certaintyInt");
    }
}
