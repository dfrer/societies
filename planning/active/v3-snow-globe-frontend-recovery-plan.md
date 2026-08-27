# Snow Globe Frontend Product Recovery Plan

## Document Control

| Field | Value |
|---|---|
| Status | **Active planning reset; implementation blocked behind Foundation Gate F0** |
| Decision date | 2026-08-26 |
| Trigger | ER-01 and its HUD/interaction recovery remained unacceptable in quality, gameplay, visuals, and overall experience |
| Product relationship | **Societies is the embodied player-facing frontend for Snow Globe** |
| Current executable | Godot project under `src/societies/`; reusable deterministic infrastructure, not an accepted presentation baseline |
| Rejected delivery | Draft PR #177; keep unmerged and do not use as the next implementation base |
| Supersedes | `v3-experience-recovery-plan.md`, ER-01, W3-06+, and the former Week 4 path |

## Reset Decision

The previous recovery strategy failed. It improved controls and HUD organization inside the existing
prototype, but did not materially refine the product concept or establish a credible quality bar. The
result still reads as a developer simulation scene rather than a cohesive Snow Globe experience.

This is not a backlog of polish defects. It is a foundation problem:

- the player identity, authority, risk, and relationship to citizens are still unresolved P0 questions;
- the playable route is authored around proving simulation commands rather than expressing a player fantasy;
- the world has no production asset layer: the tracked Godot project currently contains one image
  (the application icon), no audio files, and no imported 3D models;
- the main scene and most world objects are assembled procedurally as validation-friendly placeholders;
- Snow Globe and Societies have been advanced as separate technical tracks without an accountable
  product-integration sequence; and
- automated correctness repeatedly outpaced human judgments of agency, interaction quality, visual
  identity, causal clarity, and desire to continue.

The project therefore stops patching ER-01. The next work defines the product, establishes an accepted
visual and interaction target, then builds a new vertical-slice presentation shell over the proven
deterministic runtime.

## Product Relationship

Societies is the embodied game client through which a player experiences Snow Globe. Snow Globe is the
bounded cognition, deliberation, memory-summary, and provider-orchestration substrate for citizens. The
deterministic simulation remains the sole authority over world facts and outcomes.

```text
Godot player client
  presentation, input, camera, interaction, audiovisual feedback
            |
            | intents and immutable read projections
            v
PrototypeRuntimeSession
  sole authority for world state, commands, events, replay, persistence
            |
            | bounded citizen observations and constrained proposals
            v
Snow Globe cognition Module
  deliberation and communication; provider-neutral Adapter
  recorded/offline Adapter first, live provider only under separate authorization
```

The Interface between these Modules must stay small:

- the client receives immutable experience projections and authoritative command results;
- `GameManager` translates player intent into existing validated commands;
- Snow Globe receives bounded read-only citizen observations and returns constrained proposals;
- `PrototypeRuntimeSession` validates every proposal before any mutation;
- provider identities, credentials, routing, raw responses, and payment state never enter the Godot UI;
- the old prototype scene remains a regression harness until its replacement passes acceptance, then is
  archived or removed rather than layered underneath the new experience.

## Non-Negotiable Recovery Rules

1. Do not merge draft PR #177 or continue HUD-only refinement on its presentation.
2. Do not implement another feature before Foundation Gate F0 and Visual Gate F1 are accepted by the user.
3. Do not use headless tests, screenshots, or automated input as proof of gameplay or visual acceptance.
4. Preserve `PrototypeRuntimeSession`, validated commands/events, replay, and persistence as world authority.
5. Keep live providers, credentials, paid calls, and provider routing outside implementation until a
   separately authorized Snow Globe integration milestone.
6. Treat performance safety as red until a clean canonical run passes or the result is explicitly
   dispositioned. The rejected recovery revision recorded reference median p95 `51.9392 ms` against the
   `50 ms` safety line.
7. Prefer replacing shallow presentation code with one coherent experience shell over adding more
   conditional panels, debug affordances, or tutorial text.

## Quality North Star

The first credible slice should feel like entering a weathered, functioning ecological settlement where
every object suggests labor, scarcity, stewardship, and social consequence. The recommended visual
candidate is **weathered ecological futurism**: handcrafted blocky forms, wetland materiality, practical
settlement construction, strong silhouettes, localized warm civic light against cool natural atmosphere,
and interfaces that feel like instruments or field records rather than software dashboards.

That is a Candidate direction, not an accepted art bible. F1 must compare it with at least two materially
different alternatives before the user locks the final direction. The memorable product signature should
be the contrast between a living ecological world and the visible traces of human/citizen decisions—not a
generic survival-game HUD or a generic AI chat window.

## Recovery Sequence

### F0 — Product Foundation Lock

**Timebox:** 2-3 working days, including one user decision session.

Resolve the four P0 questions before production work:

1. Who is the player in the society?
2. What power do they begin with, earn, and risk losing?
3. Why do citizens cooperate, negotiate, refuse, or leave?
4. What smallest shared consequence proves the Snow Globe promise?

