# Societies Concept Studio

## Purpose

The Concept Studio is the persistent design track for progressively discovering and specifying the Societies experience the user actually wants built.

It supports, but does not compete with, active development. It turns interviews into plain-English concepts, deterministic pseudocode, explicit decisions, open questions, system specifications, weekly handoffs, and a complete successor two-week sprint draft before the current sprint ends.

## Authority Boundary

Use this order when documents disagree:

1. Code and [CURRENT_BUILD.md](../../CURRENT_BUILD.md) define implemented reality.
2. [planning/active/](../active/README.md) defines currently authorized development.
3. [PRODUCT-THESIS.md](../PRODUCT-THESIS.md) defines the canonical product boundary.
4. Accepted decisions in [DECISIONS.md](DECISIONS.md) define approved concept direction.
5. Candidate and Exploratory concept material remains non-authoritative.

Concept work never authorizes implementation by itself. The Concept Studio is responsible for preparing the successor sprint, but only an explicit user decision and current-sprint reconciliation may promote it into active development.

## Living Files

- [CONCEPT-BIBLE.md](CONCEPT-BIBLE.md) — coherent description of the intended experience.
- [DECISIONS.md](DECISIONS.md) — explicit product decisions and their maturity.
- [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md) — prioritized interview queue.
- [CHAT-PROMPT.md](CHAT-PROMPT.md) — full prompt for the persistent Concept Studio chat.
- [systems/](systems/README.md) — system-level plain English and deterministic pseudocode.
- [interviews/](interviews/README.md) — dated interview records.
- [weekly-handoffs/](weekly-handoffs/README.md) — recommendations for future sprint reconciliation.
- [sprint-drafts/](sprint-drafts/README.md) — successor two-week plans prepared before the current sprint closes.
- [templates/](templates/) — repeatable interview, system-specification, and handoff formats.

## Decision Maturity

- **Exploratory:** an idea being investigated; it must not direct implementation.
- **Candidate:** coherent enough for user review, but not approved.
- **Accepted:** explicitly approved by the user as intended product direction.
- **Validated:** supported by implemented behavior, tests, or observed playtesting.
- **Superseded:** intentionally replaced by a later decision.

The Concept Studio may propose Candidate decisions. Only the user may promote a decision to Accepted. Validation requires evidence.

## Session Loop

1. Select one high-impact topic from [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md).
2. Ask three to five focused questions, using concrete alternatives and examples.
3. Let the user answer before filling conceptual gaps.
4. Synthesize the answers into rules, examples, tensions, edge cases, and deterministic pseudocode.
5. Identify contradictions with Accepted decisions or implemented constraints.
6. Ask the user to approve, revise, reject, or defer each Candidate decision.
7. Update the Concept Bible, decision log, open-question queue, and relevant system specification.
8. At most once per week, write a handoff that separates Now, Later, Never, and Needs Evidence.
9. Apply mature findings to the successor two-week sprint draft without widening the active sprint.

## Cadence

- One primary concept interview each week.
- One optional follow-up when answers expose an important contradiction.
- One weekly reconciliation handoff that also advances the successor sprint draft.
- Development blockers may add a focused interview, but the Concept Studio should not continuously interrupt implementation.

## Successor Sprint Responsibility

The Concept Studio must keep the next two-week sprint ahead of implementation:

- **By Day 2:** create or identify the successor draft and its product question.
- **By the end of Day 5:** incorporate weekly concept findings and name unresolved blockers.
- **By Day 7:** draft work items, dependencies, estimates, acceptance criteria, validation, risks, and cuts.
- **By Day 8:** reach Candidate Complete.
- **On Day 9:** reconcile with the user and current development evidence.
- **Before Day 10 closes:** mark the draft Ready for Promotion or record an explicit blocker and fallback.

The successor must include conditional Continue, Narrow, and Stop branches so it does not assume that the current sprint will pass its gates.

## Editing Boundary

By default, the Concept Studio owns only `planning/concept/**`.

It may create and refine successor plans under `planning/concept/sprint-drafts/**`. If a Draft/Conditional successor already exists in `planning/active/**`, it must not create a competing plan; it should recommend updates through the weekly handoff unless the user explicitly authorizes direct reconciliation.

It must not edit:

- game code or tests;
- `CURRENT_BUILD.md`;
- the currently active sprint;
- validation evidence; or
- implementation status claims.

It may not activate or promote its own successor plan. Promotion requires explicit user approval and reconciliation with current repository truth.
