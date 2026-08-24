# Snow Globe provider-routing policy v1

## Purpose and authority boundary

`ProviderRoutingPolicyModule` is a pure synchronous pre-dispatch decision boundary in the isolated Snow Globe lab. It validates a canonical cognition-quality comparison, combines that evidence with closed caller-supplied intent/readiness/primary-attempt facts, and emits one canonical decision. It does not load the comparison from disk, probe availability, invoke either provider, acquire credentials, reserve funds, retry, write a journal, or mutate a world. A selected-provider value is advice only and grants no execution authority. Any later provider output remains untrusted and must pass deterministic validation before it can affect simulation state.

Policy schema: `snow_globe_provider_routing_policy/v1`. Decision schema: `snow_globe_provider_routing_decision/v1`. Policy digest: `fc83ef910dd2a23382165975d1560f2ba6d327fdfeb53c3f09149b4c2b0c3499`.

## Comparison admission

The caller supplies canonical comparison bytes, never a winner string or score. The Module calls `CognitionQualityComparisonModule.Validate` and supports only the committed comparison artifact at `artifacts/snowglobe/cognition-quality/provider-comparison-v1.json`, SHA-256 `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`, schema `snow_globe_cognition_quality_comparison/v1`, complete symmetric evidence, and recommendation `openrouter_default`.

Missing, malformed, oversized, asymmetric, conditional, insufficient, Ollama-default, or otherwise valid but non-committed comparison evidence selects no provider. The decision records only the closed comparison status, bounded digest/schema/recommendation binding, and no proposal, prompt, response, reasoning, provider metadata, score detail, or credential material.

## Closed inputs and routing

Intent is `preferred_online` or `local_only`. Each readiness fact is `ready`, `not_ready`, or `unknown`. Primary state is `not_started`, `dispatch_started`, `submission_possible`, `submission_unknown`, or `completed`. Undefined enum values serialize only as `invalid` and select no provider; their numeric value is not retained.

| Intent/state | Readiness | Decision |
|---|---|---|
| `preferred_online`, `not_started` | OpenRouter `ready` | OpenRouter, `preferred_openrouter_ready` |
| `preferred_online`, `not_started` | OpenRouter `not_ready`, Ollama `ready` | Ollama, `pre_dispatch_openrouter_unavailable` |
| `local_only`, `not_started` | Ollama `ready` | Ollama, `explicit_local_only`; OpenRouter readiness is not used |
| either intent | dispatch started, submission possible/unknown, or completed | no provider; post-dispatch fallback is denied |
| otherwise | unavailable, unknown, incoherent, or unsupported evidence | no provider with one closed reason code |

Ollama availability fallback is therefore possible only before any OpenRouter dispatch. It is not a retry, alternate request, speculative dispatch, or response to an uncertain submission.

## Canonical decision contract

The decision is strict UTF-8 canonical JSON, at most 4 KiB and depth 5. Exact ordered fields bind schema/status, policy digest, normalized intent/readiness/primary state, comparison status and digest/schema/recommendation, nullable selected provider, exact reason, limitations, and a final payload digest. Parsing rejects duplicate properties, trailing content, non-canonical scalar encoding, unknown values, incoherent comparison bindings, integrity-valid selected-provider/reason tampering, malformed JSON, excessive depth, and oversized input. Public byte and list surfaces are detached copies.

Fixed limitations state that readiness is caller-supplied rather than probed; comparison digests prove integrity rather than execution authenticity; fallback is pre-dispatch only; and the decision grants no provider, credential, payment, network, retry, parallel dispatch, gameplay, or world authority.

## Evidence and non-goals

Focused offline tests cover the accepted OpenRouter default, explicit local-only, pre-dispatch Ollama availability fallback, unavailable/unknown readiness, every post-dispatch denial state, missing/malformed/asymmetric/conditional/insufficient/unsupported comparison evidence, unknown enums, repeatability/culture stability, strict canonical validation, bounded/raw-free output, integrity-valid tampering, and public-surface authority. They use only the committed comparison artifact and offline synthetic comparison artifacts; no provider, credential, account, network, runtime, payment, or live-state action is performed.

This lab policy is not integrated with `src/societies`, does not prove provider availability or execution, and makes no general-quality, cost, deployment, or commercial-readiness claim.
