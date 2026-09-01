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
| Psychic sensitivity, awakened psylink, golden cube | Retuned the original's psy gene in place; removed the amplifier from its blocklist and restated the rule with the awakening exception, on all four paths (AddHediff, surgery availability, `ChangeLevel`, and the neuroformer item's own use check) |
| Sleep cycle | Removed `Rest` from the excluded-needs list, restated the refusal for androids without the subroutine |
| Death delay | New gene; postfixes layered on the original's `ShouldBeDowned` + a new `ShouldBeDead` gate |
| Mechlike (mechanitor oversight) | Full port: colony-mech integration, dormancy, work-mode think tree, uncontrolled-androids alert |
| Power cores | Original power gene retuned into "reactor powered"; battery as a `Hediff_AndroidReactor` subclass; need class repointed; charge job at a stand, with a slow stand-side trickle for anything parked on one |
| Blood types | Neutro gene retuned in place; hemogenic needs its bleed rate computed (vanilla's is always 0 on an android, see below); bloodless closes four bleed paths; coagulation hardware |
| Blood organs | `BloodOrgansExtension` per blood gene; reconciled from the blood gene's `PostAdd` and again after the original's body gene |
| Destroyed vs killed | Subcore hediff in the brain + kill/corpse/letter/thought/relation/tale/funeral gating; the original's blanket death block narrowed so androids can actually die (§4c) |
| Repair rework | `driverClass`/`giverClass` repointed; parts workbench out of the build menu, part items uncraftable, reactor recipe moved to the machining table |
| Memory rework | Memory gene retuned into optional hardware; need class repointed; heat-scrambled need via the overheating gene |
| Needs tab, social tab | `ShowOnNeedList` postfix (power + memory only) + card sized to the bars it draws; the social tab hidden outright when it would hold nothing (§4b) |
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

**Printing flow audited against the fork (2026-07-29).** All four print-path sources (`Building_
AndroidCreationStation`, `UnfinishedAndroid`, `WorkGiver_CreateAndroid`, `JobDriver_CreateAndroid`) are
verbatim ports; the three defs the fork adds (`VREA_CompleteAndroidCycle` job + work giver,
`VREA_AndroidAssembling`, `VREA_ResurrectAndroid`) match content-for-content; the polyanalyzer's only fork
delta is the subcore output line, which is patched. Two gaps were closed:

- `VREA_UnfinishedAndroid` was only having its `thingClass` repointed. The fork also overrides four fields
  inherited from vanilla's `UnfinishedBase`, and two of them are load-bearing: `drawerType` (the inherited
  `MapMeshOnly` means `UnfinishedAndroid.DrawAt` is never called, so the staged body is simply never drawn)
  and `alwaysHaulable` (a colonist carries the half-built android off to a stockpile and the print stops).
  Plus `selectable` and a zeroed `DeteriorationRate`.
- `ForkCompat.forceStandingPawn` was a dead field — nothing read it, so the body being regrown was drawn
  lying down. Now backed by `PrintedBodyPosture_Patch.cs`.

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

### 3. Blood organs — DONE
`Source/Genes/BloodOrgans.cs` carries the whole feature: `BloodOrgansExtension` / `BloodOrgan` declaring
which organ each blood type puts on which part, `BloodOrganUtil.SyncBloodOrgans` reconciling an android to
its blood type, `Gene_AndroidBlood` (now the `geneClass` of all three blood genes) and a once-only
`GameComponent_BloodOrganMigration` for saves that predate it. `ForkCompat.SyncBloodOrgans` is no longer a
stub, so the three call sites already wired in the assembler work as written. The five hediffs the original
lacks are new defs (`Defs/HediffDefs/Hediffs_Overhaul_BloodOrgans.xml`); the neutroamine pair is the
original's own `VREA_NeuroPump` and `VREA_Neutrofilter`.

Two things the overlay has to do that the fork does not:

- **The original's body gene has no "part already carries an implant" guard** — that check is the fork's own
  addition, made in the same edit that has the loop skip circulatory parts. The original installs its fixed
  counterpart on every part unconditionally, so an android would be built with the neutroamine organs
  whatever blood it runs, *and* would stack a second pump on top of the right one whenever the blood gene
  was applied first. Both are settled by a postfix on `Gene_SyntheticBody.PostAdd` that re-syncs, and by the
  sync's first pass dropping duplicates on a part as well as organs from the wrong blood type. Free of
  side effects: removing a hediff does not drop its `spawnThingOnRemoved` item — only the removal surgery
  does.
