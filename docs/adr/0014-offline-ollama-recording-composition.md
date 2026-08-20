# ADR 0014: Offline Ollama recording composition

- Status: Accepted for offline v3 bounded chunked composition; v1/v2 evidence remains historical
- Date: 2026-08-19
- Code: `9f0336f4a744162be12c996c62c2d4a857dc3ef7` (v3 framing), `a5a0823` (v2 checkpoint/coherence), `bd89187` (composition), `be7c691` (record-once command enablement), `959bea5` and `98b3ec5` (v1 corrections)

## Decision (v3 active; v1/v2 historical)

The active decision is v3: the exact `HttpClient`-exposed singleton accepts canonical `Transfer-Encoding: chunked` only. `Content-Length` is absent; declared or surfaced trailers are rejected; decoded body size is 1..8192 bytes; `BodyBounds` and `Trailer` policies are explicit; handler header/drain settings, exactly-once observed cancellation tasks, and body zeroing are required. Public operations and CLI arguments remain unchanged, with no retry, fallback, or alternate. This is offline CODE GO, not live compatibility.

Exact identities are plan `snow_globe_ollama_recording_composition_plan/v3`, receipt `snow_globe_ollama_loopback_recording_receipt/v3`, artifact `snow_globe_ollama_recording_execution_artifact/v3`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v3`; path `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v3.json`. Goldens: transport `17ccc1f0c1da7084c08cf37d1d80e11bf16ffed06e196bbba373d7a33a9b3f27`; plan `534f26d41f11b23024aab95db7714e16cc374673170e3f9c71711d6fe23c1fd5`; receipt `93328d8b9bf1ca27b07a66063bdc90f5d9877ddb4b2eb3395ff72539e914e6da` / payload `85c8484a960652d6bb0ae91a0470452f281e3a34296886ec4c603240c1e14e3b`; artifact `2635667e36fb8800f21ecded4ac90d4e5840dafe48b669b518556b38eb60aae2` / payload `a79c3a0a31e9911038b9264c52f05d8eedec6785e81e32e33d5aca1bec124111`.

Validation: drain 8/8, focused 164/164, CLI 59/59, full 650/650, builds 0/0; deep review FINAL CODE GO with no P0-P2 findings. No raw-wire header/chunk-extension/trailer-absence proof, live compatibility, model execution, quality, cost, or production claim is made. The prior offline framing action is complete/historical. Current next action: clean committed v3 preflight and, only with separate fresh authority, exactly one v3 session then one validate, no retry; this ADR does not grant current authority.

Official references: [Ollama Generate API](https://docs.ollama.com/api/generate), [HttpHeaders.NonValidated](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.nonvalidated), [HttpCompletionOption.ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption), and [HttpResponseMessage.TrailingHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage.trailingheaders).

The historical v2 decision from `a5a0823` preserved unchanged public operations and CLI arguments with typed raw-free checkpoint/policy coherence. Its v2 identities, path, and compatibility allowance remain historical evidence only; the v1 path/schema and both zero-byte tombstones remain historical and untouched. V2 was offline-supported compatibility, not live proof.

Use a fixed-cell deep Module with only zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. Accept only repository root, observed PID/start ticks, and nonce as public inputs. Bind canonical root and nonce digests; reserve a safe CreateNew target before inner authorization/recording; perform exactly one attempt; and publish a strict raw-free artifact with durable readback.

The v1 artifact schema/path and writer rules in the historical composition implementation are retained for rollback context only. The historical v2 artifact was `snow_globe_ollama_recording_execution_artifact/v2` at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v2.json`, capped at 128 KiB. Windows final reads used a no-follow pinned verified single-link handle. Writer uncertainty left an indeterminate tombstone without delete/retry. `RecordingCli` had preflight/validate plus a separately gated record-once command.

## Design-It-Twice trade-offs

Rejected: flexible public cell/launcher/sink registry, because it widens authority and substitution. Rejected: benchmark identity/type reuse, because benchmark sequencing and evidence semantics differ. Chosen: fixed-cell Module and preflight/validate CLI with explicit live gate.

## Attempt-003 v2 evidence (historical)

Commit `6e47510` produced historical EVIDENCE GO: preflight plan `40cad2ba2c4ae568d7db8968b4547dff3b96da46c7386377fc447fa833ee82c5`, record exit 3 after 8,621 ms, validate exit 0 structurally complete, and exactly one HTTP200 POST. Artifact v2 is 5,822 B, canonical SHA-256 `4e358d3dc7bb578debaad8edb6578984c6f7f9ac8ec558013e2ef8ae59c00038`. No accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim was made. The offline framing action is complete and superseded by v3 above.

## Evidence and rollback

The v1 second-attempt evidence is historical/superseded: it ran from HEAD `af0925d`, consumed its authority, and produced no valid v1 artifact/evidence. Attempt-003 is also historical; v3 above is the current offline decision and grants no live authority.
