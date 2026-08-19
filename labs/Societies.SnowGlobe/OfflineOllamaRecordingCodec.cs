using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Pure, internal canonical encoder and strict decoder for the historical offline Ollama cell.</summary>
internal static class OfflineOllamaRecordingCodecModule
{
    internal const string CodecIdentity = "snow-globe-offline-ollama-recording-codec/v1";
    internal const string TransportPortIdentity = "snow-globe-offline-ollama-recording-transport-port/v1";
    internal const string CanonicalEndpointIdentity = "http://127.0.0.1:11435/";
    internal const string GeneratePath = "/api/generate";
    internal const string Model = "qwen3.5:4b";
    internal const int MaximumRequestBytes = 16 * 1024;
    internal const int MaximumWrapperBytes = 8 * 1024;
    internal const int MaximumAggregateWrapperBytes = CognitionQualityCorpusV1.ScenarioCount * MaximumWrapperBytes;
    internal const int MaximumExtractedResponseBytes = CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] RequestPrefix = "{\"model\":\"qwen3.5:4b\",\"prompt\":"u8.ToArray();
    private static readonly byte[] RequestSuffix = Encoding.UTF8.GetBytes(",\"stream\":false,\"think\":false,\"raw\":false,\"format\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"agent_id\":{\"type\":\"string\",\"enum\":[\"agent-00\"]},\"action\":{\"type\":\"string\",\"enum\":[" + string.Join(',', Enum.GetNames<SnowGlobeActionKind>().Select(static value => "\"" + value + "\"")) + "]},\"quantity\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":64}},\"required\":[\"agent_id\",\"action\",\"quantity\"]},\"options\":{\"num_ctx\":4096,\"num_predict\":96,\"seed\":0,\"temperature\":0}}" );

    internal static OfflineOllamaRecordingTransportRequest Encode(CognitionQualityRecordingAdapterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReadOnlyMemory<byte> prompt = request.PromptUtf8;
        byte[] scratch = new byte[MaximumRequestBytes];
        try
        {
            ValidateRequest(request, prompt.Span);
            int written = 0; Append(scratch, ref written, RequestPrefix); AppendEscapedUtf8(scratch, ref written, prompt.Span); Append(scratch, ref written, RequestSuffix);
            byte[] body = scratch.AsSpan(0, written).ToArray();
            if (body.Length is < 1 or > MaximumRequestBytes) { CryptographicOperations.ZeroMemory(body); throw new InvalidOperationException("offline_ollama_request_size_invalid"); }
            return new OfflineOllamaRecordingTransportRequest(request, body);
        }
        finally { CryptographicOperations.ZeroMemory(scratch); if (MemoryMarshal.TryGetArray(prompt, out ArraySegment<byte> segment) && segment.Array is not null) CryptographicOperations.ZeroMemory(segment.AsSpan()); }
    }

    internal static CognitionQualityRecordingResponseBuffer Decode(OfflineOllamaRecordingTransportResponse transportResponse, CognitionQualityRecordingAdapterRequest request)
    {
        ArgumentNullException.ThrowIfNull(transportResponse); ArgumentNullException.ThrowIfNull(request);
        try
        {
            transportResponse.ValidateBinding(request);
            if (transportResponse.StatusCode != 200 || transportResponse.IsRedirect || !string.Equals(transportResponse.MediaType, "application/json", StringComparison.Ordinal) || !string.IsNullOrEmpty(transportResponse.ContentEncoding) || transportResponse.DeclaredBodyLength != transportResponse.BodyUtf8.Length || transportResponse.BodyUtf8.Length is < 1 or > MaximumWrapperBytes) throw new InvalidOperationException("offline_ollama_wrapper_envelope_invalid");
            byte[] wrapper = transportResponse.TakeBody(); byte[]? extracted = null;
            try { ValidateStrictUtf8(wrapper); extracted = ExtractResponse(wrapper, request.RemainingSessionMilliseconds); CognitionQualityRecordingResponseBuffer result = new(extracted); extracted = null; return result; }
            finally { CryptographicOperations.ZeroMemory(wrapper); if (extracted is not null) CryptographicOperations.ZeroMemory(extracted); }
        }
        finally { transportResponse.Dispose(); }
    }

    internal static void ValidateFixtureWrapper(byte[] wrapper)
    {
        if (wrapper.Length is < 1 or > MaximumWrapperBytes) throw new ArgumentOutOfRangeException(nameof(wrapper));
        try { byte[] extracted = ExtractResponse(wrapper, 10 * 60 * 1000); CryptographicOperations.ZeroMemory(extracted); }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or DecoderFallbackException) { throw new ArgumentException("A full Ollama generate wrapper is required.", nameof(wrapper), exception); }
    }

    private static void ValidateRequest(CognitionQualityRecordingAdapterRequest request, ReadOnlySpan<byte> prompt)
    {
        if (request.AttemptNumber != 1 || request.SlotOrdinal is < 1 or > CognitionQualityCorpusV1.ScenarioCount || !string.Equals(request.AdapterIdentity, OfflinePinnedOllamaRecordingFixture.RegistryAdapterIdentity, StringComparison.Ordinal) || !string.Equals(request.AdapterContractDigestSha256, CognitionQualityRecordingSessionCanonical.Digest(OfflinePinnedOllamaRecordingFixture.ContractDescriptor), StringComparison.Ordinal) || request.PromptByteCount != prompt.Length || prompt.Length is < 1 or > 2048 || !CognitionQualityRecordingSessionCanonical.IsDigest(request.CapabilityDigestSha256) || !CognitionQualityRecordingSessionCanonical.IsDigest(request.ProvenanceDigestSha256) || !CognitionQualityRecordingSessionCanonical.IsDigest(request.PromptDigestSha256) || !string.Equals(Digest(prompt), request.PromptDigestSha256, StringComparison.Ordinal) || request.RemainingSessionMilliseconds <= 0) throw new InvalidOperationException("offline_ollama_request_invalid");
        ValidateStrictUtf8(prompt);
    }

    private static byte[] ExtractResponse(byte[] wrapper, int remainingSessionMilliseconds)
    {
        Utf8JsonReader reader = new(wrapper, new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 4 });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new InvalidOperationException("offline_ollama_wrapper_json_invalid");
        HashSet<string> seen = new(StringComparer.Ordinal); bool model = false, created = false, response = false, done = false, reason = false, totalDuration = false, loadDuration = false, promptEvalCount = false, promptEvalDuration = false, evalCountSeen = false, evalDuration = false; long total = -1, load = -1, promptDuration = -1, evalDurationValue = -1; int promptCount = -1, evalCount = -1; byte[]? extracted = null;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType != JsonTokenType.PropertyName) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid");
                string name = reader.GetString()!; if (!seen.Add(name) || !reader.Read()) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid");
                switch (name)
                {
                    case "model": RequireString(ref reader, Model); model = true; break;
                    case "created_at": ValidateTimestamp(ref reader); created = true; break;
                    case "response": if (reader.TokenType != JsonTokenType.String || extracted is not null) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid"); extracted = CopyStringUtf8(ref reader); if (extracted.Length is < 1 or > MaximumExtractedResponseBytes) throw new InvalidOperationException("offline_ollama_response_size_invalid"); response = true; break;
                    case "done": if (reader.TokenType != JsonTokenType.True) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid"); done = true; break;
                    case "done_reason": RequireString(ref reader, "stop"); reason = true; break;
                    case "context": ValidateContext(ref reader); break;
                    case "total_duration": total = RequireNonNegativeInt64(ref reader); totalDuration = true; break;
                    case "load_duration": load = RequireNonNegativeInt64(ref reader); loadDuration = true; break;
                    case "prompt_eval_count": promptCount = RequireNonNegativeInt32(ref reader); promptEvalCount = true; break;
                    case "prompt_eval_duration": promptDuration = RequireNonNegativeInt64(ref reader); promptEvalDuration = true; break;
                    case "eval_count": evalCount = RequireNonNegativeInt32(ref reader); evalCountSeen = true; break;
                    case "eval_duration": evalDurationValue = RequireNonNegativeInt64(ref reader); evalDuration = true; break;
                    default: throw new InvalidOperationException("offline_ollama_wrapper_unknown_property");
                }
            }
            if (reader.TokenType != JsonTokenType.EndObject || reader.Read() || !(model && created && response && done && reason && totalDuration && loadDuration && promptEvalCount && promptEvalDuration && evalCountSeen && evalDuration) || total <= 0 || promptCount is < 1 or > 4096 || evalCount is < 1 or > 96 || promptDuration <= 0 || evalDurationValue <= 0) throw new InvalidOperationException("offline_ollama_wrapper_required_property_invalid");
            long maximumDuration = checked((long)remainingSessionMilliseconds * 1_000_000L); if (total > maximumDuration || checked(load + promptDuration + evalDurationValue) > total) throw new InvalidOperationException("offline_ollama_wrapper_counter_invalid"); return extracted!;
        }
        catch { if (extracted is not null) CryptographicOperations.ZeroMemory(extracted); throw; }
    }

    private static void ValidateContext(ref Utf8JsonReader reader) { if (reader.TokenType != JsonTokenType.StartArray) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid"); int count = 0; while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int value) || value < 0 || ++count > 4096) throw new InvalidOperationException("offline_ollama_wrapper_context_invalid"); if (reader.TokenType != JsonTokenType.EndArray) throw new InvalidOperationException("offline_ollama_wrapper_context_invalid"); }
    private static void RequireString(ref Utf8JsonReader reader, string exact) { if (reader.TokenType != JsonTokenType.String || !reader.ValueTextEquals(exact)) throw new InvalidOperationException("offline_ollama_wrapper_shape_invalid"); }
    private static void ValidateTimestamp(ref Utf8JsonReader reader) { if (reader.TokenType != JsonTokenType.String || reader.ValueSpan.Length is < 20 or > 64) throw new InvalidOperationException("offline_ollama_wrapper_timestamp_invalid"); string timestamp = reader.GetString()!; if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) || (!timestamp.EndsWith('Z') && timestamp.LastIndexOf('+') < 10 && timestamp.LastIndexOf('-') < 10)) throw new InvalidOperationException("offline_ollama_wrapper_timestamp_invalid"); }
    private static long RequireNonNegativeInt64(ref Utf8JsonReader reader) => reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out long value) && value >= 0 ? value : throw new InvalidOperationException("offline_ollama_wrapper_counter_invalid");
    private static int RequireNonNegativeInt32(ref Utf8JsonReader reader) => reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value) && value >= 0 ? value : throw new InvalidOperationException("offline_ollama_wrapper_counter_invalid");
    private static byte[] CopyStringUtf8(ref Utf8JsonReader reader) { int maximum = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length; byte[] scratch = new byte[Math.Max(1, maximum)]; try { int written = reader.CopyString(scratch); return scratch.AsSpan(0, written).ToArray(); } finally { CryptographicOperations.ZeroMemory(scratch); } }
    private static void ValidateStrictUtf8(ReadOnlySpan<byte> bytes) { if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) throw new InvalidOperationException("offline_ollama_utf8_bom_invalid"); try { _ = StrictUtf8.GetCharCount(bytes); } catch (DecoderFallbackException exception) { throw new InvalidOperationException("offline_ollama_utf8_invalid", exception); } }
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Append(byte[] target, ref int offset, ReadOnlySpan<byte> value) { if (value.Length > target.Length - offset) throw new InvalidOperationException("offline_ollama_request_size_invalid"); value.CopyTo(target.AsSpan(offset)); offset += value.Length; }
    private static void AppendEscapedUtf8(byte[] target, ref int offset, ReadOnlySpan<byte> value) { byte[] bytes = value.ToArray(); char[] chars = StrictUtf8.GetChars(bytes); try { AppendAscii(target, ref offset, '"'); foreach (char character in chars) { if (character == '"') Append(target, ref offset, "\\\""u8); else if (character == '\\') Append(target, ref offset, "\\\\"u8); else if (character == '\n') Append(target, ref offset, "\\n"u8); else if (character == '\r') Append(target, ref offset, "\\r"u8); else if (character == '\t') Append(target, ref offset, "\\t"u8); else if (character == '\b') Append(target, ref offset, "\\b"u8); else if (character == '\f') Append(target, ref offset, "\\f"u8); else if (character < 0x20 || character > 0x7e || character is '<' or '>' or '&' or '\'' or '+' or '`') { Append(target, ref offset, "\\u"u8); AppendAscii(target, ref offset, Hex(character >> 12)); AppendAscii(target, ref offset, Hex((character >> 8) & 15)); AppendAscii(target, ref offset, Hex((character >> 4) & 15)); AppendAscii(target, ref offset, Hex(character & 15)); } else AppendAscii(target, ref offset, character); } AppendAscii(target, ref offset, '"'); } finally { CryptographicOperations.ZeroMemory(bytes); Array.Clear(chars); } }
    private static void AppendAscii(byte[] target, ref int offset, char value) { if (offset >= target.Length) throw new InvalidOperationException("offline_ollama_request_size_invalid"); target[offset++] = checked((byte)value); }
    private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
}

