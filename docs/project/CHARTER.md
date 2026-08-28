# Societies Project Charter

## Canonical north star

> A deterministic civilization and ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences.

The purpose of Societies is not to display autonomous agents as a technical novelty. It is to create worlds in which understandable beings have material interests, limited knowledge, memory, relationships, obligations, and agency—and where humans live inside the same causal reality rather than commanding it from outside.

## Snow Globe's role

Snow Globe is the first bounded realization of the complete idea. It is deliberately small enough that the world, each citizen, each decision, and each consequence can be inspected and understood. It is not a throwaway prototype, a separate dashboard, or merely an inference laboratory. The laboratory under `labs/` proves difficult mechanisms; the Godot product under `src/societies/` must turn accepted mechanisms into one coherent lived world.

The full Societies vision may eventually contain larger populations, institutions, economies, ecology, governance, multiplayer, and long histories. None of that breadth is justified until Snow Globe proves the essential loop at miniature scale.

## Product pillars

### 1. One shared causal world

Resources, ecology, structures, time, work, commitments, policy, and consequences belong to the deterministic simulation. Human and citizen actions enter the same validated command and event system. No participant receives magical exceptions merely because they are controlled by a human, a deterministic planner, or an LLM.

### 2. Citizens are participants, not animated UI

A citizen must have bounded knowledge, recognizable interests, persistent identity, and the ability to support, counter, ask for terms, delay, refuse, withdraw labor, leave a role, or proceed without the player. Autonomy is not random obstruction: reasons must be legible and grounded in observed conditions, values, needs, trust, obligations, and remembered commitments.

### 3. The human is embodied and consequential

The player is a resident-founder, not an omnipotent administrator. The player can observe, listen, work, carry, build, repair, promise, propose, negotiate, and persuade. Influence is earned through contribution, credibility, relationships, and outcomes. The player cannot directly edit resources, minds, schedules, or hidden state.

### 4. Consequences persist

Success and failure alter the world, citizens, and next problem. Save, resume, and replay preserve authoritative outcomes. Failure should create recovery, compromise, loss, or a changed future—not a silent reset that erases social and ecological meaning.

### 5. Cognition serves the world

LLMs may interpret bounded observations, deliberate, communicate, negotiate, and summarize bounded memory. They return closed proposals and communication acts. They never own facts, mutate state, bypass validation, grant knowledge, or become hidden simulation policy. Deterministic fallback must preserve the same citizen identity and action vocabulary when inference is unavailable or rejected.

### 6. The experience must be readable and worth continuing

Technical correctness is necessary but insufficient. The player must be able to understand who is acting, why a disagreement exists, what changed, and why the next day matters. Visual identity, movement, interaction, sound, pacing, and human acceptance are product requirements, not optional polish.

## First complete product proof

The first mature Snow Globe proof is a fragile wetland settlement facing a failing causeway and a water-control decision. The embodied resident-founder encounters three citizens with materially different interests:

- **Mara**, protecting the reed nursery and long-term ecological supply;
- **Ivo**, prioritizing dry access and shelter repair before nightfall;
- **Sena**, tracking stock, labor bottlenecks, promises, and actual delivery.

The player can contribute, promise, propose, negotiate, accept a counter-offer, or refuse to lead. Citizens can disagree or act without the player. A validated outcome changes water, routes, structures, labor, resources, trust, and the next day's situation. Recorded/offline cognition and deterministic fallback must make the scenario fully playable before any governed live-model pilot is required.

This scenario may be refined through explicit planning, but its purpose is locked: prove one understandable social, material, and ecological consequence—not another collection of disconnected mechanics.

## Protected architecture invariants

1. Deterministic domain/runtime code is the sole world authority.
2. Presentation consumes projections and sends intents; it does not own canonical state.
3. Every world mutation is a validated command/event with deterministic ordering.
4. Persistence and replay reconstruct authority without recalling an LLM.
5. Model and provider details stay behind a provider-neutral interface and outside normal player presentation.
6. Invalid, stale, late, unavailable, or malformed inference fails safely and visibly to diagnostics.
7. Human product acceptance cannot be inferred from automated checks.
8. Broader systems do not activate until a bounded milestone proves their product value.

## What Societies is not

- not a chatbot placed beside a simulation;
- not an agent framework searching for a game;
- not a god-game in which autonomy disappears when inconvenient;
- not a provider-routing or persistence benchmark presented as product progress;
- not a roadmap where every interesting system must eventually be built;
- not a sequence of agent-generated milestones that the owner must supervise at source level.

## Change control

Changing the north star, player authority, citizen compact, deterministic authority, Snow Globe's role, or the first complete proof requires an explicit owner decision. Record the decision and rationale in `DECISION_LOG.md`, update the charter and active milestone together, and identify what prior work becomes superseded. Incidental implementation may not redefine the project.
