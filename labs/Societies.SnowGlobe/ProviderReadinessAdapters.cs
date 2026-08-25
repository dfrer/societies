using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

internal sealed record OpenRouterAuthenticatedMetadataReadinessResult(
    bool Ready,
    string DiagnosticCode,
    int RequestCount,
    bool SameAccountBound);

internal interface IOpenRouterAuthenticatedReadinessMetadataVerifier
{
    ValueTask<OpenRouterAuthenticatedMetadataReadinessResult> ObserveReadinessOnceAsync(
        CancellationToken cancellationToken);
}

internal sealed class OpenRouterAuthenticatedReadinessAdapter(
    IOpenRouterAuthenticatedReadinessMetadataVerifier verifier)
    : IProviderReadinessObservationAdapter
{
    private int _used;
    internal const string SourceSchemaVersion = "openrouter_authenticated_metadata_readiness/v1";
    internal static string ContractDescriptor { get; } = SourceSchemaVersion +
        "|metadata_verifier_contract_digest=" + OpenRouterPremiumHttpMetadataVerifier.OfficialContractDigestSha256 +
        "|adapter=one-shot|ready=three_gets_same_account_bound" +
        "|unavailable=credential_unavailable,provider_unavailable,metadata_rejected" +
        "|unknown=operation_cancelled,observation_timeout,observation_failed" +
        "|no-post|no-generation|no-retry";
    internal static string SourceContractDigestSha256 { get; } =
        CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(ContractDescriptor));

    public ProviderReadinessProvider Provider => ProviderReadinessProvider.OpenRouter;

    internal static OpenRouterAuthenticatedReadinessAdapter CreateProduction() => new(
        OpenRouterPremiumHttpMetadataVerifier.CreateProduction(
            new OpenRouterPremiumWindowsCredentialStore(),
            SystemOpenRouterPremiumProductionClock.Instance));

    public async ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
            return ProviderReadinessAdapterResult.Unknown(
                "observation_failed", 0, SourceSchemaVersion,
                SourceContractDigestSha256, "not_performed");
        OpenRouterAuthenticatedMetadataReadinessResult result =
            await verifier.ObserveReadinessOnceAsync(cancellationToken).ConfigureAwait(false);
        if (result.Ready)
            return ProviderReadinessAdapterResult.Ready(
                result.RequestCount,
                SourceSchemaVersion,
                SourceContractDigestSha256,
                result.SameAccountBound ? "same_account_bound" : "not_performed");
        if (result.DiagnosticCode is "credential_unavailable" or "provider_unavailable" or "metadata_rejected")
            return ProviderReadinessAdapterResult.Unavailable(
                result.DiagnosticCode, result.RequestCount, SourceSchemaVersion,
                SourceContractDigestSha256, "not_performed");
        return ProviderReadinessAdapterResult.Unknown(
            result.DiagnosticCode is "operation_cancelled" or "observation_timeout"
                ? result.DiagnosticCode : "observation_failed",
            result.RequestCount,
            SourceSchemaVersion,
            SourceContractDigestSha256,
            "not_performed");
    }
}

