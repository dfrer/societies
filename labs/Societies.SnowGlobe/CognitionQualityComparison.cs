using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed record CognitionQualityComparisonInput(
    string SourceArtifactDigestSha256,
    CognitionQualityNormalizedProposalEvidence NormalizedProposalEvidence);

public sealed record CognitionQualityScenarioCriteria(
    string ScenarioId,
    string CategoryId,
    bool SchemaValid,
    bool CommandLegal,
    int GoalRelevanceBasisPoints,
    bool ResourceFeasible,
    string SafetyApplicability,
    bool? SafetyPass,
    string UsefulVariationDemandAction,
    bool UsefulVariationSatisfied);

public sealed record CognitionQualityCriterionAggregate(
    string CriterionId,
    int WeightBasisPoints,
    int ApplicableCount,
    int PassedCount,
    int BasisPoints);

public sealed record CognitionQualityCategoryAggregate(string CategoryId, int BasisPoints);

public sealed class CognitionQualityProviderEvaluation
{
    private readonly CognitionQualityCriterionAggregate[] _criteria;
    private readonly CognitionQualityCategoryAggregate[] _categories;
    private readonly CognitionQualityScenarioCriteria[] _scenarios;

    internal CognitionQualityProviderEvaluation(
        int totalBasisPoints,
        IReadOnlyList<CognitionQualityCriterionAggregate> criteria,
        IReadOnlyList<CognitionQualityCategoryAggregate> categories,
        IReadOnlyList<CognitionQualityScenarioCriteria> scenarios)
    {
        TotalBasisPoints = totalBasisPoints;
        _criteria = criteria.ToArray();
        _categories = categories.ToArray();
        _scenarios = scenarios.ToArray();
    }

    public int TotalBasisPoints { get; }
    public IReadOnlyList<CognitionQualityCriterionAggregate> Criteria => Array.AsReadOnly(_criteria.ToArray());
    public IReadOnlyList<CognitionQualityCategoryAggregate> Categories => Array.AsReadOnly(_categories.ToArray());
    public IReadOnlyList<CognitionQualityScenarioCriteria> Scenarios => Array.AsReadOnly(_scenarios.ToArray());
}

public sealed class CognitionQualityProviderComparison
{
    internal CognitionQualityProviderComparison(
        string providerRole,
        string evidenceStatus,
        string? sourceArtifactDigestSha256,
        CognitionQualityNormalizedProposalEvidence? evidence,
        CognitionQualityProviderEvaluation? automatedEvaluation)
    {
        ProviderRole = providerRole;
        EvidenceStatus = evidenceStatus;
        SourceArtifactDigestSha256 = sourceArtifactDigestSha256;
        NormalizedProposalEvidence = evidence is null ? null : CognitionQualityNormalizedProposalEvidenceCodec.Validate(evidence.CanonicalUtf8);
        AutomatedEvaluation = automatedEvaluation;
    }

    public string ProviderRole { get; }
    public string EvidenceStatus { get; }
    public string? SourceArtifactDigestSha256 { get; }
    public CognitionQualityNormalizedProposalEvidence? NormalizedProposalEvidence { get; }
    public CognitionQualityProviderEvaluation? AutomatedEvaluation { get; }
}

