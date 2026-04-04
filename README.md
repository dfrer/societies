# Societies

Societies is currently a Godot 4 + C# prototype for validating a low-friction society-sim foundation.

The authoritative executable target is the Godot project under `src/societies/`.

- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- Current default branch: `master`

Use [CURRENT_BUILD.md](CURRENT_BUILD.md) as the short repo-truth reference.

## Current Prototype

See [CURRENT_BUILD.md](CURRENT_BUILD.md) for the up-to-date prototype scope, validation commands, and implementation details.

## Planning vs Code

The `planning/` tree contains long-term design material, including older and more ambitious directions than the current implementation.

Treat planning documents as aspirational unless they are confirmed by the current Godot code under `src/societies/`.

## Runtime Controls

- `Tab`: toggle inventory panel
- `1`: craft Stone Axe
- `2`: craft Campfire
- `F5`: toggle weather
- `F6`: save snapshot, event log, and run summary
- `F7`: reset the current deterministic run
- `F9`: load the latest snapshot set

## Repository Layout

- `src/societies/` - authoritative Godot project
- `tests/Societies.Core.Tests/` - fast .NET unit tests
- `planning/` - long-term design and research material
- `scripts/` - local workflow scripts

## Status

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
