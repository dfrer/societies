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

Run the canonical W1-03c Release matrix on Windows from clean committed source:

```powershell
./scripts/run-performance-baseline-matrix.ps1
```

Compare the exhaustive selector against exact branch-and-bound using one hash-pinned Release bundle and three counterbalanced trials:

```powershell
./scripts/run-job-selection-comparison.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 16 -Ticks 300 -Trials 3
```

Reproduce the W1-05b diagnostic attribution from the ignored W1-05 matrix artifacts:

```powershell
./scripts/analyze-performance-spikes.ps1 -InputPath artifacts/performance/w105-baseline-227a758 -OutputPath artifacts/performance/w105-baseline-227a758/spike-analysis.json
```

The matrix authority runs 14 metrics-off/on pairs: cold and natural-warm 300-tick cases for 3, 6, 12, and 16 citizens; three comparable cold 16-citizen reference trials; two 1,000-tick deterministic soaks; a 24-citizen stress case; and one forced invalidation case. `-PlanOnly` validates the inventory without making evidence claims, and `-CaseId` produces non-baseline partial characterization.

The Release route exports the `Windows Performance Release` preset and hard-fails unless the generated runner reports a managed `ExportRelease` assembly running in a non-debug Godot release template. The tracked solution maps Godot's `Debug`, `ExportDebug`, and `ExportRelease` configurations one-to-one. The base editor project still opens `scenes/main.tscn`; the preset's custom `performance_runner` feature selects `tests/PerfRunner.tscn` only in that export. Catalog JSON is explicitly packed and exported runs read it through `res://data`, so results do not depend on the process working directory.

These runs are not part of the pull-request gate. The runner writes ignored artifacts under `artifacts/performance/` and rejects content-dirty source by default so results identify reproducible code; stat-only touches whose Git blobs still match are not misclassified. It discovers Godot from `-GodotPath`, `GODOT_BIN`, `PATH`, or the standard WinGet package location. The editor/headless route remains Debug characterization. The Release execution route was first validated from clean commit `acf634f`; see `planning/active/evidence/v3-w1-03a-release-route-validation.json`.

Schema v4 separates deterministic simulation preconditioning from cache treatment and records the non-persistent selector mode plus selector-specific query, pruning, reuse, and timing diagnostics. `cold` clears only the derived route cache, `natural_warm` retains the naturally populated cache, and `forced_invalidation` commits one prepared path segment and proves the first exact post-change lookup uses the new navigation version. Eager/all-pairs prewarming remains disabled. W1-04 caches reachability and cell routes while rematerializing exact endpoints; W1-05 uses a safe exact branch-and-bound selector and retains exhaustive mode only as a benchmark reference. Only `run-performance-cache-modes.ps1` can set `cacheModeEvidence`: it also requires cold/warm configuration and hash identity and explicitly leaves baseline, full-matrix, median, and target/safety claims false.

The clean verified ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves the three mode contracts and cold/warm deterministic equivalence for a short three-citizen smoke.

The canonical W1-03c matrix completed from clean commit `a636967`. All 14 pairs, 28 metrics rows, artifact-integrity checks, cold/warm comparisons, three reference trials, repeated soaks, and the forced transition passed their evidence contracts. The measured budget did not pass: the 16-citizen cold median was p95 570.6155 ms and max 3694.2534 ms against 50 ms and 250 ms safety limits. The forced invalidation interval itself passed at 8.4171 ms, and eager/all-pairs warmup remains benchmark-only. See `planning/active/evidence/v3-w1-03c-performance-baseline-validation.json`. Week 2 feature expansion is blocked; continue correctness and algorithmic path-selection work in Godot before rerunning the matrix.

W1-04 corrects the navigation contract at implementation commit `7918d49`: blocked or disconnected endpoints no longer receive fabricated routes, diagonals cannot cut blocked corners, discounted paths retain an admissible deterministic A* search, and unreachable work is skipped with a stable diagnostic. Wetland reeds and clay use deterministic walkable interaction positions, including legacy snapshot normalization, without weakening blocked-terrain semantics. Local validation passed 110/110 .NET tests, 16/16 Godot headless tests, and all tracked managed configurations with zero warnings. See `planning/active/evidence/v3-w1-04-navigation-validation.json`.

W1-05 exact branch-and-bound selection is complete with clean Release evidence at `227a758`. Four shipped scenarios match the exhaustive reference for 300 ticks, and the 16-citizen selector drops exact path queries from 17,441 to 2,544 (85.414%) with identical deterministic hashes. Its three-trial Release median p95 is 78.0301 ms versus 656.3981 ms exhaustive. The full post-W1-05 matrix also improved the optimized 16-citizen reference median p95 from 570.6155 ms to 81.4823 ms, but its 1,552.5664 ms median maximum means the overall safety gate remains red. Do not begin Week 2 feature expansion yet; use the new route-selection diagnostics to isolate the remaining spikes. See `planning/active/evidence/v3-w1-05-job-selection-validation.json`.

W1-05b now isolates the remaining spikes from all 14 clean schema-v4 Release pairs and 5,301 diagnostic ticks. Cache misses correlate with wall time at `r=0.982573`, compared with `r=0.233264` for total lookup volume and `r=0.076652` for navigation rebuild time. Completing path segments clears the derived route cache; the following work-order ranking and idle-citizen selection repopulate it with exact A* searches. In the 16-citizen reference, all seven ticks over 250 ms are initial-cold or immediately post-invalidation, and all six ticks over one second follow invalidation. The metrics-on analysis is diagnostic rather than a new timing gate; the canonical safety failure remains authoritative. W1-06 and Week 2 remain blocked until exact cache-repopulation work is validated by a fresh matrix. See `planning/active/evidence/v3-w1-05b-spike-characterization.json`.

## Status

This tranche is about stabilizing the Godot validation base so the next prototype step can build from a truthful, deterministic, testable foundation.
