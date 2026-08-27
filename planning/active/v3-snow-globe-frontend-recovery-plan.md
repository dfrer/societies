# Snow Globe Frontend Product Recovery Plan

## Document Control

| Field | Value |
|---|---|
| Status | **Foundation Gate F0 accepted; tactile miniature selected; F1 replacement Candidate awaits review** |
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
2. Do not begin Golden Three gameplay production before Visual Gate F1 is accepted by the user. F1 may use
   disposable in-engine studies to prove its target frame.
3. Do not use headless tests, screenshots, or automated input as proof of gameplay or visual acceptance.
4. Preserve `PrototypeRuntimeSession`, validated commands/events, replay, and persistence as world authority.
5. Build and exercise the real provider-neutral cognition Interface from the first playable slice. Keep live
   providers, credentials, paid calls, and authenticated routing outside execution until the separately
   authorized live pilot.
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

**Status:** direction accepted by the user on 2026-08-26. The brief below is now the production foundation.

#### Player fantasy

> You are an embodied resident-founder helping a fragile wetland settlement survive its first hard season;
> you can work, promise, persuade, and propose, but the settlement's citizens decide what they will risk with
> you.

The player begins with a home, a vote, a pair of hands, and enough social standing to call a discussion—not
an office that grants command authority. Influence is earned through visible contribution, kept commitments,
materially grounded arguments, and outcomes citizens can judge. It is risked through broken promises,
one-sided sacrifice, resource waste, and hiding consequences.

#### First settlement situation

The settlement's flood-control causeway is failing before nightfall. Repairing it quickly requires dry access
and concentrated labor, but drawing the wetland down damages the reed nursery and future food/material
supply. Protecting the wetland preserves future resilience but leaves less time and fewer dry routes to
stabilize shelter. This is not a menu-level morality test: water level, routes, available materials, work
assignments, citizen trust, and the next morning's settlement condition must all respond visibly.

The smallest full-product consequence is therefore one negotiated water-control commitment with two viable
approaches and no universally correct answer. The player may advocate, bargain, delay to gather more material,
or refuse to lead. Citizens may support, counter-propose, refuse a burden, or proceed without the player. The
validated deterministic command establishes the world outcome; Snow Globe helps citizens reason about and
communicate their position.

#### Player verbs and limits

The five primary verbs for the first slice are:

1. **Observe and listen** — read material conditions, ongoing work, and citizen concerns in the world.
2. **Work and carry** — harvest, move, transform, and contribute real resources.
3. **Build and repair** — change one authored structure through embodied labor and authoritative commands.
4. **Commit and propose** — make a promise or put a bounded settlement action before affected citizens.
5. **Negotiate and persuade** — offer evidence, labor, timing, or compromise; accept that another citizen may
   disagree.

The player cannot directly command a citizen's mind or schedule, conjure or edit simulation resources, or
access omniscient developer-state controls. Debug information remains an optional diagnostic layer outside
the normal experience.

#### Citizen compact

- Citizens cooperate when the proposal fits their perceived needs, values, trust, obligations, and visible
  material evidence—not because the player activated them.
- Citizens can ask for terms, name a concern, counter-propose, delay, refuse, withdraw labor, or leave a role.
- A refusal must have an observable cause and a recoverable path; arbitrary obstruction is not autonomy.
- The same citizen should remain recognizable across fallback, recorded-response, and live-model paths.
- Citizens know only bounded observations and recorded commitments. They are not omniscient narrators.
- Language can reveal reasoning and create negotiation, but cannot invent inventory, alter needs, or commit a
  world action.

#### Success, failure, recovery, and continuation

- **Slice success:** the player helps the settlement reach morning with a legible water-control outcome, a
  changed physical space, and at least one citizen relationship strengthened or strained by what happened.
- **Slice failure:** the causeway or shelter condition worsens, a citizen withdraws labor, or a commitment is
  broken. Failure changes the next problem; it does not silently reset or end in a generic game-over screen.
- **Recovery:** the player can repair damage, replace lost material, renegotiate a commitment, or accept a
  smaller settlement outcome at a real cost.
- **Continuation:** the resulting water, resource, structure, and trust state creates the next day's work and
  a reason to keep living in the settlement.

