# Snow Globe authenticated provider-readiness observation v1

## Purpose and authority boundary

`ProviderReadinessObservationModule` is the single provider-neutral observation and validation path for a fresh, point-in-time OpenRouter or Ollama readiness fact. Provider-specific authentication, runtime identity, HTTP, and response parsing remain behind internal Adapters. Deterministic fake Adapters use the same interface in offline tests.

The Module emits evidence only. It cannot select a provider, create `ProviderRoutingPolicyInput`, authorize or dispatch generation, retry, spend, write a journal, or mutate simulation state. No prompt, response, reasoning, proposal, raw metadata, credential, account identifier, dynamic error, PID, executable path, or host detail is retained.

Observation schema: `snow_globe_provider_readiness_observation/v1`. Contract schema: `snow_globe_provider_readiness_observation_contract/v1`. Contract digest: `361d3d2a9b07130929e106b58b87a5318f134661834c0265170a9c3e0724c1a5`.

## Provider observations

OpenRouter uses the existing hardened authenticated metadata verifier. One observation performs exactly three sequential `GET` requests: current-key metadata, the Azure/ZDR-filtered model catalog, and the exact Azure ZDR endpoint listing. The credential store is read exactly once: one owned secret snapshot supplies all three Bearer headers and the final binding writes that exact snapshot under the derived account identity without a fourth inconsistent read. The owned byte and character buffers are zeroed; the one managed Bearer string is a documented framework-owned residual that cannot be reliably zeroed. The Adapter is one-shot, performs no `POST` or completion request, has no redirect, proxy, cookie, ambient authentication, decompression, or automatic retry, and binds only after all three responses validate. Source schema `openrouter_authenticated_metadata_readiness/v1` is bound to contract digest `5d668803d3241f8853e23e9bf54bc1e8432339ec7fe79e046e5a16a490ab5044`. The response contracts follow the official [current-key](https://openrouter.ai/docs/api/api-reference/api-keys/get-current-key), [models](https://openrouter.ai/docs/api/api-reference/models/get-models), and [ZDR endpoints](https://openrouter.ai/docs/api/api-reference/endpoints/list-endpoints-zdr) documentation.

Ollama uses one exact HTTP/1.1 `GET http://127.0.0.1:11435/api/tags` with no authorization header, content, redirect, proxy, cookie, decompression, retry, generate, pull, or update. The one-shot production Adapter revalidates the pinned Windows process/listener identity before dispatch, at connection ownership, after headers, and after the exchange. A pre-dispatch rejection is the current negative `unavailable/runtime_identity_drift`; any connected or post-dispatch rejection is raced evidence and therefore `unknown/identity_race`, never `not_ready`. Response trailers are checked again after body EOF. The bounded reader zeroes its buffer and its `MemoryStream` internal raw-metadata buffer; the detached returned body is zeroed after parsing.

The strict extracted tags codec admits only the expected model alias, artifact digest and size, GGUF family/format, quantization, completion capability, and minimum context window from the official [`GET /api/tags` response](https://docs.ollama.com/api/tags). Codec schema `ollama_api_tags_metadata/v1` has contract digest `0dd5fd5590b3a3813181096e10c605a1fbddd4f8ea97eca4e983154170c26bfd`. Ollama source contract digest `66e0a77ae5c373a52d3d0d06f3a648df470c3128e07bb4ecd2ba066b1302860c` binds that codec plus the exact registered-cell/profile digests, runtime executable SHA, model alias, artifact SHA/size/format/family, quantization, and context pins. The body is limited to 64 KiB and JSON depth 5.

## Canonical evidence contract

The artifact is strict canonical UTF-8 JSON, at most 4 KiB and depth 4. Exact ordered fields bind:

- schema, contract schema, and contract digest;
- provider, observation time, and fixed 60-second expiry;
- closed readiness (`ready`, `unavailable`, or `unknown`) and diagnostic code;
- method `GET`, bounded provider request count, and generation request count zero;
- the provider-specific source schema and exact source-contract digest;
- the closed account-binding state, fixed limitation codes, and final payload digest.

Every caller input is size-checked, copied once into owned bytes, validated and hashed only from that snapshot, then zeroed. Hostile `MemoryManager` input is read exactly once. Validation rejects invalid UTF-8, duplicates, trailing content, unknown/reordered fields, noncanonical encodings, excessive size/depth, zero or out-of-domain observation time/expiry, mismatched provider request counts, forged source identities, provider-impossible diagnostics, incoherent readiness/account binding, payload tampering, and expired observations. Public bytes and lists are detached copies.

Provider and transport failures are reduced to provider-specific closed diagnostics. OpenRouter alone may emit credential/metadata unavailable codes; Ollama alone may emit pre-dispatch runtime drift, model-metadata rejection, and post-dispatch identity race. Cancellation, timeout, and unclassified failures are `unknown`; dynamic details are never echoed.

`request_count` is the number of HTTP requests whose dispatch was started, and validation binds it to the exact provider stop points. OpenRouter `ready` is 3; credential unavailable is 0 before dispatch, 1 after key inspection, or 3 at final account binding; provider unavailable and metadata rejected are 1..3; timeout is 1..3; cancellation and unclassified failure are 0..3. Ollama `ready` is 1; pre-dispatch runtime drift is 0; provider unavailable, model-metadata rejection, timeout, and identity race are 1; cancellation and unclassified failure are 0 or 1. No other provider/diagnostic/count combination is canonical.

## Readiness assessment v2

`ProviderRoutingReadinessEvidenceModule.AssessCurrent` extends the historical v1 assessment without changing its readability. Assessment schema `snow_globe_provider_routing_readiness_assessment/v2`, contract schema `snow_globe_provider_routing_readiness_evidence/v2`, and contract digest `cbb03e6379ace033dd52becbc1314473d427330b1278b023cb3ea3f708e12e5f` bind the historical assessment contract and this observation contract.

Accepted, unexpired observations project `ready`, `not_ready`, or `unknown`. Missing, malformed, wrong-provider, and expired evidence project `unknown`, never `not_ready` or a new-attempt state. Even if both providers are ready, the assessment remains `insufficient_current_attempt_evidence`, primary attempt state remains `unknown`, routing issuance remains `not_issued`, and `routing_policy_input` remains canonical null. Establishing authenticated attempt-bound state and issuing routing input are separate future work.

## Offline validation and invocation boundary

Offline tests cover deterministic provider-neutral fake results, exact source/provenance binding, single-read hostile caller memory, expiry and time-domain boundaries, malformed and oversized metadata, runtime drift versus identity race, trailers after EOF, internal-buffer zeroing, cancellation, timeout, provider-impossible diagnostics, raw-free output, OpenRouter one-credential/three-GET sequencing and concurrent credential replacement, both Adapters' one-shot closure, Ollama one-GET sequencing, no generation, current assessment projection, repeatability, and null routing input.

This slice intentionally exposes no CLI command and performs no provider, credential, network, process, listener, or live-state action. A later governed cycle may add the smallest invocation surface only after review and merge; it must preserve the same one-shot Adapters and stop without retry or substitution.
