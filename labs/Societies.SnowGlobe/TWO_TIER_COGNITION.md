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

This slice is accepted only when the offline fake premium port and `snow_globe_financial_journal/v1` module can demonstrate, without network access:

- local and premium lanes behind `ISnowGlobeIdentifiedInferenceAdapter`;
- immutable policy identity, content-addressed premium revision, requested-lane binding, and Financial Journal Identity;
- deterministic request identity and same-request replay;
- cost arithmetic with a checked maximum and reserve-before-submit ordering;
- one-shot submission classification, including an unknown outcome that cannot resubmit;
- explicit local fallback and deterministic Idle fallback;
- separate read-only financial evidence cross-linked to, but unable to mutate, the unchanged v3 simulation ledger;
- immutable checksum-bound file artifacts, strict bounded canonical validation, live-writer poisoning on append uncertainty, and ordinary restart recovery for successfully flushed records under one host/one writer;
- opaque `byok-account-sha256-<64 lowercase hex>` binding with no key, token, credential locator, email, raw provider account, or secret;
- admit/reserve flush before dispatch-unknown flush, one reserved reopen dispatch, Unknown retention without dispatch, strict idempotency/conflict and completion/reconciliation evidence; and
- normalized-proposal replay with zero model/provider/billing calls.

The journal has an in-memory adapter and a strict file adapter. The file adapter provides ordinary restart recovery only for successfully flushed records under a single host and single writer. It is not power-loss certified, multi-process/multi-host or account-wide transactional state, commercial accounting, cross-ledger atomic, or exactly-once charging. At absolute capacity, four-record premium-admission and two-record persisted-denial headroom are protected; the module returns deterministic cached `Idle` with no provider/local call and intentionally cannot create a durable idempotency key.

No model weights, credentials, external endpoint, payment account, or live provider are required or touched by this acceptance boundary.

## Provider preflight vocabulary and current evidence

The offline preflight uses a **Credential Lease**, a **Fixed Provider Profile**, and a **Provider Execution Capability** as separate concepts. A Credential Lease owns and zeroes its transferred secret buffer; a Fixed Provider Profile is registry-owned and fixes endpoint, authentication, retry, proxy, model, and billing policy; a Provider Execution Capability is single-use and binds the exact profile, policy, journal, job, BYOK identity, bounds, source identity, clock, and nonce. These are fixture-only contracts. The source/probe does not make network, HTTP, DNS, payment, or provider calls, and no production profile or authenticated adapter exists.

Evidence is 6/6 focused tests, 348/348 full lab tests, a Release build with 0 warnings and 0 errors, and independent deep-review CODE GO. The evidence does not prove that an arbitrary trusted callback cannot retain a copied secret; it proves only the owned lease-buffer cleanup and the reviewed fixture behavior. Live parser/status/charge evidence, live credentials, and provider quality remain unimplemented.

## Gates before live work

The following are future gates, not claims satisfied by this document:

- SQLite or another transactional database replacing the file journal before commercial durability, cross-process coordination, or exactly-once/accounting claims;
- authenticated fixed-host provider adapter and security review, including bounded I/O and secret cleanup;
- credential issuance, rotation, revocation, and tenant isolation;
- provider terms, age restrictions, retention, and data-policy confirmation for each chosen model;
- billing, Stripe, accounting, tax, refunds, charge disputes, and legal review;
- an authorized paid sandbox with submission/charge reconciliation evidence;
- a frozen Societies corpus benchmark for quality, cost, and fallback behavior;
- load, latency, capacity, cost, quota, and abuse monitoring; and
- an explicit deployment and operator runbook.

Until those gates pass, this is a local research contract with an offline fake, not a hosted offering.

The isolated Ollama repair is infrastructure preflight, not premium-lane evidence. Portable v0.32.14 at `E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14` passed official asset hash verification and discovered the RTX 2070 SUPER via CUDA (compute 7.5; 8 GiB total, 7 GiB available) on loopback `127.0.0.1:11435` with cloud disabled. The default PATH Ollama 0.18.2 installation remains unchanged. One bounded qwen3.5:4b smoke completed against the pinned runtime: official digest `2A654D98E6FBA55D452B7043684E9B57A947E393BBFFA62485A7AAC05EE4EEFD`, Q4_K_M, 4,659,865,088 parameters, 34/34 layers GPU-offloaded, 3,128,038,521-byte `size_vram`, and 6,357/8,192 MiB observed loaded-state GPU use. It used one loopback call with no retry, stream/think disabled, temperature 0, context 4096, and output cap 96; wall time was 51,056 ms and output was 20 tokens. This is smoke/artifact evidence only, not a benchmark, intelligence, or quality result; the production benchmark runner remains uninvoked. The next action is the frozen benchmark contract.
