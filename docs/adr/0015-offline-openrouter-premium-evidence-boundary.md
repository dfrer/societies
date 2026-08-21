# ADR 0015: Offline OpenRouter premium-evidence boundary

- Status: Accepted offline boundary; not live-ready
- Date: 2026-08-20
- Code commit: `ef32576` (`Add offline OpenRouter premium evidence boundary`)

## Decision

Design-It-Twice compared a minimal deep Module with a common-run module. The chosen hybrid is a tiny authorize -> single-use run -> pure validate caller seam, with a separate immutable profile registry and a separate premium-evidence journal. Existing `ProviderAdapterPreflight`, offline recording, legacy `FinancialJournal`, `LocalPremiumComparison`, `SnowGlobeTwoTierCognitionModule`, the deterministic world, `src/societies/`, and v1-v4 artifacts remain unchanged.

The exact new files are:

- `labs/Societies.SnowGlobe/AssemblyInfo.cs`
- `labs/Societies.SnowGlobe/OpenRouterPremiumProfile.cs`
- `labs/Societies.SnowGlobe/OpenRouterPremiumJournal.cs`
- `labs/Societies.SnowGlobe/OpenRouterPremiumExchange.cs`
- `labs/Societies.SnowGlobe/OpenRouterPremiumEvidence.cs`
- `tests/Societies.SnowGlobe.Tests/OpenRouterPremiumProfileTests.cs`
- `tests/Societies.SnowGlobe.Tests/OpenRouterPremiumEvidenceTests.cs`
- `tests/Societies.SnowGlobe.Tests/OpenRouterPremiumHttpExchangeTests.cs`

Approval-time model/profile facts were verified on 2026-08-20 against the [OpenRouter model page](https://openrouter.ai/openai/gpt-5.6-luna-20260709), [create-a-chat-completion API](https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion), [router metadata documentation](https://openrouter.ai/docs/guides/features/router-metadata), and [provider-selection policy](https://openrouter.ai/docs/guides/routing/provider-selection): model id `openai/gpt-5.6-luna`; canonical request slug `openai/gpt-5.6-luna-20260709`; context `1,050,000`; base catalog prompt price `$0.20/M` tokens and completion price `$1.20/M`; threshold override at 272,000 input tokens is prompt `$0.40/M` and completion `$1.80/M`; `response_format`, structured outputs, and `max_tokens` supported; temperature omitted. The exact request is `POST https://openrouter.ai/api/v1/chat/completions` with Bearer authentication, `stream=false`, strict `json_schema`, `provider.order` plus `provider.only` set to `openai`, `require_parameters=true`, `allow_fallbacks=false`, `data_collection=deny`, `zdr=true`, and `X-OpenRouter-Metadata` enabled. Prices and catalog facts are approval-time evidence, not timeless facts; ZDR is a requested policy, not evidence of actual use.

The hard 4,096-input bound makes the 272,000-token threshold override unreachable for this slice. Hard profile limits are exactly 12 sequential slots, at most one exchange per slot with no thirteenth exchange, 4,096 input and 128 output tokens per slot, 8 KiB request and response per slot, a 5-second credential-lease lifetime, a 60-second aggregate execution timeout, a 1,000 microusd per-slot reservation, and a 12,000 microusd (`$0.012`) aggregate reservation ceiling.

`Authorize` has zero journal, credential, network, and file I/O. The capability is single-use. Only the exact sealed offline fake may run while live is disabled; production HTTP is compiled and tested offline only. There are no redirects, retries, fallbacks, alternates, proxies, cookies, ambient authentication, or decompression. Post-dispatch failures are `Unknown`/`Unknown`. Exact request bytes are digested and evidence is canonical and raw-free. Credential-cleanup claims are limited to exact lease-owned/application mutable buffers; managed framework Bearer copies cannot be globally proven erased.

## Validation and boundary

Independent review first returned NO-GO with 4 P1 and 2 P2 findings. One bounded correction closed all findings; final deep review returned CODE GO with no P0-P2 findings. Focused independent validation passed 68/68; owner full SnowGlobe Release passed 774/774; independent Release build passed with 0 warnings/errors. Snapshot manifest: `927cf8615ac6ad314938538a695eb9a9ce2c4bfe10c43cac4c379e9d9281b198`.

No provider key lookup, account or credit mutation, paid inference, DNS or live HTTP/provider traffic, production journal persistence, model download, world action, or deployment occurred; no retained live/provider artifact or comparison file was produced. Offline tests did construct in-memory artifacts. This boundary is not live-ready. The exact live blocker is `paid_cost_ceiling_and_account_binding_unresolved`; dated-slug callability and real response compatibility remain unverified, durable live-journal implementation is absent, nonce replay is process-local/bounded, and managed Bearer copies remain residual.

## One current next action

Bind an OpenRouter account identity plus trusted credential lease source and explicitly approve the frozen aggregate ceiling of 12,000 microusd (`$0.012`), then verify dated-slug callability, current catalog and price, and a durable journal in a security-reviewed preflight before any separately authorized one-shot provider run.