- **The heart organ is relabelled, not renamed.** The fork renames `VREA_NeuroPump` to `VREA_NeutroPump`;
  the overlay cannot, because that defName is what the original's item, install recipe and starting scenario
  point at and what every android already alive carries. `Patches/BloodOrgans.xml` relabels the hediff, item
  and recipe instead, which is the whole of the difference.

Blood organs are resolved from the blood gene in the repair path too (`CounterpartFor`), so regrowing a
heart gives back the organ that android's blood type actually uses. A circulatory part answers for itself
even when the answer is null, so a blood type that leaves a part bare keeps it bare instead of falling
through to the original's counterpart table.

### 3a. Hemogenic bleeding — vanilla does NOT work on an android
`Hediff_Injury.BleedRate` returns **0** for any wound on a part that carries a directly-added part whose
hediff is not flagged `organicAddedBodypart`. Every part of an android is exactly that: the original's
`Gene_SyntheticBody` installs a `Hediff_AndroidPart` (a `Hediff_AddedPart`) on all of them, off
`VREA_AndroidBodyPartBase`, which declares `addedPartProps` and leaves `organicAddedBodypart` at its default
`false`. Vanilla only sets that flag `true` on Anomaly's fleshy prosthetics.

That is *why* the original ships its own replacement rate for neutroamine androids — its
`Hediff_Injury_BleedRate_Patch` prefix returns a formula that skips the added-part and solid-part checks.
Nothing about it is neutroamine-specific; it is the compensation for androids being made of added parts.

So "hemogenic falls through to vanilla and bleeds red like anyone else" was wrong. The blood *filth* still
appeared, because the damage worker spawns that from `RaceProps.BloodDef` and never consults the bleed rate,
which made it look like it was working — but the rate, the health tab's bleeding line, the bleeding-to-death
timer and blood loss were all zero. `Hediff_Injury_BleedRate_Patch` in `Blood_Patches.cs` now calls the
original's own public `BleedRate` helper for a hemogenic android, so it bleeds at exactly the rate
neutroamine leaks.

**The fork has this bug too, and worse.** Its `Hediff_Injury_BleedRate_Patch` *deleted* the original's
replacement formula and routes every blood type through vanilla, with a comment asserting that normal-blood
and neutroamine androids "bleed via the vanilla logic". They do not — in the fork, neutroamine androids stop
bleeding as well. The overlay dodged that half only because it never unpatched the original's prefix. Do not
port the fork's version of this file.

### 3b. Neutroamine economy — DONE
`Recipe_ExtractNeutroamine` is ported and added as an implied def (`Neutroamine_Patches.cs`), because its
`recipeUsers` is every humanlike in the load order and XML cannot enumerate that — the same reason the
original generates its administer recipe in code.

The reservoir moved from the original's 100 to 40. The original hardcodes that figure in **four** places and
they only make sense together, so each had to be reached a different way:
- the administer recipe worker → `Recipe_AdministerNeutroamineForAndroidOverhaul` (which also gates the
  recipe to neutroamine-blood androids). Its def is implied, generated in C#, so no xpath reaches it: the
  repoint lives in `OverlayRepoints`, where it is the mechanism rather than a fallback. The recipe's
  ingredient base count is set there too.
- the refuel job → `driverClass` repointed (`Patches/Neutroamine.xml`); the original divides by its constant
  inside a lambda inside an iterator, which is no place to transpile.
- the refuel float-menu option → transpiled, but the constant is in the option's click action, which the
  compiler lifted into a display class whose generated name carries a recompile-dependent index. Found by
  shape (the one `<GetSingleOptionFor>b__*` lambda) instead of hard-coded.
- the neutro casket's refill rate → transpiled (one `ldc.r4 -0.01` in `TickInterval`).

The float-menu option and the job driver in particular must move together: the option sets `job.count` from
the reservoir figure the driver then divides by.

The 40-neutroamine **print cost** was already done, in `Window_AndroidCreation.OnGenesChanged`.

