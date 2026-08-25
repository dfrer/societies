# Provider Routing Orchestration Contract

## Scope

`ProviderRoutingOrchestrationModule` is a synchronous, provider-neutral pre-transport Module. It
composes already-produced canonical evidence through the existing readiness, durable-ledger, and
routing-policy Modules. It does not observe a provider, construct a provider request, invoke a
transport, read credentials, or grant execution authority.

The public operation is deliberately small:

- `Prepare(ProviderRoutingOrchestrationInput)` accepts the comparison artifact, optional
  OpenRouter and Ollama readiness observations, a closed routing intent, and one caller-supplied
  Unix-millisecond time.
- `Validate(ReadOnlyMemory<byte>)` strictly validates and detaches a retained orchestration result.

Ledger composition remains internal. There is no public/default filesystem root, storage seam,
transport callback, provider Adapter, endpoint, credential, model, retry, fallback, or payment
control. A production composition must supply an already-configured attempt-ledger Module inside
the assembly's governed boundary.

## Contract identity and bounds

- result schema: `snow_globe_provider_routing_orchestration_result/v1`
- contract schema: `snow_globe_provider_routing_orchestration_contract/v1`
- contract SHA-256:
  `16550d06f0eee280f4618c76bf8ff556320dc0d0198c4b15bcd8021ed29ac230`
- maximum result size: 49,152 UTF-8 bytes
- maximum result JSON depth: 12
- each input uses the maximum already enforced by its owning comparison or observation codec

The contract digest binds the exact accepted comparison identity, current-readiness schema and
contract, routing-policy schema and digest, ledger record schema and contract, ordered composition
flow, decision and claim bindings, the exact single-time rule, terminal validity carry-forward,
statuses, one-shot/snapshot rules, result bounds, fixed limitations, and absence of provider
transport or world authority.

## Preparation flow

The Module consumes one preparation call even when validation fails. It first bounds and snapshots
each caller-owned evidence memory exactly once, then zeroes those owned input buffers when the call
ends. The same comparison snapshot is reused across readiness assessment and policy evaluation.

Preparation executes this fixed sequence:

1. `ProviderRoutingReadinessEvidenceModule.AssessCurrent` validates the comparison and optional
   observations at the single supplied time.
2. An unaccepted comparison fails before attempt creation. Otherwise,
   `ProviderRoutingAttemptLedgerModule.Create` durably creates and validates one authenticated
   `not_started` record bound to that exact assessment, its expiry, its closed provider readiness,
   the accepted comparison, and intent.
3. `ProviderRoutingPolicyModule.Decide` receives only the authenticated initial record's intent and
   provider readiness, plus `not_started` and the same accepted comparison snapshot. Its canonical
   decision is validated and cross-bound to the initial record.
4. When the policy selects no provider, the Module returns `not_prepared`, preserving the
   authenticated `not_started` record and exact closed policy reason. It does not call
   `ClaimDispatch`.
5. When a provider is selected, `ProviderRoutingAttemptLedgerModule.ClaimDispatch` receives the
   initial record digest, selected provider, and exact routing-decision bytes. `prepared` is emitted
   only after the returned authenticated record validates as the coherent sequence-1
   `dispatch_started` successor. The assessment time, initial creation time, and terminal claim time
   must be identical. The terminal record must carry forward the initial creation and expiry times
   exactly; an independently authenticated record with altered validity dates is not coherent.

OpenRouter `preferred_online` and Ollama `local_only` use this identical path. Provider choice is
owned solely by the existing policy Module; orchestration has no provider-specific transport
branch.

## Terminal and failure semantics

Failures use closed, non-echoing `orchestration_*` codes. Missing, malformed, wrong-provider, or
expired readiness remains unknown/non-ready in the validated assessment and normally produces a
policy-controlled `not_prepared` result. Malformed or unsupported comparison evidence, invalid
intent/time, oversized input, and undefined enum values fail closed.

A definitely pre-tombstone ledger claim failure is reported as a closed ledger-claim failure. An
ambiguous/terminal ledger outcome is reported as `orchestration_claim_terminal_or_ambiguous`.
Once `ClaimDispatch` returns, any later validation, coherence, result-construction, canonical-
embedding, size, or unexpected failure is also terminal/ambiguous, because durable claim material
may already exist. The Module never converts such a condition to `not_started`, creates a
replacement attempt, retries, falls back, or selects an alternate provider.

## Canonical result

The JSON object has exact property order and no extensions:

1. `schema_version`
2. `status` (`prepared` or `not_prepared`)
3. `contract_schema_version`
4. `contract_digest_sha256`
5. `comparison_artifact_digest_sha256`
6. `comparison_schema_version`
7. `comparison_recommendation`
8. `intent`
9. `assessed_at_unix_ms`
10. `expires_at_unix_ms`
11. `assessment`
12. `initial_attempt_record`
13. `routing_decision`
14. `claimed_attempt_record` (canonical object or `null`)
15. `selected_provider` (`openrouter`, `ollama`, or `null`)
16. `reason_code`
17. `claim_limitation_codes`
18. `orchestration_payload_digest_sha256`

The payload digest covers the canonical object through `claim_limitation_codes`. The detached
result additionally exposes the SHA-256 of the final canonical bytes. Validation rejects invalid
UTF-8, duplicate/unordered/unknown properties, non-canonical scalar encodings, malformed/deep/
oversized JSON, digest mismatch, altered nested evidence, inconsistent provider/intent/readiness/
expiry/time/record chains, including an authenticated terminal validity-date splice, and
non-canonical reserialization. Nested readiness, policy, and ledger bytes are validated through
their existing validators; ledger authentication uses the same injected integrity anchor.

## Fixed limitations

Every result carries these exact limitation codes:

- `evidence_only_no_provider_transport_execution_authority`
- `prepared_means_durable_dispatch_claim_not_provider_submission`
- `one_shot_no_retry_fallback_or_alternate_attempt`
- `current_readiness_bounded_until_assessment_expiry`
- `provider_output_untrusted_deterministic_validation_authoritative`
- `local_integrity_anchor_does_not_prevent_whole_volume_rollback`
- `no_credential_payment_network_gameplay_or_world_authority`

The canonical result contains normalized/authenticated evidence only. It retains no credential or
account identity, prompt, proposal, reasoning, request/response body, raw provider metadata,
endpoint, path, PID, dynamic exception, or secret. `prepared` proves only a durable pre-transport
dispatch claim. It does not prove provider submission, exactly-once external effects, quality,
deployment or commercial readiness, gameplay integration, or simulation/world authority.
