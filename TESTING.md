# Test checklist — VRE-Android Overhaul (patch)

Load order: **Vanilla Races Expanded - Android** (Steam, unmodified) → **VRE-Android Overhaul**. A `.cs`
change needs a rebuild; XML applies on restart (the mod is symlinked into `Mods/`).

Only things **not yet verified in game** are listed. Dev mode assumed: spawn androids, use the androidtype
editor / *Set gene*, and the `Damage` / `Kill` tools.

---

## Already verified during the port — do not re-test

Game loads clean, androids spawn and work · androidtype editor shows its full component list and colour
selectors · subroutine and hardware genes appear (spacer, occultist, gravpilot, dull combat, enhanced
targeting) with their exclusions · twisted obelisk refuses androids · ideoligion tool treatment on the
colonist bar, "respected (awakened)" and "equal (awakened)" precepts · drafted tend not offered on an
android patient.

---

## 1. Shipped but never re-tested (from the last round of fixes)

- [ ] Base android shows **psychically dull** (one gene, not a pair with "psychically deaf"), sensitivity
      **50%**, with Biotech's dull icon.
- [ ] Android **without** occultist cannot suppress a contained entity; the option is greyed out as
      **"Cannot suppress X: \<name\> has no occultist subroutine"**. Same wording for study/tend/execute.
      Capture shows its refusal **once**, not twice.
- [ ] Android **with** occultist can suppress, study and join a ritual — and suppresses noticeably faster
      with double knowledge per study.

## 2. Psychic sensitivity on awakening

- [ ] Awaken an android → the dull gene is gone, sensitivity **100%**.
- [ ] Awakened android can receive a **psylink**: the **psylink neuroformer** item offers *Use* and it
      actually grants the psylink.
- [ ] On a **base** android that same option is **greyed out** as *"... (\<name\> has not awakened)"* — the
      neuroformer is **not** consumed. *(Before the fix the order went through, the item was destroyed and
      no psylink was gained.)*
- [ ] The neuroformer still works normally on a **human** colonist.
- [ ] Base android is immune to the golden cube; an awakened one is **not**.

## 3. Ideological subroutine  *(Ideology)*

- [ ] Android **without** it has **no ideoligion**; the moral guide cannot convert it.
- [ ] Adding the subroutine grants one; removing it takes it away.
- [ ] Androids in an existing save lose their ideoligion on first load (one-time migration).
- [ ] It is mutually exclusive with **social incapable**.

## 4. Power cores

- [ ] Both **reactor powered** and **battery powered** appear, mutually exclusive, with their own icons
      (no coffin, no missing texture).
- [ ] Inspect pane shows `Power: NN% (-NN% / day)` for both; the battery drains far faster (~3 days).
- [ ] A battery android carries **only** the cell array — no leftover reactor in its health tab.
- [ ] Below 30% it walks to a **powered** stand on its own and charges to 100%. An unpowered stand does
      nothing.
- [ ] Run a battery to 0 → shuts down where it stands, shows **"Shut down, trickle-charging"**; hauled onto
      a powered stand it charges back up and revives.

## 5. Blood

- [ ] Three options appear, mutually exclusive: **neutroamine blood**, **hemogenic**, **bloodless** — each
      with its proper icon.
- [ ] **Hemogenic** bleeds **red** — and the **health tab actually shows a bleeding line** with a rate and
      a time-to-death, not just red filth on the floor. *(The splatter comes from the damage worker and
      appeared even while the rate was 0, so check the tab, not the ground.)*
- [ ] It can actually **bleed out and die** if left untended; tending stops the bleeding.
- [ ] It accepts **blood transfusion** / **extract hemogen pack**.
- [ ] **Bloodless** never bleeds at all (no bleed rate, no bleeding-out timer) and its wounds show grey
      metal and machine bits.
- [ ] **Coagulation** (hardware) visibly slows bleeding on a neutroamine or hemogenic android.
- [ ] Neutroamine blood still bleeds blue and accrues neutro loss as before.

## 6. Sleep cycle

- [ ] With the subroutine the android has a **Rest** need and goes to bed; without it, no Rest need at all.

## 7. Death delay

- [ ] Critical damage → it keeps working, red **"Shutting down in 2h"** in the inspect pane.
- [ ] Countdown expires → goes down *and* dies together.
- [ ] Destroyed **torso** → same reserve, not instant death. Destroyed **head/brain** → instant.
- [ ] Repaired above the threshold in time → countdown clears.

## 8. Mechlike  *(Biotech — worked in the fork, this is the overlay port)*

- [ ] A mechanitor can connect to it (**5 bandwidth**), it appears in the **mechs tab**, and it keeps
      **all** work types and skills. *(All work types disabled = the colony-mech suppression broke.)*
- [ ] With no overseer: dormant with the power-off overlay, **"Uncontrolled androids"** alert, name stays
      **white**, no "may go feral".
- [ ] Work modes: work / escort (engages enemies, melee if unarmed) / sleep. Switching out of sleep wakes it
      immediately.
- [ ] Awakening strips the gene and it **leaves the control group**, freeing the bandwidth (recheck after a
      save/reload).

## 9. Death, destruction and recovery

**Androids are now killable on ordinary terms** — this is the big behavioural change; test it before the
detail below. They used to be near-immortal with an intact brain.

