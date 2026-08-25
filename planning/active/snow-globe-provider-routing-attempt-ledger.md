# Snow Globe Provider Routing Attempt Ledger Outcome Contract

## User outcome and value gate

- Add the smallest provider-neutral durable ledger that can prove one fresh routing attempt is
  `not_started` and can atomically record `dispatch_started` before any future transport receives
  request bytes.
- Clear the persistence/race gate identified by the accepted readiness assessment without granting
  provider execution, retry, payment, gameplay, or world authority.
- Keep the deep Module interface small: create one attempt, inspect its authenticated current
  evidence, claim its first dispatch once, and validate detached canonical evidence. File locking,
  durable flush, identity checks, canonical encoding, recovery, and fail-closed ambiguity remain
  implementation details behind the seam.

## Owned slice

- Implementation stays inside `labs/Societies.SnowGlobe/`, focused Snow Globe tests, this outcome
  contract, and milestone documentation.
- Add a versioned provider-neutral attempt-record/transition contract and a production Windows file
  Adapter plus an in-memory test Adapter at an internal seam.
- A new attempt binds a fresh opaque attempt identifier, creation/expiry time, accepted comparison
  digest, current-readiness assessment digest, routing intent, and canonical state `not_started`.
- A dispatch claim must name the same attempt, expected current-record digest, selected provider,
  and routing-decision digest. It must durably transition exactly once to `dispatch_started` before
  returning success.
- The file Adapter takes an internally injected, verified absolute state root and provider-neutral
  integrity anchor, then uses exclusive writer ownership, CreateNew/no-overwrite semantics, bounded
  authenticated canonical records, durable readback, no-follow/reparse/hardlink and handle/path
  identity checks, and append-only transition evidence. This slice exposes no public/default real-
  root or Credential Manager composition; isolated tests supply both internal dependencies.
- Any malformed/tampered/expired input, duplicate attempt, stale expected digest, concurrent claim,
  lease loss, partial write, or ambiguous flush fails closed. An ambiguous post-write outcome is
  never represented again as `not_started`; it becomes closed `submission_unknown` evidence or a
  terminally poisoned attempt according to the reviewed contract.

## Non-goals

- Do not invoke OpenRouter, Ollama, a credential store, a listener, a model, a provider transport,
  or any network endpoint. Do not run another readiness observation.
- Do not issue a routing-policy input, select a default at runtime, construct a provider request,
  return an execution capability, perform a completion/generation/payment, retry, fallback, or
  alternate dispatch.
- Do not change the accepted comparison, readiness observations, provider pins, routing-policy v1,
  provider financial journals, credential formats, or retained provider evidence.
- Do not integrate with or modify `src/societies/`; do not claim gameplay integration, continuous
  readiness, general intelligence, deployment readiness, or commercial readiness.
- Do not provision or mutate a real retained attempt root in this milestone. Production filesystem
  behavior is exercised only in isolated test directories; any later real attempt requires a
  separately reviewed composition and explicit evidence gate.

## Acceptance criteria

1. Versioned canonical schemas and a documented state machine exist for attempt records and
   transitions, with exact digests and closed limitation codes.
2. The public Module exposes only bounded create, inspect, claim-dispatch, and validate behavior;
   provider, path, credential, request-body, retry, and world controls are absent. Inspection never
   treats a missing or poisoned attempt as `not_started`.
3. The production file Adapter and in-memory Adapter use the same Module path and observable
   contract.
4. Red-first tests cover successful create/claim, duplicate create, repeated claim, stale digest,
   wrong attempt/provider/decision binding, expiry, invalid transitions, concurrent claim, restart
   recovery, malformed/truncated/duplicate/deep/oversized records, tampering, path traversal,
   reparse/hardlink attacks, lease loss, partial/ambiguous writes, and deterministic repeatability.
5. A claim is durably visible and canonically revalidated before success returns. Once dispatch may
   have started, no recovery path or caller can observe the attempt as `not_started` again.
6. Canonical outputs retain only the closed routing-provider code `openrouter` or `ollama`, never
   provider metadata, credential, account identity, prompt, proposal,
   reasoning, request/response body, host path, PID, dynamic exception, or secret.
7. Security review followed by independent deep review reports no unresolved P0-P2 findings.
8. Focused tests, the full Snow Globe Release suite, relevant CLI/security suites, relevant Release
   builds, secret-like scans, and diff checks pass; required PR checks pass before merge.
9. `CURRENT_BUILD.md`, `WORKFLOW.md`, the lab README, and the ledger contract state exactly what was
   proven and keep real routing/dispatch as future work.

## Evidence and delivery

- Capture an intended compile/behavioral red before production implementation, then rerun the same
  focus green.
- Security-worker ownership covers the ledger Module, persistence Adapter, contract, and focused
  tests. A separate deep reviewer audits state transitions, ambiguity handling, filesystem identity,
  caller-memory ownership, canonical integrity, authority leakage, and documentation.
- Main task owns scope, integration, final validation, repository documentation, commit, PR/check
  monitoring, merge, and final communication.
- Delivery boundary is a reviewed `codex/` pull request merged to `master` after required checks.
  No real ledger attempt, provider access, or `src/societies/` change is part of delivery.

## One next decision after this milestone

- Decide whether to add a separately reviewed orchestration Module that combines fresh readiness,
  the durable `not_started` receipt, routing policy, and the atomic dispatch claim. That later Module
  must still stop before provider transport until its own execution-security gate is approved.

## Reviewed implementation result

- The deep Module and both internal Adapters are implemented under record/contract schemas v2 and
  contract digest `99694dd77536b92b537d3f95417138b35982d7304c9c20f10f48da5c9d5c2e47`.
- Red-first evidence covered the absent surface, absent authenticated inspection, false ambiguous
  pre-tombstone classification, and absent readiness fields. Two security-owner/deep-review repair
  cycles closed all P1/P2 findings.
- Focused ledger Release is 22/22, combined ledger/policy/readiness/observation is 69/69,
  independent full Snow Globe Release is 1180/1180, and the Release build has 0 warnings/errors.
  Final independent review is GO with no P0-P3 findings.
- No provider, credential, network, live listener, real ledger root, request bytes, payment,
  generation, retry/fallback, gameplay, world mutation, or `src/societies/` action occurred.
- Implementation is complete locally; proportional final validation and reviewed PR delivery remain
  before this milestone is merged.
