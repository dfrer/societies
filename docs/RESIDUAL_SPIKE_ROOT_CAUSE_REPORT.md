# Residual Spike Root-Cause Report

> Branch: `feature/runtime-observability-and-hardening`
> Date: 2026-04-07
> Engine: Godot 4.6.stable.mono.official
> Scenario: balanced_basin, 16 citizens, 300 ticks
> Runner: PerfRunner.tscn (Godot headless)

## Executive Summary

**The navigation-invalidation hypothesis was DISPROVEN.** The residual ~2.3s spike was NOT caused by `InvalidateNavigation()` clearing the path cache when a path segment build completed. In 300 ticks with 16 citizens, ZERO navigation invalidations occurred and ZERO path segments completed building.

The actual root cause was **incomplete warmup coverage**: the central depot position was not included as a start position in the path cache warmup. The depot is the primary logistics hub and is used as a start position for many path computations on tick 1. Adding depot-as-start to the warmup eliminated the spike entirely.

## 1. Hypothesis vs Evidence

| Hypothesis | Evidence | Verdict |
|------------|----------|---------|
| Invalidation clears cache when path_segment completes | nav_invalidated=False for ALL 300 ticks | **DISPROVEN** |
| Path segments complete around tick 6-12 | path_segment_completed=False for ALL 300 ticks | **DISPROVEN** |
| Invalidation causes 2.6s spike | No invalidation occurred, yet 2.3s spike existed on tick 1 | **DISPROVEN** |
| Spike is from cold A* on tick 1 | 64 cold misses on tick 1, all from depot position (132,123) | **PROVEN** |
| Depot-as-start paths missing from warmup | Adding depot->spans/caches/structures eliminated all 64 misses | **PROVEN** |

## 2. Per-Tick Evidence (from tick-diagnostics.csv)

### Tick 1 (spike, before fix)
- tick_wall_ms: 2250.88ms
- nav_invalidated: **False**
- path_segment_completed: **False**
- cache_size_after: 9396
- path_lookups: 1351, path_hits: 1287, misses: **64**
- All 64 misses from position (132,123) = central depot

### Ticks 2-300 (steady state)
- Mean tick_wall_ms: 13.3ms (excluding tick 1)
- Max tick_wall_ms: 46.4ms (tick 136, no invalidation)
- P95: 19.4ms, P99: 41.2ms
- path_lookups/hits ratio: ~99%+ hit rate

### Key observation
- ZERO navigation invalidations in 300 ticks
- ZERO path segment completions in 300 ticks
- Only 1 structure completed in all 300 ticks (tick 168, NOT a path_segment)
- Path cache grows slowly from 9396 to 9408 as citizens explore new positions

## 3. Fix Applied

**What**: Extended `WarmPathCache()` to compute paths FROM the central depot position TO all resource spawns, caches, and structures. Also added cache<->depot reverse paths.

**Why**: The 64 cold A* misses on tick 1 were ALL from the depot position (132,123) to various destinations. The depot is the primary logistics hub for hauling, and path lookups use the depot position as the start point for many routing decisions.

**Code change**: SettlementInfrastructure.cs `WarmPathCache()` added:
- `_centralDepot.Position -> each resource spawn`
- `_centralDepot.Position -> each resource cache`
- `_centralDepot.Position -> each structure`

**Impact** (Godot 4.6 headless, balanced_basin, 16 citizens, 300 ticks):

| Metric | Before depot warm | After depot warm | Baseline (no warmup) |
|--------|-------------------|------------------|---------------------|
| Peak tick | 2250ms | **44-61ms** | 18140ms |
| Total 300 ticks | 6820ms | **4442-5191ms** | 38100ms |
| Tick-1 cold misses | 64 | **0** | ~9400 |
| Avg steady-state | 13.3ms | 13.3ms | 25.3ms |

## 4. Instrumentation Added

### Per-tick diagnostics CSV (tick-diagnostics.csv)
- tick: tick number
- tick_wall_ms: wall-clock duration of the tick
- nav_invalidated: whether InvalidateNavigation() was called
- path_segment_completed: whether a path_segment structure completed
- structures_completed: count of structures completed this tick
- cache_size_before_invalid: path cache size before invalidation
- cache_size_after: path cache size at end of tick
- nav_rebuild_ms: duration of navigation grid rebuild (if invalidation occurred)
- path_lookups: total FindPathPlan calls
- path_hits: cache hits
- orders_gen: work orders generated
- citizen_count: number of citizens

### Export commands
```csharp
// From GameManager:
manager.ExportTickDiagnostics("/path/to/tick-diagnostics.csv");
```

## 5. Navigation Invalidation Risk Assessment

While invalidation did NOT occur in 300 ticks of the balanced_basin scenario, it remains a latent risk:

1. **Path segments CAN complete** -- they just didn't in 300 ticks because construction requires materials, builders, and 6 ticks of build time. In longer runs (>500 ticks) or different scenarios, path segments will complete and trigger `InvalidateNavigation()`.

2. **When invalidation occurs**: `_pathCache.Clear()` wipes ALL cached paths, and `_navigationRulesVersion++` makes ALL subsequent lookups miss. This would cause a spike similar to the original tick-0 cold start.

3. **Mitigation for future**: Consider lazy/delta invalidation that only invalidates paths crossing the newly-built segment cells, preserving all other cached paths.

## 6. Remaining Risks

1. **Navigation invalidation spike at longer runtimes**: Not observed in 300 ticks, but WILL occur when path segments complete. The spike will be proportional to the number of active path lookups after invalidation (~9400 cached entries would need recomputation).

2. **Scaling at higher worker counts**: Only tested at 16 citizens. Higher counts (24+) may expose different bottlenecks.

3. **Warmup cost scales with map size**: More resource spawns = more warmup paths. At very large maps, warmup could take >5s.

## 7. Recommended Next Pass

1. **Validate invalidation hypothesis at longer runtimes**: Run 1000+ ticks to see if path segments complete and trigger invalidation spikes.
2. **If invalidation spikes are observed**: Implement lazy/delta invalidation (only clear paths crossing affected cells).
3. **Test at 24+ citizens**: Validate warmup scales linearly.
4. **Consider warmup as background task**: For large maps, warmup could be progressive during scene loading.
