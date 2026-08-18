# Societies Domain Context

This glossary is the vocabulary for the Snow Globe two-tier cognition contract. It is intentionally domain-only; code, transport, persistence schemas, and provider products do not belong here.

## Terms

- **Cognition Lane** — The declared operating mode for a cognition proposal: the local lane, which runs within the host's controlled local boundary, or the premium lane, which may incur an explicitly journaled external-model cost.
- **Model Policy Snapshot** — The immutable statement of a permitted premium model family, content-addressed model revision, limits, pricing basis, output contract, and execution constraints. The requested Cognition Lane is bound to the run/module, not stored in the snapshot.
- **Premium Model Revision Identity** — A cryptographic content address for the exact premium model revision admitted by a Model Policy Snapshot; a family name, release label, or floating alias is not a revision identity.
- **Premium Cognition Job** — A single bounded request admitted under one Model Policy Snapshot and one idempotency identity; it is a proposal attempt, never a world mutation.
- **Cost Reservation** — The pre-submission hold against an authorized financial allowance that bounds the maximum possible cost of one Premium Cognition Job.
- **Submission State** — The evidence state of a job's dispatch: not submitted, a response was received, or submission is unknown after an outcome that cannot prove whether dispatch occurred.
- **Charge State** — The financial state of a job's cost: not applicable, reserved, released, settled, or unknown.
- **Inference Receipt** — The immutable cross-reference describing the requested lane, policy digest, premium model revision identity, proposal outcome, primary attempt outcome, submission/charge state, and Financial Journal Identity for one cognition decision.
- **Local Fallback** — An explicitly selected local proposal path used after a premium attempt cannot safely provide a usable proposal; it is not a hidden paid retry.
- **Simulation Ledger** — The ordered, deterministic record of validated world proposals and committed simulation events. It is the authority for simulation state.
- **Financial Journal** — The separate record of reservations, releases, settlements, and unknown financial outcomes, cross-linked to Inference Receipts but never used as simulation authority.
- **Financial Journal Identity** — The bounded canonical identity of the Financial Journal associated with an Inference Receipt.

## Avoid

Do not use “AI gateway,” “generic proxy,” “billing event,” “provider response event,” or “retry” as domain terms. Do not describe the premium lane as raw API resale, and do not imply that a model, a provider, a financial journal, or an inference receipt can write simulation state directly.
