using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Societies.SnowGlobe;

internal enum OllamaLoopbackRuntimeCheckPoint { BeforeDispatch, ConnectedBeforeBody, AfterResponseHeaders, AfterExchange }
internal sealed record OllamaLoopbackConnectionIdentity(int ClientProcessId, int ClientPort, int ServerProcessId, int ServerPort);
internal readonly record struct OllamaLoopbackTcpOwnerRow(int State, uint LocalAddress, int LocalPort, uint RemoteAddress, int RemotePort, int ProcessId);
internal sealed record OllamaLoopbackRuntimeVerification(bool Accepted, string Code)
{
    internal static OllamaLoopbackRuntimeVerification Pass { get; } = new(true, "ok");
    internal static OllamaLoopbackRuntimeVerification Reject(string code) => new(false, code);
}

internal interface IOllamaLoopbackRuntimeVerifier
{
    OllamaLoopbackRuntimeVerification Verify(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection);
}

internal interface IOllamaLoopbackRecordingTransportFactory
{
    IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding);
}

internal enum OllamaLoopbackTransportFailureCode { RuntimeChanged, Poisoned, Cancelled, TimedOut, TransportFailure, HttpResponseRejected, ResponseBodyRejected }

/// <summary>Closed, raw-free transport terminal; it never echoes URI, path, headers, body, prompt, nonce, or OS exceptions.</summary>
internal sealed class OllamaLoopbackTransportException : Exception
{
    internal OllamaLoopbackTransportException(OllamaLoopbackTransportFailureCode code, SubmissionState submissionState, int? statusCode = null, string? wrapperDigestSha256 = null)
        : base(code.ToString()) { Code = code; SubmissionState = submissionState; StatusCode = statusCode; WrapperDigestSha256 = wrapperDigestSha256; }
    internal OllamaLoopbackTransportFailureCode Code { get; }
    internal SubmissionState SubmissionState { get; }
    internal ChargeState ChargeState => ChargeState.NotApplicable;
    internal int? StatusCode { get; }
    internal string? WrapperDigestSha256 { get; }
    internal bool AdditionalAttemptAuthorized => false;
}

internal sealed class ProductionOllamaLoopbackRecordingTransportFactory : IOllamaLoopbackRecordingTransportFactory
{
    public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding) => new OllamaLoopbackRecordingTransportAdapter(binding, new WindowsOllamaLoopbackRuntimeVerifier());
}

/// <summary>One-shot HTTP/1.1 request body. Hidden replay attempts fail before a second serialization.</summary>
internal sealed class SingleUseOllamaJsonContent : HttpContent
{
    private byte[] _owned;
    private int _serializationState;
    internal SingleUseOllamaJsonContent(byte[] owned) { _owned = owned; Headers.ContentType = new MediaTypeHeaderValue("application/json"); }
    internal bool HasSerializationBegun => Volatile.Read(ref _serializationState) != 0;
    internal int SecondSerializationAttemptCount { get; private set; }
    protected override bool TryComputeLength(out long length) { length = _owned.Length; return true; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _serializationState, 1, 0) != 0) { SecondSerializationAttemptCount++; throw new InvalidOperationException("live_ollama_request_replay_rejected"); }
        await stream.WriteAsync(_owned, cancellationToken).ConfigureAwait(false);
    }
    protected override void Dispose(bool disposing) { if (_owned.Length != 0) CryptographicOperations.ZeroMemory(_owned); _owned = Array.Empty<byte>(); base.Dispose(disposing); }
}

