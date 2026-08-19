using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

/// <summary>Small test-only factory for an offline fixture; it has no transport or provider inputs.</summary>
internal interface ICognitionQualityRecordingAdapterConformanceFixtureFactory
{
    ICognitionQualityRecordingAdapterConformanceFixture Create(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses);
}

/// <summary>Test-only observable seam used by the reusable conformance harness.</summary>
internal interface ICognitionQualityRecordingAdapterConformanceFixture : IDisposable
{
    CognitionQualityRecordingAdapter Adapter { get; }
    int CallCount { get; }
    IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests();
    void DisposeAdapter();
    ICognitionQualityRecordingAdapterMidflightCancellationFixture? MidflightCancellationFixture { get; }
}

/// <summary>Optional real cancellation exercise seam for a future asynchronous offline fixture.</summary>
internal interface ICognitionQualityRecordingAdapterMidflightCancellationFixture
{
    Task<CognitionQualityRecordingSessionResult> RecordWithMidflightCancellationAsync(
        CognitionQualityRecordingSessionModule module,
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityRecordingSessionAuthorization authorization);
}

internal sealed class CognitionQualityRecordingAdapterConformanceClock : ICognitionQualityRecordingSessionClock
{
    private long _now;
    internal CognitionQualityRecordingAdapterConformanceClock(long nowMilliseconds) => _now = nowMilliseconds;
    public long NowMilliseconds => Interlocked.Read(ref _now);
    internal void Advance(long milliseconds) => Interlocked.Add(ref _now, milliseconds);
}

/// <summary>Bounded, detached, raw-free results for the test-only adapter conformance suite.</summary>
internal sealed class CognitionQualityRecordingAdapterConformanceReport
{
    internal const int MaximumResultBytes = 4096;
    private readonly string[] _checkIds;
    private readonly string[] _resultCodes;
    private readonly byte[] _resultUtf8;

    internal CognitionQualityRecordingAdapterConformanceReport(ConformanceBinding? binding, IReadOnlyList<string> checkIds, IReadOnlyList<string> resultCodes)
    {
        if (checkIds.Count != resultCodes.Count || checkIds.Count is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(checkIds));
        _checkIds = checkIds.ToArray();
        _resultCodes = resultCodes.ToArray();
        if (_checkIds.Any(value => !IsCode(value)) || _resultCodes.Any(value => !IsCode(value)))
            throw new ArgumentException("Conformance report codes must be canonical.");
        BindingStatus = binding is null ? "binding_unavailable" : "bound";
        AdapterIdentityDigestSha256 = binding?.AdapterIdentityDigestSha256 ?? new string('0', 64);
        AdapterContractDigestSha256 = binding?.AdapterContractDigestSha256 ?? new string('0', 64);
        ExpectedEvidenceCanonicalDigestSha256 = binding?.ExpectedEvidenceCanonicalDigestSha256 ?? new string('0', 64);
        if (!IsDigest(AdapterIdentityDigestSha256) || !IsDigest(AdapterContractDigestSha256) || !IsDigest(ExpectedEvidenceCanonicalDigestSha256))
            throw new ArgumentException("Conformance report binding is invalid.");
        _resultUtf8 = Encoding.UTF8.GetBytes(string.Join('\n', new[]
        {
            $"binding_status|{BindingStatus}",
            $"adapter_identity_digest_sha256|{AdapterIdentityDigestSha256}",
            $"adapter_contract_digest_sha256|{AdapterContractDigestSha256}",
            $"expected_evidence_canonical_digest_sha256|{ExpectedEvidenceCanonicalDigestSha256}"
        }.Concat(_checkIds.Select((id, index) => $"{index:D2}|{id}|{_resultCodes[index]}"))));
        if (_resultUtf8.Length is < 1 or > MaximumResultBytes)
            throw new InvalidOperationException("Conformance report is not bounded.");
        DigestSha256 = Convert.ToHexString(SHA256.HashData(_resultUtf8)).ToLowerInvariant();
    }

