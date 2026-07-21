# Test checklist — VRE-Android Overhaul (patch)

Everything ported so far, grouped by feature. Load order: **Vanilla Races Expanded - Android** (Steam,
unmodified) → **VRE-Android Overhaul**. A `.cs` change needs a rebuild; XML applies on restart (the mod is
symlinked into `Mods/`).

Dev mode is assumed for most of this: spawn androids, use *Set gene* / the androidtype editor, and
`Damage` / `Kill` tools.

---

## 0. Smoke test

- [ ] Game starts with no red errors on load.
- [ ] Log shows `[VRE-Android Overhaul] patch assembly loaded`.
- [ ] The androidtype editor opens and shows its **full** component list plus the skin/hair colour
      selectors. *(If components or colour pickers are missing, a patch aborted the original's Harmony run —
      check the log for `Undefined target method`.)*
- [ ] An android spawns, works, and takes orders normally.

## 1. Genes appear and exclude correctly

In the androidtype editor:

- [ ] Combat: **War Frame**, **enhanced targeting**, **dull combat**, **death delay**.
- [ ] Subroutines: **gravpilot** (Odyssey), **occultist** (Anomaly), **sleep cycle**, **ideological**
      (Ideology), **mechlike** (Biotech), **coagulation**.
- [ ] Hardware: **reactor powered**, **battery powered**, **neutroblood**, **hemogenic**, **bloodless**,
      **delicate frame**, **spacer** (Odyssey), **psychically dull**.
- [ ] Exclusions hold: enhanced targeting ↔ dull combat; War Frame ↔ delicate frame; reactor ↔ battery;
      neutroblood ↔ hemogenic ↔ bloodless; ideological ↔ social incapable.
- [ ] There is exactly **one** psychic-sensitivity gene — "psychically dull", not a pair with
      "psychically deaf".

## 2. Psychic sensitivity

- [ ] A base android's psychic sensitivity reads **50%**.
- [ ] Awaken one → the gene is gone and sensitivity reads **100%**.
- [ ] An **awakened** android can receive a psylink (neuroformer surgery is offered and works).
- [ ] A **base** android cannot: the surgery is not offered.
- [ ] Base android is immune to the golden cube; an awakened one is **not**.

## 3. Anomaly / occultist  *(Anomaly)*

- [ ] Android **without** occultist: right-clicking a contained entity shows greyed-out
      **"Cannot suppress X: <name> has no occultist subroutine"** — same for study, tend, execute,
      bioferrite. Capture shows **"Cannot capture X: …"** once, not twice.
- [ ] Android **with** occultist: all of the above work, and it can join a psychic ritual.
- [ ] Occultist android suppresses **noticeably faster** and gains **double knowledge** per study.
- [ ] No android can ever be a psychophagy/chronophagy target.
- [ ] A duplicator/mutator obelisk offers **"Trigger mutation (garry is an android)"**, greyed out.
- [ ] An **unstudied** obelisk offers nothing at all — no spoiler about what it would do.

## 4. Ideoligion  *(Ideology)*

- [ ] Android **without** the ideological subroutine has **no ideoligion**; the moral guide cannot convert
      it ("… cannot hold beliefs").
- [ ] Adding the subroutine gives it one; removing it takes it away.
- [ ] Existing androids in an old save lose their ideoligion on first load (one-time migration).
- [ ] Precepts **"respected (awakened)"** and **"equal (awakened)"** exist.
- [ ] Under either, a **non-awakened** android: name tinted cold blue, androidtype icon on the colonist
      bar, no opinions about it, no grief when destroyed, not counted as a slave.
- [ ] Under either, an **awakened** android is treated as a person (normal colour, +20 opinion).

## 5. Power cores

- [ ] **Reactor** android: inspect pane shows `Power: NN% (-NN% / day)`, drains very slowly.
- [ ] **Battery** android: same line, drains far faster (~3 days from full).
- [ ] A battery android carries **only** the cell array — no leftover reactor in its health tab.
- [ ] Below 30% it walks to a **powered** android stand on its own and charges back to 100%.
- [ ] Drain scales with efficiency: a loadout with worse `biostatMet` drains faster.
- [ ] Run a battery to 0 → it shuts down where it stands, inspect pane shows
      **"Shut down, trickle-charging"**; hauled onto a powered stand it charges back up and revives.