- [ ] Shoot an android apart: destroying a **vital organ** or the **torso** kills it, and so does letting a
      **hemogenic** one bleed out. It does **not** die just from piling on damage (no lethal-damage
      threshold), and it does **not** die from being knocked unconscious — that still just downs it.
- [ ] A downed, unconscious android with an intact brain stays alive indefinitely and can be repaired.
- [ ] A **human** colonist still dies exactly as before (nothing here leaks onto non-androids).

### Recovery from a body
- [ ] Select a dead android's corpse → an **"Extract android subcore"** toggle appears on it with the
      subcore icon. Toggling it queues the job; a colonist extracts the subcore and it drops as an item.
- [ ] The same order is still available from the **Orders** designator (both routes make one designation,
      and toggling it off cancels).
- [ ] The recovered subcore can be loaded into the **assembler** to **reprint** the android — same name,
      same identity.
- [ ] A corpse whose subcore is **still inside** can instead be **resurrected** by an assembler bill.
- [ ] A corpse whose subcore has been **extracted** can no longer be resurrected — only reprinted.

### Destroyed vs killed

- [ ] Ordinary kill → neutral **"Android destroyed"**, no grief, relationships intact, no "killed a
      colonist" tale, **no funeral**.
- [ ] Destroyed **head** or **torso** → red **"Android killed"**, colony grieves.
- [ ] Destroying the **corpse** of a merely-destroyed android → *then* the kill letter and grief fire.
- [ ] The subcore is not visible in the health tab.

## 10. Repair rework

- [ ] Blow off an android's hand/leg → a crafter repairs it and the **part grows back**; permanent scars are
      cleared too.
- [ ] A **manually installed** bionic on that limb is left alone by repair, not overwritten.
- [ ] A removed/spent **reactor** is NOT regenerated by repair — it still has to be crafted and installed.
- [ ] A battery android's charge drops slightly while being repaired; a reactor one's does not.
- [ ] The **android parts workbench** is gone from the build menu, and android part items can no longer be
      crafted anywhere.
- [ ] The **reactor** recipe is available at the **machining table**.
- [ ] Only one crafter works on a given android at a time.

## 11. Editor defaults and needs tab

- [ ] A **new androidtype** starts with **reactor powered** selected and battery deselected, and
      **neutroamine blood** selected with hemogenic and bloodless deselected.
- [ ] **Memory recharging** starts **deselected** — a stock android is built with no working memory.
- [ ] A non-awakened android's needs tab shows **only Power and Memory**; an awakened one shows the full
      set.
- [ ] That needs tab is **shrunk to fit those bars** — narrow, only as tall as the bars it draws, with no
      mood/thoughts column and no empty space below. Selecting an awakened android (or a human) right after
      restores the full-size tab.
- [ ] Without the memory hardware there is **no memory bar** at all, and the android never reformats.
- [ ] With the memory hardware the bar drains and the android reformats when it bottoms out.
- [ ] An android with **component overheating** but no memory hardware grows a memory bar **only while
      overheating**, drains ~3x faster, and the bar disappears again once it cools down.
- [ ] The **component overheating** gene's info panel reads **"Adds need: memory space (overheating)"**.
      The **memory recharging** gene's panel is unqualified (it grants a permanent need).

## 11b. Android designer and behaviourist station

- [ ] Designer → cosmetic section has a **furskin** checkbox under Body shape. Ticking it puts fur on the
      preview; the **hair colour** swatches drive the fur's colour (not skin colour), and hair/beard style
      grids drop out while it is on.
- [ ] The printed android **actually comes out furred** — the gene survives the print, not just the preview.
- [ ] Reopening the designer on a furred android shows the box already **ticked** (and accepting again does
      not silently strip the fur).
- [ ] Behaviourist station opens the **overhaul's** editor (drone icon default, appearance genes absent).
- [ ] At that station **every hardware component is locked**: blood, power, chassis, memory recharging,
      coagulation, spacer, overheating, tolerances — all shown as installed but unclickable, with **no
      alternatives offered**. Only **subroutines** can be added or removed.
- [ ] The full editor (starting pawns / character card / designer) still lets hardware be chosen freely —
      the lock is specific to modifying an already-built body.

## 12. Social tab

- [ ] A base android with **no emotion simulators and no ideological subroutine**: the **Social tab is
      gone entirely** — no card and **no tab button** in the inspect pane.
- [ ] An android with **emotion simulators**, or an **awakened** one, or one with the **ideological**
      subroutine: normal Social tab, present and complete. Select one **right after** an android with no
      tab, and a **human** too — the tab is a shared singleton, so a wrongly-hidden tab would show up here.
- [ ] Having the Social tab open on a colonist and then selecting a base android does not leave a blank
      pane or throw — it falls back to another tab.

## 13. Misc

- [ ] Android corpses cannot be eaten.
- [ ] Neutrocasket lets you set a target fuel amount (max 120, default 40).

---

## Not ported yet — don't test

Control-group "Assigned mechs" tooltip, the extract-subcore shortcut on a selected corpse (the surgery and
designator do work), right-click "Repair" order, stand waste production, the stand's "(200 W when active)"
power line, the `PawnGenerator` dev-spawn fix for awakened androids, and uncanny valley (parked by design).
Full list and order in `PORTING.md` §5.