    public IReadOnlyList<string> CheckIds => Array.AsReadOnly(_checkIds.ToArray());
    public IReadOnlyList<string> ResultCodes => Array.AsReadOnly(_resultCodes.ToArray());
    public ReadOnlyMemory<byte> ResultUtf8 => _resultUtf8.ToArray();
    public string BindingStatus { get; }
    public string AdapterIdentityDigestSha256 { get; }
    public string AdapterContractDigestSha256 { get; }
    public string ExpectedEvidenceCanonicalDigestSha256 { get; }
    public string DigestSha256 { get; }
    public bool IsCoreConformant => BindingStatus == "bound" && _resultCodes.Take(_resultCodes.Length - 1).All(code => code == "pass");
    public bool IsFullyConformant => IsCoreConformant && _resultCodes[^1] == "pass";
    public bool IsConformant => IsFullyConformant;

    private static bool IsCode(string value) => value.Length is > 0 and <= 96
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    private static bool IsDigest(string value) => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

internal sealed record ConformanceBinding(string AdapterIdentityDigestSha256, string AdapterContractDigestSha256, string ExpectedEvidenceCanonicalDigestSha256);

/// <summary>
/// Reusable conformance evaluator for a reviewed adapter's deterministic offline fixture. It performs
/// no I/O and returns only canonical check IDs/codes and a digest; prompts and recorded responses stay local.
/// </summary>
internal static class CognitionQualityRecordingAdapterConformanceHarness
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private static readonly string[] OrderedCheckIds =
    [
        "adapter_contract_and_attestations",
        "complete_evidence_equivalence",
        "request_sequence_and_one_shot",
        "nonce_replay_closed",
        "pre_cancel_spends_authority",
        "expiry_spends_authority",
        "distinct_nonce_sessions",
        "caller_input_detached",
        "disposed_adapter_closed",
        "public_surfaces_raw_free",
        "midflight_cancellation"
    ];