internal interface IOfflineOllamaRecordingTransportPort { ValueTask<OfflineOllamaRecordingTransportResponse> ExchangeOnceAsync(OfflineOllamaRecordingTransportRequest request, CancellationToken cancellationToken); }

internal sealed class OfflineOllamaRecordingTransportRequest : IDisposable
{
    private byte[] _bodyUtf8;
    internal OfflineOllamaRecordingTransportRequest(CognitionQualityRecordingAdapterRequest request, byte[] bodyUtf8) { CapabilityDigestSha256 = request.CapabilityDigestSha256; RequestDigestSha256 = request.RequestDigestSha256; AdapterIdentity = request.AdapterIdentity; AdapterContractDigestSha256 = request.AdapterContractDigestSha256; ProvenanceDigestSha256 = request.ProvenanceDigestSha256; SlotOrdinal = request.SlotOrdinal; ScenarioId = request.ScenarioId; ObservationDigestSha256 = request.ObservationDigestSha256; PromptByteCount = request.PromptByteCount; PromptDigestSha256 = request.PromptDigestSha256; RemainingSessionMilliseconds = request.RemainingSessionMilliseconds; _bodyUtf8 = bodyUtf8; }
    internal string Method => "POST"; internal string EndpointIdentity => OfflineOllamaRecordingCodecModule.CanonicalEndpointIdentity; internal string Path => OfflineOllamaRecordingCodecModule.GeneratePath;
    internal string CapabilityDigestSha256 { get; } internal string RequestDigestSha256 { get; } internal string AdapterIdentity { get; } internal string AdapterContractDigestSha256 { get; } internal string ProvenanceDigestSha256 { get; } internal int SlotOrdinal { get; } internal string ScenarioId { get; } internal string ObservationDigestSha256 { get; } internal int PromptByteCount { get; } internal string PromptDigestSha256 { get; } internal int RemainingSessionMilliseconds { get; } internal ReadOnlyMemory<byte> BodyUtf8 => _bodyUtf8;
    public void Dispose() { if (_bodyUtf8.Length != 0) CryptographicOperations.ZeroMemory(_bodyUtf8); _bodyUtf8 = Array.Empty<byte>(); }
}

