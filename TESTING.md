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
- [ ] Awakened android can receive a **psylink** (neuroformer surgery offered and it works); a base android
      cannot.
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
- [ ] **Hemogenic** bleeds **red**, can bleed out, and accepts **blood transfusion** / **extract hemogen
      pack**.
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

## 9. Destroyed vs killed

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

## 11. Misc

- [ ] Android corpses cannot be eaten.
- [ ] Neutrocasket lets you set a target fuel amount (max 120, default 40).

---

## Not ported yet — don't test

- **Subcore recovery**: no extraction surgery, no assembler — a "destroyed" android is not mourned but
  cannot actually be brought back yet.
- **Blood organs** (hemopump / neutrofilter / data bus etc.), **android designer / assembler**, memory need
  rework, emotion simulators, needs-tab trimming, control-group "Assigned mechs" tooltip, uncanny valley
  (parked by design).
