using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>
/// A raw-free, zero-I/O projection of the fixed paid-run contract. This is deliberately not
/// an authorization: authenticated preflight remains the only path that can create authority.
/// </summary>
public sealed class OpenRouterPremiumPaidRunReadinessManifest
{
    public const string SchemaVersion = "snow_globe_openrouter_paid_run_readiness_manifest/v2";
    public const string HistoricalAttempt4V1DigestSha256 = "d1e653468fd7a39e33ad355297adf96c48bde97e40a73f6b6c6812623553f737";
    private const int ExpectedSequentialSlots = 12;
    private const int ExpectedAttemptsPerSlot = 1;
    private const int ExpectedRetries = 0;
    private const int ExpectedAlternatesOrFallbacks = 0;
    private const string RequiredReasoningEffort = "minimal";
    private const string ExpectedFinishReason = "stop";
    private const int MaximumCanonicalManifestCharacters = 4 * 1024;
    private const int MaximumCliLineCharacters = 4 * 1024;

    private OpenRouterPremiumPaidRunReadinessManifest(OpenRouterPremiumProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        OpenRouterPremiumPaidRunFrozenContract.RequireProfile(profile);
        OpenRouterPremiumBounds bounds = profile.Bounds;
        OpenRouterPremiumPaidRunRequestContract request = OpenRouterPremiumPaidRunRequestContract.Inspect(profile);
        OpenRouterPremiumPaidRunBehaviorEvidence behavior = OpenRouterPremiumPaidRunBehaviorConformance.Evidence;
        if (!profile.ZdrRequested
            || bounds.RequiredScenarioCount != ExpectedSequentialSlots
            || bounds.MaximumExchanges != ExpectedSequentialSlots
            || bounds.MaximumInputTokens != 4_096
            || bounds.MaximumOutputTokens != 512
            || bounds.CredentialLeaseLifetimeMilliseconds != OpenRouterPremiumProfile.CredentialLeaseLifetimeMilliseconds
            || bounds.TimeoutMilliseconds != OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds
            || bounds.PerSlotCostCeilingMicrousd != 1_500
            || bounds.AggregateCostCeilingMicrousd != 18_000
            || bounds.AggregateCostCeilingMicrousd != bounds.PerSlotCostCeilingMicrousd * bounds.MaximumExchanges
            || request.Model != OpenRouterPremiumProfile.ModelIdentity
            || request.ProviderOnly != OpenRouterPremiumProfile.ProviderSlug
            || request.MaximumOutputTokens != bounds.MaximumOutputTokens
            || request.ReasoningEffort != RequiredReasoningEffort
            || !request.ReasoningExcluded
            || !request.ZdrRequired
            || request.AlternateOrFallbackRoutes != ExpectedAlternatesOrFallbacks
            || behavior.ObservedAttemptsPerTerminalSlot != ExpectedAttemptsPerSlot
            || behavior.ObservedRetryCallsAfterTerminal != ExpectedRetries
            || request.RequiredFinishReason != ExpectedFinishReason
            || !behavior.StoppedAfterTerminalOrUncertainOutcome
            || !behavior.FreshExplicitAuthorizationRequired)
            throw new InvalidDataException("OpenRouter paid-run readiness contract is invalid.");

        Model = request.Model;
        CanonicalDatedRelease = OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity;
        ProviderOnly = request.ProviderOnly;
        ZdrRequired = request.ZdrRequired;
        MaximumSequentialSlots = bounds.MaximumExchanges;
        AttemptsPerSlot = behavior.ObservedAttemptsPerTerminalSlot;
        RetriesPerSlot = behavior.ObservedRetryCallsAfterTerminal;
        AlternateOrFallbackRoutes = ExpectedAlternatesOrFallbacks;
        MaximumInputTokens = bounds.MaximumInputTokens;
        MaximumOutputTokens = request.MaximumOutputTokens;
        ReasoningEffort = request.ReasoningEffort;
        ReasoningExcluded = request.ReasoningExcluded;
        RequiredFinishReason = request.RequiredFinishReason;
        PerSlotLeaseMilliseconds = bounds.CredentialLeaseLifetimeMilliseconds;
        AggregateWindowMilliseconds = bounds.TimeoutMilliseconds;
        KeyExpiryBounded = bounds.TimeoutMilliseconds == OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds
            && bounds.CredentialLeaseLifetimeMilliseconds == OpenRouterPremiumProfile.CredentialLeaseLifetimeMilliseconds;
        PerSlotCostCeilingMicrousd = bounds.PerSlotCostCeilingMicrousd;
        AggregateCostCeilingMicrousd = bounds.AggregateCostCeilingMicrousd;
        StopAfterTerminalOrUncertainOutcome = behavior.StoppedAfterTerminalOrUncertainOutcome;
        FreshExplicitAuthorizationRequired = behavior.FreshExplicitAuthorizationRequired;
        TrustAnchorRequired = true;
        TrustAnchorStatus = "not_inspected";
        ProductionFactoryConstructed = false;
        AdditionalAttemptAuthorized = false;
        StateContractDigestSha256 = OpenRouterPremiumStateGenerationStore.StateContractDigestSha256;
        StateRootPolicy = "fixed_local_app_data_v2_no_v1_observation";
        LiveReadiness = false;
        RequireSafeToken(SchemaVersion, 128);
        RequireSafeToken(Model, 256);
        RequireSafeToken(CanonicalDatedRelease, 256);
        RequireSafeToken(ProviderOnly, 128);
        RequireSafeToken(ReasoningEffort, 32);
        RequireSafeToken(RequiredFinishReason, 32);
        RequireSafeToken(TrustAnchorStatus, 32);
        RequireSafeToken(StateRootPolicy, 64);
        CanonicalManifest = string.Join('\n',
            $"schema={SchemaVersion}",
            $"model={Model}",
            $"canonical_dated_release={CanonicalDatedRelease}",
            $"provider_only={ProviderOnly}",
            $"zdr_required={ZdrRequired.ToString().ToLowerInvariant()}",
            $"maximum_sequential_slots={Invariant(MaximumSequentialSlots)}",
            $"attempts_per_slot={Invariant(AttemptsPerSlot)}",
            $"retries_per_slot={Invariant(RetriesPerSlot)}",
            $"alternate_or_fallback_routes={Invariant(AlternateOrFallbackRoutes)}",
            $"maximum_input_tokens={Invariant(MaximumInputTokens)}",
            $"maximum_output_tokens={Invariant(MaximumOutputTokens)}",
            $"reasoning_effort={ReasoningEffort}",
            $"reasoning_excluded={ReasoningExcluded.ToString().ToLowerInvariant()}",
            $"required_finish_reason={RequiredFinishReason}",
            $"per_slot_lease_ms={Invariant(PerSlotLeaseMilliseconds)}",
            $"aggregate_window_ms={Invariant(AggregateWindowMilliseconds)}",
            $"key_expiry_bounded={KeyExpiryBounded.ToString().ToLowerInvariant()}",
            $"per_slot_cost_ceiling_microusd={Invariant(PerSlotCostCeilingMicrousd)}",
            $"aggregate_cost_ceiling_microusd={Invariant(AggregateCostCeilingMicrousd)}",
            $"stop_after_terminal_or_uncertain_outcome={StopAfterTerminalOrUncertainOutcome.ToString().ToLowerInvariant()}",
            $"fresh_explicit_authorization_required={FreshExplicitAuthorizationRequired.ToString().ToLowerInvariant()}",
            $"historical_attempt_4_v1_manifest_digest_sha256={HistoricalAttempt4V1DigestSha256}",
            $"trust_anchor_required={TrustAnchorRequired.ToString().ToLowerInvariant()}",
            $"trust_anchor_status={TrustAnchorStatus}",
            $"production_factory_constructed={ProductionFactoryConstructed.ToString().ToLowerInvariant()}",
            $"additional_attempt_authorized={AdditionalAttemptAuthorized.ToString().ToLowerInvariant()}",
            $"state_contract_digest_sha256={StateContractDigestSha256}",
            $"state_root_policy={StateRootPolicy}",
            $"live_readiness={LiveReadiness.ToString().ToLowerInvariant()}");
        if (CanonicalManifest.Length is < 1 or > MaximumCanonicalManifestCharacters)
            throw new InvalidDataException("OpenRouter paid-run readiness contract is invalid.");
        DigestSha256 = OpenRouterPremiumCanonical.Digest(CanonicalManifest);
        CliLine = BuildCliLine();
        if (CliLine.Length is < 1 or > MaximumCliLineCharacters)
            throw new InvalidDataException("OpenRouter paid-run readiness contract is invalid.");
    }

