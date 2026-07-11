# Societies V3: Two-Week Development Plan

## Document Control

| Field | Value |
|---|---|
| Status | Active |
| Prepared | 2026-07-09 |
| Execution window | Monday 2026-07-13 through Friday 2026-07-24 |
| Day-zero preparation | Friday 2026-07-10, optional, 30–60 minutes |
| Baseline branch | `master` |
| Baseline commit before synchronization | `c58e3de` |
| Target engine | Godot 4.6.2 Mono + C# |
| Capacity assumption | One developer, 40–50 focused hours; 44.5-hour core, with up to 5.5 hours reserve at full capacity |
| Product milestone | V3 hardening plus one player-driven crisis slice |

This is the authoritative short-horizon execution plan for the next two weeks. `CURRENT_BUILD.md` remains the authority for what the game already contains. Older session planning remains design reference only.

## Execution Log

- **2026-07-09, Day 1 started:** refreshed `origin`, fast-forwarded local `master` from `c58e3de` to `379300d`, and created `feature/v3-runtime-hardening`.
- **Baseline correction:** synchronized master contains 79 required .NET tests. Its first Windows run passed 77 and exposed two CRLF-sensitive CSV test assertions; the runtime CSV was valid.
- **First green checkpoint:** normalized line endings in the CSV test parser, then captured a zero-warning Release build, 79/79 .NET tests, and 13/13 Godot headless tests. See `planning/active/evidence/v3-day1-validation-manifest.json`.
- **First runtime hardening unit:** added a 12-tick rendered-frame cap that retains backlog, restores unattempted intervals after a tick failure, and leaves direct test/soak stepping uncapped. Validation passed 85/85 .NET and 14/14 Godot tests; see `planning/active/evidence/v3-w1-02a-catchup-validation.json`.
- **HUD coalescing unit:** removed redundant inventory/stockpile event-driven HUD rebuilds while retaining rendered-frame and explicit command refreshes. Validation passed 85/85 .NET and 15/15 Godot tests; see `planning/active/evidence/v3-w1-02b-hud-coalescing-validation.json`.
- **Metrics collector foundation:** added a pure, unused, instance-owned collector with bounded typed batches, reset/abort-safe one-shot phase tokens, separate rendered/manual batch kinds, and invariant CSV output. Validation passed 91/91 .NET tests; runtime wiring remains the next unit. See `planning/active/evidence/v3-w1-02c-runtime-metrics-collector-validation.json`.
- **Metrics runtime wiring:** gated collection behind `SOCIETIES_PERF_METRICS=1`, preserved metrics-off allocation/clock behavior, separated capped rendered frames from uncapped manual batches, exposed value diagnostics, and timed `BuildWorkOrders` at its actual call site. Validation passed 91/91 .NET and 16/16 Godot tests; see `planning/active/evidence/v3-w1-02d-runtime-metrics-wiring-validation.json`.
- **Metrics artifact export:** added bounded `runtime-batch-metrics-v3.csv` output to the normal save path, removes stale output when metrics are disabled, and isolates optional telemetry I/O failures from core snapshot saves. The existing public save overload and positional artifact-path API remain compatible. Validation passed 91/91 .NET and 16/16 Godot tests; see `planning/active/evidence/v3-w1-02e-runtime-metrics-export-validation.json`.
- **Runtime diagnostic schema:** added exact path hit/miss accounting, last cache size, runtime invalidation/rebuild timing, worker count, and generic assignment candidate pressure. The CSV keeps its original 20-column prefix and appends eight fields; metrics-off still performs no clock read or row allocation. A forced path completion and exact candidate-count tests are green. Benchmark-only eager warmup remains separate until deterministic state equivalence is proven because current cache entries embed exact endpoints. Validation passed 91/91 .NET and 16/16 Godot tests; see `planning/active/evidence/v3-w1-02f-runtime-diagnostics-validation.json`.
- **Standalone performance-runner tooling:** implemented an optional paired runner that executes matching metrics-off and metrics-on cases, records timing statistics, deterministic hashes, diagnostics, environment/build identity, and machine-readable manifests, and requires clean committed source by default. The short characterization command is `./scripts/run-performance-pair.ps1 -Scenario balanced_basin -Seed 1337 -Citizens 3 -Ticks 3`; its ignored artifacts are written below `artifacts/performance/`. Final validation passed 4/4 focused statistics tests, 79/79 fast .NET tests, 95/95 required .NET tests, a clean Godot solution build, and 16/16 Godot headless tests. A clean-source pair from `a791c3c` produced exact snapshot, event-log, and combined-hash equivalence with three metrics batches and zero drops; see `planning/active/evidence/v3-w1-02g-performance-runner-validation.json`. This completes V3-W1-02; the current Godot editor/headless route is Debug characterization rather than V3-W1-03 Release evidence. V3-W1-03 remains next: establish a verified Release route, then run the complete cold/warm/invalidation matrix and median reference cases.
- **V3-W1-03 Release-route slice in progress:** added a tracked `Windows Performance Release` preset, an export-only `performance_runner` main-scene override, a Godot-configured `Societies.sln`, exported-PCK catalog loading, and runtime assertions that distinguish Godot's managed `ExportRelease` configuration from an ordinary managed `Release` build. `run-performance-pair.ps1 -ReleaseExport` exports and invokes the Windows console wrapper, pins Godot 4.6.2, records exact runner/process/project provenance, and rejects debug, editor-hosted, or incomplete release-template identities. Implementation validation passed 6/6 focused tests, 79/79 fast .NET tests, 95/95 required .NET tests, zero-warning Debug/ExportDebug/ExportRelease solution builds, a clean Godot solution build, and 16/16 Godot headless tests. A dirty-source Release pair proved export/PCK/wrapper mechanics and exact deterministic equivalence; a Debug negative control failed only `release_environment`. The route still requires an implementation commit, a clean pair from that exact SHA, and a machine-readable V3-W1-03a evidence file before it is complete.

