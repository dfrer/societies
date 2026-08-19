using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OfflineOllamaRecordingCodecTests
{
    [Fact]
    public void Encode_IsCanonicalBoundAndStableForAllTwelveSlots()
    {
        string[] digests = Enumerable.Range(1, 12).Select(index =>
        {
            using CognitionQualityRecordingAdapterRequest request = Request(index, $"prompt-{index}");
            using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
            Assert.InRange(encoded.BodyUtf8.Length, 1, OfflineOllamaRecordingCodecModule.MaximumRequestBytes);
            Assert.False(encoded.BodyUtf8.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
            using JsonDocument json = JsonDocument.Parse(encoded.BodyUtf8);
            JsonElement root = json.RootElement;
            Assert.Equal("qwen3.5:4b", root.GetProperty("model").GetString());
            Assert.Equal($"prompt-{index}", root.GetProperty("prompt").GetString());
            Assert.False(root.GetProperty("stream").GetBoolean()); Assert.False(root.GetProperty("think").GetBoolean()); Assert.False(root.GetProperty("raw").GetBoolean());
            Assert.Equal(4096, root.GetProperty("options").GetProperty("num_ctx").GetInt32());
            Assert.Equal(96, root.GetProperty("options").GetProperty("num_predict").GetInt32());
            Assert.Equal(0, root.GetProperty("options").GetProperty("seed").GetInt32());
            Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
            return Digest(encoded.BodyUtf8.Span);
        }).ToArray();
        Assert.Equal(new[] { "d8d63f4de1211d02d771990d521e1ddf29e29e112bbcb6bb2c51d62f266c0a04", "6d9a30dbc767039c6115c28506f8148baaf039fbe3e1bd984940d00480dc6d81", "db412f4a43cce1df21f7105cd23120c7e96cec4c207abbc5dfb95d8e63572a8b", "6856f9fad8decbe9fe848c7e6c729f1b76a4d7b598e905ab0c6cd28eb432b1fa", "542bda34a289704898d9e292684500d54314faadf9a103b5ece0f1011758107c", "ee6825989df016c3c0b92fd1721a28d9e23c85584841f09d60a2f1550f0483d3", "e2eda5646bda5e594d8eb3bf474b833e021141c4db1d036ce0aae96a38585b74", "2146bd8648bbf6eb9c93a272ad6ef16b68dfd0ab04e64ad9876eb2833ab8e5c0", "09c27f693be10ed38191757a4aba8dfecfab475e6760148d79f54d8394ab4749", "d0aaea570029c3622b06a4bf691664e0fb8d4b75378aa355184187c6238490ab", "bf9fb5be08164fc1ca30b0ee8504df677c54915caa98b3f1a7eddf35f883972d", "29e13534a7647a58dd96c8f41c070966896f69c528e1fce3accdb0ab1c73c70a" }, digests);
    }

    [Fact]
    public void Encode_UsesExactOrderedDuplicateFreeCanonicalRequestBytes()
    {
        using CognitionQualityRecordingAdapterRequest request = Request(1, "prompt");
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);

        Assert.Equal(Encoding.UTF8.GetBytes(ExpectedRequest("prompt")), encoded.BodyUtf8.ToArray());
        using JsonDocument document = JsonDocument.Parse(encoded.BodyUtf8);
        JsonElement root = document.RootElement;
        Assert.Equal(new[] { "model", "prompt", "stream", "think", "raw", "format", "options" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "type", "additionalProperties", "properties", "required" }, root.GetProperty("format").EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "agent_id", "action", "quantity" }, root.GetProperty("format").GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "num_ctx", "num_predict", "seed", "temperature" }, root.GetProperty("options").EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "agent_id", "action", "quantity" }, root.GetProperty("format").GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        AssertNoDuplicateObjectProperties(root);
    }

    [Fact]
    public void Encode_EscapesControlAndNonAsciiPromptIntoGoldenStrictJson()
    {
        const string prompt = "quote\" slash\\ line\n ctrl\u0001 latin\u00e9 emoji\U0001f600 html<>&";
        const string escapedPrompt = "quote\\\" slash\\\\ line\\n ctrl\\u0001 latin\\u00E9 emoji\\uD83D\\uDE00 html\\u003C\\u003E\\u0026";
        using CognitionQualityRecordingAdapterRequest request = Request(1, prompt);
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);

        Assert.Equal(Encoding.UTF8.GetBytes(ExpectedRequest(escapedPrompt)), encoded.BodyUtf8.ToArray());
        Assert.False(encoded.BodyUtf8.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        using JsonDocument document = JsonDocument.Parse(encoded.BodyUtf8);
        Assert.Equal(prompt, document.RootElement.GetProperty("prompt").GetString());
        AssertNoDuplicateObjectProperties(document.RootElement);
    }

    [Fact]
    public void Decode_ExtractsOwnedRawProposalWithoutProposalValidation()
    {
        using CognitionQualityRecordingAdapterRequest request = Request(1, "prompt");
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
        OfflineOllamaRecordingTransportResponse response = new(encoded, Wrapper("not-json"));
        using CognitionQualityRecordingResponseBuffer decoded = OfflineOllamaRecordingCodecModule.Decode(response, request);
        Assert.Equal("not-json", Encoding.UTF8.GetString(decoded.Snapshot()));
    }

    [Theory]
    [InlineData("{\"model\":\"qwen3.5:4b\"}")]
    [InlineData("[]")]
    [InlineData("{\"model\":\"qwen3.5:4b\",\"model\":\"qwen3.5:4b\"}")]
    [InlineData("{\"model\":\"qwen3.5:4b\",\"Model\":\"qwen3.5:4b\"}")]
    [InlineData("{\"model\":\"wrong\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":\"x\",\"done\":true,\"done_reason\":\"stop\",\"total_duration\":0,\"load_duration\":0,\"prompt_eval_count\":0,\"prompt_eval_duration\":0,\"eval_count\":0,\"eval_duration\":0}")]
    public void Decode_RejectsMalformedOrWrongWrapperShape(string wrapper) => AssertRejects(wrapper);

    [Fact]
    public void Decode_RejectsEnvelopeUtf8BoundsAndCounters()
    {
        AssertRejects(Wrapper(""));
        AssertRejects(Wrapper(new string('a', 1025)));
        AssertRejects(Wrapper("x", additional: ",\"unknown\":1"));
        using (CognitionQualityRecordingAdapterRequest contextRequest = Request(1, "prompt")) using (OfflineOllamaRecordingTransportRequest contextEncoded = OfflineOllamaRecordingCodecModule.Encode(contextRequest)) using (CognitionQualityRecordingResponseBuffer decoded = OfflineOllamaRecordingCodecModule.Decode(new OfflineOllamaRecordingTransportResponse(contextEncoded, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Wrapper("x")).Replace("[1,2]", "[4097]", StringComparison.Ordinal))), contextRequest)) Assert.Equal(1, decoded.Length);
        AssertRejects(Wrapper("x", additional: ",\"total_duration\":999999999999"));
        AssertRejects(Encoding.UTF8.GetString(Wrapper("x")).Replace("\"prompt_eval_count\":10", "\"prompt_eval_count\":0", StringComparison.Ordinal));
        AssertRejects(Encoding.UTF8.GetString(Wrapper("x")).Replace("\"total_duration\":1000000", "\"total_duration\":999999", StringComparison.Ordinal));
        AssertRejects(new byte[] { 0xff });

        using CognitionQualityRecordingAdapterRequest request = Request(1, "prompt");
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
        OfflineOllamaRecordingTransportResponse response = new(encoded, Wrapper("x")) { StatusCode = 201 };
        Assert.Throws<InvalidOperationException>(() => OfflineOllamaRecordingCodecModule.Decode(response, request));
    }

    [Fact]
    public void Decode_AcceptsOneAnd1024ByteResponses()
    {
        foreach (int length in new[] { 1, 1024 })
        {
            using CognitionQualityRecordingAdapterRequest request = Request(1, "prompt");
            using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
            using CognitionQualityRecordingResponseBuffer decoded = OfflineOllamaRecordingCodecModule.Decode(new OfflineOllamaRecordingTransportResponse(encoded, Wrapper(new string('x', length))), request);
            Assert.Equal(length, decoded.Length);
        }
    }

    [Fact]
    public void Encode_RejectsWrongPinnedIdentityOrContract()
    {
        using CognitionQualityRecordingAdapterRequest wrongIdentity = Request(1, "prompt", adapterIdentity: "other-adapter");
        using CognitionQualityRecordingAdapterRequest wrongContract = Request(1, "prompt", adapterContractDigest: new string('e', 64));
        Assert.Throws<InvalidOperationException>(() => OfflineOllamaRecordingCodecModule.Encode(wrongIdentity));
        Assert.Throws<InvalidOperationException>(() => OfflineOllamaRecordingCodecModule.Encode(wrongContract));
    }

    [Fact]
    public void Encode_EscapesHostilePromptBytesExactly()
    {
        const string prompt = "quote\" slash\\ control\u0001 nonascii-é apostrophe' plus+ tick`";
        using CognitionQualityRecordingAdapterRequest request = Request(1, prompt);
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
        string json = Encoding.UTF8.GetString(encoded.BodyUtf8.Span);
        Assert.Contains("\"prompt\":\"quote\\\" slash\\\\ control\\u0001 nonascii-\\u00E9 apostrophe\\u0027 plus\\u002B tick\\u0060\"", json, StringComparison.Ordinal);
        Assert.Equal("6e657143bfb5b528fa15fba5ec5a0cbbb5510db52c5fe1d8ec1eaff00e0527b4", Digest(encoded.BodyUtf8.Span));
    }

    [Fact]
    public async Task Transport_RequiresOneSequentialExchangePerCapabilityAndNeverAcceptsThirteenthSlot()
    {
        byte[][] wrappers = Enumerable.Range(1, 12).Select(index => Wrapper($"response-{index}")).ToArray();
        byte[] thirteenthBody;
        using (InMemoryOfflineOllamaRecordingTransportAdapter transport = new(wrappers))
        {
            const string capability = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            thirteenthBody = EncodedBody(1, "prompt", capability);

            await Assert.ThrowsAsync<InvalidOperationException>(() => ExchangeAsync(transport, 2, "prompt", capability));
            Assert.Equal(0, transport.CallCount);

            for (int slot = 1; slot <= 12; slot++)
            {
                using OfflineOllamaRecordingTransportResponse response = await ExchangeAsync(transport, slot, "prompt", capability);
                Assert.Equal(slot, response.SlotOrdinal);
                Assert.Equal($"scenario-{slot}", response.ScenarioId);
            }

            Assert.Equal(12, transport.CallCount);
            using CognitionQualityRecordingAdapterRequest thirteenthSource = Request(13, "prompt", capabilityDigest: capability);
            using OfflineOllamaRecordingTransportRequest thirteenth = new(thirteenthSource, thirteenthBody);
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ExchangeOnceAsync(thirteenth, CancellationToken.None).AsTask());
            Assert.Equal(12, transport.CallCount);
            Assert.All(thirteenthBody, value => Assert.Equal(0, value));
        }

        Assert.All(wrappers, wrapper => Assert.All(wrapper, value => Assert.Equal(0, value)));
    }

    [Fact]
    public async Task Transport_EnforcesCapacityAndCancellationDisposalOwnershipWithoutLateSuccess()
    {
        byte[][] wrappers = Enumerable.Range(1, 12).Select(index => Wrapper($"response-{index}")).ToArray();
        using (InMemoryOfflineOllamaRecordingTransportAdapter transport = new(wrappers))
        {
            for (int index = 0; index < OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities; index++)
            {
                string capability = index.ToString("x64");
                using OfflineOllamaRecordingTransportResponse response = await ExchangeAsync(transport, 1, "prompt", capability);
                Assert.Equal(1, response.SlotOrdinal);
            }

            Assert.Equal(OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities, transport.CallCount);
            string rejectedCapability = OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities.ToString("x64");
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExchangeAsync(transport, 1, "prompt", rejectedCapability));
            Assert.Equal(OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities, transport.CallCount);

            using (CognitionQualityRecordingAdapterRequest source = Request(1, "prompt"))
            using (OfflineOllamaRecordingTransportRequest preCancelled = OfflineOllamaRecordingCodecModule.Encode(source))
            using (CancellationTokenSource cancellation = new())
            {
                byte[] candidate = OwnedRequestBody(preCancelled);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ExchangeOnceAsync(preCancelled, cancellation.Token).AsTask());
                Assert.Equal(OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities, transport.CallCount);
                Assert.Contains(candidate, value => value != 0);
                preCancelled.Dispose();
                Assert.All(candidate, value => Assert.Equal(0, value));
            }
        }
        Assert.All(wrappers, wrapper => Assert.All(wrapper, value => Assert.Equal(0, value)));

        byte[][] heldWrappers = Enumerable.Range(1, 12).Select(index => Wrapper($"response-{index}")).ToArray();
        using (InMemoryOfflineOllamaRecordingTransportAdapter transport = new(heldWrappers))
        {
            Task held = transport.HoldNextExchangeForTesting();
            using CognitionQualityRecordingAdapterRequest source = Request(1, "prompt");
            using OfflineOllamaRecordingTransportRequest pendingRequest = OfflineOllamaRecordingCodecModule.Encode(source);
            byte[] candidate = OwnedRequestBody(pendingRequest);
            using CancellationTokenSource cancellation = new();
            Task<OfflineOllamaRecordingTransportResponse> pending = transport.ExchangeOnceAsync(pendingRequest, cancellation.Token).AsTask();
            await held.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(1, transport.CallCount);
            Assert.All(candidate, value => Assert.Equal(0, value));

            Task heldForDispose = transport.HoldNextExchangeForTesting();
            using CognitionQualityRecordingAdapterRequest disposeSource = Request(1, "prompt", capabilityDigest: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            using OfflineOllamaRecordingTransportRequest disposeRequest = OfflineOllamaRecordingCodecModule.Encode(disposeSource);
            byte[] disposeCandidate = OwnedRequestBody(disposeRequest);
            Task<OfflineOllamaRecordingTransportResponse> disposing = transport.ExchangeOnceAsync(disposeRequest, CancellationToken.None).AsTask();
            await heldForDispose.WaitAsync(TimeSpan.FromSeconds(2));
            transport.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => disposing);
            Assert.Equal(2, transport.CallCount);
            Assert.All(disposeCandidate, value => Assert.Equal(0, value));
        }
        Assert.All(heldWrappers, wrapper => Assert.All(wrapper, value => Assert.Equal(0, value)));
    }

    private static void AssertRejects(string wrapper) => AssertRejects(Encoding.UTF8.GetBytes(wrapper));
    private static void AssertRejects(byte[] wrapper)
    {
        using CognitionQualityRecordingAdapterRequest request = Request(1, "prompt");
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(request);
        Assert.Throws<InvalidOperationException>(() => OfflineOllamaRecordingCodecModule.Decode(new OfflineOllamaRecordingTransportResponse(encoded, wrapper), request));
    }

    private static byte[] Wrapper(string response, string? additional = null) => Encoding.UTF8.GetBytes($"{{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":{JsonSerializer.Serialize(response)},\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000{additional}}}");
    private static async Task<OfflineOllamaRecordingTransportResponse> ExchangeAsync(InMemoryOfflineOllamaRecordingTransportAdapter transport, int slot, string prompt, string capabilityDigest)
    {
        using CognitionQualityRecordingAdapterRequest source = Request(slot, prompt, capabilityDigest: capabilityDigest);
        OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source);
        return await transport.ExchangeOnceAsync(encoded, CancellationToken.None);
    }

    private static byte[] EncodedBody(int slot, string prompt, string capabilityDigest)
    {
        using CognitionQualityRecordingAdapterRequest source = Request(slot, prompt, capabilityDigest: capabilityDigest);
        using OfflineOllamaRecordingTransportRequest encoded = OfflineOllamaRecordingCodecModule.Encode(source);
        return encoded.BodyUtf8.ToArray();
    }

    private static byte[] OwnedRequestBody(OfflineOllamaRecordingTransportRequest request) => (byte[])typeof(OfflineOllamaRecordingTransportRequest).GetField("_bodyUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!;

    private static void AssertNoDuplicateObjectProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            JsonProperty[] properties = element.EnumerateObject().ToArray();
            Assert.Equal(properties.Length, properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count());
            foreach (JsonProperty property in properties) AssertNoDuplicateObjectProperties(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) AssertNoDuplicateObjectProperties(item);
    }

    private static string ExpectedRequest(string encodedPrompt) => "{\"model\":\"qwen3.5:4b\",\"prompt\":\"" + encodedPrompt + "\",\"stream\":false,\"think\":false,\"raw\":false,\"format\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"agent_id\":{\"type\":\"string\",\"enum\":[\"agent-00\"]},\"action\":{\"type\":\"string\",\"enum\":[\"Idle\",\"GatherWood\",\"GatherStone\",\"BuildShelter\",\"BuildStorage\",\"MaintainShelter\"]},\"quantity\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":64}},\"required\":[\"agent_id\",\"action\",\"quantity\"]},\"options\":{\"num_ctx\":4096,\"num_predict\":96,\"seed\":0,\"temperature\":0}}";

    private static CognitionQualityRecordingAdapterRequest Request(int slot, string prompt, string? adapterIdentity = null, string? adapterContractDigest = null, string? capabilityDigest = null)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(prompt);
        return new CognitionQualityRecordingAdapterRequest(capabilityDigest ?? new string('a', 64), "nonce-v1", new string('b', 64), new string('c', 64), new string('d', 64), adapterIdentity ?? OfflinePinnedOllamaRecordingFixture.RegistryAdapterIdentity, adapterContractDigest ?? CognitionQualityRecordingSessionCanonical.Digest(OfflinePinnedOllamaRecordingFixture.ContractDescriptor), slot, $"scenario-{slot}", new string('f', 64), bytes.Length, Digest(bytes), bytes, 10_000);
    }
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