    public string Model { get; }
    public string CanonicalDatedRelease { get; }
    public string ProviderOnly { get; }
    public bool ZdrRequired { get; }
    public int MaximumSequentialSlots { get; }
    public int AttemptsPerSlot { get; }
    public int RetriesPerSlot { get; }
    public int AlternateOrFallbackRoutes { get; }
    public int MaximumInputTokens { get; }
    public int MaximumOutputTokens { get; }
    public string ReasoningEffort { get; }
    public bool ReasoningExcluded { get; }
    public string RequiredFinishReason { get; }
    public int PerSlotLeaseMilliseconds { get; }
    public int AggregateWindowMilliseconds { get; }
    public bool KeyExpiryBounded { get; }
    public long PerSlotCostCeilingMicrousd { get; }
    public long AggregateCostCeilingMicrousd { get; }
    public bool StopAfterTerminalOrUncertainOutcome { get; }
    public bool FreshExplicitAuthorizationRequired { get; }
    public bool TrustAnchorRequired { get; }
    public string TrustAnchorStatus { get; }
    public bool ProductionFactoryConstructed { get; }
    public bool AdditionalAttemptAuthorized { get; }
    public string StateContractDigestSha256 { get; }
    public string StateRootPolicy { get; }
    public bool LiveReadiness { get; }
    public string CanonicalManifest { get; }
    public string DigestSha256 { get; }
    private string CliLine { get; }

    public static OpenRouterPremiumPaidRunReadinessManifest Create() => new(OpenRouterPremiumProfileRegistry.Selected);

    public string ToCliLine() => CliLine;

