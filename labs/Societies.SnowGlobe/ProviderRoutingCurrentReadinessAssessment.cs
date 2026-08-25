using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed class ProviderRoutingCurrentReadinessAssessment
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _gapCodes;

    internal ProviderRoutingCurrentReadinessAssessment(
        byte[] canonicalUtf8,
        long assessedAtUnixMilliseconds,
        long expiresAtUnixMilliseconds,
        string openRouterCurrentReadiness,
        string ollamaCurrentReadiness,
        IReadOnlyList<string> gapCodes,
        string payloadDigestSha256)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _gapCodes = gapCodes.ToArray();
        AssessedAtUnixMilliseconds = assessedAtUnixMilliseconds;
        ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        OpenRouterCurrentReadiness = openRouterCurrentReadiness;
        OllamaCurrentReadiness = ollamaCurrentReadiness;
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion;
    public string Status => "insufficient_current_attempt_evidence";
    public long AssessedAtUnixMilliseconds { get; }
    public long ExpiresAtUnixMilliseconds { get; }
    public string OpenRouterCurrentReadiness { get; }
    public string OllamaCurrentReadiness { get; }
    public string PrimaryAttemptCurrentState => "unknown";
    public string RoutingInputIssuanceStatus => "not_issued";
    public object? RoutingPolicyInput => null;
    public IReadOnlyList<string> GapCodes => Array.AsReadOnly(_gapCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

internal sealed record ProviderRoutingCurrentObservationFact(
    string Status,
    string? ArtifactDigestSha256,
    ProviderReadinessObservation? Observation);

public static partial class ProviderRoutingReadinessEvidenceModule
{
    public const string CurrentSchemaVersion = "snow_globe_provider_routing_readiness_assessment/v2";
    public const string CurrentContractSchemaVersion = "snow_globe_provider_routing_readiness_evidence/v2";
    public const int MaximumCurrentAssessmentBytes = 16 * 1024;
    public const int MaximumCurrentAssessmentJsonDepth = 9;

    public static string CurrentContractDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        CurrentContractSchemaVersion + "|historical_assessment_schema=" + SchemaVersion +
        "|historical_contract_digest=" + ContractDigestSha256 +
        "|current_observation_schema=" + ProviderReadinessObservationModule.SchemaVersion +
        "|current_observation_contract_digest=" + ProviderReadinessObservationModule.ContractDigestSha256 +
        "|accepted_current_observation=embedded_and_revalidated_exact_provider_and_digest" +
        "|expired=validated_but_unknown|missing_or_malformed=unknown_not_negative" +
        "|current_ready=ready|current_unavailable=not_ready|current_unknown=unknown" +
        "|primary_attempt_current_state=unknown|routing_policy_input=never_issued_v2" +
        "|caller_inputs=one_owned_snapshot_zeroed|execution_authority=none"));

    private static readonly string[] CurrentRootNames =
    [
        "schema_version", "status", "contract_schema_version", "contract_digest_sha256",
        "assessed_at_unix_ms", "expires_at_unix_ms", "historical_assessment",
        "current_observations", "current_readiness", "primary_attempt_current_state",
        "routing_input_issuance_status", "routing_policy_input", "gap_codes",
        "claim_limitation_codes", "assessment_payload_digest_sha256"
    ];
    private static readonly string[] CurrentObservationsNames = ["openrouter", "ollama"];
    private static readonly string[] CurrentFactNames = ["status", "artifact_digest_sha256", "evidence"];
    private static readonly string[] CurrentReadinessNamesV2 = ["openrouter", "ollama"];
    private static readonly string[] CurrentLimitations =
    [
        "current_readiness_is_bounded_until_assessment_expiry_not_continuous_availability",
        "primary_attempt_state_remains_unknown",
        "routing_input_not_issued",
        "readiness_does_not_authorize_dispatch_generation_payment_or_world_mutation",
        "digests_provide_integrity_not_execution_authenticity",
        "historical_provider_success_is_not_current_readiness"
    ];

    public static ProviderRoutingCurrentReadinessAssessment AssessCurrent(
        ProviderRoutingReadinessEvidenceInput historicalInput,
        ReadOnlyMemory<byte>? openRouterObservationCanonicalUtf8,
        ReadOnlyMemory<byte>? ollamaObservationCanonicalUtf8,
        long currentUnixMilliseconds)
    {
        if (currentUnixMilliseconds <= 0
            || currentUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
                - ProviderReadinessObservationModule.ObservationLifetimeMilliseconds)
            throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
        ProviderRoutingReadinessAssessment historical = Assess(historicalInput);
        ProviderRoutingCurrentObservationFact openRouter = AssessCurrentObservation(
            openRouterObservationCanonicalUtf8, "openrouter", currentUnixMilliseconds);
        ProviderRoutingCurrentObservationFact ollama = AssessCurrentObservation(
            ollamaObservationCanonicalUtf8, "ollama", currentUnixMilliseconds);
        return BuildCurrent(historical, openRouter, ollama, currentUnixMilliseconds);
    }

    public static ProviderRoutingCurrentReadinessAssessment ValidateCurrent(
        ReadOnlyMemory<byte> canonicalUtf8,
        long currentUnixMilliseconds)
    {
        if (currentUnixMilliseconds <= 0
            || currentUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
        if (canonicalUtf8.Length is < 1 or > MaximumCurrentAssessmentBytes)
            throw new ProviderRoutingReadinessEvidenceException("assessment_size_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            JsonDocument document;
            try
            {
                _ = new UTF8Encoding(false, true).GetString(snapshot);
                RejectCurrentDuplicateProperties(snapshot);
                document = JsonDocument.Parse(snapshot, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumCurrentAssessmentJsonDepth
                });
            }
            catch (DecoderFallbackException) { throw new ProviderRoutingReadinessEvidenceException("assessment_utf8_invalid"); }
            catch (ProviderRoutingReadinessEvidenceException) { throw; }
            catch (JsonException) { throw new ProviderRoutingReadinessEvidenceException("assessment_json_invalid"); }

            using (document)
            {
                JsonElement root = document.RootElement;
                RequireCurrentObjectOrder(root, CurrentRootNames);
                RequireCurrentString(root, "schema_version", CurrentSchemaVersion);
                RequireCurrentString(root, "status", "insufficient_current_attempt_evidence");
                RequireCurrentString(root, "contract_schema_version", CurrentContractSchemaVersion);
                RequireCurrentString(root, "contract_digest_sha256", CurrentContractDigestSha256);
                long assessedAt = RequireCurrentInt64(root, "assessed_at_unix_ms");
                long expiresAt = RequireCurrentInt64(root, "expires_at_unix_ms");
                if (assessedAt <= 0
                    || assessedAt > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
                        - ProviderReadinessObservationModule.ObservationLifetimeMilliseconds
                    || expiresAt > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
                if (currentUnixMilliseconds < assessedAt)
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
                if (currentUnixMilliseconds > expiresAt)
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");

                byte[] historicalBytes = CanonicalizeCurrentElement(root.GetProperty("historical_assessment"));
                ProviderRoutingReadinessAssessment historical;
                try { historical = Validate(historicalBytes); }
                finally { CryptographicOperations.ZeroMemory(historicalBytes); }

                JsonElement observations = root.GetProperty("current_observations");
                RequireCurrentObjectOrder(observations, CurrentObservationsNames);
                ProviderRoutingCurrentObservationFact openRouter = ParseCurrentFact(
                    observations.GetProperty("openrouter"), "openrouter", assessedAt);
                ProviderRoutingCurrentObservationFact ollama = ParseCurrentFact(
                    observations.GetProperty("ollama"), "ollama", assessedAt);
                long expectedExpiry = CurrentExpiry(assessedAt, openRouter, ollama);
                if (expiresAt != expectedExpiry)
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");

                string openRouterReadiness = ProjectCurrentReadiness(openRouter);
                string ollamaReadiness = ProjectCurrentReadiness(ollama);
                JsonElement readiness = root.GetProperty("current_readiness");
                RequireCurrentObjectOrder(readiness, CurrentReadinessNamesV2);
                RequireCurrentString(readiness, "openrouter", openRouterReadiness);
                RequireCurrentString(readiness, "ollama", ollamaReadiness);
                RequireCurrentString(root, "primary_attempt_current_state", "unknown");
                RequireCurrentString(root, "routing_input_issuance_status", "not_issued");
                if (root.GetProperty("routing_policy_input").ValueKind != JsonValueKind.Null)
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
                string[] gaps = CurrentGaps(historical, openRouter, ollama);
                RequireCurrentStrings(root.GetProperty("gap_codes"), gaps);
                RequireCurrentStrings(root.GetProperty("claim_limitation_codes"), CurrentLimitations);
                string payloadDigest = RequireCurrentDigest(root, "assessment_payload_digest_sha256");
                byte[] payload = CanonicalizeCurrentWithoutLast(root, "assessment_payload_digest_sha256");
                try
                {
                    if (CognitionQualityHash.Sha256(payload) != payloadDigest)
                        throw new ProviderRoutingReadinessEvidenceException("assessment_payload_digest_invalid");
                }
                finally { CryptographicOperations.ZeroMemory(payload); }

                ProviderRoutingCurrentReadinessAssessment recreated = BuildCurrent(
                    historical, openRouter, ollama, assessedAt);
                if (!recreated.CanonicalUtf8.Span.SequenceEqual(snapshot))
                    throw new ProviderRoutingReadinessEvidenceException("assessment_noncanonical");
                return recreated;
            }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingCurrentObservationFact AssessCurrentObservation(
        ReadOnlyMemory<byte>? canonical,
        string expectedProvider,
        long currentUnixMilliseconds)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0)
            return new("missing", null, null);
        if (canonical.Value.Length > ProviderReadinessObservationModule.MaximumObservationBytes)
            return new("malformed", null, null);
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            try
            {
                ProviderReadinessObservation observation = ProviderReadinessObservationModule.Validate(
                    snapshot, currentUnixMilliseconds);
                return observation.Provider == expectedProvider
                    ? new("accepted", digest, observation)
                    : new("malformed", digest, null);
            }
            catch (ProviderReadinessObservationException exception) when (exception.Code == "observation_expired")
            {
                long? expiry = TryReadObservationExpiry(snapshot);
                if (!expiry.HasValue) return new("malformed", digest, null);
                try
                {
                    ProviderReadinessObservation observation = ProviderReadinessObservationModule.Validate(
                        snapshot, expiry.Value);
                    return observation.Provider == expectedProvider
                        ? new("expired", digest, observation)
                        : new("malformed", digest, null);
                }
                catch (ProviderReadinessObservationException) { return new("malformed", digest, null); }
            }
            catch (ProviderReadinessObservationException) { return new("malformed", digest, null); }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingCurrentObservationFact ParseCurrentFact(
        JsonElement element,
        string expectedProvider,
        long assessedAt)
    {
        RequireCurrentObjectOrder(element, CurrentFactNames);
        string status = RequireCurrentClosed(element, "status", ["accepted", "expired", "missing", "malformed"]);
        string? digest = RequireCurrentNullableDigest(element, "artifact_digest_sha256");
        JsonElement evidence = element.GetProperty("evidence");
        if (status is "missing" or "malformed")
        {
            if (evidence.ValueKind != JsonValueKind.Null || status == "missing" && digest is not null)
                throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
            return new(status, digest, null);
        }
        if (evidence.ValueKind != JsonValueKind.Object || digest is null)
            throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
        byte[] bytes = CanonicalizeCurrentElement(evidence);
        try
        {
            ProviderReadinessObservation observation;
            if (status == "accepted")
                observation = ProviderReadinessObservationModule.Validate(bytes, assessedAt);
            else
            {
                long? expiry = TryReadObservationExpiry(bytes);
                if (!expiry.HasValue || assessedAt <= expiry.Value)
                    throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
                observation = ProviderReadinessObservationModule.Validate(bytes, expiry.Value);
            }
            if (observation.Provider != expectedProvider || observation.CanonicalDigestSha256 != digest)
                throw new ProviderRoutingReadinessEvidenceException("assessment_binding_invalid");
            return new(status, digest, observation);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static ProviderRoutingCurrentReadinessAssessment BuildCurrent(
        ProviderRoutingReadinessAssessment historical,
        ProviderRoutingCurrentObservationFact openRouter,
        ProviderRoutingCurrentObservationFact ollama,
        long assessedAt)
    {
        long expiresAt = CurrentExpiry(assessedAt, openRouter, ollama);
        string[] gaps = CurrentGaps(historical, openRouter, ollama);
        byte[] payload = WriteCurrent(historical, openRouter, ollama, assessedAt, expiresAt, gaps, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = WriteCurrent(historical, openRouter, ollama, assessedAt, expiresAt, gaps, payloadDigest);
        if (canonical.Length > MaximumCurrentAssessmentBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw new ProviderRoutingReadinessEvidenceException("assessment_size_invalid");
        }
        return new(canonical, assessedAt, expiresAt, ProjectCurrentReadiness(openRouter),
            ProjectCurrentReadiness(ollama), gaps, payloadDigest);
    }

    private static long CurrentExpiry(
        long assessedAt,
        ProviderRoutingCurrentObservationFact openRouter,
        ProviderRoutingCurrentObservationFact ollama)
    {
        long expiry = checked(assessedAt + ProviderReadinessObservationModule.ObservationLifetimeMilliseconds);
        if (openRouter.Status == "accepted") expiry = Math.Min(expiry, openRouter.Observation!.ExpiresAtUnixMilliseconds);
        if (ollama.Status == "accepted") expiry = Math.Min(expiry, ollama.Observation!.ExpiresAtUnixMilliseconds);
        return expiry;
    }

    private static string ProjectCurrentReadiness(ProviderRoutingCurrentObservationFact fact) =>
        fact.Status != "accepted" ? "unknown" : fact.Observation!.Readiness switch
        {
            "ready" => "ready",
            "unavailable" => "not_ready",
            _ => "unknown"
        };

    private static string[] CurrentGaps(
        ProviderRoutingReadinessAssessment historical,
        ProviderRoutingCurrentObservationFact openRouter,
        ProviderRoutingCurrentObservationFact ollama)
    {
        List<string> gaps = [];
        if (historical.SelectionEvidence != "accepted_openrouter_default")
            gaps.Add("accepted_comparison_evidence_unproven");
        AddCurrentGap(gaps, "openrouter", openRouter);
        AddCurrentGap(gaps, "ollama", ollama);
        gaps.Add("authenticated_attempt_bound_primary_state_unproven");
        return gaps.ToArray();
    }

    private static void AddCurrentGap(
        List<string> gaps,
        string provider,
        ProviderRoutingCurrentObservationFact fact)
    {
        if (ProjectCurrentReadiness(fact) != "unknown") return;
        gaps.Add(fact.Status switch
        {
            "missing" => "current_" + provider + "_readiness_evidence_missing",
            "expired" => "current_" + provider + "_readiness_evidence_expired",
            "malformed" => "current_" + provider + "_readiness_evidence_malformed",
            _ => "current_" + provider + "_readiness_unknown"
        });
    }

    private static byte[] WriteCurrent(
        ProviderRoutingReadinessAssessment historical,
        ProviderRoutingCurrentObservationFact openRouter,
        ProviderRoutingCurrentObservationFact ollama,
        long assessedAt,
        long expiresAt,
        IReadOnlyList<string> gaps,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", CurrentSchemaVersion);
        writer.WriteString("status", "insufficient_current_attempt_evidence");
        writer.WriteString("contract_schema_version", CurrentContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", CurrentContractDigestSha256);
        writer.WriteNumber("assessed_at_unix_ms", assessedAt);
        writer.WriteNumber("expires_at_unix_ms", expiresAt);
        writer.WritePropertyName("historical_assessment");
        WriteEmbedded(writer, historical.CanonicalUtf8.Span);
        writer.WritePropertyName("current_observations"); writer.WriteStartObject();
        WriteCurrentFact(writer, "openrouter", openRouter);
        WriteCurrentFact(writer, "ollama", ollama);
        writer.WriteEndObject();
        writer.WritePropertyName("current_readiness"); writer.WriteStartObject();
        writer.WriteString("openrouter", ProjectCurrentReadiness(openRouter));
        writer.WriteString("ollama", ProjectCurrentReadiness(ollama));
        writer.WriteEndObject();
        writer.WriteString("primary_attempt_current_state", "unknown");
        writer.WriteString("routing_input_issuance_status", "not_issued");
        writer.WriteNull("routing_policy_input");
        WriteCurrentStrings(writer, "gap_codes", gaps);
        WriteCurrentStrings(writer, "claim_limitation_codes", CurrentLimitations);
        if (payloadDigest is not null) writer.WriteString("assessment_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteCurrentFact(
        Utf8JsonWriter writer,
        string name,
        ProviderRoutingCurrentObservationFact fact)
    {
        writer.WritePropertyName(name); writer.WriteStartObject();
        writer.WriteString("status", fact.Status);
        if (fact.ArtifactDigestSha256 is null) writer.WriteNull("artifact_digest_sha256");
        else writer.WriteString("artifact_digest_sha256", fact.ArtifactDigestSha256);
        writer.WritePropertyName("evidence");
        if (fact.Observation is null) writer.WriteNullValue();
        else WriteEmbedded(writer, fact.Observation.CanonicalUtf8.Span);
        writer.WriteEndObject();
    }

    private static void WriteEmbedded(Utf8JsonWriter writer, ReadOnlySpan<byte> canonical)
    {
        byte[] snapshot = canonical.ToArray();
        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot);
            document.RootElement.WriteTo(writer);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(snapshot);
        }
    }

    private static long? TryReadObservationExpiry(ReadOnlySpan<byte> bytes)
    {
        byte[] snapshot = bytes.ToArray();
        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot, new JsonDocumentOptions
            {
                MaxDepth = ProviderReadinessObservationModule.MaximumJsonDepth,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            return document.RootElement.TryGetProperty("expires_at_unix_ms", out JsonElement value)
                && value.TryGetInt64(out long parsed) ? parsed : null;
        }
        catch (JsonException) { return null; }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static void RequireCurrentObjectOrder(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new ProviderRoutingReadinessEvidenceException("assessment_shape_invalid");
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count)
            throw new ProviderRoutingReadinessEvidenceException("assessment_shape_invalid");
        for (int index = 0; index < names.Count; index++)
            if (properties[index].Name != names[index])
                throw new ProviderRoutingReadinessEvidenceException("assessment_shape_invalid");
    }

    private static string RequireCurrentString(JsonElement owner, string name, string expected) =>
        RequireCurrentClosed(owner, name, [expected]);

    private static string RequireCurrentClosed(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)
            || JsonSerializer.Serialize(text) != value.GetRawText())
            throw new ProviderRoutingReadinessEvidenceException("assessment_value_invalid");
        return text;
    }

    private static long RequireCurrentInt64(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed)
            || parsed < 0 || value.GetRawText() != parsed.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new ProviderRoutingReadinessEvidenceException("assessment_value_invalid");
        return parsed;
    }

    private static string RequireCurrentDigest(JsonElement owner, string name)
    {
        string value = RequireCurrentClosed(owner, name, [owner.GetProperty(name).GetString() ?? string.Empty]);
        if (!IsCurrentDigest(value)) throw new ProviderRoutingReadinessEvidenceException("assessment_digest_invalid");
        return value;
    }

    private static string? RequireCurrentNullableDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsCurrentDigest(text) || JsonSerializer.Serialize(text) != value.GetRawText())
            throw new ProviderRoutingReadinessEvidenceException("assessment_digest_invalid");
        return text;
    }

    private static bool IsCurrentDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireCurrentStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw new ProviderRoutingReadinessEvidenceException("assessment_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || item.GetString() != expected[index++]
                || JsonSerializer.Serialize(item.GetString()) != item.GetRawText())
                throw new ProviderRoutingReadinessEvidenceException("assessment_value_invalid");
    }

    private static byte[] CanonicalizeCurrentElement(JsonElement element)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        element.WriteTo(writer); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CanonicalizeCurrentWithoutLast(JsonElement root, string lastName)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length == 0 || properties[^1].Name != lastName)
            throw new ProviderRoutingReadinessEvidenceException("assessment_shape_invalid");
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

    private static void WriteCurrentStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name); writer.WriteStartArray();
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void RejectCurrentDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            MaxDepth = MaximumCurrentAssessmentJsonDepth,
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
                throw new ProviderRoutingReadinessEvidenceException("assessment_shape_invalid");
        }
    }
}
