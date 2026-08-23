using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityRecordedResponseRunnerTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void PreferredBatchBindsExactEvidenceAndRecomputableCanonicalDocument()
    {
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), Preferred());
        CognitionQualityExecutionEvidence direct = CognitionQualityExecutionEvidenceModule.Create(Provenance(), PreferredSubmissions());

        Assert.Equal(direct.CanonicalJson, run.ExecutionEvidence.CanonicalJson);
        Assert.Equal("61cacfd4ad26512c1100a9235ee0ab534ec5945527a4da9252486ebf26675e43", run.PayloadDigestSha256);
        Assert.Equal("f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd", run.CanonicalDigestSha256);
        Assert.Equal("2700886cef55abea3aba76f0789993cd6ad7283fa22d303dac5bbbd302e1ffe8", run.ExecutionEvidence.CanonicalDigestSha256);
        Assert.Equal("complete", run.Status);
        Assert.Equal("offline_recorded_response_conversion_only", run.Semantics);
        Assert.Equal(12, run.ResponseBindings.Count);
        Assert.Equal(12, run.ProposalBatch.Count);
        Assert.All(run.ResponseBindings, binding => Assert.Equal("proposal_parsed", binding.ParseOutcome));
        using JsonDocument document = JsonDocument.Parse(run.CanonicalUtf8);
        Assert.Equal(new[] { "schema_version", "status", "semantics", "runner_identity", "parser_identity", "corpus_digest_sha256", "provenance_digest_sha256", "prompt_revision", "proposal_schema_version", "scenario_count", "response_bindings", "execution_evidence", "claim_limitation_codes", "run_payload_digest_sha256" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(CognitionQualityRecordedResponseRunnerModule.RunnerIdentity, document.RootElement.GetProperty("runner_identity").GetString());
        Assert.Equal(CognitionQualityRecordedResponseRunnerModule.ParserIdentity, document.RootElement.GetProperty("parser_identity").GetString());
        Assert.Equal(run.ExecutionEvidence.CanonicalJson, document.RootElement.GetProperty("execution_evidence").GetRawText());
        int payloadProperty = run.CanonicalJson.LastIndexOf(",\"run_payload_digest_sha256\":", StringComparison.Ordinal);
        Assert.True(payloadProperty > 0);
        Assert.Equal(run.PayloadDigestSha256, Digest(Encoding.UTF8.GetBytes(run.CanonicalJson[..payloadProperty] + "}")));
        Assert.Equal(run.CanonicalDigestSha256, Digest(run.CanonicalUtf8.Span));
        Assert.InRange(run.CanonicalUtf8.Length, 1, CognitionQualityRecordedResponseRunnerModule.MaximumRunBytes);
    }

    [Theory]
    [InlineData("{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":1}", "proposal_parsed")]
    [InlineData("{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":1,\"quantity\":2}", "response_json_duplicate_property")]
    [InlineData("{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":}", "response_json_invalid")]
    [InlineData("[]", "response_shape_invalid")]
    [InlineData("{\"agent_id\":\"AGENT\",\"action\":\"GatherWood\",\"quantity\":1}", "response_content_invalid")]
    public void CorrectlyBoundMalformedResponsesCompleteAsNoProposal(string response, string outcome)
    {
        CognitionQualityRecordedResponseFixture[] fixtures = Preferred();
        fixtures[0] = Fixture("cq1", response);
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);

        Assert.Equal(outcome, run.ResponseBindings[0].ParseOutcome);
        if (outcome == "proposal_parsed") Assert.NotNull(run.ProposalBatch[0].Proposal);
        else
        {
            Assert.Null(run.ProposalBatch[0].Proposal);
            using JsonDocument report = JsonDocument.Parse(run.ExecutionEvidence.QualityReportCanonicalJson);
            Assert.Equal("no_proposal", report.RootElement.GetProperty("categories")[0].GetProperty("scenarios")[0].GetProperty("disposition").GetString());
        }
    }

    [Fact]
    public void Utf8DepthAndGrammarFailuresAreClosedAndDoNotAbortBoundBatch()
    {
        CognitionQualityRecordedResponseFixture[] fixtures = Preferred();
        fixtures[0] = new("cq1", Observation("cq1"), new byte[] { 0xc3, 0x28 });
        fixtures[1] = Fixture("cq2", "{\"agent_id\":{\"a\":{\"a\":{\"a\":{\"a\":{\"a\":{\"a\":{\"a\":1}}}}}}},\"action\":\"GatherStone\",\"quantity\":1}");
        fixtures[2] = Fixture("cq3", "{\"agent_id\":\"agent-00\",\"action\":\"GatherStone\",\"quantity\":1} trailing");
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);

        Assert.Equal("response_utf8_invalid", run.ResponseBindings[0].ParseOutcome);
        Assert.Equal("response_json_too_deep", run.ResponseBindings[1].ParseOutcome);
        Assert.Equal("response_json_invalid", run.ResponseBindings[2].ParseOutcome);
        Assert.All(run.ProposalBatch.Take(3), submission => Assert.Null(submission.Proposal));
    }

    [Fact]
    public void WrongCanonicalAgentAndActionSpecificInvalidQuantityRemainTypedForExistingScorer()
    {
        CognitionQualityRecordedResponseFixture[] fixtures = Preferred();
        fixtures[0] = Fixture("cq1", "{\"agent_id\":\"agent-99\",\"action\":\"GatherWood\",\"quantity\":1}");
        fixtures[1] = Fixture("cq2", "{\"agent_id\":\"agent-00\",\"action\":\"GatherStone\",\"quantity\":0}");
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);

        Assert.Equal("proposal_parsed", run.ResponseBindings[0].ParseOutcome);
        Assert.Equal("agent-99", run.ProposalBatch[0].Proposal!.AgentId);
        Assert.Equal("proposal_parsed", run.ResponseBindings[1].ParseOutcome);
        Assert.Contains("\"disposition\":\"contract_invalid\"", run.ExecutionEvidence.QualityReportCanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvelopeFailuresAreClosedBeforeAnyPartialResult()
    {
        Assert.Equal("proposal_schema_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"), Preferred())).Code);
        Assert.Equal("fixture_count_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), Preferred()[..^1])).Code);

        CognitionQualityRecordedResponseFixture[] nullFixture = Preferred();
        nullFixture[0] = null!;
        Assert.Equal("fixture_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), nullFixture)).Code);

        CognitionQualityRecordedResponseFixture[] wrongOrder = Preferred();
        (wrongOrder[0], wrongOrder[1]) = (wrongOrder[1], wrongOrder[0]);
        Assert.Equal("fixture_order_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), wrongOrder)).Code);
        CognitionQualityRecordedResponseFixture[] wrongObservation = Preferred();
        wrongObservation[0] = new("cq1", new string('a', 64), Encoding.UTF8.GetBytes("{}"));
        Assert.Equal("fixture_observation_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), wrongObservation)).Code);
        CognitionQualityRecordedResponseFixture[] tooLong = Preferred();
        tooLong[0] = new("cq1", Observation("cq1"), new byte[1025]);
        Assert.Equal("response_size_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), tooLong)).Code);
        CognitionQualityRecordedResponseFixture[] empty = Preferred();
        empty[0] = new("cq1", Observation("cq1"), Array.Empty<byte>());
        Assert.Equal("response_size_invalid", Assert.Throws<CognitionQualityRecordedResponseRunnerException>(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), empty)).Code);
    }

    [Fact]
    public async Task OutputsAreDetachedCultureIndependentAndPure()
    {
        CognitionQualityRecordedResponseFixture[] fixtures = Preferred();
        ReadOnlyMemory<byte> fixtureBytes = fixtures[0].ResponseUtf8;
        Assert.True(MemoryMarshal.TryGetArray(fixtureBytes, out ArraySegment<byte> fixtureSegment));
        fixtureSegment.Array![fixtureSegment.Offset] = (byte)'!';
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);
        fixtures[0] = Fixture("cq1", "{}");
        ReadOnlyMemory<byte> bytes = run.CanonicalUtf8;
        Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', run.CanonicalUtf8.Span[0]);
        Assert.Equal("proposal_parsed", run.ResponseBindings[0].ParseOutcome);
        Assert.Equal("f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd", run.CanonicalDigestSha256);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)run.ClaimLimitationCodes)[0] = "changed");

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(run.CanonicalDigestSha256, CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), Preferred()).CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        string[] digests = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() => CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), Preferred()).CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(run.CanonicalDigestSha256, digest));
    }

    [Fact]
    public void PublicSurfaceIsOneSynchronousOperationAndDoesNotExposeRawResponseEvidence()
    {
        MethodInfo operation = Assert.Single(typeof(CognitionQualityRecordedResponseRunnerModule).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Run", operation.Name);
        Assert.Equal(typeof(CognitionQualityRecordedResponseRun), operation.ReturnType);
        string members = string.Join('|', new[] { typeof(CognitionQualityRecordedResponseRunnerModule), typeof(CognitionQualityRecordedResponseRun), typeof(CognitionQualityRecordedResponseBinding) }
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Select(member => member.ToString()));
        foreach (string forbidden in new[] { "Http", "Socket", "File", "Stream", "ProviderHost", "Route", "Credential", "Payment", "Journal", "Cancellation", "Task", "Delegate", "World" })
            Assert.DoesNotContain(forbidden, members, StringComparison.OrdinalIgnoreCase);

        string sentinel = "RAW_SENTINEL_DO_NOT_ECHO";
        CognitionQualityRecordedResponseFixture[] fixtures = Preferred();
        fixtures[0] = Fixture("cq1", "{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":1,\"x\":\"" + sentinel + "\"}");
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);
        Assert.DoesNotContain(sentinel, run.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("raw_response_not_retained", run.ClaimLimitationCodes);
        Assert.Contains("no_provider_status_retry_or_charge_evidence", run.ClaimLimitationCodes);
    }

    private static CognitionQualityExecutionProvenance Provenance() => CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
    private static CognitionQualityRecordedResponseFixture[] Preferred() => Enumerable.Range(1, 12).Select(index => Fixture($"cq{index}", Json(index))).ToArray();
    private static CognitionQualityRecordedResponseFixture Fixture(string scenarioId, string json) => new(scenarioId, Observation(scenarioId), Encoding.UTF8.GetBytes(json));
    private static string Observation(string scenarioId) => CognitionQualityCorpusV1.CreateSnapshot().Scenarios.Single(scenario => scenario.ScenarioId == scenarioId).ObservationDigestSha256;
    private static CognitionQualitySubmission[] PreferredSubmissions() => Enumerable.Range(1, 12).Select(index => new CognitionQualitySubmission($"cq{index}", Proposal(index))).ToArray();
    private static SnowGlobeActionProposal Proposal(int index) => index switch
    {
        1 => new("agent-00", SnowGlobeActionKind.GatherWood, 12),
        2 => new("agent-00", SnowGlobeActionKind.GatherStone, 6),
        3 => new("agent-00", SnowGlobeActionKind.GatherStone, 2),
        4 or 5 or 6 => new("agent-00", SnowGlobeActionKind.BuildShelter),
        7 => new("agent-00", SnowGlobeActionKind.GatherWood, 8),
        8 or 9 => new("agent-00", SnowGlobeActionKind.BuildStorage),
        _ => new("agent-00", SnowGlobeActionKind.Idle)
    };
    private static string Json(int index)
    {
        SnowGlobeActionProposal proposal = Proposal(index);
        return $"{{\"agent_id\":\"{proposal.AgentId}\",\"action\":\"{proposal.Action}\",\"quantity\":{proposal.Quantity}}}";
    }
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
