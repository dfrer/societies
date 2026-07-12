# Societies

Societies is currently a Godot 4 + C# prototype for validating a low-friction society-sim foundation.

The authoritative executable target is the Godot project under `src/societies/`.

- Project: `src/societies/project.godot`
- Main scene: `src/societies/scenes/main.tscn`
- C# project: `src/societies/Societies.csproj`
- C# solution: `src/societies/Societies.sln`
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

## Optional Performance Runs

Run a matching metrics-off/metrics-on Debug characterization pair from clean committed source:

```powershell
./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold
```

On Windows, run the same short pair through the tracked Godot Release export route after installing the Godot 4.6.2 .NET export templates:

```powershell
./scripts/run-performance-pair.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3 -CacheMode cold
```

Run the complete cache-mode contract with identical cold/warm preconditioning and a one-tick forced invalidation case:

```powershell
./scripts/run-performance-cache-modes.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -PreconditioningTicks 2 -Ticks 2

# Verified Release route on Windows.
./scripts/run-performance-cache-modes.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 3 -PreconditioningTicks 2 -Ticks 2
```

The Release route exports the `Windows Performance Release` preset and hard-fails unless the generated runner reports a managed `ExportRelease` assembly running in a non-debug Godot release template. The tracked solution maps Godot's `Debug`, `ExportDebug`, and `ExportRelease` configurations one-to-one. The base editor project still opens `scenes/main.tscn`; the preset's custom `performance_runner` feature selects `tests/PerfRunner.tscn` only in that export. Catalog JSON is explicitly packed and exported runs read it through `res://data`, so results do not depend on the process working directory.

These runs are not part of the pull-request gate. The runner writes ignored artifacts under `artifacts/performance/` and rejects a dirty source tree by default so results identify reproducible code. It discovers Godot from `-GodotPath`, `GODOT_BIN`, `PATH`, or the standard WinGet package location. The editor/headless route remains Debug characterization. The Release route is validated from clean commit `acf634f`; see `planning/active/evidence/v3-w1-03a-release-route-validation.json`. That short smoke proves only the execution route—V3-W1-03 still requires the full cold/warm/invalidation matrix and median reference runs before any baseline claim.

Schema v3 separates deterministic simulation preconditioning from cache treatment: `cold` clears only the derived route cache, `natural_warm` retains the naturally populated cache, and `forced_invalidation` commits one prepared path segment and proves the first exact post-change lookup uses the new navigation version. Eager/all-pairs prewarming remains disabled because the current cell-keyed cache stores exact endpoints. Only `run-performance-cache-modes.ps1` can set `cacheModeEvidence`: it also requires cold/warm configuration and hash identity and explicitly leaves baseline, full-matrix, median, and target/safety claims false.

The clean verified ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves the three mode contracts and cold/warm deterministic equivalence for a short three-citizen smoke. It is not the V3-W1-03 performance baseline; the complete citizen-count matrix, soak, stress, and median reference runs remain W1-03c.

## Status

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
