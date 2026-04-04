# Societies

Societies is currently a Godot 4 + C# prototype for validating a low-friction society-sim foundation.

The authoritative executable target is the Godot project under `src/societies/`.

- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- Current default branch: `master`

Use [CURRENT_BUILD.md](CURRENT_BUILD.md) as the short repo-truth reference.

## Current Prototype

The current build is a deterministic local sandbox, not the full original MVP.

Implemented in the authoritative path:

- local session bootstrap
- flat terrain and first-person movement
- harvesting for wood, stone, and berries
- simple inventory and two recipes
- fixed-tick day/night and weather state
- deterministic worker, stockpile, and campfire loop
- snapshot, event-log, and run-summary output
- xUnit coverage plus Godot headless regression tests

Deferred or aspirational, not real in the current build:

- real ENet multiplayer
- database-backed persistence
- voxel terrain
- broader economy and logistics simulation
- governance systems
- full AI citizen behavior stack

## Planning vs Code

The `planning/` tree contains long-term design material, including older and more ambitious directions than the current implementation.

Treat planning documents as aspirational unless they are confirmed by the current Godot code under `src/societies/`.

## Validation

Local validation commands:

```powershell
dotnet build src/societies/Societies.csproj
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
```

One-command local validation:

```powershell
./scripts/run-prototype-validation.ps1
```

Optional output override for snapshots, event logs, and run summaries:

```powershell
$env:SOCIETIES_RUN_OUTPUT_DIR = "C:\temp\societies-runs"
```

Default output location remains `user://prototype_runs`.

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
