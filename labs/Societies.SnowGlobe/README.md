# Societies Snow Globe Lab

A standalone, headless research toy for proving agent-infrastructure choices before they enter the Godot runtime. It is intentionally isolated from `src/societies/`. The lab contains a production-capable Ollama benchmark boundary; one separately approved qwen3.5:4b pull and bounded local smoke have completed, while the first benchmark capability was consumed by a tags-only parse failure and no benchmark evidence exists.

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

## Offline two-tier cognition

`SnowGlobeTwoTierCognitionModule` places local and premium lanes behind the existing `ISnowGlobeIdentifiedInferenceAdapter`; the deterministic world remains the sole state authority. Its immutable policy binds an exact content-addressed premium model revision to a financial run/journal, reserves before submission, marks submission unknown before the offline fake provider, permits at most one premium attempt, retains an unknown reservation without resubmission, and uses explicit local fallback followed by deterministic `Idle`. Detached receipts capture policy, financial, requested-lane, model/revision, and primary-outcome evidence. The in-memory journal and fake are offline only. Existing v3 normalized recorded-response replay remains provider/financial-call-free and digest-equal.

This slice makes no HTTP, credential, provider/model/payment call, network, model download, production durability, live-quality, deployment, raw API resale, Stripe/billing/account, or run-store schema claim. The durable contract is documented in [CONTEXT.md](../../CONTEXT.md), [ADR 0001](../../docs/adr/0001-two-tier-cognition.md), and [TWO_TIER_COGNITION.md](TWO_TIER_COGNITION.md). Future work requires a transactional-database replacement for commercial durability, authenticated fixed-host adapters, credential and provider-policy review, commercial billing/legal controls, paid sandbox reconciliation, corpus benchmarking, and load/cost/abuse monitoring.

## Durable Financial Journal

`snow_globe_financial_journal/v1` is a separate BCL-only module with a small command/query interface and in-memory plus strict-file adapters. The file artifact is immutable and checksum-bound: it requires a bounded canonical UTF-8 header, mandatory checksummed LF JSONL, a live-writer lease, strict per-kind/sequence validation, and complete archive or explicit paging. Corrupt or torn input fails closed without repair; append uncertainty poisons the writer. The opaque BYOK identity is only `byok-account-sha256-<64 lowercase hex>` and contains no key, token, locator, email, raw account, or secret.

Admission/reservation is flushed before dispatch-unknown, and only then can the offline fake provider be called. Reopen may dispatch one reserved job once; Unknown never dispatches and retains its allowance. Idempotency/conflict, cap math, concurrency/reentrancy, completion tuples, reconciliation CAS/evidence/account binding, and immutable receipts are validated. Four records of premium-admission headroom and two records of persisted-denial headroom are protected; at absolute capacity the module returns deterministic cached `Idle` without provider/local calls and intentionally cannot create a durable idempotency key.

This is ordinary restart recovery for successfully flushed records under one host/one writer, not power-loss certification, multi-process/multi-host/account-wide transactional state, commercial accounting, cross-ledger atomicity, or exactly-once charging. It is a research journal, not a commercial database; SQLite or another transactional DB remains required before those claims. The existing proposal caller, deterministic world, and run-store v3 remain unchanged. See [ADR 0002](../../docs/adr/0002-durable-financial-journal.md).

## Offline local-model preflight

`OllamaBenchmarkRunner` is the bounded single shared local-model-server boundary. Execution requires an explicit immutable single-use authorization capability bound to the exact plan, canonical loopback endpoint, installed artifact digest/size/format/family/quantization, and runtime process identity. It performs strict `/api/tags` and `/api/generate` validation, bounded FIFO admission, in-flight VRAM sampling, response-size/depth limits, and endpoint poisoning when cancellation-ignoring transport is observed. Proposals are parsed and validated only in a disposable scratch world; they never become simulation authority. Evidence is metrics-only.

`Societies.SnowGlobe.BenchmarkCli` is the Windows-pinned process boundary for the frozen first compatibility cell. Its first authorized capability made exactly one successful `GET /api/tags`, then failed closed as `tags_json_invalid`; it made zero `/api/generate`, warmup, measured, retry, fallback, or evidence-write requests. Server/listener cleanup was green, but the canonical evidence JSON is absent and no benchmark or quality result exists. The contract now validates exact v0.32.14 tag fields (`capabilities`, `parent_model`, `context_length`, `embedding_length`), completion and `>=4096` context semantics, exact TCP tuple owner-PID binding with capability-PID comparison, aggregate `nvidia-smi` PID/start/path evidence, Windows directory-handle containment, bounded child cleanup, and the distinct exact `/api/tags` size `3,389,983,735` versus model layer/store size. CLI tests pass 28/28 and runner tests 76/76; the last independent full lab gate before the final CLI-only share change was 367/367, with 0-warning/0-error builds.

The consumed capability is not retryable. The next action is exactly one fresh explicit authorization for a new one-shot benchmark capability. The default PATH Ollama 0.18.2 remains untouched; the pinned E: runtime/model remains the only local target.

`SnowGlobeLocalModelAdapterPreflight` remains the pure planning/evidence contract for the future shared local-model boundary. It accepts only canonical `http://127.0.0.1:<port>/` or `http://[::1]:<port>/` endpoints and rejects credentials, non-loopback hosts, redirects, retries, and execution authority. Request, output, queue, latency, context, and VRAM budgets are explicit.