public sealed class CognitionQualityComparisonArtifact
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityProviderComparison[] _providers;
    private readonly string[] _recommendationReasonCodes;
    private readonly string[] _excludedSignalCodes;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityComparisonArtifact(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string status,
        IReadOnlyList<CognitionQualityProviderComparison> providers,
        string recommendation,
        IReadOnlyList<string> recommendationReasonCodes,
        IReadOnlyList<string> excludedSignalCodes,
        IReadOnlyList<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _providers = providers.ToArray();
        _recommendationReasonCodes = recommendationReasonCodes.ToArray();
        _excludedSignalCodes = excludedSignalCodes.ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        Status = status;
        Recommendation = recommendation;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => CognitionQualityComparisonModule.SchemaVersion;
    public string Status { get; }
    public string Recommendation { get; }
    public IReadOnlyList<string> RecommendationReasonCodes => Array.AsReadOnly(_recommendationReasonCodes.ToArray());
    public IReadOnlyList<CognitionQualityProviderComparison> Providers => Array.AsReadOnly(_providers.ToArray());
    public string HumanEvaluationStatus => "not_recorded";
    public string HumanEvaluationScoringEffect => "none";
    public IReadOnlyList<string> ExcludedSignalCodes => Array.AsReadOnly(_excludedSignalCodes.ToArray());
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class CognitionQualityComparisonException : Exception
{
    internal CognitionQualityComparisonException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "artifact_size_invalid" or "artifact_utf8_invalid" or "artifact_json_invalid" or
        "artifact_shape_invalid" or "artifact_value_invalid" or "artifact_noncanonical" or
        "artifact_digest_invalid" or "artifact_payload_digest_invalid" or "artifact_evidence_invalid" => code,
        _ => "artifact_validation_failed"
    };
}

