# Current Build

## Authoritative Implementation

The current authoritative executable build is the Godot 4 + C# project under `src/societies/`.

- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- C# solution: `src/societies/Societies.sln`
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
- editor-filesystem and exported-PCK catalog loading from the same validated JSON data
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
dotnet build src/societies/Societies.sln --configuration ExportRelease
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1

# Optional Debug characterization; runs matching metrics-off and metrics-on cases.
./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold

# Optional Windows Release-route smoke; requires Godot 4.6.2 .NET export templates.
./scripts/run-performance-pair.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold

# Optional three-mode contract; validates cold/warm equivalence plus forced invalidation.
./scripts/run-performance-cache-modes.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -PreconditioningTicks 2 -Ticks 2

# Canonical W1-03c Windows Release matrix; 14 pairs and 28 metrics rows.
./scripts/run-performance-baseline-matrix.ps1
```

## Validation Notes

- The current test suite includes pure simulation tests, persistence tests, HUD tests, voxel-spike tests, and Godot headless smoke coverage.
- The required manifest declares 103 .NET tests: 84 fast, 10 integration, and 9 soak, plus 16 Godot headless tests.
- The full validation script is authoritative, but it is substantially longer than the individual .NET or headless passes because it rebuilds and reruns both.
- The optional performance pair is not a pull-request gate. It requires clean committed source by default and writes ignored run artifacts under `artifacts/performance/`.
- Schema v3 records cold, natural-warm, and forced-invalidation cache transitions in metrics-off and metrics-on results. Natural warmup advances deterministic simulation state; eager/all-pairs cache prefill remains disabled.
- A single pair can prove only its own transition. `scripts/run-performance-cache-modes.ps1` is the cross-mode authority: it requires identical cold/warm configuration, environment, tick bounds, and deterministic hashes plus a valid forced-invalidation transition.
- The Godot editor/headless execution route loads the managed Debug build, so its metrics-off/on pair remains characterization only.
- The tracked Windows export route uses the `Windows Performance Release` preset and accepts Release evidence only when the exported binary identifies its managed assembly as `ExportRelease`, reports the Godot release/template features, and reports neither a debug build nor the editor feature. It does not silently promote an ordinary managed `Release` build to reference evidence.
- The tracked solution exposes Godot's three managed configurations (`Debug`, `ExportDebug`, and `ExportRelease`) without mapping an ordinary solution `Release` configuration back to Debug.
- Raw catalog JSON is explicitly included in the Windows preset. Editor runs use the validated filesystem directory; exported builds always use packed `res://data` resources, avoiding working-directory-dependent inputs.
- The Release execution route is validated from clean commit `acf634f`; see `planning/active/evidence/v3-w1-03a-release-route-validation.json`. This is route evidence only, not a performance baseline.
- The clean schema-v3 ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves cold/warm deterministic equivalence and the forced-invalidation transition for a short three-citizen smoke only.
- The canonical W1-03c ExportRelease matrix passed its evidence contract from clean commit `a636967`: 14/14 pairs, 28 metrics rows, 354/354 manifest artifact hashes, cold/warm equality at 3/6/12/16 citizens, three comparable 16-citizen reference trials, two deterministic 1,000-tick soaks, the 24-citizen stress case, and the forced transition. The budget result is `safety_failure`, not a green performance gate: reference median p95 is 570.6155 ms versus 50 ms safety and median max is 3694.2534 ms versus 250 ms safety. Forced invalidation itself passes at 8.4171 ms. See `planning/active/evidence/v3-w1-03c-performance-baseline-validation.json`. Week 2 feature expansion remains blocked while W1-04/W1-05 correctness and path-selection work continues.
- The voxel spike is experimental only. The authoritative gameplay runtime remains heightfield-based through M3.

## CI Scope

CI should validate the authoritative Godot build and its supporting .NET tests only.
