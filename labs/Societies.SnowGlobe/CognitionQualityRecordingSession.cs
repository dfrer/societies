using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

/// <summary>
/// Exact caller authorization for one process-local, offline recording session. This is not network,
/// provider, payment, or model-execution authority.
/// </summary>
public sealed record CognitionQualityRecordingSessionAuthorization(
    string PromptPublicationDigestSha256,
    string PromptSetDigestSha256,
    string ProvenanceDigestSha256,
    string AdapterIdentity,
    string AdapterContractDigestSha256,
    string AuthorizationNonce,
    int CapabilityLifetimeMilliseconds,
    int SessionTimeoutMilliseconds);

public enum CognitionQualityRecordingAuthorizationFailureCode
{
    NonceReused,
    NonceCapacityExceeded
}

/// <summary>Closed authorization failure that never echoes the caller-supplied nonce.</summary>
public sealed class CognitionQualityRecordingAuthorizationException : Exception
{
    internal CognitionQualityRecordingAuthorizationException(CognitionQualityRecordingAuthorizationFailureCode code)
        : base(code.ToString()) => Code = code;

    public CognitionQualityRecordingAuthorizationFailureCode Code { get; }
}

public enum CognitionQualityRecordingSessionOutcomeCode
{
    Complete,
    CapabilityReused,
    CapabilityExpired,
    BindingMismatch,
    Cancelled,
    TimedOut,
    DefinitelyNotSubmitted,
    SubmissionUnknown,
    AdapterFailure,
    AdapterEnvelopeInvalid,
    EvidenceRejected
}

/// <summary>Detached raw-free terminal result. Evidence is present only after all twelve slots succeed.</summary>
public sealed class CognitionQualityRecordingSessionResult
{
    internal CognitionQualityRecordingSessionResult(
        CognitionQualityRecordingSessionOutcomeCode outcomeCode,
        CognitionQualityRecordingSessionCapability capability,
        int completedSlotCount,
        int? terminalSlotOrdinal,
        SubmissionState submissionState,
        ChargeState chargeState,
        CognitionQualityRecordingEvidence? evidence)
    {
        OutcomeCode = outcomeCode;
        AdapterIdentity = capability.AdapterIdentity;
        AdapterContractDigestSha256 = capability.AdapterContractDigestSha256;
        CapabilityDigestSha256 = capability.CapabilityDigestSha256;
        PromptPublicationDigestSha256 = capability.PromptPublicationDigestSha256;
        PromptSetDigestSha256 = capability.PromptSetDigestSha256;
        ProvenanceDigestSha256 = capability.ProvenanceDigestSha256;
        CompletedSlotCount = completedSlotCount;
        TerminalSlotOrdinal = terminalSlotOrdinal;
        SubmissionState = submissionState;
        ChargeState = chargeState;
        Evidence = evidence;
    }

    public CognitionQualityRecordingSessionOutcomeCode OutcomeCode { get; }
    public string AdapterIdentity { get; }
    public string AdapterContractDigestSha256 { get; }
    public string CapabilityDigestSha256 { get; }
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string ProvenanceDigestSha256 { get; }
    public int CompletedSlotCount { get; }
    public int? TerminalSlotOrdinal { get; }
    public SubmissionState SubmissionState { get; }
    public ChargeState ChargeState { get; }
    public CognitionQualityRecordingEvidence? Evidence { get; }
    public bool IsOfflineFixture => true;
    public bool HasTransportDeliveryAttestation => false;
    public bool HasModelExecutionAttestation => false;
    public bool AdditionalAttemptAuthorized => false;
}

/// <summary>
/// Process-local, single-use authority bound to the exact module and exact registry-owned Adapter
/// object. Restart durability, cross-process idempotency, provider submission, and charge guarantees
/// are deliberately outside this capability.
/// </summary>
public sealed class CognitionQualityRecordingSessionCapability
{
    private int _state;

