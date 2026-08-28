# Societies Project Decision Log

Project-level decisions change product identity, authority, roadmap, or operating rules. Technical implementation decisions with narrower scope belong in `docs/adr/`.

## D-001 — Deterministic simulation owns reality

**Status:** Accepted  
World facts and every state-changing outcome belong to deterministic domain/runtime code. Humans, deterministic planners, and model-assisted citizens all act through validated commands and events. Presentation and inference cannot mutate authority directly.

## D-002 — LLMs are bounded cognition, not simulation authority

**Status:** Accepted  
LLMs may interpret citizen-known state, deliberate, communicate, negotiate, summarize bounded memory, and propose closed actions. Invalid, stale, late, or unavailable output resolves through typed rejection or deterministic fallback. Replay does not recall a model.

## D-003 — Snow Globe is the first complete miniature of Societies

**Status:** Accepted  
Snow Globe is not merely an isolated lab. The lab proves mechanisms; the Godot product must realize the full human/citizen/consequence loop at small scale before civilization-scale expansion.

## D-004 — The player is an embodied resident-founder

**Status:** Accepted 2026-08-26  
The player has hands, a home, a voice, and limited social standing—not omnipotent command authority. Influence comes from work, promises, persuasion, and outcomes. Citizens may counter, refuse, withdraw, or proceed.

## D-005 — First mature consequence is the wetland water-control commitment

**Status:** Accepted direction 2026-08-26  
The first complete proof centers on a failing causeway, ecological and shelter tradeoffs, Mara/Ivo/Sena, and a negotiated outcome that changes physical, resource, labor, trust, and next-day state.

## D-006 — Human acceptance is a required evidence class

**Status:** Accepted  
Automated correctness, screenshots, diagnostics, and agent review cannot establish player feel, visual coherence, citizen credibility, or desire to continue. The owner verdict is recorded exactly and may keep a mechanically green milestone incomplete.

## D-007 — EB-01R is the accepted bounded worldcraft baseline

**Status:** Accepted 2026-08-27  
Commit `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9` is the preserved source baseline. The owner accepted world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel. This does not accept performance, accessibility, release readiness, citizen integration, or the complete product.

## D-008 — One repository and one active milestone

**Status:** Accepted 2026-08-27  
The product, Snow Globe lab, cognition-quality work, evidence, research, and planning remain in `dfrer/societies` with explicit directory boundaries. Only `planning/active/MILESTONE.md` authorizes work. Historical plans are archived rather than allowed to compete.

## D-009 — Planning is the owner's primary development interface

**Status:** Accepted 2026-08-27  
The owner contributes mainly through product planning, critique, tradeoffs, and human acceptance. Agents translate that into technical packets, implementation, validation, and review. The owner is not expected to manage source-level decisions from message to message.

## D-010 — Execution agents do not own the roadmap

**Status:** Accepted 2026-08-27  
An agent may recommend follow-on work, but cannot activate it. The next task comes from the ordered missing evidence in an owner-accepted milestone. Adjacent infrastructure or feature expansion requires an explicit decision.

## D-011 — Consolidate before further feature work

**Status:** Active 2026-08-27  
Preserve and integrate the accepted stack, replace conflicting authority documents, archive historical active plans, add governance checks and scoped instructions, and close obsolete delivery paths before selecting the next product proof.

## D-012 — Do not perform a broad code rewrite during consolidation

**Status:** Accepted 2026-08-27  
The accepted runtime and lab code remain behaviorally unchanged during repository governance work. Large files and mixed responsibilities are recorded as debt. Refactor them only through bounded tasks with characterization and triggered validation.

## Open decision after consolidation

Select whether the next bounded proof prioritizes visual/product-target reconciliation, one participating citizen interface, performance/collision architecture, or a deliberately coupled minimum slice. The planning agent must inspect current code and evidence, explain tradeoffs, and recommend a product outcome without asking the owner to choose low-level implementation details.
