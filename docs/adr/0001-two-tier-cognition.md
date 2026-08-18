# ADR 0001: Two-Tier Cognition Behind the Existing Simulation Seam

## Status

Accepted

## Context

Societies needs two products around one deterministic simulation: a low-cost local lane and an optional paid premium lane. Model output is an untrusted proposal. The existing simulation caller seam, `ISnowGlobeIdentifiedInferenceAdapter`, already gives the world one immutable observation-to-proposal boundary. Introducing a second gameplay authority or mixing financial state into the simulation run store would make replay, failure handling, and accounting harder to reason about.

Premium work also has a financial safety boundary. A bounded request must be tied to an immutable run policy, reserved before submission, and recorded independently from world events. A dispatch whose outcome cannot prove whether it happened must be marked unknown before the provider boundary is called; it must never be silently submitted again. The simulation must remain useful when premium work is unavailable or financially ambiguous.

## Decision

- Keep `ISnowGlobeIdentifiedInferenceAdapter` as the one simulation caller seam.
- Place a deep two-tier cognition module behind that seam. It owns lane selection, immutable policy binding, idempotency, admission, reservation, submission-state classification, charge-state classification, and explicit fallback.
- Keep a separate read-only financial evidence seam for Inference Receipts and the Financial Journal. It is audit evidence, not a simulation command path.
- Treat the deterministic world validator and commit path as the sole simulation state authority.
- Bind each run/checkpoint to one immutable Model Policy Snapshot, requested Cognition Lane, and Financial Journal Identity. The snapshot carries a stable premium model family plus an exact sha256-<64 lowercase hex> Premium Model Revision Identity; a floating “best model” lookup or release label is not admissible per turn.
- Reserve the maximum permitted cost before a Premium Cognition Job is submitted. Mark submission unknown before calling a future provider adapter when the call's outcome could be ambiguous.
- An unknown charge permits no automatic resubmission. The module chooses the explicit local fallback, then deterministic Idle if no valid proposal is available.
- Keep the financial journal separate from the unchanged v3 simulation ledger, with cross-links through Inference Receipts.
- Make each Inference Receipt directly carry the policy digest, Financial Journal Identity, requested lane, premium model identity and exact revision, final outcome, and allowlisted primary-attempt outcome. Provider text is not evidence and is never copied into a receipt.
- Normalized-proposal replay reads recorded responses and makes no model, provider, or billing calls.
- Add fixed provider-specific adapters only after their contracts are separately reviewed. Do not build an arbitrary-origin OpenAI-compatible proxy.
- This slice uses only an offline fake premium port and an in-memory journal. It performs no network, credential, payment, provider, or model operation.

## Alternatives Considered

### Put tier and billing in the scheduler

Rejected. The scheduler should order observations and validated commits. Adding pricing, submission certainty, and financial recovery there would widen a timing component into a payment authority and make deterministic scheduling harder to test.

### Introduce a large Cognition Director as a gameplay interface

Rejected for now. A new broad interface would duplicate the existing simulation seam and invite callers to depend on unvalidated cognition or financial details. The module can remain deep behind the proven adapter interface; a later read-only evidence seam serves inspection without becoming gameplay authority.

### Dynamically choose the best model per turn

Rejected. It would make a run's behavior, price ceiling, and replay evidence depend on a changing catalog. Policy selection belongs at run/checkpoint admission, where it can be frozen and identified.

### Build a generic provider proxy

Rejected. An arbitrary-origin proxy obscures provider identity, terms, data policy, cost, and failure semantics, and risks becoming raw API resale. Future adapters must be fixed to an approved provider/host contract with its own security and commercial review.

### Add financial fields to run-store v3

Rejected. The v3 store is the deterministic simulation ledger and its replay contract is already validated. Financial settlement has different durability, privacy, reconciliation, and retention requirements; it needs a separate journal linked by receipts.

## Consequences

The simulation keeps one small interface and one state authority while the cognition module absorbs premium complexity. Local fallback and deterministic Idle preserve progress without a paid failover. The cost is a second evidence model and a future durability boundary: the current process-local in-memory journal proves one-shot behavior only while that process lives. It does not provide durable exactly-once delivery, crash recovery, or commercial accounting. Before any live use, the provider adapter, credential lifecycle, durable financial journal, terms/data policy, authorized paid sandbox, and accounting/legal controls must be reviewed independently.
