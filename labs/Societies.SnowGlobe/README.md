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
| Scheduling | sequential shared queue | controlled parallel comparison | state/event digest, rejected actions |
| Inference | scripted offline adapter | offline loopback preflight only | calls, queue turns, bounded benchmark evidence |
| Context | current structured observation | bounded memory summary | proposals, accepted actions, replay equality |
| World pressure | fixed seed / 8 agents | resource scarcity or larger cohorts | structures, maintenance, rejected actions |

The starting target is local-first hardware with roughly 8 GB VRAM: small local experiments, one request at a time, and no hidden model download. Any real provider integration remains a separately approved boundary.

## Resilience experiment

`SnowGlobeResilienceExperiment` is a bounded, offline fixture matrix for 8- and 16-agent shared snapshots. It covers inference timeout, malformed response, adapter crash, queue saturation, and conflicting resource claims. Frozen proposals always commit in ordinal agent-ID order; a failed primary receives exactly one deterministic repair, then an `Idle` fallback only if that repair is rejected. The canonical `snow_globe_resilience_experiment/v1` report records completion/progress, rejection/repair/fallback, dispatch fairness, queue/in-flight bounds, first divergence, and repeat/replay equivalence. It rejects incomplete or incoherent cells before serializing bytes.

## Run-store interruption and replay experiment

`SnowGlobeRunStore` is a local-only, single-writer `snow_globe_run_store/v2` artifact: a small `run.json` identity header and append-only `ledger.jsonl`. V2 intentionally rejects v1 rather than migrating it because every canonical record checksum is now bound to the strict header. The supported schema/rules/prompt tuple is exact, and response-ledger replay requires the exact expected adapter identity. Each ordinal turn records a response, proposal, validation commit, and accepted event; every complete tick records a checkpoint with state and event digests. The identity includes seed and the exact ordered agent schedule. Prompts, participant text, credentials, provider payloads, timestamps, and host data are excluded.

Reading is side-effect-free and fails closed on incomplete or unterminated JSONL, unsupported identity, checksum mismatch, duplicate/out-of-order sequence, incomplete/reordered agents, unknown or noncanonical actions, divergent ledger pairs, and mismatched checkpoints. Header, ledger, record, depth, and field bounds are enforced before large allocation. A whole-operation instance lease and a stable exclusive handle lease prevent interleaved writers while allowing stale lock-file recovery. Capacity is reserved for a whole tick, and world seed, agent schedule, tick, state digest, and event digest must match the latest stored checkpoint before any append.

`SnowGlobeReplayAdapter` consumes only normalized recorded responses and has no model/provider path. The deterministic world still validates every replayed proposal, so a response ledger cannot become state authority. Focused Release tests prove exact state and event digest equality for the eight-agent scripted control and an eight-agent conflicting-claim resilience fallback across uninterrupted execution, checkpoint/resume, and recorded-response replay; they also inject the listed corruption failures and verify read/reconstruction never rewrite original artifacts.

## Offline local-model preflight

`SnowGlobeLocalModelAdapterPreflight` defines—but does not execute—the future single shared local-model-server boundary. It accepts only canonical `http://127.0.0.1:<port>/` or `http://[::1]:<port>/` endpoints and rejects credentials, non-loopback hosts, redirects, retries, and execution authority. Request, output, queue, latency, context, and VRAM budgets are explicit.

Future 8 GB benchmark evidence must be bounded canonical metrics-only JSON bytes. Validation derives the exact sample count and SHA-256 plus latency percentiles/maximum, peak/total queue wait, byte peaks, VRAM, throughput, failure, and fallback counts. Raw prompts, responses, provider payloads, credentials, and secrets are forbidden. This contract performs no file/network/GPU/model operation and makes no live-quality or hardware-fit claim.

## Headless observer controls

`SnowGlobeObserverShell` supplies pause, resume, bounded step, and inspect controls without adding a second authority. A candidate world completes the existing scheduler turn first; only a verified successful delta replays through the live world's validator. First/middle/last adapter failures do not mutate the live tick. The shell requires exclusive world-mutation ownership, verifies post-commit tick/count/state/event identity, permanently fails closed on external mutation, and exposes 32-event cursor pages with the cached full-history digest.

This is not a Godot UI and does not yet expose participant commands. Final local evidence is 141/141 Release tests, a Release build with 0 warnings and 0 errors, clean `git diff --check`, and independent deep review with CODE GO/no findings. No provider, model, credential, download, network, GPU probe, Godot runtime, or `src/societies/` change occurred.
