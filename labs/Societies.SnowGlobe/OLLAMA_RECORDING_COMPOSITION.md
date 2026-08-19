# Offline Ollama recording composition contract

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. This is offline code proof; no live execution occurred.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Execution and artifact

Execute reserves and pins a safe CreateNew target before inner `Authorize`/`RecordOnce`, performs exactly one attempt, and publishes/readbacks `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty leaves an indeterminate tombstone; it never deletes or retries. Windows final read uses the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

Artifact schema is `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation binds the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. The artifact embeds raw-free receipt data and nested recording-evidence digests only. The legitimate completed-12 `Failed`/`EvidenceRejected` row is valid; impossible tuples are rejected. Claims remain limited: no independent artifact-loaded proof, model execution, quality, cost, world authority, production readiness, or commercial proof.

## CLI and evidence

`RecordingCli` exposes only preflight and validate. Preflight performs zero I/O; validate performs only a bounded local read. Record/live/execute commands fail closed before production construction, and no live command exists. Validation was focused 46/46, full SnowGlobe 575/575, RecordingCli 8/8, BenchmarkCli 56/56, lab/CLI builds 0 warnings/errors; independent deep review FINAL CODE GO with no P0-P2. Earlier transient benchmark theory was isolated 5/5 and final full green.

No live Ollama/listener/socket/HTTP/process inspection/file hash/model/GPU/provider/credential/payment action occurred. The actual Windows/store/live path remains uninvoked. Rollback is exact removal/revert of the 11 files in `bd89187`.

## Design-It-Twice decision and next gate

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. The next gate is a separately security-reviewed `record-once` command requiring exact preflight plan digest and explicit live-local acknowledgement, followed by the user’s 2026-08-19 fresh authorization for one bounded qwen3.5:4b attempt with no retry, alternate, or download.
