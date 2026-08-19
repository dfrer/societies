# ADR 0012: Offline Ollama Recording Codec and Transport Port

## Decision

Commit `016551c` keeps the public recording-session API unchanged and adds an internal pure codec plus an internal provisional transport port. The codec freezes the canonical qwen3.5:4b `/api/generate` body and strictly decodes complete bounded Ollama wrapper envelopes. The port has only an in-memory fake backed by the v2 pinned fixture; no live transport is implemented.

## Design-it-twice trade-off

We rejected a public profile registry because it would turn historical pinned metadata into public provider authority and imply live Ollama behavior. We also rejected expanding the public fixture into a transport facade because the fixture’s purpose is bounded offline recording evidence, not delivery or model execution. Keeping the public recording-session Interface stable preserves its existing authorization and raw-free outcome contract.

The internal pure codec isolates canonical encoding, strict wrapper validation, ownership, and error mapping. The internal provisional port gives the future transport a narrow one-method seam without granting sockets, credentials, retries, fallback, provider, payment, or delivery authority. The source-closed profile remains frozen until a second model justifies a separately reviewed profile decision.

## Evidence and limits

The v2 fixture accepts exactly twelve full wrapper byte sequences, not raw proposals. The codec enforces the frozen request order/options, strict UTF-8/JSON and envelope rules, 16 KiB request, 8 KiB wrapper, 98,304-byte aggregate wrapper, and 1..1,024-byte extracted-response bounds. It copies and owns each exchange once, zeroes owned buffers, preserves caller buffers, and tracks bounded capabilities. Direct new evidence covers held caller cancellation => public `Cancelled` with no evidence, and fixture/transport disposal => public `AdapterFailure`; both paths prove no retry and owned-buffer zeroing. Generic session timeout behavior remains predecessor recording-session evidence, not codec-specific evidence. Inner proposal semantics remain downstream responsibility.

Focused codec-plus-fixture validation is 39/39, full SnowGlobe Release is 477/477, the lab Release build is 0 warnings/errors, Benchmark CLI is 56/56, and independent deep review is FINAL CODE GO. Hashes are recorded in the [codec contract](../../labs/Societies.SnowGlobe/OFFLINE_OLLAMA_RECORDING_CODEC.md). No live/network/model/provider/payment action occurred; no current Ollama behavior or delivery/model-execution attestation is claimed.

## Follow-up gate

Implement and security-review an offline-tested loopback recording transport Adapter/preflight behind the existing internal port, but do not start Ollama, open sockets, send model requests, or claim delivery until a separate fresh live authorization.