## Decision and Intended Outcome

Continue from the existing Godot project and establish a clean V3 boundary. Do not restart the repository and do not migrate to Unity during this sprint.

The sprint has two linked outcomes:

1. **Week 1 — trustworthy simulation kernel:** correct navigation, measured performance, bounded runtime instrumentation, and a single authoritative owner for resource quantities.
2. **Week 2 — smallest meaningful game loop:** the player contributes to the settlement, chooses a directive, observes citizens respond for understandable reasons, and reaches a clear Stable or Collapsed outcome.

At the end of Day 10, record one explicit result:

- **Continue V3:** technical gates pass and the crisis demonstrates understandable agency.
- **Narrow iteration:** engineering passes, but playtests show that the crisis, directive, or citizen causality needs refinement.
- **Stop feature expansion:** correctness, determinism, persistence, or the performance safety gate fails; return to the last green boundary before adding systems.

## Starting Reality

### Repository and validation baseline

- Local `master` is at `c58e3de` and is two commits behind `origin/master`.
- `origin/master` adds `7a4270a` (metrics CSV/build fix) and `379300d` (uncapped work-order diagnostics).
- The pre-sync audit produced a Release build with zero warnings/errors, 64/64 .NET tests passing, and 13/13 Godot headless tests passing.
- Synchronizing `7a4270a` and `379300d` adds 15 required .NET tests, so the active preservation floor is now 79 .NET and 13 Godot tests. The old 64 count is historical evidence only.
- `origin/feature/runtime-observability-and-hardening` is based on the older local master and overlaps current simulation work. It is evidence and a source of selected changes, not a branch to merge wholesale.

### Performance evidence, not yet the new baseline

The hardening branch demonstrated that 16 citizens over 300 ticks can reach approximately 13.3 ms mean, 19.4 ms p95, and 41.2 ms p99 after warmup. It also reduced a large initial spike from roughly 18 seconds to 2.6 seconds by eagerly warming routes.

That result is encouraging but incomplete:

- eager warmup creates roughly 9,400 cached routes at the current world scale;
- the benchmark did not exercise a real navigation invalidation;
- timing and diagnostic collection still need lifecycle, memory, and CSV hardening;
- the job selector still computes exact paths for too many candidates.

All sprint performance claims must be regenerated from the synchronized V3 branch.

### Product baseline

The current build is a strong simulation debugger but not yet a compelling player loop. It has first-person harvesting, a player inventory and separate settlement stockpile, one crafting recipe, citizen task explanations, persistence, settlement classifications, and rich debug overlays. It does not yet let the player deliberately shape the shared economy or make a consequential settlement-level choice.

The V3 slice will reuse those systems. It will not add markets, broad governance, dialogue, combat, networking, or production art.

## Scope

### Sprint goals

- Synchronize and lock a reproducible V3 baseline.
- Selectively port and harden useful runtime observability.
- Fix known navigation defects before optimizing navigation use.
- Reduce exact path queries without changing the exhaustive selector's result.
- Move resource quantities into session-owned authoritative state.
- Add safe snapshot schema handling for the changed state boundary.
- Create one deterministic crisis scenario named `empty_stores`.
- Let the player transfer eligible resources into the settlement stockpile atomically.
- Let the player choose between two visible settlement directives.
- Show how a directive affected a citizen's assignment reason.
- Resolve the crisis with deterministic, visible rules.
- Capture enough telemetry and playtest evidence to decide what to build next.

### Explicit non-goals

- Unity migration or DOTS prototype
- multithreaded simulation or ECS conversion
- multiplayer, ENet, backend services, or production accounts
- authoritative voxel terrain
- markets, trade, laws, elections, broad governance, or autonomous external AI agents
- combat, social relationships, dialogue, or approval simulation
- crafting expansion or redesign
- spatial indexing, crowd simulation, or 256-citizen optimization
- a separate engine-neutral assembly or removal of every Godot type
- custom models, animation polish, music, or audio
- multiple crises, dynamic difficulty, or a quest framework

These are deferred, not rejected. None is required to answer the next product question: *is directing and helping a visible society fun and understandable?*

## Target Runtime Boundary

