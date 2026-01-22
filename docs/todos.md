# Project TODOs

Tracked issues and refactor tasks discovered during the comprehensive review.

## Configuration & Determinism
- [x] Adopt `TuningConfig` getters in simulation systems to reduce ad-hoc `Dictionary.get()` usage and ensure schema defaults/validation are enforced consistently.
- [x] Add a regression test that fails when `tuning.json` misses required schema keys (validate error path).
- [x] Document expected behavior when `tuning.json` fails to load (fail-fast vs. warn).

## Agent Behavior & AI
- [x] Fix local pollution usage in hunger drain (currently read but unused).
- [x] Split `DefaultBrain` into modular planners (survival/economy/governance) with targeted tests.
- [x] Add unit tests for goal stack edge cases (empty stack, instant completion loops).

## Environment & Ecology
- [x] Decide on pollution spread model and implement (or explicitly defer with a test/flag).
- [x] Implement flora growth mechanics or document future design.

## Enforcement & Governance
- [x] Complete policy-based fine logic (replace TODOs in enforcement).
- [x] Audit law/tax tuning consistency with enforcement calculations.

## Economy & Contracts
- [x] Verify contract payout profitability calculation (currently TODO in brain).
- [x] Add invariant checks for escrow/locked money vs. agent balances.

## World Generation & Resources
- [x] Make node spawn min/max start values configurable per resource type.
- [x] Add tests verifying resource node counts and caps match tuning.

## Visualization Stability
- [x] Investigate Godot visualizer quitting immediately on launch (no error surfaced). Capture logs and add a reproducible checklist.
- [x] Add startup diagnostics to `viz/visualizer_main.gd` (e.g., early error reporting or safe logging) to capture crash context.
