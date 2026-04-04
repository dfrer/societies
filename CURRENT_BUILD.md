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
dotnet build src/societies/Societies.csproj
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1
```

## Validation Notes

- The current test suite includes pure simulation tests, persistence tests, HUD tests, voxel-spike tests, and Godot headless smoke coverage.
- The full validation script is authoritative, but it is substantially longer than the individual .NET or headless passes because it rebuilds and reruns both.
- The voxel spike is experimental only. The authoritative gameplay runtime remains heightfield-based through M3.

## CI Scope

CI should validate the authoritative Godot build and its supporting .NET tests only.
