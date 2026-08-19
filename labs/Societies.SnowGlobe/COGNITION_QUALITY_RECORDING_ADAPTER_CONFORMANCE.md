# Offline Cognition-Quality Recording Adapter Conformance

## Contract

Commit `8a5d339` adds `CognitionQualityRecordingAdapterConformanceHarness` as a reusable, test-only offline conformance harness for the public recording-session Module and Adapter contract. It evaluates a candidate fixture through the real public `CognitionQualityRecordingSessionModule`, binds the exact candidate identity and Adapter contract digest, derives the expected evidence canonical digest from the fixed ordered responses, and emits a bounded raw-free report.

The ordered checks are exactly: `adapter_contract_and_attestations`, `complete_evidence_equivalence`, `request_sequence_and_one_shot`, `nonce_replay_closed`, `pre_cancel_spends_authority`, `expiry_spends_authority`, `distinct_nonce_sessions`, `caller_input_detached`, `disposed_adapter_closed`, `public_surfaces_raw_free`, and `midflight_cancellation`. The harness performs no retry, fallback, alternate, or thirteenth call. Snapshot requests and retained fixture buffers are disposed and zeroed; caller-owned response inputs remain unchanged. The report is detached, bounded, and contains no raw prompt, response, model, or fixture bytes.

The report binds the candidate identity SHA-256 digest, Adapter contract digest, expected evidence canonical digest, ordered check IDs/results, and its own canonical digest. The `OfflineFixedResponseCognitionQualityRecordingAdapter` fixture passes the first ten checks and is **core-conformant, not fully conformant**, because it does not exercise the optional midflight-cancellation seam. The async test fixture implements that seam, proves cancellation while acquisition is in flight, and reaches full harness conformance. A fixture failure returns a closed, raw-free failure report without throwing or echoing fixture text.

## Boundaries and limitations

This is test-only, deterministic, offline evidence. It performs no I/O, network, live/provider/model call, credential, payment, Ollama, journal, file, or world-authority action, and it does not alter production live/provider authority. Conformance of these fixtures does not certify future provider Adapters, hidden retries, copied-buffer handling outside the exercised fixture, or general security. Future Adapters still require separate registry/source work and security review.

## Evidence

- Commit: `8a5d339` (`Add offline recording adapter conformance harness`).
- Source hashes: harness `1DF7E16ABA14AEAEC7B7397A2561A5158180C462A3815DC56146037A177FB23F`; tests `D1CDBCA028781EE192A9697E0FA80FDC620300243A0B0DEEE83F77D5AE8E22FD`.
- Fixed fixture report: ten `pass` results followed by `not_exercised_by_fixed_fixture`; `IsCoreConformant=true`, `IsFullyConformant=false`.
- Async fixture report: all eleven checks `pass`; `IsCoreConformant=true`, `IsFullyConformant=true`, `IsConformant=true`.
- Validation: focused conformance tests 5/5; full Snow Globe Release 438/438; Release build 0 warnings/errors; independent deep review CODE GO.

## Sole current next action

Design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against this harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.
