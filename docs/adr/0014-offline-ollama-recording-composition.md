# ADR 0014: Offline Ollama recording composition

- Status: Accepted for v4 offline score-summary handoff; v1-v3 evidence remains historical
- Date: 2026-08-19
- Code HEAD: `18f2dc622ce27f14dd9f5d4126176a944244ae8d`; prior framing code `9f0336f4a744162be12c996c62c2d4a857dc3ef7`; v2 `a5a0823`; composition `bd89187`; record-once `be7c691`; v1 corrections `959bea5`, `98b3ec5`

## Decision (v4 offline score-summary; Attempt-001..004 historical)

The current decision is the bounded offline v4 score-summary handoff in commit `9bb4027`, atop v3 evidence commit `38c9bdb`. The fixed raw-free codec embeds the canonical quality report once and retains no prompts, responses, proposals, submissions, or model text. Exact scoring tuple checks include scenario-specific lower-utility reachability; dispositions are bounded; the detached result establishes structure/integrity only. For a complete result, only the score summary is populated; terminal and `EvidenceRejected` are null. No historical backfill occurred.

Terminal outcomes also include `Failed`, `Cancelled`, and `TimedOut`; only `Complete` populates the score summary/digest, while `EvidenceRejected` and other non-Complete terminal paths retain null summary/digest. The authoritative comparison fields are `premium`, `premium_cost`, `performance_delta`, and `quality_delta`, all null with status `insufficient_live_premium_evidence`.

The v4 schema identities are plan `snow_globe_ollama_recording_composition_plan/v4`, receipt `snow_globe_ollama_loopback_recording_receipt/v4`, and artifact `snow_globe_ollama_recording_execution_artifact/v4`; the artifact filename/path is `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v4.json`. Transport remains v3 and the high-level adapter/profile remains v2. The receipt is canonical digest-only and the artifact embeds the summary once. The two-input `LocalPremiumComparison` overload emits local cognition facts only, with `premium`, `premium_cost`, `performance_delta`, and `quality_delta` null and status `insufficient_live_premium_evidence`; the one-input golden is unchanged. No live v4 artifact exists. This does not claim authenticity, premium quality/cost/winner, or general intelligence.

Validation recorded red phases `10/23/33`, then `6/92/98`, followed by final targeted `98/98`, owned `157/157`, full SnowGlobe `706/706`, Recording CLI `59/59`, Benchmark CLI `56/56`, and three Release builds with 0 warnings/errors. Independent deep review was FINAL CODE GO with no P0-P2 findings. Snapshot `21c3f18942072aaa6954a6f95dd86528c33b94386d6263938b906565a66032e8`.

Attempt-001 through Attempt-004 chronology and v1-v3 artifacts remain preserved; older actions are historical/superseded. Exactly one current authorized action remains: execute one bounded local v4 preflight/record-once/validate against retained `qwen3.5:4b`, with no retry/alternate/download, preserving v1-v3. It has not occurred.

The v3 live settlement below is historical, not current.

The v3 live settlement is historical and superseded by the current offline v4 score-summary handoff. Static preflight was accepted after correctly scoping the GPU gate to non-Ollama WDDM apps. The exact `HttpClient`-exposed singleton uses canonical chunked framing; public operations and CLI arguments are unchanged, with no retry, fallback, or alternate. Server `server1`; preflight 1 succeeded with plan `13788130a3573ba8205cf833495e877ca26fb0daecab421bcab27880d4cb4e31`; record 1 succeeded in 28,609 ms; validate 1 succeeded in 148 ms; 12 ordered `/api/generate` POSTs returned 200 in 20.2868581 s. This is FINAL EVIDENCE GO with no P0-P2 findings.

