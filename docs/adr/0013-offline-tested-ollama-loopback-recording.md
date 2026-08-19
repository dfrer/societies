# ADR 0013: Offline-tested Ollama loopback recording

- Status: Accepted for local offline evidence; live use separately gated
- Date: 2026-08-19
- Code: `a713267`

## Decision

Add a registry-closed exact `qwen3.5:4b` loopback facade and transport behind the existing codec port. Authorization is pure zero-I/O, process-local, object-bound, atomically single-use, with 60-second capability lifetime, 300-second session timeout, and 1,024 non-evicting nonces. Exactly 12 sequential `POST /api/generate` exchanges use the fixed endpoint, runtime, artifact, request, wrapper, response, HTTP/1.1, and Windows identity bounds. Charge is always `NotApplicable`, additional attempts are never authorized, evidence appears only after all 12 exchanges, and receipts/summaries are raw-free and bounded to 32 KiB.

## Trade-offs

The dedicated identity avoids impersonating unchanged `OfflinePinnedOllamaRecordingFixture` or `CognitionQualityRecordingSession`. Reusing benchmark transport was rejected because tags, warmup, benchmark admission, and sequencing differ. Reusing the fixture was rejected because it would blur no-I/O evidence with live transport identity. A general provider registry was rejected because it would widen authority and allow unreviewed substitution.

## Evidence and rollback

Offline validation: security 90/90, full 529/529, CLI 56/56, Release 0 warnings/errors; deep review 114/114 focused, 529/529 full, CLI 56/56, build 0/0, CODE GO, no P0-P2. No live Ollama/listener/socket/HTTP/process/file hash/model/GPU/provider/credential/payment or Windows verification call occurred. This does not prove live compatibility, artifact loading, model execution, quality, cost, world authority, production readiness, or commercial readiness. Inventory includes the codec/profile+port, facade, transport, and all three focused test files from `a713267`; the unchanged fixture/session remain no-I/O. Rollback reverts the two modified existing files (`OfflineOllamaRecordingCodec.cs` and its tests), removes only the four newly added facade/transport source and test files, and preserves the fixture/session contracts plus their tests. The current action is recorded in WORKFLOW; it is offline-only one-shot composition/CLI plus canonical evidence writer/validator, with no Ollama start or request.
