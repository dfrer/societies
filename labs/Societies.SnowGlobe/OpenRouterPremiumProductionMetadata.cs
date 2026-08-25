using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

internal sealed class OpenRouterPremiumHttpMetadataVerifier : IOpenRouterPremiumProductionMetadataVerifier,
    IOpenRouterAuthenticatedReadinessMetadataVerifier
{
    internal const string CurrentKeyUri = "https://openrouter.ai/api/v1/key";
    internal const string ModelsUri = "https://openrouter.ai/api/v1/models?zdr=true&providers=Azure";
    internal const string ZdrEndpointsUri = "https://openrouter.ai/api/v1/endpoints/zdr";
    internal const int MaximumKeyResponseBytes = 16 * 1024;
    internal const int MaximumModelsResponseBytes = 1024 * 1024;
    internal const int MaximumZdrResponseBytes = 1024 * 1024;
    internal const int MinimumKeyRemainingLifetimeMilliseconds = 5 * 60 * 1000;
    internal const string RequiredKeyExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    internal static readonly string OfficialContractDescriptor =
         "verified_at=2026-08-21\n"
        + "key_docs=https://openrouter.ai/docs/api/api-reference/api-keys/get-current-key\n"
         + "models_docs=https://openrouter.ai/docs/api/api-reference/models/get-models\n"
         + "zdr_docs=https://openrouter.ai/docs/api/api-reference/endpoints/list-endpoints-zdr\n"
         + "routing_docs=https://openrouter.ai/docs/guides/routing/provider-selection\n"
        + $"model_page=https://openrouter.ai/{OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity}\n"
        + $"api_model_id={OpenRouterPremiumProfile.ModelIdentity}\nrelease_date={OpenRouterPremiumProfile.ModelReleaseDateUtc}\n"
         + $"catalog_evidence_digest_sha256={OpenRouterPremiumProfile.CatalogEvidenceDigestSha256}\n"
         + "pricing_override_provenance=authenticated_current_api_and_frozen_model_page\n"
         + "pricing_override_authenticated_current=true\npricing_override_reachable=false\n"
        + $"key_expires_at=exact_utc_seconds_or_null_for_nonexpiring_with_24h_attestation\nkey_minimum_remaining_lifetime_ms={MinimumKeyRemainingLifetimeMilliseconds}\n"
        + "key_limit_policy=capped_with_at_least_0.018_remaining_or_both_limit_fields_null_for_unlimited\n"
         + "key_byok_usage_fields=optional_bounded_nonnegative_decimal_telemetry_only\n"
         + "credential_cycle=one_owned_snapshot_for_all_three_gets_and_exact_snapshot_account_binding\n"
         + $"paid_provider_tag={OpenRouterPremiumProfile.ProviderSlug}\npaid_provider_sort=price\n"
         + $"paid_max_prompt_usd_per_million={OpenRouterPremiumProfile.ProviderMaxPromptUsdPerMillionTokens.ToString(CultureInfo.InvariantCulture)}\n"
         + $"paid_max_completion_usd_per_million={OpenRouterPremiumProfile.ProviderMaxCompletionUsdPerMillionTokens.ToString(CultureInfo.InvariantCulture)}\n"
         + "paid_output_limit_parameter=max_completion_tokens\n"
        + $"key_uri={CurrentKeyUri}\nmodels_uri={ModelsUri}\nzdr_uri={ZdrEndpointsUri}\n"
         + $"method=GET\nauthorization=Bearer\nhardened_policy_sha256={OpenRouterPremiumHardenedHttp.PolicyDigestSha256}";
    internal static readonly string OfficialContractDigestSha256 = OpenRouterPremiumCanonical.Digest(OfficialContractDescriptor);
    internal static string HardenedHttpPolicyDigestSha256 => OpenRouterPremiumHardenedHttp.PolicyDigestSha256;

    private readonly IOpenRouterPremiumCredentialStore _store;
    private readonly IOpenRouterPremiumProductionClock _clock;
    private readonly Func<HttpMessageHandler> _handlerFactory;

    private OpenRouterPremiumHttpMetadataVerifier(IOpenRouterPremiumCredentialStore store,
        IOpenRouterPremiumProductionClock clock, Func<HttpMessageHandler> handlerFactory)
    {
        _store = store; _clock = clock; _handlerFactory = handlerFactory;
    }

    internal static OpenRouterPremiumHttpMetadataVerifier CreateProduction(
        IOpenRouterPremiumCredentialStore store, IOpenRouterPremiumProductionClock clock) =>
        new(store, clock, OpenRouterPremiumHardenedHttp.CreateSocketsHandler);

    internal static OpenRouterPremiumHttpMetadataVerifier CreateForOfflineTests(
        IOpenRouterPremiumCredentialStore store, IOpenRouterPremiumProductionClock clock,
        Func<HttpMessageHandler> handlerFactory) => new(store, clock, handlerFactory);

    internal bool RedirectsAllowed => false;
    internal bool AutomaticRetriesAllowed => false;
    internal bool ProxyAllowed => false;
    internal bool CookiesAllowed => false;
    internal bool AmbientAuthenticationAllowed => false;
    internal bool AutomaticDecompressionAllowed => false;

    public ValueTask<OpenRouterPremiumVerifiedMetadata> VerifyOnceAsync(CancellationToken cancellationToken) =>
        VerifyCoreAsync(cancellationToken, null);

    public async ValueTask<OpenRouterAuthenticatedMetadataReadinessResult> ObserveReadinessOnceAsync(
        CancellationToken cancellationToken)
    {
        int requestCount = 0;
        try
        {
            using OpenRouterPremiumVerifiedMetadata verified = await VerifyCoreAsync(
                cancellationToken,
                () => requestCount++).ConfigureAwait(false);
            return new(true, "ready", requestCount, true);
        }
        catch (OperationCanceledException)
        {
            return new(false, "operation_cancelled", requestCount, false);
        }
        catch (OpenRouterPremiumProductionException exception)
        {
            return new(false, ClassifyReadinessFailure(exception.Code), requestCount, false);
        }
        catch
        {
            return new(false, "observation_failed", requestCount, false);
        }
    }

    private async ValueTask<OpenRouterPremiumVerifiedMetadata> VerifyCoreAsync(
        CancellationToken cancellationToken,
        Action? requestStarted)
    {
        long observedAt = _clock.NowMilliseconds;
        using CancellationTokenSource aggregate = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        aggregate.CancelAfter(20_000);
        using HttpMessageHandler handler = _handlerFactory();
        using HttpClient client = new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        OpenRouterPremiumStoredCredential? credential = null;
        byte[]? credentialMaterial = null;
        char[]? credentialCharacters = null;
        string? managedBearer = null;
        byte[]? keyBytes = null; byte[]? modelsBytes = null; byte[]? zdrBytes = null; byte[]? bundleBytes = null;
        try
        {
            credential = _store.Read();
            credentialMaterial = credential.TransferOwnedMaterial();
            if (!OpenRouterPremiumCredentialMaterial.IsValid(credentialMaterial))
                throw new OpenRouterPremiumProductionException("credential_malformed");
            credentialCharacters = new char[credentialMaterial.Length];
            for (int index = 0; index < credentialMaterial.Length; index++)
                credentialCharacters[index] = (char)credentialMaterial[index];
            managedBearer = new string(credentialCharacters); // Documented framework-owned managed residual.
            keyBytes = await GetOnceAsync(client, CurrentKeyUri, MaximumKeyResponseBytes,
                "metadata_key_http_status_terminal", managedBearer, requestStarted, aggregate.Token).ConfigureAwait(false);
            KeyObservation key = ParseKey(keyBytes, observedAt);
            modelsBytes = await GetOnceAsync(client, ModelsUri, MaximumModelsResponseBytes,
                "metadata_models_http_status_terminal", managedBearer, requestStarted, aggregate.Token).ConfigureAwait(false);
            ModelObservation model = ParseModels(modelsBytes);
            zdrBytes = await GetOnceAsync(client, ZdrEndpointsUri, MaximumZdrResponseBytes,
                "metadata_zdr_http_status_terminal", managedBearer, requestStarted, aggregate.Token).ConfigureAwait(false);
            aggregate.Token.ThrowIfCancellationRequested();
            ZdrObservation zdr = ParseZdr(zdrBytes);
            if (model.ProviderName != zdr.ProviderName || model.ModelId != zdr.ModelId
                || model.PromptUsdPerToken != zdr.PromptUsdPerToken
                || model.CompletionUsdPerToken != zdr.CompletionUsdPerToken)
                throw new OpenRouterPremiumProductionException("metadata_crosscheck_mismatch");
            aggregate.Token.ThrowIfCancellationRequested();
            _store.BindAccount(key.AccountBindingIdentity, credential.AccountBindingIdentity, credentialMaterial);
            bundleBytes = BuildBundle(key, model, observedAt,
                OpenRouterPremiumCanonical.Digest(keyBytes), OpenRouterPremiumCanonical.Digest(modelsBytes), OpenRouterPremiumCanonical.Digest(zdrBytes));
            OpenRouterPremiumVerifiedMetadata result = new(bundleBytes);
            bundleBytes = null;
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new OpenRouterPremiumProductionException("metadata_timeout"); }
        catch (HttpRequestException) { throw new OpenRouterPremiumProductionException("metadata_transport_failed"); }
        finally
        {
            if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
            if (modelsBytes is not null) CryptographicOperations.ZeroMemory(modelsBytes);
            if (zdrBytes is not null) CryptographicOperations.ZeroMemory(zdrBytes);
            if (bundleBytes is not null) CryptographicOperations.ZeroMemory(bundleBytes);
            if (credentialMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(credentialMaterial);
                credential?.ZeroObserver(credentialMaterial.All(static value => value == 0));
            }
            if (credentialCharacters is not null) Array.Clear(credentialCharacters);
            managedBearer = null;
            credential?.Dispose();
        }
    }

    private async Task<byte[]> GetOnceAsync(HttpClient client, string exactUri, int maximumBytes,
        string statusFailureCode, string managedBearer, Action? requestStarted,
        CancellationToken aggregateToken)
    {
        using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(aggregateToken);
        requestTimeout.CancelAfter(5_000);
        using HttpRequestMessage request = new(HttpMethod.Get, exactUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managedBearer);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestStarted?.Invoke();
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            requestTimeout.Token).ConfigureAwait(false);
        string effective = response.RequestMessage?.RequestUri?.AbsoluteUri ?? string.Empty;
        if (!string.Equals(effective, exactUri, StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("metadata_effective_uri_mismatch");
        if ((int)response.StatusCode != 200)
            throw new OpenRouterPremiumProductionException(statusFailureCode);
        if (response.Content.Headers.ContentEncoding.Count != 0)
            throw new OpenRouterPremiumProductionException("metadata_encoding_forbidden");
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new OpenRouterPremiumProductionException("metadata_content_type_invalid");
        string? charset = response.Content.Headers.ContentType?.CharSet;
        if (charset is not null && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
            throw new OpenRouterPremiumProductionException("metadata_content_type_invalid");
        if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
            throw new OpenRouterPremiumProductionException("metadata_response_too_large");
        return await OpenRouterPremiumHardenedHttp.ReadBoundedAsync(
            response.Content, maximumBytes,
            static () => new OpenRouterPremiumProductionException("metadata_response_too_large"),
            requestTimeout.Token).ConfigureAwait(false);
    }

    private static string ClassifyReadinessFailure(string code) => code switch
    {
        "credential_missing" or "credential_malformed" or "credential_account_mismatch" or
        "key_authority_insufficient" or "key_expiry_window_invalid" => "credential_unavailable",
        "metadata_timeout" => "observation_timeout",
        "metadata_transport_failed" or "metadata_key_http_status_terminal" or
        "metadata_models_http_status_terminal" or "metadata_zdr_http_status_terminal" => "provider_unavailable",
        "key_metadata_invalid" or "model_metadata_invalid" or "model_metadata_mismatch" or
        "zdr_metadata_invalid" or "zdr_route_mismatch" or "metadata_crosscheck_mismatch" or
        "metadata_content_type_invalid" or "metadata_effective_uri_mismatch" or
        "metadata_encoding_forbidden" or "metadata_parameters_invalid" or
        "metadata_price_mismatch" or "metadata_response_too_large" or "metadata_shape_invalid" => "metadata_rejected",
        _ => "observation_failed"
    };

    private static KeyObservation ParseKey(byte[] bytes, long observedAt)
    {
        using JsonDocument document = MetadataJson.Parse(bytes, 6, 512, 256, "key_metadata_invalid");
        JsonElement root = document.RootElement;
        MetadataJson.RequireAllowed(root, ["data"], "key_metadata_invalid");
        JsonElement data = MetadataJson.Object(root, "data");
        MetadataJson.RequireAllowed(data,
            ["creator_user_id", "label", "limit", "limit_remaining", "usage", "expires_at", "is_management_key",
             "usage_daily", "usage_weekly", "usage_monthly", "is_free_tier", "is_provisioning_key",
             "byok_usage", "byok_usage_daily", "byok_usage_weekly", "byok_usage_monthly",
             "include_byok_in_limit", "created_at", "updated_at", "rate_limit", "limit_reset"], "key_metadata_invalid");
        string creator = MetadataJson.String(data, "creator_user_id", 256);
        string label = MetadataJson.String(data, "label", 256);
        decimal? limit = MetadataJson.NullableNonnegativeDecimal(data, "limit", "key_metadata_invalid");
        decimal? remaining = MetadataJson.NullableNonnegativeDecimal(data, "limit_remaining", "key_metadata_invalid");
        decimal usage = MetadataJson.Decimal(data, "usage");
        MetadataJson.OptionalLimitReset(data, "limit_reset", "key_metadata_invalid");
        foreach (string telemetry in new[] { "byok_usage", "byok_usage_daily", "byok_usage_weekly", "byok_usage_monthly" })
            MetadataJson.OptionalNonnegativeDecimal(data, telemetry, "key_metadata_invalid");
        bool unlimited = limit is null && remaining is null;
        bool capped = limit is not null && remaining is not null;
        if (!unlimited && !capped)
            throw new OpenRouterPremiumProductionException("key_metadata_invalid");
        if (capped && (limit!.Value < 0.018m || remaining!.Value < 0.018m || usage > limit.Value)
            || MetadataJson.Boolean(data, "is_management_key"))
            throw new OpenRouterPremiumProductionException("key_authority_insufficient");
        if (!data.TryGetProperty("expires_at", out JsonElement expiry))
            throw new OpenRouterPremiumProductionException("key_expiry_window_invalid");
        long authorityExpiry;
        if (expiry.ValueKind == JsonValueKind.Null)
        {
            authorityExpiry = checked(observedAt + (long)TimeSpan.FromDays(1).TotalMilliseconds);
        }
        else
        {
            if (expiry.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParseExact(expiry.GetString(), RequiredKeyExpiryFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed)
                || parsed.Offset != TimeSpan.Zero
                || parsed.ToUnixTimeMilliseconds() < checked(observedAt + MinimumKeyRemainingLifetimeMilliseconds))
                throw new OpenRouterPremiumProductionException("key_expiry_window_invalid");
            authorityExpiry = Math.Min(parsed.ToUnixTimeMilliseconds(),
                checked(observedAt + (long)TimeSpan.FromDays(1).TotalMilliseconds));
        }
        string binding = OpenRouterPremiumCanonical.Digest(string.Join('|',
            "openrouter-account-key-subject/v1", creator, label));
        return new("byok-account-sha256-" + binding, authorityExpiry);
    }

    private static ModelObservation ParseModels(byte[] bytes)
    {
        using JsonDocument document = MetadataJson.Parse(bytes, 10, 131_072, 4096, "model_metadata_invalid");
        JsonElement root = document.RootElement;
        MetadataJson.RequireAllowed(root, ["data", "links", "total_count"], "model_metadata_invalid");
        JsonElement data = root.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() is < 1 or > 4096)
            throw new OpenRouterPremiumProductionException("model_metadata_invalid");
        if (root.TryGetProperty("links", out JsonElement links) && links.ValueKind != JsonValueKind.Object
            || root.TryGetProperty("total_count", out JsonElement totalCount)
                && (!totalCount.TryGetInt32(out int parsedTotal) || parsedTotal < data.GetArrayLength()))
            throw new OpenRouterPremiumProductionException("model_metadata_invalid");
        List<ModelObservation> matches = [];
        foreach (JsonElement model in data.EnumerateArray())
        {
            if (model.ValueKind != JsonValueKind.Object) throw new OpenRouterPremiumProductionException("model_metadata_invalid");
            if (!model.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.String
                || id.GetString() != OpenRouterPremiumProfile.ModelIdentity) continue;
            MetadataJson.RequireAllowed(model,
                ["id", "canonical_slug", "hugging_face_id", "name", "created", "description", "context_length",
                 "architecture", "pricing", "top_provider", "per_request_limits", "supported_parameters", "default_parameters",
                 "links", "supported_voices", "expiration_date", "knowledge_cutoff", "benchmarks", "reasoning"],
                 "model_metadata_invalid");
            if (MetadataJson.String(model, "canonical_slug", 256) != OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity
                || MetadataJson.Int32(model, "context_length") != OpenRouterPremiumProfile.ContextLengthTokens)
                throw new OpenRouterPremiumProductionException("model_metadata_mismatch");
            JsonElement pricing = MetadataJson.Object(model, "pricing");
            (string prompt, string completion) = MetadataJson.Pricing(pricing, "model_metadata_invalid");
            MetadataJson.RequirePricingOverride(pricing, "model_metadata_invalid");
            MetadataJson.RequireParameters(model.GetProperty("supported_parameters"));
            matches.Add(new(OpenRouterPremiumProfile.ModelIdentity, OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity,
                OpenRouterPremiumProfile.ProviderResponseIdentity,
                prompt, completion));
        }
        if (matches.Count != 1) throw new OpenRouterPremiumProductionException("model_metadata_mismatch");
        return matches[0];
    }

    private static ZdrObservation ParseZdr(byte[] bytes)
    {
        using JsonDocument document = MetadataJson.Parse(bytes, 10, 131_072, 4096, "zdr_metadata_invalid");
        JsonElement root = document.RootElement;
        MetadataJson.RequireAllowed(root, ["data"], "zdr_metadata_invalid");
        JsonElement data = root.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() is < 1 or > 4096)
            throw new OpenRouterPremiumProductionException("zdr_metadata_invalid");
        List<ZdrObservation> matches = [];
        foreach (JsonElement endpoint in data.EnumerateArray())
        {
            if (endpoint.ValueKind != JsonValueKind.Object) throw new OpenRouterPremiumProductionException("zdr_metadata_invalid");
            if (!endpoint.TryGetProperty("model_id", out JsonElement id) || id.ValueKind != JsonValueKind.String
                || id.GetString() != OpenRouterPremiumProfile.CanonicalModelSlug) continue;
            MetadataJson.RequireAllowed(endpoint,
                ["name", "model_id", "model_name", "context_length", "pricing", "provider_name", "tag", "quantization",
                 "max_completion_tokens", "max_prompt_tokens", "supported_parameters", "status", "uptime_last_30m",
                 "latency_last_30m", "throughput_last_30m", "supports_implicit_caching", "supports_voice_cloning",
                 "uptime_last_1d", "uptime_last_5m"],
                "zdr_metadata_invalid");
            string provider = MetadataJson.String(endpoint, "provider_name", 128);
            if (provider != OpenRouterPremiumProfile.ProviderResponseIdentity
                || MetadataJson.String(endpoint, "tag", 128) != OpenRouterPremiumProfile.ProviderSlug
                || MetadataJson.Int32(endpoint, "context_length") != OpenRouterPremiumProfile.ContextLengthTokens)
                continue;
            (string prompt, string completion) = MetadataJson.Pricing(MetadataJson.Object(endpoint, "pricing"), "zdr_metadata_invalid");
            MetadataJson.RequireParameters(endpoint.GetProperty("supported_parameters"));
            matches.Add(new(OpenRouterPremiumProfile.CanonicalModelSlug, provider, prompt, completion));
        }
        if (matches.Count != 1) throw new OpenRouterPremiumProductionException("zdr_route_mismatch");
        return matches[0];
    }

    private static byte[] BuildBundle(KeyObservation key, ModelObservation model, long observedAt,
        string keyDigest, string modelsDigest, string zdrDigest)
    {
        DateTimeOffset observed = DateTimeOffset.FromUnixTimeMilliseconds(observedAt);
        string observedText = observed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string expiresText = DateTimeOffset.FromUnixTimeMilliseconds(key.AuthorityExpiresAtUnixMilliseconds)
            .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        OpenRouterPremiumCatalogSnapshot catalog = new(ModelsUri, "GET", "Bearer", true, 200, "data",
            OpenRouterPremiumProfile.ModelIdentity, model.CanonicalSlug, OpenRouterPremiumProfile.ContextLengthTokens,
            model.PromptUsdPerToken, model.CompletionUsdPerToken,
            OpenRouterPremiumProfile.PricingOverrideMinimumPromptTokens,
            OpenRouterPremiumProfile.PricingOverridePromptUsdPerToken,
            OpenRouterPremiumProfile.PricingOverrideCompletionUsdPerToken,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
            true, false,
            ["max_completion_tokens", "reasoning", "response_format", "structured_outputs"], OpenRouterPremiumProfile.ProviderSlug, true, true);
        string evidenceDigest = OpenRouterPremiumCanonical.Digest(string.Join('|', OfficialContractDigestSha256,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
            keyDigest, modelsDigest, zdrDigest, key.AccountBindingIdentity));
        OpenRouterPremiumCredentialSourceAttestation attestation = new(
            "snow_globe_openrouter_credential_source_attestation/v1",
            OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
            OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity,
            key.AccountBindingIdentity, true, true, true, false,
            "openrouter.chat.completions/one-shot-12", observedText, expiresText,
            "authenticated-metadata-" + evidenceDigest[..32]);
        OpenRouterPremiumActivationBundle bundle = new(OpenRouterPremiumActivationPreflightCodec.SchemaVersion,
            observedText, key.AccountBindingIdentity,
            OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity,
            "production-preflight-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            12, 18_000, catalog, attestation, string.Empty);
        return OpenRouterPremiumActivationPreflightCodec.Write(bundle);
    }

    private sealed record KeyObservation(string AccountBindingIdentity, long AuthorityExpiresAtUnixMilliseconds);
    private sealed record ModelObservation(string ModelId, string CanonicalSlug, string ProviderName,
        string PromptUsdPerToken, string CompletionUsdPerToken);
    private sealed record ZdrObservation(string ModelId, string ProviderName, string PromptUsdPerToken, string CompletionUsdPerToken);
}

internal static class MetadataJson
{
    internal static JsonDocument Parse(byte[] bytes, int maximumDepth, int maximumTokens, int maximumArrayItems, string code)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes); int tokens = 0;
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = maximumDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            Stack<HashSet<string>> objects = new(); Stack<(bool IsArray, int Count)> containers = new();
            while (reader.Read())
            {
                if (++tokens > maximumTokens) throw new OpenRouterPremiumProductionException(code);
                bool startsValue = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray
                    or JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null;
                if (startsValue && containers.TryPeek(out (bool IsArray, int Count) parent) && parent.IsArray)
                {
                    containers.Pop();
                    if (++parent.Count > maximumArrayItems) throw new OpenRouterPremiumProductionException(code);
                    containers.Push(parent);
                }
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    objects.Push(new(StringComparer.Ordinal)); containers.Push((false, 0));
                }
                else if (reader.TokenType == JsonTokenType.StartArray) containers.Push((true, 0));
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    objects.Pop(); containers.Pop();
                }
                else if (reader.TokenType == JsonTokenType.EndArray) containers.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
                    throw new OpenRouterPremiumProductionException(code);
                if (reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String)
                {
                    long length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                    if (length > 8192) throw new OpenRouterPremiumProductionException(code);
                }
            }
            JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = maximumDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (document.RootElement.ValueKind != JsonValueKind.Object) { document.Dispose(); throw new OpenRouterPremiumProductionException(code); }
            return document;
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException(code); }
    }

    internal static void RequireAllowed(JsonElement value, string[] allowed, string code)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new OpenRouterPremiumProductionException(code);
        HashSet<string> names = new(allowed, StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Contains(property.Name)) throw new OpenRouterPremiumProductionException(code);
    }

    internal static JsonElement Object(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement result) && result.ValueKind == JsonValueKind.Object
            ? result : throw new OpenRouterPremiumProductionException("metadata_shape_invalid");
    internal static string String(JsonElement value, string name, int maximum) =>
        value.TryGetProperty(name, out JsonElement result) && result.ValueKind == JsonValueKind.String
        && result.GetString() is string parsed && parsed.Length is > 0 && parsed.Length <= maximum
        && !parsed.Any(char.IsControl) ? parsed : throw new OpenRouterPremiumProductionException("metadata_shape_invalid");
    internal static bool Boolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement result) && result.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? result.GetBoolean() : throw new OpenRouterPremiumProductionException("metadata_shape_invalid");
    internal static int Int32(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement result) && result.TryGetInt32(out int parsed)
            ? parsed : throw new OpenRouterPremiumProductionException("metadata_shape_invalid");
    internal static decimal Decimal(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement result) && result.ValueKind == JsonValueKind.Number
        && result.TryGetDecimal(out decimal parsed) && parsed >= 0
            ? parsed : throw new OpenRouterPremiumProductionException("metadata_shape_invalid");

    internal static decimal? NullableNonnegativeDecimal(JsonElement value, string name, string code)
    {
        if (!value.TryGetProperty(name, out JsonElement result))
            throw new OpenRouterPremiumProductionException(code);
        if (result.ValueKind == JsonValueKind.Null) return null;
        if (result.ValueKind == JsonValueKind.Number && result.TryGetDecimal(out decimal parsed) && parsed >= 0)
            return parsed;
        throw new OpenRouterPremiumProductionException(code);
    }

    internal static void OptionalNonnegativeDecimal(JsonElement value, string name, string code)
    {
        if (!value.TryGetProperty(name, out JsonElement result)) return;
        if (result.ValueKind != JsonValueKind.Number || !result.TryGetDecimal(out decimal parsed) || parsed < 0)
            throw new OpenRouterPremiumProductionException(code);
    }

    internal static void OptionalLimitReset(JsonElement value, string name, string code)
    {
        if (!value.TryGetProperty(name, out JsonElement result) || result.ValueKind == JsonValueKind.Null) return;
        if (result.ValueKind != JsonValueKind.String
            || result.GetString() is not ("daily" or "weekly" or "monthly"))
            throw new OpenRouterPremiumProductionException(code);
    }

    internal static (string Prompt, string Completion) Pricing(JsonElement pricing, string code)
    {
        RequireAllowed(pricing,
            ["prompt", "completion", "request", "image", "image_token", "internal_reasoning", "web_search",
             "input_cache_read", "input_cache_write", "discount", "overrides"], code);
        string prompt = String(pricing, "prompt", 64); string completion = String(pricing, "completion", 64);
        if (prompt != OpenRouterPremiumProfile.PromptUsdPerToken
            || completion != OpenRouterPremiumProfile.CompletionUsdPerToken)
            throw new OpenRouterPremiumProductionException("metadata_price_mismatch");
        return (prompt, completion);
    }

    internal static void RequirePricingOverride(JsonElement pricing, string code)
    {
        if (!pricing.TryGetProperty("overrides", out JsonElement overrides)
            || overrides.ValueKind != JsonValueKind.Array || overrides.GetArrayLength() != 1)
            throw new OpenRouterPremiumProductionException(code);
        JsonElement value = overrides[0];
        if (value.ValueKind != JsonValueKind.Object) throw new OpenRouterPremiumProductionException(code);
        RequireAllowed(value, ["min_prompt_tokens", "prompt", "completion", "input_cache_read", "input_cache_write"], code);
        if (Int32(value, "min_prompt_tokens") != OpenRouterPremiumProfile.PricingOverrideMinimumPromptTokens
            || String(value, "prompt", 64) != OpenRouterPremiumProfile.PricingOverridePromptUsdPerToken
            || String(value, "completion", 64) != OpenRouterPremiumProfile.PricingOverrideCompletionUsdPerToken)
            throw new OpenRouterPremiumProductionException("metadata_price_mismatch");
        if (value.TryGetProperty("input_cache_read", out JsonElement cacheRead)
            && (cacheRead.ValueKind != JsonValueKind.String || (cacheRead.GetString()?.Length ?? 0) is < 1 or > 64)
            || value.TryGetProperty("input_cache_write", out JsonElement cacheWrite)
                && (cacheWrite.ValueKind != JsonValueKind.String || (cacheWrite.GetString()?.Length ?? 0) is < 1 or > 64))
            throw new OpenRouterPremiumProductionException(code);
    }

    internal static void RequireParameters(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Array || parameters.GetArrayLength() is < 4 or > 64)
            throw new OpenRouterPremiumProductionException("metadata_parameters_invalid");
        HashSet<string> values = new(parameters.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new OpenRouterPremiumProductionException("metadata_parameters_invalid")), StringComparer.Ordinal);
        if (!values.Contains("max_completion_tokens") || !values.Contains("reasoning")
            || !values.Contains("response_format") || !values.Contains("structured_outputs"))
            throw new OpenRouterPremiumProductionException("metadata_parameters_invalid");
    }
}
