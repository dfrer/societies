# ADR 0002: Durable Financial Journal Boundary

## Status

Accepted

## Context

Premium cognition needs financial evidence that survives ordinary restart without becoming simulation authority. The v3 run store must remain unchanged, and no provider, payment, credential, or network call is authorized in this research slice. A process-local journal cannot prove recovery of a flushed reservation or safely classify a reopened unknown dispatch.

## Decision

Create the BCL-only `snow_globe_financial_journal/v1` module with a small command/query interface and two adapters: an in-memory adapter for deterministic tests and a strict file adapter for bounded local evidence. The deterministic world remains the sole state authority, and `ISnowGlobeIdentifiedInferenceAdapter` plus run-store v3 remain unchanged.

File artifacts use an immutable checksum-bound header, mandatory checksummed LF JSONL records, and a live-writer lease. The adapter validates bounded UTF-8, canonical bytes, allowed record kinds, checksums, sequence, completion/archive or explicit paging, and account/policy/run bindings. Corrupt or torn input fails closed without repair; append uncertainty poisons the writer.

The only BYOK identity is opaque `byok-account-sha256-<64 lowercase hex>`; no key, token, credential locator, email, raw provider account, or secret is accepted. The header exact-binds journal, run, lane, policy, premium revision, BYOK identity, caps, and checksum.

Durable order is admit-and-reserve flush, dispatch-unknown flush, then the offline fake provider. Reopen may dispatch one reserved job once; an Unknown job never dispatches and retains its allowance. Strict idempotency/conflict, cap arithmetic, reentrancy/concurrency, completion-tuple validation, reconciliation CAS/evidence/account binding, and immutable receipts are required.

The journal reserves headroom for four records per premium admission and two records per persisted denial. At absolute capacity the cognition module returns deterministic cached `Idle`, makes no provider or local call, and intentionally cannot create a durable idempotency key; this limitation is observable and not repaired by silently exceeding capacity.

## Alternatives Considered

- **SQLite or another transactional database now** — Rejected for this offline BCL-only slice; it is the required future replacement before commercial or cross-process guarantees.
- **Port-only financial seam** — Rejected because the current milestone needs restart evidence and a strict file adapter while preserving a small interface.
- **Put financial records in run-store v3** — Rejected because simulation replay and financial reconciliation have different authorities and failure semantics.

## Consequences

Successfully flushed records can be reopened under a single-host, single-writer contract, with bounded corruption rejection and explicit unknown-charge retention. This does not certify power-loss behavior, multi-process/multi-host coordination, account-wide transactional state, commercial accounting, cross-ledger atomicity, or exactly-once charging. The file journal is a research milestone, not a commercial database. Before live work, replace or augment it with a transactional database and separately authorize credential-lease/fixed-host provider adapter, commercial billing/legal controls, and reconciliation evidence.