    internal CognitionQualityRecordingSessionCapability(
        CognitionQualityRecordingSessionModule module,
        CognitionQualityRecordingAdapter adapter,
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityRecordingSessionAuthorization authorization,
        long issuedAtMilliseconds,
        long expiresAtMilliseconds)
    {
        Module = module;
        Adapter = adapter;
        Publication = publication;
        Provenance = provenance;
        Authorization = authorization with { };
        IssuedAtMilliseconds = issuedAtMilliseconds;
        ExpiresAtMilliseconds = expiresAtMilliseconds;
        AdapterIdentity = authorization.AdapterIdentity;
        AdapterContractDigestSha256 = authorization.AdapterContractDigestSha256;
        PromptPublicationDigestSha256 = authorization.PromptPublicationDigestSha256;
        PromptSetDigestSha256 = authorization.PromptSetDigestSha256;
        ProvenanceDigestSha256 = authorization.ProvenanceDigestSha256;
        CapabilityDigestSha256 = CognitionQualityRecordingSessionCanonical.Digest(string.Join('|',
            PromptPublicationDigestSha256,
            PromptSetDigestSha256,
            ProvenanceDigestSha256,
            AdapterIdentity,
            AdapterContractDigestSha256,
            authorization.AuthorizationNonce,
            authorization.CapabilityLifetimeMilliseconds,
            authorization.SessionTimeoutMilliseconds,
            issuedAtMilliseconds,
            expiresAtMilliseconds));
    }

    internal CognitionQualityRecordingSessionModule Module { get; }
    internal CognitionQualityRecordingAdapter Adapter { get; }
    internal CognitionQualityPromptEnvelopePublication Publication { get; }
    internal CognitionQualityExecutionProvenance Provenance { get; }
    internal CognitionQualityRecordingSessionAuthorization Authorization { get; }
    internal bool TryConsume() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

    public string AdapterIdentity { get; }
    public string AdapterContractDigestSha256 { get; }
    public string CapabilityDigestSha256 { get; }
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string ProvenanceDigestSha256 { get; }
    public long IssuedAtMilliseconds { get; }
    public long ExpiresAtMilliseconds { get; }
    public bool IsConsumed => Volatile.Read(ref _state) != 0;
    public bool IsNetworkAuthorization => false;
    public bool CanRetry => false;
}

