# Offline Ollama Recording Codec

## Milestone

Commit `016551c` adds the internal category-1 pure codec and category-3 provisional port for the offline pinned recording lane. The codec encodes the exact canonical `POST /api/generate` request for model `qwen3.5:4b`; the port currently has only the deterministic in-memory fake. This is recording evidence, not a client or live transport.

The public `CognitionQualityRecordingSession` Interface is unchanged. `OfflinePinnedOllamaRecordingFixture` is now `offline-pinned-ollama-qwen35-4b-recording-fixture-v2` and accepts exactly twelve complete, bounded Ollama generate wrapper byte sequences. It does not accept raw proposal bytes. The fixture retains the prior pinned qwen3.5:4b artifact provenance as metadata only; it has no live attestation.

## Interfaces and exact contract

The internal pure codec is `OfflineOllamaRecordingCodecModule` (`snow-globe-offline-ollama-recording-codec/v1`). `Encode` accepts one existing recording-session request and produces an owned `OfflineOllamaRecordingTransportRequest` with `POST`, canonical endpoint identity `http://127.0.0.1:11435/`, path `/api/generate`, and a canonical UTF-8 JSON body. The body uses the frozen field order and options: `model`, escaped `prompt`, `stream:false`, `think:false`, `raw:false`, object `format` with the closed `agent-00`/action/quantity schema, then `options` with `num_ctx:4096`, `num_predict:96`, `seed:0`, and `temperature:0`. Request bytes are bounded to 16 KiB.

`Decode` accepts one bound `OfflineOllamaRecordingTransportResponse`, requires HTTP 200, exact `application/json`, no redirect, no content encoding, and an exact declared length. It reads and takes the wrapper body once, then strictly validates UTF-8 and JSON (no BOM, comments, trailing commas, duplicate or unknown properties, depth above 4, or trailing data). The wrapper must contain the exact model, a parseable timestamp with an explicit offset, a nonempty `response`, `done:true`, `done_reason:"stop"`, and all required positive/nonnegative counters. If present, `context` must be a strict bounded nonnegative Int32 token array of at most 4,096 entries. Total and component durations must be coherent with the remaining session budget. Wrapper bytes are bounded to 8 KiB and extracted response bytes to 1..1,024.

The codec extracts owned response bytes only; it does not parse proposal semantics. A malformed inner proposal therefore remains downstream `no_proposal` evidence when the wrapper itself is valid. Envelope, binding, status, media, encoding, length, model, timestamp, done/stop, context, counter, or coherence failures are rejected before a response buffer is returned.

The internal `IOfflineOllamaRecordingTransportPort` exposes exactly one `ExchangeOnceAsync` method. Its only implementation is `InMemoryOfflineOllamaRecordingTransportAdapter`, which owns copied wrappers, permits exactly one sequential exchange per slot, tracks at most 4,096 capabilities, and performs no retry, fallback, alternate, or thirteenth call. Per-wrapper and aggregate pre-copy bounds are enforced; request and wrapper buffers are read/copied once, owned buffers are zeroed, and caller buffers are preserved. Direct new evidence covers held caller cancellation => public `Cancelled` with no evidence, and fixture/transport disposal => public `AdapterFailure`; both paths prove no retry and owned-buffer zeroing. Generic session timeout behavior remains predecessor recording-session evidence, not codec-specific evidence. Public outcomes remain raw-free and report offline `NotApplicable` submission/charge semantics.

## Non-goals and claim limits

This slice has no `HttpClient`, socket, process, file, environment, credential, provider, payment, model-execution, delivery, or alternate transport authority. It makes no claim about current Ollama behavior, transport delivery, model execution, quality, winner, cost, or production readiness. Loopback transport/provider use remains a separate authorization and security-review gate. The existing public recording-session Interface and authoritative Godot project are unchanged.

## Validation

Independent deep review returned FINAL CODE GO after multiple corrections. Focused codec-plus-fixture validation passed 39/39; full SnowGlobe Release passed 477/477; the lab Release build passed with 0 warnings and 0 errors; Benchmark CLI passed 56/56. Diff and whitespace checks were clean, and no live, network, model, provider, or payment action occurred.

Source hashes at the committed milestone are codec `741549C4DA89EAB362AF6EC0B3FA664168BEBCF0F373BE151CF2269AE3E8482A` and fixture `1FF2DB77CF53285B5A7BF5DAED0FA24EDF8AC551C7C92B2B63ED5A0AE491A2CA`.

The next action is: implement and security-review an offline-tested loopback recording transport Adapter/preflight behind the existing internal port, but do not start Ollama, open sockets, send model requests, or claim delivery until a separate fresh live authorization.
