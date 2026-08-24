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

## Historical generation 2 terminal and third-generation correction contract

- The native-finish correction merged through PR #148 as `c6429bc`; required `build-test-smoke` passed in 4m15s. From that exact merged source, Release rebuilt with 0 warnings/errors and the zero-I/O plan, preflight, paid record-once, and local validate each ran exactly once.
- Authorization `5fa1e4853f7fb24679c3bbf7f7f0bad1783030a5c61fdfcdc01ab3d7e8d1f8a5`, generation `g2-c40ab7d6aa310f99608a4ee935258c1b74087fe0c3e8089b04ea79f65e57fb25`.
- Exactly one Azure-only ZDR exchange stopped `provider_response_rejected_response_json_unknown_property`; no retry, fallback, alternate, second slot, or accepted proposal. Evidence is 2,233 bytes/SHA-256 `71953be66499f6bc0e163b12a9d3329d7eb7e96ac1d1ec924a5221a6eb11909f`; receipt is 793 bytes/SHA-256 `c593ec52d829b888829fe20a8fc0a9a75e05900fec0e307b1091eb95a9b0d0e8`; journal is 2,864 bytes/SHA-256 `bb1b94f09c1377b212e1437d789e62df27dc64077c7a24dd065f090faf88129b`, final checksum `61a29f34bb4c5972a3cde7e5204a3be024190a084e4c1acee2734504cc513957`.
- Submission and charge are Unknown, token and local-settlement counters are untrusted zero, proposal is null, and the response digest is nonzero. Generation 2 and all its stages are consumed and must never be rerun.
- Current official Chat Completions documentation includes additive usage fields absent from the local allowlists: `usage.is_byok`, `usage.server_tool_use_details`, and prompt/completion breakdown members under `usage.cost_details`. The raw-free generation-2 diagnostic does not identify which documented additive field was present.
- Before generation 3, add only bounded typed validation for those current documented usage members: require `is_byok=false`; require server-tool requested/executed counts to be zero if present; accept only documented nonnegative nullable cost breakdown fields consistent with the existing trusted total-cost ceiling. Preserve strict unknown-property rejection everywhere else, exact proposal schema, routing/provider/ZDR binding, total cost/token gates, first-terminal stop, no retry/fallback/alternate, secrets, raw-free evidence, and all historical terminal validation.
- Prove the correction red first with current documented fixture shapes plus malformed/type/range/nonzero-tool/cost-inconsistency cases; run focused/full Release gates and independent deep review; merge delivery before the third and final authorized generation.
- The correction is implemented and green. It admits only the documented typed/zero/bounded/nullable usage forms above, does not retain them, and keeps unknown-property rejection elsewhere. Evidence passes 16/16 new usage cases, 21/21 narrow bridge cases, 213/213 focused OpenRouter, 100/100 CLI/security, 1029/1029 full Snow Globe, both relevant builds with 0 warnings/errors, and clean diff checks. Independent deep re-review is FINAL GO with no unresolved P0-P3 findings; PR #149 merged it before generation 3.

## Historical final generation and blocked delivery boundary

- The correction merged through PR #149 as `d23a6cf`; required `build-test-smoke` passed in 4m23s. From that merged source, Release rebuilt 0/0 and the zero-I/O plan, third preflight, paid record-once, and local validate each ran exactly once.
- Authorization `057f82366a4b9846aa2371f7de31749bc9e951bf5ba936de0990298232d54ddf`; preflight artifact 2,344 bytes/SHA-256 `46a65a796fffe693324a53f46de4e4ca519ebf6928a94ca1ad8bcfc6761e017b`; generation `g2-74906e534c14cf9255a7e41ecca9012ec941ec2e7c4b4c2c8cda359be45da9b8`.
- One Azure-only ZDR exchange stopped `provider_response_rejected_response_json_unknown_property`; no retry/fallback/alternate, second slot, or accepted proposal. Evidence is 2,233 bytes/SHA-256 `f295600120bce3c4ada87dc645aeb1280d56c5cd31bd3a4f6d5afbda63c584b1`; receipt `b4671467bc82bb1742f7a9840952392df70db847d4e259b1aa54ecbc5ffea064`; journal 2,864 bytes/SHA-256 `54e6ddd51417023e454c3d03a02e378db923c4d9502e6174769b6ed5b168b07b`, final checksum `9389bd3abe3e3c2b657477331090530938bdc64924c0c6d7991cbb3c7b82fcf3`.
- All three authorized generations and stages are consumed. Exactly three paid exchanges occurred, one per generation. Aggregate authorization was capped at 54,000 microusd; each dispatched one 1,500-microusd-reserved slot, but provider charges are Unknown and zero local settlement is not zero cost.
- Accepted local Ollama compatibility is proven 12/12. Accepted OpenRouter compatibility is not proven. The final raw-free diagnostic remains too broad to identify the undocumented property, so the milestone is blocked at the OpenRouter provider-completion boundary.
- No fourth generation is authorized. The one practical future action is to design, test, independently review, and deliver location-specific raw-free unknown-property diagnostics before requesting any fresh paid authority.