/// <summary>
/// Registry-controlled external recording seam. The internal constructor prevents applications from
/// injecting unreviewed implementations. A future local or premium Adapter must be added to this
/// assembly only after review; this Interface cannot detect hidden retries or copied response bytes,
/// or force timely cancellation against a non-cooperative implementation.
/// </summary>
public abstract class CognitionQualityRecordingAdapter
{
    internal CognitionQualityRecordingAdapter(string adapterIdentity, string adapterContractDigestSha256)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(adapterIdentity))
            throw new ArgumentException("Adapter identity is invalid.", nameof(adapterIdentity));
        if (!CognitionQualityRecordingSessionCanonical.IsDigest(adapterContractDigestSha256))
            throw new ArgumentException("Adapter contract digest is invalid.", nameof(adapterContractDigestSha256));
        AdapterIdentity = adapterIdentity;
        AdapterContractDigestSha256 = adapterContractDigestSha256;
    }

    public string AdapterIdentity { get; }
    public string AdapterContractDigestSha256 { get; }
    public bool IsOfflineFixture => true;
    public bool HasTransportDeliveryAttestation => false;
    public bool HasModelExecutionAttestation => false;
    public bool AutomaticRetriesAllowed => false;

    internal abstract ValueTask<CognitionQualityRecordingAdapterResponse> AcquireOnceAsync(
        CognitionQualityRecordingAdapterRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// The generic public offline runtime Adapter: a deterministic in-memory fixed-response fake.
/// The separately pinned Ollama recording fixture is also public; neither has live authority.
/// This Adapter performs no I/O and returns NotApplicable submission and charge states.
/// </summary>
public sealed class OfflineFixedResponseCognitionQualityRecordingAdapter : CognitionQualityRecordingAdapter, IDisposable, IAsyncDisposable
{
    public const string ContractDescriptor = "snow-globe-offline-fixed-response-recording-adapter/v1|one-call-per-slot|no-retry|owned-response-buffer|no-io|no-delivery-attestation|no-model-execution-attestation";
    public const int MaximumTrackedCapabilities = 4096;
    private readonly byte[][] _responses;
    private readonly object _gate = new();
    private readonly List<CognitionQualityRecordingAdapterRequest> _requests = new();
    private readonly Dictionary<string, int> _nextSlotByCapability = new(StringComparer.Ordinal);
    private int _callCount;
    private bool _disposed;

    public OfflineFixedResponseCognitionQualityRecordingAdapter(
        string adapterIdentity,
        IReadOnlyList<ReadOnlyMemory<byte>> fixedResponses)
        : base(adapterIdentity, CognitionQualityRecordingSessionCanonical.Digest(ContractDescriptor))
    {
        ArgumentNullException.ThrowIfNull(fixedResponses);
        if (fixedResponses.Count != CognitionQualityCorpusV1.ScenarioCount)
            throw new ArgumentException("Exactly twelve fixed responses are required.", nameof(fixedResponses));

        _responses = new byte[fixedResponses.Count][];
        try
        {
            int aggregate = 0;
            for (int index = 0; index < fixedResponses.Count; index++)
            {
                ReadOnlyMemory<byte> response = fixedResponses[index];
                int responseLength = response.Length;
                if (responseLength is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes)
                    throw new ArgumentOutOfRangeException(nameof(fixedResponses));
                aggregate = checked(aggregate + responseLength);
                if (aggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes)
                    throw new ArgumentOutOfRangeException(nameof(fixedResponses));
                _responses[index] = response.ToArray();
            }
        }
        catch { ZeroResponses(); throw; }
    }

    public int CallCount => Volatile.Read(ref _callCount);
    public bool IsDisposed { get { lock (_gate) return _disposed; } }

    internal IReadOnlyList<CognitionQualityRecordingAdapterRequest> SnapshotRequests()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Array.AsReadOnly(_requests.Select(request => request.Detach()).ToArray());
        }
    }

    internal override ValueTask<CognitionQualityRecordingAdapterResponse> AcquireOnceAsync(
        CognitionQualityRecordingAdapterRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? owned = null;
        CognitionQualityRecordingAdapterRequest? detached = null;
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
            }
            CognitionQualityRecordingAdapterResponse response = CognitionQualityRecordingAdapterResponse.ForOfflineSuccess(
                request,
                AdapterIdentity,
                AdapterContractDigestSha256,
                new CognitionQualityRecordingResponseBuffer(owned));
            owned = null;
            return ValueTask.FromResult(response);
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

internal interface ICognitionQualityRecordingSessionClock
{
    long NowMilliseconds { get; }
}

internal sealed class SystemCognitionQualityRecordingSessionClock : ICognitionQualityRecordingSessionClock
{
    private static readonly double MillisecondsPerTick = 1000d / Stopwatch.Frequency;
    public long NowMilliseconds => checked((long)(Stopwatch.GetTimestamp() * MillisecondsPerTick));
}

internal sealed class CognitionQualityRecordingAdapterRequest : IDisposable
{
    private readonly byte[] _promptUtf8;

    internal CognitionQualityRecordingAdapterRequest(
        string capabilityDigestSha256,
        string authorizationNonce,
        string promptPublicationDigestSha256,
        string promptSetDigestSha256,
        string provenanceDigestSha256,
        string adapterIdentity,
        string adapterContractDigestSha256,
        int slotOrdinal,
        string scenarioId,
        string observationDigestSha256,
        int promptByteCount,
        string promptDigestSha256,
        ReadOnlySpan<byte> promptUtf8,
        int remainingSessionMilliseconds)
    {
        CapabilityDigestSha256 = capabilityDigestSha256;
        AuthorizationNonce = authorizationNonce;
        PromptPublicationDigestSha256 = promptPublicationDigestSha256;
        PromptSetDigestSha256 = promptSetDigestSha256;
        ProvenanceDigestSha256 = provenanceDigestSha256;
        AdapterIdentity = adapterIdentity;
        AdapterContractDigestSha256 = adapterContractDigestSha256;
        SlotOrdinal = slotOrdinal;
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        PromptByteCount = promptByteCount;
        PromptDigestSha256 = promptDigestSha256;
        _promptUtf8 = promptUtf8.ToArray();
        RemainingSessionMilliseconds = remainingSessionMilliseconds;
        RequestDigestSha256 = CognitionQualityRecordingSessionCanonical.Digest(string.Join('|',
            capabilityDigestSha256,
            authorizationNonce,
            promptPublicationDigestSha256,
            promptSetDigestSha256,
            provenanceDigestSha256,
            adapterIdentity,
            adapterContractDigestSha256,
            slotOrdinal,
            scenarioId,
            observationDigestSha256,
            promptByteCount,
            promptDigestSha256,
            1,
            remainingSessionMilliseconds));
    }

