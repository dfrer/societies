using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed record OllamaLoopbackRuntimeBinding(int ProcessId, long ProcessStartUtcTicks, string CanonicalExecutablePath, string ExecutableSha256, string EndpointIdentity, int EndpointOwnerProcessId);

/// <summary>One fresh process-local nonce. Lifetime and timeout are fixed by the registered module.</summary>
public sealed record OllamaLoopbackRecordingAuthorization(string AuthorizationNonce);

public enum SnowGlobeOllamaLoopbackRecordingOutcomeCode { Complete, Failed, Cancelled, TimedOut }

public enum SnowGlobeOllamaLoopbackRecordingFailureCode
{
    None, CapabilityReused, CapabilityExpired, BindingMismatch, Disposed, RuntimeBindingInvalid,
    RuntimeChanged, TransportPoisoned, TransportFailure, HttpResponseRejected, ResponseBodyRejected,
    WrapperRejected, EvidenceRejected
}

public enum OllamaLoopbackRecordingAuthorizationFailureCode { NonceReused, NonceCapacityExceeded }

/// <summary>Closed authorization failure that never echoes a caller nonce or runtime path.</summary>
public sealed class OllamaLoopbackRecordingAuthorizationException : Exception
{
    internal OllamaLoopbackRecordingAuthorizationException(OllamaLoopbackRecordingAuthorizationFailureCode code) : base(code.ToString()) => Code = code;
    public OllamaLoopbackRecordingAuthorizationFailureCode Code { get; }
}

/// <summary>One detached, raw-free attempted-slot binding in the runtime receipt.</summary>
public sealed class SnowGlobeOllamaLoopbackSlotReceipt
{
    internal SnowGlobeOllamaLoopbackSlotReceipt(int slotOrdinal, string requestDigestSha256, string? wrapperDigestSha256, int? statusCode, SubmissionState submissionState)
    { SlotOrdinal = slotOrdinal; RequestDigestSha256 = requestDigestSha256; WrapperDigestSha256 = wrapperDigestSha256; StatusCode = statusCode; SubmissionState = submissionState; }
    public int SlotOrdinal { get; }
    public string RequestDigestSha256 { get; }
    public string? WrapperDigestSha256 { get; }
    public int? StatusCode { get; }
    public SubmissionState SubmissionState { get; }
    public ChargeState ChargeState => ChargeState.NotApplicable;
    public bool AdditionalAttemptAuthorized => false;
}

/// <summary>Canonical raw-free local observation receipt; it is not independent artifact-loaded proof.</summary>
public sealed class SnowGlobeOllamaLoopbackRecordingReceipt
{
    private readonly byte[] _canonicalUtf8;
    private readonly SnowGlobeOllamaLoopbackSlotReceipt[] _slots;
    internal SnowGlobeOllamaLoopbackRecordingReceipt(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        IReadOnlyList<SnowGlobeOllamaLoopbackSlotReceipt> slots,
        string? nestedEvidenceDigestSha256,
        OllamaRecordingTerminalCheckpointCode terminalCheckpoint,
        OllamaRecordingTerminalPolicyCode terminalPolicy)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray(); PayloadDigestSha256 = payloadDigestSha256; CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        _slots = slots.Select(static slot => new SnowGlobeOllamaLoopbackSlotReceipt(slot.SlotOrdinal, slot.RequestDigestSha256, slot.WrapperDigestSha256, slot.StatusCode, slot.SubmissionState)).ToArray();
        NestedRecordingEvidenceDigestSha256 = nestedEvidenceDigestSha256;
        TerminalCheckpointCode = terminalCheckpoint.ToString(); TerminalPolicyCode = terminalPolicy.ToString();
    }
    public string SchemaVersion => SnowGlobePinnedOllamaRecordingModule.ReceiptSchemaVersion;
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public IReadOnlyList<SnowGlobeOllamaLoopbackSlotReceipt> Slots => Array.AsReadOnly(_slots.Select(static slot => new SnowGlobeOllamaLoopbackSlotReceipt(slot.SlotOrdinal, slot.RequestDigestSha256, slot.WrapperDigestSha256, slot.StatusCode, slot.SubmissionState)).ToArray());
    public string? NestedRecordingEvidenceDigestSha256 { get; }
    public string TerminalCheckpointCode { get; }
    public string TerminalPolicyCode { get; }
}