internal sealed class OllamaAuthenticatedReadinessAdapter : IProviderReadinessObservationAdapter, IDisposable
{
    internal const string SourceSchemaVersion = "ollama_loopback_tags_readiness/v1";
    internal const int MaximumTagsResponseBytes = 64 * 1024;
    internal const int RequestTimeoutMilliseconds = 10_000;
    internal static string ContractDescriptor { get; } =
        "ollama_loopback_tags_readiness/v1|verified_at=2026-08-24|docs=https://docs.ollama.com/api/tags" +
        "|endpoint=http://127.0.0.1:11435/api/tags|method=GET|http=1.1-exact|no-authorization-header" +
        "|redirect-off|decompression-off|proxy-off|cookies-off|credentials-off|max-connection-1" +
        "|response-headers-read|response-headers-8k|body-max-65536|json-depth-5|strict-unknown-fields" +
        "|runtime-owner-before-connected-after-headers-after-exchange|exact-qwen3.5:4b-alias-and-artifact-digest" +
        "|predispatch_identity_rejection=unavailable_runtime_identity_drift" +
        "|postdispatch_identity_rejection=unknown_identity_race" +
        "|adapter=one-shot" +
        "|registered_cell_digest=" + SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256 +
        "|profile_digest=" + SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256 +
        "|runtime_executable_sha256=" + SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256 +
        "|runtime_model_reference=" + SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference +
        "|artifact_digest_sha256=" + SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256 +
        "|artifact_size_bytes=" + SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes.ToString(CultureInfo.InvariantCulture) +
        "|artifact_format=" + SnowGlobePinnedOllamaRecordingModule.ArtifactFormat +
        "|model_family=" + SnowGlobePinnedOllamaRecordingModule.ModelFamily +
        "|quantization_level=" + SnowGlobePinnedOllamaRecordingModule.QuantizationLevel +
        "|context_window_tokens=" + SnowGlobePinnedOllamaRecordingModule.ContextWindowTokens.ToString(CultureInfo.InvariantCulture) +
        "|tags_codec_schema=" + OllamaTagsMetadataCodec.SchemaVersion +
        "|tags_codec_contract_digest=" + OllamaTagsMetadataCodec.ContractDigestSha256 +
        "|no-generate|no-retry|no-fallback|no-alternate|no-pull|no-update";
    internal static string SourceContractDigestSha256 { get; } =
        CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(ContractDescriptor));
    internal static Uri TagsUri { get; } = new("http://127.0.0.1:11435/api/tags", UriKind.Absolute);

    private readonly OllamaLoopbackRuntimeBinding _binding;
    private readonly IOllamaLoopbackRuntimeVerifier _runtimeVerifier;
    private readonly HttpClient _client;
    private readonly SocketsHttpHandler? _productionHandler;
    private readonly object _gate = new();
    private OllamaLoopbackConnectionIdentity? _connection;
    private int _used;
    private int _disposed;

    internal OllamaAuthenticatedReadinessAdapter(
        OllamaLoopbackRuntimeBinding binding,
        IOllamaLoopbackRuntimeVerifier runtimeVerifier)
    {
        SnowGlobePinnedOllamaRecordingModule.ValidateRuntimeBinding(binding);
        _binding = binding with { };
        _runtimeVerifier = runtimeVerifier;
        _productionHandler = CreateProductionHandler();
        _client = new HttpClient(_productionHandler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = MaximumTagsResponseBytes
        };
    }

    internal OllamaAuthenticatedReadinessAdapter(
        OllamaLoopbackRuntimeBinding binding,
        IOllamaLoopbackRuntimeVerifier runtimeVerifier,
        HttpMessageHandler scriptedHandler)
    {
        SnowGlobePinnedOllamaRecordingModule.ValidateRuntimeBinding(binding);
        _binding = binding with { };
        _runtimeVerifier = runtimeVerifier;
        _client = new HttpClient(scriptedHandler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = MaximumTagsResponseBytes
        };
    }

    public ProviderReadinessProvider Provider => ProviderReadinessProvider.Ollama;
    internal SocketsHttpHandler? ProductionHandler => _productionHandler;

    internal static OllamaAuthenticatedReadinessAdapter CreateProduction(PinnedRuntimeObservation runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        OllamaLoopbackRuntimeBinding binding = new(
            runtime.ProcessId,
            runtime.ProcessStartUtcTicks,
            SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath,
            SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
            SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity,
            runtime.ProcessId);
        return new(binding, new WindowsOllamaLoopbackRuntimeVerifier());
    }

    public async ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0 || Volatile.Read(ref _disposed) != 0)
            return Unknown("observation_failed", 0);
        if (!Verify(OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null))
            return Unavailable("runtime_identity_drift", 0);
        if (cancellationToken.IsCancellationRequested)
            return Unknown("operation_cancelled", 0);

        int requestCount = 0;
        byte[]? tagsBytes = null;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeoutMilliseconds);
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TagsUri)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.ExpectContinue = false;
            requestCount++;
            using HttpResponseMessage response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            OllamaLoopbackConnectionIdentity? connection;
            lock (_gate) connection = _connection;
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection))
                return Unknown("identity_race", requestCount);
            ValidateResponse(response);
            tagsBytes = await ReadBoundedAsync(response.Content, timeout.Token).ConfigureAwait(false);
            if (response.TrailingHeaders.Count() != 0)
                throw new LocalModelBenchmarkException("tags_json_invalid");
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection))
                return Unknown("identity_race", requestCount);
            _ = OllamaTagsMetadataCodec.Validate(tagsBytes, ExpectedModel());
            return ProviderReadinessAdapterResult.Ready(
                requestCount, SourceSchemaVersion, SourceContractDigestSha256, "not_applicable");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Unknown("operation_cancelled", requestCount);
        }
        catch (OperationCanceledException)
        {
            return Unknown("observation_timeout", requestCount);
        }
        catch (LocalModelBenchmarkException)
        {
            return Unavailable("model_metadata_rejected", requestCount);
        }
        catch (OllamaReadinessIdentityRaceException)
        {
            return Unknown("identity_race", requestCount);
        }
        catch (HttpRequestException exception) when (exception.InnerException is OllamaReadinessIdentityRaceException)
        {
            return Unknown("identity_race", requestCount);
        }
        catch (HttpRequestException)
        {
            return Unavailable("provider_unavailable", requestCount);
        }
        catch
        {
            return Unknown("observation_failed", requestCount);
        }
        finally
        {
            if (tagsBytes is not null) CryptographicOperations.ZeroMemory(tagsBytes);
        }
    }

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
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.Zero,
        PooledConnectionIdleTimeout = TimeSpan.Zero,
        MaxResponseHeadersLength = 8,
        MaxResponseDrainSize = 0,
        ActivityHeadersPropagator = null,
        ConnectCallback = ConnectExactLoopbackAsync
    };

    private async ValueTask<Stream> ConnectExactLoopbackAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (context.DnsEndPoint.Host != "127.0.0.1" || context.DnsEndPoint.Port != 11435)
            throw new HttpRequestException("loopback_target_rejected");
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 11435), cancellationToken)
                .ConfigureAwait(false);
            int clientPort = ((IPEndPoint)socket.LocalEndPoint!).Port;
            OllamaLoopbackConnectionIdentity connection = new(
                Environment.ProcessId, clientPort, _binding.ProcessId, 11435);
            if (!Verify(OllamaLoopbackRuntimeCheckPoint.ConnectedBeforeBody, connection))
                throw new OllamaReadinessIdentityRaceException();
            lock (_gate) _connection = connection;
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal sealed class OllamaReadinessIdentityRaceException : Exception;

    private static void ValidateResponse(HttpResponseMessage response)
    {
        if (response.RequestMessage?.RequestUri?.AbsoluteUri != TagsUri.AbsoluteUri
            || response.RequestMessage.Method != HttpMethod.Get
            || response.RequestMessage.Content is not null
            || response.RequestMessage.Headers.Authorization is not null
            || response.Version != HttpVersion.Version11
            || (int)response.StatusCode != 200)
            throw new HttpRequestException("tags_response_rejected");
        if (response.Content.Headers.ContentEncoding.Count != 0
            || !string.Equals(response.Content.Headers.ContentType?.MediaType,
                "application/json", StringComparison.OrdinalIgnoreCase))
            throw new LocalModelBenchmarkException("tags_json_invalid");
        string? charset = response.Content.Headers.ContentType?.CharSet;
        if (charset is not null && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
            throw new LocalModelBenchmarkException("tags_json_invalid");
        if (response.Content.Headers.ContentLength is long length
            && (length < 1 || length > MaximumTagsResponseBytes))
            throw new LocalModelBenchmarkException("tags_json_invalid");
        if (response.TrailingHeaders.Count() != 0)
            throw new LocalModelBenchmarkException("tags_json_invalid");
    }

    internal static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken,
        Action<bool>? internalBufferZeroObserver = null)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream output = new(Math.Min(MaximumTagsResponseBytes, 16 * 1024));
        byte[] buffer = new byte[8 * 1024];
        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (output.Length + read > MaximumTagsResponseBytes)
                    throw new LocalModelBenchmarkException("tags_json_invalid");
                output.Write(buffer, 0, read);
            }
            if (output.Length == 0) throw new LocalModelBenchmarkException("tags_json_invalid");
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            byte[] internalBuffer = output.GetBuffer();
            CryptographicOperations.ZeroMemory(internalBuffer);
            internalBufferZeroObserver?.Invoke(internalBuffer.All(static value => value == 0));
        }
    }

    private bool Verify(
        OllamaLoopbackRuntimeCheckPoint checkPoint,
        OllamaLoopbackConnectionIdentity? connection) =>
        _runtimeVerifier.Verify(_binding, checkPoint, connection).Accepted;

    private static OllamaTagsExpectedModel ExpectedModel() => new(
        SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
        SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
        SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes,
        SnowGlobePinnedOllamaRecordingModule.ArtifactFormat,
        SnowGlobePinnedOllamaRecordingModule.ModelFamily,
        null,
        SnowGlobePinnedOllamaRecordingModule.QuantizationLevel,
        SnowGlobePinnedOllamaRecordingModule.ContextWindowTokens);

    private static ProviderReadinessAdapterResult Unavailable(string code, int count) =>
        ProviderReadinessAdapterResult.Unavailable(
            code, count, SourceSchemaVersion, SourceContractDigestSha256, "not_applicable");

    private static ProviderReadinessAdapterResult Unknown(string code, int count) =>
        ProviderReadinessAdapterResult.Unknown(
            code, count, SourceSchemaVersion, SourceContractDigestSha256, "not_applicable");

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _client.Dispose();
    }
}
