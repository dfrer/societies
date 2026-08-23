# ADR 0005: Offline Cognition-Quality Execution Evidence

## Status

Accepted

## Context

The fixed Cognition Quality Corpus v1 can prove deterministic scoring of a recorded twelve-proposal batch, but a score alone does not identify which model or policy revision the batch claims to represent. A future live adapter must not be smuggled into this offline evidence layer.

## Decision

Adopt the schema `snow_globe_cognition_quality_execution_evidence/v1` with one pure synchronous operation:

`Create(provenance, exact ordered 12 submissions)`

The module snapshots the ordered batch once, runs the existing frozen corpus scorer, and emits bounded standalone canonical evidence. The evidence embeds the exact canonical recorded submission and quality report and binds provenance, corpus, scoring, submission, report, payload, and final evidence digests. The maximum envelope size is 64 KiB.

Local provenance binds canonical model identity, a SHA-256 model revision, the exact execution-policy/contract digest, prompt revision, proposal schema, and adapter identity. Premium provenance derives and binds the execution-policy digest plus model, prompt, and schema identities from one validated `ModelPolicySnapshot`; it does not retain the snapshot object or emit raw host, route, or cost fields.

Provenance is caller-attested identity, not execution attestation. The evidence layer has no provider, network, credential, payment, journal, file, live-action, or world-authority capability. It makes no general intelligence, model quality, winner, or cost claim.

## Consequences

Offline fixtures can be independently recomputed and compared without changing corpus semantics or introducing a provider adapter. A caller can truthfully bind a recorded result to a declared local or premium policy revision, but cannot claim that the declared model actually executed until a separately authorized execution-attestation boundary exists. Any semantic change to the schema or binding requires a new version.