internal sealed class OfflineOllamaRecordingTransportResponse : IDisposable
{
    private byte[] _bodyUtf8;
    internal OfflineOllamaRecordingTransportResponse(OfflineOllamaRecordingTransportRequest request, byte[] bodyUtf8) { CapabilityDigestSha256 = request.CapabilityDigestSha256; RequestDigestSha256 = request.RequestDigestSha256; AdapterIdentity = request.AdapterIdentity; AdapterContractDigestSha256 = request.AdapterContractDigestSha256; ProvenanceDigestSha256 = request.ProvenanceDigestSha256; SlotOrdinal = request.SlotOrdinal; ScenarioId = request.ScenarioId; ObservationDigestSha256 = request.ObservationDigestSha256; PromptByteCount = request.PromptByteCount; PromptDigestSha256 = request.PromptDigestSha256; StatusCode = 200; MediaType = "application/json"; DeclaredBodyLength = bodyUtf8.Length; _bodyUtf8 = bodyUtf8; }
    internal string CapabilityDigestSha256 { get; } internal string RequestDigestSha256 { get; } internal string AdapterIdentity { get; } internal string AdapterContractDigestSha256 { get; } internal string ProvenanceDigestSha256 { get; } internal int SlotOrdinal { get; } internal string ScenarioId { get; } internal string ObservationDigestSha256 { get; } internal int PromptByteCount { get; } internal string PromptDigestSha256 { get; } internal int StatusCode { get; set; } internal string MediaType { get; set; } internal string? ContentEncoding { get; set; } internal bool IsRedirect { get; set; } internal int DeclaredBodyLength { get; set; } internal ReadOnlyMemory<byte> BodyUtf8 => _bodyUtf8;
    internal byte[] TakeBody() { byte[] body = _bodyUtf8; _bodyUtf8 = Array.Empty<byte>(); return body; }
    internal void ValidateBinding(CognitionQualityRecordingAdapterRequest request) { if (!string.Equals(CapabilityDigestSha256, request.CapabilityDigestSha256, StringComparison.Ordinal) || !string.Equals(RequestDigestSha256, request.RequestDigestSha256, StringComparison.Ordinal) || !string.Equals(AdapterIdentity, request.AdapterIdentity, StringComparison.Ordinal) || !string.Equals(AdapterContractDigestSha256, request.AdapterContractDigestSha256, StringComparison.Ordinal) || !string.Equals(ProvenanceDigestSha256, request.ProvenanceDigestSha256, StringComparison.Ordinal) || SlotOrdinal != request.SlotOrdinal || !string.Equals(ScenarioId, request.ScenarioId, StringComparison.Ordinal) || !string.Equals(ObservationDigestSha256, request.ObservationDigestSha256, StringComparison.Ordinal) || PromptByteCount != request.PromptByteCount || !string.Equals(PromptDigestSha256, request.PromptDigestSha256, StringComparison.Ordinal)) throw new InvalidOperationException("offline_ollama_binding_invalid"); }
    public void Dispose() { if (_bodyUtf8.Length != 0) CryptographicOperations.ZeroMemory(_bodyUtf8); _bodyUtf8 = Array.Empty<byte>(); }
}

