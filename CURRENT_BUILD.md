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
- a runnable automated test path

## Current Prototype Scope

The current build is Prototype 1: a local playable sandbox for validating the basic loop.

Implemented today:

- scene bootstrap
- local-first session startup
- flat terrain
- first-person movement and harvesting
- simple inventory counts
- two hard-coded recipes
- day/night cycle
- clear/rain weather states
- deterministic worker/stockpile/campfire loop
- local snapshot + event-log output
- run-summary output
- debug HUD

Deferred or not implemented as real production systems yet:

- authoritative multiplayer
- persistent save backend
- AI citizens
- economy and logistics simulation
- governance systems
- voxel terrain

## Validation Commands

```powershell
dotnet build src/societies/Societies.csproj
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1
```

## CI Scope

CI should validate the authoritative Godot build only.
