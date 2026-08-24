using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public enum ProviderRoutingIntent
{
    PreferredOnline = 0,
    LocalOnly = 1
}

public enum ProviderReadiness
{
    Ready = 0,
    NotReady = 1,
    Unknown = 2
}

public enum ProviderPrimaryAttemptState
{
    NotStarted = 0,
    DispatchStarted = 1,
    SubmissionPossible = 2,
    SubmissionUnknown = 3,
    Completed = 4
}

public enum ProviderRoutingSelectedProvider
{
    Ollama = 0,
    OpenRouter = 1
}

/// <summary>Closed caller facts; none of these values carries provider execution authority.</summary>
public sealed record ProviderRoutingPolicyInput(
    ProviderRoutingIntent Intent,
    ProviderReadiness OpenRouterReadiness,
    ProviderReadiness OllamaReadiness,
    ProviderPrimaryAttemptState PrimaryAttemptState);

/// <summary>Detached canonical pre-dispatch decision with no execution capability.</summary>
public sealed class ProviderRoutingDecision
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _claimLimitationCodes;

    internal ProviderRoutingDecision(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string intentCode,
        string openRouterReadinessCode,
        string ollamaReadinessCode,
        string primaryAttemptStateCode,
        string comparisonStatus,
        string? comparisonArtifactDigestSha256,
        string? comparisonSchemaVersion,
        string? comparisonRecommendation,
        ProviderRoutingSelectedProvider? selectedProvider,
        string reasonCode,
        IReadOnlyList<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        IntentCode = intentCode;
        OpenRouterReadinessCode = openRouterReadinessCode;
        OllamaReadinessCode = ollamaReadinessCode;
        PrimaryAttemptStateCode = primaryAttemptStateCode;
        ComparisonStatus = comparisonStatus;
        ComparisonArtifactDigestSha256 = comparisonArtifactDigestSha256;
        ComparisonSchemaVersion = comparisonSchemaVersion;
        ComparisonRecommendation = comparisonRecommendation;
        SelectedProvider = selectedProvider;
        ReasonCode = reasonCode;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderRoutingPolicyModule.DecisionSchemaVersion;
    public string Status => "complete";
    public string IntentCode { get; }
    public string OpenRouterReadinessCode { get; }
    public string OllamaReadinessCode { get; }
    public string PrimaryAttemptStateCode { get; }
    public string ComparisonStatus { get; }
    public string? ComparisonArtifactDigestSha256 { get; }
    public string? ComparisonSchemaVersion { get; }
    public string? ComparisonRecommendation { get; }
    public ProviderRoutingSelectedProvider? SelectedProvider { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

/// <summary>Closed, non-echoing failure surface for untrusted canonical decision bytes.</summary>
public sealed class ProviderRoutingPolicyException : Exception
{
    internal ProviderRoutingPolicyException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "decision_size_invalid" or "decision_utf8_invalid" or "decision_json_invalid" or
        "decision_shape_invalid" or "decision_value_invalid" or "decision_noncanonical" or
        "decision_digest_invalid" or "decision_payload_digest_invalid" or "decision_binding_invalid" => code,
        _ => "decision_validation_failed"
    };
}

/// <summary>Pure synchronous policy over one accepted comparison and closed pre-dispatch facts.</summary>
public static class ProviderRoutingPolicyModule
{
    public const string DecisionSchemaVersion = "snow_globe_provider_routing_decision/v1";
    public const string PolicySchemaVersion = "snow_globe_provider_routing_policy/v1";
    public const string AcceptedComparisonArtifactDigestSha256 = "b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c";
    public const int MaximumDecisionBytes = 4 * 1024;
    public const int MaximumDecisionJsonDepth = 5;

    public static string PolicyDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        "snow_globe_provider_routing_policy/v1|comparison_schema=snow_globe_cognition_quality_comparison/v1|" +
        "comparison_digest=b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c|" +
        "supported_recommendation=openrouter_default|preferred_online=openrouter_when_ready_and_not_started|" +
        "availability_fallback=ollama_only_when_openrouter_not_ready_and_not_started|" +
        "local_only=ollama_when_ready_and_not_started|post_dispatch=no_provider|" +
        "missing_malformed_asymmetric_conditional_insufficient_unsupported=no_provider|execution_authority=none"));

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "policy_schema_version", "policy_digest_sha256",
        "intent", "readiness", "primary_attempt_state", "comparison", "selected_provider",
        "reason_code", "claim_limitation_codes", "decision_payload_digest_sha256"
    ];
    private static readonly string[] ReadinessNames = ["openrouter", "ollama"];
    private static readonly string[] ComparisonNames =
    [
        "status", "artifact_digest_sha256", "schema_version", "recommendation"
    ];
    private static readonly string[] IntentCodes = ["preferred_online", "local_only", "invalid"];
    private static readonly string[] ReadinessCodes = ["ready", "not_ready", "unknown", "invalid"];
    private static readonly string[] PrimaryCodes =
    [
        "not_started", "dispatch_started", "submission_possible", "submission_unknown", "completed", "invalid"
    ];
    private static readonly string[] ComparisonStatuses =
    [
        "accepted", "missing", "malformed", "asymmetric", "conditional", "insufficient",
        "recommendation_unsupported", "artifact_unsupported"
    ];
    private static readonly string[] RecommendationCodes =
    [
        "openrouter_default", "ollama_default", "conditional_routing", "insufficient_evidence"
    ];
    private static readonly string[] ReasonCodes =
    [
        "preferred_openrouter_ready", "explicit_local_only", "pre_dispatch_openrouter_unavailable",
        "primary_dispatch_started", "primary_submission_possible", "primary_submission_unknown", "primary_completed",
        "openrouter_readiness_unknown", "ollama_readiness_unknown", "ollama_not_ready", "no_provider_ready",
        "comparison_missing", "comparison_malformed", "comparison_asymmetric", "comparison_conditional",
        "comparison_insufficient", "comparison_recommendation_unsupported", "comparison_artifact_unsupported",
        "input_invalid"
    ];
    private static readonly string[] ClaimLimitations =
    [
        "decision_only_no_execution_authority",
        "accepted_comparison_digest_bound",
        "availability_is_caller_supplied_not_probed",
        "ollama_availability_fallback_is_pre_dispatch_only",
        "no_automatic_post_dispatch_fallback",
        "no_parallel_or_alternate_dispatch_authority",
        "no_provider_credential_payment_network_or_world_authority",
        "provider_output_untrusted_deterministic_validation_authoritative"
    ];

    public static ProviderRoutingDecision Decide(
        ProviderRoutingPolicyInput input,
        ReadOnlyMemory<byte>? comparisonArtifactCanonicalUtf8)
    {
        ArgumentNullException.ThrowIfNull(input);
        InputFacts inputFacts = Normalize(input);
        ComparisonFacts comparison = AnalyzeComparison(comparisonArtifactCanonicalUtf8);
        return Build(inputFacts, comparison);
    }

    public static ProviderRoutingDecision Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumDecisionBytes) throw Failure("decision_size_invalid");
        try { _ = StrictUtf8.GetString(canonicalUtf8.Span); }
        catch (DecoderFallbackException) { throw Failure("decision_utf8_invalid"); }

        JsonDocument document;
        try
        {
            RejectDuplicateProperties(canonicalUtf8.Span);
            Utf8JsonReader reader = new(canonicalUtf8.Span, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumDecisionJsonDepth
            });
            document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("decision_json_invalid"); }
        }
        catch (ProviderRoutingPolicyException) { throw; }
        catch (JsonException) { throw Failure("decision_json_invalid"); }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireObjectAndOrder(root, RootNames);
            RequireCanonicalScalars(root);
            RequireString(root, "schema_version", DecisionSchemaVersion);
            RequireString(root, "status", "complete");
            RequireString(root, "policy_schema_version", PolicySchemaVersion);
            RequireString(root, "policy_digest_sha256", PolicyDigestSha256);
            string intent = RequireClosedString(root, "intent", IntentCodes);
            JsonElement readiness = root.GetProperty("readiness");
            RequireObjectAndOrder(readiness, ReadinessNames);
            string openRouter = RequireClosedString(readiness, "openrouter", ReadinessCodes);
            string ollama = RequireClosedString(readiness, "ollama", ReadinessCodes);
            string primary = RequireClosedString(root, "primary_attempt_state", PrimaryCodes);

            JsonElement comparisonValue = root.GetProperty("comparison");
            RequireObjectAndOrder(comparisonValue, ComparisonNames);
            ComparisonFacts comparison = new(
                RequireClosedString(comparisonValue, "status", ComparisonStatuses),
                RequireNullableDigest(comparisonValue, "artifact_digest_sha256"),
                RequireNullableClosedString(comparisonValue, "schema_version", [CognitionQualityComparisonModule.SchemaVersion]),
                RequireNullableClosedString(comparisonValue, "recommendation", RecommendationCodes));
            ValidateComparisonFacts(comparison);

            JsonElement selectedValue = root.GetProperty("selected_provider");
            ProviderRoutingSelectedProvider? selected = selectedValue.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String when selectedValue.GetString() == "ollama" => ProviderRoutingSelectedProvider.Ollama,
                JsonValueKind.String when selectedValue.GetString() == "openrouter" => ProviderRoutingSelectedProvider.OpenRouter,
                _ => throw Failure("decision_value_invalid")
            };
            string reason = RequireClosedString(root, "reason_code", ReasonCodes);
            RequireExactStrings(root.GetProperty("claim_limitation_codes"), ClaimLimitations);
            string payloadDigest = RequireDigest(root, "decision_payload_digest_sha256");
            byte[] payload = CanonicalizeWithoutLast(root, "decision_payload_digest_sha256");
            try
            {
                if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal))
                    throw Failure("decision_payload_digest_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(payload); }

            ProviderRoutingDecision recreated = Build(new InputFacts(
                intent,
                openRouter,
                ollama,
                primary,
                intent != "invalid" && openRouter != "invalid" && ollama != "invalid" && primary != "invalid"), comparison);
            if (recreated.SelectedProvider != selected || !string.Equals(recreated.ReasonCode, reason, StringComparison.Ordinal))
                throw Failure("decision_binding_invalid");
            if (!recreated.CanonicalUtf8.Span.SequenceEqual(canonicalUtf8.Span)) throw Failure("decision_noncanonical");
            return recreated;
        }
    }

    private static InputFacts Normalize(ProviderRoutingPolicyInput input)
    {
        string intent = input.Intent switch
        {
            ProviderRoutingIntent.PreferredOnline => "preferred_online",
            ProviderRoutingIntent.LocalOnly => "local_only",
            _ => "invalid"
        };
        string openRouter = ReadinessCode(input.OpenRouterReadiness);
        string ollama = ReadinessCode(input.OllamaReadiness);
        string primary = input.PrimaryAttemptState switch
        {
            ProviderPrimaryAttemptState.NotStarted => "not_started",
            ProviderPrimaryAttemptState.DispatchStarted => "dispatch_started",
            ProviderPrimaryAttemptState.SubmissionPossible => "submission_possible",
            ProviderPrimaryAttemptState.SubmissionUnknown => "submission_unknown",
            ProviderPrimaryAttemptState.Completed => "completed",
            _ => "invalid"
        };
        return new InputFacts(
            intent,
            openRouter,
            ollama,
            primary,
            intent != "invalid" && openRouter != "invalid" && ollama != "invalid" && primary != "invalid");
    }

    private static string ReadinessCode(ProviderReadiness readiness) => readiness switch
    {
        ProviderReadiness.Ready => "ready",
        ProviderReadiness.NotReady => "not_ready",
        ProviderReadiness.Unknown => "unknown",
        _ => "invalid"
    };

    private static ComparisonFacts AnalyzeComparison(ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0) return new ComparisonFacts("missing", null, null, null);
        if (canonical.Value.Length > CognitionQualityComparisonModule.MaximumArtifactBytes)
            return new ComparisonFacts("malformed", null, null, null);
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            CognitionQualityComparisonArtifact artifact;
            try { artifact = CognitionQualityComparisonModule.Validate(snapshot); }
            catch (CognitionQualityComparisonException) { return new ComparisonFacts("malformed", digest, null, null); }

            string recommendation = artifact.Recommendation;
            if (!string.Equals(artifact.Status, "complete", StringComparison.Ordinal)
                || artifact.Providers.Count != 2
                || artifact.Providers.Any(provider => provider.EvidenceStatus != "complete" || provider.AutomatedEvaluation is null))
                return new ComparisonFacts("asymmetric", digest, artifact.SchemaVersion, recommendation);
            if (recommendation == "conditional_routing")
                return new ComparisonFacts("conditional", digest, artifact.SchemaVersion, recommendation);
            if (recommendation == "insufficient_evidence")
                return new ComparisonFacts("insufficient", digest, artifact.SchemaVersion, recommendation);
            if (recommendation != "openrouter_default")
                return new ComparisonFacts("recommendation_unsupported", digest, artifact.SchemaVersion, recommendation);
            if (!string.Equals(digest, AcceptedComparisonArtifactDigestSha256, StringComparison.Ordinal))
                return new ComparisonFacts("artifact_unsupported", digest, artifact.SchemaVersion, recommendation);
            return new ComparisonFacts("accepted", digest, artifact.SchemaVersion, recommendation);
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingDecision Build(InputFacts input, ComparisonFacts comparison)
    {
        ValidateComparisonFacts(comparison);
        (ProviderRoutingSelectedProvider? selected, string reason) = Route(input, comparison);
        byte[] payload = Write(input, comparison, selected, reason, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(input, comparison, selected, reason, payloadDigest);
        if (canonical.Length is < 1 or > MaximumDecisionBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("decision_size_invalid");
        }
        return new ProviderRoutingDecision(
            canonical,
            payloadDigest,
            input.Intent,
            input.OpenRouterReadiness,
            input.OllamaReadiness,
            input.PrimaryAttemptState,
            comparison.Status,
            comparison.ArtifactDigestSha256,
            comparison.SchemaVersion,
            comparison.Recommendation,
            selected,
            reason,
            ClaimLimitations);
    }

    private static (ProviderRoutingSelectedProvider? Selected, string Reason) Route(
        InputFacts input,
        ComparisonFacts comparison)
    {
        if (!input.Valid) return (null, "input_invalid");
        if (input.PrimaryAttemptState != "not_started")
        {
            return input.PrimaryAttemptState switch
            {
                "dispatch_started" => (null, "primary_dispatch_started"),
                "submission_possible" => (null, "primary_submission_possible"),
                "submission_unknown" => (null, "primary_submission_unknown"),
                "completed" => (null, "primary_completed"),
                _ => (null, "input_invalid")
            };
        }
        if (comparison.Status != "accepted") return (null, comparison.Status switch
        {
            "missing" => "comparison_missing",
            "malformed" => "comparison_malformed",
            "asymmetric" => "comparison_asymmetric",
            "conditional" => "comparison_conditional",
            "insufficient" => "comparison_insufficient",
            "recommendation_unsupported" => "comparison_recommendation_unsupported",
            "artifact_unsupported" => "comparison_artifact_unsupported",
            _ => "comparison_malformed"
        });

        if (input.Intent == "local_only") return input.OllamaReadiness switch
        {
            "ready" => (ProviderRoutingSelectedProvider.Ollama, "explicit_local_only"),
            "not_ready" => (null, "ollama_not_ready"),
            "unknown" => (null, "ollama_readiness_unknown"),
            _ => (null, "input_invalid")
        };
        if (input.OpenRouterReadiness == "ready")
            return (ProviderRoutingSelectedProvider.OpenRouter, "preferred_openrouter_ready");
        if (input.OpenRouterReadiness == "unknown") return (null, "openrouter_readiness_unknown");
        return input.OllamaReadiness switch
        {
            "ready" => (ProviderRoutingSelectedProvider.Ollama, "pre_dispatch_openrouter_unavailable"),
            "not_ready" => (null, "no_provider_ready"),
            "unknown" => (null, "ollama_readiness_unknown"),
            _ => (null, "input_invalid")
        };
    }

    private static void ValidateComparisonFacts(ComparisonFacts facts)
    {
        bool digest = IsDigest(facts.ArtifactDigestSha256);
        bool schema = string.Equals(facts.SchemaVersion, CognitionQualityComparisonModule.SchemaVersion, StringComparison.Ordinal);
        bool coherent = facts.Status switch
        {
            "accepted" => digest && schema
                && facts.ArtifactDigestSha256 == AcceptedComparisonArtifactDigestSha256
                && facts.Recommendation == "openrouter_default",
            "missing" => facts.ArtifactDigestSha256 is null && facts.SchemaVersion is null && facts.Recommendation is null,
            "malformed" => (facts.ArtifactDigestSha256 is null || digest)
                && facts.SchemaVersion is null && facts.Recommendation is null,
            "asymmetric" => digest && schema && facts.Recommendation == "insufficient_evidence",
            "conditional" => digest && schema && facts.Recommendation == "conditional_routing",
            "insufficient" => digest && schema && facts.Recommendation == "insufficient_evidence",
            "recommendation_unsupported" => digest && schema && facts.Recommendation == "ollama_default",
            "artifact_unsupported" => digest && schema
                && facts.ArtifactDigestSha256 != AcceptedComparisonArtifactDigestSha256
                && facts.Recommendation == "openrouter_default",
            _ => false
        };
        if (!coherent) throw Failure("decision_binding_invalid");
    }

    private static byte[] Write(
        InputFacts input,
        ComparisonFacts comparison,
        ProviderRoutingSelectedProvider? selected,
        string reason,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", DecisionSchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("policy_schema_version", PolicySchemaVersion);
        writer.WriteString("policy_digest_sha256", PolicyDigestSha256);
        writer.WriteString("intent", input.Intent);
        writer.WritePropertyName("readiness"); writer.WriteStartObject();
        writer.WriteString("openrouter", input.OpenRouterReadiness); writer.WriteString("ollama", input.OllamaReadiness); writer.WriteEndObject();
        writer.WriteString("primary_attempt_state", input.PrimaryAttemptState);
        writer.WritePropertyName("comparison"); writer.WriteStartObject();
        writer.WriteString("status", comparison.Status);
        WriteNullableString(writer, "artifact_digest_sha256", comparison.ArtifactDigestSha256);
        WriteNullableString(writer, "schema_version", comparison.SchemaVersion);
        WriteNullableString(writer, "recommendation", comparison.Recommendation);
        writer.WriteEndObject();
        writer.WritePropertyName("selected_provider");
        if (selected is null) writer.WriteNullValue();
        else writer.WriteStringValue(selected == ProviderRoutingSelectedProvider.Ollama ? "ollama" : "openrouter");
        writer.WriteString("reason_code", reason);
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray();
        foreach (string claim in ClaimLimitations) writer.WriteStringValue(claim);
        writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("decision_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure("decision_shape_invalid");
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure("decision_shape_invalid");
        for (int index = 0; index < names.Count; index++)
            if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal))
                throw Failure("decision_shape_invalid");
    }

    private static string RequireClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) throw Failure("decision_value_invalid");
        return text;
    }

    private static string? RequireNullableClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) throw Failure("decision_value_invalid");
        return text;
    }

    private static void RequireString(JsonElement owner, string name, string expected)
    {
        if (!string.Equals(RequireClosedString(owner, name, [expected]), expected, StringComparison.Ordinal))
            throw Failure("decision_value_invalid");
    }

    private static string RequireDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsDigest(text)) throw Failure("decision_digest_invalid");
        return text!;
    }

    private static string? RequireNullableDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsDigest(text)) throw Failure("decision_digest_invalid");
        return text;
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireExactStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw Failure("decision_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index++], StringComparison.Ordinal))
                throw Failure("decision_value_invalid");
    }

    private static void RequireCanonicalScalars(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject()) RequireCanonicalScalars(property.Value);
            return;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) RequireCanonicalScalars(item);
            return;
        }
        if (value.ValueKind == JsonValueKind.String
            && !string.Equals(JsonSerializer.Serialize(value.GetString()), value.GetRawText(), StringComparison.Ordinal))
            throw Failure("decision_noncanonical");
        if (value.ValueKind == JsonValueKind.Number) throw Failure("decision_noncanonical");
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement root, string lastName)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length == 0 || properties[^1].Name != lastName) throw Failure("decision_shape_invalid");
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^1])
        {
            writer.WritePropertyName(property.Name); property.Value.WriteTo(writer);
        }
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            MaxDepth = MaximumDecisionJsonDepth,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName
                && (stack.Count == 0 || !stack.Peek().Add(reader.GetString()!)))
                throw Failure("decision_shape_invalid");
        }
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name); else writer.WriteString(name, value);
    }

    private sealed record InputFacts(
        string Intent,
        string OpenRouterReadiness,
        string OllamaReadiness,
        string PrimaryAttemptState,
        bool Valid);

    private sealed record ComparisonFacts(
        string Status,
        string? ArtifactDigestSha256,
        string? SchemaVersion,
        string? Recommendation);

    private static ProviderRoutingPolicyException Failure(string code) => new(code);
}
