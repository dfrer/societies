# Workflow

The canonical process is [`docs/project/DEVELOPMENT_PROCESS.md`](docs/project/DEVELOPMENT_PROCESS.md). This root file is the current handoff boundary for Packet 02V.

## Packet 02V continuation packet

- **Outcome:** Packet 02A deterministic Causeway substrate delivered and mechanically complete; Packet 02V is the next authorized visual-production boundary, with no visual-completion claim.
- **Base:** implementation started from `master` `1745896535124bd39ca6321fe6430d93de81bf43`; delivered through PR #196 to merged `master` `155fa9dac62eebc516bcff189fd5692071f366d9` (tree `5c2e8b53a4b18802ead74b1ba57647c2846763cf`).
- **Branch/status:** reviewed feature head `c268207501817335d54e31cf432ade17f93ca78a` (same tree) was merged. Hosted product and lab gates passed as recorded below. No playable, visual-complete, accessibility, deployment, or release claim is made.
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
- Clean source `566b21d536b94f7d7eea17a2c432d2c1729af698` (tree `883a4f005a491f68c2f9bfec92dd24738e8da922`) completed a fresh attested v5 route with 3 realtime + 3 fixed trials. Process p95 median/worst was 7.3971/7.4032 ms; physics was 20.7075/20.7089 ms. Same-route deltas were -16.348967808273347% process and -10.38599679778442% physics, within the +10% regression budget. Backlog was 0/0/0, collision counts stayed 64 bodies and 12,777 -> 12,781 shapes, all Causeway command/edit/reload/fixed-replay equalities passed, and the 33.33 ms hard-safety line passed. Classification is `target_missed` because physics p95 exceeds 16.67 ms; GPU/display remain unmeasured headlessly.
- `godot --headless --build-solutions --quit` remains a known red signal-11 attempt and was not repeated.

- Hosted product run `33682822948` / job `100423159671`: success; fast 462/462, Godot build-solutions, headless 29/29, governance/diff pass. Hosted lab run `33682822608` (detector `100423158477`, suite `100423215546`, final `lab-tests` `100424336278`): success; core 1,186 passed, 5 documented historical byte-pinning skips, 0 failed; companions 56/56, 94/94, 104/104. Displayed/GPU timing remains unmeasured and these results do not create human visual acceptance.

## Next action and gates

Authority/tooling, salvage/provenance, and scope reviews are GO with no P0-P3 findings. Canonical evidence is [`planning/active/evidence/snow-globe-social-kernel-packet-02a-validation.json`](planning/active/evidence/snow-globe-social-kernel-packet-02a-validation.json). PR #196 and its exact hosted contexts reconciled successfully; Packet 02A is mechanically complete and delivered. There is no human visual acceptance for 02A. Packet 02V is next authorized and requires a near-final authored district plus interactive in-engine owner acceptance; Packet 03, citizens, cognition/providers, assets, deployment, and release remain unstarted/unauthorized.

Run `python scripts/check-project-governance.py`, contradiction search, and `git diff --check` before handoff. Packet 02V requires a near-final authored district and interactive in-engine owner acceptance; Packet 03 remains unstarted and unauthorized until that gate passes.
