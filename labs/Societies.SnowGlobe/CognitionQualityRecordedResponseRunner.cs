using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>One caller-supplied, offline response fixture bound to a frozen cognition-quality scenario.</summary>
public sealed class CognitionQualityRecordedResponseFixture
{
    private readonly byte[] _responseUtf8;

    public CognitionQualityRecordedResponseFixture(string scenarioId, string observationDigestSha256, ReadOnlyMemory<byte> responseUtf8)
    {
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        _responseUtf8 = responseUtf8.ToArray();
    }

    public string ScenarioId { get; }
    public string ObservationDigestSha256 { get; }
    /// <summary>A detached copy is returned so the fixture cannot expose its retained backing array.</summary>
    public ReadOnlyMemory<byte> ResponseUtf8 => _responseUtf8.ToArray();
    internal byte[] CopyResponseUtf8() => _responseUtf8.ToArray();
    /// <summary>Clears a temporary module-owned fixture once its detached run has been produced.</summary>
    internal void ClearResponseUtf8() => CryptographicOperations.ZeroMemory(_responseUtf8);
}

/// <summary>Detached, raw-response-free binding and parse result for one frozen scenario.</summary>
public sealed class CognitionQualityRecordedResponseBinding
{
    internal CognitionQualityRecordedResponseBinding(string scenarioId, string observationDigestSha256, int responseByteCount, string responseDigestSha256, string parseOutcome)
    {
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        ResponseByteCount = responseByteCount;
        ResponseDigestSha256 = responseDigestSha256;
        ParseOutcome = parseOutcome;
    }

    public string ScenarioId { get; }
    public string ObservationDigestSha256 { get; }
    public int ResponseByteCount { get; }
    public string ResponseDigestSha256 { get; }
    public string ParseOutcome { get; }
}

/// <summary>Detached immutable result of converting exactly one frozen batch of offline response fixtures.</summary>
public sealed class CognitionQualityRecordedResponseRun
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityRecordedResponseBinding[] _responseBindings;
    private readonly CognitionQualitySubmission[] _proposalBatch;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityRecordedResponseRun(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        IReadOnlyList<CognitionQualityRecordedResponseBinding> responseBindings,
        IReadOnlyList<CognitionQualitySubmission> proposalBatch,
        CognitionQualityExecutionEvidence executionEvidence,
        IEnumerable<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        _responseBindings = responseBindings.Select(binding => new CognitionQualityRecordedResponseBinding(binding.ScenarioId, binding.ObservationDigestSha256, binding.ResponseByteCount, binding.ResponseDigestSha256, binding.ParseOutcome)).ToArray();
        _proposalBatch = proposalBatch.Select(submission => new CognitionQualitySubmission(submission.ScenarioId, submission.Proposal is null ? null : submission.Proposal with { })).ToArray();
        ExecutionEvidence = executionEvidence;
        _claimLimitationCodes = claimLimitationCodes.ToArray();
    }

    public string SchemaVersion => CognitionQualityRecordedResponseRunnerModule.SchemaVersion;
    public string Status => "complete";
    public string Semantics => "offline_recorded_response_conversion_only";
    public string RunnerIdentity => CognitionQualityRecordedResponseRunnerModule.RunnerIdentity;
    public string ParserIdentity => CognitionQualityRecordedResponseRunnerModule.ParserIdentity;
    public IReadOnlyList<CognitionQualityRecordedResponseBinding> ResponseBindings => Array.AsReadOnly(_responseBindings.Select(binding => new CognitionQualityRecordedResponseBinding(binding.ScenarioId, binding.ObservationDigestSha256, binding.ResponseByteCount, binding.ResponseDigestSha256, binding.ParseOutcome)).ToArray());
    public IReadOnlyList<CognitionQualitySubmission> ProposalBatch => Array.AsReadOnly(_proposalBatch.Select(submission => new CognitionQualitySubmission(submission.ScenarioId, submission.Proposal is null ? null : submission.Proposal with { })).ToArray());
    public CognitionQualityExecutionEvidence ExecutionEvidence { get; }
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

