# Snow Globe cognition-quality benchmark and provider-selection outcome contract

## Outcome

Produce a deterministic, provider-neutral comparison of Ollama `qwen3.5:4b` and OpenRouter `openai/gpt-5.6-luna` on the same frozen twelve Snow Globe cognition scenarios. Publish an evidence-backed recommendation for a default provider, conditional routing policy, or `insufficient_evidence` without granting either model world-state authority.

## Verified starting point

- Fresh worktree: `E:\AIExperiments\games\societies-codex-cognition-quality`.
- Branch: `codex/snowglobe-cognition-quality-benchmark`, created directly from `origin/master` at `1d279ea44bdde5574d9d2bea449a8ceba7c3a5c1`.
- `origin/master` contains provider-completion merge `1d279ea` from PR #161 and its parent merge `7897adf` from PR #160.
- The worktree was clean before this contract was added; unrelated dirty work in `E:\AIExperiments\games\societies` is preserved and untouched.
- The accepted Ollama artifact exists at `C:\Users\hunte\.codex\worktrees\b452\societies\artifacts\snowglobe\local-model\qwen3.5-4b-recording-execution-v5.json`, is 16,148 bytes, and matches SHA-256 `448af70b6ac262e67ddd0a6da3c76174d15faf0a2c771e2ca7a57bffb596cf57`.
- The accepted OpenRouter artifact exists in the fixed v2 generation `g2-fab8d9bc51468be6d63d5f00b864a1060b360a1e6d75e69ee2c7549e920094a4`, is 8,497 bytes, and matches SHA-256 `ebbcc39d8e1ab7d0ee926600753aeaa5e420f70c5b14dee62b57614945c65e51`.
- Both artifacts bind the same prompt publication, prompt set, corpus, scoring contract, ordered scenario IDs, and proposal schema.

## Evidence sufficiency finding

- The accepted OpenRouter artifact retains one normalized `SnowGlobeActionProposal` for each of the twelve frozen scenarios. It retains no raw provider response or credential.
- The accepted Ollama v5 artifact deliberately retains only a validated, recomputable cognition-quality score summary. It retains no normalized proposal batch, raw response, or model text.
- The accepted pair is therefore sufficient to compare the existing v1 aggregate utility score, but insufficient to re-score both proposal batches through one new code path or calculate the required useful-variation measure symmetrically.
- No proposal will be inferred from a score, disposition, digest, style, verbosity, confidence, or model identity. Before any new model execution, add and review the smallest evidence extension that retains only the normalized proposal batch already admitted by the shared parser and deterministic validator.

## Owned slice

- `labs/Societies.SnowGlobe/`: a deep, synchronous provider-neutral evaluation Module; a versioned comparison schema and rubric; strict bounded codecs for the accepted evidence inputs and normalized proposal evidence; and the smallest additive Ollama execution-artifact/recording projection needed to retain normalized proposals.
- Snow Globe test projects and, only if needed for bounded offline generation/validation, the existing Snow Globe recording CLI.
- `artifacts/snowglobe/cognition-quality/`: one canonical comparison artifact plus only the minimum reviewed normalized-proposal evidence required for reproducibility.
- Milestone documentation: this contract, `labs/Societies.SnowGlobe/README.md`, `CURRENT_BUILD.md`, and `WORKFLOW.md`.

## Non-goals

- No change under `src/societies/`, no Godot/gameplay integration, and no state mutation outside reconstructed scratch validation.
- No raw prompt, raw model response, reasoning text, prose/style score, confidence score, provider error text, credential, account secret, or unrestricted provider metadata in the new evidence.
- No automatic retry, fallback, alternate provider/model, parallel provider call, thirteenth scenario call, unbounded cost, deployment, release, commercial-readiness, general-intelligence, or world-authority claim.
- No reinterpretation or rewrite of the accepted Ollama v5 or OpenRouter artifacts. Historical hashes and schemas remain valid.
- No new OpenRouter paid inference is planned: its accepted artifact already contains the required normalized proposals. Any evidence that unexpectedly requires another paid call must retain the standing Azure-only, ZDR-required, sequential one-attempt, no-retry/fallback/alternate, 18,000-microusd ceiling and stop-on-uncertainty controls.

## Versioned evaluation contract

