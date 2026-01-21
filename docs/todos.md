# Project TODOs

Tracked issues and refactor tasks discovered during the comprehensive review.

## Configuration & Determinism
- [ ] Adopt `TuningConfig` getters in simulation systems to reduce ad-hoc `Dictionary.get()` usage and ensure schema defaults/validation are enforced consistently.
- [ ] Add a regression test that fails when `tuning.json` misses required schema keys (validate error path).
- [ ] Document expected behavior when `tuning.json` fails to load (fail-fast vs. warn).

## Agent Behavior & AI
- [ ] Fix local pollution usage in hunger drain (currently read but unused).
- [ ] Split `DefaultBrain` into modular planners (survival/economy/governance) with targeted tests.
- [ ] Add unit tests for goal stack edge cases (empty stack, instant completion loops).

## Environment & Ecology
- [ ] Decide on pollution spread model and implement (or explicitly defer with a test/flag).
- [ ] Implement flora growth mechanics or document future design.

## Enforcement & Governance
- [ ] Complete policy-based fine logic (replace TODOs in enforcement).
- [ ] Audit law/tax tuning consistency with enforcement calculations.

## Economy & Contracts
- [ ] Verify contract payout profitability calculation (currently TODO in brain).
- [ ] Add invariant checks for escrow/locked money vs. agent balances.

## World Generation & Resources
- [ ] Make node spawn min/max start values configurable per resource type.
- [ ] Add tests verifying resource node counts and caps match tuning.

## Visualization Stability
- [ ] Investigate Godot visualizer quitting immediately on launch (no error surfaced). Capture logs and add a reproducible checklist.
- [ ] Add startup diagnostics to `viz/visualizer_main.gd` (e.g., early error reporting or safe logging) to capture crash context.