- [ ] Unpowered stand does not charge.

## 6. Blood

- [ ] **Neutroblood** android bleeds neutroamine (blue filth) and accrues neutro loss — unchanged from the
      base mod, but efficiency is now higher.
- [ ] **Hemogenic** android bleeds **red**, can bleed out, and accepts **blood transfusion** and
      **extract hemogen pack** surgeries.
- [ ] **Bloodless** android never bleeds at all: no bleed rate, no "bleeding to death" timer, wounds show
      grey metal and machine bits.
- [ ] **Coagulation** subroutine visibly slows bleeding on a neutroblood or hemogenic android (and does
      nothing on a bloodless one).

## 7. Sleep cycle

- [ ] Android **with** the subroutine has a **Rest** need, tires, and goes to bed.
- [ ] Android **without** it has no Rest need at all.

## 8. Death delay

- [ ] Give an android the subroutine, then blow off a leg / deal critical damage: it **keeps working**, and
      the inspect pane shows a red **"Shutting down in 2h"** countdown.
- [ ] Let the countdown expire → it goes down *and* dies at the same moment.
- [ ] Destroy the **torso** → same two-hour reserve, not instant death.
- [ ] Destroy the **head/brain** → instant, reserve bypassed.
- [ ] Repair it back above the threshold before the timer expires → countdown clears and can start again.
- [ ] An android with **no reactor** still drops immediately (reserve does not cover being unpowered).

## 9. Mechlike / mechanitor oversight  *(Biotech)*

- [ ] A mechanitor can **connect** to a mechlike android like a mech; it costs **5 bandwidth**.
- [ ] It appears in the **mechs tab** and can be assigned to a control group.
- [ ] It keeps **all** its work types and skills (this is the regression to watch — if every work type is
      disabled, the colony-mech suppression broke).
- [ ] With **no overseer** it stands dormant with the power-off overlay, and the **"Uncontrolled androids"**
      alert appears — *not* vanilla's "uncontrolled mechs".
- [ ] It never goes feral; the inspect pane shows only "Overseer: …", no "may go feral".
- [ ] Its name on the colonist bar stays **white**.
- [ ] Work modes: **work** = normal jobs; **escort** = follows the mechanitor and engages enemies (with a
      gun it shoots, without one it charges into melee, at mech-like detection range); **sleep** = powers
      down where it stands (or goes to bed with the sleep cycle).
- [ ] Switching from sleep back to work/escort **wakes it immediately**.
- [ ] Right-click orders work inside command range, and are refused with "Out of command range" outside it.
- [ ] Awaken a mechlike android → the gene is stripped and it **leaves the control group** and frees the
      bandwidth (check the group after a save/reload too).

## 10. Destroyed vs killed

- [ ] Kill an android with ordinary damage → neutral letter **"Android destroyed"**, and:
  - [ ] no grief thoughts for anyone,
  - [ ] its relationships stay intact,
  - [ ] no "killed a colonist" tale / no social penalty for the killer,
  - [ ] **no funeral obligation**.
- [ ] Destroy its **head** or **torso** → red letter **"Android killed"**, colony grieves normally.
- [ ] Destroy the **corpse** of a merely-destroyed android → *then* the kill letter and grief fire.
- [ ] A killed android with an ideoligion gets a funeral; a destroyed one never does.
- [ ] The subcore is **not** visible in the health tab.

## 11. Misc

- [ ] Android corpses cannot be eaten by anyone (pawns, animals, nutrient paste).
- [ ] Drafted **tend** is not offered on an android patient.
- [ ] Neutrocasket lets you set a target fuel amount (max 120, default 40).

---

## Not ported yet — don't test these

- **Repair rework**: missing limbs do **not** regenerate, and android part items are still crafted at the
  android parts station. *(Blocks removing the parts station and moving the reactor recipe.)*
- **Subcore recovery**: "destroyed" androids cannot actually be resurrected or reprinted yet — the subcore
  survives and is not mourned, but there is no extraction surgery and no assembler.
- **Android designer / assembler**, memory need rework, emotion simulators, uncanny valley
  (parked by design), needs-tab trimming, the control-group "Assigned mechs" tooltip.