```mermaid
flowchart LR
    Input["Godot input and interaction"] --> Command["Deterministic runtime commands"]
    Command --> Session["PrototypeRuntimeSession"]
    Session --> World["World and resource ledger"]
    Session --> Settlement["Citizens, stockpile, structures, directive, crisis"]
    Session --> Output["Snapshots, events, metrics, command results"]
    Output --> Presenter["Godot presenter and HUD"]
    Presenter --> View["Scene nodes are views, prompts, and hit targets"]
```

| State | Authoritative owner | Godot scene responsibility |
|---|---|---|
| Resource IDs and quantities | `PrototypeRuntimeSession` resource ledger | Render snapshots and provide interaction targets |
| Player inventory | Runtime session | Display and submit commands |
| Settlement stockpile | Settlement/runtime session | Display only |
| Citizens, orders, structures, paths | Settlement/runtime session | Render only |
| Active directive and crisis | Runtime session | Display and submit choices |
| Saves, events, and metrics | Captured from runtime state | Trigger/export only |

The scene must not decrement resources or repair runtime state after a tick. Commands mutate state once; presenters consume results.

## Definition of Success

### Technical release gates

| Area | V3 target | Safety failure or blocker |
|---|---|---|
| Build | Release build succeeds with zero warnings/errors | Build failure or new unexplained warning |
| Existing tests | Existing 79 .NET and 13 Godot tests, plus new tests, all pass | Any unexplained loss or failure |
| Determinism | Repeated fixed-seed 1,000-tick runs have exact canonical state and event ordering | Any mismatch |
| Resume | Uninterrupted 1,000 ticks equals save at 500, restore, and continue | Any state loss, duplication, or event-order mismatch |
| 16 citizens / 300 ticks | Median of three: p95 ≤25 ms, p99 ≤50 ms, max ≤100 ms, total ≤6 s | p95 >50 ms or any tick >250 ms |
| 16 citizens / 1,000 ticks | p95 ≤25 ms, no crash/stall/backlog, exact repeat hash | Any tick >250 ms or nondeterminism |
| 24-citizen stress | Characterize; target p95 ≤50 ms and max ≤200 ms | Report before making it a hard cross-machine gate |
| Bootstrap | ≤3 s target; ≤2 s stretch | >5 s |
| Forced navigation invalidation | ≤150 ms target | >250 ms or incorrect route reuse |
| Assignment work | At least 60% fewer assignment-time exact path queries at 16 citizens | Behavior differs from exhaustive reference |
| Fixed-step protection | Never process more than 12 catch-up ticks in one rendered frame | Cap bypassed or backlog grows without warning |
| Resource authority | One writer; harvesting and deposit cannot duplicate or lose resources | Any scene-owned quantity mutation remains |

Absolute timing gates use the same named reference machine, fixed scenario and seed, Release build, and the median of three runs. CI on unrelated hardware reports trends and enforces broad safety limits. Cache hit rate is diagnostic unless warm-cache mode is enabled; in that mode target at least 99% after warmup and investigate below 98%.

The release reference is `balanced_basin`, simulation seed `1337`, 16 citizens, Release build, with runtime metrics disabled for the primary timing run and an equivalent metrics-enabled companion run for diagnosis. Define intervals consistently:

- **cold bootstrap:** entry to `PrototypeRuntimeSession.Initialize` through generated world, initialized simulation, and first presentable runtime snapshot, with no pre-warmed route cache;
- **warm benchmark:** the same fixed run after the benchmark-only warmup completes; warmup time is reported separately;
- **forced invalidation:** the tick that commits a path-segment navigation-version change through completion of the first correct post-change route lookup, excluding artifact serialization.

The performance runner must record its exact invocation and configuration in the manifest so these boundaries can be reproduced.

### Player-loop gates

- A no-input reference run reaches Collapsed inside the tuned crisis window.
- A scripted contribute-and-direct run reaches Stable.
- Contributions never lose or duplicate resources and are consumed through normal citizen work.
- Directives produce materially different future assignments without canceling active tasks or overriding critical eating/sleeping.
- A citizen explanation names the directive when it materially influenced the assignment.
- The result is emitted once, survives save/load, and explains why it occurred.
- The slice is playable without editing debug state or using a developer command.
- No P0/P1 crash, data loss, soft lock, or progression blocker remains.

### Playtest decision gates

Recruit five observed testers. The numerical gates below are valid only when all five complete a usable session. Three or four completed sessions are directional evidence and must be labeled product validation incomplete; fewer than three are insufficient for a product conclusion.

- 4/5 identify the crisis goal within two minutes.
- 4/5 contribute without facilitator instruction.
- 3/5 correctly explain the directives.
- 3/5 correctly explain why one inspected citizen chose a task.
- 3/5 rate clarity and agency at least 5/7.
- 3/5 say they would try another crisis.
- No critical defect invalidates a session.

Low win rate alone means tuning. Failure to understand contribution, directives, or citizen causality means the loop needs design iteration.

## Work Breakdown

Estimates include implementation, focused tests, and review. Automated suite runtime is not counted as focused labor. The core totals approximately 44.5 hours, leaving up to 5.5 hours at full capacity for regression repair and tuning. Use the capacity adjustments below if fewer than 44.5 hours are actually available. Stretch work is separate.