Exact identities remain plan `snow_globe_ollama_recording_composition_plan/v3`, receipt `snow_globe_ollama_loopback_recording_receipt/v3`, artifact `snow_globe_ollama_recording_execution_artifact/v3`, and transport `snow-globe-ollama-loopback-recording-transport-adapter/v3`; path `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v3.json`. Artifact: 9,621 B, SHA/canonical `12c3e0f9b8fe13f8eaf2525642e130e4298e18c37f2fff58c2a316d2292f7b67`, payload `da3797cfe041ec949083eaf6e5ec9fecd22df4564ff767b176d94e2da10a50a1`; receipt: 6,169 B, SHA `e1a43bc8b7c44dfde6d71e372f1c6237239efe4ffd60716b869be72bf9dcb6b1`, payload `4d3541fff307a3f7dcd5aea1958c51ba0cc49f7b62df01ee07b086868dfb97fc`; nested digest `cd846a45a85085d1943ce8eb0c8b10ad489a802c1727f58d9d1ca04328e594e7`.

The run completed all 12 slots with `ResponseReceived`/200, `NotApplicable`, checkpoint/policy `None`, zero counters, and `additional=false`; validator accepted. CUDA RTX2070S was 34/34. Operator cleanup was zero but not independently retained or re-observed; raw-free/cloud/body-log were false. Limits: HttpClient-exposed framing only, no raw-wire proof, nested scoring digest not embedded/revalidated, retained captures cannot prove absence of overwritten invocations, HEAD is provenance not artifact field, and cleanup is operator observation. No quality/intelligence/winner/cost/commercial/general-compatibility/world-authority claim. V3 is complete/historical. Historical/superseded next action: offline Design-It-Twice for bounded raw-free nested score-summary projection or explicit non-retention before local-premium quality comparison; it does not replace the current authorized v4 action above.

Official references: [Ollama Generate API](https://docs.ollama.com/api/generate), [HttpHeaders.NonValidated](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.nonvalidated), [HttpCompletionOption.ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption), and [HttpResponseMessage.TrailingHeaders](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage.trailingheaders).

The historical v2 decision from `a5a0823` preserved unchanged public operations and CLI arguments with typed raw-free checkpoint/policy coherence. Its v2 identities, path, and compatibility allowance remain historical evidence only; the v1 path/schema and both zero-byte tombstones remain historical and untouched. V2 was offline-supported compatibility, not live proof.

Use a fixed-cell deep Module with only zero-I/O `Prepare`, atomic single-use `ExecuteAndPublishOnceAsync`, and bounded `ValidateArtifact`. Accept only repository root, observed PID/start ticks, and nonce as public inputs. Bind canonical root and nonce digests; reserve a safe CreateNew target before inner authorization/recording; perform exactly one attempt; and publish a strict raw-free artifact with durable readback.

The v1 artifact schema/path and writer rules in the historical composition implementation are retained for rollback context only. The historical v2 artifact was `snow_globe_ollama_recording_execution_artifact/v2` at `artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v2.json`, capped at 128 KiB. Windows final reads used a no-follow pinned verified single-link handle. Writer uncertainty left an indeterminate tombstone without delete/retry. `RecordingCli` had preflight/validate plus a separately gated record-once command.

## Design-It-Twice trade-offs

Rejected: flexible public cell/launcher/sink registry, because it widens authority and substitution. Rejected: benchmark identity/type reuse, because benchmark sequencing and evidence semantics differ. Chosen: fixed-cell Module and preflight/validate CLI with explicit live gate.

## Attempt-003 v2 evidence (historical)

Commit `6e47510` produced historical EVIDENCE GO: preflight plan `40cad2ba2c4ae568d7db8968b4547dff3b96da46c7386377fc447fa833ee82c5`, record exit 3 after 8,621 ms, validate exit 0 structurally complete, and exactly one HTTP200 POST. Artifact v2 is 5,822 B, canonical SHA-256 `4e358d3dc7bb578debaad8edb6578984c6f7f9ac8ec558013e2ef8ae59c00038`. No accepted body/wrapper/nested evidence/12-slot/quality/compatibility claim was made. The offline framing action is complete and superseded by the v3 live settlement above.

## Evidence and rollback

The v1 second-attempt evidence is historical/superseded: it ran from HEAD `af0925d`, consumed its authority, and produced no valid v1 artifact/evidence. Attempt-003 is also historical; the v3 live settlement above is complete and grants no fresh authority.
