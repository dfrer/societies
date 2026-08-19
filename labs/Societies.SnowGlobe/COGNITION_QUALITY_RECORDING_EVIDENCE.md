# Offline cognition-quality recording evidence

## Contract

`CognitionQualityRecordingEvidenceModule.Create(publication, provenance, exact ordered responses)` is one pure synchronous, dependency-category-1/in-process operation. The schema is `snow_globe_cognition_quality_recording_evidence/v1` and its semantics are `offline_recording_evidence_binding_only`. There is no Adapter or port at this seam.

The operation requires exactly 12 responses. Each response is 1..1,024 bytes; the aggregate response limit is 12,288 bytes; and the final canonical artifact is limited to 192 KiB. It embeds the exact existing prompt publication and exact existing recorded-response run, and adds an ordered response-set digest over `scenario_id`, `observation_digest_sha256`, `response_byte_count`, and `response_digest_sha256`. The canonical envelope binds prompt-set, provenance, recorded-run, and nested execution evidence.

All-or-error means only in-memory atomicity. It does not mean a file transaction, delivery guarantee, transport attestation, or execution proof. The result retains no raw response bytes. Module-owned temporary snapshots and fixtures are cleared; caller-owned bytes are not cleared. Correctly bound malformed content is a closed runner outcome (`no_proposal`); envelope corruption or coherence failure aborts.

Response association and identity are caller-attested. There is no prompt-delivery or model-execution attestation, provider status/retry/charge evidence, model-quality/general-intelligence/winner/cost claim, network/provider/credential/payment/journal/file/authoritative-world authority, or live action. `src/societies/` remains unchanged. Claim limitation codes and the one-operation deep Module are part of the canonical contract.

## Evidence

Implementation is code commit `bf756ed`, `Add offline cognition recording evidence`. Goldens are response-set `0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c`, payload `069aa258c0a6870aa6d8c60f14aed800cbb46923564d3b62f36a41ba3159a7fd`, and final `61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4`.

Source hashes are recording module `FD63A0B25A834DBEBC3A832FCEF032BEE6A52004E94312DDAB9E3AFE3652B353`, runner `16DE688AD721AFE402810DF507F36C89370CCAA81C114DDC499ACEACF1D19900`, and tests `E550EC0B69FB82804AF7DFD1BF48C62B0916A39503206FD0CE71CF7225D2025D`. Focused new-plus-predecessor validation passed 30/30; full Snow Globe Release passed 416/416; Release build passed with 0 warnings/errors; and `git diff --check` is clean. Independent deep review was FINAL CODE GO after fixing and rejecting split-brain publication, false serialized digest, provenance canonical mismatch, and undefined lane-99 regressions.

## Boundary and next action

The prior recording-evidence action is historical/completed. Exactly one current next action: design and implement an entirely offline, provider-neutral cognition recording-session Interface with an offline fake Adapter and one-shot/no-retry authorization that can later feed either the pinned local Ollama lane or a premium provider lane into this evidence Module. It must not make live calls, hold real credentials, or claim transport/model execution until separately authorized and evidenced.
