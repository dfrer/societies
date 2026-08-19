using System.Security.Cryptography;

namespace Societies.SnowGlobe;

/// <summary>
/// Immutable metadata for the one recorded local-model cell represented by
/// <see cref="OfflinePinnedOllamaRecordingFixture"/>. These values describe a prior recording
/// target only; they neither locate nor execute a runtime.
/// </summary>
public sealed class OfflinePinnedOllamaRecordingFixtureProvenance
{
    internal OfflinePinnedOllamaRecordingFixtureProvenance() { }

    public string NormalizedModelIdentity => "qwen3.5-4b";
    public string RuntimeModelReference => "qwen3.5:4b";
    public string ArtifactDigestSha256 => "2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd";
    public long ArtifactSizeBytes => 3_389_983_735;
    public string ArtifactFormat => "gguf";
    public string ModelFamily => "qwen35";
    public long ParameterCount => 4_659_865_088;
    public string QuantizationLevel => "Q4_K_M";
    public string LoopbackServerIdentity => "snow-globe-ollama-loopback-v1";
    public string CanonicalEndpoint => "http://127.0.0.1:11435/";
    public string EndpointIdentity => CanonicalEndpoint;
    public string RuntimeProcessIdentity => "ollama-v0.32.14-sha11d7729c";
    public string BenchmarkAdapterIdentity => "ollama-generate-v1";
    public string BenchmarkPromptIdentity => "snow-globe-proposal-v1";
    public string BenchmarkContractId => "9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0";
    public string FrozenEvidenceDigestSha256 => "961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d";
    public int ContextWindowTokens => 4096;
    public int OutputTokenLimit => 96;
    public string RuntimeExecutableSha256 => "11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54";
    public string MetadataDigestSha256 => CognitionQualityRecordingSessionCanonical.Digest(OfflinePinnedOllamaRecordingFixture.ContractDescriptor);
    public bool IsMetadataOnly => true;
    public bool HasTransportDeliveryAttestation => false;
    public bool HasModelExecutionAttestation => false;
}

/// <summary>
/// Registry-closed, entirely in-memory replay of exactly twelve previously caller-supplied response
/// buffers for the pinned qwen3.5:4b cell. It is not an Ollama client and has no path, environment,
/// socket, process, provider, credential, payment, retry, or model-execution authority.
/// </summary>
public sealed class OfflinePinnedOllamaRecordingFixture : CognitionQualityRecordingAdapter, IDisposable, IAsyncDisposable
{
    public const string RegistryAdapterIdentity = "offline-pinned-ollama-qwen35-4b-recording-fixture-v1";
    public const string ContractDescriptor = "snow-globe-offline-pinned-ollama-recording-fixture/v1|qwen3.5-4b|qwen3.5:4b|2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd|3389983735|gguf|qwen35|4659865088|Q4_K_M|snow-globe-ollama-loopback-v1|http://127.0.0.1:11435/|ollama-v0.32.14-sha11d7729c|ollama-generate-v1|snow-globe-proposal-v1|9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0|961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d|4096|96|11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54|one-call-per-slot|no-retry|owned-response-buffer|no-io|no-delivery-attestation|no-model-execution-attestation";
    public const int MaximumTrackedCapabilities = 4096;

    private readonly byte[][] _responses;
    private readonly object _gate = new();
    private readonly List<CognitionQualityRecordingAdapterRequest> _requests = new();
    private readonly Dictionary<string, int> _nextSlotByCapability = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private TaskCompletionSource<bool>? _heldAcquisition;
    private int _holdNextAcquisition;
    private int _callCount;
    private bool _disposed;

    public OfflinePinnedOllamaRecordingFixture(IReadOnlyList<ReadOnlyMemory<byte>> recordedResponses)
        : base(RegistryAdapterIdentity, CognitionQualityRecordingSessionCanonical.Digest(ContractDescriptor))
    {
        ArgumentNullException.ThrowIfNull(recordedResponses);
        int count = recordedResponses.Count;
        if (count != CognitionQualityCorpusV1.ScenarioCount)
            throw new ArgumentException("Exactly twelve recorded responses are required.", nameof(recordedResponses));

        _responses = new byte[count][];
        try
        {
            int aggregate = 0;
            for (int index = 0; index < count; index++)
            {
                ReadOnlyMemory<byte> response = recordedResponses[index];
                if (response.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes)
                    throw new ArgumentOutOfRangeException(nameof(recordedResponses));
                aggregate = checked(aggregate + response.Length);
                if (aggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes)
                    throw new ArgumentOutOfRangeException(nameof(recordedResponses));
                _responses[index] = response.ToArray();
            }
        }
        catch
        {
            ZeroResponses();
            throw;
        }
    }

