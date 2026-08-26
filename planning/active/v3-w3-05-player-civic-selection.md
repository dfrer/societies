# V3 W3-05 Follow-up: Player Civic Selection and Author Smoke

## Outcome contract

**Outcome:** In the authoritative Godot main scene, a player can select exactly one Protect or
Drawdown civic policy, inspect conflicting citizen material interests, and read the existing
quota, wetland-health, and consequence projection without granting presentation any world-state
authority.

**Owned slice:** `GameManager` keyboard input/presentation route, compact HUD inspector/help text,
focused managed and Godot smoke tests, and local handoff evidence.

**Non-goals:** W3-06+, W4, new policies, cognition schema/digest/vocabulary/fallback changes,
schema or migration changes, Snow Globe, network/provider/credential/payment/retry work, broad UI,
and performance optimization.

**Value gate:** Human agency and causal legibility while retaining deterministic command/event
ownership and the existing offline cognition behavior.

**Delivery boundary:** Local implementation and validation only. No commit, push, pull request,
merge, or visual/author-pass claim is made by this worktree.

## Player-facing route

- `4` requests **Protect the wetland** and `5` requests **Draw down the wetland**.
- `GameManager.SelectCivicPolicy` derives the current simulation tick and policy version, then
  calls only `PrototypeRuntimeSession.SelectCivicPolicy`. It does not write civic, wetland,
  event-log, citizen, or persistence state itself.
- The existing compact wetland HUD remains the source of the selected policy, reed quota,
  wetland health/band, and consequence text.
- `F3` continues to cycle citizens. The inspector now shows a read-only civic stance and plain
  material reason, including a forager's future-reeds interest and a builder's shelter-now
  interest with support/opposition after selection.

## Preserved contracts

- `SelectCivicPolicy` remains the sole civic-policy mutation authority. Duplicate player input
  rejects through the existing one-selection guard; stale command rejection remains covered by
  the existing session tests.
- Existing cognition resolution/apply remains exactly once and non-mutating for policy state.
  Missing/offline evidence still uses deterministic fallback.
- Schema-v9 persistence/replay/resume, reset, and no-input/offline behavior are unchanged; the
  existing W3-05 cross-loop suite remains the authority for those contracts.

## Validation and observation boundary

- Focused managed civic/UI regression: pass; see the companion machine-readable evidence.
- The new Godot-hosted input smoke passed for both policies, duplicate rejection, compact wetland
  reading, and both inspected interests. A later full headless wrapper must still complete before
  a full-suite claim.
- The performance matrix is required for the milestone correctness delivery because this follow-up
  changes production main-scene behavior. The parent delivery owner will run it after the
  implementation commit; no performance claim is made from this worktree.
- Automated input observation is not a human author smoke. A human-visible main-scene run and
  visual/interaction capture remain required before claiming author or visual acceptance.

## Manual author-smoke instructions

1. Launch `src/societies/scenes/main.tscn` in the Godot project with `balanced_basin` seed 1337.
2. Press `4`, read the Protect policy, `0/4` reed quota, `Healthy 75/100`, and preserved-material
   consequence; press `F3` until both future-reeds and shelter-now citizens appear, observing
   support and opposition.
3. Press `F7`, then `5`; verify Drawdown, `0/12`, `Strained 45/100`, and the degrading-material
   consequence, with the two citizen stances reversed.
4. Record the exact commit, scenario, seed, actions, HUD/inspector observations, and any visual or
   interaction limitation. Do not mark this passed from automated/headless output alone.
