# Snow Globe routing-readiness evidence outcome contract

## Outcome

Add a deterministic, provider-neutral readiness-evidence assessment inside the isolated Snow Globe lab. It must validate existing comparison, OpenRouter preflight/execution, and Ollama execution artifacts, record exactly what each artifact proves, and refuse to manufacture `ProviderRoutingPolicyInput` when current provider readiness or a durable new-attempt `not_started` fact is unproven.

The accepted current artifacts are expected to produce an explicit `insufficient_current_readiness_evidence` result. That is the correct bounded result, not a blocker or provider failure.

## Verified starting point

- Fresh branch: `codex/snowglobe-routing-readiness-evidence` from `origin/master` at `85b5840b6f6b48eef84af746b3a6fffbd8286d88` (merged PR #164).
- Isolated worktree: `E:\AIExperiments\games\societies-codex-cognition-quality`, clean before this contract. Unrelated dirty work in `E:\AIExperiments\games\societies` remains untouched.
- `ProviderRoutingPolicyModule` is reviewed and merged. It correctly treats readiness and primary attempt state as caller-supplied advisory facts and grants no execution authority.
- The accepted comparison proves `openrouter_default` on the frozen corpus.
- Existing OpenRouter activation-preflight artifacts are canonical and can prove evaluated eligibility/bindings, but `LiveTrafficEnabled` is false and they do not prove current readiness.
- Existing Ollama execution artifacts prove a past exact compatibility recording; they explicitly do not prove current runtime/model/listener readiness.
- Existing OpenRouter execution artifacts and journal snapshots can prove states for identified historical generations, but no public read-only authenticated aggregate proves a proposed new attempt is globally `not_started`. Absence is not evidence.

## Scope

- Add one deep synchronous Module under `labs/Societies.SnowGlobe` with a small `Assess`/`Validate` interface.
- Accept only detached canonical artifact bytes: required cognition comparison plus optional OpenRouter activation preflight, Ollama execution, and OpenRouter execution evidence.
- Validate every supplied artifact through its existing canonical validator using one owned snapshot per caller input.
- Emit a strict versioned canonical readiness-assessment artifact with closed statuses, exact input digests/schema identities where valid, historical/current distinction, routing-input issuance status, gap codes, limitations, and payload digest.
- Add red-first focused tests and a lab contract document.

## Required assessment semantics

- The accepted comparison may establish `selection_evidence=accepted_openrouter_default`; it does not establish readiness.
- A complete accepted Ollama recording establishes `historical_compatibility_complete` only. Current Ollama readiness remains `unknown`.
- A validated OpenRouter activation-preflight artifact establishes only its recorded eligibility/bindings. Because live traffic is disabled and freshness/current execution authority are absent, current OpenRouter readiness remains `unknown`.
- A validated OpenRouter execution artifact establishes only the recorded state of its identified historical generation. It never proves a different proposed attempt is `not_started`.
- Missing or malformed optional artifacts are reported with closed raw-free statuses and cannot increase readiness.
- `ProviderRoutingPolicyInput` is never issued unless current provider readiness and an authenticated attempt-bound primary state are both proven. No existing accepted artifact combination meets that standard in v1, so the v1 issuance status is `not_issued` with explicit gap codes.
- The assessment must never infer `ready` from a past success, `not_ready` from missing evidence, or `not_started` from absence of files/slots/artifacts.

## Non-goals

- No provider, network, process, listener, file-system discovery, credential, Windows Credential Manager, account, payment, journal writer, state-root, or live-state access.
- No provider call, model execution, readiness probe, retry, fallback execution, routing decision, route authorization, or world mutation.
- No new authenticated production readiness Adapter, no state-store migration, and no `src/societies/` integration.
- No reinterpretation or rewrite of accepted artifacts and no claim of current provider availability, deployment, general intelligence, or commercial readiness.

## Acceptance criteria

1. A versioned canonical readiness-assessment schema exists and is bounded, strict, deterministic, and raw-free.
2. Both provider histories and OpenRouter activation evidence use one assessment path with their existing validators.
3. Tests cover accepted historical evidence, missing/malformed/oversized/deep/duplicate inputs, changing caller memory, past-success-not-current-ready, missing-not-not-ready, historical-generation-not-new-attempt, no-input issuance, canonical tampering, repeatability, and public-surface authority.
4. The assessment reports the exact evidence gaps required before a future authenticated readiness Adapter can exist.
5. No public interface exposes an Adapter, transport, credential, account, endpoint/model selector, payment/cost, retry, journal writer, file/path, Task, or world control.
6. Focused and proportional full Release validation pass; independent review has no unresolved P0-P2.
7. `CURRENT_BUILD.md`, `WORKFLOW.md`, and lab documentation state exactly what was proven and that current routing readiness remains unproven.

## Review and delivery

- A bounded `security_worker` owns implementation and red-first focused tests because this evaluates provider/security evidence. It must not invoke providers, access credentials, stage, commit, push, or spawn workers.
- An independent `deep_reviewer` reviews evidence semantics, historical/current separation, attempt-identity claims, canonical validation, memory ownership, raw-free behavior, public authority, tests, and documentation.
- The main task owns integration, full validation, milestone documentation, Git/PR delivery, required-check monitoring, merge, and user communication.
- Completion requires a reviewed PR merged to `master`, a clean synchronized worktree, no `src/societies/` change, and no provider/live-state action.

## Implemented and reviewed result

- `ProviderRoutingReadinessEvidenceModule` exposes only synchronous `Assess` and `Validate`. Every nonempty caller artifact is size-checked, snapshotted exactly once, hashed/validated through its existing validator from that snapshot, and zeroed afterward.
- Red-first compilation failed exclusively on the absent assessment types. Initial focused implementation passed 7/7.
- Independent review found two P2 integrity gaps. First, an integrity-valid assessment could pair the exact accepted comparison digest with malformed/unsupported status; forged re-digested regressions now reject every non-accepted pairing with that digest. Second, the readiness contract digest omitted the exact comparison identity; contract digest `80a6e228280f3d8e4e75459279452049076fded3fe5709a7e5523a61a61200be` now binds schema `snow_globe_cognition_quality_comparison/v1` and SHA-256 `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`.
- Review also identified one P3 coverage omission; an all-inputs-missing regression now proves `unaccepted`, `unknown` readiness/state, `not_issued`, null routing input, and the accepted-comparison gap.
- Final validation is 9/9 focused Release, 1137/1137 full Snow Globe Release, 59/59 Recording CLI Release, 100/100 OpenRouter CLI/security, and three Release builds with 0 warnings/errors. Independent deep review is FINAL GO with no unresolved P0-P2.
- The final result remains deliberately `insufficient_current_readiness_evidence`. No provider, credential, account, network, process/listener, payment, live-state, journal/state-root, route, execution, or `src/societies/` action occurred. PR delivery is the remaining gate.