/// <summary>Hardened production implementation of the existing internal recording transport seam.</summary>
internal sealed class OllamaLoopbackRecordingTransportAdapter : IOfflineOllamaRecordingTransportPort
{
    internal const int CancellationDrainMilliseconds = 250;
    internal const string ContractDescriptor = "snow-globe-ollama-loopback-recording-transport-adapter/v1|http-1.1-exact|post-generate|redirect-off|decompression-off|proxy-off|cookies-off|credentials-off|max-connection-1|response-headers-read|content-type-application-json-no-parameters|content-length-required|body-8192|single-serialization|runtime-owner-before-between-after|explicit-cancellation-cause|cancel-drain-250ms|one-late-observer|poison-on-indeterminate|no-retry";
    internal static string ContractDigestSha256 { get; } = CognitionQualityRecordingSessionCanonical.Digest(ContractDescriptor);
    private static readonly Uri GenerateUri = new("http://127.0.0.1:11435/api/generate", UriKind.Absolute);
    private readonly OllamaLoopbackRuntimeBinding _binding;
    private readonly IOllamaLoopbackRuntimeVerifier _runtimeVerifier;
    private readonly HttpClient _client;
    private readonly SocketsHttpHandler? _productionHandler;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _disposeCancellation = new();
    private string? _capabilityDigest;
    private OllamaLoopbackConnectionIdentity? _connection;
    private Task? _lateObserver;
    private Task? _disposeTask;
    private int _nextSlot = 1;
    private int _callCount;
    private int _observerCount;
    private int _inFlight;
    private int _poisoned;
    private int _disposed;
    private int _clientDisposed;
    private int _disposeCancellationDisposed;
    private int _disposeCancellationIssued;

    internal OllamaLoopbackRecordingTransportAdapter(OllamaLoopbackRuntimeBinding binding, IOllamaLoopbackRuntimeVerifier runtimeVerifier)
    {
        _binding = binding with { }; _runtimeVerifier = runtimeVerifier;
        _productionHandler = CreateProductionHandler();
        _client = new HttpClient(_productionHandler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = OfflineOllamaRecordingCodecModule.MaximumWrapperBytes };
    }

    internal OllamaLoopbackRecordingTransportAdapter(OllamaLoopbackRuntimeBinding binding, IOllamaLoopbackRuntimeVerifier runtimeVerifier, HttpMessageHandler scriptedHandler)
    {
        _binding = binding with { }; _runtimeVerifier = runtimeVerifier; _client = new HttpClient(scriptedHandler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = OfflineOllamaRecordingCodecModule.MaximumWrapperBytes };
    }

    internal int CallCount => Volatile.Read(ref _callCount);
    internal int LateObserverCount => Volatile.Read(ref _observerCount);
    internal bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;
    internal SocketsHttpHandler? ProductionHandler => _productionHandler;