#### First fifteen minutes — state-reactive beat sheet

This is a set of discoverable beats, not a forced sequence. The first three meaningful actions may be taken
in any order, and the later discussion must reflect what the player actually observed and contributed.

| Time | Player experience | Required state response |
|---|---|---|
| 0:00-2:00 | Arrive through the wetland edge, hear labor before seeing the depot, and notice the damaged causeway, high-water cue, and three citizens already working. | No tutorial modal. Camera, sound, composition, and one contextual prompt establish scale and urgency. |
| 2:00-5:00 | Choose among inspecting the breach with Mara, helping Ivo move repair material, or checking the depot shortage with Sena. | Each route reveals different evidence and changes a small authoritative state; no route is a fake choice. |
| 5:00-8:00 | Complete one tactile work contribution or make a concrete promise about the missing material. | The resource, structure, citizen animation, audio, and concise feedback all acknowledge the exact command result. |
| 8:00-11:00 | Encounter the disagreement: preserve the reed nursery and accept slower repairs, draw down water for access, or delay while seeking a better contribution. | Citizens explain different material interests; prior help, delay, and promises affect willingness and available terms. |
| 11:00-14:00 | Propose, negotiate, accept a counter-offer, or refuse to lead. | Snow Globe uses the real cognition Interface; the runtime validates any proposal and preserves a deterministic fallback. |
| 14:00-15:00 | Watch citizens act and the settlement physically change. | Water/route/structure state, work behavior, trust, and the next need reflect the accepted outcome and remain after save/replay. |

The first cast is deliberately small:

- **Mara, wetland keeper:** protects the reed nursery and future food/material supply; will accept short-term
  risk if the ecological cost is bounded and repaired.
- **Ivo, builder:** prioritizes dry access and shelter before nightfall; will refuse a plan that leaves the
  repair crew exposed without a credible alternative.
- **Sena, storekeeper-hauler:** sees stock, promises, and labor bottlenecks; supports the plan whose claimed
  inputs actually exist and remembers who delivered what they promised.

These names and biographies may be refined during F1 voice/art direction, but their conflicting material
interests and autonomy are locked for the slice.

**Exit gate:** passed for direction. F1 may refine expression, but changing player authority, citizen autonomy,
or the central water-control consequence requires an explicit decision update rather than incidental
implementation drift.

## Snow Globe Cognition Is a Cross-Cutting Product Track

LLM integration is part of this recovery from the beginning. It is not a chat window added after the game
works, and it is not permission to let model output become simulation truth. Each phase must advance the same
small experience-cognition Interface while remaining playable through deterministic fallback.

### Module, Interface, and Adapter boundary

The existing `PrototypeCognitionModule` proves strict observation, proposal, validation, event, and fallback
ownership for the civic prototype. Its closed actions and 64-character canonical summary are intentionally
too narrow for the new embodied conversation. The recovery must add a versioned experience-cognition contract
rather than weakening or silently changing that validated contract.

The intended boundary is:

```text
Authoritative settlement Module
  publishes a bounded citizen-known observation and allowed proposal vocabulary
                         |
                         v
Citizen experience-cognition Interface
  one request -> one cancellation-bounded receipt
                         |
            +------------+------------+
            |                         |
            v                         v
Recorded/offline Adapter       Governed live Snow Globe Adapter
first production path          separately authorized pilot
            |                         |
            +------------+------------+
                         v
Authoritative validator
  accepts or rejects the closed proposal; records the communication receipt;
  only validated commands/events may change the world
```

The Godot presentation Adapter receives a sanitized experience projection. It never receives credentials,
provider routes, raw responses, billing state, or a mutable copy of citizen/simulation state. Snow Globe
provider Modules remain behind the Interface and must not become Godot dependencies.

### Request and response shape

Each request contains only the information the specific citizen can reasonably know:

- citizen identity and stable traits relevant to the situation;
- current need, role, work, trust, obligation, and commitment bands;
- bounded visible settlement facts and recent authoritative events;
- the player's exact proposal, contribution, promise, or question in a closed interaction context;
- the small set of proposals that the runtime is currently willing to validate; and
- digests/identities needed to reject stale or mismatched output.