### 3c. Bespoke charging job — DONE
`JobDriver_ChargeAndroid` is ported into `Source/AI/AndroidCharging.cs` with the `VREA_ChargeAndroid` job
def, the charging mote, the cable pulse and the MechCharger start/loop sounds. The android now walks to a
powered stand, stands on it and draws 200 W from the grid until full, instead of lying down on it as a bed.
`VREA_Transition_LowPower` and its `MakeDowned` / `BattleLog.Add` pair are ported too
(`LowPowerCollapse_Patches.cs`), so a battery android that runs dry gets a low-power collapse line in the
combat log rather than "a capacitor array caused X to fall unconscious".

The "recharge" work mode now sends a battery android to a stand (`JobGiver_AndroidRecharge`), which is what
the fork's think tree does; a reactor android and a battery android with no stand available still fall
through to powering down.

Two deliberate departures from the fork:

- **The stand's `Tick` postfix stays**, at its slow rate, alongside the job. The fork does not have one, but
  it also does not make its stand a bed — here the stand is the original's `Building_Bed`, and an android
  already too flat to run a job has to be hauled onto one, at which point nothing would ever charge it. The
  postfix skips anyone running the charge job, because `CurOccupant` is *any* android standing still on the
  cell, that one included, and two chargers at once would double the rate and fight over the stand's power
  output.
- **No charging waste.** The fork's driver feeds `Stand.AddChargingWaste` and refuses a stand that is full
  of it; the stand's waste system is a separate unported item (§5), so those two lines are left out.

### 4. Editor UX for exclusive hardware — DONE
Blood/power/chassis swap-on-click and the locked components at the behaviorist station are ported and now
actually reachable — every entry point opens the overlay's windows (see 2).

Requirements and conflicts are ported too. The fork declares `requiresOneOf` / `conflictsWith` as fields on
its own `AndroidGeneDef` subclass, which an overlay cannot add to the original's def class; the same data
now rides along in an `AndroidComponentRequirements` mod extension, read by the four `ForkCompat` helpers
the editor windows were already written against. So this was data, not UI: the "Requires: ..." and
"Conflicts with: ..." lines, their red states and the refusal to accept an invalid selection all came alive
at once.

Six of the eight belong to the original's genes (`Patches/ComponentRequirements.xml`); coagulation and the
ideological subroutine are overlay defs and declare theirs inline. `VREA_MemoryRecharge` and
`VREA_ReactorPowered` are fork defNames — the overlay retunes the original's `VREA_MemoryProcessing` and
`VREA_Power` in their place, so the requirements point at those. Checked against every shipped androidtype:
none of them carries a component whose requirement it would now fail.

### 4b. Needs tab sizing / Social tab visibility — DONE
The two tabs had their *content* trimmed (the `ShowOnNeedList` postfix and the log-only social card) but
not their *size*, so both still opened at full humanlike height with a large empty area under the content.
Two fork patches now ported into `Source/HarmonyPatches/TabSizing_Patches.cs`:

- `NeedsCardUtility.GetSize` postfix — a non-awakened android's needs card is measured from the bars it
  actually shows (`225 x needCount * min(70, FullSize.y / needCount)`) instead of the humanlike default.
  The card is only ever full height because `pawn.needs.mood` is non-null, which the overhaul keeps for the
  awakening mechanic and never draws.
- `NeedsCardUtility.DoNeedsMoodAndThoughts` prefix — draws just `DoNeeds` for those androids, so no
  mood/thoughts column is laid out in the space that is no longer there.
The fork's third patch here, an `InspectTabBase.UpdateSize` postfix shrinking the Social tab from 510 to
185 px for a log-only android, was ported and then **removed again**: at the user's request the Social tab
is now hidden outright for those androids (see below), so a reduced height had nothing left to apply to.

Verified against the stock (non-publicized) `Assembly-CSharp`: `GetSize`, `DoNeedsMoodAndThoughts`,
`DoNeeds` and `FullSize` are all public, so they are patched and called directly.
`ITab_Pawn_Social.SelPawnForSocialInfo` has a private getter and is reached through `AccessTools`, exactly
as the fork does. `NeedsCardUtility.DoMoodAndThoughts` is private and is already patched by the original
mod, so the overhaul does not touch it.

