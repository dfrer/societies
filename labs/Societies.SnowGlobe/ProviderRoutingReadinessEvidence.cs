using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Caller-owned canonical evidence bytes; assessment snapshots every supplied value once.</summary>
public sealed class ProviderRoutingReadinessEvidenceInput
{
    public ProviderRoutingReadinessEvidenceInput(
        ReadOnlyMemory<byte>? comparisonArtifactCanonicalUtf8,
        ReadOnlyMemory<byte>? openRouterActivationPreflightCanonicalUtf8 = null,
        ReadOnlyMemory<byte>? ollamaExecutionCanonicalUtf8 = null,
        ReadOnlyMemory<byte>? openRouterExecutionCanonicalUtf8 = null)
    {
        ComparisonArtifactCanonicalUtf8 = comparisonArtifactCanonicalUtf8;
        OpenRouterActivationPreflightCanonicalUtf8 = openRouterActivationPreflightCanonicalUtf8;
        OllamaExecutionCanonicalUtf8 = ollamaExecutionCanonicalUtf8;
        OpenRouterExecutionCanonicalUtf8 = openRouterExecutionCanonicalUtf8;
    }

    public ReadOnlyMemory<byte>? ComparisonArtifactCanonicalUtf8 { get; }
    public ReadOnlyMemory<byte>? OpenRouterActivationPreflightCanonicalUtf8 { get; }
    public ReadOnlyMemory<byte>? OllamaExecutionCanonicalUtf8 { get; }
    public ReadOnlyMemory<byte>? OpenRouterExecutionCanonicalUtf8 { get; }
}

/// <summary>Closed, raw-free classification of one validated or rejected artifact.</summary>
public sealed class ProviderRoutingReadinessEvidenceFact
{
    internal ProviderRoutingReadinessEvidenceFact(
        string status,
        string? artifactDigestSha256,
        string? schemaVersion,
        string temporalScope,
        string? detailCode)
    {
        Status = status;
        ArtifactDigestSha256 = artifactDigestSha256;
        SchemaVersion = schemaVersion;
        TemporalScope = temporalScope;
        DetailCode = detailCode;
    }

    public string Status { get; }
    public string? ArtifactDigestSha256 { get; }
    public string? SchemaVersion { get; }
    public string TemporalScope { get; }
    public string? DetailCode { get; }
}

/// <summary>Detached canonical assessment; it intentionally contains no routing-policy input.</summary>
public sealed class ProviderRoutingReadinessAssessment
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _gapCodes;
    private readonly string[] _claimLimitationCodes;

    internal ProviderRoutingReadinessAssessment(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string selectionEvidence,
        ProviderRoutingReadinessEvidenceFact comparisonEvidence,
        ProviderRoutingReadinessEvidenceFact openRouterActivationEvidence,
        ProviderRoutingReadinessEvidenceFact ollamaExecutionEvidence,
        ProviderRoutingReadinessEvidenceFact openRouterExecutionEvidence,
        IReadOnlyList<string> gapCodes,
        IReadOnlyList<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _gapCodes = gapCodes.ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        SelectionEvidence = selectionEvidence;
        ComparisonEvidence = Copy(comparisonEvidence);
        OpenRouterActivationEvidence = Copy(openRouterActivationEvidence);
        OllamaExecutionEvidence = Copy(ollamaExecutionEvidence);
        OpenRouterExecutionEvidence = Copy(openRouterExecutionEvidence);
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderRoutingReadinessEvidenceModule.SchemaVersion;
    public string Status => "insufficient_current_readiness_evidence";
    public string SelectionEvidence { get; }
    public ProviderRoutingReadinessEvidenceFact ComparisonEvidence { get; }
    public ProviderRoutingReadinessEvidenceFact OpenRouterActivationEvidence { get; }
    public ProviderRoutingReadinessEvidenceFact OllamaExecutionEvidence { get; }
    public ProviderRoutingReadinessEvidenceFact OpenRouterExecutionEvidence { get; }
    public string OpenRouterCurrentReadiness => "unknown";
    public string OllamaCurrentReadiness => "unknown";
    public string PrimaryAttemptCurrentState => "unknown";
    public string RoutingInputIssuanceStatus => "not_issued";
    public IReadOnlyList<string> GapCodes => Array.AsReadOnly(_gapCodes.ToArray());
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();

    private static ProviderRoutingReadinessEvidenceFact Copy(ProviderRoutingReadinessEvidenceFact value) =>
        new(value.Status, value.ArtifactDigestSha256, value.SchemaVersion, value.TemporalScope, value.DetailCode);
}

