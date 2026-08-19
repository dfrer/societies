# ADR 0008: Offline Cognition-Quality Recording Evidence

## Decision

Add the offline `snow_globe_cognition_quality_recording_evidence/v1` binding boundary in code commit `bf756ed` (`Add offline cognition recording evidence`). The boundary is `semantics=offline_recording_evidence_binding_only` and exposes one pure synchronous operation, `Create(publication, provenance, exact ordered responses)`.

The operation validates and embeds the exact existing prompt publication, caller-attested provenance, and exact existing recorded-response run. It adds an ordered response-set digest over `scenario_id`, `observation_digest_sha256`, `response_byte_count`, and `response_digest_sha256`. The canonical envelope therefore binds prompt-set, provenance, recorded-run, and nested execution evidence without introducing an Adapter or port at this dependency-category-1/in-process seam.

## Contract

- Exactly 12 responses are required, each 1..1,024 bytes, with a 12,288-byte aggregate limit.
- The final canonical recording-evidence artifact is capped at 192 KiB.
- The operation is all-or-error only for in-memory atomicity. It is not a file transaction, delivery guarantee, transport proof, or execution proof.
- Raw response bytes are not retained in the result. Module-owned temporary snapshots and detached fixtures are cleared; caller-owned input bytes are never cleared.
- Correctly bound malformed response content remains a closed runner outcome (`no_proposal`); publication, provenance, envelope, and coherence corruption abort the operation.
- Response association and identity are caller-attested. The envelope does not attest prompt delivery or model execution and contains no provider status, retry, or charge evidence.
- The slice makes no model-quality, general-intelligence, winner, cost, network, provider, credential, payment, journal, file, authoritative-world, or live-action claim. `src/societies/` is unchanged.

The claim codes and the one-operation deep Module are described in [the recording-evidence contract](../../labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_EVIDENCE.md).

## Evidence

- Response-set digest: `0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c`.
- Payload digest: `069aa258c0a6870aa6d8c60f14aed800cbb46923564d3b62f36a41ba3159a7fd`.
- Final digest: `61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4`.
- Source hashes: recording module `FD63A0B25A834DBEBC3A832FCEF032BEE6A52004E94312DDAB9E3AFE3652B353`; runner `16DE688AD721AFE402810DF507F36C89370CCAA81C114DDC499ACEACF1D19900`; tests `E550EC0B69FB82804AF7DFD1BF48C62B0916A39503206FD0CE71CF7225D2025D`.
- Focused new-plus-predecessor validation: 30/30. Full Snow Globe Release: 416/416. Release build: 0 warnings/errors. `git diff --check`: clean.
- Independent deep review: FINAL CODE GO after fixing and rejecting split-brain publication, false serialized digest, provenance canonical mismatch, and undefined lane-99 regressions.

## Status and next action

The previous recording-evidence action is historical/completed, and the subsequent recording-session and Adapter-conformance actions are also complete and historical. Current milestone truth and the sole next action are recorded in [CURRENT_BUILD.md](../../CURRENT_BUILD.md), [the conformance contract](../../labs/Societies.SnowGlobe/COGNITION_QUALITY_RECORDING_ADAPTER_CONFORMANCE.md), and [ADR 0010](0010-offline-cognition-quality-recording-adapter-conformance.md): design and implement an entirely **OFFLINE** pinned local Ollama recording Adapter fixture against the conformance harness, without starting Ollama, making model calls, using network, or changing production live/provider authority.