/// <summary>Detached terminal result whose summary fields and optional receipt are raw-free. Optional nested evidence intentionally exposes a detached prompt publication and proposal batch for offline scoring.</summary>
public sealed class SnowGlobeOllamaLoopbackRecordingResult
{
    internal SnowGlobeOllamaLoopbackRecordingResult(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcomeCode,
        SnowGlobeOllamaLoopbackRecordingFailureCode failureCode,
        AuthorizedOllamaLoopbackRecordingSession session,
        int completedSlotCount,
        int? terminalSlotOrdinal,
        SubmissionState terminalSubmissionState,
        int? terminalStatusCode,
        SnowGlobeOllamaLoopbackRecordingReceipt? receipt,
        CognitionQualityRecordingEvidence? evidence,
        OllamaRecordingTerminalCheckpointCode terminalCheckpoint,
        OllamaRecordingTerminalPolicyCode terminalPolicy)
    {
        OutcomeCode = outcomeCode; FailureCode = failureCode; CompletedSlotCount = completedSlotCount; TerminalSlotOrdinal = terminalSlotOrdinal; TerminalSubmissionState = terminalSubmissionState; TerminalStatusCode = terminalStatusCode; Receipt = receipt; Evidence = evidence;
        TerminalCheckpointCode = terminalCheckpoint.ToString(); TerminalPolicyCode = terminalPolicy.ToString();
        RegisteredCellDigestSha256 = SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256; ProfileDigestSha256 = SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256; AdapterContractDigestSha256 = SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256; CodecContractDigestSha256 = SnowGlobePinnedOllamaRecordingModule.CodecContractDigestSha256; TransportContractDigestSha256 = OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256;
        RuntimeBindingDigestSha256 = session.RuntimeBindingDigestSha256; CapabilityDigestSha256 = session.CapabilityDigestSha256; PromptPublicationDigestSha256 = session.PromptPublicationDigestSha256; PromptSetDigestSha256 = session.PromptSetDigestSha256; ProvenanceDigestSha256 = session.ProvenanceDigestSha256; ExecutablePathDigestSha256 = session.ExecutablePathDigestSha256;
        ExecutableSha256 = SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256; EndpointIdentity = SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity; RuntimeProcessId = session.RuntimeProcessId; RuntimeProcessStartUtcTicks = session.RuntimeProcessStartUtcTicks; EndpointOwnerProcessId = session.EndpointOwnerProcessId;
    }
    public SnowGlobeOllamaLoopbackRecordingOutcomeCode OutcomeCode { get; }
    public SnowGlobeOllamaLoopbackRecordingFailureCode FailureCode { get; }
    public int CompletedSlotCount { get; }
    public int? TerminalSlotOrdinal { get; }
    public SubmissionState TerminalSubmissionState { get; }
    public ChargeState TerminalChargeState => ChargeState.NotApplicable;
    public int? TerminalStatusCode { get; }
    public bool AdditionalAttemptAuthorized => false;
    public string RegisteredCellDigestSha256 { get; }
    public string ProfileDigestSha256 { get; }
    public string AdapterContractDigestSha256 { get; }
    public string CodecContractDigestSha256 { get; }
    public string TransportContractDigestSha256 { get; }
    public string RuntimeBindingDigestSha256 { get; }
    public string CapabilityDigestSha256 { get; }
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string ProvenanceDigestSha256 { get; }
    public string ExecutablePathDigestSha256 { get; }
    public string ExecutableSha256 { get; }
    public string EndpointIdentity { get; }
    public int RuntimeProcessId { get; }
    public long RuntimeProcessStartUtcTicks { get; }
    public int EndpointOwnerProcessId { get; }
    public SnowGlobeOllamaLoopbackRecordingReceipt? Receipt { get; }
    public string TerminalCheckpointCode { get; }
    public string TerminalPolicyCode { get; }
    /// <summary>Optional detached evidence containing the prompt publication and proposal batch used for offline scoring; it is present only after all twelve slots complete.</summary>
    public CognitionQualityRecordingEvidence? Evidence { get; }
    public bool HasIndependentArtifactLoadedProof => false;
    public bool HasBenchmarkOrQualityClaim => false;
    public bool HasWorldOrSimulationAuthority => false;
    public bool HasRetryAuthority => false;
}

