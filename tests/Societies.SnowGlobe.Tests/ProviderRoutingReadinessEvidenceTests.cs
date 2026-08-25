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

public sealed class ProviderRoutingReadinessEvidenceTests
{
    private static readonly byte[] Comparison = ReadArtifact("artifacts/snowglobe/cognition-quality/provider-comparison-v1.json");
    private static readonly byte[] OllamaExecution = ReadArtifact("artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v6.json");

    [Fact]
    public void AcceptedHistoricalEvidenceRemainsUnknownAndNeverIssuesRoutingInput()
    {
        byte[] activation = CreateEligibleActivationArtifact();
        byte[] openRouterExecution = CreateHistoricalOpenRouterExecutionArtifact();

        ProviderRoutingReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.Assess(new(
            Comparison,
            activation,
            OllamaExecution,
            openRouterExecution));

        Assert.Equal("insufficient_current_readiness_evidence", assessment.Status);
        Assert.Equal("80a6e228280f3d8e4e75459279452049076fded3fe5709a7e5523a61a61200be",
            ProviderRoutingReadinessEvidenceModule.ContractDigestSha256);
        Assert.Equal("accepted_openrouter_default", assessment.SelectionEvidence);
        Assert.Equal("accepted", assessment.ComparisonEvidence.Status);
        Assert.Equal(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            assessment.ComparisonEvidence.ArtifactDigestSha256);
        Assert.Equal(CognitionQualityComparisonModule.SchemaVersion, assessment.ComparisonEvidence.SchemaVersion);
        Assert.Equal("selection_only", assessment.ComparisonEvidence.TemporalScope);
        Assert.Equal("evaluated_eligible_live_traffic_disabled", assessment.OpenRouterActivationEvidence.Status);
        Assert.Equal(Digest(activation), assessment.OpenRouterActivationEvidence.ArtifactDigestSha256);
        Assert.Equal(OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion,
            assessment.OpenRouterActivationEvidence.SchemaVersion);
        Assert.Equal("evaluated_eligibility_only", assessment.OpenRouterActivationEvidence.TemporalScope);
        Assert.Equal("historical_compatibility_complete", assessment.OllamaExecutionEvidence.Status);
        Assert.Equal(Digest(OllamaExecution), assessment.OllamaExecutionEvidence.ArtifactDigestSha256);
        Assert.Equal(OllamaRecordingExecutionArtifactModule.SchemaVersion, assessment.OllamaExecutionEvidence.SchemaVersion);
        Assert.Equal("historical_only", assessment.OllamaExecutionEvidence.TemporalScope);
        Assert.Equal("historical_generation_terminal", assessment.OpenRouterExecutionEvidence.Status);
        Assert.Equal(Digest(openRouterExecution), assessment.OpenRouterExecutionEvidence.ArtifactDigestSha256);
        Assert.Equal(OpenRouterPremiumEvidenceArtifactModule.SchemaVersion, assessment.OpenRouterExecutionEvidence.SchemaVersion);
        Assert.Equal("historical_generation_only", assessment.OpenRouterExecutionEvidence.TemporalScope);
        Assert.Equal("unknown", assessment.OpenRouterCurrentReadiness);
        Assert.Equal("unknown", assessment.OllamaCurrentReadiness);
        Assert.Equal("unknown", assessment.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
        Assert.Equal(new[]
        {
            "current_openrouter_authenticated_readiness_unproven",
            "current_ollama_runtime_readiness_unproven",
            "authenticated_attempt_bound_primary_state_unproven",
            "freshness_current_observation_unproven"
        }, assessment.GapCodes);
        Assert.DoesNotContain("\"routing_policy_input\":{", assessment.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"routing_policy_input\":null", assessment.CanonicalJson, StringComparison.Ordinal);
        Assert.Equal(assessment.CanonicalDigestSha256,
            ProviderRoutingReadinessEvidenceModule.Validate(assessment.CanonicalUtf8).CanonicalDigestSha256);
    }

    [Fact]
    public void ValidAlternateComparisonCannotConferAcceptedSelectionEvidence()
    {
        CognitionQualityComparisonArtifact accepted = CognitionQualityComparisonModule.Validate(Comparison);
        CognitionQualityProviderComparison ollama = accepted.Providers[0];
        CognitionQualityProviderComparison openRouter = accepted.Providers[1];
        string alternateSourceDigest = ollama.SourceArtifactDigestSha256![0] == '0'
            ? "1" + ollama.SourceArtifactDigestSha256[1..]
            : "0" + ollama.SourceArtifactDigestSha256[1..];
        CognitionQualityComparisonArtifact alternate = CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(alternateSourceDigest, ollama.NormalizedProposalEvidence!),
            new CognitionQualityComparisonInput(openRouter.SourceArtifactDigestSha256!, openRouter.NormalizedProposalEvidence!));
        Assert.Equal("openrouter_default", alternate.Recommendation);
        Assert.NotEqual(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256, alternate.CanonicalDigestSha256);

        ProviderRoutingReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.Assess(new(
            alternate.CanonicalUtf8));

        Assert.Equal("unsupported", assessment.ComparisonEvidence.Status);
        Assert.Equal("unaccepted", assessment.SelectionEvidence);
        Assert.Contains("accepted_comparison_evidence_unproven", assessment.GapCodes);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
    }

