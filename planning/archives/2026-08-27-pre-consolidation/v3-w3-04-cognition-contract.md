# V3 W3-04 Cognition Contract and Deterministic Fallback

## Outcome contract

**Outcome:** The gameplay runtime can publish one bounded canonical observation for an existing
citizen after a civic policy has been selected, validate an untrusted closed-vocabulary stance
proposal against that exact observation and the current deterministic world, and record either an
accepted proposal or an equivalent deterministic fallback through one provider-neutral Module.

**Owned slice:** `src/societies/`, focused managed and Godot tests where needed, W3-04 evidence,
and milestone truth in `CURRENT_BUILD.md`, `WORKFLOW.md`, `README.md`, and the active Weeks 3-4
plan.

**Value gate:** This clears the gameplay-side cognition seam without granting a model, provider,
or proposal any world authority. Deterministic state remains available and replayable when proposal
evidence is absent or unusable.

**Delivery boundary:** One coherent commit on `codex/w3-04-cognition-contract`, pull request,
required checks, independent deep review with no unresolved P0-P2 findings, merge, ancestor
verification against the latest `origin/master`, and a clean isolated worktree.

## Scope and interface

Implement one deep provider-neutral Module with a small public interface for:

1. publishing an immutable canonical observation for one citizen and the currently selected civic
   policy;
2. resolving bounded raw proposal evidence or a closed no-output condition into either a validated
   proposal, a deterministic fallback proposal, or a fail-closed rejection; and
3. applying only the Module's accepted result through the existing runtime event-recording path.

The Module owns canonical UTF-8 encoding, strict parsing and validation order, digest calculation,
state/observation binding, reason and summary derivation, fallback calculation, closed source
classification, and normalized error codes. Its public surface must expose no provider, credential,
network, payment, retry, filesystem-path, arbitrary-command, or direct world-mutation capability.

`PrototypeRuntimeSession.SelectCivicPolicy` remains the sole civic-policy mutation path. A cognition
proposal is a citizen stance on the already-selected policy; it never selects or changes policy,
wetland state, citizen needs, inventory, or settlement state. Applying an accepted result records a
bounded canonical cognition-decision event through the existing session event log. A rejected
proposal records nothing and mutates nothing. The closed `request_reconsideration` action is an
accepted non-mutating stance only when its deterministic reason is coherent with opposition to the
selected policy.

## Versioned contracts

The v1 observation is strict canonical JSON and contains only deterministic civic-decision facts:

- schema identity/version;
- citizen ID;
- simulation tick;
- bounded state identity derived from the exact relevant facts;
- nutrition and fatigue bands, deterministic role, and derived material-interest reason;
- current non-neutral civic policy;
- wetland health band and remaining reed quota;
- the exact ordered action set `support_policy`, `oppose_policy`,
  `request_reconsideration`;
- a SHA-256 observation payload digest.

The v1 proposal is strict canonical JSON and contains only:

- schema identity/version;
- citizen ID;
- proposed action from the closed action vocabulary;
- reason code from the existing closed citizen-interest vocabulary;
- the exact bounded deterministic display summary;
- observation digest and state identity bindings.

Document maximum byte size, nesting depth, string lengths, field order, vocabulary, schema identity,
and contract digests in code and the W3-04 evidence. Equal state and inputs must produce byte-identical
observations, fallback proposals, summaries, and digests.

## Deterministic authority and fallback

Validation recomputes the observation from the current session before accepting proposal evidence.
It rejects unsupported versions; malformed, missing, duplicated, reordered, unknown, trailing,
oversized, over-deep, invalid-UTF-8, or noncanonical content; undefined enums; wrong citizen,
observation, or state binding; stale tick/state; illegal actions; incoherent action/reason pairs; and
summaries that differ from the existing deterministic interest projection.

Fallback derives its action, reason, and summary from the same
`PrototypeCitizenInterestEvaluator` result used by current citizen-interest projections. It emits
the same proposal schema, passes through the same validator, and enters the same downstream event
path. Only `validated_proposal` and `deterministic_fallback` are valid decision-source values.
Missing, invalid, cancelled, timed-out, and unavailable proposal conditions may select fallback;
the rejected evidence itself never causes a mutation or event.

Schema-v9 runtime snapshots remain unchanged unless implementation evidence proves a schema change
is strictly necessary. The preferred design records the bounded cognition decision in the existing
canonical event log, so current snapshot migration remains untouched while save/resume and replay
can prove identical fallback behavior.

## Non-goals

- No Snow Globe source change or gameplay-to-Snow-Globe integration.
- No Ollama, OpenRouter, provider, credential, account, network, payment, model download, readiness,
  request construction, transport, retry, or fallback-provider action.
- No free-form command, arbitrary reason, prompt, reasoning trace, confidence, provider identity,
  model metadata, or transport metadata.
- No autonomous policy mutation, extra civic policy, general dialogue/memory/personality,
  relationship, law, election, market, governance framework, W3-05, W4, demo packaging, production
  art, deployment, or commercial-readiness claim.

## Acceptance and evidence

- Red-first focused tests cover byte-identical observation/proposal/digest output; valid support,
  oppose, and reconsideration; every required malformed/binding/coherence/enum rejection; and no
  state/event mutation on rejected evidence.
- Fallback is covered for missing, invalid, cancelled, timed-out, and unavailable evidence and is
  proven to traverse the same schema, validator, and event path as accepted proposal evidence.
- Fixed-seed uninterrupted and save/resume fallback runs produce identical policy, wetland,
  citizen-reason, event, snapshot, and final-state digests.
- Existing Eat/Sleep/recovery, directives, crisis, civic selection, wetland enforcement, schema-v9
  persistence, v5-v8 migration, and replay behavior remain unchanged.
- Focused tests, full .NET, Godot headless, Release and ExportRelease builds, deterministic artifacts,
  and the proportional performance gate pass.
- Independent deep review reports no unresolved P0-P2 findings.
- Final documentation distinguishes local proof from live AI, intelligence, author-smoke, demo,
  deployment, and commercial readiness.
