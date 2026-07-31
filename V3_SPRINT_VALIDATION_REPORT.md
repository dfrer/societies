# V3 Sprint Validation Report (W2-06)

## Outcome

The W2-06 decision is **Closed — Stop Feature Expansion**. The required performance gate is red, and manual/product evidence was not completed, so this is not a Complete/green milestone. The clean ExportRelease matrix is contract-valid (14/14 pairs), but the 16-citizen reference median p95 is 51.5988 ms against the 50 ms safety limit, and both 1,000-tick soak p95 values are also above 50 ms. The next work is a bounded performance characterization/repair; do not activate feature expansion or Weeks 3-4.

## Repository truth

- Base and merged master: `f0e88f0` (PR #119 merged).
- Validation branch: `feature/v3-w2-06-sprint-validation`.
- Matrix repair source: `7952f37`, independently reviewed with no P0-P3 findings.
- Technical evidence: [v3-w2-06-technical-validation.json](planning/active/evidence/v3-w2-06-technical-validation.json), schema 2, status `BLOCKED_SAFETY_FAILURE`.

W2-05 is merged and locally validated; W2-VIS remains complete only under its documented timing and visual-readback waivers. Neither waiver is reclassified here.

## Technical evidence

| Gate | Result | Evidence |
|---|---|---|
| Release build | Pass, 0 warnings/errors | f0e88f0 |
| ExportRelease build | Pass, 0 warnings/errors | f0e88f0 |
| Full validation at HEAD | Exit 0 in 737.9s; .NET 334/334 (0 failed/skipped, 9m22s), Godot 23/23 | `7952f37`; docs/narrative manifest notes only dirty |
| Performance contracts | Pass, 14/14 pairs | Clean 7952f37 matrix |
| 16-citizen reference p95 | **Fail**: median 51.5988 ms vs 50 ms | Trials 54.2747 / 50.1764 / 51.5988 |
| 16-citizen reference maximum | Pass: median 184.1399 ms vs 250 ms | Clean matrix |
| 1,000-tick soak p95 | **Fail**: 50.2444 / 52.75 ms vs 50 ms | Deterministic repeat passed |
| 1,000-tick soak maximum | Pass: 201.8161 / 177.7758 ms vs 250 ms | Clean matrix |
| Forced invalidation | Pass: 27.8065 ms | Transition and safety contract passed |
| 24-citizen stress | Characterization red, non-gating | 146.4716 ms p95 / 247.0534 ms max |

Compared with the accepted W1-03c baseline, the current reference median improved from 570.6155 ms to 51.5988 ms p95 and from 3694.2534 ms to 184.1399 ms maximum. Improvement does not make the hard safety gate green.

HEAD validation also reports a generated test-build CS0436 warning, plus expected/nonfatal Godot frame-cap and optional runtime-metrics temp-path access-denied warnings. Debug project build completed with 0 warnings/errors. Earlier Release/ExportRelease results at `f0e88f0` are distinct provenance; final current-head builds rerun after the wrapper passed: Release `Societies.csproj` 0 warnings/errors in 1.57s and ExportRelease `Societies.sln` 0 warnings/errors in 4.96s.

The explicit checkpoint/resume filter was not run after the red performance result. The current declared contract remains historical evidence, not a newly reproven W2-06 result.

## Product and playtest evidence

The author 20–30 minute smoke and external observed playtests were not run after the technical stop. Product validation is therefore incomplete. No tester count, comprehension result, or observed-session claim is made.

W2-VIS visual acceptance remains `waived_not_passed`; its timing-only p95 failure and unavailable persistence/hash equivalence remain unchanged. A waiver is not a pass and does not upgrade the visual or performance gate.

## Decision mapping

| Decision | Rule | Result |
|---|---|---|
| Continue V3 | All required technical gates green and product evidence sufficient | Not eligible |
| Narrow clarity iteration | Technical gates green but observed clarity misses | Not evaluated; playtests unavailable |
| Stop Feature Expansion | Required performance/correctness gate red | **Selected** |

Weeks 3-4 remain Draft/Conditional, inactive, and blocked by this stop decision; later activation remains possible after green performance repair and explicit continuation. The accepted concept direction remains accepted concept direction only; it is not validated runtime scope.

## Next single action

Run one bounded 16-citizen performance characterization/repair focused on the residual p95 variance and rerun the clean ExportRelease matrix before any feature or playtest expansion. Preserve deterministic contracts, the non-gating 24-citizen classification, and all W2-VIS waivers.
