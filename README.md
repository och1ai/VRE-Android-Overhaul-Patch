# Vanilla Races Expanded - Android: Overhaul Patch

An **overlay add-on** for [Vanilla Races Expanded - Android](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380).
It requires the original mod and does **not** redistribute it — it loads afterward and overrides its
behaviour via XML def-repointing and Harmony, so users still subscribe to the original.

## What it reworks
Androids become repairable, mechanoid-like machines: repair instead of permanent damage; a blood /
core / subroutine rework; a body designer; a gestator-style assembler; and deeper Anomaly / psychic /
Ideology integration.

## Structure of the dev workspace (three sibling folders)
- `../VRE-Android-Original/` — the base mod as the overhaul was built on (reference, unmodified).
- `../VanillaRacesExpanded-Android/` — the full standalone modified fork (source of the ported logic).
- `../VRE-Android-Overhaul-Patch/` — **this** add-on: only new code + patches, no original code.

## Status
Early scaffold. Subsystems are being converted from the standalone fork into overlay form
(def class-repointing + `Harmony.UnpatchAll("VREAndroidsMod")` + XML PatchOperations).