## Resumed location-diagnostic outcome contract

### User outcome and reliability gate

- Resume the blocked OpenRouter completion milestone by replacing the single broad response-unknown diagnostic with a finite, raw-free code identifying only the strict response-object scope that rejected an unknown property.
- The diagnostic may reveal the predefined parser scope, but never the provider property name, value, response body, prompt, refusal text, credential, or any other provider content.
- Historical artifacts carrying `provider_response_rejected_response_json_unknown_property` remain canonical, readable, and unchanged.

### Implementation scope and non-goals

- Limit production changes to the OpenRouter response parser's existing strict allowlist boundaries and its closed terminal-code enum. Limit tests to deterministic fabricated response fixtures and historical compatibility coverage; update only the provider-completion milestone documents needed for delivery.
- Keep the proposal schema and proposal-specific unknown-property diagnostic unchanged. Preserve routing/provider/Azure/ZDR binding, cost/token ceilings, secret handling, bounded I/O, no redirects, first-terminal stop, durable claims, evidence validation, and no retry/fallback/alternate behavior.
- Do not touch `src/societies/`, disclose raw provider data, infer the unknown property, loosen an allowlist, add a provider or model, alter retained evidence, or invoke a live/provider stage before reviewed merged delivery.

### Red-first acceptance evidence

- First add a tight deterministic fixture loop that expects a distinct closed scope code at each strict response allowlist and therefore fails against the current broad diagnostic. Capture the exact filtered command and failing result before changing production code.
- Make the smallest production correction, then rerun the identical command green. Every emitted diagnostic is selected from a predefined finite set and contains no dynamic property or path text.
- Prove the historical broad code still validates; all existing malformed/type/range/routing/ZDR/cost/refusal/finish/security cases remain fail-closed.
- Run the focused OpenRouter tests, full OpenRouter CLI/security tests, full Snow Globe Release suite, relevant Release builds, and `git diff --check`. Require an independent `deep_reviewer` FINAL GO with no unresolved P0-P3 findings.
- Deliver and merge the correction through a `codex/` branch, PR, and required check before any live generation.

### Newly authorized governed generation

- After exact merged-source rebuild, authorize one fresh governed OpenRouter generation: Azure-only, ZDR-required, at most 12 sequential scenario slots, one attempt per slot, no retry, no fallback, no alternate provider/model, and a maximum reserved ceiling of 18,000 microusd.
- Invoke the zero-I/O plan once, live preflight once, paid `record-once` once, and local validate once. Stop immediately after any terminal or uncertain result and inspect only the retained raw-free evidence.
- This authorization covers exactly that one generation. It does not authorize rerunning a consumed stage or an additional generation. Provider charge remains Unknown unless independently settled; zero local settlement is never a zero-charge claim.

### Offline implementation gate

- Red-first Release evidence failed all fourteen fabricated response-scope expectations with `response_json_unknown_property`; the identical filtered command passes 14/14 after the direct mapping.
- The parser now emits one finite predefined code for each of the fourteen existing strict response allowlists. No allowed-property set or evaluation order changed; proposal parsing and historical broad-code validation remain exact.
- Focused parser/historical/exhaustive-vocabulary evidence passes 92/92. The full Snow Globe suite passes 1044/1044, OpenRouter CLI/security passes 100/100, shared Recording CLI passes 59/59, both relevant Release builds have 0 warnings/errors, and diff checks are clean apart from line-ending notices.
- No provider, network, credential, runtime, retained-artifact, paid, account, gameplay, or `src/societies/` action occurred during implementation or validation. Independent security/public-contract review was FINAL GO with no unresolved P0-P3 findings; PR #151 passed its required check in 4m31s and merged as `63288d2` before the consumed generation-4 sequence recorded below.

