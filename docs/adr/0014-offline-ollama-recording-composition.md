# ADR 0014: Offline Ollama recording composition

- Status: Accepted for offline composition; one live record-once attempt consumed fresh authority and produced no valid artifact
- Date: 2026-08-19
- Code: `bd89187` (composition), `be7c691` (record-once command enablement), `959bea5` (offline correction)

## Decision

Use a fixed-cell deep Module with only zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. Accept only repository root, observed PID/start ticks, and nonce as public inputs. Bind canonical root and nonce digests; reserve a safe CreateNew target before inner authorization/recording; perform exactly one attempt; and publish a strict raw-free artifact with durable readback.

The artifact is `snow_globe_ollama_recording_execution_artifact/v1` at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`, capped at 128 KiB. Windows final reads use a no-follow pinned verified single-link handle. Writer uncertainty leaves an indeterminate tombstone without delete/retry. `RecordingCli` has preflight/validate plus a separately gated record-once command; the prior dry-run-only statement is historical.

## Design-It-Twice trade-offs

Rejected: flexible public cell/launcher/sink registry, because it widens authority and substitution. Rejected: benchmark identity/type reuse, because benchmark sequencing and evidence semantics differ. Chosen: fixed-cell Module and preflight/validate CLI with explicit live gate.

## Evidence and rollback

The second authorized attempt ran from HEAD `af0925d`: exactly one preflight invocation exited 0 with plan `ed35264777d6b6022708db092abd24e771d520b4d6b530937a72867d774decba`; exactly one record-once invocation emitted `RECORDING_FAILED` / `composition_execution_indeterminate` (exit-5 class; numeric exit lost by the outer null-output parser); exactly one validate invocation exited 1 `artifact_size_invalid`; exactly one `POST /api/generate` returned HTTP 200 in 41.625569 s; GET and other POST counts were zero; no retry/alternate/fallback/download/update. The prior tombstone was recoverably moved to `qwen3.5-4b-recording-execution-v1.failed-20260819-001.empty`; archive and new artifact are each 0-byte, non-reparse, single-link, SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. Retained cause is only strongest source-consistent `HttpResponseRejected`, unconfirmed because headers/checkpoint were not retained. Fix `98b3ec5` admits the exact null-wrapper/status range and closes adjacent rejection forms. Validation was owner 92/92, correction+artifact 50/50, transport+session 52/52, SnowGlobe 594/594, RecordingCli 47/47, builds 0/0, deep review FINAL CODE GO. Both authorities are consumed; no retry. The sole next action is a bounded offline diagnostic/observability decision before any separately authorized future attempt.