/// <summary>Closed, non-echoing validation failure for untrusted assessment bytes.</summary>
public sealed class ProviderRoutingReadinessEvidenceException : Exception
{
    internal ProviderRoutingReadinessEvidenceException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "assessment_size_invalid" or "assessment_utf8_invalid" or "assessment_json_invalid" or
        "assessment_shape_invalid" or "assessment_value_invalid" or "assessment_noncanonical" or
        "assessment_digest_invalid" or "assessment_payload_digest_invalid" or "assessment_binding_invalid" => code,
        _ => "assessment_validation_failed"
    };
}

/// <summary>Pure synchronous classifier of historical evidence; it cannot issue a routing input.</summary>
public static class ProviderRoutingReadinessEvidenceModule
{
    public const string SchemaVersion = "snow_globe_provider_routing_readiness_assessment/v1";
    public const string ContractSchemaVersion = "snow_globe_provider_routing_readiness_evidence/v1";
    public const int MaximumAssessmentBytes = 8 * 1024;
    public const int MaximumAssessmentJsonDepth = 6;

    public static string ContractDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        "snow_globe_provider_routing_readiness_evidence/v1|comparison_schema=" + CognitionQualityComparisonModule.SchemaVersion +
        "|comparison_digest=" + ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256 +
        "|comparison=required_validated_exact_accepted_openrouter_default_selection_only|" +
        "openrouter_activation=optional_validated_evaluated_eligibility_only_live_traffic_disabled|" +
        "ollama_execution=optional_validated_historical_compatibility_only|" +
        "openrouter_execution=optional_validated_historical_generation_only|" +
        "missing=unknown_not_negative|current_openrouter_readiness=unknown|current_ollama_readiness=unknown|" +
        "primary_attempt_current_state=unknown|routing_policy_input=never_issued_v1|caller_inputs=single_owned_snapshot_zeroed|" +
        "execution_authority=none"));

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "contract_schema_version", "contract_digest_sha256", "selection_evidence",
        "comparison_evidence", "openrouter_activation_evidence", "ollama_execution_evidence", "openrouter_execution_evidence",
        "current_readiness", "primary_attempt_current_state", "routing_input_issuance_status", "routing_policy_input",
        "gap_codes", "claim_limitation_codes", "assessment_payload_digest_sha256"
    ];
    private static readonly string[] EvidenceNames =
    [
        "status", "artifact_digest_sha256", "schema_version", "temporal_scope", "detail_code"
    ];
    private static readonly string[] CurrentReadinessNames = ["openrouter", "ollama"];
    private static readonly string[] ComparisonStatuses = ["accepted", "missing", "malformed", "unsupported"];
    private static readonly string[] ActivationStatuses =
        ["missing", "malformed", "evaluated_eligible_live_traffic_disabled", "evaluated_ineligible"];
    private static readonly string[] OllamaStatuses =
        ["missing", "malformed", "historical_compatibility_complete", "historical_execution_terminal"];
    private static readonly string[] OpenRouterStatuses =
        ["missing", "malformed", "historical_generation_complete", "historical_generation_terminal"];
    private static readonly string[] ComparisonDetails =
        ["openrouter_default", "ollama_default", "conditional_routing", "insufficient_evidence"];
    private static readonly string[] ActivationDetails = ["claimed_eligible", "claimed_ineligible"];
    private static readonly string[] ExecutionDetails = ["complete", "terminal"];
    private static readonly string[] TemporalScopes =
        ["none", "selection_only", "evaluated_eligibility_only", "historical_only", "historical_generation_only"];
    private static readonly string[] ClaimLimitations =
    [
        "assessment_only_no_routing_input_or_execution_authority",
        "selection_evidence_does_not_prove_readiness",
        "past_ollama_success_does_not_prove_current_readiness",
        "activation_eligibility_does_not_enable_live_traffic",
        "historical_openrouter_generation_does_not_prove_new_attempt_state",
        "missing_evidence_is_unknown_not_negative",
        "digests_provide_integrity_not_authenticity",
        "no_provider_credential_account_network_payment_or_world_authority"
    ];

    public static ProviderRoutingReadinessAssessment Assess(ProviderRoutingReadinessEvidenceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ProviderRoutingReadinessEvidenceFact comparison = AssessComparison(input.ComparisonArtifactCanonicalUtf8);
        ProviderRoutingReadinessEvidenceFact activation = AssessActivation(input.OpenRouterActivationPreflightCanonicalUtf8);
        ProviderRoutingReadinessEvidenceFact ollama = AssessOllama(input.OllamaExecutionCanonicalUtf8);
        ProviderRoutingReadinessEvidenceFact openRouter = AssessOpenRouter(input.OpenRouterExecutionCanonicalUtf8);
        return Build(comparison, activation, ollama, openRouter);
    }

    public static ProviderRoutingReadinessAssessment Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumAssessmentBytes) throw Failure("assessment_size_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            try { _ = StrictUtf8.GetString(snapshot); }
            catch (DecoderFallbackException) { throw Failure("assessment_utf8_invalid"); }

            JsonDocument document;
            try
            {
                RejectDuplicateProperties(snapshot);
                Utf8JsonReader reader = new(snapshot, new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumAssessmentJsonDepth
                });
                document = JsonDocument.ParseValue(ref reader);
                if (reader.Read()) { document.Dispose(); throw Failure("assessment_json_invalid"); }
            }
            catch (ProviderRoutingReadinessEvidenceException) { throw; }
            catch (JsonException) { throw Failure("assessment_json_invalid"); }

            using (document)
            {
                JsonElement root = document.RootElement;
                RequireObjectAndOrder(root, RootNames);
                RequireCanonicalScalars(root);
                RequireString(root, "schema_version", SchemaVersion);
                RequireString(root, "status", "insufficient_current_readiness_evidence");
                RequireString(root, "contract_schema_version", ContractSchemaVersion);
                RequireString(root, "contract_digest_sha256", ContractDigestSha256);
                string selection = RequireClosedString(root, "selection_evidence", ["accepted_openrouter_default", "unaccepted"]);
                ProviderRoutingReadinessEvidenceFact comparison = ParseFact(
                    root.GetProperty("comparison_evidence"), ComparisonStatuses, ComparisonDetails);
                ProviderRoutingReadinessEvidenceFact activation = ParseFact(
                    root.GetProperty("openrouter_activation_evidence"), ActivationStatuses, ActivationDetails);
                ProviderRoutingReadinessEvidenceFact ollama = ParseFact(
                    root.GetProperty("ollama_execution_evidence"), OllamaStatuses, ExecutionDetails);
                ProviderRoutingReadinessEvidenceFact openRouter = ParseFact(
                    root.GetProperty("openrouter_execution_evidence"), OpenRouterStatuses, ExecutionDetails);
                ValidateFactBindings(comparison, activation, ollama, openRouter);
                if (selection != Selection(comparison)) throw Failure("assessment_binding_invalid");

                JsonElement current = root.GetProperty("current_readiness");
                RequireObjectAndOrder(current, CurrentReadinessNames);
                RequireString(current, "openrouter", "unknown");
                RequireString(current, "ollama", "unknown");
                RequireString(root, "primary_attempt_current_state", "unknown");
                RequireString(root, "routing_input_issuance_status", "not_issued");
                if (root.GetProperty("routing_policy_input").ValueKind != JsonValueKind.Null)
                    throw Failure("assessment_binding_invalid");
                RequireExactStrings(root.GetProperty("gap_codes"), Gaps(comparison, activation, ollama, openRouter));
                RequireExactStrings(root.GetProperty("claim_limitation_codes"), ClaimLimitations);
                string payloadDigest = RequireDigest(root, "assessment_payload_digest_sha256");
                byte[] payload = CanonicalizeWithoutLast(root, "assessment_payload_digest_sha256");
                try
                {
                    if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal))
                        throw Failure("assessment_payload_digest_invalid");
                }
                finally { CryptographicOperations.ZeroMemory(payload); }

                ProviderRoutingReadinessAssessment recreated = Build(comparison, activation, ollama, openRouter);
                if (!recreated.CanonicalUtf8.Span.SequenceEqual(snapshot)) throw Failure("assessment_noncanonical");
                return recreated;
            }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingReadinessEvidenceFact AssessComparison(ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0) return Missing();
        if (canonical.Value.Length > CognitionQualityComparisonModule.MaximumArtifactBytes) return Malformed();
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            CognitionQualityComparisonArtifact artifact;
            try { artifact = CognitionQualityComparisonModule.Validate(snapshot); }
            catch (CognitionQualityComparisonException) { return Malformed(digest); }
            bool accepted = digest == ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256
                && artifact.Status == "complete"
                && artifact.Recommendation == "openrouter_default"
                && artifact.Providers.Count == 2
                && artifact.Providers.All(provider => provider.EvidenceStatus == "complete");
            return new(
                accepted ? "accepted" : "unsupported",
                digest,
                artifact.SchemaVersion,
                "selection_only",
                artifact.Recommendation);
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingReadinessEvidenceFact AssessActivation(ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0) return Missing();
        if (canonical.Value.Length > OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes) return Malformed();
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            OpenRouterPremiumActivationPreflightArtifact artifact;
            try { artifact = OpenRouterPremiumActivationPreflightArtifactModule.Validate(snapshot); }
            catch (OpenRouterPremiumEvidenceException) { return Malformed(digest); }
            return new(
                artifact.ClaimedEligible ? "evaluated_eligible_live_traffic_disabled" : "evaluated_ineligible",
                digest,
                artifact.SchemaVersion,
                "evaluated_eligibility_only",
                artifact.ClaimedEligible ? "claimed_eligible" : "claimed_ineligible");
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingReadinessEvidenceFact AssessOllama(ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0) return Missing();
        if (canonical.Value.Length > OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes) return Malformed();
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            OllamaRecordingExecutionArtifact artifact;
            try { artifact = OllamaRecordingExecutionArtifactModule.Validate(snapshot); }
            catch (OllamaRecordingExecutionArtifactException) { return Malformed(digest); }
            bool complete = artifact.OutcomeCode == "Complete"
                && artifact.RecordingResultPresent
                && artifact.RecordingOutcomeCode == "Complete"
                && artifact.CompletedSlotCount == CognitionQualityCorpusV1.ScenarioCount
                && artifact.ReceiptPresent;
            return new(
                complete ? "historical_compatibility_complete" : "historical_execution_terminal",
                digest,
                artifact.SchemaVersion,
                "historical_only",
                complete ? "complete" : "terminal");
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingReadinessEvidenceFact AssessOpenRouter(ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue || canonical.Value.Length == 0) return Missing();
        if (canonical.Value.Length > OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes) return Malformed();
        byte[] snapshot = canonical.Value.Span.ToArray();
        try
        {
            string digest = CognitionQualityHash.Sha256(snapshot);
            OpenRouterPremiumEvidenceArtifact artifact;
            try { artifact = OpenRouterPremiumEvidenceArtifactModule.Validate(snapshot); }
            catch (OpenRouterPremiumEvidenceException) { return Malformed(digest); }
            bool complete = artifact.Status == "complete";
            return new(
                complete ? "historical_generation_complete" : "historical_generation_terminal",
                digest,
                artifact.SchemaVersion,
                "historical_generation_only",
                complete ? "complete" : "terminal");
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static ProviderRoutingReadinessAssessment Build(
        ProviderRoutingReadinessEvidenceFact comparison,
        ProviderRoutingReadinessEvidenceFact activation,
        ProviderRoutingReadinessEvidenceFact ollama,
        ProviderRoutingReadinessEvidenceFact openRouter)
    {
        ValidateFactBindings(comparison, activation, ollama, openRouter);
        string selection = Selection(comparison);
        string[] gaps = Gaps(comparison, activation, ollama, openRouter);
        byte[] payload = Write(selection, comparison, activation, ollama, openRouter, gaps, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(selection, comparison, activation, ollama, openRouter, gaps, payloadDigest);
        if (canonical.Length is < 1 or > MaximumAssessmentBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("assessment_size_invalid");
        }
        return new(canonical, payloadDigest, selection, comparison, activation, ollama, openRouter, gaps, ClaimLimitations);
    }

    private static string Selection(ProviderRoutingReadinessEvidenceFact comparison) =>
        comparison.Status == "accepted" ? "accepted_openrouter_default" : "unaccepted";

    private static string[] Gaps(
        ProviderRoutingReadinessEvidenceFact comparison,
        ProviderRoutingReadinessEvidenceFact activation,
        ProviderRoutingReadinessEvidenceFact ollama,
        ProviderRoutingReadinessEvidenceFact openRouter)
    {
        List<string> gaps = [];
        if (comparison.Status != "accepted") gaps.Add("accepted_comparison_evidence_unproven");
        AddOptionalGap(gaps, activation.Status, "openrouter_activation_evidence");
        AddOptionalGap(gaps, ollama.Status, "ollama_execution_evidence");
        AddOptionalGap(gaps, openRouter.Status, "openrouter_execution_evidence");
        gaps.Add("current_openrouter_authenticated_readiness_unproven");
        gaps.Add("current_ollama_runtime_readiness_unproven");
        gaps.Add("authenticated_attempt_bound_primary_state_unproven");
        gaps.Add("freshness_current_observation_unproven");
        return gaps.ToArray();
    }

    private static void AddOptionalGap(List<string> gaps, string status, string prefix)
    {
        if (status == "missing") gaps.Add(prefix + "_missing");
        else if (status == "malformed") gaps.Add(prefix + "_malformed");
    }

    private static ProviderRoutingReadinessEvidenceFact Missing() => new("missing", null, null, "none", null);
    private static ProviderRoutingReadinessEvidenceFact Malformed(string? digest = null) => new("malformed", digest, null, "none", null);

    private static void ValidateFactBindings(
        ProviderRoutingReadinessEvidenceFact comparison,
        ProviderRoutingReadinessEvidenceFact activation,
        ProviderRoutingReadinessEvidenceFact ollama,
        ProviderRoutingReadinessEvidenceFact openRouter)
    {
        bool comparisonValid = comparison.Status switch
        {
            "missing" => IsAbsent(comparison),
            "malformed" => IsMalformed(comparison)
                && comparison.ArtifactDigestSha256 != ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            "accepted" => IsValid(comparison, CognitionQualityComparisonModule.SchemaVersion, "selection_only", ComparisonDetails)
                && comparison.ArtifactDigestSha256 == ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256
                && comparison.DetailCode == "openrouter_default",
            "unsupported" => IsValid(comparison, CognitionQualityComparisonModule.SchemaVersion, "selection_only", ComparisonDetails)
                && comparison.ArtifactDigestSha256 != ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            _ => false
        };
        bool activationValid = activation.Status switch
        {
            "missing" => IsAbsent(activation),
            "malformed" => IsMalformed(activation),
            "evaluated_eligible_live_traffic_disabled" => IsValid(activation,
                OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion, "evaluated_eligibility_only", ["claimed_eligible"]),
            "evaluated_ineligible" => IsValid(activation,
                OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion, "evaluated_eligibility_only", ["claimed_ineligible"]),
            _ => false
        };
        bool ollamaValid = ollama.Status switch
        {
            "missing" => IsAbsent(ollama),
            "malformed" => IsMalformed(ollama),
            "historical_compatibility_complete" => IsValid(ollama, null, "historical_only", ["complete"])
                && IsOllamaSchema(ollama.SchemaVersion),
            "historical_execution_terminal" => IsValid(ollama, null, "historical_only", ["terminal"])
                && IsOllamaSchema(ollama.SchemaVersion),
            _ => false
        };
        bool openRouterValid = openRouter.Status switch
        {
            "missing" => IsAbsent(openRouter),
            "malformed" => IsMalformed(openRouter),
            "historical_generation_complete" => IsValid(openRouter,
                OpenRouterPremiumEvidenceArtifactModule.SchemaVersion, "historical_generation_only", ["complete"]),
            "historical_generation_terminal" => IsValid(openRouter,
                OpenRouterPremiumEvidenceArtifactModule.SchemaVersion, "historical_generation_only", ["terminal"]),
            _ => false
        };
        if (!comparisonValid || !activationValid || !ollamaValid || !openRouterValid)
            throw Failure("assessment_binding_invalid");
    }

    private static bool IsAbsent(ProviderRoutingReadinessEvidenceFact fact) =>
        fact.ArtifactDigestSha256 is null && fact.SchemaVersion is null && fact.TemporalScope == "none" && fact.DetailCode is null;

    private static bool IsMalformed(ProviderRoutingReadinessEvidenceFact fact) =>
        (fact.ArtifactDigestSha256 is null || IsDigest(fact.ArtifactDigestSha256))
        && fact.SchemaVersion is null && fact.TemporalScope == "none" && fact.DetailCode is null;

    private static bool IsValid(
        ProviderRoutingReadinessEvidenceFact fact,
        string? expectedSchema,
        string temporalScope,
        IReadOnlyList<string> details) =>
        IsDigest(fact.ArtifactDigestSha256)
        && (expectedSchema is null || fact.SchemaVersion == expectedSchema)
        && fact.SchemaVersion is not null
        && fact.TemporalScope == temporalScope
        && fact.DetailCode is not null
        && details.Contains(fact.DetailCode, StringComparer.Ordinal);

    private static bool IsOllamaSchema(string? schema) => schema is
        OllamaRecordingExecutionArtifactModule.SchemaVersion or
        OllamaRecordingExecutionArtifactModule.PreviousSchemaVersion or
        OllamaRecordingExecutionArtifactModule.LegacySchemaVersion;

    private static byte[] Write(
        string selection,
        ProviderRoutingReadinessEvidenceFact comparison,
        ProviderRoutingReadinessEvidenceFact activation,
        ProviderRoutingReadinessEvidenceFact ollama,
        ProviderRoutingReadinessEvidenceFact openRouter,
        IReadOnlyList<string> gaps,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "insufficient_current_readiness_evidence");
        writer.WriteString("contract_schema_version", ContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", ContractDigestSha256);
        writer.WriteString("selection_evidence", selection);
        WriteFact(writer, "comparison_evidence", comparison);
        WriteFact(writer, "openrouter_activation_evidence", activation);
        WriteFact(writer, "ollama_execution_evidence", ollama);
        WriteFact(writer, "openrouter_execution_evidence", openRouter);
        writer.WritePropertyName("current_readiness"); writer.WriteStartObject();
        writer.WriteString("openrouter", "unknown"); writer.WriteString("ollama", "unknown"); writer.WriteEndObject();
        writer.WriteString("primary_attempt_current_state", "unknown");
        writer.WriteString("routing_input_issuance_status", "not_issued");
        writer.WriteNull("routing_policy_input");
        WriteStrings(writer, "gap_codes", gaps);
        WriteStrings(writer, "claim_limitation_codes", ClaimLimitations);
        if (payloadDigest is not null) writer.WriteString("assessment_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void WriteFact(Utf8JsonWriter writer, string name, ProviderRoutingReadinessEvidenceFact fact)
    {
        writer.WritePropertyName(name); writer.WriteStartObject();
        writer.WriteString("status", fact.Status);
        WriteNullableString(writer, "artifact_digest_sha256", fact.ArtifactDigestSha256);
        WriteNullableString(writer, "schema_version", fact.SchemaVersion);
        writer.WriteString("temporal_scope", fact.TemporalScope);
        WriteNullableString(writer, "detail_code", fact.DetailCode);
        writer.WriteEndObject();
    }

    private static ProviderRoutingReadinessEvidenceFact ParseFact(
        JsonElement value,
        IReadOnlyList<string> statuses,
        IReadOnlyList<string> details)
    {
        RequireObjectAndOrder(value, EvidenceNames);
        string status = RequireClosedString(value, "status", statuses);
        string? digest = RequireNullableDigest(value, "artifact_digest_sha256");
        string? schema = RequireNullableClosedString(value, "schema_version",
        [
            CognitionQualityComparisonModule.SchemaVersion,
            OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion,
            OllamaRecordingExecutionArtifactModule.SchemaVersion,
            OllamaRecordingExecutionArtifactModule.PreviousSchemaVersion,
            OllamaRecordingExecutionArtifactModule.LegacySchemaVersion,
            OpenRouterPremiumEvidenceArtifactModule.SchemaVersion
        ]);
        string temporal = RequireClosedString(value, "temporal_scope", TemporalScopes);
        string? detail = RequireNullableClosedString(value, "detail_code", details);
        return new(status, digest, schema, temporal, detail);
    }

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure("assessment_shape_invalid");
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure("assessment_shape_invalid");
        for (int index = 0; index < names.Count; index++)
            if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal))
                throw Failure("assessment_shape_invalid");
    }

    private static string RequireClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) throw Failure("assessment_value_invalid");
        return text;
    }

    private static string? RequireNullableClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) throw Failure("assessment_value_invalid");
        return text;
    }

    private static void RequireString(JsonElement owner, string name, string expected) =>
        _ = RequireClosedString(owner, name, [expected]);

    private static string RequireDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsDigest(text)) throw Failure("assessment_digest_invalid");
        return text!;
    }

    private static string? RequireNullableDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsDigest(text)) throw Failure("assessment_digest_invalid");
        return text;
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireExactStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw Failure("assessment_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index++], StringComparison.Ordinal))
                throw Failure("assessment_value_invalid");
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
            throw Failure("assessment_noncanonical");
        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            throw Failure("assessment_noncanonical");
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement root, string lastName)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length == 0 || properties[^1].Name != lastName) throw Failure("assessment_shape_invalid");
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^1])
        {
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            MaxDepth = MaximumAssessmentJsonDepth,
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
                throw Failure("assessment_shape_invalid");
        }
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name); else writer.WriteString(name, value);
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name); writer.WriteStartArray();
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static ProviderRoutingReadinessEvidenceException Failure(string code) => new(code);
}
