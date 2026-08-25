using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

internal enum ProviderReadinessProvider
{
    OpenRouter = 1,
    Ollama = 2
}

internal interface IProviderReadinessClock
{
    long NowMilliseconds { get; }
}

internal sealed class SystemProviderReadinessClock : IProviderReadinessClock
{
    internal static SystemProviderReadinessClock Instance { get; } = new();
    private SystemProviderReadinessClock() { }
    public long NowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

internal interface IProviderReadinessObservationAdapter
{
    ProviderReadinessProvider Provider { get; }
    ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(CancellationToken cancellationToken);
}

internal sealed record ProviderReadinessAdapterResult
{
    private ProviderReadinessAdapterResult(
        string readiness,
        string diagnosticCode,
        int requestCount,
        string sourceSchemaVersion,
        string sourceContractDigestSha256,
        string accountBindingStatus)
    {
        Readiness = readiness;
        DiagnosticCode = diagnosticCode;
        RequestCount = requestCount;
        SourceSchemaVersion = sourceSchemaVersion;
        SourceContractDigestSha256 = sourceContractDigestSha256;
        AccountBindingStatus = accountBindingStatus;
    }

    internal string Readiness { get; }
    internal string DiagnosticCode { get; }
    internal int RequestCount { get; }
    internal string SourceSchemaVersion { get; }
    internal string SourceContractDigestSha256 { get; }
    internal string AccountBindingStatus { get; }

    internal static ProviderReadinessAdapterResult Ready(
        int requestCount,
        string sourceSchemaVersion,
        string sourceContractDigestSha256,
        string accountBindingStatus) =>
        new("ready", "ready", requestCount, sourceSchemaVersion, sourceContractDigestSha256, accountBindingStatus);

    internal static ProviderReadinessAdapterResult Unavailable(
        string diagnosticCode,
        int requestCount,
        string sourceSchemaVersion,
        string sourceContractDigestSha256,
        string accountBindingStatus) =>
        new("unavailable", diagnosticCode, requestCount, sourceSchemaVersion, sourceContractDigestSha256, accountBindingStatus);

    internal static ProviderReadinessAdapterResult Unknown(
        string diagnosticCode,
        int requestCount,
        string sourceSchemaVersion,
        string sourceContractDigestSha256,
        string accountBindingStatus) =>
        new("unknown", diagnosticCode, requestCount, sourceSchemaVersion, sourceContractDigestSha256, accountBindingStatus);
}

public sealed class ProviderReadinessObservation
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _limitationCodes;