    private string BuildCliLine() => string.Join(' ',
        "OPENROUTER_PAID_RUN_PLAN",
        $"schema={SchemaVersion}",
        $"model={Model}",
        $"canonical_dated_release={CanonicalDatedRelease}",
        $"provider_only={ProviderOnly}",
        $"zdr_required={ZdrRequired.ToString().ToLowerInvariant()}",
        $"maximum_sequential_slots={Invariant(MaximumSequentialSlots)}",
        $"attempts_per_slot={Invariant(AttemptsPerSlot)}",
        $"retries_per_slot={Invariant(RetriesPerSlot)}",
        $"alternate_or_fallback_routes={Invariant(AlternateOrFallbackRoutes)}",
        $"maximum_input_tokens={Invariant(MaximumInputTokens)}",
        $"maximum_output_tokens={Invariant(MaximumOutputTokens)}",
        $"reasoning_effort={ReasoningEffort}",
        $"reasoning_excluded={ReasoningExcluded.ToString().ToLowerInvariant()}",
        $"required_finish_reason={RequiredFinishReason}",
        $"per_slot_lease_ms={Invariant(PerSlotLeaseMilliseconds)}",
        $"aggregate_window_ms={Invariant(AggregateWindowMilliseconds)}",
        $"key_expiry_bounded={KeyExpiryBounded.ToString().ToLowerInvariant()}",
        $"per_slot_cost_ceiling_microusd={Invariant(PerSlotCostCeilingMicrousd)}",
        $"aggregate_cost_ceiling_microusd={Invariant(AggregateCostCeilingMicrousd)}",
        $"stop_after_terminal_or_uncertain_outcome={StopAfterTerminalOrUncertainOutcome.ToString().ToLowerInvariant()}",
        $"fresh_explicit_authorization_required={FreshExplicitAuthorizationRequired.ToString().ToLowerInvariant()}",
        $"historical_attempt_4_v1_manifest_digest_sha256={HistoricalAttempt4V1DigestSha256}",
        $"trust_anchor_required={TrustAnchorRequired.ToString().ToLowerInvariant()}",
        $"trust_anchor_status={TrustAnchorStatus}",
        $"production_factory_constructed={ProductionFactoryConstructed.ToString().ToLowerInvariant()}",
        $"additional_attempt_authorized={AdditionalAttemptAuthorized.ToString().ToLowerInvariant()}",
        $"state_contract_digest_sha256={StateContractDigestSha256}",
        $"state_root_policy={StateRootPolicy}",
        $"live_readiness={LiveReadiness.ToString().ToLowerInvariant()}",
        $"manifest_digest_sha256={DigestSha256}");

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static void RequireSafeToken(string value, int maximumCharacters)
    {
        if (value.Length is < 1 || value.Length > maximumCharacters
            || value.Any(static character => !(character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-' or '/')))
            throw new InvalidDataException("OpenRouter paid-run readiness contract is invalid.");
    }
}

internal static class OpenRouterPremiumPaidRunFrozenContract
{
    internal const string Model = "openai/gpt-5.6-luna";
    internal const string DatedRelease = "openai/gpt-5.6-luna-20260709";
    internal const string ProviderSlug = "azure";
    internal const string ProviderResponseIdentity = "Azure";
    internal const string RequiredReasoningEffort = "minimal";
    internal const string RequiredFinishReason = "stop";
    internal const int RequiredScenarioCount = 12;
    internal const int MaximumExchanges = 12;
    internal const int MaximumRequestBytes = 8 * 1024;
    internal const int MaximumResponseBytes = 8 * 1024;
    internal const int MaximumAggregateRequestBytes = 12 * 8 * 1024;
    internal const int MaximumAggregateResponseBytes = 12 * 8 * 1024;
    internal const int MaximumResponseHeaderBytes = 8 * 1024;
    internal const int MaximumJsonDepth = 8;
    internal const int MaximumJsonTokens = 256;
    internal const int MaximumStringCharacters = 4 * 1024;
    internal const int MaximumArrayItems = 16;
    internal const int MaximumInputTokens = 4 * 1024;
    internal const int MaximumOutputTokens = 512;
    internal const int AggregateWindowMilliseconds = 240_000;
    internal const int CredentialLeaseLifetimeMilliseconds = 15_000;
    internal const long PerSlotCostCeilingMicrousd = 1_500;
    internal const long AggregateCostCeilingMicrousd = 18_000;
    internal const decimal ProviderMaxPromptUsdPerMillionTokens = 0.2m;
    internal const decimal ProviderMaxCompletionUsdPerMillionTokens = 1.2m;

    internal static void RequireProfile(OpenRouterPremiumProfile profile)
    {
        OpenRouterPremiumBounds bounds = profile.Bounds;
        if (OpenRouterPremiumProfile.SchemaVersion != "snow_globe_openrouter_premium_profile/v1"
            || OpenRouterPremiumProfile.EffectiveUri != "https://openrouter.ai/api/v1/chat/completions"
            || OpenRouterPremiumProfile.HttpMethod != "POST"
            || OpenRouterPremiumProfile.AuthenticationScheme != "Bearer"
            || OpenRouterPremiumProfile.ModelIdentity != Model
            || OpenRouterPremiumProfile.CanonicalModelSlug != Model
            || OpenRouterPremiumProfile.ModelReleaseDateUtc != "2026-07-09"
            || OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity != DatedRelease
            || OpenRouterPremiumProfile.ProviderSlug != ProviderSlug
            || OpenRouterPremiumProfile.ProviderResponseIdentity != ProviderResponseIdentity
            || OpenRouterPremiumProfile.ContextLengthTokens != 1_050_000
            || OpenRouterPremiumProfile.PromptMicrousdPerMillionTokens != 200_000
            || OpenRouterPremiumProfile.CompletionMicrousdPerMillionTokens != 1_200_000
            || OpenRouterPremiumProfile.PromptUsdPerToken != "0.0000002"
            || OpenRouterPremiumProfile.CompletionUsdPerToken != "0.0000012"
            || OpenRouterPremiumProfile.PricingOverrideMinimumPromptTokens != 272_000
            || OpenRouterPremiumProfile.PricingOverridePromptUsdPerToken != "0.0000004"
            || OpenRouterPremiumProfile.PricingOverrideCompletionUsdPerToken != "0.0000018"
            || OpenRouterPremiumProfile.ProviderMaxPromptUsdPerMillionTokens != ProviderMaxPromptUsdPerMillionTokens
            || OpenRouterPremiumProfile.ProviderMaxCompletionUsdPerMillionTokens != ProviderMaxCompletionUsdPerMillionTokens
            || OpenRouterPremiumProfile.PromptRevision != "prompt-v1"
            || OpenRouterPremiumProfile.ProposalSchemaVersion != "snow_globe_cognition_quality_proposal_response/v1"
            || OpenRouterPremiumProfile.OutputSchemaIdentity != "snow_globe_openrouter_premium_json_schema/v1"
            || OpenRouterPremiumProfile.LiveTrafficBlockerCode != "paid_cost_ceiling_and_account_binding_unresolved"
            || bounds.RequiredScenarioCount != RequiredScenarioCount
            || bounds.MaximumExchanges != MaximumExchanges
            || bounds.MaximumRequestBytes != MaximumRequestBytes
            || bounds.MaximumResponseBytes != MaximumResponseBytes
            || bounds.MaximumAggregateRequestBytes != MaximumAggregateRequestBytes
            || bounds.MaximumAggregateResponseBytes != MaximumAggregateResponseBytes
            || bounds.MaximumResponseHeaderBytes != MaximumResponseHeaderBytes
            || bounds.MaximumJsonDepth != MaximumJsonDepth
            || bounds.MaximumJsonTokens != MaximumJsonTokens
            || bounds.MaximumStringCharacters != MaximumStringCharacters
            || bounds.MaximumArrayItems != MaximumArrayItems
            || bounds.MaximumInputTokens != MaximumInputTokens
            || bounds.MaximumOutputTokens != MaximumOutputTokens
            || bounds.TimeoutMilliseconds != AggregateWindowMilliseconds
            || bounds.CredentialLeaseLifetimeMilliseconds != CredentialLeaseLifetimeMilliseconds
            || bounds.PerSlotCostCeilingMicrousd != PerSlotCostCeilingMicrousd
            || bounds.AggregateCostCeilingMicrousd != AggregateCostCeilingMicrousd
            || !profile.SupportsResponseFormat
            || !profile.RequiresStructuredOutputs
            || !profile.SupportsMaximumTokens
            || profile.TemperatureAdvertised
            || !profile.ZdrRequested
            || profile.CanonicalModelCallabilityProven
            || profile.LiveTrafficEnabled)
            throw new InvalidDataException("OpenRouter paid-run frozen profile is invalid.");
    }
}

internal sealed record OpenRouterPremiumPaidRunBehaviorEvidence(
    int SuccessExchangeCalls,
    int SuccessLeaseCalls,
    int SuccessMaximumConcurrentCalls,
    int UncertainExchangeCalls,
    int UncertainLeaseCalls,
    string UncertainStatus,
    SubmissionState UncertainSubmissionState,
    ChargeState UncertainChargeState,
    int PreDispatchTerminalExchangeCalls,
    int PreDispatchTerminalLeaseCalls,
    string PreDispatchTerminalStatus,
    SubmissionState PreDispatchTerminalSubmissionState,
    ChargeState PreDispatchTerminalChargeState,
    string ReuseFailureCode,
    int ReuseExchangeCalls,
    int ReuseLeaseCalls,
    string DuplicateNonceFailureCode,
    int DuplicateNonceExchangeCalls,
    int DuplicateNonceLeaseCalls,
    IReadOnlyList<bool> SuccessLeaseZeroObservations,
    IReadOnlyList<bool> UncertainLeaseZeroObservations,
    IReadOnlyList<bool> PreDispatchTerminalLeaseZeroObservations)
{
    internal int ObservedAttemptsPerTerminalSlot => UncertainExchangeCalls;
    internal int ObservedRetryCallsAfterTerminal => Math.Max(0, UncertainExchangeCalls - 1);
    internal bool StoppedAfterTerminalOrUncertainOutcome =>
        UncertainStatus == "terminal"
        && UncertainExchangeCalls == 1
        && UncertainLeaseCalls == 1
        && UncertainSubmissionState == SubmissionState.SubmissionUnknown
        && UncertainChargeState == ChargeState.Unknown
        && PreDispatchTerminalStatus == "terminal"
        && PreDispatchTerminalExchangeCalls == 0
        && PreDispatchTerminalLeaseCalls == 1
        && PreDispatchTerminalSubmissionState == SubmissionState.DefinitelyNotSubmitted
        && PreDispatchTerminalChargeState == ChargeState.Released;
    internal bool FreshExplicitAuthorizationRequired =>
        ReuseFailureCode == "capability_consumed" && ReuseExchangeCalls == 0 && ReuseLeaseCalls == 0
        && DuplicateNonceFailureCode == "authorization_nonce_consumed"
        && DuplicateNonceExchangeCalls == 0 && DuplicateNonceLeaseCalls == 0;
    internal bool AllLeaseBuffersZeroed =>
        HasExactZeroObservations(SuccessLeaseZeroObservations, SuccessLeaseCalls,
            OpenRouterPremiumPaidRunFrozenContract.RequiredScenarioCount)
        && HasExactZeroObservations(UncertainLeaseZeroObservations, UncertainLeaseCalls, 1)
        && HasExactZeroObservations(PreDispatchTerminalLeaseZeroObservations, PreDispatchTerminalLeaseCalls, 1);

    private static bool HasExactZeroObservations(
        IReadOnlyList<bool>? observations,
        int observedLeaseCalls,
        int expectedObservations) =>
        observations is not null
        && observedLeaseCalls == expectedObservations
        && observations.Count == expectedObservations
        && observations.All(static zeroed => zeroed);
}

/// <summary>
/// Executes the real in-memory evidence module once per behavior case with registered, zero-I/O
/// fake exchange, lease, clock, and journal seams. The resulting facts are cached because the
/// nonce registry is deliberately single-use and bounded.
/// </summary>
internal static class OpenRouterPremiumPaidRunBehaviorConformance
{
    private static long _nonceSequence;
    private static readonly Lazy<OpenRouterPremiumPaidRunBehaviorEvidence> Verified =
        new(Verify, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static OpenRouterPremiumPaidRunBehaviorEvidence Evidence => Verified.Value;

    private static OpenRouterPremiumPaidRunBehaviorEvidence Verify()
    {
        ProbeContext success = CreateContext(failingSlot: null);
        OpenRouterPremiumExecutionCapability successCapability = OpenRouterPremiumEvidenceModule.Authorize(success.Authorization);
        OpenRouterPremiumExecutionCapability duplicateNonceCapability = OpenRouterPremiumEvidenceModule.Authorize(success.Authorization);
        OpenRouterPremiumEvidenceArtifact successArtifact = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            successCapability, success.Exchange, success.Leases, success.Journal, success.Clock, CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();
        int exchangeCallsBeforeReuse = success.Exchange.CallCount;
        int leaseCallsBeforeReuse = success.Leases.CallCount;
        string reuseFailureCode;
        try
        {
            _ = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
                successCapability, success.Exchange, success.Leases, success.Journal, success.Clock, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
            throw new InvalidDataException("OpenRouter paid-run executor reuse unexpectedly succeeded.");
        }
        catch (OpenRouterPremiumEvidenceException exception) { reuseFailureCode = exception.Code; }
        string duplicateNonceFailureCode;
        try
        {
            _ = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
                duplicateNonceCapability, success.Exchange, success.Leases, success.Journal, success.Clock, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
            throw new InvalidDataException("OpenRouter paid-run duplicate nonce unexpectedly succeeded.");
        }
        catch (OpenRouterPremiumEvidenceException exception) { duplicateNonceFailureCode = exception.Code; }

        ProbeContext uncertain = CreateContext(failingSlot: 1);
        OpenRouterPremiumEvidenceArtifact uncertainArtifact = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            OpenRouterPremiumEvidenceModule.Authorize(uncertain.Authorization), uncertain.Exchange, uncertain.Leases,
            uncertain.Journal, uncertain.Clock, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        OpenRouterPremiumSlotReceipt uncertainReceipt = uncertainArtifact.Slots.Single();
        ProbeContext preDispatchTerminal = CreateContext(failingSlot: null, OfflineCredentialLeaseBehavior.AcquireException);
        OpenRouterPremiumEvidenceArtifact preDispatchTerminalArtifact = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            OpenRouterPremiumEvidenceModule.Authorize(preDispatchTerminal.Authorization), preDispatchTerminal.Exchange,
            preDispatchTerminal.Leases, preDispatchTerminal.Journal, preDispatchTerminal.Clock, CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();
        OpenRouterPremiumSlotReceipt preDispatchTerminalReceipt = preDispatchTerminalArtifact.Slots.Single();
        OpenRouterPremiumPaidRunBehaviorEvidence evidence = new(
            success.Exchange.CallCount,
            success.Leases.CallCount,
            success.Exchange.MaximumConcurrentCalls,
            uncertain.Exchange.CallCount,
            uncertain.Leases.CallCount,
            uncertainArtifact.Status,
            uncertainReceipt.SubmissionState,
            uncertainReceipt.ChargeState,
            preDispatchTerminal.Exchange.CallCount,
            preDispatchTerminal.Leases.CallCount,
            preDispatchTerminalArtifact.Status,
            preDispatchTerminalReceipt.SubmissionState,
            preDispatchTerminalReceipt.ChargeState,
            reuseFailureCode,
            success.Exchange.CallCount - exchangeCallsBeforeReuse,
            success.Leases.CallCount - leaseCallsBeforeReuse,
            duplicateNonceFailureCode,
            success.Exchange.CallCount - exchangeCallsBeforeReuse,
            success.Leases.CallCount - leaseCallsBeforeReuse,
            success.Leases.LeaseZeroObservations,
            uncertain.Leases.LeaseZeroObservations,
            preDispatchTerminal.Leases.LeaseZeroObservations);
        if (successArtifact.Status != "complete"
            || successArtifact.ExchangeCount != OpenRouterPremiumPaidRunFrozenContract.RequiredScenarioCount
            || evidence.SuccessExchangeCalls != OpenRouterPremiumPaidRunFrozenContract.RequiredScenarioCount
            || evidence.SuccessLeaseCalls != OpenRouterPremiumPaidRunFrozenContract.RequiredScenarioCount
            || evidence.SuccessMaximumConcurrentCalls != 1
            || !evidence.StoppedAfterTerminalOrUncertainOutcome
            || !evidence.FreshExplicitAuthorizationRequired
            || !evidence.AllLeaseBuffersZeroed)
            throw new InvalidDataException("OpenRouter paid-run executor conformance is invalid.");
        return evidence;
    }

    private static ProbeContext CreateContext(
        int? failingSlot,
        OfflineCredentialLeaseBehavior leaseBehavior = OfflineCredentialLeaseBehavior.Normal)
    {
        long nonce = Interlocked.Increment(ref _nonceSequence);
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        ByokAccountBindingIdentity account = new("byok-account-sha256-" + new string('c', 64));
        string suffix = leaseBehavior == OfflineCredentialLeaseBehavior.AcquireException
            ? $"predispatch-terminal-{nonce}"
            : failingSlot is null ? $"success-{nonce}" : $"uncertain-{nonce}";
        OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
            $"openrouter-premium-journal/readiness-{suffix}", $"openrouter-premium-run/readiness-{suffix}", profile, account);
        InMemoryOpenRouterPremiumJournal journal = new(header);
        FakeCredentialLeaseSource leases = new(behavior: leaseBehavior);
        ScriptedOpenRouterPremiumExchange exchange = ScriptedOpenRouterPremiumExchange.CreateSuccessful(failingSlot);
        OfflineOpenRouterPremiumClock clock = new(1_000);
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
            $"openrouter-premium-authorization/readiness-{suffix}",
            1_000,
            1_000 + OpenRouterPremiumPaidRunFrozenContract.AggregateWindowMilliseconds);
        return new(authorization, exchange, leases, journal, clock);
    }

    private sealed record ProbeContext(
        OpenRouterPremiumAuthorization Authorization,
        ScriptedOpenRouterPremiumExchange Exchange,
        FakeCredentialLeaseSource Leases,
        InMemoryOpenRouterPremiumJournal Journal,
        OfflineOpenRouterPremiumClock Clock);
}

/// <summary>
/// Pure, bounded inspection of the exact canonical request serializer used by the paid runner.
/// It returns policy facts only; prompt bytes are never retained or surfaced.
/// </summary>
internal sealed record OpenRouterPremiumPaidRunRequestContract(
    string Model,
    string ProviderOnly,
    int MaximumOutputTokens,
    string ReasoningEffort,
    bool ReasoningExcluded,
    bool ZdrRequired,
    int AlternateOrFallbackRoutes,
    string RequiredFinishReason)
{
    private const string ExpectedProductionAdapterContractDigestSha256 =
        "ddffd802e6398e3da844b46258c6f3425907e0972d97212088a496635b47a3ff";

    internal static OpenRouterPremiumPaidRunRequestContract Inspect(OpenRouterPremiumProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        RequireTransportPolicy();
        CognitionQualityPromptEnvelopePublication publication =
            CognitionQualityPromptEnvelopeBuilderModule.Create(OpenRouterPremiumProfile.PromptRevision);
        if (publication.Slots.Count != profile.Bounds.RequiredScenarioCount
            || publication.CanonicalDigestSha256 != profile.PromptPublicationDigestSha256
            || publication.PromptSetDigestSha256 != profile.PromptSetDigestSha256)
            throw Invalid();

        OpenRouterPremiumPaidRunRequestContract? expected = null;
        int ordinal = 0;
        foreach (CognitionQualityPromptEnvelopeSlot slot in publication.Slots)
        {
            ordinal++;
            if (slot.ScenarioId != $"cq{ordinal}") throw Invalid();
            byte[] prompt = slot.CopyPromptUtf8();
            byte[]? request = null;
            try
            {
                request = OpenRouterPremiumCanonicalRequestSerializer.Serialize(profile, prompt);
                if (request.Length is < 1 || request.Length > profile.Bounds.MaximumRequestBytes) throw Invalid();
                OpenRouterPremiumPaidRunRequestContract current = Parse(request, prompt, profile);
                if (expected is null) expected = current;
                else if (current != expected) throw Invalid();
            }
            catch (InvalidDataException) { throw; }
            catch { throw Invalid(); }
            finally
            {
                CryptographicOperations.ZeroMemory(prompt);
                if (request is not null) CryptographicOperations.ZeroMemory(request);
            }
        }

        if (expected is null
            || !HasDescriptorLine(OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor,
                $"api_model_id={expected.Model}")
            || !HasDescriptorLine(OpenRouterPremiumProfile.CatalogEvidenceCanonicalDescriptor,
                $"release_revision_path={OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity}"))
            throw Invalid();
        RequireResponsePolicy(profile, publication.Slots[0], expected);
        return expected with { RequiredFinishReason = OpenRouterPremiumPaidRunFrozenContract.RequiredFinishReason };
    }

    private static OpenRouterPremiumPaidRunRequestContract Parse(
        byte[] request,
        byte[] expectedPrompt,
        OpenRouterPremiumProfile profile)
    {
        using JsonDocument document = JsonDocument.Parse(request, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = profile.Bounds.MaximumJsonDepth
        });
        JsonElement root = document.RootElement;
        RequireExact(root, "model", "messages", "max_completion_tokens", "reasoning", "stream", "response_format", "provider");
        string model = String(root, "model", 256);
        int maximumOutputTokens = Integer(root, "max_completion_tokens");
        if (model != OpenRouterPremiumPaidRunFrozenContract.Model
            || maximumOutputTokens != OpenRouterPremiumPaidRunFrozenContract.MaximumOutputTokens
            || Boolean(root, "stream")) throw Invalid();

        JsonElement messages = Property(root, "messages", JsonValueKind.Array);
        if (messages.GetArrayLength() != 1) throw Invalid();
        JsonElement message = messages[0];
        RequireExact(message, "role", "content");
        if (String(message, "role", 16) != "user") throw Invalid();
        string content = String(message, "content", CognitionQualityPromptEnvelopeBuilderModule.MaximumPromptBytes);
        string expectedContent;
        try { expectedContent = new UTF8Encoding(false, true).GetString(expectedPrompt); }
        catch (DecoderFallbackException) { throw Invalid(); }
        if (!string.Equals(content, expectedContent, StringComparison.Ordinal)) throw Invalid();

        JsonElement reasoning = Property(root, "reasoning", JsonValueKind.Object);
        RequireExact(reasoning, "effort", "exclude");
        string effort = String(reasoning, "effort", 32);
        bool exclude = Boolean(reasoning, "exclude");
        if (effort != OpenRouterPremiumPaidRunFrozenContract.RequiredReasoningEffort || !exclude) throw Invalid();

        JsonElement responseFormat = Property(root, "response_format", JsonValueKind.Object);
        RequireExact(responseFormat, "type", "json_schema");
        if (String(responseFormat, "type", 32) != "json_schema") throw Invalid();
        JsonElement jsonSchema = Property(responseFormat, "json_schema", JsonValueKind.Object);
        RequireExact(jsonSchema, "name", "strict", "schema");
        if (String(jsonSchema, "name", 64) != "snow_globe_action_proposal"
            || !Boolean(jsonSchema, "strict")
            || Property(jsonSchema, "schema", JsonValueKind.Object).ValueKind != JsonValueKind.Object)
            throw Invalid();

        JsonElement provider = Property(root, "provider", JsonValueKind.Object);
        RequireExact(provider, "order", "only", "allow_fallbacks", "require_parameters", "data_collection", "zdr", "sort", "max_price");
        string order = SingleString(provider, "order", 128);
        string only = SingleString(provider, "only", 128);
        if (order != OpenRouterPremiumPaidRunFrozenContract.ProviderSlug
            || only != OpenRouterPremiumPaidRunFrozenContract.ProviderSlug
            || order != only || Boolean(provider, "allow_fallbacks") || !Boolean(provider, "require_parameters")
            || String(provider, "data_collection", 16) != "deny" || !Boolean(provider, "zdr")
            || String(provider, "sort", 16) != "price")
            throw Invalid();
        JsonElement maxPrice = Property(provider, "max_price", JsonValueKind.Object);
        RequireExact(maxPrice, "prompt", "completion");
        if (Decimal(maxPrice, "prompt") != OpenRouterPremiumPaidRunFrozenContract.ProviderMaxPromptUsdPerMillionTokens
            || Decimal(maxPrice, "completion") != OpenRouterPremiumPaidRunFrozenContract.ProviderMaxCompletionUsdPerMillionTokens)
            throw Invalid();

        return new(model, only, maximumOutputTokens, effort, exclude, true, 0, string.Empty);
    }

    private static void RequireTransportPolicy()
    {
        const string expectedDescriptor = "openrouter-hardened-http-policy/v1|no-redirect|no-retry-loop|no-proxy|no-cookies|no-ambient-auth|no-preauth|no-decompression|headers-8k|connect-5s|max-connection-1|pool-life-60s|pool-idle-5s|response-drain-0|no-activity-propagation|bounded-body";
        const string expectedDigest = "b654e07b2e7a2e169b0a21ef63b2f07981a03abb09614b6ed73b1e5200cca030";
        if (OpenRouterPremiumHardenedHttp.PolicyDescriptor != expectedDescriptor
            || OpenRouterPremiumHardenedHttp.PolicyDigestSha256 != expectedDigest
            || OpenRouterPremiumCanonical.Digest(expectedDescriptor) != expectedDigest
            || OpenRouterPremiumHttpExchange.AdapterContractDigestSha256
                != ExpectedProductionAdapterContractDigestSha256)
            throw Invalid();
    }

    private static void RequireResponsePolicy(
        OpenRouterPremiumProfile profile,
        CognitionQualityPromptEnvelopeSlot slot,
        OpenRouterPremiumPaidRunRequestContract requestContract)
    {
        byte[] body = WriteResponseProbe(requestContract.Model);
        byte[] invalidFinish = body.ToArray();
        byte[] invalidAttempt = body.ToArray();
        try
        {
            string requestDigest = new('1', 64);
            OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
                body, 200, profile, 1, slot.ScenarioId, slot.PromptDigestSha256, requestDigest);
            if (receipt.OutcomeCode != "premium_evidence_success") throw Invalid();
            ReplaceAsciiExact(invalidFinish, "\"finish_reason\":\"stop\"", "\"finish_reason\":\"tool\"");
            RequireParserRejection(invalidFinish, profile, slot, requestDigest, "response_finish_reason_not_stop");
            ReplaceAsciiExact(invalidAttempt, "\"attempt\":1", "\"attempt\":2");
            RequireParserRejection(invalidAttempt, profile, slot, requestDigest, "response_routing_invalid");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(body);
            CryptographicOperations.ZeroMemory(invalidFinish);
            CryptographicOperations.ZeroMemory(invalidAttempt);
        }
    }