/// <summary>Closed error surface for envelope and integrity failures. Caller values are never echoed.</summary>
public sealed class CognitionQualityRecordedResponseRunnerException : Exception
{
    internal CognitionQualityRecordedResponseRunnerException(string code) : base(code)
    {
        if (!CognitionQualityRecordedResponseRunnerErrors.IsAllowlisted(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Pure synchronous conversion of exact recorded bytes into one evidence-bound offline corpus submission.</summary>
public static class CognitionQualityRecordedResponseRunnerModule
{
    public const string SchemaVersion = "snow_globe_cognition_quality_recorded_response_run/v1";
    public const string RunnerIdentity = "snow_globe_cognition_quality_recorded_response_runner/v1";
    public const string ProposalSchemaVersion = "snow_globe_cognition_quality_proposal_response/v1";
    public const string ParserIdentity = "snow_globe_cognition_quality_recorded_response_parser/v1";
    public const int MaximumResponseBytes = 1024;
    public const int MaximumAggregateResponseBytes = 12 * MaximumResponseBytes;
    public const int MaximumRunBytes = 96 * 1024;

    private static readonly string[] ClaimLimitations =
    [
        "identity_attribution_is_caller_attested",
        "fixture_binding_is_caller_attested",
        "no_execution_attestation",
        "offline_conversion_only",
        "invalid_response_maps_to_no_proposal",
        "raw_response_not_retained",
        "no_provider_status_retry_or_charge_evidence",
        "offline_fixed_corpus",
        "observable_action_utility_only",
        "no_general_quality_claim",
        "no_cost_claim",
        "no_winner_claim"
    ];

    public static CognitionQualityRecordedResponseRun Run(CognitionQualityExecutionProvenance provenance, IReadOnlyList<CognitionQualityRecordedResponseFixture> fixtures)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(fixtures);
        if (!string.Equals(provenance.ProposalSchemaVersion, ProposalSchemaVersion, StringComparison.Ordinal))
            throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.ProposalSchemaInvalid);
        if (fixtures.Count != CognitionQualityCorpusV1.ScenarioCount)
            throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.FixtureCountInvalid);

        CognitionQualityCorpusSnapshot corpus;
        try { corpus = CognitionQualityCorpusV1.CreateSnapshot(); }
        catch (CognitionQualityException) { throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.CorpusInvalid); }

        FrozenFixture[] frozen = new FrozenFixture[CognitionQualityCorpusV1.ScenarioCount];
        try
        {
            int aggregateByteCount = 0;
            IReadOnlyList<CognitionQualityScenario> scenarios = corpus.Scenarios;
            for (int index = 0; index < frozen.Length; index++)
            {
                CognitionQualityRecordedResponseFixture fixture = fixtures[index] ?? throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.FixtureInvalid);
                CognitionQualityScenario scenario = scenarios[index];
                if (!string.Equals(fixture.ScenarioId, scenario.ScenarioId, StringComparison.Ordinal))
                    throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.FixtureOrderInvalid);
                if (!string.Equals(fixture.ObservationDigestSha256, scenario.ObservationDigestSha256, StringComparison.Ordinal))
                    throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.FixtureObservationInvalid);

