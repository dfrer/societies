# Runtime Stability and Validation Branch Plan

> Branch: `feature/runtime-observability-and-hardening`
> Base: master @ c58e3de

## Purpose

This branch adds the runtime observability, freeze-diagnosis, and validation foundation needed to measure and harden the authoritative simulation loop. It is NOT a feature-expansion pass.

## What Changed

### Core Files

| File | What | Why | Category |
|------|------|-----|----------|
| `RuntimeFrameMetrics.cs` (new) | Per-phase wall-clock timing accumulator with zero overhead when disabled | Need evidence, not guesses, about where time goes | Instrumentation |
| `GameManager.cs` | Instrumented `_Process`, `ProcessSimulationTick`, `SaveSnapshotToDisk`; added catch-up cap (`MaxTicksPerFrame=12`); added backlog watchdog; merged redundant HUD observers | Measure tick/frame health; prevent runaway stall from expensive first tick; eliminate N+1 HUD rebuilds | Instrumentation + Hardening |
| `PrototypeRuntimeSession.cs` | Added `SettlementSimulationDiagnostics` property + `SettlementDiagnosticsSnapshot` struct | Expose settlement-level diagnostics (work orders, path lookups, etc.) to external observers | Instrumentation |
| `SettlementSimulation.cs` | Added `Stopwatch` timing around `BuildWorkOrders` call | Measure the single most expensive simulation phase per tick | Instrumentation |
| `PrototypeRunArtifactManager.cs` | Added `PerfFrameTimingsCsvPath` | Parallel output path for perf CSV alongside simulation artifacts | Instrumentation |

### Tests

| File | What | Why |
|------|------|-----|
| `RuntimePerformanceCharacterization.cs` (new) | `Characterize_PerfAcrossWorkerSizes` — runs balanced_basin at 3/6/12 workers for 60–300 ticks with per-tick timing capture | Reproducible perf/stall validation |

### Docs

| File | Purpose |
|------|---------|
| `docs/AUTHORITATIVE_STATE_AND_GAP_AUDIT.md` | What this pass proved, evidence from perf runs, what remains uncertain |
| `docs/RUNTIME_STABILITY_AND_VALIDATION_BRANCH_PLAN.md` | This file — change inventory and rationale |
| `planning/meta/PLANNING_STATUS_MATRIX.md` | Maps planning docs against current implementation reality |

## How to Enable Metrics

```bash
# Enable per-frame timing collection
export SOCIETIES_PERF_METRICS=1

# Run the Godot prototype — metrics print a summary every 50 frames
# and export CSV + summary on F6 snapshot save

# Or run the .NET perf test (no Godot needed):
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj \
  --filter "Category=PerfStallCharacterization"
```

## Perf Output Location

CSV and JSON artifacts are written to:
- `test-output/perf-characterization/` (next to the test assembly)
- Or via `SOCIETIES_RUN_OUTPUT_DIR` env var in the Godot runtime

## Hardening Rationale

See `docs/AUTHORITATIVE_STATE_AND_GAP_AUDIT.md` Section "Phase 3".

## Next Pass Recommendation

See `docs/AUTHORITATIVE_STATE_AND_GAP_AUDIT.md` Section "Recommended Next Pass".
