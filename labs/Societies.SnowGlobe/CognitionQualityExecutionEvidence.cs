using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>
/// Caller-attested identity binding for a recorded cognition-quality submission batch.
/// It intentionally proves only that the supplied batch was scored under the frozen offline contract.
/// </summary>
public sealed class CognitionQualityExecutionProvenance
{
    private readonly byte[] _canonicalUtf8;

    private CognitionQualityExecutionProvenance(
        CognitionLane lane,
        string modelIdentity,
        string modelRevisionIdentity,
        string executionPolicyDigestSha256,
        string promptRevision,
        string proposalSchemaVersion,
        string? localAdapterIdentity)
    {
        Lane = lane;
        ModelIdentity = modelIdentity;
        ModelRevisionIdentity = modelRevisionIdentity;
        ExecutionPolicyDigestSha256 = executionPolicyDigestSha256;
        PromptRevision = promptRevision;
        ProposalSchemaVersion = proposalSchemaVersion;
        LocalAdapterIdentity = localAdapterIdentity;
        byte[] payload = WriteCanonical(lane, modelIdentity, modelRevisionIdentity, executionPolicyDigestSha256, promptRevision, proposalSchemaVersion, localAdapterIdentity, null);
        ProvenanceDigestSha256 = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        _canonicalUtf8 = WriteCanonical(lane, modelIdentity, modelRevisionIdentity, executionPolicyDigestSha256, promptRevision, proposalSchemaVersion, localAdapterIdentity, ProvenanceDigestSha256);
    }

    public CognitionLane Lane { get; }
    public string ModelIdentity { get; }
    public string ModelRevisionIdentity { get; }
    public string ExecutionPolicyDigestSha256 { get; }
    public string PromptRevision { get; }
    public string ProposalSchemaVersion { get; }
    public string? LocalAdapterIdentity { get; }
    public string ProvenanceDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();

    public static CognitionQualityExecutionProvenance ForLocal(
        string modelIdentity,
        string modelRevisionIdentity,
        string executionPolicyDigestSha256,
        string promptRevision,
        string proposalSchemaVersion,
        string localAdapterIdentity)
    {
        return Create(CognitionLane.Local, modelIdentity, modelRevisionIdentity, executionPolicyDigestSha256, promptRevision, proposalSchemaVersion, localAdapterIdentity);
    }

    public static CognitionQualityExecutionProvenance ForPremium(ModelPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            policy.Validate();
        }
        catch (ArgumentException)
        {
            throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.ProvenanceInvalid);
        }

        return Create(
            CognitionLane.Premium,
            policy.PremiumModelIdentity,
            policy.PremiumModelRevisionIdentity,
            policy.Digest,
            policy.PromptRevision,
            policy.ProposalSchemaVersion,
            null);
    }

    internal CognitionQualityExecutionProvenance Detach() => new(Lane, ModelIdentity, ModelRevisionIdentity, ExecutionPolicyDigestSha256, PromptRevision, ProposalSchemaVersion, LocalAdapterIdentity);

    internal static byte[] WriteCanonical(CognitionLane lane, string modelIdentity, string modelRevisionIdentity, string executionPolicyDigestSha256, string promptRevision, string proposalSchemaVersion, string? localAdapterIdentity, string? provenanceDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("lane", LaneName(lane));
        writer.WriteString("model_identity", modelIdentity);
        writer.WriteString("model_revision_identity", modelRevisionIdentity);
        writer.WriteString("execution_policy_digest_sha256", executionPolicyDigestSha256);
        writer.WriteString("prompt_revision", promptRevision);
        writer.WriteString("proposal_schema_version", proposalSchemaVersion);
        if (localAdapterIdentity is null) writer.WriteNull("local_adapter_identity"); else writer.WriteString("local_adapter_identity", localAdapterIdentity);
        if (provenanceDigest is not null) writer.WriteString("provenance_digest_sha256", provenanceDigest);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static void WriteTo(Utf8JsonWriter writer, CognitionQualityExecutionProvenance provenance)
    {
        writer.WriteRawValue(WriteCanonical(
            provenance.Lane,
            provenance.ModelIdentity,
            provenance.ModelRevisionIdentity,
            provenance.ExecutionPolicyDigestSha256,
            provenance.PromptRevision,
            provenance.ProposalSchemaVersion,
            provenance.LocalAdapterIdentity,
            provenance.ProvenanceDigestSha256));
    }

    private static CognitionQualityExecutionProvenance Create(CognitionLane lane, string modelIdentity, string modelRevisionIdentity, string executionPolicyDigestSha256, string promptRevision, string proposalSchemaVersion, string? localAdapterIdentity)
    {
        if (!Enum.IsDefined(lane)
            || !IsCanonicalIdentity(modelIdentity)
            || !IsModelRevision(modelRevisionIdentity)
            || !IsDigest(executionPolicyDigestSha256)
            || !IsCanonicalIdentity(promptRevision)
            || !IsCanonicalIdentity(proposalSchemaVersion)
            || (lane == CognitionLane.Local && !IsCanonicalIdentity(localAdapterIdentity))
            || (lane == CognitionLane.Premium && localAdapterIdentity is not null))
        {
            throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.ProvenanceInvalid);
        }
        return new CognitionQualityExecutionProvenance(lane, modelIdentity, modelRevisionIdentity, executionPolicyDigestSha256, promptRevision, proposalSchemaVersion, localAdapterIdentity);
    }

    private static bool IsCanonicalIdentity(string? value) => SnowGlobeInferenceIdentity.IsCanonical(value);
    private static bool IsModelRevision(string? value) => value is { Length: 71 }
        && value.StartsWith("sha256-", StringComparison.Ordinal)
        && value.Skip(7).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string LaneName(CognitionLane lane) => lane == CognitionLane.Local ? "local" : "premium";
}