    private SocketsHttpHandler CreateProductionHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseProxy = false,
        Proxy = null,
        UseCookies = false,
        Credentials = null,
        PreAuthenticate = false,
        MaxConnectionsPerServer = 1,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.Zero,
        PooledConnectionIdleTimeout = TimeSpan.Zero,
        ConnectCallback = ConnectExactLoopbackAsync
    };

    private async ValueTask<Stream> ConnectExactLoopbackAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.DnsEndPoint.Host, "127.0.0.1", StringComparison.Ordinal) || context.DnsEndPoint.Port != 11435) throw new HttpRequestException("loopback_target_rejected");
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 11435), cancellationToken).ConfigureAwait(false);
            int clientPort = ((IPEndPoint)socket.LocalEndPoint!).Port; OllamaLoopbackConnectionIdentity connection = new(Environment.ProcessId, clientPort, _binding.ProcessId, 11435);
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.ConnectedBeforeBody, connection)) throw new HttpRequestException("loopback_owner_rejected");
            lock (_gate) _connection = connection;
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch { socket.Dispose(); throw; }
    }

    public async ValueTask<OfflineOllamaRecordingTransportResponse> ExchangeOnceAsync(OfflineOllamaRecordingTransportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try { ReserveExactSlot(request); } catch { request.Dispose(); throw; }
        if (!Verify(OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null)) { Poison(); request.Dispose(); ReleaseInFlight(); throw Failure(OllamaLoopbackTransportFailureCode.RuntimeChanged, SubmissionState.DefinitelyNotSubmitted); }
        if (cancellationToken.IsCancellationRequested) { Poison(); request.Dispose(); ReleaseInFlight(); throw Failure(OllamaLoopbackTransportFailureCode.Cancelled, SubmissionState.DefinitelyNotSubmitted); }
        byte[] body = request.TakeBody(); SingleUseOllamaJsonContent? content = null; HttpRequestMessage? message = null; HttpResponseMessage? response = null; bool ownershipTransferred = false;
        using CancellationTokenSource timeout = new(Math.Max(1, request.RemainingSessionMilliseconds));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token, timeout.Token);
        try
        {
            content = new SingleUseOllamaJsonContent(body); body = Array.Empty<byte>();
            message = new HttpRequestMessage(HttpMethod.Post, GenerateUri) { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact, Content = content };
            message.Headers.ExpectContinue = false;
            try { ValidateOutboundMessage(message); } catch { Poison(); throw; }
            Task<HttpResponseMessage> sendTask = _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            try { response = await sendTask.WaitAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                OllamaLoopbackTransportFailureCode cancellationCode = ClassifyCancellationCause(cancellationToken, _disposeCancellation.Token, timeout.Token);
                await DrainAsync(sendTask).ConfigureAwait(false);
                if (sendTask.IsCompletedSuccessfully) { Poison(); response = sendTask.Result; throw Failure(cancellationCode, SubmissionState.ResponseReceived, (int)response.StatusCode); }
                if (sendTask.IsCompleted) { Poison(); throw Failure(cancellationCode, content.HasSerializationBegun ? SubmissionState.SubmissionUnknown : SubmissionState.DefinitelyNotSubmitted); }
                bool serializationBegan = content.HasSerializationBegun; Poison(); InstallLateSendObserver(sendTask, message); ownershipTransferred = true; message = null; content = null;
                throw Failure(cancellationCode, serializationBegan ? SubmissionState.SubmissionUnknown : SubmissionState.DefinitelyNotSubmitted);
            }
            catch
            {
                Poison(); throw Failure(OllamaLoopbackTransportFailureCode.TransportFailure, content.HasSerializationBegun ? SubmissionState.SubmissionUnknown : SubmissionState.DefinitelyNotSubmitted);
            }

            int statusCode = (int)response.StatusCode;
            OllamaLoopbackConnectionIdentity? connection; lock (_gate) connection = _connection;
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection)) { Poison(); throw Failure(OllamaLoopbackTransportFailureCode.RuntimeChanged, SubmissionState.ResponseReceived, statusCode); }
            try { ValidateResponseHeaders(response, statusCode); } catch (OllamaLoopbackTransportException) { Poison(); throw; }
            long declaredLength = response.Content.Headers.ContentLength!.Value;
            Task<byte[]> readTask = ReadExactBodyAsync(response.Content, checked((int)declaredLength), linked.Token);
            byte[] wrapper;
            try { wrapper = await readTask.WaitAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                OllamaLoopbackTransportFailureCode cancellationCode = ClassifyCancellationCause(cancellationToken, _disposeCancellation.Token, timeout.Token);
                Poison();
                await DrainAsync(readTask).ConfigureAwait(false);
                if (readTask.IsCompletedSuccessfully) CryptographicOperations.ZeroMemory(readTask.Result);
                else if (!readTask.IsCompleted) { Poison(); InstallLateReadObserver(readTask, response, message); ownershipTransferred = true; response = null; message = null; content = null; }
                throw Failure(cancellationCode, SubmissionState.ResponseReceived, statusCode);
            }
            catch { Poison(); throw Failure(OllamaLoopbackTransportFailureCode.ResponseBodyRejected, SubmissionState.ResponseReceived, statusCode); }

            string wrapperDigest = CognitionQualityHash.Sha256(wrapper);
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection)) { Poison(); CryptographicOperations.ZeroMemory(wrapper); throw Failure(OllamaLoopbackTransportFailureCode.RuntimeChanged, SubmissionState.ResponseReceived, statusCode, wrapperDigest); }
            OfflineOllamaRecordingTransportResponse result = new(request, wrapper) { StatusCode = 200, MediaType = "application/json", ContentEncoding = null, IsRedirect = false, DeclaredBodyLength = wrapper.Length };
            wrapper = Array.Empty<byte>();
            return result;
        }
        finally
        {
            request.Dispose();
            ReleaseInFlight();
            if (body.Length != 0) CryptographicOperations.ZeroMemory(body);
            if (!ownershipTransferred) { response?.Dispose(); message?.Dispose(); }
        }
    }

    private void ReserveExactSlot(OfflineOllamaRecordingTransportRequest request)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (Volatile.Read(ref _poisoned) != 0) throw Failure(OllamaLoopbackTransportFailureCode.Poisoned, SubmissionState.DefinitelyNotSubmitted);
            if (_inFlight != 0) throw Failure(OllamaLoopbackTransportFailureCode.TransportFailure, SubmissionState.DefinitelyNotSubmitted);
            if (!string.Equals(request.Method, "POST", StringComparison.Ordinal) || !string.Equals(request.EndpointIdentity, SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity, StringComparison.Ordinal) || !string.Equals(request.Path, SnowGlobePinnedOllamaRecordingModule.GeneratePath, StringComparison.Ordinal) || !string.Equals(request.AdapterIdentity, SnowGlobePinnedOllamaRecordingModule.AdapterIdentity, StringComparison.Ordinal) || !string.Equals(request.AdapterContractDigestSha256, SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256, StringComparison.Ordinal) || request.BodyUtf8.Length is < 1 or > OfflineOllamaRecordingCodecModule.MaximumRequestBytes || request.SlotOrdinal is < 1 or > CognitionQualityCorpusV1.ScenarioCount) throw Failure(OllamaLoopbackTransportFailureCode.TransportFailure, SubmissionState.DefinitelyNotSubmitted);
            if (_capabilityDigest is null) _capabilityDigest = request.CapabilityDigestSha256;
            if (!string.Equals(_capabilityDigest, request.CapabilityDigestSha256, StringComparison.Ordinal) || request.SlotOrdinal != _nextSlot || _nextSlot > CognitionQualityCorpusV1.ScenarioCount) throw Failure(OllamaLoopbackTransportFailureCode.TransportFailure, SubmissionState.DefinitelyNotSubmitted);
            _nextSlot++; _inFlight = 1; Interlocked.Increment(ref _callCount);
        }
    }

    private static void ValidateOutboundMessage(HttpRequestMessage message)
    {
        if (message.Method != HttpMethod.Post || message.RequestUri != GenerateUri || message.Version != HttpVersion.Version11 || message.VersionPolicy != HttpVersionPolicy.RequestVersionExact || message.Headers.Authorization is not null || message.Headers.Contains("Cookie") || message.Headers.Contains("Proxy-Authorization") || message.Content is null || !IsExactApplicationJson(message.Content.Headers.ContentType)) throw Failure(OllamaLoopbackTransportFailureCode.TransportFailure, SubmissionState.DefinitelyNotSubmitted);
    }

    private static void ValidateResponseHeaders(HttpResponseMessage response, int statusCode)
    {
        if (statusCode != 200 || response.Version != HttpVersion.Version11 || response.Headers.Location is not null || response.Headers.TransferEncoding.Count != 0 || response.Headers.TransferEncodingChunked == true) throw Failure(OllamaLoopbackTransportFailureCode.HttpResponseRejected, SubmissionState.ResponseReceived, statusCode);
        HttpContentHeaders headers = response.Content.Headers;
        if (!IsExactApplicationJson(headers.ContentType) || headers.ContentEncoding.Count != 0 || headers.ContentLength is null || headers.ContentLength is < 1 or > OfflineOllamaRecordingCodecModule.MaximumWrapperBytes) throw Failure(OllamaLoopbackTransportFailureCode.HttpResponseRejected, SubmissionState.ResponseReceived, statusCode);
    }

    private static async Task<byte[]> ReadExactBodyAsync(HttpContent content, int declaredLength, CancellationToken cancellationToken)
    {
        byte[] scratch = new byte[OfflineOllamaRecordingCodecModule.MaximumWrapperBytes + 1];
        try
        {
            using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false); int total = 0;
            while (true) { int read = await stream.ReadAsync(scratch.AsMemory(total, scratch.Length - total), cancellationToken).ConfigureAwait(false); if (read == 0) break; total += read; if (total > OfflineOllamaRecordingCodecModule.MaximumWrapperBytes || total > declaredLength) throw new InvalidOperationException("live_ollama_body_size_invalid"); }
            if (total != declaredLength) throw new InvalidOperationException("live_ollama_body_length_invalid"); return scratch.AsSpan(0, total).ToArray();
        }
        finally { CryptographicOperations.ZeroMemory(scratch); }
    }

    private static async Task DrainAsync(Task task) => _ = await Task.WhenAny(task, Task.Delay(CancellationDrainMilliseconds)).ConfigureAwait(false);

    private void InstallLateSendObserver(Task<HttpResponseMessage> task, HttpRequestMessage message)
    {
        Interlocked.Increment(ref _observerCount); Task observer = Task.Run(async () => { await Task.Yield(); try { HttpResponseMessage late = await task.ConfigureAwait(false); late.Dispose(); } catch { } finally { message.Dispose(); CompleteDisposeIfRequested(); } }); lock (_gate) _lateObserver = observer;
    }
    private void InstallLateReadObserver(Task<byte[]> task, HttpResponseMessage response, HttpRequestMessage message)
    {
        Interlocked.Increment(ref _observerCount); Task observer = Task.Run(async () => { await Task.Yield(); try { byte[] late = await task.ConfigureAwait(false); CryptographicOperations.ZeroMemory(late); } catch { } finally { response.Dispose(); message.Dispose(); CompleteDisposeIfRequested(); } }); lock (_gate) _lateObserver = observer;
    }

    private bool Verify(OllamaLoopbackRuntimeCheckPoint point, OllamaLoopbackConnectionIdentity? connection)
    { try { return _runtimeVerifier.Verify(_binding, point, connection).Accepted; } catch { return false; } }
    private void Poison() => Interlocked.Exchange(ref _poisoned, 1);
    private void ReleaseInFlight() { bool complete; lock (_gate) { _inFlight = 0; complete = Volatile.Read(ref _disposed) != 0 && Volatile.Read(ref _disposeCancellationIssued) != 0 && _lateObserver is null; } if (complete) CompleteLateDispose(); }
    private static bool IsExactApplicationJson(MediaTypeHeaderValue? value) => value is not null && string.Equals(value.MediaType, "application/json", StringComparison.Ordinal) && value.Parameters.Count == 0;
    private static OllamaLoopbackTransportFailureCode ClassifyCancellationCause(CancellationToken session, CancellationToken disposal, CancellationToken timeout)
    {
        if (session.IsCancellationRequested || disposal.IsCancellationRequested) return OllamaLoopbackTransportFailureCode.Cancelled;
        if (timeout.IsCancellationRequested) return OllamaLoopbackTransportFailureCode.TimedOut;
        return OllamaLoopbackTransportFailureCode.TransportFailure;
    }
    private static OllamaLoopbackTransportException Failure(OllamaLoopbackTransportFailureCode code, SubmissionState submission, int? status = null, string? wrapperDigest = null) => new(code, submission, status, wrapperDigest);

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            Interlocked.Exchange(ref _disposed, 1);
            _disposeTask = DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await Task.Yield(); _disposeCancellation.Cancel(); Volatile.Write(ref _disposeCancellationIssued, 1);
        Task? observer = null; bool inFlight = true; long deadline = Environment.TickCount64 + CancellationDrainMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            lock (_gate) { observer = _lateObserver; inFlight = _inFlight != 0; }
            if (observer is not null || !inFlight) break;
            await Task.Delay(10).ConfigureAwait(false);
        }
        if (observer is null && !inFlight) { CompleteLateDispose(); return; }
        if (observer is null) return;
        await Task.WhenAny(observer, Task.Delay(CancellationDrainMilliseconds)).ConfigureAwait(false);
        if (observer.IsCompleted) CompleteLateDispose();
        else _ = observer.ContinueWith(static (_, state) => ((OllamaLoopbackRecordingTransportAdapter)state!).CompleteLateDispose(), this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private void CompleteLateDispose() { DisposeClient(); if (Interlocked.Exchange(ref _disposeCancellationDisposed, 1) == 0) _disposeCancellation.Dispose(); }
    private void CompleteDisposeIfRequested() { if (Volatile.Read(ref _disposed) != 0 && Volatile.Read(ref _disposeCancellationIssued) != 0) CompleteLateDispose(); }
    private void DisposeClient() { if (Interlocked.Exchange(ref _clientDisposed, 1) == 0) _client.Dispose(); }
}

