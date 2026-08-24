using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ProviderRoutingPolicyTests
{
    private static readonly byte[] AcceptedComparison = ReadAcceptedComparison();

    [Fact]
    public void PreferredOnlineSelectsOpenRouterFromExactAcceptedComparison()
    {
        ProviderRoutingDecision decision = Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);

        Assert.Equal(ProviderRoutingSelectedProvider.OpenRouter, decision.SelectedProvider);
        Assert.Equal("preferred_openrouter_ready", decision.ReasonCode);
        Assert.Equal("accepted", decision.ComparisonStatus);
        Assert.Equal(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256, decision.ComparisonArtifactDigestSha256);
        Assert.Equal("openrouter_default", decision.ComparisonRecommendation);
        Assert.Equal("fc83ef910dd2a23382165975d1560f2ba6d327fdfeb53c3f09149b4c2b0c3499", ProviderRoutingPolicyModule.PolicyDigestSha256);
        Assert.Equal(decision.CanonicalDigestSha256, ProviderRoutingPolicyModule.Validate(decision.CanonicalUtf8).CanonicalDigestSha256);
    }

    [Fact]
    public void LocalOnlyAndExplicitPreDispatchAvailabilityFallbackSelectOnlyOllama()
    {
        ProviderRoutingDecision local = Decide(
            ProviderRoutingIntent.LocalOnly,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);
        Assert.Equal(ProviderRoutingSelectedProvider.Ollama, local.SelectedProvider);
        Assert.Equal("explicit_local_only", local.ReasonCode);

        ProviderRoutingDecision fallback = Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.NotReady,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);
        Assert.Equal(ProviderRoutingSelectedProvider.Ollama, fallback.SelectedProvider);
        Assert.Equal("pre_dispatch_openrouter_unavailable", fallback.ReasonCode);
    }

    [Theory]
    [InlineData(ProviderPrimaryAttemptState.DispatchStarted, "primary_dispatch_started")]
    [InlineData(ProviderPrimaryAttemptState.SubmissionPossible, "primary_submission_possible")]
    [InlineData(ProviderPrimaryAttemptState.SubmissionUnknown, "primary_submission_unknown")]
    [InlineData(ProviderPrimaryAttemptState.Completed, "primary_completed")]
    public void AnyStartedPossibleUnknownOrCompletedPrimaryDeniesFallback(
        ProviderPrimaryAttemptState state,
        string reason)
    {
        ProviderRoutingDecision decision = Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.NotReady,
            ProviderReadiness.Ready,
            state);

        Assert.Null(decision.SelectedProvider);
        Assert.Equal(reason, decision.ReasonCode);
        Assert.Equal("accepted", decision.ComparisonStatus);
    }

    [Theory]
    [InlineData(ProviderRoutingIntent.PreferredOnline, ProviderReadiness.Unknown, ProviderReadiness.Ready, "openrouter_readiness_unknown")]
    [InlineData(ProviderRoutingIntent.PreferredOnline, ProviderReadiness.NotReady, ProviderReadiness.NotReady, "no_provider_ready")]
    [InlineData(ProviderRoutingIntent.PreferredOnline, ProviderReadiness.NotReady, ProviderReadiness.Unknown, "ollama_readiness_unknown")]
    [InlineData(ProviderRoutingIntent.LocalOnly, ProviderReadiness.Ready, ProviderReadiness.NotReady, "ollama_not_ready")]
    [InlineData(ProviderRoutingIntent.LocalOnly, ProviderReadiness.Ready, ProviderReadiness.Unknown, "ollama_readiness_unknown")]
    public void UnavailableOrUnknownReadinessSelectsNoProvider(
        ProviderRoutingIntent intent,
        ProviderReadiness openRouter,
        ProviderReadiness ollama,
        string reason)
    {
        ProviderRoutingDecision decision = Decide(intent, openRouter, ollama, ProviderPrimaryAttemptState.NotStarted);
        Assert.Null(decision.SelectedProvider);
        Assert.Equal(reason, decision.ReasonCode);
    }

    [Fact]
    public void MissingMalformedAsymmetricAndUnsupportedComparisonsFailClosed()
    {
        ProviderRoutingPolicyInput input = Input(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);

        AssertClosed(input, null, "missing", "comparison_missing");
        AssertClosed(input, "{bad"u8.ToArray(), "malformed", "comparison_malformed");

        byte[] secretBearingMalformed = "{\"raw\":\"ROUTING_SECRET_SENTINEL\""u8.ToArray();
        ProviderRoutingDecision secretBearingDecision = ProviderRoutingPolicyModule.Decide(input, secretBearingMalformed);
        Assert.Null(secretBearingDecision.SelectedProvider);
        Assert.Equal("malformed", secretBearingDecision.ComparisonStatus);
        Assert.Equal("comparison_malformed", secretBearingDecision.ReasonCode);
        Assert.DoesNotContain("ROUTING_SECRET_SENTINEL", secretBearingDecision.CanonicalJson, StringComparison.Ordinal);

        byte[] oversized = new byte[CognitionQualityComparisonModule.MaximumArtifactBytes + 1];
        ProviderRoutingDecision oversizedDecision = ProviderRoutingPolicyModule.Decide(input, oversized);
        Assert.Null(oversizedDecision.SelectedProvider);
        Assert.Equal("malformed", oversizedDecision.ComparisonStatus);
        Assert.Equal("comparison_malformed", oversizedDecision.ReasonCode);
        Assert.Null(oversizedDecision.ComparisonArtifactDigestSha256);

        CognitionQualityNormalizedProposalEvidence preferred = Evidence(PreferredEnvelope(), new string('c', 64));
        CognitionQualityNormalizedProposalEvidence idle = Evidence(AllIdleEnvelope(), new string('d', 64));
        CognitionQualityComparisonArtifact unsupportedWinner = Compare(preferred, idle);
        Assert.Equal("ollama_default", unsupportedWinner.Recommendation);
        AssertClosed(input, unsupportedWinner.CanonicalUtf8, "recommendation_unsupported", "comparison_recommendation_unsupported");

        CognitionQualityComparisonArtifact alternateOpenRouter = Compare(idle, preferred);
        Assert.Equal("openrouter_default", alternateOpenRouter.Recommendation);
        AssertClosed(input, alternateOpenRouter.CanonicalUtf8, "artifact_unsupported", "comparison_artifact_unsupported");

        CognitionQualityComparisonArtifact insufficient = Compare(preferred, preferred);
        Assert.Equal("insufficient_evidence", insufficient.Recommendation);
        AssertClosed(input, insufficient.CanonicalUtf8, "insufficient", "comparison_insufficient");

        CognitionQualityComparisonArtifact asymmetric = CognitionQualityComparisonModule.CompareCanonical(
            new string('a', 64), null,
            new string('b', 64), preferred.CanonicalUtf8);
        AssertClosed(input, asymmetric.CanonicalUtf8, "asymmetric", "comparison_asymmetric");

        CognitionQualitySubmission[] left = AllIdleEnvelope();
        CognitionQualitySubmission[] right = AllIdleEnvelope();
        CognitionQualitySubmission[] ideal = PreferredEnvelope();
        for (int index = 0; index < 6; index++) left[index] = ideal[index];
        for (int index = 6; index < 12; index++) right[index] = ideal[index];
        left[6] = ideal[6];
        CognitionQualityComparisonArtifact conditional = Compare(
            Evidence(left, new string('e', 64)),
            Evidence(right, new string('f', 64)));
        Assert.Equal("conditional_routing", conditional.Recommendation);
        AssertClosed(input, conditional.CanonicalUtf8, "conditional", "comparison_conditional");
    }

    [Fact]
    public void UnknownEnumInputsFailClosedWithoutEchoOrSelection()
    {
        ProviderRoutingPolicyInput[] invalid =
        [
            Input((ProviderRoutingIntent)999, ProviderReadiness.Ready, ProviderReadiness.Ready, ProviderPrimaryAttemptState.NotStarted),
            Input(ProviderRoutingIntent.PreferredOnline, (ProviderReadiness)999, ProviderReadiness.Ready, ProviderPrimaryAttemptState.NotStarted),
            Input(ProviderRoutingIntent.PreferredOnline, ProviderReadiness.Ready, (ProviderReadiness)999, ProviderPrimaryAttemptState.NotStarted),
            Input(ProviderRoutingIntent.PreferredOnline, ProviderReadiness.Ready, ProviderReadiness.Ready, (ProviderPrimaryAttemptState)999)
        ];

        foreach (ProviderRoutingPolicyInput input in invalid)
        {
            ProviderRoutingDecision decision = ProviderRoutingPolicyModule.Decide(input, AcceptedComparison);
            Assert.Null(decision.SelectedProvider);
            Assert.Equal("input_invalid", decision.ReasonCode);
            Assert.DoesNotContain("999", decision.CanonicalJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DecisionIsCanonicalBoundedRawFreeDetachedAndRepeatable()
    {
        ProviderRoutingDecision decision = Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);
        Assert.InRange(decision.CanonicalUtf8.Length, 1, ProviderRoutingPolicyModule.MaximumDecisionBytes);
        foreach (string forbidden in new[]
        {
            "raw_prompt", "raw_response", "reasoning_text", "credential_value", "secret_value", "Bearer "
        })
            Assert.DoesNotContain(forbidden, decision.CanonicalJson, StringComparison.OrdinalIgnoreCase);

        ReadOnlyMemory<byte> detached = decision.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', decision.CanonicalUtf8.Span[0]);

        string[] digests = Enumerable.Range(0, 32).Select(_ => Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted).CanonicalDigestSha256).ToArray();
        Assert.Single(digests.Distinct(StringComparer.Ordinal));
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(decision.CanonicalDigestSha256, Decide(
                ProviderRoutingIntent.PreferredOnline,
                ProviderReadiness.Ready,
                ProviderReadiness.Ready,
                ProviderPrimaryAttemptState.NotStarted).CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CallerMemoryIsSnapshottedOnceBeforeComparisonHashAndValidation()
    {
        CognitionQualityComparisonArtifact accepted = CognitionQualityComparisonModule.Validate(AcceptedComparison);
        CognitionQualityProviderComparison ollama = accepted.Providers[0];
        CognitionQualityProviderComparison openRouter = accepted.Providers[1];
        string alternateSourceDigest = ollama.SourceArtifactDigestSha256![0] == '0'
            ? "1" + ollama.SourceArtifactDigestSha256[1..]
            : "0" + ollama.SourceArtifactDigestSha256[1..];
        CognitionQualityComparisonArtifact alternate = CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(alternateSourceDigest, ollama.NormalizedProposalEvidence!),
            new CognitionQualityComparisonInput(openRouter.SourceArtifactDigestSha256!, openRouter.NormalizedProposalEvidence!));
        Assert.Equal("openrouter_default", alternate.Recommendation);
        Assert.Equal(AcceptedComparison.Length, alternate.CanonicalUtf8.Length);
        Assert.NotEqual(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256, alternate.CanonicalDigestSha256);

        using ChangingMemoryManager manager = new(AcceptedComparison, alternate.CanonicalUtf8.ToArray());
        ProviderRoutingDecision decision = ProviderRoutingPolicyModule.Decide(
            Input(
                ProviderRoutingIntent.PreferredOnline,
                ProviderReadiness.Ready,
                ProviderReadiness.Ready,
                ProviderPrimaryAttemptState.NotStarted),
            manager.CreateReadOnlyMemory());

        Assert.Equal(1, manager.GetSpanCallCount);
        Assert.Equal("accepted", decision.ComparisonStatus);
        Assert.Equal(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256, decision.ComparisonArtifactDigestSha256);
        Assert.Equal(ProviderRoutingSelectedProvider.OpenRouter, decision.SelectedProvider);
    }

    [Fact]
    public void CanonicalTamperDuplicateOversizeAndDeepInputsAreRejected()
    {
        ProviderRoutingDecision decision = Decide(
            ProviderRoutingIntent.PreferredOnline,
            ProviderReadiness.Ready,
            ProviderReadiness.Ready,
            ProviderPrimaryAttemptState.NotStarted);
        byte[] changed = decision.CanonicalUtf8.ToArray(); changed[^2] ^= 1;
        Assert.Throws<ProviderRoutingPolicyException>(() => ProviderRoutingPolicyModule.Validate(changed));
        byte[] reboundSelection = ForgeDecision(decision, root => root["selected_provider"] = "ollama");
        Assert.Equal("decision_binding_invalid", Assert.Throws<ProviderRoutingPolicyException>(() =>
            ProviderRoutingPolicyModule.Validate(reboundSelection)).Code);
        byte[] duplicate = Encoding.UTF8.GetBytes(decision.CanonicalJson.Replace(
            "{\"schema_version\"",
            "{\"schema_version\":\"duplicate\",\"schema_version\"",
            StringComparison.Ordinal));
        Assert.Equal("decision_shape_invalid", Assert.Throws<ProviderRoutingPolicyException>(() =>
            ProviderRoutingPolicyModule.Validate(duplicate)).Code);
        Assert.Equal("decision_size_invalid", Assert.Throws<ProviderRoutingPolicyException>(() =>
            ProviderRoutingPolicyModule.Validate(new byte[ProviderRoutingPolicyModule.MaximumDecisionBytes + 1])).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 20) + new string(']', 20));
        Assert.Equal("decision_json_invalid", Assert.Throws<ProviderRoutingPolicyException>(() =>
            ProviderRoutingPolicyModule.Validate(deep)).Code);
    }

    [Fact]
    public void PublicSurfaceHasNoExecutionOrProviderControlCapability()
    {
        Assert.Equal(new[] { "Decide", "Validate" }, typeof(ProviderRoutingPolicyModule)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name).Order(StringComparer.Ordinal));
        string surface = string.Join('|', new[]
        {
            typeof(ProviderRoutingPolicyModule), typeof(ProviderRoutingPolicyInput), typeof(ProviderRoutingDecision)
        }.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(member => member.ToString()));
        foreach (string forbidden in new[]
        {
            "Adapter", "Delegate", "Transport", "Credential", "Cost", "Endpoint", "Model", "Retry",
            "File", "Journal", "Task", "World", "Http", "Socket", "Process", "Stream"
        })
            Assert.DoesNotContain(forbidden, surface, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderRoutingDecision Decide(
        ProviderRoutingIntent intent,
        ProviderReadiness openRouter,
        ProviderReadiness ollama,
        ProviderPrimaryAttemptState primary) =>
        ProviderRoutingPolicyModule.Decide(Input(intent, openRouter, ollama, primary), AcceptedComparison);

    private static ProviderRoutingPolicyInput Input(
        ProviderRoutingIntent intent,
        ProviderReadiness openRouter,
        ProviderReadiness ollama,
        ProviderPrimaryAttemptState primary) => new(intent, openRouter, ollama, primary);

    private static void AssertClosed(
        ProviderRoutingPolicyInput input,
        ReadOnlyMemory<byte>? comparison,
        string comparisonStatus,
        string reason)
    {
        ProviderRoutingDecision decision = ProviderRoutingPolicyModule.Decide(input, comparison);
        Assert.Null(decision.SelectedProvider);
        Assert.Equal(comparisonStatus, decision.ComparisonStatus);
        Assert.Equal(reason, decision.ReasonCode);
    }

    private static CognitionQualityComparisonArtifact Compare(
        CognitionQualityNormalizedProposalEvidence ollama,
        CognitionQualityNormalizedProposalEvidence openRouter) => CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(new string('a', 64), ollama),
            new CognitionQualityComparisonInput(new string('b', 64), openRouter));

    private static CognitionQualityNormalizedProposalEvidence Evidence(
        IReadOnlyList<CognitionQualitySubmission> proposals,
        string digest) => CognitionQualityNormalizedProposalEvidenceCodec.Create(
            CognitionQualityRecordingEvidenceModule.SchemaVersion,
            digest,
            proposals);

    private static CognitionQualitySubmission[] PreferredEnvelope() => CognitionQualityCorpusV1.CreateSnapshot().Scenarios
        .Select(scenario => new CognitionQualitySubmission(scenario.ScenarioId, Preferred(scenario.ScenarioId))).ToArray();

    private static CognitionQualitySubmission[] AllIdleEnvelope() => CognitionQualityCorpusV1.CreateSnapshot().Scenarios
        .Select(scenario => new CognitionQualitySubmission(
            scenario.ScenarioId,
            new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle))).ToArray();

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

    private static byte[] ReadAcceptedComparison()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("repository_root_not_found");
        string path = Path.Combine(directory.FullName, "artifacts", "snowglobe", "cognition-quality", "provider-comparison-v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return bytes;
    }

    private static byte[] ForgeDecision(ProviderRoutingDecision decision, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(decision.CanonicalUtf8.Span)!.AsObject();
        mutate(root);
        root.Remove("decision_payload_digest_sha256");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root["decision_payload_digest_sha256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private sealed class ChangingMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _later;
        private int _getSpanCallCount;

        internal ChangingMemoryManager(byte[] first, byte[] later)
        {
            Assert.Equal(first.Length, later.Length);
            _first = first.ToArray();
            _later = later.ToArray();
        }

        internal int GetSpanCallCount => _getSpanCallCount;
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_first.Length);

        public override Span<byte> GetSpan() =>
            Interlocked.Increment(ref _getSpanCallCount) == 1 ? _first : _later;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }

        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_later);
        }
    }
}