    [Fact]
    public void PinnedAcceptedComparisonDigestCannotBeRelabeledUnsupportedOrMalformed()
    {
        ProviderRoutingReadinessAssessment accepted = ProviderRoutingReadinessEvidenceModule.Assess(new(Comparison));
        byte[] unsupported = ForgeAssessment(accepted, root =>
        {
            root["selection_evidence"] = "unaccepted";
            JsonObject comparison = root["comparison_evidence"]!.AsObject();
            comparison["status"] = "unsupported";
            comparison["detail_code"] = "ollama_default";
            root["gap_codes"]!.AsArray().Insert(0, "accepted_comparison_evidence_unproven");
        });
        byte[] malformed = ForgeAssessment(accepted, root =>
        {
            root["selection_evidence"] = "unaccepted";
            JsonObject comparison = root["comparison_evidence"]!.AsObject();
            comparison["status"] = "malformed";
            comparison["schema_version"] = null;
            comparison["temporal_scope"] = "none";
            comparison["detail_code"] = null;
            root["gap_codes"]!.AsArray().Insert(0, "accepted_comparison_evidence_unproven");
        });

        Assert.Equal("assessment_binding_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.Validate(unsupported)).Code);
        Assert.Equal("assessment_binding_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.Validate(malformed)).Code);
    }

    [Fact]
    public void MissingEvidenceIsUnknownAndNeverNegativeOrNotStarted()
    {
        ProviderRoutingReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.Assess(new(
            Comparison,
            null,
            null,
            null));

        Assert.Equal("missing", assessment.OpenRouterActivationEvidence.Status);
        Assert.Equal("missing", assessment.OllamaExecutionEvidence.Status);
        Assert.Equal("missing", assessment.OpenRouterExecutionEvidence.Status);
        Assert.Equal("unknown", assessment.OpenRouterCurrentReadiness);
        Assert.Equal("unknown", assessment.OllamaCurrentReadiness);
        Assert.Equal("unknown", assessment.PrimaryAttemptCurrentState);
        Assert.DoesNotContain("not_ready", assessment.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"primary_attempt_current_state\":\"not_started\"", assessment.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("openrouter_activation_evidence_missing", assessment.GapCodes);
        Assert.Contains("ollama_execution_evidence_missing", assessment.GapCodes);
        Assert.Contains("openrouter_execution_evidence_missing", assessment.GapCodes);
    }

    [Fact]
    public void AllMissingIncludingRequiredComparisonCannotIssueRoutingFacts()
    {
        ProviderRoutingReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.Assess(new(
            null,
            null,
            null,
            null));

        Assert.Equal("unaccepted", assessment.SelectionEvidence);
        Assert.Equal("missing", assessment.ComparisonEvidence.Status);
        Assert.Equal("unknown", assessment.OpenRouterCurrentReadiness);
        Assert.Equal("unknown", assessment.OllamaCurrentReadiness);
        Assert.Equal("unknown", assessment.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
        Assert.Contains("\"routing_policy_input\":null", assessment.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("accepted_comparison_evidence_unproven", assessment.GapCodes);
    }

    [Fact]
    public void MalformedDuplicateDeepAndOversizedEvidenceFailsClosedWithoutReadingOversizedMemory()
    {
        byte[] duplicateComparison = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Comparison).Replace(
            "{\"schema_version\"",
            "{\"schema_version\":\"duplicate\",\"schema_version\"",
            StringComparison.Ordinal));
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 24) + new string(']', 24));
        ProviderRoutingReadinessAssessment malformed = ProviderRoutingReadinessEvidenceModule.Assess(new(
            duplicateComparison,
            deep,
            "{bad"u8.ToArray(),
            "{bad"u8.ToArray()));
        Assert.Equal("malformed", malformed.ComparisonEvidence.Status);
        Assert.Equal("malformed", malformed.OpenRouterActivationEvidence.Status);
        Assert.Equal("malformed", malformed.OllamaExecutionEvidence.Status);
        Assert.Equal("malformed", malformed.OpenRouterExecutionEvidence.Status);
        Assert.Equal("not_issued", malformed.RoutingInputIssuanceStatus);

        using UnreadableOversizedMemoryManager comparison = new(CognitionQualityComparisonModule.MaximumArtifactBytes + 1);
        using UnreadableOversizedMemoryManager activation = new(OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes + 1);
        using UnreadableOversizedMemoryManager ollama = new(OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes + 1);
        using UnreadableOversizedMemoryManager openRouter = new(OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes + 1);
        ProviderRoutingReadinessAssessment oversized = ProviderRoutingReadinessEvidenceModule.Assess(new(
            comparison.CreateReadOnlyMemory(),
            activation.CreateReadOnlyMemory(),
            ollama.CreateReadOnlyMemory(),
            openRouter.CreateReadOnlyMemory()));
        Assert.Equal("malformed", oversized.ComparisonEvidence.Status);
        Assert.Equal("malformed", oversized.OpenRouterActivationEvidence.Status);
        Assert.Equal("malformed", oversized.OllamaExecutionEvidence.Status);
        Assert.Equal("malformed", oversized.OpenRouterExecutionEvidence.Status);
        Assert.Equal(0, comparison.GetSpanCallCount + activation.GetSpanCallCount + ollama.GetSpanCallCount + openRouter.GetSpanCallCount);
        Assert.All(new[]
        {
            oversized.ComparisonEvidence,
            oversized.OpenRouterActivationEvidence,
            oversized.OllamaExecutionEvidence,
            oversized.OpenRouterExecutionEvidence
        }, evidence => Assert.Null(evidence.ArtifactDigestSha256));
    }

    [Fact]
    public void EveryCallerArtifactIsSnapshottedExactlyOnceBeforeValidation()
    {
        byte[] activation = CreateEligibleActivationArtifact();
        byte[] openRouterExecution = CreateHistoricalOpenRouterExecutionArtifact();
        using ChangingMemoryManager comparison = new(Comparison);
        using ChangingMemoryManager activationMemory = new(activation);
        using ChangingMemoryManager ollama = new(OllamaExecution);
        using ChangingMemoryManager openRouter = new(openRouterExecution);

        ProviderRoutingReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.Assess(new(
            comparison.CreateReadOnlyMemory(),
            activationMemory.CreateReadOnlyMemory(),
            ollama.CreateReadOnlyMemory(),
            openRouter.CreateReadOnlyMemory()));

        Assert.Equal("accepted", assessment.ComparisonEvidence.Status);
        Assert.Equal("evaluated_eligible_live_traffic_disabled", assessment.OpenRouterActivationEvidence.Status);
        Assert.Equal("historical_compatibility_complete", assessment.OllamaExecutionEvidence.Status);
        Assert.Equal("historical_generation_terminal", assessment.OpenRouterExecutionEvidence.Status);
        Assert.Equal(1, comparison.GetSpanCallCount);
        Assert.Equal(1, activationMemory.GetSpanCallCount);
        Assert.Equal(1, ollama.GetSpanCallCount);
        Assert.Equal(1, openRouter.GetSpanCallCount);
    }

    [Fact]
    public void AssessmentIsCanonicalBoundedRawFreeDetachedAndRepeatable()
    {
        byte[] rawSentinel = "{\"raw_prompt\":\"READINESS_SECRET_SENTINEL\""u8.ToArray();
        ProviderRoutingReadinessEvidenceInput input = new(Comparison, rawSentinel, rawSentinel, rawSentinel);
        ProviderRoutingReadinessAssessment first = ProviderRoutingReadinessEvidenceModule.Assess(input);
        ProviderRoutingReadinessAssessment second = ProviderRoutingReadinessEvidenceModule.Assess(input);

        Assert.Equal(first.CanonicalDigestSha256, second.CanonicalDigestSha256);
        Assert.InRange(first.CanonicalUtf8.Length, 1, ProviderRoutingReadinessEvidenceModule.MaximumAssessmentBytes);
        Assert.DoesNotContain("READINESS_SECRET_SENTINEL", first.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("raw_prompt", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        ReadOnlyMemory<byte> detached = first.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', first.CanonicalUtf8.Span[0]);

        byte[] tampered = first.CanonicalUtf8.ToArray();
        tampered[^2] ^= 1;
        Assert.Throws<ProviderRoutingReadinessEvidenceException>(() => ProviderRoutingReadinessEvidenceModule.Validate(tampered));
        byte[] duplicate = Encoding.UTF8.GetBytes(first.CanonicalJson.Replace(
            "{\"schema_version\"",
            "{\"schema_version\":\"duplicate\",\"schema_version\"",
            StringComparison.Ordinal));
        Assert.Equal("assessment_shape_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.Validate(duplicate)).Code);
        Assert.Equal("assessment_size_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.Validate(new byte[ProviderRoutingReadinessEvidenceModule.MaximumAssessmentBytes + 1])).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 24) + new string(']', 24));
        Assert.Equal("assessment_json_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.Validate(deep)).Code);
    }

    [Fact]
    public void PublicSurfaceHasNoRoutingInputIssuanceOrExecutionAuthority()
    {
        Assert.Equal(new[] { "Assess", "Validate" }, typeof(ProviderRoutingReadinessEvidenceModule)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(ProviderRoutingReadinessEvidenceModule).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ProviderRoutingReadinessEvidenceModule).Namespace
                && type.Name.StartsWith("ProviderRoutingReadiness", StringComparison.Ordinal))
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)),
            member => member is MethodInfo method
                && (method.ReturnType == typeof(ProviderRoutingPolicyInput)
                    || method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ProviderRoutingPolicyInput))));
        string surface = string.Join('|', new[]
        {
            typeof(ProviderRoutingReadinessEvidenceModule),
            typeof(ProviderRoutingReadinessEvidenceInput),
            typeof(ProviderRoutingReadinessAssessment),
            typeof(ProviderRoutingReadinessEvidenceFact)
        }.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(member => member.ToString()));
        foreach (string forbidden in new[]
        {
            "Adapter", "Transport", "Credential", "Account", "Endpoint", "Model", "Selector", "Cost", "Payment",
            "Retry", "File", "Path", "Journal", "Task", "World", "Http", "Socket", "Process", "Stream"
        })
            Assert.DoesNotContain(forbidden, surface, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateHistoricalOpenRouterExecutionArtifact()
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        ByokAccountBindingIdentity account = new("byok-account-sha256-" + new string('a', 64));
        OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
            "openrouter-premium-journal/readiness-history",
            "openrouter-premium-run/readiness-history",
            profile,
            account);
        FakeCredentialLeaseSource leases = new();
        ScriptedOpenRouterPremiumExchange exchange = ScriptedOpenRouterPremiumExchange.CreateSuccessful();
        OpenRouterPremiumAuthorization authorization = new(
            profile.Identity,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
            OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
            account,
            header.JournalIdentity,
            header.HeaderChecksumSha256,
            exchange.Identity,
            exchange.ContractDigestSha256,
            leases.Identity,
            "openrouter-premium-authorization/readiness-history",
            1_000,
            1_000 + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds);
        OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(authorization);
        CognitionQualityPromptEnvelopeSlot prompt = capability.Publication.Slots[0];
        OpenRouterPremiumSlotReceipt receipt = new(
            1,
            prompt.ScenarioId,
            prompt.PromptDigestSha256,
            new string('1', 64),
            new string('2', 64),
            SubmissionState.SubmissionUnknown,
            ChargeState.Unknown,
            0,
            0,
            0,
            0,
            null,
            "provider_response_rejected");
        return OpenRouterPremiumEvidenceArtifactModule.Create(capability, header, exchange.Identity, [receipt]).CanonicalUtf8.ToArray();
    }