### Week 1 — V3 hardening and state authority

| ID | Deliverable | Estimate | Depends on |
|---|---|---:|---|
| V3-W1-01 | Synchronize and lock the baseline | 2 h | — |
| V3-W1-02 | Selectively port and harden observability | 4.5 h | W1-01 |
| V3-W1-03 | Capture current performance evidence | 1.5 h | W1-02 |
| V3-W1-04 | Correct navigation semantics | 4.5 h | W1-03 |
| V3-W1-05 | Preserve-exact job-selection optimization | 4.5 h | W1-04 |
| V3-W1-06 | Session-owned resource ledger and schema v6 | 6.5 h | W1-04; contracts begin during W1-05 |
|  | **Week 1 total** | **23.5 h** |  |

#### V3-W1-01 — Synchronize and lock the baseline

- Confirm a clean worktree and fetch current remote state.
- Fast-forward `master` to include `7a4270a` and `379300d`.
- Run the full validation loop before branching.
- Record SHA, hardware, tool versions, scenario, seed, test counts, and durations.
- Create `feature/v3-runtime-hardening` from the verified SHA.
- Create a local baseline tag only after the synchronized commit is green.
- Add a test manifest and classify tests as Fast, Integration, Soak, or Extended without removing coverage.

Acceptance:

- Baseline metadata and commands are reproducible.
- The authoritative build and both test suites pass.
- The baseline can be restored without destructive history operations.
- Fast feedback is identified; the full validation script remains authoritative.

#### V3-W1-02 — Selectively port and harden observability

Port useful behavior from `origin/feature/runtime-observability-and-hardening` as reviewable units:

- 12-tick catch-up cap and backlog warning;
- phase/tick timing and artifact export;
- Godot performance runner;
- path lookup, hit, miss, invalidation, cache size, work-order, and worker-count diagnostics;
- HUD update deduplication;
- optional eager route warmup for comparison only.

Harden it during the port:

- explicit per-run reset so metrics cannot leak across tests/restarts;
- bounded or streamed tick rows rather than an unbounded list;
- invariant-culture, escaped CSV;
- no diagnostic allocation or `Stopwatch` work when metrics are disabled;
- correct nested phase measurement and non-zero tick accumulation;
- preserve `WorkOrdersGeneratedUncapped` from master;
- move performance characterization to `Societies.Core.Tests.Extended`;
- label old benchmark reports as historical.

Acceptance:

- Metrics-on and metrics-off runs end with the same deterministic hash.
- Metrics-off performs no per-tick diagnostic-row allocation.
- Exported CSV parses and contains real measurements.
- A Godot test proves `_Process` never advances more than 12 catch-up ticks per rendered frame.
- Characterization is excluded from the normal pull-request gate.
- Each logical port can be reverted independently.

#### V3-W1-03 — Capture current performance evidence

Run cold and warm configurations for 3, 6, 12, and 16 citizens over 300 ticks, plus a 16-citizen 1,000-tick soak, a 24-citizen stress run, and a forced path-completion/invalidation case.

Capture initialization time, tick p50/p95/p99/max, exact path queries, hits/misses, cache size, candidates per idle citizen, invalidations/rebuild time, capped/uncapped orders, final hash, and artifact paths.

Acceptance:

- Three comparable runs exist for the release-scale case.
- A machine-readable manifest identifies environment and configuration.
- Cold, warm, and invalidated behavior compare without changing results.
- Evidence decides whether all-pairs warmup remains benchmark-only or is temporarily required.

#### V3-W1-04 — Correct navigation semantics

- Replace fake direct paths for unreachable destinations with explicit `TryFindPath`/reachability semantics.
- Reject unwalkable starts and destinations.
- Prevent diagonal corner cutting unless both adjacent orthogonal cells are traversable.
- Scale the A* heuristic by the minimum possible traversal multiplier, including built-path discounts.
- Skip and count unreachable work orders instead of assigning them.
- Preserve stable deterministic tie-breaking.

Acceptance tests:

- disconnected destinations are unreachable and never route through blocked terrain;
- blocked orthogonal neighbors prevent an illegal diagonal, while valid diagonals remain allowed;
- A* cost matches a small Dijkstra reference on handcrafted grids, including built paths;
- repeated queries return identical reachability, cells, waypoints, and cost;
- a citizen with only unreachable work stays idle with a diagnostic reason;
- any fixture/hash change is reviewed and explained as a correctness change.

#### V3-W1-05 — Preserve-exact job-selection optimization

Replace exhaustive exact routing of every candidate with exact branch-and-bound:

1. Derive and document a safe score upper bound using straight-line distance and the existing score formula.
2. Sort by that bound, then stable `OrderId`.
3. Compute exact routes only while a remaining candidate can beat the best exact score.
4. Evaluate ties; stop only when the next bound is strictly below the best score.
5. Skip unreachable candidates.
6. Reuse the selected route when travel begins.

Do not use an arbitrary top-3/top-5 shortlist. If a safe bound cannot be demonstrated, keep the exhaustive reference and optimize elsewhere.

