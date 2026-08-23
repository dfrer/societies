# ADR 0006: Offline Recorded-Response Cognition-Quality Runner

## Status

Accepted; implementation committed locally at `c7926d3`.

## Decision

Add one pure, synchronous, provider-neutral runner that converts exactly twelve already-recorded response fixtures into the existing Cognition Quality Corpus v1 proposal batch. The runner is a research/evidence boundary only; `src/societies/` and the deterministic world remain untouched.

Each fixture is bound to the corpus scenario ID and observation digest, and the ordered batch is validated before conversion. Responses are detached at input. A response is limited to 1..1,024 raw response bytes, the aggregate response budget is 12,288 bytes, and the canonical run artifact is limited to 96 KiB. Invalid UTF-8 maps to the closed `response_utf8_invalid` outcome. Raw response bytes are not retained in output: each binding records byte count, SHA-256 digest, and parse outcome.

Envelope or binding failures (count, order, scenario identity, observation digest, or size) abort the whole operation. A correctly bound response that is malformed or semantically unparseable becomes `no_proposal` with its parse outcome preserved. A representable proposal, including a wrong agent or action-specific invalid quantity, remains typed input for the existing scorer and deterministic feasibility authority.

The canonical artifact binds separate runner, parser, and proposal-schema identities, the caller-supplied prompt revision, provenance digest, response bindings, and nested execution evidence. Provenance is caller-attested identity, not proof that a model executed. The contract provides no network, model, provider, credential, payment, journal, file, or world authority; it performs no live call and makes no quality-improvement, provider-winner, intelligence, or cost claim.

## Validation

- Focused runner tests: 11/11.
- Full Snow Globe Release tests: 404/404.
- Release build: 0 warnings, 0 errors.
- Independent deep review: CODE GO.
- Preferred payload: `61cacfd4ad26512c1100a9235ee0ab534ec5945527a4da9252486ebf26675e43`.
- Preferred canonical run: `f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd`.
- Nested execution evidence: `2700886cef55abea3aba76f0789993cd6ad7283fa22d303dac5bbbd302e1ffe8`.

## Consequences

The lab can compare future recorded local or premium responses against one frozen corpus without coupling scoring to transport or provider behavior. This is not v3 normalized simulation replay: it does not replay or commit into authoritative or persisted simulation state. The existing scorer reconstructs a private scratch world and uses `ValidateAndCommit` only as feasibility authority. A future live corpus run remains separately authorized and must supply its own execution and provider evidence.

## Next action

Implement an entirely offline canonical prompt-envelope builder for the same twelve frozen observations, publishing exact bounded prompt bytes and response slots bound to a caller-supplied prompt revision and the existing runner, parser, and proposal-schema identities before any separately authorized live local or premium corpus recording.
