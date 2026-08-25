# Snow Globe provider-routing readiness evidence v1

## Purpose and authority boundary

`ProviderRoutingReadinessEvidenceModule` is a pure synchronous classifier in the isolated Snow Globe lab. It accepts caller-owned canonical evidence bytes, validates each supplied artifact through its existing validator, and emits a detached canonical statement of what the evidence proves and what remains unknown. It does not inspect files, provider processes, listeners, credentials, accounts, journals, network state, or live provider state. It cannot invoke a provider, authorize a request, select a route, retry, spend, or mutate a world.

Assessment schema: `snow_globe_provider_routing_readiness_assessment/v1`. Contract schema: `snow_globe_provider_routing_readiness_evidence/v1`. Contract digest: `80a6e228280f3d8e4e75459279452049076fded3fe5709a7e5523a61a61200be`. The contract identity explicitly binds comparison schema `snow_globe_cognition_quality_comparison/v1` and accepted comparison SHA-256 `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`.

V1 always reports `insufficient_current_readiness_evidence`, both provider readiness values as `unknown`, current primary-attempt state as `unknown`, routing-input issuance as `not_issued`, and canonical `routing_policy_input:null`. It has no API that returns or accepts `ProviderRoutingPolicyInput`.

## Evidence admission and temporal meaning

Every nonempty input is size-checked before access, copied exactly once into owned bytes, hashed and validated only from that snapshot, and zeroed after classification. Missing bytes are not read. Oversized bytes are not read or hashed. Malformed evidence is reduced to a closed status and optional digest; raw bytes and validator details are never emitted.

| Evidence | Existing validator | Valid classification | What it does not prove |
|---|---|---|---|
| Required cognition-quality comparison | `CognitionQualityComparisonModule.Validate` | The exact accepted digest and `openrouter_default` establish `accepted_openrouter_default` selection evidence | Provider readiness, freshness, or attempt state |
| Optional OpenRouter activation preflight | `OpenRouterPremiumActivationPreflightArtifactModule.Validate` | Recorded eligible evidence becomes `evaluated_eligible_live_traffic_disabled`; recorded ineligible evidence remains `evaluated_ineligible` | Current readiness, live-traffic enablement, execution authority, or fresh trust context |
| Optional Ollama recording execution | `OllamaRecordingExecutionArtifactModule.Validate` | A completed 12-slot recording becomes `historical_compatibility_complete`; other valid terminal evidence remains historical | Current runtime, model, listener, or endpoint readiness |
| Optional OpenRouter execution | `OpenRouterPremiumEvidenceArtifactModule.Validate` | Complete or terminal evidence records only the state of its identified historical generation | A different proposed attempt being `not_started`, globally absent, or safe to dispatch |

Missing optional evidence is `missing`, current readiness stays `unknown`, and no `not_ready` conclusion is made. Malformed or unsupported evidence cannot improve the assessment.

The accepted comparison digest is unique in the assessment vocabulary: it can appear only with `status=accepted`, its exact schema, and `detail_code=openrouter_default`. Any non-accepted comparison fact carrying that digest is incoherent and rejected, including a fully re-digested assessment that relabels it malformed or unsupported.

## Gaps required before routing input could exist

Even when all four supplied artifacts validate, V1 reports these gaps in fixed order:

1. `current_openrouter_authenticated_readiness_unproven`
2. `current_ollama_runtime_readiness_unproven`
3. `authenticated_attempt_bound_primary_state_unproven`
4. `freshness_current_observation_unproven`

Missing or malformed inputs add closed artifact-specific gap codes. An unaccepted comparison adds `accepted_comparison_evidence_unproven`. These gaps describe absent proof; they are not provider-health findings.

## Canonical assessment contract

The assessment is strict UTF-8 canonical JSON, at most 8 KiB and depth 6. Exact ordered fields bind the schemas and contract digest; selection and four evidence facts; artifact digests and schema identities when valid; temporal scope; fixed unknown current-state values; null routing input; ordered gaps and limitations; and a final payload digest. Validation rejects duplicate properties, trailing content, unknown or incoherent values, noncanonical scalar encodings, excessive depth, oversized input, payload tampering, and structural changes. Public byte, list, and fact surfaces are detached.

The artifact contains no prompt, response, reasoning, proposal, provider metadata, credential, account binding, endpoint, model selector, path, journal content, or secret. Digests provide integrity rather than authenticity.

## Offline evidence and non-goals

Focused tests cover the committed comparison and completed Ollama v6 artifact, structurally valid detached activation evidence with live traffic disabled, a valid historical OpenRouter terminal generation, missing and malformed evidence, duplicate/deep/oversized input, unreadable oversized memory, changing caller memory, raw-free output, canonical tampering, repeatability, detachment, and public-surface authority. Test fixture construction performs no provider, credential, network, paid, file-discovery by production code, or live-state action.

This Module is not a readiness Adapter, route selector, provider preflight, attempt registry, state store, or execution gate. A future authenticated Adapter would require separately authorized current observations, freshness rules, and a durable attempt-bound aggregate before it could safely construct routing-policy facts.
