# Porting status — fork → overlay patch

Source of truth for the design is the standalone fork (`~/rimworld-modding/VanillaRacesExpanded-Android`).
This file tracks what has been adapted into the overlay and what has not. **Adapt, never redesign** — read
the fork's version of a def/class in full and match it field for field before writing the overlay version.

---

## Done

| Feature | How it was adapted |
|---|---|
| Anomaly: obelisk immunity, occultist gating | New Harmony patches; `CanInteract` rejection so vanilla draws its own disabled option |
| Ideoligion: tool precepts, colonist-bar icon + blue name, funeral gating | New patches; the four tool consequences fire only for the overlay's own precepts |
| Ideological subroutine | New gene + `SetIdeo` choke point + convert-ability gate + load migration |
| Psychic sensitivity, awakened psylink, golden cube | Retuned the original's psy gene in place; removed the amplifier from its blocklist and restated the rule with the awakening exception |
| Sleep cycle | Removed `Rest` from the excluded-needs list, restated the refusal for androids without the subroutine |
| Death delay | New gene; postfixes layered on the original's `ShouldBeDowned` + a new `ShouldBeDead` gate |
| Mechlike (mechanitor oversight) | Full port: colony-mech integration, dormancy, work-mode think tree, uncontrolled-androids alert |
| Power cores | Original power gene retuned into "reactor powered"; battery as a `Hediff_AndroidReactor` subclass; need class repointed; charging via a stand `Tick` postfix instead of a bespoke job |
| Blood types | Neutro gene retuned in place; hemogenic needs no code at all; bloodless closes four bleed paths; coagulation hardware |
| Destroyed vs killed | Subcore hediff in the brain + kill/corpse/letter/thought/relation/tale/funeral gating |
| Repair rework | `driverClass`/`giverClass` repointed; parts workbench out of the build menu, part items uncraftable, reactor recipe moved to the machining table |
| Memory rework | Memory gene retuned into optional hardware; need class repointed; heat-scrambled need via the overheating gene |
| Needs tab, social tab | `ShowOnNeedList` postfix (power + memory only); `DrawSocialCard` prefix for a log-only card |
| Misc | Android corpses inedible, drafted tend, neutrocasket configurable fuel |

---

## Remaining, in dependency order

### 1. Subcore recovery — DONE (extraction half)
`AndroidPersonaData`, the `AndroidSubcore` item, `SpawnSubcore`, the extraction surgery, designator, work
giver, job driver and float-menu option are all ported, plus `RealDeathFromData` for a stored core being
destroyed. The persona is captured on death and carried by the item.
**Still open:** nothing consumes a recovered subcore yet - that is the assembler (2).

### 2. Android designer — DO THIS BEFORE THE ASSEMBLER
`Window_AndroidDesign` (653 lines) + `VREA_UIHelper` (190). Also the 7e hair-colour palette (story
override, gene only via gene-swatch) and keeping skin/hair/body-shape genes out of the component editor
(`GeneValidator`).

**Why the order changed:** a trial port of the assembler showed it references `Window_AndroidDesign`
directly, so it cannot compile until the designer exists. The assembler files were parked rather than
committed half-built.

### 3. Assembler (printer rework)
`Building_AndroidCreationStation` (954 lines, a 5x rewrite of the original's 184 - so `thingClass`
repointing to a copied class, not a subclass), `UnfinishedAndroid` staged render, `ITab_AndroidBills`
(lives inside the building file), `WorkGiver_CompleteAndroidCycle` + `JobDriver_CompleteAndroidCycle`
(inside `WorkGiver_CreateAndroid.cs`), print/resurrect/reprint bills.

Trial port found the complete dependency list - nothing else is missing:
- **Stock-assembly accessor swaps** (the fork builds against a publicized Assembly-CSharp):
  `apparel.wornApparel` -> `WornApparel`, `equipment.equipment` -> `AllEquipmentListForReading`,
  `ThingDefCount.thingDef`/`.count` -> `.ThingDef`/`.Count`.
- **Access modifiers**: `DrawAt`, `Tick`, `TickInterval`, `FillTab`, `MakeNewToils` are all `protected`.
- **Fork helpers to supply**: `Utils.AndroidMaterialCost`, `RemoveDuplicateGenes`,
  `suppressAndroidNotifications`, `SyncBloodOrgans` (no-op until 4), plus redirects for `HasSubcore`,
  `SyncPowerCore`, `SyncAndroidIdeo`, `IsSkinColorGene`/`IsHairColorGene` which already exist here.
- **Defs to add/resolve**: `VREA_AndroidAssembling`, `VREA_CompleteAndroidCycle`, `VREA_ResurrectAndroid`,
  and DefOf-style lookups for `VREA_AndroidSubcore` / `VREA_BatteryPowered`.
- `PawnUtility_GetPosture_Patch.forceStandingPawn` (fork-only static, port with the patch).

### 4. Blood organs
`BloodOrgansExtension`, `Gene_AndroidBlood`, per-blood-type organ hediffs (hemopump/neutrofilter/data
bus/heatsink/fluid reprocessor) and the load-time reconcile. Cosmetic-ish but part of the parts economy.

### 5. Editor UX for exclusive hardware
`Window_CreateAndroidBase` / `Window_AndroidCreation` / `Window_AndroidModification`: blood/power/chassis
swap-on-click, locked components at the behaviorist station, requirement and conflict tooltips
(`requiresOneOf` / `conflictsWith` need the extended `AndroidGeneDef`, which the overlay cannot add —
needs a different mechanism).

### 6. Smaller behavioural deltas
- `MechanitorControlGroupGizmo` "Assigned mechs" tooltip (reflective, needs the power need).
- `Pawn_HealthTracker_MakeDowned` + `CompPowerTrader` inspect + `ThingWithComps_GetGizmos` +
  `InspectTabBase_UpdateSize` + `NeedsCardUtility` sizing.
- `PawnUtility_ShouldSendNotificationAbout` (designer-preview notification muting — only matters with 3).
- `FloatMenuOptionProvider_RepairAndroid` (right-click "Repair" order).
- Stand waste production while charging (`ZeroWaste` / `ExtraWaste` genes).
- `Recipe_ExtractNeutroamine`, neutroamine reservoir 40, print cost 40.
- `PawnGenerator` dev-spawn fix for awakened androids.
- `Gene_RainVulnerability` / `Gene_SelfDestructProtocols` deltas.

### 7. Deferred by design
- **Uncanny valley** — parked pending the user's redesign.
- 7h awakened extras: void mental breaks, reading books.

---

## Known overlay-specific gaps

- `requiresOneOf` / `conflictsWith` are fork-only fields on `AndroidGeneDef`. The overlay cannot extend the
  original's def class, so gene requirements are expressed in descriptions and exclusions use vanilla
  `exclusionTags` (plus a startup pass for generated genes, see `IdeoCapability_Exclusion`).
- Charging is done with a vanilla `LayDown` job on the stand plus a `Tick` postfix, not the fork's bespoke
  `JobDriver_ChargeAndroid` — no charging mote or cable pulse yet.
