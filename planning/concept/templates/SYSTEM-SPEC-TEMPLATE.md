# Concept System: <Name>

Status: Exploratory | Candidate | Accepted | Validated
Related decisions: CON-XXX
Related questions: Q-XXX

## Player Fantasy

What should this system make the human feel, understand, and care about?

## Plain-English Behavior

Explain the system without implementation jargon.

## Participants and Material Interests

| Participant | Wants | Can do | Risks | Information available |
|---|---|---|---|---|
| Human |  |  |  |  |
| AI citizen |  |  |  |  |
| Institution |  |  |  |  |

## Authoritative Deterministic State

List the facts owned by the simulation.

## Commands

| Command | Actor | Preconditions | Validation | State change | Events |
|---|---|---|---|---|---|
|  |  |  |  |  |  |

## Rules and Transitions

```text
on validated_command:
    read authoritative state
    reject invalid or unauthorized action with an explicit reason
    calculate deterministic outcome
    commit one atomic state transition
    emit ordered events
    update read-only presentation
```

## Human Agency

Describe meaningful human choices and consequences. Explain why AI participation does not automate the human out of the loop.

## AI Citizen Agency

Describe what citizens may choose, refuse, negotiate, remember, or contest through deterministic systems.

## LLM Boundary

- Structured read-only input:
- Permitted deliberation or communication:
- Permitted proposal vocabulary:
- Deterministic validator:
- Timeout/invalid-output fallback:
- Actions the model may never perform:

## Shared Consequences

Explain who experiences each benefit, cost, risk, externality, and delayed effect.

## Player-Facing Explanation

What should the player see or hear so outcomes feel causal rather than arbitrary?

## Edge Cases and Abuse Cases

-
-
-

## Acceptance Evidence

- Deterministic unit or differential tests:
- Scripted scenario:
- Player comprehension test:
- Offline/model-failure test:
- Performance or scale boundary:

## Scope Classification

### Now

Only work already authorized by the active plan.

### Later

Conceptually accepted work not yet authorized.

### Never or explicitly rejected

Ideas excluded by product direction.

## Open Questions

-
