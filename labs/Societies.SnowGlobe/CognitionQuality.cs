using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Closed, offline scorer for the frozen cognition-quality corpus.</summary>
public static class CognitionQuality
{
    public const int PointsPerScenario = 100;
    public const int MaximumReportBytes = 32 * 1024;
    /// <summary>Maximum canonical UTF-8 submission envelope size; identities are normalized before encoding.</summary>
    public const int MaximumSubmissionBytes = 16 * 1024;

    /// <summary>Scores one exact ordered, detached submission envelope.</summary>
    public static CognitionQualityReport Evaluate(CognitionQualityCorpusSnapshot corpus, IReadOnlyList<CognitionQualitySubmission> submissions)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(submissions);
        CognitionQualityCorpusV1.ValidateSnapshot(corpus);

        CognitionQualitySubmission[] envelope = submissions.ToArray();
        IReadOnlyList<CognitionQualityScenario> scenarios = corpus.Scenarios;
        if (envelope.Length != scenarios.Count) throw new CognitionQualityException(CognitionQualityErrors.EnvelopeCountInvalid);

        for (int index = 0; index < scenarios.Count; index++)
        {
            CognitionQualitySubmission submission = envelope[index] ?? throw new CognitionQualityException(CognitionQualityErrors.EnvelopeInvalid);
            if (!string.Equals(submission.ScenarioId, scenarios[index].ScenarioId, StringComparison.Ordinal))
            {
                throw new CognitionQualityException(CognitionQualityErrors.EnvelopeOrderInvalid);
            }
            ValidateSubmittedAgentIdentity(submission.Proposal);
        }

        byte[] canonicalSubmission = CognitionQualityCorpusV1.WriteSubmissionEnvelope(envelope);
        if (canonicalSubmission.Length is 0 or > MaximumSubmissionBytes)
        {
            CryptographicOperations.ZeroMemory(canonicalSubmission);
            throw new CognitionQualityException(CognitionQualityErrors.EnvelopeInvalid);
        }
        string submissionDigest = CognitionQualityHash.Sha256(canonicalSubmission);
        CryptographicOperations.ZeroMemory(canonicalSubmission);

