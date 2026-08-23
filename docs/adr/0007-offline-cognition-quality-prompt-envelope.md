# ADR 0007: Offline Cognition-Quality Prompt Envelope

## Status

Accepted; implementation committed locally at `bcba42a`.

## Decision

Add one pure, synchronous prompt-envelope builder for the frozen twelve-scenario Cognition Quality Corpus. `Create(canonical caller prompt revision)` publishes exact corpus-ordered compact UTF-8 prompts and detached response slots without contacting a model, provider, network, credential, payment system, journal, file, or authoritative simulation state.

The publication identity is `snow_globe_cognition_quality_prompt_envelope_publication/v1`, produced by builder identity `snow_globe_cognition_quality_prompt_envelope_builder/v1`. Each prompt uses `snow_globe_cognition_quality_prompt/v1`, embeds the caller-supplied canonical prompt revision, and includes survival rules, costs, observation, and strict response grammar. Prompts exclude scenario/category labels, scores, preferred answers, setup/state/event metadata, model/provider identity, credentials, and financial data.

Each slot binds a scenario ID and observation SHA-256 digest. Publication slots contain prompt byte count/digest/base64 bytes and empty response fields. Prompt bytes are limited to 1..2,048 per slot, 24,576 bytes aggregate, and 64 KiB for the canonical publication. The publication is raw-response-free; `BindRecordedResponses` is a separate bounded handoff to the existing recorded-response runner and returns detached response fixtures, which necessarily retain their detached raw bytes until consumed.

The canonical publication binds corpus, scoring, validator, runner, parser, and proposal-schema identities, prompt bytes and digests, prompt-set digest, payload digest, final digest, and claim limitations. Caller identity is an attestation only: publication does not prove prompt transport or model execution.

## Validation

- Focused prompt-envelope tests: 6/6.
- Full Snow Globe Release tests: 410/410.
- Release build: 0 warnings, 0 errors.
- Independent deep review: FINAL CODE GO.
- Payload digest: `d879faa5af02e5b95108d7b9355a763acee1e120a1c68986c62c0e3b8907ce87`.
- Canonical publication digest: `966727433db3095e804148bba18e23da368d5fbbf58e7b0e2e58de349b47e9ae`.
- Prompt-set digest: `f9baf35ff43fbd4977d050488f0bb1ebfb37bb9b1fb98ddbd2fa83384e9bbcbb`.

## Consequences

The lab now has a deterministic, provider-neutral prompt boundary reusable by future local and premium recorders while preserving exact observation bindings and parser compatibility. It does not add transport, execution, quality, intelligence, winner, or price evidence. Raw prompt bytes are published by design; raw response bytes remain outside the publication and are handled only by the bounded runner path.

## Next action

Implement an entirely offline recording-evidence envelope that atomically binds this prompt publication and prompt-set digest, provenance, exact ordered response digests, and existing runner evidence before any separately authorized live local or premium corpus recording. This must not claim transport delivery or model execution.
