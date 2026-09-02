# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). This root file is the current handoff boundary for Packet 02A.

## Packet 02A continuation packet

- **Outcome:** deterministic Causeway substrate implemented locally, with no visual-completion claim.
- **Base:** `master` `1745896535124bd39ca6321fe6430d93de81bf43`.
- **Branch/status:** `feature/social-kernel-02a-causeway-substrate`, awaiting technical review and hosted delivery; not committed, PR-open, hosted-green, merge-ready, playable, visual-complete, or release-ready.
- **Retained:** deterministic causeway state/catalog/events/session; schema-v12 persistence/replay/artifact/summary; v5 route/profile; `ExecuteCausewayIntent`; zero-tick HUD refresh suppression/coalescing.
- **Dropped:** rejected presenter/`Label3D`, keyboard review controls, source-string UI scaffold/tests, and historical evidence/status.
- **Historical only:** checkpoint `7aad77dc10f2edf84e44f5f43e0082568a1ab8e9` and rejection head `269c80e16766094378afd4809bca40e045ab686b`.

## Validation evidence

- Governance passed.
- Full managed 582/582 and fast 462/462 passed with zero failures or skips; integration 11 and soak 109 remain exactly declared.
- Causeway authority 44/44; authority/artifact/baseline 85/85; broader persistence/voxel 162/162; executable profile/tooling and accepted-scene contracts 21/21, including 8/8 fail-closed tooling cases.
- Direct Release and Debug builds: 0 warnings/errors.
- Godot headless: 29/29, observed exit 0.
- Schema-v12 freezes and binds the actual Causeway definition schema/version/SHA-256 digest; catalog mismatch fails closed. Exact-field, duplicate, and order strictness plus valid anchors are covered. Accepted deterministic route transition is `ContributeCommunityTimber`, revision `0->1`, with Causeway equality across edit/reload/fixed replay.
- Earlier ad-hoc reuse-derived timing is invalid and is not Packet 02A evidence. A corrected fresh export with explicit `--quit` exited 0 and finalized a fail-closed v2 attestation, but post-export full-worktree drift stopped the wrapper before trials. All six current performance trials are unrun, so Packet 02A has no current performance classification; the 16.67 ms target, 33.33 ms hard-safety result, and GPU/display timing remain unresolved.
- `godot --headless --build-solutions --quit` remains a known red signal-11 attempt and was not repeated.

## Next action and gates

Technical review must inspect the actual Packet 02A diff and authority-fix boundary, then hosted delivery must run from the exact branch head. Packet 02V starts only after both are reconciled. There is no human visual acceptance for 02A. Packet 02V requires a near-final authored district and interactive in-engine owner acceptance; Packet 03, citizens, cognition/providers, assets, deployment, and release remain unstarted/unauthorized.

Run `python scripts/check-project-governance.py`, contradiction search, and `git diff --check` before handoff. No commit, push, or PR is authorized here.
