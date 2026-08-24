# Cognition Quality Corpus v1

Status: implemented offline v1 handoff. This document records fixed-corpus utility evidence only; it does not claim general intelligence, model quality, a provider winner, or price evidence.

## Purpose and scope

The Cognition Quality Corpus is a frozen twelve-scenario survival-progression corpus. Each scenario reconstructs a legitimate scratch deterministic world, exposes one bounded observation, accepts one candidate action proposal, and scores the proposal against observable progress. The world validator is authoritative for feasibility; the rubric is authoritative only for the bounded utility points assigned after feasibility.

The corpus is observation-only. It has no provider, model, network, file, payment, financial, or world-mutation authority. It does not judge prose and does not infer hidden history, private goals, future state, or coordination.

## Scenarios and observations

There are exactly four categories with three scenarios each:

- `shelter_acquisition`: `cq1`, `cq2`, `cq3`
- `shelter_construction`: `cq4`, `cq5`, `cq6`
- `storage_progression`: `cq7`, `cq8`, `cq9`
- `safe_restraint`: `cq10`, `cq11`, `cq12`

The canonical observation is limited to: `agent_id`, `home_slot`, `tick`, `available_wood`, `available_stone`, `stockpile_wood`, `stockpile_stone`, `shelter_count`, and `storage_count`. The candidate proposal is one action for the observed agent. The action must be a defined action kind; gather and maintain quantities are positive and at most 64, while idle, shelter construction, and storage construction carry quantity zero. No other state or narrative is admissible.

Scratch reconstruction is legitimate because the scenario publishes the canonical observation and detached state/event/observation identities; evaluation reconstructs the declared deterministic setup to check that the observation is the one being scored. Reconstruction does not grant the evaluator authority to invent state or alter the authoritative world.

## Feasibility and scoring

Every proposal is first checked against the observation contract. A contract failure receives `contract_invalid`. A contract-valid proposal is passed to the existing deterministic `ValidateAndCommit` boundary in the reconstructed scratch world. A rejected commit receives `domain_rejected`; no utility points are awarded.

Each scenario is worth 100 raw points. A proposal matching the scenario's preferred action receives:

- for gather actions: `25 + floor(75 * min(quantity, required_progress) / required_progress)`;
- for all other preferred actions: 100 points.

A validator-accepted proposal earning below the 100-point target receives `feasible_suboptimal`, including a preferred gather with only partial observable progress and any accepted non-preferred action. Non-preferred actions receive 10 points, except `Idle` when progress is available, which receives 25 points. A missing proposal receives zero points and `no_proposal`. The report converts raw points to basis points with `raw_points * 10000 / (scenario_count * 100)`, using integer arithmetic.

The five dispositions are exhaustive and exact:

- `no_proposal` — no candidate proposal was supplied.
- `contract_invalid` — the proposal violates the bounded proposal contract.
- `domain_rejected` — the deterministic validator rejects the proposal.
- `feasible_suboptimal` — the validator accepts a proposal whose score is below 100 points, whether it is a partially progressive preferred gather or a non-preferred accepted action.
- `maximum_utility` — the validator accepts the preferred action and the rubric awards its target utility.

## Submission and report binding

Evaluation consumes exactly one ordered submission for each of the twelve scenarios. Scenario ids must match the canonical corpus order; count, null envelope entries, and order are contract-bound. A submitted proposal AgentId is canonical only when it is 1..64 characters of ASCII lowercase letters, digits, or hyphen and matches the observed AgentId; any other AgentId is rejected by the bounded envelope/contract boundary rather than normalized into a different identity. The report binds the corpus content digest, scoring-rule digest, ordered scenario results, raw points, basis points, dispositions, limitation codes, and its own canonical payload digest. Canonical JSON is bounded to the published corpus/report limits and is emitted without provider or model text.

## Limitations and versioning

This is a bounded action-utility measure, not a general intelligence test. It cannot assess prose, explanation quality, long-horizon planning, durable memory, private goals, multi-agent coordination, unseen consequences, or cost-adjusted intelligence. It cannot establish a model IQ, smartest model, quality winner, or best intelligence per dollar. Provider absence is not a cognition disposition and must not be converted into one.

v1 content, scenario order, observations, scratch setups, validator boundary, scoring formula, disposition meanings, canonical submission binding, and report schema are immutable after publication of the corpus digest. Any semantic change requires a new v2 corpus and digest; v2 must not silently reinterpret v1 reports.

## Provider-neutral normalized evidence and comparison rubric v1

