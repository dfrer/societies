# ADR 0003: Offline credential and fixed-provider preflight

Status: Accepted for the isolated Snow Globe lab contract; not a live integration.

## Decision

Represent future provider access with three explicit boundaries: a Credential Lease, a Fixed Provider Profile, and a Provider Execution Capability. The lease owns and zeroes its transferred secret buffer. The profile is immutable and registry-owned. The capability is single-use and binds the exact profile, policy, financial journal, job, BYOK identity, bounds, source identity, clock, and nonce.

The current implementation is fixture-only and offline. It performs no HTTP, DNS, socket, provider, payment, credential, or model operation. The deterministic simulation and financial journal remain the only authorities for state and financial evidence respectively.

## Evidence

The slice passes 6/6 focused tests and 348/348 full Snow Globe lab tests. The Release build reports 0 warnings and 0 errors, and independent deep review is CODE GO. The evidence proves owned-buffer cleanup and reviewed fixture behavior; it cannot guarantee that an arbitrary trusted callback did not copy a secret.

## Consequences

No authenticated adapter, production profile, live parser/status/charge evidence, or provider quality claim is available. Before live work, add bounded authenticated transport, secret-source lifecycle review, status/charge reconciliation, no-retry rules for ambiguous outcomes, and a separately authorized paid or local benchmark contract.
