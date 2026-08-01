# V3 Sprint Validation Report (W2-06)

## Outcome

The W2-06 decision remains **Closed — Stop Feature Expansion**. A measured partial repair is locally validated, but the clean ExportRelease matrix remains `safety_failure`: reference median p95 is 55.0153 ms against the 50 ms safety limit. This is not an overall performance improvement, merge candidate, Continue V3 decision, or Weeks 3-4 activation. The next action is characterization-only.

## Repository truth

This report is superseded by the measured partial repair in [v3-w2-06-performance-repair-validation.json](planning/active/evidence/v3-w2-06-performance-repair-validation.json). The repair branch is pushed/published at `b84f70e` (`feature/v3-w2-06-performance-repair`, commits `a8e04cc`, `84cc609`, `44414ec`), with no PR and no merge. Its 14/14 contract-valid matrix still has a red reference p95 median of `55.0153 ms` versus the `50 ms` safety limit; it is not an overall performance improvement or merge candidate. Base/master is merged PR #120 at `6d0958f`.

- Base and merged master: `6d0958f` (PR #120 merged).
- Repair branch: `feature/v3-w2-06-performance-repair` (pushed/published at `b84f70e`, unmerged; commits `a8e04cc`, `84cc609`, `44414ec`; no PR).
- Independent final review: GO, no P0-P3 findings.
- Technical evidence: [v3-w2-06-performance-repair-validation.json](planning/active/evidence/v3-w2-06-performance-repair-validation.json), status `BLOCKED_SAFETY_FAILURE`.

W2-05 is merged and locally validated; W2-VIS remains complete only under its documented timing and visual-readback waivers. Neither waiver is reclassified here.

## Technical evidence

| Gate | Result | Evidence |
|---|---|---|
| Release build | Pass, 0 warnings/errors | f0e88f0 |
| ExportRelease build | Pass, 0 warnings/errors | f0e88f0 |
| Pre-repair validation (historical) | Exit 0 in 737.9s; .NET 334/334 (0 failed/skipped, 9m22s), Godot 23/23 | `7952f37`; not the repair result |
| Full validation at repair HEAD | Exit 0 in 500.8s; .NET 341/341 (0 failed/skipped, 8m11s), Godot 23/23 | `44414ec` repair validation |
| Performance contracts | Pass, 14/14 pairs | Clean 7952f37 matrix |
| 16-citizen reference p95 | **Fail**: median 55.0153 ms vs 50 ms | Repair trials 55.2065 / 55.0153 / 51.0703 |
| 16-citizen reference maximum | Pass: median 142.6394 ms vs 250 ms | Repair matrix |
| 1,000-tick soak p95 | Pass: 46.826 / 45.1262 ms vs 50 ms | Deterministic repeat passed |
| 1,000-tick soak maximum | Pass: 178.8473 / 164.876 ms vs 250 ms | Repair matrix |
| Forced invalidation | Pass: 23.4193 ms | Transition and safety contract passed |
| 24-citizen stress | Characterization red, non-gating | 142.5931 ms p95 / 206.4644 ms max |

Compared with the immediately prior W2-06 run, soaks, maxima, forced transition, and stress characterization improved, but reference p95 median worsened by 3.4165 ms (51.5988 to 55.0153) and remains red. Do not call this an overall performance improvement.

The repair validation observed a focused generated test-build CS0436 warning, plus expected/nonfatal Godot frame-cap and optional runtime-metrics temp-path access-denied warnings. Final repair builds passed with 0 warnings/errors: Release `Societies.csproj` in 3.80s and ExportRelease `Societies.sln` in 2.24s; the authoritative wrapper exited 0 in 500.8s.

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

Run one characterization-only breakdown of the deterministic 15 reference spike ticks across BuildWorkOrders, route selection, scene sync/invalidation, and total tick overhead. Do not optimize or expand features until an exercised dominant cost is selected; preserve deterministic contracts, the non-gating stress classification, and all W2-VIS waivers.