    internal string CapabilityDigestSha256 { get; }
    internal string AuthorizationNonce { get; }
    internal string PromptPublicationDigestSha256 { get; }
    internal string PromptSetDigestSha256 { get; }
    internal string ProvenanceDigestSha256 { get; }
    internal string AdapterIdentity { get; }
    internal string AdapterContractDigestSha256 { get; }
    internal int SlotOrdinal { get; }
    internal string ScenarioId { get; }
    internal string ObservationDigestSha256 { get; }
    internal int PromptByteCount { get; }
    internal string PromptDigestSha256 { get; }
    internal int AttemptNumber => 1;
    internal int RemainingSessionMilliseconds { get; }
    internal string RequestDigestSha256 { get; }
    internal ReadOnlyMemory<byte> PromptUtf8 => _promptUtf8.ToArray();

    internal CognitionQualityRecordingAdapterRequest Detach() => new(
        CapabilityDigestSha256,
        AuthorizationNonce,
        PromptPublicationDigestSha256,
        PromptSetDigestSha256,
        ProvenanceDigestSha256,
        AdapterIdentity,
        AdapterContractDigestSha256,
        SlotOrdinal,
        ScenarioId,
        ObservationDigestSha256,
        PromptByteCount,
        PromptDigestSha256,
        _promptUtf8,
        RemainingSessionMilliseconds);

    internal void ZeroPrompt() => CryptographicOperations.ZeroMemory(_promptUtf8);
    public void Dispose() => ZeroPrompt();
}

internal sealed class CognitionQualityRecordingAdapterResponse
{
    internal CognitionQualityRecordingAdapterResponse(
        string capabilityDigestSha256,
        string requestDigestSha256,
        string adapterIdentity,
        string adapterContractDigestSha256,
        int slotOrdinal,
        string scenarioId,
        string observationDigestSha256,
        string promptDigestSha256,
        SubmissionState submissionState,
        ChargeState chargeState,
        bool hasTransportDeliveryAttestation,
        bool hasModelExecutionAttestation,
        CognitionQualityRecordingResponseBuffer responseBuffer)
    {
        CapabilityDigestSha256 = capabilityDigestSha256;
        RequestDigestSha256 = requestDigestSha256;
        AdapterIdentity = adapterIdentity;
        AdapterContractDigestSha256 = adapterContractDigestSha256;
        SlotOrdinal = slotOrdinal;
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        PromptDigestSha256 = promptDigestSha256;
        SubmissionState = submissionState;
        ChargeState = chargeState;
        HasTransportDeliveryAttestation = hasTransportDeliveryAttestation;
        HasModelExecutionAttestation = hasModelExecutionAttestation;
        ResponseBuffer = responseBuffer;
    }

    internal string CapabilityDigestSha256 { get; }
    internal string RequestDigestSha256 { get; }
    internal string AdapterIdentity { get; }
    internal string AdapterContractDigestSha256 { get; }
    internal int SlotOrdinal { get; }
    internal string ScenarioId { get; }
    internal string ObservationDigestSha256 { get; }
    internal string PromptDigestSha256 { get; }
    internal SubmissionState SubmissionState { get; }
    internal ChargeState ChargeState { get; }
    internal bool HasTransportDeliveryAttestation { get; }
    internal bool HasModelExecutionAttestation { get; }
    internal CognitionQualityRecordingResponseBuffer ResponseBuffer { get; }