**Deliberate departure from the fork: the Social tab is removed, not shrunk.** The fork keeps a log-only
card at reduced height; the user asked for the tab and its button to disappear entirely when there would be
nothing in it. `ITab_Pawn_Social_IsVisible_Patch` postfixes the tab's `IsVisible` getter for that. The
`DrawSocialCard` prefix is kept as the rule for what such a card *would* hold, for any path that draws it
without going through the tab.

The fork's `SocialTabLogOnly` predicate moved into `ForkCompat` so the visibility gate and the card read one
definition and cannot disagree. It calls the **original's** `Utils.Emotionless`
(`PsychologyDisabled && !EmotionSimulators`) rather than the fork's (`!EmotionSimulators && !IsAwakened`);
the two agree in practice, because `VREA_PsychologyDisabled` is a `VREA_HardwareBase` gene — so
`isCoreComponent`, present on every android and not deselectable in the editor — and carries
`removeWhenAwakened`, so awakening drops it. `SocialCardUtility.DrawRelationsAndOpinions` needed no port at
all: the original mod already prefixes it with the same `Emotionless()` check.

### 4c. Death, resurrection and reprint — DONE
The destroyed-vs-killed model, the subcore, the extraction chain and the assembler's resurrect bill were all
already ported. What was missing was the thing that makes any of it reachable: **androids could barely die.**

The original blocks death twice over, and both are blanket rules:
- a postfix on `ShouldBeDead` returning `false` for *any* death cause while the brain is intact;
- a postfix on `ShouldBeDeadFromRequiredCapacity` nulling *every* missing capacity, likewise brain-gated.

Between them, nothing short of decapitation ends an android, so there was hardly ever a corpse to resurrect
or a subcore to pull. Both are now unpatched (`VREAndroidsOverhaulMod.UnpatchOriginal`) and restated in
`Source/HarmonyPatches/AndroidLethality_Patches.cs` as the fork has them — narrowed to the two causes that
genuinely describe a broken machine rather than a dead one:

- `ShouldBeDeadFromLethalDamageThreshold` → always false for an android. The accumulated-damage rule is a
  statistical "enough is enough" for flesh; a chassis is repaired part by part instead.
- `ShouldBeDeadFromRequiredCapacity` → nulled **only** for `Consciousness`, and only with the brain intact.
  Being switched off is not dying.

Everything else kills normally: a destroyed vital organ, a destroyed torso (the core-part efficiency check),
a hediff whose `CauseDeathNow()` fires — including **blood loss at full severity**, which only started
working for hemogenic androids with the §3a fix, so the two changes complete each other. That death is then
usually a *destruction* rather than a kill, because the subcore survives it, which is precisely what leaves
a body to resurrect at the assembler or a subcore to extract and reprint from.

**This is a real difficulty change.** Androids used to be near-unkillable; they now die on the same terms as
anyone else apart from those two exemptions. The death delay subroutine is the in-game answer to that: it
holds both the downing and the death off for two hours (`DelayedDeactivation_Patches.cs`), for a critical
failure anywhere but the head.

Also ported here: `ThingWithComps_GetGizmos_Patch` (now in `AndroidCorpse_Patches.cs`), the "extract
subcore" toggle on a selected dead android, so the recovery is reachable from the corpse itself and not only
from the Orders designator.

Everything else in the death/resurrect cluster needed no work: the fork's `CompAbilityEffect_Resurrect_Valid`,
`JobDriver_Resurrect_Resurrect`, `ResurrectionUtility_ResurrectWithSideEffects`,
`MutantUtility_CanResurrectAsShambler`, `Corpse_GetInspectString`, the three `CompRottable` patches,
`Designator_ExtractSkull_CanDesignateThing`, `Building_SubcoreScanner_CanAcceptPawn`,
`CompTargetable_BaseTargetValidator` and `IncidentWorker_UnnaturalCorpseArrival_ValidatePawn` are all
**byte-identical to the original's**, so they come free with the base mod.

### 4d. Editor and cosmetic requests (user-directed, beyond the fork)
Three changes asked for in session, none of them ports:

- **Hardware is locked at the behaviourist station.** The fork locked only blood, power and chassis there
  and let the rest of the hardware be swapped from the chair. `Window_AndroidModification` now treats
  everything in the `VREA_Hardware` display category as locked, so the station reprograms **subroutines
  only**; hardware is chosen when the body is printed. The full editor is untouched.
