# Cognition Quality Execution Evidence v1

Status: implemented offline v1 handoff. This is a bounded evidence envelope for recorded proposals, not proof of a live model execution.

## Contract

The schema is `snow_globe_cognition_quality_execution_evidence/v1`. The module exposes one pure synchronous operation:

`Create(provenance, exact ordered 12 submissions)`

It snapshots the submission batch once, evaluates the existing frozen Cognition Quality Corpus v1, and returns standalone canonical evidence. The evidence embeds the exact recorded submission and quality report, then binds:

- provenance digest;
- corpus and scoring digests;
- submission and report digests;
- canonical payload digest; and
- final evidence digest.

The envelope is bounded to 64 KiB. The corpus remains exactly twelve ordered scenarios and uses the existing `ValidateAndCommit` feasibility authority.

## Provenance and limits

Local provenance binds canonical model identity, SHA-256 model revision, exact execution-policy/contract digest, prompt revision, proposal schema, and adapter identity. Premium provenance derives and binds the execution-policy digest plus model, prompt, and schema identities from one validated `ModelPolicySnapshot`; it does not retain the snapshot object or emit raw host, route, or cost fields.

The provenance is caller-attested identity. It is not execution attestation. The evidence has no provider, network, credential, payment, journal, file, live-action, or world-authority capability. It makes no general-intelligence, model-quality, provider-winner, or cost claim.

## Published digests

The settled local fixture binds:

- provenance: `8fb647f1f9e8a515ad490ccaec1372c4d2c110efa5599c33f93bf087a8821cfc`;
- submission: `9473f2021caffd85586d32ea550f46ee717d082b6d1dcba50ab979c8832a2757`;
- all-optimal report: `7d7d918caa0f11f2367fabf1cc538c38d014b97c53acd8b32f94acbb0678652c`;
- payload: `353c266be57dce3b4e3f15bc67920ac3325df75cae0eca00529ffa348014b9dd`; and
- final evidence: `7130deb0945697a14631ddea9bdc29e699b4b1217ae948a729e39ec827f3272a`.

The settled premium fixture binds provenance `e5fb33c1246784b3ff70165ea531297b2fe069d6425d002a770db62b82f32540`, payload `551a06855ee776ea03e8d27e1546806b9308a0517fbe1861e4d3180908ec9261`, and evidence `5a4efa252c84feadfb5ce878e0b6d50f2ceef6f51f2818667865475db666408c`.

## Validation and boundary

Focused execution-evidence tests passed 7/7; the full SnowGlobe Release suite passed 393/393; the Release build completed with 0 warnings and 0 errors; and independent deep review returned GO. No live model/provider call, model download, network, credential, payment, or `src/societies/` change occurred in this slice.
