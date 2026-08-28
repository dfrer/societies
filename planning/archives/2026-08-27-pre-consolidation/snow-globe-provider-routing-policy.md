# Snow Globe provider-routing policy outcome contract

## Outcome

Add a deterministic, provider-neutral pre-dispatch routing policy inside the isolated Snow Globe lab. The policy consumes the versioned cognition-quality comparison and explicit operational intent/readiness facts, selects OpenRouter as the preferred provider when the accepted evidence supports it, and permits Ollama only for explicit local/offline operation or when OpenRouter is known unavailable before any dispatch. It must never turn an attempted, possibly submitted, or uncertain OpenRouter operation into an automatic Ollama retry.

## Verified starting point

- Isolated worktree: `E:\AIExperiments\games\societies-codex-cognition-quality`.
- Branch: `codex/snowglobe-provider-routing-policy`, created from `origin/master` at `1d6bbf3558fdd7742488551cc7f734540b924b8e`.
- The worktree was clean before this contract was added. Unrelated dirty work in `E:\AIExperiments\games\societies` remains untouched.
- PR #163 merged the reviewed cognition-quality artifact and recommendation: OpenRouter 8,341/10,000, Ollama 4,444/10,000, deterministic result `openrouter_default`.
- The canonical comparison artifact is `artifacts/snowglobe/cognition-quality/provider-comparison-v1.json`, SHA-256 `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`.
- Both providers remain compatible only through the isolated Snow Globe recording/proposal contract. Deterministic validation remains the sole world-state authority.

## Scope

- Add one deep, synchronous routing-policy Module under `labs/Societies.SnowGlobe` with a small interface and no provider Adapter dependency.
- Consume and validate the existing canonical comparison artifact through its committed validator; do not accept a caller-supplied winner string or score.
- Accept only closed operational intent, pre-dispatch readiness, and primary-attempt-state values.
- Emit a versioned, canonical, bounded, raw-free decision containing the selected provider or no provider, exact reason code, comparison/recommendation binding, and limitation codes.
- Add red-first tests under `tests/Societies.SnowGlobe.Tests` and milestone documentation.

## Routing rules

- `preferred_online` with a supported `openrouter_default` comparison selects OpenRouter only when OpenRouter is ready and the primary attempt has not started.
- `preferred_online` may select Ollama only when OpenRouter is explicitly not ready, Ollama is ready, and the primary attempt has not started. This is pre-dispatch availability fallback, not retry.
- `local_only` selects Ollama when Ollama is ready and the primary attempt has not started; it never probes or selects OpenRouter.
- Any state indicating dispatch started, submission possible, submission unknown, or completion denies fallback and selects no provider.
- Missing, malformed, unsupported, or asymmetric comparison evidence; `conditional_routing`; `insufficient_evidence`; unknown enum values; unavailable selected providers; and incoherent inputs fail closed with no provider.
- The policy never performs execution. A decision does not grant provider, credential, payment, network, retry, world, or gameplay authority.

## Non-goals

- No `src/societies/` change and no Godot/gameplay integration.
- No HTTP, socket, process, file discovery, credential, account, payment, provider call, model execution, availability probe, journal mutation, or world mutation.
- No automatic retry, post-dispatch fallback, alternate request, speculative/parallel dispatch, category-level routing, cost optimization, deployment, or release configuration.
- No change to OpenRouter Azure-only/ZDR, one-attempt, cost, credential, or evidence controls; no change to Ollama loopback safeguards.
- No claim that Ollama ties or exceeds OpenRouter quality, or that the fixed corpus establishes general intelligence or commercial readiness.

## Acceptance criteria

1. A versioned deterministic routing schema and canonical decision codec exist.
2. The Module validates the comparison artifact and uses its recommendation rather than caller-provided scores or identity preferences.
3. Tests cover OpenRouter default, explicit local-only, pre-dispatch Ollama fallback, no-provider readiness, malformed/missing comparison, unsupported/tied recommendation, unknown enum inputs, attempted/unknown submission denial, equal-input repeatability, canonical tamper rejection, and bounded/raw-free output.
4. Provider output remains untrusted and deterministic validation remains authoritative.
5. The public surface exposes no Adapter, delegate, transport, credential, cost, endpoint, model, retry, file, journal, Task, or world control.
6. Focused and full Release validation pass, and independent review reports no unresolved P0-P2 findings.
7. `CURRENT_BUILD.md`, `WORKFLOW.md`, and lab documentation state exactly what is proven and what remains unintegrated.

## Review and delivery

- A bounded `security_worker` owns implementation and focused red-first tests because this changes the provider-selection seam. The worker must not invoke providers, access credentials, stage, commit, push, or spawn workers.
- An independent `deep_reviewer` reviews fail-closed semantics, post-dispatch denial, canonical evidence binding, public-surface authority, determinism, tests, and documentation.
- The main task owns integration, full validation, milestone documentation, Git/PR delivery, required-check monitoring, merge, and user communication.
- Completion requires a reviewed PR merged to `master`, a clean synchronized worktree, no `src/societies/` change, and no provider action.

## Implemented and reviewed result

- `ProviderRoutingPolicyModule` exposes only synchronous `Decide` and `Validate` operations. It validates and digest-pins the accepted comparison, implements the exact routing matrix above, and emits a strict 4 KiB/depth-5 canonical decision with no execution authority.
- Red-first compilation failed on the absent policy interface. Initial focused implementation passed 16/16.
- Independent review found one P2 mutable-input binding gap: caller-owned memory was read separately for hashing and validation. The adversarial regression failed 1/17 with five memory reads. The security owner repaired it by taking one owned snapshot, using it for both hash and validation, and zeroing it in `finally`; focused validation is now 17/17.
- Final validation is 1128/1128 full Snow Globe Release, 59/59 Recording CLI Release, 100/100 OpenRouter CLI/security, and three Release builds with 0 warnings/errors. Diff checking is clean and no `src/societies/` path changed.
- Independent `deep_reviewer` re-review is FINAL GO with no unresolved P0-P3 findings. The remaining limit is explicit: readiness and attempt state are caller-supplied advisory facts, and a routing decision does not authorize execution.
- No provider, credential, account, network, runtime, paid, live-state, journal, or world action occurred. PR delivery is the remaining gate.