- **Furskin in the designer's cosmetic section.** A checkbox under Body shape toggling Biotech's `Furskin`.
  It needs the explicit capture in `AcceptInner` as well as the preview toggle: the component capture keeps
  only android genes and body-type genes, so a plain cosmetic gene would be dropped on accept.
  `EnforceChosenAppearanceGenes` leaves it alone (it strips only skin-colour, hair-colour, body-type and
  melanin genes). The gene carries `skinIsHairColor`, so the hair swatches colour the fur.
- **"Adds need: memory space (overheating)"** on the component-overheating gene. Vanilla builds that line in
  the private `GeneDef.GetDescriptionFull`; the postfix qualifies the need label there rather than on the
  `DescriptionFull` property, which caches and would re-append the qualifier on every call.

### 5. Smaller behavioural deltas
Verified against the fork on 2026-07-29; everything below is confirmed absent, not assumed.
- `MechanitorControlGroupGizmo` "Assigned mechs" tooltip (reflective, needs the power need).
- `CompPowerTrader` inspect: the android stand's "(200 W when active)" power line.
  (`InspectTabBase_UpdateSize` and the `NeedsCardUtility` sizing are done, see §4b;
  `Pawn_HealthTracker_MakeDowned` came with the charge job, see §3c.)
- `FloatMenuOptionProvider_RepairAndroid` and `Recipe_RemoveArtificialBodyPart`.
- Stand waste production while charging (`ZeroWaste` / `ExtraWaste` genes).
- `PawnGenerator` dev-spawn fix for awakened androids.
- `Gene_RainVulnerability` / `Gene_SelfDestructProtocols` deltas.
- `PawnUtility_GetPosture_Patch`: the `forceStandingPawn` half is ported
  (`PrintedBodyPosture_Patch.cs`), but the fork also made the original's `isPawnRendering` `[ThreadStatic]`,
  which is what stops an android on a charging stand flickering between standing and lying. Fixing that
  means unpatching and restating the original's prefix.

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

**Repointing a def is not enough on its own.** A save stores every thing, gene, hediff and need with the
class it was running as and rebuilds it from that stored name, never from the def — so a def repoint only
reaches things created *after* the overlay was added. An assembler already standing in a colony keeps
opening the original's window forever; an android already alive keeps the original's power gene, reactor
and needs. Nothing is logged and it reads exactly like the feature was never ported.

`Source/HarmonyPatches/SavedClass_Patches.cs` declares each rewrite to
`BackCompatibility.GetBackCompatibleType` (the hook the game uses for renamed classes; its converter chain
is a private list mods cannot join, hence the postfix). **Every class repoint added to `Patches/*.xml` needs
an entry there too**, unless the class is only ever created fresh. Replacements must subclass what they
replace — or, as with the assembler, tolerate the saved nodes they will not find — and any entry on a class
the overlay does not own must also match the node's def, so nothing outside this mod is touched.

---

## Known overlay-specific gaps

- `requiresOneOf` / `conflictsWith` are fork-only fields on `AndroidGeneDef`, so the overlay carries the
  same data in an `AndroidComponentRequirements` mod extension (§4). Mutually exclusive *groups* remain a
  separate mechanism, on vanilla `exclusionTags` plus a startup pass for the load-time-generated genes
  (`IdeoCapability_Exclusion`).
- The android stand is the original's `Building_Bed`, not the fork's unassignable charger, so it is still
  owner-assignable and the charge job claims a stand for its android on the way in.
- **Suspected, unfixed: the power core has the duplicate the blood organs just got fixed.** The same
  unguarded loop that installs the neutroamine organs also installs `VREA_Reactor` on the stomach, so a
  reactor android whose power gene was applied before the body gene ends up with two reactors —
  `SyncPowerCore` only strips cores whose def differs from the gene's, and its install guard then sees the
  core is present and adds nothing. A battery android is stripped correctly, but if the stripping happens at
  `SpawnSetup` rather than during generation it runs on a spawned pawn, and `Hediff_AndroidReactor.
  PostRemoved` spawns a toxic wastepack whenever `MapHeld != null`. Neither has been reproduced in game;
  found by reading the original's `Gene_SyntheticBody.PostAdd` while porting the organs. Fixing it is the
  same shape as the organ fix (keep the first core, drop the rest) plus a way to remove a reactor without
  its waste.
