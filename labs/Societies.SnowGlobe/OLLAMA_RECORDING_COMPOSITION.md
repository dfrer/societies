# Offline Ollama recording composition contract

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. The prior dry-run-only state is historical: local code commit `be7c691` added the record-once surface; a later authorized invocation consumed one fresh authority, producing no valid artifact/evidence.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Execution and artifact

Execute reserves and pins a safe CreateNew target before inner `Authorize`/`RecordOnce`, performs exactly one attempt, and publishes/readbacks `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty leaves an indeterminate tombstone; it never deletes or retries. Windows final read uses the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

Artifact schema is `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation binds the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. The artifact embeds raw-free receipt data and nested recording-evidence digests only. The legitimate completed-12 `Failed`/`EvidenceRejected` row is valid; impossible tuples are rejected. Claims remain limited: no independent artifact-loaded proof, model execution, quality, cost, world authority, production readiness, or commercial proof.

## CLI and evidence

`RecordingCli` exposes preflight, validate, and separately gated record-once. A later authorized invocation used plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`: exactly one preflight invocation exited 0; exactly one record-once invocation exited 5 after 7,881 ms; exactly one validate invocation exited 1 `artifact_size_invalid`; exactly one HTTP 200 POST occurred in 7.0314715 s, with no retry/fallback/alternate/download. RuntimeChanged receipt forms are exact, and all require the terminal receipt row: before dispatch `DefinitelyNotSubmitted`/null status/null wrapper; after headers `ResponseReceived`/status 100..599/null wrapper; after exchange `ResponseReceived`/status 200/non-null wrapper. Validation after offline correction `959bea5` was focused correction+artifact 50/50, transport+session 52/52, SnowGlobe 588/588, RecordingCli 45/45, both Release builds 0 warnings/errors, deep CODE GO. Earlier transient benchmark theory was isolated 5/5 and final full green.

No valid artifact/evidence exists: the fixed artifact is a preserved 0-byte tombstone (`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`). Cleanup found no Ollama/llama-server, 11434/11435 rows, or attributable GPU process. Post-response `RuntimeChanged` is the strongest source-consistent inference, unconfirmed because the checkpoint was not retained. Offline correction `959bea5` accepts the exact RuntimeChanged receipt matrix and removes the consumed-TCP-row requirement at AfterExchange; earlier PID/start/path/hash/listener and exact tuple checks remain. Ignored tombstone/logs are preserved outside this documentation slice. Rollback is exact removal/revert of the 11 files in `bd89187`.

## Design-It-Twice decision and next gate

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. The next gate is no retry now: a separately reviewed decision on tombstone disposition, followed by fresh one-shot authority only if the user later wants a new live attempt.
