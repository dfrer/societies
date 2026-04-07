# Runtime State and Gap Audit

> Authoritative as of: $(date)
> Branch: `feature/runtime-observability-and-hardening`
> Base: master @ c58e3de (PR #96 merge)

## What This Pass Proved

### Phase 1: Baseline Observability (DONE)

Added `RuntimeFrameMetrics` — a lightweight, zero-overhead (when disabled) phase-level timing accumulator.

- `src/societies/scripts/core/RuntimeFrameMetrics.cs` — new file, 295 lines
  - Per-frame phase timers (frame total, session advance, BuildWorkOrders, scene sync, HUD, save artifact, harvest apply)
  - Per-tick duration ring buffer (last 128 ticks)
  - Running peaks (max tick ms, max frame ms, max ticks-per-frame)
  - CSV export + session summary
  - Toggle via `SOCIETIES_PERF_METRICS=1` env var

### Phase 2: Reproducible Perf Validation (DONE)

Added `RuntimePerformanceCharacterization.cs` test that runs the settlement simulation through a worker-count matrix and outputs:
- `perf-matrix.csv` — per-run summary
- `perf-per-tick-*.csv` — per-tick timing detail
- `perf-results.json` — full results
- `perf-hotspots.md` — annotated analysis

**Evidence from 4 runs (balanced_basin, 3/6/12 workers, 60–300 ticks):**

| Workers | Ticks | Avg Tick | P95 Tick | Max Tick | Peak Orders | >50ms ticks | Hit Rate |
|---------|-------|----------|----------|----------|-------------|-------------|----------|
| 3       | 60    | 242ms    | 73ms     | 10892ms  | 50          | 58          | 98.1%    |
| 6       | 60    | 292ms    | 82ms     | 14247ms  | 50          | 40          | 97.6%    |
| 6       | 300   | 98ms     | 100ms    | 12479ms  | 50          | 140         | 99.3%    |
| 12      | 60    | 400ms    | 470ms    | 16086ms  | 60          | 41          | 96.4%    |

**Key findings:**
- **Tick 0 is pathological** (10–19s): First-tick warm-up computes all paths from scratch (226–439 cache misses). This is the single biggest freeze risk.
- **After tick 0**: Normal ticks are 40–80ms for 3–6 workers; 12 workers show 400–1600ms spikes when structure changes trigger cache invalidation.
- **Path cache**: 96–99% hit rate after first tick. The cache works — the problem is initial population and invalidation.
- **All runs** had >40 ticks above the 50ms frame budget threshold.
- **12-worker run** was too heavy to complete the full 300-tick characterization within timeout.

### Phase 3: Evidence-Backed Hardening (DONE)

#### H1: Catch-up loop cap (GameManager._Process)
- **What**: Added `MaxTicksPerFrame = 12` ceiling to the fixed-tick catch-up while loop.
- **Evidence**: Tick 0 takes 10–19s without instrumentation. Without a cap, the accumulated `_tickAccumulator` would force the catch-up loop to try to process 200–400+ ticks in a single frame, cascading into a full runtime stall.
- **Tradeoff**: Simulation will lag wall-clock during heavy ticks (ticks will be processed gradually over multiple frames instead of catching up instantly). This is correct behavior — a frozen game rendering frames is better than a frozen game not rendering anything.
- **Classification**: Durable improvement. This is a standard real-time simulation guard.
- **Watcher**: Added `[PERF WARNING]` log when backlog exceeds 2× tick interval.

#### H2: HUD rebuild throttling (OnInventoryChanged/OnStockpileChanged)
- **What**: Merged `OnInventoryChanged()` and `OnStockpileChanged()` into a single `OnRuntimeStateChanged()` no-op observer. HUD is already rebuilt once per frame by `_Process()`.
- **Evidence**: Every inventory mutation (and there are many per tick — harvesting, depositing, consuming) fires `Changed`, which called `UpdateHud()`. This caused N+1 HUD rebuilds per tick on top of the one in `_Process()`.
- **Tradeoff**: Zero behavioral change — HUD text is still refreshed every frame. Only removes redundant mid-frame rebuilds.
- **Classification**: Stopgap. If future work adds event-driven HUD subsystems, observers may need real handlers again.

### Phase 4: Validation Architecture (DONE)

- Added `RuntimePerformanceCharacterization.cs` as a first-class perf test in the core test project.
- Marked with `Category=PerfStallCharacterization` so it runs alongside extended tests, not the PR gate.
- Outputs machine-readable CSV + JSON artifacts suitable for CI artifact collection.

### What Remains Uncertain

1. **First-tick cold start (10–19s)**: Still the single biggest freeze risk. The path cache is empty, so every worker-source-destination combination computes a raw A* path. Potential fix: pre-warm the path cache during scene initialization, or limit first-tick work order generation.
2. **12-worker at 300+ ticks**: Timed out in .NET test harness. The 60-tick run showed avg 400ms/tick with 1600ms spikes. Real Godot headless runs may differ.
3. **HUD/presentation cost in live Godot**: Our perf tests measure pure simulation; scene sync + HUD costs require the live Godot loop. The instrumentation is in place to measure this at runtime.
4. **Save artifact cost**: Not measured yet — only triggered by F6/manual save.

### Recommended Next Pass

1. **Pre-warm path cache**: During scene load, pre-compute paths for all initial citizen-home-resource combinations. This would eliminate the 10–19s first-tick freeze.
2. **Run headless soak with metrics**: Use `Godot --headless` + `SOCIETIES_PERF_METRICS=1` to capture real Godot loop timings (HUD, scene sync, presentation).
3. **Investigate path cache invalidation cost**: 12-worker runs show 400–1600ms spikes tied to `InvalidateNavigation()` clearing the cache and rebuilding the grid. Consider lazy invalidation or grid delta updates.