    private static byte[] CreateEligibleActivationArtifact()
    {
        byte[] payload = WriteEligibleActivationArtifact(null);
        string payloadDigest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(payload);
        return WriteEligibleActivationArtifact(payloadDigest);
    }

    private static byte[] WriteEligibleActivationArtifact(string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion);
        writer.WriteString("decision", "eligible_for_separately_authorized_one_shot");
        writer.WriteBoolean("eligible", true); writer.WriteBoolean("live_traffic_enabled", false);
        writer.WriteString("profile_digest_sha256", OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256);
        writer.WriteString("canonical_model_slug", OpenRouterPremiumProfile.CanonicalModelSlug);
        writer.WriteBoolean("api_model_id_verified", true);
        writer.WriteString("provider_slug", OpenRouterPremiumProfile.ProviderSlug);
        writer.WriteString("evidence_observed_at_utc", "2026-08-21T12:00:00Z");
        writer.WriteNumber("evaluated_at_unix_milliseconds", DateTimeOffset.Parse(
            "2026-08-21T12:00:00Z", CultureInfo.InvariantCulture).ToUnixTimeMilliseconds());
        writer.WriteString("catalog_snapshot_digest_sha256", new string('1', 64));
        writer.WriteString("frozen_catalog_evidence_digest_sha256", OpenRouterPremiumProfile.CatalogEvidenceDigestSha256);
        writer.WriteBoolean("pricing_override_authenticated_current", true);
        writer.WriteBoolean("pricing_override_reachable_for_profile", false);
        writer.WriteString("credential_attestation_digest_sha256", new string('2', 64));
        writer.WriteString("account_binding_identity", "byok-account-sha256-" + new string('a', 64));
        writer.WriteString("credential_source_identity", OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity);
        writer.WriteString("journal_identity", "openrouter-premium-journal/readiness-history");
        writer.WriteString("journal_header_checksum_sha256", new string('3', 64));
        writer.WriteString("durable_restart_evidence_digest_sha256", new string('4', 64));
        writer.WriteString("registered_trust_evidence_digest_sha256", new string('5', 64));
        writer.WriteString("authorization_nonce_digest_sha256", new string('6', 64));
        writer.WriteString("durable_preflight_consumption_evidence_digest_sha256", new string('7', 64));
        writer.WriteNumber("maximum_requests", 12); writer.WriteNumber("per_slot_cost_ceiling_microusd", 1_500);
        writer.WriteNumber("aggregate_cost_ceiling_microusd", 18_000);
        writer.WritePropertyName("blocker_codes"); writer.WriteStartArray(); writer.WriteEndArray();
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray();
        foreach (string limitation in new[]
        {
            "attestation_identity_is_approval_input_not_cryptographic_signature",
            "catalog_snapshot_requires_registered_authenticated_metadata_trust",
            "pricing_override_is_frozen_content_addressed_evidence_not_authenticated_current_api",
            "pricing_override_is_unreachable_under_profile_input_bound",
            "flush_request_and_restart_readback_do_not_prove_physical_media_durability",
            "preflight_eligibility_does_not_enable_live_traffic_or_authorize_spend"
        }) writer.WriteStringValue(limitation);
        writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("preflight_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static byte[] ReadArtifact(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("repository_root_not_found");
        return File.ReadAllBytes(Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Digest(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static byte[] ForgeAssessment(ProviderRoutingReadinessAssessment assessment, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(assessment.CanonicalUtf8.Span)!.AsObject();
        mutate(root);
        root.Remove("assessment_payload_digest_sha256");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root["assessment_payload_digest_sha256"] = Digest(payload);
        CryptographicOperations.ZeroMemory(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private sealed class ChangingMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _later;
        private int _getSpanCallCount;

        internal ChangingMemoryManager(byte[] first)
        {
            _first = first.ToArray();
            _later = Enumerable.Repeat((byte)' ', first.Length).ToArray();
        }

        internal int GetSpanCallCount => _getSpanCallCount;
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_first.Length);
        public override Span<byte> GetSpan() => Interlocked.Increment(ref _getSpanCallCount) == 1 ? _first : _later;
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_later);
        }
    }

    private sealed class UnreadableOversizedMemoryManager : MemoryManager<byte>
    {
        private readonly int _length;
        private int _getSpanCallCount;

        internal UnreadableOversizedMemoryManager(int length) => _length = length;
        internal int GetSpanCallCount => _getSpanCallCount;
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_length);
        public override Span<byte> GetSpan()
        {
            Interlocked.Increment(ref _getSpanCallCount);
            throw new InvalidOperationException("oversized_memory_must_not_be_read");
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }
}