    internal static CognitionQualityRecordingAdapterResponse ForOfflineSuccess(
        CognitionQualityRecordingAdapterRequest request,
        string adapterIdentity,
        string adapterContractDigestSha256,
        CognitionQualityRecordingResponseBuffer buffer) => new(
            request.CapabilityDigestSha256,
            request.RequestDigestSha256,
            adapterIdentity,
            adapterContractDigestSha256,
            request.SlotOrdinal,
            request.ScenarioId,
            request.ObservationDigestSha256,
            request.PromptDigestSha256,
            SubmissionState.NotApplicable,
            ChargeState.NotApplicable,
            false,
            false,
            buffer);
}

/// <summary>Owns one exact mutable response buffer and clears it on every disposal path.</summary>
internal sealed class CognitionQualityRecordingResponseBuffer : IDisposable
{
    private readonly byte[] _owned;
    private readonly Action<bool> _zeroObserver;
    private int _disposed;

    internal CognitionQualityRecordingResponseBuffer(byte[] owned, Action<bool>? zeroObserver = null)
    {
        ArgumentNullException.ThrowIfNull(owned);
        _owned = owned;
        _zeroObserver = zeroObserver ?? (static _ => { });
    }

    internal int Length => _owned.Length;

    internal byte[] Snapshot()
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(CognitionQualityRecordingResponseBuffer));
        return _owned.ToArray();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CryptographicOperations.ZeroMemory(_owned);
            _zeroObserver(_owned.All(value => value == 0));
        }
    }
}

/// <summary>
/// In-process orchestration only: Authorize binds a one-shot capability and RecordOnceAsync consumes
/// it. No provider, transport, credential, file, journal, payment, or world authority exists here.
/// </summary>
public sealed class CognitionQualityRecordingSessionModule
{
    public const int MaximumCapabilityLifetimeMilliseconds = 10 * 60 * 1000;
    public const int MaximumSessionTimeoutMilliseconds = 5 * 60 * 1000;
    public const int MaximumAuthorizedNonces = 1024;
    private readonly CognitionQualityRecordingAdapter _adapter;
    private readonly ICognitionQualityRecordingSessionClock _clock;
    private readonly object _nonceGate = new();
    private readonly HashSet<string> _authorizedNonces = new(StringComparer.Ordinal);

    public CognitionQualityRecordingSessionModule(CognitionQualityRecordingAdapter adapter)
        : this(adapter, new SystemCognitionQualityRecordingSessionClock()) { }

