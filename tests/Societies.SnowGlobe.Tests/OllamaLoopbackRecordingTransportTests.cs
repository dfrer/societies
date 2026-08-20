using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Xunit;

namespace Societies.SnowGlobe.Tests;

[Collection("OllamaLoopbackRecordingSerial")]
public sealed class OllamaLoopbackRecordingTransportTests
{
    [Fact]
    public async Task ProductionHandlerPolicy_IsExactAndConstructionPerformsNoIo()
    {
        using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier());
        SocketsHttpHandler handler = Assert.IsType<SocketsHttpHandler>(adapter.ProductionHandler);
        Assert.False(handler.AllowAutoRedirect); Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression); Assert.False(handler.UseProxy); Assert.Null(handler.Proxy); Assert.False(handler.UseCookies); Assert.Null(handler.Credentials); Assert.False(handler.PreAuthenticate); Assert.Equal(1, handler.MaxConnectionsPerServer); Assert.NotNull(handler.ConnectCallback);
        Assert.StartsWith("snow-globe-ollama-loopback-recording-transport-adapter/v2|", OllamaLoopbackRecordingTransportAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains("content-type-application-json-none-or-one-charset-utf-8", OllamaLoopbackRecordingTransportAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains("typed-terminal-checkpoint-policy", OllamaLoopbackRecordingTransportAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Equal(0, adapter.CallCount); Assert.Equal(0, adapter.LateObserverCount); Assert.False(adapter.IsPoisoned);
        await adapter.DisposeAsync();
    }

    [Fact]
    public void WindowsVerifierPureTcpPolicy_DecodesNetworkPortAndRequiresExactListenerAndTupleOrientation()
    {
        const uint loopback = 0x0100007f; const int currentProcessId = 42; const int clientPort = 54321; OllamaLoopbackRuntimeBinding binding = Binding(); OllamaLoopbackConnectionIdentity connection = new(currentProcessId, clientPort, binding.ProcessId, 11435);
        uint encodedPort = unchecked((ushort)IPAddress.HostToNetworkOrder(unchecked((short)11435))); Assert.Equal(11435, WindowsOllamaLoopbackRuntimeVerifier.DecodePort(encodedPort)); Assert.True(WindowsOllamaLoopbackRuntimeVerifier.IsExactIpv4Loopback(loopback)); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.IsExactIpv4Loopback(0)); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.IsExactIpv4Loopback(0x0200007f));
        OllamaLoopbackTcpOwnerRow listener = new(2, loopback, 11435, 0, 0, binding.ProcessId); OllamaLoopbackTcpOwnerRow server = new(5, loopback, 11435, loopback, clientPort, binding.ProcessId); OllamaLoopbackTcpOwnerRow client = new(5, loopback, clientPort, loopback, 11435, currentProcessId); OllamaLoopbackTcpOwnerRow[] valid = [listener, server, client];
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null, [listener], currentProcessId).Accepted); Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.ConnectedBeforeBody, connection, valid, currentProcessId).Accepted); Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection, valid, currentProcessId).Accepted); Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection, valid, currentProcessId).Accepted);
        Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null, [listener, listener], currentProcessId).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null, [listener with { LocalAddress = 0 }], currentProcessId).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.BeforeDispatch, null, [listener with { ProcessId = binding.ProcessId + 1 }], currentProcessId).Accepted);
        OllamaLoopbackTcpOwnerRow reversedServer = server with { LocalPort = clientPort, RemotePort = 11435 }; Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection, [listener, reversedServer, client], currentProcessId).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection with { ClientProcessId = currentProcessId + 1 }, valid, currentProcessId).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection, [listener, server with { State = 2 }, client], currentProcessId).Accepted);
    }

    [Fact]
    public void WindowsVerifierPureTcpPolicy_AfterExchangeAllowsClosedTupleButStillRejectsListenerAndProcessIdentityChanges()
    {
        const uint loopback = 0x0100007f; const int currentProcessId = 42; const int clientPort = 54321; OllamaLoopbackRuntimeBinding binding = Binding(); OllamaLoopbackConnectionIdentity connection = new(currentProcessId, clientPort, binding.ProcessId, 11435);
        OllamaLoopbackTcpOwnerRow listener = new(2, loopback, 11435, 0, 0, binding.ProcessId);
        OllamaLoopbackTcpOwnerRow server = new(5, loopback, 11435, loopback, clientPort, binding.ProcessId); OllamaLoopbackTcpOwnerRow client = new(5, loopback, clientPort, loopback, 11435, currentProcessId);

        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.ConnectedBeforeBody, connection, [listener, server, client], currentProcessId).Accepted);
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, connection, [listener, server, client], currentProcessId).Accepted);
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection, [listener], currentProcessId).Accepted);
        Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection, [], currentProcessId).Accepted);
        Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyTcpOwnership(binding, OllamaLoopbackRuntimeCheckPoint.AfterExchange, connection, [listener with { ProcessId = binding.ProcessId + 1 }], currentProcessId).Accepted);
        Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId + 1, false, binding.ProcessStartUtcTicks).Accepted);
        Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId, false, binding.ProcessStartUtcTicks + 1).Accepted);
    }

    [Fact]
    public void WindowsVerifierPureProcessPolicy_RejectsPidReuseExitStartPathAndHashDrift()
    {
        OllamaLoopbackRuntimeBinding binding = Binding();
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId, false, binding.ProcessStartUtcTicks).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId + 1, false, binding.ProcessStartUtcTicks).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId, true, binding.ProcessStartUtcTicks).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessIdentity(binding, binding.ProcessId, false, binding.ProcessStartUtcTicks + 1).Accepted);
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessPath(binding, binding.CanonicalExecutablePath).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessPath(binding, binding.CanonicalExecutablePath.ToLowerInvariant()).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyProcessPath(binding, null).Accepted);
        Assert.True(WindowsOllamaLoopbackRuntimeVerifier.VerifyExecutableHash(binding, binding.ExecutableSha256).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyExecutableHash(binding, binding.ExecutableSha256.ToUpperInvariant()).Accepted); Assert.False(WindowsOllamaLoopbackRuntimeVerifier.VerifyExecutableHash(binding, null).Accepted);
    }

    [Theory]
    [MemberData(nameof(RejectedStatuses))]
    public async Task EveryNon200Status_IsResponseReceivedNotApplicableAndNeverRetried(int status)
    {
        InspectingHandler handler = new(_ => Response((HttpStatusCode)status, Wrapper())); await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler);
        using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());
        Assert.Equal(OllamaLoopbackTransportFailureCode.HttpResponseRejected, exception.Code); Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.Equal(status, exception.StatusCode); Assert.Equal(OllamaRecordingTerminalCheckpointCode.ResponseHeaders, exception.Checkpoint); Assert.Equal(OllamaRecordingTerminalPolicyCode.HttpStatus, exception.Policy); Assert.False(exception.AdditionalAttemptAuthorized); Assert.Equal(exception.Code.ToString(), exception.Message); Assert.Equal(1, handler.CallCount); Assert.Equal(1, adapter.CallCount);
    }

    public static IEnumerable<object[]> RejectedStatuses()
    {
        int[] statuses = [201, 204, 206, 300, 301, 302, 303, 307, 308, 401, 403, 407, 408, 425, 429, 500, 502, 503, 504];
        return statuses.Select(static status => new object[] { status });
    }

    [Fact]
    public async Task Exact200Request_IsHttp11PostNoAmbientAuthorityAndReturnsOwnedBoundedBody()
    {
        byte[] callerWrapper = Wrapper(); InspectingHandler handler = new(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("http://127.0.0.1:11435/api/generate", request.RequestUri!.AbsoluteUri); Assert.Equal(HttpVersion.Version11, request.Version); Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy); Assert.False(request.Headers.ExpectContinue); Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie")); Assert.False(request.Headers.Contains("Proxy-Authorization")); Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType); Assert.Null(request.Content.Headers.ContentType.CharSet);
            return Response(HttpStatusCode.OK, callerWrapper.ToArray());
        });
        await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        using OfflineOllamaRecordingTransportResponse response = await adapter.ExchangeOnceAsync(encoded, CancellationToken.None); Assert.Equal(200, response.StatusCode); Assert.Equal(callerWrapper, response.BodyUtf8.ToArray()); Assert.Equal(1, handler.CallCount); Assert.Equal(1, adapter.CallCount);
    }

    [Fact]
    public async Task PinnedRuntimeOfficialJsonUtf8ContentType_IsAccepted()
    {
        byte[] callerWrapper = Wrapper(); InspectingHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response(HttpStatusCode.OK, callerWrapper.ToArray());
            response.Content.Headers.ContentType!.CharSet = "utf-8";
            return response;
        });
        await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler);
        using CognitionQualityRecordingAdapterRequest source = Request(1);
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);

        using OfflineOllamaRecordingTransportResponse response = await adapter.ExchangeOnceAsync(encoded, CancellationToken.None);

        Assert.Equal(200, response.StatusCode); Assert.Equal(callerWrapper, response.BodyUtf8.ToArray());
        Assert.Equal(1, handler.CallCount); Assert.Equal(1, adapter.CallCount);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json;charset=utf-8")]
    [InlineData("application/json;CHARSET=UTF-8")]
    [InlineData("application/json;charset=\"utf-8\"")]
    [InlineData("APPLICATION/JSON;Charset=\"UTF-8\"")]
    public void PinnedRuntimeContentType_ExactAcceptedParsedShapes(string value) =>
        Assert.True(OllamaLoopbackRecordingTransportAdapter.IsPinnedRuntimeApplicationJson(MediaTypeHeaderValue.Parse(value)));

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/problem+json")]
    [InlineData("application/json;charset=utf-16")]
    [InlineData("application/json;profile=unexpected")]
    [InlineData("application/json;charset=utf-8;profile=unexpected")]
    [InlineData("application/json;charset=utf-8;charset=utf-8")]
    public void PinnedRuntimeContentType_RejectsEveryOtherOrAdditionalParsedShape(string value) =>
        Assert.False(OllamaLoopbackRecordingTransportAdapter.IsPinnedRuntimeApplicationJson(MediaTypeHeaderValue.Parse(value)));

    [Fact]
    public async Task ResponseHeaderPolicy_UsesDeterministicFirstFailureOrder()
    {
        foreach ((Func<HttpResponseMessage> Create, OllamaRecordingTerminalPolicyCode Expected) value in new[]
        {
            ((Func<HttpResponseMessage>)(() => Mutate(Response(HttpStatusCode.OK, Wrapper()), response =>
            {
                response.Version = HttpVersion.Version20;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            })), OllamaRecordingTerminalPolicyCode.HttpVersion),
            ((Func<HttpResponseMessage>)(() => Mutate(Response(HttpStatusCode.OK, Wrapper()), response =>
            {
                response.Headers.TransferEncodingChunked = true;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            })), OllamaRecordingTerminalPolicyCode.TransferEncoding)
        })
        {
            InspectingHandler handler = new(_ => value.Create());
            await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler);
            using CognitionQualityRecordingAdapterRequest source = Request(1);
            using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
            OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());
            Assert.Equal(OllamaRecordingTerminalCheckpointCode.ResponseHeaders, exception.Checkpoint);
            Assert.Equal(value.Expected, exception.Policy);
        }
    }

    [Fact]
    public async Task MalformedOrDuplicateRawContentType_IsClosedAsTypedContentPolicy()
    {
        foreach (string[] rawValues in new[]
        {
            new[] { "application/json;charset=\"" },
            new[] { "application/json", "application/json;charset=utf-8" }
        })
        {
            InspectingHandler handler = new(_ => Mutate(Response(HttpStatusCode.OK, Wrapper()), response =>
            {
                response.Content.Headers.Remove("Content-Type");
                Assert.True(response.Content.Headers.TryAddWithoutValidation("Content-Type", rawValues));
            }));
            await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler);
            using CognitionQualityRecordingAdapterRequest source = Request(1);
            using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);

            OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());

            Assert.Equal(OllamaLoopbackTransportFailureCode.HttpResponseRejected, exception.Code);
            Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState); Assert.Equal(200, exception.StatusCode);
            Assert.Equal(OllamaRecordingTerminalCheckpointCode.ResponseHeaders, exception.Checkpoint);
            Assert.Equal(OllamaRecordingTerminalPolicyCode.ContentType, exception.Policy);
            Assert.Null(exception.InnerException); Assert.Equal(exception.Code.ToString(), exception.Message);
        }
    }

    [Fact]
    public async Task RawHeaderParserAbuse_IsRejectedWithExactFirstPolicy()
    {
        (string Name, string[] Values, OllamaRecordingTerminalPolicyCode Policy, byte[] Body)[] cases =
        [
            ("Location", ["http://["], OllamaRecordingTerminalPolicyCode.Redirect, Wrapper()),
            ("Transfer-Encoding", ["@@@"], OllamaRecordingTerminalPolicyCode.TransferEncoding, Wrapper()),
            ("Transfer-Encoding", [string.Empty], OllamaRecordingTerminalPolicyCode.TransferEncoding, Wrapper()),
            ("Content-Encoding", ["@@@"], OllamaRecordingTerminalPolicyCode.ContentEncoding, Wrapper()),
            ("Content-Encoding", [string.Empty], OllamaRecordingTerminalPolicyCode.ContentEncoding, Wrapper()),
            ("Content-Length", ["3", "4"], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", ["3", "3"], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", ["003"], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", ["+3"], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", ["-3"], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", [string.Empty], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray()),
            ("Content-Length", [" 3 "], OllamaRecordingTerminalPolicyCode.ContentLength, "abc"u8.ToArray())
        ];

        foreach ((string name, string[] values, OllamaRecordingTerminalPolicyCode policy, byte[] body) in cases)
        {
            InspectingHandler handler = new(_ => Mutate(Response(HttpStatusCode.OK, body), response =>
            {
                HttpHeaders headers = name is "Location" or "Transfer-Encoding" ? response.Headers : response.Content.Headers;
                headers.Remove(name);
                Assert.True(headers.TryAddWithoutValidation(name, values));
            }));
            await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler);
            using CognitionQualityRecordingAdapterRequest source = Request(1);
            using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);

            OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());

            Assert.Equal(OllamaLoopbackTransportFailureCode.HttpResponseRejected, exception.Code);
            Assert.Equal(OllamaRecordingTerminalCheckpointCode.ResponseHeaders, exception.Checkpoint);
            Assert.Equal(policy, exception.Policy); Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState);
            Assert.Equal(200, exception.StatusCode); Assert.Equal(exception.Code.ToString(), exception.Message); Assert.Null(exception.InnerException);
            string rawSentinel = string.Join('|', values);
            if (rawSentinel.Length != 0) Assert.DoesNotContain(rawSentinel, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RequestContent_SerializesExactlyOnceAndZerosOwnedBytes()
    {
        byte[] owned = "secret-request-body"u8.ToArray(); using SingleUseOllamaJsonContent content = new(owned); await content.CopyToAsync(Stream.Null); await Assert.ThrowsAsync<InvalidOperationException>(() => content.CopyToAsync(Stream.Null)); Assert.True(content.HasSerializationBegun); Assert.Equal(1, content.SecondSerializationAttemptCount); content.Dispose(); Assert.All(owned, static value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task ResponseHeaders_RejectMediaCharsetRedirectEncodingTransferAndLengthViolations()
    {
        List<Func<HttpResponseMessage>> cases =
        [
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentType = new("text/plain")),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentType!.CharSet = "utf-16"),
            () => WithContentTypeParameters(Response(HttpStatusCode.OK, Wrapper()), new NameValueHeaderValue("profile", "\"unexpected\"")),
            () => WithContentTypeParameters(Response(HttpStatusCode.OK, Wrapper()), new NameValueHeaderValue("profile", "\"one\""), new NameValueHeaderValue("profile", "\"two\"")),
            () => WithContentTypeParameters(Response(HttpStatusCode.OK, Wrapper()), new NameValueHeaderValue("unexpected", "value")),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Headers.Location = new Uri("http://127.0.0.1:11435/other")),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentEncoding.Add("gzip")),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Headers.TransferEncodingChunked = true),
            () => new HttpResponseMessage(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = new UnknownLengthContent(Wrapper()) },
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentLength = 0),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentLength = OfflineOllamaRecordingCodecModule.MaximumWrapperBytes + 1),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentLength = Wrapper().Length + 1),
            () => Mutate(Response(HttpStatusCode.OK, Wrapper()), response => response.Content.Headers.ContentLength = Wrapper().Length - 1)
        ];
        foreach (Func<HttpResponseMessage> create in cases)
        {
            InspectingHandler handler = new(_ => create()); await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
            OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask()); Assert.Contains(exception.Code, new[] { OllamaLoopbackTransportFailureCode.HttpResponseRejected, OllamaLoopbackTransportFailureCode.ResponseBodyRejected }); Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.Equal(1, handler.CallCount);
        }
    }

    [Theory]
    [InlineData(false, SubmissionState.DefinitelyNotSubmitted)]
    [InlineData(true, SubmissionState.SubmissionUnknown)]
    public async Task UncausedOperationCanceledDuringSend_IsTransportFailureAndPreservesSubmissionFence(bool serializeBody, SubmissionState expected)
    {
        UncausedCancellationHandler handler = new(serializeBody); await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());
        Assert.Equal(OllamaLoopbackTransportFailureCode.TransportFailure, exception.Code); Assert.Equal(expected, exception.SubmissionState); Assert.Equal(OllamaRecordingTerminalCheckpointCode.RequestDispatch, exception.Checkpoint); Assert.Equal(OllamaRecordingTerminalPolicyCode.TransportIo, exception.Policy); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.False(exception.AdditionalAttemptAuthorized); Assert.Equal("TransportFailure", exception.Message); Assert.Equal(1, handler.CallCount); Assert.True(adapter.IsPoisoned);
    }

    [Fact]
    public async Task UncausedOperationCanceledDuringBodyRead_IsTransportFailureAfterResponseHeaders()
    {
        InspectingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = new UncausedCancellationBodyContent() }); await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());
        Assert.Equal(OllamaLoopbackTransportFailureCode.TransportFailure, exception.Code); Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState); Assert.Equal(200, exception.StatusCode); Assert.Equal(OllamaRecordingTerminalCheckpointCode.ResponseBody, exception.Checkpoint); Assert.Equal(OllamaRecordingTerminalPolicyCode.BodyRead, exception.Policy); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.False(exception.AdditionalAttemptAuthorized); Assert.Equal("TransportFailure", exception.Message); Assert.True(adapter.IsPoisoned);
    }

    [Fact]
    public async Task PostBodyRuntimeRejection_ExposesDigestOnlyAndRetainsNoResponseObject()
    {
        byte[] wrapper = Wrapper(); InspectingHandler handler = new(_ => Response(HttpStatusCode.OK, wrapper.ToArray()));
        RejectAfterExchangeVerifier verifier = new();
        await using OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), verifier, handler);
        using CognitionQualityRecordingAdapterRequest source = Request(1);
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);

        OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask());

        Assert.Equal(OllamaLoopbackTransportFailureCode.RuntimeChanged, exception.Code);
        Assert.Equal(OllamaRecordingTerminalCheckpointCode.AfterExchange, exception.Checkpoint);
        Assert.Equal(OllamaRecordingTerminalPolicyCode.RuntimeOwnership, exception.Policy);
        Assert.Equal(CognitionQualityHash.Sha256(wrapper), exception.WrapperDigestSha256);
        Assert.Equal(exception.Code.ToString(), exception.Message); Assert.Null(exception.InnerException);
        Assert.DoesNotContain(Encoding.UTF8.GetString(wrapper), exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount); Assert.Equal(1, adapter.CallCount); Assert.True(adapter.IsPoisoned);
    }

    [Theory]
    [InlineData(false, SubmissionState.DefinitelyNotSubmitted)]
    [InlineData(true, SubmissionState.SubmissionUnknown)]
    public async Task CancellationIgnoringSend_IsBoundedClassifiedPoisonedAndObservedExactlyOnce(bool serializeBody, SubmissionState expected)
    {
        IgnoringSendHandler handler = new(serializeBody); OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); using CancellationTokenSource cancellation = new();
        Task<OfflineOllamaRecordingTransportResponse> pending = adapter.ExchangeOnceAsync(encoded, cancellation.Token).AsTask(); await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel();
        OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => pending); Assert.Equal(expected, exception.SubmissionState); Assert.Equal(OllamaLoopbackTransportFailureCode.Cancelled, exception.Code); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.True(adapter.IsPoisoned); Assert.Equal(1, adapter.LateObserverCount);
        using CognitionQualityRecordingAdapterRequest secondSource = Request(2); using OfflineOllamaRecordingTransportRequest second = OfflineOllamaRecordingCodecModule.Encode(secondSource, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); OllamaLoopbackTransportException poisoned = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => adapter.ExchangeOnceAsync(second, CancellationToken.None).AsTask()); Assert.Equal(OllamaLoopbackTransportFailureCode.Poisoned, poisoned.Code); Assert.Equal(1, handler.CallCount);
        TrackingByteArrayContent lateContent = new(Wrapper()); HttpResponseMessage lateResponse = new(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = lateContent }; lateContent.Headers.ContentType = new("application/json"); handler.Release.TrySetResult(lateResponse); await EventuallyAsync(() => lateContent.IsDisposed);
        await Task.WhenAll(adapter.DisposeAsync().AsTask(), adapter.DisposeAsync().AsTask(), adapter.DisposeAsync().AsTask()); Assert.Equal(1, adapter.LateObserverCount);
    }

    [Fact]
    public async Task CancellationIgnoringResponseBody_IsResponseReceivedBoundedAndConcurrentDisposeSafe()
    {
        HangingBodyContent body = new(); InspectingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = body }); OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); using CancellationTokenSource cancellation = new();
        Task<OfflineOllamaRecordingTransportResponse> pending = adapter.ExchangeOnceAsync(encoded, cancellation.Token).AsTask(); await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel(); OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => pending);
        Assert.Equal(SubmissionState.ResponseReceived, exception.SubmissionState); Assert.Equal(200, exception.StatusCode); Assert.True(adapter.IsPoisoned); Assert.Equal(1, adapter.LateObserverCount);
        Task allDisposals = Task.WhenAll(adapter.DisposeAsync().AsTask(), adapter.DisposeAsync().AsTask(), adapter.DisposeAsync().AsTask()); await allDisposals.WaitAsync(TimeSpan.FromSeconds(2));
        body.Release.TrySetResult(true); await EventuallyAsync(() => body.IsDisposed); Assert.Equal(1, adapter.LateObserverCount);
    }

    [Theory]
    [InlineData(false, SubmissionState.DefinitelyNotSubmitted)]
    [InlineData(true, SubmissionState.SubmissionUnknown)]
    public async Task SessionTimeoutBeforeOrAfterSerialization_UsesClosedClassificationAndNoRetry(bool serializeBody, SubmissionState expected)
    {
        IgnoringSendHandler handler = new(serializeBody); OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1, 20); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        Task<OfflineOllamaRecordingTransportResponse> pending = adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask(); await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); OllamaLoopbackTransportException exception = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => pending);
        Assert.Equal(OllamaLoopbackTransportFailureCode.TimedOut, exception.Code); Assert.Equal(expected, exception.SubmissionState); Assert.Equal(ChargeState.NotApplicable, exception.ChargeState); Assert.False(exception.AdditionalAttemptAuthorized); Assert.Equal(1, adapter.LateObserverCount); Assert.True(adapter.IsPoisoned);
        handler.Release.TrySetResult(Response(HttpStatusCode.OK, Wrapper())); await adapter.DisposeAsync();
    }

    [Fact]
    public async Task DisposeRacingCancellationIgnoringSend_IsBoundedAndDoesNotDisposeLateOwnedBuffersEarly()
    {
        IgnoringSendHandler handler = new(true); OllamaLoopbackRecordingTransportAdapter adapter = new(Binding(), new PassVerifier(), handler); using CognitionQualityRecordingAdapterRequest source = Request(1); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile);
        Task<OfflineOllamaRecordingTransportResponse> exchange = adapter.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask(); await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); Task firstDispose = adapter.DisposeAsync().AsTask(); Task secondDispose = adapter.DisposeAsync().AsTask(); await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(2));
        OllamaLoopbackTransportException terminal = await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => exchange); Assert.Equal(SubmissionState.SubmissionUnknown, terminal.SubmissionState); Assert.Equal(1, adapter.LateObserverCount);
        TrackingByteArrayContent lateContent = new(Wrapper()); HttpResponseMessage lateResponse = new(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = lateContent }; lateContent.Headers.ContentType = new("application/json"); Assert.False(lateContent.IsDisposed); handler.Release.TrySetResult(lateResponse); await EventuallyAsync(() => lateContent.IsDisposed); Assert.Equal(1, adapter.LateObserverCount);
    }

    [Fact]
    public async Task WrongDuplicateSkippedConcurrentAndThirteenthSlotsAreRejectedWithoutHiddenCalls()
    {
        InspectingHandler skippedHandler = new(_ => Response(HttpStatusCode.OK, Wrapper())); await using (OllamaLoopbackRecordingTransportAdapter skipped = new(Binding(), new PassVerifier(), skippedHandler))
        {
            using CognitionQualityRecordingAdapterRequest source = Request(2); using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => skipped.ExchangeOnceAsync(encoded, CancellationToken.None).AsTask()); Assert.Equal(0, skippedHandler.CallCount);
        }
        IgnoringSendHandler heldHandler = new(true); OllamaLoopbackRecordingTransportAdapter concurrent = new(Binding(), new PassVerifier(), heldHandler); using CognitionQualityRecordingAdapterRequest firstSource = Request(1); using OfflineOllamaRecordingTransportRequest first = OfflineOllamaRecordingCodecModule.Encode(firstSource, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); Task<OfflineOllamaRecordingTransportResponse> held = concurrent.ExchangeOnceAsync(first, CancellationToken.None).AsTask(); await heldHandler.Started.Task;
        using CognitionQualityRecordingAdapterRequest concurrentSource = Request(2); using OfflineOllamaRecordingTransportRequest concurrentRequest = OfflineOllamaRecordingCodecModule.Encode(concurrentSource, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => concurrent.ExchangeOnceAsync(concurrentRequest, CancellationToken.None).AsTask()); Assert.Equal(1, heldHandler.CallCount);
        heldHandler.Release.TrySetResult(Response(HttpStatusCode.OK, Wrapper())); using OfflineOllamaRecordingTransportResponse completed = await held;
        using CognitionQualityRecordingAdapterRequest duplicateSource = Request(1); using OfflineOllamaRecordingTransportRequest duplicate = OfflineOllamaRecordingCodecModule.Encode(duplicateSource, SnowGlobePinnedOllamaRecordingModule.LiveCodecProfile); await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => concurrent.ExchangeOnceAsync(duplicate, CancellationToken.None).AsTask());
        using CognitionQualityRecordingAdapterRequest thirteenthSource = Request(13); using OfflineOllamaRecordingTransportRequest thirteenth = new(thirteenthSource, "{}"u8.ToArray()); await Assert.ThrowsAsync<OllamaLoopbackTransportException>(() => concurrent.ExchangeOnceAsync(thirteenth, CancellationToken.None).AsTask()); await concurrent.DisposeAsync();
    }

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] body)
    { HttpResponseMessage response = new(status) { Version = HttpVersion.Version11, Content = new ByteArrayContent(body) }; response.Content.Headers.ContentType = new("application/json"); response.Content.Headers.ContentLength = body.Length; return response; }
    private static HttpResponseMessage Mutate(HttpResponseMessage response, Action<HttpResponseMessage> mutate) { mutate(response); return response; }
    private static HttpResponseMessage WithContentTypeParameters(HttpResponseMessage response, params NameValueHeaderValue[] parameters) { foreach (NameValueHeaderValue parameter in parameters) response.Content.Headers.ContentType!.Parameters.Add(parameter); return response; }
    private static OllamaLoopbackRuntimeBinding Binding() => new(777, new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256, SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity, 777);
    private static CognitionQualityRecordingAdapterRequest Request(int slot, int remainingMilliseconds = 10_000)
    {
        byte[] prompt = Encoding.UTF8.GetBytes($"prompt-{slot}"); return new(new string('a', 64), "transport-capability-v1", new string('b', 64), new string('c', 64), new string('d', 64), SnowGlobePinnedOllamaRecordingModule.AdapterIdentity, SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256, slot, $"scenario-{slot}", new string('e', 64), prompt.Length, CognitionQualityHash.Sha256(prompt), prompt, remainingMilliseconds);
    }
    private static byte[] Wrapper() => Encoding.UTF8.GetBytes("{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":\"{\\\"agent_id\\\":\\\"agent-00\\\",\\\"action\\\":\\\"Idle\\\",\\\"quantity\\\":0}\",\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000}");
    private static async Task EventuallyAsync(Func<bool> condition) { for (int index = 0; index < 100 && !condition(); index++) await Task.Delay(10); Assert.True(condition()); }

    private sealed class PassVerifier : IOllamaLoopbackRuntimeVerifier
    { public OllamaLoopbackRuntimeVerification Verify(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection) => OllamaLoopbackRuntimeVerification.Pass; }

    private sealed class RejectAfterExchangeVerifier : IOllamaLoopbackRuntimeVerifier
    {
        public OllamaLoopbackRuntimeVerification Verify(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection) =>
            checkPoint == OllamaLoopbackRuntimeCheckPoint.AfterExchange
                ? OllamaLoopbackRuntimeVerification.Reject("raw-sentinel-C:/outside/nonce")
                : OllamaLoopbackRuntimeVerification.Pass;
    }

    private sealed class InspectingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response; private int _calls; internal InspectingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response; internal int CallCount => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken); return _response(request); }
    }

    private sealed class IgnoringSendHandler : HttpMessageHandler
    {
        private readonly bool _serialize; private int _calls; internal IgnoringSendHandler(bool serialize) => _serialize = serialize; internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal TaskCompletionSource<HttpResponseMessage> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal int CallCount => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); if (_serialize) _ = await request.Content!.ReadAsByteArrayAsync(CancellationToken.None); Started.TrySetResult(true); return await Release.Task.ConfigureAwait(false); }
    }

    private sealed class UncausedCancellationHandler : HttpMessageHandler
    {
        private readonly bool _serialize; private int _calls; internal UncausedCancellationHandler(bool serialize) => _serialize = serialize; internal int CallCount => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); if (_serialize) _ = await request.Content!.ReadAsByteArrayAsync(CancellationToken.None); throw new OperationCanceledException("attacker-controlled-internal-oce"); }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _body; internal UnknownLengthContent(byte[] body) { _body = body; Headers.ContentType = new("application/json"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_body).AsTask();
    }

    private sealed class TrackingByteArrayContent : ByteArrayContent
    { internal TrackingByteArrayContent(byte[] body) : base(body) { } internal bool IsDisposed { get; private set; } protected override void Dispose(bool disposing) { IsDisposed = true; base.Dispose(disposing); } }

    private sealed class HangingBodyContent : HttpContent
    {
        internal HangingBodyContent() { Headers.ContentType = new("application/json"); Headers.ContentLength = 1; }
        internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal bool IsDisposed { get; private set; }
        protected override bool TryComputeLength(out long length) { length = 1; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) { Started.TrySetResult(true); await Release.Task.ConfigureAwait(false); await stream.WriteAsync("{"u8.ToArray(), CancellationToken.None); }
        protected override void Dispose(bool disposing) { IsDisposed = true; base.Dispose(disposing); }
    }

    private sealed class UncausedCancellationBodyContent : HttpContent
    {
        internal UncausedCancellationBodyContent() { Headers.ContentType = new("application/json"); Headers.ContentLength = 1; }
        protected override bool TryComputeLength(out long length) { length = 1; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(new OperationCanceledException("attacker-controlled-body-oce"));
    }
}