Each receipt has two deliberately separate channels:

1. **Action proposal:** a closed structured intent such as support, oppose, counter, ask for evidence, defer,
   refuse work, or accept with a bounded term. This may affect the world only after runtime validation.
2. **Communication act:** a bounded utterance plus stance, addressed subject, and cited observation/commitment
   identifiers. It can explain, question, warn, remember, or negotiate, but it cannot declare new world facts.

Provider/model identity, attempt disposition, latency, and fallback source are retained in diagnostics and
evidence. Normal player presentation communicates hesitation, refusal, confidence, or interruption through
performance and concise language—not technical provider status.

### Memory and replay rules

- Authoritative memory is a bounded ledger of observed world events, interpersonal commitments, fulfillment,
  breach, and disclosed facts. The simulation owns that ledger.
- A model-generated memory summary is advisory, content-bounded, provenance-bound, and replaceable. It cannot
  supersede the underlying events or grant a citizen knowledge they never observed.
- A fresh run may call an Adapter at a defined high-value moment. Replay and resume use the recorded normalized
  receipt and validation outcome; they do not call a model again.
- Stale observation, malformed output, unknown actions, invented evidence references, timeout, cancellation,
  and unavailability all resolve through typed rejection or deterministic fallback.
- Recorded and fallback paths must produce the same authoritative invariants even when their wording differs.

### Interaction and latency rules

Citizen cognition must feel embodied rather than like opening a chatbot:

- approach, gaze, work interruption, posture, and spatial context establish who is speaking and why;
- the player chooses from context-grounded intent, evidence, promise, or question surfaces rather than typing
  an unrestricted prompt in the first slice;
- the citizen acknowledges the player immediately through animation/audio while deliberation is pending;
- simulation and camera remain responsive; no provider call blocks a world tick or holds an authority lock;
- late output is discarded if the observation is stale;
- the deterministic response is always available and must still sound like the same citizen; and
- accessibility includes subtitles, readable speaker/stance cues, controllable text timing, and non-audio
  indication of interruption or refusal.

Candidate latency targets for F4 are immediate local acknowledgement within one rendered frame, a useful
local-model response target below 2.5 seconds, a premium response target below 5 seconds, and a hard bounded
timeout no greater than 6 seconds. These are experience targets to test, not current performance claims.

### Phase-by-phase cognition delivery

| Phase | Cognition result | Provider boundary |
|---|---|---|
| F1 | Design the embodied conversation language, disclosure rules, pending/fallback presentation, and three-citizen voice constraints. | No inference execution. |
| F2 | Implement the real gameplay-facing Interface and exercise one citizen acknowledgement/request through deterministic and recorded-response Adapters. | Offline and recorded only. |
| F3 | Use that Interface for the water-control disagreement, one counter-offer/refusal, bounded commitment memory, exact receipt replay, and state-reactive wording. | Offline/recorded production path; no provider dependency. |
| F4 | Run one governed live local-model pilot and, only if separately authorized with cost/security limits, one premium comparison through the same Interface. | Live execution is explicit and bounded. |
| F5 | Extend accepted cognition to all three citizens, one short memory horizon, and the second starting situation. | Only accepted Adapters; no uncontrolled call volume. |
| F6 | Validate fallback, replay, cancellation, stale output, latency, privacy, cost evidence, and user preference over deterministic-only presentation. | No release claim without explicit evidence. |

### LLM quality gate

The live or recorded language path passes only if the user can identify value beyond prettier prose. For each
evaluated moment, record whether the response is:

1. grounded in facts the citizen could know;
2. recognizably consistent with that citizen's interests and prior commitments;
3. concise enough to preserve play rhythm;
4. actionable—creating understanding, a term, a warning, or a meaningful choice;
5. restrained from inventing authority or facts; and
6. materially preferred over the deterministic fallback.

Any hallucinated fact, personality collapse, repetitive exposition, ungrounded refusal, or apparent direct
world mutation is a fail. If the model adds only flavor text, keep the Interface and fallback but do not pay
the latency, operational, or provider cost.

### F1 — Visual and Interaction Target Package