    internal CognitionQualityRecordingSessionModule(
        CognitionQualityRecordingAdapter adapter,
        ICognitionQualityRecordingSessionClock clock)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(clock);
        if (!adapter.IsOfflineFixture
            || adapter.HasTransportDeliveryAttestation
            || adapter.HasModelExecutionAttestation
            || adapter.AutomaticRetriesAllowed)
            throw new ArgumentException("Only the reviewed offline no-retry Adapter registry is accepted.", nameof(adapter));
        _adapter = adapter;
        _clock = clock;
    }

    public CognitionQualityRecordingSessionCapability Authorize(
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityRecordingSessionAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(authorization);
        ValidatePublicationAndProvenance(publication, provenance);
        ValidateAuthorization(publication, provenance, authorization);

        CognitionQualityPromptEnvelopePublication detachedPublication = new(
            publication.CanonicalUtf8.ToArray(),
            publication.PayloadDigestSha256,
            publication.PromptRevision,
            publication.Slots,
            publication.ClaimLimitationCodes);
        CognitionQualityExecutionProvenance detachedProvenance = provenance.Detach();
        long issuedAt = _clock.NowMilliseconds;
        long expiresAt;
        try { expiresAt = checked(issuedAt + authorization.CapabilityLifetimeMilliseconds); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(authorization)); }
        lock (_nonceGate)
        {
            if (_authorizedNonces.Contains(authorization.AuthorizationNonce))
                throw new CognitionQualityRecordingAuthorizationException(CognitionQualityRecordingAuthorizationFailureCode.NonceReused);
            if (_authorizedNonces.Count >= MaximumAuthorizedNonces)
                throw new CognitionQualityRecordingAuthorizationException(CognitionQualityRecordingAuthorizationFailureCode.NonceCapacityExceeded);
            _authorizedNonces.Add(authorization.AuthorizationNonce);
        }
        return new CognitionQualityRecordingSessionCapability(
            this,
            _adapter,
            detachedPublication,
            detachedProvenance,
            authorization,
            issuedAt,
            expiresAt);
    }

    public async ValueTask<CognitionQualityRecordingSessionResult> RecordOnceAsync(
        CognitionQualityRecordingSessionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);

        // Spending happens before every caller-controlled check, including cancellation and mismatch.
        if (!capability.TryConsume())
            return Result(CognitionQualityRecordingSessionOutcomeCode.CapabilityReused, capability, 0, null);
        if (!ReferenceEquals(capability.Module, this) || !ReferenceEquals(capability.Adapter, _adapter))
            return Result(CognitionQualityRecordingSessionOutcomeCode.BindingMismatch, capability, 0, null);
        if (cancellationToken.IsCancellationRequested)
            return Result(CognitionQualityRecordingSessionOutcomeCode.Cancelled, capability, 0, null);
        if (_clock.NowMilliseconds >= capability.ExpiresAtMilliseconds)
            return Result(CognitionQualityRecordingSessionOutcomeCode.CapabilityExpired, capability, 0, null);
        if (!BindingsMatch(capability))
            return Result(CognitionQualityRecordingSessionOutcomeCode.BindingMismatch, capability, 0, null);

        long sessionDeadline;
        try { sessionDeadline = checked(_clock.NowMilliseconds + capability.Authorization.SessionTimeoutMilliseconds); }
        catch (OverflowException) { return Result(CognitionQualityRecordingSessionOutcomeCode.BindingMismatch, capability, 0, null); }

        using CancellationTokenSource timeout = new(capability.Authorization.SessionTimeoutMilliseconds);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        byte[][] snapshots = new byte[CognitionQualityCorpusV1.ScenarioCount][];
        int completed = 0;
        int aggregateBytes = 0;
        try
        {
            IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots = capability.Publication.Slots;
            for (int index = 0; index < slots.Count; index++)
            {
                int ordinal = index + 1;
                if (cancellationToken.IsCancellationRequested)
                    return Result(CognitionQualityRecordingSessionOutcomeCode.Cancelled, capability, completed, ordinal);
                if (timeout.IsCancellationRequested || _clock.NowMilliseconds >= sessionDeadline)
                    return Result(CognitionQualityRecordingSessionOutcomeCode.TimedOut, capability, completed, ordinal);

                CognitionQualityPromptEnvelopeSlot slot = slots[index];
                int remaining = RemainingMilliseconds(sessionDeadline);
                using CognitionQualityRecordingAdapterRequest request = new(
                    capability.CapabilityDigestSha256,
                    capability.Authorization.AuthorizationNonce,
                    capability.PromptPublicationDigestSha256,
                    capability.PromptSetDigestSha256,
                    capability.ProvenanceDigestSha256,
                    capability.AdapterIdentity,
                    capability.AdapterContractDigestSha256,
                    ordinal,
                    slot.ScenarioId,
                    slot.ObservationDigestSha256,
                    slot.PromptByteCount,
                    slot.PromptDigestSha256,
                    slot.PromptUtf8.Span,
                    remaining);

                CognitionQualityRecordingAdapterResponse? response;
                try
                {
                    response = await _adapter.AcquireOnceAsync(request, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return cancellationToken.IsCancellationRequested
                        ? Result(CognitionQualityRecordingSessionOutcomeCode.Cancelled, capability, completed, ordinal)
                        : Result(CognitionQualityRecordingSessionOutcomeCode.TimedOut, capability, completed, ordinal);
                }
                catch
                {
                    return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterFailure, capability, completed, ordinal);
                }

                if (response is null)
                    return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal);

                CognitionQualityRecordingResponseBuffer? buffer = response.ResponseBuffer;
                if (buffer is null)
                    return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal);
                using (buffer)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.Cancelled, capability, completed, ordinal);
                    if (timeout.IsCancellationRequested || _clock.NowMilliseconds >= sessionDeadline)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.TimedOut, capability, completed, ordinal);
                    if (!ResponseBindingsMatch(response, request))
                        return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal,
                            NormalizeSubmission(response.SubmissionState), NormalizeCharge(response.ChargeState));
                    if (response.HasTransportDeliveryAttestation || response.HasModelExecutionAttestation)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal,
                            response.SubmissionState, response.ChargeState);
                    if (response.SubmissionState == SubmissionState.DefinitelyNotSubmitted
                        && response.ChargeState == ChargeState.NotApplicable)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.DefinitelyNotSubmitted, capability, completed, ordinal,
                            response.SubmissionState, response.ChargeState);
                    if (response.SubmissionState == SubmissionState.SubmissionUnknown
                        && response.ChargeState == ChargeState.Unknown)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.SubmissionUnknown, capability, completed, ordinal,
                            response.SubmissionState, response.ChargeState);
                    if (response.SubmissionState != SubmissionState.NotApplicable
                        || response.ChargeState != ChargeState.NotApplicable
                        || buffer.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal,
                            NormalizeSubmission(response.SubmissionState), NormalizeCharge(response.ChargeState));

                    int nextAggregate;
                    try { nextAggregate = checked(aggregateBytes + buffer.Length); }
                    catch (OverflowException)
                    {
                        return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal);
                    }
                    if (nextAggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes)
                        return Result(CognitionQualityRecordingSessionOutcomeCode.AdapterEnvelopeInvalid, capability, completed, ordinal);
                    byte[] snapshot = buffer.Snapshot();
                    snapshots[index] = snapshot;
                    aggregateBytes = nextAggregate;
                    completed++;
                }
            }

            CognitionQualityRecordingEvidence evidence;
            try
            {
                evidence = CognitionQualityRecordingEvidenceModule.Create(
                    capability.Publication,
                    capability.Provenance,
                    snapshots.Select(snapshot => (ReadOnlyMemory<byte>)snapshot).ToArray());
            }
            catch
            {
                return Result(CognitionQualityRecordingSessionOutcomeCode.EvidenceRejected, capability, completed, completed);
            }
            return Result(CognitionQualityRecordingSessionOutcomeCode.Complete, capability, completed, null,
                SubmissionState.NotApplicable, ChargeState.NotApplicable, evidence);
        }
        finally
        {
            foreach (byte[]? snapshot in snapshots)
                if (snapshot is not null) CryptographicOperations.ZeroMemory(snapshot);
        }
    }

    private static CognitionQualityRecordingSessionResult Result(
        CognitionQualityRecordingSessionOutcomeCode outcome,
        CognitionQualityRecordingSessionCapability capability,
        int completed,
        int? terminalSlot,
        SubmissionState submission = SubmissionState.NotApplicable,
        ChargeState charge = ChargeState.NotApplicable,
        CognitionQualityRecordingEvidence? evidence = null) => new(
            outcome,
            capability,
            completed,
            terminalSlot,
            submission,
            charge,
            evidence);

    private int RemainingMilliseconds(long deadline)
    {
        long remaining = deadline - _clock.NowMilliseconds;
        return remaining <= 0 ? 0 : remaining > int.MaxValue ? int.MaxValue : (int)remaining;
    }

    private bool BindingsMatch(CognitionQualityRecordingSessionCapability capability) =>
        string.Equals(capability.PromptPublicationDigestSha256, capability.Publication.CanonicalDigestSha256, StringComparison.Ordinal)
        && string.Equals(capability.PromptSetDigestSha256, capability.Publication.PromptSetDigestSha256, StringComparison.Ordinal)
        && string.Equals(capability.ProvenanceDigestSha256, capability.Provenance.ProvenanceDigestSha256, StringComparison.Ordinal)
        && string.Equals(capability.AdapterIdentity, _adapter.AdapterIdentity, StringComparison.Ordinal)
        && string.Equals(capability.AdapterContractDigestSha256, _adapter.AdapterContractDigestSha256, StringComparison.Ordinal);

    private static bool ResponseBindingsMatch(
        CognitionQualityRecordingAdapterResponse response,
        CognitionQualityRecordingAdapterRequest request) =>
        string.Equals(response.CapabilityDigestSha256, request.CapabilityDigestSha256, StringComparison.Ordinal)
        && string.Equals(response.RequestDigestSha256, request.RequestDigestSha256, StringComparison.Ordinal)
        && string.Equals(response.AdapterIdentity, request.AdapterIdentity, StringComparison.Ordinal)
        && string.Equals(response.AdapterContractDigestSha256, request.AdapterContractDigestSha256, StringComparison.Ordinal)
        && response.SlotOrdinal == request.SlotOrdinal
        && string.Equals(response.ScenarioId, request.ScenarioId, StringComparison.Ordinal)
        && string.Equals(response.ObservationDigestSha256, request.ObservationDigestSha256, StringComparison.Ordinal)
        && string.Equals(response.PromptDigestSha256, request.PromptDigestSha256, StringComparison.Ordinal)
        && Enum.IsDefined(response.SubmissionState)
        && Enum.IsDefined(response.ChargeState);

    private static SubmissionState NormalizeSubmission(SubmissionState state) =>
        Enum.IsDefined(state) ? state : SubmissionState.SubmissionUnknown;

    private static ChargeState NormalizeCharge(ChargeState state) =>
        Enum.IsDefined(state) ? state : ChargeState.Unknown;

    private void ValidateAuthorization(
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        CognitionQualityRecordingSessionAuthorization authorization)
    {
        if (!CognitionQualityRecordingSessionCanonical.IsDigest(authorization.PromptPublicationDigestSha256)
            || !CognitionQualityRecordingSessionCanonical.IsDigest(authorization.PromptSetDigestSha256)
            || !CognitionQualityRecordingSessionCanonical.IsDigest(authorization.ProvenanceDigestSha256)
            || !SnowGlobeInferenceIdentity.IsCanonical(authorization.AdapterIdentity)
            || !CognitionQualityRecordingSessionCanonical.IsDigest(authorization.AdapterContractDigestSha256)
            || !SnowGlobeInferenceIdentity.IsCanonical(authorization.AuthorizationNonce)
            || authorization.CapabilityLifetimeMilliseconds is < 1 or > MaximumCapabilityLifetimeMilliseconds
            || authorization.SessionTimeoutMilliseconds is < 1 or > MaximumSessionTimeoutMilliseconds)
            throw new ArgumentException("Recording-session authorization is invalid.", nameof(authorization));

        if (provenance.Lane == CognitionLane.Local
            && !string.Equals(provenance.LocalAdapterIdentity, _adapter.AdapterIdentity, StringComparison.Ordinal))
            throw new ArgumentException("Local provenance must bind the exact recording Adapter identity.", nameof(provenance));

        if (!string.Equals(authorization.PromptPublicationDigestSha256, publication.CanonicalDigestSha256, StringComparison.Ordinal)
            || !string.Equals(authorization.PromptSetDigestSha256, publication.PromptSetDigestSha256, StringComparison.Ordinal)
            || !string.Equals(authorization.ProvenanceDigestSha256, provenance.ProvenanceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(authorization.AdapterIdentity, _adapter.AdapterIdentity, StringComparison.Ordinal)
            || !string.Equals(authorization.AdapterContractDigestSha256, _adapter.AdapterContractDigestSha256, StringComparison.Ordinal))
            throw new ArgumentException("Recording-session authorization bindings do not match.", nameof(authorization));
    }

    private static void ValidatePublicationAndProvenance(
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance)
    {
        ReadOnlyMemory<byte>[] validationResponses = Enumerable.Range(0, CognitionQualityCorpusV1.ScenarioCount)
            .Select(_ => (ReadOnlyMemory<byte>)"{}"u8.ToArray()).ToArray();
        try
        {
            _ = CognitionQualityRecordingEvidenceModule.Create(publication, provenance, validationResponses);
        }
        catch (CognitionQualityRecordingEvidenceException exception)
        {
            throw new ArgumentException("Prompt publication or provenance is invalid.", nameof(publication), exception);
        }
        finally
        {
            foreach (ReadOnlyMemory<byte> response in validationResponses)
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(response, out ArraySegment<byte> segment) && segment.Array is not null)
                    CryptographicOperations.ZeroMemory(segment.Array.AsSpan(segment.Offset, segment.Count));
        }
    }
}

internal static class CognitionQualityRecordingSessionCanonical
{
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    internal static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
