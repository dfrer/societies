using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumPaidRunReadinessManifestTests
{
    private const string FrozenCanonicalManifest = """
        schema=snow_globe_openrouter_paid_run_readiness_manifest/v2
        model=openai/gpt-5.6-luna
        canonical_dated_release=openai/gpt-5.6-luna-20260709
        provider_only=azure
        zdr_required=true
        maximum_sequential_slots=12
        attempts_per_slot=1
        retries_per_slot=0
        alternate_or_fallback_routes=0
        maximum_input_tokens=4096
        maximum_output_tokens=512
        reasoning_effort=minimal
        reasoning_excluded=true
        required_finish_reason=stop
        per_slot_lease_ms=15000
        aggregate_window_ms=240000
        key_expiry_bounded=true
        per_slot_cost_ceiling_microusd=1500
        aggregate_cost_ceiling_microusd=18000
        stop_after_terminal_or_uncertain_outcome=true
        fresh_explicit_authorization_required=true
        historical_attempt_4_v1_manifest_digest_sha256=d1e653468fd7a39e33ad355297adf96c48bde97e40a73f6b6c6812623553f737
        trust_anchor_required=true
        trust_anchor_status=not_inspected
        production_factory_constructed=false
        additional_attempt_authorized=false
        state_contract_digest_sha256=REPLACE_STATE_DIGEST
        state_root_policy=fixed_local_app_data_v2_no_v1_observation
        live_readiness=false
        """;

    [Fact]
    public void Create_IsCanonicalBoundedRawFreeAndDigestBound()
    {
        OpenRouterPremiumPaidRunReadinessManifest manifest = OpenRouterPremiumPaidRunReadinessManifest.Create();

        Assert.Equal("openai/gpt-5.6-luna", manifest.Model);
        Assert.Equal("openai/gpt-5.6-luna-20260709", manifest.CanonicalDatedRelease);
        Assert.Equal("azure", manifest.ProviderOnly);
        Assert.True(manifest.ZdrRequired);
        Assert.Equal(12, manifest.MaximumSequentialSlots);
        Assert.Equal(1, manifest.AttemptsPerSlot);
        Assert.Equal(0, manifest.RetriesPerSlot);
        Assert.Equal(0, manifest.AlternateOrFallbackRoutes);
        Assert.Equal(4_096, manifest.MaximumInputTokens);
        Assert.Equal(512, manifest.MaximumOutputTokens);
        Assert.Equal("minimal", manifest.ReasoningEffort);
        Assert.True(manifest.ReasoningExcluded);
        Assert.Equal("stop", manifest.RequiredFinishReason);
        Assert.Equal(15_000, manifest.PerSlotLeaseMilliseconds);
        Assert.Equal(240_000, manifest.AggregateWindowMilliseconds);
        Assert.True(manifest.KeyExpiryBounded);
        Assert.Equal(1_500, manifest.PerSlotCostCeilingMicrousd);
        Assert.Equal(18_000, manifest.AggregateCostCeilingMicrousd);
        Assert.True(manifest.StopAfterTerminalOrUncertainOutcome);
        Assert.True(manifest.FreshExplicitAuthorizationRequired);
        Assert.True(manifest.TrustAnchorRequired);
        Assert.Equal("not_inspected", manifest.TrustAnchorStatus);
        Assert.False(manifest.ProductionFactoryConstructed);
        Assert.False(manifest.AdditionalAttemptAuthorized);
        Assert.Equal(OpenRouterPremiumStateGenerationStore.StateContractDigestSha256,
            manifest.StateContractDigestSha256);
        Assert.Equal("fixed_local_app_data_v2_no_v1_observation", manifest.StateRootPolicy);
        Assert.False(manifest.LiveReadiness);
        Assert.Equal(FrozenCanonicalManifest.Replace("REPLACE_STATE_DIGEST",
            OpenRouterPremiumStateGenerationStore.StateContractDigestSha256, StringComparison.Ordinal),
            manifest.CanonicalManifest);
        Assert.Equal("eea26a92d318a2ba102c7979d0cb44563d8bef967ae00b627bc6263ff59d759d",
            manifest.DigestSha256);
        Assert.Equal(OpenRouterPremiumCanonical.Digest(manifest.CanonicalManifest), manifest.DigestSha256);
        Assert.True(OpenRouterPremiumCanonical.IsDigest(manifest.DigestSha256));
        Assert.DoesNotContain("https://", manifest.CanonicalManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization_digest", manifest.CanonicalManifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", manifest.CanonicalManifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", manifest.CanonicalManifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("response", manifest.CanonicalManifest, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(manifest.CanonicalManifest.Length, 1, 4 * 1024);
        string cliLine = manifest.ToCliLine();
        Assert.InRange(cliLine.Length, 1, 4 * 1024);
        Assert.DoesNotContain('\r', cliLine);
        Assert.DoesNotContain('\n', cliLine);
        Assert.All(cliLine, character => Assert.InRange(character, ' ', '~'));
    }

    [Fact]
    public void Create_IsBackedByTheActualSingleUseExecutorBehavior()
    {
        OpenRouterPremiumPaidRunBehaviorEvidence evidence =
            OpenRouterPremiumPaidRunBehaviorConformance.Evidence;

        Assert.Equal(12, evidence.SuccessExchangeCalls);
        Assert.Equal(12, evidence.SuccessLeaseCalls);
        Assert.Equal(1, evidence.SuccessMaximumConcurrentCalls);
        Assert.Equal(1, evidence.UncertainExchangeCalls);
        Assert.Equal(1, evidence.UncertainLeaseCalls);
        Assert.Equal("terminal", evidence.UncertainStatus);
        Assert.Equal(SubmissionState.SubmissionUnknown, evidence.UncertainSubmissionState);
        Assert.Equal(ChargeState.Unknown, evidence.UncertainChargeState);
        Assert.Equal(0, evidence.PreDispatchTerminalExchangeCalls);
        Assert.Equal(1, evidence.PreDispatchTerminalLeaseCalls);
        Assert.Equal("terminal", evidence.PreDispatchTerminalStatus);
        Assert.Equal(SubmissionState.DefinitelyNotSubmitted, evidence.PreDispatchTerminalSubmissionState);
        Assert.Equal(ChargeState.Released, evidence.PreDispatchTerminalChargeState);
        Assert.Equal("capability_consumed", evidence.ReuseFailureCode);
        Assert.Equal(0, evidence.ReuseExchangeCalls);
        Assert.Equal(0, evidence.ReuseLeaseCalls);
        Assert.Equal("authorization_nonce_consumed", evidence.DuplicateNonceFailureCode);
        Assert.Equal(0, evidence.DuplicateNonceExchangeCalls);
        Assert.Equal(0, evidence.DuplicateNonceLeaseCalls);
        Assert.Equal(12, evidence.SuccessLeaseZeroObservations.Count);
        for (int ordinal = 0; ordinal < 12; ordinal++)
            Assert.True(evidence.SuccessLeaseZeroObservations[ordinal], $"Lease zero observation {ordinal + 1} was false.");
        Assert.Single(evidence.UncertainLeaseZeroObservations);
        Assert.True(evidence.UncertainLeaseZeroObservations[0]);
        Assert.Single(evidence.PreDispatchTerminalLeaseZeroObservations);
        Assert.True(evidence.PreDispatchTerminalLeaseZeroObservations[0]);
        Assert.True(evidence.AllLeaseBuffersZeroed);
    }

    [Fact]
    public void LeaseZeroConformance_RejectsMissingFalseExtraAndNullObservationEvidence()
    {
        OpenRouterPremiumPaidRunBehaviorEvidence valid = OpenRouterPremiumPaidRunBehaviorConformance.Evidence;
        Assert.True(valid.AllLeaseBuffersZeroed);

        Assert.False((valid with
        {
            SuccessLeaseZeroObservations = Array.AsReadOnly(valid.SuccessLeaseZeroObservations.Take(11).ToArray())
        }).AllLeaseBuffersZeroed);

        bool[] falseObservation = valid.SuccessLeaseZeroObservations.ToArray();
        falseObservation[5] = false;
        Assert.False((valid with
        {
            SuccessLeaseZeroObservations = Array.AsReadOnly(falseObservation)
        }).AllLeaseBuffersZeroed);

        Assert.False((valid with
        {
            SuccessLeaseZeroObservations = Array.AsReadOnly(valid.SuccessLeaseZeroObservations.Append(true).ToArray())
        }).AllLeaseBuffersZeroed);
        Assert.False((valid with { SuccessLeaseZeroObservations = null! }).AllLeaseBuffersZeroed);
        Assert.False((valid with { UncertainLeaseZeroObservations = Array.Empty<bool>() }).AllLeaseBuffersZeroed);
        Assert.False((valid with { PreDispatchTerminalLeaseZeroObservations = Array.AsReadOnly(new[] { false }) }).AllLeaseBuffersZeroed);
    }

    [Fact]
    public void Create_IsDeterministic()
    {
        OpenRouterPremiumPaidRunReadinessManifest first = OpenRouterPremiumPaidRunReadinessManifest.Create();
        OpenRouterPremiumPaidRunReadinessManifest second = OpenRouterPremiumPaidRunReadinessManifest.Create();

        Assert.Equal(first.CanonicalManifest, second.CanonicalManifest);
        Assert.Equal(first.DigestSha256, second.DigestSha256);
        Assert.Equal(first.ToCliLine(), second.ToCliLine());
    }
}
