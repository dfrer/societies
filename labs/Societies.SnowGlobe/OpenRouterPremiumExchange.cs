using System.Buffers;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParserRejection = Societies.SnowGlobe.OpenRouterPremiumResponseParserRejectionCode;

namespace Societies.SnowGlobe;

public sealed class OpenRouterPremiumExchangeRequest : IDisposable
{
    private readonly byte[] _promptUtf8;
    private readonly byte[] _canonicalRequestUtf8;
    private int _disposed;
    private int _canonicalConsumed;

    private OpenRouterPremiumExchangeRequest(
        string profileDigestSha256,
        string capabilityDigestSha256,
        int slotIndex,
        string scenarioId,
        string observationDigestSha256,
        string promptDigestSha256,
        byte[] promptUtf8,
        byte[] canonicalRequestUtf8)
    {
        ProfileDigestSha256 = profileDigestSha256;
        CapabilityDigestSha256 = capabilityDigestSha256;
        SlotIndex = slotIndex;
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        PromptDigestSha256 = promptDigestSha256;
        _promptUtf8 = promptUtf8;
        _canonicalRequestUtf8 = canonicalRequestUtf8;
        RequestDigestSha256 = OpenRouterPremiumCanonical.Digest(canonicalRequestUtf8);
    }

    public string ProfileDigestSha256 { get; }
    public string CapabilityDigestSha256 { get; }
    public int SlotIndex { get; }
    public string ScenarioId { get; }
    public string ObservationDigestSha256 { get; }
    public string PromptDigestSha256 { get; }
    public int PromptByteCount => _promptUtf8.Length;
    public int CanonicalRequestByteCount => _canonicalRequestUtf8.Length;
    public string RequestDigestSha256 { get; }
    public ReadOnlyMemory<byte> PromptUtf8
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _promptUtf8.ToArray();
        }
    }
    internal ReadOnlySpan<byte> PromptSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _promptUtf8;
        }
    }
    internal byte[] ConsumeCanonicalRequestUtf8()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        byte[] copy = _canonicalRequestUtf8.ToArray();
        if (Interlocked.CompareExchange(ref _canonicalConsumed, 1, 0) != 0)
        {
            CryptographicOperations.ZeroMemory(copy);
            throw new OpenRouterPremiumEvidenceException("exchange_request_consumed");
        }
        CryptographicOperations.ZeroMemory(_canonicalRequestUtf8);
        CryptographicOperations.ZeroMemory(_promptUtf8);
        return copy;
    }

    internal static OpenRouterPremiumExchangeRequest CreateForProfile(
        OpenRouterPremiumProfile profile,
        CognitionQualityPromptEnvelopeSlot slot,
        string capabilityDigestSha256)
    {
        int index = int.Parse(slot.ScenarioId.AsSpan(2), CultureInfo.InvariantCulture);
        byte[] prompt = slot.PromptUtf8.ToArray();
        byte[]? canonical = null;
        try
        {
            canonical = OpenRouterPremiumCanonicalRequestSerializer.Serialize(profile, prompt);
            if (canonical.Length is < 1 || canonical.Length > profile.Bounds.MaximumRequestBytes)
                throw new OpenRouterPremiumEvidenceException("request_too_large");
            return new(profile.ProfileDigestSha256, capabilityDigestSha256, index, slot.ScenarioId,
                slot.ObservationDigestSha256, slot.PromptDigestSha256, prompt, canonical);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(prompt);
            if (canonical is not null) CryptographicOperations.ZeroMemory(canonical);
            throw;
        }
    }

    internal void ValidateForProfile(OpenRouterPremiumProfile profile)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!string.Equals(ProfileDigestSha256, profile.ProfileDigestSha256, StringComparison.Ordinal)
            || SlotIndex is < 1 or > 12 || !string.Equals(ScenarioId, $"cq{SlotIndex}", StringComparison.Ordinal)
            || !OpenRouterPremiumCanonical.IsDigest(CapabilityDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(ObservationDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(PromptDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(RequestDigestSha256)
            || PromptByteCount is < 1 or > CognitionQualityPromptEnvelopeBuilderModule.MaximumPromptBytes
            || CanonicalRequestByteCount is < 1 || CanonicalRequestByteCount > profile.Bounds.MaximumRequestBytes
            || Volatile.Read(ref _canonicalConsumed) != 0
            || !string.Equals(OpenRouterPremiumCanonical.Digest(_promptUtf8), PromptDigestSha256, StringComparison.Ordinal)
            || !string.Equals(OpenRouterPremiumCanonical.Digest(_canonicalRequestUtf8), RequestDigestSha256, StringComparison.Ordinal))
            throw new OpenRouterPremiumEvidenceException("exchange_request_binding_invalid");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        CryptographicOperations.ZeroMemory(_promptUtf8);
        CryptographicOperations.ZeroMemory(_canonicalRequestUtf8);
    }
}

