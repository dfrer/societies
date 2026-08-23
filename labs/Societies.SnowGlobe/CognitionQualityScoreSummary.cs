using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>One immutable disposition count in a validated cognition-quality score summary.</summary>
public sealed record CognitionQualityScoreDispositionCount(string Disposition, int Count);

/// <summary>One immutable scenario view in a validated cognition-quality score summary.</summary>
public sealed record CognitionQualityScoreScenario(string ScenarioId, int RawPoints, int BasisPoints, string Disposition);

/// <summary>One immutable category view in a validated cognition-quality score summary.</summary>
public sealed class CognitionQualityScoreCategory
{
    private readonly CognitionQualityScoreScenario[] _scenarios;

    internal CognitionQualityScoreCategory(string categoryId, int rawPoints, int basisPoints, IEnumerable<CognitionQualityScoreScenario> scenarios)
    {
        CategoryId = categoryId;
        RawPoints = rawPoints;
        BasisPoints = basisPoints;
        _scenarios = scenarios.Select(static value => value with { }).ToArray();
    }

    public string CategoryId { get; }
    public int RawPoints { get; }
    public int BasisPoints { get; }
    public IReadOnlyList<CognitionQualityScoreScenario> Scenarios => Array.AsReadOnly(_scenarios.Select(static value => value with { }).ToArray());
}

