using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityExecutionEvidenceTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void PreferredLocalBatch_ProducesStableGoldenCanonicalEvidence()
    {
        CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred());

        Assert.Equal(CognitionQualityExecutionEvidenceModule.SchemaVersion, evidence.SchemaVersion);
        Assert.Equal("4de8c4a993b58875f27c5867c29a54679de789dacb03d2b4d8099e26340f1f8f", evidence.QualityContract.CorpusDigestSha256);
        Assert.Equal("043dc7f01ae544d4698e9c8b44c0f2c27b9f0a66fdba3a1e2249b868a64c35b0", evidence.QualityContract.ScoringDigestSha256);
        Assert.Equal("7d7d918caa0f11f2367fabf1cc538c38d014b97c53acd8b32f94acbb0678652c", evidence.QualityContract.ReportDigestSha256);
        Assert.Equal(12, evidence.ScenarioCount);
        Assert.Equal(1200, evidence.Score.RawPoints);
        Assert.Equal(10_000, evidence.Score.BasisPoints);
        Assert.Equal("8fb647f1f9e8a515ad490ccaec1372c4d2c110efa5599c33f93bf087a8821cfc", evidence.Provenance.ProvenanceDigestSha256);
        Assert.Equal("9473f2021caffd85586d32ea550f46ee717d082b6d1dcba50ab979c8832a2757", evidence.QualityContract.SubmissionDigestSha256);
        Assert.Equal("353c266be57dce3b4e3f15bc67920ac3325df75cae0eca00529ffa348014b9dd", evidence.PayloadDigestSha256);
        Assert.Equal("7130deb0945697a14631ddea9bdc29e699b4b1217ae948a729e39ec827f3272a", evidence.CanonicalDigestSha256);
    }

    [Fact]
    public void PreferredPremiumBatch_UsesPolicyOnlyAndProducesStableGoldenCanonicalEvidence()
    {
        CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(CognitionQualityExecutionProvenance.ForPremium(Policy()), Preferred());

        Assert.Equal(CognitionLane.Premium, evidence.Provenance.Lane);
        Assert.Null(evidence.Provenance.LocalAdapterIdentity);
        Assert.Equal(Policy().PremiumModelIdentity, evidence.Provenance.ModelIdentity);
        Assert.Equal(Policy().PremiumModelRevisionIdentity, evidence.Provenance.ModelRevisionIdentity);
        Assert.Equal(Policy().PromptRevision, evidence.Provenance.PromptRevision);
        Assert.Equal(Policy().ProposalSchemaVersion, evidence.Provenance.ProposalSchemaVersion);
        Assert.Equal(Policy().Digest, evidence.Provenance.ExecutionPolicyDigestSha256);
        Assert.DoesNotContain("premium.example", evidence.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("v1/propose", evidence.CanonicalJson, StringComparison.Ordinal);
        Assert.Equal("e5fb33c1246784b3ff70165ea531297b2fe069d6425d002a770db62b82f32540", evidence.Provenance.ProvenanceDigestSha256);
        Assert.Equal("551a06855ee776ea03e8d27e1546806b9308a0517fbe1861e4d3180908ec9261", evidence.PayloadDigestSha256);
        Assert.Equal("5a4efa252c84feadfb5ce878e0b6d50f2ceef6f51f2818667865475db666408c", evidence.CanonicalDigestSha256);
    }

    [Fact]
    public void FactoriesBindEveryFieldAndRejectMismatches()
    {
        CognitionQualityExecutionProvenance baseline = Local();
        CognitionQualityExecutionProvenance[] changed =
        [
            CognitionQualityExecutionProvenance.ForLocal("model-v2", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"),
            CognitionQualityExecutionProvenance.ForLocal("model-v1", "sha256-1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"),
            CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, "bbcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", "prompt-v1", "proposal-v1", "adapter-v1"),
            CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v2", "proposal-v1", "adapter-v1"),
            CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v2", "adapter-v1"),
            CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v2")
        ];
        Assert.All(changed, value => Assert.NotEqual(baseline.ProvenanceDigestSha256, value.ProvenanceDigestSha256));
        Assert.Empty(typeof(CognitionQualityExecutionProvenance).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionProvenance.ForLocal("Model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"));
        Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionProvenance.ForLocal("model-v1", "sha256-ABC", PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"));
        Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest.ToUpperInvariant(), "prompt-v1", "proposal-v1", "adapter-v1"));

        ModelPolicySnapshot invalid = Policy() with { PremiumModelRevisionIdentity = "sha256-ABC" };
        Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionProvenance.ForPremium(invalid));
    }

    [Fact]
    public void BatchFailuresMapWithoutEchoingCallerDataAndScoreDomainOutcomesRemainRecorded()
    {
        Assert.Throws<ArgumentNullException>(() => CognitionQualityExecutionEvidenceModule.Create(null!, Preferred()));
        Assert.Throws<ArgumentNullException>(() => CognitionQualityExecutionEvidenceModule.Create(Local(), null!));
        Assert.Equal("recorded_batch_count_invalid", Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred()[..^1])).Code);

        CognitionQualitySubmission[] wrongOrder = Preferred();
        (wrongOrder[0], wrongOrder[1]) = (wrongOrder[1], wrongOrder[0]);
        Assert.Equal("recorded_batch_order_invalid", Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionEvidenceModule.Create(Local(), wrongOrder)).Code);

        CognitionQualitySubmission[] uncanonical = Preferred();
        uncanonical[0] = new("cq1", new SnowGlobeActionProposal("Agent-00-secret", SnowGlobeActionKind.GatherWood, 1));
        CognitionQualityExecutionEvidenceException exception = Assert.Throws<CognitionQualityExecutionEvidenceException>(() => CognitionQualityExecutionEvidenceModule.Create(Local(), uncanonical));
        Assert.Equal("recorded_batch_invalid", exception.Code);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);

        foreach (SnowGlobeActionProposal proposal in new[]
                 {
                     new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 0),
                     new SnowGlobeActionProposal("agent-00", (SnowGlobeActionKind)999, 1),
                     new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 65)
                 })
        {
            CognitionQualitySubmission[] batch = Preferred();
            batch[0] = new("cq1", proposal);
            CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(Local(), batch);
            using JsonDocument report = JsonDocument.Parse(evidence.QualityReportCanonicalJson);
            Assert.Equal("contract_invalid", report.RootElement.GetProperty("categories")[0].GetProperty("scenarios")[0].GetProperty("disposition").GetString());
        }
    }

    [Fact]
    public async Task OutputsAreDetached_CanonicalAndCultureIndependent()
    {
        CognitionQualitySubmission[] batch = Preferred();
        CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(Local(), batch);
        batch[0] = new("changed", null);
        ReadOnlyMemory<byte> bytes = evidence.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', evidence.CanonicalUtf8.Span[0]);
        Assert.DoesNotContain("\n", evidence.CanonicalJson, StringComparison.Ordinal);
        Assert.NotEqual(0xef, evidence.CanonicalUtf8.Span[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)evidence.ClaimLimitationCodes)[0] = "changed");

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(evidence.CanonicalDigestSha256, CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred()).CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        string[] digests = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred()).CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(evidence.CanonicalDigestSha256, digest));
    }

    [Fact]
    public void CanonicalDocumentEmbedsExactExistingArtifactsAndRecomputableDigests()
    {
        CognitionQualityExecutionEvidence evidence = CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred());
        using JsonDocument document = JsonDocument.Parse(evidence.CanonicalUtf8);
        Assert.Equal(new[] { "schema_version", "status", "semantics", "provenance", "quality_contract", "scenario_count", "score", "claim_limitation_codes", "recorded_submission", "quality_report", "evidence_payload_digest_sha256" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(evidence.RecordedSubmissionCanonicalJson, document.RootElement.GetProperty("recorded_submission").GetRawText());
        Assert.Equal(evidence.QualityReportCanonicalJson, document.RootElement.GetProperty("quality_report").GetRawText());
        Assert.Equal(evidence.QualityContract.SubmissionDigestSha256, Digest(Encoding.UTF8.GetBytes(evidence.RecordedSubmissionCanonicalJson)));
        Assert.Equal(evidence.QualityContract.ReportDigestSha256, Digest(Encoding.UTF8.GetBytes(evidence.QualityReportCanonicalJson)));
        int payloadProperty = evidence.CanonicalJson.LastIndexOf(",\"evidence_payload_digest_sha256\":", StringComparison.Ordinal);
        Assert.True(payloadProperty > 0);
        Assert.Equal(evidence.PayloadDigestSha256, Digest(Encoding.UTF8.GetBytes(evidence.CanonicalJson[..payloadProperty] + "}")));
        Assert.Equal(evidence.CanonicalDigestSha256, Digest(evidence.CanonicalUtf8.Span));
        Assert.InRange(evidence.CanonicalUtf8.Length, 1, CognitionQualityExecutionEvidenceModule.MaximumEvidenceBytes);

        CognitionQualityReport independentlyScored = CognitionQuality.Evaluate(CognitionQualityCorpusV1.CreateSnapshot(), Preferred());
        Assert.Equal(independentlyScored.CanonicalJson, evidence.QualityReportCanonicalJson);
    }

    [Fact]
    public void PublicModuleSurfaceIsOnePureSynchronousOperationAndClaimsStayHonest()
    {
        MethodInfo method = Assert.Single(typeof(CognitionQualityExecutionEvidenceModule).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", method.Name);
        Assert.Equal(typeof(CognitionQualityExecutionEvidence), method.ReturnType);
        string publicMembers = string.Join('|', new[] { typeof(CognitionQualityExecutionEvidenceModule), typeof(CognitionQualityExecutionEvidence), typeof(CognitionQualityExecutionProvenance), typeof(CognitionQualityExecutionQualityContract), typeof(CognitionQualityExecutionScore) }
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Select(member => member.ToString()));
        foreach (string forbidden in new[] { "Http", "Socket", "File", "Stream", "ProviderHost", "Route", "Credential", "Payment", "Journal", "Cancellation", "Task", "Delegate", "World" })
            Assert.DoesNotContain(forbidden, publicMembers, StringComparison.OrdinalIgnoreCase);

        CognitionQualityExecutionEvidence local = CognitionQualityExecutionEvidenceModule.Create(Local(), Preferred());
        CognitionQualityExecutionEvidence premium = CognitionQualityExecutionEvidenceModule.Create(CognitionQualityExecutionProvenance.ForPremium(Policy()), Preferred());
        Assert.Equal(local.QualityContract.SubmissionDigestSha256, premium.QualityContract.SubmissionDigestSha256);
        Assert.Equal(local.QualityContract.ReportDigestSha256, premium.QualityContract.ReportDigestSha256);
        Assert.NotEqual(local.Provenance.ProvenanceDigestSha256, premium.Provenance.ProvenanceDigestSha256);
        Assert.NotEqual(local.CanonicalDigestSha256, premium.CanonicalDigestSha256);
        Assert.Contains("identity_attribution_is_caller_attested", local.ClaimLimitationCodes);
        Assert.Contains("no_execution_attestation", local.ClaimLimitationCodes);
        Assert.Equal(new[] { "identity_attribution_is_caller_attested", "no_execution_attestation", "offline_fixed_corpus", "observable_action_utility_only", "no_general_quality_claim", "no_cost_claim", "no_winner_claim" }, local.ClaimLimitationCodes);

        CognitionQualitySubmission[] equallyScored = Preferred();
        equallyScored[0] = new("cq1", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 64));
        equallyScored[1] = new("cq2", new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 32));
        CognitionQualityExecutionEvidence alternate = CognitionQualityExecutionEvidenceModule.Create(Local(), equallyScored);
        Assert.Equal(local.Score.RawPoints, alternate.Score.RawPoints);
        Assert.NotEqual(local.QualityContract.SubmissionDigestSha256, alternate.QualityContract.SubmissionDigestSha256);
        Assert.NotEqual(local.QualityContract.ReportDigestSha256, alternate.QualityContract.ReportDigestSha256);
        Assert.NotEqual(local.CanonicalDigestSha256, alternate.CanonicalDigestSha256);
    }

    private static CognitionQualityExecutionProvenance Local() => CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1");
    private static ModelPolicySnapshot Policy() => new("policy-v1", "premium.example", "v1/propose", "premium-model-v1", Revision, "prompt-v1", "proposal-v1", "adapter-v1", "usd", 1_000_000, 1_000_000, 10, 10, 20, 1000);
    private static CognitionQualitySubmission[] Preferred() => Enumerable.Range(1, 12).Select(index => new CognitionQualitySubmission($"cq{index}", Preferred(index))).ToArray();
    private static SnowGlobeActionProposal Preferred(int index) => index switch
    {
        1 => new("agent-00", SnowGlobeActionKind.GatherWood, 12),
        2 => new("agent-00", SnowGlobeActionKind.GatherStone, 6),
        3 => new("agent-00", SnowGlobeActionKind.GatherStone, 2),
        4 or 5 or 6 => new("agent-00", SnowGlobeActionKind.BuildShelter),
        7 => new("agent-00", SnowGlobeActionKind.GatherWood, 8),
        8 or 9 => new("agent-00", SnowGlobeActionKind.BuildStorage),
        _ => new("agent-00", SnowGlobeActionKind.Idle)
    };
    private static string Digest(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