Acceptance:

- Differential tests compare exhaustive and optimized selection across every shipped scenario for at least 300 ticks.
- Selected order IDs and final snapshots match exactly.
- Assignment-time exact path queries fall by at least 60% at 16 citizens.
- Tick p95 does not regress more than 10%.
- Cold, warm, and invalidation benchmarks are rerun.
- If cold startup meets budget, eager all-pairs warmup leaves normal startup.

#### V3-W1-06 — Session-owned resource ledger and schema v6

This is an atomic authority migration, not a full rewrite.

- Give each generated resource site a stable deterministic ID.
- Add a pure C# resource ledger owned by `PrototypeRuntimeSession`.
- Represent player and AI harvesting with command/result records using IDs and quantities.
- Make `Advance` consume session state instead of a scene-captured resource list.
- Make the presenter render snapshots; scene nodes cannot mutate quantity.
- Source persistence, metrics, and summaries from the ledger.
- Restore the session before repainting the scene.
- Bump runtime snapshots from schema v5 to v6.
- Add explicit v5-to-v6 migration that imports existing resource snapshots into the authoritative ledger. Freeze v6 to this resource-authority shape; it must not acquire Week 2 crisis fields later.
- Reject unsupported future schemas and malformed data clearly.

Acceptance:

- A pure headless session advances 300 ticks without presenter or scene-resource input.
- Player and AI harvest decrement exactly once.
- Missing and depleted IDs fail deterministically without mutation.
- Resources survive save/load and checkpoint/resume exactly.
- The loop no longer uses scene `CaptureResourceSites()` or presenter `ApplyHarvestRequest()` as authoritative mutations.
- v5 migration, v6 round-trip, malformed input, and future-version rejection tests pass.
- The migration lands atomically; if unfinished, revert it and retain only contracts/tests/design notes.

### Week 1 Exit Gate

The **hard safety gate** requires:

- standard build, core tests, Godot build, and smoke tests are green;
- navigation correctness passes;
- determinism and checkpoint/resume are exact;
- an enabled optimized selector is equivalent to exhaustive selection; otherwise the exhaustive selector remains active;
- 16-citizen performance stays below every safety limit;
- resource quantity has one authoritative writer;
- v5 migration and v6 round-trip pass;
- work is split into reviewable observability, navigation/selection, and state-ownership units.

The **target gate** additionally requires the ≤25 ms p95 target, ≤3 s bootstrap, and at least 60% fewer assignment-time exact path queries. Full Week 2 scope begins only when both gates pass.

The contracted Week 2 scope is allowed only when every hard safety item—including W1-06 resource authority—is green but one or more target items miss. If any hard safety item is red, stop feature expansion and use Week 2 to restore a green runtime. Do not hide a red technical gate behind new UI.

### Week 2 — Player-Driven Crisis Slice

| ID | Deliverable | Estimate | Depends on |
|---|---|---:|---|
| V3-W2-01 | Freeze `empty_stores` crisis contract and reference runs | 2.5 h | Week 1 gate |
| V3-W2-02 | Atomic contribution to the shared economy | 3.5 h | W1-06, W2-01 |
| V3-W2-03 | Two directives and citizen causal explanation | 4.5 h | W2-01 |
| V3-W2-04 | Deterministic outcome and minimal crisis HUD | 3.5 h | W2-02, W2-03 |
| V3-W2-05 | Schema v7, telemetry, and automated slice tests | 3 h | W2-02–04 |
| V3-W2-06 | Release validation, observed playtests, and decision report | 4 h | W2-05 |
|  | **Week 2 total** | **21 h** |  |

#### V3-W2-01 — Freeze the crisis contract

Add one catalog-driven scenario, `empty_stores`, using existing terrain and settlement systems:

- 12 citizens;
- two starting huts and a third hut queued;
- low meals and hearth fuel;
- reachable berries and logs;
- enough resources for either strategy to matter;
- a visible deadline and deterministic seed.

The loop is:

> understand the shortage → harvest → contribute → choose a directive → inspect citizen response → adapt → reach an outcome

Start with these tuning targets and adjust only from recorded reference runs:

- no-input collapse in roughly 8–14 minutes;
- a scripted strategy stabilizes in roughly 10–18 minutes;
- Stable requires at least 9 of 12 citizens capable, 6 meals, hearth fuel 4, and 50% bed coverage, held for 45 seconds;
- Collapsed occurs when 6 citizens remain incapacitated for 10 seconds or the 18-minute deadline expires.

All crisis timers use simulation ticks. The minute/second values above describe simulation time at normal speed for player readability. Crisis time advances exactly once per simulation tick, stops while simulation is paused, and is unaffected by render frame rate or machine speed. Faster simulation advances more ticks by design, while the HUD always derives its clock from tick state.

Acceptance:

- Conditions are catalog/domain data, not HUD logic.
- Hold timers reset when a condition breaks and prevent one-tick outcomes.
- Fixed-seed no-action and candidate-success traces are retained as test evidence.
- Existing scenarios are unchanged when crisis configuration is absent.

#### V3-W2-02 — Atomic contribution to the shared economy

