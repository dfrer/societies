# Societies Concept Decisions

This log separates approved direction from attractive speculation.

## Status Rules

- **Exploratory:** under discussion.
- **Candidate:** proposed for user approval.
- **Accepted:** explicitly approved by the user.
- **Validated:** supported by implementation or playtest evidence.
- **Superseded:** replaced by a named later decision.

Only the user may promote Candidate decisions to Accepted.

## Decision Log

| ID | Status | Decision | Rationale | Source | Implications |
|---|---|---|---|---|---|
| CON-001 | Accepted | The product north star is a deterministic civilization/ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences. | Establishes the experience being built. | [Product Thesis](../PRODUCT-THESIS.md) | Future planning should connect mechanics to this promise. |
| CON-002 | Accepted | The deterministic simulation owns all facts and world-changing outcomes. | Preserves replay, testability, fairness, and shared reality. | [Product Thesis](../PRODUCT-THESIS.md) | LLM output must use validated commands rather than direct mutation. |
| CON-003 | Accepted | Future LLMs may interpret, deliberate, communicate, summarize bounded memory, negotiate, and propose actions, but are not world authorities. | Uses language models where they add value without making simulation truth probabilistic. | [Product Thesis](../PRODUCT-THESIS.md) | Every model-facing feature needs a structured input/output contract and fallback. |
| CON-004 | Accepted | Humans must remain consequential participants. | AI citizens should enrich the experience rather than play the game for the human. | [Product Thesis](../PRODUCT-THESIS.md) | Human choices need readable material consequences. |
| CON-005 | Accepted | Model failure or offline operation must preserve simulation progress and deterministic replay. | The game must remain a coherent simulation without a provider. | [Product Thesis](../PRODUCT-THESIS.md) | Deterministic fallback behavior is required for every LLM-assisted action. |
| CON-006 | Accepted | Concept work does not authorize implementation; the active plan remains execution authority. | Prevents a parallel design track from becoming a competing roadmap. | [Concept Studio README](README.md) | Weekly handoffs recommend rather than silently schedule work. |
| CON-007 | Accepted | The Concept Studio prepares and finishes the next two-week sprint proposal before the current sprint ends. | Continuous clarification should improve the next plan without disrupting current implementation. | User direction, 2026-07-14 | The draft advances weekly, includes conditional closing branches, and requires explicit promotion. |

## Pending Decisions

Candidate decisions created during interviews belong below this line until the user approves, rejects, or defers them.

| ID | Status | Proposed decision | Why it matters | Required user choice |
|---|---|---|---|---|
| — | — | No candidates yet. | — | Begin the player-role interview. |
