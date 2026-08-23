# Offline Ollama recording composition contract

## Current v4 score-summary handoff (Attempt-005 and comparison complete)

## Attempt-005 completed v4 evidence

The bounded in-memory local-premium comparison is complete/historical. Benchmark SHA `961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d`; v4 SHA `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`; report 8,916 B SHA `19f7053418471c8c70bdb9fffbfcca042f5bd87c24796a28227a672558990e56`, payload `3c3ff4a3e97344afb80d2a6283827e3d846c73e0b7730765c5d09601db6d4acc`. Status `insufficient_live_premium_evidence`; premium fields null; 12 scenarios and 262/1200. No report file/provider/network/live traffic/mutation. FINAL COMPARISON GO, no P0-P2.

Exactly one next action remains: offline Design-It-Twice for the first live-premium evidence/profile boundary and separately chosen provider/official endpoint/auth/schema/credential/redirect/status/retry/charge/cost policy plus explicit paid/provider authority. No provider or authority is selected or available.

Attempt-005 completed preflight/record-once/validate exactly once, all exit 0 in 141/26,420/175 ms; record `Complete`/`None`, 12/12, plan digest `6c70ed6d69c378eb1fcfbc744dacdb4af41085cb57eaac42b4ee45e1ebd333b4`. Artifact 16,148 B SHA `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`; receipt `86ccd0b468a1b633f386b0abbe90386695f994c40be47f24dcffd63867529d65`; summary `1958e8b6c4601c9a9e9834403cd431a67a48324735dca7484d10809509245a9a`; report `0e5fe1d7a8849caf7294c79c5c80863db0aa7521fb3b7e991b469867538a4fe1`. Score 262/1200, 12 HTTP 200 POSTs, no retry/alternate/fallback/download/update; deep review FINAL EVIDENCE GO; cleanup operator-observed only.

The bounded offline local-premium comparison is complete and historical. The sole current action is the offline live-premium boundary Design-It-Twice above.

Commit `9bb4027` adds the internal fixed raw-free score-summary codec atop v3 evidence commit `38c9bdb`. It embeds the canonical quality report once; prompts, responses, proposals, submissions, and model text are not retained. Exact scoring tuple checks include scenario-specific lower-utility reachability. Dispositions are bounded, the detached result is structure/integrity evidence only; for a complete result, only the score summary is populated, while terminal and `EvidenceRejected` are null. There is no historical backfill.

Terminal outcomes also include `Failed`, `Cancelled`, and `TimedOut`; only `Complete` populates the score summary/digest, while `EvidenceRejected` and other non-Complete terminal paths retain null summary/digest. The authoritative comparison fields are `premium`, `premium_cost`, `performance_delta`, and `quality_delta`, all null with status `insufficient_live_premium_evidence`.

The v4 schema identities are plan `snow_globe_ollama_recording_composition_plan/v4`, receipt `snow_globe_ollama_loopback_recording_receipt/v4`, and artifact `snow_globe_ollama_recording_execution_artifact/v4`; the retained artifact path is `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v4.json`. Transport remains v3 and the high-level adapter/profile remains v2. The receipt is canonical digest-only and the artifact embeds the summary once. The two-input `LocalPremiumComparison` overload emits local cognition facts only (`premium`, `premium_cost`, `performance_delta`, and `quality_delta` null; status `insufficient_live_premium_evidence`); the one-input golden is unchanged.

Validation progressed red `10/23/33`, then `6/92/98`, to final targeted `98/98`; owned `157/157`, full SnowGlobe `706/706`, Recording CLI `59/59`, Benchmark CLI `56/56`, and three Release builds passed with 0 warnings/errors. Independent deep review was FINAL CODE GO with no P0-P2 findings; snapshot `21c3f18942072aaa6954a6f95dd86528c33b94386d6263938b906565a66032e8`.

Attempt-001 through Attempt-004 chronology and v1-v3 artifacts remain preserved; older actions are historical/superseded. Attempt-005 and the bounded offline comparison are complete. The sole current action is the offline live-premium boundary Design-It-Twice above.

## Historical v3 live settlement (Attempt-004; completed/historical)

Clean HEAD `18f2dc622ce27f14dd9f5d4126176a944244ae8d` is FINAL EVIDENCE GO with no P0-P2 findings. Static preflight was accepted after correctly scoping the GPU gate to non-Ollama WDDM apps. Server `server1`; CLI preflight 1 succeeded with plan `13788130a3573ba8205cf833495e877ca26fb0daecab421bcab27880d4cb4e31`; record 1 succeeded in 28,609 ms; validate 1 succeeded in 148 ms. Exactly 12 ordered `POST /api/generate` requests returned 200 in 20.2868581 s. No retry/fallback/alternate/pull/update/cloud/credential/payment action occurred.

