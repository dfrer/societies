# Snow Globe provider-completion outcome contract

## User outcome

Complete the existing isolated Snow Globe cognition-recording implementations so the pinned local Ollama cell and the fixed OpenRouter production bridge each produce accepted structured cognition proposals through the same deterministic recording and proposal-validation contract. Deterministic validation remains the sole authority over accepted proposals and world state.

## Verified starting point

- Worktree: `C:\Users\hunte\.codex\worktrees\b452\societies`.
- Branch: `codex/snowglobe-provider-completion` at synchronized `master` commit `e22924437f475b37d0fb7eb03c078d0007c7d6fd`.
- The branch was clean before this contract was added.
- The immutable sixth OpenRouter result remains `provider_response_rejected_response_finish_invalid`; it will not be reclassified, moved, deleted, reset, or rewritten.
- Ollama 0.18.2 was listening on `127.0.0.1:11434` with no listed model. No process was listening on the registered cell at `127.0.0.1:11435`.
- The repository's registered Ollama cell remains the pinned portable executable at `E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14\ollama.exe`, fixed executable/model digests, `qwen3.5:4b`, and loopback-only endpoint `http://127.0.0.1:11435/`.

## Shared completion contract

- Both paths use the existing cognition-recording interface, 12-scenario corpus, prompt publication, and strict proposal schema `snow_globe_cognition_quality_proposal_response/v1`.
- Every accepted proposal must pass the current deterministic recorded-response parser and validation rules before it can become evidence. Provider output never directly changes simulation state.
- The Ollama path completes one governed `preflight` -> `record-once` -> local `validate` sequence and retains a raw-free execution artifact proving 12/12 accepted proposals.
- The OpenRouter path completes the existing zero-I/O `plan` -> live `preflight` -> paid `record-once` -> local `validate` sequence and retains raw-free accepted evidence through the current production bridge.
- Existing historical OpenRouter and Ollama evidence remains readable and unchanged.

## Scope

- Inspect and correct only the existing Snow Globe cognition recording, Ollama loopback transport/composition/CLI, OpenRouter production bridge/profile/CLI, shared proposal schema, focused tests, and milestone documentation required to meet the outcome.
- Restore the registered local Ollama cell on loopback `127.0.0.1:11435`, download the pinned `qwen3.5:4b` model if absent, and validate its exact process/listener/model identity before use.
- Refresh and apply current official OpenRouter chat-completion, structured-output, routing, reasoning-exclusion, and ZDR requirements.
- Retain existing raw-free diagnostics, secret handling, bounded I/O, no-redirect, first-terminal stop, durable claim, and evidence-validation protections.

## Non-goals

- No `src/societies/` or gameplay integration unless repository evidence proves the lab outcome is otherwise impossible; that would require an explained scope change first.
- No parallel provider hierarchy, fallback provider, alternate model/provider, automatic retry, deployment, public release, commercial-readiness, zero-cost, general model-quality, or world-authority claim.
- No credential, prompt, provider body, refusal text, response body, or secret disclosure.
- No account-wide privacy/configuration change and no deletion, reset, relocation, rewriting, or reclassification of retained evidence.

## OpenRouter live contract and paid boundary

- Official documentation refreshed 2026-08-23: `POST https://openrouter.ai/api/v1/chat/completions`; `response_format.type=json_schema`; `json_schema.strict=true`; `provider.require_parameters=true`; `provider.only=[\"azure\"]`; `provider.allow_fallbacks=false`; `provider.data_collection=deny`; `provider.zdr=true`; non-streaming; reasoning excluded from the response.
- At most three fresh governed generations are authorized in this continuation.
- Each generation retains the existing maximum reserved ceiling of 18,000 microusd; total maximum reserved ceiling is 54,000 microusd.
- Generations are sequential, Azure-only, ZDR-required, no retry, no fallback, and no alternate provider.
- For each generation, invoke preflight once, record-once once, and local validate once. Stop after every terminal or uncertain result and inspect only its raw-free diagnostic.
- Never repeat an unchanged failed request/profile/source. A compatibility correction must be evidence-backed, tested, reviewed, committed, and delivered before a later governed generation.
- Stop after accepted evidence or the third governed generation with an exact blocker. Local zero settlement is never a zero-charge claim.

## Red-first acceptance evidence

- Add or update focused tests that fail under the current incompatible behavior and prove the smallest correction for the exact Ollama or OpenRouter diagnostic.
- Prove both adapters emit responses accepted by the same strict proposal parser for all required scenarios; malformed, truncated, extra-field, refusal, wrong-finish, wrong-provider, missing-ZDR, retry/fallback, oversized, timeout, and indeterminate cases continue to fail closed.
- Prove historical evidence compatibility and unchanged immutable artifact bytes/digests.
- Focused Ollama transport/composition/recording CLI tests pass.
- Focused OpenRouter production bridge/evidence/profile/CLI/security tests pass.
- Full Snow Globe Release suite and all relevant CLI/security suites pass.
- All relevant Release builds complete with zero warnings and zero errors; `git diff --check` is clean.
- Independent `deep_reviewer` returns FINAL GO with no unresolved P0-P3 findings after implementation and after any provider/security-contract correction.

## Delivery boundary

- Main task owns runtime/provider stage invocation, retained evidence inspection, current-state documents, full validation, Git/PR delivery, required-check monitoring, merges, and the final report.
- Provider/runtime implementation and focused tests are owned by one bounded `security_worker`; workers do not spawn workers and must preserve unrelated work.
- Update `CURRENT_BUILD.md`, `WORKFLOW.md`, `labs/Societies.SnowGlobe/README.md`, focused contracts, and consequential workflow issues at the milestone boundary.
- Deliver through `codex/` branches, commits, pushes, PRs, required checks, and merges. Finish with clean synchronized `master`, exact final HEAD, evidence digests, runtime state, paid-stage counts and limitations, PR/check results, and one practical next action.

## Reviewed implementation and local fixed point

- The shared proposal parser and additive Ollama v5 path are implemented. Historical v4 parsing and comparison/v2 canonical output remain exact; v5 comparison is additive v3.
- Offline Release evidence passes 172/172 changed provider/runtime/comparison tests, 59/59 Recording CLI tests, 97/97 OpenRouter CLI/security tests, and 1009/1009 full Snow Globe tests. Three Release builds have 0 warnings / 0 errors; diff checks are clean.
- Independent deep review is FINAL GO with no unresolved P0-P3 after one P1 fix preserved the exact historical v4 comparison report.
- One local preflight, one record-once, and one validate completed against the exact registered loopback runtime. Raw-free v5 artifact: 16,148 bytes/SHA-256 `448af70b6ac262e67ddd0a6da3c76174d15faf0a2c771e2ca7a57bffb596cf57`; receipt `835dda47c070d60fb29376b7a51c321be8b82a5e9d63c471f9e3e1fca3a0a8b`; 12/12 complete; zero retry/fallback/alternate.
- Historical v4 remains 16,148 bytes/SHA-256 `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`.
- No OpenRouter stage has been invoked under the fresh authority. Deliver this reviewed slice before the first governed OpenRouter generation.
