# Societies V3 Experience Recovery Plan

## Document Control

| Field | Value |
|---|---|
| Status | **Ready** |
| Decision date | 2026-08-26 |
| Activation | User-directed after the W3-05 manual play assessment failed |
| First milestone | **ER-01 First Believable Settlement Loop** |
| Current implementation truth | [CURRENT_BUILD.md](../../CURRENT_BUILD.md) |
| Product direction | [PRODUCT-THESIS.md](../PRODUCT-THESIS.md) |
| Superseded execution path | W3-06+ and the old Week 4 sequence remain inactive |

## Why the Trajectory Changes Here

W3-05 is a narrow engineering success and a product-experience failure.

The deterministic civic command, citizen-interest reasons, wetland consequence, persistence,
fallback, tests, and delivery are real. The user-led manual assessment on 2026-08-26 is also real:

- the experience feels exceptionally basic and largely non-functional relative to the product goal;
- play feels on rails rather than like participation in a living settlement;
- repeated runs look and feel substantially the same;
- the HUD lacks hierarchy and refinement; and
- interactions and world objects feel unfinished.

This is the missing product gate that automated input and headless tests could not answer. It is not
a request for cosmetic polish around the same test harness. The next work must make one existing
deterministic loop feel intentional, embodied, variable, and causally readable before adding more
systems.

## Outcome Contract

**User outcome:** the first 8-10 minutes feel like participating in a settlement with a problem,
not operating a developer test scene.

**Owned slice:** one player-facing loop connecting orientation, resource interaction, contribution,
one civic choice, one citizen response, and one visible shared consequence.

**Reliability gate:** simulation truth and every mutation continue to flow through the existing
deterministic command/event Interfaces. Presentation must not become a second authority.

**Product gate:** the user can complete the loop without a key list or facilitator explanation and
can identify what they chose, why a citizen reacted, and what changed in the settlement or wetland.

**Delivery gate:** focused checks, the authoritative local validation wrapper, warning-free production
builds, and a user-run acceptance pass are recorded separately. Automated checks cannot mark the
product gate green.

### Non-goals

- W3-06+, the former Week 4 expansion, general governance, markets, law, multiplayer, or combat.
- Live LLM/provider integration, Snow Globe integration, credentials, network calls, or paid services.
- A production-art overhaul, voxel-terrain replacement, or broad content catalog.
- A generalized quest framework, dialogue system, policy engine, or UI framework.
- Hiding the prototype's limits behind scripted narration or a longer tutorial.

## ER-01: First Believable Settlement Loop

### Player path

The exact labels may be refined during implementation, but the path remains bounded:

1. Arrive with a clear settlement need and a visible next action.
2. Locate and harvest one relevant resource through a readable focus, affordance, and result.
3. Bring it to the central depot and see inventory, depot, and settlement state respond.
4. Reach the civic decision through a deliberate in-world or HUD choice surface; number keys may
   remain shortcuts, but cannot be the only understandable route.
5. Inspect or encounter at least one citizen whose material interest is legible.
6. Observe one immediate supply effect and one wetland/ecological consequence tied to the choice.
7. Finish with a clear changed state and a reason to try the contrasting setup.

### Required experience improvements

#### 1. HUD hierarchy

- Keep one primary settlement need/decision visible.
- Show one contextual interaction prompt near the player's focus.
- Show concise inventory/contribution feedback close to the action.
- Move debug metrics, raw controls, and dense inspector data behind an optional diagnostic layer.
- Preserve readable contrast and avoid critical text collisions at 1280x720.

#### 2. Interaction and object feedback

- A focused harvestable or depot has a clear highlight/state change and action label.
- Harvest, depletion, rejection, contribution, and civic-selection results have distinct feedback.
- Resource and depot visuals expose materially different states without relying on floating debug text.
- Interaction feedback is derived from authoritative state/result data and stores no competing state.

#### 3. Agency and causal sequencing

- The civic choice is presented as a tradeoff, not an unexplained numeric hotkey.
- At least one citizen's current work or material interest is visible before the choice.
- After the choice, the citizen/world response is sequenced so cause and effect can be followed.
- The player can choose either policy and continue; the loop does not prescribe one correct answer.

#### 4. Bounded variation

- Supply exactly two curated deterministic starting profiles using existing scenario/seed machinery.
- The profiles must differ in at least three player-visible ways: resource approach, immediate settlement
  pressure, and environmental/world cue.
- Both profiles use the same authoritative rules and remain exactly replayable.
- This is contrast evidence, not a claim of broad procedural variety.

### Architecture and seams

