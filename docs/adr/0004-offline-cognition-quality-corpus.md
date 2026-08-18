# ADR 0004: Offline Cognition Quality Corpus and Closed Utility Rubric

## Status

Accepted

## Context

Operational compatibility and latency evidence do not establish cognition quality. Societies needs a bounded, repeatable comparison of structured action proposals against the same deterministic world observations. The current observation is a single decision-point view: it does not expose durable history, private goals, long-horizon planning, or multi-agent coordination.

## Decision

Adopt Choice B: a scratch deterministic world reconstructed from each frozen scenario, the existing `ValidateAndCommit` feasibility authority, and a closed observation-only integer rubric. The corpus contains twelve survival-progression scenarios in four fixed categories: `shelter_acquisition`, `shelter_construction`, `storage_progression`, and `safe_restraint`.

The published corpus content, scoring rules, ordered submission envelope, and canonical report are content-addressed and bound to their declared digests. A report records per-scenario outcomes, raw points, basis points, dispositions, and limitation codes. Once v1 content and its digest are published, v1 is immutable; a semantic change requires v2.

Feasibility is authoritative only through `ValidateAndCommit`. The rubric scores observable utility after feasibility. The five Proposal Disposition values are exactly `no_proposal`, `contract_invalid`, `domain_rejected`, `feasible_suboptimal`, and `maximum_utility`.

## Alternatives Considered

### Choice A: Exact allowlist memorization

Rejected. It would reward reproducing known answers rather than selecting a valid and useful action from the supplied observation.

### Choice B: Scratch deterministic world, existing validator, closed integer rubric

Accepted. It is replayable and auditable offline while keeping world feasibility with the deterministic simulation authority and utility scoring bounded to declared observations.

### Choice C: LLM or prose judge with multi-tick scoring

Rejected for v1. A prose judge introduces an unbounded, non-replayable authority, and multi-tick scoring requires durable history, goals, and coordination state that the current observation does not provide.

## Consequences

The corpus can compare bounded proposal utility without provider, model, network, file, payment, or world-authority access. It cannot measure general intelligence, model IQ, a smartest model, a quality winner, or best intelligence per dollar. Expanding observations, adding durable history, changing scoring semantics, or changing disposition meanings requires a new corpus version.