- Add a runtime command such as `ContributeToStockpile(itemId, amount)` with an explicit result.
- Accept eligible raw resources from player inventory and add them to the central stockpile in one mutation.
- Keep crafted tools personal in the core scope.
- Use a central-depot proximity prompt that deposits all eligible resources. A 3D crate and partial-quantity picker are stretch work.
- Emit clear success, empty-inventory, invalid-item, and rejected-command feedback.

Acceptance:

- Success removes and adds identical quantities exactly once.
- Failure leaves both inventories unchanged.
- Citizens consume contributions through normal work systems.
- Repeated input in one frame cannot duplicate a transfer.
- Contribution events and per-resource counters are deterministic.
- Unit, session, save/load, and Godot interaction tests cover the path.

#### V3-W2-03 — Two directives and citizen causal explanation

Add two choices:

- **Food & Fuel:** favors berries, meals, firewood, hearth refueling, and related hauling.
- **Shelter:** favors timber/thatch, supplying the queued hut, and construction.

Rules:

- a neutral state exists until the player chooses;
- a directive changes scoring for future assignments only;
- it never cancels active work;
- critical eating, sleeping, and recovery remain dominant;
- modifiers are constant-time and deterministic;
- when the optimized selector is enabled, every modifier is included in the branch-and-bound upper-bound calculation introduced in W1-05;
- the directive is session-owned and its v7 snapshot contract is frozen here; serialization lands in W2-05;
- when it changes a winning assignment, `CurrentOrderReason` includes a cause such as `Why: Shelter — construction lumber`.

Acceptance:

- Fixed-seed runs under each directive produce materially different assignment mixes.
- Food & Fuel increases relevant assignments; Shelter increases hut/material assignments.
- Critical-needs tests prove directives cannot starve or exhaust a citizen for lower-priority work.
- Exhaustive-versus-optimized differential tests pass under Neutral, Food & Fuel, and Shelter for every shipped scenario; selected order IDs and final snapshots match exactly.
- Inspector/HUD text claims directive influence only when it mattered.
- Command, event, and snapshot ordering remain deterministic.

#### V3-W2-04 — Deterministic outcome and minimal crisis HUD

- Display crisis name, remaining time, active directive, cumulative contributions, and four stability conditions.
- Show hold progress without adding a general quest framework.
- Emit Stable or Collapsed once, pause the crisis, and show a compact causal summary.
- Allow reset/replay from terminal state.
- Use existing procedural/debug UI; do not create an art dependency.

Acceptance:

- The terminal event fires exactly once.
- Save/load during a hold preserves progress.
- Save/load after completion preserves the result without re-emitting it.
- The domain exposes elapsed/deadline ticks, both hold counters, terminal outcome, and event-emitted state for W2-05 persistence.
- The no-input reference collapses and scripted reference stabilizes.
- A contribution is reflected on the next presentation update.

#### V3-W2-05 — Schema v7, telemetry, and automated slice tests

Freeze the directive/crisis persistence contract, bump runtime snapshots from v6 to v7, and add an explicit v6-to-v7 migration with Neutral/no-crisis defaults. A v5 save must migrate through v6 to v7. Extend events, summaries, and metrics with:

- elapsed/deadline ticks, stability-hold ticks, collapse-hold ticks, terminal outcome, and terminal-event-emitted state;
- outcome, failure reason, and elapsed time;
- first directive and first contribution ticks;
- directive changes and final directive;
- contributions by resource;
- peak incapacitated citizens;
- minimum meals and hearth fuel;
- maximum bed coverage;
- final condition values;
- stability-hold entry/break and terminal events.

Required scenarios:

- no input → Collapsed;
- scripted Food & Fuel followed by Shelter → Stable;
- repeated same-seed run → identical outcome, events, and snapshot;
- save at midpoint/resume → identical final result;
- existing non-crisis scenario → unchanged behavior;
- failed contribution → no mutation;
- critical-needs override → preserved.

Acceptance:

- v6-to-v7, chained v5-to-v7, v7 round-trip, malformed input, and future-version rejection tests pass;
- Artifacts validate and contain no unbounded per-tick narrative log.
- The full suite remains green.
- Slice tests use commands/domain state rather than brittle UI timing where possible.
- One Godot smoke test covers the real input-to-HUD path.

#### V3-W2-06 — Release validation, playtests, and decision

Run the release candidate from a clean state:

- Release build and full .NET/Godot suites;
- 16-citizen matrix and 1,000-tick determinism/resume;
- forced navigation invalidation;
- a 20–30 minute smoke covering contribution, both directives, inspection, save/load, outcome, reset, and artifacts;
- five observed playtests if testers are available.

Give testers only the in-game briefing. Record time to first directive, harvest, contribution, citizen inspection, outcome, facilitator prompts, and confusion/boredom moments.

Ask afterward:

1. What was the crisis?
2. What actions could change it?
3. What did each directive do?
4. Why was the inspected citizen doing that task?
5. Did contributing visibly affect the settlement?
6. What caused the result, and did it feel fair?
7. Would you play another crisis?