                byte[] response = fixture.CopyResponseUtf8();
                if (response.Length is < 1 or > MaximumResponseBytes)
                {
                    CryptographicOperations.ZeroMemory(response);
                    throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.ResponseSizeInvalid);
                }
                aggregateByteCount = checked(aggregateByteCount + response.Length);
                if (aggregateByteCount > MaximumAggregateResponseBytes)
                {
                    CryptographicOperations.ZeroMemory(response);
                    throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.ResponseAggregateSizeInvalid);
                }
                frozen[index] = new FrozenFixture(scenario.ScenarioId, scenario.ObservationDigestSha256, response);
            }

            CognitionQualityRecordedResponseBinding[] bindings = new CognitionQualityRecordedResponseBinding[frozen.Length];
            CognitionQualitySubmission[] proposals = new CognitionQualitySubmission[frozen.Length];
            for (int index = 0; index < frozen.Length; index++)
            {
                FrozenFixture fixture = frozen[index];
                CognitionQualityProposalResponseParseResult parsed = CognitionQualityProposalResponseContract.Parse(fixture.ResponseUtf8);
                bindings[index] = new CognitionQualityRecordedResponseBinding(fixture.ScenarioId, fixture.ObservationDigestSha256, fixture.ResponseUtf8.Length, CognitionQualityHash.Sha256(fixture.ResponseUtf8), parsed.Outcome);
                proposals[index] = new CognitionQualitySubmission(fixture.ScenarioId, parsed.Proposal is null ? null : parsed.Proposal with { });
            }

            CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(provenance, proposals);
            VerifyCoherence(corpus, provenance, bindings, proposals, evidence);
            byte[] payload = WriteRun(provenance, corpus, bindings, evidence, null);
            string payloadDigest = CognitionQualityHash.Sha256(payload);
            CryptographicOperations.ZeroMemory(payload);
            byte[] canonical = WriteRun(provenance, corpus, bindings, evidence, payloadDigest);
            if (canonical.Length is 0 or > MaximumRunBytes)
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.RunSizeInvalid);
            }
            return new CognitionQualityRecordedResponseRun(canonical, payloadDigest, bindings, proposals, evidence, ClaimLimitations);
        }
        finally
        {
            foreach (FrozenFixture? fixture in frozen)
            {
                if (fixture is not null) fixture.Zero();
            }
        }
    }

    private static void VerifyCoherence(CognitionQualityCorpusSnapshot corpus, CognitionQualityExecutionProvenance provenance, IReadOnlyList<CognitionQualityRecordedResponseBinding> bindings, IReadOnlyList<CognitionQualitySubmission> proposals, CognitionQualityExecutionEvidence evidence)
    {
        if (!string.Equals(corpus.CanonicalDigestSha256, CognitionQualityCorpusV1.ExpectedManifestDigestSha256, StringComparison.Ordinal)
            || !string.Equals(provenance.ProvenanceDigestSha256, evidence.Provenance.ProvenanceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(provenance.PromptRevision, evidence.Provenance.PromptRevision, StringComparison.Ordinal)
            || !string.Equals(provenance.ProposalSchemaVersion, ProposalSchemaVersion, StringComparison.Ordinal)
            || bindings.Count != CognitionQualityCorpusV1.ScenarioCount
            || proposals.Count != CognitionQualityCorpusV1.ScenarioCount
            || evidence.ScenarioCount != CognitionQualityCorpusV1.ScenarioCount
            || !string.Equals(evidence.QualityContract.CorpusDigestSha256, corpus.CanonicalDigestSha256, StringComparison.Ordinal))
        {
            throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.IntegrityInvalid);
        }

        byte[] submission = CognitionQualityCorpusV1.WriteSubmissionEnvelope(proposals);
        try
        {
            if (!string.Equals(evidence.QualityContract.SubmissionDigestSha256, CognitionQualityHash.Sha256(submission), StringComparison.Ordinal)
                || !string.Equals(evidence.RecordedSubmissionCanonicalJson, Encoding.UTF8.GetString(submission), StringComparison.Ordinal)
                || evidence.CanonicalUtf8.Length is < 1 or > CognitionQualityExecutionEvidenceModule.MaximumEvidenceBytes)
            {
                throw new CognitionQualityRecordedResponseRunnerException(CognitionQualityRecordedResponseRunnerErrors.IntegrityInvalid);
            }
        }
        finally { CryptographicOperations.ZeroMemory(submission); }
    }

    private static byte[] WriteRun(CognitionQualityExecutionProvenance provenance, CognitionQualityCorpusSnapshot corpus, IReadOnlyList<CognitionQualityRecordedResponseBinding> bindings, CognitionQualityExecutionEvidence evidence, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("semantics", "offline_recorded_response_conversion_only");
        writer.WriteString("runner_identity", RunnerIdentity);
        writer.WriteString("parser_identity", ParserIdentity);
        writer.WriteString("corpus_digest_sha256", corpus.CanonicalDigestSha256);
        writer.WriteString("provenance_digest_sha256", provenance.ProvenanceDigestSha256);
        writer.WriteString("prompt_revision", provenance.PromptRevision);
        writer.WriteString("proposal_schema_version", provenance.ProposalSchemaVersion);
        writer.WriteNumber("scenario_count", bindings.Count);
        writer.WritePropertyName("response_bindings");
        writer.WriteStartArray();
        foreach (CognitionQualityRecordedResponseBinding binding in bindings)
        {
            writer.WriteStartObject();
            writer.WriteString("scenario_id", binding.ScenarioId);
            writer.WriteString("observation_digest_sha256", binding.ObservationDigestSha256);
            writer.WriteNumber("response_byte_count", binding.ResponseByteCount);
            writer.WriteString("response_digest_sha256", binding.ResponseDigestSha256);
            writer.WriteString("parse_outcome", binding.ParseOutcome);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("execution_evidence"); writer.WriteRawValue(evidence.CanonicalUtf8.Span, skipInputValidation: false);
        writer.WritePropertyName("claim_limitation_codes");
        writer.WriteStartArray(); foreach (string code in ClaimLimitations) writer.WriteStringValue(code); writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("run_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private sealed class FrozenFixture
    {
        internal FrozenFixture(string scenarioId, string observationDigestSha256, byte[] responseUtf8) { ScenarioId = scenarioId; ObservationDigestSha256 = observationDigestSha256; ResponseUtf8 = responseUtf8; }
        internal string ScenarioId { get; }
        internal string ObservationDigestSha256 { get; }
        internal byte[] ResponseUtf8 { get; }
        internal void Zero() { if (ResponseUtf8 is not null) CryptographicOperations.ZeroMemory(ResponseUtf8); }
    }

}

internal sealed record CognitionQualityProposalResponseParseResult(SnowGlobeActionProposal? Proposal, string Outcome);

/// <summary>The one deterministic parser for the shared proposal-response/v1 payload contract.</summary>
internal static class CognitionQualityProposalResponseContract
{
    internal const string SchemaVersion = CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion;

    internal static CognitionQualityProposalResponseParseResult Parse(ReadOnlySpan<byte> responseUtf8)
    {
        try { _ = new UTF8Encoding(false, true).GetString(responseUtf8); }
        catch (DecoderFallbackException) { return new(null, "response_utf8_invalid"); }

        try
        {
            Utf8JsonReader depthReader = new(responseUtf8, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 128 });
            while (depthReader.Read())
            {
                if (depthReader.CurrentDepth >= 8) return new(null, "response_json_too_deep");
            }
        }
        catch (JsonException) { return new(null, "response_json_invalid"); }

        try
        {
            Utf8JsonReader reader = new(responseUtf8, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 128 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return new(null, "response_shape_invalid");

            string? agentId = null;
            string? actionText = null;
            int? quantity = null;
            HashSet<string> names = new(StringComparer.Ordinal);
            while (true)
            {
                if (!reader.Read()) return new(null, "response_json_invalid");
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) return new(null, "response_shape_invalid");
                string propertyName = reader.GetString()!;
                if (!names.Add(propertyName)) return new(null, "response_json_duplicate_property");
                if (propertyName is not "agent_id" and not "action" and not "quantity") return new(null, "response_shape_invalid");
                if (!reader.Read()) return new(null, "response_json_invalid");
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.EndObject or JsonTokenType.EndArray or JsonTokenType.PropertyName)
                    return new(null, "response_shape_invalid");

                if (propertyName == "agent_id")
                {
                    if (reader.TokenType != JsonTokenType.String) return new(null, "response_shape_invalid");
                    agentId = reader.GetString();
                }
                else if (propertyName == "action")
                {
                    if (reader.TokenType != JsonTokenType.String) return new(null, "response_shape_invalid");
                    actionText = reader.GetString();
                }
                else
                {
                    if (reader.TokenType != JsonTokenType.Number) return new(null, "response_shape_invalid");
                    if (!reader.TryGetInt32(out int parsedQuantity)) return new(null, "response_content_invalid");
                    quantity = parsedQuantity;
                }
            }
            if (names.Count != 3 || agentId is null || actionText is null || quantity is null || reader.Read()) return new(null, "response_shape_invalid");
            if (!IsCanonicalAgentId(agentId) || !SnowGlobeRunStore.TryParseCanonicalAction(actionText, out SnowGlobeActionKind action))
                return new(null, "response_content_invalid");
            return new(new SnowGlobeActionProposal(agentId, action, quantity.Value), "proposal_parsed");
        }
        catch (JsonException) { return new(null, "response_json_invalid"); }
    }

    private static bool IsCanonicalAgentId(string value)
    {
        if (value.Length is < 1 or > 64) return false;
        foreach (char character in value)
        {
            if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '-')) return false;
        }
        return true;
    }
}

internal static class CognitionQualityRecordedResponseRunnerErrors
{
    internal const string ProposalSchemaInvalid = "proposal_schema_invalid";
    internal const string FixtureCountInvalid = "fixture_count_invalid";
    internal const string FixtureInvalid = "fixture_invalid";
    internal const string FixtureOrderInvalid = "fixture_order_invalid";
    internal const string FixtureObservationInvalid = "fixture_observation_invalid";
    internal const string ResponseSizeInvalid = "response_size_invalid";
    internal const string ResponseAggregateSizeInvalid = "response_aggregate_size_invalid";
    internal const string CorpusInvalid = "corpus_invalid";
    internal const string IntegrityInvalid = "run_integrity_invalid";
    internal const string RunSizeInvalid = "run_size_invalid";
    internal static bool IsAllowlisted(string code) => code is ProposalSchemaInvalid or FixtureCountInvalid or FixtureInvalid or FixtureOrderInvalid or FixtureObservationInvalid or ResponseSizeInvalid or ResponseAggregateSizeInvalid or CorpusInvalid or IntegrityInvalid or RunSizeInvalid;
}
