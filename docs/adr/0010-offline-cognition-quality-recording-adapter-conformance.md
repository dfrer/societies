# ADR 0010: Offline Cognition-Quality Recording Adapter Conformance

## Decision

Add the test-only `CognitionQualityRecordingAdapterConformanceHarness` in commit `8a5d339`. The harness exercises candidate fixtures through the real public recording-session Module and binds candidate identity, Adapter contract digest, expected evidence canonical digest, and a bounded canonical report.

## Contract and evidence

The eleven ordered checks cover Adapter attestations, complete evidence equivalence, sequential one-shot behavior, nonce replay, pre-cancel and expiry authority consumption, distinct nonce sessions, caller-input detachment, disposal closure, raw-free public surfaces, and the optional midflight-cancellation seam. There is no retry, fallback, alternate, or thirteenth call. Snapshot requests and retained fixture buffers are disposed and zeroed; caller inputs remain intact. Reports are bounded and raw-free.

The fixed offline response fixture is core-conformant but not fully conformant: its final result is `not_exercised_by_fixed_fixture` because it has no midflight seam. The async test fixture exercises cancellation during acquisition and is fully conformant. This harness is evidence for these offline fixtures only; it does not certify future provider Adapters, hidden retries, copied buffers outside the tested path, or security.

The harness performs no I/O, network, live/provider/model call, credential, payment, Ollama, journal, file, or world-authority action, and does not change production live/provider authority.

Source hashes are harness `1DF7E16ABA14AEAEC7B7397A2561A5158180C462A3815DC56146037A177FB23F` and tests `D1CDBCA028781EE192A9697E0FA80FDC620300243A0B0DEEE83F77D5AE8E22FD`. Focused validation passed 5/5; full Snow Globe Release passed 438/438; Release build passed with 0 warnings/errors; independent deep review returned CODE GO.

## Status and sole current next action

The conformance harness action is complete at `8a5d339`. Exactly one current next action remains: design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against this harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.
