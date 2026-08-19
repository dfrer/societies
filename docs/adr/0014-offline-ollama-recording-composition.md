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

The authorized attempt used one fresh authority: exactly one preflight invocation exited 0 with plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`; exactly one record-once invocation exited 5 after 7,881 ms; exactly one validate invocation exited 1 `artifact_size_invalid`; exactly one HTTP 200 POST in 7.0314715 s; no retry/fallback/alternate/download. The fixed artifact remains a preserved 0-byte tombstone (`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`), so no valid artifact/evidence exists. Post-response `RuntimeChanged` is the strongest source-consistent inference, unconfirmed because the checkpoint was not retained. Offline correction `959bea5` accepts the exact receipt matrix, with the terminal receipt row required for all three forms: before dispatch `DefinitelyNotSubmitted`/null status/null wrapper; after headers `ResponseReceived`/status 100..599/null wrapper; after exchange `ResponseReceived`/status 200/non-null wrapper. It removes the consumed-TCP-row requirement at AfterExchange; earlier PID/start/path/hash/listener and exact tuple checks remain. Validation after correction was focused correction+artifact 50/50, transport+session 52/52, SnowGlobe 588/588, RecordingCli 45/45, both Release builds 0 warnings/errors, deep CODE GO. Ignored tombstone/logs remain preserved outside this documentation boundary. Exact inventory is the 11 files in `bd89187`; rollback removes/reverts exactly those files and preserves prior fixture/session contracts.
