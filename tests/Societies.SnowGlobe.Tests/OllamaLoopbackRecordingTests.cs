using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Societies.SnowGlobe.Tests;

[CollectionDefinition("OllamaLoopbackRecordingSerial", DisableParallelization = true)]
public sealed class OllamaLoopbackRecordingSerialCollection { }

[Collection("OllamaLoopbackRecordingSerial")]
public sealed class OllamaLoopbackRecordingTests
{
    [Fact]
    public void PublicInterface_IsRegistryClosedAndAuthorizePerformsZeroRuntimeOrTransportIo()
    {
        Assert.Single(typeof(SnowGlobePinnedOllamaRecordingModule).GetConstructors());
        ConstructorInfo constructor = Assert.Single(typeof(SnowGlobePinnedOllamaRecordingModule).GetConstructors());
        Assert.Empty(constructor.GetParameters());
        MethodInfo authorize = typeof(SnowGlobePinnedOllamaRecordingModule).GetMethod(nameof(SnowGlobePinnedOllamaRecordingModule.Authorize))!;
        Assert.Equal([typeof(CognitionQualityPromptEnvelopePublication), typeof(OllamaLoopbackRuntimeBinding), typeof(OllamaLoopbackRecordingAuthorization)], authorize.GetParameters().Select(static parameter => parameter.ParameterType));
        Type[] liveTypes = [typeof(SnowGlobePinnedOllamaRecordingModule), typeof(AuthorizedOllamaLoopbackRecordingSession), typeof(OllamaLoopbackRuntimeBinding), typeof(OllamaLoopbackRecordingAuthorization), typeof(SnowGlobeOllamaLoopbackRecordingResult), typeof(SnowGlobeOllamaLoopbackRecordingReceipt)];
        Assert.DoesNotContain(liveTypes.SelectMany(static type => type.GetConstructors().SelectMany(static value => value.GetParameters())), static parameter => parameter.ParameterType == typeof(HttpClient) || typeof(Delegate).IsAssignableFrom(parameter.ParameterType));

        FixedClock clock = new(10); CountingFactory factory = new(static () => throw new InvalidOperationException("must_not_create"));
        SnowGlobePinnedOllamaRecordingModule module = new(clock, factory);
        AuthorizedOllamaLoopbackRecordingSession session = module.Authorize(Publication(), Binding(), new("authorize-no-io-v1"));
        Assert.Equal(0, factory.CreateCount);
        Assert.False(session.IsConsumed);
        Assert.False(session.AdditionalAttemptAuthorized);
        string[] publicMethods = liveTypes.SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)).Where(static method => !method.IsSpecialName).Select(static method => method.Name).ToArray();
        Assert.DoesNotContain(publicMethods, static name => name.Contains("Apply", StringComparison.OrdinalIgnoreCase) || name.Contains("World", StringComparison.OrdinalIgnoreCase) || name.Contains("Simulation", StringComparison.OrdinalIgnoreCase) || name.Contains("Retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegisteredCell_IsExactAndDistinctFromHistoricalFixture()
    {
        Assert.Equal("qwen3.5:4b", SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference);
        Assert.Equal("qwen3.5-4b", SnowGlobePinnedOllamaRecordingModule.NormalizedModelIdentity);
        Assert.Equal(@"E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14\ollama.exe", SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath);
        Assert.Equal("11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54", SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256);
        Assert.Equal("2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd", SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256);
        Assert.Equal(3_389_983_735, SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes);
        Assert.Equal(4_659_865_088, SnowGlobePinnedOllamaRecordingModule.ParameterCount);
        Assert.Equal("http://127.0.0.1:11435/", SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity);
        Assert.Equal(4096, SnowGlobePinnedOllamaRecordingModule.ContextWindowTokens);
        Assert.Equal(96, SnowGlobePinnedOllamaRecordingModule.OutputTokenLimit);
        Assert.NotEqual(OfflinePinnedOllamaRecordingFixture.RegistryAdapterIdentity, SnowGlobePinnedOllamaRecordingModule.AdapterIdentity);
        Assert.NotEqual(CognitionQualityRecordingSessionCanonical.Digest(OfflinePinnedOllamaRecordingFixture.ContractDescriptor), SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256);
        Assert.Equal(CognitionQualityRecordingSessionCanonical.Digest(SnowGlobePinnedOllamaRecordingModule.RegisteredCellDescriptor), SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256);
    }

    [Fact]
    public void Authorization_RejectsWrongSourceBindingAndNonceReuseWithoutCreatingTransport()
    {
        CountingFactory factory = new(static () => throw new InvalidOperationException()); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), factory); CognitionQualityPromptEnvelopePublication publication = Publication();
        Assert.Throws<ArgumentException>(() => module.Authorize(publication, Binding() with { CanonicalExecutablePath = @"E:\other\ollama.exe" }, new("wrong-path-v1")));
        Assert.Throws<ArgumentException>(() => module.Authorize(publication, Binding() with { ExecutableSha256 = new string('0', 64) }, new("wrong-hash-v1")));
        Assert.Throws<ArgumentException>(() => module.Authorize(publication, Binding() with { EndpointIdentity = "http://localhost:11435/" }, new("wrong-endpoint-v1")));
        Assert.Throws<ArgumentException>(() => module.Authorize(publication, Binding() with { EndpointOwnerProcessId = 778 }, new("wrong-owner-v1")));
        _ = module.Authorize(publication, Binding(), new("reuse-nonce-v1"));
        OllamaLoopbackRecordingAuthorizationException reused = Assert.Throws<OllamaLoopbackRecordingAuthorizationException>(() => module.Authorize(publication, Binding(), new("reuse-nonce-v1")));
        Assert.Equal(OllamaLoopbackRecordingAuthorizationFailureCode.NonceReused, reused.Code); Assert.DoesNotContain("reuse-nonce-v1", reused.Message, StringComparison.Ordinal); Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Capability_IsAtomicOneUseExpirationAndObjectBound()
    {
        FixedClock clock = new(100); CountingFactory factory = SuccessFactory(); SnowGlobePinnedOllamaRecordingModule module = new(clock, factory);
        AuthorizedOllamaLoopbackRecordingSession expired = module.Authorize(Publication(), Binding(), new("expired-capability-v1")); clock.Advance(SnowGlobePinnedOllamaRecordingModule.CapabilityLifetimeMilliseconds);
        SnowGlobeOllamaLoopbackRecordingResult expiredResult = await expired.RecordOnceAsync(); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityExpired, expiredResult.FailureCode); Assert.Equal(0, factory.CreateCount);

        FixedClock freshClock = new(1); SnowGlobePinnedOllamaRecordingModule first = new(freshClock, SuccessFactory()); SnowGlobePinnedOllamaRecordingModule second = new(freshClock, SuccessFactory());
        AuthorizedOllamaLoopbackRecordingSession session = first.Authorize(Publication(), Binding(), new("object-bound-v1"));
        SnowGlobeOllamaLoopbackRecordingResult mismatch = await second.RecordAuthorizedSessionAsync(session, CancellationToken.None); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.BindingMismatch, mismatch.FailureCode);
        SnowGlobeOllamaLoopbackRecordingResult complete = await session.RecordOnceAsync(); SnowGlobeOllamaLoopbackRecordingResult reused = await session.RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete, complete.OutcomeCode); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityReused, reused.FailureCode); Assert.False(reused.AdditionalAttemptAuthorized);
    }

    [Fact]
    public void Authorization_NonceReservationIsConcurrentAndNonEvictingAt1024()
    {
        SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(static () => throw new InvalidOperationException())); CognitionQualityPromptEnvelopePublication publication = Publication();
        Parallel.For(0, SnowGlobePinnedOllamaRecordingModule.MaximumAuthorizedNonces, index => _ = module.Authorize(publication, Binding(), new($"capacity-nonce-{index:D4}-v1")));
        OllamaLoopbackRecordingAuthorizationException full = Assert.Throws<OllamaLoopbackRecordingAuthorizationException>(() => module.Authorize(publication, Binding(), new("capacity-overflow-v1")));
        Assert.Equal(OllamaLoopbackRecordingAuthorizationFailureCode.NonceCapacityExceeded, full.Code);
        OllamaLoopbackRecordingAuthorizationException reused = Assert.Throws<OllamaLoopbackRecordingAuthorizationException>(() => module.Authorize(publication, Binding(), new("capacity-nonce-0000-v1")));
        Assert.Equal(OllamaLoopbackRecordingAuthorizationFailureCode.NonceReused, reused.Code);
    }

    [Fact]
    public async Task CompletePath_AttemptsExactlyTwelveSequentialSlotsAndCreatesDistinctRawFreeReceipt()
    {
        CountingFactory factory = SuccessFactory(); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), factory); CognitionQualityPromptEnvelopePublication publication = Publication();
        byte[] callerPublication = publication.CanonicalUtf8.ToArray(); byte[][] callerPrompts = publication.Slots.Select(static slot => slot.PromptUtf8.ToArray()).ToArray();
        AuthorizedOllamaLoopbackRecordingSession session = module.Authorize(publication, Binding(), new("complete-recording-v1")); SnowGlobeOllamaLoopbackRecordingResult result = await session.RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete, result.OutcomeCode); Assert.Equal(12, result.CompletedSlotCount); Assert.Null(result.TerminalSlotOrdinal); Assert.NotNull(result.Evidence); Assert.NotNull(result.Receipt); Assert.Equal(12, result.Receipt!.Slots.Count);
        Assert.Equal(Enumerable.Range(1, 12), result.Receipt.Slots.Select(static slot => slot.SlotOrdinal)); Assert.All(result.Receipt.Slots, static slot => { Assert.Equal(200, slot.StatusCode); Assert.Equal(SubmissionState.ResponseReceived, slot.SubmissionState); Assert.False(slot.AdditionalAttemptAuthorized); });
        Assert.Equal(result.Evidence!.CanonicalDigestSha256, result.Receipt.NestedRecordingEvidenceDigestSha256); Assert.NotEqual(result.Evidence.CanonicalDigestSha256, result.Receipt.CanonicalDigestSha256);
        PropertyInfo evidenceProperty = Assert.Single(typeof(SnowGlobeOllamaLoopbackRecordingResult).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(static property => property.Name == nameof(SnowGlobeOllamaLoopbackRecordingResult.Evidence))); Assert.Equal(typeof(CognitionQualityRecordingEvidence), evidenceProperty.PropertyType);
        Assert.NotEmpty(result.Evidence.PromptPublication.Slots[0].PromptUtf8.ToArray()); Assert.NotNull(result.Evidence.RecordedResponseRun.ProposalBatch[0].Proposal);
        string json = result.Receipt.CanonicalJson; Assert.DoesNotContain("complete-recording-v1", json, StringComparison.Ordinal); Assert.DoesNotContain(SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, json, StringComparison.Ordinal); Assert.DoesNotContain("agent-00", json, StringComparison.Ordinal); Assert.DoesNotContain("GatherWood", json, StringComparison.Ordinal); Assert.Contains(result.ExecutablePathDigestSha256, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Encoding.UTF8.GetString(result.Evidence.PromptPublication.Slots[0].PromptUtf8.Span), json, StringComparison.Ordinal);
        Assert.False(result.HasBenchmarkOrQualityClaim); Assert.False(result.HasIndependentArtifactLoadedProof); Assert.False(result.HasWorldOrSimulationAuthority); Assert.False(result.HasRetryAuthority);
        Assert.Equal(callerPublication, publication.CanonicalUtf8.ToArray()); Assert.Equal(callerPrompts, publication.Slots.Select(static slot => slot.PromptUtf8.ToArray()).ToArray());
        JsonDocument.Parse(result.Receipt.CanonicalUtf8); Assert.Equal(result.Receipt.CanonicalDigestSha256, CognitionQualityHash.Sha256(result.Receipt.CanonicalUtf8.Span));
        Assert.Equal("da913180079fc534543748bc53198f7d10de527137f038812fa5f735b90c62ee", result.Receipt.CanonicalDigestSha256);
        AssertReceiptPayloadDigest(result.Receipt); Assert.InRange(result.Receipt.CanonicalUtf8.Length, 1, SnowGlobePinnedOllamaRecordingModule.MaximumReceiptBytes); byte[] detached = result.Receipt.CanonicalUtf8.ToArray(); detached[0] ^= 0xff; Assert.Equal((byte)'{', result.Receipt.CanonicalUtf8.Span[0]);
    }

    [Fact]
    public async Task RuntimeIdentityFailureBeforeDispatch_IsDefinitelyNotSubmittedAndRawFree()
    {
        WrapperHandler handler = new(); ScriptedVerifier verifier = new((point, _) => point == OllamaLoopbackRuntimeCheckPoint.BeforeDispatch ? OllamaLoopbackRuntimeVerification.Reject("wrong_pid_start_path_hash_or_owner") : OllamaLoopbackRuntimeVerification.Pass);
        CountingFactory factory = new(binding => new OllamaLoopbackRecordingTransportAdapter(binding, verifier, handler)); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), factory);
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("runtime-reject-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged, result.FailureCode); Assert.Equal(SubmissionState.DefinitelyNotSubmitted, result.TerminalSubmissionState); Assert.Equal(0, result.CompletedSlotCount); Assert.Equal(1, result.TerminalSlotOrdinal); Assert.Equal(0, handler.CallCount); Assert.Null(result.Evidence); Assert.NotNull(result.Receipt); Assert.DoesNotContain("wrong_pid", result.Receipt!.CanonicalJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RuntimeIdentityChangeDuringExchange_IsResponseReceivedAndStopsSession(int rejectedPointValue)
    {
        OllamaLoopbackRuntimeCheckPoint rejectedPoint = (OllamaLoopbackRuntimeCheckPoint)rejectedPointValue;
        WrapperHandler handler = new(); ScriptedVerifier verifier = new((point, _) => point == rejectedPoint ? OllamaLoopbackRuntimeVerification.Reject("changed") : OllamaLoopbackRuntimeVerification.Pass);
        SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(binding => new OllamaLoopbackRecordingTransportAdapter(binding, verifier, handler)));
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new($"runtime-change-{rejectedPoint.ToString().ToLowerInvariant()}-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged, result.FailureCode); Assert.Equal(SubmissionState.ResponseReceived, result.TerminalSubmissionState); Assert.Equal(0, result.CompletedSlotCount); Assert.Equal(1, result.TerminalSlotOrdinal); Assert.Equal(1, handler.CallCount); Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task PartialHttpFailure_HasNoNestedEvidenceNoRetryAndNoConcurrentExchange()
    {
        WrapperHandler handler = new(failureSlot: 3, failureStatus: HttpStatusCode.TooManyRequests); CountingFactory factory = new(binding => new OllamaLoopbackRecordingTransportAdapter(binding, new ScriptedVerifier(), handler)); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), factory);
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("partial-http-failure-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.HttpResponseRejected, result.FailureCode); Assert.Equal(2, result.CompletedSlotCount); Assert.Equal(3, result.TerminalSlotOrdinal); Assert.Equal(429, result.TerminalStatusCode); Assert.Equal(SubmissionState.ResponseReceived, result.TerminalSubmissionState); Assert.Null(result.Evidence); Assert.Null(result.Receipt!.NestedRecordingEvidenceDigestSha256); Assert.Equal(3, handler.CallCount); Assert.Equal(1, handler.MaximumConcurrentCalls); Assert.False(result.AdditionalAttemptAuthorized);
    }

    [Fact]
    public async Task CallerCancellationBeforeDispatch_CreatesNoTransportAndAuthorizesNoRetry()
    {
        CountingFactory factory = SuccessFactory(); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), factory); using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("pre-dispatch-cancel-v1")).RecordOnceAsync(cancellation.Token);
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled, result.OutcomeCode); Assert.Equal(SubmissionState.DefinitelyNotSubmitted, result.TerminalSubmissionState); Assert.Equal(0, factory.CreateCount); Assert.False(result.AdditionalAttemptAuthorized); Assert.Null(result.Evidence);
    }

    [Theory]
    [InlineData(false, SubmissionState.DefinitelyNotSubmitted)]
    [InlineData(true, SubmissionState.SubmissionUnknown)]
    public async Task UncausedHandlerCancellation_IsPublicTransportFailureWithExactSubmissionFence(bool serializeBody, SubmissionState expected)
    {
        UncausedCancellationHandler handler = new(serializeBody); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(binding => new OllamaLoopbackRecordingTransportAdapter(binding, new ScriptedVerifier(), handler)));
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new($"uncaused-oce-{serializeBody.ToString().ToLowerInvariant()}-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, result.OutcomeCode); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure, result.FailureCode); Assert.Equal(expected, result.TerminalSubmissionState); Assert.Equal(ChargeState.NotApplicable, result.TerminalChargeState); Assert.Equal(0, result.CompletedSlotCount); Assert.Equal(1, result.TerminalSlotOrdinal); Assert.Null(result.TerminalStatusCode); Assert.False(result.AdditionalAttemptAuthorized); Assert.Null(result.Evidence); Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UncausedBodyReadCancellation_IsPublicTransportFailureAfterResponseHeaders()
    {
        UncausedBodyCancellationHandler handler = new(); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(binding => new OllamaLoopbackRecordingTransportAdapter(binding, new ScriptedVerifier(), handler)));
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("uncaused-body-oce-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed, result.OutcomeCode); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure, result.FailureCode); Assert.Equal(SubmissionState.ResponseReceived, result.TerminalSubmissionState); Assert.Equal(ChargeState.NotApplicable, result.TerminalChargeState); Assert.Equal(200, result.TerminalStatusCode); Assert.Equal(0, result.CompletedSlotCount); Assert.Equal(1, result.TerminalSlotOrdinal); Assert.False(result.AdditionalAttemptAuthorized); Assert.Null(result.Evidence); Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RuntimeChangeBetweenSlots_StopsBeforeSecondSubmission()
    {
        int beforeDispatch = 0; WrapperHandler handler = new(); ScriptedVerifier verifier = new((point, _) => point == OllamaLoopbackRuntimeCheckPoint.BeforeDispatch && Interlocked.Increment(ref beforeDispatch) == 2 ? OllamaLoopbackRuntimeVerification.Reject("pid_reused") : OllamaLoopbackRuntimeVerification.Pass);
        SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(binding => new OllamaLoopbackRecordingTransportAdapter(binding, verifier, handler))); SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("between-slot-change-v1")).RecordOnceAsync();
        Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged, result.FailureCode); Assert.Equal(1, result.CompletedSlotCount); Assert.Equal(2, result.TerminalSlotOrdinal); Assert.Equal(SubmissionState.DefinitelyNotSubmitted, result.TerminalSubmissionState); Assert.Equal(1, handler.CallCount); Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task AcceptedHttpEnvelopeWithWrongModelWrapper_IsRejectedWithoutNestedEvidence()
    {
        WrapperHandler handler = new(wrapperMutation: static bytes => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("qwen3.5:4b", "wrong-model", StringComparison.Ordinal))); SnowGlobePinnedOllamaRecordingModule module = new(new FixedClock(1), new CountingFactory(binding => new OllamaLoopbackRecordingTransportAdapter(binding, new ScriptedVerifier(), handler)));
        SnowGlobeOllamaLoopbackRecordingResult result = await module.Authorize(Publication(), Binding(), new("wrong-wrapper-model-v1")).RecordOnceAsync(); Assert.Equal(SnowGlobeOllamaLoopbackRecordingFailureCode.WrapperRejected, result.FailureCode); Assert.Equal(SubmissionState.ResponseReceived, result.TerminalSubmissionState); Assert.Equal(0, result.CompletedSlotCount); Assert.Null(result.Evidence); Assert.NotNull(result.Receipt!.Slots[0].WrapperDigestSha256);
    }

    private static CountingFactory SuccessFactory() => new(binding => new OllamaLoopbackRecordingTransportAdapter(binding, new ScriptedVerifier(), new WrapperHandler()));
    private static CognitionQualityPromptEnvelopePublication Publication() => CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
    private static OllamaLoopbackRuntimeBinding Binding() => new(777, new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256, SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity, 777);
    private static string Action(int index) => index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
    private static int Quantity(int index) => index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
    private static byte[] Wrapper(int index)
    {
        string proposal = $"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}";
        return Encoding.UTF8.GetBytes($"{{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":{JsonSerializer.Serialize(proposal)},\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000}}");
    }

    private static void AssertReceiptPayloadDigest(SnowGlobeOllamaLoopbackRecordingReceipt receipt)
    {
        using JsonDocument document = JsonDocument.Parse(receipt.CanonicalUtf8); JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray(); Assert.Equal("receipt_payload_digest_sha256", properties[^1].Name); Assert.Equal(receipt.PayloadDigestSha256, properties[^1].Value.GetString());
        System.Buffers.ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer); writer.WriteStartObject(); foreach (JsonProperty property in properties[..^1]) { writer.WritePropertyName(property.Name); writer.WriteRawValue(property.Value.GetRawText(), skipInputValidation: false); } writer.WriteEndObject(); writer.Flush(); Assert.Equal(receipt.PayloadDigestSha256, CognitionQualityHash.Sha256(buffer.WrittenSpan));
    }

    private sealed class FixedClock : ICognitionQualityRecordingSessionClock
    {
        private long _now; internal FixedClock(long now) => _now = now; public long NowMilliseconds => Interlocked.Read(ref _now); internal void Advance(long value) => Interlocked.Add(ref _now, value);
    }

    private sealed class CountingFactory : IOllamaLoopbackRecordingTransportFactory
    {
        private readonly Func<OllamaLoopbackRuntimeBinding, IOfflineOllamaRecordingTransportPort> _create;
        private int _count;
        internal CountingFactory(Func<IOfflineOllamaRecordingTransportPort> create) : this(_ => create()) { }
        internal CountingFactory(Func<OllamaLoopbackRuntimeBinding, IOfflineOllamaRecordingTransportPort> create) => _create = create;
        internal int CreateCount => Volatile.Read(ref _count);
        public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding) { Interlocked.Increment(ref _count); return _create(binding); }
    }

    private sealed class ScriptedVerifier : IOllamaLoopbackRuntimeVerifier
    {
        private readonly Func<OllamaLoopbackRuntimeCheckPoint, OllamaLoopbackConnectionIdentity?, OllamaLoopbackRuntimeVerification> _verify;
        internal ScriptedVerifier(Func<OllamaLoopbackRuntimeCheckPoint, OllamaLoopbackConnectionIdentity?, OllamaLoopbackRuntimeVerification>? verify = null) => _verify = verify ?? ((_, _) => OllamaLoopbackRuntimeVerification.Pass);
        public OllamaLoopbackRuntimeVerification Verify(OllamaLoopbackRuntimeBinding binding, OllamaLoopbackRuntimeCheckPoint checkPoint, OllamaLoopbackConnectionIdentity? connection) => _verify(checkPoint, connection);
    }

    private sealed class WrapperHandler : HttpMessageHandler
    {
        private readonly int? _failureSlot; private readonly HttpStatusCode _failureStatus; private readonly Func<byte[], byte[]> _wrapperMutation; private int _calls; private int _concurrent; private int _maximumConcurrent;
        internal WrapperHandler(int? failureSlot = null, HttpStatusCode failureStatus = HttpStatusCode.InternalServerError, Func<byte[], byte[]>? wrapperMutation = null) { _failureSlot = failureSlot; _failureStatus = failureStatus; _wrapperMutation = wrapperMutation ?? (static bytes => bytes); }
        internal int CallCount => Volatile.Read(ref _calls); internal int MaximumConcurrentCalls => Volatile.Read(ref _maximumConcurrent);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int concurrent = Interlocked.Increment(ref _concurrent); int observed; do { observed = Volatile.Read(ref _maximumConcurrent); if (observed >= concurrent) break; } while (Interlocked.CompareExchange(ref _maximumConcurrent, concurrent, observed) != observed);
            try
            {
                int slot = Interlocked.Increment(ref _calls); Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("http://127.0.0.1:11435/api/generate", request.RequestUri!.AbsoluteUri); Assert.Equal(HttpVersion.Version11, request.Version); Assert.Equal(HttpVersionPolicy.RequestVersionExact, request.VersionPolicy); Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie")); Assert.False(request.Headers.Contains("Proxy-Authorization"));
                byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken); Assert.InRange(body.Length, 1, OfflineOllamaRecordingCodecModule.MaximumRequestBytes); Assert.Contains("\"stream\":false", Encoding.UTF8.GetString(body), StringComparison.Ordinal); Assert.Contains("\"num_ctx\":4096", Encoding.UTF8.GetString(body), StringComparison.Ordinal);
                byte[] responseBody = _failureSlot == slot ? "{}"u8.ToArray() : _wrapperMutation(Wrapper(slot)); HttpResponseMessage response = new(_failureSlot == slot ? _failureStatus : HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = new ByteArrayContent(responseBody) }; response.Content.Headers.ContentType = new("application/json"); return response;
            }
            finally { Interlocked.Decrement(ref _concurrent); }
        }
    }

    private sealed class UncausedCancellationHandler : HttpMessageHandler
    {
        private readonly bool _serialize; private int _calls; internal UncausedCancellationHandler(bool serialize) => _serialize = serialize; internal int CallCount => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); if (_serialize) _ = await request.Content!.ReadAsByteArrayAsync(CancellationToken.None); throw new OperationCanceledException("attacker-controlled-internal-oce"); }
    }

    private sealed class UncausedBodyCancellationHandler : HttpMessageHandler
    {
        private int _calls; internal int CallCount => Volatile.Read(ref _calls);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); _ = await request.Content!.ReadAsByteArrayAsync(CancellationToken.None); return new HttpResponseMessage(HttpStatusCode.OK) { Version = HttpVersion.Version11, Content = new UncausedCancellationBodyContent() }; }
    }

    private sealed class UncausedCancellationBodyContent : HttpContent
    {
        internal UncausedCancellationBodyContent() => Headers.ContentType = new("application/json");
        protected override bool TryComputeLength(out long length) { length = 1; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(new OperationCanceledException("attacker-controlled-body-oce"));
    }
}
