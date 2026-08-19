# Offline Cognition-Quality Recording Adapter Conformance

## Contract

Commit `8a5d339` adds `CognitionQualityRecordingAdapterConformanceHarness` as a reusable, test-only offline conformance harness for the public recording-session Module and Adapter contract. It evaluates a candidate fixture through the real public `CognitionQualityRecordingSessionModule`, binds the exact candidate identity and Adapter contract digest, derives the expected evidence canonical digest from the fixed ordered responses, and emits a bounded raw-free report.

The ordered checks are exactly: `adapter_contract_and_attestations`, `complete_evidence_equivalence`, `request_sequence_and_one_shot`, `nonce_replay_closed`, `pre_cancel_spends_authority`, `expiry_spends_authority`, `distinct_nonce_sessions`, `caller_input_detached`, `disposed_adapter_closed`, `public_surfaces_raw_free`, and `midflight_cancellation`. The harness performs no retry, fallback, alternate, or thirteenth call. Snapshot requests and retained fixture buffers are disposed and zeroed; caller-owned response inputs remain unchanged. The report is detached, bounded, and contains no raw prompt, response, model, or fixture bytes.

The report binds the candidate identity SHA-256 digest, Adapter contract digest, expected evidence canonical digest, ordered check IDs/results, and its own canonical digest. The `OfflineFixedResponseCognitionQualityRecordingAdapter` fixture passes the first ten checks and is **core-conformant, not fully conformant**, because it does not exercise the optional midflight-cancellation seam. The async test fixture implements that seam, proves cancellation while acquisition is in flight, and reaches full harness conformance. A fixture failure returns a closed, raw-free failure report without throwing or echoing fixture text.

Commit `8f95875` adds the registry-closed `OfflinePinnedOllamaRecordingFixture`. It is entirely in memory: exactly twelve caller-supplied response buffers are copied, and frozen qwen3.5:4b metadata (canonical endpoint, runtime identity/hash, benchmark adapter/prompt/contract/evidence identities, artifact metadata, context 4096, output limit 96) is bound by contract digest and provenance. The candidate-neutral harness proves its exact eleven ordered checks and full offline result. `OfflinePinnedOllamaRecordingFixtureTests` prove exact count/index read-once behavior, fixture-owned response/request zeroing, caller preservation, concurrent capability safety, and pre/mid cancellation via a deterministic internal seam. Generic session timeout semantics are predecessor evidence, not pinned-fixture timeout evidence. The fixture has no path, PID, file, environment, socket, process, provider, model, credential, payment, or live authority and does not attest transport or model execution.

## Boundaries and limitations

This is test-only, deterministic, offline evidence. It performs no I/O, network, live/provider/model call, credential, payment, Ollama, journal, file, or world-authority action, and it does not alter production live/provider authority. Conformance of these fixtures does not certify future provider Adapters, hidden retries, copied-buffer handling outside the exercised fixture, or general security. Future Adapters still require separate registry/source work and security review.

## Evidence

- Commit: `8a5d339` (`Add offline recording adapter conformance harness`).
- Source hashes: harness `1DF7E16ABA14AEAEC7B7397A2561A5158180C462A3815DC56146037A177FB23F`; tests `D1CDBCA028781EE192A9697E0FA80FDC620300243A0B0DEEE83F77D5AE8E22FD`.
- Fixed fixture report: ten `pass` results followed by `not_exercised_by_fixed_fixture`; `IsCoreConformant=true`, `IsFullyConformant=false`.
- Async fixture report: all eleven checks `pass`; `IsCoreConformant=true`, `IsFullyConformant=true`, `IsConformant=true`.
- Validation: focused conformance tests 5/5; full Snow Globe Release 438/438; Release build 0 warnings/errors; independent deep review CODE GO.

## Sole current next action

The pinned fixture action is complete and historical at `8f95875`. Focused conformance+fixture validation is 12/12; full Release is 445/445; build is 0 warnings/errors; benchmark CLI is 56/56 before the final narrow correction; deep review is CODE GO. Fixture, harness, and test hashes are `B308723D8253222458BAD80A6B14041178CDED8829DC6C1B276F156EF91FA0B3`, `4AF2C88F5199ACAD1E9CA2EB78DB9877E145C1DDEA239797DE09D0085F7E7C3A`, and `C96C92B01294C37444612C859D2B3D0F562E9DE1867318BE4F6809A4F7D1D0EC`.

Exactly one current next action remains: design and implement an entirely **OFFLINE** bounded Ollama recording request/response codec plus fake transport port against this fixture, with no sockets, server/model calls, credentials, or live authority; actual loopback transport remains separately authorized and security-reviewed.