internal static class OpenRouterPremiumCanonicalRequestSerializer
{
    internal static byte[] Serialize(OpenRouterPremiumProfile profile, ReadOnlySpan<byte> promptUtf8)
    {
        try { _ = new UTF8Encoding(false, true).GetCharCount(promptUtf8); }
        catch (DecoderFallbackException) { throw new OpenRouterPremiumEvidenceException("prompt_utf8_invalid"); }
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("model", OpenRouterPremiumProfile.CanonicalModelSlug);
            writer.WritePropertyName("messages"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("role", "user"); writer.WriteString("content", promptUtf8); writer.WriteEndObject(); writer.WriteEndArray();
            writer.WriteNumber("max_completion_tokens", profile.Bounds.MaximumOutputTokens);
            writer.WritePropertyName("reasoning"); writer.WriteStartObject();
            writer.WriteString("effort", "minimal"); writer.WriteBoolean("exclude", true); writer.WriteEndObject();
            writer.WriteBoolean("stream", false);
            writer.WritePropertyName("response_format"); writer.WriteStartObject(); writer.WriteString("type", "json_schema");
            writer.WritePropertyName("json_schema"); writer.WriteStartObject(); writer.WriteString("name", "snow_globe_action_proposal");
            writer.WriteBoolean("strict", true); writer.WritePropertyName("schema"); WriteProposalSchema(writer);
            writer.WriteEndObject(); writer.WriteEndObject();
            writer.WritePropertyName("provider"); writer.WriteStartObject();
            writer.WritePropertyName("order"); writer.WriteStartArray(); writer.WriteStringValue(OpenRouterPremiumProfile.ProviderSlug); writer.WriteEndArray();
            writer.WritePropertyName("only"); writer.WriteStartArray(); writer.WriteStringValue(OpenRouterPremiumProfile.ProviderSlug); writer.WriteEndArray();
            writer.WriteBoolean("allow_fallbacks", false); writer.WriteBoolean("require_parameters", true);
            writer.WriteString("data_collection", "deny"); writer.WriteBoolean("zdr", true);
            writer.WriteString("sort", "price"); writer.WritePropertyName("max_price"); writer.WriteStartObject();
            writer.WriteNumber("prompt", OpenRouterPremiumProfile.ProviderMaxPromptUsdPerMillionTokens);
            writer.WriteNumber("completion", OpenRouterPremiumProfile.ProviderMaxCompletionUsdPerMillionTokens);
            writer.WriteEndObject(); writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void WriteProposalSchema(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("type", "object"); writer.WritePropertyName("properties"); writer.WriteStartObject();
        writer.WritePropertyName("agent_id"); writer.WriteStartObject(); writer.WriteString("type", "string"); writer.WriteEndObject();
        writer.WritePropertyName("action"); writer.WriteStartObject(); writer.WriteString("type", "string"); writer.WritePropertyName("enum"); writer.WriteStartArray();
        foreach (string action in new[] { "Idle", "GatherWood", "GatherStone", "BuildShelter", "BuildStorage", "MaintainShelter" }) writer.WriteStringValue(action);
        writer.WriteEndArray(); writer.WriteEndObject();
        writer.WritePropertyName("quantity"); writer.WriteStartObject(); writer.WriteString("type", "integer"); writer.WriteNumber("minimum", 0); writer.WriteNumber("maximum", 64); writer.WriteEndObject();
        writer.WriteEndObject(); writer.WritePropertyName("required"); writer.WriteStartArray(); writer.WriteStringValue("agent_id"); writer.WriteStringValue("action"); writer.WriteStringValue("quantity"); writer.WriteEndArray();
        writer.WriteBoolean("additionalProperties", false); writer.WriteEndObject();
    }
}

public sealed class OpenRouterPremiumExchangeResponse : IDisposable
{
    private readonly byte[] _body;
    private int _disposed;

    private OpenRouterPremiumExchangeResponse(int statusCode, string effectiveUri, int responseHeaderBytes, byte[] ownedBody, bool exchangeReachedServer)
    {
        StatusCode = statusCode;
        EffectiveUri = effectiveUri;
        ResponseHeaderBytes = responseHeaderBytes;
        _body = ownedBody;
        ExchangeReachedServer = exchangeReachedServer;
    }

    public int StatusCode { get; }
    public string EffectiveUri { get; }
    public int ResponseHeaderBytes { get; }
    public int ResponseByteCount => _body.Length;
    public bool ExchangeReachedServer { get; }
    internal ReadOnlySpan<byte> BodySpan
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(OpenRouterPremiumExchangeResponse));
            return _body;
        }
    }

    public static OpenRouterPremiumExchangeResponse Received(int statusCode, string effectiveUri, int responseHeaderBytes, byte[] ownedBody)
    {
        ArgumentNullException.ThrowIfNull(ownedBody);
        if (statusCode is < 100 or > 599 || responseHeaderBytes < 0) throw new ArgumentOutOfRangeException(nameof(statusCode));
        return new(statusCode, effectiveUri, responseHeaderBytes, ownedBody, true);
    }

    public static OpenRouterPremiumExchangeResponse SubmissionUnknown(string effectiveUri) =>
        new(0, effectiveUri, 0, [], true);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) CryptographicOperations.ZeroMemory(_body);
    }
}

/// <summary>True-external exchange port. Exactly one request serialization and exchange is permitted per call.</summary>
public interface IOpenRouterPremiumExchange
{
    string Identity { get; }
    string ContractDigestSha256 { get; }
    ValueTask<OpenRouterPremiumExchangeResponse> ExchangeOnceAsync(
        OpenRouterPremiumExchangeRequest request,
        ReadOnlyMemory<byte> bearerCredential,
        CancellationToken cancellationToken);
}

public sealed class ScriptedOpenRouterPremiumExchange : IOpenRouterPremiumExchange
{
    public const string AdapterIdentity = "openrouter-premium-scripted-offline/v1";
    public static readonly string AdapterContractDigestSha256 = OpenRouterPremiumCanonical.Digest(
        "openrouter-premium-scripted-offline-contract/v1|registered-sealed-type|one-call|sequential|shared-canonical-request|exact-request-byte-digest|consume-and-zero-request|bounded-response|raw-free-errors|no-network");
    private readonly int? _failingSlot;
    private int _calls;
    private int _active;
    private int _maximumActive;
    private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _firstCallRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ScriptedOpenRouterPremiumExchange(int? failingSlot)
    {
        if (failingSlot is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(failingSlot));
        _failingSlot = failingSlot;
    }

    public string Identity => AdapterIdentity;
    public string ContractDigestSha256 => AdapterContractDigestSha256;
    public int CallCount => Volatile.Read(ref _calls);
    public int MaximumConcurrentCalls => Volatile.Read(ref _maximumActive);
    public bool PauseFirstCall { get; set; }
    public Task FirstCallStarted => _firstCallStarted.Task;
    public static ScriptedOpenRouterPremiumExchange CreateSuccessful(int? failingSlot = null) => new(failingSlot);
    public void ReleaseFirstCall() => _firstCallRelease.TrySetResult();

