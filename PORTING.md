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

### 2. UI cluster (designer + assembler + creation windows) — DONE, UNTESTED
All nine files ported: `Window_CreateAndroidBase`, `Window_CreateAndroidXenotype`, `Window_AndroidCreation`,
`Window_AndroidModification`, `Window_AndroidDesign`, `VREA_UIHelper`, `Building_AndroidCreationStation`
(+ `ITab_AndroidBills`), `UnfinishedAndroid`, `WorkGiver_CreateAndroid` (+ the cycle work giver and job
driver). Wired up by `Patches/Assembler.xml` (thingClass, bills tab, label/description, job and work-giver
classes, polyanalyzer output) and `Defs/Assembler_Overhaul.xml` (cycle job/work giver, resurrect recipe,
assembling effecter).

**Editor entry points repointed.** All of the overhaul's editor behaviour (the drone default icon instead
of the vanilla blank "Basic" face, one blood type + one power source preselected instead of every core
component, the appearance genes hidden because they moved to the designer) lives in the rewritten
`Window_CreateAndroidBase`, so only windows deriving from OUR base get it. Three call sites still built the
ORIGINAL's windows — which is why the rewrite only ever showed up on the assembler-designer path.
`Source/HarmonyPatches/AndroidTypeEditor_Patches.cs` now repoints each:
- starting-pawns "android editor" button — transpiler swapping the one `newobj` (a prefix is impossible:
  the original names its first parameter `__instance`, which Harmony reserves and passes as null on a
  static method);
- character card "android editor..." option — prefix restating the option, because its `newobj` sits inside
  a compiler-generated closure;
- behaviourist station `TryAcceptPawn` — transpiler, because closing the stock window instead would run its
  `Close()` → `CancelModification()` and eject the colonist the method just loaded.

Each patch has a `Prepare()` guard and each transpiler falls back to untouched IL with a warning, so a
missing anchor degrades to the unmodified editor rather than failing hard.

Shared surface lives in `Source/ForkCompat.cs`: the fork's `Utils` helpers, an `OverhaulDefOf`, and
`VanillaGeneUI` reflection wrappers for `GeneUIUtility.DrawStat`, `DrawIconSelector` and
`ValidSymbolRegex`, which vanilla keeps private.

**Carried gap:** `requiresOneOf` / `conflictsWith` cannot exist on the original's `AndroidGeneDef`, so
`RequiredHardware` / `RequirementSatisfiedBy` / `ConflictInSelection` report "no requirement, no conflict".
Mutually exclusive groups still work (vanilla `exclusionTags`); only the requirement and conflict TOOLTIPS
are missing. Restoring them needs our own `DefModExtension` carrying that data.

### 3. Blood organs
`BloodOrgansExtension`, `Gene_AndroidBlood`, per-blood-type organ hediffs (hemopump/neutrofilter/data
bus/heatsink/fluid reprocessor) and the load-time reconcile. Cosmetic-ish but part of the parts economy.

### 4. Editor UX for exclusive hardware (part of 2 once that lands)
Blood/power/chassis swap-on-click and the locked components at the behaviorist station are ported and now
actually reachable — every entry point opens the overlay's windows (see 2).
**Still open:** requirement and conflict tooltips (`requiresOneOf` / `conflictsWith` need the extended
`AndroidGeneDef`, which the overlay cannot add — needs a different mechanism).

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

## Repoint integrity check

`Source/OverlayRepoints.cs` restates every class repoint done by `Patches/*.xml` as a startup assertion,
because a `PatchOperation` whose xpath stops matching fails **quietly**: the game starts, most of the mod
works, and one subsystem silently keeps running the original's code. Anything that did not take is named in
the log and then set from code.

On a healthy load it prints `[VRE-Android Overhaul] all N def repoints applied.` Any other message from it
is a real bug in the matching patch file — fix the xpath, do not rely on the code fallback.

---

## Known overlay-specific gaps

- `requiresOneOf` / `conflictsWith` are fork-only fields on `AndroidGeneDef`. The overlay cannot extend the
  original's def class, so gene requirements are expressed in descriptions and exclusions use vanilla
  `exclusionTags` (plus a startup pass for generated genes, see `IdeoCapability_Exclusion`).
- Charging is done with a vanilla `LayDown` job on the stand plus a `Tick` postfix, not the fork's bespoke
  `JobDriver_ChargeAndroid` — no charging mote or cable pulse yet.
