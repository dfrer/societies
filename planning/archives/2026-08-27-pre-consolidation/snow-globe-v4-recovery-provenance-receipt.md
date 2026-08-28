# Snow Globe v4 recovery-provenance receipt

## Status

Complete and merged. This contract does not authorize provider, credential, network, payment, deployment, mutable-session, or `src/societies/` work.

## Outcome contract

- User outcome: a read-only lab caller can tell whether a strict v4 run has an already-durable recovery continuation and, if so, inspect its authenticated disposition and source binding.
- Scope: `InspectRecoveryProvenance` only; existing `Inspect`, `Read`, writer, recovery, and deterministic-world behavior stay unchanged.
- Non-goals: v5 format, pause persistence, recovery repair, model/provider paths, path-policy changes, and gameplay changes.
- Acceptance: exact identity plus two-read raw-evidence stability; no receipt on invalid/drifting/forked evidence; no durable claim for a pending tail; bounded, detached receipt data only after strict continuation validation; v2/v3 never fabricate v4 provenance.

## Current evidence and next gate

- Release build: passed, 0 warnings / 0 errors.
- Focused `PersistedRunInspectorTests`: passed 23/23, including required-null marker regressions.
- Focused inspector/RunStore-v4-crash-recovery/persisted-session-v4-recovery selection: passed 58/58.
- Full Snow Globe Release suite: passed 943/943.
- `git diff --check`: passed.
- Independent determinism/public-contract review: FINAL GO with no P0-P3 findings after closing the required-null marker P1.
- Implementation commit `10b9816` was published through PR #133; required `build-test-smoke` passed in 4m12s and the PR merged to `master` as `11d662b`.
- Next decision: durable pause/resume requires an explicit v5 contract; stronger path/ABA identity remains a separate filesystem-hardening slice.
