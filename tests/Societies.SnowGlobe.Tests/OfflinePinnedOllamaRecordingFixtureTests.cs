using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OfflinePinnedOllamaRecordingFixtureTests
{
    [Fact]
    public void PinnedMetadataAndExecutionProvenance_AreExactAndMetadataOnly()
    {
        using OfflinePinnedOllamaRecordingFixture fixture = new(Responses());
        OfflinePinnedOllamaRecordingFixtureProvenance pin = fixture.PinnedProvenance;

        Assert.Equal("offline-pinned-ollama-qwen35-4b-recording-fixture-v1", fixture.AdapterIdentity);
        Assert.Equal("qwen3.5-4b", pin.NormalizedModelIdentity);
        Assert.Equal("qwen3.5:4b", pin.RuntimeModelReference);
        Assert.Equal("2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd", pin.ArtifactDigestSha256);
        Assert.Equal(3_389_983_735, pin.ArtifactSizeBytes);
        Assert.Equal("gguf", pin.ArtifactFormat);
        Assert.Equal("qwen35", pin.ModelFamily);
        Assert.Equal(4_659_865_088, pin.ParameterCount);
        Assert.Equal("Q4_K_M", pin.QuantizationLevel);
        Assert.Equal("snow-globe-ollama-loopback-v1", pin.LoopbackServerIdentity);
        Assert.Equal("http://127.0.0.1:11435/", pin.CanonicalEndpoint);
        Assert.Equal(pin.CanonicalEndpoint, pin.EndpointIdentity);
        Assert.Equal("ollama-v0.32.14-sha11d7729c", pin.RuntimeProcessIdentity);
        Assert.Equal("ollama-generate-v1", pin.BenchmarkAdapterIdentity);
        Assert.Equal("snow-globe-proposal-v1", pin.BenchmarkPromptIdentity);
        Assert.Equal("9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0", pin.BenchmarkContractId);
        Assert.Equal("961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d", pin.FrozenEvidenceDigestSha256);
        LocalModelBenchmarkPlan frozenPlan = FrozenLocalBenchmarkRegistry.CreatePlan();
        Assert.Equal(pin.BenchmarkContractId, frozenPlan.ContractId);
        Assert.Equal(pin.CanonicalEndpoint, frozenPlan.Endpoint);
        Assert.Equal(pin.BenchmarkAdapterIdentity, frozenPlan.AdapterIdentity);
        Assert.Equal(pin.BenchmarkPromptIdentity, frozenPlan.PromptIdentity);
        Assert.Equal(4096, pin.ContextWindowTokens);
        Assert.Equal(96, pin.OutputTokenLimit);
        Assert.Equal("11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54", pin.RuntimeExecutableSha256);
        Assert.True(pin.IsMetadataOnly);
        Assert.False(pin.HasTransportDeliveryAttestation);
        Assert.False(pin.HasModelExecutionAttestation);
        Assert.False(fixture.HasTransportAuthority);
        Assert.False(fixture.HasModelExecutionAuthority);
        Assert.False(fixture.HasFileOrEnvironmentAuthority);
        Assert.False(fixture.HasCredentialOrPaymentAuthority);
        Assert.True(fixture.IsOfflineFixture);
        Assert.False(fixture.AutomaticRetriesAllowed);
        Assert.Equal(CognitionQualityRecordingSessionCanonical.Digest(OfflinePinnedOllamaRecordingFixture.ContractDescriptor), fixture.AdapterContractDigestSha256);
        Assert.Equal(fixture.AdapterContractDigestSha256, pin.MetadataDigestSha256);

        CognitionQualityExecutionProvenance provenance = fixture.CreatePinnedExecutionProvenance("prompt-v1");
        Assert.Equal(CognitionLane.Local, provenance.Lane);
        Assert.Equal(pin.NormalizedModelIdentity, provenance.ModelIdentity);
        Assert.Equal("sha256-" + pin.ArtifactDigestSha256, provenance.ModelRevisionIdentity);
        Assert.Equal(pin.MetadataDigestSha256, provenance.ExecutionPolicyDigestSha256);
        Assert.Equal("prompt-v1", provenance.PromptRevision);
        Assert.NotEqual(pin.BenchmarkPromptIdentity, provenance.PromptRevision);
        Assert.Equal(fixture.AdapterIdentity, provenance.LocalAdapterIdentity);
    }

    [Fact]
    public void PublicSurface_ContainsNoIoTransportOrSecretBearingTypes()
    {
        Type[] publicTypes = [typeof(OfflinePinnedOllamaRecordingFixture), typeof(OfflinePinnedOllamaRecordingFixtureProvenance)];
        foreach (Type type in publicTypes)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                AssertSafeType(field.FieldType, allowRawInput: false);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                AssertSafeType(property.PropertyType, allowRawInput: false);
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertSafeType(method.ReturnType, allowRawInput: false);
                foreach (ParameterInfo parameter in method.GetParameters()) AssertSafeType(parameter.ParameterType, allowRawInput: false);
            }
            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                foreach (ParameterInfo parameter in constructor.GetParameters())
                    AssertSafeType(parameter.ParameterType, allowRawInput: parameter.ParameterType == typeof(IReadOnlyList<ReadOnlyMemory<byte>>));
        }
    }

    [Fact]
    public void Constructor_CapturesCountAndEachResponseExactlyOnceBeforeCopying()
    {
        HostileReadOnceResponseList responses = new(Responses());
        using OfflinePinnedOllamaRecordingFixture fixture = new(responses);

        Assert.Equal(1, responses.CountReadCount);
        Assert.Equal(Enumerable.Repeat(1, 12), responses.IndexReadCounts);
        byte[][] owned = (byte[][])typeof(OfflinePinnedOllamaRecordingFixture).GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        Assert.Equal(12, owned.Length);
    }

    [Fact]
    public async Task CandidateConformance_BindsPinnedProvenanceAndExercisesMidflightCancellation()
    {
        CognitionQualityRecordingAdapterConformanceReport report = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new PinnedFixtureFactory());

        Assert.Equal(Enumerable.Repeat("pass", 11), report.ResultCodes);
        Assert.True(report.IsCoreConformant);
        Assert.True(report.IsFullyConformant);
        Assert.True(report.IsConformant);
        Assert.Equal("bound", report.BindingStatus);

        using OfflinePinnedOllamaRecordingFixture fixture = new(Responses());
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityRecordingEvidence expected = CognitionQualityRecordingEvidenceModule.Create(publication, fixture.CreatePinnedExecutionProvenance("prompt-v1"), Responses());
        Assert.Equal(expected.CanonicalDigestSha256, report.ExpectedEvidenceCanonicalDigestSha256);
        Assert.NotEqual(
            CognitionQualityRecordingEvidenceModule.Create(publication, CognitionQualityExecutionProvenance.ForLocal("fixture-model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, fixture.AdapterIdentity), Responses()).CanonicalDigestSha256,
            report.ExpectedEvidenceCanonicalDigestSha256);
    }

    [Fact]
    public async Task Session_UsesTwelveDetachedBuffersOnceAndClearsOwnedStateOnDispose()
    {
        ReadOnlyMemory<byte>[] callerResponses = Responses();
        byte[][] callerArrays = callerResponses.Select(memory =>
        {
            Assert.True(MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment));
            return segment.Array!;
        }).ToArray();
        using OfflinePinnedOllamaRecordingFixture fixture = new(callerResponses);
        for (int index = 0; index < callerArrays.Length; index++) callerArrays[index][0] ^= 0x5a;
        byte[][] expectedCaller = callerArrays.Select(bytes => bytes.ToArray()).ToArray();
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = fixture.CreatePinnedExecutionProvenance("prompt-v1");
        CognitionQualityRecordingSessionModule module = new(fixture, new CognitionQualityRecordingAdapterConformanceClock(1_000));
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture, "pinned-order-v1"));

        CognitionQualityRecordingSessionResult complete = await module.RecordOnceAsync(capability);
        CognitionQualityRecordingSessionResult reused = await module.RecordOnceAsync(capability);
        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = fixture.SnapshotRequests();
        try
        {
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, complete.OutcomeCode);
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
            Assert.Equal(12, fixture.CallCount);
            Assert.Equal(Enumerable.Range(1, 12), requests.Select(request => request.SlotOrdinal));
            Assert.Equal(publication.Slots.Select(slot => slot.ScenarioId), requests.Select(request => request.ScenarioId));
            Assert.Equal(publication.Slots.Select(slot => slot.ObservationDigestSha256), requests.Select(request => request.ObservationDigestSha256));
            Assert.Equal(CognitionQualityRecordingEvidenceModule.Create(publication, provenance, Responses()).ResponseSetDigestSha256, complete.Evidence!.ResponseSetDigestSha256);
            for (int index = 0; index < callerArrays.Length; index++) Assert.Equal(expectedCaller[index], callerArrays[index]);
        }
        finally
        {
            foreach (CognitionQualityRecordingAdapterRequest request in requests)
            {
                request.Dispose();
                Assert.All(request.PromptUtf8.ToArray(), value => Assert.Equal(0, value));
            }
        }

        CognitionQualityRecordingAdapterRequest[] ownedRequests = ((List<CognitionQualityRecordingAdapterRequest>)typeof(OfflinePinnedOllamaRecordingFixture)
            .GetField("_requests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!).ToArray();
        fixture.Dispose();
        Assert.True(fixture.IsDisposed);
        byte[][] owned = (byte[][])typeof(OfflinePinnedOllamaRecordingFixture).GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        Assert.All(owned, response => Assert.All(response, value => Assert.Equal(0, value)));
        Assert.All(ownedRequests, request => Assert.All(request.PromptUtf8.ToArray(), value => Assert.Equal(0, value)));
    }

    [Fact]
    public async Task ConstructorAndCancellation_RejectMalformedInputsAndNeverRetry()
    {
        Assert.Throws<ArgumentException>(() => new OfflinePinnedOllamaRecordingFixture(Responses()[..^1]));
        ReadOnlyMemory<byte>[] oversized = Responses();
        oversized[0] = new byte[CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfflinePinnedOllamaRecordingFixture(oversized));

        using (OfflinePinnedOllamaRecordingFixture malformedFixture = new(MalformedResponses()))
        {
            CognitionQualityPromptEnvelopePublication malformedPublication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
            CognitionQualityExecutionProvenance malformedProvenance = malformedFixture.CreatePinnedExecutionProvenance("prompt-v1");
            CognitionQualityRecordingSessionModule malformedModule = new(malformedFixture, new CognitionQualityRecordingAdapterConformanceClock(1_500));
            CognitionQualityRecordingSessionResult malformed = await malformedModule.RecordOnceAsync(malformedModule.Authorize(malformedPublication, malformedProvenance, Authorization(malformedPublication, malformedProvenance, malformedFixture, "pinned-malformed-v1")));
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, malformed.OutcomeCode);
            Assert.Equal(12, malformedFixture.CallCount);
        }

        using OfflinePinnedOllamaRecordingFixture fixture = new(Responses());
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = fixture.CreatePinnedExecutionProvenance("prompt-v1");
        CognitionQualityRecordingSessionModule module = new(fixture, new CognitionQualityRecordingAdapterConformanceClock(2_000));
        Task held = fixture.HoldNextAcquisitionForCancellationTesting();
        using CancellationTokenSource cancellation = new();
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture, "pinned-cancel-v1"));
        Task<CognitionQualityRecordingSessionResult> pending = module.RecordOnceAsync(capability, cancellation.Token).AsTask();
        await held.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        CognitionQualityRecordingSessionResult cancelled = await pending;

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Cancelled, cancelled.OutcomeCode);
        Assert.Null(cancelled.Evidence);
        Assert.Equal(0, cancelled.CompletedSlotCount);
        Assert.Equal(1, cancelled.TerminalSlotOrdinal);
        Assert.Equal(1, fixture.CallCount);
        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = fixture.SnapshotRequests();
        try { Assert.Single(requests); Assert.Equal(1, requests[0].SlotOrdinal); }
        finally { foreach (CognitionQualityRecordingAdapterRequest request in requests) request.Dispose(); }
    }

    [Fact]
    public async Task DisposeDuringHeldAcquisition_CancelsAndZerosWithoutCallerCancellation()
    {
        using OfflinePinnedOllamaRecordingFixture fixture = new(Responses());
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityExecutionProvenance provenance = fixture.CreatePinnedExecutionProvenance("prompt-v1");
        CognitionQualityRecordingSessionModule module = new(fixture, new CognitionQualityRecordingAdapterConformanceClock(2_500));
        Task held = fixture.HoldNextAcquisitionForCancellationTesting();
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture, "pinned-dispose-held-v1"));
        Task<CognitionQualityRecordingSessionResult> pending = module.RecordOnceAsync(capability).AsTask();
        await held.WaitAsync(TimeSpan.FromSeconds(2));
        CognitionQualityRecordingAdapterRequest[] ownedRequests = ((List<CognitionQualityRecordingAdapterRequest>)typeof(OfflinePinnedOllamaRecordingFixture)
            .GetField("_requests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!).ToArray();

        fixture.Dispose();
        CognitionQualityRecordingSessionResult result = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        CognitionQualityRecordingSessionResult reused = await module.RecordOnceAsync(capability);

        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.AdapterFailure, result.OutcomeCode);
        Assert.Null(result.Evidence);
        Assert.Equal(0, result.CompletedSlotCount);
        Assert.Equal(1, result.TerminalSlotOrdinal);
        Assert.Equal(SubmissionState.NotApplicable, result.SubmissionState);
        Assert.Equal(ChargeState.NotApplicable, result.ChargeState);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
        Assert.Equal(1, fixture.CallCount);
        Assert.True(fixture.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => fixture.SnapshotRequests());
        byte[][] owned = (byte[][])typeof(OfflinePinnedOllamaRecordingFixture).GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        Assert.All(owned, response => Assert.All(response, value => Assert.Equal(0, value)));
        Assert.All(ownedRequests, request => Assert.All(request.PromptUtf8.ToArray(), value => Assert.Equal(0, value)));
    }

    private static void AssertSafeType(Type type, bool allowRawInput)
    {
        if (type.IsByRef || type.IsPointer)
        {
            AssertSafeType(type.GetElementType()!, allowRawInput);
            return;
        }
        if (type.IsArray)
        {
            AssertSafeType(type.GetElementType()!, allowRawInput);
            return;
        }
        if (type == typeof(byte) || type == typeof(Memory<byte>) || type == typeof(ReadOnlyMemory<byte>))
        {
            Assert.True(allowRawInput, $"Raw-bearing public surface: {type}");
            return;
        }
        Type candidate = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        string? ns = candidate.Namespace;
        Assert.DoesNotContain("System.IO", ns, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net", ns, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics", ns, StringComparison.Ordinal);
        Assert.NotEqual(typeof(Stream), candidate);
        Assert.NotEqual(typeof(Uri), candidate);
        foreach (Type argument in type.GetGenericArguments()) AssertSafeType(argument, allowRawInput);
    }

    private static CognitionQualityRecordingSessionAuthorization Authorization(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, OfflinePinnedOllamaRecordingFixture fixture, string nonce) => new(publication.CanonicalDigestSha256, publication.PromptSetDigestSha256, provenance.ProvenanceDigestSha256, fixture.AdapterIdentity, fixture.AdapterContractDigestSha256, nonce, 500, 5_000);
    private static ReadOnlyMemory<byte>[] Responses() => Enumerable.Range(1, 12).Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}")).ToArray();
    private static ReadOnlyMemory<byte>[] MalformedResponses()
    {
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = new byte[] { 0xff };
        return responses;
    }
    private static string Action(int index) => index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
    private static int Quantity(int index) => index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private sealed class HostileReadOnceResponseList : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private readonly ReadOnlyMemory<byte>[] _responses;
        private readonly int[] _indexReadCounts;
        private int _countReadCount;

        internal HostileReadOnceResponseList(ReadOnlyMemory<byte>[] responses)
        {
            _responses = responses;
            _indexReadCounts = new int[responses.Length];
        }

        public int Count => Interlocked.Increment(ref _countReadCount) == 1 ? _responses.Length : int.MaxValue;
        public int CountReadCount => Volatile.Read(ref _countReadCount);
        public IReadOnlyList<int> IndexReadCounts => Array.AsReadOnly(_indexReadCounts.ToArray());
        public ReadOnlyMemory<byte> this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_responses.Length || Interlocked.Increment(ref _indexReadCounts[index]) != 1)
                    throw new InvalidOperationException("response_index_read_more_than_once");
                return _responses[index];
            }
        }

        public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator() => throw new InvalidOperationException("enumeration_not_allowed");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PinnedFixtureFactory : ICognitionQualityRecordingAdapterConformanceFixtureFactory
    {
        public ICognitionQualityRecordingAdapterConformanceFixture Create(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => new PinnedFixture(callerResponses);
    }

    private sealed class PinnedFixture : ICognitionQualityRecordingAdapterConformanceFixture, ICognitionQualityRecordingAdapterMidflightCancellationFixture
    {
        private readonly OfflinePinnedOllamaRecordingFixture _adapter;
        internal PinnedFixture(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => _adapter = new OfflinePinnedOllamaRecordingFixture(callerResponses);
        public CognitionQualityRecordingAdapter Adapter => _adapter;
        public int CallCount => _adapter.CallCount;
        public IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests() => _adapter.SnapshotRequests();
        public void DisposeAdapter() => _adapter.Dispose();
        public CognitionQualityExecutionProvenance CreateExecutionProvenance(string promptRevision) => _adapter.CreatePinnedExecutionProvenance(promptRevision);
        public ICognitionQualityRecordingAdapterMidflightCancellationFixture? MidflightCancellationFixture => this;
        public void Dispose() => _adapter.Dispose();

        public async Task<CognitionQualityRecordingSessionResult> RecordWithMidflightCancellationAsync(CognitionQualityRecordingSessionModule module, CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, CognitionQualityRecordingSessionAuthorization authorization)
        {
            Task held = _adapter.HoldNextAcquisitionForCancellationTesting();
            using CancellationTokenSource cancellation = new();
            CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, authorization);
            Task<CognitionQualityRecordingSessionResult> pending = module.RecordOnceAsync(capability, cancellation.Token).AsTask();
            await held.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            return await pending;
        }
    }
}
