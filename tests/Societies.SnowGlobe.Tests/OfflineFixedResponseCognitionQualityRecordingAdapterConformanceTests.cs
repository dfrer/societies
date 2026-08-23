using System.Text;
using System.Security.Cryptography;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OfflineFixedResponseCognitionQualityRecordingAdapterConformanceTests
{
    [Fact]
    public async Task FixedFixture_ProducesTheCanonicalConformantReport()
    {
        CognitionQualityRecordingAdapterConformanceReport report = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new FixedFixtureFactory());

        Assert.Equal(new[]
        {
            "adapter_contract_and_attestations", "complete_evidence_equivalence", "request_sequence_and_one_shot",
            "nonce_replay_closed", "pre_cancel_spends_authority", "expiry_spends_authority", "distinct_nonce_sessions",
            "caller_input_detached", "disposed_adapter_closed", "public_surfaces_raw_free", "midflight_cancellation"
        }, report.CheckIds);
        Assert.Equal(Enumerable.Repeat("pass", 10).Append("not_exercised_by_fixed_fixture"), report.ResultCodes);
        Assert.True(report.IsCoreConformant);
        Assert.False(report.IsFullyConformant);
        Assert.False(report.IsConformant);
        Assert.Equal("bound", report.BindingStatus);
        Assert.Matches("^[0-9a-f]{64}$", report.AdapterIdentityDigestSha256);
        Assert.Matches("^[0-9a-f]{64}$", report.AdapterContractDigestSha256);
        Assert.Matches("^[0-9a-f]{64}$", report.ExpectedEvidenceCanonicalDigestSha256);
        Assert.InRange(report.ResultUtf8.Length, 1, CognitionQualityRecordingAdapterConformanceReport.MaximumResultBytes);
        string reportText = Encoding.UTF8.GetString(report.ResultUtf8.Span);
        Assert.DoesNotContain("agent_id", reportText, StringComparison.Ordinal);
        Assert.DoesNotContain("GatherWood", reportText, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-model", reportText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentEvaluations_AreDeterministicDetachedAndBounded()
    {
        Task<CognitionQualityRecordingAdapterConformanceReport>[] evaluations = Enumerable.Range(0, 8)
            .Select(_ => CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new FixedFixtureFactory())).ToArray();
        CognitionQualityRecordingAdapterConformanceReport[] reports = await Task.WhenAll(evaluations);
        CognitionQualityRecordingAdapterConformanceReport first = reports[0];
        Assert.All(reports, report =>
        {
            Assert.Equal(first.CheckIds, report.CheckIds);
            Assert.Equal(first.ResultCodes, report.ResultCodes);
            Assert.Equal(first.DigestSha256, report.DigestSha256);
            Assert.Equal(first.ResultUtf8.ToArray(), report.ResultUtf8.ToArray());
        });
        byte[] detached = first.ResultUtf8.ToArray();
        detached[0] ^= 0x5a;
        Assert.NotEqual(detached, first.ResultUtf8.ToArray());
    }

    [Fact]
    public async Task CandidateBinding_PreventsDifferentCanonicalAdaptersFromSharingReportEvidence()
    {
        CognitionQualityRecordingAdapterConformanceReport first = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new FixedFixtureFactory("adapter-v1"));
        CognitionQualityRecordingAdapterConformanceReport second = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new FixedFixtureFactory("adapter-v2"));

        Assert.True(first.IsCoreConformant);
        Assert.True(second.IsCoreConformant);
        Assert.NotEqual(first.AdapterIdentityDigestSha256, second.AdapterIdentityDigestSha256);
        Assert.NotEqual(first.ExpectedEvidenceCanonicalDigestSha256, second.ExpectedEvidenceCanonicalDigestSha256);
        Assert.NotEqual(first.DigestSha256, second.DigestSha256);
        Assert.NotEqual(first.ResultUtf8.ToArray(), second.ResultUtf8.ToArray());
    }

    [Fact]
    public async Task AsyncFixture_ExercisesMidflightCancellationAndCanBecomeFullyConformant()
    {
        CognitionQualityRecordingAdapterConformanceReport report = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new AsyncCancellationFixtureFactory());

        Assert.Equal("pass", report.ResultCodes[^1]);
        Assert.True(report.IsCoreConformant);
        Assert.True(report.IsFullyConformant);
        Assert.True(report.IsConformant);
    }

    [Fact]
    public async Task BadFactory_YieldsClosedFailureReportWithoutThrowingOrEchoing()
    {
        CognitionQualityRecordingAdapterConformanceReport report = await CognitionQualityRecordingAdapterConformanceHarness.EvaluateAsync(new ThrowingFactory());

        Assert.Equal("fixture_failure", report.ResultCodes[0]);
        Assert.All(report.ResultCodes.Skip(1).Take(8), code => Assert.Equal("closed_failure", code));
        Assert.Equal("closed_failure", report.ResultCodes[^1]);
        Assert.False(report.IsConformant);
        string text = Encoding.UTF8.GetString(report.ResultUtf8.Span);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bad fixture", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedFixtureFactory : ICognitionQualityRecordingAdapterConformanceFixtureFactory
    {
        private readonly string _identity;
        internal FixedFixtureFactory(string identity = "adapter-v1") => _identity = identity;
        public ICognitionQualityRecordingAdapterConformanceFixture Create(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => new FixedFixture(_identity, callerResponses);
    }

    private sealed class ThrowingFactory : ICognitionQualityRecordingAdapterConformanceFixtureFactory
    {
        public ICognitionQualityRecordingAdapterConformanceFixture Create(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => throw new InvalidOperationException("bad fixture secret");
    }

    private sealed class AsyncCancellationFixtureFactory : ICognitionQualityRecordingAdapterConformanceFixtureFactory
    {
        public ICognitionQualityRecordingAdapterConformanceFixture Create(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => new AsyncCancellationFixture(callerResponses);
    }

    private sealed class FixedFixture : ICognitionQualityRecordingAdapterConformanceFixture
    {
        private readonly OfflineFixedResponseCognitionQualityRecordingAdapter _adapter;
        internal FixedFixture(string identity, IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => _adapter = new OfflineFixedResponseCognitionQualityRecordingAdapter(identity, callerResponses);
        public CognitionQualityRecordingAdapter Adapter => _adapter;
        public int CallCount => _adapter.CallCount;
        public IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests() => _adapter.SnapshotRequests();
        public void DisposeAdapter() => _adapter.Dispose();
        public ICognitionQualityRecordingAdapterMidflightCancellationFixture? MidflightCancellationFixture => null;
        public void Dispose() => _adapter.Dispose();
    }

    private sealed class AsyncCancellationFixture : ICognitionQualityRecordingAdapterConformanceFixture, ICognitionQualityRecordingAdapterMidflightCancellationFixture
    {
        private readonly AsyncFixedAdapter _adapter;
        internal AsyncCancellationFixture(IReadOnlyList<ReadOnlyMemory<byte>> callerResponses) => _adapter = new AsyncFixedAdapter("adapter-v1", callerResponses);
        public CognitionQualityRecordingAdapter Adapter => _adapter;
        public int CallCount => _adapter.CallCount;
        public IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests() => _adapter.SnapshotRequests();
        public void DisposeAdapter() => _adapter.Dispose();
        public ICognitionQualityRecordingAdapterMidflightCancellationFixture? MidflightCancellationFixture => this;
        public void Dispose() => _adapter.Dispose();

        public async Task<CognitionQualityRecordingSessionResult> RecordWithMidflightCancellationAsync(
            CognitionQualityRecordingSessionModule module,
            CognitionQualityPromptEnvelopePublication publication,
            CognitionQualityExecutionProvenance provenance,
            CognitionQualityRecordingSessionAuthorization authorization)
        {
            _adapter.HoldNextAcquisition();
            using CancellationTokenSource cancellation = new();
            CognitionQualityRecordingSessionCapability capability = module.Authorize(publication, provenance, authorization);
            Task<CognitionQualityRecordingSessionResult> pending = module.RecordOnceAsync(capability, cancellation.Token).AsTask();
            await _adapter.WaitForHeldAcquisitionAsync();
            cancellation.Cancel();
            return await pending;
        }
    }

    private sealed class AsyncFixedAdapter : CognitionQualityRecordingAdapter, IDisposable
    {
        private const string Descriptor = "test-async-fixed-recording-adapter/v1|one-call-per-slot|no-retry|no-io";
        private readonly byte[][] _responses;
        private readonly object _gate = new();
        private readonly List<CognitionQualityRecordingAdapterRequest> _requests = new();
        private TaskCompletionSource<bool> _heldAcquisition = NewSignal();
        private int _holdNext;
        private int _calls;
        private bool _disposed;

        internal AsyncFixedAdapter(string identity, IReadOnlyList<ReadOnlyMemory<byte>> responses)
            : base(identity, CognitionQualityRecordingSessionCanonical.Digest(Descriptor)) => _responses = responses.Select(response => response.ToArray()).ToArray();

        internal int CallCount => Volatile.Read(ref _calls);

        internal void HoldNextAcquisition()
        {
            _heldAcquisition = NewSignal();
            Interlocked.Exchange(ref _holdNext, 1);
        }

        internal Task WaitForHeldAcquisitionAsync() => _heldAcquisition.Task.WaitAsync(TimeSpan.FromSeconds(2));

        internal IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests()
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Array.AsReadOnly(_requests.Select(request => request.Detach()).ToArray());
            }
        }

        internal override async ValueTask<CognitionQualityRecordingAdapterResponse> AcquireOnceAsync(CognitionQualityRecordingAdapterRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? owned = null;
            try
            {
                lock (_gate)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    owned = _responses[request.SlotOrdinal - 1].ToArray();
                    _requests.Add(request.Detach());
                    Interlocked.Increment(ref _calls);
                }
                if (Interlocked.Exchange(ref _holdNext, 0) != 0)
                {
                    _heldAcquisition.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                CognitionQualityRecordingAdapterResponse response = CognitionQualityRecordingAdapterResponse.ForOfflineSuccess(request, AdapterIdentity, AdapterContractDigestSha256, new CognitionQualityRecordingResponseBuffer(owned));
                owned = null;
                return response;
            }
            finally
            {
                if (owned is not null) CryptographicOperations.ZeroMemory(owned);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (byte[] response in _responses) CryptographicOperations.ZeroMemory(response);
                foreach (CognitionQualityRecordingAdapterRequest request in _requests) request.Dispose();
                _requests.Clear();
            }
        }

        private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
