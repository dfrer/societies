# Offline Ollama recording composition contract

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. The prior dry-run-only state is historical: local code commit `be7c691` added the record-once surface; a later authorized invocation consumed one fresh authority, producing no valid artifact/evidence.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Execution and artifact

Execute reserves and pins a safe CreateNew target before inner `Authorize`/`RecordOnce`, performs exactly one attempt, and publishes/readbacks `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty leaves an indeterminate tombstone; it never deletes or retries. Windows final read uses the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

Artifact schema is `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation binds the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. The artifact embeds raw-free receipt data and nested recording-evidence digests only. The legitimate completed-12 `Failed`/`EvidenceRejected` row is valid; impossible tuples are rejected. Claims remain limited: no independent artifact-loaded proof, model execution, quality, cost, world authority, production readiness, or commercial proof.

## CLI and evidence

### Second bounded attempt settlement (supersedes the earlier attempt)

From HEAD `af0925d`, exactly one preflight exited 0 with plan `ed35264777d6b6022708db092abd24e771d520b4d6b530937a72867d774decba`; exactly one record-once emitted `RECORDING_FAILED` / `composition_execution_indeterminate`; exactly one validate exited 1 `artifact_size_invalid`; exactly one `POST /api/generate` returned HTTP 200 in 41.625569 s. Fix `98b3ec5` admits `HttpResponseRejected` / `ResponseReceived` / status 100..599 / null wrapper / mandatory terminal row and closes adjacent rejection forms. Both authorities are consumed, no valid artifact/evidence exists, and no retry is allowed. The sole next action is a bounded offline diagnostic/observability decision before any separately authorized future attempt.

### First bounded attempt (historical)

The first attempt used plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`: exactly one preflight exited 0, exactly one record-once exited 5 after 7,881 ms, exactly one validate exited 1 `artifact_size_invalid`, and exactly one HTTP 200 POST occurred in 7.0314715 s, with no retry/fallback/alternate/download. Its offline correction `959bea5` validation was focused correction+artifact 50/50, transport+session 52/52, with the earlier full results retained as historical evidence. Its contemporaneous `RuntimeChanged` inference is historical and superseded; it is not current cause evidence. The first 0-byte tombstone was later archived in the same directory as `qwen3.5-4b-recording-execution-v1.failed-20260819-001.empty`; no valid artifact/evidence exists.

## Design-It-Twice decision and next gate

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. No retry now. The sole next action is a bounded offline diagnostic/observability decision before any separately authorized future attempt.
