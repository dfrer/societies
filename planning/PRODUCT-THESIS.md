# Societies Product Thesis

## Canonical North Star

> "A deterministic civilization/ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences."

This is the product north star. It defines direction, not current implementation status. For implemented reality, use [CURRENT_BUILD.md](../CURRENT_BUILD.md). For the active delivery sequence, use [active/README.md](active/README.md).

## Product Boundary

### Deterministic simulation owns the world

The simulation is the authoritative owner of facts, time, resources, ecology, policy state, eligibility, and every world-changing outcome. State changes enter only through validated deterministic commands/events. Seeded replay, save/resume, ordering, and outcomes must remain reproducible without a model service.

### AI and LLM responsibilities

AI citizens may use deterministic systems and, later, LLM-assisted capabilities to:

- interpret structured, read-only world and citizen state;
- deliberate over material interests and stated preferences;
- communicate, explain reasons, and negotiate;
- summarize bounded memory; and
- propose actions for deterministic validation.

An LLM is not an authority on world facts, does not mutate world state directly, and cannot bypass deterministic validation. Model output is advisory input, never a hidden simulation rule.

### Humans remain consequential

Human choices must have readable, material consequences that citizens and the shared world experience. AI participation should make those choices richer, not automate the human out of the loop.

### Resilience rule

Offline operation, model failure, invalid model output, timeout, or unavailable provider must preserve simulation progress and deterministic replay. The deterministic fallback must select from the same valid action vocabulary and expose that fallback clearly enough for diagnosis.

## Current Reality vs. Intent

| Area | Current implemented reality | Future intent |
|---|---|---|
| World simulation | Local deterministic settlement, logistics, resource ledger, and `empty_stores` crisis contract | Civilization/ecology simulation with shared consequences |
| Citizens | Deterministic needs and work assignment | Understandable material interests, negotiation, governance participation, and communication |
| Human agency | Local harvesting and validated atomic contribution to the shared settlement stockpile | Consequential participation in trade, negotiation, and governance |
| LLMs | No live model integration | Structured interpretation, deliberation, communication, memory summaries, and action proposals |
| Networking | Not authoritative | Shared human/AI society experience, only when deterministic authority remains intact |

W2-02 through W2-05 and W3-01 through W3-05 are delivered as bounded deterministic engineering slices. The user-led play assessment on 2026-08-26 nevertheless failed: the current experience feels on rails, visually repetitive, and unfinished relative to this thesis. The next active question is therefore experience depth, not system breadth. See [ER-01 First Believable Settlement Loop](active/v3-experience-recovery-plan.md), [the validation report](../V3_SPRINT_VALIDATION_REPORT.md), and [V3 Weeks 3-4](active/v3-weeks-3-4-development-plan.md).

## Near-Term Product Question

Can one existing deterministic settlement loop feel intentional, embodied, variable, and consequential enough that the player wants to continue?

The civic-policy machinery is necessary but no longer sufficient evidence. The smallest credible next test is an 8-10 minute loop connecting resource interaction, contribution, a deliberate civic choice, a readable citizen response, and a visible shared consequence across two contrasting deterministic starts. General laws, markets, multiplayer, social graphs, and live LLM integration remain deferred.
