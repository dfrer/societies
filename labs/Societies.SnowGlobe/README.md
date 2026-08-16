# Societies Snow Globe Lab

A standalone, headless research toy for proving agent-infrastructure choices before they enter the Godot runtime. It is intentionally isolated from `src/societies/` and has no provider, model weight, network, or credential dependency.

## Architecture

- `SnowGlobeWorld` owns persistent agent records, resources, stockpiles, structures, event history, validation, commit, and deterministic digests.
- `ISnowGlobeInferenceAdapter` receives an immutable `SnowGlobeObservation` and returns a proposed action. It cannot mutate or retain the world through the interface.
- `SequentialInferenceScheduler` is the baseline: it observes, awaits one proposal, validates, and commits in stable agent-ID order. It records queue/inference/proposal/accept/reject metrics.
- `ScriptedInferenceAdapter` is the offline baseline. The fixed seed run has eight agents gather wood and stone, build a shelter and storage asset, then maintain the shelter.
- The event log records committed actions only. Invalid proposals return a rejection result and do not alter resources, structures, agents, or the event log. Replaying the canonical event sequence produces the same state and event digests.

## Run locally

```powershell
dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release
dotnet build labs/Societies.SnowGlobe/Societies.SnowGlobe.csproj --configuration Release
```

## Initial experiment matrix

| Question | Fixed control | Comparison later | Metrics |
| --- | --- | --- | --- |
| Scheduling | sequential shared queue | bounded batched planning | state/event digest, rejected actions |
| Inference | scripted offline adapter | provider-neutral local adapter | calls, queue turns, latency recorded externally |
| Context | current structured observation | bounded memory summary | proposals, accepted actions, replay equality |
| World pressure | fixed seed / 8 agents | resource scarcity or larger cohorts | structures, maintenance, rejected actions |

The starting target is local-first hardware with roughly 8 GB VRAM: small local experiments, one request at a time, and no hidden model download. Any real provider integration remains a separately approved boundary.

## Resilience experiment

`SnowGlobeResilienceExperiment` is a bounded, offline fixture matrix for 8- and 16-agent shared snapshots. It covers inference timeout, malformed response, adapter crash, queue saturation, and conflicting resource claims. Frozen proposals always commit in ordinal agent-ID order; a failed primary receives exactly one deterministic repair, then an `Idle` fallback only if that repair is rejected. The canonical `snow_globe_resilience_experiment/v1` report records completion/progress, rejection/repair/fallback, dispatch fairness, queue/in-flight bounds, first divergence, and repeat/replay equivalence. It rejects incomplete or incoherent cells before serializing bytes.

## Later Godot observer-participant shell

A future Godot shell may render `SnowGlobeWorld` snapshots, expose human proposals through the same validated proposal contract, and display canonical events. It must not become a second state authority: the lab world remains the source of facts, validation, and replay.