The existing `PrototypeRuntimeSession` remains the deep gameplay Module and sole owner of policy,
resource, citizen-interest, wetland, persistence, and event facts.

- `GameManager` is the application seam that translates player intent into existing validated commands.
- `PlayerCharacter` is an input/targeting Adapter; it must not decide authoritative harvest,
  contribution, or policy outcomes.
- `PrototypeHud`, `PrototypeHudPresenter`, `PrototypeHudTextBuilder`, `ResourceNode`, and
  `PrototypeSettlementScenePresenter` are presentation Adapters over read-only state/results.
- If multiple presentation Adapters need shared state, add one immutable, pure read projection instead
  of copying gameplay state into several controls. Keep the Interface capability-shaped: current goal,
  valid interaction, consequence, and feedback—not widget-shaped.
- Scenario variation should use the existing catalog and seed generation seam. Do not create a second
  randomization path or special-case world facts in presentation code.

This keeps high Leverage in the deterministic Module, makes the UI replaceable, and preserves Locality:
changes to presentation do not require changing simulation authority.

## ER-01 Acceptance

### Automated and code evidence

- Both curated profiles load from validated data and replay identically for the same command trace.
- Every harvest, contribution, and civic selection still uses the existing validated command/result path.
- Focus/rejection/success/depletion and HUD hierarchy have focused managed or Godot-hosted coverage.
- Existing persistence, civic-loop, wetland, and no-input continuation regressions remain green.
- The authoritative wrapper and production builds pass; performance safety is reported without
  relabeling a red budget as green.

### User-led play acceptance

The user runs both curated profiles without Computer Use automation. This gate passes only when the
user can answer yes to all of the following:

1. I knew what mattered and what I could do without reading a developer key list.
2. Harvesting and contributing felt like interactions with world objects, not invisible test calls.
3. The civic choice felt like my decision and both options communicated a real tradeoff.
4. I could tell why at least one citizen cared and what changed because of my choice.
5. The two starts felt meaningfully different, even though they used the same rules.
6. The HUD supported play instead of looking like a dense diagnostic overlay.
7. I would willingly try the contrasting choice or continue playing for another ten minutes.

Record failures verbatim. One narrow iteration within ER-01 is allowed. If the second assessment still
fails substantially, stop feature work and revise the loop or product premise; do not bury the result
under more systems.

## Development Trajectory After ER-01

| Gate | Purpose | Exit evidence | Expansion unlocked |
|---|---|---|---|
| **ER-01 First Believable Loop** | Make one existing loop embodied, legible, and intentionally variable | Automated authority/replay checks plus user acceptance of both profiles | ER-02 only |
| **ER-02 Living Settlement Agency** | Reduce the on-rails feeling through visible citizen priorities, work choices, refusal/blockage, and responses grounded in material state | Two citizens make distinguishable deterministic choices the user can explain | ER-03 only |
| **ER-03 Replayable Settlement Variety** | Broaden deterministic starting conditions, settlement shape, pressures, and visual identity without duplicating rules | Three or more curated profiles produce meaningfully different strategies and remain replayable | Sandbox breadth planning |
| **ER-04 Connected Sandbox Foundation** | Add one shallow connected embodied progression chain, such as homestead work to production to exchange/settlement participation | A sustained 20-30 minute loop with no developer-only step | Re-evaluate W3-06/W4 and Demo 1 scope |
| **Later cognition integration** | Enrich proven citizen decisions with bounded language/model capabilities | Deterministic fallback and proposal validation remain authoritative; separate provider authorization | Provider work, if explicitly approved |

The sequence deliberately moves from experience depth to breadth. New mechanics must either deepen
agency, increase readable systemic variation, or connect an existing isolated system into the playable
loop. A feature that does none of those should not enter the active plan.

## Cut Order and Stop Rules

Cut first:

1. decorative animation variety that does not improve state readability;
2. more than two starting profiles;
3. extra text/copy variants;
4. nonessential inspector detail; and
5. new content outside the chosen loop.

Never cut the deliberate choice surface, contextual interaction feedback, visible citizen reason,
meaningfully contrasting deterministic profile, user-led acceptance, or singular simulation authority.

Stop and return to planning if implementation requires a new authoritative state path, broad scenario
framework, general quest system, provider integration, or a product-direction decision about the
player's powers. Those are separate milestones.

## Practical Next Action

Start a clean isolated worktree from live `origin/master`, read this plan and `CURRENT_BUILD.md`, then
implement only **ER-01 First Believable Settlement Loop**. The primary checkout contains unrelated
dirty work and must remain untouched.