/// <summary>Detached immutable output of the offline recorded-submission binding operation.</summary>
public sealed class CognitionQualityExecutionEvidence
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityExecutionEvidence(byte[] canonicalUtf8, string payloadDigestSha256, CognitionQualityExecutionProvenance provenance, CognitionQualityExecutionQualityContract qualityContract, int scenarioCount, CognitionQualityExecutionScore score, IEnumerable<string> claimLimitationCodes, string recordedSubmissionCanonicalJson, string qualityReportCanonicalJson)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        Provenance = provenance.Detach();
        QualityContract = qualityContract.Detach();
        ScenarioCount = scenarioCount;
        Score = score.Detach();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        RecordedSubmissionCanonicalJson = recordedSubmissionCanonicalJson;
        QualityReportCanonicalJson = qualityReportCanonicalJson;
    }

    public string SchemaVersion => CognitionQualityExecutionEvidenceModule.SchemaVersion;
    public string Status => "complete";
    public string Semantics => "offline_recorded_submission_binding_only";
    public CognitionQualityExecutionProvenance Provenance { get; }
    public CognitionQualityExecutionQualityContract QualityContract { get; }
    public int ScenarioCount { get; }
    public CognitionQualityExecutionScore Score { get; }
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string RecordedSubmissionCanonicalJson { get; }
    public string QualityReportCanonicalJson { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class CognitionQualityExecutionQualityContract
{
    internal CognitionQualityExecutionQualityContract(string corpusDigestSha256, string scoringDigestSha256, string submissionDigestSha256, string reportDigestSha256)
    {
        CorpusDigestSha256 = corpusDigestSha256;
        ScoringDigestSha256 = scoringDigestSha256;
        SubmissionDigestSha256 = submissionDigestSha256;
        ReportDigestSha256 = reportDigestSha256;
    }

    public string CorpusSchemaVersion => CognitionQualityCorpusV1.CorpusSchemaVersion;
    public string ScoringSchemaVersion => CognitionQualityCorpusV1.ScoringSchemaVersion;
    public string ReportSchemaVersion => CognitionQualityCorpusV1.ReportSchemaVersion;
    public string ValidatorIdentity => CognitionQualityCorpusV1.ValidatorIdentity;
    public string CorpusDigestSha256 { get; }
    public string ScoringDigestSha256 { get; }
    public string SubmissionDigestSha256 { get; }
    public string ReportDigestSha256 { get; }
    internal CognitionQualityExecutionQualityContract Detach() => new(CorpusDigestSha256, ScoringDigestSha256, SubmissionDigestSha256, ReportDigestSha256);
}

public sealed class CognitionQualityExecutionScore
{
    internal CognitionQualityExecutionScore(int maximumPoints, int rawPoints, int basisPoints) { MaximumPoints = maximumPoints; RawPoints = rawPoints; BasisPoints = basisPoints; }
    public int MaximumPoints { get; }
    public int RawPoints { get; }
    public int BasisPoints { get; }
    internal CognitionQualityExecutionScore Detach() => new(MaximumPoints, RawPoints, BasisPoints);
}

/// <summary>Closed error surface; messages are codes and never repeat caller-provided data.</summary>
public sealed class CognitionQualityExecutionEvidenceException : Exception
{
    internal CognitionQualityExecutionEvidenceException(string code) : base(code)
    {
        if (!CognitionQualityExecutionEvidenceErrors.IsAllowlisted(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }
    public string Code { get; }
}

/// <summary>Pure synchronous owner of the frozen v1 corpus and evidence serialization.</summary>
public static class CognitionQualityExecutionEvidenceModule
{
    public const string SchemaVersion = "snow_globe_cognition_quality_execution_evidence/v1";
    public const int MaximumEvidenceBytes = 64 * 1024;
    private static readonly string[] ClaimLimitations =
    [
        "identity_attribution_is_caller_attested",
        "no_execution_attestation",
        "offline_fixed_corpus",
        "observable_action_utility_only",
        "no_general_quality_claim",
        "no_cost_claim",
        "no_winner_claim"
    ];

    public static CognitionQualityExecutionEvidence Create(CognitionQualityExecutionProvenance provenance, IReadOnlyList<CognitionQualitySubmission> recordedBatch)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(recordedBatch);
        if (recordedBatch.Count != CognitionQualityCorpusV1.ScenarioCount)
            throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.RecordedBatchCountInvalid);

        CognitionQualitySubmission[] batch = new CognitionQualitySubmission[CognitionQualityCorpusV1.ScenarioCount];
        for (int index = 0; index < batch.Length; index++)
        {
            CognitionQualitySubmission entry = recordedBatch[index] ?? throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.RecordedBatchInvalid);
            if (!string.Equals(entry.ScenarioId, $"cq{index + 1}", StringComparison.Ordinal))
                throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.RecordedBatchOrderInvalid);
            batch[index] = new CognitionQualitySubmission(entry.ScenarioId, entry.Proposal is null ? null : entry.Proposal with { });
        }

        CognitionQualityCorpusSnapshot corpus;
        CognitionQualityReport report;
        try
        {
            corpus = CognitionQualityCorpusV1.CreateSnapshot();
            report = CognitionQuality.Evaluate(corpus, batch);
        }
        catch (CognitionQualityException exception)
        {
            throw new CognitionQualityExecutionEvidenceException(Map(exception.Code));
        }

        byte[] submission = CognitionQualityCorpusV1.WriteSubmissionEnvelope(batch);
        try
        {
            ValidateCoherence(corpus, report, submission);
            CognitionQualityExecutionQualityContract contract = new(corpus.CanonicalDigestSha256, CognitionQualityCorpusV1.ScoringDigestSha256, report.SubmissionDigestSha256, report.CanonicalDigestSha256);
            CognitionQualityExecutionScore score = new(CognitionQualityCorpusV1.MaximumPoints, report.RawPoints, report.BasisPoints);
            byte[] payload = WriteEvidence(provenance, contract, score, submission, report.CanonicalUtf8.Span, null);
            string payloadDigest = CognitionQualityHash.Sha256(payload);
            CryptographicOperations.ZeroMemory(payload);
            byte[] evidence = WriteEvidence(provenance, contract, score, submission, report.CanonicalUtf8.Span, payloadDigest);
            if (evidence.Length is 0 or > MaximumEvidenceBytes)
            {
                CryptographicOperations.ZeroMemory(evidence);
                throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.EvidenceSizeInvalid);
            }
            return new CognitionQualityExecutionEvidence(evidence, payloadDigest, provenance, contract, CognitionQualityCorpusV1.ScenarioCount, score, ClaimLimitations, Encoding.UTF8.GetString(submission), report.CanonicalJson);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(submission);
        }
    }

    private static void ValidateCoherence(CognitionQualityCorpusSnapshot corpus, CognitionQualityReport report, ReadOnlySpan<byte> submission)
    {
        if (!string.Equals(corpus.CanonicalDigestSha256, CognitionQualityCorpusV1.ExpectedManifestDigestSha256, StringComparison.Ordinal)
            || !string.Equals(CognitionQualityCorpusV1.ScoringDigestSha256, CognitionQualityCorpusV1.ExpectedScoringDigestSha256, StringComparison.Ordinal)
            || !string.Equals(report.SubmissionDigestSha256, CognitionQualityHash.Sha256(submission), StringComparison.Ordinal)
            || report.Results.Count != CognitionQualityCorpusV1.ScenarioCount
            || report.RawPoints is < 0 or > CognitionQualityCorpusV1.MaximumPoints
            || report.BasisPoints != checked(report.RawPoints * 10_000 / CognitionQualityCorpusV1.MaximumPoints)
            || report.CanonicalUtf8.Length is 0 or > CognitionQuality.MaximumReportBytes)
        {
            throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.IntegrityInvalid);
        }
        for (int index = 0; index < report.Results.Count; index++)
        {
            CognitionQualityScenarioResult result = report.Results[index];
            if (!string.Equals(result.ScenarioId, $"cq{index + 1}", StringComparison.Ordinal)
                || result.RawPoints is < 0 or > CognitionQuality.PointsPerScenario
                || result.BasisPoints != result.RawPoints * 100)
            {
                throw new CognitionQualityExecutionEvidenceException(CognitionQualityExecutionEvidenceErrors.IntegrityInvalid);
            }
        }
    }

    private static byte[] WriteEvidence(CognitionQualityExecutionProvenance provenance, CognitionQualityExecutionQualityContract contract, CognitionQualityExecutionScore score, ReadOnlySpan<byte> submission, ReadOnlySpan<byte> report, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("semantics", "offline_recorded_submission_binding_only");
        writer.WritePropertyName("provenance"); CognitionQualityExecutionProvenance.WriteTo(writer, provenance);
        writer.WritePropertyName("quality_contract");
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
        writer.WriteNumber("scenario_count", CognitionQualityCorpusV1.ScenarioCount);
        writer.WritePropertyName("score");
        writer.WriteStartObject();
        writer.WriteNumber("max_points", score.MaximumPoints);
        writer.WriteNumber("raw_points", score.RawPoints);
        writer.WriteNumber("basis_points", score.BasisPoints);
        writer.WriteEndObject();
        writer.WritePropertyName("claim_limitation_codes");
        writer.WriteStartArray(); foreach (string code in ClaimLimitations) writer.WriteStringValue(code); writer.WriteEndArray();
        writer.WritePropertyName("recorded_submission"); writer.WriteRawValue(submission, skipInputValidation: false);
        writer.WritePropertyName("quality_report"); writer.WriteRawValue(report, skipInputValidation: false);
        if (payloadDigest is not null) writer.WriteString("evidence_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static string Map(string code) => code switch
    {
        CognitionQualityErrors.EnvelopeCountInvalid => CognitionQualityExecutionEvidenceErrors.RecordedBatchCountInvalid,
        CognitionQualityErrors.EnvelopeOrderInvalid => CognitionQualityExecutionEvidenceErrors.RecordedBatchOrderInvalid,
        CognitionQualityErrors.EnvelopeInvalid or CognitionQualityErrors.ReportSizeInvalid => CognitionQualityExecutionEvidenceErrors.RecordedBatchInvalid,
        CognitionQualityErrors.CorpusSnapshotInvalid => CognitionQualityExecutionEvidenceErrors.QualityContractInvalid,
        _ => CognitionQualityExecutionEvidenceErrors.IntegrityInvalid
    };
}

internal static class CognitionQualityExecutionEvidenceErrors
{
    internal const string ProvenanceInvalid = "provenance_invalid";
    internal const string RecordedBatchCountInvalid = "recorded_batch_count_invalid";
    internal const string RecordedBatchOrderInvalid = "recorded_batch_order_invalid";
    internal const string RecordedBatchInvalid = "recorded_batch_invalid";
    internal const string QualityContractInvalid = "quality_contract_invalid";
    internal const string IntegrityInvalid = "evidence_integrity_invalid";
    internal const string EvidenceSizeInvalid = "evidence_size_invalid";
    internal static bool IsAllowlisted(string code) => code is ProvenanceInvalid or RecordedBatchCountInvalid or RecordedBatchOrderInvalid or RecordedBatchInvalid or QualityContractInvalid or IntegrityInvalid or EvidenceSizeInvalid;
}