**Timebox:** 4-6 working days after F0.

**Current state:** the first [F1 Candidate package](v3-snow-globe-f1-visual-target-package.md) was rejected by
the user as far too realistic. Tactile clay-and-wood miniature is now the selected dominant language. Three
replacement treatments—Hearthwood Causeway, Reed-Kiln Wetlands, and Painted Sluice Toyworks—compare warmth,
wetland craft, and causal-mechanical readability inside that shared language. They remain unaccepted candidate
evidence; the user must run the replacement study and choose a base treatment before the complete five-view
target package or F2 begins.

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
- embodied conversation studies for listening, deliberation-pending, counter-offer, refusal, fallback, and
  interruption without a generic chat panel;
- a deliberately tiny vertical-slice asset kit and technical budgets;
- accessibility targets for contrast, type size, motion reduction, and non-color-only states.

The initial kit is intentionally small: one terrain/wetland set, one depot, two structures, three resource
families, three citizen silhouettes, one tool, one held resource, one civic gathering point, and the effects
and audio needed for the slice. Every asset must serve interaction or state readability.

**Exit gate:** the user accepts one visual direction and an in-engine target frame at normal play resolution.
Concept art alone does not unlock F2.

### F2 — Golden Three Minutes

**Timebox:** 6-8 working days.

Build a new Godot experience scene over the current deterministic runtime. Do not patch the old main scene
into this shape. The scene must prove:

- a deliberate arrival and first sightline;
- character scale, movement, camera, lighting, atmosphere, and sound;
- one tactile resource interaction with anticipation, contact, result, and world-state change;
- one citizen visibly doing work or responding to conditions;
- one short citizen acknowledgement or grounded request through the real experience-cognition Interface,
  exercised with deterministic and recorded-response Adapters;
- one authored settlement landmark and one readable ecological cue;
- minimal HUD that disappears when it has nothing useful to say.

No civic-policy menu, profile switcher, broad settlement system, unrestricted dialogue, or live provider is
required here. The point is to establish feel, production quality, and the embodied cognition Seam before
rebuilding the longer loop.

**Exit gate:** the user plays the three-minute route and rates visual identity, movement/camera, interaction
feel, world coherence, and desire to continue at least 4/5, with no category below 3/5.

### F3 — Golden Fifteen-Minute Vertical Slice

**Timebox:** 10-12 working days after F2 acceptance.

Build one coherent settlement situation with:

- one settlement and one ecological zone;
- three named citizens with distinct material interests and visible work;
- one short production chain with embodied collection, transformation, delivery, and use;
- at least three viable first actions, so the route is not a disguised tutorial rail;
- one request the player may accept, negotiate, delay, or refuse;
- one citizen who can disagree or refuse for a readable reason;
- one bounded promise/commitment ledger and one state-reactive counter-offer through the cognition Interface;
- one shared decision whose consequences alter citizen behavior and the physical world;
- one persistent changed state and a clear reason to continue.

The slice should use existing deterministic systems where they support the accepted fantasy. Existing
mechanics that exist only because they were previously implemented do not automatically earn a place.

**Exit gate:** without facilitation, the user can explain who they are, what they chose, why citizens reacted,
what changed, and what they want to do next. Agency, interaction quality, causal clarity, citizen presence,
visual identity, HUD hierarchy, and desire to continue must each score at least 4/5.

### F4 — Governed Live Snow Globe Pilot

**Timebox:** 5-7 working days after F3 acceptance.

Take the already-playable, offline-proven citizen moment through the governed Snow Globe Adapter:

- one frozen interaction corpus drawn from the accepted vertical slice;
- one bounded local-model run through the exact production Interface;
- one deterministic-only versus recorded versus live experience comparison;
- cancellation, timeout, stale-response, malformed-output, privacy, and no-hidden-retry evidence;
- exact replay from the recorded normalized receipt without a second call; and
- a premium comparison only if separately authorized with an explicit model revision, call limit, cost ceiling,
  credential boundary, retry/charge classification, and cleanup proof.

The local and premium Adapters must implement the same Interface already exercised in F2/F3. This milestone
does not create a provider-specific UI or a second state path. Language enriches an already-readable citizen;
it does not compensate for missing simulation behavior or world presentation.

