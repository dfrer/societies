# Snow Globe OpenRouter raw-free rejection diagnostics

## Outcome

Future bounded OpenRouter evidence must retain the exact existing parser rejection code when a received response is rejected, so an operator can distinguish finish, cost, routing, JSON, proposal, usage, and other closed validation failures without retaining provider text.

## Scope

- Preserve the parser's bounded `OpenRouterPremiumEvidenceException.Code` in the terminal slot outcome using a deterministic `provider_response_rejected_<parser-code>` identity.
- Keep the current no-retry, stop-after-first-terminal-result, `SubmissionUnknown`, `ChargeState.Unknown`, zero trusted tokens/cost, null proposal, and response-digest behavior.
- Validate the diagnostic through the in-memory fake HTTP path and the real evidence, journal, production-bridge, validation, and CLI formatting paths.
- Update the isolated lab contract, repository truth, and continuation record.

## Non-goals

- No credential, provider, account, network, paid, retained-evidence, live-state, preflight, `record-once`, or `validate` access.
- No retry, fallback, alternate provider, parser relaxation, request/profile/routing/cost/timeout change, schema-version change, historical artifact rewrite, or attempt to infer the discarded fifth-run cause.
- No gameplay or `src/societies/` change and no claim that OpenRouter is currently compatible or ready for another paid attempt.

## Compatibility

- Existing evidence and journals with terminal `provider_response_rejected` remain valid and byte-for-byte untouched.
- The evidence and journal wire shapes and schema identities remain unchanged; only the already-bounded terminal outcome vocabulary gains deterministic diagnostic identities.
- Success, HTTP terminal, timeout, cancellation, credential, submission-unknown, and generic exchange-failure outcomes remain unchanged.
- The diagnostic suffix comes only from the closed local parser exception vocabulary, never from provider text.

## Acceptance tests

- A deterministic fake received response that fails finish validation records `provider_response_rejected_response_finish_invalid` through evidence, journal readback, production result, local validation, and CLI output.
- Cost, routing/binding, JSON/shape, usage, and proposal failures retain their corresponding closed parser codes and stop after one call with no retry.
- Historical generic `provider_response_rejected` artifacts and journal receipts still validate.
- Unexpected exchange exceptions and pre-parser transport-policy failures do not leak exception messages and retain their existing generic outcomes.
- Terminal evidence remains raw-free: no response body, model content, error message, or secret is added.

## Validation

- Red-first focused regression through the fake-only production bridge.
- Focused OpenRouter exchange/evidence/journal/production/CLI tests in Release.
- Full Snow Globe Release suite and both Snow Globe/OpenRouter CLI Release builds with zero warnings/errors.
- `git diff --check` and independent deep security/public-contract review.

## Implementation-owner evidence

- Red first: the 11-case fake production-bridge theory passed the 3 unchanged HTTP/pre-parser cases and failed all 8 parser-code cases because each actual terminal remained generic `provider_response_rejected`.
- Green targeted: the same matrix plus exact CLI formatting passed 12/12 after preserving the closed parser code.
- Focused Release: evidence/journal/HTTP tests passed 88/88; production-bridge/CLI-security tests passed 80/80, including the current v2 production run-and-validation path.
- Release builds: `Societies.SnowGlobe` and `Societies.SnowGlobe.OpenRouterCli` each built with 0 warnings and 0 errors.
- Compatibility: generated historical generic artifacts and durable journal records still validate/read back as `provider_response_rejected`; schema identities and JSON shapes were not changed.
- Review P3 closure: every diagnosable parser throw now uses one typed enum that directly supplies its wire suffix; exhaustive coverage iterates all declared codes, rejects undefined values, and proves untyped response-looking or unrelated evidence exceptions retain the generic fallback. The duplicate manual code list was removed.
- Main delivery gate passed the full 995/995 Snow Globe Release suite and full 80/80 OpenRouter CLI/security suite; both Release builds have 0 warnings/errors and `git diff --check` is clean. Independent deep security/public-contract re-review is FINAL GO with no P0-P3 findings after the typed-vocabulary P3 closure. Shared truth is updated; Git/PR delivery remains.

## Delivery

- Deliver only this coherent offline slice on a `codex/` branch through commit, push, pull request, required-check monitoring, and merge.
- Return `master` clean and synchronized with exact local and hosted evidence.
