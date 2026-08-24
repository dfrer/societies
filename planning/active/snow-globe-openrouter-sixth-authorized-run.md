# Snow Globe OpenRouter sixth authorized run

## Authorization

The user's fresh authorization applies to one bounded OpenRouter session under the immediately preceding stated terms: at most 12 sequential requests, no retry or alternate provider, and a maximum aggregate charge of 18,000 microusd (`$0.018`). This authority is consumed by the first terminal `preflight` invocation, whether it succeeds or fails. No stage may be invoked more than once.

## Outcome

Exercise the reviewed v2 production boundary once and retain raw-free evidence that either accepts the bounded twelve-slot run or identifies the first terminal/uncertain outcome with the new parser diagnostic when applicable.

## Scope

- Reconfirm exact branch, source HEAD, clean/known working-tree inventory, origin synchronization, frozen manifest, Release binaries, and unchanged `src/societies/` boundary before live access.
- Run zero-I/O `plan` once as an offline assertion only.
- Run `preflight` exactly once. It may read the fixed state anchor and stored OpenRouter credential and perform the contract's three authenticated metadata GETs; it performs no paid inference.
- Only if preflight succeeds, run `record-once` exactly once using the returned authorization digest and the exact 18,000-microusd acknowledgement. It may issue at most 12 sequential Azure-only ZDR requests and must stop on the first terminal/uncertain result.
- Only after `record-once` produces a durable artifact, run local `validate` exactly once with the same authorization digest.
- Record only bounded raw-free command output, exit status, hashes, counts, terminal code, local settlement, and documented state identities. Preserve all generated state/evidence in place.

## Non-goals

- No retry, alternate provider, fallback, second session, credential store/delete, anchor provision/replace/delete/rotation, state reset, evidence movement/deletion, account-console query, accounting follow-up, raw prompt/response capture, provider-debug logging, deployment, release, or `src/societies/` change.
- No claim of provider zero cost when local charge is unknown, no quality/winner claim without accepted twelve-slot evidence, and no power-loss, exactly-once, cross-host, or commercial-readiness claim.

## Acceptance and stop rules

- `plan`, `preflight`, `record-once`, and `validate` each execute at most once; `record-once` and `validate` are conditional on prior durable success/evidence.
- Any nonzero or terminal stage result consumes its applicable authority and stops forward execution except that a terminal `record-once` artifact may still receive its single local `validate` invocation.
- No more than 12 provider POSTs occur, sequentially, with no retry or alternate route; the aggregate preauthorized ceiling remains 18,000 microusd.
- Evidence/reporting distinguishes `ResponseReceived`, `SubmissionUnknown`, `ChargeState.Unknown`, and locally settled cost exactly as emitted; it does not infer provider billing.

## Validation and review

- The execution source is merged `master` `80d0ab232d70b0c79bd643acf02186204369722c`, whose offline gates passed 995/995 Snow Globe and 80/80 OpenRouter CLI/security tests with zero-warning Release builds and independent FINAL GO review.
- After execution, verify raw-free artifacts through the one permitted local validation stage and bounded file metadata/hashes without reading raw provider content.
- Obtain independent deep security/evidence review before final claims or delivery.

## Delivery

- Operator-observed invocations are plan/preflight/record/validate 1/1/1/1. Preflight accepted authority `16892f25b9c6fe81fcee1911295634f9cc88cf8bff633aa66678bef65fa42574`; one paid exchange stopped as `provider_response_rejected_response_finish_invalid`; local validation accepted evidence `8ed4027c13f90eff69600c694d0f7d03bb316fd8bc936edbd66e20c4dc644366` and receipt `c1416abc97142356808e57f9d57369672e7845fbc5d5ba6521e487a92e3d6c41`.
- Independent deep security/evidence review is FINAL EVIDENCE GO with no P0-P3. Durable state proves one completed preflight generation, execution claim, and validation claim; it does not prove exact CLI invocation counts, provider status/charge, or the exact finish-reason value.
- Evidence-record commit `ce217a6` was published through PR #143; required `build-test-smoke` passed in 8s and the PR merged to `master` as `e8df0c1`. Publish this exact delivery record, then return `master` clean and synchronized. No further provider action is authorized.