### Delivery boundary

- Main task owns documentation, provider/runtime invocation, evidence inspection, full validation, Git/PR/check/merge delivery, and final repository-state reporting.
- One bounded `security_worker` owns the parser/test correction and does not stage, commit, use credentials, call the provider, change live state, or spawn workers. One independent `deep_reviewer` reviews the complete security/public-contract diff before delivery.
- At the resulting milestone boundary, update `CURRENT_BUILD.md`, `WORKFLOW.md`, this contract, and the Snow Globe README with facts, validation, evidence digests, provider-stage counts, charge uncertainty, runtime state, delivery state, remaining blocker, and one practical next action.

## Historical generation 4 terminal and prompt-detail compatibility contract

### Consumed live result

- Location-diagnostic PR #151 passed required `build-test-smoke` in 4m31s and merged as `63288d2`. The OpenRouter CLI rebuilt from that exact merge with 0 warnings and 0 errors.
- The zero-I/O plan, authenticated preflight, paid `record-once`, and local validate were each invoked exactly once. Authorization `ae157e559ce8c95a9ac5ff331766f607565aae5144b74489f6266a031e5dd9fc`; generation `g2-bcec3e99ac2a434927a118ff446a8fce0b0e1f81bb66190b118550da9322b700`.
- Exactly one Azure-only ZDR exchange stopped `provider_response_rejected_response_usage_prompt_tokens_details_unknown_property`. No second slot, retry, fallback, alternate, or accepted proposal occurred. The authority and every stage are consumed; no rerun or additional generation is authorized.
- Raw-free evidence is 2,279 bytes/SHA-256 `711cb320634c8606768aa860a43a8109d5dd89c80d303b9f4d37dfe9c678d398`; receipt is 793 bytes/SHA-256 `6d7d576737c4d727bda20b52e90904afcb5c41a23b3664c77efcca11698bab9e`; journal is 2,887 bytes/SHA-256 `8659c1d5dc25e010e0338afd0171e40c46cba4f5cee9634af8c040e0a1fb94a7`, four records, final checksum `856017252e1734f7191f4e8f74ff363c4e79577b6df668644423ab614920dd7b`.
- Submission and charge are Unknown; trusted token and local-settlement counters are zero, proposal is null, response digest is nonzero, and both consumed claims are present. Zero local settlement is not a zero-charge claim.

### Evidence-backed compatibility outcome

- Current official OpenRouter Usage Accounting documentation identifies `cached_tokens`, `cache_write_tokens`, and `audio_tokens` in `usage.prompt_tokens_details`; all three are already admitted by the parser. The retained raw-free scope therefore proves an additive field in that non-authoritative detail object, but intentionally does not identify its name or value.
- Accept additive `usage.prompt_tokens_details` members only through the object's existing typed envelope: every value must be a JSON integer from zero through the fixed maximum input-token bound. The object remains bounded by the existing response-body, JSON-token, depth, duplicate-property, and string limits; none of its members is retained or used for trusted token or cost accounting.
- Preserve exact known-field behavior, `usage.prompt_tokens`, `usage.total_tokens`, cost ceilings, every other strict unknown-property scope, proposal schema, routing/Azure/ZDR binding, finish/refusal gates, evidence shape, secret handling, and no retry/fallback/alternate behavior.
- Do not make `completion_tokens_details`, server-tool details, cost details, routing metadata, errors, choices, messages, root, or proposal fields additive. Do not access provider content, infer or retain a dynamic property name, change `src/societies/`, or authorize another live generation.

### Red-first and delivery acceptance