`snow_globe_cognition_quality_normalized_proposals/v1` is the raw-free interchange boundary for provider comparison. It accepts exactly twelve corpus-ordered source-parser slots from either the existing recording-evidence schema or the accepted OpenRouter evidence schema. Each slot has its exact scenario id and either a representable proposal or canonical JSON `null` when the bound response produced `no_proposal`. A present proposal requires canonical agent syntax, a defined action enum, and an integer quantity; undefined actions and malformed normalized-evidence shape fail closed. Parser-representable wrong observed agents and defined-action invalid quantities remain present so the deterministic evaluator can record `schema_valid=true`, `command_legal=false`, zero feasibility, and zero goal relevance. A null slot records `schema_valid=false`, `command_legal=false`, zero feasibility/relevance, safety failure when applicable, and no useful-variation contribution. It binds the frozen corpus/scoring/validator, exact prompt publication and prompt-set digests, proposal schema, source-evidence schema/digest, canonical slot batch, fixed limitations, and its own payload digest. It is limited to 16 KiB and JSON depth 6. It retains only scenario id plus nullable normalized `agent_id`/numeric `action`/integer `quantity`; it retains no prompt, raw response, reasoning, secret, provider metadata, style, verbosity, or confidence.

`snow_globe_cognition_quality_comparison/v1` evaluates both static roles through one exact code path and embeds both normalized-evidence objects in one canonical artifact. Its rubric identity is `snow_globe_cognition_quality_comparison_rubric/v1`; the canonical rubric digest is derived from the frozen rules and recommendation thresholds at runtime. The 10,000-basis-point automated score weights are schema validity 1,500; command legality 2,000; graded goal relevance 2,500; deterministic resource feasibility 2,000; safe-restraint behavior 1,000; and useful action variation 1,000. Useful variation counts feasible coverage of the five action demands actually preferred by the frozen scenarios; arbitrary action diversity earns nothing. Safety is applicable only to `safe_restraint` and requires feasible `Idle`.

Recommendation values are exactly `ollama_default`, `openrouter_default`, `conditional_routing`, and `insufficient_evidence`. Equal proposal inputs, an exact automated-score tie, missing/malformed/asymmetric evidence, or a margin below 500 basis points is insufficient. A default additionally requires at least 7,000 basis points. Conditional routing requires each role to win at least one category by 1,000 basis points, each provider to meet the named 4,000-basis-point conditional minimum score, and an aggregate margin from 500 through 1,500 basis points inclusive. The rubric digest binds nullable `no_proposal` schema-validity semantics, that conditional per-provider floor, and the category/aggregate thresholds. Human evaluation is separately `not_recorded` with scoring effect `none`; style, verbosity, confidence, reasoning length, latency, price, and provider identity are explicitly excluded from scoring.

The comparison artifact is bounded to 96 KiB and JSON depth 12. It records per-scenario criteria, per-category and per-criterion aggregates, exact source-artifact/evidence digests, the deterministic recommendation and reasons, automated-versus-human separation, exclusions, and limitations. Digests establish integrity, not execution authenticity. The evaluator reconstructs only scratch worlds and never gains authoritative-world mutation authority.

At the historical implementation gate, the accepted OpenRouter artifact already retained all twelve normalized proposals, while the accepted Ollama v5 artifact deliberately retained only the score summary and could not be symmetrically re-scored. A separately governed Ollama recording was therefore required to produce the additive v6 execution artifact before final comparison. That recording and comparison are now complete as reported below; no provider, runtime, credential, network, or live-state action occurred while implementing the evaluation boundary itself.

## Published v1 evidence

### Provider comparison result

The canonical provider comparison is `artifacts/snowglobe/cognition-quality/provider-comparison-v1.json`, SHA-256 `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`. It compares accepted OpenRouter evidence SHA-256 `ebbcc39d8e1ab7d0ee926600753aeaa5e420f70c5b14dee62b57614945c65e51` with fresh governed Ollama v6 evidence SHA-256 `7c9f0698ba93e10745d2820095d8e4040f61de96a9509e2014efdfe196682fe6` through one code path. OpenRouter scores 8,341/10,000 and Ollama 4,444/10,000; the deterministic recommendation is `openrouter_default`. Ollama remains the compatible local/offline fallback. Human judgment is absent and non-scoring, and the result makes no broader gameplay, intelligence, price-performance, deployment, commercial-readiness, or world-authority claim.

Corpus digest: `4de8c4a993b58875f27c5867c29a54679de789dacb03d2b4d8099e26340f1f8f`. Scoring digest: `043dc7f01ae544d4698e9c8b44c0f2c27b9f0a66fdba3a1e2249b868a64c35b0`. All-optimal 1200/1200 report digest: `7d7d918caa0f11f2367fabf1cc538c38d014b97c53acd8b32f94acbb0678652c`. Focused validation is 12/12; full SnowGlobe Release validation is 386/386; Release build is 0 warnings/errors; and independent deep review is FINAL CODE/DOC GO with no P0-P2 findings. No live model/provider/credential/payment action occurred.
