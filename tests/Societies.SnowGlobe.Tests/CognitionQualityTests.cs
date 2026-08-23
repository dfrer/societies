using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityTests
{
    [Fact]
    public void Corpus_HasFrozenTwelveScenarioFourCategoryShapeAndBoundedCanonicalManifest()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        Assert.Equal(12, corpus.Scenarios.Count);
        Assert.Equal(4, corpus.Scenarios.Select(scenario => scenario.CategoryId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(corpus.Scenarios.GroupBy(scenario => scenario.CategoryId, StringComparer.Ordinal), category => Assert.Equal(3, category.Count()));
        Assert.Equal(new[] { "shelter_acquisition", "shelter_construction", "storage_progression", "safe_restraint" }, corpus.Scenarios.Select(scenario => scenario.CategoryId).Distinct());
        Assert.Equal(Encoding.UTF8.GetBytes(corpus.CanonicalJson), corpus.CanonicalUtf8.ToArray());
        Assert.Equal("4de8c4a993b58875f27c5867c29a54679de789dacb03d2b4d8099e26340f1f8f", corpus.CanonicalDigestSha256);
        using JsonDocument manifest = JsonDocument.Parse(corpus.CanonicalUtf8);
        Assert.Equal("043dc7f01ae544d4698e9c8b44c0f2c27b9f0a66fdba3a1e2249b868a64c35b0", manifest.RootElement.GetProperty("scoring_digest_sha256").GetString());
        Assert.Equal("snow_globe_validate_and_commit_v1", manifest.RootElement.GetProperty("validator_identity").GetString());
        Assert.InRange(corpus.CanonicalUtf8.Length, 1, CognitionQualityCorpusV1.MaximumCorpusBytes);
        Assert.NotEqual(0xef, corpus.CanonicalUtf8.Span[0]);
        Assert.Equal((byte)'}', corpus.CanonicalUtf8.Span[^1]);
    }

    [Fact]
    public void ExactPreferredEnvelope_Scores1200AndProducesStableGoldenReport()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, PreferredEnvelope(corpus));
        Assert.Equal(1200, report.RawPoints);
        Assert.Equal(10_000, report.BasisPoints);
        Assert.All(report.Results, result => Assert.Equal("maximum_utility", result.Disposition));
        Assert.Equal("7d7d918caa0f11f2367fabf1cc538c38d014b97c53acd8b32f94acbb0678652c", report.CanonicalDigestSha256);
        Assert.NotEmpty(report.SubmissionDigestSha256);
        using JsonDocument document = JsonDocument.Parse(report.CanonicalUtf8);
        Assert.Equal("complete", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(1200, document.RootElement.GetProperty("max_points").GetInt32());
        Assert.Equal(5, document.RootElement.GetProperty("disposition_counts").EnumerateObject().Count());
        Assert.Equal(4, document.RootElement.GetProperty("claim_limitation_codes").GetArrayLength());
    }

    [Fact]
    public void GatherQuantitiesAndIdleUseClosedIntegerRubric()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] envelope = PreferredEnvelope(corpus);
        envelope[0] = new(envelope[0].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 1));
        envelope[1] = new(envelope[1].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, envelope);
        Assert.Equal(31, report.Results[0].RawPoints);
        Assert.Equal(25, report.Results[1].RawPoints);
        Assert.Equal("feasible_suboptimal", report.Results[0].Disposition);
        Assert.Equal("feasible_suboptimal", report.Results[1].Disposition);
    }

    [Fact]
    public void ClosedDispositionSet_CoversNullContractDomainSuboptimalAndMaximum()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] envelope = PreferredEnvelope(corpus);
        envelope[0] = new(envelope[0].ScenarioId, null);
        envelope[1] = new(envelope[1].ScenarioId, new SnowGlobeActionProposal("other", SnowGlobeActionKind.GatherStone, 1));
        envelope[2] = new(envelope[2].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 3));
        envelope[3] = new(envelope[3].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, envelope);
        Assert.Equal(new[] { "no_proposal", "contract_invalid", "domain_rejected", "feasible_suboptimal", "maximum_utility" }, report.Results.Select(result => result.Disposition).Distinct());
    }

    [Fact]
    public void WrongCountOrderAndScenarioInputsFailClosed()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] exact = PreferredEnvelope(corpus);
        Assert.Throws<CognitionQualityException>(() => CognitionQuality.Evaluate(corpus, exact[..^1]));
        (exact[0], exact[1]) = (exact[1], exact[0]);
        Assert.Throws<CognitionQualityException>(() => CognitionQuality.Evaluate(corpus, exact));
    }

    [Fact]
    public void AdversarialAgentActionQuantityResourceShelterAndDigestInputsAreClosed()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] envelope = PreferredEnvelope(corpus);
        envelope[0] = new(envelope[0].ScenarioId, new SnowGlobeActionProposal("wrong-agent", SnowGlobeActionKind.GatherWood, 1));
        envelope[1] = new(envelope[1].ScenarioId, new SnowGlobeActionProposal("agent-00", (SnowGlobeActionKind)999, 1));
        envelope[2] = new(envelope[2].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 0));
        envelope[3] = new(envelope[3].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.BuildShelter, 1));
        envelope[4] = new(envelope[4].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.MaintainShelter, 1));
        envelope[5] = new(envelope[5].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 11));
        CognitionQualityReport result = CognitionQuality.Evaluate(corpus, envelope);
        Assert.Equal(new[] { "contract_invalid", "contract_invalid", "contract_invalid", "contract_invalid", "domain_rejected", "domain_rejected" }, result.Results.Take(6).Select(item => item.Disposition));
        Assert.DoesNotContain("wrong-agent", result.CanonicalJson, StringComparison.Ordinal);

        byte[] altered = corpus.CanonicalUtf8.ToArray();
        altered[0] ^= 1;
        ConstructorInfo constructor = Assert.Single(typeof(CognitionQualityCorpusSnapshot).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        CognitionQualityCorpusSnapshot forged = (CognitionQualityCorpusSnapshot)constructor.Invoke(new object[] { altered, corpus.Scenarios });
        Assert.Throws<CognitionQualityException>(() => CognitionQuality.Evaluate(forged, PreferredEnvelope(corpus)));
    }

    [Fact]
    public void SubmittedAgentIdentityIsCanonicalBoundedAndNeverLossilyDigestible()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        foreach (string identity in new[] { new string('a', 1_000_000), "Agent-00", "agent\u0001", "\uFFFD", "\uD800" })
        {
            CognitionQualitySubmission[] envelope = PreferredEnvelope(corpus);
            envelope[0] = new(envelope[0].ScenarioId, new SnowGlobeActionProposal(identity, SnowGlobeActionKind.GatherWood, 1));
            CognitionQualityException exception = Assert.Throws<CognitionQualityException>(() => CognitionQuality.Evaluate(corpus, envelope));
            Assert.Equal("envelope_invalid", exception.Code);
            Assert.DoesNotContain(identity, exception.Message, StringComparison.Ordinal);
        }

        string maxIdentity = new('a', 64);
        CognitionQualitySubmission[] maxEnvelope = PreferredEnvelope(corpus);
        maxEnvelope[0] = new(maxEnvelope[0].ScenarioId, new SnowGlobeActionProposal(maxIdentity, SnowGlobeActionKind.GatherWood, 1));
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, maxEnvelope);
        Assert.Equal("contract_invalid", report.Results[0].Disposition);
        Assert.DoesNotContain(maxIdentity, report.CanonicalJson, StringComparison.Ordinal);
        Assert.InRange(report.CanonicalUtf8.Length, 1, CognitionQuality.MaximumReportBytes);
    }

    [Fact]
    public void EqualScoresDoNotProduceAnyWinnerClaim()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] first = PreferredEnvelope(corpus);
        CognitionQualitySubmission[] second = PreferredEnvelope(corpus);
        second[0] = new(second[0].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 64));
        second[1] = new(second[1].ScenarioId, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 32));
        CognitionQualityReport left = CognitionQuality.Evaluate(corpus, first);
        CognitionQualityReport right = CognitionQuality.Evaluate(corpus, second);
        Assert.Equal(1200, left.RawPoints);
        Assert.NotEqual(left.SubmissionDigestSha256, right.SubmissionDigestSha256);
        Assert.NotEqual(left.CanonicalDigestSha256, right.CanonicalDigestSha256);
        Assert.DoesNotContain("winner", left.CanonicalJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultCodesAreDeeplyDetachedFromCanonicalReport()
    {
        CognitionQualityReport report = CognitionQuality.Evaluate(CognitionQualityCorpusV1.CreateSnapshot(), PreferredEnvelope(CognitionQualityCorpusV1.CreateSnapshot()));
        IReadOnlyList<string> codes = report.Results[0].LimitationCodes;
        Assert.False(codes is string[]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)codes)[0] = "changed");
        Assert.Contains("observable_target_met", report.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("observable_target_met", report.Results[0].LimitationCodes);
    }

    [Fact]
    public async Task InputAndOutputsAreDetached_ConcurrentAndCultureIndependent()
    {
        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualitySubmission[] envelope = PreferredEnvelope(corpus);
        CognitionQualityReport report = CognitionQuality.Evaluate(corpus, envelope);
        envelope[0] = new("changed", null);
        ReadOnlyMemory<byte> reportBytes = report.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(reportBytes, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal(1200, report.RawPoints);
        Assert.NotEqual(0xef, report.CanonicalUtf8.Span[0]);
        Assert.Equal((byte)'}', report.CanonicalUtf8.Span[^1]);
        Assert.DoesNotContain("\n", report.CanonicalJson, StringComparison.Ordinal);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(report.CanonicalDigestSha256, CognitionQuality.Evaluate(corpus, PreferredEnvelope(corpus)).CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }

        string[] values = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => CognitionQuality.Evaluate(corpus, PreferredEnvelope(corpus)).CanonicalDigestSha256)));
        Assert.All(values, value => Assert.Equal(report.CanonicalDigestSha256, value));
    }

    [Fact]
    public void PublicSurfaceHasNoExecutionOrExternalCapability()
    {
        MethodInfo method = Assert.Single(typeof(CognitionQuality).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Evaluate", method.Name);
        Assert.Equal(typeof(CognitionQualityReport), method.ReturnType);
        foreach (Type type in new[] { typeof(CognitionQuality), typeof(CognitionQualityCorpusV1), typeof(CognitionQualityCorpusSnapshot), typeof(CognitionQualityReport) })
        {
            string members = string.Join('|', type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(member => member.ToString()));
            foreach (string forbidden in new[] { "Http", "Socket", "File", "Stream", "Provider", "Credential", "Payment", "Journal", "Delegate" })
            {
                Assert.DoesNotContain(forbidden, members, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ExistingLocalPremiumComparisonGoldenContractRemainsPresent()
    {
        Assert.NotNull(typeof(LocalPremiumComparison).GetMethod("Evaluate", new[] { typeof(ReadOnlyMemory<byte>) }));
    }

    private static CognitionQualitySubmission[] PreferredEnvelope(CognitionQualityCorpusSnapshot corpus) => corpus.Scenarios.Select(scenario => new CognitionQualitySubmission(scenario.ScenarioId, Preferred(scenario.ScenarioId))).ToArray();

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