    public async ValueTask<OpenRouterPremiumExchangeResponse> ExchangeOnceAsync(
        OpenRouterPremiumExchangeRequest request,
        ReadOnlyMemory<byte> bearerCredential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        byte[]? canonicalRequest = null;
        int active = Interlocked.Increment(ref _active);
        UpdateMaximum(active);
        try
        {
            request.ValidateForProfile(OpenRouterPremiumProfileRegistry.Selected);
            canonicalRequest = request.ConsumeCanonicalRequestUtf8();
            if (bearerCredential.IsEmpty) throw new OpenRouterPremiumEvidenceException("credential_invalid");
            int call = Interlocked.Increment(ref _calls);
            if (call != request.SlotIndex || !string.Equals(request.ScenarioId, $"cq{call}", StringComparison.Ordinal))
                throw new OpenRouterPremiumEvidenceException("exchange_sequence_invalid");
            if (call == 1)
            {
                _firstCallStarted.TrySetResult();
                if (PauseFirstCall) await _firstCallRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            if (_failingSlot == call)
                return OpenRouterPremiumExchangeResponse.Received(503, OpenRouterPremiumProfile.EffectiveUri, 64,
                    Encoding.UTF8.GetBytes("{\"error\":{\"code\":503,\"message\":\"offline scripted terminal\"}}"));
            return OpenRouterPremiumExchangeResponse.Received(200, OpenRouterPremiumProfile.EffectiveUri, 128,
                WriteSuccess(request.ScenarioId));
        }
        finally
        {
            if (canonicalRequest is not null) CryptographicOperations.ZeroMemory(canonicalRequest);
            Interlocked.Decrement(ref _active);
        }
    }

    private void UpdateMaximum(int active)
    {
        int current;
        while (active > (current = Volatile.Read(ref _maximumActive))
            && Interlocked.CompareExchange(ref _maximumActive, active, current) != current) { }
    }

    private static byte[] WriteSuccess(string scenarioId)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", "gen-offline-" + scenarioId);
            writer.WriteString("object", "chat.completion");
            writer.WriteNumber("created", 1);
            writer.WriteString("model", OpenRouterPremiumProfile.CanonicalModelSlug);
            writer.WritePropertyName("choices"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteNumber("index", 0); writer.WriteString("finish_reason", "stop");
            writer.WritePropertyName("message"); writer.WriteStartObject(); writer.WriteString("role", "assistant");
            writer.WriteString("content", "{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":12}");
            writer.WriteEndObject(); writer.WriteEndObject(); writer.WriteEndArray();
            writer.WritePropertyName("usage"); writer.WriteStartObject();
            writer.WriteNumber("prompt_tokens", 100); writer.WriteNumber("completion_tokens", 20);
            writer.WriteNumber("total_tokens", 120); writer.WriteNumber("cost", 0.000044m); writer.WriteEndObject();
            writer.WritePropertyName("openrouter_metadata"); writer.WriteStartObject();
            writer.WriteString("requested", OpenRouterPremiumProfile.CanonicalModelSlug); writer.WriteString("strategy", "direct");
            writer.WriteNumber("attempt", 1); writer.WriteBoolean("is_byok", false);
            writer.WritePropertyName("endpoints"); writer.WriteStartObject(); writer.WriteNumber("total", 1);
            writer.WritePropertyName("available"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("provider", OpenRouterPremiumProfile.ProviderResponseIdentity);
            writer.WriteString("model", OpenRouterPremiumProfile.CanonicalModelSlug); writer.WriteBoolean("selected", true);
            writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
            writer.WritePropertyName("attempts"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("provider", OpenRouterPremiumProfile.ProviderResponseIdentity);
            writer.WriteString("model", OpenRouterPremiumProfile.CanonicalModelSlug); writer.WriteNumber("status", 200);
            writer.WriteEndObject(); writer.WriteEndArray();
            writer.WritePropertyName("pipeline"); writer.WriteStartArray(); writer.WriteEndArray();
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}

internal enum OpenRouterPremiumExchangeKind { ScriptedOffline, ProductionHttp }

internal sealed record OpenRouterPremiumExchangeRegistration(
    string Identity,
    string ContractDigestSha256,
    OpenRouterPremiumExchangeKind Kind);

internal static class OpenRouterPremiumExchangeRegistry
{
    internal static OpenRouterPremiumExchangeRegistration Resolve(IOpenRouterPremiumExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        Type exactType = exchange.GetType();
        if (exactType == typeof(ScriptedOpenRouterPremiumExchange))
            return new(ScriptedOpenRouterPremiumExchange.AdapterIdentity,
                ScriptedOpenRouterPremiumExchange.AdapterContractDigestSha256,
                OpenRouterPremiumExchangeKind.ScriptedOffline);
        if (exactType == typeof(OpenRouterPremiumHttpExchange))
            return new(OpenRouterPremiumHttpExchange.AdapterIdentity,
                OpenRouterPremiumHttpExchange.AdapterContractDigestSha256,
                OpenRouterPremiumExchangeKind.ProductionHttp);
        throw new OpenRouterPremiumEvidenceException("exchange_not_registered");
    }
}

/// <summary>
/// Managed HTTP necessarily creates framework-owned bearer header material that cannot be globally
/// proven erased. This Adapter only claims zeroing of its lease-owned and temporary mutable buffers.
/// </summary>
public sealed class OpenRouterPremiumHttpExchange : IOpenRouterPremiumExchange, IDisposable
{
    public const string AdapterIdentity = "openrouter-premium-http/openrouter-chat-completions/v1";
    public static readonly string AdapterContractDigestSha256 = OpenRouterPremiumCanonical.Digest(
        "openrouter-premium-http-contract/v1|registered-sealed-type|post-exact-uri|exact-api-model-id|bearer|shared-canonical-request|exact-request-byte-digest|one-serialization|one-exchange|consume-and-zero-request|response-headers-read|strict-utf8|raw-free-errors|hardened_policy_sha256="
        + OpenRouterPremiumHardenedHttp.PolicyDigestSha256);
    internal static string HardenedHttpPolicyDigestSha256 => OpenRouterPremiumHardenedHttp.PolicyDigestSha256;
    private readonly HttpClient _client;
    private readonly Action<bool>? _requestCopyZeroObserver;
    private int _serializations;
    private bool _disposed;

    private OpenRouterPremiumHttpExchange(HttpMessageHandler handler, Action<bool>? requestCopyZeroObserver = null)
    {
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _requestCopyZeroObserver = requestCopyZeroObserver;
    }

    public string Identity => AdapterIdentity;
    public string ContractDigestSha256 => AdapterContractDigestSha256;
    public bool RedirectsAllowed => false;
    public bool AutomaticRetriesAllowed => false;
    public bool ProxyAllowed => false;
    public bool CookiesAllowed => false;
    public bool AmbientAuthenticationAllowed => false;
    public bool AutomaticDecompressionAllowed => false;
    public int SerializationCount => Volatile.Read(ref _serializations);

    public static OpenRouterPremiumHttpExchange CreateProduction()
        => new(OpenRouterPremiumHardenedHttp.CreateSocketsHandler());

    internal static OpenRouterPremiumHttpExchange CreateForOfflineTests(
        HttpMessageHandler handler,
        Action<bool>? requestCopyZeroObserver = null) => new(handler, requestCopyZeroObserver);

    public async ValueTask<OpenRouterPremiumExchangeResponse> ExchangeOnceAsync(
        OpenRouterPremiumExchangeRequest request,
        ReadOnlyMemory<byte> bearerCredential,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        request.ValidateForProfile(profile);
        byte[]? body = null;
        char[]? credentialChars = null;
        string? credentialString = null;
        try
        {
            body = request.ConsumeCanonicalRequestUtf8();
            Interlocked.Increment(ref _serializations);
            credentialChars = DecodeCredential(bearerCredential.Span);
            credentialString = new string(credentialChars); // Framework/header-owned copies are the documented residual.
            using HttpRequestMessage message = new(HttpMethod.Post, OpenRouterPremiumProfile.EffectiveUri);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentialString);
            message.Headers.TryAddWithoutValidation("X-OpenRouter-Metadata", "enabled");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Content = new ByteArrayContent(body);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { return OpenRouterPremiumExchangeResponse.SubmissionUnknown(OpenRouterPremiumProfile.EffectiveUri); }
            catch (HttpRequestException)
            { return OpenRouterPremiumExchangeResponse.SubmissionUnknown(OpenRouterPremiumProfile.EffectiveUri); }

            using (response)
            {
                string effective = response.RequestMessage?.RequestUri?.AbsoluteUri ?? string.Empty;
                int headerBytes = CountHeaders(response, profile.Bounds.MaximumResponseHeaderBytes);
                if (!string.Equals(effective, OpenRouterPremiumProfile.EffectiveUri, StringComparison.Ordinal))
                    throw new OpenRouterPremiumEvidenceException("effective_uri_mismatch");
                if (response.Content.Headers.ContentEncoding.Count != 0)
                    throw new OpenRouterPremiumEvidenceException("response_encoding_forbidden");
                if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                    throw new OpenRouterPremiumEvidenceException("response_content_type_invalid");
                string? charset = response.Content.Headers.ContentType?.CharSet;
                if (charset is not null && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
                    throw new OpenRouterPremiumEvidenceException("response_content_type_invalid");
                if (response.Content.Headers.ContentLength is long length && length > profile.Bounds.MaximumResponseBytes)
                    throw new OpenRouterPremiumEvidenceException("response_too_large");
                byte[] responseBody = await OpenRouterPremiumHardenedHttp.ReadBoundedAsync(
                    response.Content, profile.Bounds.MaximumResponseBytes,
                    static () => new OpenRouterPremiumEvidenceException("response_too_large"), cancellationToken).ConfigureAwait(false);
                return OpenRouterPremiumExchangeResponse.Received((int)response.StatusCode, effective, headerBytes, responseBody);
            }
        }
        finally
        {
            if (credentialChars is not null) Array.Clear(credentialChars);
            if (body is not null)
            {
                CryptographicOperations.ZeroMemory(body);
                _requestCopyZeroObserver?.Invoke(body.All(value => value == 0));
            }
            credentialString = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }

    private static char[] DecodeCredential(ReadOnlySpan<byte> credential)
    {
        if (credential.Length is < 1 or > 512) throw new OpenRouterPremiumEvidenceException("credential_invalid");
        char[] chars = new char[credential.Length];
        for (int index = 0; index < credential.Length; index++)
        {
            byte value = credential[index];
            if (value is < 0x21 or > 0x7e || value is (byte)'\r' or (byte)'\n')
            {
                Array.Clear(chars);
                throw new OpenRouterPremiumEvidenceException("credential_invalid");
            }
            chars[index] = (char)value;
        }
        return chars;
    }

    private static int CountHeaders(HttpResponseMessage response, int maximum)
    {
        int total = 0;
        foreach ((string name, IEnumerable<string> values) in response.Headers.Concat(response.Content.Headers))
        {
            total = checked(total + name.Length);
            foreach (string value in values) total = checked(total + value.Length);
            if (total > maximum) throw new OpenRouterPremiumEvidenceException("response_headers_too_large");
        }
        return total;
    }

}

// These identifiers are the wire suffixes. Every diagnosable parser rejection is constructed from this type.
internal enum OpenRouterPremiumResponseParserRejectionCode
{
    proposal_binding_invalid,
    proposal_content_invalid,
    proposal_shape_invalid,
    proposal_unknown_property,
    response_array_too_large,
    response_binding_invalid,
    response_choices_invalid,
    response_choice_error_present,
    response_choice_index_invalid,
    response_choice_unknown_property,
    response_cost_invalid,
    response_created_invalid,
    response_error_invalid,
    response_error_metadata_unknown_property,
    response_error_unknown_property,
    response_finish_reason_missing,
    response_finish_reason_not_stop,
    response_finish_reason_type_invalid,
    response_json_duplicate_property,
    response_json_invalid,
    response_json_token_limit,
    response_json_too_deep,
    // Historical-only: retained so immutable evidence and raw-free diagnostics remain readable.
    response_json_unknown_property,
    response_logprobs_non_null,
    response_message_invalid,
    response_message_unknown_property,
    // Historical-only: retained so immutable evidence and raw-free diagnostics remain readable.
    response_native_finish_reason_not_stop,
    response_native_finish_reason_type_invalid,
    response_number_invalid,
    response_pipeline_forbidden,
    response_reasoning_detail_unknown_property,
    response_reasoning_invalid,
    response_refusal_non_null,
    response_root_unknown_property,
    response_routing_attempt_unknown_property,
    response_routing_candidate_unknown_property,
    response_routing_endpoints_unknown_property,
    response_routing_invalid,
    response_shape_invalid,
    response_string_too_long,
    response_too_large,
    response_usage_completion_tokens_details_unknown_property,
    response_usage_cost_details_unknown_property,
    response_usage_invalid,
    response_usage_prompt_tokens_details_unknown_property,
    response_usage_server_tool_use_details_unknown_property,
    response_usage_unknown_property,
    response_utf8_invalid
}

internal enum OpenRouterPremiumProviderErrorOutcomeCode
{
    provider_error_code_400_terminal,
    provider_error_code_401_terminal,
    provider_error_code_402_terminal,
    provider_error_code_403_terminal,
    provider_error_code_404_terminal,
    provider_error_code_408_terminal,
    provider_error_code_413_terminal,
    provider_error_code_422_terminal,
    provider_error_code_429_terminal,
    provider_error_code_500_terminal,
    provider_error_code_502_terminal,
    provider_error_code_503_terminal,
    provider_error_code_524_terminal,
    provider_error_code_529_terminal,
    provider_error_terminal
}

internal static class OpenRouterPremiumResponseParser
{
    private const string GenericRejectedOutcomeCode = "provider_response_rejected";
    private const string DiagnosticRejectedOutcomePrefix = GenericRejectedOutcomeCode + "_";
    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    { "id", "object", "created", "model", "provider", "choices", "usage", "openrouter_metadata", "system_fingerprint", "service_tier", "error" };

    private static OpenRouterPremiumEvidenceException Rejected(ParserRejection code) => new(code);

    internal static string ToRejectedOutcomeCode(OpenRouterPremiumEvidenceException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ParserRejectionCode is OpenRouterPremiumResponseParserRejectionCode parserRejectionCode
            ? DiagnosticRejectedOutcomePrefix + parserRejectionCode
            : GenericRejectedOutcomeCode;
    }

    internal static OpenRouterPremiumSlotReceipt Parse(ReadOnlySpan<byte> body, int statusCode, OpenRouterPremiumProfile profile, string scenarioId, string requestDigestSha256) =>
        Parse(body, statusCode, profile, int.Parse(scenarioId.AsSpan(2), CultureInfo.InvariantCulture), scenarioId,
            new string('0', 64), requestDigestSha256);

    internal static OpenRouterPremiumSlotReceipt Parse(
        ReadOnlySpan<byte> body,
        int statusCode,
        OpenRouterPremiumProfile profile,
        int slotIndex,
        string scenarioId,
        string promptDigestSha256,
        string requestDigestSha256)
    {
        if (body.Length > profile.Bounds.MaximumResponseBytes) throw Rejected(ParserRejection.response_too_large);
        ValidateLexical(body, profile.Bounds);
        string responseDigest = OpenRouterPremiumCanonical.Digest(body);
        using JsonDocument document = ParseDocument(body, profile.Bounds.MaximumJsonDepth);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Rejected(ParserRejection.response_shape_invalid);
        RejectUnknown(root, RootProperties, ParserRejection.response_root_unknown_property);
        ValidateOptionalNullableString(root, "system_fingerprint", 128);
        ValidateOptionalNullableString(root, "service_tier", 64);

        if (statusCode != 200)
        {
            ValidateError(root);
            return new(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256, responseDigest,
                SubmissionState.SubmissionUnknown, ChargeState.Unknown,
                0, 0, 0, 0, null, $"http_{statusCode}_terminal");
        }

        if (root.TryGetProperty("error", out _))
        {
            int providerErrorCode = ValidateError(root);
            return Unknown(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256, responseDigest,
                ClassifyProviderErrorOutcome(providerErrorCode).ToString());
        }
        RequireString(root, "object", "chat.completion");
        RequireString(root, "model", OpenRouterPremiumProfile.CanonicalModelSlug);
        if (root.TryGetProperty("provider", out JsonElement provider))
        {
            if (provider.ValueKind != JsonValueKind.String
                || provider.GetString() != OpenRouterPremiumProfile.ProviderResponseIdentity)
                throw Rejected(ParserRejection.response_binding_invalid);
        }
        _ = RequireBoundedString(root, "id", 128);
        if (!root.TryGetProperty("created", out JsonElement created) || !created.TryGetInt64(out long createdValue) || createdValue < 0)
            throw Rejected(ParserRejection.response_created_invalid);

        SnowGlobeActionProposal proposal = ParseChoice(root, profile, scenarioId);
        (int promptTokens, int completionTokens, int totalTokens, long settled) = ParseUsage(root, profile);
        ParseRouting(root, profile);
        return new(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256, responseDigest,
            SubmissionState.ResponseReceived, ChargeState.Settled, promptTokens, completionTokens, totalTokens,
            settled, proposal, "premium_evidence_success");
    }

    internal static OpenRouterPremiumSlotReceipt Unknown(int slotIndex, string scenarioId, string promptDigest, string requestDigest, string responseDigest, string outcome) =>
        new(slotIndex, scenarioId, promptDigest, requestDigest, responseDigest, SubmissionState.SubmissionUnknown,
            ChargeState.Unknown, 0, 0, 0, 0, null, outcome);

    private static SnowGlobeActionProposal ParseChoice(JsonElement root, OpenRouterPremiumProfile profile, string scenarioId)
    {
        if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1)
            throw Rejected(ParserRejection.response_choices_invalid);
        JsonElement choice = choices[0];
        RejectUnknown(choice, new HashSet<string>(["index", "finish_reason", "native_finish_reason", "logprobs", "message", "error"], StringComparer.Ordinal), ParserRejection.response_choice_unknown_property);
        if (!choice.TryGetProperty("index", out JsonElement index) || !index.TryGetInt32(out int indexValue) || indexValue != 0)
            throw Rejected(ParserRejection.response_choice_index_invalid);
        if (!choice.TryGetProperty("finish_reason", out JsonElement finish))
            throw Rejected(ParserRejection.response_finish_reason_missing);
        if (finish.ValueKind != JsonValueKind.String)
            throw Rejected(ParserRejection.response_finish_reason_type_invalid);
        if (!string.Equals(finish.GetString(), "stop", StringComparison.Ordinal))
            throw Rejected(ParserRejection.response_finish_reason_not_stop);
        if (choice.TryGetProperty("error", out _))
            throw Rejected(ParserRejection.response_choice_error_present);
        if (choice.TryGetProperty("native_finish_reason", out JsonElement nativeFinish))
        {
            if (nativeFinish.ValueKind != JsonValueKind.String)
                throw Rejected(ParserRejection.response_native_finish_reason_type_invalid);
            if ((nativeFinish.GetString()?.Length ?? 0) > profile.Bounds.MaximumStringCharacters)
                throw Rejected(ParserRejection.response_string_too_long);
        }
        if (choice.TryGetProperty("logprobs", out JsonElement logprobs) && logprobs.ValueKind != JsonValueKind.Null)
            throw Rejected(ParserRejection.response_logprobs_non_null);
        if (!choice.TryGetProperty("message", out JsonElement message) || message.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_message_invalid);
        RejectUnknown(message, new HashSet<string>(["role", "content", "refusal", "reasoning", "reasoning_content", "reasoning_details"], StringComparer.Ordinal), ParserRejection.response_message_unknown_property);
        RequireString(message, "role", "assistant");
        if (message.TryGetProperty("refusal", out JsonElement refusal) && refusal.ValueKind != JsonValueKind.Null)
            throw Rejected(ParserRejection.response_refusal_non_null);
        ValidateOptionalNullableString(message, "reasoning", profile.Bounds.MaximumStringCharacters);
        ValidateOptionalNullableString(message, "reasoning_content", profile.Bounds.MaximumStringCharacters);
        ValidateReasoningDetails(message, profile.Bounds);
        string content = RequireBoundedString(message, "content", CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes);
        return ParseProposal(Encoding.UTF8.GetBytes(content), scenarioId);
    }

    private static void ValidateReasoningDetails(JsonElement message, OpenRouterPremiumBounds bounds)
    {
        if (!message.TryGetProperty("reasoning_details", out JsonElement details)) return;
        if (details.ValueKind != JsonValueKind.Array || details.GetArrayLength() > bounds.MaximumArrayItems)
            throw Rejected(ParserRejection.response_reasoning_invalid);
        int expectedIndex = 0;
        foreach (JsonElement detail in details.EnumerateArray())
        {
            if (detail.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_reasoning_invalid);
            RejectUnknown(detail, new HashSet<string>(["type", "summary", "data", "text", "signature", "id", "format", "index"], StringComparer.Ordinal),
                ParserRejection.response_reasoning_detail_unknown_property);
            string type = RequireBoundedString(detail, "type", 32);
            string format = RequireBoundedString(detail, "format", 64);
            if (format is not ("unknown" or "openai-responses-v1" or "azure-openai-responses-v1"
                or "bedrock-openai-responses-v1" or "xai-responses-v1" or "meta-responses-v1"
                or "anthropic-claude-v1" or "google-gemini-v1"))
                throw Rejected(ParserRejection.response_reasoning_invalid);
            ValidateOptionalNullableString(detail, "id", 128);
            if (detail.TryGetProperty("index", out JsonElement index)
                && (!index.TryGetInt32(out int indexValue) || indexValue != expectedIndex))
                throw Rejected(ParserRejection.response_reasoning_invalid);
            switch (type)
            {
                case "reasoning.summary":
                    _ = RequireBoundedString(detail, "summary", bounds.MaximumStringCharacters);
                    if (detail.TryGetProperty("data", out _) || detail.TryGetProperty("text", out _)
                        || detail.TryGetProperty("signature", out _))
                        throw Rejected(ParserRejection.response_reasoning_invalid);
                    break;
                case "reasoning.encrypted":
                    _ = RequireBoundedString(detail, "data", bounds.MaximumStringCharacters);
                    if (detail.TryGetProperty("summary", out _) || detail.TryGetProperty("text", out _)
                        || detail.TryGetProperty("signature", out _))
                        throw Rejected(ParserRejection.response_reasoning_invalid);
                    break;
                case "reasoning.text":
                    _ = RequireBoundedString(detail, "text", bounds.MaximumStringCharacters);
                    ValidateOptionalNullableString(detail, "signature", 512);
                    if (detail.TryGetProperty("summary", out _) || detail.TryGetProperty("data", out _))
                        throw Rejected(ParserRejection.response_reasoning_invalid);
                    break;
                default:
                    throw Rejected(ParserRejection.response_reasoning_invalid);
            }
            expectedIndex++;
        }
    }

    private static SnowGlobeActionProposal ParseProposal(ReadOnlySpan<byte> content, string scenarioId)
    {
        ValidateLexical(content, OpenRouterPremiumProfileRegistry.Selected.Bounds with { MaximumJsonDepth = 3, MaximumJsonTokens = 16, MaximumArrayItems = 4 });
        using JsonDocument document = ParseDocument(content, 3);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Rejected(ParserRejection.proposal_shape_invalid);
        RejectUnknown(root, new HashSet<string>(["agent_id", "action", "quantity"], StringComparer.Ordinal), ParserRejection.proposal_unknown_property);
        if (root.EnumerateObject().Count() != 3) throw Rejected(ParserRejection.proposal_shape_invalid);
        string agent = RequireBoundedString(root, "agent_id", 64);
        CognitionQualityScenario scenario = CognitionQualityCorpusV1.CreateSnapshot().Scenarios.Single(value => value.ScenarioId == scenarioId);
        if (!string.Equals(agent, scenario.Observation.AgentId, StringComparison.Ordinal))
            throw Rejected(ParserRejection.proposal_binding_invalid);
        string actionName = RequireBoundedString(root, "action", 32);
        if (!SnowGlobeRunStore.TryParseCanonicalAction(actionName, out SnowGlobeActionKind action)
            || !root.TryGetProperty("quantity", out JsonElement quantity) || !quantity.TryGetInt32(out int amount))
            throw Rejected(ParserRejection.proposal_content_invalid);
        bool shape = action switch
        {
            SnowGlobeActionKind.Idle or SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage => amount == 0,
            SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone or SnowGlobeActionKind.MaintainShelter => amount is >= 1 and <= 64,
            _ => false
        };
        if (!shape) throw Rejected(ParserRejection.proposal_content_invalid);
        CognitionQualityProposalResponseParseResult shared = CognitionQualityProposalResponseContract.Parse(content);
        if (shared.Outcome != "proposal_parsed" || shared.Proposal is not { } proposal
            || proposal.AgentId != agent || proposal.Action != action || proposal.Quantity != amount)
            throw Rejected(ParserRejection.proposal_content_invalid);
        return proposal;
    }

    private static (int Prompt, int Completion, int Total, long Cost) ParseUsage(JsonElement root, OpenRouterPremiumProfile profile)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage) || usage.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_usage_invalid);
        RejectUnknown(usage, new HashSet<string>(["prompt_tokens", "completion_tokens", "total_tokens", "cost", "is_byok", "prompt_tokens_details", "completion_tokens_details", "server_tool_use_details", "cost_details"], StringComparer.Ordinal), ParserRejection.response_usage_unknown_property);
        int prompt = RequireInteger(usage, "prompt_tokens", 0, profile.Bounds.MaximumInputTokens);
        int completion = RequireInteger(usage, "completion_tokens", 0, profile.Bounds.MaximumOutputTokens);
        int total = RequireInteger(usage, "total_tokens", 0, profile.Bounds.MaximumInputTokens + profile.Bounds.MaximumOutputTokens);
        if (total != prompt + completion || !usage.TryGetProperty("cost", out JsonElement costElement)
            || costElement.ValueKind != JsonValueKind.Number || !costElement.TryGetDecimal(out decimal cost) || cost < 0)
            throw Rejected(ParserRejection.response_usage_invalid);
        if (usage.TryGetProperty("is_byok", out JsonElement isByok)
            && (isByok.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || isByok.GetBoolean()))
            throw Rejected(ParserRejection.response_usage_invalid);
        ValidateNameAdditiveOptionalIntegerObject(
            usage, "prompt_tokens_details", profile.Bounds.MaximumInputTokens);
        ValidateNameAdditiveOptionalIntegerObject(
            usage, "completion_tokens_details", profile.Bounds.MaximumOutputTokens);
        ValidateOptionalZeroIntegerObject(usage, "server_tool_use_details",
            ["tool_calls_executed", "tool_calls_requested"], profile.Bounds.MaximumArrayItems,
            ParserRejection.response_usage_server_tool_use_details_unknown_property);
        if (usage.TryGetProperty("cost_details", out JsonElement costDetails))
        {
            if (costDetails.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_usage_invalid);
            RejectUnknown(costDetails, new HashSet<string>([
                "upstream_inference_cost", "upstream_inference_prompt_cost", "upstream_inference_completions_cost"
            ], StringComparer.Ordinal), ParserRejection.response_usage_cost_details_unknown_property);
            decimal maximumDetailCost = Math.Min(cost, profile.Bounds.PerSlotCostCeilingMicrousd / 1_000_000m);
            decimal? upstream = ReadOptionalCostDetail(costDetails, "upstream_inference_cost", maximumDetailCost);
            decimal? upstreamPrompt = ReadOptionalCostDetail(costDetails, "upstream_inference_prompt_cost", maximumDetailCost);
            decimal? upstreamCompletion = ReadOptionalCostDetail(costDetails, "upstream_inference_completions_cost", maximumDetailCost);
            if (upstreamPrompt.HasValue && upstreamCompletion.HasValue
                && upstreamPrompt.Value > maximumDetailCost - upstreamCompletion.Value)
                throw Rejected(ParserRejection.response_usage_invalid);
            decimal componentCost = upstreamPrompt.GetValueOrDefault() + upstreamCompletion.GetValueOrDefault();
            if (upstream.HasValue && (upstreamPrompt.HasValue || upstreamCompletion.HasValue)
                && componentCost > upstream.Value)
                throw Rejected(ParserRejection.response_usage_invalid);
        }
        decimal microusdDecimal = cost * 1_000_000m;
        if (microusdDecimal > long.MaxValue) throw Rejected(ParserRejection.response_cost_invalid);
        long microusd = checked((long)decimal.Ceiling(microusdDecimal));
        long catalogMaximum = checked((long)decimal.Ceiling(
            ((decimal)prompt * OpenRouterPremiumProfile.PromptMicrousdPerMillionTokens + (decimal)completion * OpenRouterPremiumProfile.CompletionMicrousdPerMillionTokens) / 1_000_000m));
        if (microusd > catalogMaximum || microusd > profile.Bounds.PerSlotCostCeilingMicrousd)
            throw Rejected(ParserRejection.response_cost_invalid);
        return (prompt, completion, total, microusd);
    }

    private static void ParseRouting(JsonElement root, OpenRouterPremiumProfile profile)
    {
        if (!root.TryGetProperty("openrouter_metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_routing_invalid);
        ValidateOptionalNullableString(metadata, "region", 64);
        ValidateOptionalNullableString(metadata, "summary", 512);
        if (metadata.TryGetProperty("params", out JsonElement parameters) && parameters.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_routing_invalid);
        RequireString(metadata, "requested", OpenRouterPremiumProfile.CanonicalModelSlug); RequireString(metadata, "strategy", "direct");
        if (!metadata.TryGetProperty("attempt", out JsonElement attempt) || !attempt.TryGetInt32(out int attemptValue) || attemptValue != 1
            || !metadata.TryGetProperty("is_byok", out JsonElement byok) || byok.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || byok.GetBoolean())
            throw Rejected(ParserRejection.response_routing_invalid);
        if (!metadata.TryGetProperty("endpoints", out JsonElement endpoints) || endpoints.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_routing_invalid);
        RejectUnknown(endpoints, new HashSet<string>(["total", "available"], StringComparer.Ordinal), ParserRejection.response_routing_endpoints_unknown_property);
        int total = RequireInteger(endpoints, "total", 1, profile.Bounds.MaximumArrayItems);
        if (!endpoints.TryGetProperty("available", out JsonElement available)
            || available.ValueKind != JsonValueKind.Array || available.GetArrayLength() is < 1
            || available.GetArrayLength() > profile.Bounds.MaximumArrayItems || total < available.GetArrayLength())
            throw Rejected(ParserRejection.response_routing_invalid);
        int selectedCount = 0;
        foreach (JsonElement candidate in available.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_routing_invalid);
            RejectUnknown(candidate, new HashSet<string>(["provider", "model", "selected"], StringComparer.Ordinal), ParserRejection.response_routing_candidate_unknown_property);
            string candidateProvider = RequireBoundedString(candidate, "provider", 128);
            string candidateModel = RequireBoundedString(candidate, "model", 256);
            if (!candidate.TryGetProperty("selected", out JsonElement selectedFlag)
                || selectedFlag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw Rejected(ParserRejection.response_routing_invalid);
            if (!selectedFlag.GetBoolean()) continue;
            selectedCount++;
            if (candidateProvider != OpenRouterPremiumProfile.ProviderResponseIdentity
                || candidateModel != OpenRouterPremiumProfile.CanonicalModelSlug)
                throw Rejected(ParserRejection.response_binding_invalid);
        }
        if (selectedCount != 1) throw Rejected(ParserRejection.response_routing_invalid);
        if (metadata.TryGetProperty("attempts", out JsonElement attempts))
        {
            if (attempts.ValueKind != JsonValueKind.Array || attempts.GetArrayLength() != 1)
                throw Rejected(ParserRejection.response_routing_invalid);
            JsonElement routedAttempt = attempts[0];
            RejectUnknown(routedAttempt, new HashSet<string>(["provider", "model", "status"], StringComparer.Ordinal), ParserRejection.response_routing_attempt_unknown_property);
            RequireString(routedAttempt, "provider", OpenRouterPremiumProfile.ProviderResponseIdentity);
            RequireString(routedAttempt, "model", OpenRouterPremiumProfile.CanonicalModelSlug);
            if (RequireInteger(routedAttempt, "status", 200, 200) != 200)
                throw Rejected(ParserRejection.response_routing_invalid);
        }
        if (metadata.TryGetProperty("pipeline", out JsonElement pipeline)
            && (pipeline.ValueKind != JsonValueKind.Array || pipeline.GetArrayLength() != 0))
            throw Rejected(ParserRejection.response_pipeline_forbidden);
    }

    private static void ValidateLexical(ReadOnlySpan<byte> body, OpenRouterPremiumBounds bounds)
    {
        try { _ = new UTF8Encoding(false, true).GetString(body); }
        catch (DecoderFallbackException) { throw Rejected(ParserRejection.response_utf8_invalid); }
        try
        {
            Utf8JsonReader reader = new(body, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = bounds.MaximumJsonDepth });
            Stack<HashSet<string>?> objects = new();
            Stack<int> arrays = new();
            int tokens = 0;
            while (reader.Read())
            {
                if (++tokens > bounds.MaximumJsonTokens) throw Rejected(ParserRejection.response_json_token_limit);
                if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new HashSet<string>(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string name = reader.GetString()!;
                    if (name.Length > 128) throw Rejected(ParserRejection.response_string_too_long);
                    HashSet<string>? names = objects.Peek();
                    if (names is null || !names.Add(name)) throw Rejected(ParserRejection.response_json_duplicate_property);
                }
                else if (reader.TokenType == JsonTokenType.StartArray) arrays.Push(0);
                else if (reader.TokenType == JsonTokenType.EndArray) arrays.Pop();
                else if (reader.TokenType == JsonTokenType.String)
                {
                    long encodedLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                    if (encodedLength > bounds.MaximumStringCharacters * 4L || (reader.GetString()?.Length ?? 0) > bounds.MaximumStringCharacters)
                        throw Rejected(ParserRejection.response_string_too_long);
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    ReadOnlySpan<byte> raw = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                    if (raw.Length > 32 || raw.Contains((byte)'e') || raw.Contains((byte)'E'))
                        throw Rejected(ParserRejection.response_number_invalid);
                }
                if (arrays.Count > 0 && reader.TokenType is not (JsonTokenType.EndArray or JsonTokenType.PropertyName))
                {
                    int count = arrays.Pop() + 1;
                    if (count > bounds.MaximumArrayItems) throw Rejected(ParserRejection.response_array_too_large);
                    arrays.Push(count);
                }
            }
        }
        catch (OpenRouterPremiumEvidenceException) { throw; }
        catch (JsonException exception) when (exception.Message.Contains("depth", StringComparison.OrdinalIgnoreCase))
        { throw Rejected(ParserRejection.response_json_too_deep); }
        catch (JsonException) { throw Rejected(ParserRejection.response_json_invalid); }
        catch (InvalidOperationException) { throw Rejected(ParserRejection.response_json_invalid); }
    }

    private static JsonDocument ParseDocument(ReadOnlySpan<byte> body, int maxDepth)
    {
        try { return JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = maxDepth }); }
        catch (JsonException) { throw Rejected(ParserRejection.response_json_invalid); }
    }

