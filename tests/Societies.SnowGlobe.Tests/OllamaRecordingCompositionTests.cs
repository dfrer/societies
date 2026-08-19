using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OllamaRecordingCompositionTests
{
    private const string Root = @"C:\offline-societies-recording-tests";
    private static readonly long StartTicks = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public void PublicModuleSurface_IsThreeOperationsAndPrepareIsDeterministicZeroIoRawFree()
    {
        MethodInfo[] operations = typeof(SnowGlobeOllamaRecordingCompositionModule).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "ExecuteAndPublishOnceAsync", "Prepare", "ValidateArtifact" }, operations.Select(static value => value.Name).Order(StringComparer.Ordinal));
        SnowGlobeOllamaRecordingCompositionModule first = new(Root); SnowGlobeOllamaRecordingCompositionModule second = new(Root);
        OllamaRecordingCompositionPlan a = first.Prepare(new(777, StartTicks), "composition-nonce-v1");
        OllamaRecordingCompositionPlan b = second.Prepare(new(777, StartTicks), "composition-nonce-v1");
        Assert.Equal("88c848fa574ae1fe4e90197231d7c414ab2f5b5e08faa85e13ab59c8e04b060b", a.PlanDigestSha256);
        Assert.Equal(a.PlanDigestSha256, b.PlanDigestSha256);
        Assert.Equal(OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, a.RelativeArtifactPath);
        Assert.Equal(SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256, a.RegisteredCellDigestSha256);
        Assert.False(a.IsConsumed); Assert.False(a.AdditionalAttemptAuthorized);
        string publicValues = string.Join('|', typeof(OllamaRecordingCompositionPlan).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.GetValue(a)?.ToString()));
        Assert.DoesNotContain("composition-nonce-v1", publicValues, StringComparison.Ordinal);
        Assert.DoesNotContain(Root, publicValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, publicValues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrepareRejectsInvalidCallerFactsWithoutEchoing()
    {
        foreach (string invalidRoot in new[]
        {
            "relative-root-secret", Root + "\\", @"C:/offline-societies-recording-tests",
            @"C:\offline\repo\..\repo", @"C:\offline\repo.", @"C:\offline\repo:stream",
            @"C:\offline\CON\repo", @"\\server\share\repo", @"\\?\C:\offline\repo"
        })
            Assert.Equal("repository_root_lexically_invalid", Assert.Throws<OllamaRecordingCompositionException>(() => new SnowGlobeOllamaRecordingCompositionModule(invalidRoot)).Code);
        SnowGlobeOllamaRecordingCompositionModule module = new(Root);
        Assert.Equal("runtime_observation_invalid", Assert.Throws<OllamaRecordingCompositionException>(() => module.Prepare(null!, "valid-nonce-v1")).Code);
        foreach (PinnedRuntimeObservation invalid in new[] { new PinnedRuntimeObservation(0, StartTicks), new PinnedRuntimeObservation(1, 0), new PinnedRuntimeObservation(1, DateTime.MaxValue.Ticks) with { ProcessStartUtcTicks = DateTime.MaxValue.Ticks + 1 } })
            Assert.Equal("runtime_observation_invalid", Assert.Throws<OllamaRecordingCompositionException>(() => module.Prepare(invalid, "valid-nonce-v1")).Code);
        OllamaRecordingCompositionException nonce = Assert.Throws<OllamaRecordingCompositionException>(() => module.Prepare(new(1, StartTicks), "NOT CANONICAL SECRET"));
        Assert.Equal("authorization_nonce_invalid", nonce.Code); Assert.DoesNotContain("SECRET", nonce.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullPlanFailsWithOnlyTheAllowlistedCode()
    {
        SnowGlobeOllamaRecordingCompositionModule module = new(Root);
        OllamaRecordingCompositionException failure = await Assert.ThrowsAsync<OllamaRecordingCompositionException>(() => module.ExecuteAndPublishOnceAsync(null!).AsTask());
        Assert.Equal("plan_invalid", failure.Code); Assert.Equal(failure.Code, failure.Message); Assert.Null(failure.InnerException);
    }

    [Fact]
    public async Task HappyPath_UsesExistingOfflineTransportExactlyTwelveTimesAndPublishesOnce()
    {
        InMemoryOllamaRecordingArtifactStore store = new(); TestTransportFactory factory = new(TestWrappers.Valid());
        SnowGlobePinnedOllamaRecordingModule inner = new(new FixedClock(1), factory);
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, inner, store);
        OllamaRecordingCompositionPlan plan = module.Prepare(new(777, StartTicks), "happy-composition-v1");
        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(plan);
        Assert.True(result.ArtifactPublished); Assert.Equal("Complete", result.OutcomeCode); Assert.Equal("None", result.FailureCode);
        Assert.Equal(1, factory.CreateCount); Assert.Equal(12, factory.Transport!.CallCount);
        Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount); Assert.Equal(0, store.ReadCount);
        Assert.NotNull(result.Artifact); Assert.Equal(12, result.Artifact!.CompletedSlotCount); Assert.True(result.Artifact.ReceiptPresent); Assert.NotNull(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(result.Artifact.CanonicalDigestSha256, module.ValidateArtifact().CanonicalDigestSha256); Assert.Equal(1, store.ReadCount);
        string json = Encoding.UTF8.GetString(result.Artifact.CanonicalUtf8.Span);
        Assert.DoesNotContain("happy-composition-v1", json, StringComparison.Ordinal);
        Assert.DoesNotContain(SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("agent-00", json, StringComparison.Ordinal); Assert.DoesNotContain("GatherWood", json, StringComparison.Ordinal);
        Assert.False(result.HasRawRecordingEvidence); Assert.False(result.AdditionalAttemptAuthorized);
    }

    [Fact]
    public async Task TerminalInnerOutcome_IsPersistedExactlyOnceWithoutNestedEvidenceOrRetry()
    {
        byte[][] wrappers = TestWrappers.Valid(); wrappers[2] = "{}"u8.ToArray();
        InMemoryOllamaRecordingArtifactStore store = new(); TestTransportFactory factory = new(wrappers);
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);
        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "terminal-composition-v1"));
        Assert.Equal("Failed", result.OutcomeCode); Assert.Equal("WrapperRejected", result.FailureCode);
        Assert.Equal(2, result.Artifact!.CompletedSlotCount); Assert.Equal(3, result.Artifact.TerminalSlotOrdinal);
        Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256); Assert.True(result.Artifact.ReceiptPresent);
        Assert.Equal(3, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount);
        using JsonDocument document = JsonDocument.Parse(result.Artifact.CanonicalUtf8); JsonElement summary = document.RootElement.GetProperty("result");
        Assert.Equal(0, summary.GetProperty("automatic_retry_count").GetInt32()); Assert.Equal(0, summary.GetProperty("fallback_count").GetInt32()); Assert.False(summary.GetProperty("additional_attempt_authorized").GetBoolean());
    }

    [Fact]
    public async Task OneSlotHttp200WrapperRejected_PersistsTerminalArtifactInsteadOfGenericIndeterminate()
    {
        byte[][] wrappers = TestWrappers.Valid(); wrappers[0] = "{}"u8.ToArray();
        InMemoryOllamaRecordingArtifactStore store = new(); TestTransportFactory factory = new(wrappers);
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);

        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(
            module.Prepare(new(777, StartTicks), "one-slot-wrapper-rejected-v1"));

        Assert.Equal("Failed", result.OutcomeCode); Assert.Equal("WrapperRejected", result.FailureCode);
        Assert.True(result.ArtifactPublished); Assert.NotNull(result.Artifact);
        Assert.Equal(0, result.Artifact!.CompletedSlotCount); Assert.Equal(1, result.Artifact.TerminalSlotOrdinal);
        Assert.Equal(SubmissionState.ResponseReceived.ToString(), result.Artifact.TerminalSubmissionState);
        Assert.Equal(ChargeState.NotApplicable, result.Artifact.TerminalChargeState); Assert.Equal(200, result.Artifact.TerminalStatusCode);
        Assert.True(result.Artifact.ReceiptPresent); Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(1, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    public async Task OneSlotHttp200RuntimeChangedAfterResponse_PersistsExactTerminalArtifactInsteadOfGenericIndeterminate(string? wrapperDigest)
    {
        InMemoryOllamaRecordingArtifactStore store = new();
        ThrowingFactory factory = new(OllamaLoopbackTransportFailureCode.RuntimeChanged, SubmissionState.ResponseReceived, 200, wrapperDigest);
        SnowGlobeOllamaRecordingCompositionModule module = new(
            Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);

        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(
            module.Prepare(new(777, StartTicks), "one-slot-http200-runtime-changed-v1"));

        Assert.Equal("Failed", result.OutcomeCode); Assert.Equal("RuntimeChanged", result.FailureCode);
        Assert.True(result.ArtifactPublished); Assert.NotNull(result.Artifact);
        Assert.Equal(0, result.Artifact!.CompletedSlotCount); Assert.Equal(1, result.Artifact.TerminalSlotOrdinal);
        Assert.Equal(SubmissionState.ResponseReceived.ToString(), result.Artifact.TerminalSubmissionState);
        Assert.Equal(ChargeState.NotApplicable, result.Artifact.TerminalChargeState); Assert.Equal(200, result.Artifact.TerminalStatusCode);
        Assert.True(result.Artifact.ReceiptPresent); Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(1, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount);
    }

    [Theory]
    [InlineData((int)OllamaLoopbackTransportFailureCode.RuntimeChanged, (int)SubmissionState.SubmissionUnknown, null)]
    [InlineData((int)OllamaLoopbackTransportFailureCode.RuntimeChanged, (int)SubmissionState.DefinitelyNotSubmitted, 200)]
    [InlineData((int)OllamaLoopbackTransportFailureCode.RuntimeChanged, (int)SubmissionState.ResponseReceived, null)]
    [InlineData((int)OllamaLoopbackTransportFailureCode.Poisoned, (int)SubmissionState.ResponseReceived, 200)]
    public async Task NonTransportEmittableRuntimeTerminalTuplesRemainClosed(int codeValue, int submissionValue, int? status)
    {
        OllamaLoopbackTransportFailureCode code = (OllamaLoopbackTransportFailureCode)codeValue;
        SubmissionState submission = (SubmissionState)submissionValue;
        InMemoryOllamaRecordingArtifactStore store = new(); ThrowingFactory factory = new(code, submission, status);
        SnowGlobeOllamaRecordingCompositionModule module = new(
            Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);

        OllamaRecordingCompositionException failure = await Assert.ThrowsAsync<OllamaRecordingCompositionException>(() =>
            module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), $"invalid-runtime-terminal-{codeValue}-{submissionValue}-{status?.ToString() ?? "null"}")).AsTask());

        Assert.Equal("composition_execution_indeterminate", failure.Code); Assert.Null(failure.InnerException);
        Assert.Equal(1, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(0, store.PublishCount);
    }

    [Fact]
    public async Task AuthorizationRejectionAfterReservationPersistsExactCompositionOnlyArtifact()
    {
        TestTransportFactory factory = new(TestWrappers.Valid()); SnowGlobePinnedOllamaRecordingModule inner = new(new FixedClock(1), factory);
        InMemoryOllamaRecordingArtifactStore firstStore = new(); InMemoryOllamaRecordingArtifactStore rejectedStore = new();
        SnowGlobeOllamaRecordingCompositionModule first = new(Root, inner, firstStore); SnowGlobeOllamaRecordingCompositionModule rejected = new(Root, inner, rejectedStore);
        _ = await first.ExecuteAndPublishOnceAsync(first.Prepare(new(777, StartTicks), "shared-authorization-v1"));
        OllamaRecordingCompositionResult result = await rejected.ExecuteAndPublishOnceAsync(rejected.Prepare(new(777, StartTicks), "shared-authorization-v1"));
        Assert.Equal("AuthorizationRejected", result.OutcomeCode); Assert.Equal("AuthorizationRejected", result.FailureCode);
        Assert.NotNull(result.Artifact); Assert.False(result.Artifact!.RecordingResultPresent); Assert.Null(result.Artifact.CompletedSlotCount);
        Assert.False(result.Artifact.ReceiptPresent); Assert.Null(result.Artifact.ReceiptDigestSha256); Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(1, rejectedStore.ReserveCount); Assert.Equal(1, rejectedStore.PublishCount); Assert.Equal(1, factory.CreateCount); Assert.Equal(12, factory.Transport!.CallCount);
    }

    [Fact]
    public async Task PostTwelveEvidenceRejectionProjectionPersistsExactRawFreeArtifactOnce()
    {
        TestTransportFactory factory = new(TestWrappers.Valid()); SnowGlobePinnedOllamaRecordingModule inner = new(new FixedClock(1), factory);
        InMemoryOllamaRecordingArtifactStore store = new();
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, inner, store, (session, complete) =>
        {
            Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete, complete.OutcomeCode);
            Assert.Equal(12, complete.CompletedSlotCount); Assert.NotNull(complete.Receipt); Assert.NotNull(complete.Evidence);
            SnowGlobeOllamaLoopbackRecordingReceipt receipt = EvidenceRejectedReceipt(complete.Receipt!);
            return inner.CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed,
                SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected, session, 12, 12,
                SubmissionState.ResponseReceived, 200, receipt, null);
        });

        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "evidence-rejected-projection-v1"));
        Assert.Equal("Failed", result.OutcomeCode); Assert.Equal("EvidenceRejected", result.FailureCode);
        Assert.NotNull(result.Artifact); Assert.Equal("Failed", result.Artifact!.RecordingOutcomeCode); Assert.Equal("EvidenceRejected", result.Artifact.RecordingFailureCode);
        Assert.Equal(12, result.Artifact.CompletedSlotCount); Assert.Equal(12, result.Artifact.TerminalSlotOrdinal);
        Assert.Equal(SubmissionState.ResponseReceived.ToString(), result.Artifact.TerminalSubmissionState); Assert.Equal(ChargeState.NotApplicable, result.Artifact.TerminalChargeState); Assert.Equal(200, result.Artifact.TerminalStatusCode);
        Assert.True(result.Artifact.ReceiptPresent); Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(1, factory.CreateCount); Assert.Equal(12, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount);
        string json = Encoding.UTF8.GetString(result.Artifact.CanonicalUtf8.Span);
        Assert.DoesNotContain("agent-00", json, StringComparison.Ordinal); Assert.DoesNotContain("GatherWood", json, StringComparison.Ordinal);
        Assert.Equal(result.Artifact.CanonicalDigestSha256, module.ValidateArtifact().CanonicalDigestSha256);
    }

    [Theory]
    [InlineData((int)OllamaLoopbackTransportFailureCode.RuntimeChanged, SubmissionState.DefinitelyNotSubmitted, null, "Failed", "RuntimeChanged")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.Poisoned, SubmissionState.DefinitelyNotSubmitted, null, "Failed", "TransportPoisoned")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.Cancelled, SubmissionState.SubmissionUnknown, null, "Cancelled", "Cancelled")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.TimedOut, SubmissionState.SubmissionUnknown, null, "TimedOut", "TimedOut")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.TransportFailure, SubmissionState.SubmissionUnknown, null, "Failed", "TransportFailure")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.HttpResponseRejected, SubmissionState.ResponseReceived, 429, "Failed", "HttpResponseRejected")]
    [InlineData((int)OllamaLoopbackTransportFailureCode.ResponseBodyRejected, SubmissionState.ResponseReceived, 200, "Failed", "ResponseBodyRejected")]
    public async Task EveryClosedTransportTerminalPersistsExactRawFreeArtifactOnce(int codeValue, SubmissionState submission, int? status, string outcome, string failure)
    {
        OllamaLoopbackTransportFailureCode code = (OllamaLoopbackTransportFailureCode)codeValue;
        InMemoryOllamaRecordingArtifactStore store = new(); ThrowingFactory factory = new(code, submission, status);
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);
        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), $"terminal-{code.ToString().ToLowerInvariant()}-v1"));
        Assert.Equal(outcome, result.OutcomeCode); Assert.Equal(failure, result.FailureCode); Assert.NotNull(result.Artifact);
        Assert.Equal(submission.ToString(), result.Artifact!.TerminalSubmissionState); Assert.Equal(status, result.Artifact.TerminalStatusCode);
        Assert.True(result.Artifact.ReceiptPresent); Assert.Null(result.Artifact.NestedRecordingEvidenceDigestSha256);
        Assert.Equal(1, factory.CreateCount); Assert.Equal(1, factory.Transport!.CallCount); Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount);
    }

    [Fact]
    public async Task PreCancellationConsumesPlanWithoutStoreOrTransportAndReuseStaysClosed()
    {
        InMemoryOllamaRecordingArtifactStore store = new(); TestTransportFactory factory = new(TestWrappers.Valid());
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);
        OllamaRecordingCompositionPlan plan = module.Prepare(new(777, StartTicks), "pre-cancel-composition-v1");
        using CancellationTokenSource cancelled = new(); cancelled.Cancel();
        OllamaRecordingCompositionResult first = await module.ExecuteAndPublishOnceAsync(plan, cancelled.Token);
        OllamaRecordingCompositionResult second = await module.ExecuteAndPublishOnceAsync(plan);
        Assert.Equal("Cancelled", first.OutcomeCode); Assert.Null(first.Artifact); Assert.Equal("PlanReused", second.FailureCode);
        Assert.True(plan.IsConsumed); Assert.Equal(0, store.ReserveCount); Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task PlanIsObjectBoundAndAtomicAcrossConcurrentReuse()
    {
        InMemoryOllamaRecordingArtifactStore foreignStore = new(); TestTransportFactory foreignFactory = new(TestWrappers.Valid());
        SnowGlobeOllamaRecordingCompositionModule owner = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), new TestTransportFactory(TestWrappers.Valid())), new InMemoryOllamaRecordingArtifactStore());
        SnowGlobeOllamaRecordingCompositionModule foreign = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), foreignFactory), foreignStore);
        OllamaRecordingCompositionPlan foreignPlan = owner.Prepare(new(777, StartTicks), "foreign-plan-v1");
        OllamaRecordingCompositionResult rejected = await foreign.ExecuteAndPublishOnceAsync(foreignPlan);
        Assert.Equal("BindingMismatch", rejected.FailureCode); Assert.True(foreignPlan.IsConsumed); Assert.Equal(0, foreignStore.ReserveCount);
        OllamaRecordingCompositionResult spent = await owner.ExecuteAndPublishOnceAsync(foreignPlan);
        Assert.Equal("PlanReused", spent.FailureCode); Assert.Equal(0, foreignStore.ReserveCount);

        InMemoryOllamaRecordingArtifactStore store = new(); TestTransportFactory factory = new(TestWrappers.Valid());
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), factory), store);
        OllamaRecordingCompositionPlan plan = module.Prepare(new(777, StartTicks), "concurrent-plan-v1");
        OllamaRecordingCompositionResult[] results = await Task.WhenAll(module.ExecuteAndPublishOnceAsync(plan).AsTask(), module.ExecuteAndPublishOnceAsync(plan).AsTask());
        Assert.Single(results, value => value.ArtifactPublished); Assert.Single(results, value => value.FailureCode == "PlanReused");
        Assert.Equal(1, store.ReserveCount); Assert.Equal(1, store.PublishCount); Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task ConcurrentForeignAndOwnerAttemptHaveExactlyOneTerminalConsumer()
    {
        InMemoryOllamaRecordingArtifactStore ownerStore = new(); InMemoryOllamaRecordingArtifactStore foreignStore = new();
        TestTransportFactory ownerFactory = new(TestWrappers.Valid()); TestTransportFactory foreignFactory = new(TestWrappers.Valid());
        SnowGlobeOllamaRecordingCompositionModule owner = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), ownerFactory), ownerStore);
        SnowGlobeOllamaRecordingCompositionModule foreign = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), foreignFactory), foreignStore);
        OllamaRecordingCompositionPlan plan = owner.Prepare(new(777, StartTicks), "foreign-owner-race-v1");
        OllamaRecordingCompositionResult[] results = await Task.WhenAll(owner.ExecuteAndPublishOnceAsync(plan).AsTask(), foreign.ExecuteAndPublishOnceAsync(plan).AsTask());
        Assert.Single(results, result => result.FailureCode != "PlanReused");
        Assert.InRange(ownerStore.ReserveCount + foreignStore.ReserveCount, 0, 1);
        Assert.InRange(ownerFactory.CreateCount + foreignFactory.CreateCount, 0, 1);
    }

    [Fact]
    public void SameCallerFactsOnDifferentRootsProduceDifferentPlanDigests()
    {
        SnowGlobeOllamaRecordingCompositionModule first = new(@"C:\root-binding-a"); SnowGlobeOllamaRecordingCompositionModule second = new(@"C:\root-binding-b");
        OllamaRecordingCompositionPlan a = first.Prepare(new(777, StartTicks), "root-binding-v1"); OllamaRecordingCompositionPlan b = second.Prepare(new(777, StartTicks), "root-binding-v1");
        Assert.NotEqual(a.RepositoryRootDigestSha256, b.RepositoryRootDigestSha256); Assert.NotEqual(a.PlanDigestSha256, b.PlanDigestSha256);
    }

    [Fact]
    public async Task PublicExceptionsAreCodeOnlyAndHostileDisposalCannotMaskClosedFailure()
    {
        const string secret = @"C:\attacker\secret-path nonce-secret win32-message";
        SnowGlobePinnedOllamaRecordingModule inner = new(new FixedClock(1), new TestTransportFactory(TestWrappers.Valid()));
        SnowGlobeOllamaRecordingCompositionModule reserveFailure = new(Root, inner, new ThrowingStore(secret));
        OllamaRecordingCompositionException reserve = await Assert.ThrowsAsync<OllamaRecordingCompositionException>(() => reserveFailure.ExecuteAndPublishOnceAsync(reserveFailure.Prepare(new(777, StartTicks), "exception-reserve-v1")).AsTask());
        Assert.Null(reserve.InnerException); Assert.Equal(reserve.Code, reserve.Message); Assert.DoesNotContain(secret, reserve.ToString(), StringComparison.Ordinal);

        DisposalThrowingStore hostile = new(secret); SnowGlobeOllamaRecordingCompositionModule disposeFailure = new(Root, new SnowGlobePinnedOllamaRecordingModule(new FixedClock(1), new TestTransportFactory(TestWrappers.Valid())), hostile);
        OllamaRecordingCompositionException disposal = await Assert.ThrowsAsync<OllamaRecordingCompositionException>(() => disposeFailure.ExecuteAndPublishOnceAsync(disposeFailure.Prepare(new(777, StartTicks), "exception-dispose-v1")).AsTask());
        Assert.Equal("artifact_publication_indeterminate", disposal.Code); Assert.Null(disposal.InnerException); Assert.Equal(disposal.Code, disposal.Message); Assert.DoesNotContain(secret, disposal.ToString(), StringComparison.Ordinal); Assert.Equal(1, hostile.DisposeCount);

        OllamaRecordingExecutionArtifactException artifact = Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(new byte[] { 0xc3, 0x28 }));
        Assert.Null(artifact.InnerException); Assert.Equal(artifact.Code, artifact.Message); Assert.DoesNotContain("DecoderFallback", artifact.ToString(), StringComparison.Ordinal);
    }

    private sealed class FixedClock : ICognitionQualityRecordingSessionClock
    {
        private readonly long _now; internal FixedClock(long now) => _now = now; public long NowMilliseconds => _now;
    }

    private static SnowGlobeOllamaLoopbackRecordingReceipt EvidenceRejectedReceipt(SnowGlobeOllamaLoopbackRecordingReceipt complete)
    {
        JsonObject receipt = JsonNode.Parse(complete.CanonicalUtf8.Span)!.AsObject();
        receipt["status"] = "terminal"; receipt["outcome"] = "Failed"; receipt["failure_code"] = "EvidenceRejected";
        receipt["terminal_slot_ordinal"] = 12; receipt["nested_recording_evidence_digest_sha256"] = null;
        receipt.Remove("receipt_payload_digest_sha256"); byte[] payload = JsonSerializer.SerializeToUtf8Bytes(receipt);
        string payloadDigest = CognitionQualityHash.Sha256(payload); Array.Clear(payload);
        receipt["receipt_payload_digest_sha256"] = payloadDigest; byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(receipt);
        return new SnowGlobeOllamaLoopbackRecordingReceipt(canonical, payloadDigest, complete.Slots, null);
    }

    internal sealed class TestTransportFactory : IOllamaLoopbackRecordingTransportFactory
    {
        private readonly byte[][] _wrappers; private int _count;
        internal TestTransportFactory(byte[][] wrappers) => _wrappers = wrappers;
        internal int CreateCount => Volatile.Read(ref _count);
        internal InMemoryOfflineOllamaRecordingTransportAdapter? Transport { get; private set; }
        public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding)
        {
            Interlocked.Increment(ref _count); Transport = new(_wrappers.Select(static value => value.ToArray()).ToArray()); return Transport;
        }
    }

    private sealed class ThrowingFactory : IOllamaLoopbackRecordingTransportFactory
    {
        private readonly OllamaLoopbackTransportFailureCode _code; private readonly SubmissionState _submission; private readonly int? _status; private readonly string? _wrapperDigest; private int _count;
        internal ThrowingFactory(OllamaLoopbackTransportFailureCode code, SubmissionState submission, int? status, string? wrapperDigest = null) { _code = code; _submission = submission; _status = status; _wrapperDigest = wrapperDigest; }
        internal int CreateCount => Volatile.Read(ref _count); internal ThrowingTransport? Transport { get; private set; }
        public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding) { Interlocked.Increment(ref _count); Transport = new(_code, _submission, _status, _wrapperDigest); return Transport; }
    }

    private sealed class ThrowingTransport : IOfflineOllamaRecordingTransportPort
    {
        private readonly OllamaLoopbackTransportFailureCode _code; private readonly SubmissionState _submission; private readonly int? _status; private readonly string? _wrapperDigest; private int _count;
        internal ThrowingTransport(OllamaLoopbackTransportFailureCode code, SubmissionState submission, int? status, string? wrapperDigest) { _code = code; _submission = submission; _status = status; _wrapperDigest = wrapperDigest; }
        internal int CallCount => Volatile.Read(ref _count);
        public ValueTask<OfflineOllamaRecordingTransportResponse> ExchangeOnceAsync(OfflineOllamaRecordingTransportRequest request, CancellationToken cancellationToken)
        { Interlocked.Increment(ref _count); request.Dispose(); return ValueTask.FromException<OfflineOllamaRecordingTransportResponse>(new OllamaLoopbackTransportException(_code, _submission, _status, _wrapperDigest)); }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingStore : IOllamaRecordingArtifactStore
    {
        private readonly string _secret; internal ThrowingStore(string secret) => _secret = secret;
        public IOllamaRecordingArtifactReservation Reserve(string absoluteRepositoryRoot, string relativeArtifactPath) => throw new OllamaRecordingArtifactStoreException("artifact_reservation_failed", new IOException(_secret));
        public byte[] ReadBounded(string absoluteRepositoryRoot, string relativeArtifactPath, int maximumBytes) => throw new NotSupportedException();
    }

    private sealed class DisposalThrowingStore : IOllamaRecordingArtifactStore
    {
        private readonly string _secret; private int _disposeCount; internal DisposalThrowingStore(string secret) => _secret = secret; internal int DisposeCount => Volatile.Read(ref _disposeCount);
        public IOllamaRecordingArtifactReservation Reserve(string absoluteRepositoryRoot, string relativeArtifactPath) => new Reservation(this);
        public byte[] ReadBounded(string absoluteRepositoryRoot, string relativeArtifactPath, int maximumBytes) => throw new NotSupportedException();
        private sealed class Reservation : IOllamaRecordingArtifactReservation
        {
            private readonly DisposalThrowingStore _owner; internal Reservation(DisposalThrowingStore owner) => _owner = owner;
            public byte[] PublishAndReadBack(ReadOnlyMemory<byte> canonicalUtf8, int maximumBytes) => throw new OllamaRecordingArtifactStoreException("artifact_publication_indeterminate", new IOException(_owner._secret));
            public void Dispose() { Interlocked.Increment(ref _owner._disposeCount); throw new IOException(_owner._secret); }
        }
    }
}

internal static class TestWrappers
{
    internal static byte[][] Valid() => Enumerable.Range(1, 12).Select(Wrapper).ToArray();
    private static byte[] Wrapper(int index)
    {
        string action = index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
        int quantity = index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
        string proposal = $"{{\"agent_id\":\"agent-00\",\"action\":\"{action}\",\"quantity\":{quantity}}}";
        return Encoding.UTF8.GetBytes($"{{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":{JsonSerializer.Serialize(proposal)},\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000}}" );
    }
}