**Exit gate:** the citizen's communication is grounded in visible material state, adds a meaningful choice or
understanding, fails safely offline, and is preferred by the user over the deterministic-only presentation.

### F5 — Meaningful Variation and Settlement Depth

**Timebox:** 8-10 working days.

Only after F4 passes, add a second starting situation and deepen replayability:

- different settlement pressure and spatial composition;
- different citizen conflict and resource approach;
- at least one different viable strategy, not merely different copy or colors;
- cognition across all three citizens with a bounded short memory horizon and controlled invocation budget;
- shared rules and exact replay;
- no increase in system breadth unless it produces a new player decision.

**Exit gate:** the user can describe a different strategy and social/ecological tension for each start, and
willingly replays one to test another choice.

### F6 — Acceptance, Performance, and Delivery

**Timebox:** 4-6 working days.

- resolve or explicitly disposition the canonical performance safety failure;
- run focused tests, full authoritative validation, replay/resume, Release/ExportRelease, and required CI;
- validate offline, recorded, local-live, and any authorized premium path without allowing provider
  availability to determine whether the simulation can continue;
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
| F0 Product Foundation | Accepted resident-founder fantasy, stakes, citizen compact, and first-15-minute brief | Complete | Accepted 2026-08-26 |
| F1 Visual Target | Accepted art/UX/audio/conversation language and in-engine target frame | 4-6 days | Visual direction acceptance |
| F2 Golden Three | New scene proves movement, atmosphere, interaction, world identity, and the real offline cognition Interface | 6-8 days | Playable feel score |
| F3 Golden Fifteen | Non-rail settlement slice with negotiation, commitment memory, and persistent consequence | 10-12 days | Full product scorecard |
| F4 Governed Live Pilot | Accepted citizen moment exercised with a bounded live Snow Globe Adapter | 5-7 days | Adds value over fallback |
| F5 Variation | Second genuinely different situation, strategy, and bounded multi-citizen cognition | 8-10 days | Meaningful replay contrast |
| F6 Hardening | Performance, validation, provider/fallback evidence, CI, and final user acceptance | 4-6 days | Go/narrow-rework/stop |

Expected path from F1: roughly 7-10 focused working weeks, depending on user review turnaround, asset
production, and whether a live-provider comparison is authorized.
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
10. desire to continue;
11. citizen dialogue groundedness and distinctiveness; and
12. whether Snow Globe creates meaningful choice or understanding beyond deterministic fallback.

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
4. premium-provider comparison after the local governed pilot;
5. general markets, law, multiplayer, long-term semantic memory, and relationship-system breadth.

Never cut:

- the accepted player fantasy and stakes;
- at least three viable first actions in the Golden Fifteen;
- tactile interaction and audiovisual feedback;
- visible citizen work, disagreement, or refusal;
- the physical shared consequence;
- the accepted visual target;
- deterministic authority, validated commands, replay, and offline fallback; or
- the provider-neutral experience-cognition Interface and one bounded, grounded citizen exchange; or
- user-led quality acceptance.

## Stop Rules

- If the accepted F0 fantasy later fails play, reopen it explicitly; do not mutate it through incidental
  implementation choices.
- If F1 has no accepted in-engine target, do not build the vertical slice.
- If F2 fails twice on feel or visual identity, stop and replace the presentation approach rather than adding
  gameplay systems.
- If F3 still feels on rails, remove scripted sequencing and improve choice/state response before F4.
- If Snow Globe communication is not better than deterministic presentation, preserve the Interface and
  recorded/fallback path but stop live-provider expansion until the product interaction is redesigned.
- If performance safety remains red, diagnose it before breadth; do not relabel a failed run as green.

## Immediate Next Action

Run the [tactile miniature F1 comparison route](v3-snow-globe-f1-visual-target-package.md#standalone-review-route),
compare Hearthwood Causeway, Reed-Kiln Wetlands, and Painted Sluice Toyworks in open, pending, refusal,
consequence, and reduced-motion states, and select one base treatment or a precise bounded blend. The selected
treatment then receives the complete five-view package and normal-play in-engine target needed to pass F1.
