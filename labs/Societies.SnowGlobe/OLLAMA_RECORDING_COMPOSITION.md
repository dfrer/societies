# Offline Ollama recording composition contract

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. The prior dry-run-only state is historical: local code commit `be7c691` added the record-once surface; a later authorized invocation consumed one fresh authority, producing no valid artifact/evidence.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Historical v1 execution and artifact (superseded by v2)

Historical v1 `Execute` reserved and pinned a safe CreateNew target before inner `Authorize`/`RecordOnce`, performed exactly one attempt, and published/read back only at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty left an indeterminate tombstone; it never deleted or retried. The v1 Windows final read used the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

The historical v1 artifact schema was `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation bound the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. This v1 description is rollback history only; the active v2 path and identities are below.

## V2 CLI and evidence (v1 attempt records below are historical)

### V1 bounded attempt settlement (historical/superseded)

The v1 settlement from HEAD `af0925d` is historical: exactly one preflight exited 0, exactly one record-once emitted `RECORDING_FAILED` / `composition_execution_indeterminate`, exactly one validate exited 1, and exactly one HTTP 200 POST occurred. Fix `98b3ec5` is historical. Both v1 authorities are consumed and no valid v1 artifact/evidence exists.

### First bounded attempt (historical)

The first attempt used plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`: exactly one preflight exited 0, exactly one record-once exited 5 after 7,881 ms, exactly one validate exited 1 `artifact_size_invalid`, and exactly one HTTP 200 POST occurred in 7.0314715 s, with no retry/fallback/alternate/download. Its offline correction `959bea5` validation was focused correction+artifact 50/50, transport+session 52/52, with the earlier full results retained as historical evidence. Its contemporaneous `RuntimeChanged` inference is historical and superseded; it is not current cause evidence. The first 0-byte tombstone was later archived in the same directory as `qwen3.5-4b-recording-execution-v1.failed-20260819-001.empty`; no valid artifact/evidence exists.

### Offline v2 milestone (`a5a0823`)

Public composition operations and CLI arguments remain unchanged. The internal typed raw-free terminal checkpoint and policy coherence are shared by artifact and CLI. Exact active identities are plan `snow_globe_ollama_recording_composition_plan/v2`, receipt `snow_globe_ollama_loopback_recording_receipt/v2`, artifact `snow_globe_ollama_recording_execution_artifact/v2`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v2`. The fixed path is `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v2.json`; both v1 zero-byte tombstones remain untouched. Content-Type accepts `application/json` with no parameters OR exactly one `charset=utf-8` parameter; raw duplicate/malformed headers and non-canonical Content-Length fail closed. `HttpResponseRejected` and `TransportFailure`/`ResponseReceived`/status 200 are transport-emittable accepted outcomes; wrapper buffers are zeroed on all paths, unexpected prepublication exceptions produce one raw-free checkpoint artifact, and publication uncertainty remains a tombstone. Red evidence core0/3, CLI0/2, and parser-abuse/artifact-kind0/2 was corrected before final validation: v2 110/110, review 129/129, SnowGlobe 615/615, RecordingCli 49/49, builds 0/0, deep CODE GO. This is offline-supported compatibility, not live proof.

Official source evidence for that compatibility interpretation is [Ollama v0.32.14 routes.go](https://raw.githubusercontent.com/ollama/ollama/v0.32.14/server/routes.go) and the [Ollama Generate API](https://docs.ollama.com/api/generate): local non-stream JSON uses Gin `c.JSON`, cloud proxy behavior explicitly sets `application/json; charset=utf-8`, and `stream:false` is documented as `application/json`. These sources support the offline compatibility allowance only, not live execution proof.

## Design-It-Twice decision and next gate

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. Attempt-003 is complete with EVIDENCE GO and no accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim. The sole current next action is an offline Design-It-Twice decision and security tests for bounded transfer framing (accept exact chunked versus retain rejection); no fresh live attempt unless separately authorized after code/review.
