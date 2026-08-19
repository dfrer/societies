# Societies Snow Globe Lab

## Offline cognition-quality recording session

Commit `8256512` implements the provider-neutral `CognitionQualityRecordingSessionModule` with public instance operations `Authorize` and `RecordOnceAsync`. The sole public runtime Adapter is `OfflineFixedResponseCognitionQualityRecordingAdapter`. Exact process-local authorization binds publication/prompt-set/provenance/Adapter identity and contract digest, canonical nonce, capability lifetime, and session timeout; nonce tombstones are bounded/non-evicting at 1,024 and `OfflineFixedResponseCognitionQualityRecordingAdapter` capability tracking is capped at 4,096. A capability binds exact module and Adapter references and is consumed atomically by any call, including pre-cancel, expiry, wrong-module, or binding failure.

Exactly twelve slots execute sequentially, one attempt each, with no retry/fallback/alternate/thirteenth call. Responses are 1..1,024 bytes each and 12,288 aggregate with exact binding echoes. Evidence is emitted only after all twelve; partial/unknown/cancel/timeout/exception/binding/evidence-failure paths are raw-free and evidence-free. Disposed fixtures and prompt copies are zeroed while caller inputs are preserved. Results claim an offline fixture only, not delivery/model execution or another attempt. There is no network, provider, credential, payment, file, journal, world, Ollama, or premium authority. Process-local authorization is not restart-durable. See [the full recording-session contract](COGNITION_QUALITY_RECORDING_SESSION.md) and [ADR 0009](../../docs/adr/0009-offline-cognition-quality-recording-session.md).

Validation for the recording-session milestone was focused 17/17, full Snow Globe Release 433/433, Release build 0 warnings/errors, and independent deep review FINAL CODE GO after five corrections. That action is historical; the current conformance milestone is documented below.

## Offline cognition-quality recording Adapter conformance

Commit `8a5d339` adds the test-only `CognitionQualityRecordingAdapterConformanceHarness`, which exercises candidate fixtures through the real public recording-session Module. It binds the exact candidate identity SHA-256 digest, Adapter contract digest, expected evidence canonical digest, and ordered check/result list, then emits a bounded raw-free report. The eleven checks cover exact binding, evidence equivalence, sequential one-shot behavior, nonce and authority consumption, caller-input detachment, disposal closure, raw-free public surfaces, and optional midflight cancellation. Snapshot requests and retained fixture buffers are disposed and zeroed; caller inputs remain unchanged. There is no retry, fallback, alternate, or thirteenth call.

The `OfflineFixedResponseCognitionQualityRecordingAdapter` fixture is core-conformant but not fully conformant: it passes ten checks and reports `not_exercised_by_fixed_fixture` for midflight cancellation. The async test fixture exercises that optional seam and is fully conformant. This test-only evidence does not certify future provider Adapters, hidden retries, copied buffers outside the exercised path, or security. It performs no I/O, network, live/provider/model call, credential, payment, Ollama, journal, file, or world-authority action and does not change production live/provider authority. See [the conformance contract](COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md) and [ADR 0010](../../docs/adr/0010-offline-cognition-quality-recording-adapter-conformance.md).

Source hashes are harness `1DF7E16ABA14AEAEC7B7397A2561A5158180C462A3815DC56146037A177FB23F` and tests `D1CDBCA028781EE192A9697E0FA80FDC620300243A0B0DEEE83F77D5AE8E22FD`. Focused conformance validation passed 5/5, full Snow Globe Release passed 438/438, Release build passed with 0 warnings/errors, and independent deep review returned CODE GO. The sole current next action is to design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against this harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.

A standalone, headless research toy for proving agent-infrastructure choices before they enter the Godot runtime. It is intentionally isolated from `src/societies/`. The lab contains a production-capable Ollama benchmark boundary; the frozen qwen3.5:4b compatibility cell completed with canonical metrics evidence, while general intelligence, quality, and production readiness remain unproven.

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