/// <summary>
/// Detached raw-free score projection. It can only be obtained from a successful recording result,
/// receipt, validated v4 artifact, or the internal fixed codec.
/// </summary>
public sealed class CognitionQualityScoreSummary
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityScoreDispositionCount[] _dispositionCounts;
    private readonly CognitionQualityScoreCategory[] _categories;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityScoreSummary(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string provenanceDigestSha256,
        string recordingEvidenceDigestSha256,
        string executionEvidenceDigestSha256,
        CognitionQualityExecutionQualityContract qualityContract,
        int scenarioCount,
        int maximumPoints,
        int rawPoints,
        int basisPoints,
        IEnumerable<CognitionQualityScoreDispositionCount> dispositionCounts,
        IEnumerable<CognitionQualityScoreCategory> categories,
        IEnumerable<string> claimLimitationCodes,
        string qualityReportCanonicalJson)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _dispositionCounts = dispositionCounts.Select(static value => value with { }).ToArray();
        _categories = categories.Select(static value => new CognitionQualityScoreCategory(value.CategoryId, value.RawPoints, value.BasisPoints, value.Scenarios)).ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        ProvenanceDigestSha256 = provenanceDigestSha256;
        RecordingEvidenceDigestSha256 = recordingEvidenceDigestSha256;
        ExecutionEvidenceDigestSha256 = executionEvidenceDigestSha256;
        QualityContract = qualityContract.Detach();
        ScenarioCount = scenarioCount;
        MaximumPoints = maximumPoints;
        RawPoints = rawPoints;
        BasisPoints = basisPoints;
        QualityReportCanonicalJson = qualityReportCanonicalJson;
    }

    public string SchemaVersion => CognitionQualityScoreSummaryCodec.SchemaVersion;
    public string Status => "complete";
    public string Semantics => CognitionQualityScoreSummaryCodec.Semantics;
    public string ProvenanceDigestSha256 { get; }
    public string RecordingEvidenceDigestSha256 { get; }
    public string ExecutionEvidenceDigestSha256 { get; }
    public CognitionQualityExecutionQualityContract QualityContract { get; }
    public string QualityReportDigestSha256 => QualityContract.ReportDigestSha256;
    public int ScenarioCount { get; }
    public int MaximumPoints { get; }
    public int RawPoints { get; }
    public int BasisPoints { get; }
    public IReadOnlyList<CognitionQualityScoreDispositionCount> DispositionCounts => Array.AsReadOnly(_dispositionCounts.Select(static value => value with { }).ToArray());
    public IReadOnlyList<CognitionQualityScoreCategory> Categories => Array.AsReadOnly(_categories.Select(static value => new CognitionQualityScoreCategory(value.CategoryId, value.RawPoints, value.BasisPoints, value.Scenarios)).ToArray());
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string QualityReportCanonicalJson { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

internal sealed class CognitionQualityScoreSummaryException : Exception
{
    internal CognitionQualityScoreSummaryException(string code) : base(code) => Code = code;
    internal string Code { get; }
}

/// <summary>Internal fixed projection and strict canonical validator; it exposes no caller-selectable policy.</summary>
internal static class CognitionQualityScoreSummaryCodec
{
    internal const string SchemaVersion = "snow_globe_cognition_quality_score_summary/v1";
    internal const string Semantics = "raw_free_offline_cognition_quality_projection_only";
    internal const int MaximumSummaryBytes = 40 * 1024;
    internal const int MaximumJsonDepth = 8;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] SummaryNames =
    [
        "schema_version", "status", "semantics", "provenance_digest_sha256",
        "recording_evidence_digest_sha256", "execution_evidence_digest_sha256", "quality_contract",
        "scenario_count", "max_points", "raw_points", "basis_points", "disposition_counts",
        "claim_limitation_codes", "categories", "quality_report", "summary_payload_digest_sha256"
    ];
    private static readonly string[] QualityContractNames =
    [
        "corpus_schema_version", "scoring_schema_version", "report_schema_version", "validator_identity",
        "corpus_digest_sha256", "scoring_digest_sha256", "submission_digest_sha256", "report_digest_sha256"
    ];
    private static readonly string[] ReportNames =
    [
        "schema_version", "status", "validator_identity", "corpus_digest_sha256", "scoring_digest_sha256",
        "submission_digest_sha256", "max_points", "raw_points", "basis_points", "disposition_counts",
        "claim_limitation_codes", "categories", "report_payload_digest_sha256"
    ];
    private static readonly string[] CategoryNames = ["category_id", "raw_points", "basis_points", "scenarios"];
    private static readonly string[] ScenarioNames = ["scenario_id", "raw_points", "basis_points", "disposition", "limitation_codes"];
    private static readonly string[] SummaryScenarioNames = ["scenario_id", "raw_points", "basis_points", "disposition"];
    private static readonly string[] RecordingEvidenceNames =
    [
        "schema_version", "status", "semantics", "prompt_publication", "response_set_digest_sha256",
        "provenance", "recorded_response_run", "claim_limitation_codes", "recording_evidence_payload_digest_sha256"
    ];
    private static readonly string[] RecordedRunNames =
    [
        "schema_version", "status", "semantics", "runner_identity", "parser_identity", "corpus_digest_sha256",
        "provenance_digest_sha256", "prompt_revision", "proposal_schema_version", "scenario_count",
        "response_bindings", "execution_evidence", "claim_limitation_codes", "run_payload_digest_sha256"
    ];
    private static readonly string[] ResponseBindingNames =
    [
        "scenario_id", "observation_digest_sha256", "response_byte_count", "response_digest_sha256", "parse_outcome"
    ];
    private static readonly string[] ExecutionEvidenceNames =
    [
        "schema_version", "status", "semantics", "provenance", "quality_contract", "scenario_count", "score",
        "claim_limitation_codes", "recorded_submission", "quality_report", "evidence_payload_digest_sha256"
    ];
    private static readonly string[] ScoreNames = ["max_points", "raw_points", "basis_points"];
    private static readonly string[] ParseOutcomes =
    [
        "proposal_parsed", "response_utf8_invalid", "response_json_too_deep", "response_json_invalid",
        "response_shape_invalid", "response_json_duplicate_property", "response_content_invalid"
    ];
    private static readonly int[] LowerObservableUtilityScenarioOrdinals = [1, 2, 3, 4, 5, 6, 8, 9, 12];
    private static readonly string[] ScenarioLimitationCodes =
    [
        "proposal_missing", "proposal_contract_invalid", "proposal_domain_rejected", "observable_target_met",
        "partial_observable_progress", "observable_progress_available", "lower_observable_utility"
    ];
    private static readonly string[] ClaimLimitations =
    [
        "offline_score_projection_only",
        "identity_attribution_is_caller_attested",
        "recording_and_execution_digests_provide_integrity_not_authenticity",
        "embedded_quality_report_is_the_recomputable_score_source",
        "raw_prompt_response_submission_and_proposal_not_retained",
        "no_execution_or_transport_attestation",
        "offline_fixed_corpus",
        "observable_action_utility_only",
        "no_general_quality_or_intelligence_claim",
        "no_cost_latency_price_or_winner_claim"
    ];

    internal static CognitionQualityScoreSummary Create(CognitionQualityRecordingEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        CognitionQualityExecutionEvidence execution = evidence.RecordedResponseRun.ExecutionEvidence;
        if (!string.Equals(evidence.SchemaVersion, CognitionQualityRecordingEvidenceModule.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(evidence.Status, "complete", StringComparison.Ordinal)
            || !string.Equals(CognitionQualityHash.Sha256(evidence.CanonicalUtf8.Span), evidence.CanonicalDigestSha256, StringComparison.Ordinal)
            || !string.Equals(CognitionQualityHash.Sha256(evidence.RecordedResponseRun.CanonicalUtf8.Span), evidence.RecordedResponseRun.CanonicalDigestSha256, StringComparison.Ordinal)
            || !string.Equals(CognitionQualityHash.Sha256(execution.CanonicalUtf8.Span), execution.CanonicalDigestSha256, StringComparison.Ordinal)
            || !string.Equals(execution.Provenance.PromptRevision, evidence.PromptPublication.PromptRevision, StringComparison.Ordinal))
            throw Failure("score_summary_source_invalid");

        try { ValidateSourceBindings(evidence, execution); }
        catch (CognitionQualityScoreSummaryException) { throw Failure("score_summary_source_invalid"); }
        catch (CognitionQualityExecutionEvidenceException) { throw Failure("score_summary_source_invalid"); }
        catch (CognitionQualityPromptEnvelopeException) { throw Failure("score_summary_source_invalid"); }
        catch (JsonException) { throw Failure("score_summary_source_invalid"); }
        catch (InvalidOperationException) { throw Failure("score_summary_source_invalid"); }

        byte[] reportBytes = StrictUtf8.GetBytes(execution.QualityReportCanonicalJson);
        try
        {
            ReportFacts report = ValidateQualityReport(reportBytes, execution.QualityContract);
            if (execution.ScenarioCount != report.ScenarioCount
                || execution.Score.MaximumPoints != report.MaximumPoints
                || execution.Score.RawPoints != report.RawPoints
                || execution.Score.BasisPoints != report.BasisPoints)
                throw Failure("score_summary_source_invalid");

            byte[] payload = Write(
                execution.Provenance.ProvenanceDigestSha256, evidence.CanonicalDigestSha256,
                execution.CanonicalDigestSha256, execution.QualityContract, report, reportBytes, null);
            string payloadDigest = CognitionQualityHash.Sha256(payload);
            CryptographicOperations.ZeroMemory(payload);
            byte[] canonical = Write(
                execution.Provenance.ProvenanceDigestSha256, evidence.CanonicalDigestSha256,
                execution.CanonicalDigestSha256, execution.QualityContract, report, reportBytes, payloadDigest);
            try { return Validate(canonical); }
            finally { CryptographicOperations.ZeroMemory(canonical); }
        }
        finally { CryptographicOperations.ZeroMemory(reportBytes); }
    }

    private static void ValidateSourceBindings(CognitionQualityRecordingEvidence evidence, CognitionQualityExecutionEvidence execution)
    {
        CognitionQualityRecordedResponseRun run = evidence.RecordedResponseRun;
        CognitionQualityPromptEnvelopePublication expectedPublication = CognitionQualityPromptEnvelopeBuilderModule.Create(evidence.PromptPublication.PromptRevision);
        CognitionQualityExecutionProvenance expectedProvenance = execution.Provenance.Detach();
        if (!evidence.PromptPublication.CanonicalUtf8.Span.SequenceEqual(expectedPublication.CanonicalUtf8.Span)
            || !string.Equals(evidence.PromptPublication.PayloadDigestSha256, expectedPublication.PayloadDigestSha256, StringComparison.Ordinal)
            || !string.Equals(evidence.PromptPublication.CanonicalDigestSha256, expectedPublication.CanonicalDigestSha256, StringComparison.Ordinal)
            || !execution.Provenance.CanonicalUtf8.Span.SequenceEqual(expectedProvenance.CanonicalUtf8.Span))
            throw Failure("score_summary_source_invalid");

        ValidateExecutionSource(execution, expectedProvenance);
        ValidateRunSource(run, execution, expectedProvenance, evidence.PromptPublication);

        ReadOnlyMemory<byte> evidenceUtf8 = evidence.CanonicalUtf8;
        if (evidenceUtf8.Length is < 1 or > CognitionQualityRecordingEvidenceModule.MaximumEvidenceBytes)
            throw Failure("score_summary_source_invalid");
        using JsonDocument document = ParseSource(evidenceUtf8.Span);
        JsonElement root = document.RootElement;
        RequireCanonicalScalarEncoding(root);
        RequireObjectAndOrder(root, RecordingEvidenceNames, "score_summary_source_invalid");
        RequireString(root, "schema_version", CognitionQualityRecordingEvidenceModule.SchemaVersion);
        RequireString(root, "status", "complete");
        RequireString(root, "semantics", "offline_recording_evidence_binding_only");
        RequireNestedCanonical(root, "prompt_publication", expectedPublication.CanonicalUtf8.Span);
        RequireString(root, "response_set_digest_sha256", evidence.ResponseSetDigestSha256);
        RequireNestedCanonical(root, "provenance", expectedProvenance.CanonicalUtf8.Span);
        RequireNestedCanonical(root, "recorded_response_run", run.CanonicalUtf8.Span);
        RequirePayloadDigest(root, RecordingEvidenceNames[^1], evidence.PayloadDigestSha256);
        RequireCanonicalRoot(root, evidenceUtf8.Span);

        string responseSetDigest = ComputeResponseSetDigest(run.ResponseBindings);
        if (!string.Equals(responseSetDigest, evidence.ResponseSetDigestSha256, StringComparison.Ordinal))
            throw Failure("score_summary_source_invalid");
    }

    private static void ValidateRunSource(
        CognitionQualityRecordedResponseRun run,
        CognitionQualityExecutionEvidence execution,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityPromptEnvelopePublication publication)
    {
        ReadOnlyMemory<byte> runUtf8 = run.CanonicalUtf8;
        if (runUtf8.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumRunBytes)
            throw Failure("score_summary_source_invalid");
        using JsonDocument document = ParseSource(runUtf8.Span);
        JsonElement root = document.RootElement;
        RequireCanonicalScalarEncoding(root);
        RequireObjectAndOrder(root, RecordedRunNames, "score_summary_source_invalid");
        RequireString(root, "schema_version", CognitionQualityRecordedResponseRunnerModule.SchemaVersion);
        RequireString(root, "status", "complete");
        RequireString(root, "semantics", "offline_recorded_response_conversion_only");
        RequireString(root, "runner_identity", CognitionQualityRecordedResponseRunnerModule.RunnerIdentity);
        RequireString(root, "parser_identity", CognitionQualityRecordedResponseRunnerModule.ParserIdentity);
        RequireString(root, "corpus_digest_sha256", CognitionQualityCorpusV1.ExpectedManifestDigestSha256);
        RequireString(root, "provenance_digest_sha256", provenance.ProvenanceDigestSha256);
        RequireString(root, "prompt_revision", provenance.PromptRevision);
        RequireString(root, "proposal_schema_version", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion);
        RequireInt32(root, "scenario_count", CognitionQualityCorpusV1.ScenarioCount);
        ValidateResponseBindings(root.GetProperty("response_bindings"), run.ResponseBindings, publication.Slots);
        RequireNestedCanonical(root, "execution_evidence", execution.CanonicalUtf8.Span);
        RequirePayloadDigest(root, RecordedRunNames[^1], run.PayloadDigestSha256);
        RequireCanonicalRoot(root, runUtf8.Span);

        byte[] recordedSubmission = CognitionQualityCorpusV1.WriteSubmissionEnvelope(run.ProposalBatch);
        byte[] executionSubmission = StrictUtf8.GetBytes(execution.RecordedSubmissionCanonicalJson);
        try
        {
            if (!recordedSubmission.AsSpan().SequenceEqual(executionSubmission))
                throw Failure("score_summary_source_invalid");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recordedSubmission);
            CryptographicOperations.ZeroMemory(executionSubmission);
        }
    }

    private static void ValidateExecutionSource(CognitionQualityExecutionEvidence execution, CognitionQualityExecutionProvenance provenance)
    {
        ReadOnlyMemory<byte> executionUtf8 = execution.CanonicalUtf8;
        if (executionUtf8.Length is < 1 or > CognitionQualityExecutionEvidenceModule.MaximumEvidenceBytes)
            throw Failure("score_summary_source_invalid");
        using JsonDocument document = ParseSource(executionUtf8.Span);
        JsonElement root = document.RootElement;
        RequireCanonicalScalarEncoding(root);
        RequireObjectAndOrder(root, ExecutionEvidenceNames, "score_summary_source_invalid");
        RequireString(root, "schema_version", CognitionQualityExecutionEvidenceModule.SchemaVersion);
        RequireString(root, "status", "complete");
        RequireString(root, "semantics", "offline_recorded_submission_binding_only");
        RequireNestedCanonical(root, "provenance", provenance.CanonicalUtf8.Span);
        CognitionQualityExecutionQualityContract parsedContract = ParseQualityContract(root.GetProperty("quality_contract"));
        RequireContractEquals(parsedContract, execution.QualityContract);
        RequireInt32(root, "scenario_count", execution.ScenarioCount);
        JsonElement score = root.GetProperty("score");
        RequireObjectAndOrder(score, ScoreNames, "score_summary_source_invalid");
        RequireInt32(score, "max_points", execution.Score.MaximumPoints);
        RequireInt32(score, "raw_points", execution.Score.RawPoints);
        RequireInt32(score, "basis_points", execution.Score.BasisPoints);
        byte[] submission = StrictUtf8.GetBytes(execution.RecordedSubmissionCanonicalJson);
        byte[] report = StrictUtf8.GetBytes(execution.QualityReportCanonicalJson);
        try
        {
            RequireNestedCanonical(root, "recorded_submission", submission);
            RequireNestedCanonical(root, "quality_report", report);
            if (!string.Equals(CognitionQualityHash.Sha256(submission), execution.QualityContract.SubmissionDigestSha256, StringComparison.Ordinal))
                throw Failure("score_summary_source_invalid");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(submission);
            CryptographicOperations.ZeroMemory(report);
        }
        RequirePayloadDigest(root, ExecutionEvidenceNames[^1], execution.PayloadDigestSha256);
        RequireCanonicalRoot(root, executionUtf8.Span);
    }

    private static void ValidateResponseBindings(
        JsonElement value,
        IReadOnlyList<CognitionQualityRecordedResponseBinding> bindings,
        IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots)
    {
        if (value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() != CognitionQualityCorpusV1.ScenarioCount
            || bindings.Count != CognitionQualityCorpusV1.ScenarioCount
            || slots.Count != CognitionQualityCorpusV1.ScenarioCount)
            throw Failure("score_summary_source_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            CognitionQualityRecordedResponseBinding binding = bindings[index];
            CognitionQualityPromptEnvelopeSlot slot = slots[index++];
            RequireObjectAndOrder(item, ResponseBindingNames, "score_summary_source_invalid");
            RequireString(item, "scenario_id", slot.ScenarioId);
            RequireString(item, "scenario_id", binding.ScenarioId);
            RequireString(item, "observation_digest_sha256", slot.ObservationDigestSha256);
            RequireString(item, "observation_digest_sha256", binding.ObservationDigestSha256);
            int byteCount = RequireInt32(item, "response_byte_count", binding.ResponseByteCount);
            if (byteCount is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes)
                throw Failure("score_summary_source_invalid");
            RequireString(item, "response_digest_sha256", binding.ResponseDigestSha256);
            _ = RequireDigest(item, "response_digest_sha256");
            string outcome = RequireClosedString(item, "parse_outcome", ParseOutcomes, "score_summary_source_invalid");
            if (!string.Equals(outcome, binding.ParseOutcome, StringComparison.Ordinal)) throw Failure("score_summary_source_invalid");
        }
    }

    private static string ComputeResponseSetDigest(IReadOnlyList<CognitionQualityRecordedResponseBinding> bindings)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartArray();
        foreach (CognitionQualityRecordedResponseBinding binding in bindings)
        {
            writer.WriteStartObject();
            writer.WriteString("scenario_id", binding.ScenarioId);
            writer.WriteString("observation_digest_sha256", binding.ObservationDigestSha256);
            writer.WriteNumber("response_byte_count", binding.ResponseByteCount);
            writer.WriteString("response_digest_sha256", binding.ResponseDigestSha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.Flush();
        return CognitionQualityHash.Sha256(buffer.WrittenSpan);
    }

    internal static CognitionQualityScoreSummary Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumSummaryBytes) throw Failure("score_summary_size_invalid");
        ReadOnlySpan<byte> bom = [0xef, 0xbb, 0xbf];
        if (canonicalUtf8.Span.StartsWith(bom)) throw Failure("score_summary_utf8_invalid");
        try { _ = StrictUtf8.GetString(canonicalUtf8.Span); }
        catch (DecoderFallbackException) { throw Failure("score_summary_utf8_invalid"); }

        using JsonDocument document = Parse(canonicalUtf8.Span);
        JsonElement root = document.RootElement;
        RequireCanonicalScalarEncoding(root);
        RequireObjectAndOrder(root, SummaryNames, "score_summary_shape_invalid");
        RequireString(root, "schema_version", SchemaVersion);
        RequireString(root, "status", "complete");
        RequireString(root, "semantics", Semantics);
        string provenanceDigest = RequireDigest(root, "provenance_digest_sha256");
        string recordingDigest = RequireDigest(root, "recording_evidence_digest_sha256");
        string executionDigest = RequireDigest(root, "execution_evidence_digest_sha256");
        CognitionQualityExecutionQualityContract contract = ParseQualityContract(root.GetProperty("quality_contract"));
        byte[] reportBytes = Canonicalize(root.GetProperty("quality_report"));
        try
        {
            ReportFacts report = ValidateQualityReport(reportBytes, contract);
            RequireInt32(root, "scenario_count", report.ScenarioCount);
            RequireInt32(root, "max_points", report.MaximumPoints);
            RequireInt32(root, "raw_points", report.RawPoints);
            RequireInt32(root, "basis_points", report.BasisPoints);
            ValidateDispositionCounts(root.GetProperty("disposition_counts"), report.DispositionCounts);
            RequireExactStringArray(root.GetProperty("claim_limitation_codes"), ClaimLimitations, "score_summary_claims_invalid");
            ValidateSummaryCategories(root.GetProperty("categories"), report.Categories);

            string payloadDigest = RequireDigest(root, "summary_payload_digest_sha256");
            byte[] payload = CanonicalizeWithoutLast(root, SummaryNames[^1]);
            try
            {
                if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal))
                    throw Failure("score_summary_payload_digest_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(payload); }

            byte[] canonical = Canonicalize(root);
            if (!canonical.AsSpan().SequenceEqual(canonicalUtf8.Span))
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw Failure("score_summary_noncanonical");
            }
            return new CognitionQualityScoreSummary(
                canonical, payloadDigest, provenanceDigest, recordingDigest, executionDigest, contract,
                report.ScenarioCount, report.MaximumPoints, report.RawPoints, report.BasisPoints,
                report.DispositionCounts, report.Categories, ClaimLimitations, StrictUtf8.GetString(reportBytes));
        }
        finally { CryptographicOperations.ZeroMemory(reportBytes); }
    }

    private static byte[] Write(
        string provenanceDigest,
        string recordingDigest,
        string executionDigest,
        CognitionQualityExecutionQualityContract contract,
        ReportFacts report,
        ReadOnlySpan<byte> qualityReport,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("semantics", Semantics);
        writer.WriteString("provenance_digest_sha256", provenanceDigest);
        writer.WriteString("recording_evidence_digest_sha256", recordingDigest);
        writer.WriteString("execution_evidence_digest_sha256", executionDigest);
        writer.WritePropertyName("quality_contract"); WriteQualityContract(writer, contract);
        writer.WriteNumber("scenario_count", report.ScenarioCount);
        writer.WriteNumber("max_points", report.MaximumPoints);
        writer.WriteNumber("raw_points", report.RawPoints);
        writer.WriteNumber("basis_points", report.BasisPoints);
        WriteDispositionCounts(writer, report.DispositionCounts);
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray();
        foreach (string code in ClaimLimitations) writer.WriteStringValue(code);
        writer.WriteEndArray();
        WriteSummaryCategories(writer, report.Categories);
        writer.WritePropertyName("quality_report"); writer.WriteRawValue(qualityReport, skipInputValidation: false);
        if (payloadDigest is not null) writer.WriteString("summary_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static ReportFacts ValidateQualityReport(ReadOnlySpan<byte> reportUtf8, CognitionQualityExecutionQualityContract contract)
    {
        if (reportUtf8.Length is < 1 or > CognitionQuality.MaximumReportBytes) throw Failure("score_summary_report_invalid");
        using JsonDocument document = Parse(reportUtf8);
        JsonElement root = document.RootElement;
        RequireCanonicalScalarEncoding(root);
        RequireObjectAndOrder(root, ReportNames, "score_summary_report_invalid");
        RequireString(root, "schema_version", contract.ReportSchemaVersion);
        RequireString(root, "status", "complete");
        RequireString(root, "validator_identity", contract.ValidatorIdentity);
        RequireString(root, "corpus_digest_sha256", contract.CorpusDigestSha256);
        RequireString(root, "scoring_digest_sha256", contract.ScoringDigestSha256);
        RequireString(root, "submission_digest_sha256", contract.SubmissionDigestSha256);
        int maximumPoints = RequireInt32(root, "max_points");
        int rawPoints = RequireInt32(root, "raw_points");
        int basisPoints = RequireInt32(root, "basis_points");
        if (maximumPoints != CognitionQualityCorpusV1.MaximumPoints || rawPoints < 0 || rawPoints > maximumPoints
            || basisPoints != checked(rawPoints * 10_000 / maximumPoints)) throw Failure("score_summary_report_arithmetic_invalid");

        CognitionQualityScoreDispositionCount[] counts = ParseDispositionCounts(root.GetProperty("disposition_counts"));
        RequireExactStringArray(root.GetProperty("claim_limitation_codes"), CognitionQualityCorpusV1.ClaimLimitationCodes, "score_summary_report_invalid");
        CognitionQualityScoreCategory[] categories = ParseReportCategories(root.GetProperty("categories"), counts, rawPoints);
        string reportPayloadDigest = RequireDigest(root, "report_payload_digest_sha256");
        byte[] payload = CanonicalizeWithoutLast(root, ReportNames[^1]);
        try
        {
            if (!string.Equals(CognitionQualityHash.Sha256(payload), reportPayloadDigest, StringComparison.Ordinal))
                throw Failure("score_summary_report_digest_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(payload); }

        byte[] canonical = Canonicalize(root);
        try
        {
            if (!canonical.AsSpan().SequenceEqual(reportUtf8)
                || !string.Equals(CognitionQualityHash.Sha256(canonical), contract.ReportDigestSha256, StringComparison.Ordinal))
                throw Failure("score_summary_report_digest_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(canonical); }
        return new ReportFacts(CognitionQualityCorpusV1.ScenarioCount, maximumPoints, rawPoints, basisPoints, counts, categories);
    }

    private static CognitionQualityExecutionQualityContract ParseQualityContract(JsonElement value)
    {
        RequireObjectAndOrder(value, QualityContractNames, "score_summary_quality_contract_invalid");
        RequireString(value, "corpus_schema_version", CognitionQualityCorpusV1.CorpusSchemaVersion);
        RequireString(value, "scoring_schema_version", CognitionQualityCorpusV1.ScoringSchemaVersion);
        RequireString(value, "report_schema_version", CognitionQualityCorpusV1.ReportSchemaVersion);
        RequireString(value, "validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
        string corpusDigest = RequireDigest(value, "corpus_digest_sha256");
        string scoringDigest = RequireDigest(value, "scoring_digest_sha256");
        string submissionDigest = RequireDigest(value, "submission_digest_sha256");
        string reportDigest = RequireDigest(value, "report_digest_sha256");
        if (!string.Equals(corpusDigest, CognitionQualityCorpusV1.ExpectedManifestDigestSha256, StringComparison.Ordinal)
            || !string.Equals(scoringDigest, CognitionQualityCorpusV1.ExpectedScoringDigestSha256, StringComparison.Ordinal))
            throw Failure("score_summary_quality_contract_invalid");
        return new CognitionQualityExecutionQualityContract(corpusDigest, scoringDigest, submissionDigest, reportDigest);
    }

    private static void WriteQualityContract(Utf8JsonWriter writer, CognitionQualityExecutionQualityContract contract)
    {
        writer.WriteStartObject();
        writer.WriteString("corpus_schema_version", contract.CorpusSchemaVersion);
        writer.WriteString("scoring_schema_version", contract.ScoringSchemaVersion);
        writer.WriteString("report_schema_version", contract.ReportSchemaVersion);
        writer.WriteString("validator_identity", contract.ValidatorIdentity);
        writer.WriteString("corpus_digest_sha256", contract.CorpusDigestSha256);
        writer.WriteString("scoring_digest_sha256", contract.ScoringDigestSha256);
        writer.WriteString("submission_digest_sha256", contract.SubmissionDigestSha256);
        writer.WriteString("report_digest_sha256", contract.ReportDigestSha256);
        writer.WriteEndObject();
    }

    private static CognitionQualityScoreDispositionCount[] ParseDispositionCounts(JsonElement value)
    {
        RequireObjectAndOrder(value, CognitionQualityCorpusV1.Dispositions, "score_summary_dispositions_invalid");
        CognitionQualityScoreDispositionCount[] counts = CognitionQualityCorpusV1.Dispositions
            .Select(disposition => new CognitionQualityScoreDispositionCount(disposition, RequireInt32(value, disposition)))
            .ToArray();
        if (counts.Any(static item => item.Count is < 0 or > CognitionQualityCorpusV1.ScenarioCount))
            throw Failure("score_summary_dispositions_invalid");
        int total = 0;
        foreach (CognitionQualityScoreDispositionCount item in counts) total += item.Count;
        if (total != CognitionQualityCorpusV1.ScenarioCount) throw Failure("score_summary_dispositions_invalid");
        return counts;
    }

    private static void ValidateDispositionCounts(JsonElement value, IReadOnlyList<CognitionQualityScoreDispositionCount> expected)
    {
        CognitionQualityScoreDispositionCount[] actual = ParseDispositionCounts(value);
        if (!actual.SequenceEqual(expected)) throw Failure("score_summary_dispositions_invalid");
    }

    private static void WriteDispositionCounts(Utf8JsonWriter writer, IReadOnlyList<CognitionQualityScoreDispositionCount> counts)
    {
        writer.WritePropertyName("disposition_counts"); writer.WriteStartObject();
        foreach (CognitionQualityScoreDispositionCount item in counts) writer.WriteNumber(item.Disposition, item.Count);
        writer.WriteEndObject();
    }

    private static CognitionQualityScoreCategory[] ParseReportCategories(JsonElement value, IReadOnlyList<CognitionQualityScoreDispositionCount> counts, int totalRawPoints)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != CognitionQualityCorpusV1.CategoryCount)
            throw Failure("score_summary_categories_invalid");
        List<CognitionQualityScoreCategory> categories = new(CognitionQualityCorpusV1.CategoryCount);
        int categoryIndex = 0;
        int scenarioOrdinal = 1;
        Dictionary<string, int> derivedCounts = CognitionQualityCorpusV1.Dispositions.ToDictionary(static value => value, static _ => 0, StringComparer.Ordinal);
        foreach (JsonElement category in value.EnumerateArray())
        {
            RequireObjectAndOrder(category, CategoryNames, "score_summary_categories_invalid");
            string categoryId = RequireString(category, "category_id", CognitionQualityCorpusV1.CategoryIds[categoryIndex++]);
            int categoryRaw = RequireInt32(category, "raw_points");
            int categoryBasis = RequireInt32(category, "basis_points");
            JsonElement scenarios = category.GetProperty("scenarios");
            if (scenarios.ValueKind != JsonValueKind.Array || scenarios.GetArrayLength() != 3) throw Failure("score_summary_scenarios_invalid");
            List<CognitionQualityScoreScenario> scenarioViews = new(3);
            int derivedRaw = 0;
            foreach (JsonElement scenario in scenarios.EnumerateArray())
            {
                RequireObjectAndOrder(scenario, ScenarioNames, "score_summary_scenarios_invalid");
                int currentScenarioOrdinal = scenarioOrdinal++;
                string scenarioId = RequireString(scenario, "scenario_id", $"cq{currentScenarioOrdinal}");
                int scenarioRaw = RequireInt32(scenario, "raw_points");
                int scenarioBasis = RequireInt32(scenario, "basis_points");
                string disposition = RequireClosedString(scenario, "disposition", CognitionQualityCorpusV1.Dispositions, "score_summary_scenarios_invalid");
                JsonElement limitations = scenario.GetProperty("limitation_codes");
                if (limitations.ValueKind != JsonValueKind.Array || limitations.GetArrayLength() != 1
                    || limitations[0].ValueKind != JsonValueKind.String
                    || !ScenarioLimitationCodes.Contains(limitations[0].GetString(), StringComparer.Ordinal))
                    throw Failure("score_summary_scenarios_invalid");
                string limitation = limitations[0].GetString()!;
                if (scenarioRaw is < 0 or > CognitionQuality.PointsPerScenario || scenarioBasis != scenarioRaw * 100)
                    throw Failure("score_summary_report_arithmetic_invalid");
                if (!IsExactScoringTuple(currentScenarioOrdinal, scenarioRaw, disposition, limitation))
                    throw Failure("score_summary_scenarios_invalid");
                derivedRaw = checked(derivedRaw + scenarioRaw);
                derivedCounts[disposition]++;
                scenarioViews.Add(new CognitionQualityScoreScenario(scenarioId, scenarioRaw, scenarioBasis, disposition));
            }
            if (categoryRaw != derivedRaw || categoryBasis != checked(categoryRaw * 10_000 / (3 * CognitionQuality.PointsPerScenario)))
                throw Failure("score_summary_report_arithmetic_invalid");
            categories.Add(new CognitionQualityScoreCategory(categoryId, categoryRaw, categoryBasis, scenarioViews));
        }
        if (scenarioOrdinal != CognitionQualityCorpusV1.ScenarioCount + 1
            || categories.Sum(static item => item.RawPoints) != totalRawPoints
            || counts.Any(item => derivedCounts[item.Disposition] != item.Count))
            throw Failure("score_summary_report_arithmetic_invalid");
        return categories.ToArray();
    }

    private static bool IsExactScoringTuple(int scenarioOrdinal, int rawPoints, string disposition, string limitation)
    {
        if (!string.Equals(CognitionQualityCorpusV1.ScoringDigestSha256, CognitionQualityCorpusV1.ExpectedScoringDigestSha256, StringComparison.Ordinal))
            return false;
        if (disposition == "maximum_utility")
            return rawPoints == CognitionQuality.PointsPerScenario && limitation == "observable_target_met";
        if (disposition == "no_proposal")
            return rawPoints == 0 && limitation == "proposal_missing";
        if (disposition == "contract_invalid")
            return rawPoints == 0 && limitation == "proposal_contract_invalid";
        if (disposition == "domain_rejected")
            return rawPoints == 0 && limitation == "proposal_domain_rejected";
        if (disposition != "feasible_suboptimal") return false;
        if (rawPoints == 10 && limitation == "lower_observable_utility")
            return LowerObservableUtilityScenarioOrdinals.Contains(scenarioOrdinal);
        if (rawPoints == 25 && limitation == "observable_progress_available") return scenarioOrdinal is >= 1 and <= 9;
        if (limitation != "partial_observable_progress") return false;

        int requiredProgress = scenarioOrdinal switch
        {
            1 => 12,
            2 => 6,
            3 => 2,
            7 => 8,
            _ => 0
        };
        for (int quantity = 1; quantity < requiredProgress; quantity++)
            if (rawPoints == 25 + 75 * quantity / requiredProgress) return true;
        return false;
    }

    private static void ValidateSummaryCategories(JsonElement value, IReadOnlyList<CognitionQualityScoreCategory> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count) throw Failure("score_summary_categories_invalid");
        int categoryIndex = 0;
        foreach (JsonElement category in value.EnumerateArray())
        {
            CognitionQualityScoreCategory expectedCategory = expected[categoryIndex++];
            RequireObjectAndOrder(category, CategoryNames, "score_summary_categories_invalid");
            RequireString(category, "category_id", expectedCategory.CategoryId);
            RequireInt32(category, "raw_points", expectedCategory.RawPoints);
            RequireInt32(category, "basis_points", expectedCategory.BasisPoints);
            JsonElement scenarios = category.GetProperty("scenarios");
            if (scenarios.ValueKind != JsonValueKind.Array || scenarios.GetArrayLength() != expectedCategory.Scenarios.Count)
                throw Failure("score_summary_scenarios_invalid");
            int scenarioIndex = 0;
            foreach (JsonElement scenario in scenarios.EnumerateArray())
            {
                CognitionQualityScoreScenario expectedScenario = expectedCategory.Scenarios[scenarioIndex++];
                RequireObjectAndOrder(scenario, SummaryScenarioNames, "score_summary_scenarios_invalid");
                RequireString(scenario, "scenario_id", expectedScenario.ScenarioId);
                RequireInt32(scenario, "raw_points", expectedScenario.RawPoints);
                RequireInt32(scenario, "basis_points", expectedScenario.BasisPoints);
                RequireString(scenario, "disposition", expectedScenario.Disposition);
            }
        }
    }

    private static void WriteSummaryCategories(Utf8JsonWriter writer, IReadOnlyList<CognitionQualityScoreCategory> categories)
    {
        writer.WritePropertyName("categories"); writer.WriteStartArray();
        foreach (CognitionQualityScoreCategory category in categories)
        {
            writer.WriteStartObject();
            writer.WriteString("category_id", category.CategoryId);
            writer.WriteNumber("raw_points", category.RawPoints);
            writer.WriteNumber("basis_points", category.BasisPoints);
            writer.WritePropertyName("scenarios"); writer.WriteStartArray();
            foreach (CognitionQualityScoreScenario scenario in category.Scenarios)
            {
                writer.WriteStartObject();
                writer.WriteString("scenario_id", scenario.ScenarioId);
                writer.WriteNumber("raw_points", scenario.RawPoints);
                writer.WriteNumber("basis_points", scenario.BasisPoints);
                writer.WriteString("disposition", scenario.Disposition);
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static JsonDocument Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            Utf8JsonReader reader = new(utf8, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaximumJsonDepth });
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("score_summary_json_invalid"); }
            return document;
        }
        catch (CognitionQualityScoreSummaryException) { throw; }
        catch (JsonException) { throw Failure("score_summary_json_invalid"); }
    }

    private static JsonDocument ParseSource(ReadOnlySpan<byte> utf8)
    {
        try
        {
            Utf8JsonReader reader = new(utf8, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("score_summary_source_invalid"); }
            return document;
        }
        catch (CognitionQualityScoreSummaryException) { throw; }
        catch (JsonException) { throw Failure("score_summary_source_invalid"); }
    }

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names, string code)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure(code);
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure(code);
        for (int index = 0; index < names.Count; index++)
            if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal)) throw Failure(code);
    }

    private static string RequireString(JsonElement owner, string name, string expected)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || !string.Equals(value.GetString(), expected, StringComparison.Ordinal)) throw Failure("score_summary_value_invalid");
        return expected;
    }

    private static string RequireClosedString(JsonElement owner, string name, IReadOnlyList<string> expected, string code)
    {
        JsonElement value = owner.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !expected.Contains(text, StringComparer.Ordinal)) throw Failure(code);
        return text;
    }

    private static string RequireDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        string? digest = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (digest is not { Length: 64 } || digest.Any(static character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Failure("score_summary_digest_invalid");
        return digest;
    }

    private static int RequireInt32(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed)) throw Failure("score_summary_number_invalid");
        return parsed;
    }

    private static int RequireInt32(JsonElement owner, string name, int expected)
    {
        int parsed = RequireInt32(owner, name);
        if (parsed != expected) throw Failure("score_summary_number_invalid");
        return parsed;
    }

    private static void RequireExactStringArray(JsonElement value, IReadOnlyList<string> expected, string code)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count) throw Failure(code);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index++], StringComparison.Ordinal)) throw Failure(code);
    }

    private static void RequireCanonicalScalarEncoding(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject()) RequireCanonicalScalarEncoding(property.Value);
            return;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) RequireCanonicalScalarEncoding(item);
            return;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            if (!string.Equals(JsonSerializer.Serialize(value.GetString()), value.GetRawText(), StringComparison.Ordinal)) throw Failure("score_summary_noncanonical");
            return;
        }
        if (value.ValueKind == JsonValueKind.Number
            && (!value.TryGetInt64(out long integer) || !string.Equals(integer.ToString(CultureInfo.InvariantCulture), value.GetRawText(), StringComparison.Ordinal)))
            throw Failure("score_summary_noncanonical");
    }

    private static void RequireContractEquals(
        CognitionQualityExecutionQualityContract actual,
        CognitionQualityExecutionQualityContract expected)
    {
        if (!string.Equals(actual.CorpusSchemaVersion, expected.CorpusSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(actual.ScoringSchemaVersion, expected.ScoringSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(actual.ReportSchemaVersion, expected.ReportSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(actual.ValidatorIdentity, expected.ValidatorIdentity, StringComparison.Ordinal)
            || !string.Equals(actual.CorpusDigestSha256, expected.CorpusDigestSha256, StringComparison.Ordinal)
            || !string.Equals(actual.ScoringDigestSha256, expected.ScoringDigestSha256, StringComparison.Ordinal)
            || !string.Equals(actual.SubmissionDigestSha256, expected.SubmissionDigestSha256, StringComparison.Ordinal)
            || !string.Equals(actual.ReportDigestSha256, expected.ReportDigestSha256, StringComparison.Ordinal))
            throw Failure("score_summary_source_invalid");
    }

    private static void RequireNestedCanonical(JsonElement owner, string propertyName, ReadOnlySpan<byte> expected)
    {
        byte[] canonical = Canonicalize(owner.GetProperty(propertyName));
        try
        {
            if (!canonical.AsSpan().SequenceEqual(expected)) throw Failure("score_summary_source_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }

    private static void RequirePayloadDigest(JsonElement root, string propertyName, string expected)
    {
        RequireString(root, propertyName, expected);
        _ = RequireDigest(root, propertyName);
        byte[] payload = CanonicalizeWithoutLast(root, propertyName);
        try
        {
            if (!string.Equals(CognitionQualityHash.Sha256(payload), expected, StringComparison.Ordinal))
                throw Failure("score_summary_source_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static void RequireCanonicalRoot(JsonElement root, ReadOnlySpan<byte> expected)
    {
        byte[] canonical = Canonicalize(root);
        try
        {
            if (!canonical.AsSpan().SequenceEqual(expected)) throw Failure("score_summary_source_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }

    private static byte[] Canonicalize(JsonElement value)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        value.WriteTo(writer); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement value, string lastName)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.NameEquals(lastName)) continue;
            writer.WritePropertyName(property.Name); property.Value.WriteTo(writer);
        }
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private sealed record ReportFacts(
        int ScenarioCount,
        int MaximumPoints,
        int RawPoints,
        int BasisPoints,
        CognitionQualityScoreDispositionCount[] DispositionCounts,
        CognitionQualityScoreCategory[] Categories);

    private static CognitionQualityScoreSummaryException Failure(string code) => new(code);
}
