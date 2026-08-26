# Societies V3: Weeks 3-4 Development Plan

## Document Control

| Field | Value |
|---|---|
| Status | **Draft/Conditional** |
| Execution window | Mon 2026-07-27 to Fri 2026-08-07 |
| Capacity | One developer, 40-50 hours |
| Activation | W3-05 test/evidence slice merged via PR #173 at `f8eb222`; local player-selection follow-up is in validation; W3-06+ remain inactive |
| Product north star | [PRODUCT-THESIS.md](../PRODUCT-THESIS.md) |
| Current implementation truth | [CURRENT_BUILD.md](../../CURRENT_BUILD.md) |
| Predecessor | [V3 two-week development plan](v3-two-week-development-plan.md) |

W3-03 merged via PR #124 at `2a82fd0` (implementation `a513636`, evidence/docs `16be638`). W3-04 merged via PR #171 at `ea1f7ee` (implementation `c107d18`, evidence/docs `cefae37`). The W3-05 test/evidence slice merged via PR #173 at `f8eb222` (implementation/tests `616600e`, evidence/docs `a394a8f`) and adds no feature/runtime scope. W3-06+ and broader Weeks 3-4 remain inactive; this document does not authorize another feature.

## Entry State and Decision Rule

Known repository truth at drafting:

- The Week 1 hard performance/correctness gate is green.
- The formal performance target remains missed, and 24-citizen stress remains characterization-red.
- W2-02 (`empty_stores` crisis contract plus atomic shared-economy contribution) is validated and merged.
- W2-02 through W2-05 are validated and merged; W2-06 initially concluded **Stop Feature Expansion**, then the clean `478a4d9` repair cleared the hard performance safety gate. W3-01 merged at `7b747af`; W3-02 merged at `d9e297f`; W3-03 merged at `2a82fd0`; W3-04 merged at `ea1f7ee`.

W2-06 hard safety gates are green at `478a4d9`, and W3-01 through W3-05 are merged; delivery truth remains in Git/GitHub. Keep W3-06+ and broader Weeks 3-4 inactive. Candidate `5c732b1` carries the local `4`/`5` civic input and F3 interest inspection path; its review repair adds `6` for one visible offline cognition fallback. Final repair evidence and human author smoke remain incomplete.

The July 27-August 7 dates are historical proposal only, not current authorization. This document remains Draft/Conditional: W3-01 through W3-05 are completed bounded slices, and W3-06+ plus broader Weeks 3-4 remain inactive pending another continuation decision.

### W3-01 accepted bounded exception

Implementation commit `9d8bff4` adds one irreversible protect/draw-down civic policy contract: a typed command requires current tick and expected version, accepts the inclusive `0..1200` decision window, and emits one stable exactly-once event. Strict schema-v8 checkpoint, run-summary, and artifact persistence are implemented; strict v8 rejection and neutral v5-v7 migration are covered. The slice deliberately contains no UI, effects, reasons, LLM integration, or W3-02+ work.

Validation: focused 145/145; full 376/376 .NET (0 failed/skipped); Godot 4.6.2 headless exit 0 with manifest 23 (console count not independently parsed); Debug/Release/ExportRelease zero warnings/errors; deep review GO with no P0-P3 findings. Clean performance matrix is 14/14 pairs and 354/354 hashes; reference median p95/max `47.9643/176.1198 ms`, soaks `38.5737/205.6055` and `38.0676/192.3137`, forced invalidation `22.8738 ms`; c24 `151.4178/198.9147 ms` is non-gating characterization and `target_missed` remains aspirational. A general comparative regression assessment was not performed; hard safety did not regress against unchanged thresholds.

Evidence: [validation](evidence/v3-w3-01-validation.json) (`b256b8edd2a117cde6253e75c80be638e8c17203ebb2f689dc1d992e66fe7d17`) and [performance](evidence/v3-w3-01-performance-validation.json) (`580c98175056cdba1f021d7624434cba70da767ed49fed5a3009654af47ab623`).

### W3-02 accepted bounded slice

Implementation commit `9706e22` adds deterministic derived-only citizen preferences and reasons, ordinal capture, relative supports/opposes/uncommitted labels, and an atomic aggregate summary captured after policy selection. Legacy schema-v8 zero-summary compatibility and strict present-summary validation remain intact; critical Eat/Sleep/recovery behavior is unchanged. No schema/UI/wetland effects/LLM integration was added.

