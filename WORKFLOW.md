# W3-03 milestone handoff

# Snow Globe Lab first vertical slice handoff

# Snow Globe Lab scheduling comparison handoff

## Outcome

- Added the separate first scheduling experiment under `labs/Societies.SnowGlobe/` with focused tests under `tests/Societies.SnowGlobe.Tests/`; `src/societies/` remains unchanged.
- Both modes use the same recorded scripted responses and frozen shared per-tick observations. Sequential deliberation awaits them in stable order; controlled parallel deliberation collects them concurrently, then both enter the same ordinal validation/commit path.
- Canonical state/event replay matches for both modes. A deterministic gate fixture proves eight concurrent in-flight requests and reverse-order completion for controlled parallel versus one request in flight for sequential. Metrics are logical recorded latency units, critical-path latency, throughput, and equal dispatch coverage (not a broader fairness claim); malformed/failing responses are rejected without an action commit, and invalid latency contributes zero units.

## Evidence

- `dotnet build labs/Societies.SnowGlobe/Societies.SnowGlobe.csproj --configuration Release --no-restore` — passed, 0 warnings / 0 errors.
- `dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release --no-restore` — passed, 14/14, including deterministic out-of-order completion and missing/mismatch/null/nonpositive-latency/wrong-agent/explicit-failure/generic-exception fail-closed fixtures.
- `git diff --check` — passed.

## Boundary and next action

- Research-only and offline: no provider/model/network/credential use, Godot integration, runtime change, merge, push, or promotion.
- A later experiment may vary bounded cohort size or fixture latency while retaining the frozen-snapshot and ordered-commit contract.

## Outcome

- Added the separate local-only headless lab at labs/Societies.SnowGlobe/ with tests at tests/Societies.SnowGlobe.Tests/; src/societies/ is unchanged.
- Persistent agent records and deterministic world state are separate from the value-only, provider-neutral inference interface. The initial scripted adapter is offline only.
- Eight fixed-seed agents run sequential observe -> deliberate -> validate -> commit turns, gather shared resources, construct one shelter and one storage asset, then maintain the shelter. Committed actions are replayable through canonical event/state digests.

## Evidence

- dotnet build labs/Societies.SnowGlobe/Societies.SnowGlobe.csproj --configuration Release --no-restore - passed, 0 warnings / 0 errors.
- dotnet test tests/Societies.SnowGlobe.Tests/Societies.SnowGlobe.Tests.csproj --configuration Release --no-restore - passed, 4/4.
- git diff --check - passed.

## Risks and next action

- This is research infrastructure only: no real model/provider, credential, network, Godot observer shell, or production-runtime integration was added.
- Next bounded decision: compare a bounded batched-planning scheduler against the sequential baseline without relaxing deterministic validation and commit order.

## Outcome

- W3-02 merged through PR #123 at origin/master `d9e297f`. W3-03 implementation is committed locally at `a513636` on `feature/v3-w3-03-wetland-consequences`; docs/evidence publication is pending and GitHub delivery remains pending.
- The bounded slice adds session-owned deterministic wetland consequences, exact Protect/DrawDown per-unit quota and health rules, player+AI pre-ledger enforcement, canonical events, cached exhausted planning projection, strict schema-v9 persistence with v5-v8 migration/legacy continuation, and a minimal policy/health/quota/consequence HUD.
- W3-04+ remain inactive; another explicit **Continue V3** decision is required before activation. No LLM/fallback, restoration jobs, general law system, market, or broad UI was added.

## Validation and evidence

- Validation: focused wetland+artifact 37/37, core 129/129, UI 13/13; authoritative wrapper exit 0 in 416.9 s; Debug 0; full Release 442/442 with 0 failed/skipped; Godot 23/23; Release/ExportRelease 0 warnings/errors; deep review GO with no P0-P3 findings across 23 files (1,809/86).
- Performance: clean 14/14 pairs and 354/354 hashes; raw matrix `target_missed` (not safety failure), while established milestone gate is green at reference median p95/max `43.1679/213.4861 ms`, both soaks and forced invalidation green/deterministic, and c24 non-gating. No same-session A/B was run, so W3-02 comparison is non-causal.
- [Validation evidence](planning/active/evidence/v3-w3-03-validation.json) and [performance evidence](planning/active/evidence/v3-w3-03-performance-validation.json).

## Preserved context and risks

W2-VIS timing/visual-readback waivers, W2-06 history, Demo 1 concept classification, and historical schema/timing facts remain unchanged. Author/external clarity smoke and browser/device certification were not performed. Raw `target_missed`, no A/B causality, and recovered performance-worker workspace replacement are residual risks; issue records are WI-SOCIETIES-2026-009 and WI-GLOBAL-2026-078.

## Exactly one recommended next action

Pursue GitHub delivery of local implementation commit `a513636` together with reconciliation commits for these docs and evidence files.

## Changed files

- Implementation: W3-03 production/test changes are committed in `a513636` and are not modified by this reconciliation.
- Evidence added: `planning/active/evidence/v3-w3-03-validation.json` and `planning/active/evidence/v3-w3-03-performance-validation.json`.
- Documentation modified: `CURRENT_BUILD.md`, `README.md`, `WORKFLOW.md`, `planning/active/v3-weeks-3-4-development-plan.md`, and `planning/active/v3-two-week-development-plan.md`.
