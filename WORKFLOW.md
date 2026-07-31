# W2-05 continuation packet

## Outcome and scope

V3-W2-05 is the immediate persistence foundation: attach crisis, contribution, and directive state to runtime schema v7; provide safe v5-to-v6-to-v7 migration and v7 round-trip behavior; extend bounded telemetry and automated slice coverage. Broader Demo 1 civic/economy/multiplayer/provider work is non-goal scope for this slice.

## Repository state

- Worktree: `C:\tmp\societies-v3-w2-05`
- Branch: `feature/v3-w2-05-schema-v7`
- Base: `03bb3ec7ff82d13389e6e94b62f159b87f022b4b`
- W2-VIS remains complete only under its documented two-exception waiver.
- W2-05 is implemented and locally validated, committed at `cea7f8a`, and published in draft PR #119; it remains unmerged.

## Accepted versus validated

Accepted Demo 1 direction is documented in the reviewed Concept Studio imports: bounded persistent 2 km x 2 km island, classic survival UX, blocky/voxel-inspired presentation over heightfield, logged deterministic admin commands, incremental claims/homesteads/settlements/jobs/production/crafting/shops/trade, 24 normal and 40 characterization citizens, eventual 1-3 humans, and provider-neutral Cognition Director with deterministic routines/offline fallback and LLM proposals only. This is planning direction, not validated runtime.

Validated W2-VIS exceptions remain: p95 `55.4529 ms` is over the 50 ms safety budget, max `142.9547 ms` is under the 250 ms limit, and persistence/hash equivalence is unavailable. The visual readback exception is waived, not passed.

## Validation record

- Focused W2-05 evidence: long `PrototypeCrisisReferenceTraceTests` 2/2 passed in 8.1752 minutes; Stable checkpoint at tick 600 resumes exactly to terminal tick 1253 with final Shelter.
- Release validation: `dotnet build src/societies/Societies.csproj --configuration Release` pass (0 warnings/errors, 1.07s); ExportRelease solution build pass (0 warnings/errors, 2.11s); `dotnet test ... --configuration Release` 333/333, 0 failed/skipped (8m35s); standalone Godot 4.6.2 Mono 23/23 (150.8s); `scripts/run-prototype-validation.ps1` exit 0 with Debug build 0 warnings/errors, 333/333 tests, and Godot 23/23.
- Schema-v7 evidence: strict directive/contribution/full-crisis checkpoint and telemetry persistence; v5->v6->v7 migration; prepare-before-commit; strict nested presence; bounded manifest-last coherent snapshot/event/summary generation; bounded atomic save/load pairs; exactly-once terminal; overflow-safe fields; no per-tick narrative log. Reference hashes: no-input 7496 / `9ce28bf44c480d43b1313a48abfdea82900c266afc04f5351eff6e61d4e6b93a`; 10.5 capture 8149 / `8a0239837c5f96ac5ef0e470e9e91178d620b7362213cf47eaa2aa20b637eecc`; scripted Stable `b939d06b57bbf0c480e5f1b96d5c778e3789465d078ebee459e58f5a6e32b104`.

## Known risks and next action

Do not activate Weeks 3-4 without W2-06 **Continue V3**. Residuals: the simple Food & Fuel -> Shelter-only pacing schedule exceeded 304 seconds during development (the representative adaptive Stable path is proven); atomic replacement relies on same-volume rename without explicit fsync; secondary V2/world/metrics artifacts remain outside the core generation manifest. The single next action is to complete protected-master checks and review on draft PR #119; do not claim merge or release. Final `git diff --check` and manifest JSON validation pass.
