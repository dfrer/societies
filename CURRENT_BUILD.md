# Current Build

## Authoritative Implementation

The current authoritative executable build is the Godot 4 + C# project under `src/societies/`.

- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- Current default branch in this repository: `master`

This is the only in-repo implementation that currently has:

- a tracked engine project
- a tracked main scene entrypoint
- a buildable C# project
- a runnable automated validation path

## Current Prototype Scope

The current build is Prototype V2 M3: a deterministic local settlement simulation with terrain-aware logistics and an isolated voxel feasibility spike.

Implemented today:

- deterministic local session bootstrap
- heightfield terrain with biome/buildability/movement-cost overlays
- first-person movement and harvesting
- observer camera and runtime overlay cycling
- seeded scenario world generation
- citizen-based settlement simulation with food, fatigue, beds, hearth service, and build queue management
- terrain-aware route planning, built path corridors, remote depots, and logistics metrics
- local JSON snapshot + event-log + run-summary output
- V2 artifact exports including world summary and metrics CSV
- opt-in bounded runtime diagnostics and a standalone paired performance runner
- headless .NET + Godot validation coverage
- experimental voxel spike module for chunking, edits, meshing, persistence, and walkability-mask extraction

Deferred or not implemented as authoritative runtime systems yet:

- authoritative multiplayer
- production backend persistence
- voxel terrain as the main gameplay world
- pathfinding-based collision avoidance or crowd simulation
- markets, trade, and governance systems
- combat and social simulation

## Validation Commands

```powershell
dotnet build src/societies/Societies.csproj --configuration Release
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1

# Optional Debug characterization; runs matching metrics-off and metrics-on cases.
./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3
```

## Validation Notes

- The current test suite includes pure simulation tests, persistence tests, HUD tests, voxel-spike tests, and Godot headless smoke coverage.
- The required manifest declares 95 .NET tests: 79 fast, 8 integration, and 8 soak, plus 16 Godot headless tests.
- The full validation script is authoritative, but it is substantially longer than the individual .NET or headless passes because it rebuilds and reruns both.
- The optional performance pair is not a pull-request gate. It requires clean committed source by default and writes ignored run artifacts under `artifacts/performance/`.
- The current Godot editor/headless execution route loads the managed Debug build, so its metrics-off/on pair is Debug characterization, not V3-W1-03 Release evidence. A reference claim still requires a verified Release route, the full cold/warm/invalidation matrix, and median reference runs.
- The voxel spike is experimental only. The authoritative gameplay runtime remains heightfield-based through M3.

## CI Scope

CI should validate the authoritative Godot build and its supporting .NET tests only.