Deliver `V3_SPRINT_VALIDATION_REPORT.md` with baseline-versus-RC results, defects, playtest evidence, and one sprint conclusion.

## Ten-Day Schedule

| Day | Date | Focus | End-of-day proof |
|---|---|---|---|
| Day 0 | Fri Jul 10 | Optional remote sync, tester invitations, tool/version check | No code change required |
| Day 1 | Mon Jul 13 | W1-01; begin staged observability port | Green synchronized baseline, V3 branch, manifest |
| Day 2 | Tue Jul 14 | Finish W1-02 and W1-03 | Hardened metrics plus cold/warm/invalidation evidence |
| Day 3 | Wed Jul 15 | W1-04 navigation; draft W1-06 ledger/migration contracts | Navigation tests green; authority contracts and migration fixtures ready |
| Day 4 | Thu Jul 16 | W1-05 optimization; begin W1-06 implementation | Differential report; resource migration underway behind tests |
| Day 5 | Fri Jul 17 | W1-06 resource authority/schema; Week 1 gate | One writer, v5 migration/v6 round-trip, full green gate |
| Day 6 | Mon Jul 20 | W2-01 and W2-02 | Crisis setup and atomic shared-economy contribution |
| Day 7 | Tue Jul 21 | W2-03 | Directives visible in assignment reasons |
| Day 8 | Wed Jul 22 | W2-04; first author smoke | End-to-end loop plus first confusion/defect notes |
| Day 9 | Thu Jul 23 | W2-05, 2–3 observed sessions, code freeze at midday | Repeat/resume green, artifacts, first tester evidence |
| Day 10 | Fri Jul 24 | Clean RC, remaining observed sessions, report | Final validation and evidence-backed decision |

## Validation Strategy

### Test tiers

| Tier | Purpose | Cadence |
|---|---|---|
| Fast | Build, deterministic units, navigation edges, schema checks, short session advance, one Godot bootstrap | Before coding, after logical changes, before push; <90 s local target |
| Required full | All .NET and Godot headless tests | Every review/merge; parallel CI jobs when available |
| Nightly/slow | 1,000-tick repeat, save-at-500 resume, 16/24-citizen perf, catch-up cap, artifact validation | Nightly during sprint |
| Extended | Frontier starvation, capped/uncapped workload, path/logistics, 16/24/64 scaling | End of each week; 256 citizens informational only |

The existing full validation loop remains authoritative until a replacement proves it runs the same manifest.

Standard commands:

```powershell
dotnet build src/societies/Societies.csproj --configuration Release
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1
```