Recommended starting Candidate: the player is an embodied resident-founder with limited formal power who
earns influence by contributing, keeping commitments, and persuading citizens with their own material
interests. This avoids both the powerless laborer fantasy and the omnipotent city-manager fantasy, but it
must be explicitly accepted, revised, or rejected by the user.

Deliverables:

- one-sentence player fantasy;
- one-page experience brief;
- first-15-minutes beat sheet;
- five primary player verbs and three explicitly unavailable powers;
- citizen cooperation/refusal compact;
- success, failure, recovery, and continuation definitions;
- a revised Snow Globe/client capability map.

**Exit gate:** the user explicitly accepts the player fantasy, stakes, citizen relationship, and smallest
shared consequence. Until then, no scene, HUD, asset, or gameplay implementation begins.

### F1 — Visual and Interaction Target Package

**Timebox:** 3-5 working days after F0.

Produce three meaningfully different visual directions. Each must show the same accepted gameplay moment
from gameplay camera, close interaction, settlement overview, citizen encounter, and decision/consequence
views. At least one direction should develop the recommended weathered ecological futurism; the others must
not be palette swaps.

The selected package contains:

- five approved style frames and an in-engine target frame;
- camera, field of view, scale, and movement-feel target;
- terrain, water, foliage, structure, prop, citizen, effect, and lighting language;
- typography, iconography, HUD composition, and interaction-prompt language;
- animation and motion principles for focus, impact, work, refusal, and consequence;
- ambient, interaction, citizen, and consequence audio direction;
- a deliberately tiny vertical-slice asset kit and technical budgets;
- accessibility targets for contrast, type size, motion reduction, and non-color-only states.

The initial kit is intentionally small: one terrain/wetland set, one depot, two structures, three resource
families, three citizen silhouettes, one tool, one held resource, one civic gathering point, and the effects
and audio needed for the slice. Every asset must serve interaction or state readability.

**Exit gate:** the user accepts one visual direction and an in-engine target frame at normal play resolution.
Concept art alone does not unlock F2.

### F2 — Golden Three Minutes

**Timebox:** 5-7 working days.

Build a new Godot experience scene over the current deterministic runtime. Do not patch the old main scene
into this shape. The scene must prove:

- a deliberate arrival and first sightline;
- character scale, movement, camera, lighting, atmosphere, and sound;
- one tactile resource interaction with anticipation, contact, result, and world-state change;
- one citizen visibly doing work or responding to conditions;
- one authored settlement landmark and one readable ecological cue;
- minimal HUD that disappears when it has nothing useful to say.

No civic policy, profile switcher, diagnostics, live cognition, broad settlement systems, or tutorial chain
is required here. The point is to establish feel and production quality before rebuilding the longer loop.

**Exit gate:** the user plays the three-minute route and rates visual identity, movement/camera, interaction
feel, world coherence, and desire to continue at least 4/5, with no category below 3/5.

### F3 — Golden Fifteen-Minute Vertical Slice

**Timebox:** 10 working days after F2 acceptance.

Build one coherent settlement situation with:

- one settlement and one ecological zone;
- three named citizens with distinct material interests and visible work;
- one short production chain with embodied collection, transformation, delivery, and use;
- at least three viable first actions, so the route is not a disguised tutorial rail;
- one request the player may accept, negotiate, delay, or refuse;
- one citizen who can disagree or refuse for a readable reason;
- one shared decision whose consequences alter citizen behavior and the physical world;
- one persistent changed state and a clear reason to continue.

The slice should use existing deterministic systems where they support the accepted fantasy. Existing
mechanics that exist only because they were previously implemented do not automatically earn a place.

**Exit gate:** without facilitation, the user can explain who they are, what they chose, why citizens reacted,
what changed, and what they want to do next. Agency, interaction quality, causal clarity, citizen presence,
visual identity, HUD hierarchy, and desire to continue must each score at least 4/5.

### F4 — Snow Globe Embodied Citizen

**Timebox:** 5-7 working days after F3 acceptance.

Connect one citizen moment to Snow Globe through a real narrow seam:

- immutable structured observation from the accepted vertical slice;
- one bounded deliberation/communication proposal vocabulary;
- deterministic validation, rejection, timeout, and fallback;
- visible source-appropriate presentation without exposing provider mechanics;
- exact replay for deterministic fallback and recorded-response Adapter paths.

Use a recorded or offline Adapter first. A live local or premium provider run requires separate explicit
authorization, security review, cost limits, and failure/retry rules. Language enriches an already-readable
citizen; it does not compensate for missing simulation behavior or world presentation.

**Exit gate:** the citizen's communication is grounded in visible material state, adds a meaningful choice or
understanding, fails safely offline, and is preferred by the user over the deterministic-only presentation.

### F5 — Meaningful Variation and Settlement Depth

**Timebox:** 8-10 working days.

Only after F4 passes, add a second starting situation and deepen replayability:

