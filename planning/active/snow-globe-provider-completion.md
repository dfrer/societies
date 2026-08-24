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
- One local preflight, one record-once, and one validate completed against the exact registered loopback runtime. Raw-free v5 artifact: 16,148 bytes/SHA-256 `448af70b6ac262e67ddd0a6da3c76174d15faf0a2c771e2ca7a57bffb596cf57`; receipt `835dda47c070d60fb29376b7a51c321be8b82a5e9d63c471f9e3e1fca3a0a8b1`; 12/12 complete; zero retry/fallback/alternate.
- Historical v4 remains 16,148 bytes/SHA-256 `fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e`.
- The implementation/local slice merged through PR #147 as `0a59339`; required `build-test-smoke` passed in 4m19s.
- Generation 1 then consumed one zero-I/O plan, one preflight, one paid record-once, and one local validate. Authorization `331ed7373ae3938d8fd13603a199b064d84b1d760836efd83ed4868846a2418c`, generation `g2-c8fb9caaa6c1e83cba7a553fb7b9e1744d0d847b5f0006e83ad885b8cfeb33f3`.
- It stopped after exactly one Azure-only ZDR exchange with `provider_response_rejected_response_native_finish_reason_not_stop`; no retry/fallback/alternate or accepted proposal. Evidence SHA-256 `500c27a40a37e9cfaa49066ca4d293b5c337bce36003271683b21452a601d347`; receipt `e6a717dd6825fd0d8d2f0973b1c798b2cd197bc64ff00dbce5630ddac6bb2abb`; journal `fcbef4a12b3753983859a018c33ce27985e4eda3e38e819b9463582b326cf2c2`.
- Charge is Unknown; zero local settlement is not zero cost. Generation 1 must never be rerun.
- The exact native-finish correction is implemented: normalized `finish_reason=stop` remains mandatory; optional native metadata must be a bounded string but its value is non-authoritative and unretained; historical native-non-stop terminal validation remains intact.
- Correction evidence passes 196/196 focused OpenRouter tests, 98/98 full OpenRouter CLI/security tests, 59/59 Recording CLI tests, 1012/1012 full Snow Globe Release tests, relevant Release builds with 0 warnings/errors, and clean diff checks. Independent deep security/public-contract re-review is FINAL GO with no unresolved P0-P3 findings; merged PR delivery remains before generation 2.

## Generation 2 terminal and third-generation correction contract

- The native-finish correction merged through PR #148 as `c6429bc`; required `build-test-smoke` passed in 4m15s. From that exact merged source, Release rebuilt with 0 warnings/errors and the zero-I/O plan, preflight, paid record-once, and local validate each ran exactly once.
- Authorization `5fa1e4853f7fb24679c3bbf7f7f0bad1783030a5c61fdfcdc01ab3d7e8d1f8a5`, generation `g2-c40ab7d6aa310f99608a4ee935258c1b74087fe0c3e8089b04ea79f65e57fb25`.
- Exactly one Azure-only ZDR exchange stopped `provider_response_rejected_response_json_unknown_property`; no retry, fallback, alternate, second slot, or accepted proposal. Evidence is 2,233 bytes/SHA-256 `71953be66499f6bc0e163b12a9d3329d7eb7e96ac1d1ec924a5221a6eb11909f`; receipt is 793 bytes/SHA-256 `c593ec52d829b888829fe20a8fc0a9a75e05900fec0e307b1091eb95a9b0d0e8`; journal is 2,864 bytes/SHA-256 `bb1b94f09c1377b212e1437d789e62df27dc64077c7a24dd065f090faf88129b`, final checksum `61a29f34bb4c5972a3cde7e5204a3be024190a084e4c1acee2734504cc513957`.
- Submission and charge are Unknown, token and local-settlement counters are untrusted zero, proposal is null, and the response digest is nonzero. Generation 2 and all its stages are consumed and must never be rerun.
- Current official Chat Completions documentation includes additive usage fields absent from the local allowlists: `usage.is_byok`, `usage.server_tool_use_details`, and prompt/completion breakdown members under `usage.cost_details`. The raw-free generation-2 diagnostic does not identify which documented additive field was present.
- Before generation 3, add only bounded typed validation for those current documented usage members: require `is_byok=false`; require server-tool requested/executed counts to be zero if present; accept only documented nonnegative nullable cost breakdown fields consistent with the existing trusted total-cost ceiling. Preserve strict unknown-property rejection everywhere else, exact proposal schema, routing/provider/ZDR binding, total cost/token gates, first-terminal stop, no retry/fallback/alternate, secrets, raw-free evidence, and all historical terminal validation.
- Prove the correction red first with current documented fixture shapes plus malformed/type/range/nonzero-tool/cost-inconsistency cases; run focused/full Release gates and independent deep review; merge delivery before the third and final authorized generation.
- The correction is implemented and green. It admits only the documented typed/zero/bounded/nullable usage forms above, does not retain them, and keeps unknown-property rejection elsewhere. Evidence passes 16/16 new usage cases, 21/21 narrow bridge cases, 213/213 focused OpenRouter, 100/100 CLI/security, 1029/1029 full Snow Globe, both relevant builds with 0 warnings/errors, and clean diff checks. Independent deep re-review is FINAL GO with no unresolved P0-P3 findings; PR #149 merged it before generation 3.

## Final generation and blocked delivery boundary

- The correction merged through PR #149 as `d23a6cf`; required `build-test-smoke` passed in 4m23s. From that merged source, Release rebuilt 0/0 and the zero-I/O plan, third preflight, paid record-once, and local validate each ran exactly once.
- Authorization `057f82366a4b9846aa2371f7de31749bc9e951bf5ba936de0990298232d54ddf`; preflight artifact 2,344 bytes/SHA-256 `46a65a796fffe693324a53f46de4e4ca519ebf6928a94ca1ad8bcfc6761e017b`; generation `g2-74906e534c14cf9255a7e41ecca9012ec941ec2e7c4b4c2c8cda359be45da9b8`.
- One Azure-only ZDR exchange stopped `provider_response_rejected_response_json_unknown_property`; no retry/fallback/alternate, second slot, or accepted proposal. Evidence is 2,233 bytes/SHA-256 `f295600120bce3c4ada87dc645aeb1280d56c5cd31bd3a4f6d5afbda63c584b1`; receipt `b4671467bc82bb1742f7a9840952392df70db847d4e259b1aa54ecbc5ffea064`; journal 2,864 bytes/SHA-256 `54e6ddd51417023e454c3d03a02e378db923c4d9502e6174769b6ed5b168b07b`, final checksum `9389bd3abe3e3c2b657477331090530938bdc64924c0c6d7991cbb3c7b82fcf3`.
- All three authorized generations and stages are consumed. Exactly three paid exchanges occurred, one per generation. Aggregate authorization was capped at 54,000 microusd; each dispatched one 1,500-microusd-reserved slot, but provider charges are Unknown and zero local settlement is not zero cost.
- Accepted local Ollama compatibility is proven 12/12. Accepted OpenRouter compatibility is not proven. The final raw-free diagnostic remains too broad to identify the undocumented property, so the milestone is blocked at the OpenRouter provider-completion boundary.
- No fourth generation is authorized. The one practical future action is to design, test, independently review, and deliver location-specific raw-free unknown-property diagnostics before requesting any fresh paid authority.