If `godot` is not on `PATH`, Day 0 must resolve and record the absolute Godot 4.6.2 Mono executable (or the repository's supported environment variable) and use that same binary throughout the sprint.

### Required artifacts

- `validation-manifest.json`: SHA, dirty state, hardware, tool versions, command, scenario, seed, counts, durations, and budget result
- `.trx` test results and coverage output if already supported
- complete Godot headless log and machine-readable summary
- `perf-matrix.csv`, `perf-results.json`, and summary text
- bounded per-tick diagnostics for performance runs
- canonical state hashes for repeat/resume
- representative snapshot, event log, run summary, world summary, and metrics CSV
- final `V3_SPRINT_VALIDATION_REPORT.md`

Retain review artifacts for at least seven days, nightly/extended artifacts for 30 days, and the final bundle for 90 days or with the milestone tag.

### Daily cadence

- Start from the latest green commit and run the fast gate.
- After a logical change, run targeted tests and build the touched layer.
- Before push/handoff, run the fast gate.
- End each day with latest green SHA, open P0/P1 list, failure classification, and performance trend.
- Do not call a retry a resolution; retain and classify the first failure artifact.
- Freeze feature scope at midday Day 9. Remove or disable incomplete work rather than waive a gate.

## Commit and Rollback Strategy

Recommended reviewable units:

1. baseline/test taxonomy;
2. metrics and artifacts;
3. catch-up protection and presentation throttling;
4. navigation correctness;
5. exact selection optimization;
6. resource ledger and migration;
7. crisis domain and contribution;
8. directives and explanations;
9. outcome UI, telemetry, and tuning;
10. validation report and documentation.

Use `feature/v3-runtime-hardening` for Week 1. After the Week 1 gate is integrated or otherwise locked at a green SHA, create `feature/v3-empty-stores-slice` from that boundary for Week 2. Do not make the crisis slice depend on unreviewed hardening-branch history.

Use normal revert commits. Never rewrite history destructively to rescue the sprint.

Block or revert the smallest responsible unit when:

- a baseline correctness test fails without an approved behavior change;
- deterministic hashes diverge;
- save/load loses or duplicates state;
- reachability changes without an intentional tested correction;
- 16-citizen p95 exceeds 50 ms, any tick exceeds 250 ms, or bootstrap exceeds 5 s;
- the full suite remains red at end of day.

## Risks and Mitigations

| Risk | Signal | Mitigation |
|---|---|---|
| Stale hardening branch conflicts | Settlement/metrics overlap | Port manually in small units; never wholesale merge |
| Warmup moves rather than removes work | Large cache and slow init | Keep optional, reduce selection queries, test invalidation |
| Navigation fix changes outcomes | Fixture/hash changes | Compare with Dijkstra, review expected changes, preserve ties |
| Unsafe pruning changes jobs | Differential mismatch | Require proven bound; keep exhaustive oracle/fallback |
| Half-migrated resource authority | Scene and session both mutate | Make atomic or revert completely |
| Schema change breaks saves | v5 failure or silent defaults | Migration fixtures, future-version rejection, round-trip tests |
| Directives become hard orders | Needs overridden/task churn | Modify future scoring only; active tasks and critical needs win |
| Crisis becomes UI-only | HUD and simulation disagree | Domain owns conditions/outcome; HUD presents snapshots |
| Timing tests are noisy | Machines disagree | Same-machine median; counters and safety bands in CI |
| Playtests unavailable | Fewer than five usable outside sessions | Finish scripted evidence; treat 3–4 as directional and mark product validation incomplete |
| Scope expands | Mid-sprint system proposals | Defer them; enforce Day 5 and Day 9 gates |

## Scope Cuts and Contingencies

### Cut order

Cut first:

1. partial-quantity deposit selection;
2. bespoke 3D crate; use a depot proximity prompt;
3. mouse directive menus; keep two clear key/button actions;
4. custom art, audio, animation, and transition polish;
5. coverage thresholds and broad CI restructuring;
6. 24/64-citizen tuning beyond characterization;
7. extra directives, crises, dialogue, difficulty, or quest markers.

Never cut:

- navigation correctness;
- deterministic state and checkpoint/resume;
- one authoritative resource writer;
- atomic shared-economy contribution;
- one consequential directive with a citizen `Why` explanation;
- deterministic outcome and basic telemetry;
- at least one end-to-end playtest/smoke pass.

### Contracted Week 2 if Week 1 is late

If every hard safety item is green but the Week 1 target gate is late or missed:

- use `food_poor_highlands` instead of adding `empty_stores`;
- implement only atomic contribution and one Food & Fuel directive through the command boundary;
- show existing settlement classification and citizen reason instead of a new terminal overlay;
- spend Days 9–10 on determinism, persistence, performance, and causal-clarity testing;
- freeze the full v7 persistence shape with safe no-crisis defaults, but defer crisis logic and terminal UI; do not silently extend the same schema later.

If resource authority, navigation correctness, build/tests, determinism, resume, or a performance safety limit is red, none of those contracted features begins.

If 16-citizen p95 remains above 50 ms on Day 5, Week 2 becomes correctness/performance only. This supports another algorithm sprint, not by itself a Unity/DOTS migration.

If determinism or resume is red on Day 7, stop feature work and return to the latest green SHA. If scripted success is not possible by midday Day 9, stop presentation polish and use the remaining time for pacing, correctness, and causal clarity.

### Capacity adjustment

- **Under 30 hours:** complete W1-01 through W1-04, then W1-06. If the hard gate passes on the exhaustive selector, skip W1-05 and use any remaining time for the contribution command on an existing scenario. Defer directives/crisis and do not claim the slice complete. Never implement contribution without the atomic resource migration.
- **30–40 hours:** complete Week 1 and the contracted Week 2 slice.
- **40–50 hours:** execute this core plan.
- **More than 50 hours:** use extra time only for stretch work after core gates pass.

## Stretch Backlog

- 3D contribution crate with clearer affordance
- partial-quantity contribution UI
- parallel required .NET and Godot CI jobs while preserving the manifest
- automated performance baseline comparison and safety-failure exit code
- improved terminal presentation using existing assets
- Infrastructure directive, only after two-mode causality is clear
- 64-citizen extended characterization
- more external playtests

## End-of-Sprint Review

The Day 10 report answers:

1. Did V3 preserve or improve correctness, determinism, persistence, and performance?
2. Is every resource mutation owned by the runtime session?
3. Can a player understand how contributing and directing changes behavior?
4. Does the crisis create a readable decision with a fair outcome?
5. What is the smallest next milestone justified by evidence?

Decision rules:

- **Technical and playtest gates pass:** continue V3 in Godot; plan a second crisis or deeper citizen-policy interaction.
- **Technical passes, playtest misses:** keep the architecture and run a narrow clarity iteration; do not rewrite.
- **Performance target misses but safety passes:** continue algorithm work before considering engine migration.
- **Correctness, determinism, or persistence fails:** revert to last green and suspend expansion.
- **External tests incomplete:** engineering may be complete, but do not claim product validation.

## Definition of Done

- Every core acceptance criterion is passed or explicitly recorded as cut.
- The final commit is reproducible from a clean checkout.
- Required automated validation is green.
- Performance evidence uses the fixed reference configuration.
- One manual run covers start, contribution, both directives, citizen explanation, save/load, outcome, reset, and artifacts.
- Playtest evidence or its availability gap is recorded.
- `CURRENT_BUILD.md` is updated to describe implemented V3 reality, not intent.
- The report records Continue V3, Narrow Iteration, or Stop Feature Expansion.
- This document becomes Complete or Superseded.
