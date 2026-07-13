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

# Clean Release comparison of exhaustive and exact branch-and-bound selection.
./scripts/run-job-selection-comparison.ps1 -ReleaseExport -Scenario balanced_basin -Seed 1337 -Citizens 16 -Ticks 300 -Trials 3

# Reproduce current residual spike attribution from the clean W1-05e matrix artifacts.
./scripts/analyze-performance-spikes.ps1 -InputPath artifacts/performance/w105e-baseline-9d5ca14 -OutputPath artifacts/performance/w105e-baseline-9d5ca14/spike-analysis.json

# Canonical W1-03c Windows Release matrix; 14 pairs and 28 metrics rows.
./scripts/run-performance-baseline-matrix.ps1
```

## Validation Notes

- The current test suite includes pure simulation tests, persistence tests, HUD tests, voxel-spike tests, and Godot headless smoke coverage.
- The required manifest declares 174 .NET tests: 106 fast, 11 integration, and 57 soak, plus 16 Godot headless tests.
- The full validation script is authoritative, but it is substantially longer than the individual .NET or headless passes because it rebuilds and reruns both.
- The optional performance pair is not a pull-request gate. It requires clean committed source by default and writes ignored run artifacts under `artifacts/performance/`.
- Performance result schema v6 records cold, natural-warm, and forced-invalidation cache transitions plus independent non-persistent selector, extraction-planning, and route-distance modes, exact-query accounting, pruning, route reuse, cached-distance replay hits, and phase timing. The runtime metrics CSV remains schema v4. Natural warmup advances deterministic simulation state; eager/all-pairs cache prefill remains disabled.
- A single pair can prove only its own transition. `scripts/run-performance-cache-modes.ps1` is the cross-mode authority: it requires identical cold/warm configuration, environment, tick bounds, and deterministic hashes plus a valid forced-invalidation transition.
- The Godot editor/headless execution route loads the managed Debug build, so its metrics-off/on pair remains characterization only.
- The tracked Windows export route uses the `Windows Performance Release` preset and accepts Release evidence only when the exported binary identifies its managed assembly as `ExportRelease`, reports the Godot release/template features, and reports neither a debug build nor the editor feature. It does not silently promote an ordinary managed `Release` build to reference evidence.
- The tracked solution exposes Godot's three managed configurations (`Debug`, `ExportDebug`, and `ExportRelease`) without mapping an ordinary solution `Release` configuration back to Debug.
- Raw catalog JSON is explicitly included in the Windows preset. Editor runs use the validated filesystem directory; exported builds always use packed `res://data` resources, avoiding working-directory-dependent inputs.
- The Release execution route is validated from clean commit `acf634f`; see `planning/active/evidence/v3-w1-03a-release-route-validation.json`. This is route evidence only, not a performance baseline.
- The clean schema-v3 ExportRelease cache-mode comparison passed from implementation commit `5444cc3`; see `planning/active/evidence/v3-w1-03b-cache-mode-validation.json`. It proves cold/warm deterministic equivalence and the forced-invalidation transition for a short three-citizen smoke only.
- The canonical W1-03c ExportRelease matrix passed its evidence contract from clean commit `a636967`: 14/14 pairs, 28 metrics rows, 354/354 manifest artifact hashes, cold/warm equality at 3/6/12/16 citizens, three comparable 16-citizen reference trials, two deterministic 1,000-tick soaks, the 24-citizen stress case, and the forced transition. The budget result is `safety_failure`, not a green performance gate: reference median p95 is 570.6155 ms versus 50 ms safety and median max is 3694.2534 ms versus 250 ms safety. Forced invalidation itself passes at 8.4171 ms. See `planning/active/evidence/v3-w1-03c-performance-baseline-validation.json`. Week 2 feature expansion remains blocked while W1-04/W1-05 correctness and path-selection work continues.
- W1-04 navigation correctness is implemented at `7918d49`: unreachable routes are explicit, blocked endpoints and diagonal corner cutting are rejected, the discounted A* heuristic is admissible, equal-priority ordering is deterministic, cached cell routes rematerialize exact endpoints, and unreachable work is skipped with diagnostics. Existing wetland resources remain usable through deterministic walkable interaction positions, while fresh and restored settlement placements are normalized to walkable cells. Validation passed 110/110 .NET tests, 16/16 Godot headless tests, zero-warning Debug/ExportDebug/ExportRelease builds, and independent review with no P0/P1 findings. See `planning/active/evidence/v3-w1-04-navigation-validation.json`.
- W1-05 exact selection is implemented from `b1fddaf` with clean Release evidence at `227a758`: a guarded straight-line score bound, strict-below-best pruning, ordinal ties, multi-leg reachability checks, and selected first-route reuse preserve exhaustive results. All four shipped scenarios matched per-tick assignments, events, resource application, and final snapshots for 300 ticks. At 16 citizens, selector exact queries fell from 17,441 to 2,544 (85.414%) in every clean Release trial; median p95 fell from 656.3981 ms exhaustive to 78.0301 ms optimized. Validation passed 119/119 .NET tests, 16/16 Godot headless tests, all managed configurations with zero warnings, the optimized cache-mode contract, and independent implementation review with no P0/P1 findings. The post-W1-05 canonical matrix passed all evidence contracts and improved the 16-citizen reference median p95 from 570.6155 ms to 81.4823 ms, but the 50 ms p95 and 250 ms maximum safety limits still fail. Week 2 feature expansion remains blocked while the remaining spikes are isolated. See `planning/active/evidence/v3-w1-05-job-selection-validation.json`.
- W1-05b characterizes those remaining spikes without changing gameplay. A deterministic postprocessor validated all 14 clean schema-v4 ExportRelease equivalence pairs and 5,301 metrics-on diagnostic ticks. Total wall time correlates strongly with path-cache misses (`r=0.982573`) but weakly with lookup volume (`r=0.233264`) and navigation rebuild time (`r=0.076652`). Each 16-citizen reference trial has the same 16 ticks over 50 ms; all seven ticks over 250 ms are initial-cold or immediately post-invalidation, and all six ticks over one second follow invalidation. Both 1,000-tick soaks reproduce the same 50 spike ticks and all 18 one-second spikes follow invalidation. Navigation rebuild itself is small; full-cache repopulation through work-order route ranking and exact citizen selection dominates. The safety gate remains red, so W1-06 and Week 2 remain blocked pending an exact cache-repopulation optimization and a fresh canonical matrix. See `planning/active/evidence/v3-w1-05b-spike-characterization.json`.
- W1-05c materially reduces cache-repopulation cost without changing A-star policy or deterministic gameplay. Exact bounded extraction planning cuts total path lookups by 22.72% to 67.96% across the four 300-tick scenario comparisons. Dense row-major navigation state, pooled generation-stamped A-star workspaces, exact multiplier caching, and shared immutable neighbor topology preserve dictionary-reference routes, float bits, unreachable behavior, ties, versioning, generation wrap, and concurrent queries. Full validation passes 151/151 .NET tests and 16/16 Godot headless tests. The clean schema-v5 14-pair ExportRelease matrix from `a772d15` passes all contracts and 354 artifact hashes: 16-citizen median p95 improves from the immediate pre-slice 81.4823 ms to 36.9614 ms, and median max from 1552.5664 ms to 259.702 ms. The forced metrics-off full tick is 245.699 ms against this slice's 250 ms diagnostic bound, while the formal commit-to-first-correct-lookup target passes at 11.8671 ms. The overall safety gate remains narrowly red: reference max is 9.702 ms above safety, soak maxima are 323.231/322.1246 ms, and 24-citizen stress reaches 272.6378 ms p95 and 607.9651 ms max. Remaining wall time still tracks cache misses (`r=0.959689`), with selector/global-frontier fanout dominating the worst stress tick. W1-06 and Week 2 remain blocked pending a reduction in distinct exact searches and another canonical matrix. See `planning/active/evidence/v3-w1-05c-cache-repopulation-validation.json`.
- W1-05d removes that shared-frontier fanout without using its lower-bound fields as routes or scores. A topology-only geometric field is built only when at least eight distinct current-version route keys are missing, shared by the settlement anchor, central depot, or one exact citizen origin for the current tick, and used only to strengthen strict candidate pruning. The unchanged versioned A-star remains authoritative for every unpruned and selected route. Eight direct lower-bound cases plus four 300-tick exhaustive selector differentials preserve exact worker state, route bytes and float bits, events, resources, endpoints, unreachable behavior, ties, and navigation invalidation; Balanced Basin selector queries fall from 2,544 to 1,482. Full validation passes 159/159 .NET and 16/16 Godot tests, with zero-warning Debug, ExportDebug, and ExportRelease builds and final independent review reporting no P0/P1/P2 findings. The clean schema-v5 14-pair matrix from `8688c27` passes all contracts and 354 hashes. Reference median max falls to 159.8224 ms, both soak maxima fall to 169.5998/165.5849 ms, and 24-citizen stress improves to 128.9719 ms p95 / 165.0386 ms max. Stress tick 292 falls from 67 general + 225 selector misses and 626.3771 ms to 34 + 2 misses and 157.7138 ms. The hard gate nevertheless remains `safety_failure`: one 16-citizen reference trial records 50.9476 ms p95 against the 50 ms limit, despite the 46.9161 ms median p95 and all maximum limits passing. Forced invalidation remains exact and passes at 11.4945 ms. W1-06 and Week 2 remain blocked pending a residual general work-order construction/variance reduction and another clean matrix. See `planning/active/evidence/v3-w1-05d-shared-frontier-validation.json`.
- W1-05e removes cached-route reconstruction from distance-only general work-order queries. A current-version positive general-cache hit now replays the cached cell path's distance with the same endpoint validation and float operation order, without allocating waypoints, copying route cells, or recomputing cost/ticks; misses, negative hits, selector/full-plan queries, and navigation-version invalidation remain unchanged. A full-materialization reference mode plus nine direct bit-exact cases and four 300-tick shipped-scenario differentials preserve routes, distance bits, assignments, events, resources, snapshots, endpoint/unreachable semantics, ties, and invalidation. The measured matrix exercised 316,534 fast-path hits. Full validation passes 174/174 .NET and 16/16 Godot tests, and final independent review reports no P0/P1/P2 findings. Performance/equivalence results advance to schema v6 while runtime metrics CSV remains v4; the spike analyzer accepts both v5 and v6 evidence. The clean 14-pair matrix from `9d5ca14` passes every contract and 354/354 hashes. Reference median p95 is 49.1537 ms and median max is 156.3861 ms; both soaks pass safety at 37.475/37.5293 ms p95 and 170.4334/162.4554 ms max. Stress is 132.4614 ms p95 / 154.5145 ms max, and forced invalidation is exact at 12.7154 ms. The hard gate remains `safety_failure` because reference trial 2 reaches 51.2372 ms p95, 1.2372 ms above safety. This slice reduces repeated cached-path materialization but intentionally does not change the 4,176 general or 1,262 selector exact misses observed across the matrix; distinct-search reduction remains the next blocker. Keep W1-06 and Week 2 blocked. See `planning/active/evidence/v3-w1-05e-cached-distance-validation.json`.
- The voxel spike is experimental only. The authoritative gameplay runtime remains heightfield-based through M3.

## CI Scope

CI should validate the authoritative Godot build and its supporting .NET tests only.
