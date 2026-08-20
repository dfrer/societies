# Offline Ollama recording composition contract

## Current v3 bounded chunked framing (offline CODE GO)

Clean code commit `9f0336f4a744162be12c996c62c2d4a857dc3ef7` records the bounded transport decision for the exact `HttpClient`-exposed singleton. It accepts canonical `Transfer-Encoding: chunked` only; `Content-Length` is absent; declared and surfaced trailers are rejected; decoded body is bounded to 1..8192 bytes; and `BodyBounds`/`Trailer` policies are explicit. Handler header/drain settings, cancellation tasks observed exactly once, and body zeroing are part of the contract. Public operations and CLI arguments remain unchanged, with no retry, fallback, or alternate.

Exact identities are plan `snow_globe_ollama_recording_composition_plan/v3`, receipt `snow_globe_ollama_loopback_recording_receipt/v3`, artifact `snow_globe_ollama_recording_execution_artifact/v3`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v3`; fixed path `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v3.json`. Goldens are transport `17ccc1f0c1da7084c08cf37d1d80e11bf16ffed06e196bbba373d7a33a9b3f27`, plan `534f26d41f11b23024aab95db7714e16cc374673170e3f9c71711d6fe23c1fd5`, receipt `93328d8b9bf1ca27b07a66063bdc90f5d9877ddb4b2eb3395ff72539e914e6da` / payload `85c8484a960652d6bb0ae91a0470452f281e3a34296886ec4c603240c1e14e3b`, artifact `2635667e36fb8800f21ecded4ac90d4e5840dafe48b669b518556b38eb60aae2` / payload `a79c3a0a31e9911038b9264c52f05d8eedec6785e81e32e33d5aca1bec124111`.

Validation passed drain 8/8, focused 164/164, CLI 59/59, full 650/650, builds 0/0; deep review returned FINAL CODE GO with no P0-P2 findings. This is offline CODE GO, not raw-wire proof of header/chunk-extension/trailer absence and not live compatibility, model execution, quality, cost, or production evidence. The earlier offline framing action is complete/historical. Current next action is one clean committed v3 preflight and, only with separate fresh authority, exactly one v3 session then one validate, no retry; this does not grant current authority.

Official references: [Ollama Generate API](https://docs.ollama.com/api/generate), [HttpHeaders.NonValidated](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.nonvalidated), [HttpCompletionOption.ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption), and [HttpResponseMessage.TrailingHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage.trailingheaders). These sources describe public API behavior; they do not turn this offline evidence into raw-wire or live compatibility proof.

Commit `bd89187` adds a fixed qwen3.5:4b composition Module and isolated CLI. The prior dry-run-only state is historical: local code commit `be7c691` added the record-once surface; a later authorized invocation consumed one fresh authority, producing no valid artifact/evidence.

## Public surface and binding

The deep Module exposes exactly three operations: deterministic zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. The only irreducible public inputs are repository root, observed process ID/start ticks, and authorization nonce. Endpoint, model, path, hash, headers, timeout, retry, delegates, Adapter, and store selectors are fixed internal policy, not caller controls.

Prepare canonicalizes and digests the Windows repository root, digests rather than publishes the raw nonce, and binds the fixed qwen cell, prompt/provenance, runtime observation, and artifact path. The plan is object/module-bound and consumes atomically on foreign, cancelled, or reused execution.

## Historical v1 execution and artifact (superseded by v2)

Historical v1 `Execute` reserved and pinned a safe CreateNew target before inner `Authorize`/`RecordOnce`, performed exactly one attempt, and published/read back only at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json`. Writer uncertainty left an indeterminate tombstone; it never deleted or retried. The v1 Windows final read used the same no-follow pinned verified single-link handle, rejecting ancestor/final swaps, reparse points, hardlinks, and outside-root reads.

The historical v1 artifact schema was `snow_globe_ollama_recording_execution_artifact/v1`, maximum 128 KiB and JSON depth 8. Strict canonical and digest validation bound the repository root, cell/profile/adapter/codec/transport, plan, runtime, receipt, and artifact payload. This v1 description is rollback history only; v2 is historical and v3 is current above.

## V2 CLI and evidence (v1 attempt records below are historical)

### V1 bounded attempt settlement (historical/superseded)

The v1 settlement from HEAD `af0925d` is historical: exactly one preflight exited 0, exactly one record-once emitted `RECORDING_FAILED` / `composition_execution_indeterminate`, exactly one validate exited 1, and exactly one HTTP 200 POST occurred. Fix `98b3ec5` is historical. Both v1 authorities are consumed and no valid v1 artifact/evidence exists.

### First bounded attempt (historical)

The first attempt used plan `a9e7a10b973c7114d01361cbbeaa5705bd782385664d5a5ef923e0df3b5df39d`: exactly one preflight exited 0, exactly one record-once exited 5 after 7,881 ms, exactly one validate exited 1 `artifact_size_invalid`, and exactly one HTTP 200 POST occurred in 7.0314715 s, with no retry/fallback/alternate/download. Its offline correction `959bea5` validation was focused correction+artifact 50/50, transport+session 52/52, with the earlier full results retained as historical evidence. Its contemporaneous `RuntimeChanged` inference is historical and superseded; it is not current cause evidence. The first 0-byte tombstone was later archived in the same directory as `qwen3.5-4b-recording-execution-v1.failed-20260819-001.empty`; no valid artifact/evidence exists.

### Offline v2 milestone (`a5a0823`)

Public composition operations and CLI arguments remain unchanged. The internal typed raw-free terminal checkpoint and policy coherence are shared by artifact and CLI. Exact v2 identities and path remain preserved historical evidence; v2 was offline-supported compatibility, not live proof, and is superseded by v3 above.

Official source evidence for that compatibility interpretation is [Ollama v0.32.14 routes.go](https://raw.githubusercontent.com/ollama/ollama/v0.32.14/server/routes.go) and the [Ollama Generate API](https://docs.ollama.com/api/generate): local non-stream JSON uses Gin `c.JSON`, cloud proxy behavior explicitly sets `application/json; charset=utf-8`, and `stream:false` is documented as `application/json`. These sources support the offline compatibility allowance only, not live execution proof.

## Historical Design-It-Twice decision

A flexible public cell/launcher/sink registry was rejected because it widens identity and authority. Benchmark identity/type reuse was rejected because benchmark sequencing and evidence semantics differ. The chosen fixed-cell Module plus dry-run/validate CLI keeps the boundary closed. Attempt-003 is historical EVIDENCE GO with no accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim. The offline framing decision is complete; v3 above is authoritative for the current offline boundary.
