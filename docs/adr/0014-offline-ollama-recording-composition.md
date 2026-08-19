# ADR 0014: Offline Ollama recording composition

- Status: Accepted for offline composition; live record-once separately gated
- Date: 2026-08-19
- Code: `bd89187`

## Decision

Use a fixed-cell deep Module with only zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. Accept only repository root, observed PID/start ticks, and nonce as public inputs. Bind canonical root and nonce digests; reserve a safe CreateNew target before inner authorization/recording; perform exactly one attempt; and publish a strict raw-free artifact with durable readback.

The artifact is `snow_globe_ollama_recording_execution_artifact/v1` at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`, capped at 128 KiB. Windows final reads use a no-follow pinned verified single-link handle. Writer uncertainty leaves an indeterminate tombstone without delete/retry. `RecordingCli` is dry-run/validate only and has no live command.

## Design-It-Twice trade-offs

Rejected: flexible public cell/launcher/sink registry, because it widens authority and substitution. Rejected: benchmark identity/type reuse, because benchmark sequencing and evidence semantics differ. Chosen: fixed-cell Module and preflight/validate CLI with explicit live gate.

## Evidence and rollback

Validation was focused 46/46, full SnowGlobe 575/575, RecordingCli 8/8, BenchmarkCli 56/56, lab/CLI builds 0 warnings/errors; independent deep review FINAL CODE GO, no P0-P2. No live Ollama/listener/socket/HTTP/process inspection/file hash/model/GPU/provider/credential/payment action occurred. Exact inventory is the 11 files in `bd89187`; rollback removes/reverts exactly those files and preserves prior fixture/session contracts. The only current next action is the separately security-reviewed `record-once` gate requiring exact preflight plan digest and explicit live-local acknowledgement, followed by the user’s fresh bounded authorization.
