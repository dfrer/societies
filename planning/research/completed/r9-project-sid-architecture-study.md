# R9: Project Sid Architecture Study

## Decision summary

Project Sid is useful as a research pattern catalogue, not as adoptable software or an auditable reference implementation. Its strongest idea for Societies is a closed outcome-feedback loop:

`expected action result -> authoritative observed result or rejection -> next decision`

Snow Globe should adopt that pattern inside its existing versioned proposal/command boundary. Societies should not inherit Project Sid's shared mutable agent state, LLM-controlled institutions, fragile free-text parsing, live-provider dependence, or scale-first product claims.

## Evidence and adoption boundary

- The public repository at main commit [`757ea101c476a012085e5645393dc501ffed5ea4`](https://github.com/altera-al/project-sid/commit/757ea101c476a012085e5645393dc501ffed5ea4) contains only `README.md`, `2024-10-31.pdf`, and `visual_abstract.png`.
- The [initial commit](https://github.com/altera-al/project-sid/commit/f2eaa1882a5aff4330eff23fd0874d8c2a4b5a9c) added those same three files. There are no source files, tests, runtime configuration, datasets, tags, or [releases](https://github.com/altera-al/project-sid/releases).
- [Issue #1 requesting code](https://github.com/altera-al/project-sid/issues/1) remains open.
- The [paper](https://arxiv.org/html/2411.00114) is CC BY 4.0. The repository has no software or asset license.

Therefore the published ideas and paper may be studied and independently reimplemented with attribution. The repository does not grant permission to copy unpublished code or assume reuse rights for its assets. No implementation-level behavior, security property, scalability claim, or reproduction claim can be audited from the public repository.

## Paper-supported architecture

The [PIANO architecture](https://arxiv.org/html/2411.00114#S2) can be summarized as:

`Minecraft observations, chat, and outcomes -> concurrent multi-rate modules -> shared Agent State -> bottlenecked Cognitive Controller -> high-level intent -> talk, skill, and motor modules -> Minecraft -> action-awareness feedback`

Important characteristics:

- each agent nominally has ten concurrent modules operating at different rates;
- one cognitive controller makes high-level deliberate decisions from a filtered information bottleneck;
- the controller's intent conditions speech and action modules for behavioral coherence;
- memory, action awareness, goal generation, social awareness, talking, and skill execution are named modules, although not all ten are fully specified;
- action awareness compares expected and observed outcomes and showed better item acquisition in an ablation;
- Minecraft appears to own mechanics in practice, but the Minecraft version, server implementation, bot protocol, tick synchronization, navigation interface, and command validation boundary are unpublished.

The paper does not disclose a Project Sid persistence schema, replay system, deterministic tick/version contract, stale-result rejection, idempotence rule, offline fallback, provider retry/charge policy, or separation between agent belief and authoritative world fact.

## Evidence quality

The experiments are interesting exploratory evidence but are strongly scaffolded.

- Single-agent progression is the strongest result: 25 isolated agents, five repeats, architecture ablations, and expected-versus-observed feedback. Spawn location was a significant confound.
- Professional roles were classified after play by GPT-4o from generated goals. Agents also received seeded location memories and community objectives.
- Governance began with an existing constitution, a fixed tax, scheduled elections, explicit pro/anti-tax influencers, and a remote election manager. It demonstrates response to a supplied institution, not unprompted institutional emergence.
- The published [governance prompts](https://arxiv.org/html/2411.00114#S9.SS4) use delimiters and Python-list-shaped free text for amendment and vote processing. No canonical ballot, voter identity binding, deterministic tally, enactment authorization, replay, or injection boundary is disclosed.
- Religion was deliberately seeded with 20 evangelists and conversion was inferred from selected utterance keywords.
- Cultural analysis used one 500-agent run. Runs above 1,000 reportedly exceeded Minecraft-server capacity and became sporadically unresponsive, without published hardware, topology, queueing, backpressure, or cost evidence.
- The work does not publish a human-player benchmark proving that a person remains understandable and consequential inside the society.

## Societies compatibility

Project Sid's action awareness is post-action grounding, not pre-action authority. In Societies the compatible form is:

1. deterministic runtime publishes a bounded, knowledge-limited observation;
2. deterministic or LLM cognition returns a typed proposal bound to actor, tick, and observed version;
3. the runtime validates or rejects the proposal and alone mutates world state;
4. the resulting authoritative event or typed rejection becomes the next observation;
5. replay consumes recorded normalized proposals/results and never recalls a provider.

Agent memory, summaries, relationships, and beliefs remain non-authoritative projections with source, viewpoint, confidence, and observation time. The world/event log remains fact authority.

LLMs may propose laws, draft speech, and explain votes. Deterministic systems must own eligibility, identity, ballot recording, tally, enactment, enforcement, and consequences.

## Recommendation matrix

| Priority | Scope | Decision |
|---|---|---|
| P0 | Snow Globe now | Add or retain explicit expected-result, authoritative-result-or-rejection, and next-observation records. |
| P0 | Snow Globe now | Use frozen-scenario ablations with identical starting state, model, and rubric; score authoritative action traces rather than prose alone. |
| P0 | Snow Globe now | Enforce hard per-call, per-agent, and per-scenario time/call budgets; retain deterministic continuation on timeout or failure. |
| P1 | Main-compatible later | Separate slow cognition from fast deterministic reflex/fallback while rejecting stale proposals through a versioned scheduler. |
| P1 | Main-compatible later | Keep beliefs and social summaries distinct from the authoritative event log and preserve provenance. |
| P1 | Main-compatible later | Use one high-level intent to condition dialogue and animation without granting it mutation authority. |
| P1 | Main-compatible later | Permit model-authored institutional proposals and explanations while deterministic code owns every formal state transition. |
| P2 | Offline study | Test multi-rate cognition with 4-12 agents and measure latency, stale decisions, contradiction, cost, and player readability. |
| P2 | Offline study | Compare tiered summarized memory against exact event-derived retrieval for fact loss, hallucination propagation, privacy leakage, and replay divergence. |
| P2 | Offline study | Evaluate specialization using deterministic production/action traces; use LLM labels only as secondary qualitative analysis. |
| Reject | Both builds | Do not treat Project Sid as a dependency or claim code-level reproduction. |
| Reject | Both builds | Do not use shared mutable unversioned agent state, memory as world truth, or LLM authority over world facts and institutions. |
| Reject | Both builds | Do not use delimiter/list parsing, raw prompt interpolation, live-provider-only progression, or simulation ticks blocked on cognition. |
| Reject | Product planning | Do not make 500/1,000-agent counts a milestone before one human-consequential loop is believable. |

## Planning implication

No Project Sid integration should be added during EB-01 or EB-02. The P0 feedback-and-ablation patterns belong in the later bounded cognition lab and EB-03 acceptance design. Before any main-build integration, specify the versioned proposal envelope, stale-result rule, authoritative outcome feedback, belief-versus-fact separation, deterministic institutional pipeline, cost limits, and offline fallback.

## Sources

- [Project Sid repository](https://github.com/altera-al/project-sid)
- [Project Sid paper](https://arxiv.org/html/2411.00114)
- [Official Project Sid write-up](https://fundamentalresearchlabs.com/blog/project-sid)
- [Official Altera.AL to Fundamental Research Labs announcement](https://www.fundamentalresearchlabs.ai/blog/introducing-fundamental)

Research snapshot: 2026-08-27. No local or live provider execution was used.
