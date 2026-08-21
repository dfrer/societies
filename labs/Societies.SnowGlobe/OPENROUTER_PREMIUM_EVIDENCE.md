# OpenRouter premium evidence boundary

This is the offline contract for the OpenRouter boundary in code commit `ef32576`. It does not authorize or prove a live provider run.

## Shape and preserved seams

Design-It-Twice compared a minimal deep Module with a common-run module. The selected hybrid keeps a tiny authorize -> single-use run -> pure validate caller seam, a separate immutable profile registry, and a separate premium-evidence journal. `ProviderAdapterPreflight`, offline recording, legacy `FinancialJournal`, `LocalPremiumComparison`, `SnowGlobeTwoTierCognitionModule`, the deterministic world, `src/societies/`, and v1-v4 artifacts are unchanged.

The new source files are `AssemblyInfo.cs`, `OpenRouterPremiumProfile.cs`, `OpenRouterPremiumJournal.cs`, `OpenRouterPremiumExchange.cs`, and `OpenRouterPremiumEvidence.cs`. The new tests are `OpenRouterPremiumProfileTests.cs`, `OpenRouterPremiumEvidenceTests.cs`, and `OpenRouterPremiumHttpExchangeTests.cs`, all under the corresponding SnowGlobe lab/test directories.

## Frozen approval-time profile

The 2026-08-20 approval-time facts were checked against the [official model page](https://openrouter.ai/openai/gpt-5.6-luna-20260709), [official create-a-chat-completion reference](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion), [official router metadata documentation](https://openrouter.ai/docs/guides/features/router-metadata), and [official provider-selection documentation](https://openrouter.ai/docs/guides/routing/provider-selection):

- Model id: `openai/gpt-5.6-luna`; canonical request slug: `openai/gpt-5.6-luna-20260709`.
- Context: `1,050,000`; base catalog prompt price `$0.20/M` tokens; completion price `$1.20/M` tokens. At 272,000 input tokens the catalog threshold override is prompt `$0.40/M` and completion `$1.80/M`; the hard 4,096-input bound makes that override unreachable for this slice.
- Supported: `response_format`, structured outputs, and `max_tokens`; temperature is omitted.
- Endpoint: `POST https://openrouter.ai/api/v1/chat/completions`; Bearer authentication; `stream=false`.
- Request policy: strict `json_schema`; provider `order` and `only` set to `openai`; `require_parameters=true`; `allow_fallbacks=false`; `data_collection=deny`; `zdr=true`; `X-OpenRouter-Metadata` enabled.

These prices and catalog facts are approval-time evidence, not timeless values. The requested ZDR policy is not evidence that ZDR was actually used.

## Limits and semantics

The profile permits exactly 12 sequential slots, one exchange per slot, and no thirteenth exchange. Each slot allows 4,096 input tokens, 128 output tokens, 8 KiB request bytes, and 8 KiB response bytes; the credential lease lifetime is 5 seconds, aggregate execution timeout is 60 seconds, and per-slot reservation is 1,000 microusd. The aggregate reservation ceiling is 12,000 microusd (`$0.012`).

`Authorize` performs zero journal, credential, network, or file I/O. A capability is single-use. With live disabled, only the exact sealed offline fake may run; production HTTP is compiled and tested offline only. Redirects, retries, fallbacks, alternates, proxies, cookies, ambient authentication, and decompression are disallowed. A post-dispatch failure is `Unknown`/`Unknown`. Exact request bytes are digested; retained evidence is canonical and raw-free. Cleanup claims cover only exact lease-owned/application mutable buffers. Managed framework Bearer copies cannot be globally proven erased.

## Evidence and limits

Independent review first found NO-GO with 4 P1 and 2 P2 findings. One bounded correction closed all findings; final deep review was CODE GO with no P0-P2. Focused independent validation was 68/68; owner SnowGlobe Release was 774/774; independent Release build had 0 warnings/errors. Snapshot manifest: `927cf8615ac6ad314938538a695eb9a9ce2c4bfe10c43cac4c379e9d9281b198`.

No key lookup, account/credit mutation, paid inference, DNS/live HTTP/provider traffic, production journal persistence, model download, world action, or deployment occurred; no retained live/provider artifact or comparison file was produced. Offline tests did construct in-memory artifacts. This is not live-ready. Blocker: `paid_cost_ceiling_and_account_binding_unresolved`; dated-slug callability and real response compatibility are unverified, no durable live journal exists, nonce replay is process-local/bounded, and managed Bearer copies remain residual. This evidence makes no provider-authenticity, quality, winner, cost-result, or global-secret-erasure claim.

## One current next action

Bind an OpenRouter account identity plus trusted credential lease source and explicitly approve the frozen aggregate ceiling of 12,000 microusd (`$0.012`), then verify dated-slug callability, current catalog and price, and a durable journal in a security-reviewed preflight before any separately authorized one-shot provider run.
