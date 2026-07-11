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

## Optional Performance Characterization

Run a matching metrics-off/metrics-on Debug characterization pair from clean committed source:

```powershell
./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3
```

This is not part of the pull-request gate. The runner writes ignored artifacts under `artifacts/performance/` and rejects a dirty source tree by default so results identify reproducible code. It discovers Godot from `-GodotPath`, `GODOT_BIN`, `PATH`, or the standard WinGet package location. The current Godot editor/headless route is Debug characterization only; V3-W1-03 Release evidence still requires a verified Release route, the full cold/warm/invalidation matrix, and median reference runs.

## Status

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
