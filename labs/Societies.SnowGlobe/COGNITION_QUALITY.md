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

## Published v1 evidence

Corpus digest: `4de8c4a993b58875f27c5867c29a54679de789dacb03d2b4d8099e26340f1f8f`. Scoring digest: `043dc7f01ae544d4698e9c8b44c0f2c27b9f0a66fdba3a1e2249b868a64c35b0`. All-optimal 1200/1200 report digest: `7d7d918caa0f11f2367fabf1cc538c38d014b97c53acd8b32f94acbb0678652c`. Focused validation is 12/12; full SnowGlobe Release validation is 386/386; Release build is 0 warnings/errors; and independent deep review is FINAL CODE/DOC GO with no P0-P2 findings. No live model/provider/credential/payment action occurred.