    private static void RejectUnknown(JsonElement element, HashSet<string> allowed, ParserRejection code)
    {
        foreach (JsonProperty property in element.EnumerateObject()) if (!allowed.Contains(property.Name)) throw Rejected(code);
    }

    private static int ValidateError(JsonElement root)
    {
        foreach (JsonProperty property in root.EnumerateObject())
            if (property.Name is not ("error" or "openrouter_metadata"))
                throw Rejected(ParserRejection.response_error_invalid);
        if (!root.TryGetProperty("error", out JsonElement error) || error.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_error_invalid);
        RejectUnknown(error, new HashSet<string>(["code", "message", "metadata"], StringComparer.Ordinal), ParserRejection.response_error_unknown_property);
        if (!error.TryGetProperty("code", out JsonElement code) || !code.TryGetInt32(out int codeValue) || codeValue is < 1 or > 999
            || !error.TryGetProperty("message", out JsonElement message) || message.ValueKind != JsonValueKind.String
            || (message.GetString()?.Length ?? 0) is < 1 or > 1024)
            throw Rejected(ParserRejection.response_error_invalid);
        if (error.TryGetProperty("metadata", out JsonElement metadata))
        {
            if (metadata.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_error_invalid);
            RejectUnknown(metadata, new HashSet<string>(["provider_name", "raw"], StringComparer.Ordinal), ParserRejection.response_error_metadata_unknown_property);
            ValidateOptionalNullableString(metadata, "provider_name", 128);
            ValidateOptionalNullableString(metadata, "raw", OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumStringCharacters);
        }
        if (root.TryGetProperty("openrouter_metadata", out JsonElement routing))
        {
            if (routing.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_error_invalid);
            if (routing.TryGetProperty("requested", out JsonElement requested)
                && (requested.ValueKind != JsonValueKind.String
                    || requested.GetString() != OpenRouterPremiumProfile.CanonicalModelSlug))
                throw Rejected(ParserRejection.response_binding_invalid);
            if (routing.TryGetProperty("attempt", out JsonElement attempt)
                && (!attempt.TryGetInt32(out int attemptValue) || attemptValue is < 0 or > 1))
                throw Rejected(ParserRejection.response_error_invalid);
            if (routing.TryGetProperty("params", out JsonElement parameters) && parameters.ValueKind != JsonValueKind.Object)
                throw Rejected(ParserRejection.response_error_invalid);
        }
        return codeValue;
    }

