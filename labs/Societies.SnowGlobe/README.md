# Societies Snow Globe Lab

A standalone, headless research toy for proving agent-infrastructure choices before they enter the Godot runtime. It is intentionally isolated from `src/societies/`. The lab now contains a production-capable Ollama benchmark boundary, but it remains offline until a separately approved model download and run.

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

`SnowGlobeRunStore` creates local-only, single-writer `snow_globe_run_store/v3` artifacts. Strict bounded v2 reading and reconstruction remain supported, but v2 directories are never upgraded or opened for participant append. V3 adds the exact `snow_globe_participant_command/v1` identity and stores each admitted command plus receipt as one compound, checksum-bound `ParticipantEvaluation` record at a tick boundary. Accepted events still replay through the deterministic validator; stale and domain-rejected evaluations reconstruct the same bounded idempotent receipt without becoming world authority.

Reading is side-effect-free and fails closed on incomplete or unterminated JSONL, unsupported identity, checksum mismatch, duplicate/out-of-order sequence, misplaced participant records, incomplete agent schedules, unknown actions/reasons, ignored fields, or mismatched checkpoints. Scheduled ticks are framed in memory and batch-appended only after complete validation. Managed pre-append failures preserve the prior checkpoint; observed append/flush uncertainty poisons the live writer, and process-crash atomicity is not claimed.

`SnowGlobeReplayAdapter` consumes only normalized recorded responses and has no model/provider path. The deterministic world still validates every replayed proposal, so a response ledger cannot become state authority. Focused Release tests prove exact state and event digest equality for the eight-agent scripted control and an eight-agent conflicting-claim resilience fallback across uninterrupted execution, checkpoint/resume, and recorded-response replay; they also inject the listed corruption failures and verify read/reconstruction never rewrite original artifacts.

## Offline local-model preflight

`OllamaBenchmarkRunner` is the bounded single shared local-model-server boundary. Execution requires an explicit immutable single-use authorization capability bound to the exact plan, canonical loopback endpoint, installed artifact digest/size/format/family/quantization, and runtime process identity. It performs strict `/api/tags` and `/api/generate` validation, bounded FIFO admission, in-flight VRAM sampling, response-size/depth limits, and endpoint poisoning when cancellation-ignoring transport is observed. Proposals are parsed and validated only in a disposable scratch world; they never become simulation authority. Evidence is metrics-only.

`SnowGlobeLocalModelAdapterPreflight` remains the pure planning/evidence contract for the future shared local-model boundary. It accepts only canonical `http://127.0.0.1:<port>/` or `http://[::1]:<port>/` endpoints and rejects credentials, non-loopback hosts, redirects, retries, and execution authority. Request, output, queue, latency, context, and VRAM budgets are explicit.

Future 8 GB benchmark evidence must be bounded canonical metrics-only JSON bytes. Validation derives the exact sample count and SHA-256 plus latency percentiles/maximum, peak/total queue wait, byte peaks, VRAM, throughput, failure, and fallback counts. Raw prompts, responses, provider payloads, credentials, and secrets are forbidden. This contract performs no file/network/GPU/model operation and makes no live-quality or hardware-fit claim.

## Headless observer controls

`SnowGlobeObserverShell` supplies pause, resume, bounded step, inspect, and paused structured participant commands without adding a second authority. Commands carry canonical opaque participant/idempotency IDs plus expected tick/state/event identity and target an existing agent action; there is no participant text. The world atomically compares identity and validates the proposal. Admitted accepted, stale, and domain-rejected results are idempotent; busy, cancellation, unpaused, malformed, ownership-lost, and saturation responses are transient.

World collections are detached, ownership checks use a lock-protected mutation revision, and snapshots page at most 32 events without rehashing full history. `SnowGlobePersistedSession` owns the v3 store, world, identified adapter, pause state, and operation gate. It restores the durable world after managed failures, reconstructs receipts across reopen, rejects mutable v2 sessions, fail-closes on poison/coherence loss, and safely handles reentrant/concurrent disposal.

This is not a Godot UI and does not add authentication, networking, or free-form participant communication. Final local evidence is 296/296 Release tests, a lab Release build with 0 warnings and 0 errors, clean `git diff --check`, and independent FINAL CODE GO with no P0-P2 findings. No model weights were downloaded, no Ollama server was started, and no live model/provider/network inference occurred. No credentials, Godot gameplay, or `src/societies/` change occurred, and no live-quality claim is made. See [LOCAL_MODEL_RESEARCH_2026-08-16.md](LOCAL_MODEL_RESEARCH_2026-08-16.md) for the current model and benchmark strategy.