`Societies.SnowGlobe.BenchmarkCli` completed the frozen qwen3.5:4b compatibility cell: tags 1/1 HTTP 200, warmup 1/1, measured 10/10, failures=0, fallbacks=0. Outer wall 15.709 s; p50 887.0709 ms; p95=p99=max 1,036.6006 ms; throughput 57.637718 tok/s. Queue bound/peak 1/1; total/peak wait 0.5206 ms; maximum request/output 801/873 B. Static VRAM 6,351 MiB; sampled peak 6,432/8,192 MiB below 6,963.2 MiB across 179 samples. TCP recorded 482 clean bounded samples with `bounded_samples_do_not_guarantee_unsampled_transient_exposure`; not proof between samples. `external_server_startup_configuration_verified=false` remains explicit. Canonical evidence is unchanged: `artifacts/snowglobe/local-model/qwen3.5-4b-frozen-benchmark-v1.json`, 3,618 B, SHA-256 `961B54B7D8CFB2AEAD566579499ADB3AA21F1D85BFBE0B7C6FC504A8ADC40E0D`. Offline validation accepted it with 0 errors; final evidence review is FINAL EVIDENCE GO with no P0-P2 findings. This proves local compatibility/fit/latency for this frozen cell only, not general intelligence, quality, or production readiness.

## Offline local-premium comparison

`LocalPremiumComparison.Evaluate(ReadOnlyMemory<byte>)` is the one-entry deep Module for comparing the completed local cell with a future premium lane. Its registry binds the exact local artifact SHA-256 `961B54B7D8CFB2AEAD566579499ADB3AA21F1D85BFBE0B7C6FC504A8ADC40E0D` and frozen plan/workload/prompt/schema/context/output/sample identities. The strict 16 KiB/depth-8 parser reuses `ValidateBenchmarkEvidence`; internal absent/offline fixture premium Adapters cannot count as live. Status is `insufficient_live_premium_evidence`; `premium`, `premium_cost`, and `performance_delta` are null. The canonical comparison report is 2,015 B, SHA-256 `845c429f3d1f90da13111affb2adf5480e6bbb72aa8a95e04de07730080dadce`; contract hash `5ca8f57d8dd4fb5de18a1179c1a8acf25eef944ac7350f30514f097932d95227`. No winner, quality/intelligence, or local-cost-zero claim is made, and no file/network/provider/credential/payment/model/journal Apply/world mutation occurred. Focused validation is 7/7, full lab validation 374/374, build 0 warnings/errors, independent CODE GO with no P0-P2 findings.

Historical next action, superseded: define a versioned audited cognition-quality corpus/score contract offline before any separately authorized live premium provider/credential/payment run. The corpus, execution-evidence envelope, and recorded-response runner are now complete.

## Offline cognition-quality recorded-response runner

Commit `c7926d3` adds `CognitionQualityRecordedResponseRunnerModule.Run`, one pure synchronous conversion over exactly 12 ordered, already-recorded response fixtures. Each fixture is bound to the frozen scenario ID and observation digest. Responses are limited to 1..1,024 raw response bytes each and 12,288 bytes total; invalid UTF-8 maps to `response_utf8_invalid`, and canonical output is capped at 96 KiB. Envelope corruption aborts the operation. Correctly bound malformed content becomes `no_proposal` with a closed parse outcome, while a representable wrong-agent or action-specific invalid proposal remains typed input for the existing scorer and deterministic feasibility authority.

The detached, raw-free artifact binds distinct runner, parser, and proposal-schema identities plus the caller-supplied prompt revision, caller-attested provenance, per-response byte counts/digests/outcomes, and nested execution evidence. Caller attestation is not execution attestation. The runner has no network, model, provider, credential, payment, journal, file, world, or live-call authority and is distinct from v3 normalized simulation replay. Validation passed 11/11 focused tests, 404/404 full Snow Globe Release tests, and a Release build with 0 warnings/errors; independent deep review returned GO. Preferred digests are payload `61cacfd4ad26512c1100a9235ee0ab534ec5945527a4da9252486ebf26675e43`, canonical run `f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd`, and nested execution evidence `2700886cef55abea3aba76f0789993cd6ad7283fa22d303dac5bbbd302e1ffe8`. See [the runner contract](COGNITION_QUALITY_RECORDED_RESPONSE_RUNNER.md) and [ADR 0006](../../docs/adr/0006-offline-cognition-quality-recorded-response-runner.md).

The prompt-envelope next action is complete in `bcba42a`; current truth follows in [the prompt-envelope contract](COGNITION_QUALITY_PROMPT_ENVELOPE.md) and [ADR 0007](../../docs/adr/0007-offline-cognition-quality-prompt-envelope.md).

## Offline cognition-quality prompt envelope

