# V3 Sprint Validation Report (W2-06)

## Outcome

W2-06 is locally accepted at implementation commit `478a4d9`. The clean ExportRelease matrix passes all hard contracts, safety limits, hashes, schemas, and artifact bindings. Its aggregate `budgetStatus=target_missed` reflects stricter aspirational 25 ms reference/soak targets and non-gating c24 characterization, not a hard acceptance failure. The Stop Feature Expansion gate is cleared locally; the next bounded action is W3-01 only. No broader Weeks 3–4 gameplay work has started.

## Repository truth

- Validated implementation source: `478a4d9` on the clean reserve-extraction optimization branch.
- Canonical matrix: [v3-w2-06-clean-canonical-matrix-validation.json](planning/active/evidence/v3-w2-06-clean-canonical-matrix-validation.json).
- Implementation validation: [v3-w2-06-geometric-distance-field-cache-implementation-validation.json](planning/active/evidence/v3-w2-06-geometric-distance-field-cache-implementation-validation.json).
- Matrix/validation context: [v3-w2-06-performance-repair-validation.json](planning/active/evidence/v3-w2-06-performance-repair-validation.json).
- Git/GitHub remains authoritative for PR and merge state; this report does not claim stale pending-merge status.

W2-05 is merged and locally validated. W2-VIS remains complete only under its documented timing and visual-readback waivers; neither waiver is reclassified here.

## Technical evidence

| Gate | Result |
|---|---|
| Clean ExportRelease matrix | 14/14 pairs; 354 artifacts; 0 failed checks |
| 16-citizen reference | Median p95 45.011 ms ≤ 50; median max 172.7039 ms ≤ 250 |
| 1,000-tick soaks | p95 41.0153 / 41.24 ms; max 175.5645 / 168.2482 ms; deterministic repeat pass |
| Forced invalidation | Correct at 23.3878 ms |
| 24-citizen stress | 157.0955 ms p95 / 198.9522 ms max; characterization only, `safetyGateApplied=false` |
| Final full validation | Wrapper exit 0 in 325.2s; .NET 350/350; Godot 23/23; Debug/Release/ExportRelease 0 warnings/errors; [evidence](planning/active/evidence/v3-w2-06-final-validation.json) |

The aggregate target miss is explained by the stricter aspirational 25 ms reference/soak target and c24 50 ms characterization target. All requested hard-safety limits and contracts pass.

## Product and caveats

Author smoke and external observed playtests were not run after the technical stop; no tester or comprehension evidence is claimed. W2-VIS visual acceptance remains `waived_not_passed`; its timing-only p95 failure and unavailable persistence/hash equivalence remain unchanged. The accepted Demo 1 direction remains accepted concept direction only, not validated runtime scope.

## Decision and next action

The W2-06 Stop Feature Expansion gate is cleared locally. Confirm Git/GitHub delivery truth, then activate W3-01 only. Do not claim broader Weeks 3–4 gameplay has started.