- different settlement pressure and spatial composition;
- different citizen conflict and resource approach;
- at least one different viable strategy, not merely different copy or colors;
- shared rules and exact replay;
- no increase in system breadth unless it produces a new player decision.

**Exit gate:** the user can describe a different strategy and social/ecological tension for each start, and
willingly replays one to test another choice.

### F6 — Acceptance, Performance, and Delivery

**Timebox:** 3-5 working days.

- resolve or explicitly disposition the canonical performance safety failure;
- run focused tests, full authoritative validation, replay/resume, Release/ExportRelease, and required CI;
- run the user-led visual/gameplay acceptance route on both starts;
- fix only defects against the accepted slice; do not add feature breadth;
- publish a go, narrow-rework, or stop decision with evidence separated by technical, visual, gameplay, and
  provider status.

**Exit gate:** technical gates are green, performance safety is green or explicitly accepted as a named risk,
and the user accepts the product scorecard. Only then may the roadmap consider broader economy, governance,
multiplayer, or content expansion.

## Milestone Summary

| Milestone | Primary result | Typical duration | Human gate |
|---|---|---:|---|
| F0 Product Foundation | Player fantasy, stakes, citizen compact, first-15-minute brief | 2-3 days | Explicit direction acceptance |
| F1 Visual Target | Accepted art/UX/audio language and in-engine target frame | 3-5 days | Visual direction acceptance |
| F2 Golden Three | New scene proves movement, atmosphere, interaction, and world identity | 5-7 days | Playable feel score |
| F3 Golden Fifteen | Non-rail settlement vertical slice with persistent consequence | 10 days | Full product scorecard |
| F4 Embodied Citizen | One bounded Snow Globe-enhanced citizen moment | 5-7 days | Adds value over fallback |
| F5 Variation | Second genuinely different situation and strategy | 8-10 days | Meaningful replay contrast |
| F6 Hardening | Performance, validation, CI, and final user acceptance | 3-5 days | Go/narrow-rework/stop |

Expected path: roughly 6-8 focused working weeks, depending on user review turnaround and asset production.
This is a quality-driven estimate, not a release promise.

## Product Scorecard

Every playable gate records a 1-5 user score and one sentence of evidence for:

1. player identity and stakes;
2. agency and available choices;
3. movement, camera, and interaction feel;
4. citizen presence and autonomy;
5. causal clarity;
6. visual identity and world coherence;
7. HUD hierarchy and information timing;
8. audio and feedback cohesion;
9. meaningful variation; and
10. desire to continue.

F2 uses the five categories named in its gate. F3 and later require all applicable categories at 4/5 or
higher. Averages cannot hide a category below threshold. The user's verdict is authoritative and recorded
verbatim; automated checks cannot promote a failed human result.

## Production Discipline

- Build the new presentation shell in a fresh isolated branch from the accepted planning base, not from PR
  #177 and not in the dirty primary checkout.
- Keep one tightly coupled vertical-slice implementation owner. Add separate art/UX or review ownership only
  where files and responsibilities do not overlap.
- Review the playable route at gameplay camera, close interaction, citizen encounter, settlement overview,
  consequence, and required accessibility views—not only the spawn frame.
- Commit target packages, source assets, import settings, scene changes, and evidence with clear provenance.
- Prefer a small authored composition and reusable visual grammar over procedural placeholder breadth.
- Maintain an explicit asset list, source/license record, collision/LOD/import contract, and performance budget.
- Do not call a slice playable, polished, or accepted until the user has played it.

## Cut Order

Cut first:

1. extra biomes, profiles, buildings, citizens, and resource families;
2. dynamic weather breadth and large-map traversal;
3. optional overlays, diagnostics, and secondary HUD views;
4. live provider integration;
5. general markets, law, multiplayer, memory, and relationship systems.

Never cut:

- the accepted player fantasy and stakes;
- at least three viable first actions in the Golden Fifteen;
- tactile interaction and audiovisual feedback;
- visible citizen work, disagreement, or refusal;
- the physical shared consequence;
- the accepted visual target;
- deterministic authority, validated commands, replay, and offline fallback; or
- user-led quality acceptance.

## Stop Rules

- If F0 cannot produce an accepted player fantasy, stop implementation and continue concept work.
- If F1 has no accepted in-engine target, do not build the vertical slice.
- If F2 fails twice on feel or visual identity, stop and replace the presentation approach rather than adding
  gameplay systems.
- If F3 still feels on rails, remove scripted sequencing and improve choice/state response before F4.
- If Snow Globe communication is not better than deterministic presentation, keep the offline fallback and
  defer model integration.
- If performance safety remains red, diagnose it before breadth; do not relabel a failed run as green.

## Immediate Next Action

Run the F0 Product Foundation session. Resolve the player role, source and limits of power, reasons citizens
cooperate or refuse, and the smallest shared consequence. Record the accepted answers in `DECISIONS.md`, then
produce the one-page experience brief and first-15-minutes beat sheet. No implementation begins before that
acceptance.