Exact identities remain plan `snow_globe_ollama_recording_composition_plan/v3`, receipt `snow_globe_ollama_loopback_recording_receipt/v3`, artifact `snow_globe_ollama_recording_execution_artifact/v3`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v3`; fixed path `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v3.json`. Artifact is 9,621 B, SHA/canonical `12c3e0f9b8fe13f8eaf2525642e130e4298e18c37f2fff58c2a316d2292f7b67`, payload `da3797cfe041ec949083eaf6e5ec9fecd22df4564ff767b176d94e2da10a50a1`; receipt is 6,169 B, SHA `e1a43bc8b7c44dfde6d71e372f1c6237239efe4ffd60716b869be72bf9dcb6b1`, payload `4d3541fff307a3f7dcd5aea1958c51ba0cc49f7b62df01ee07b086868dfb97fc`; nested digest `cd846a45a85085d1943ce8eb0c8b10ad489a802c1727f58d9d1ca04328e594e7`.

All 12 slots completed with `ResponseReceived`/200, `NotApplicable`, checkpoint/policy `None`, zero counters, and `additional=false`; validator accepted. CUDA RTX2070S was 34/34. Operator cleanup was zero but not independently retained or re-observed; raw-free/cloud/body-log were false. Limits are HttpClient-exposed framing only, no raw-wire proof, no embedded/revalidated nested scoring digest, no proof against overwritten captures, HEAD as provenance only, and cleanup as operator observation. No quality/intelligence/winner/cost/commercial/general-compatibility/world-authority claim. The v3 action and its score-summary next action are complete and historical.

Official references: [Ollama Generate API](https://docs.ollama.com/api/generate), [HttpHeaders.NonValidated](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.nonvalidated), [HttpCompletionOption.ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption), and [HttpResponseMessage.TrailingHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage.trailingheaders). These sources describe public API behavior; they do not turn this offline evidence into raw-wire or live compatibility proof.

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. The prior dry-run-only state is historical: local code commit `be7c691` added the record-once surface; a later authorized invocation consumed one fresh authority, producing no valid artifact/evidence.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Historical v1 execution and artifact (superseded by v2)

Historical v1 `Execute` reserved and pinned a safe CreateNew target before inner `Authorize`/`RecordOnce`, performed exactly one attempt, and published/read back only at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty left an indeterminate tombstone; it never deleted or retried. The v1 Windows final read used the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

The historical v1 artifact schema was `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation bound the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. This v1 description is rollback history only; v2 and v3 are historical/superseded above.

## V2 CLI and evidence (v1 attempt records below are historical)

### V1 bounded attempt settlement (historical/superseded)

The v1 settlement from HEAD `af0925d` is historical: exactly one preflight exited 0, exactly one record-once emitted `RECORDING_FAILED` / `composition_execution_indeterminate`, exactly one validate exited 1, and exactly one HTTP 200 POST occurred. Fix `98b3ec5` is historical. Both v1 authorities are consumed and no valid v1 artifact/evidence exists.

### First bounded attempt (historical)

The first attempt used plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`: exactly one preflight exited 0, exactly one record-once exited 5 after 7,881 ms, exactly one validate exited 1 `artifact_size_invalid`, and exactly one HTTP 200 POST occurred in 7.0314715 s, with no retry/fallback/alternate/download. Its offline correction `959bea5` validation was focused correction+artifact 50/50, transport+session 52/52, with the earlier full results retained as historical evidence. Its contemporaneous `RuntimeChanged` inference is historical and superseded; it is not current cause evidence. The first 0-byte tombstone was later archived in the same directory as `qwen3.5-4b-recording-execution-v1.failed-20260819-001.empty`; no valid artifact/evidence exists.

### Offline v2 milestone (`a5a0823`)

Public composition operations and CLI arguments remain unchanged. The internal typed raw-free terminal checkpoint and policy coherence are shared by artifact and CLI. Exact v2 identities and path remain preserved historical evidence; v2 was offline-supported compatibility, not live proof, and is superseded by the v3 live settlement above.

Official source evidence for that compatibility interpretation is [Ollama v0.32.14 routes.go](https://raw.githubusercontent.com/ollama/ollama/v0.32.14/server/routes.go) and the [Ollama Generate API](https://docs.ollama.com/api/generate): local non-stream JSON uses Gin `c.JSON`, cloud proxy behavior explicitly sets `application/json; charset=utf-8`, and `stream:false` is documented as `application/json`. These sources support the offline compatibility allowance only, not live execution proof.

## Historical Design-It-Twice decision

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. Attempt-003 is historical EVIDENCE GO with no accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim. The offline framing decision and v3 live settlement above are historical; v4 is authoritative for the current action.
