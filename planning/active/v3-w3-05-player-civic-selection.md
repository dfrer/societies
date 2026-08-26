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

**Delivery boundary:** Candidate `37afb1b` and evidence/docs `155b4e6` merged through PR #175 at
`36c445b` after required `build-test-smoke` passed in 4m13s. Automated validation, the clean
performance contract, independent review, and code delivery are complete. Human/visual author
observation remains pending; no author-pass claim is made.

## Player-facing route

- `4` requests **Protect the wetland**, `5` requests **Draw down the wetland**, and `6` records
  one existing deterministic offline cognition fallback for the currently inspected citizen.
- `GameManager.SelectCivicPolicy` derives the current simulation tick and policy version, then
  calls only `PrototypeRuntimeSession.SelectCivicPolicy`. It does not write civic, wetland,
  event-log, citizen, or persistence state itself.
- The existing compact wetland HUD remains the source of the selected policy, reed quota,
  wetland health/band, and consequence text.
- `F3` continues to cycle citizens. The inspector now shows a read-only civic stance toward the
  **selected** policy and a plain material reason, including a forager's future-reeds interest and
  a builder's shelter-now interest with support/opposition after selection.

## Preserved contracts

- `SelectCivicPolicy` remains the sole civic-policy mutation authority. Duplicate player input
  rejects through the existing one-selection guard; stale command rejection remains covered by
  the existing session tests.
- Existing cognition resolution/apply remains exactly once and non-mutating for policy state.
  Key `6` publishes the selected citizen observation, resolves the existing `Unavailable()` path,
  applies it once, and visibly reports `deterministic_fallback | civic.cognition.decision`.
  Repeating `6` fails closed from authoritative `civic.cognition.decision` event history, which
  is restored by the existing schema-v9 artifact route without changing its schema or module
  semantics.
- Schema-v9 persistence/replay/resume, reset, and no-input/offline behavior are unchanged; the
  existing W3-05 cross-loop suite remains the authority for those contracts.

## Validation and observation boundary

- The final full wrapper passes 485/485 managed tests and 25/25 Godot tests. Release and
  ExportRelease production builds pass with zero warnings and zero errors.
- The repaired Godot-hosted input smoke passes for both policies, correctly labelled opposition,
  combined inspector layout, duplicate policy/cognition rejection, compact wetland reading, both
  inspected interests, and the visible offline cognition event. It saves and restores through
  `GameManager`, then proves resumed `6` rejects without changing event count or policy; `F7`
  creates a fresh session where one new event is allowed.
- The clean 14/14 Release matrix contract passes at production commit `edfb673`; final commit
  `37afb1b` changes only a managed test assertion. Its raw timing budget is `safety_failure`:
  correctness delivery is green, but this is not a performance-budget pass.
- Deep review is GO with no P0-P2 findings, including the restored-session guard and final
  1280x720 capacity assertion.
- The visible `Societies (DEBUG)` window launched and was uniquely identified, but two reliable
  state-capture attempts failed with `SetIsBorderRequired failed: No such interface supported
  (0x80004002)`. No blind input was sent. Human-visible policy, citizen, cognition, and reed-harvest
  observations remain required before claiming author or visual acceptance.

## Manual author-smoke instructions

1. Launch `src/societies/scenes/main.tscn` in the Godot project with `balanced_basin` seed 1337.
2. Press `4`, read the Protect policy, `0/4` reed quota, `Healthy 75/100`, and preserved-material
   consequence; press `F3` until both future-reeds and shelter-now citizens appear, observing
   support and opposition.
3. Press `F7`, then `5`; verify Drawdown, `0/12`, `Strained 45/100`, and the degrading-material
   consequence, with the two citizen stances reversed.
4. With a policy selected and a citizen inspected, press `6`; record the visible
   `deterministic_fallback | civic.cognition.decision` result. Press `6` again and record its
   rejection without a second event.
5. Press `F6`, `F7`, and `F9`, then press `6` again. Record that the restored session rejects it
   without selecting another policy or recording another cognition decision; press `F7`, choose a
   policy, and confirm one fresh `6` action is available.
6. Harvest exactly one reed through the ordinary player interaction route, then record the updated
   quota/health reading. This is a manual observation boundary: the author must confirm the real
   resource target and interaction before claiming it.
7. Record the exact candidate commit, scenario, seed, actions, HUD/inspector observations, and any
   visual or interaction limitation. Do not mark this passed from automated/headless output alone.