/// <summary>Windows-only exact process/file/listener/connected-tuple verifier used only by RecordOnceAsync.</summary>
internal sealed class WindowsOllamaLoopbackRuntimeVerifier : IOllamaLoopbackRuntimeVerifier
{
    private const int AfInet = 2;
    private const int ErrorInsufficientBuffer = 122;
    private const int TcpTableOwnerPidAll = 5;
    private const int TcpRowBytes = 24;
    private const int MaximumTableBytes = 8 * 1024 * 1024;
    private const int StateListen = 2;
    private const int StateEstablished = 5;

    public OllamaLoopbackRuntimeVerification Verify(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return OllamaLoopbackRuntimeVerification.Reject("windows_required");
            SnowGlobePinnedOllamaRecordingModule.ValidateRuntimeBinding(binding);
            using Process process = Process.GetProcessById(binding.ProcessId);
            OllamaLoopbackRuntimeVerification processIdentity = VerifyProcessIdentity(binding, process.Id, process.HasExited, process.StartTime.ToUniversalTime().Ticks); if (!processIdentity.Accepted) return processIdentity;
            string? path = process.MainModule?.FileName;
            OllamaLoopbackRuntimeVerification processPath = VerifyProcessPath(binding, path); if (!processPath.Accepted) return processPath;
            using (FileStream stream = new(path!, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan))
            {
                string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                OllamaLoopbackRuntimeVerification processHash = VerifyExecutableHash(binding, digest); if (!processHash.Accepted) return processHash;
            }
            return VerifyTcpOwnership(binding, checkPoint, connection, SnapshotTcpRows(), Environment.ProcessId);
        }
        catch { return OllamaLoopbackRuntimeVerification.Reject("runtime_verification_failed"); }
    }

    internal static OllamaLoopbackRuntimeVerification VerifyProcessIdentity(OllamaLoopbackRuntimeBinding binding, int observedProcessId, bool hasExited, long observedStartUtcTicks) => observedProcessId == binding.ProcessId && !hasExited && observedStartUtcTicks == binding.ProcessStartUtcTicks ? OllamaLoopbackRuntimeVerification.Pass : OllamaLoopbackRuntimeVerification.Reject("process_identity_changed");
    internal static OllamaLoopbackRuntimeVerification VerifyProcessPath(OllamaLoopbackRuntimeBinding binding, string? observedCanonicalPath) => string.Equals(observedCanonicalPath, binding.CanonicalExecutablePath, StringComparison.Ordinal) ? OllamaLoopbackRuntimeVerification.Pass : OllamaLoopbackRuntimeVerification.Reject("process_path_changed");
    internal static OllamaLoopbackRuntimeVerification VerifyExecutableHash(OllamaLoopbackRuntimeBinding binding, string? observedSha256) => string.Equals(observedSha256, binding.ExecutableSha256, StringComparison.Ordinal) ? OllamaLoopbackRuntimeVerification.Pass : OllamaLoopbackRuntimeVerification.Reject("process_hash_changed");

    internal static OllamaLoopbackRuntimeVerification VerifyTcpOwnership(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection, IReadOnlyList<OllamaLoopbackTcpOwnerRow> rows, int currentProcessId)
    {
        OllamaLoopbackTcpOwnerRow[] listeners = rows.Where(static row => row.State == StateListen && row.LocalPort == 11435).ToArray();
        if (listeners.Length != 1 || !IsExactIpv4Loopback(listeners[0].LocalAddress) || listeners[0].ProcessId != binding.EndpointOwnerProcessId) return OllamaLoopbackRuntimeVerification.Reject("listener_owner_changed");
        if (checkPoint == OllamaLoopbackRuntimeCheckPoint.BeforeDispatch) return OllamaLoopbackRuntimeVerification.Pass;
        if (connection is null || connection.ServerProcessId != binding.ProcessId || connection.ServerPort != 11435 || connection.ClientProcessId != currentProcessId || connection.ClientPort is < 1 or > 65535) return OllamaLoopbackRuntimeVerification.Reject("connection_identity_missing");
        if (checkPoint == OllamaLoopbackRuntimeCheckPoint.AfterExchange) return OllamaLoopbackRuntimeVerification.Pass;
        bool server = rows.Any(row => row.State == StateEstablished && row.ProcessId == binding.ProcessId && row.LocalPort == 11435 && row.RemotePort == connection.ClientPort && IsExactIpv4Loopback(row.LocalAddress) && IsExactIpv4Loopback(row.RemoteAddress));
        bool client = rows.Any(row => row.State == StateEstablished && row.ProcessId == currentProcessId && row.LocalPort == connection.ClientPort && row.RemotePort == 11435 && IsExactIpv4Loopback(row.LocalAddress) && IsExactIpv4Loopback(row.RemoteAddress));
        return server && client ? OllamaLoopbackRuntimeVerification.Pass : OllamaLoopbackRuntimeVerification.Reject("connection_owner_changed");
    }

    private static OllamaLoopbackTcpOwnerRow[] SnapshotTcpRows()
    {
        int size = 0; int status = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
        if (status != ErrorInsufficientBuffer || size is < 4 or > MaximumTableBytes) throw new InvalidOperationException("tcp_table_size_invalid");
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            status = GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0); if (status != 0) throw new InvalidOperationException("tcp_table_read_failed");
            int count = Marshal.ReadInt32(buffer); if (count < 0 || count > (size - 4) / TcpRowBytes) throw new InvalidOperationException("tcp_table_count_invalid");
            OllamaLoopbackTcpOwnerRow[] rows = new OllamaLoopbackTcpOwnerRow[count]; IntPtr row = IntPtr.Add(buffer, 4);
            for (int index = 0; index < count; index++, row = IntPtr.Add(row, TcpRowBytes))
            {
                int state = Marshal.ReadInt32(row, 0); uint localAddress = unchecked((uint)Marshal.ReadInt32(row, 4)); uint localPortRaw = unchecked((uint)Marshal.ReadInt32(row, 8)); uint remoteAddress = unchecked((uint)Marshal.ReadInt32(row, 12)); uint remotePortRaw = unchecked((uint)Marshal.ReadInt32(row, 16)); int processId = Marshal.ReadInt32(row, 20);
                rows[index] = new OllamaLoopbackTcpOwnerRow(state, localAddress, DecodePort(localPortRaw), remoteAddress, DecodePort(remotePortRaw), processId);
            }
            return rows;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static int DecodePort(uint raw) => unchecked((ushort)IPAddress.NetworkToHostOrder(unchecked((short)(raw & 0xffff))));
    internal static bool IsExactIpv4Loopback(uint address) { byte[] bytes = BitConverter.GetBytes(address); return bytes.Length == 4 && bytes[0] == 127 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 1; }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool order, int addressFamily, int tableClass, uint reserved);
}
