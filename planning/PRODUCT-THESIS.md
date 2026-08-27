# Societies Product Thesis

## Canonical North Star

> "A deterministic civilization/ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences."

This is the product north star. It defines direction, not current implementation status. For implemented reality, use [CURRENT_BUILD.md](../CURRENT_BUILD.md). For the active delivery sequence, use [active/README.md](active/README.md).

## Product Boundary

### Deterministic simulation owns the world

The simulation is the authoritative owner of facts, time, resources, ecology, policy state, eligibility, and every world-changing outcome. State changes enter only through validated deterministic commands/events. Seeded replay, save/resume, ordering, and outcomes must remain reproducible without a model service.

### AI and LLM responsibilities

AI citizens use deterministic systems and may use LLM-assisted capabilities to:

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

### Societies is Snow Globe's embodied frontend

Societies is the player-facing world in which Snow Globe's bounded citizen cognition becomes tangible.
Snow Globe may support deliberation, communication, negotiation, and memory summaries through constrained
proposals, but it is not a separate dashboard and never becomes world authority. The Godot client must present
citizens, labor, ecology, choices, and consequences as a cohesive playable experience rather than expose
provider or orchestration mechanics.

## Current Reality vs. Intent

| Area | Current implemented reality | Future intent |
|---|---|---|
| World simulation | Local deterministic settlement, logistics, resource ledger, and `empty_stores` crisis contract | Civilization/ecology simulation with shared consequences |
| Citizens | Deterministic needs and work assignment | Understandable material interests, negotiation, governance participation, and communication |
| Human agency | Local harvesting and validated atomic contribution to the shared settlement stockpile | Consequential participation in trade, negotiation, and governance |
| LLMs | Strict civic cognition contract and deterministic fallback exist; Snow Globe lab has offline/live evidence machinery, but no gameplay-facing Snow Globe Interface or live client integration exists | Structured interpretation, deliberation, communication, bounded memory summaries, and validated action proposals embedded in play |
| Networking | Not authoritative | Shared human/AI society experience, only when deterministic authority remains intact |

W2-02 through W2-05 and W3-01 through W3-05 are delivered as bounded deterministic engineering slices. The user-led play assessment on 2026-08-26 failed, and the later ER-01 HUD/interaction recovery remained unacceptable: the project refined the old demo without materially resolving its concept, gameplay, visuals, or overall experience. Foundation Gate F0 is now accepted around an embodied resident-founder, autonomous citizens, and a negotiated wetland water-control consequence. The next active question is how F1 expresses that foundation at an accepted visual and interaction quality bar. See the [Snow Globe Frontend Product Recovery Plan](active/v3-snow-globe-frontend-recovery-plan.md).

## Near-Term Product Direction

The player is an embodied resident-founder with limited formal power. Contribution, commitments, persuasion,
and visible outcomes create influence; citizens may negotiate, counter, refuse, withdraw labor, or proceed
without the player. The first shared consequence is a failing wetland causeway and a negotiated water-control
commitment that changes ecology, resources, work, trust, and the next day's situation.

The civic-policy machinery and current presentation are reusable evidence, not the product template. The next
credible step is Visual Gate F1 followed by a new Golden Three/Golden Fifteen presentation shell. The real
provider-neutral cognition Interface enters with Golden Three through deterministic and recorded-response
Adapters; the live Snow Globe pilot follows only after the offline experience passes. General laws, markets,
multiplayer, social graphs, and uncontrolled provider integration remain deferred.