    internal ProviderReadinessObservation(
        byte[] canonicalUtf8,
        string provider,
        long observedAtUnixMilliseconds,
        long expiresAtUnixMilliseconds,
        string readiness,
        string diagnosticCode,
        int requestCount,
        string sourceSchemaVersion,
        string sourceContractDigestSha256,
        string accountBindingStatus,
        string payloadDigestSha256,
        IReadOnlyList<string> limitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _limitationCodes = limitationCodes.ToArray();
        Provider = provider;
        ObservedAtUnixMilliseconds = observedAtUnixMilliseconds;
        ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        Readiness = readiness;
        DiagnosticCode = diagnosticCode;
        RequestCount = requestCount;
        SourceSchemaVersion = sourceSchemaVersion;
        SourceContractDigestSha256 = sourceContractDigestSha256;
        AccountBindingStatus = accountBindingStatus;
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderReadinessObservationModule.SchemaVersion;
    public string Status => "complete";
    public string Provider { get; }
    public long ObservedAtUnixMilliseconds { get; }
    public long ExpiresAtUnixMilliseconds { get; }
    public string Readiness { get; }
    public string DiagnosticCode { get; }
    public int RequestCount { get; }
    public int GenerationRequestCount => 0;
    public string RequestMethod => "GET";
    public string SourceSchemaVersion { get; }
    public string SourceContractDigestSha256 { get; }
    public string AccountBindingStatus { get; }
    public IReadOnlyList<string> LimitationCodes => Array.AsReadOnly(_limitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class ProviderReadinessObservationException : Exception
{
    internal ProviderReadinessObservationException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "observation_size_invalid" or "observation_utf8_invalid" or "observation_json_invalid" or
        "observation_shape_invalid" or "observation_value_invalid" or "observation_noncanonical" or
        "observation_digest_invalid" or "observation_payload_digest_invalid" or
        "observation_binding_invalid" or "observation_expired" or "observation_time_invalid" => code,
        _ => "observation_validation_failed"
    };
}

/// <summary>
/// Produces and validates one current, provider-neutral readiness fact. The result is evidence only:
/// it cannot select, authorize, or dispatch a provider generation.
/// </summary>
public static class ProviderReadinessObservationModule
{
    public const string SchemaVersion = "snow_globe_provider_readiness_observation/v1";
    public const string ContractSchemaVersion = "snow_globe_provider_readiness_observation_contract/v1";
    public const int ObservationLifetimeMilliseconds = 60_000;
    public const int MaximumObservationBytes = 4 * 1024;
    public const int MaximumJsonDepth = 4;

    public static string ContractDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        ContractSchemaVersion +
        "|one_provider_per_artifact|providers=openrouter,ollama|readiness=ready,unavailable,unknown" +
        "|openrouter_source_schema=" + OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion +
        "|openrouter_source_contract_digest=" + OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256 +
        "|ollama_source_schema=" + OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion +
        "|ollama_source_contract_digest=" + OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256 +
        "|adapters=one_shot" +
        "|openrouter=exact_three_sequential_authenticated_gets_current_key_filtered_models_zdr_endpoints_no_post" +
        "|ollama=exact_one_loopback_get_api_tags_pinned_windows_process_listener_model_alias_digest_no_generate" +
        "|openrouter_unavailable=credential_unavailable,provider_unavailable,metadata_rejected" +
        "|openrouter_unknown=operation_cancelled,observation_timeout,observation_failed" +
        "|ollama_unavailable=provider_unavailable,runtime_identity_drift_predispatch,model_metadata_rejected" +
        "|ollama_unknown=operation_cancelled,observation_timeout,identity_race_postdispatch,observation_failed" +
        "|request_counts=openrouter_credential_unavailable_0_or_1_or_3_provider_unavailable_1_to_3_metadata_rejected_1_to_3_cancelled_0_to_3_timeout_1_to_3_failed_0_to_3" +
        "|request_counts_ollama_runtime_identity_drift_0_provider_unavailable_or_model_metadata_rejected_or_timeout_or_identity_race_1_cancelled_or_failed_0_or_1" +
        "|time_domain=observed_at_positive_expiry_and_validation_time_within_datetimeoffset" +
        "|expiry_ms=60000|caller_input=one_owned_snapshot_zeroed|raw_provider_metadata=forbidden" +
        "|credential_response_path_pid_account_dynamic_error=forbidden|generation_request_count=zero" +
        "|routing_payment_world_execution_authority=none"));

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "contract_schema_version", "contract_digest_sha256", "provider",
        "observed_at_unix_ms", "expires_at_unix_ms", "readiness", "diagnostic_code", "request_method",
        "request_count", "generation_request_count", "source_schema_version", "source_contract_digest_sha256",
        "account_binding_status", "limitation_codes", "observation_payload_digest_sha256"
    ];
    private static readonly string[] Limitations =
    [
        "point_in_time_observation_not_continuous_availability",
        "readiness_does_not_authorize_dispatch_generation_or_payment",
        "provider_output_is_not_world_authority",
        "raw_metadata_credentials_accounts_host_identity_and_dynamic_errors_not_retained",
        "primary_attempt_state_and_routing_input_not_established"
    ];
    private static readonly string[] UnavailableDiagnostics =
    [
        "credential_unavailable", "provider_unavailable", "metadata_rejected",
        "runtime_identity_drift", "model_metadata_rejected"
    ];
    private static readonly string[] UnknownDiagnostics =
    [
        "operation_cancelled", "observation_timeout", "identity_race", "observation_failed"
    ];

    internal static async ValueTask<ProviderReadinessObservation> ObserveAsync(
        IProviderReadinessObservationAdapter adapter,
        IProviderReadinessClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(clock);
        if (!Enum.IsDefined(adapter.Provider)) throw Failure("observation_binding_invalid");
        long observedAt = clock.NowMilliseconds;
        if (observedAt <= 0 || observedAt > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() - ObservationLifetimeMilliseconds)
            throw Failure("observation_time_invalid");

        ProviderReadinessAdapterResult result;
        try
        {
            result = await adapter.ObserveOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = FailureResult(adapter.Provider, "operation_cancelled");
        }
        catch
        {
            result = FailureResult(adapter.Provider, "observation_failed");
        }

        ValidateAdapterResult(adapter.Provider, result);
        return Build(adapter.Provider, observedAt, checked(observedAt + ObservationLifetimeMilliseconds), result);
    }

    public static ProviderReadinessObservation Validate(
        ReadOnlyMemory<byte> canonicalUtf8,
        long currentUnixMilliseconds)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumObservationBytes)
            throw Failure("observation_size_invalid");
        if (currentUnixMilliseconds <= 0
            || currentUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            throw Failure("observation_time_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            try { _ = StrictUtf8.GetString(snapshot); }
            catch (DecoderFallbackException) { throw Failure("observation_utf8_invalid"); }

            JsonDocument document;
            try
            {
                RejectDuplicateProperties(snapshot);
                Utf8JsonReader reader = new(snapshot, new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumJsonDepth
                });
                document = JsonDocument.ParseValue(ref reader);
                if (reader.Read()) { document.Dispose(); throw Failure("observation_json_invalid"); }
            }
            catch (ProviderReadinessObservationException) { throw; }
            catch (JsonException) { throw Failure("observation_json_invalid"); }

            using (document)
            {
                JsonElement root = document.RootElement;
                RequireObjectAndOrder(root, RootNames);
                RequireString(root, "schema_version", SchemaVersion);
                RequireString(root, "status", "complete");
                RequireString(root, "contract_schema_version", ContractSchemaVersion);
                RequireString(root, "contract_digest_sha256", ContractDigestSha256);
                string providerText = RequireClosed(root, "provider", ["openrouter", "ollama"]);
                ProviderReadinessProvider provider = providerText == "openrouter"
                    ? ProviderReadinessProvider.OpenRouter : ProviderReadinessProvider.Ollama;
                long observedAt = RequireInt64(root, "observed_at_unix_ms");
                long expiresAt = RequireInt64(root, "expires_at_unix_ms");
                if (observedAt <= 0 || observedAt > MaximumObservedAtUnixMilliseconds)
                    throw Failure("observation_time_invalid");
                string readiness = RequireClosed(root, "readiness", ["ready", "unavailable", "unknown"]);
                string diagnostic = RequireClosed(root, "diagnostic_code",
                    ["ready", .. UnavailableDiagnostics, .. UnknownDiagnostics]);
                RequireString(root, "request_method", "GET");
                int requestCount = RequireInt32(root, "request_count");
                if (RequireInt32(root, "generation_request_count") != 0)
                    throw Failure("observation_binding_invalid");
                string sourceSchema = RequireIdentity(root, "source_schema_version", 128);
                string sourceDigest = RequireDigest(root, "source_contract_digest_sha256");
                string accountBinding = RequireClosed(root, "account_binding_status",
                    ["same_account_bound", "not_performed", "not_applicable"]);
                RequireExactStrings(root.GetProperty("limitation_codes"), Limitations);
                string payloadDigest = RequireDigest(root, "observation_payload_digest_sha256");
                if (expiresAt != checked(observedAt + ObservationLifetimeMilliseconds))
                    throw Failure("observation_binding_invalid");
                if (currentUnixMilliseconds < observedAt) throw Failure("observation_time_invalid");
                if (currentUnixMilliseconds > expiresAt) throw Failure("observation_expired");
                ProviderReadinessAdapterResult result = newResult(
                    readiness, diagnostic, requestCount, sourceSchema, sourceDigest, accountBinding);
                ValidateAdapterResult(provider, result);

                byte[] payload = CanonicalizeWithoutLast(root, "observation_payload_digest_sha256");
                try
                {
                    if (CognitionQualityHash.Sha256(payload) != payloadDigest)
                        throw Failure("observation_payload_digest_invalid");
                }
                finally { CryptographicOperations.ZeroMemory(payload); }

                ProviderReadinessObservation recreated = Build(provider, observedAt, expiresAt, result);
                if (!recreated.CanonicalUtf8.Span.SequenceEqual(snapshot))
                    throw Failure("observation_noncanonical");
                return recreated;
            }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderReadinessAdapterResult newResult(
        string readiness, string diagnostic, int count, string schema, string digest, string binding) =>
        readiness switch
        {
            "ready" => ProviderReadinessAdapterResult.Ready(count, schema, digest, binding),
            "unavailable" => ProviderReadinessAdapterResult.Unavailable(diagnostic, count, schema, digest, binding),
            _ => ProviderReadinessAdapterResult.Unknown(diagnostic, count, schema, digest, binding)
        };

    private static ProviderReadinessAdapterResult FailureResult(
        ProviderReadinessProvider provider,
        string diagnostic) => ProviderReadinessAdapterResult.Unknown(
            diagnostic,
            0,
            provider == ProviderReadinessProvider.OpenRouter
                ? OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion
                : OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
            provider == ProviderReadinessProvider.OpenRouter
                ? OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256
                : OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256,
            provider == ProviderReadinessProvider.OpenRouter ? "not_performed" : "not_applicable");

    private static void ValidateAdapterResult(
        ProviderReadinessProvider provider,
        ProviderReadinessAdapterResult? result)
    {
        if (result is null || !IsIdentity(result.SourceSchemaVersion, 128)
            || !IsDigest(result.SourceContractDigestSha256))
            throw Failure("observation_binding_invalid");
        string expectedSourceSchema = provider == ProviderReadinessProvider.OpenRouter
            ? OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion
            : OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion;
        string expectedSourceDigest = provider == ProviderReadinessProvider.OpenRouter
            ? OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256
            : OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256;
        if (result.SourceSchemaVersion != expectedSourceSchema
            || result.SourceContractDigestSha256 != expectedSourceDigest)
            throw Failure("observation_binding_invalid");
        int maximumRequests = provider == ProviderReadinessProvider.OpenRouter ? 3 : 1;
        if (result.RequestCount < 0 || result.RequestCount > maximumRequests)
            throw Failure("observation_binding_invalid");
        IReadOnlyList<string> providerUnavailable = provider == ProviderReadinessProvider.OpenRouter
            ? ["credential_unavailable", "provider_unavailable", "metadata_rejected"]
            : ["provider_unavailable", "runtime_identity_drift", "model_metadata_rejected"];
        IReadOnlyList<string> providerUnknown = provider == ProviderReadinessProvider.OpenRouter
            ? ["operation_cancelled", "observation_timeout", "observation_failed"]
            : ["operation_cancelled", "observation_timeout", "identity_race", "observation_failed"];
        bool readinessValid = result.Readiness switch
        {
            "ready" => result.DiagnosticCode == "ready" && result.RequestCount == maximumRequests,
            "unavailable" => providerUnavailable.Contains(result.DiagnosticCode, StringComparer.Ordinal),
            "unknown" => providerUnknown.Contains(result.DiagnosticCode, StringComparer.Ordinal),
            _ => false
        };
        bool accountValid = provider == ProviderReadinessProvider.OpenRouter
            ? result.AccountBindingStatus == (result.Readiness == "ready" ? "same_account_bound" : "not_performed")
            : result.AccountBindingStatus == "not_applicable";
        bool requestCountValid = provider switch
        {
            ProviderReadinessProvider.OpenRouter => result.DiagnosticCode switch
            {
                "ready" => result.RequestCount == 3,
                "credential_unavailable" => result.RequestCount is 0 or 1 or 3,
                "provider_unavailable" or "metadata_rejected" or
                "observation_timeout" => result.RequestCount is >= 1 and <= 3,
                "operation_cancelled" or "observation_failed" => result.RequestCount is >= 0 and <= 3,
                _ => false
            },
            ProviderReadinessProvider.Ollama => result.DiagnosticCode switch
            {
                "ready" => result.RequestCount == 1,
                "runtime_identity_drift" => result.RequestCount == 0,
                "provider_unavailable" or "model_metadata_rejected" or
                "observation_timeout" or "identity_race" => result.RequestCount == 1,
                "operation_cancelled" or "observation_failed" => result.RequestCount is 0 or 1,
                _ => false
            },
            _ => false
        };
        if (!readinessValid || !accountValid || !requestCountValid)
            throw Failure("observation_binding_invalid");
    }

    private static ProviderReadinessObservation Build(
        ProviderReadinessProvider provider,
        long observedAt,
        long expiresAt,
        ProviderReadinessAdapterResult result)
    {
        byte[] payload = Write(provider, observedAt, expiresAt, result, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(provider, observedAt, expiresAt, result, payloadDigest);
        if (canonical.Length > MaximumObservationBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("observation_size_invalid");
        }
        return new(canonical, ProviderText(provider), observedAt, expiresAt, result.Readiness,
            result.DiagnosticCode, result.RequestCount, result.SourceSchemaVersion,
            result.SourceContractDigestSha256, result.AccountBindingStatus, payloadDigest, Limitations);
    }

    private static byte[] Write(
        ProviderReadinessProvider provider,
        long observedAt,
        long expiresAt,
        ProviderReadinessAdapterResult result,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("contract_schema_version", ContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", ContractDigestSha256);
        writer.WriteString("provider", ProviderText(provider));
        writer.WriteNumber("observed_at_unix_ms", observedAt);
        writer.WriteNumber("expires_at_unix_ms", expiresAt);
        writer.WriteString("readiness", result.Readiness);
        writer.WriteString("diagnostic_code", result.DiagnosticCode);
        writer.WriteString("request_method", "GET");
        writer.WriteNumber("request_count", result.RequestCount);
        writer.WriteNumber("generation_request_count", 0);
        writer.WriteString("source_schema_version", result.SourceSchemaVersion);
        writer.WriteString("source_contract_digest_sha256", result.SourceContractDigestSha256);
        writer.WriteString("account_binding_status", result.AccountBindingStatus);
        writer.WritePropertyName("limitation_codes"); writer.WriteStartArray();
        foreach (string limitation in Limitations) writer.WriteStringValue(limitation);
        writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("observation_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static string ProviderText(ProviderReadinessProvider provider) => provider switch
    {
        ProviderReadinessProvider.OpenRouter => "openrouter",
        ProviderReadinessProvider.Ollama => "ollama",
        _ => throw Failure("observation_binding_invalid")
    };

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure("observation_shape_invalid");
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure("observation_shape_invalid");
        for (int index = 0; index < names.Count; index++)
            if (properties[index].Name != names[index]) throw Failure("observation_shape_invalid");
    }

    private static string RequireString(JsonElement owner, string name, string expected) =>
        RequireClosed(owner, name, [expected]);

    private static string RequireClosed(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || JsonSerializer.Serialize(text) != value.GetRawText()
            || !allowed.Contains(text, StringComparer.Ordinal))
            throw Failure("observation_value_invalid");
        return text;
    }

    private static string RequireIdentity(JsonElement owner, string name, int maximum)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsIdentity(text, maximum) || JsonSerializer.Serialize(text) != value.GetRawText())
            throw Failure("observation_value_invalid");
        return text!;
    }

    private static string RequireDigest(JsonElement owner, string name)
    {
        string text = RequireIdentity(owner, name, 64);
        if (!IsDigest(text)) throw Failure("observation_digest_invalid");
        return text;
    }

    private static long RequireInt64(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed)
            || parsed < 0 || value.GetRawText() != parsed.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw Failure("observation_value_invalid");
        return parsed;
    }

    private static int RequireInt32(JsonElement owner, string name)
    {
        long parsed = RequireInt64(owner, name);
        if (parsed > int.MaxValue) throw Failure("observation_value_invalid");
        return (int)parsed;
    }

    private static bool IsIdentity(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum
        && value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.' or '/');

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireExactStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw Failure("observation_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || item.GetString() != expected[index++]
                || JsonSerializer.Serialize(item.GetString()) != item.GetRawText())
                throw Failure("observation_value_invalid");
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement root, string lastName)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length == 0 || properties[^1].Name != lastName)
            throw Failure("observation_shape_invalid");
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^1])
        {
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            MaxDepth = MaximumJsonDepth,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
                stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject)
                stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName
                && (stack.Count == 0 || !stack.Peek().Add(reader.GetString()!)))
                throw Failure("observation_shape_invalid");
        }
    }

    private static ProviderReadinessObservationException Failure(string code) => new(code);

    private static long MaximumObservedAtUnixMilliseconds =>
        DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() - ObservationLifetimeMilliseconds;
}
