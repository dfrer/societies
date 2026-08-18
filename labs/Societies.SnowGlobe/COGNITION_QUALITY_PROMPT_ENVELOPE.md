# Cognition-quality prompt envelope

The committed `bcba42a` slice adds `CognitionQualityPromptEnvelopeBuilderModule.Create`. It is one pure synchronous operation that publishes exact compact UTF-8 prompts for all twelve frozen Cognition Quality Corpus scenarios in corpus order.

## Contract

- Publication schema: `snow_globe_cognition_quality_prompt_envelope_publication/v1`.
- Builder identity: `snow_globe_cognition_quality_prompt_envelope_builder/v1`.
- Prompt schema: `snow_globe_cognition_quality_prompt/v1`.
- Prompt revision: one caller-supplied canonical `SnowGlobeInferenceIdentity` revision, embedded in every prompt.
- Prompt limits: 1..2,048 bytes per prompt, 24,576 bytes aggregate, and 64 KiB canonical publication.
- Exactly twelve slots, each bound to its scenario ID and observation SHA-256 digest.
- Each slot publishes prompt byte count, prompt SHA-256, base64 prompt bytes, and empty response byte count/digest fields.

Prompts contain survival order, resource costs, deterministic rules, observation, and strict response grammar. They exclude scenario/category labels, scores, preferred answers, setup/state/event metadata, model/provider identity, credentials, and financial data. The response grammar asks for one flat JSON object with `agent_id`, `action`, and `quantity`; the existing runner remains the parser and scorer boundary.

The canonical publication binds corpus, scoring, validator, runner, parser, and proposal-schema identities plus prompt bytes, prompt digests, prompt-set digest, payload digest, final digest, and claim limitations. `Create` emits no response bytes. `BindRecordedResponses` validates exact provenance revision/schema/count and can return detached fixtures for the existing runner; those fixtures necessarily retain detached raw response bytes until consumed.

## Evidence and limitations

- Focused tests: 6/6.
- Full Snow Globe Release tests: 410/410.
- Release build: 0 warnings/errors.
- Independent deep review: FINAL CODE GO.
- Payload: `d879faa5af02e5b95108d7b9355a763acee1e120a1c68986c62c0e3b8907ce87`.
- Canonical publication: `966727433db3095e804148bba18e23da368d5fbbf58e7b0e2e58de349b47e9ae`.
- Prompt set: `f9baf35ff43fbd4977d050488f0bb1ebfb37bb9b1fb98ddbd2fa83384e9bbcbb`.

This is offline prompt publication only. Caller attestation is not proof of transport delivery or model execution. No provider, model, network, credential, payment, journal, file, authoritative-world, or persisted-world action occurred. The slice makes no general quality, intelligence, provider-winner, or price claim.

## Next action

Implement an entirely offline recording-evidence envelope that atomically binds the prompt publication and prompt-set digest, provenance, exact ordered response digests, and existing runner evidence before any separately authorized live local or premium corpus recording. It must not claim transport delivery.