/// <summary>Only internal port implementation: a deterministic, owned-copy fixture with no I/O capability.</summary>
internal sealed class InMemoryOfflineOllamaRecordingTransportAdapter : IOfflineOllamaRecordingTransportPort, IDisposable
{
    private readonly byte[][] _wrappers; private readonly object _gate = new(); private readonly Dictionary<string, int> _nextByCapability = new(StringComparer.Ordinal); private readonly CancellationTokenSource _disposeCancellation = new(); private TaskCompletionSource<bool>? _held; private int _holdNext; private bool _disposed;
    internal InMemoryOfflineOllamaRecordingTransportAdapter(byte[][] ownedWrappers) { ArgumentNullException.ThrowIfNull(ownedWrappers); if (ownedWrappers.Length != CognitionQualityCorpusV1.ScenarioCount) throw new ArgumentException("Exactly twelve wrapper fixtures are required.", nameof(ownedWrappers)); _wrappers = ownedWrappers; try { int aggregate = 0; foreach (byte[] wrapper in _wrappers) { if (wrapper is null || wrapper.Length is < 1 or > OfflineOllamaRecordingCodecModule.MaximumWrapperBytes) throw new ArgumentOutOfRangeException(nameof(ownedWrappers)); aggregate = checked(aggregate + wrapper.Length); if (aggregate > OfflineOllamaRecordingCodecModule.MaximumAggregateWrapperBytes) throw new ArgumentOutOfRangeException(nameof(ownedWrappers)); } } catch { ZeroWrappers(); throw; } }
    internal int CallCount { get; private set; } internal Task HoldNextExchangeForTesting() { lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); _held = new(TaskCreationOptions.RunContinuationsAsynchronously); _holdNext = 1; return _held.Task; } }
    public async ValueTask<OfflineOllamaRecordingTransportResponse> ExchangeOnceAsync(OfflineOllamaRecordingTransportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested(); TaskCompletionSource<bool>? held = null; byte[]? wrapper = null;
        try { lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); if (!string.Equals(request.Method, "POST", StringComparison.Ordinal) || !string.Equals(request.EndpointIdentity, OfflineOllamaRecordingCodecModule.CanonicalEndpointIdentity, StringComparison.Ordinal) || !string.Equals(request.Path, OfflineOllamaRecordingCodecModule.GeneratePath, StringComparison.Ordinal) || request.BodyUtf8.Length is < 1 or > OfflineOllamaRecordingCodecModule.MaximumRequestBytes || request.SlotOrdinal is < 1 or > CognitionQualityCorpusV1.ScenarioCount) throw new InvalidOperationException("offline_ollama_transport_request_invalid"); int expected = _nextByCapability.TryGetValue(request.CapabilityDigestSha256, out int value) ? value : 1; if (request.SlotOrdinal != expected) throw new InvalidOperationException("offline_ollama_transport_slot_invalid"); if (!_nextByCapability.ContainsKey(request.CapabilityDigestSha256) && _nextByCapability.Count >= OfflinePinnedOllamaRecordingFixture.MaximumTrackedCapabilities) throw new InvalidOperationException("offline_ollama_transport_capacity_exceeded"); _nextByCapability[request.CapabilityDigestSha256] = expected + 1; CallCount++; if (Interlocked.Exchange(ref _holdNext, 0) != 0) held = _held; }
            if (held is not null) { held.TrySetResult(true); try { using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token); await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token).ConfigureAwait(false); } catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { throw new ObjectDisposedException(nameof(InMemoryOfflineOllamaRecordingTransportAdapter)); } }
            cancellationToken.ThrowIfCancellationRequested(); lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); wrapper = _wrappers[request.SlotOrdinal - 1].ToArray(); } OfflineOllamaRecordingTransportResponse response = new(request, wrapper); wrapper = null; return response;
        }
        finally { request.Dispose(); if (wrapper is not null) CryptographicOperations.ZeroMemory(wrapper); }
    }
    public void Dispose() { TaskCompletionSource<bool>? held; lock (_gate) { if (_disposed) return; _disposed = true; ZeroWrappers(); _nextByCapability.Clear(); held = _held; _held = null; } _disposeCancellation.Cancel(); held?.TrySetCanceled(); }
    private void ZeroWrappers() { foreach (byte[]? wrapper in _wrappers) if (wrapper is not null) CryptographicOperations.ZeroMemory(wrapper); }
}