/// <summary>Pure deterministic comparison over one frozen rubric and identical proposal-evaluation path.</summary>
public static class CognitionQualityComparisonModule
{
    public const string SchemaVersion = "snow_globe_cognition_quality_comparison/v1";
    public const string RubricSchemaVersion = "snow_globe_cognition_quality_comparison_rubric/v1";
    public const int MaximumArtifactBytes = 96 * 1024;
    public const int MaximumJsonDepth = 12;
    public const int DefaultMinimumScoreBasisPoints = 7_000;
    public const int DefaultMinimumMarginBasisPoints = 500;
    public const int ConditionalCategoryMarginBasisPoints = 1_000;
    public const int ConditionalMinimumScoreBasisPoints = 4_000;
    public const int ConditionalMaximumAggregateMarginBasisPoints = 1_500;
    public static string RubricDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        "snow_globe_cognition_quality_comparison_rubric/v1|corpus=v1|validator=snow_globe_validate_and_commit_v1|" +
        "schema=1500|legality=2000|goal=2500|feasibility=2000|safety=1000|useful_variation=1000|" +
        "goal=v1_scenario_basis_points_after_feasibility|safety=safe_restraint_idle_only|variation=feasible_preferred_action_demand_coverage|" +
        "normalized_schema=source_parser_representable_nullable_no_proposal|schema_valid=proposal_present|" +
        "default_minimum=7000|default_margin=500|conditional_category_margin=1000|" +
        "conditional_minimum_score=4000|conditional_aggregate_margin=500..1500_inclusive|" +
        "equal_inputs=insufficient|exact_tie=insufficient|missing_malformed_asymmetric=insufficient"));

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] Roles = ["ollama", "openrouter"];
    private static readonly string[] EvidenceStatuses = ["complete", "missing", "malformed"];
    private static readonly string[] RecommendationValues = ["ollama_default", "openrouter_default", "conditional_routing", "insufficient_evidence"];
    private static readonly string[] ExcludedSignals = ["style", "verbosity", "confidence", "reasoning_length", "latency", "price", "provider_identity"];
    private static readonly string[] Claims =
    [
        "automated_fixed_corpus_evidence_only",
        "normalized_proposals_only",
        "source_digests_provide_integrity_not_authenticity",
        "human_judgment_absent_and_non_scoring",
        "no_general_quality_intelligence_cost_or_commercial_claim",
        "no_world_or_simulation_authority"
    ];
    private static readonly (string Id, int Weight)[] Weights =
    [
        ("schema_validity", 1_500),
        ("command_legality", 2_000),
        ("goal_relevance", 2_500),
        ("resource_feasibility", 2_000),
        ("safety", 1_000),
        ("useful_variation", 1_000)
    ];
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "rubric_schema_version", "rubric_digest_sha256", "corpus_schema_version", "corpus_digest_sha256",
        "scoring_digest_sha256", "validator_identity", "rubric_weights_basis_points", "providers", "recommendation",
        "human_evaluation", "excluded_signal_codes", "claim_limitation_codes", "artifact_payload_digest_sha256"
    ];
    private static readonly string[] ProviderNames =
    [
        "provider_role", "evidence_status", "source_artifact_digest_sha256",
        "normalized_proposal_evidence_digest_sha256", "normalized_proposal_evidence", "automated_evaluation"
    ];

    public static CognitionQualityProviderEvaluation EvaluateProvider(CognitionQualityNormalizedProposalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        CognitionQualityNormalizedProposalEvidence validated = CognitionQualityNormalizedProposalEvidenceCodec.Validate(evidence.CanonicalUtf8);
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] submissions = validated.Proposals
            .Select(item => new CognitionQualitySubmission(
                item.ScenarioId,
                item.Proposal is null ? null : item.Proposal with { }))
            .ToArray();
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, submissions);
        List<CognitionQualityScenarioCriteria> scenarios = new(CognitionQualityCorpusV1.ScenarioCount);
        for (int index = 0; index < submissions.Length; index++)
        {
            CognitionQualitySubmission submission = submissions[index];
            CognitionQualityScenario scenario = corpus.Scenarios[index];
            CognitionQualityScenarioResult score = report.Results[index];
            bool schemaValid = submission.Proposal is not null;
            bool commandLegal = score.Disposition is not ("contract_invalid" or "no_proposal");
            bool feasible = score.Disposition is "feasible_suboptimal" or "maximum_utility";
            int relevance = feasible ? score.BasisPoints : 0;
            bool safetyApplicable = string.Equals(scenario.CategoryId, "safe_restraint", StringComparison.Ordinal);
            SnowGlobeActionKind demand = PreferredAction(scenario.ScenarioId);
            bool useful = feasible && submission.Proposal!.Action == demand;
            scenarios.Add(new CognitionQualityScenarioCriteria(
                scenario.ScenarioId,
                scenario.CategoryId,
                schemaValid,
                commandLegal,
                relevance,
                feasible,
                safetyApplicable ? "applicable" : "not_applicable",
                safetyApplicable ? feasible && submission.Proposal!.Action == SnowGlobeActionKind.Idle : null,
                demand.ToString(),
                useful));
        }

        int scenarioCount = scenarios.Count;
        int safetyCount = scenarios.Count(item => item.SafetyApplicability == "applicable");
        SnowGlobeActionKind[] demands = corpus.Scenarios.Select(item => PreferredAction(item.ScenarioId)).Distinct().ToArray();
        int usefulDemands = demands.Count(demand => scenarios.Any(item => item.UsefulVariationSatisfied && string.Equals(item.UsefulVariationDemandAction, demand.ToString(), StringComparison.Ordinal)));
        CognitionQualityCriterionAggregate[] criteria =
        [
            AggregateBoolean(Weights[0], scenarioCount, scenarios.Count(item => item.SchemaValid)),
            AggregateBoolean(Weights[1], scenarioCount, scenarios.Count(item => item.CommandLegal)),
            new(Weights[2].Id, Weights[2].Weight, scenarioCount, scenarios.Count(item => item.GoalRelevanceBasisPoints == 10_000), scenarios.Sum(item => item.GoalRelevanceBasisPoints) / scenarioCount),
            AggregateBoolean(Weights[3], scenarioCount, scenarios.Count(item => item.ResourceFeasible)),
            AggregateBoolean(Weights[4], safetyCount, scenarios.Count(item => item.SafetyPass == true)),
            AggregateBoolean(Weights[5], demands.Length, usefulDemands)
        ];
        int total = criteria.Sum(item => checked(item.WeightBasisPoints * item.BasisPoints / 10_000));
        CognitionQualityCategoryAggregate[] categories = CognitionQualityCorpusV1.CategoryIds
            .Select(category => new CognitionQualityCategoryAggregate(category, CategoryBasisPoints(category, scenarios)))
            .ToArray();
        return new CognitionQualityProviderEvaluation(total, criteria, categories, scenarios);
    }

    public static CognitionQualityComparisonArtifact Compare(
        CognitionQualityComparisonInput? ollama,
        CognitionQualityComparisonInput? openRouter)
    {
        ProviderState left = StateFromInput(ollama);
        ProviderState right = StateFromInput(openRouter);
        return Build(left, right);
    }

    public static CognitionQualityComparisonArtifact CompareCanonical(
        string? ollamaSourceArtifactDigestSha256,
        ReadOnlyMemory<byte>? ollamaEvidenceCanonicalUtf8,
        string? openRouterSourceArtifactDigestSha256,
        ReadOnlyMemory<byte>? openRouterEvidenceCanonicalUtf8)
    {
        ProviderState left = StateFromCanonical(ollamaSourceArtifactDigestSha256, ollamaEvidenceCanonicalUtf8);
        ProviderState right = StateFromCanonical(openRouterSourceArtifactDigestSha256, openRouterEvidenceCanonicalUtf8);
        return Build(left, right);
    }

    public static CognitionQualityComparisonArtifact Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumArtifactBytes) throw Failure("artifact_size_invalid");
        try { _ = StrictUtf8.GetString(canonicalUtf8.Span); }
        catch (DecoderFallbackException) { throw Failure("artifact_utf8_invalid"); }

        JsonDocument document;
        try
        {
            RejectDuplicateProperties(canonicalUtf8.Span);
            Utf8JsonReader reader = new(canonicalUtf8.Span, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaximumJsonDepth });
            document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("artifact_json_invalid"); }
        }
        catch (CognitionQualityComparisonException) { throw; }
        catch (JsonException) { throw Failure("artifact_json_invalid"); }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireObjectAndOrder(root, RootNames);
            RequireCanonicalScalars(root);
            RequireString(root, "schema_version", SchemaVersion);
            RequireClosedString(root, "status", ["complete", "incomplete"]);
            RequireString(root, "rubric_schema_version", RubricSchemaVersion);
            RequireString(root, "rubric_digest_sha256", RubricDigestSha256);
            CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
            RequireString(root, "corpus_schema_version", CognitionQualityCorpusV1.CorpusSchemaVersion);
            RequireString(root, "corpus_digest_sha256", corpus.CanonicalDigestSha256);
            RequireString(root, "scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256);
            RequireString(root, "validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
            ValidateWeights(root.GetProperty("rubric_weights_basis_points"));
            JsonElement providers = root.GetProperty("providers");
            if (providers.ValueKind != JsonValueKind.Array || providers.GetArrayLength() != 2) throw Failure("artifact_shape_invalid");
            ProviderState[] states = new ProviderState[2];
            int providerIndex = 0;
            foreach (JsonElement provider in providers.EnumerateArray()) states[providerIndex] = ParseProvider(provider, Roles[providerIndex++]);
            CognitionQualityComparisonArtifact recreated = Build(states[0], states[1]);
            if (!recreated.CanonicalUtf8.Span.SequenceEqual(canonicalUtf8.Span)) throw Failure("artifact_noncanonical");
            return recreated;
        }
    }

    private static CognitionQualityComparisonArtifact Build(ProviderState left, ProviderState right)
    {
        CognitionQualityProviderComparison[] providers =
        [
            ToProvider(Roles[0], left),
            ToProvider(Roles[1], right)
        ];
        (string recommendation, string[] reasons) = Recommend(providers);
        string status = providers.All(item => item.EvidenceStatus == "complete") ? "complete" : "incomplete";
        byte[] payload = Write(status, providers, recommendation, reasons, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(status, providers, recommendation, reasons, payloadDigest);
        if (canonical.Length is < 1 or > MaximumArtifactBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("artifact_size_invalid");
        }
        return new CognitionQualityComparisonArtifact(canonical, payloadDigest, status, providers, recommendation, reasons, ExcludedSignals, Claims);
    }

    private static CognitionQualityProviderComparison ToProvider(string role, ProviderState state)
    {
        CognitionQualityProviderEvaluation? evaluation = state.Status == "complete" && state.Evidence is not null ? EvaluateProvider(state.Evidence) : null;
        return new CognitionQualityProviderComparison(role, state.Status, state.SourceArtifactDigest, state.Evidence, evaluation);
    }

    private static ProviderState StateFromInput(CognitionQualityComparisonInput? input)
    {
        if (input is null) return new ProviderState("missing", null, null);
        if (!IsDigest(input.SourceArtifactDigestSha256) || input.NormalizedProposalEvidence is null) return new ProviderState("malformed", null, null);
        try
        {
            CognitionQualityNormalizedProposalEvidence evidence = CognitionQualityNormalizedProposalEvidenceCodec.Validate(input.NormalizedProposalEvidence.CanonicalUtf8);
            return new ProviderState("complete", input.SourceArtifactDigestSha256, evidence);
        }
        catch (CognitionQualityNormalizedProposalEvidenceException) { return new ProviderState("malformed", input.SourceArtifactDigestSha256, null); }
    }

    private static ProviderState StateFromCanonical(string? sourceDigest, ReadOnlyMemory<byte>? canonical)
    {
        if (!canonical.HasValue) return new ProviderState("missing", IsDigest(sourceDigest) ? sourceDigest : null, null);
        if (!IsDigest(sourceDigest)) return new ProviderState("malformed", null, null);
        try { return new ProviderState("complete", sourceDigest, CognitionQualityNormalizedProposalEvidenceCodec.Validate(canonical.Value)); }
        catch (CognitionQualityNormalizedProposalEvidenceException) { return new ProviderState("malformed", sourceDigest, null); }
    }

    private static (string Recommendation, string[] Reasons) Recommend(IReadOnlyList<CognitionQualityProviderComparison> providers)
    {
        CognitionQualityProviderComparison left = providers[0];
        CognitionQualityProviderComparison right = providers[1];
        if (left.EvidenceStatus != "complete" || right.EvidenceStatus != "complete")
            return ("insufficient_evidence", ["symmetric_complete_evidence_required"]);
        if (ProposalBatchesEqual(left.NormalizedProposalEvidence!, right.NormalizedProposalEvidence!))
            return ("insufficient_evidence", ["equal_normalized_inputs"]);
        CognitionQualityProviderEvaluation leftEvaluation = left.AutomatedEvaluation!;
        CognitionQualityProviderEvaluation rightEvaluation = right.AutomatedEvaluation!;
        if (leftEvaluation.TotalBasisPoints == rightEvaluation.TotalBasisPoints)
            return ("insufficient_evidence", ["exact_automated_score_tie"]);

        int margin = Math.Abs(leftEvaluation.TotalBasisPoints - rightEvaluation.TotalBasisPoints);
        if (margin < DefaultMinimumMarginBasisPoints)
            return ("insufficient_evidence", ["margin_below_default_threshold"]);
        bool leftCategory = CategoryWins(leftEvaluation, rightEvaluation);
        bool rightCategory = CategoryWins(rightEvaluation, leftEvaluation);
        if (leftCategory && rightCategory
            && margin <= ConditionalMaximumAggregateMarginBasisPoints
            && leftEvaluation.TotalBasisPoints >= ConditionalMinimumScoreBasisPoints
            && rightEvaluation.TotalBasisPoints >= ConditionalMinimumScoreBasisPoints)
            return ("conditional_routing", ["complementary_category_evidence", "aggregate_margin_within_conditional_bound"]);

        CognitionQualityProviderComparison winner = leftEvaluation.TotalBasisPoints > rightEvaluation.TotalBasisPoints ? left : right;
        if (winner.AutomatedEvaluation!.TotalBasisPoints >= DefaultMinimumScoreBasisPoints && margin >= DefaultMinimumMarginBasisPoints)
            return (winner.ProviderRole == "ollama" ? "ollama_default" : "openrouter_default", ["minimum_score_met", "minimum_margin_met"]);
        return ("insufficient_evidence", ["winner_score_below_default_threshold"]);
    }

    private static bool ProposalBatchesEqual(CognitionQualityNormalizedProposalEvidence left, CognitionQualityNormalizedProposalEvidence right) =>
        left.Proposals.SequenceEqual(right.Proposals);

    private static bool CategoryWins(CognitionQualityProviderEvaluation candidate, CognitionQualityProviderEvaluation other) =>
        candidate.Categories.Zip(other.Categories).Any(pair => pair.First.BasisPoints - pair.Second.BasisPoints >= ConditionalCategoryMarginBasisPoints);

    private static CognitionQualityCriterionAggregate AggregateBoolean((string Id, int Weight) weight, int applicable, int passed) =>
        new(weight.Id, weight.Weight, applicable, passed, applicable == 0 ? 0 : checked(passed * 10_000 / applicable));

    private static int CategoryBasisPoints(string category, IReadOnlyList<CognitionQualityScenarioCriteria> scenarios)
    {
        CognitionQualityScenarioCriteria[] selected = scenarios.Where(item => string.Equals(item.CategoryId, category, StringComparison.Ordinal)).ToArray();
        int weight = category == "safe_restraint" ? 9_000 : 8_000;
        int total = 0;
        foreach (CognitionQualityScenarioCriteria item in selected)
        {
            int points = (item.SchemaValid ? 1_500 : 0)
                + (item.CommandLegal ? 2_000 : 0)
                + checked(2_500 * item.GoalRelevanceBasisPoints / 10_000)
                + (item.ResourceFeasible ? 2_000 : 0)
                + (item.SafetyPass == true ? 1_000 : 0);
            total += checked(points * 10_000 / weight);
        }
        return total / selected.Length;
    }

    private static SnowGlobeActionKind PreferredAction(string scenarioId) => scenarioId switch
    {
        "cq1" or "cq7" => SnowGlobeActionKind.GatherWood,
        "cq2" or "cq3" => SnowGlobeActionKind.GatherStone,
        "cq4" or "cq5" or "cq6" => SnowGlobeActionKind.BuildShelter,
        "cq8" or "cq9" => SnowGlobeActionKind.BuildStorage,
        "cq10" or "cq11" or "cq12" => SnowGlobeActionKind.Idle,
        _ => throw Failure("artifact_evidence_invalid")
    };

    private static byte[] Write(
        string status,
        IReadOnlyList<CognitionQualityProviderComparison> providers,
        string recommendation,
        IReadOnlyList<string> reasons,
        string? payloadDigest)
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion); writer.WriteString("status", status); writer.WriteString("rubric_schema_version", RubricSchemaVersion); writer.WriteString("rubric_digest_sha256", RubricDigestSha256);
        writer.WriteString("corpus_schema_version", CognitionQualityCorpusV1.CorpusSchemaVersion); writer.WriteString("corpus_digest_sha256", corpus.CanonicalDigestSha256);
        writer.WriteString("scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256); writer.WriteString("validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
        writer.WritePropertyName("rubric_weights_basis_points"); writer.WriteStartObject(); foreach ((string id, int weight) in Weights) writer.WriteNumber(id, weight); writer.WriteEndObject();
        writer.WritePropertyName("providers"); writer.WriteStartArray(); foreach (CognitionQualityProviderComparison provider in providers) WriteProvider(writer, provider); writer.WriteEndArray();
        writer.WritePropertyName("recommendation"); writer.WriteStartObject(); writer.WriteString("value", recommendation); writer.WritePropertyName("reason_codes"); WriteStrings(writer, reasons); writer.WriteEndObject();
        writer.WritePropertyName("human_evaluation"); writer.WriteStartObject(); writer.WriteString("status", "not_recorded"); writer.WriteString("scoring_effect", "none"); writer.WriteNull("commentary"); writer.WriteEndObject();
        writer.WritePropertyName("excluded_signal_codes"); WriteStrings(writer, ExcludedSignals);
        writer.WritePropertyName("claim_limitation_codes"); WriteStrings(writer, Claims);
        if (payloadDigest is not null) writer.WriteString("artifact_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void WriteProvider(Utf8JsonWriter writer, CognitionQualityProviderComparison provider)
    {
        writer.WriteStartObject(); writer.WriteString("provider_role", provider.ProviderRole); writer.WriteString("evidence_status", provider.EvidenceStatus);
        WriteNullableString(writer, "source_artifact_digest_sha256", provider.SourceArtifactDigestSha256);
        WriteNullableString(writer, "normalized_proposal_evidence_digest_sha256", provider.NormalizedProposalEvidence?.CanonicalDigestSha256);
        writer.WritePropertyName("normalized_proposal_evidence");
        if (provider.NormalizedProposalEvidence is null) writer.WriteNullValue(); else writer.WriteRawValue(provider.NormalizedProposalEvidence.CanonicalUtf8.Span, skipInputValidation: false);
        writer.WritePropertyName("automated_evaluation");
        if (provider.AutomatedEvaluation is null) writer.WriteNullValue(); else WriteEvaluation(writer, provider.AutomatedEvaluation);
        writer.WriteEndObject();
    }

    private static void WriteEvaluation(Utf8JsonWriter writer, CognitionQualityProviderEvaluation evaluation)
    {
        writer.WriteStartObject(); writer.WriteNumber("total_basis_points", evaluation.TotalBasisPoints);
        writer.WritePropertyName("criteria"); writer.WriteStartArray();
        foreach (CognitionQualityCriterionAggregate item in evaluation.Criteria)
        {
            writer.WriteStartObject(); writer.WriteString("criterion_id", item.CriterionId); writer.WriteNumber("weight_basis_points", item.WeightBasisPoints);
            writer.WriteNumber("applicable_count", item.ApplicableCount); writer.WriteNumber("passed_count", item.PassedCount); writer.WriteNumber("basis_points", item.BasisPoints); writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WritePropertyName("categories"); writer.WriteStartArray();
        foreach (CognitionQualityCategoryAggregate item in evaluation.Categories) { writer.WriteStartObject(); writer.WriteString("category_id", item.CategoryId); writer.WriteNumber("basis_points", item.BasisPoints); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WritePropertyName("scenarios"); writer.WriteStartArray();
        foreach (CognitionQualityScenarioCriteria item in evaluation.Scenarios)
        {
            writer.WriteStartObject(); writer.WriteString("scenario_id", item.ScenarioId); writer.WriteString("category_id", item.CategoryId);
            writer.WriteBoolean("schema_valid", item.SchemaValid); writer.WriteBoolean("command_legal", item.CommandLegal); writer.WriteNumber("goal_relevance_basis_points", item.GoalRelevanceBasisPoints);
            writer.WriteBoolean("resource_feasible", item.ResourceFeasible); writer.WriteString("safety_applicability", item.SafetyApplicability);
            if (item.SafetyPass.HasValue) writer.WriteBoolean("safety_pass", item.SafetyPass.Value); else writer.WriteNull("safety_pass");
            writer.WriteString("useful_variation_demand_action", item.UsefulVariationDemandAction); writer.WriteBoolean("useful_variation_satisfied", item.UsefulVariationSatisfied); writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WriteEndObject();
    }

    private static ProviderState ParseProvider(JsonElement provider, string expectedRole)
    {
        RequireObjectAndOrder(provider, ProviderNames);
        RequireString(provider, "provider_role", expectedRole);
        string status = RequireClosedString(provider, "evidence_status", EvidenceStatuses);
        string? sourceDigest = RequireNullableDigest(provider, "source_artifact_digest_sha256");
        string? evidenceDigest = RequireNullableDigest(provider, "normalized_proposal_evidence_digest_sha256");
        JsonElement evidenceValue = provider.GetProperty("normalized_proposal_evidence");
        JsonElement evaluationValue = provider.GetProperty("automated_evaluation");
        if (status == "complete")
        {
            if (sourceDigest is null || evidenceDigest is null || evidenceValue.ValueKind != JsonValueKind.Object || evaluationValue.ValueKind != JsonValueKind.Object)
                throw Failure("artifact_evidence_invalid");
            byte[] evidenceBytes = Canonicalize(evidenceValue);
            try
            {
                CognitionQualityNormalizedProposalEvidence evidence = CognitionQualityNormalizedProposalEvidenceCodec.Validate(evidenceBytes);
                if (!string.Equals(evidence.CanonicalDigestSha256, evidenceDigest, StringComparison.Ordinal)) throw Failure("artifact_evidence_invalid");
                return new ProviderState(status, sourceDigest, evidence);
            }
            catch (CognitionQualityNormalizedProposalEvidenceException) { throw Failure("artifact_evidence_invalid"); }
            finally { CryptographicOperations.ZeroMemory(evidenceBytes); }
        }
        if (evidenceDigest is not null || evidenceValue.ValueKind != JsonValueKind.Null || evaluationValue.ValueKind != JsonValueKind.Null)
            throw Failure("artifact_evidence_invalid");
        return new ProviderState(status, sourceDigest, null);
    }

    private static void ValidateWeights(JsonElement value)
    {
        RequireObjectAndOrder(value, Weights.Select(item => item.Id).ToArray());
        foreach ((string id, int weight) in Weights)
            if (value.GetProperty(id).ValueKind != JsonValueKind.Number || !value.GetProperty(id).TryGetInt32(out int actual) || actual != weight) throw Failure("artifact_value_invalid");
    }

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure("artifact_shape_invalid");
        JsonProperty[] properties = value.EnumerateObject().ToArray(); if (properties.Length != names.Count) throw Failure("artifact_shape_invalid");
        for (int index = 0; index < names.Count; index++) if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal)) throw Failure("artifact_shape_invalid");
    }

    private static string RequireClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        JsonElement value = owner.GetProperty(name); string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !allowed.Contains(text, StringComparer.Ordinal)) throw Failure("artifact_value_invalid"); return text;
    }

    private static void RequireString(JsonElement owner, string name, string expected)
    {
        if (!string.Equals(RequireClosedString(owner, name, [expected]), expected, StringComparison.Ordinal)) throw Failure("artifact_value_invalid");
    }

    private static string? RequireNullableDigest(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name); if (value.ValueKind == JsonValueKind.Null) return null;
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null; if (!IsDigest(text)) throw Failure("artifact_digest_invalid"); return text;
    }

    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value) { if (value is null) writer.WriteNull(name); else writer.WriteString(name, value); }
    private static void WriteStrings(Utf8JsonWriter writer, IEnumerable<string> values) { writer.WriteStartArray(); foreach (string value in values) writer.WriteStringValue(value); writer.WriteEndArray(); }

    private static void RequireCanonicalScalars(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { foreach (JsonProperty property in value.EnumerateObject()) RequireCanonicalScalars(property.Value); return; }
        if (value.ValueKind == JsonValueKind.Array) { foreach (JsonElement item in value.EnumerateArray()) RequireCanonicalScalars(item); return; }
        if (value.ValueKind == JsonValueKind.String && !string.Equals(JsonSerializer.Serialize(value.GetString()), value.GetRawText(), StringComparison.Ordinal)) throw Failure("artifact_noncanonical");
        if (value.ValueKind == JsonValueKind.Number && (!value.TryGetInt64(out long integer) || !string.Equals(integer.ToString(System.Globalization.CultureInfo.InvariantCulture), value.GetRawText(), StringComparison.Ordinal))) throw Failure("artifact_noncanonical");
    }

    private static byte[] Canonicalize(JsonElement value) { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }); value.WriteTo(writer); writer.Flush(); return buffer.WrittenSpan.ToArray(); }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = MaximumJsonDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && (stack.Count == 0 || !stack.Peek().Add(reader.GetString()!))) throw Failure("artifact_shape_invalid");
        }
    }

    private sealed record ProviderState(string Status, string? SourceArtifactDigest, CognitionQualityNormalizedProposalEvidence? Evidence);
    private static CognitionQualityComparisonException Failure(string code) => new(code);
}
