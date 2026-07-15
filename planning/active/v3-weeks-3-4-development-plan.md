# Societies V3: Weeks 3-4 Development Plan

## Document Control

| Field | Value |
|---|---|
| Status | **Draft/Conditional** |
| Execution window | Mon 2026-07-27 to Fri 2026-08-07 |
| Capacity | One developer, 40-50 hours |
| Activation | Only after `V3_SPRINT_VALIDATION_REPORT.md` from W2-06 concludes **Continue V3** |
| Product north star | [PRODUCT-THESIS.md](../PRODUCT-THESIS.md) |
| Current implementation truth | [CURRENT_BUILD.md](../../CURRENT_BUILD.md) |
| Predecessor | [V3 two-week development plan](v3-two-week-development-plan.md) |

This plan is not executable while W2-06 is unresolved. It describes a bounded follow-on if the existing V3 slice earns continuation; it does not declare any planned feature implemented.

## Entry State and Decision Rule

Known repository truth at drafting:

- The Week 1 hard performance/correctness gate is green.
- The formal performance target remains missed, and 24-citizen stress remains characterization-red.
- W2-02 (`empty_stores` crisis contract plus atomic shared-economy contribution) is validated and merged.
- W2-03 directive causality is validated and merged; W2-04 through W2-06 remain before this plan can activate.

Activation requires W2-06 to conclude **Continue V3**. If correctness, determinism, persistence, build/test, or applicable safety gates are red, return to correctness/performance work and do not start this plan. If technical gates pass but observed playtests miss causal clarity, run a narrow clarity iteration against the existing civic loop rather than widening the product scope.

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
| W3-01 Civic state and command/event contract | 5 h | W2-06 Continue V3; green technical gate | Exactly one policy state, valid decision window, deterministic command/event ordering, save/resume contract |
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
