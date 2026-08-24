# Snow Globe OpenRouter finish-diagnostic refinement

## Outcome

Future raw-free OpenRouter evidence must distinguish each existing finish-admission failure without retaining provider values: choice-index invalid, finish reason missing, finish reason wrong type, finish reason not `stop`, choice error present, native finish reason wrong type, native finish reason not `stop`, logprobs non-null, and refusal non-null.

## Scope

- Replace the parser's broad typed `response_finish_invalid` emission with one typed closed code for each existing decision above.
- Preserve the existing `provider_response_rejected_<parser-code>` evidence path through journal, artifact, v1/v2 production results, validation, and CLI output.
- Add red-first fake HTTP coverage at the real parser and production-bridge seams for every new code, including raw-free/no-retry financial behavior.
- Keep the typed parser vocabulary exhaustive so future additions cannot silently fall back to generic evidence.

## Non-goals

- No parser relaxation, accepted-response change, request/profile/model/routing/cost/timeout/retry change, schema/version change, historical artifact rewrite, provider-value retention, or inference about the sixth response's exact condition.
- No credential, provider, network, paid, account, retained-evidence, live-state, preflight, `record-once`, `validate`, deployment, release, gameplay, or `src/societies/` action.

## Compatibility

- Historical evidence and journals containing `provider_response_rejected_response_finish_invalid` remain canonical and valid without rewrite.
- The broad typed parser code may cease to be emitted by new parsing; existing wire readers continue accepting the historical identity.
- V1 evidence/journal shapes and schema identities remain unchanged. Success and every non-finish terminal code remain unchanged.
- New codes contain only closed local condition names. They never include the actual finish value, provider error, refusal text, response body, exception message, or secret.

## Acceptance tests

- A deterministic fake response drives every existing finish-admission branch and initially demonstrates that all branches collapse to `response_finish_invalid`.
- After the fix, each branch produces its exact typed parser code and corresponding `provider_response_rejected_<code>` terminal outcome through evidence, journal readback, run result, validation, and CLI formatting where applicable.
- Every branch remains terminal after one call with `SubmissionUnknown` / `ChargeState.Unknown`, zero trusted tokens/local settlement, null proposal, response digest retained, and no retry.
- Historical broad diagnostic artifacts and journal records still validate/read back unchanged.
- Exhaustive typed-vocabulary coverage and source construction prevent drift or untyped response-looking diagnostics.

## Validation

- Red-first targeted parser/production-bridge loop in Release.
- Focused evidence/journal/HTTP and production-bridge/CLI-security Release selections.
- Full Snow Globe Release suite, full OpenRouter CLI/security suite, and both Release builds with zero warnings/errors.
- `git diff --check` and independent deep security/public-contract review.

## Evidence

- Root cause: `ParseChoice` coupled index, finish-reason, and choice-error checks into one compound guard, then reused the same broad code for native finish, logprobs, and refusal. The zero-I/O readiness manifest also asserted that historical broad code in its fake parser probe.
- Red-first Release command: `dotnet test tests/Societies.SnowGlobe.OpenRouterCli.Tests/Societies.SnowGlobe.OpenRouterCli.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OpenRouterProductionBridgeTests.StatusParserAndPreParserFailuresAreTerminalUnknownRawFreeAndNeverRetried"`. Result: 9 failed, 10 passed; all nine actual outcomes were `provider_response_rejected_response_finish_invalid` instead of their distinct expected codes.
- Green targeted evidence: production bridge 19/19; parser finish-admission coverage 14/14; historical artifact/journal plus exhaustive vocabulary 5/5; v2 result/validation plus current-and-historical CLI formatting 11/11; readiness-manifest golden 4/4; zero-I/O plan 1/1.
- Green aggregate evidence: focused evidence/journal/HTTP 99/99; focused production/CLI 97/97; full Snow Globe Release 1006/1006; full OpenRouter CLI/security Release 97/97; both Release builds 0 warnings / 0 errors.
- Compatibility: historical generic and `provider_response_rejected_response_finish_invalid` artifacts/journals remain canonical and readable. The broad code is absent from the active enum and an untyped exception using it maps to generic evidence. Manifest schema, fields, CLI line, digest `eea26a92d318a2ba102c7979d0cb44563d8bef967ae00b627bc6263ff59d759d`, and `live_readiness=false` are unchanged.
- Boundary: no provider, network, credential, account, paid, retained-evidence, live-state, preflight, `record-once`, `validate`, download, gameplay, or `src/societies/` action occurred. The immutable sixth-run evidence cannot be reclassified into one of the new codes.

## Delivery

- The focused contract, lab README, `CURRENT_BUILD.md`, and `WORKFLOW.md` record the reviewed outcome and immutable sixth-run limitation.
- Implementation commit `2dff642` was published through PR #145; required `build-test-smoke` passed in 4m15s and the PR merged to `master` as `d0c7d09`.
- Publish this exact delivery record through a documentation-only `codex/` branch, then return `master` clean and synchronized.
