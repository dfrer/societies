# ADR 0014: Offline Ollama recording composition

- Status: Accepted for offline v2 composition; v1 attempts remain historical and consumed
- Date: 2026-08-19
- Code: `bd89187` (composition), `be7c691` (record-once command enablement), `959bea5` (v1 correction), `98b3ec5` (v1 coherence fix), `a5a0823` (v2 checkpoint/coherence and artifacts)

## Decision (v2 active; v1 historical)

The active decision is v2 from `a5a0823`: use the unchanged public operations and CLI arguments with typed raw-free checkpoint/policy coherence. Exact identities are plan `snow_globe_ollama_recording_composition_plan/v2`, receipt `snow_globe_ollama_loopback_recording_receipt/v2`, artifact `snow_globe_ollama_recording_execution_artifact/v2`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v2`; the fixed path is `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v2.json`. The v1 path/schema and both zero-byte tombstones remain historical and untouched. Content-Type accepts `application/json` with no parameters or exactly one `charset=utf-8` parameter; malformed/duplicate raw headers and non-canonical Content-Length fail closed. This is offline-supported compatibility, not live proof.

Use a fixed-cell deep Module with only zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. Accept only repository root, observed PID/start ticks, and nonce as public inputs. Bind canonical root and nonce digests; reserve a safe CreateNew target before inner authorization/recording; perform exactly one attempt; and publish a strict raw-free artifact with durable readback.

The v1 artifact schema/path and writer rules in the historical composition implementation are retained for rollback context only. The active v2 artifact is `snow_globe_ollama_recording_execution_artifact/v2` at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v2.json`, capped at 128 KiB. Windows final reads use a no-follow pinned verified single-link handle. Writer uncertainty leaves an indeterminate tombstone without delete/retry. `RecordingCli` has preflight/validate plus a separately gated record-once command.

## Design-It-Twice trade-offs

Rejected: flexible public cell/launcher/sink registry, because it widens authority and substitution. Rejected: benchmark identity/type reuse, because benchmark sequencing and evidence semantics differ. Chosen: fixed-cell Module and preflight/validate CLI with explicit live gate.

## Attempt-003 v2 evidence (current)

Commit `6e47510` produced EVIDENCE GO: preflight plan `40cad2ba2c4ae568d7db8968b4547dff3b96da46c7386377fc447fa833ee82c5`, record exit 3 after 8,621 ms with typed `Failed` / `HttpResponseRejected` / `ResponseReceived` / 200 / `NotApplicable` / `TransferEncoding`, validate exit 0 structurally complete, and exactly one HTTP200 POST in 7.8811713 s. Artifact v2 is 5,822 B, canonical SHA-256 `4e358d3dc7bb578debaad8edb6578984c6f7f9ac8ec558013e2ef8ae59c00038`. No accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim is made. Current action is offline transfer-framing decision/security tests; no fresh live attempt absent separate authorization.

## Evidence and rollback

The v1 second-attempt evidence is historical/superseded: it ran from HEAD `af0925d`, consumed its authority, and produced no valid v1 artifact/evidence. Attempt-003 is the current completed evidence; the sole current action is offline transfer-framing decision/security tests, with no fresh live attempt absent separate authorization.