- First add deterministic fabricated fixtures proving a previously unknown prompt-detail member is accepted only when its value is a bounded nonnegative integer, while unknown fields in all other scopes still fail with their exact finite codes. Capture the focused red result before production changes, then rerun the identical command green.
- Prove malformed, null, fractional, string, negative, over-bound, duplicate, excessive-size/depth/token, historical-artifact, and exhaustive typed-vocabulary cases remain fail-closed and canonical.
- Run focused OpenRouter parser/evidence tests, the full Snow Globe Release suite, OpenRouter CLI/security, shared Recording CLI, relevant Release builds, and diff checks. Require independent deep review FINAL GO with no unresolved P0-P3.
- Deliver through a reviewed `codex/` PR and required check. Then update repository truth with the exact consumed-generation evidence and the remaining boundary. No post-merge provider invocation is part of this slice.

### Offline implementation gate

- Red-first failed only the intended valid additive prompt-detail member with `response_usage_prompt_tokens_details_unknown_property`; seven malformed, out-of-range, and unchanged completion-detail cases passed. The identical command is green 8/8 after the one-scope typed-envelope correction.
- Focused parser, historical-artifact, and exhaustive-vocabulary evidence passes 99/99, including a shallow 260-token fixture that proves the configured 256-token ceiling fails raw-free as `response_json_token_limit`. The former prompt-detail location code remains valid historical vocabulary; thirteen other location-specific unknown-property scopes remain active and strict.
- Full Snow Globe Release passes 1051/1051, OpenRouter CLI/security passes 100/100, Recording CLI passes 59/59, both relevant builds have 0 warnings/errors, and diff checks are clean apart from line-ending notices.
- No provider, network, credential, account, paid, live-state, retained-artifact, gameplay, or `src/societies/` action occurred during this offline correction. Independent deep review is FINAL GO with no unresolved P0-P3 after closing stale-generation wording and the missing JSON-token-limit regression. PR #152 passed required `build-test-smoke` in 2m53s and merged the correction as `43c553a`.
- This completed the bounded offline follow-up and generation-4 evidence delivery. OpenRouter accepted compatibility remained unproven; at that boundary no additional generation was authorized and the next action required a fresh explicit paid decision. The standing continuation below supersedes only that former authority limit.

## Historical post-correction generation terminal

- The fresh authority checkpoint passed independent deep review, required `build-test-smoke`, and merged through PR #154 as `31a284b` before invocation. Release rebuilt from that exact source with 0 warnings/errors.
- Zero-I/O plan, authenticated preflight, paid `record-once`, and local validate each ran once under the fixed Azure-only/ZDR-required, 12-slot, one-attempt, no-retry/fallback/alternate, 18,000-microusd maximum-reservation contract. Plan digest `eea26a92d318a2ba102c7979d0cb44563d8bef967ae00b627bc6263ff59d759d`; authorization `b73c2447a22046d36123b7615832697557567c4e1dd74c75ae37587414660fb8`; generation `g2-28a74bf64ac12fc54ff6b5ad9e1597c98a17fe3d04c74abce2c239f734a8998b`; preflight artifact 2,344 bytes/SHA-256 `ea7a19664af8039bc3b827a775293d0b0c6ec19b2403e2f500c59fec77dd52e0`.
- Exactly one Azure-only ZDR exchange stopped `provider_error_terminal`; no second slot, retry, fallback, alternate, or accepted proposal occurred. Validation accepted 2,165-byte evidence SHA-256 `b5b02bf6ff3be1e00dd7001f8159dddeb26c60fc543a3eb43d6a1083e45a9b69`, 793-byte receipt SHA-256 `ad2bfc8a8f59b0c135f0b760770f4b537ddfd91d68cbd3665a03770908f3a73e`, and 2,830-byte/four-record journal SHA-256 `a279337d3edc2b4e2f71d1fc13d225bdc067bea44a7e10a3ef8cea08af3c88c6`; final checksum `cbf980f83f953661d32ff4731e509522517b67537deebc376aed800a7c406e85`.
- Submission and charge are Unknown; trusted tokens/local settlement are zero, proposal is null, response digest is nonzero, and both consumed claims exist. The classifier proves only an HTTP 200 structurally valid provider error object, not its exact provider code/message/metadata/body. The authority and every stage are consumed; no rerun or second generation is authorized, no exact local correction is justified, and accepted OpenRouter compatibility remains unproven.

## Standing provider-completion continuation

