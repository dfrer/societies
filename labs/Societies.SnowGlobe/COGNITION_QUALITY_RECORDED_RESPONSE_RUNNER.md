# Cognition-quality recorded-response runner

The committed `c7926d3` slice adds `CognitionQualityRecordedResponseRunnerModule.Run`: one pure synchronous operation over exactly twelve ordered, already-recorded response fixtures. It snapshots and detaches input, checks each fixture against the frozen observation binding, parses the bounded response, and emits a detached canonical proposal batch plus evidence.

## Contract

- Schema: `snow_globe_cognition_quality_recorded_response_run/v1`.
- Exactly 12 fixtures, in corpus order, with matching scenario and observation digest.
- Response budget: 1..1,024 raw response bytes per response and 12,288 bytes aggregate; invalid UTF-8 maps to `response_utf8_invalid`.
- Canonical output budget: 96 KiB.
- Strict flat JSON proposal shape: `agent_id`, `action`, `quantity`; no unknown, duplicate, nested, trailing, comment, or trailing-comma content.
- Separate identities are emitted: runner `snow_globe_cognition_quality_recorded_response_runner/v1`, parser `snow_globe_cognition_quality_recorded_response_parser/v1`, and proposal schema `snow_globe_cognition_quality_proposal_response/v1`.
- Prompt revision is caller-supplied through validated provenance; the runner does not define a fixed prompt.
- Raw response bytes are omitted from output; only byte count, digest, and parse outcome are retained.

Envelope/binding failures abort the operation. A correctly bound malformed response produces a null proposal (`no_proposal`) with a closed parse outcome. A representable but scorer-invalid proposal remains typed input for the existing corpus scorer and `ValidateAndCommit` feasibility authority. This preserves the distinction between evidence integrity and proposal quality.

The runner is caller-attested and offline. It has no network, model, provider, credential, payment, journal, file, or authoritative-world authority, performs no live call, and does not claim improved quality, general intelligence, a provider winner, or cost superiority. It is distinct from v3 normalized simulation replay: it converts response bytes into corpus submissions and does not replay or commit into authoritative or persisted simulation state. The existing scorer reconstructs a private scratch world and uses `ValidateAndCommit` only as feasibility authority.

## Evidence

Independent review and validation for `c7926d3`:

- focused tests 11/11;
- full Snow Globe Release tests 404/404;
- Release build 0 warnings/errors;
- deep review CODE GO;
- payload `61cacfd4ad26512c1100a9235ee0ab534ec5945527a4da9252486ebf26675e43`;
- canonical run `f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd`;
- nested execution evidence `2700886cef55abea3aba76f0789993cd6ad7283fa22d303dac5bbbd302e1ffe8`.

## Next action

Implement an entirely offline canonical prompt-envelope builder for the same twelve frozen observations. It must publish exact bounded prompt bytes and response slots bound to a caller-supplied prompt revision and the existing runner, parser, and proposal-schema identities before any separately authorized live local or premium corpus recording.
