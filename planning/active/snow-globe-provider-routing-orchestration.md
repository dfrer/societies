# Snow Globe Provider Routing Orchestration Outcome Contract

## User outcome and value gate

- Add the smallest provider-neutral orchestration Module that composes the accepted cognition
  comparison, fresh canonical readiness observations, routing policy, and the durable attempt ledger
  into one fail-closed pre-transport result.
- Clear the composition/coherence gate: one operation must validate one owned snapshot of every
  evidence input, assess readiness at one caller-supplied time, create one authenticated
  `not_started` attempt, derive policy only from that exact attempt and assessment, and durably claim
  `dispatch_started` before reporting a provider as prepared.
- Keep deterministic validation authoritative. The result is orchestration evidence only and cannot
  serialize a provider request, access credentials, invoke transport, accept model output, or mutate
  simulation state.

## Owned slice

- Implementation stays inside `labs/Societies.SnowGlobe/`, focused Snow Globe tests, this outcome
  contract, and milestone documentation.
- Add one deep `ProviderRoutingOrchestrationModule` with a small prepare/validate interface. Its
  ledger dependency and deterministic test seams remain internal; callers cannot supply storage,
  provider endpoints, credentials, request bodies, or transport callbacks through the public
  interface.
- Preparation accepts the canonical comparison artifact, canonical OpenRouter and Ollama readiness
  observations, closed routing intent, and one current Unix-millisecond value. Each caller memory
  input is bounded and snapshotted once before it is reused across assessment and policy.
- The Module must use `ProviderRoutingReadinessEvidenceModule.AssessCurrent`,
  `ProviderRoutingAttemptLedgerModule.Create`, `ProviderRoutingPolicyModule.Decide`, and, only when
  the policy selects a provider, `ProviderRoutingAttemptLedgerModule.ClaimDispatch` through their
  existing validated interfaces.
- A prepared result binds the exact comparison, assessment, initial attempt record, routing
  decision, terminal claimed record, selected provider, intent, assessment expiry, orchestration
  contract identity, fixed limitations, and final payload digest.
- A no-selection result may retain the newly created authenticated `not_started` attempt plus the
  exact policy reason, but it must not claim dispatch or provider readiness beyond the validated
  assessment. Any terminal/ambiguous ledger failure remains terminal and is never translated back
  to `not_started` or a new attempt.
- One Module instance accepts at most one preparation call. Deterministic test construction may
  inject the in-memory ledger and deterministic attempt ID source internally; no public/default real
  ledger root is added.

## Non-goals

- Do not observe either provider, refresh the expired retained observations, access credentials or
  accounts, inspect processes/listeners, make network calls, construct or serialize request bytes,
  invoke a provider Adapter, generate a proposal, pay, retry, fall back after claim, or dispatch an
  alternate provider.
- Do not add a transport interface, execution capability, public filesystem composition, real ledger
  root, command-line invocation, retained live artifact, or provider-specific orchestration branch.
- Do not change the accepted comparison, scoring recommendation, routing-policy rules, readiness or
  observation schemas, attempt-ledger state machine, provider pins, credential formats, financial
  journals, or historical evidence.
- Do not integrate with or modify `src/societies/`; do not claim gameplay integration, continuous
  readiness, general intelligence, deployment readiness, exactly-once provider submission, or
  commercial readiness.

## Acceptance criteria

1. A versioned canonical orchestration-result schema and contract digest exist with strict bounds,
   canonical validation, detached public surfaces, and closed non-echoing failures.
2. OpenRouter and Ollama preparation use the identical Module path and existing policy rules; no
   provider-specific request or transport behavior is introduced.
3. A provider is reported as prepared only after the exact fresh assessment, initial ledger record,
   decision, and durable `dispatch_started` record are mutually coherent and canonically validated.
4. Missing, malformed, wrong-provider, expired, changing-memory, invalid-intent, no-selection,
   duplicate invocation, ledger-create failure, stale claim, terminal/ambiguous claim, and undefined
   enum cases fail closed or produce an explicit non-prepared result without authority leakage.
5. Tests cover both selected providers, equal inputs, deterministic repeatability with deterministic
   internal dependencies, score/policy ties or unsupported comparison evidence, post-claim replay
   denial, and exact no-transport/public-interface assertions.
6. Canonical output retains no credential, account identity, prompt, proposal, reasoning, request or
   response body, raw provider metadata, endpoint, path, PID, dynamic exception, or secret.
7. Security-worker implementation followed by independent deep review reports no unresolved P0-P2
   findings.
8. Focused tests, full Snow Globe Release tests, relevant provider CLI/security suites, relevant
   Release builds, secret/path scans, diff checks, and required pull-request checks pass before merge.
9. `CURRENT_BUILD.md`, `WORKFLOW.md`, the Snow Globe README, and a focused contract document state
   exactly what was proven and leave provider transport as a separate future security milestone.

## Evidence and delivery

- Capture an intended compile/behavioral red before production implementation, then rerun the same
  focus green through the public Module interface.
- One bounded `security_worker` owns the Module, canonical codec/result, focused tests, and focused
  contract documentation. It must not stage, commit, invoke providers, access credentials or live
  state, add a real root, or spawn workers.
- One independent `deep_reviewer` audits evidence coherence, ordering, one-shot behavior, ambiguous
  ledger outcomes, caller-memory ownership, canonical integrity, authority leakage, and public
  documentation after implementation.
- The main task owns scope, integration, milestone documentation, proportional final validation,
  commit, pull request, required-check waiting, merge, and final repository-state reporting.
- Delivery boundary is one reviewed `codex/` pull request merged into `master`. No live readiness
  observation, real attempt, provider request, credential action, or `src/societies/` change occurs.

## Next boundary after this milestone

- A later, separately reviewed execution-security milestone may introduce the first true provider
  transport Adapter and request-byte handoff. It must consume the durable prepared evidence exactly
  once, preserve sequential/no-retry/Azure-only/ZDR-required/cost controls, and continue treating all
  model output as untrusted deterministic-validation input.

## Reviewed implementation result

- `ProviderRoutingOrchestrationModule` implements the one-shot `Prepare`/`Validate` interface under
  result schema `snow_globe_provider_routing_orchestration_result/v1` and contract schema
  `snow_globe_provider_routing_orchestration_contract/v1`. Contract digest is
  `16550d06f0eee280f4618c76bf8ff556320dc0d0198c4b15bcd8021ed29ac230`.
- Initial red-first compilation failed only for the absent Module/result. Adversarial red regressions
  then exposed authenticated terminal validity-date splicing and post-claim result-construction
  misclassification. Repairs enforce exact created/expiry carry-forward, the single assessed/
  created/claimed time, and terminal/ambiguous classification for every failure after claim return.
- Focused Release passes 11/11, full Snow Globe Release passes 1191/1191, OpenRouter CLI/security
  Release passes 104/104, Recording CLI Release passes 94/94, and three Release builds pass with
  zero warnings/errors. Independent deep re-review is FINAL GO with no P0-P3 findings.
- No live readiness observation, real ledger root, provider, credential, network, request bytes,
  payment, generation, retry/fallback, gameplay, world mutation, or `src/societies/` action occurred.
  Reviewed PR delivery remains the final milestone gate.