Validation: 410/410 .NET tests; Godot 23/23; Debug/Release/ExportRelease production builds with zero warnings/errors; deep review GO with no P0-P3 findings. Performance is clean 14/14 pairs and 354/354 hashes. The established milestone median gate is green at reference p95/max `47.4881/178.653 ms`; both soaks and forced invalidation are green and deterministic. The raw runner remains `safety_failure` because one t2 reference p95 is `63.5804 ms`; formal target remains missed. This is an isolated variance risk with no A-B causal attribution.

Evidence: [validation](evidence/v3-w3-02-validation.json) and [performance](evidence/v3-w3-02-performance-validation.json).

### W3-04 accepted bounded slice

Implementation commit `c107d18` adds a provider-neutral v1 observation/proposal contract for a citizen's stance on an already-selected civic policy. Canonical UTF-8 JSON is closed to `support_policy`, `oppose_policy`, and `request_reconsideration`, bounded to 1,024 bytes and depth 2, and validated through typed closed errors. Missing, invalid, cancelled, timed-out, and unavailable proposal paths use the same deterministic evaluator, validator, and exactly-once `civic.cognition.decision` event path. `SelectCivicPolicy` remains the sole policy mutation; schema v9 and v5-v8 migration are unchanged.

Validation: focused cognition 25/25; focused civic/session regression 105/105; full 467/467 .NET with 0 failed/skipped; Godot 23/23; Debug, Release, and ExportRelease production builds with zero warnings/errors; independent deep review FINAL CODE and acceptance-coverage GO with no remaining P0-P2 findings. The clean performance matrix passes all 14/14 pairs and 354/354 hashes. Raw status remains `target_missed`, while the established safety gate is green at reference median p95/max `48.0286/171.7133 ms`; both soaks and forced invalidation are green/deterministic, and c24 is non-gating characterization.

Evidence: [validation](evidence/v3-w3-04-validation.json) and [performance](evidence/v3-w3-04-performance-validation.json). This slice includes no live model/provider, credentials, network path, Snow Globe integration, policy mutation, W3-05, or author/external smoke.

### W3-05 targeted-tests delivery and author-smoke boundary

The W3-05 test-only slice adds 14 cross-loop managed cases and one Godot-hosted deterministic civic-loop smoke. It exercises both selected policies, conflicting structured citizen reasons, valid proposal and every closed fallback source, rejection/inertness cases, exactly-once cognition events without policy mutation, schema-v9 checkpoint/resume, and no-input continuation. The new suite is 14/14; selected W3-01 through W3-04 civic regressions are 107/107; the full wrapper passes 481/481 managed and 24/24 Godot tests; and Release/ExportRelease production builds have zero warnings/errors. The Godot smoke passed for Protect and DrawDown. See [validation evidence](evidence/v3-w3-05-validation.json).

The historical test/evidence boundary had no player-facing policy input. Candidate `5c732b1` supplies `GameManager` key routes (`4` Protect, `5` Drawdown) that call only `PrototypeRuntimeSession.SelectCivicPolicy`; its uncommitted review repair corrects selected-policy labels, restores the real seven-line inspector budget, and adds `6` to apply one existing `Unavailable()` deterministic fallback for the inspected citizen. The HUD reuses the existing policy/quota/health/consequence text, the F3 inspector exposes future-reeds and shelter-now support/opposition, and status visibly reports `deterministic_fallback | civic.cognition.decision`. The outcome contract and current candidate evidence are [here](v3-w3-05-player-civic-selection.md) and [here](evidence/v3-w3-05-player-civic-selection-validation.json). It is not yet a final matrix, full-wrapper, human author-smoke, visual-acceptance, independent-review, or delivery claim.

### Draft/Conditional Demo 1 foundation direction

Concept Studio decisions accepted after W2-VIS describe a bounded persistent 2 km x 2 km island sandbox with classic survival UX, blocky/voxel-inspired presentation over the existing heightfield, logged deterministic admin commands, incremental claims/homesteads/settlements/jobs/production/crafting/shops/trade, 24 normal citizens with a 40-citizen characterization ceiling, and eventual testing with 1-3 humans. The Cognition Director is provider-neutral and coordinates independent citizen minds; deterministic routines and offline fallback remain authoritative, and LLMs may propose but never directly mutate world state. These are **Accepted** concept directions, not **Validated** implementation claims, and remain conditional on a green performance repair, a later explicit continuation decision, and a later scoped plan.

## Product Question

Can AI citizens hold understandable material interests and participate with a human in one consequential civic decision?

The answer must be tested through one playable, deterministic civic-policy loop:

