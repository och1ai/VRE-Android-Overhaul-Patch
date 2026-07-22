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

### 2. The UI cluster: designer + assembler + creation windows — ONE unit, ~1900 lines

A trial port of the designer showed it and the assembler are **mutually dependent**, so they cannot be
done in sequence:

- `Window_AndroidDesign` references the assembler's `curDesign`, `printMode`, the `PrintMode` enum,
  `MakeDesignAndroid` and `GestationTicks`.
- `Building_AndroidCreationStation` opens `Window_AndroidDesign`.
- Both reach `Window_AndroidCreation.onTypeResult`, a fork addition, which drags in
  `Window_CreateAndroidBase` (853 lines in the fork).

So the unit is: `Window_CreateAndroidBase` (853) + `Window_AndroidDesign` (653) + `VREA_UIHelper` (190) +
`Window_AndroidModification` (122) + `Window_AndroidCreation` (108) + `Building_AndroidCreationStation`
(954) + `UnfinishedAndroid` (101) + `WorkGiver_CreateAndroid` (95). Copy them all, then resolve one shared
surface (below) in a single pass. Partial ports do not compile, so nothing lands until the whole unit does.

**The one structural blocker:** `Window_CreateAndroidBase` uses `requiresOneOf` / `conflictsWith`, which
are fields the fork ADDED to `AndroidGeneDef`. The overlay cannot extend the original's def class. Options,
cheapest first: (a) drop those two features from the ported window (requirements/conflict tooltips are
cosmetic - exclusions already work via `exclusionTags`); (b) carry the data in our own `DefModExtension` on
the overlay's genes and read that instead; (c) Harmony-patch the def loader. **(b) is the recommended
route** - it keeps the feature and costs one extension class plus a lookup helper.

**Shared surface to supply once** (found by trial-porting both halves; nothing else is missing):
- Accessor swaps for the stock assembly: `apparel.wornApparel` -> `WornApparel`,
  `equipment.equipment` -> `AllEquipmentListForReading`, `ThingDefCount.thingDef`/`.count` ->
  `.ThingDef`/`.Count`.
- Access modifiers: `DrawAt`, `Tick`, `TickInterval`, `FillTab`, `MakeNewToils`, `Satisfied`,
  `TryGiveJob`, `Designation`, provider `Drafted`/`Undrafted`/`Multiselect`/`GetSingleOptionFor`.
- Fork `Utils` members to add locally: `AndroidMaterialCost`, `RemoveDuplicateGenes`,
  `suppressAndroidNotifications`, `SyncBloodOrgans` (no-op until 4), `AllSkinColorAndroidGenes`,
  `SkinColorOf`, `IsBloodGene`, `IsPowerGene`. Already present and only needing a redirect:
  `HasSubcore`, `SyncPowerCore`, `SyncAndroidIdeo`, `IsSkinColorGene`, `IsHairColorGene`.
- DefOf-style lookups: `VREA_AndroidSubcore`, `VREA_BatteryPowered`, `VREA_ReactorPowered` (= the
  retuned `VREA_Power`), `VREA_NormalBlood`, `VREA_Ideological`, `VREA_AndroidAssembling`,
  `VREA_CompleteAndroidCycle`, `VREA_ResurrectAndroid`, `VRE_AndroidXenotypeIcon7`.
- `PawnUtility_GetPosture_Patch.forceStandingPawn` (fork-only static, port with that patch).
- Defs: assembler `thingClass` + `inspectorTabs` repoint, label/description, the resurrect recipe and the
  cycle work giver; drop the tool-cabinet linkable and its place worker.

### 3. Blood organs
`BloodOrgansExtension`, `Gene_AndroidBlood`, per-blood-type organ hediffs (hemopump/neutrofilter/data
bus/heatsink/fluid reprocessor) and the load-time reconcile. Cosmetic-ish but part of the parts economy.

### 4. Editor UX for exclusive hardware (part of 2 once that lands)
`Window_CreateAndroidBase` / `Window_AndroidCreation` / `Window_AndroidModification`: blood/power/chassis
swap-on-click, locked components at the behaviorist station, requirement and conflict tooltips
(`requiresOneOf` / `conflictsWith` need the extended `AndroidGeneDef`, which the overlay cannot add —
needs a different mechanism).

### 5. Smaller behavioural deltas
- `MechanitorControlGroupGizmo` "Assigned mechs" tooltip (reflective, needs the power need).
- `Pawn_HealthTracker_MakeDowned` + `CompPowerTrader` inspect + `ThingWithComps_GetGizmos` +
  `InspectTabBase_UpdateSize` + `NeedsCardUtility` sizing.
- `PawnUtility_ShouldSendNotificationAbout` (designer-preview notification muting — only matters with 3).
- `FloatMenuOptionProvider_RepairAndroid` (right-click "Repair" order).
- Stand waste production while charging (`ZeroWaste` / `ExtraWaste` genes).
- `Recipe_ExtractNeutroamine`, neutroamine reservoir 40, print cost 40.
- `PawnGenerator` dev-spawn fix for awakened androids.
- `Gene_RainVulnerability` / `Gene_SelfDestructProtocols` deltas.

### 6. Deferred by design
- **Uncanny valley** — parked pending the user's redesign.
- 7h awakened extras: void mental breaks, reading books.

---

## Known overlay-specific gaps

- `requiresOneOf` / `conflictsWith` are fork-only fields on `AndroidGeneDef`. The overlay cannot extend the
  original's def class, so gene requirements are expressed in descriptions and exclusions use vanilla
  `exclusionTags` (plus a startup pass for generated genes, see `IdeoCapability_Exclusion`).
- Charging is done with a vanilla `LayDown` job on the stand plus a `Tick` postfix, not the fork's bespoke
  `JobDriver_ChargeAndroid` — no charging mote or cable pulse yet.