    private static OpenRouterPremiumProviderErrorOutcomeCode ClassifyProviderErrorOutcome(int code) => code switch
    {
        400 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_400_terminal,
        401 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_401_terminal,
        402 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_402_terminal,
        403 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_403_terminal,
        404 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_404_terminal,
        408 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_408_terminal,
        413 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_413_terminal,
        422 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_422_terminal,
        429 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_429_terminal,
        500 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_500_terminal,
        502 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_502_terminal,
        503 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_503_terminal,
        524 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_524_terminal,
        529 => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_code_529_terminal,
        _ => OpenRouterPremiumProviderErrorOutcomeCode.provider_error_terminal
    };

    private static void ValidateOptionalIntegerObject(
        JsonElement parent,
        string property,
        string[] allowed,
        int maximum,
        ParserRejection unknownPropertyCode)
    {
        if (!parent.TryGetProperty(property, out JsonElement details)) return;
        if (details.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_usage_invalid);
        RejectUnknown(details, new HashSet<string>(allowed, StringComparer.Ordinal), unknownPropertyCode);
        ValidateIntegerObjectValues(details, maximum);
    }

    private static void ValidateNameAdditiveOptionalIntegerObject(
        JsonElement parent,
        string property,
        int maximum)
    {
        if (!parent.TryGetProperty(property, out JsonElement details)) return;
        if (details.ValueKind != JsonValueKind.Object)
            throw Rejected(ParserRejection.response_usage_invalid);
        ValidateIntegerObjectValues(details, maximum);
    }

