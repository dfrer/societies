# Two-Tier Cognition Contract

Status: offline module contract under development. This document does not describe a live service.

## Product shape

The lab now has two intended products around one deterministic simulation:

1. **Base local lane.** A controlled local model path is used when the host has suitable local capacity. The model proposes; the deterministic validator decides.
2. **Paid premium lane.** A higher-value hosted model may propose under an explicitly authorized policy and financial journal. The caller does not receive a transparent paid failover chain, and premium failure never becomes an excuse to resubmit an ambiguous charge.

Both lanes consume the same structured observation and return the same untrusted proposal shape. Neither lane writes world state. A single shared simulation process remains authoritative.

## Run and cost rules

- A run or checkpoint freezes one Model Policy Snapshot and separately binds its requested Cognition Lane and Financial Journal Identity. Provider catalog changes cannot silently alter an active run.
- The snapshot names a stable premium model family and requires an exact sha256-<64 lowercase hex> Premium Model Revision Identity. Floating aliases and release labels cannot cross the premium provider seam.
- The premium lane admits a bounded Premium Cognition Job with a stable request identity and a maximum cost derived from that snapshot.
- A Cost Reservation is recorded before any future provider submission.
- Submission State is made unknown before dispatch whenever a post-dispatch outcome could be ambiguous. Unknown Charge State means no automatic resubmission.
- The module may select one explicit Local Fallback and then deterministic Idle. A fallback is visible in the Inference Receipt alongside the allowlisted primary-attempt outcome; arbitrary provider error text is never persisted. It is not a second hidden paid attempt.
- Every Inference Receipt directly carries the policy digest, Financial Journal Identity, requested lane, premium model identity and revision, submission/charge state, and detached proposal evidence.
- Simulation Ledger records only validated proposals and deterministic commits. Financial Journal records reservations and outcomes separately, cross-linked by Inference Receipt identity.
- Replaying normalized recorded proposals performs no model, provider, or financial call.

## Commercial boundary

The intended commercial pilot is customer-owned BYOK or direct-provider billing before any managed prepaid service. A managed service would require a durable financial journal, provider and legal confirmation, credential lifecycle controls, billing/Stripe/accounting/tax design, abuse monitoring, and an explicit product decision. This design does not authorize raw API resale or imply that the lab is a provider proxy.

The initial contract carries structured, typed cognition inputs only. Raw chat and personal data are out of scope. There is no current claim of live provider quality, payment readiness, deployment readiness, or model availability.

## Offline acceptance boundary

This slice is accepted only when an offline fake premium port and in-memory journal can demonstrate, without network access:

- local and premium lanes behind `ISnowGlobeIdentifiedInferenceAdapter`;
- immutable policy identity, content-addressed premium revision, requested-lane binding, and Financial Journal Identity;
- deterministic request identity and same-request replay;
- cost arithmetic with a checked maximum and reserve-before-submit ordering;
- one-shot submission classification, including an unknown outcome that cannot resubmit;
- explicit local fallback and deterministic Idle fallback;
- separate read-only financial evidence cross-linked to, but unable to mutate, the unchanged v3 simulation ledger; and
- normalized-proposal replay with zero model/provider/billing calls.

The current journal and receipt index are in-memory and process-local. They demonstrate bounded admission and duplicate suppression only within one live module instance; they do not claim durable exactly-once behavior, restart reconciliation, or live billing correctness.

No model weights, credentials, external endpoint, payment account, or live provider are required or touched by this acceptance boundary.

## Gates before live work

The following are future gates, not claims satisfied by this document:

- durable database-backed Financial Journal with restart and reconciliation semantics;
- authenticated fixed-host provider adapter and security review, including bounded I/O and secret cleanup;
- credential issuance, rotation, revocation, and tenant isolation;
- provider terms, age restrictions, retention, and data-policy confirmation for each chosen model;
- billing, Stripe, accounting, tax, refunds, charge disputes, and legal review;
- an authorized paid sandbox with submission/charge reconciliation evidence;
- a frozen Societies corpus benchmark for quality, cost, and fallback behavior;
- load, latency, capacity, cost, quota, and abuse monitoring; and
- an explicit deployment and operator runbook.

Until those gates pass, this is a local research contract with an offline fake, not a hosted offering.
