# Offline-tested Ollama loopback recording contract

Commit `a713267` adds a registry-closed exact `qwen3.5:4b` loopback facade and transport behind the existing codec port. Capability lifetime is exactly 60 seconds; session timeout is exactly 300 seconds; receipts are bounded to 32 KiB. This is offline code evidence, not live Ollama or model-execution attestation.

## Identity and sequence

The cell is normalized `qwen3.5-4b`, runtime `qwen3.5:4b`, endpoint `http://127.0.0.1:11435/`, `POST /api/generate`, runtime `E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14\ollama.exe`, runtime SHA-256 `11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54`, artifact SHA-256 `2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd`, 3,389,983,735 bytes, GGUF/qwen35/Q4_K_M, 4,659,865,088 parameters, context 4,096, output 96, seed 0, temperature 0.

`Authorize` performs zero I/O and binds a fresh nonce, publication, provenance, runtime binding, adapter identity, and contract digests. Nonce capacity is 1,024, non-evicting. The returned object-bound session permits one atomic `RecordOnceAsync`: exactly 12 sequential slots, one request each. No tags, warmup, retry, fallback, alternate, or thirteenth call exists.

## Bounds and state

Requests are 1..16,384 bytes; wrappers 1..8,192 each and 98,304 aggregate; extracted responses 1..1,024 each and 12,288 aggregate; canonical receipts are at most 32 KiB. HTTP is exact HTTP/1.1, status 200, exact `application/json` with zero parameters, exact declared `Content-Length`, no redirects, proxy, cookies, credentials, decompression, transfer/content encoding, authorization headers, or default headers; the handler disables proxy/cookies/decompression, allows one connection, and disables pooled connection lifetime and idle reuse. Windows checks PID/start/path/hash, unique IPv4 loopback listener ownership, and connected tuple before/between/after exchanges.

Cancellation/submission matrix: cancellation before body serialization is `DefinitelyNotSubmitted`; cancellation after body serialization starts but before response headers is `SubmissionUnknown`; cancellation after response headers is `ResponseReceived`; an uncaused `OperationCanceledException` is `TransportFailure` with the conservative state available at that point. Timeout and disposal are terminal and never authorize another attempt. Every row remains charge `NotApplicable`, and `AdditionalAttemptAuthorized` is false.

Clean idle disposal cancels, drains, and disposes normally. A failed or indeterminate exchange poisons the transport; if send/read work remains after cancellation, ownership transfers to exactly one forced-async late observer, which disposes responses/messages and zeroes late buffers before completing disposal. The transport client and cancellation source are released only after that ownership closes.

Nested `CognitionQualityRecordingEvidence` is emitted only after all 12 wrapper exchanges succeed. Summary and receipt are raw-free; optional detached Evidence intentionally exposes prompts/proposals for offline scoring. Receipt golden digest: `da913180079fc534543748bc53198f7d10de527137f038812fa5f735b90c62ee`. The unchanged public fixture and recording session remain no-I/O, and the live adapter cannot impersonate fixture v2.

## Evidence limits and live gate

Offline validation was security 90/90, full 529/529, CLI 56/56, Release 0 warnings/errors; independent review was 114/114 focused, 529/529 full, CLI 56/56, build 0/0, CODE GO, no P0-P2. No live Ollama/listener/socket/HTTP/process/file hash/model/GPU/provider/credential/payment action occurred; actual Windows P/Invoke/process/hash/connect paths were uninvoked. This does not prove live compatibility, artifact loading, model execution, quality, cost, world authority, production readiness, or commercial proof. A later live recording needs fresh authorization. Official references: [authentication](https://docs.ollama.com/api/authentication), [generate](https://docs.ollama.com/api/generate), [streaming](https://docs.ollama.com/api/streaming).

Rollback removes this adapter/transport and this contract/ADR; the codec, fixture, and recording-session contracts remain usable.