        List<CognitionQualityScenarioResult> results = new(scenarios.Count);
        foreach (CognitionQualitySubmission submission in envelope) results.Add(CognitionQualityCorpusV1.Score(submission.ScenarioId, submission.Proposal));
        return BuildReport(corpus, submissionDigest, results);
    }

    /// <summary>Only non-null ASCII lowercase letters, digits, and hyphens, with one through 64 UTF-16/UTF-8 units, may enter a submitted proposal.</summary>
    private static void ValidateSubmittedAgentIdentity(SnowGlobeActionProposal? proposal)
    {
        if (proposal is null) return;
        string? agentId = proposal.AgentId;
        if (agentId is null || agentId.Length is < 1 or > 64)
        {
            throw new CognitionQualityException(CognitionQualityErrors.EnvelopeInvalid);
        }
        foreach (char value in agentId)
        {
            if (!((value >= 'a' && value <= 'z') || (value >= '0' && value <= '9') || value == '-'))
            {
                throw new CognitionQualityException(CognitionQualityErrors.EnvelopeInvalid);
            }
        }
    }

    private static CognitionQualityReport BuildReport(CognitionQualityCorpusSnapshot corpus, string submissionDigest, IReadOnlyList<CognitionQualityScenarioResult> results)
    {
        int rawPoints = checked(results.Sum(result => result.RawPoints));
        int basisPoints = checked(rawPoints * 10_000 / CognitionQualityCorpusV1.MaximumPoints);
        byte[] payload = WriteReport(corpus, submissionDigest, results, rawPoints, basisPoints, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] report = WriteReport(corpus, submissionDigest, results, rawPoints, basisPoints, payloadDigest);
        if (report.Length is 0 or > MaximumReportBytes)
        {
            CryptographicOperations.ZeroMemory(report);
            throw new CognitionQualityException(CognitionQualityErrors.ReportSizeInvalid);
        }
        return new CognitionQualityReport(report, rawPoints, basisPoints, submissionDigest, results);
    }

    private static byte[] WriteReport(CognitionQualityCorpusSnapshot corpus, string submissionDigest, IReadOnlyList<CognitionQualityScenarioResult> results, int rawPoints, int basisPoints, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", CognitionQualityCorpusV1.ReportSchemaVersion);
            writer.WriteString("status", "complete");
            writer.WriteString("validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
            writer.WriteString("corpus_digest_sha256", corpus.CanonicalDigestSha256);
            writer.WriteString("scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256);
            writer.WriteString("submission_digest_sha256", submissionDigest);
            writer.WriteNumber("max_points", CognitionQualityCorpusV1.MaximumPoints);
            writer.WriteNumber("raw_points", rawPoints);
            writer.WriteNumber("basis_points", basisPoints);
            writer.WritePropertyName("disposition_counts");
            writer.WriteStartObject();
            foreach (string disposition in CognitionQualityCorpusV1.Dispositions) writer.WriteNumber(disposition, results.Count(result => string.Equals(result.Disposition, disposition, StringComparison.Ordinal)));
            writer.WriteEndObject();
            writer.WritePropertyName("claim_limitation_codes");
            writer.WriteStartArray();
            foreach (string code in CognitionQualityCorpusV1.ClaimLimitationCodes) writer.WriteStringValue(code);
            writer.WriteEndArray();
            writer.WritePropertyName("categories");
            writer.WriteStartArray();
            foreach (string categoryId in CognitionQualityCorpusV1.CategoryIds)
            {
                CognitionQualityScenarioResult[] category = results.Where(result => string.Equals(result.CategoryId, categoryId, StringComparison.Ordinal)).ToArray();
                int categoryPoints = checked(category.Sum(result => result.RawPoints));
                writer.WriteStartObject();
                writer.WriteString("category_id", categoryId);
                writer.WriteNumber("raw_points", categoryPoints);
                writer.WriteNumber("basis_points", checked(categoryPoints * 10_000 / (category.Length * PointsPerScenario)));
                writer.WritePropertyName("scenarios");
                writer.WriteStartArray();
                foreach (CognitionQualityScenarioResult result in category)
                {
                    writer.WriteStartObject();
                    writer.WriteString("scenario_id", result.ScenarioId);
                    writer.WriteNumber("raw_points", result.RawPoints);
                    writer.WriteNumber("basis_points", result.BasisPoints);
                    writer.WriteString("disposition", result.Disposition);
                    writer.WritePropertyName("limitation_codes");
                    writer.WriteStartArray();
                    foreach (string code in result.LimitationCodes) writer.WriteStringValue(code);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (payloadDigest is not null) writer.WriteString("report_payload_digest_sha256", payloadDigest);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
}

public sealed class CognitionQualityCorpusSnapshot
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityScenario[] _scenarios;
    internal CognitionQualityCorpusSnapshot(byte[] canonicalUtf8, IReadOnlyList<CognitionQualityScenario> scenarios)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _scenarios = scenarios.ToArray();
        CanonicalJson = Encoding.UTF8.GetString(_canonicalUtf8);
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }
    public string CanonicalJson { get; }
    public string CanonicalDigestSha256 { get; }
    public IReadOnlyList<CognitionQualityScenario> Scenarios => Array.AsReadOnly(_scenarios.ToArray());
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed record CognitionQualityScenario(string ScenarioId, string CategoryId, SnowGlobeObservation Observation, string StateDigestSha256, string EventDigestSha256, string ObservationDigestSha256);
public sealed record CognitionQualitySubmission(string ScenarioId, SnowGlobeActionProposal? Proposal);

public sealed class CognitionQualityScenarioResult
{
    private readonly string[] _limitationCodes;
    internal CognitionQualityScenarioResult(string scenarioId, string categoryId, int rawPoints, int basisPoints, string disposition, IEnumerable<string> limitationCodes)
    {
        ScenarioId = scenarioId;
        CategoryId = categoryId;
        RawPoints = rawPoints;
        BasisPoints = basisPoints;
        Disposition = disposition;
        _limitationCodes = limitationCodes.ToArray();
    }
    public string ScenarioId { get; }
    public string CategoryId { get; }
    public int RawPoints { get; }
    public int BasisPoints { get; }
    public string Disposition { get; }
    public IReadOnlyList<string> LimitationCodes => Array.AsReadOnly(_limitationCodes.ToArray());
}

public sealed class CognitionQualityReport
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityScenarioResult[] _results;
    internal CognitionQualityReport(byte[] canonicalUtf8, int rawPoints, int basisPoints, string submissionDigestSha256, IReadOnlyList<CognitionQualityScenarioResult> results)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _results = results.ToArray();
        CanonicalJson = Encoding.UTF8.GetString(_canonicalUtf8);
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        RawPoints = rawPoints;
        BasisPoints = basisPoints;
        SubmissionDigestSha256 = submissionDigestSha256;
    }
    public string CanonicalJson { get; }
    public string CanonicalDigestSha256 { get; }
    public string SubmissionDigestSha256 { get; }
    public int RawPoints { get; }
    public int BasisPoints { get; }
    public IReadOnlyList<CognitionQualityScenarioResult> Results => Array.AsReadOnly(_results.ToArray());
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class CognitionQualityException : Exception
{
    internal CognitionQualityException(string code) : base(code)
    {
        if (!CognitionQualityErrors.IsAllowlisted(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }
    public string Code { get; }
}

internal static class CognitionQualityErrors
{
    internal const string EnvelopeCountInvalid = "envelope_count_invalid";
    internal const string EnvelopeOrderInvalid = "envelope_order_invalid";
    internal const string EnvelopeInvalid = "envelope_invalid";
    internal const string CorpusSnapshotInvalid = "corpus_snapshot_invalid";
    internal const string ReportSizeInvalid = "report_size_invalid";
    internal static bool IsAllowlisted(string code) => code is EnvelopeCountInvalid or EnvelopeOrderInvalid or EnvelopeInvalid or CorpusSnapshotInvalid or ReportSizeInvalid;
}

internal static class CognitionQualityHash
{
    internal static string Sha256(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