/// <summary>Object-bound atomic one-use authority for one ordered twelve-slot recording attempt.</summary>
public sealed class AuthorizedOllamaLoopbackRecordingSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _disposeCancellation = new();
    private Task<SnowGlobeOllamaLoopbackRecordingResult>? _execution;
    private Task? _disposeTask;
    private int _state;
    private int _disposed;

    internal AuthorizedOllamaLoopbackRecordingSession(SnowGlobePinnedOllamaRecordingModule module, CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, OllamaLoopbackRuntimeBinding runtimeBinding, OllamaLoopbackRecordingAuthorization authorization, long issuedAtMilliseconds, long expiresAtMilliseconds)
    {
        Module = module; Publication = publication; Provenance = provenance; RuntimeBinding = runtimeBinding with { }; Authorization = authorization with { }; IssuedAtMilliseconds = issuedAtMilliseconds; ExpiresAtMilliseconds = expiresAtMilliseconds;
        PromptPublicationDigestSha256 = publication.CanonicalDigestSha256; PromptSetDigestSha256 = publication.PromptSetDigestSha256; ProvenanceDigestSha256 = provenance.ProvenanceDigestSha256;
        ExecutablePathDigestSha256 = CognitionQualityRecordingSessionCanonical.Digest(runtimeBinding.CanonicalExecutablePath); RuntimeBindingDigestSha256 = SnowGlobePinnedOllamaRecordingModule.DigestRuntimeBinding(runtimeBinding);
        CapabilityDigestSha256 = CognitionQualityRecordingSessionCanonical.Digest(string.Join('|', PromptPublicationDigestSha256, PromptSetDigestSha256, ProvenanceDigestSha256, RuntimeBindingDigestSha256, authorization.AuthorizationNonce, issuedAtMilliseconds, expiresAtMilliseconds));
        RuntimeProcessId = runtimeBinding.ProcessId; RuntimeProcessStartUtcTicks = runtimeBinding.ProcessStartUtcTicks; EndpointOwnerProcessId = runtimeBinding.EndpointOwnerProcessId;
    }

    internal SnowGlobePinnedOllamaRecordingModule Module { get; }
    internal CognitionQualityPromptEnvelopePublication Publication { get; }
    internal CognitionQualityExecutionProvenance Provenance { get; }
    internal OllamaLoopbackRuntimeBinding RuntimeBinding { get; }
    internal OllamaLoopbackRecordingAuthorization Authorization { get; }
    internal CancellationToken DisposalToken => _disposeCancellation.Token;
    public string CapabilityDigestSha256 { get; }
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string ProvenanceDigestSha256 { get; }
    public string RuntimeBindingDigestSha256 { get; }
    public string ExecutablePathDigestSha256 { get; }
    public int RuntimeProcessId { get; }
    public long RuntimeProcessStartUtcTicks { get; }
    public int EndpointOwnerProcessId { get; }
    public long IssuedAtMilliseconds { get; }
    public long ExpiresAtMilliseconds { get; }
    public bool IsConsumed => Volatile.Read(ref _state) != 0;
    public bool AdditionalAttemptAuthorized => false;

    public ValueTask<SnowGlobeOllamaLoopbackRecordingResult> RecordOnceAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            return ValueTask.FromResult(Module.CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityReused, this, 0, null, SubmissionState.NotApplicable, null, null, null));
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return ValueTask.FromResult(Module.CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.Disposed, this, 0, null, SubmissionState.DefinitelyNotSubmitted, null, null, null));
            _execution = Module.RecordAuthorizedSessionAsync(this, cancellationToken);
            return new ValueTask<SnowGlobeOllamaLoopbackRecordingResult>(_execution);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            Interlocked.Exchange(ref _disposed, 1);
            _disposeTask = DisposeSessionAsync(_execution);
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeSessionAsync(Task? active)
    {
        await Task.Yield(); _disposeCancellation.Cancel();
        if (active is null) { _disposeCancellation.Dispose(); return; }
        await Task.WhenAny(active, Task.Delay(OllamaLoopbackRecordingTransportAdapter.CancellationDrainMilliseconds)).ConfigureAwait(false);
        if (active.IsCompleted) _disposeCancellation.Dispose();
        else _ = active.ContinueWith(static (_, state) => ((CancellationTokenSource)state!).Dispose(), _disposeCancellation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

/// <summary>
/// Registry-closed facade for one pinned loopback cell. Construction and authorization perform no
/// process, file, listener, socket, HTTP, model, environment, credential, payment, or GPU I/O.
/// </summary>
public sealed class SnowGlobePinnedOllamaRecordingModule
{
    public const string ReceiptSchemaVersion = "snow_globe_ollama_loopback_recording_receipt/v3";
    public const string AdapterIdentity = "snow-globe-pinned-ollama-loopback-recording-adapter/v1";
    public const string NormalizedModelIdentity = "qwen3.5-4b";
    public const string RuntimeModelReference = "qwen3.5:4b";
    public const string RuntimeExecutablePath = @"E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14\ollama.exe";
    public const string RuntimeExecutableSha256 = "11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54";
    public const string ArtifactDigestSha256 = "2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd";
    public const long ArtifactSizeBytes = 3_389_983_735;
    public const string ArtifactFormat = "gguf";
    public const string ModelFamily = "qwen35";
    public const long ParameterCount = 4_659_865_088;
    public const string QuantizationLevel = "Q4_K_M";
    public const string CanonicalEndpointIdentity = "http://127.0.0.1:11435/";
    public const string GeneratePath = "/api/generate";
    public const int ContextWindowTokens = 4096;
    public const int OutputTokenLimit = 96;
    public const int Seed = 0;
    public const int Temperature = 0;
    public const int CapabilityLifetimeMilliseconds = 60_000;
    public const int SessionTimeoutMilliseconds = 300_000;
    public const int MaximumAuthorizedNonces = 1024;
    public const int MaximumReceiptBytes = 32 * 1024;
    public const string RegisteredCellDescriptor = "snow-globe-pinned-ollama-loopback-cell/v1|qwen3.5-4b|qwen3.5:4b|E:\\AIModels\\OllamaRuntimeRepair\\runtime-v0.32.14\\ollama.exe|11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54|2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd|3389983735|gguf|qwen35|4659865088|Q4_K_M|http://127.0.0.1:11435/|POST|/api/generate|4096|96|seed-0|temperature-0";
    public const string ProfileDescriptor = "snow-globe-pinned-ollama-loopback-live-profile/v1|request-16384|wrapper-8192|wrapper-aggregate-98304|response-min-1|max-1024|response-aggregate-12288|one-ordered-12-slot-session|no-retry|no-fallback|no-alternate|no-tags|observed-response-only";
    public const string CodecContractDescriptor = "snow-globe-ollama-loopback-live-codec/v1|snow-globe-offline-ollama-recording-codec/v1|qwen3.5:4b|canonical-body|strict-wrapper|depth-4|duplicate-reject|trailing-reject|invalid-utf8-reject";
    public const string AdapterContractDescriptor = "snow-globe-pinned-ollama-loopback-recording-adapter/v1|registered-cell|profile-bound-codec|runtime-owner-before-between-after|one-use|one-call-per-slot|no-retry|no-fallback|no-alternate|no-credentials|no-payment|no-world-authority";
    internal static readonly string[] ClaimLimitations = ["process_local_observation_only", "returned_model_field_only", "httpclient_exposed_framing_only_no_raw_wire_proof", "no_independent_artifact_loaded_proof", "no_benchmark_quality_intelligence_winner_or_cost_claim", "no_world_or_simulation_authority", "no_retry_authority"];
    private readonly ICognitionQualityRecordingSessionClock _clock;
    private readonly IOllamaLoopbackRecordingTransportFactory _transportFactory;
    private readonly object _nonceGate = new();
    private readonly HashSet<string> _authorizedNonces = new(StringComparer.Ordinal);

    public SnowGlobePinnedOllamaRecordingModule() : this(new SystemCognitionQualityRecordingSessionClock(), new ProductionOllamaLoopbackRecordingTransportFactory()) { }
    internal SnowGlobePinnedOllamaRecordingModule(ICognitionQualityRecordingSessionClock clock, IOllamaLoopbackRecordingTransportFactory transportFactory)
    { ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(transportFactory); _clock = clock; _transportFactory = transportFactory; }

    public static string RegisteredCellDigestSha256 { get; } = CognitionQualityRecordingSessionCanonical.Digest(RegisteredCellDescriptor);
    public static string ProfileDigestSha256 { get; } = CognitionQualityRecordingSessionCanonical.Digest(ProfileDescriptor);
    public static string AdapterContractDigestSha256 { get; } = CognitionQualityRecordingSessionCanonical.Digest(AdapterContractDescriptor);
    public static string CodecContractDigestSha256 { get; } = CognitionQualityRecordingSessionCanonical.Digest(CodecContractDescriptor);
    internal static OfflineOllamaRecordingCodecProfile LiveCodecProfile { get; } = new("snow-globe-pinned-ollama-loopback-live-profile/v1", AdapterIdentity, AdapterContractDigestSha256, RuntimeModelReference);

    public AuthorizedOllamaLoopbackRecordingSession Authorize(CognitionQualityPromptEnvelopePublication publication, OllamaLoopbackRuntimeBinding runtimeBinding, OllamaLoopbackRecordingAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(publication); ArgumentNullException.ThrowIfNull(runtimeBinding); ArgumentNullException.ThrowIfNull(authorization);
        ValidatePublication(publication); ValidateRuntimeBinding(runtimeBinding);
        if (!SnowGlobeInferenceIdentity.IsCanonical(authorization.AuthorizationNonce)) throw new ArgumentException("Loopback recording authorization is invalid.", nameof(authorization));
        long issuedAt = _clock.NowMilliseconds; long expiresAt;
        try { expiresAt = checked(issuedAt + CapabilityLifetimeMilliseconds); } catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(authorization)); }
        lock (_nonceGate)
        {
            if (_authorizedNonces.Contains(authorization.AuthorizationNonce)) throw new OllamaLoopbackRecordingAuthorizationException(OllamaLoopbackRecordingAuthorizationFailureCode.NonceReused);
            if (_authorizedNonces.Count >= MaximumAuthorizedNonces) throw new OllamaLoopbackRecordingAuthorizationException(OllamaLoopbackRecordingAuthorizationFailureCode.NonceCapacityExceeded);
            _authorizedNonces.Add(authorization.AuthorizationNonce);
        }
        CognitionQualityPromptEnvelopePublication detached = new(publication.CanonicalUtf8.ToArray(), publication.PayloadDigestSha256, publication.PromptRevision, publication.Slots, publication.ClaimLimitationCodes);
        CognitionQualityExecutionProvenance provenance = CognitionQualityExecutionProvenance.ForLocal(NormalizedModelIdentity, "sha256-" + ArtifactDigestSha256, AdapterContractDigestSha256, publication.PromptRevision, CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, AdapterIdentity);
        return new AuthorizedOllamaLoopbackRecordingSession(this, detached, provenance, runtimeBinding, authorization, issuedAt, expiresAt);
    }

    internal async Task<SnowGlobeOllamaLoopbackRecordingResult> RecordAuthorizedSessionAsync(AuthorizedOllamaLoopbackRecordingSession session, CancellationToken callerCancellation)
    {
        if (!ReferenceEquals(session.Module, this)) return CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.BindingMismatch, session, 0, null, SubmissionState.DefinitelyNotSubmitted, null, null, null);
        if (callerCancellation.IsCancellationRequested || session.DisposalToken.IsCancellationRequested) return CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled, SnowGlobeOllamaLoopbackRecordingFailureCode.None, session, 0, 1, SubmissionState.DefinitelyNotSubmitted, null, null, null);
        if (_clock.NowMilliseconds >= session.ExpiresAtMilliseconds) return CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityExpired, session, 0, null, SubmissionState.DefinitelyNotSubmitted, null, null, null);
        if (!BindingsMatch(session)) return CreateResult(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.BindingMismatch, session, 0, null, SubmissionState.DefinitelyNotSubmitted, null, null, null);

        using CancellationTokenSource timeout = new(SessionTimeoutMilliseconds);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation, session.DisposalToken, timeout.Token);
        byte[][] proposals = new byte[CognitionQualityCorpusV1.ScenarioCount][];
        List<SnowGlobeOllamaLoopbackSlotReceipt> slots = new(CognitionQualityCorpusV1.ScenarioCount);
        IOfflineOllamaRecordingTransportPort? transport = null;
        int completed = 0; int aggregate = 0; long deadline = checked(_clock.NowMilliseconds + SessionTimeoutMilliseconds);
        try
        {
            try { transport = _transportFactory.Create(session.RuntimeBinding); }
            catch { return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeBindingInvalid, session, completed, 1, SubmissionState.DefinitelyNotSubmitted, null, slots, null, null); }
            IReadOnlyList<CognitionQualityPromptEnvelopeSlot> publicationSlots = session.Publication.Slots;
            for (int index = 0; index < CognitionQualityCorpusV1.ScenarioCount; index++)
            {
                int ordinal = index + 1;
                if (callerCancellation.IsCancellationRequested || session.DisposalToken.IsCancellationRequested) return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled, SnowGlobeOllamaLoopbackRecordingFailureCode.None, session, completed, ordinal, SubmissionState.DefinitelyNotSubmitted, null, slots, null, null);
                if (timeout.IsCancellationRequested || _clock.NowMilliseconds >= deadline) return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut, SnowGlobeOllamaLoopbackRecordingFailureCode.None, session, completed, ordinal, SubmissionState.DefinitelyNotSubmitted, null, slots, null, null);
                CognitionQualityPromptEnvelopeSlot slot = publicationSlots[index]; int remaining = RemainingMilliseconds(deadline);
                using CognitionQualityRecordingAdapterRequest request = new(session.CapabilityDigestSha256, session.Authorization.AuthorizationNonce, session.PromptPublicationDigestSha256, session.PromptSetDigestSha256, session.ProvenanceDigestSha256, AdapterIdentity, AdapterContractDigestSha256, ordinal, slot.ScenarioId, slot.ObservationDigestSha256, slot.PromptByteCount, slot.PromptDigestSha256, slot.PromptUtf8.Span, remaining);
                using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request, LiveCodecProfile);
                string requestDigest = encoded.BodyDigestSha256;
                OfflineOllamaRecordingTransportResponse raw;
                try { raw = await transport.ExchangeOnceAsync(encoded, linked.Token).ConfigureAwait(false); }
                catch (OllamaLoopbackTransportException exception)
                {
                    slots.Add(new SnowGlobeOllamaLoopbackSlotReceipt(ordinal, requestDigest, exception.WrapperDigestSha256, exception.StatusCode, exception.SubmissionState));
                    SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome = callerCancellation.IsCancellationRequested || session.DisposalToken.IsCancellationRequested ? SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled : timeout.IsCancellationRequested || exception.Code == OllamaLoopbackTransportFailureCode.TimedOut ? SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut : exception.Code == OllamaLoopbackTransportFailureCode.Cancelled ? SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled : SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed;
                    SnowGlobeOllamaLoopbackRecordingFailureCode failure = outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed ? MapTransportFailure(exception.Code) : SnowGlobeOllamaLoopbackRecordingFailureCode.None;
                    return Terminal(outcome, failure, session, completed, ordinal, exception.SubmissionState, exception.StatusCode, slots, null, null, exception.Checkpoint, exception.Policy);
                }
                catch
                {
                    slots.Add(new SnowGlobeOllamaLoopbackSlotReceipt(ordinal, requestDigest, null, null, SubmissionState.SubmissionUnknown));
                    return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure, session, completed, ordinal, SubmissionState.SubmissionUnknown, null, slots, null, null, OllamaRecordingTerminalCheckpointCode.RequestDispatch, OllamaRecordingTerminalPolicyCode.TransportIo);
                }

                string wrapperDigest = CognitionQualityHash.Sha256(raw.BodyUtf8.Span);
                try
                {
                    using CognitionQualityRecordingResponseBuffer buffer = OfflineOllamaRecordingCodecModule.Decode(raw, request, LiveCodecProfile);
                    int nextAggregate = checked(aggregate + buffer.Length);
                    if (nextAggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes) throw new InvalidOperationException("live_ollama_response_aggregate_invalid");
                    proposals[index] = buffer.Snapshot(); aggregate = nextAggregate; completed++;
                    slots.Add(new SnowGlobeOllamaLoopbackSlotReceipt(ordinal, requestDigest, wrapperDigest, 200, SubmissionState.ResponseReceived));
                }
                catch
                {
                    slots.Add(new SnowGlobeOllamaLoopbackSlotReceipt(ordinal, requestDigest, wrapperDigest, 200, SubmissionState.ResponseReceived));
                    return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.WrapperRejected, session, completed, ordinal, SubmissionState.ResponseReceived, 200, slots, null, null);
                }
            }
            CognitionQualityRecordingEvidence evidence;
            try { evidence = CognitionQualityRecordingEvidenceModule.Create(session.Publication, session.Provenance, proposals.Select(static value => (ReadOnlyMemory<byte>)value).ToArray()); }
            catch { return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected, session, completed, completed, SubmissionState.ResponseReceived, 200, slots, null, null); }
            return Terminal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete, SnowGlobeOllamaLoopbackRecordingFailureCode.None, session, completed, null, SubmissionState.ResponseReceived, 200, slots, evidence, evidence.CanonicalDigestSha256);
        }
        finally
        {
            if (transport is not null) { try { await transport.DisposeAsync().ConfigureAwait(false); } catch { } }
            foreach (byte[]? proposal in proposals) if (proposal is not null) CryptographicOperations.ZeroMemory(proposal);
        }
    }

    internal SnowGlobeOllamaLoopbackRecordingResult CreateResult(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome,
        SnowGlobeOllamaLoopbackRecordingFailureCode failure,
        AuthorizedOllamaLoopbackRecordingSession session,
        int completed,
        int? terminalSlot,
        SubmissionState submission,
        int? status,
        SnowGlobeOllamaLoopbackRecordingReceipt? receipt,
        CognitionQualityRecordingEvidence? evidence,
        OllamaRecordingTerminalCheckpointCode? checkpoint = null,
        OllamaRecordingTerminalPolicyCode? policy = null)
    {
        (OllamaRecordingTerminalCheckpointCode resolvedCheckpoint, OllamaRecordingTerminalPolicyCode resolvedPolicy) =
            checkpoint is not null && policy is not null
                ? (checkpoint.Value, policy.Value)
                : InferTerminalEvidence(outcome, failure, submission, status, receipt);
        return new(outcome, failure, session, completed, terminalSlot, submission, status, receipt, evidence, resolvedCheckpoint, resolvedPolicy);
    }

    private SnowGlobeOllamaLoopbackRecordingResult Terminal(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome,
        SnowGlobeOllamaLoopbackRecordingFailureCode failure,
        AuthorizedOllamaLoopbackRecordingSession session,
        int completed,
        int? terminalSlot,
        SubmissionState submission,
        int? status,
        IReadOnlyList<SnowGlobeOllamaLoopbackSlotReceipt> slots,
        CognitionQualityRecordingEvidence? evidence,
        string? nestedEvidenceDigest,
        OllamaRecordingTerminalCheckpointCode? checkpoint = null,
        OllamaRecordingTerminalPolicyCode? policy = null)
    {
        (OllamaRecordingTerminalCheckpointCode resolvedCheckpoint, OllamaRecordingTerminalPolicyCode resolvedPolicy) =
            checkpoint is not null && policy is not null
                ? (checkpoint.Value, policy.Value)
                : InferTerminalEvidence(outcome, failure, submission, status, null);
        SnowGlobeOllamaLoopbackRecordingReceipt receipt = CreateReceipt(outcome, failure, session, completed, terminalSlot, slots, nestedEvidenceDigest, resolvedCheckpoint, resolvedPolicy);
        return CreateResult(outcome, failure, session, completed, terminalSlot, submission, status, receipt, evidence, resolvedCheckpoint, resolvedPolicy);
    }

    private static SnowGlobeOllamaLoopbackRecordingReceipt CreateReceipt(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome,
        SnowGlobeOllamaLoopbackRecordingFailureCode failure,
        AuthorizedOllamaLoopbackRecordingSession session,
        int completed,
        int? terminalSlot,
        IReadOnlyList<SnowGlobeOllamaLoopbackSlotReceipt> slots,
        string? nestedEvidenceDigest,
        OllamaRecordingTerminalCheckpointCode checkpoint,
        OllamaRecordingTerminalPolicyCode policy)
    {
        byte[] payload = WriteReceipt(outcome, failure, session, completed, terminalSlot, slots, nestedEvidenceDigest, checkpoint, policy, null); string payloadDigest = CognitionQualityHash.Sha256(payload); CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = WriteReceipt(outcome, failure, session, completed, terminalSlot, slots, nestedEvidenceDigest, checkpoint, policy, payloadDigest);
        if (canonical.Length is < 1 or > MaximumReceiptBytes) { CryptographicOperations.ZeroMemory(canonical); throw new InvalidOperationException("live_ollama_receipt_size_invalid"); }
        return new SnowGlobeOllamaLoopbackRecordingReceipt(canonical, payloadDigest, slots, nestedEvidenceDigest, checkpoint, policy);
    }

    private static byte[] WriteReceipt(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome,
        SnowGlobeOllamaLoopbackRecordingFailureCode failure,
        AuthorizedOllamaLoopbackRecordingSession session,
        int completed,
        int? terminalSlot,
        IReadOnlyList<SnowGlobeOllamaLoopbackSlotReceipt> slots,
        string? nestedEvidenceDigest,
        OllamaRecordingTerminalCheckpointCode checkpoint,
        OllamaRecordingTerminalPolicyCode policy,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }); writer.WriteStartObject();
        writer.WriteString("schema_version", ReceiptSchemaVersion); writer.WriteString("status", outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete ? "complete" : "terminal"); writer.WriteString("outcome", outcome.ToString()); writer.WriteString("failure_code", failure.ToString()); writer.WriteString("terminal_checkpoint_code", checkpoint.ToString()); writer.WriteString("terminal_policy_code", policy.ToString());
        writer.WriteString("registered_cell_digest_sha256", RegisteredCellDigestSha256); writer.WriteString("profile_digest_sha256", ProfileDigestSha256); writer.WriteString("adapter_identity", AdapterIdentity); writer.WriteString("adapter_contract_digest_sha256", AdapterContractDigestSha256); writer.WriteString("codec_contract_digest_sha256", CodecContractDigestSha256); writer.WriteString("transport_contract_digest_sha256", OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256);
        writer.WriteNumber("runtime_process_id", session.RuntimeProcessId); writer.WriteNumber("runtime_process_start_utc_ticks", session.RuntimeProcessStartUtcTicks); writer.WriteString("runtime_executable_path_digest_sha256", session.ExecutablePathDigestSha256); writer.WriteString("runtime_executable_sha256", RuntimeExecutableSha256); writer.WriteString("endpoint_identity", CanonicalEndpointIdentity); writer.WriteNumber("endpoint_owner_process_id", session.EndpointOwnerProcessId);
        writer.WriteString("prompt_publication_digest_sha256", session.PromptPublicationDigestSha256); writer.WriteString("prompt_set_digest_sha256", session.PromptSetDigestSha256); writer.WriteString("provenance_digest_sha256", session.ProvenanceDigestSha256); writer.WriteString("capability_digest_sha256", session.CapabilityDigestSha256); writer.WriteString("runtime_binding_digest_sha256", session.RuntimeBindingDigestSha256);
        writer.WritePropertyName("slots"); writer.WriteStartArray(); foreach (SnowGlobeOllamaLoopbackSlotReceipt slot in slots) { writer.WriteStartObject(); writer.WriteNumber("slot_ordinal", slot.SlotOrdinal); writer.WriteString("request_digest_sha256", slot.RequestDigestSha256); if (slot.WrapperDigestSha256 is null) writer.WriteNull("wrapper_digest_sha256"); else writer.WriteString("wrapper_digest_sha256", slot.WrapperDigestSha256); if (slot.StatusCode is null) writer.WriteNull("status_code"); else writer.WriteNumber("status_code", slot.StatusCode.Value); writer.WriteString("submission_state", slot.SubmissionState.ToString()); writer.WriteString("charge_state", ChargeState.NotApplicable.ToString()); writer.WriteBoolean("additional_attempt_authorized", false); writer.WriteEndObject(); } writer.WriteEndArray();
        writer.WriteNumber("completed_slot_count", completed); if (terminalSlot is null) writer.WriteNull("terminal_slot_ordinal"); else writer.WriteNumber("terminal_slot_ordinal", terminalSlot.Value); writer.WriteNumber("automatic_retry_count", 0); writer.WriteNumber("fallback_count", 0); writer.WriteNumber("alternate_endpoint_or_model_count", 0); if (nestedEvidenceDigest is null) writer.WriteNull("nested_recording_evidence_digest_sha256"); else writer.WriteString("nested_recording_evidence_digest_sha256", nestedEvidenceDigest);
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray(); foreach (string limitation in ClaimLimitations) writer.WriteStringValue(limitation); writer.WriteEndArray(); if (payloadDigest is not null) writer.WriteString("receipt_payload_digest_sha256", payloadDigest); writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    internal static string DigestRuntimeBinding(OllamaLoopbackRuntimeBinding binding) => CognitionQualityRecordingSessionCanonical.Digest(string.Join('|', binding.ProcessId, binding.ProcessStartUtcTicks, CognitionQualityRecordingSessionCanonical.Digest(binding.CanonicalExecutablePath), binding.ExecutableSha256, binding.EndpointIdentity, binding.EndpointOwnerProcessId));
    internal static void ValidateRuntimeBinding(OllamaLoopbackRuntimeBinding binding)
    {
        if (binding.ProcessId <= 0 || binding.ProcessStartUtcTicks <= 0 || binding.ProcessStartUtcTicks > DateTime.MaxValue.Ticks || !string.Equals(binding.CanonicalExecutablePath, RuntimeExecutablePath, StringComparison.Ordinal) || !string.Equals(binding.ExecutableSha256, RuntimeExecutableSha256, StringComparison.Ordinal) || !string.Equals(binding.EndpointIdentity, CanonicalEndpointIdentity, StringComparison.Ordinal) || binding.EndpointOwnerProcessId != binding.ProcessId) throw new ArgumentException("Runtime binding is not the registered loopback cell.", nameof(binding));
    }

    private static void ValidatePublication(CognitionQualityPromptEnvelopePublication publication)
    {
        CognitionQualityPromptEnvelopePublication expected;
        try { expected = CognitionQualityPromptEnvelopeBuilderModule.Create(publication.PromptRevision); } catch { throw new ArgumentException("Prompt publication is invalid.", nameof(publication)); }
        if (!publication.CanonicalUtf8.Span.SequenceEqual(expected.CanonicalUtf8.Span) || !string.Equals(publication.PayloadDigestSha256, expected.PayloadDigestSha256, StringComparison.Ordinal) || !string.Equals(publication.CanonicalDigestSha256, expected.CanonicalDigestSha256, StringComparison.Ordinal) || !string.Equals(publication.PromptSetDigestSha256, expected.PromptSetDigestSha256, StringComparison.Ordinal) || publication.Slots.Count != CognitionQualityCorpusV1.ScenarioCount) throw new ArgumentException("Prompt publication is invalid.", nameof(publication));
    }

    private static bool BindingsMatch(AuthorizedOllamaLoopbackRecordingSession session) => string.Equals(session.PromptPublicationDigestSha256, session.Publication.CanonicalDigestSha256, StringComparison.Ordinal) && string.Equals(session.PromptSetDigestSha256, session.Publication.PromptSetDigestSha256, StringComparison.Ordinal) && string.Equals(session.ProvenanceDigestSha256, session.Provenance.ProvenanceDigestSha256, StringComparison.Ordinal) && string.Equals(session.Provenance.LocalAdapterIdentity, AdapterIdentity, StringComparison.Ordinal) && string.Equals(session.RuntimeBindingDigestSha256, DigestRuntimeBinding(session.RuntimeBinding), StringComparison.Ordinal);
    private int RemainingMilliseconds(long deadline) { long remaining = deadline - _clock.NowMilliseconds; return remaining <= 0 ? 0 : remaining > int.MaxValue ? int.MaxValue : (int)remaining; }
    private static SnowGlobeOllamaLoopbackRecordingFailureCode MapTransportFailure(OllamaLoopbackTransportFailureCode code) => code switch { OllamaLoopbackTransportFailureCode.RuntimeChanged => SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged, OllamaLoopbackTransportFailureCode.Poisoned => SnowGlobeOllamaLoopbackRecordingFailureCode.TransportPoisoned, OllamaLoopbackTransportFailureCode.HttpResponseRejected => SnowGlobeOllamaLoopbackRecordingFailureCode.HttpResponseRejected, OllamaLoopbackTransportFailureCode.ResponseBodyRejected => SnowGlobeOllamaLoopbackRecordingFailureCode.ResponseBodyRejected, _ => SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure };

    private static (OllamaRecordingTerminalCheckpointCode, OllamaRecordingTerminalPolicyCode) InferTerminalEvidence(
        SnowGlobeOllamaLoopbackRecordingOutcomeCode outcome,
        SnowGlobeOllamaLoopbackRecordingFailureCode failure,
        SubmissionState submission,
        int? status,
        SnowGlobeOllamaLoopbackRecordingReceipt? receipt)
    {
        if (outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete)
            return (OllamaRecordingTerminalCheckpointCode.None, OllamaRecordingTerminalPolicyCode.None);
        if (outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled)
            return (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.Cancellation);
        if (outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut)
            return (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.Timeout);
        return failure switch
        {
            SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityExpired =>
                (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.Capability),
            SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeBindingInvalid =>
                (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.RuntimeBinding),
            SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged =>
                (submission == SubmissionState.ResponseReceived
                    ? (receipt?.Slots.LastOrDefault()?.WrapperDigestSha256 is null ? OllamaRecordingTerminalCheckpointCode.ResponseHeaders : OllamaRecordingTerminalCheckpointCode.AfterExchange)
                    : OllamaRecordingTerminalCheckpointCode.BeforeDispatch,
                    OllamaRecordingTerminalPolicyCode.RuntimeOwnership),
            SnowGlobeOllamaLoopbackRecordingFailureCode.TransportPoisoned =>
                (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.TransportState),
            SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure =>
                (submission == SubmissionState.ResponseReceived && status == 200 ? OllamaRecordingTerminalCheckpointCode.ResponseBody : OllamaRecordingTerminalCheckpointCode.RequestDispatch,
                    submission == SubmissionState.ResponseReceived && status == 200 ? OllamaRecordingTerminalPolicyCode.BodyRead : OllamaRecordingTerminalPolicyCode.TransportIo),
            SnowGlobeOllamaLoopbackRecordingFailureCode.HttpResponseRejected =>
                (OllamaRecordingTerminalCheckpointCode.ResponseHeaders, status == 200 ? OllamaRecordingTerminalPolicyCode.ContentType : OllamaRecordingTerminalPolicyCode.HttpStatus),
            SnowGlobeOllamaLoopbackRecordingFailureCode.ResponseBodyRejected =>
                (OllamaRecordingTerminalCheckpointCode.ResponseBody, OllamaRecordingTerminalPolicyCode.BodyRead),
            SnowGlobeOllamaLoopbackRecordingFailureCode.WrapperRejected =>
                (OllamaRecordingTerminalCheckpointCode.WrapperDecode, OllamaRecordingTerminalPolicyCode.WrapperShape),
            SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected =>
                (OllamaRecordingTerminalCheckpointCode.EvidenceConstruction, OllamaRecordingTerminalPolicyCode.EvidenceShape),
            _ => (OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.RequestPolicy)
        };
    }
}