Commit `bcba42a` adds `CognitionQualityPromptEnvelopeBuilderModule.Create`, a pure synchronous publication of exactly 12 corpus-ordered compact UTF-8 prompts. The publication identity is `snow_globe_cognition_quality_prompt_envelope_publication/v1`, produced by builder `snow_globe_cognition_quality_prompt_envelope_builder/v1`, with prompt schema `snow_globe_cognition_quality_prompt/v1`. Every prompt embeds the canonical caller-supplied prompt revision. Each containing slot binds its scenario ID and observation digest.

Prompts include survival order, costs, rules, the observation, and strict response grammar. They exclude scenario/category, score, preferred answer, setup/state/event, model/provider, credential, and financial data. Each slot publishes prompt byte count/digest/base64 bytes with empty response fields. Bounds are 1..2,048 bytes per prompt, 24,576 aggregate, and 64 KiB publication. The publication itself retains no response bytes; `BindRecordedResponses` validates exact provenance revision/schema/count and returns detached fixtures for the existing runner.

The canonical publication binds corpus/scoring/validator plus runner/parser/proposal identities, prompt bytes/digests, prompt-set/payload/final digests, and claim limitations. Caller attestation is not prompt-transport or model-execution attestation. Validation passed 6/6 focused, 410/410 full Release, and a 0-warning/0-error Release build; independent deep review returned FINAL CODE GO. Preferred digests are payload `d879faa5af02e5b95108d7b9355a763acee1e120a1c68986c62c0e3b8907ce87`, canonical `966727433db3095e804148bba18e23da368d5fbbf58e7b0e2e58de349b47e9ae`, and prompt set `f9baf35ff43fbd4977d050488f0bb1ebfb37bb9b1fb98ddbd2fa83384e9bbcbb`.

The recording-evidence action is complete in `bf756ed`; current truth follows in [the recording-evidence contract](COGNITION_QUALITY_RECORDING_EVIDENCE.md) and [ADR 0008](../../docs/adr/0008-offline-cognition-quality-recording-evidence.md).

## Offline cognition-quality recording evidence

Commit `bf756ed` adds `CognitionQualityRecordingEvidenceModule.Create(publication, provenance, exact ordered responses)`, one pure synchronous in-process operation under `snow_globe_cognition_quality_recording_evidence/v1` and `offline_recording_evidence_binding_only`. It validates and embeds the exact prompt publication, caller-attested provenance, exact recorded-response run, and nested execution evidence, and adds the ordered response-set digest over scenario ID, observation digest, response byte count, and response digest. There is no Adapter or port at this seam.

Exactly 12 responses are required, each 1..1,024 bytes, with a 12,288-byte aggregate limit and a 192 KiB final canonical-artifact limit. All-or-error is in-memory only. The raw-free result retains no response bytes; Module-owned temporary snapshots and detached fixtures are cleared, while caller-owned inputs are never cleared. Correctly bound malformed content remains `no_proposal`; publication, provenance, envelope, or coherence corruption aborts.

Response association and identity are caller-attested. The envelope proves neither prompt delivery nor model execution, records no provider status/retry/charge evidence, and makes no quality, intelligence, winner, or cost claim. It grants no network, provider, credential, payment, journal, file, authoritative-world, or live-action authority; `src/societies/` remains unchanged. Goldens are response-set `0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c`, payload `069aa258c0a6870aa6d8c60f14aed800cbb46923564d3b62f36a41ba3159a7fd`, and final `61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4`. Focused new-plus-predecessor validation passed 30/30, full Snow Globe Release passed 416/416, and the Release build passed with 0 warnings/errors. Independent deep review returned FINAL CODE GO after four adversarial identity/digest fixes.

The previous recording-evidence action is historical/completed. The recording-session action is now complete in `8256512`; current truth and the sole next action are recorded above and in [the recording-session contract](COGNITION_QUALITY_RECORDING_SESSION.md).

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

This is a historical runtime-repair handoff. It records the earlier smoke and interrupted pre-comparison attempt; it is superseded by the completed frozen qwen3.5:4b compatibility evidence and offline local-premium comparison above. The default PATH installation remains Ollama 0.18.2 and was deliberately unchanged.

The historical smoke used the official `qwen3.5:4b` artifact with digest `2A654D98E6FBA55D452B7043684E9B57A947E393BBFFA62485A7AAC05EE4EEFD`; its detailed metrics remain historical artifact evidence. The completed frozen cell and comparison report above are authoritative for current local compatibility and comparison status.