The offline credential/provider preflight adds three explicit terms to that boundary: a **Credential Lease** exclusively owns a mutable secret buffer transferred by trusted credential infrastructure and zeroes that lease-owned buffer; a **Fixed Provider Profile** is an immutable registry-owned endpoint/policy identity whose transport and retry settings are not caller-controlled; and a **Provider Execution Capability** is the single-use, policy-bound permission required before any future authenticated submission. The current fixture exercises acquisition, cancellation, exception, cleanup, and no-retry behavior without HTTP, DNS, sockets, credentials, payment, or a production provider profile. It cannot claim that arbitrary trusted callbacks cannot retain a copied secret.

The credential/preflight slice is validated by 6/6 focused tests and 348/348 full lab tests, with a Release build reporting 0 warnings and 0 errors. Independent deep review concluded CODE GO. This is offline evidence only: no production profile, authenticated HTTP adapter, live parser/status/charge evidence, live credential, or provider call exists.

Future 8 GB benchmark evidence must be bounded canonical metrics-only JSON bytes. Validation derives the exact sample count and SHA-256 plus latency percentiles/maximum, peak/total queue wait, byte peaks, VRAM, throughput, failure, and fallback counts. Raw prompts, responses, provider payloads, credentials, and secrets are forbidden. This contract performs no file/network/GPU/model operation and makes no live-quality or hardware-fit claim.

## Headless observer controls

`SnowGlobeObserverShell` supplies pause, resume, bounded step, inspect, and paused structured participant commands without adding a second authority. Commands carry canonical opaque participant/idempotency IDs plus expected tick/state/event identity and target an existing agent action; there is no participant text. The world atomically compares identity and validates the proposal. Admitted accepted, stale, and domain-rejected results are idempotent; busy, cancellation, unpaused, malformed, ownership-lost, and saturation responses are transient.

World collections are detached, ownership checks use a lock-protected mutation revision, and snapshots page at most 32 events without rehashing full history. `SnowGlobePersistedSession` owns the v3 store, world, identified adapter, pause state, and operation gate. It restores the durable world after managed failures, reconstructs receipts across reopen, rejects mutable v2 sessions, fail-closes on poison/coherence loss, and safely handles reentrant/concurrent disposal.

This is not a Godot UI and does not add authentication, networking, or free-form participant communication. The durable journal/observer evidence remains 342/342; the later credential/preflight slice adds 6/6 focused and 348/348 full lab tests, a Release build with 0 warnings and 0 errors, and independent deep-review CODE GO. The journal, two-tier slice, and provider preflight remain offline code contracts: no live credential, authenticated HTTP, live provider/parser/status/charge call, payment action, or live BYOK secret was used. The separate qwen3.5:4b smoke is recorded above and is not benchmark or quality evidence. No Godot gameplay or `src/societies/` change occurred, and no live-quality claim is made. See [LOCAL_MODEL_RESEARCH_2026-08-16.md](LOCAL_MODEL_RESEARCH_2026-08-16.md), [TWO_TIER_COGNITION.md](TWO_TIER_COGNITION.md), [ADR 0002](../../docs/adr/0002-durable-financial-journal.md), and [ADR 0003](../../docs/adr/0003-offline-provider-preflight.md).

## Repaired Ollama runtime preflight

An isolated portable Ollama v0.32.14 runtime was verified at `E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14` from the official Windows asset. The asset SHA-256 is `5AE5BCA5F0D297F5E35665E01DB399A69A8EAC3F8FAD89CD9D2531FD495C9457`; controlled startup discovered the RTX 2070 SUPER through CUDA compute 7.5 with 8 GiB total and 7 GiB available VRAM. The server was loopback-only at `127.0.0.1:11435` with cloud disabled, and the process was stopped after the preflight.

This repaired the isolated runtime/GPU-discovery path only. The default PATH installation remains Ollama 0.18.2 and was deliberately unchanged. One bounded qwen3.5:4b local smoke has now completed; it is not a benchmark or live-model quality claim. The production benchmark runner remains uninvoked.

The smoke used the official `qwen3.5:4b` artifact with manifest/full model digest `2A654D98E6FBA55D452B7043684E9B57A947E393BBFFA62485A7AAC05EE4EEFD`, 5 files / 3,389,984,444 bytes, family `qwen35`, 4,659,865,088 parameters (Q4_K_M). It was one local loopback call with no retry, `stream=false`, `think=false`, temperature 0, `num_ctx=4096`, and `num_predict=96`. Wall time was 51,056 ms; API total 50,958,399,400 ns; load 29,253,514,900 ns; prompt 82 tokens / 21,438,342,000 ns; output 20 tokens / 262,642,000 ns (~76.149 tok/s output metric). Structured output was 63 bytes with 0 thinking bytes, raw text was not retained, and its SHA-256 was `B9E223C20EA06E2D48FD96C151B095A8A7494527CD9D6AAD69B24F98FF97D4AD`. All 34/34 layers were GPU-offloaded; `/api/ps` size_vram was 3,128,038,521 bytes and loaded-state GPU use was 6,357/8,192 MiB. No outbound traffic occurred, the server stopped with no listeners, and the model remains on E:. This is smoke evidence only, not benchmark, intelligence, or quality evidence. The next action is the frozen benchmark contract using the pinned portable runtime.
