using System.Globalization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityScoreSummaryTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public async Task CompleteRecordingEvidence_ProjectsCanonicalDetachedCultureAndConcurrencyStableSummary()
    {
        CognitionQualityRecordingEvidence evidence = Evidence();
        CognitionQualityScoreSummary summary = CognitionQualityScoreSummaryCodec.Create(evidence);
        Assert.Equal("d9f53db1f8157ad05f4cc82ca4c63867d2fb7d4ff9881c14ba06b5a072011307", summary.CanonicalDigestSha256);
        Assert.Equal("afedc0059f9eb2601413ef10ffa74569fb487ab0e66c02f454f364bff7c09812", summary.PayloadDigestSha256);

        Assert.Equal(CognitionQualityScoreSummaryCodec.SchemaVersion, summary.SchemaVersion);
        Assert.Equal("complete", summary.Status);
        Assert.Equal(evidence.RecordedResponseRun.ExecutionEvidence.Provenance.ProvenanceDigestSha256, summary.ProvenanceDigestSha256);
        Assert.Equal(evidence.CanonicalDigestSha256, summary.RecordingEvidenceDigestSha256);
        Assert.Equal(evidence.RecordedResponseRun.ExecutionEvidence.CanonicalDigestSha256, summary.ExecutionEvidenceDigestSha256);
        Assert.Equal(evidence.RecordedResponseRun.ExecutionEvidence.QualityContract.ReportDigestSha256, summary.QualityReportDigestSha256);
        Assert.Equal(12, summary.ScenarioCount);
        Assert.Equal(1_200, summary.MaximumPoints);
        Assert.Equal(summary.RawPoints * 10_000 / summary.MaximumPoints, summary.BasisPoints);
        Assert.Equal(12, summary.DispositionCounts.Sum(static value => value.Count));
        Assert.Equal(4, summary.Categories.Count);
        Assert.Equal(12, summary.Categories.Sum(static value => value.Scenarios.Count));
        Assert.Equal(summary.RawPoints, summary.Categories.Sum(static value => value.RawPoints));
        Assert.Equal(summary.CanonicalDigestSha256, CognitionQualityHash.Sha256(summary.CanonicalUtf8.Span));
        Assert.InRange(summary.CanonicalUtf8.Length, 1, CognitionQualityScoreSummaryCodec.MaximumSummaryBytes);

        using JsonDocument document = JsonDocument.Parse(summary.CanonicalUtf8);
        Assert.Equal(evidence.RecordedResponseRun.ExecutionEvidence.QualityReportCanonicalJson, document.RootElement.GetProperty("quality_report").GetRawText());
        Assert.DoesNotContain("recorded_submission", summary.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("agent_id", summary.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GatherWood", summary.CanonicalJson, StringComparison.Ordinal);

        ReadOnlyMemory<byte> detached = summary.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', summary.CanonicalUtf8.Span[0]);

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(summary.CanonicalDigestSha256, CognitionQualityScoreSummaryCodec.Create(evidence).CanonicalDigestSha256);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        string[] digests = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => CognitionQualityScoreSummaryCodec.Create(evidence).CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(summary.CanonicalDigestSha256, digest));
    }

    [Fact]
    public void StrictCodecRejectsMalformedDuplicateDeepOversizeNoncanonicalUnknownAndIntegrityForgeries()
    {
        CognitionQualityScoreSummary summary = CognitionQualityScoreSummaryCodec.Create(Evidence());
        byte[] valid = summary.CanonicalUtf8.ToArray();
        string json = summary.CanonicalJson;

        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(ReadOnlyMemory<byte>.Empty));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(new byte[CognitionQualityScoreSummaryCodec.MaximumSummaryBytes + 1]));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(new byte[] { 0xef, 0xbb, 0xbf }.Concat(valid).ToArray()));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(new byte[] { 0xff }));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes(json + "{}")));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes(json.Replace(
            "{\"schema_version\":", "{\"schema_version\":\"snow_globe_cognition_quality_score_summary/v1\",\"schema_version\":", StringComparison.Ordinal))));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes(json.Replace(
            "{\"schema_version\":", "{\"unknown\":0,\"schema_version\":", StringComparison.Ordinal))));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes(json.Replace(
            "\"scenario_count\":12,", string.Empty, StringComparison.Ordinal))));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes(json.Replace(
            "\"scenario_count\":12", "\"scenario_count\":12.0", StringComparison.Ordinal))));
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(Encoding.UTF8.GetBytes("{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{\"i\":0}}}}}}}}}")));

        JsonObject categoryForgery = JsonNode.Parse(valid)!.AsObject();
        categoryForgery["categories"]![0]!["raw_points"] = categoryForgery["categories"]![0]!["raw_points"]!.GetValue<int>() + 1;
        RecomputeLastDigest(categoryForgery);
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(JsonSerializer.SerializeToUtf8Bytes(categoryForgery)));

        JsonObject dispositionForgery = JsonNode.Parse(valid)!.AsObject();
        dispositionForgery["disposition_counts"]!["maximum_utility"] = 11;
        dispositionForgery["disposition_counts"]!["feasible_suboptimal"] = 1;
        RecomputeLastDigest(dispositionForgery);
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(JsonSerializer.SerializeToUtf8Bytes(dispositionForgery)));

        JsonObject reportForgery = JsonNode.Parse(valid)!.AsObject();
        JsonObject report = reportForgery["quality_report"]!.AsObject();
        report["categories"]![0]!["scenarios"]![0]!["raw_points"] = 99;
        RecomputeLastDigest(report, "report_payload_digest_sha256");
        RecomputeLastDigest(reportForgery);
        Assert.Throws<CognitionQualityScoreSummaryException>(() => CognitionQualityScoreSummaryCodec.Validate(JsonSerializer.SerializeToUtf8Bytes(reportForgery)));
    }

    [Fact]
    public void RawSentinelResponsesNeverEnterSummaryAndAllViewsRecomputeExactReportArithmetic()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = CognitionQualityExecutionProvenance.ForLocal(
            "model-v1", Revision, PolicyDigest, publication.PromptRevision,
            CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = Encoding.UTF8.GetBytes("RAW_SENTINEL_DO_NOT_RETAIN");
        CognitionQualityScoreSummary summary = CognitionQualityScoreSummaryCodec.Create(
            CognitionQualityRecordingEvidenceModule.Create(publication, provenance, responses));

        Assert.DoesNotContain("RAW_SENTINEL_DO_NOT_RETAIN", summary.CanonicalJson, StringComparison.Ordinal);
        Assert.Equal(summary.RawPoints, summary.Categories.Sum(static category => category.RawPoints));
        Assert.Equal(summary.ScenarioCount, summary.Categories.Sum(static category => category.Scenarios.Count));
        Assert.Equal(summary.ScenarioCount, summary.DispositionCounts.Sum(static disposition => disposition.Count));
        Assert.All(summary.Categories.SelectMany(static category => category.Scenarios), static scenario =>
        {
            Assert.InRange(scenario.RawPoints, 0, CognitionQuality.PointsPerScenario);
            Assert.Equal(scenario.RawPoints * 100, scenario.BasisPoints);
        });
    }

    [Fact]
    public void ProjectionRejectsTypedRecordingRunAndExecutionSplitBrainForgeries()
    {
        CognitionQualityRecordingEvidence first = Evidence();
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = CognitionQualityExecutionProvenance.ForLocal(
            "model-v1", Revision, PolicyDigest, publication.PromptRevision,
            CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
        ReadOnlyMemory<byte>[] changedResponses = Responses();
        changedResponses[0] = Encoding.UTF8.GetBytes("{\"agent_id\":\"agent-00\",\"action\":\"Idle\",\"quantity\":1}");
        CognitionQualityRecordingEvidence second = CognitionQualityRecordingEvidenceModule.Create(publication, provenance, changedResponses);

        CognitionQualityRecordingEvidence runSplit = new(
            first.CanonicalUtf8.ToArray(), first.PayloadDigestSha256, first.PromptPublication,
            second.RecordedResponseRun, first.ResponseSetDigestSha256, first.ClaimLimitationCodes);
        CognitionQualityScoreSummaryException runError = Assert.Throws<CognitionQualityScoreSummaryException>(
            () => CognitionQualityScoreSummaryCodec.Create(runSplit));
        Assert.Equal("score_summary_source_invalid", runError.Code);

        CognitionQualityRecordedResponseRun executionSplitRun = new(
            first.RecordedResponseRun.CanonicalUtf8.ToArray(), first.RecordedResponseRun.PayloadDigestSha256,
            first.RecordedResponseRun.ResponseBindings, first.RecordedResponseRun.ProposalBatch,
            second.RecordedResponseRun.ExecutionEvidence, first.RecordedResponseRun.ClaimLimitationCodes);
        CognitionQualityRecordingEvidence executionSplit = new(
            first.CanonicalUtf8.ToArray(), first.PayloadDigestSha256, first.PromptPublication,
            executionSplitRun, first.ResponseSetDigestSha256, first.ClaimLimitationCodes);
        CognitionQualityScoreSummaryException executionError = Assert.Throws<CognitionQualityScoreSummaryException>(
            () => CognitionQualityScoreSummaryCodec.Create(executionSplit));
        Assert.Equal("score_summary_source_invalid", executionError.Code);
    }

    [Theory]
    [InlineData(1, 100, "maximum_utility", "observable_target_met", true)]
    [InlineData(1, 0, "no_proposal", "proposal_missing", true)]
    [InlineData(1, 0, "contract_invalid", "proposal_contract_invalid", true)]
    [InlineData(1, 0, "domain_rejected", "proposal_domain_rejected", true)]
    [InlineData(1, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(2, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(3, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(4, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(5, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(6, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(8, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(9, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(12, 10, "feasible_suboptimal", "lower_observable_utility", true)]
    [InlineData(1, 25, "feasible_suboptimal", "observable_progress_available", true)]
    [InlineData(1, 31, "feasible_suboptimal", "partial_observable_progress", true)]
    [InlineData(2, 37, "feasible_suboptimal", "partial_observable_progress", true)]
    [InlineData(3, 62, "feasible_suboptimal", "partial_observable_progress", true)]
    [InlineData(7, 34, "feasible_suboptimal", "partial_observable_progress", true)]
    [InlineData(1, 100, "no_proposal", "proposal_missing", false)]
    [InlineData(1, 100, "maximum_utility", "proposal_missing", false)]
    [InlineData(1, 99, "maximum_utility", "observable_target_met", false)]
    [InlineData(1, 30, "feasible_suboptimal", "partial_observable_progress", false)]
    [InlineData(1, 25, "feasible_suboptimal", "partial_observable_progress", false)]
    [InlineData(1, 31, "feasible_suboptimal", "observable_progress_available", false)]
    [InlineData(7, 10, "feasible_suboptimal", "lower_observable_utility", false)]
    [InlineData(10, 10, "feasible_suboptimal", "lower_observable_utility", false)]
    [InlineData(11, 10, "feasible_suboptimal", "lower_observable_utility", false)]
    [InlineData(10, 25, "feasible_suboptimal", "observable_progress_available", false)]
    [InlineData(4, 31, "feasible_suboptimal", "partial_observable_progress", false)]
    public void StrictCodecEnforcesExactPerScenarioScoringTuple(
        int scenarioOrdinal,
        int rawPoints,
        string disposition,
        string limitationCode,
        bool accepted)
    {
        CognitionQualityScoreSummary summary = CognitionQualityScoreSummaryCodec.Create(Evidence());
        byte[] forged = ForgeTuple(summary, scenarioOrdinal, rawPoints, disposition, limitationCode);

        if (accepted)
        {
            CognitionQualityScoreSummary validated = CognitionQualityScoreSummaryCodec.Validate(forged);
            Assert.Equal(rawPoints, validated.Categories.SelectMany(static value => value.Scenarios)
                .Single(value => value.ScenarioId == $"cq{scenarioOrdinal}").RawPoints);
        }
        else
        {
            CognitionQualityScoreSummaryException error = Assert.Throws<CognitionQualityScoreSummaryException>(
                () => CognitionQualityScoreSummaryCodec.Validate(forged));
            Assert.Equal("score_summary_scenarios_invalid", error.Code);
        }
    }

    [Theory]
    [InlineData(true, int.MaxValue)]
    [InlineData(true, int.MinValue)]
    [InlineData(true, -1)]
    [InlineData(true, 13)]
    [InlineData(true, 11)]
    [InlineData(false, int.MaxValue)]
    [InlineData(false, int.MinValue)]
    [InlineData(false, -1)]
    [InlineData(false, 13)]
    [InlineData(false, 11)]
    public void ExtremeAndInvalidDispositionCountsAlwaysFailClosed(bool embeddedReport, int count)
    {
        CognitionQualityScoreSummary summary = CognitionQualityScoreSummaryCodec.Create(Evidence());
        byte[] forged = ForgeDispositionCount(summary, embeddedReport, count);

        CognitionQualityScoreSummaryException error = Assert.Throws<CognitionQualityScoreSummaryException>(
            () => CognitionQualityScoreSummaryCodec.Validate(forged));
        Assert.Equal("score_summary_dispositions_invalid", error.Code);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void PublicSummarySurfaceHasNoCreatorOptionsDelegateIoProviderTaskOrWorldAuthority()
    {
        Assert.Empty(typeof(CognitionQualityScoreSummary).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.False(typeof(CognitionQualityScoreSummaryCodec).IsPublic);
        Type[] types =
        [
            typeof(CognitionQualityScoreSummary), typeof(CognitionQualityScoreCategory),
            typeof(CognitionQualityScoreScenario), typeof(CognitionQualityScoreDispositionCount)
        ];
        MemberInfo[] members = types.SelectMany(static type => type.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).ToArray();
        string surface = string.Join('|', members.Select(static member => member.ToString()));
        foreach (string forbidden in new[] { "HttpClient", "Stream", "Socket", "File", "Credential", "Provider", "Journal", "Delegate", "Task", "World", "Proposal", "Prompt", "Response" })
            Assert.DoesNotContain(forbidden, surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(members.OfType<MethodInfo>(), static method => method.Name.Contains("Create", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Project", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Calculate", StringComparison.OrdinalIgnoreCase));
    }

    private static CognitionQualityRecordingEvidence Evidence()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = CognitionQualityExecutionProvenance.ForLocal(
            "model-v1", Revision, PolicyDigest, publication.PromptRevision,
            CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
        return CognitionQualityRecordingEvidenceModule.Create(publication, provenance, Responses());
    }

    private static ReadOnlyMemory<byte>[] Responses() => Enumerable.Range(1, 12)
        .Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}"))
        .ToArray();

    private static string Action(int index) => index switch
    {
        1 or 7 => "GatherWood",
        2 or 3 => "GatherStone",
        4 or 5 or 6 => "BuildShelter",
        8 or 9 => "BuildStorage",
        _ => "Idle"
    };

    private static int Quantity(int index) => index switch
    {
        1 => 3,
        2 => 4,
        3 => 3,
        4 => 1,
        5 => 1,
        6 => 1,
        7 => 2,
        8 => 1,
        9 => 1,
        _ => 1
    };

    internal static void RecomputeLastDigest(JsonObject value, string propertyName = "summary_payload_digest_sha256")
    {
        value.Remove(propertyName);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
        value[propertyName] = CognitionQualityHash.Sha256(payload);
    }

    private static byte[] ForgeTuple(
        CognitionQualityScoreSummary summary,
        int scenarioOrdinal,
        int rawPoints,
        string disposition,
        string limitationCode)
    {
        JsonObject root = JsonNode.Parse(summary.CanonicalUtf8.Span)!.AsObject();
        JsonObject report = root["quality_report"]!.AsObject();
        JsonObject scenario = report["categories"]![(scenarioOrdinal - 1) / 3]!["scenarios"]![(scenarioOrdinal - 1) % 3]!.AsObject();
        scenario["raw_points"] = rawPoints;
        scenario["basis_points"] = rawPoints * 100;
        scenario["disposition"] = disposition;
        scenario["limitation_codes"] = new JsonArray(limitationCode);
        RecomputeReportAndSummary(root);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static byte[] ForgeDispositionCount(CognitionQualityScoreSummary summary, bool embeddedReport, int count)
    {
        JsonObject root = JsonNode.Parse(summary.CanonicalUtf8.Span)!.AsObject();
        if (embeddedReport)
        {
            JsonObject report = root["quality_report"]!.AsObject();
            report["disposition_counts"]!["no_proposal"] = count;
            RecomputeLastDigest(report, "report_payload_digest_sha256");
            root["quality_contract"]!["report_digest_sha256"] = CognitionQualityHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(report));
        }
        else
        {
            root["disposition_counts"]!["no_proposal"] = count;
        }
        RecomputeLastDigest(root);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    internal static void RecomputeReportAndSummary(JsonObject root)
    {
        JsonObject report = root["quality_report"]!.AsObject();
        Dictionary<string, int> counts = CognitionQualityCorpusV1.Dispositions.ToDictionary(static value => value, static _ => 0, StringComparer.Ordinal);
        int totalRaw = 0;
        int categoryIndex = 0;
        foreach (JsonNode? categoryNode in report["categories"]!.AsArray())
        {
            JsonObject category = categoryNode!.AsObject();
            int categoryRaw = 0;
            int scenarioIndex = 0;
            foreach (JsonNode? scenarioNode in category["scenarios"]!.AsArray())
            {
                JsonObject scenario = scenarioNode!.AsObject();
                int raw = scenario["raw_points"]!.GetValue<int>();
                categoryRaw += raw;
                counts[scenario["disposition"]!.GetValue<string>()]++;
                JsonObject summaryScenario = root["categories"]![categoryIndex]!["scenarios"]![scenarioIndex++]!.AsObject();
                summaryScenario["raw_points"] = raw;
                summaryScenario["basis_points"] = raw * 100;
                summaryScenario["disposition"] = scenario["disposition"]!.GetValue<string>();
            }
            category["raw_points"] = categoryRaw;
            category["basis_points"] = categoryRaw * 10_000 / 300;
            JsonObject summaryCategory = root["categories"]![categoryIndex++]!.AsObject();
            summaryCategory["raw_points"] = categoryRaw;
            summaryCategory["basis_points"] = categoryRaw * 10_000 / 300;
            totalRaw += categoryRaw;
        }
        report["raw_points"] = totalRaw;
        report["basis_points"] = totalRaw * 10_000 / CognitionQualityCorpusV1.MaximumPoints;
        root["raw_points"] = totalRaw;
        root["basis_points"] = totalRaw * 10_000 / CognitionQualityCorpusV1.MaximumPoints;
        foreach ((string disposition, int count) in counts)
        {
            report["disposition_counts"]![disposition] = count;
            root["disposition_counts"]![disposition] = count;
        }
        RecomputeLastDigest(report, "report_payload_digest_sha256");
        root["quality_contract"]!["report_digest_sha256"] = CognitionQualityHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(report));
        RecomputeLastDigest(root);
    }
}
