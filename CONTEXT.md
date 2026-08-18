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
- **Durable Financial Journal** — The append-only, checksum-bound financial record that can reopen successfully flushed journal facts under its single-host, single-writer contract; it is not a commercial accounting database or an exactly-once charging guarantee.
- **BYOK Account Binding Identity** — An opaque canonical binding in the form `byok-account-sha256-<64 lowercase hex>` that identifies the customer-owned billing account without containing a key, token, credential locator, email, provider account, or secret.
- **Reconciliation Evidence** — The bounded, immutable evidence tuple used to reconcile one admitted request's reservation, dispatch certainty, completion, charge outcome, and BYOK Account Binding Identity without copying provider text or secrets.
- **Financial Journal Identity** — The bounded canonical identity of the Financial Journal associated with an Inference Receipt.
- **Cognition Quality Corpus** — The versioned collection of structured Quality Scenarios and closed observation-only scoring rules used to compare bounded cognition proposals; it grants no model or provider authority.
- **Quality Scenario** — One bounded observation, candidate proposal, declared goal and constraints, expected feasibility outcome, and Action Utility Rubric case in the Cognition Quality Corpus.
- **Action Utility Rubric** — The closed integer rubric that scores a proposal's observable utility against a scenario's declared goal, constraints, and consequences after feasibility is established.
- **Cognition Quality Score** — The corpus-versioned aggregate of rubric points and Proposal Dispositions; it is a bounded research measure, not a measure of general intelligence.
- **Proposal Disposition** — The one recorded operational outcome of a candidate proposal: `no_proposal`, `contract_invalid`, `domain_rejected`, `feasible_suboptimal`, or `maximum_utility`.
- **Cognition Quality Execution Evidence** — A standalone, content-addressed offline record that binds one caller-attested model/policy provenance value and one exact ordered twelve-proposal submission to the frozen Cognition Quality Corpus v1, its scoring rules, and its canonical report. It is not execution attestation, provider evidence, or a general intelligence measure.

## Avoid

Do not use “AI gateway,” “generic proxy,” “billing event,” “provider response event,” or “retry” as domain terms. Do not describe the premium lane as raw API resale, and do not imply that a model, a provider, a financial journal, or an inference receipt can write simulation state directly. Avoid “model IQ,” “smartest model,” “quality winner,” and “best intelligence per dollar”; the corpus measures bounded proposal utility, not general intelligence or commercial superiority.
