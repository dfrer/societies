using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityRecordingSessionTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string AdapterIdentity = "adapter-v1";

    [Fact]
    public async Task GoldenSession_IsExactOneShotAndDeterministicAcrossIndependentModules()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        ReadOnlyMemory<byte>[] responses = Responses();
        using OfflineFixedResponseCognitionQualityRecordingAdapter firstAdapter = new(AdapterIdentity, responses);
        using OfflineFixedResponseCognitionQualityRecordingAdapter secondAdapter = new(AdapterIdentity, responses);
        Assert.True(MemoryMarshal.TryGetArray(responses[0], out ArraySegment<byte> callerOwned));
        callerOwned.Array![callerOwned.Offset] = 0;
        FixedClock firstClock = new(1000);
        FixedClock secondClock = new(1000);
        CognitionQualityRecordingSessionModule firstModule = new(firstAdapter, firstClock);
        CognitionQualityRecordingSessionModule secondModule = new(secondAdapter, secondClock);

        CognitionQualityRecordingSessionCapability firstCapability = firstModule.Authorize(publication, provenance, Authorization(publication, provenance, firstAdapter));
        CognitionQualityRecordingSessionCapability secondCapability = secondModule.Authorize(publication, provenance, Authorization(publication, provenance, secondAdapter));
        CognitionQualityRecordingSessionResult first = await firstModule.RecordOnceAsync(firstCapability);
        CognitionQualityRecordingSessionResult second = await secondModule.RecordOnceAsync(secondCapability);

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, first.OutcomeCode);
        Assert.Equal(12, first.CompletedSlotCount);
        Assert.Null(first.TerminalSlotOrdinal);
        Assert.Equal(SubmissionState.NotApplicable, first.SubmissionState);
        Assert.Equal(ChargeState.NotApplicable, first.ChargeState);
        Assert.True(first.IsOfflineFixture);
        Assert.False(first.HasTransportDeliveryAttestation);
        Assert.False(first.HasModelExecutionAttestation);
        Assert.False(first.AdditionalAttemptAuthorized);
        Assert.NotNull(first.Evidence);
        Assert.Equal("0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c", first.Evidence!.ResponseSetDigestSha256);
        Assert.Equal("61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4", first.Evidence.CanonicalDigestSha256);
        Assert.Equal(first.Evidence.CanonicalDigestSha256, second.Evidence!.CanonicalDigestSha256);
        Assert.Equal(first.CapabilityDigestSha256, second.CapabilityDigestSha256);
        Assert.Equal(12, firstAdapter.CallCount);
        Assert.Equal(12, secondAdapter.CallCount);

        CognitionQualityRecordingSessionResult reused = await firstModule.RecordOnceAsync(firstCapability);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
        Assert.Null(reused.Evidence);
        Assert.Equal(12, firstAdapter.CallCount);
    }

    [Fact]
    public async Task Requests_BindExactPublicationProvenanceAdapterPromptOrderAndSingleAttempt()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        using OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(2000));
        CognitionQualityRecordingSessionAuthorization authorization = Authorization(publication, provenance, adapter);
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, authorization);

        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(capability);

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode);
        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = adapter.SnapshotRequests();
        try
        {
            Assert.Equal(12, requests.Count);
            for (int index = 0; index < requests.Count; index++)
            {
                CognitionQualityRecordingAdapterRequest request = requests[index];
                CognitionQualityPromptEnvelopeSlot slot = publication.Slots[index];
                Assert.Equal(index + 1, request.SlotOrdinal);
                Assert.Equal(1, request.AttemptNumber);
                Assert.Equal(capability.CapabilityDigestSha256, request.CapabilityDigestSha256);
                Assert.Equal(authorization.AuthorizationNonce, request.AuthorizationNonce);
                Assert.Equal(publication.CanonicalDigestSha256, request.PromptPublicationDigestSha256);
                Assert.Equal(publication.PromptSetDigestSha256, request.PromptSetDigestSha256);
                Assert.Equal(provenance.ProvenanceDigestSha256, request.ProvenanceDigestSha256);
                Assert.Equal(adapter.AdapterIdentity, request.AdapterIdentity);
                Assert.Equal(adapter.AdapterContractDigestSha256, request.AdapterContractDigestSha256);
                Assert.Equal(slot.ScenarioId, request.ScenarioId);
                Assert.Equal(slot.ObservationDigestSha256, request.ObservationDigestSha256);
                Assert.Equal(slot.PromptByteCount, request.PromptByteCount);
                Assert.Equal(slot.PromptDigestSha256, request.PromptDigestSha256);
                Assert.True(slot.PromptUtf8.Span.SequenceEqual(request.PromptUtf8.Span));
                Assert.True(request.RemainingSessionMilliseconds > 0);
            }
            Assert.Equal(12, requests.Select(request => request.RequestDigestSha256).Distinct(StringComparer.Ordinal).Count());
        }
        finally { foreach (CognitionQualityRecordingAdapterRequest request in requests) request.Dispose(); }
    }

    [Fact]
    public async Task BoundMalformedResponse_IsScoredWhileTransferredBuffersAreAlwaysZeroed()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        List<byte[]> transferred = new();
        List<bool> zeroObservations = new();
        ScriptedAdapter adapter = new(async (request, token) =>
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            byte[] owned = request.SlotOrdinal == 1 ? new byte[] { 0xc3, 0x28 } : Responses()[request.SlotOrdinal - 1].ToArray();
            transferred.Add(owned);
            return OfflineResponse(request, owned, zeroObservations.Add);
        });
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(3000));

        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(
            module.Authorize(publication, provenance, Authorization(publication, provenance, adapter)));

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode);
        Assert.Equal("response_utf8_invalid", result.Evidence!.RecordedResponseRun.ResponseBindings[0].ParseOutcome);
        Assert.All(transferred, bytes => Assert.All(bytes, value => Assert.Equal(0, value)));
        Assert.Equal(12, zeroObservations.Count);
        Assert.All(zeroObservations, Assert.True);
        Assert.DoesNotContain("System.Byte", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PartialAdapterException_TerminatesWithoutEvidenceOrLaterCalls()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        ScriptedAdapter adapter = new((request, _) => request.SlotOrdinal == 4
            ? throw new InvalidOperationException("offline_script_failure")
            : ValueTask.FromResult(OfflineResponse(request, Responses()[request.SlotOrdinal - 1].ToArray())));
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(4000));

        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(
            module.Authorize(publication, provenance, Authorization(publication, provenance, adapter)));

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.AdapterFailure, result.OutcomeCode);
        Assert.Equal(3, result.CompletedSlotCount);
        Assert.Equal(4, result.TerminalSlotOrdinal);
        Assert.Null(result.Evidence);
        Assert.Equal(4, adapter.CallCount);
        Assert.False(result.AdditionalAttemptAuthorized);
    }

    [Fact]
    public async Task SubmissionTerminalStates_FailClosedWithoutLaterCalls()
    {
        await AssertSubmissionTerminal(SubmissionState.DefinitelyNotSubmitted, ChargeState.NotApplicable,
            CognitionQualityRecordingSessionOutcomeCode.DefinitelyNotSubmitted);
        await AssertSubmissionTerminal(SubmissionState.SubmissionUnknown, ChargeState.Unknown,
            CognitionQualityRecordingSessionOutcomeCode.SubmissionUnknown);
    }

    [Fact]
    public async Task MalformedEnvelopes_AreRejectedAndOwnedBuffersAreZeroed()
    {
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, capabilityDigest: new string('0', 64)));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, requestDigest: new string('0', 64)));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, slotOrdinal: 2));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, submission: SubmissionState.Dispatching));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, submission: SubmissionState.ResponseReceived));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, charge: ChargeState.Settled));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, submission: SubmissionState.DefinitelyNotSubmitted, charge: ChargeState.Settled));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, submission: SubmissionState.SubmissionUnknown, charge: ChargeState.NotApplicable));
        await AssertMalformed((request, buffer) => OfflineResponse(request, buffer, transportAttestation: true));
        await AssertMalformed((request, _) => OfflineResponse(request, Array.Empty<byte>()));
        await AssertMalformed((request, _) => OfflineResponse(request, new byte[CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes + 1]));
    }

    [Fact]
    public async Task CancellationBeforeAndDuringAcquisition_SpendsAuthorityAndStopsCalls()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        using OfflineFixedResponseCognitionQualityRecordingAdapter preAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule preModule = new(preAdapter, new FixedClock(5000));
        CognitionQualityRecordingSessionCapability preCapability = preModule.Authorize(publication, provenance, Authorization(publication, provenance, preAdapter));
        using CancellationTokenSource preCancelled = new();
        preCancelled.Cancel();

        CognitionQualityRecordingSessionResult pre = await preModule.RecordOnceAsync(preCapability, preCancelled.Token);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Cancelled, pre.OutcomeCode);
        Assert.True(preCapability.IsConsumed);
        Assert.Equal(0, preAdapter.CallCount);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, (await preModule.RecordOnceAsync(preCapability)).OutcomeCode);

        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedAdapter duringAdapter = new(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        });
        CognitionQualityRecordingSessionModule duringModule = new(duringAdapter, new FixedClock(6000));
        CognitionQualityRecordingSessionCapability duringCapability = duringModule.Authorize(publication, provenance, Authorization(publication, provenance, duringAdapter));
        using CancellationTokenSource duringCancellation = new();
        Task<CognitionQualityRecordingSessionResult> pending = duringModule.RecordOnceAsync(duringCapability, duringCancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        duringCancellation.Cancel();
        CognitionQualityRecordingSessionResult during = await pending;
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Cancelled, during.OutcomeCode);
        Assert.Equal(1, during.TerminalSlotOrdinal);
        Assert.Equal(1, duringAdapter.CallCount);
        Assert.Null(during.Evidence);
    }

    [Fact]
    public async Task SessionTimeout_IsTerminalAndDoesNotAuthorizeRetry()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        ScriptedAdapter adapter = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        });
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(7000));
        CognitionQualityRecordingSessionAuthorization authorization = Authorization(publication, provenance, adapter) with { SessionTimeoutMilliseconds = 25 };
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, authorization);

        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(capability);

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.TimedOut, result.OutcomeCode);
        Assert.Equal(1, adapter.CallCount);
        Assert.Null(result.Evidence);
        Assert.False(result.AdditionalAttemptAuthorized);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, (await module.RecordOnceAsync(capability)).OutcomeCode);
    }

    [Fact]
    public async Task ExpiryWrongModuleAndConcurrentDoubleUse_AllSpendExactlyOnce()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();

        FixedClock expiryClock = new(8000);
        using OfflineFixedResponseCognitionQualityRecordingAdapter expiryAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule expiryModule = new(expiryAdapter, expiryClock);
        CognitionQualityRecordingSessionCapability expired = expiryModule.Authorize(publication, provenance, Authorization(publication, provenance, expiryAdapter));
        expiryClock.Advance(1000);
        CognitionQualityRecordingSessionResult expiry = await expiryModule.RecordOnceAsync(expired);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityExpired, expiry.OutcomeCode);
        Assert.Equal(0, expiryAdapter.CallCount);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, (await expiryModule.RecordOnceAsync(expired)).OutcomeCode);

        using OfflineFixedResponseCognitionQualityRecordingAdapter ownerAdapter = new(AdapterIdentity, Responses());
        using OfflineFixedResponseCognitionQualityRecordingAdapter wrongAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule owner = new(ownerAdapter, new FixedClock(9000));
        CognitionQualityRecordingSessionModule wrong = new(wrongAdapter, new FixedClock(9000));
        CognitionQualityRecordingSessionCapability wrongCapability = owner.Authorize(publication, provenance, Authorization(publication, provenance, ownerAdapter));
        CognitionQualityRecordingSessionResult mismatch = await wrong.RecordOnceAsync(wrongCapability);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.BindingMismatch, mismatch.OutcomeCode);
        Assert.Equal(0, ownerAdapter.CallCount);
        Assert.Equal(0, wrongAdapter.CallCount);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, (await owner.RecordOnceAsync(wrongCapability)).OutcomeCode);

        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedAdapter concurrentAdapter = new(async (request, token) =>
        {
            if (request.SlotOrdinal == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            }
            return OfflineResponse(request, Responses()[request.SlotOrdinal - 1].ToArray());
        });
        CognitionQualityRecordingSessionModule concurrentModule = new(concurrentAdapter, new FixedClock(10000));
        CognitionQualityRecordingSessionCapability concurrentCapability = concurrentModule.Authorize(publication, provenance, Authorization(publication, provenance, concurrentAdapter));
        Task<CognitionQualityRecordingSessionResult> first = concurrentModule.RecordOnceAsync(concurrentCapability).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CognitionQualityRecordingSessionResult loser = await concurrentModule.RecordOnceAsync(concurrentCapability);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, loser.OutcomeCode);
        release.TrySetResult();
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, (await first).OutcomeCode);
        Assert.Equal(12, concurrentAdapter.CallCount);
    }

    [Fact]
    public async Task EvidenceFailureAfterAllSlots_IsRawFreeAndDoesNotPermitThirteenthCall()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        CognitionQualityRecordingSessionCapability? capability = null;
        ScriptedAdapter adapter = new((request, _) =>
        {
            if (request.SlotOrdinal == 12)
            {
                FieldInfo canonical = typeof(CognitionQualityExecutionProvenance).GetField("_canonicalUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
                canonical.SetValue(capability!.Provenance, "{}"u8.ToArray());
            }
            return ValueTask.FromResult(OfflineResponse(request, Responses()[request.SlotOrdinal - 1].ToArray()));
        });
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(11000));
        capability = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter));

        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(capability);

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.EvidenceRejected, result.OutcomeCode);
        Assert.Equal(12, result.CompletedSlotCount);
        Assert.Equal(12, result.TerminalSlotOrdinal);
        Assert.Null(result.Evidence);
        Assert.Equal(12, adapter.CallCount);
    }

    [Fact]
    public async Task NonceTombstonesRejectSequentialConcurrentReplayAndCapacityWithoutEcho()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        using OfflineFixedResponseCognitionQualityRecordingAdapter sequentialAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule sequentialModule = new(sequentialAdapter, new FixedClock(11500));
        CognitionQualityRecordingSessionAuthorization same = Authorization(publication, provenance, sequentialAdapter, "nonce-sequential-v1");
        _ = sequentialModule.Authorize(publication, provenance, same);
        CognitionQualityRecordingAuthorizationException sequential = Assert.Throws<CognitionQualityRecordingAuthorizationException>(
            () => sequentialModule.Authorize(publication, provenance, same));
        Assert.Equal(CognitionQualityRecordingAuthorizationFailureCode.NonceReused, sequential.Code);
        Assert.DoesNotContain(same.AuthorizationNonce, sequential.Message, StringComparison.Ordinal);

        using OfflineFixedResponseCognitionQualityRecordingAdapter concurrentAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule concurrentModule = new(concurrentAdapter, new FixedClock(11600));
        CognitionQualityRecordingSessionAuthorization concurrentAuthorization = Authorization(publication, provenance, concurrentAdapter, "nonce-concurrent-v1");
        string[] outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
        {
            try { _ = concurrentModule.Authorize(publication, provenance, concurrentAuthorization); return "success"; }
            catch (CognitionQualityRecordingAuthorizationException exception) { return exception.Code.ToString(); }
        })));
        Assert.Single(outcomes, outcome => outcome == "success");
        Assert.Equal(15, outcomes.Count(outcome => outcome == nameof(CognitionQualityRecordingAuthorizationFailureCode.NonceReused)));
        Assert.Equal(0, concurrentAdapter.CallCount);

        using OfflineFixedResponseCognitionQualityRecordingAdapter capacityAdapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule capacityModule = new(capacityAdapter, new FixedClock(11700));
        FieldInfo tombstonesField = typeof(CognitionQualityRecordingSessionModule).GetField("_authorizedNonces", BindingFlags.Instance | BindingFlags.NonPublic)!;
        HashSet<string> tombstones = (HashSet<string>)tombstonesField.GetValue(capacityModule)!;
        for (int index = 0; index < CognitionQualityRecordingSessionModule.MaximumAuthorizedNonces; index++)
            Assert.True(tombstones.Add($"capacity-nonce-{index:D4}"));
        CognitionQualityRecordingSessionAuthorization overflow = Authorization(publication, provenance, capacityAdapter, "capacity-overflow-v1");
        CognitionQualityRecordingAuthorizationException capacity = Assert.Throws<CognitionQualityRecordingAuthorizationException>(
            () => capacityModule.Authorize(publication, provenance, overflow));
        Assert.Equal(CognitionQualityRecordingAuthorizationFailureCode.NonceCapacityExceeded, capacity.Code);
        Assert.DoesNotContain(overflow.AuthorizationNonce, capacity.Message, StringComparison.Ordinal);
        Assert.Equal(CognitionQualityRecordingSessionModule.MaximumAuthorizedNonces, tombstones.Count);
    }

    [Fact]
    public async Task FixedAdapterUsesSlotOrdinalPerCapabilityAcrossSequentialAndConcurrentSessions()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        using OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(11800));
        CognitionQualityRecordingSessionCapability first = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "multi-capability-one"));
        CognitionQualityRecordingSessionCapability second = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "multi-capability-two"));
        CognitionQualityRecordingSessionResult firstResult = await module.RecordOnceAsync(first);
        CognitionQualityRecordingSessionResult secondResult = await module.RecordOnceAsync(second);

        CognitionQualityRecordingSessionCapability third = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "multi-capability-three"));
        CognitionQualityRecordingSessionCapability fourth = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "multi-capability-four"));
        CognitionQualityRecordingSessionResult[] concurrent = await Task.WhenAll(
            Task.Run(async () => await module.RecordOnceAsync(third)),
            Task.Run(async () => await module.RecordOnceAsync(fourth)));

        CognitionQualityRecordingSessionResult[] all = new[] { firstResult, secondResult }.Concat(concurrent).ToArray();
        Assert.All(all, result => Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode));
        Assert.All(all, result => Assert.Equal("0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c", result.Evidence!.ResponseSetDigestSha256));
        Assert.All(all, result => Assert.Equal("61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4", result.Evidence!.CanonicalDigestSha256));
        Assert.Equal(48, adapter.CallCount);

        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = adapter.SnapshotRequests();
        try
        {
            Assert.Equal(4, requests.GroupBy(request => request.CapabilityDigestSha256, StringComparer.Ordinal).Count());
            foreach (IGrouping<string, CognitionQualityRecordingAdapterRequest> capabilityRequests in requests.GroupBy(request => request.CapabilityDigestSha256, StringComparer.Ordinal))
                Assert.Equal(Enumerable.Range(1, 12), capabilityRequests.Select(request => request.SlotOrdinal));
        }
        finally { foreach (CognitionQualityRecordingAdapterRequest request in requests) request.Dispose(); }
    }

    [Fact]
    public void FixedAdapterRejectsOversizedMemoryBeforeReadingOrCopying()
    {
        using UnreadableOversizedMemoryManager manager = new(CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes + 1);
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = manager.UnreadableMemory;

        Assert.Throws<ArgumentOutOfRangeException>(() => new OfflineFixedResponseCognitionQualityRecordingAdapter(AdapterIdentity, responses));

        Assert.Equal(0, manager.GetSpanCallCount);
    }

    [Fact]
    public async Task FixedAdapterCapturesEachHostileIndexerEntryExactlyOnce()
    {
        ReadOnlyMemory<byte>[] stable = Responses();
        using UnreadableOversizedMemoryManager oversized = new(CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes + 1);
        ChangingIndexerResponses hostile = new(stable, oversized.UnreadableMemory);
        using OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, hostile);

        Assert.Equal(Enumerable.Repeat(1, 12), hostile.SnapshotReadCounts());
        Assert.Equal(0, oversized.GetSpanCallCount);

        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(11850));
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(
            module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "hostile-indexer-v1")));
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode);
        Assert.Equal("0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c", result.Evidence!.ResponseSetDigestSha256);
        Assert.Equal(Enumerable.Repeat(1, 12), hostile.SnapshotReadCounts());
        Assert.Equal(0, oversized.GetSpanCallCount);
    }

    [Fact]
    public async Task FixedAdapterDisposalZerosRetainedCopiesPreservesCallerBytesAndRejectsFurtherAcquisition()
    {
        byte[][] callerOwned = Responses().Select(response => response.ToArray()).ToArray();
        byte[][] callerExpected = callerOwned.Select(response => response.ToArray()).ToArray();
        OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, callerOwned.Select(response => (ReadOnlyMemory<byte>)response).ToArray());
        FieldInfo responsesField = typeof(OfflineFixedResponseCognitionQualityRecordingAdapter).GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[][] retainedResponses = ((byte[][])responsesField.GetValue(adapter)!).ToArray();
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(11900));
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete,
            (await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "dispose-normal-v1")))).OutcomeCode);
        FieldInfo requestsField = typeof(OfflineFixedResponseCognitionQualityRecordingAdapter).GetField("_requests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        List<CognitionQualityRecordingAdapterRequest> retainedRequests = (List<CognitionQualityRecordingAdapterRequest>)requestsField.GetValue(adapter)!;
        FieldInfo promptField = typeof(CognitionQualityRecordingAdapterRequest).GetField("_promptUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[][] retainedPrompts = retainedRequests.Select(request => (byte[])promptField.GetValue(request)!).ToArray();
        int callsBeforeDispose = adapter.CallCount;

        adapter.Dispose();
        adapter.Dispose();
        await adapter.DisposeAsync();

        Assert.True(adapter.IsDisposed);
        Assert.All(retainedResponses, response => Assert.All(response, value => Assert.Equal(0, value)));
        Assert.All(retainedPrompts, prompt => Assert.All(prompt, value => Assert.Equal(0, value)));
        for (int index = 0; index < callerOwned.Length; index++) Assert.Equal(callerExpected[index], callerOwned[index]);
        CognitionQualityRecordingSessionCapability postDispose = module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, "dispose-post-v1"));
        CognitionQualityRecordingSessionResult rejected = await module.RecordOnceAsync(postDispose);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.AdapterFailure, rejected.OutcomeCode);
        Assert.Equal(1, rejected.TerminalSlotOrdinal);
        Assert.Null(rejected.Evidence);
        Assert.Equal(callsBeforeDispose, adapter.CallCount);
    }

    [Fact]
    public async Task FixedAdapterDisposeRacesSessionsWithoutLeakingOrThrowing()
    {
        byte[][] callerOwned = Responses().Select(response => response.ToArray()).ToArray();
        byte[][] callerExpected = callerOwned.Select(response => response.ToArray()).ToArray();
        OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, callerOwned.Select(response => (ReadOnlyMemory<byte>)response).ToArray());
        FieldInfo responsesField = typeof(OfflineFixedResponseCognitionQualityRecordingAdapter).GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[][] retained = ((byte[][])responsesField.GetValue(adapter)!).ToArray();
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(11950));
        CognitionQualityRecordingSessionCapability[] capabilities = Enumerable.Range(0, 16)
            .Select(index => module.Authorize(publication, provenance, Authorization(publication, provenance, adapter, $"dispose-race-{index:D2}"))).ToArray();
        using ManualResetEventSlim start = new(false);
        Task<CognitionQualityRecordingSessionResult>[] sessions = capabilities.Select(capability => Task.Run(async () =>
        {
            start.Wait();
            return await module.RecordOnceAsync(capability);
        })).ToArray();
        Task disposer = Task.Run(() => { start.Wait(); adapter.Dispose(); });
        start.Set();
        CognitionQualityRecordingSessionResult[] results = await Task.WhenAll(sessions);
        await disposer;

        Assert.All(results, result => Assert.Contains(result.OutcomeCode, new[]
        {
            CognitionQualityRecordingSessionOutcomeCode.Complete,
            CognitionQualityRecordingSessionOutcomeCode.AdapterFailure
        }));
        Assert.All(results.Where(result => result.OutcomeCode != CognitionQualityRecordingSessionOutcomeCode.Complete), result => Assert.Null(result.Evidence));
        Assert.All(retained, response => Assert.All(response, value => Assert.Equal(0, value)));
        for (int index = 0; index < callerOwned.Length; index++) Assert.Equal(callerExpected[index], callerOwned[index]);
    }

    [Fact]
    public void AuthorizationAndPublicSurface_AreClosedBoundedAndRawFree()
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        using OfflineFixedResponseCognitionQualityRecordingAdapter adapter = new(AdapterIdentity, Responses());
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(12000));
        CognitionQualityRecordingSessionAuthorization valid = Authorization(publication, provenance, adapter);
        foreach (CognitionQualityRecordingSessionAuthorization invalid in new[]
        {
            valid with { PromptPublicationDigestSha256 = new string('0', 64) },
            valid with { PromptSetDigestSha256 = new string('0', 64) },
            valid with { ProvenanceDigestSha256 = new string('0', 64) },
            valid with { AdapterIdentity = "other-adapter-v1" },
            valid with { AdapterContractDigestSha256 = new string('0', 64) },
            valid with { AuthorizationNonce = "INVALID NONCE" },
            valid with { CapabilityLifetimeMilliseconds = 0 },
            valid with { SessionTimeoutMilliseconds = CognitionQualityRecordingSessionModule.MaximumSessionTimeoutMilliseconds + 1 }
        }) Assert.Throws<ArgumentException>(() => module.Authorize(publication, provenance, invalid));
        CognitionQualityExecutionProvenance wrongLocalAdapter = CognitionQualityExecutionProvenance.ForLocal(
            "model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "other-adapter-v1");
        Assert.Throws<ArgumentException>(() => module.Authorize(publication, wrongLocalAdapter, Authorization(publication, wrongLocalAdapter, adapter)));

        Assert.Equal(new[] { "Authorize", "RecordOnceAsync" }, typeof(CognitionQualityRecordingSessionModule)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(method => method.Name).Order().ToArray());
        foreach (PropertyInfo property in typeof(CognitionQualityRecordingSessionResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.NotEqual(typeof(byte[]), property.PropertyType);
            Assert.NotEqual(typeof(ReadOnlyMemory<byte>), property.PropertyType);
            Assert.NotEqual(typeof(Memory<byte>), property.PropertyType);
        }
        Assert.True(typeof(CognitionQualityRecordingAdapter).IsAbstract);
        Assert.NotNull(typeof(CognitionQualityRecordingAdapter).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single());
        Assert.Empty(typeof(CognitionQualityRecordingAdapter).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        string publicSurface = string.Join('|', new[]
        {
            typeof(CognitionQualityRecordingSessionModule),
            typeof(CognitionQualityRecordingSessionCapability),
            typeof(CognitionQualityRecordingSessionResult),
            typeof(CognitionQualityRecordingAdapter),
            typeof(OfflineFixedResponseCognitionQualityRecordingAdapter)
        }.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)));
        foreach (string forbidden in new[] { "HttpClient", "Socket", "FileInfo", "Credential", "Payment", "Journal", "World" })
            Assert.DoesNotContain(forbidden, publicSurface, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertSubmissionTerminal(SubmissionState submission, ChargeState charge, CognitionQualityRecordingSessionOutcomeCode outcome)
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        ScriptedAdapter adapter = new((request, _) => ValueTask.FromResult(OfflineResponse(request, Responses()[0].ToArray(), submission: submission, charge: charge)));
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(13000));
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, adapter)));
        Assert.Equal(outcome, result.OutcomeCode);
        Assert.Equal(submission, result.SubmissionState);
        Assert.Equal(charge, result.ChargeState);
        Assert.Equal(0, result.CompletedSlotCount);
        Assert.Equal(1, result.TerminalSlotOrdinal);
        Assert.Null(result.Evidence);
        Assert.Equal(1, adapter.CallCount);
    }

    private static async Task AssertMalformed(Func<CognitionQualityRecordingAdapterRequest, byte[], CognitionQualityRecordingAdapterResponse> responseFactory)
    {
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance();
        byte[]? transferred = null;
        bool zeroed = false;
        ScriptedAdapter adapter = new((request, _) =>
        {
            byte[] seed = Responses()[0].ToArray();
            CognitionQualityRecordingAdapterResponse response = responseFactory(request, seed);
            FieldInfo owned = typeof(CognitionQualityRecordingResponseBuffer).GetField("_owned", BindingFlags.Instance | BindingFlags.NonPublic)!;
            transferred = (byte[])owned.GetValue(response.ResponseBuffer)!;
            FieldInfo observer = typeof(CognitionQualityRecordingResponseBuffer).GetField("_zeroObserver", BindingFlags.Instance | BindingFlags.NonPublic)!;
            observer.SetValue(response.ResponseBuffer, (Action<bool>)(value => zeroed = value));
            return ValueTask.FromResult(response);
        });
        CognitionQualityRecordingSessionModule module = new(adapter, new FixedClock(14000));
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, adapter)));
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, result.OutcomeCode);
        Assert.Equal(1, adapter.CallCount);
        Assert.Null(result.Evidence);
        Assert.NotNull(transferred);
        Assert.True(zeroed);
        Assert.All(transferred!, value => Assert.Equal(0, value));
    }

    private static CognitionQualityRecordingAdapterResponse OfflineResponse(
        CognitionQualityRecordingAdapterRequest request,
        byte[] owned,
        Action<bool>? zeroObserver = null,
        string? capabilityDigest = null,
        string? requestDigest = null,
        int? slotOrdinal = null,
        SubmissionState submission = SubmissionState.NotApplicable,
        ChargeState charge = ChargeState.NotApplicable,
        bool transportAttestation = false) => new(
            capabilityDigest ?? request.CapabilityDigestSha256,
            requestDigest ?? request.RequestDigestSha256,
            request.AdapterIdentity,
            request.AdapterContractDigestSha256,
            slotOrdinal ?? request.SlotOrdinal,
            request.ScenarioId,
            request.ObservationDigestSha256,
            request.PromptDigestSha256,
            submission,
            charge,
            transportAttestation,
            false,
            new CognitionQualityRecordingResponseBuffer(owned, zeroObserver));

    private static CognitionQualityRecordingSessionAuthorization Authorization(
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityRecordingAdapter adapter,
        string authorizationNonce = "recording-session-nonce-v1") => new(
            publication.CanonicalDigestSha256,
            publication.PromptSetDigestSha256,
            provenance.ProvenanceDigestSha256,
            adapter.AdapterIdentity,
            adapter.AdapterContractDigestSha256,
            authorizationNonce,
            500,
            5000);

    private static CognitionQualityPromptEnvelopePublication Publication() => CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
    private static CognitionQualityExecutionProvenance Provenance() => CognitionQualityExecutionProvenance.ForLocal(
        "model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, AdapterIdentity);
    private static ReadOnlyMemory<byte>[] Responses() => Enumerable.Range(1, 12)
        .Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}"))
        .ToArray();
    private static string Action(int index) => index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
    private static int Quantity(int index) => index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };

    private sealed class FixedClock : ICognitionQualityRecordingSessionClock
    {
        private long _now;
        internal FixedClock(long now) => _now = now;
        public long NowMilliseconds => Interlocked.Read(ref _now);
        internal void Advance(long milliseconds) => Interlocked.Add(ref _now, milliseconds);
    }

    private sealed class ScriptedAdapter : CognitionQualityRecordingAdapter
    {
        private readonly Func<CognitionQualityRecordingAdapterRequest, CancellationToken, ValueTask<CognitionQualityRecordingAdapterResponse>> _handler;
        private int _calls;
        internal ScriptedAdapter(Func<CognitionQualityRecordingAdapterRequest, CancellationToken, ValueTask<CognitionQualityRecordingAdapterResponse>> handler)
            : base(CognitionQualityRecordingSessionTests.AdapterIdentity, CognitionQualityRecordingSessionCanonical.Digest("offline-scripted-recording-adapter/v1")) => _handler = handler;
        internal int CallCount => Volatile.Read(ref _calls);
        internal override ValueTask<CognitionQualityRecordingAdapterResponse> AcquireOnceAsync(CognitionQualityRecordingAdapterRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return _handler(request, cancellationToken);
        }
    }

    private sealed class UnreadableOversizedMemoryManager : MemoryManager<byte>
    {
        private readonly int _length;
        private int _getSpanCalls;
        internal UnreadableOversizedMemoryManager(int length) => _length = length;
        internal ReadOnlyMemory<byte> UnreadableMemory => CreateMemory(_length);
        internal int GetSpanCallCount => Volatile.Read(ref _getSpanCalls);
        public override Span<byte> GetSpan()
        {
            Interlocked.Increment(ref _getSpanCalls);
            throw new InvalidOperationException("oversized_memory_must_not_be_read");
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new InvalidOperationException("oversized_memory_must_not_be_pinned");
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }

    private sealed class ChangingIndexerResponses : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private readonly ReadOnlyMemory<byte>[] _stable;
        private readonly ReadOnlyMemory<byte> _hostileSecondValue;
        private readonly int[] _reads;
        internal ChangingIndexerResponses(ReadOnlyMemory<byte>[] stable, ReadOnlyMemory<byte> hostileSecondValue)
        {
            _stable = stable.ToArray();
            _hostileSecondValue = hostileSecondValue;
            _reads = new int[_stable.Length];
        }
        public int Count => _stable.Length;
        public ReadOnlyMemory<byte> this[int index]
        {
            get
            {
                int read = Interlocked.Increment(ref _reads[index]);
                return index == 0 && read > 1 ? _hostileSecondValue : _stable[index];
            }
        }
        internal IReadOnlyList<int> SnapshotReadCounts() => Enumerable.Range(0, _reads.Length)
            .Select(index => Volatile.Read(ref _reads[index])).ToArray();
        public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator() => ((IEnumerable<ReadOnlyMemory<byte>>)_stable).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