- Both providers enter the evaluator as the same exact ordered twelve-scenario normalized proposal batch with explicit artifact provenance and digest binding.
- The evaluator uses the frozen `snow_globe_cognition_quality_corpus/v1`, the existing deterministic proposal contract, and reconstructed `snow_globe_validate_and_commit_v1`; provider output remains untrusted.
- The rubric reports, separately and explainably: schema validity, command legality, goal relevance, resource feasibility, safety on the frozen restraint scenarios, and useful action variation across scenario demands. Style, verbosity, reasoning length, confidence, latency, price, and provider identity contribute zero quality points.
- Automated results and any human judgment are separate fields. The canonical milestone artifact contains automated evidence only; human judgment is absent unless explicitly recorded as non-scoring commentary.
- Recommendation logic is deterministic and versioned. Missing/malformed/asymmetric evidence, an exact score tie without a contract-defined discriminator, or a margin below the contract threshold yields `insufficient_evidence` rather than an arbitrary winner.
- A default-provider recommendation requires a strictly supported corpus result. A conditional-routing recommendation requires category-level complementary evidence defined by the rubric, not intuition.

## Red-first acceptance

- First capture failing focused tests for the intended new schema/evaluator behavior before production implementation.
- Tests cover equal inputs, exact score ties, malformed and missing evidence, wrong order/count/scenario/provenance, illegal proposals, deterministic feasibility rejection, deterministic repeatability across culture/concurrency, useful-variation calculation, recommendation thresholds, and canonical byte/digest validation.
- Historical Ollama v5 and OpenRouter accepted artifacts continue to validate byte-for-byte without schema reinterpretation.
- Both provider proposal batches run through the same evaluator entry point and identical rubric.
- One canonical comparison artifact records per-scenario criterion results, category and provider aggregates, source hashes/provenance, recommendation, automated-versus-human separation, and limitations. It contains no secret or raw provider response metadata.
- Focused Release tests, the full Snow Globe Release suite, relevant CLI/security suites, relevant Release builds, and `git diff --check` pass.
- Independent review reports no unresolved P0-P2 findings.

## Provider/security gate and evidence generation

- One bounded `security_worker` owns the provider-evidence seam, evaluator implementation, and focused red-first tests. It may edit only the owned Snow Globe lab/test/CLI/doc slice, must preserve all unrelated work, must not stage/commit/push, must not access credentials or invoke a provider, and must not spawn workers.
- After implementation is locally green, an independent `deep_reviewer` reviews security, deterministic scoring, evidence symmetry, schema compatibility, and recommendation correctness. Any substantive fix returns to the owning `security_worker`, followed by re-review.
- Only after CODE GO may the main task invoke one new Ollama preflight -> record-once -> validate sequence if the accepted v5 evidence cannot be extended offline. It remains sequential, one attempt per each of exactly twelve scenarios, no retry/fallback/alternate/thirteenth request, and uses the exact registered loopback runtime/model identities. The resulting normalized-proposal evidence is separately hashed and validated.
- The main task does not invoke a new OpenRouter generation unless later reviewed code proves the accepted artifact unusable. Any such change requires a fresh security review and deep review before use.

## Evidence and delivery boundary

- Main task owns artifact inspection, outcome-contract maintenance, provider/runtime invocation, full validation, comparison generation, documentation reconciliation, Git commits, PR/check monitoring, merge, and final user communication.
- Deliver reviewed implementation and any required evidence extension before generating new provider evidence. If preserving that sequencing requires an implementation PR followed by an evidence/documentation PR, use both and wait for required checks before each merge.
- At the milestone boundary, `CURRENT_BUILD.md` and `WORKFLOW.md` state exact source/artifact hashes, automated results, recommendation, limitations, validation, PR/check/merge status, and the one practical next action.
- Completion means the final PR is merged to `master`, required checks pass, `src/societies/` is unchanged, and the final recommendation is supported strictly by retained evidence.

## Reviewed offline implementation gate

- A `security_worker` implemented the normalized-proposal evidence schema, provider-neutral comparison Module, additive Ollama v6 projection, focused red-first tests, and lab contract documentation without invoking a provider or changing `src/societies/`.
- Four red-first repair cycles closed: sub-500 conditional routing; impossible undefined-action evidence; exact accepted-v5 regression coverage; preservation of completed defined-action contract-illegal proposals; binding the 4,000 conditional floor into the rubric digest; and preservation/scoring of completed parser `no_proposal` slots.
- The final normalized schema retains exactly twelve ordered slots, each containing a source-parser-representable proposal or canonical null. It rejects undefined enum actions and malformed/corrupt evidence, while retaining wrong-agent or defined-action invalid-quantity proposals for deterministic command-legality scoring.
- Final validation is 4/4 targeted, 162/162 broad, 1111/1111 full Snow Globe Release, 59/59 Recording CLI Release, Snow Globe and Recording CLI Release builds with 0 warnings/errors, and clean diff checking. The exact accepted v5 file remains 16,148 bytes/SHA-256 `448af70b6ac262e67ddd0a6da3c76174d15faf0a2c771e2ca7a57bffb596cf57` and validates with no inferred normalized evidence.
- Independent `deep_reviewer` re-review is FINAL GO with no P0-P3 findings. The residual risk is live evidence only: one reviewed, delivered Ollama v6 recording is required before comparison and provider selection.
