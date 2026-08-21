using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

public sealed record OpenRouterPremiumProfileIdentity
{
    private const string Prefix = "openrouter-premium-profile-sha256-";

    public OpenRouterPremiumProfileIdentity(string value)
    {
        if (value is null || value.Length != Prefix.Length + 64 || !value.StartsWith(Prefix, StringComparison.Ordinal)
            || !OpenRouterPremiumCanonical.IsDigest(value[Prefix.Length..]))
            throw new ArgumentException("OpenRouter premium profile identity is invalid.", nameof(value));
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record OpenRouterPremiumBounds(
    int RequiredScenarioCount,
    int MaximumExchanges,
    int MaximumRequestBytes,
    int MaximumResponseBytes,
    int MaximumAggregateRequestBytes,
    int MaximumAggregateResponseBytes,
    int MaximumResponseHeaderBytes,
    int MaximumJsonDepth,
    int MaximumJsonTokens,
    int MaximumStringCharacters,
    int MaximumArrayItems,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    int TimeoutMilliseconds,
    int CredentialLeaseLifetimeMilliseconds,
    long PerSlotCostCeilingMicrousd,
    long AggregateCostCeilingMicrousd);

/// <summary>
/// Immutable approval-time facts. Catalog and documentation evidence is intentionally dated and
/// content-addressed; it is not a claim that mutable provider metadata is timeless.
/// </summary>
public sealed class OpenRouterPremiumProfile
{
    internal OpenRouterPremiumProfile()
    {
        Bounds = new OpenRouterPremiumBounds(
            12, 12, 8 * 1024, 8 * 1024, 12 * 8 * 1024, 12 * 8 * 1024,
            8 * 1024, 8, 256, 4 * 1024, 16, 4 * 1024, 128, 60_000, 5_000,
            1_000, 12_000);
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create(PromptRevision);
        PromptPublicationDigestSha256 = publication.CanonicalDigestSha256;
        PromptSetDigestSha256 = publication.PromptSetDigestSha256;
        CorpusDigestSha256 = CognitionQualityCorpusV1.ExpectedManifestDigestSha256;
        ScoringDigestSha256 = CognitionQualityCorpusV1.ScoringDigestSha256;

        string descriptor = string.Join('\n',
            SchemaVersion, EffectiveUri, HttpMethod, AuthenticationScheme, ProviderSlug, ProviderResponseIdentity,
            ModelIdentity, CanonicalModelSlug, ContextLengthTokens, PromptMicrousdPerMillionTokens,
            CompletionMicrousdPerMillionTokens, CatalogEvidenceDigestSha256, EndpointEvidenceDigestSha256,
            PromptRevision, PromptPublicationDigestSha256, PromptSetDigestSha256, CorpusDigestSha256,
            ScoringDigestSha256, ProposalSchemaVersion, OutputSchemaIdentity,
            $"request_model={CanonicalModelSlug}", "stream=false", "response_format=json_schema", "strict=true", "temperature=omitted",
            "provider.order=openai", "provider.only=openai", "provider.allow_fallbacks=false",
            "provider.require_parameters=true", "provider.data_collection=deny", "provider.zdr=true",
            "redirect=false", "retry=false", "proxy=false", "cookies=false", "ambient_auth=false",
            "decompression=false", "router_metadata=enabled", "canonical_slug_callability=unverified",
            "live=false", LiveTrafficBlockerCode,
            OpenRouterPremiumCanonical.Bounds(Bounds));
        ProfileDigestSha256 = OpenRouterPremiumCanonical.Digest(descriptor);
        Identity = new OpenRouterPremiumProfileIdentity("openrouter-premium-profile-sha256-" + ProfileDigestSha256);
        Validate();
    }

    public const string SchemaVersion = "snow_globe_openrouter_premium_profile/v1";
    public const string EffectiveUri = "https://openrouter.ai/api/v1/chat/completions";
    public const string HttpMethod = "POST";
    public const string AuthenticationScheme = "Bearer";
    public const string ProviderSlug = "openai";
    public const string ProviderResponseIdentity = "OpenAI";
    public const string ModelIdentity = "openai/gpt-5.6-luna";
    public const string CanonicalModelSlug = "openai/gpt-5.6-luna-20260709";
    public const int ContextLengthTokens = 1_050_000;
    public const long PromptMicrousdPerMillionTokens = 200_000;
    public const long CompletionMicrousdPerMillionTokens = 1_200_000;
    public const string CatalogEvidenceDigestSha256 = "68aabd8ed3d88a41d4b0d0856512e6008d780b39eca9606682812f9755e91e9f";
    public const string EndpointEvidenceDigestSha256 = "5117d521a43040174b59ba95486f3aef4736e243100d61fbd7e18dc3c4d22284";
    public const string CatalogEvidenceCanonicalDescriptor = "retrieved_at=2026-08-20\nsource=https://openrouter.ai/api/v1/models\nid=openai/gpt-5.6-luna\ncanonical_slug=openai/gpt-5.6-luna-20260709\ncontext_length=1050000\nprompt_usd_per_token=0.0000002\ncompletion_usd_per_token=0.0000012\npricing_override_min_prompt_tokens=272000\npricing_override_prompt_usd_per_token=0.0000004\npricing_override_completion_usd_per_token=0.0000018\nsupported_parameters=include_reasoning,max_completion_tokens,max_tokens,reasoning,reasoning_effort,response_format,seed,structured_outputs,tool_choice,tools\ntemperature_advertised=false";
    public const string EndpointEvidenceCanonicalDescriptor = "verified_at=2026-08-20\nendpoint_source=https://openrouter.ai/docs/api/api-reference/chat/create-a-chat-completion\nmetadata_source=https://openrouter.ai/docs/guides/features/router-metadata\nendpoint=https://openrouter.ai/api/v1/chat/completions\nmethod=POST\nauthorization=Bearer\ncontent_type=application/json\nrequest_model=openai/gpt-5.6-luna-20260709\nstream=false\nresponse_format=json_schema\njson_schema_strict=true\nprovider_order=openai\nprovider_only=openai\nprovider_allow_fallbacks=false\nprovider_require_parameters=true\nprovider_data_collection=deny\nprovider_zdr=true\nrequest_header_x-openrouter-metadata=enabled\nsuccess_response_field=openrouter_metadata\nautomatic_retry=false\nredirect=false";
    public const string CatalogRetrievedDate = "2026-08-20";
    public const string EndpointContractVerifiedDate = "2026-08-20";
    public const string PromptRevision = "prompt-v1";
    public const string ProposalSchemaVersion = "snow_globe_cognition_quality_proposal_response/v1";
    public const string OutputSchemaIdentity = "snow_globe_openrouter_premium_json_schema/v1";
    public const string LiveTrafficBlockerCode = "paid_cost_ceiling_and_account_binding_unresolved";

    public OpenRouterPremiumProfileIdentity Identity { get; }
    public string ProfileDigestSha256 { get; }
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string CorpusDigestSha256 { get; }
    public string ScoringDigestSha256 { get; }
    public OpenRouterPremiumBounds Bounds { get; }
    public bool SupportsResponseFormat => true;
    public bool RequiresStructuredOutputs => true;
    public bool SupportsMaximumTokens => true;
    public bool TemperatureAdvertised => false;
    public bool ZdrRequested => true;
    public bool CanonicalModelCallabilityProven => false;
    public bool LiveTrafficEnabled => false;

    private void Validate()
    {
        if (!Uri.TryCreate(EffectiveUri, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "openrouter.ai" || uri.Port != 443
            || uri.AbsolutePath != "/api/v1/chat/completions" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || Bounds.RequiredScenarioCount != CognitionQualityCorpusV1.ScenarioCount
            || Bounds.MaximumExchanges != Bounds.RequiredScenarioCount
            || Bounds.MaximumInputTokens + Bounds.MaximumOutputTokens > ContextLengthTokens
            || Bounds.AggregateCostCeilingMicrousd != Bounds.PerSlotCostCeilingMicrousd * Bounds.RequiredScenarioCount
            || !OpenRouterPremiumCanonical.IsDigest(ProfileDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(PromptPublicationDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(PromptSetDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(CorpusDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(ScoringDigestSha256)
            || !string.Equals(OpenRouterPremiumCanonical.Digest(CatalogEvidenceCanonicalDescriptor), CatalogEvidenceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(OpenRouterPremiumCanonical.Digest(EndpointEvidenceCanonicalDescriptor), EndpointEvidenceDigestSha256, StringComparison.Ordinal))
            throw new InvalidDataException("OpenRouter premium profile is invalid.");
    }
}

public static class OpenRouterPremiumProfileRegistry
{
    private static readonly OpenRouterPremiumProfile Profile = new();
    public static OpenRouterPremiumProfile Selected => Profile;

    public static OpenRouterPremiumProfile Resolve(OpenRouterPremiumProfileIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity != Profile.Identity)
            throw new OpenRouterPremiumEvidenceException("profile_mismatch");
        return Profile;
    }
}

internal static class OpenRouterPremiumCanonical
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static string Digest(string value) => Digest(StrictUtf8.GetBytes(value));
    internal static string Digest(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    internal static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    internal static bool IsIdentity(string? value) => SnowGlobeInferenceIdentity.IsCanonical(value);
    internal static string Bounds(OpenRouterPremiumBounds value) => string.Join('|',
        value.RequiredScenarioCount, value.MaximumExchanges, value.MaximumRequestBytes, value.MaximumResponseBytes,
        value.MaximumAggregateRequestBytes, value.MaximumAggregateResponseBytes, value.MaximumResponseHeaderBytes,
        value.MaximumJsonDepth, value.MaximumJsonTokens, value.MaximumStringCharacters, value.MaximumArrayItems,
        value.MaximumInputTokens, value.MaximumOutputTokens, value.TimeoutMilliseconds,
        value.CredentialLeaseLifetimeMilliseconds, value.PerSlotCostCeilingMicrousd, value.AggregateCostCeilingMicrousd);
}
