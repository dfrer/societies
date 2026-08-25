# Snow Globe Authenticated Provider Readiness Outcome Contract

## User outcome and cleared gate

- Add the smallest reviewed provider-neutral observation layer that can replace historical compatibility inference with a fresh, raw-free statement about current OpenRouter and Ollama readiness.
- OpenRouter readiness is established only by one bounded authenticated metadata cycle over the current key, selected model catalog, and exact Azure ZDR endpoint. Ollama readiness is established only by a current pinned Windows process/listener observation plus the exact installed-model identity and digest returned by a non-generating loopback metadata request.
- Preserve deterministic world authority. A readiness observation is untrusted routing evidence only; it cannot execute a proposal, mutate simulation state, authorize payment, or dispatch a provider request.

## Scope

- Keep production changes inside `labs/Societies.SnowGlobe` and the smallest existing provider CLIs needed to invoke and validate the observation. Keep tests under the matching Snow Globe test projects and retain only canonical raw-free evidence under `artifacts/snowglobe/provider-readiness/`.
- Introduce one deep Module with a small observe/validate interface. Provider-specific process, credential, transport, and parsing behavior remains behind production Adapters; deterministic fake Adapters exercise the same Module interface.
- Reuse the hardened OpenRouter metadata verifier: exactly three sequential authenticated GETs to the current-key, Azure/ZDR-filtered model, and ZDR-endpoint contracts; no redirect, proxy, cookie, ambient authentication, decompression, automatic retry, or completion request. Its existing same-account binding is the only permitted credential-store mutation.
- Reuse the pinned Ollama Windows process/listener verification and extract the exact `/api/tags` model check into a non-generating one-request Adapter. Require exact loopback endpoint, PID/start identity, executable path/hash, model alias, and accepted model digest.
- Emit versioned canonical evidence with observation time and expiry, per-provider readiness, closed raw-free diagnostics, contract/source identities, request counts, provider-action limitations, and artifact/payload digests. Do not retain credentials, response bodies, raw provider metadata, host paths, PIDs, ports other than the frozen endpoint identity, account identifiers, or dynamic provider error text.
- Extend the existing readiness assessment through canonical evidence validation only. If current readiness is proven, the assessment may record it, but primary-attempt state remains `unknown`, routing issuance remains `not_issued`, and `routing_policy_input` remains null.

## Non-goals

- Do not create or consume a provider generation, runtime authorization, paid request, completion request, benchmark, proposal, retry, fallback, alternate provider/model, model pull/update, deployment action, or gameplay integration.
- Do not call `OpenRouterPremiumV2ProductionBridge.PreflightAsync`, `RecordOnceAsync`, `OllamaBenchmarkRunner.RunAsync`, or any Ollama generate/recording operation.
- Do not infer a fresh routing attempt is `NotStarted` from absence. Do not add a durable routing-attempt ledger, dispatch permit, provider selection, or request execution in this slice. That is the next separate security milestone.
- Do not change `src/societies/`, the accepted cognition comparison, benchmark scores, provider recommendation, proposal schema, provider routing policy, historical evidence, credential format, financial journal, or established one-attempt safeguards.
- Do not claim continuous availability, future availability, general model quality, intelligence, price-performance, gameplay quality, deployment readiness, or commercial readiness.

## Acceptance criteria

1. A versioned deterministic current-readiness schema and contract digest exist.
2. OpenRouter and Ollama observations flow through the same deep Module interface and produce provider-neutral facts.
3. OpenRouter performs exactly three authenticated metadata GETs and zero POST/completion requests; Ollama performs exactly one loopback metadata request and zero generate requests.
4. Tests cover ready, unavailable, malformed/oversized metadata, identity drift, credential/provider failure, cancellation/timeout, equal deterministic inputs, caller-memory mutation, missing evidence, stale/expired evidence, and deterministic repeatability.
5. Every partial, ambiguous, raced, stale, malformed, or unknown result fails closed without raw metadata or secret retention.
6. The canonical assessment continues to report primary-attempt `unknown`, routing `not_issued`, and null routing input even when one or both providers are currently ready.
7. Provider/security implementation receives `security_worker` ownership and independent `deep_reviewer` review with no unresolved P0-P2 findings.
8. `CURRENT_BUILD.md`, `WORKFLOW.md`, and the Snow Globe README state exactly what was proven and preserve the isolated-lab limitation.

## Evidence and validation

- Red-first tests must fail only on the absent Module/Adapter behavior before production implementation.
- Iterate with focused Release tests for the new Module and each provider Adapter, then run the full Snow Globe Release suite, both provider CLI/security suites, relevant Release builds, canonical artifact validation, secret-like scans, and `git diff --check`.
- Provider observation is authorized only after the offline implementation passes security review, required pull-request checks, and merge. From that exact merge, run at most one sequential readiness cycle: one OpenRouter three-GET cycle and one Ollama one-GET cycle, with no retry or substitution. Stop on the first terminal or uncertain result for each provider.
- Record exact source commit, contract digest, observation request counts, artifact digest, validation outcome, limitations, and whether same-account binding occurred. Never record credentials or raw metadata.
- Independently review the retained evidence and documentation before its delivery pull request. No live rerun is permitted to repair evidence.

## Delivery boundary

- The main task owns scope, documentation, integration, live invocation, artifact inspection, Git/PR delivery, required-check waiting, merge, and final repository-state reporting.
- One bounded `security_worker` owns production/test implementation and focused validation. It must not stage, commit, access credentials, invoke providers, inspect live state, or spawn workers.
- One independent `deep_reviewer` reviews the complete security/public-contract diff before implementation delivery and the retained evidence before final evidence delivery.
- Deliver the reviewed offline implementation through a `codex/` pull request and required checks before any live observation. Deliver retained evidence and milestone documentation through a second reviewed pull request. Stop only on a terminal/uncertain provider observation, an unresolved P0-P2 finding, or an external blocker.

## Implemented offline boundary

- The reviewed implementation completes the provider-neutral observation Module, strict canonical v1 evidence, additive readiness-assessment v2, fixed provider Adapters, exact source/provenance binding, one-shot enforcement, and offline adversarial coverage.
- Review red-first repairs closed the OpenRouter credential-snapshot race, late Ollama trailers, raced post-dispatch identity classification, producer/validator time mismatches, provider-impossible diagnostics and request counts, incomplete Ollama provenance binding, and missing one-shot coverage.
- Final evidence is 106/106 focused core/readiness/Ollama Release, 104/104 OpenRouter CLI/security Release, 1158/1158 full Snow Globe Release, 59/59 Recording CLI Release, and three Release builds with 0 warnings/errors. Independent deep review is FINAL GO with no P0-P3 findings.
- The smallest safe production CLI and retained-artifact surface was intentionally not added to this security slice. Therefore the live observation and second evidence pull request described above remain a separate post-merge milestone; no provider, credential, process/listener, network, live-state, payment, routing, gameplay, or `src/societies/` action occurred here.
