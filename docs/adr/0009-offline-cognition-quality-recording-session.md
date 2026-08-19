# ADR 0009: Offline Cognition-Quality Recording Session

## Decision

Add the process-local `CognitionQualityRecordingSessionModule` in commit `8256512` (`Add offline cognition recording session`). The public instance API is `Authorize` plus `RecordOnceAsync`; the only public runtime adapter is `OfflineFixedResponseCognitionQualityRecordingAdapter`.

## Contract

- Authorization binds publication, prompt-set, provenance, adapter identity/contract, canonical nonce, capability lifetime, and session timeout.
- Nonce tombstones are bounded and non-evicting at 1,024; `OfflineFixedResponseCognitionQualityRecordingAdapter` capability tracking is capped at 4,096.
- A capability binds exact module and adapter references and has one atomic use. Any call, including pre-cancel, expiry, wrong-module, or binding failure, consumes it.
- Twelve canonical slots execute sequentially with one attempt each. There is no retry, fallback, alternate, or thirteenth call.
- Responses are bounded to 1..1,024 bytes each and 12,288 aggregate, with exact binding echoes. Evidence is created only after all twelve succeed.
- Partial/unknown/cancelled/timed-out/exception/binding/evidence-failure paths are raw-free and contain no evidence. Bound malformed bytes use the existing closed scoring path.
- Fake fixtures and prompt copies are explicitly disposed and zeroed; caller inputs remain intact. Successful offline fixture completion is `NotApplicable`/`NotApplicable`; closed test or terminal paths preserve `DefinitelyNotSubmitted` or `SubmissionUnknown`/`Unknown` without delivery or charge attestation. Results claim an offline fixture only, with no delivery, execution, or additional-attempt authority.

The module performs no network, provider, credential, payment, file, journal, world, Ollama, or premium action. It is process-local rather than restart-durable, and a fresh nonce can authorize a genuinely new session. Future adapters require separate registry/source change and deep security review, including hidden retry, copied-byte, cancellation, provider, credential, runtime, and financial controls.

## Evidence

Source/test hashes are `3354932C1BF170C495A59CCA607A8F84F77D64C9E724E9B17AE92B96502190B1` and `BCFFEE0DF340AC1E98C0C089B58F1BDB7A04F4B66F56356BA18CCC0DB400214D`. Focused validation passed 17/17; full Snow Globe Release passed 433/433; Release build passed with 0 warnings/errors. Independent deep review returned FINAL CODE GO after five corrections: nonce replay, multi-capability slot indexing, pre-copy bounds, explicit zeroing lifecycle, and mutable-indexer TOCTOU. `src/societies/` was unchanged.

## Status and next action

The recording-session and conformance-harness actions are complete and historical at `8a5d339`; the pinned fixture is complete at `8f95875`. Current milestone truth and the sole next action are recorded in [CURRENT_BUILD.md](../../CURRENT_BUILD.md), [the conformance contract](../../labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md), and [ADR 0011](0011-offline-pinned-ollama-recording-fixture.md): design and implement an entirely **OFFLINE** bounded Ollama recording request/response codec plus fake transport port against the completed fixture, without sockets, server/model calls, credentials, or live authority.