- a human chooses one of two policies;
- one session-owned policy state records the choice;
- citizens expose deterministic material preferences and concise reasons;
- the policy changes a bounded allocation rule through deterministic commands/events; and
- one shared ecological consequence becomes visible and replayable.

This is not a general law system, market, multiplayer feature, social graph, or live LLM integration.

## Bounded Scope

### Civic decision

Within the existing settlement scenario, offer exactly two policies for a fixed decision interval:

| Option | Deterministic allocation effect | Citizen material interest | Shared consequence |
|---|---|---|
| **Protect the wetland** | Reserve a bounded reed/wetland harvest quota and prioritize restoration work | Food/fuel security versus long-term resource availability | Wetland health improves or is preserved; short-term materials are constrained |
| **Draw down the wetland** | Permit the same bounded quota for immediate settlement supply | Immediate food/fuel/shelter pressure versus future availability | Wetland health declines; immediate supply is less constrained |

The session stores exactly one `CivicPolicyState` (neutral before a decision, then one selected option with tick/version metadata). Citizen preferences are deterministic functions of structured needs, assigned role/resource dependency, and wetland state. Every displayed reason must point to those facts; no hidden personality or model inference is required.

### Required deterministic surface

- Validated command: choose or change the policy only at permitted decision points.
- Deterministic event: policy chosen/changed, preference summary, quota application, and ecological transition.
- Session state: policy, bounded quota, wetland-health value/band, deterministic citizen preference/reason data, and required checkpoint fields.
- Presentation: current policy, visible wetland-health band, one shared consequence, and inspected-citizen reason.
- Replay: identical seed plus command sequence yields identical state, events, reasons, and outcome.

### Explicit non-goals

- General laws, constitutions, elections, taxes, or an extensible policy engine.
- Markets, prices, contracts, trade networks, or economy simulation.
- Multiplayer, networking, persistent accounts, or backend services.
- Social graph, relationship simulation, open-ended dialogue, or autonomous political campaigns.
- Live LLM/provider integration, prompt infrastructure, semantic memory, or model evaluation.
- New production art, broad content expansion, or 24-citizen target tuning beyond required characterization.

## LLM-Readiness Contract (No Model Integration)

The civic loop exposes a versioned, read-only structured input and a constrained proposed-action envelope. Implement only the schema, deterministic validator, fixtures, and fallback; no provider call occurs in this plan.

```json
{
  "schemaVersion": 1,
  "tick": 0,
  "citizen": {
    "id": "citizen-001",
    "materialState": { "foodNeed": 0, "fuelNeed": 0, "shelterNeed": 0 },
    "role": "gatherer"
  },
  "civicContext": {
    "policy": "protect_wetland",
    "wetlandHealth": 0,
    "remainingHarvestQuota": 0
  },
  "allowedActions": ["support_policy", "oppose_policy", "request_reconsideration"]
}
```

```json
{
  "schemaVersion": 1,
  "citizenId": "citizen-001",
  "proposedAction": "support_policy",
  "reasonCode": "future_reed_supply",
  "summary": "bounded display text"
}
```

The deterministic validator rejects wrong version, unknown citizen/action/reason code, stale tick, malformed content, or any request outside the current allowed-action set. It converts only validated proposals into existing deterministic commands/events. The offline fallback derives the same proposal and reason code from the deterministic preference function, records `decisionSource: deterministic_fallback`, and preserves replay. Live model integration remains deferred until this civic loop is proven through validation and clarity evidence.

## Work Breakdown

### Week 3: deterministic civic loop and proof (20-24 hours)

| Item | Estimate | Dependencies | Acceptance |
|---|---:|---|---|
| W3-01 Civic state and command/event contract | 5 h | W2-06 delivered at green hard gate | Exactly one policy state, valid decision window, deterministic command/event ordering, save/resume contract |
| W3-02 Citizen interests and causal reasons | 5 h | W3-01 | Each citizen has a deterministic material preference/reason; reasons trace to structured facts; no policy bypasses critical needs |
| W3-03 Wetland quota and shared consequence | 5 h | W3-01 | Both policies produce bounded, distinct, visible, replayable wetland/supply effects |
| W3-04 LLM-readiness schema and fallback | 4 h | W3-02 | Versioned fixtures, validation failures, constrained action vocabulary, deterministic fallback/replay equivalence |
| W3-05 Targeted tests and author smoke | 3-5 h | W3-01 to W3-04 | Fixed-seed policy paths, invalid command rejection, save/resume, no-input baseline, and one manual end-to-end run |

