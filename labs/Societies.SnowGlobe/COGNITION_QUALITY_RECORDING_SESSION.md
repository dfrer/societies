# Offline Cognition-Quality Recording Session

## Contract

Commit `8256512` adds `CognitionQualityRecordingSessionModule`, an offline, provider-neutral orchestration boundary. The public instance module has exactly two operations: `Authorize` and `RecordOnceAsync`. The sole public runtime adapter is `OfflineFixedResponseCognitionQualityRecordingAdapter`; it is a deterministic in-memory fixture with no I/O.

Authorization is process-local and binds the publication canonical digest, prompt-set digest, provenance digest, adapter identity and contract digest, a canonical nonce, capability lifetime, and session timeout. Nonce tombstones are bounded and non-evicting at 1,024 entries; `OfflineFixedResponseCognitionQualityRecordingAdapter` tracks at most 4,096 capabilities. A capability binds the exact module and exact adapter object and is consumed atomically. Any call, including pre-cancellation, expiry, wrong-module use, or binding failure, spends the capability.

Recording is sequential over exactly 12 canonical slots. Each slot receives one attempt only: no retry, fallback, alternate adapter, or thirteenth call. Responses are 1..1,024 bytes each and 12,288 bytes aggregate, with exact binding echoes validated before evidence creation. Evidence is produced only after all 12 slots succeed and is passed to the existing recording-evidence Module. Correctly bound malformed bytes follow its existing closed scoring path; partial, definitely-not-submitted, submission-unknown, cancellation, timeout, adapter exception, binding failure, or evidence failure returns a raw-free no-evidence result.

Retained fake fixtures and prompt copies require explicit adapter disposal and are zeroed on disposal; caller inputs are preserved. Results identify an offline fixture only: they make no transport-delivery or model-execution claim and never authorize another attempt. Successful offline fixture completion is `NotApplicable`/`NotApplicable`; closed test or terminal paths preserve `DefinitelyNotSubmitted` or `SubmissionUnknown`/`Unknown` without delivery or charge attestation.

## Boundaries and limitations

This slice performs no network, provider, credential, payment, file, journal, world-authority, or live-model action. It does not authorize or perform Ollama or premium calls. Recording evidence remains caller-attested binding evidence only. The capability is not restart-durable; a caller can create a genuinely fresh authorization with a different nonce. Future adapters require a separate registry/source change and deep security review; they must own hidden retry prevention, copied-byte lifecycle, cooperative cancellation, runtime/provider/credential/financial controls, and any delivery or execution attestations.

## Evidence

- Source hash: `3354932C1BF170C495A59CCA607A8F84F77D64C9E724E9B17AE92B96502190B1`; test hash: `BCFFEE0DF340AC1E98C0C089B58F1BDB7A04F4B66F56356BA18CCC0DB400214D`.
- Focused validation: 17/17. Full Snow Globe Release validation: 433/433.
- Release build: 0 warnings/errors. `src/societies/` unchanged.
- Independent deep review: FINAL CODE GO after five corrections covering nonce replay, multi-capability slot indexing, pre-copy bounds, explicit zeroing lifecycle, and mutable-indexer TOCTOU.

The former next action to add an Adapter conformance harness is complete and historical at `8a5d339`. Current milestone truth and the sole next action are recorded in [CURRENT_BUILD.md](../../CURRENT_BUILD.md), [the conformance contract](COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md), and [ADR 0010](../../docs/adr/0010-offline-cognition-quality-recording-adapter-conformance.md): design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against the harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.