    public OfflinePinnedOllamaRecordingFixtureProvenance PinnedProvenance { get; } = new();
    public int CallCount => Volatile.Read(ref _callCount);
    public bool IsDisposed { get { lock (_gate) return _disposed; } }
    public bool HasTransportAuthority => false;
    public bool HasModelExecutionAuthority => false;
    public bool HasFileOrEnvironmentAuthority => false;
    public bool HasCredentialOrPaymentAuthority => false;

    /// <summary>
    /// Builds the local-lane record from immutable pinned benchmark metadata, not a live runtime.
    /// <paramref name="promptRevision"/> is the exact recorded-prompt revision; it is distinct from
    /// the benchmark prompt identity retained above.
    /// </summary>
    internal CognitionQualityExecutionProvenance CreatePinnedExecutionProvenance(string promptRevision)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(promptRevision))
            throw new ArgumentException("Prompt revision is invalid.", nameof(promptRevision));
        return CognitionQualityExecutionProvenance.ForLocal(
            PinnedProvenance.NormalizedModelIdentity,
            "sha256-" + PinnedProvenance.ArtifactDigestSha256,
            PinnedProvenance.MetadataDigestSha256,
            promptRevision,
            CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion,
            RegistryAdapterIdentity);
    }

    internal IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Array.AsReadOnly(_requests.Select(request => request.Detach()).ToArray());
        }
    }

    /// <summary>Internal deterministic test seam; production callers cannot supply a delegate or provider.</summary>
    internal Task HoldNextAcquisitionForCancellationTesting()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _heldAcquisition = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _holdNextAcquisition, 1);
            return _heldAcquisition.Task;
        }
    }

    internal override async ValueTask<CognitionQualityRecordingAdapterResponse> AcquireOnceAsync(
        CognitionQualityRecordingAdapterRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? owned = null;
        CognitionQualityRecordingAdapterRequest? detached = null;
        TaskCompletionSource<bool>? held = null;
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!CognitionQualityRecordingSessionCanonical.IsDigest(request.CapabilityDigestSha256)
                    || request.SlotOrdinal is < 1 or > CognitionQualityCorpusV1.ScenarioCount)
                    throw new InvalidOperationException("offline_recording_request_invalid");
                if (!_nextSlotByCapability.TryGetValue(request.CapabilityDigestSha256, out int expectedSlot))
                {
                    if (_nextSlotByCapability.Count >= MaximumTrackedCapabilities)
                        throw new InvalidOperationException("offline_recording_capability_capacity_exceeded");
                    expectedSlot = 1;
                }
                if (request.SlotOrdinal != expectedSlot)
                    throw new InvalidOperationException("offline_recording_slot_attempt_invalid");

                owned = _responses[request.SlotOrdinal - 1].ToArray();
                detached = request.Detach();
                _nextSlotByCapability[request.CapabilityDigestSha256] = expectedSlot + 1;
                _requests.Add(detached);
                detached = null;
                Interlocked.Increment(ref _callCount);
                if (Interlocked.Exchange(ref _holdNextAcquisition, 0) != 0)
                    held = _heldAcquisition;
            }

            if (held is not null)
            {
                held.TrySetResult(true);
                try
                {
                    using CancellationTokenSource disposalLinked = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _disposeCancellation.Token);
                    await Task.Delay(Timeout.InfiniteTimeSpan, disposalLinked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(nameof(OfflinePinnedOllamaRecordingFixture));
                }
            }

            CognitionQualityRecordingAdapterResponse response = CognitionQualityRecordingAdapterResponse.ForOfflineSuccess(
                request,
                RegistryAdapterIdentity,
                AdapterContractDigestSha256,
                new CognitionQualityRecordingResponseBuffer(owned));
            owned = null;
            return response;
        }
        finally
        {
            if (owned is not null) CryptographicOperations.ZeroMemory(owned);
            detached?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ZeroResponses();
            foreach (CognitionQualityRecordingAdapterRequest request in _requests) request.ZeroPrompt();
            _requests.Clear();
            _nextSlotByCapability.Clear();
            _disposeCancellation.Cancel();
            _heldAcquisition?.TrySetCanceled();
            _heldAcquisition = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ZeroResponses()
    {
        foreach (byte[]? response in _responses)
            if (response is not null) CryptographicOperations.ZeroMemory(response);
    }
}