    internal static async Task<CognitionQualityRecordingAdapterConformanceReport> EvaluateAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory? factory)
    {
        ConformanceBinding? binding = TryCreateBinding(factory);
        List<string> results = new(OrderedCheckIds.Length);
        await AddCheckAsync(results, factory, binding, "adapter_contract_and_attestations", AdapterContractAndAttestationsAsync);
        await AddCheckAsync(results, factory, binding, "complete_evidence_equivalence", CompleteEvidenceEquivalenceAsync);
        await AddCheckAsync(results, factory, binding, "request_sequence_and_one_shot", RequestSequenceAndOneShotAsync);
        await AddCheckAsync(results, factory, binding, "nonce_replay_closed", NonceReplayClosedAsync);
        await AddCheckAsync(results, factory, binding, "pre_cancel_spends_authority", PreCancelSpendsAuthorityAsync);
        await AddCheckAsync(results, factory, binding, "expiry_spends_authority", ExpirySpendsAuthorityAsync);
        await AddCheckAsync(results, factory, binding, "distinct_nonce_sessions", DistinctNonceSessionsAsync);
        await AddCheckAsync(results, factory, binding, "caller_input_detached", CallerInputDetachedAsync);
        await AddCheckAsync(results, factory, binding, "disposed_adapter_closed", DisposedAdapterClosedAsync);
        AddPublicSurfaceCheck(results);
        await AddMidflightCancellationCheckAsync(results, factory, binding);
        return new CognitionQualityRecordingAdapterConformanceReport(binding, OrderedCheckIds, results);
    }

    private static async Task AddCheckAsync(
        ICollection<string> results,
        ICognitionQualityRecordingAdapterConformanceFixtureFactory? factory,
        ConformanceBinding? binding,
        string checkId,
        Func<ICognitionQualityRecordingAdapterConformanceFixtureFactory, ConformanceBinding, Task> assertion)
    {
        try
        {
            if (factory is null || binding is null) throw new InvalidOperationException();
            await assertion(factory, binding);
            results.Add("pass");
        }
        catch
        {
            results.Add(checkId == "adapter_contract_and_attestations" ? "fixture_failure" : "closed_failure");
        }
    }

    private static void AddPublicSurfaceCheck(ICollection<string> results)
    {
        try
        {
            foreach (PropertyInfo property in typeof(CognitionQualityRecordingSessionResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(Memory<byte>) || property.PropertyType == typeof(ReadOnlyMemory<byte>))
                    throw new InvalidOperationException();
            foreach (PropertyInfo property in typeof(CognitionQualityRecordingAdapterConformanceReport).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.Name != nameof(CognitionQualityRecordingAdapterConformanceReport.ResultUtf8)
                    && (property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(Memory<byte>) || property.PropertyType == typeof(ReadOnlyMemory<byte>)))
                    throw new InvalidOperationException();
            results.Add("pass");
        }
        catch { results.Add("closed_failure"); }
    }

    private static async Task AddMidflightCancellationCheckAsync(ICollection<string> results, ICognitionQualityRecordingAdapterConformanceFixtureFactory? factory, ConformanceBinding? binding)
    {
        try
        {
            if (factory is null || binding is null) throw new InvalidOperationException();
            using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
            if (fixture.MidflightCancellationFixture is null)
            {
                results.Add("not_exercised_by_fixed_fixture");
                return;
            }
            CognitionQualityPromptEnvelopePublication publication = Publication();
            CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
            CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(9000));
            CognitionQualityRecordingSessionResult result = await fixture.MidflightCancellationFixture.RecordWithMidflightCancellationAsync(module, publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-midflight-v1"));
            if (result.OutcomeCode != CognitionQualityRecordingSessionOutcomeCode.Cancelled || result.Evidence is not null || result.CompletedSlotCount != 0) throw new InvalidOperationException();
            results.Add("pass");
        }
        catch { results.Add("closed_failure"); }
    }

    private static Task AdapterContractAndAttestationsAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityRecordingAdapter adapter = fixture.Adapter;
        Assert.True(SnowGlobeInferenceIdentity.IsCanonical(adapter.AdapterIdentity));
        Assert.True(IsDigest(adapter.AdapterContractDigestSha256));
        Assert.True(adapter.IsOfflineFixture);
        Assert.False(adapter.HasTransportDeliveryAttestation);
        Assert.False(adapter.HasModelExecutionAttestation);
        Assert.False(adapter.AutomaticRetriesAllowed);
        Assert.Equal(binding.AdapterIdentityDigestSha256, DigestIdentity(adapter.AdapterIdentity));
        Assert.Equal(binding.AdapterContractDigestSha256, adapter.AdapterContractDigestSha256);
        return Task.CompletedTask;
    }

    private static async Task CompleteEvidenceEquivalenceAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out ReadOnlyMemory<byte>[] callerResponses);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(1000));
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-complete-v1")));
        CognitionQualityRecordingEvidence direct = CognitionQualityRecordingEvidenceModule.Create(publication, provenance, CanonicalResponses());
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode);
        Assert.NotNull(result.Evidence);
        Assert.Equal(direct.CanonicalDigestSha256, result.Evidence!.CanonicalDigestSha256);
        Assert.Equal(binding.ExpectedEvidenceCanonicalDigestSha256, result.Evidence.CanonicalDigestSha256);
        Assert.Equal(direct.ResponseSetDigestSha256, result.Evidence.ResponseSetDigestSha256);
        Assert.Equal(12, result.CompletedSlotCount);
        Assert.Null(result.TerminalSlotOrdinal);
        Assert.Equal(12, fixture.CallCount);
        Assert.Equal(12, callerResponses.Length);
    }

    private static async Task RequestSequenceAndOneShotAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(2000));
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-sequence-v1"));
        CognitionQualityRecordingSessionResult complete = await module.RecordOnceAsync(capability);
        int calls = fixture.CallCount;
        CognitionQualityRecordingSessionResult reused = await module.RecordOnceAsync(capability);
        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = fixture.SnapshotRequests();
        try
        {
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, complete.OutcomeCode);
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
            Assert.Equal(calls, fixture.CallCount);
            Assert.Equal(12, requests.Count);
            for (int index = 0; index < requests.Count; index++)
            {
                CognitionQualityRecordingAdapterRequest request = requests[index];
                CognitionQualityPromptEnvelopeSlot slot = publication.Slots[index];
                Assert.Equal(index + 1, request.SlotOrdinal);
                Assert.Equal(1, request.AttemptNumber);
                Assert.Equal(capability.CapabilityDigestSha256, request.CapabilityDigestSha256);
                Assert.Equal(capability.AdapterIdentity, request.AdapterIdentity);
                Assert.Equal(capability.AdapterContractDigestSha256, request.AdapterContractDigestSha256);
                Assert.Equal(slot.ScenarioId, request.ScenarioId);
                Assert.Equal(slot.ObservationDigestSha256, request.ObservationDigestSha256);
                Assert.Equal(slot.PromptDigestSha256, request.PromptDigestSha256);
            }
        }
        finally { DisposeSnapshotRequests(requests); }
    }

    private static Task NonceReplayClosedAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(3000));
        CognitionQualityRecordingSessionAuthorization authorization = Authorization(publication, provenance, fixture.Adapter, "conformance-nonce-v1");
        _ = module.Authorize(publication, provenance, authorization);
        CognitionQualityRecordingAuthorizationException sequential = Assert.Throws<CognitionQualityRecordingAuthorizationException>(() => module.Authorize(publication, provenance, authorization));
        Assert.Equal(CognitionQualityRecordingAuthorizationFailureCode.NonceReused, sequential.Code);
        string[] outcomes = Task.WhenAll(Enumerable.Range(0, 8).Select(ignored => Task.Run(() =>
        {
            try { _ = module.Authorize(publication, provenance, authorization); return "unexpected"; }
            catch (CognitionQualityRecordingAuthorizationException exception) { return exception.Code.ToString(); }
        }))).GetAwaiter().GetResult();
        Assert.All(outcomes, outcome => Assert.Equal(nameof(CognitionQualityRecordingAuthorizationFailureCode.NonceReused), outcome));
        Assert.Equal(0, fixture.CallCount);
        return Task.CompletedTask;
    }

    private static async Task PreCancelSpendsAuthorityAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(4000));
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-cancel-v1"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        CognitionQualityRecordingSessionResult cancelled = await module.RecordOnceAsync(capability, cancellation.Token);
        CognitionQualityRecordingSessionResult reused = await module.RecordOnceAsync(capability);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Cancelled, cancelled.OutcomeCode);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
        Assert.Equal(0, fixture.CallCount);
    }

    private static async Task ExpirySpendsAuthorityAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingAdapterConformanceClock clock = new(5000);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, clock);
        CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-expiry-v1", 10));
        clock.Advance(11);
        CognitionQualityRecordingSessionResult expired = await module.RecordOnceAsync(capability);
        CognitionQualityRecordingSessionResult reused = await module.RecordOnceAsync(capability);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityExpired, expired.OutcomeCode);
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, reused.OutcomeCode);
        Assert.Equal(0, fixture.CallCount);
    }

    private static async Task DistinctNonceSessionsAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(6000));
        CognitionQualityRecordingSessionCapability first = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-distinct-a-v1"));
        CognitionQualityRecordingSessionCapability second = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-distinct-b-v1"));
        CognitionQualityRecordingSessionResult[] results = await Task.WhenAll(module.RecordOnceAsync(first).AsTask(), module.RecordOnceAsync(second).AsTask());
        CognitionQualityRecordingSessionCapability third = module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-distinct-c-v1"));
        CognitionQualityRecordingSessionResult sequential = await module.RecordOnceAsync(third);
        IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests = fixture.SnapshotRequests();
        try
        {
            Assert.All(results, result => Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode));
            Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, sequential.OutcomeCode);
            Assert.Equal(36, fixture.CallCount);
            foreach (string capabilityDigest in new[] { first.CapabilityDigestSha256, second.CapabilityDigestSha256, third.CapabilityDigestSha256 })
                Assert.Equal(Enumerable.Range(1, 12), requests.Where(request => request.CapabilityDigestSha256 == capabilityDigest).Select(request => request.SlotOrdinal));
        }
        finally { DisposeSnapshotRequests(requests); }
    }

    private static async Task CallerInputDetachedAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out ReadOnlyMemory<byte>[] callerResponses);
        byte[][] mutable = callerResponses.Select(memory =>
        {
            Assert.True(MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment));
            Assert.NotNull(segment.Array);
            return segment.Array!;
        }).ToArray();
        for (int index = 0; index < mutable.Length; index++) mutable[index][0] ^= 0x5a;
        byte[][] changedCaller = mutable.Select(value => value.ToArray()).ToArray();
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(7000));
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-detach-v1")));
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.Complete, result.OutcomeCode);
        Assert.Equal(CognitionQualityRecordingEvidenceModule.Create(publication, provenance, CanonicalResponses()).ResponseSetDigestSha256, result.Evidence!.ResponseSetDigestSha256);
        for (int index = 0; index < mutable.Length; index++) Assert.Equal(changedCaller[index], mutable[index]);
    }

    private static async Task DisposedAdapterClosedAsync(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding)
    {
        using ICognitionQualityRecordingAdapterConformanceFixture fixture = Create(factory, binding, out _);
        CognitionQualityPromptEnvelopePublication publication = Publication();
        CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
        CognitionQualityRecordingSessionModule module = new(fixture.Adapter, new CognitionQualityRecordingAdapterConformanceClock(8000));
        fixture.DisposeAdapter();
        CognitionQualityRecordingSessionResult result = await module.RecordOnceAsync(module.Authorize(publication, provenance, Authorization(publication, provenance, fixture.Adapter, "conformance-dispose-v1")));
        Assert.Equal(CognitionQualityRecordingSessionOutcomeCode.AdapterFailure, result.OutcomeCode);
        Assert.Null(result.Evidence);
        Assert.Equal(0, fixture.CallCount);
    }

    private static ConformanceBinding? TryCreateBinding(ICognitionQualityRecordingAdapterConformanceFixtureFactory? factory)
    {
        try
        {
            if (factory is null) return null;
            using ICognitionQualityRecordingAdapterConformanceFixture fixture = factory.Create(CanonicalResponses());
            if (fixture is null || !SnowGlobeInferenceIdentity.IsCanonical(fixture.Adapter.AdapterIdentity) || !IsDigest(fixture.Adapter.AdapterContractDigestSha256)) return null;
            CognitionQualityPromptEnvelopePublication publication = Publication();
            CognitionQualityExecutionProvenance provenance = Provenance(fixture.Adapter);
            CognitionQualityRecordingEvidence expected = CognitionQualityRecordingEvidenceModule.Create(publication, provenance, CanonicalResponses());
            return new ConformanceBinding(DigestIdentity(fixture.Adapter.AdapterIdentity), fixture.Adapter.AdapterContractDigestSha256, expected.CanonicalDigestSha256);
        }
        catch { return null; }
    }

    private static ICognitionQualityRecordingAdapterConformanceFixture Create(ICognitionQualityRecordingAdapterConformanceFixtureFactory factory, ConformanceBinding binding, out ReadOnlyMemory<byte>[] callerResponses)
    {
        callerResponses = CanonicalResponses();
        ICognitionQualityRecordingAdapterConformanceFixture fixture = factory.Create(callerResponses);
        if (fixture is null) throw new InvalidOperationException();
        if (!string.Equals(DigestIdentity(fixture.Adapter.AdapterIdentity), binding.AdapterIdentityDigestSha256, StringComparison.Ordinal)
            || !string.Equals(fixture.Adapter.AdapterContractDigestSha256, binding.AdapterContractDigestSha256, StringComparison.Ordinal))
        {
            fixture.Dispose();
            throw new InvalidOperationException();
        }
        return fixture;
    }

    private static void DisposeSnapshotRequests(IReadOnlyList<CognitionQualityRecordingAdapterRequest> requests)
    {
        foreach (CognitionQualityRecordingAdapterRequest request in requests)
        {
            request.Dispose();
            Assert.All(request.PromptUtf8.ToArray(), value => Assert.Equal(0, value));
        }
    }

    private static CognitionQualityPromptEnvelopePublication Publication() => CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
    private static CognitionQualityExecutionProvenance Provenance(CognitionQualityRecordingAdapter adapter) => CognitionQualityExecutionProvenance.ForLocal("fixture-model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, adapter.AdapterIdentity);
    private static CognitionQualityRecordingSessionAuthorization Authorization(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, CognitionQualityRecordingAdapter adapter, string nonce, int lifetimeMilliseconds = 500) => new(publication.CanonicalDigestSha256, publication.PromptSetDigestSha256, provenance.ProvenanceDigestSha256, adapter.AdapterIdentity, adapter.AdapterContractDigestSha256, nonce, lifetimeMilliseconds, 5000);
    private static ReadOnlyMemory<byte>[] CanonicalResponses() => Enumerable.Range(1, 12).Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}")).ToArray();
    private static string Action(int index) => index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
    private static int Quantity(int index) => index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
    private static bool IsDigest(string value) => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string DigestIdentity(string identity) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
}