    private static byte[] WriteResponseProbe(string model)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", "readiness-contract-probe"); writer.WriteString("object", "chat.completion");
            writer.WriteNumber("created", 1); writer.WriteString("model", model);
            writer.WritePropertyName("choices"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteNumber("index", 0); writer.WriteString("finish_reason", "stop");
            writer.WritePropertyName("message"); writer.WriteStartObject(); writer.WriteString("role", "assistant");
            writer.WriteString("content", "{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":12}");
            writer.WriteEndObject(); writer.WriteEndObject(); writer.WriteEndArray();
            writer.WritePropertyName("usage"); writer.WriteStartObject(); writer.WriteNumber("prompt_tokens", 1);
            writer.WriteNumber("completion_tokens", 1); writer.WriteNumber("total_tokens", 2);
            writer.WriteNumber("cost", 0.0000014m); writer.WriteEndObject();
            writer.WritePropertyName("openrouter_metadata"); writer.WriteStartObject(); writer.WriteString("requested", model);
            writer.WriteString("strategy", "direct"); writer.WriteNumber("attempt", 1); writer.WriteBoolean("is_byok", false);
            writer.WritePropertyName("endpoints"); writer.WriteStartObject(); writer.WriteNumber("total", 1);
            writer.WritePropertyName("available"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("provider", OpenRouterPremiumPaidRunFrozenContract.ProviderResponseIdentity); writer.WriteString("model", model);
            writer.WriteBoolean("selected", true); writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
            writer.WritePropertyName("attempts"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("provider", OpenRouterPremiumPaidRunFrozenContract.ProviderResponseIdentity); writer.WriteString("model", model);
            writer.WriteNumber("status", 200); writer.WriteEndObject(); writer.WriteEndArray();
            writer.WritePropertyName("pipeline"); writer.WriteStartArray(); writer.WriteEndArray();
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void RequireParserRejection(
        byte[] body,
        OpenRouterPremiumProfile profile,
        CognitionQualityPromptEnvelopeSlot slot,
        string requestDigest,
        string expectedCode)
    {
        try
        {
            _ = OpenRouterPremiumResponseParser.Parse(
                body, 200, profile, 1, slot.ScenarioId, slot.PromptDigestSha256, requestDigest);
            throw Invalid();
        }
        catch (OpenRouterPremiumEvidenceException exception) when (exception.Code == expectedCode) { }
    }

    private static void ReplaceAsciiExact(byte[] buffer, string oldValue, string newValue)
    {
        byte[] oldBytes = Encoding.ASCII.GetBytes(oldValue);
        byte[] newBytes = Encoding.ASCII.GetBytes(newValue);
        try
        {
            if (oldBytes.Length != newBytes.Length) throw Invalid();
            int index = buffer.AsSpan().IndexOf(oldBytes);
            if (index < 0 || buffer.AsSpan(index + oldBytes.Length).IndexOf(oldBytes) >= 0) throw Invalid();
            newBytes.CopyTo(buffer, index);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldBytes);
            CryptographicOperations.ZeroMemory(newBytes);
        }
    }

    private static bool HasDescriptorLine(string descriptor, string required) =>
        descriptor.Split('\n', StringSplitOptions.None).Contains(required, StringComparer.Ordinal);

    private static void RequireExact(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal))
            throw Invalid();
    }

    private static JsonElement Property(JsonElement value, string name, JsonValueKind kind)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != kind) throw Invalid();
        return property;
    }

    private static string String(JsonElement value, string name, int maximumCharacters)
    {
        JsonElement property = Property(value, name, JsonValueKind.String);
        string? result = property.GetString();
        if (result is null || result.Length is < 1 || result.Length > maximumCharacters) throw Invalid();
        return result;
    }

    private static bool Boolean(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
        return property.GetBoolean();
    }

    private static int Integer(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetInt32(out int result)) throw Invalid();
        return result;
    }

    private static decimal Decimal(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetDecimal(out decimal result)) throw Invalid();
        return result;
    }

    private static string SingleString(JsonElement value, string name, int maximumCharacters)
    {
        JsonElement array = Property(value, name, JsonValueKind.Array);
        if (array.GetArrayLength() != 1 || array[0].ValueKind != JsonValueKind.String) throw Invalid();
        string? result = array[0].GetString();
        if (result is null || result.Length is < 1 || result.Length > maximumCharacters) throw Invalid();
        return result;
    }

    private static InvalidDataException Invalid() =>
        new("OpenRouter paid-run readiness contract is invalid.");
}