Week 3 exit: a human can make one policy choice, inspect at least two conflicting citizen reasons, and observe one shared ecological/material consequence without a model service.

### Week 4: clarity, reliability, and decision (20-26 hours)

| Item | Estimate | Dependencies | Acceptance |
|---|---:|---|---|
| W4-01 Minimal presentation and causal clarity | 5 h | Week 3 exit | Policy, consequence, citizen reason, and decision timing are readable at normal play resolution |
| W4-02 Persistence, artifacts, and deterministic replay | 5 h | W4-01 | Policy/wetland/checkpoint restore identically; event/run-summary schema is bounded and validates |
| W4-03 Narrow observed clarity playtests | 5-7 h | W4-01 | 3-5 sessions if available; record comprehension without facilitator explanation beyond the in-game briefing |
| W4-04 Clean validation and report | 5-7 h | W4-02, W4-03 | Clean build/tests, applicable performance characterization, artifacts, defects, and one decision recorded |
| W4-05 Contingency buffer | 0-2 h | As needed | Used only for correctness, clarity, or validation defects |

## Acceptance Gates

### Technical gates

- Existing full build, .NET suite, Godot headless suite, deterministic repeat/resume, and persistence checks remain green.
- The policy command is the sole policy mutation path; malformed, stale, or disallowed commands do not mutate state.
- Fixed-seed policy sequences reproduce state hashes, events, citizen reasons, quota consumption, and wetland-health transitions exactly.
- LLM-readiness schema validation and deterministic fallback pass without network access; no live model dependency enters the runtime.
- The 16-citizen hard safety gate must remain green. The formal target and 24-citizen stress remain tracked separately and are not silently reclassified as green.

### Product clarity gates

Without facilitator explanation beyond the game briefing, testers should be able to say:

1. which policy they chose and what it changed;
2. why at least one inspected citizen supported or opposed it in material terms;
3. what shared ecological/material consequence followed; and
4. whether the consequence felt connected to the decision.

If technical gates are green but this clarity is missed, run one narrow clarity iteration: improve wording, state visibility, causal sequencing, or reason presentation within the same two-policy loop. Do not add systems to compensate for unclear communication.

## Validation Commands

Run targeted tests during implementation, then the existing authoritative checks from a clean state:

```powershell
dotnet build src/societies/Societies.csproj --configuration Release
dotnet build src/societies/Societies.sln --configuration ExportRelease
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj --configuration Release
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
./scripts/run-prototype-validation.ps1
./scripts/run-performance-baseline-matrix.ps1
```

The final report must distinguish local/debug confidence from clean ExportRelease evidence and must not promote the 24-citizen stress characterization to a gate that it has not passed.

## Risks and Cut Order

| Risk | Response |
|---|---|
| Policy affects outcomes but is not understandable | Instrument reasons and consequence transitions; use the narrow clarity iteration before adding scope |
| Citizen preferences become arbitrary | Restrict every preference and display reason to deterministic material-state inputs |
| Schema invites premature model dependency | Keep schemas pure, versioned, fixture-tested, and offline; defer provider calls |
| Existing performance safety regresses | Stop feature work, characterize/revert the smallest responsible unit, restore green evidence |
| Playtests are unavailable | Complete author smoke and scripted evidence; label product clarity evidence incomplete |

Cut first, in order:

1. policy changes after the initial decision; keep one irreversible decision;
2. extra citizen reason variants; keep two clear material-interest reasons;
3. richer wetland visuals; retain a clear health band and one consequence;
4. optional LLM proposal envelope UI; retain schema, validator, fixtures, and fallback;
5. extra playtests beyond the minimum available sessions.

Never cut deterministic command/event ownership, replay/resume, the visible consequence, an inspectable citizen reason, the bounded schema/fallback contract, or the final validation report.

## Day 10 Decision (Fri 2026-08-07)

Publish a concise Weeks 3-4 validation report and choose one outcome:

- **Continue V3:** technical gates pass and the civic decision is understandable and consequential.
- **Narrow clarity iteration:** technical gates pass but playtests cannot explain the policy, citizen interest, or consequence; keep the same bounded loop.
- **Return to correctness:** determinism, persistence, build/test, or performance safety is red; halt expansion and repair from the last green boundary.
- **Defer LLM integration:** always, unless a later explicitly approved milestone is justified by validated civic-loop evidence. This plan contains no live model integration.

The report links its evidence to [the product thesis](../PRODUCT-THESIS.md), [the predecessor plan](v3-two-week-development-plan.md), and [current build reality](../../CURRENT_BUILD.md).