- The user granted standing authorization for all future work required to make both Ollama and OpenRouter functional. Apply that authority through sequential bounded generations rather than retries: each OpenRouter generation remains Azure-only/ZDR-required, at most 12 one-attempt slots, no retry/fallback/alternate, maximum 18,000 microusd reserved, with plan/preflight/record/validate invoked at most once each. Never rerun a consumed stage; stop each terminal/uncertain generation; review and merge every correction before the next paid verification.
- Ollama is currently functional on the frozen cell. Fresh preflight plan `c3da44aec1539b76e0dfe9d6c208f29116a9625567dee241fa1fff1f7d106138` and local validate accept the existing v5 artifact as `Complete`, SHA-256 `448af70b6ac262e67ddd0a6da3c76174d15faf0a2c771e2ca7a57bffb596cf57`. It binds the currently listening repaired-runtime PID/start identity, endpoint, runtime/model hashes, and 12/12 HTTP-200 result with zero retry/fallback/alternate. No new recording was created.
- The finite provider-error correction merged through PR #156 as `cb3aac0`; its governed plan/preflight/record/validate each ran once. Authorization `796151a2c4f95071d0577aebcc6e7c0c68fafa4dcd6fd035f078274f8ad7f39b`; generation `g2-73e72cc01b5140e1ff1709d647e8491061fde5193a090e6dedc8fafd7fab1e61`. One Azure/ZDR exchange stopped `provider_response_rejected_response_usage_completion_tokens_details_unknown_property`; no second slot, retry, fallback, alternate, or proposal occurred. Evidence/receipt/journal SHA-256 are `4f98fd4ffa8028a7bf8aafb02815e7adb55db5d7cb5ff831728dd511e35d48ad`, `1a6fd3891ecfa4ff7cb0e94d518dc71ecfd6928843b2b71ab13bca57d7f75de4`, and `f9f1059ff435aafd5a39122c1933033975ed5ddd952135aaa08021a039477562`; final checksum `f30b4cafe21c978ec578d877b36987050ecbda65002950a69f3880f9ae600cef`. Submission/charge remain Unknown; the generation and stages are consumed.
- The current correction makes only `completion_tokens_details` name-additive with unretained/non-authoritative integer values `0..MaximumOutputTokens`; every other strict scope and all security/financial/routing/no-retry controls remain unchanged. Red-first failed only the intended valid case while nine controls passed, then green 10/10. Focused 127/127, OpenRouter 263/263, full 1079/1079, CLI/security 100/100, Recording CLI 59/59, builds 0/0, and independent deep review FINAL GO/no P0-P3. No provider action used it. Deliver through reviewed PR/check, then run one governed generation from the exact merge.
- The two preceding OpenRouter bullets are historical. Completion-detail PR #158 merged as `5530fbc` after its replacement required check passed in 4m13s. Its governed plan/preflight/record/validate each ran once. Authorization `3f9633fdf0245db262e1488f8c4526cc7beafa985ce0b76cde509ec3d3f53611`; generation `g2-160a5f74500f20073f4f7efa771477258bd9913b8a763ae2792fbbcb831161d4`. One Azure/ZDR exchange stopped `provider_response_rejected_response_binding_invalid`; no second slot, retry, fallback, alternate, or proposal occurred. Evidence/receipt/journal SHA-256 are `25bba5c5cd8c44293e80816d2b514143657e219c0def785a877ec31e8daaebe0`, `ab6cc6320d57b49abdf8c4aaa71caa26b5006b7dc3f4b6ca35470e64ee1c4262`, and `f7dcde9445080b74c70cc2eb2a2fecdcf33f68bbbc2aeae007e832a07663f61f`; final checksum `fed2b4dd59ac5ce2a46a7c0d42979367463a9effccbf3359b586a568f6b7b379`. Submission/charge remain Unknown; the generation and stages are consumed.
- The current correction partitions the eleven exact binding gates into fixed location-only raw-free outcomes; every comparison and all security/financial/routing/no-retry controls remain unchanged. Red-first 0/11 becomes green 11/11. OpenRouter 275/275, full 1091/1091, CLI/security 100/100, Recording CLI 59/59, builds 0/0, and independent deep review FINAL GO/no P0-P3. No provider action used it. Deliver through reviewed PR/check, then run one governed generation from the exact merge.