    private static void ValidateIntegerObjectValues(JsonElement details, int maximum)
    {
        foreach (JsonProperty detail in details.EnumerateObject())
            if (detail.Value.ValueKind != JsonValueKind.Number
                || !detail.Value.TryGetInt32(out int value) || value < 0 || value > maximum)
                throw Rejected(ParserRejection.response_usage_invalid);
    }

    private static void ValidateOptionalZeroIntegerObject(
        JsonElement parent,
        string property,
        string[] allowed,
        int maximum,
        ParserRejection unknownPropertyCode)
    {
        ValidateOptionalIntegerObject(parent, property, allowed, maximum, unknownPropertyCode);
        if (!parent.TryGetProperty(property, out JsonElement details)) return;
        foreach (JsonProperty detail in details.EnumerateObject())
            if (detail.Value.GetInt32() != 0)
                throw Rejected(ParserRejection.response_usage_invalid);
    }

    private static decimal? ReadOptionalCostDetail(JsonElement parent, string property, decimal maximum)
    {
        if (!parent.TryGetProperty(property, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out decimal result)
            || result < 0 || result > maximum)
            throw Rejected(ParserRejection.response_usage_invalid);
        return result;
    }

    private static void ValidateOptionalNullableString(JsonElement parent, string property, int maximum)
    {
        if (!parent.TryGetProperty(property, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return;
        if (value.ValueKind != JsonValueKind.String || (value.GetString()?.Length ?? 0) > maximum)
            throw Rejected(ParserRejection.response_shape_invalid);
    }

    private static void RequireString(JsonElement element, string property, string expected)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String
            || !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            throw Rejected(ParserRejection.response_binding_invalid);
    }

    private static string RequireBoundedString(JsonElement element, string property, int maximum)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw Rejected(ParserRejection.response_shape_invalid);
        string result = value.GetString()!;
        if (result.Length is < 1 || result.Length > maximum) throw Rejected(ParserRejection.response_string_too_long);
        return result;
    }

    private static int RequireInteger(JsonElement element, string property, int minimum, int maximum)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || !value.TryGetInt32(out int result) || result < minimum || result > maximum)
            throw Rejected(ParserRejection.response_usage_invalid);
        return result;
    }
}
