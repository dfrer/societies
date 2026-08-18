using System.Reflection;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class TwoTierCognitionTests
{
    private const string ModelRevisionIdentity = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherModelRevisionIdentity = "sha256-abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void Policy_IsStableFieldBoundAndRevisionIsStructurallyPinned()
    {
        ModelPolicySnapshot policy = Policy();
        policy.Validate();
        Assert.Equal(policy.Digest, Policy().Digest);
        object[] mutations =
        {
            policy with { PolicyId = "policy-v2" },
            policy with { ProviderHost = "other.example" },
            policy with { Route = "v2/propose" },
            policy with { PremiumModelIdentity = "qwen3.8-32b" },
            policy with { PremiumModelRevisionIdentity = OtherModelRevisionIdentity },
            policy with { PromptRevision = "prompt-v2" },
            policy with { ProposalSchemaVersion = "proposal-v2" },
            policy with { LocalAdapterIdentity = "test_local/v2" },
            policy with { Currency = "cad" },
            policy with { InputMicrousdPerMillionTokens = 2_000_000 },
            policy with { OutputMicrousdPerMillionTokens = 2_000_000 },
            policy with { MaximumInputTokens = 11 },
            policy with { MaximumOutputTokens = 11 },
            policy with { CostCeilingMicrousd = 21 },
            policy with { TimeoutMilliseconds = 1001 },
            policy with { RedirectsAllowed = true },
            policy with { AutomaticRetriesAllowed = true }
        };
        Assert.All(mutations.Cast<ModelPolicySnapshot>(), mutation => Assert.NotEqual(policy.Digest, mutation.Digest));
        Assert.Throws<ArgumentException>(() => (policy with { PremiumModelRevisionIdentity = "latest" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { PremiumModelRevisionIdentity = "sha256-0123" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { PremiumModelRevisionIdentity = ModelRevisionIdentity.ToUpperInvariant() }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { Route = "models/*" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { ProviderHost = "premium.example/v1" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { LocalAdapterIdentity = "TEST_LOCAL/v1" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { RedirectsAllowed = true }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { Currency = "cad" }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { CostCeilingMicrousd = 1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (policy with { InputMicrousdPerMillionTokens = long.MaxValue, MaximumInputTokens = int.MaxValue, CostCeilingMicrousd = long.MaxValue }).Validate());
    }

    [Fact]
    public void MalformedOrFloatingRevision_CannotReachPremiumProvider()
    {
        FakePremiumCognitionProvider premium = SuccessProvider();
        foreach (string revision in new[] { "", "latest", "qwen3.8-27b-r1", "sha256-0123", ModelRevisionIdentity.ToUpperInvariant() })
        {
            Assert.Throws<ArgumentException>(() => new SnowGlobeTwoTierCognitionModule(
                CognitionLane.Premium,
                "financial-run-v1",
                Policy() with { PremiumModelRevisionIdentity = revision },
                new CountingAdapter(Proposal()),
                new(),
                premium));
        }
        Assert.Equal(0, premium.SubmissionCount);
    }

    [Fact]
    public void AdapterIdentity_BindsPolicyLaneAndFinancialJournalIdentity()
    {
        CountingAdapter local = new(Proposal());
        SnowGlobeTwoTierCognitionModule baseline = new(CognitionLane.Premium, "financial-run-v1", Policy(), local, new(), SuccessProvider());
        SnowGlobeTwoTierCognitionModule changedRevision = new(CognitionLane.Premium, "financial-run-v1", Policy() with { PremiumModelRevisionIdentity = OtherModelRevisionIdentity }, local, new(), SuccessProvider());
        SnowGlobeTwoTierCognitionModule changedLane = new(CognitionLane.Local, "financial-run-v1", Policy(), local, new());
        SnowGlobeTwoTierCognitionModule changedJournal = new(CognitionLane.Premium, "financial-run-v2", Policy(), local, new(), SuccessProvider());

        Assert.NotEqual(baseline.AdapterIdentity, changedRevision.AdapterIdentity);
        Assert.NotEqual(baseline.AdapterIdentity, changedLane.AdapterIdentity);
        Assert.NotEqual(baseline.AdapterIdentity, changedJournal.AdapterIdentity);
    }

    [Fact]
    public async Task LocalLane_UsesOnlyLocalAndRecordsDetachedReceipt()
    {
        CountingAdapter local = new(Proposal());
        FakePremiumCognitionProvider premium = SuccessProvider();
        InMemoryCognitionJobJournal journal = new();
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Local, local, journal, premium);

        SnowGlobeActionProposal outcome = await module.ProposeAsync(Observation(), default);

        Assert.Equal(Proposal(), outcome);
        Assert.Equal(1, local.Calls);
        Assert.Equal(0, premium.SubmissionCount);
        Assert.Empty(module.SnapshotPremiumJobs());
        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
        Assert.Equal(ChargeState.NotApplicable, receipt.ChargeState);
        Assert.Equal("local_success", receipt.ReasonCode);
        Assert.Equal("local_success", receipt.PrimaryOutcomeCode);
        Assert.Equal(Policy().Digest, receipt.PolicyDigest);
        Assert.Equal("financial-run-v1", receipt.FinancialJournalIdentity);
        Assert.Equal(CognitionLane.Local, receipt.RequestedLane);
        Assert.Equal(ModelRevisionIdentity, receipt.PremiumModelRevisionIdentity);
        SnowGlobeActionProposal mutated = receipt.Proposal with { Quantity = 99 };
        Assert.NotEqual(mutated, Assert.Single(module.SnapshotReceipts()).Proposal);
    }

    [Fact]
    public async Task PremiumSuccess_SettlesExactCheckedCostAndDoesNotInvokeFallback()
    {
        CountingAdapter local = new(Proposal());
        FakePremiumCognitionProvider premium = SuccessProvider(3, 2);
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), premium);

        SnowGlobeActionProposal proposal = await module.ProposeAsync(Observation(), default);
        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());

        Assert.Equal(Proposal(), proposal);
        Assert.Equal(1, premium.SubmissionCount);
        Assert.Equal(0, local.Calls);
        Assert.Equal(SubmissionState.ResponseReceived, receipt.SubmissionState);
        Assert.Equal(ChargeState.Settled, receipt.ChargeState);
        Assert.Equal(5, receipt.Reservation.SettledMicrousd);
        Assert.Equal(Policy().CostCeilingMicrousd, receipt.Reservation.ReservedMicrousd);
        Assert.Equal(new[] { "reserved", "submission_unknown" }, module.SnapshotTrace().Take(2));
        Assert.Contains(Policy().Digest, module.AdapterIdentity, StringComparison.Ordinal);
        PremiumCognitionJob job = Assert.Single(module.SnapshotPremiumJobs());
        Assert.Equal(Policy().Digest, job.PolicyDigest);
        Assert.Equal(Policy().PremiumModelIdentity, job.PremiumModelIdentity);
        Assert.Equal(ModelRevisionIdentity, job.PremiumModelRevisionIdentity);
        Assert.Equal(receipt.JobDigest, job.JobDigest);
        Assert.Equal(receipt.PolicyDigest, job.PolicyDigest);
    }

    [Fact]
    public async Task DuplicateAndConcurrentCalls_ReplayOneExactReceiptAndSubmitOnce()
    {
        CountingAdapter local = new(Proposal());
        GateProvider premium = new(SuccessResult());
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), premium);
        Task<SnowGlobeActionProposal> first = module.ProposeAsync(Observation(), default).AsTask();
        await premium.Started.Task;
        Task<SnowGlobeActionProposal> second = module.ProposeAsync(Observation(), default).AsTask();
        premium.Release();

        SnowGlobeActionProposal[] results = await Task.WhenAll(first, second);
        SnowGlobeActionProposal third = await module.ProposeAsync(Observation(), default);

        Assert.All(results, result => Assert.Equal(Proposal(), result));
        Assert.Equal(Proposal(), third);
        Assert.Equal(1, premium.Calls);
        Assert.Equal(0, local.Calls);
        Assert.Single(module.SnapshotReceipts());
    }

    [Fact]
    public async Task KeyConflict_RejectsWithoutSecondProviderCall()
    {
        FakePremiumCognitionProvider premium = SuccessProvider();
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, new CountingAdapter(Proposal()), new(), premium);
        await module.ProposeAsync(Observation(), default);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await module.ProposeAsync(Observation() with { AvailableWood = 99 }, default));
        Assert.Equal(1, premium.SubmissionCount);
    }

    [Theory]
    [InlineData(SubmissionState.DefinitelyNotSubmitted, PremiumResponseStatus.Rejected, ChargeState.Released, "provider_rejected")]
    [InlineData(SubmissionState.SubmissionUnknown, PremiumResponseStatus.TimedOut, ChargeState.Unknown, "provider_timeout")]
    public async Task UnusablePremiumResponse_UsesOneFallbackAndClassifiesConservatively(SubmissionState submission, PremiumResponseStatus status, ChargeState expectedCharge, string expectedPrimaryOutcome)
    {
        CountingAdapter local = new(Proposal());
        FakePremiumCognitionProvider premium = new(_ => new PremiumCognitionProviderResult(submission, status, "premium.example", "v1/propose", "qwen3.8-27b", ModelRevisionIdentity, false, 0, 0, null, "raw secret must not persist"));
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), premium);

        SnowGlobeActionProposal result = await module.ProposeAsync(Observation(), default);
        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
        await module.ProposeAsync(Observation(), default);

        Assert.Equal(Proposal(), result);
        Assert.Equal(expectedCharge, receipt.ChargeState);
        Assert.Equal("local_fallback", receipt.ReasonCode);
        Assert.Equal(expectedPrimaryOutcome, receipt.PrimaryOutcomeCode);
        Assert.Equal(Policy().CostCeilingMicrousd, receipt.Reservation.ReservedMicrousd);
        Assert.Equal(1, premium.SubmissionCount);
        Assert.Equal(1, local.Calls);
        Assert.DoesNotContain("secret", receipt.ReasonCode, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", receipt.PrimaryOutcomeCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExceptionOrInvalidResponse_LeavesReservationUnknownAndFallsBackOnce()
    {
        CountingAdapter local = new(Proposal());
        FakePremiumCognitionProvider crashing = new(_ => throw new InvalidOperationException("raw provider credential text"));
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), crashing);
        await module.ProposeAsync(Observation(), default);
        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
        await module.ProposeAsync(Observation(), default);
        Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
        Assert.Equal(1, crashing.SubmissionCount);
        Assert.Equal(1, local.Calls);

        FakePremiumCognitionProvider invalid = new(_ => SuccessResult() with { Redirected = true });
        SnowGlobeTwoTierCognitionModule invalidModule = Module(CognitionLane.Premium, new CountingAdapter(Proposal()), new(), invalid);
        await invalidModule.ProposeAsync(Observation(), default);
        Assert.Equal(ChargeState.Unknown, Assert.Single(invalidModule.SnapshotReceipts()).ChargeState);
    }

    [Fact]
    public async Task NullOrContradictoryDefinitelyNotSubmittedResult_HoldsFullReservationUnknown()
    {
        foreach (IPremiumCognitionProvider premium in new IPremiumCognitionProvider[]
                 {
                     new NullProvider(),
                     new FakePremiumCognitionProvider(_ => SuccessResult() with
                     {
                         SubmissionState = SubmissionState.DefinitelyNotSubmitted,
                         Status = PremiumResponseStatus.Success
                     }),
                     new FakePremiumCognitionProvider(_ => SuccessResult(0, 0) with
                     {
                         SubmissionState = SubmissionState.DefinitelyNotSubmitted,
                         Status = PremiumResponseStatus.Rejected,
                         Proposal = null,
                         EffectiveHost = "other.example"
                     })
                 })
        {
            CountingAdapter local = new(Proposal());
            SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), premium);

            Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));
            InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
            Assert.Equal(SubmissionState.SubmissionUnknown, receipt.SubmissionState);
            Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
            Assert.Equal(Policy().CostCeilingMicrousd, receipt.Reservation.ReservedMicrousd);
            Assert.Equal(1, local.Calls);
        }
    }

    [Fact]
    public async Task ConfiguredTimeout_CompletesUnknownReceiptAndReplayNeverResubmits()
    {
        SlowIgnoringCancellationProvider premium = new(TimeSpan.FromMilliseconds(150));
        CountingAdapter local = new(Proposal());
        ModelPolicySnapshot policy = Policy() with { TimeoutMilliseconds = 20 };
        SnowGlobeTwoTierCognitionModule module = new(CognitionLane.Premium, "financial-run-v1", policy, local, new(), premium);

        Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));
        Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));

        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
        Assert.Equal(SubmissionState.SubmissionUnknown, receipt.SubmissionState);
        Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
        Assert.Equal(policy.CostCeilingMicrousd, receipt.Reservation.ReservedMicrousd);
        Assert.Equal(1, premium.Calls);
        Assert.Equal(1, local.Calls);
    }

    [Fact]
    public async Task MidFlightCallerCancellation_TerminalizesUnknownAndDuplicateDoesNotResubmit()
    {
        GateProvider premium = new(SuccessResult());
        CountingAdapter local = new(Proposal());
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), premium);
        using CancellationTokenSource cancellation = new();

        Task<SnowGlobeActionProposal> first = module.ProposeAsync(Observation(), cancellation.Token).AsTask();
        await premium.Started.Task;
        cancellation.Cancel();

        Assert.Equal(Proposal(), await first);
        Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));
        InferenceReceipt receipt = Assert.Single(module.SnapshotReceipts());
        Assert.Equal(SubmissionState.SubmissionUnknown, receipt.SubmissionState);
        Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
        Assert.Equal(Policy().CostCeilingMicrousd, receipt.Reservation.ReservedMicrousd);
        Assert.Equal("provider_unknown", receipt.PrimaryOutcomeCode);
        Assert.Equal(1, premium.Calls);
        Assert.Equal(1, local.Calls);
        premium.Release();
    }

    [Fact]
    public async Task CompletedPremiumWork_FreesConcurrentCapacityForANewJob()
    {
        FakePremiumCognitionProvider premium = SuccessProvider();
        CountingAdapter local = new(Proposal());
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new InMemoryCognitionJobJournal(1), premium);

        await module.ProposeAsync(Observation(), default);
        await module.ProposeAsync(Observation() with { Tick = 1 }, default);

        Assert.Equal(2, premium.SubmissionCount);
        Assert.Equal(0, local.Calls);
        Assert.Equal(2, module.SnapshotReceipts().Count);
    }

    [Fact]
    public async Task ActiveCapacityDenial_CompletesThenCapacityIsReusable()
    {
        GateProvider premium = new(SuccessResult());
        CountingAdapter local = new(Proposal());
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new InMemoryCognitionJobJournal(1), premium);

        Task<SnowGlobeActionProposal> first = module.ProposeAsync(Observation(), default).AsTask();
        await premium.Started.Task;
        Assert.Equal(Proposal(), await module.ProposeAsync(Observation() with { Tick = 1 }, default));
        InferenceReceipt denied = Assert.Single(module.SnapshotReceipts());
        Assert.Equal("capacity_denied", denied.PrimaryOutcomeCode);
        Assert.Equal(1, premium.Calls);

        premium.Release();
        Assert.Equal(Proposal(), await first);
        Assert.Equal(Proposal(), await module.ProposeAsync(Observation() with { Tick = 2 }, default));
        Assert.Equal(2, premium.Calls);
        Assert.Equal(1, local.Calls);
        Assert.Equal(3, module.SnapshotReceipts().Count);
    }

    [Fact]
    public async Task InvalidLaneObservationOrPreDispatchCancellation_RejectsBeforeSideEffects()
    {
        FakePremiumCognitionProvider premium = SuccessProvider();
        InMemoryCognitionJobJournal journal = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => Module((CognitionLane)99, new CountingAdapter(Proposal()), journal, premium));

        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, new CountingAdapter(Proposal()), journal, premium);
        await Assert.ThrowsAsync<ArgumentException>(async () => await module.ProposeAsync(Observation() with { AgentId = "agent|00" }, default));
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await module.ProposeAsync(Observation(), cancelled.Token));

        Assert.Equal(0, premium.SubmissionCount);
        Assert.Empty(module.SnapshotReceipts());
        Assert.Empty(module.SnapshotPremiumJobs());
    }

    [Fact]
    public async Task InvalidIdentityUsageOrProposal_IsRejectedBeforeWorldAndFallsBack()
    {
        foreach (PremiumCognitionProviderResult result in new[]
                 {
                     SuccessResult() with { EffectiveHost = "other.example" },
                     SuccessResult() with { InputTokens = 999 },
                     SuccessResult() with { Proposal = new SnowGlobeActionProposal("wrong-agent", SnowGlobeActionKind.GatherWood, 1) },
                     SuccessResult() with { Proposal = new SnowGlobeActionProposal("agent-00", (SnowGlobeActionKind)999, 1) }
                 })
        {
            CountingAdapter local = new(Proposal());
            SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, new(), new FakePremiumCognitionProvider(_ => result));
            Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));
            Assert.Equal(ChargeState.Unknown, Assert.Single(module.SnapshotReceipts()).ChargeState);
            Assert.Equal(1, local.Calls);
        }
    }

    [Fact]
    public async Task LocalFailureAndCapacityDenial_ReturnDeterministicIdleWithoutProvider()
    {
        CountingAdapter badLocal = new(new SnowGlobeActionProposal("other", SnowGlobeActionKind.GatherWood, 1));
        SnowGlobeTwoTierCognitionModule localModule = Module(CognitionLane.Local, badLocal, new(), SuccessProvider());
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), await localModule.ProposeAsync(Observation(), default));

        FakePremiumCognitionProvider premium = SuccessProvider();
        SnowGlobeTwoTierCognitionModule premiumModule = Module(CognitionLane.Premium, new CountingAdapter(new SnowGlobeActionProposal("other", SnowGlobeActionKind.GatherWood, 1)), new InMemoryCognitionJobJournal(0), premium);
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), await premiumModule.ProposeAsync(Observation(), default));
        Assert.Equal(0, premium.SubmissionCount);
        InferenceReceipt receipt = Assert.Single(premiumModule.SnapshotReceipts());
        Assert.Equal(ChargeState.NotApplicable, receipt.ChargeState);
        Assert.Equal("deterministic_idle", receipt.ReasonCode);
    }

    [Fact]
    public void PublicValueDtos_DoNotExposeTransportSecretsOrRuntimeSelectors()
    {
        Type[] dtoTypes = { typeof(PremiumCognitionJob), typeof(InferenceReceipt) };
        string[] forbidden = { "prompt", "uri", "url", "header", "credential", "secret", "provider", "price", "retry", "error" };
        foreach (PropertyInfo property in dtoTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
            Assert.DoesNotContain(forbidden, forbiddenName => property.Name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(SnowGlobeTwoTierCognitionModule).GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field => field.FieldType == typeof(SnowGlobeWorld));
    }

    [Fact]
    public async Task ExistingScheduler_RemainsTheStateChangeAuthority()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, 1);
        FakePremiumCognitionProvider premium = new(_ => SuccessResult() with { Proposal = new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.BuildShelter, 0) });
        SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, new CountingAdapter(Proposal()), new(), premium);
        SnowGlobeRunResult result = await new SequentialInferenceScheduler(module).RunAsync(world, 1);
        Assert.Equal(1, result.Metrics.RejectedActions);
        Assert.Empty(world.Events);
    }

    [Fact]
    public void TwoTierSlice_DoesNotAlterTheVersionThreeRunStoreLedgerKinds()
    {
        Assert.Equal(new[]
        {
            SnowGlobeLedgerKind.Response,
            SnowGlobeLedgerKind.Proposal,
            SnowGlobeLedgerKind.Commit,
            SnowGlobeLedgerKind.Event,
            SnowGlobeLedgerKind.Checkpoint,
            SnowGlobeLedgerKind.ParticipantEvaluation
        }, Enum.GetValues<SnowGlobeLedgerKind>());
    }

    [Fact]
    public async Task PersistedTwoTierRun_ReconstructsAndRecordedResponseReplayDoesNoCognitionOrFinancialWork()
    {
        string root = Path.Combine(Path.GetTempPath(), "snow-globe-two-tier-" + Guid.NewGuid().ToString("N"));
        try
        {
            FakePremiumCognitionProvider premium = SuccessProvider();
            CountingAdapter local = new(Proposal());
            InMemoryCognitionJobJournal journal = new();
            SnowGlobeTwoTierCognitionModule module = Module(CognitionLane.Premium, local, journal, premium);
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(module.AdapterIdentity, agentCount: 1);
            SnowGlobePersistedRunResult persisted;
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                persisted = await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), module, store, 2);

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            int submissionsBeforeReplay = premium.SubmissionCount;
            int localCallsBeforeReplay = local.Calls;
            int receiptsBeforeReplay = journal.SnapshotReceipts().Count;
            int traceBeforeReplay = journal.SnapshotTrace().Count;
            SnowGlobeWorld reconstructed = SnowGlobePersistedRun.Reconstruct(ledger, identity).World;
            SnowGlobeWorld replayWorld = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            SnowGlobeRunResult replay = await new SequentialInferenceScheduler(new SnowGlobeReplayAdapter(ledger, module.AdapterIdentity)).RunAsync(replayWorld, 2);

            Assert.Equal(persisted.Result.StateDigest, reconstructed.StateDigest());
            Assert.Equal(persisted.Result.EventDigest, reconstructed.EventDigest());
            Assert.Equal(persisted.Result.StateDigest, replay.StateDigest);
            Assert.Equal(persisted.Result.EventDigest, replay.EventDigest);
            Assert.Equal(submissionsBeforeReplay, premium.SubmissionCount);
            Assert.Equal(localCallsBeforeReplay, local.Calls);
            Assert.Equal(receiptsBeforeReplay, journal.SnapshotReceipts().Count);
            Assert.Equal(traceBeforeReplay, journal.SnapshotTrace().Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ModelPolicySnapshot Policy() => new("policy-v1", "premium.example", "v1/propose", "qwen3.8-27b", ModelRevisionIdentity, "prompt-v1", "proposal-v1", "test_local/v1", "usd", 1_000_000, 1_000_000, 10, 10, 20, 1000);
    private static SnowGlobeObservation Observation() => new("agent-00", 0, 0, 10, 10, 0, 0, 0, 0);
    private static SnowGlobeActionProposal Proposal() => new("agent-00", SnowGlobeActionKind.GatherWood, 1);
    private static SnowGlobeTwoTierCognitionModule Module(CognitionLane lane, ISnowGlobeIdentifiedInferenceAdapter local, InMemoryCognitionJobJournal journal, IPremiumCognitionProvider premium) => new(lane, "financial-run-v1", Policy(), local, journal, premium);
    private static FakePremiumCognitionProvider SuccessProvider(int input = 1, int output = 1) => new(_ => SuccessResult(input, output));
    private static PremiumCognitionProviderResult SuccessResult(int input = 1, int output = 1) => new(SubmissionState.ResponseReceived, PremiumResponseStatus.Success, "premium.example", "v1/propose", "qwen3.8-27b", ModelRevisionIdentity, false, input, output, Proposal());

    private sealed class CountingAdapter : ISnowGlobeIdentifiedInferenceAdapter
    {
        private readonly SnowGlobeActionProposal _proposal;
        public CountingAdapter(SnowGlobeActionProposal proposal) => _proposal = proposal;
        public string AdapterIdentity => "test_local/v1";
        public int Calls { get; private set; }
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken) { Calls++; return ValueTask.FromResult(_proposal with { }); }
    }
    private sealed class GateProvider : IPremiumCognitionProvider
    {
        private readonly PremiumCognitionProviderResult _result; private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public GateProvider(PremiumCognitionProviderResult result) => _result = result;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public async ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken) { Calls++; Started.TrySetResult(); await _release.Task.ConfigureAwait(false); return _result; }
        public void Release() => _release.TrySetResult();
    }
    private sealed class NullProvider : IPremiumCognitionProvider
    {
        public ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken) => ValueTask.FromResult<PremiumCognitionProviderResult>(null!);
    }
    private sealed class SlowIgnoringCancellationProvider : IPremiumCognitionProvider
    {
        private readonly TimeSpan _delay;
        public SlowIgnoringCancellationProvider(TimeSpan delay) => _delay = delay;
        public int Calls { get; private set; }
        public async ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(_delay);
            return SuccessResult();
        }
    }
}
