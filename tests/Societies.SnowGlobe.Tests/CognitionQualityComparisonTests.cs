using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityComparisonTests
{
    private const string OllamaArtifactDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OpenRouterArtifactDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string OllamaEvidenceDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string OpenRouterEvidenceDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    [Fact]
    public void NormalizedProposalEvidence_IsCanonicalBoundedRawFreeAndRoundTrips()
    {
        CognitionQualityNormalizedProposalEvidence evidence = Evidence(PreferredEnvelope(), OllamaEvidenceDigest);
        CognitionQualityNormalizedProposalEvidence validated = CognitionQualityNormalizedProposalEvidenceCodec.Validate(evidence.CanonicalUtf8);

        Assert.Equal("snow_globe_cognition_quality_normalized_proposals/v1", evidence.SchemaVersion);
        Assert.Equal(evidence.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Equal(12, validated.Proposals.Count);
        Assert.InRange(evidence.CanonicalUtf8.Length, 1, CognitionQualityNormalizedProposalEvidenceCodec.MaximumEvidenceBytes);
        Assert.DoesNotContain("prompt", string.Join('|', validated.Proposals.Select(item => item.Proposal!.AgentId)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw_response", evidence.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning_text", evidence.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential_value", evidence.CanonicalJson, StringComparison.OrdinalIgnoreCase);

        using JsonDocument document = JsonDocument.Parse(evidence.CanonicalUtf8);
        Assert.Equal(new[]
        {
            "schema_version", "status", "source_evidence_schema_version", "source_evidence_digest_sha256",
            "corpus_schema_version", "corpus_digest_sha256", "scoring_digest_sha256", "validator_identity",
            "prompt_publication_digest_sha256", "prompt_set_digest_sha256", "proposal_schema_version",
            "proposals", "claim_limitation_codes", "evidence_payload_digest_sha256"
        }, document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public void NormalizedProposalEvidence_RejectsCountOrderMalformedOversizedAndDeepInputs()
    {
        CognitionQualitySubmission[] preferred = PreferredEnvelope();
        Assert.Equal("proposal_count_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() => Evidence(preferred[..^1], OllamaEvidenceDigest)).Code);
        (preferred[0], preferred[1]) = (preferred[1], preferred[0]);
        Assert.Equal("proposal_order_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() => Evidence(preferred, OllamaEvidenceDigest)).Code);
        Assert.Equal("evidence_json_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() => CognitionQualityNormalizedProposalEvidenceCodec.Validate(Encoding.UTF8.GetBytes("{bad"))).Code);
        Assert.Equal("evidence_size_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() => CognitionQualityNormalizedProposalEvidenceCodec.Validate(new byte[CognitionQualityNormalizedProposalEvidenceCodec.MaximumEvidenceBytes + 1])).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 20) + new string(']', 20));
        Assert.Equal("evidence_json_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() => CognitionQualityNormalizedProposalEvidenceCodec.Validate(deep)).Code);
    }

    [Fact]
    public void NullableNoProposalSlotsAreCanonicalAndScoreAsSchemaAndCommandInvalid()
    {
        CognitionQualitySubmission[] proposals = PreferredEnvelope();
        proposals[0] = new("cq1", null);
        proposals[11] = new("cq12", null);
        CognitionQualityNormalizedProposalEvidence evidence = Evidence(proposals, OllamaEvidenceDigest);
        CognitionQualityNormalizedProposalEvidence validated = CognitionQualityNormalizedProposalEvidenceCodec.Validate(evidence.CanonicalUtf8);

        Assert.Null(validated.Proposals[0].Proposal);
        Assert.Null(validated.Proposals[11].Proposal);
        using JsonDocument document = JsonDocument.Parse(validated.CanonicalUtf8);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("proposals")[0].GetProperty("proposal").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("proposals")[11].GetProperty("proposal").ValueKind);

        CognitionQualityProviderEvaluation score = CognitionQualityComparisonModule.EvaluateProvider(validated);
        Assert.False(score.Scenarios[0].SchemaValid); Assert.False(score.Scenarios[0].CommandLegal);
        Assert.False(score.Scenarios[0].ResourceFeasible); Assert.Equal(0, score.Scenarios[0].GoalRelevanceBasisPoints);
        Assert.False(score.Scenarios[11].SchemaValid); Assert.False(score.Scenarios[11].CommandLegal);
        Assert.Equal("applicable", score.Scenarios[11].SafetyApplicability); Assert.False(score.Scenarios[11].SafetyPass);
        Assert.False(score.Scenarios[0].UsefulVariationSatisfied); Assert.False(score.Scenarios[11].UsefulVariationSatisfied);
    }

    [Fact]
    public void EqualInputsAndExactScoreTiesNeverSelectAProvider()
    {
        CognitionQualityNormalizedProposalEvidence same = Evidence(PreferredEnvelope(), OllamaEvidenceDigest);
        CognitionQualityComparisonArtifact equalInputs = Compare(same, same);
        Assert.Equal("insufficient_evidence", equalInputs.Recommendation);
        Assert.Contains("equal_normalized_inputs", equalInputs.RecommendationReasonCodes);

        CognitionQualitySubmission[] changed = PreferredEnvelope();
        changed[0] = new("cq1", new("agent-00", SnowGlobeActionKind.GatherWood, 64));
        CognitionQualityComparisonArtifact exactTie = Compare(same, Evidence(changed, OpenRouterEvidenceDigest));
        Assert.Equal("insufficient_evidence", exactTie.Recommendation);
        Assert.Contains("exact_automated_score_tie", exactTie.RecommendationReasonCodes);
    }

    [Fact]
    public void RepresentableContractIllegalValuesRemainScorableButUndefinedActionsFailClosed()
    {
        CognitionQualitySubmission[] undefinedAction = PreferredEnvelope();
        undefinedAction[0] = new("cq1", new("agent-00", (SnowGlobeActionKind)999, 1));
        Assert.Equal("proposal_contract_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() =>
            Evidence(undefinedAction, OllamaEvidenceDigest)).Code);

        CognitionQualitySubmission[] invalidQuantity = PreferredEnvelope();
        invalidQuantity[0] = new("cq1", new("agent-00", SnowGlobeActionKind.GatherWood, 0));
        CognitionQualityProviderEvaluation invalidQuantityScore = CognitionQualityComparisonModule.EvaluateProvider(
            Evidence(invalidQuantity, OllamaEvidenceDigest));
        Assert.True(invalidQuantityScore.Scenarios[0].SchemaValid);
        Assert.False(invalidQuantityScore.Scenarios[0].CommandLegal);
        Assert.False(invalidQuantityScore.Scenarios[0].ResourceFeasible);
        Assert.Equal(0, invalidQuantityScore.Scenarios[0].GoalRelevanceBasisPoints);

        CognitionQualitySubmission[] wrongAgent = PreferredEnvelope();
        wrongAgent[0] = new("cq1", new("agent-01", SnowGlobeActionKind.GatherWood, 1));
        CognitionQualityProviderEvaluation wrongAgentScore = CognitionQualityComparisonModule.EvaluateProvider(
            Evidence(wrongAgent, OllamaEvidenceDigest));
        Assert.True(wrongAgentScore.Scenarios[0].SchemaValid);
        Assert.False(wrongAgentScore.Scenarios[0].CommandLegal);

        CognitionQualityNormalizedProposalEvidence admitted = Evidence(PreferredEnvelope(), OllamaEvidenceDigest);
        byte[] forgedUndefined = ForgeNormalizedEvidence(admitted, proposal => proposal["action"] = 999);
        Assert.Equal("proposal_contract_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() =>
            CognitionQualityNormalizedProposalEvidenceCodec.Validate(forgedUndefined)).Code);
        CognitionQualityComparisonArtifact rejected = CognitionQualityComparisonModule.CompareCanonical(
            OllamaArtifactDigest, forgedUndefined,
            OpenRouterArtifactDigest, Evidence(PreferredEnvelope(), OpenRouterEvidenceDigest).CanonicalUtf8);
        Assert.Equal("malformed", rejected.Providers[0].EvidenceStatus);
        Assert.Null(rejected.Providers[0].AutomatedEvaluation);
        Assert.Equal("insufficient_evidence", rejected.Recommendation);

        byte[] forgedQuantity = ForgeNormalizedEvidence(admitted, proposal => proposal["quantity"] = 0);
        CognitionQualityNormalizedProposalEvidence validatedQuantity = CognitionQualityNormalizedProposalEvidenceCodec.Validate(forgedQuantity);
        Assert.False(CognitionQualityComparisonModule.EvaluateProvider(validatedQuantity).Scenarios[0].CommandLegal);
    }

    [Fact]
    public void DomainInfeasibleCommandsRemainSeparateFromAdmittedSchemaAndGoalScoring()
    {
        CognitionQualitySubmission[] proposals = PreferredEnvelope();
        proposals[1] = new("cq2", new("agent-00", SnowGlobeActionKind.GatherStone, 64));
        CognitionQualityProviderEvaluation result = CognitionQualityComparisonModule.EvaluateProvider(Evidence(proposals, OllamaEvidenceDigest));

        Assert.True(result.Scenarios[1].SchemaValid);
        Assert.True(result.Scenarios[1].CommandLegal);
        Assert.False(result.Scenarios[1].ResourceFeasible);
        Assert.Equal(0, result.Scenarios[1].GoalRelevanceBasisPoints);
    }

    [Fact]
    public void UsefulVariationRewardsFeasibleDemandCoverageNotArbitraryActionChanges()
    {
        CognitionQualityProviderEvaluation preferred = CognitionQualityComparisonModule.EvaluateProvider(Evidence(PreferredEnvelope(), OllamaEvidenceDigest));
        CognitionQualityProviderEvaluation allIdle = CognitionQualityComparisonModule.EvaluateProvider(Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest));

        Assert.Equal(10_000, preferred.Criteria.Single(item => item.CriterionId == "useful_variation").BasisPoints);
        Assert.Equal(2_000, allIdle.Criteria.Single(item => item.CriterionId == "useful_variation").BasisPoints);
        Assert.Equal(5, preferred.Scenarios.Select(item => item.UsefulVariationDemandAction).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SafetyIsScoredOnlyOnFrozenRestraintScenariosAndRejectsFeasibleBusywork()
    {
        CognitionQualitySubmission[] proposals = PreferredEnvelope();
        proposals[11] = new("cq12", new("agent-00", SnowGlobeActionKind.GatherWood, 1));
        CognitionQualityProviderEvaluation result = CognitionQualityComparisonModule.EvaluateProvider(Evidence(proposals, OllamaEvidenceDigest));

        Assert.All(result.Scenarios.Take(9), item => { Assert.Equal("not_applicable", item.SafetyApplicability); Assert.Null(item.SafetyPass); });
        Assert.True(result.Scenarios[9].SafetyPass);
        Assert.True(result.Scenarios[10].SafetyPass);
        Assert.True(result.Scenarios[11].CommandLegal);
        Assert.True(result.Scenarios[11].ResourceFeasible);
        Assert.False(result.Scenarios[11].SafetyPass);
    }

    [Fact]
    public void RecommendationSupportsBothDefaultsConditionalAndInsufficientThresholds()
    {
        CognitionQualityNormalizedProposalEvidence preferred = Evidence(PreferredEnvelope(), OllamaEvidenceDigest);
        CognitionQualityNormalizedProposalEvidence idle = Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest);
        Assert.Equal("ollama_default", Compare(preferred, idle).Recommendation);
        Assert.Equal("openrouter_default", Compare(idle, preferred).Recommendation);

        CognitionQualitySubmission[] left = AllIdleEnvelope();
        CognitionQualitySubmission[] right = AllIdleEnvelope();
        CognitionQualitySubmission[] ideal = PreferredEnvelope();
        for (int index = 0; index < 6; index++) left[index] = ideal[index];
        for (int index = 6; index < 12; index++) right[index] = ideal[index];
        left[6] = ideal[6]; // break the exact aggregate tie while retaining complementary category wins.
        Assert.Equal("conditional_routing", Compare(Evidence(left, OllamaEvidenceDigest), Evidence(right, OpenRouterEvidenceDigest)).Recommendation);

        CognitionQualitySubmission[] near = PreferredEnvelope();
        near[0] = new("cq1", new("agent-00", SnowGlobeActionKind.GatherWood, 11));
        Assert.Equal("insufficient_evidence", Compare(preferred, Evidence(near, OpenRouterEvidenceDigest)).Recommendation);
    }

    [Fact]
    public void ConditionalRoutingRequiresAggregateMarginFrom500Through1500Inclusive()
    {
        CognitionQualityNormalizedProposalEvidence lower = Evidence(ConditionalStorageEnvelope(), OllamaEvidenceDigest);
        CognitionQualityNormalizedProposalEvidence belowMinimum = Evidence(ConditionalComplementEnvelopeBelowMinimum(), OpenRouterEvidenceDigest);
        CognitionQualityProviderEvaluation lowerScore = CognitionQualityComparisonModule.EvaluateProvider(lower);
        CognitionQualityProviderEvaluation belowMinimumScore = CognitionQualityComparisonModule.EvaluateProvider(belowMinimum);
        int belowMinimumMargin = Math.Abs(lowerScore.TotalBasisPoints - belowMinimumScore.TotalBasisPoints);
        Assert.Equal(219, belowMinimumMargin);
        Assert.InRange(belowMinimumMargin, 1, CognitionQualityComparisonModule.DefaultMinimumMarginBasisPoints - 1);
        AssertComplementaryCategoryWins(lowerScore, belowMinimumScore);
        CognitionQualityComparisonArtifact insufficient = Compare(lower, belowMinimum);
        Assert.Equal("insufficient_evidence", insufficient.Recommendation);
        Assert.Contains("margin_below_default_threshold", insufficient.RecommendationReasonCodes);

        CognitionQualityNormalizedProposalEvidence atMinimum = Evidence(ConditionalComplementEnvelopeAtMinimum(), OpenRouterEvidenceDigest);
        CognitionQualityProviderEvaluation atMinimumScore = CognitionQualityComparisonModule.EvaluateProvider(atMinimum);
        Assert.Equal(CognitionQualityComparisonModule.DefaultMinimumMarginBasisPoints,
            Math.Abs(lowerScore.TotalBasisPoints - atMinimumScore.TotalBasisPoints));
        AssertComplementaryCategoryWins(lowerScore, atMinimumScore);
        Assert.Equal("conditional_routing", Compare(lower, atMinimum).Recommendation);
    }

    [Fact]
    public void MissingMalformedAndAsymmetricEvidenceProduceBoundedInsufficientArtifact()
    {
        CognitionQualityComparisonArtifact missing = CognitionQualityComparisonModule.CompareCanonical(
            OllamaArtifactDigest, null, OpenRouterArtifactDigest, Evidence(PreferredEnvelope(), OpenRouterEvidenceDigest).CanonicalUtf8);
        Assert.Equal("insufficient_evidence", missing.Recommendation);
        Assert.Equal("missing", missing.Providers[0].EvidenceStatus);
        Assert.Equal("complete", missing.Providers[1].EvidenceStatus);

        CognitionQualityComparisonArtifact malformed = CognitionQualityComparisonModule.CompareCanonical(
            OllamaArtifactDigest, Encoding.UTF8.GetBytes("{bad"), OpenRouterArtifactDigest, Evidence(PreferredEnvelope(), OpenRouterEvidenceDigest).CanonicalUtf8);
        Assert.Equal("insufficient_evidence", malformed.Recommendation);
        Assert.Equal("malformed", malformed.Providers[0].EvidenceStatus);
        Assert.DoesNotContain("{bad", malformed.CanonicalJson, StringComparison.Ordinal);
        Assert.InRange(malformed.CanonicalUtf8.Length, 1, CognitionQualityComparisonModule.MaximumArtifactBytes);
    }

    [Fact]
    public async Task ComparisonIsCanonicalDetachedCultureConcurrencyAndValidationStable()
    {
        CognitionQualityComparisonArtifact artifact = Compare(
            Evidence(PreferredEnvelope(), OllamaEvidenceDigest),
            Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest));
        CognitionQualityComparisonArtifact validated = CognitionQualityComparisonModule.Validate(artifact.CanonicalUtf8);
        Assert.Equal(artifact.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Equal(4_000, CognitionQualityComparisonModule.ConditionalMinimumScoreBasisPoints);
        Assert.Equal("0f598bd977cd361d228316cfa7e2e15d74bf75ff7a67d335f8ef8129287d6db8", CognitionQualityComparisonModule.RubricDigestSha256);
        Assert.NotEqual("cd27a0344ed2abf8db7c612ef69ac71121cc0a6024539d45b887dcce7bb81211", CognitionQualityComparisonModule.RubricDigestSha256);
        using JsonDocument canonical = JsonDocument.Parse(artifact.CanonicalUtf8);
        Assert.Equal(CognitionQualityComparisonModule.RubricDigestSha256, canonical.RootElement.GetProperty("rubric_digest_sha256").GetString());
        byte[] reboundRubric = ForgeComparisonArtifact(artifact, root => root["rubric_digest_sha256"] = new string('a', 64));
        Assert.Equal("artifact_value_invalid", Assert.Throws<CognitionQualityComparisonException>(() =>
            CognitionQualityComparisonModule.Validate(reboundRubric)).Code);
        Assert.Equal("not_recorded", artifact.HumanEvaluationStatus);
        Assert.Equal("none", artifact.HumanEvaluationScoringEffect);
        Assert.Equal(new[] { "style", "verbosity", "confidence", "reasoning_length", "latency", "price", "provider_identity" }, artifact.ExcludedSignalCodes);
        Assert.All(artifact.Providers, provider => Assert.NotNull(provider.AutomatedEvaluation));

        ReadOnlyMemory<byte> detached = artifact.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', artifact.CanonicalUtf8.Span[0]);

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(artifact.CanonicalDigestSha256, Compare(Evidence(PreferredEnvelope(), OllamaEvidenceDigest), Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest)).CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }

        string[] digests = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() => Compare(Evidence(PreferredEnvelope(), OllamaEvidenceDigest), Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest)).CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(artifact.CanonicalDigestSha256, digest));
    }

    [Fact]
    public void ProvenanceDigestArtifactTamperAndDuplicatePropertiesFailClosedWithoutEcho()
    {
        CognitionQualitySubmission[] preferred = PreferredEnvelope();
        Assert.Equal("evidence_digest_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() =>
            CognitionQualityNormalizedProposalEvidenceCodec.Create(CognitionQualityRecordingEvidenceModule.SchemaVersion, "not-a-digest-secret", preferred)).Code);
        Assert.Equal("evidence_binding_invalid", Assert.Throws<CognitionQualityNormalizedProposalEvidenceException>(() =>
            CognitionQualityNormalizedProposalEvidenceCodec.Create("unknown-source/v1", OllamaEvidenceDigest, preferred)).Code);

        CognitionQualityComparisonArtifact artifact = Compare(Evidence(preferred, OllamaEvidenceDigest), Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest));
        byte[] changed = artifact.CanonicalUtf8.ToArray(); changed[^2] ^= 1;
        CognitionQualityComparisonException changedFailure = Assert.Throws<CognitionQualityComparisonException>(() => CognitionQualityComparisonModule.Validate(changed));
        Assert.DoesNotContain("secret", changedFailure.Message, StringComparison.OrdinalIgnoreCase);
        byte[] duplicate = Encoding.UTF8.GetBytes(artifact.CanonicalJson.Replace("{\"schema_version\"", "{\"schema_version\":\"duplicate\",\"schema_version\"", StringComparison.Ordinal));
        Assert.Equal("artifact_shape_invalid", Assert.Throws<CognitionQualityComparisonException>(() => CognitionQualityComparisonModule.Validate(duplicate)).Code);

        CognitionQualityComparisonArtifact badSource = CognitionQualityComparisonModule.CompareCanonical(
            "not-a-digest-secret", Evidence(preferred, OllamaEvidenceDigest).CanonicalUtf8,
            OpenRouterArtifactDigest, Evidence(AllIdleEnvelope(), OpenRouterEvidenceDigest).CanonicalUtf8);
        Assert.Equal("malformed", badSource.Providers[0].EvidenceStatus);
        Assert.DoesNotContain("not-a-digest-secret", badSource.CanonicalJson, StringComparison.Ordinal);
        Assert.Equal("artifact_size_invalid", Assert.Throws<CognitionQualityComparisonException>(() =>
            CognitionQualityComparisonModule.Validate(new byte[CognitionQualityComparisonModule.MaximumArtifactBytes + 1])).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 20) + new string(']', 20));
        Assert.Equal("artifact_json_invalid", Assert.Throws<CognitionQualityComparisonException>(() => CognitionQualityComparisonModule.Validate(deep)).Code);
    }

    private static CognitionQualityComparisonArtifact Compare(CognitionQualityNormalizedProposalEvidence ollama, CognitionQualityNormalizedProposalEvidence openRouter) =>
        CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(OllamaArtifactDigest, ollama),
            new CognitionQualityComparisonInput(OpenRouterArtifactDigest, openRouter));

    private static CognitionQualityNormalizedProposalEvidence Evidence(IReadOnlyList<CognitionQualitySubmission> proposals, string sourceDigest) =>
        CognitionQualityNormalizedProposalEvidenceCodec.Create(CognitionQualityRecordingEvidenceModule.SchemaVersion, sourceDigest, proposals);

    private static CognitionQualitySubmission[] PreferredEnvelope() => CognitionQualityCorpusV1.CreateSnapshot().Scenarios
        .Select(scenario => new CognitionQualitySubmission(scenario.ScenarioId, Preferred(scenario.ScenarioId))).ToArray();

    private static CognitionQualitySubmission[] AllIdleEnvelope() => CognitionQualityCorpusV1.CreateSnapshot().Scenarios
        .Select(scenario => new CognitionQualitySubmission(scenario.ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle))).ToArray();

    private static CognitionQualitySubmission[] ConditionalStorageEnvelope()
    {
        CognitionQualitySubmission[] values = AllIdleEnvelope();
        values[7] = new("cq8", Preferred("cq8"));
        values[8] = new("cq9", Preferred("cq9"));
        return values;
    }

    private static CognitionQualitySubmission[] ConditionalComplementEnvelopeBelowMinimum()
    {
        CognitionQualitySubmission[] values = AllIdleEnvelope();
        values[4] = new("cq5", Preferred("cq5"));
        values[5] = new("cq6", Preferred("cq6"));
        values[6] = new("cq7", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 1));
        return values;
    }

    private static CognitionQualitySubmission[] ConditionalComplementEnvelopeAtMinimum()
    {
        CognitionQualitySubmission[] values = AllIdleEnvelope();
        values[0] = new("cq1", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 1));
        values[1] = new("cq2", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 5));
        values[2] = new("cq3", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 1));
        values[5] = new("cq6", Preferred("cq6"));
        values[6] = new("cq7", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 2));
        return values;
    }

    private static void AssertComplementaryCategoryWins(
        CognitionQualityProviderEvaluation first,
        CognitionQualityProviderEvaluation second)
    {
        Assert.Contains(first.Categories.Zip(second.Categories), pair =>
            pair.First.BasisPoints - pair.Second.BasisPoints >= CognitionQualityComparisonModule.ConditionalCategoryMarginBasisPoints);
        Assert.Contains(first.Categories.Zip(second.Categories), pair =>
            pair.Second.BasisPoints - pair.First.BasisPoints >= CognitionQualityComparisonModule.ConditionalCategoryMarginBasisPoints);
    }

    private static byte[] ForgeNormalizedEvidence(
        CognitionQualityNormalizedProposalEvidence evidence,
        Action<JsonObject> mutateProposal)
    {
        JsonObject root = JsonNode.Parse(evidence.CanonicalUtf8.Span)!.AsObject();
        JsonObject proposal = root["proposals"]!.AsArray()[0]!["proposal"]!.AsObject();
        mutateProposal(proposal);
        root.Remove("evidence_payload_digest_sha256");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root["evidence_payload_digest_sha256"] = CognitionQualityHash.Sha256(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static byte[] ForgeComparisonArtifact(
        CognitionQualityComparisonArtifact artifact,
        Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(artifact.CanonicalUtf8.Span)!.AsObject();
        mutate(root);
        root.Remove("artifact_payload_digest_sha256");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root["artifact_payload_digest_sha256"] = CognitionQualityHash.Sha256(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static SnowGlobeActionProposal Preferred(string id) => id switch
    {
        "cq1" => new("agent-00", SnowGlobeActionKind.GatherWood, 12),
        "cq2" => new("agent-00", SnowGlobeActionKind.GatherStone, 6),
        "cq3" => new("agent-00", SnowGlobeActionKind.GatherStone, 2),
        "cq4" or "cq5" or "cq6" => new("agent-00", SnowGlobeActionKind.BuildShelter),
        "cq7" => new("agent-00", SnowGlobeActionKind.GatherWood, 8),
        "cq8" or "cq9" => new("agent-00", SnowGlobeActionKind.BuildStorage),
        "cq10" or "cq11" or "cq12" => new("agent-00", SnowGlobeActionKind.Idle),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
