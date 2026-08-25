using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ProviderReadinessObservationTests
{
    [Fact]
    public void ContractDigestsAreBoundGoldenIdentities()
    {
        Assert.Equal("0dd5fd5590b3a3813181096e10c605a1fbddd4f8ea97eca4e983154170c26bfd",
            OllamaTagsMetadataCodec.ContractDigestSha256);
        Assert.Equal("5d668803d3241f8853e23e9bf54bc1e8432339ec7fe79e046e5a16a490ab5044",
            OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256);
        Assert.Equal("66e0a77ae5c373a52d3d0d06f3a648df470c3128e07bb4ecd2ba066b1302860c",
            OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256);
        Assert.Equal("361d3d2a9b07130929e106b58b87a5318f134661834c0265170a9c3e0724c1a5",
            ProviderReadinessObservationModule.ContractDigestSha256);
        Assert.Equal("cbb03e6379ace033dd52becbc1314473d427330b1278b023cb3ea3f708e12e5f",
            ProviderRoutingReadinessEvidenceModule.CurrentContractDigestSha256);
    }

    [Fact]
    public async Task ObserveAsync_UsesOneProviderNeutralPathAndIsDeterministic()
    {
        FrozenProviderReadinessClock clock = new(1_787_580_000_000);
        FakeAdapter openRouter = new(
            ProviderReadinessProvider.OpenRouter,
            ProviderReadinessAdapterResult.Ready(
                requestCount: 3,
                sourceSchemaVersion: OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                sourceContractDigestSha256: OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                accountBindingStatus: "same_account_bound"));

        ProviderReadinessObservation first = await ProviderReadinessObservationModule.ObserveAsync(
            openRouter, clock, CancellationToken.None);
        ProviderReadinessObservation second = await ProviderReadinessObservationModule.ObserveAsync(
            openRouter, clock, CancellationToken.None);

        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal("openrouter", first.Provider);
        Assert.Equal("ready", first.Readiness);
        Assert.Equal("ready", first.DiagnosticCode);
        Assert.Equal(3, first.RequestCount);
        Assert.Equal(0, first.GenerationRequestCount);
        Assert.Equal("same_account_bound", first.AccountBindingStatus);
        Assert.Equal(2, openRouter.CallCount);
        Assert.Equal(first.CanonicalDigestSha256,
            ProviderReadinessObservationModule.Validate(first.CanonicalUtf8, clock.NowMilliseconds).CanonicalDigestSha256);
    }

    [Fact]
    public async Task ObserveAsync_ClosesThrownAndUnavailableAdapterOutcomesWithoutRawDetail()
    {
        FrozenProviderReadinessClock clock = new(1_787_580_000_000);
        ProviderReadinessObservation unavailable = await ProviderReadinessObservationModule.ObserveAsync(
            new FakeAdapter(
                ProviderReadinessProvider.Ollama,
                ProviderReadinessAdapterResult.Unavailable(
                    "runtime_identity_drift", 0, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable")),
            clock,
            CancellationToken.None);
        ProviderReadinessObservation unknown = await ProviderReadinessObservationModule.ObserveAsync(
            new FakeAdapter(ProviderReadinessProvider.Ollama, new InvalidOperationException("raw dynamic detail")),
            clock,
            CancellationToken.None);

        Assert.Equal("unavailable", unavailable.Readiness);
        Assert.Equal("runtime_identity_drift", unavailable.DiagnosticCode);
        Assert.Equal("unknown", unknown.Readiness);
        Assert.Equal("observation_failed", unknown.DiagnosticCode);
        Assert.DoesNotContain("raw dynamic detail", unknown.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_SnapshotsCallerBytesAndRejectsExpiredEvidence()
    {
        FrozenProviderReadinessClock clock = new(1_787_580_000_000);
        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            new FakeAdapter(
                ProviderReadinessProvider.Ollama,
                ProviderReadinessAdapterResult.Ready(1, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable")),
            clock,
            CancellationToken.None);
        byte[] bytes = observation.CanonicalUtf8.ToArray();

        ProviderReadinessObservation current = ProviderReadinessObservationModule.Validate(
            bytes, observation.ExpiresAtUnixMilliseconds);
        Assert.Equal("ready", current.Readiness);
        ProviderReadinessObservationException expired = Assert.Throws<ProviderReadinessObservationException>(() =>
            ProviderReadinessObservationModule.Validate(bytes, observation.ExpiresAtUnixMilliseconds + 1));
        Assert.Equal("observation_expired", expired.Code);

        bytes[0] ^= 0x01;
        Assert.Equal("ready", current.Readiness);
    }

    [Fact]
    public async Task OllamaAdapterUsesOneTagsGetAndNeverGenerateOrAuthorization()
    {
        RecordingHandler handler = new(ValidTags());
        CountingRuntimeVerifier verifier = new();
        using OllamaAuthenticatedReadinessAdapter adapter = new(Binding(), verifier, handler);

        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);

        Assert.Equal("ready", observation.Readiness);
        Assert.Equal(1, observation.RequestCount);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Get, handler.Methods.Single());
        Assert.Equal("http://127.0.0.1:11435/api/tags", handler.Uris.Single());
        Assert.Null(handler.AuthorizationSchemes.Single());
        Assert.False(handler.HadContent.Single());
        Assert.DoesNotContain("/api/generate", observation.CanonicalJson, StringComparison.Ordinal);
        Assert.Equal(new[]
        {
            OllamaLoopbackRuntimeCheckPoint.BeforeDispatch,
            OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders,
            OllamaLoopbackRuntimeCheckPoint.AfterExchange
        }, verifier.CheckPoints);

        ProviderReadinessObservation repeated = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);
        Assert.Equal("unknown", repeated.Readiness);
        Assert.Equal("observation_failed", repeated.DiagnosticCode);
        Assert.Equal(0, repeated.RequestCount);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task OllamaAdapterRejectsIdentityDriftBeforeHttpAndMalformedTagsAfterOneGet()
    {
        RecordingHandler noCall = new(ValidTags());
        using OllamaAuthenticatedReadinessAdapter driftAdapter = new(
            Binding(), new CountingRuntimeVerifier(rejectFirst: true), noCall);
        ProviderReadinessObservation drift = await ProviderReadinessObservationModule.ObserveAsync(
            driftAdapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);
        Assert.Equal("unavailable", drift.Readiness);
        Assert.Equal("runtime_identity_drift", drift.DiagnosticCode);
        Assert.Equal(0, drift.RequestCount);
        Assert.Equal(0, noCall.CallCount);

        RecordingHandler malformedHandler = new(Encoding.UTF8.GetBytes("{\"models\":[]}"));
        using OllamaAuthenticatedReadinessAdapter malformedAdapter = new(
            Binding(), new CountingRuntimeVerifier(), malformedHandler);
        ProviderReadinessObservation malformed = await ProviderReadinessObservationModule.ObserveAsync(
            malformedAdapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);
        Assert.Equal("unavailable", malformed.Readiness);
        Assert.Equal("model_metadata_rejected", malformed.DiagnosticCode);
        Assert.Equal(1, malformed.RequestCount);
        Assert.Equal(1, malformedHandler.CallCount);
    }

    [Fact]
    public async Task OllamaPostDispatchIdentityRejectionIsUnknownRace()
    {
        foreach (OllamaLoopbackRuntimeCheckPoint rejectedCheckPoint in new[]
                 { OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders, OllamaLoopbackRuntimeCheckPoint.AfterExchange })
        {
            using OllamaAuthenticatedReadinessAdapter adapter = new(
                Binding(), new CountingRuntimeVerifier(rejectedCheckPoint), new RecordingHandler(ValidTags()));

            ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
                adapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);

            Assert.Equal("unknown", observation.Readiness);
            Assert.Equal("identity_race", observation.DiagnosticCode);
            Assert.Equal(1, observation.RequestCount);
        }
    }

    [Fact]
    public async Task OllamaConnectedBeforeBodyWrappedIdentityRaceIsUnknown()
    {
        HttpRequestException wrapped = new(
            "framework-wrapper-not-retained",
            new OllamaAuthenticatedReadinessAdapter.OllamaReadinessIdentityRaceException());
        using OllamaAuthenticatedReadinessAdapter adapter = new(
            Binding(), new CountingRuntimeVerifier(), new ThrowingHandler(wrapped));

        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);

        Assert.Equal("unknown", observation.Readiness);
        Assert.Equal("identity_race", observation.DiagnosticCode);
        Assert.Equal(1, observation.RequestCount);
        Assert.DoesNotContain("framework-wrapper", observation.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaAdapterRejectsTrailersThatArriveOnlyAfterBodyEof()
    {
        TrailerAfterEofHandler handler = new(ValidTags());
        using OllamaAuthenticatedReadinessAdapter adapter = new(
            Binding(), new CountingRuntimeVerifier(), handler);

        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None);

        Assert.Equal("unavailable", observation.Readiness);
        Assert.Equal("model_metadata_rejected", observation.DiagnosticCode);
        Assert.True(handler.TrailerWasPublished);
        Assert.Equal(1, observation.RequestCount);
    }

    [Fact]
    public void OllamaProductionHandlerIsHardenedAndTagsCodecRejectsOversizedShape()
    {
        using OllamaAuthenticatedReadinessAdapter adapter = new(Binding(), new CountingRuntimeVerifier());
        SocketsHttpHandler handler = Assert.IsType<SocketsHttpHandler>(adapter.ProductionHandler);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.False(handler.PreAuthenticate);
        Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression);
        Assert.Equal(1, handler.MaxConnectionsPerServer);
        Assert.Equal(0, handler.MaxResponseDrainSize);
        Assert.NotNull(handler.ConnectCallback);

        byte[] deeplyNested = Encoding.UTF8.GetBytes("{\"models\":[{\"x\":{\"a\":{\"b\":{\"c\":{\"d\":0}}}}}]}" );
        Assert.Equal("tags_json_too_deep", Assert.Throws<LocalModelBenchmarkException>(() =>
            OllamaTagsMetadataCodec.Validate(deeplyNested, ExpectedTagsModel())).Code);
    }

    [Fact]
    public async Task OllamaAdapterClosesCancellationTimeoutAndOversizedMetadataWithoutRetry()
    {
        const long now = 1_787_580_000_000;
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        RecordingHandler cancelledHandler = new(ValidTags());
        using OllamaAuthenticatedReadinessAdapter cancelledAdapter = new(
            Binding(), new CountingRuntimeVerifier(), cancelledHandler);
        ProviderReadinessObservation cancelledObservation =
            await ProviderReadinessObservationModule.ObserveAsync(
                cancelledAdapter, new FrozenProviderReadinessClock(now), cancelled.Token);

        using OllamaAuthenticatedReadinessAdapter timeoutAdapter = new(
            Binding(), new CountingRuntimeVerifier(), new ThrowingHandler(new OperationCanceledException()));
        ProviderReadinessObservation timeoutObservation =
            await ProviderReadinessObservationModule.ObserveAsync(
                timeoutAdapter, new FrozenProviderReadinessClock(now), CancellationToken.None);

        RecordingHandler oversizedHandler = new(new byte[OllamaAuthenticatedReadinessAdapter.MaximumTagsResponseBytes + 1]);
        using OllamaAuthenticatedReadinessAdapter oversizedAdapter = new(
            Binding(), new CountingRuntimeVerifier(), oversizedHandler);
        ProviderReadinessObservation oversizedObservation =
            await ProviderReadinessObservationModule.ObserveAsync(
                oversizedAdapter, new FrozenProviderReadinessClock(now), CancellationToken.None);

        Assert.Equal("operation_cancelled", cancelledObservation.DiagnosticCode);
        Assert.Equal(0, cancelledObservation.RequestCount);
        Assert.Equal(0, cancelledHandler.CallCount);
        Assert.Equal("observation_timeout", timeoutObservation.DiagnosticCode);
        Assert.Equal(1, timeoutObservation.RequestCount);
        Assert.Equal("model_metadata_rejected", oversizedObservation.DiagnosticCode);
        Assert.Equal(1, oversizedObservation.RequestCount);
        Assert.Equal(1, oversizedHandler.CallCount);
        Assert.All(new[] { cancelledObservation, timeoutObservation, oversizedObservation },
            observation => Assert.Equal(0, observation.GenerationRequestCount));
    }

    [Fact]
    public async Task OllamaBoundedReaderZeroesItsInternalRawMetadataBuffer()
    {
        bool? internalBufferWasZero = null;
        using ByteArrayContent content = new(ValidTags());

        byte[] result = await OllamaAuthenticatedReadinessAdapter.ReadBoundedAsync(
            content, CancellationToken.None, zeroed => internalBufferWasZero = zeroed);

        try
        {
            Assert.True(internalBufferWasZero);
            Assert.NotEmpty(result);
        }
        finally { CryptographicOperations.ZeroMemory(result); }
    }

    [Fact]
    public async Task ObserveAsyncRejectsAdapterClaimWithUnboundSourceContract()
    {
        FakeAdapter forged = new(
            ProviderReadinessProvider.OpenRouter,
            ProviderReadinessAdapterResult.Ready(
                3,
                OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                Digest("attacker-controlled-contract"),
                "same_account_bound"));

        ProviderReadinessObservationException exception =
            await Assert.ThrowsAsync<ProviderReadinessObservationException>(async () =>
                await ProviderReadinessObservationModule.ObserveAsync(
                    forged, new FrozenProviderReadinessClock(1_787_580_000_000), CancellationToken.None));

        Assert.Equal("observation_binding_invalid", exception.Code);
    }

    [Fact]
    public async Task ValidateRejectsProviderImpossibleDiagnosticsAfterCanonicalRedigest()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, now);
        List<byte[]> forgedOpenRouter = [];
        foreach (string diagnostic in new[] { "runtime_identity_drift", "model_metadata_rejected" })
        {
            forgedOpenRouter.Add(ForgeCanonical(openRouter.CanonicalUtf8.ToArray(), root =>
            {
                root["readiness"] = "unavailable";
                root["diagnostic_code"] = diagnostic;
                root["account_binding_status"] = "not_performed";
            }, "observation_payload_digest_sha256"));
        }
        forgedOpenRouter.Add(ForgeCanonical(openRouter.CanonicalUtf8.ToArray(), root =>
        {
            root["readiness"] = "unknown";
            root["diagnostic_code"] = "identity_race";
            root["account_binding_status"] = "not_performed";
        }, "observation_payload_digest_sha256"));
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama, now);
        List<byte[]> forgedOllama = [];
        foreach (string diagnostic in new[] { "credential_unavailable", "metadata_rejected" })
        {
            forgedOllama.Add(ForgeCanonical(ollama.CanonicalUtf8.ToArray(), root =>
            {
                root["readiness"] = "unavailable";
                root["diagnostic_code"] = diagnostic;
            }, "observation_payload_digest_sha256"));
        }

        foreach (byte[] forged in forgedOpenRouter.Concat(forgedOllama))
            Assert.Equal("observation_binding_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
                ProviderReadinessObservationModule.Validate(forged, now)).Code);
    }

    [Fact]
    public async Task ValidateRejectsProviderDiagnosticRequestCountIncoherenceAfterCanonicalRedigest()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, now);
        List<byte[]> forged = [];
        foreach (string diagnostic in new[] { "metadata_rejected", "provider_unavailable" })
            forged.Add(ForgeCanonical(openRouter.CanonicalUtf8.ToArray(), root =>
            {
                root["readiness"] = "unavailable";
                root["diagnostic_code"] = diagnostic;
                root["request_count"] = 0;
                root["account_binding_status"] = "not_performed";
            }, "observation_payload_digest_sha256"));

        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama, now);
        foreach ((string readiness, string diagnostic, int count) in new[]
                 {
                     ("unavailable", "runtime_identity_drift", 1),
                     ("unknown", "identity_race", 0),
                     ("unavailable", "model_metadata_rejected", 0),
                     ("unavailable", "provider_unavailable", 0)
                 })
            forged.Add(ForgeCanonical(ollama.CanonicalUtf8.ToArray(), root =>
            {
                root["readiness"] = readiness;
                root["diagnostic_code"] = diagnostic;
                root["request_count"] = count;
            }, "observation_payload_digest_sha256"));

        foreach (byte[] canonical in forged)
            Assert.Equal("observation_binding_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
                ProviderReadinessObservationModule.Validate(canonical, now)).Code);
    }

    [Fact]
    public async Task OllamaSourceContractBindsRegisteredProvenanceAndRejectsTamper()
    {
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.QuantizationLevel,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(SnowGlobePinnedOllamaRecordingModule.ContextWindowTokens.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(OllamaTagsMetadataCodec.SchemaVersion,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);
        Assert.Contains(OllamaTagsMetadataCodec.ContractDigestSha256,
            OllamaAuthenticatedReadinessAdapter.ContractDescriptor, StringComparison.Ordinal);

        const long now = 1_787_580_000_000;
        ProviderReadinessObservation observation = await ReadyObservation(
            ProviderReadinessProvider.Ollama, now);
        byte[] forged = ForgeCanonical(observation.CanonicalUtf8.ToArray(), root =>
            root["source_contract_digest_sha256"] = Digest("tampered-ollama-source"),
            "observation_payload_digest_sha256");

        Assert.Equal("observation_binding_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
            ProviderReadinessObservationModule.Validate(forged, now)).Code);
    }

    [Fact]
    public async Task CurrentAssessmentRecordsReadyFactsButNeverIssuesRoutingInput()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, now);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama, now);
        ProviderRoutingReadinessEvidenceInput historical = new(null);

        ProviderRoutingCurrentReadinessAssessment first =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                historical, openRouter.CanonicalUtf8, ollama.CanonicalUtf8, now);
        ProviderRoutingCurrentReadinessAssessment second =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                historical, openRouter.CanonicalUtf8, ollama.CanonicalUtf8, now);
        ProviderRoutingCurrentReadinessAssessment validated =
            ProviderRoutingReadinessEvidenceModule.ValidateCurrent(first.CanonicalUtf8, now);

        Assert.Equal("ready", first.OpenRouterCurrentReadiness);
        Assert.Equal("ready", first.OllamaCurrentReadiness);
        Assert.Equal("unknown", first.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", first.RoutingInputIssuanceStatus);
        Assert.Null(first.RoutingPolicyInput);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Contains("authenticated_attempt_bound_primary_state_unproven", first.GapCodes);
    }

    [Fact]
    public async Task CurrentAssessmentTreatsMissingMalformedAndExpiredEvidenceAsUnknown()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation expired = await ReadyObservation(
            ProviderReadinessProvider.Ollama,
            now - ProviderReadinessObservationModule.ObservationLifetimeMilliseconds - 1);
        byte[] malformed = Encoding.UTF8.GetBytes("{}");
        byte[] expiredBytes = expired.CanonicalUtf8.ToArray();

        ProviderRoutingCurrentReadinessAssessment assessment =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                new ProviderRoutingReadinessEvidenceInput(null), malformed, expiredBytes, now);
        malformed[0] = (byte)'[';
        expiredBytes[0] ^= 0x01;

        Assert.Equal("unknown", assessment.OpenRouterCurrentReadiness);
        Assert.Equal("unknown", assessment.OllamaCurrentReadiness);
        Assert.Contains("current_openrouter_readiness_evidence_malformed", assessment.GapCodes);
        Assert.Contains("current_ollama_readiness_evidence_expired", assessment.GapCodes);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
        Assert.Null(assessment.RoutingPolicyInput);
        Assert.Equal(assessment.CanonicalDigestSha256,
            ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                assessment.CanonicalUtf8, now).CanonicalDigestSha256);
    }

    [Fact]
    public async Task CurrentAssessmentMapsAuthenticatedUnavailableToNotReadyWithoutAttemptInference()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation unavailable = await ProviderReadinessObservationModule.ObserveAsync(
            new FakeAdapter(
                ProviderReadinessProvider.OpenRouter,
                ProviderReadinessAdapterResult.Unavailable(
                    "credential_unavailable", 0,
                    OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                    "not_performed")),
            new FrozenProviderReadinessClock(now), CancellationToken.None);

        ProviderRoutingCurrentReadinessAssessment assessment =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                new ProviderRoutingReadinessEvidenceInput(null), unavailable.CanonicalUtf8, null, now);

        Assert.Equal("not_ready", assessment.OpenRouterCurrentReadiness);
        Assert.Equal("unknown", assessment.OllamaCurrentReadiness);
        Assert.Equal("unknown", assessment.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
        Assert.Null(assessment.RoutingPolicyInput);
        Assert.Equal("assessment_binding_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                new ProviderRoutingReadinessEvidenceInput(null), null, null, -1)).Code);
    }

    [Fact]
    public async Task ValidatorsSnapshotHostileMemoryExactlyOnce()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation observation = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, now);
        byte[] observationBytes = observation.CanonicalUtf8.ToArray();
        using ChangingMemoryManager observationMemory = new(observationBytes);

        ProviderReadinessObservation validated = ProviderReadinessObservationModule.Validate(
            observationMemory.CreateReadOnlyMemory(), now);

        Assert.Equal(observation.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Equal(1, observationMemory.GetSpanCallCount);

        using ChangingMemoryManager inputMemory = new(observationBytes);
        ProviderRoutingCurrentReadinessAssessment assessment =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                new ProviderRoutingReadinessEvidenceInput(null), inputMemory.CreateReadOnlyMemory(), null, now);
        Assert.Equal(1, inputMemory.GetSpanCallCount);

        using ChangingMemoryManager assessmentMemory = new(assessment.CanonicalUtf8.ToArray());
        ProviderRoutingCurrentReadinessAssessment validatedAssessment =
            ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                assessmentMemory.CreateReadOnlyMemory(), now);
        Assert.Equal(assessment.CanonicalDigestSha256, validatedAssessment.CanonicalDigestSha256);
        Assert.Equal(1, assessmentMemory.GetSpanCallCount);
        CryptographicOperations.ZeroMemory(observationBytes);
    }

    [Fact]
    public async Task ValidatorsRejectProducerImpossibleZeroAndOverflowingTimesWithClosedErrors()
    {
        const long now = 1_787_580_000_000;
        ProviderReadinessObservation observation = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, now);
        byte[] zeroObservation = ForgeCanonical(observation.CanonicalUtf8.ToArray(), root =>
        {
            root["observed_at_unix_ms"] = 0;
            root["expires_at_unix_ms"] = ProviderReadinessObservationModule.ObservationLifetimeMilliseconds;
        }, "observation_payload_digest_sha256");
        byte[] highObservation = ForgeCanonical(observation.CanonicalUtf8.ToArray(), root =>
        {
            long high = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
            root["observed_at_unix_ms"] = high;
            root["expires_at_unix_ms"] = high;
        }, "observation_payload_digest_sha256");

        Assert.Equal("observation_time_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
            ProviderReadinessObservationModule.Validate(zeroObservation,
                ProviderReadinessObservationModule.ObservationLifetimeMilliseconds)).Code);
        Assert.Equal("observation_time_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
            ProviderReadinessObservationModule.Validate(highObservation,
                DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())).Code);
        Assert.Equal("observation_time_invalid", Assert.Throws<ProviderReadinessObservationException>(() =>
            ProviderReadinessObservationModule.Validate(
                observation.CanonicalUtf8, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() + 1)).Code);

        ProviderRoutingCurrentReadinessAssessment assessment =
            ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                new ProviderRoutingReadinessEvidenceInput(null), null, null, now);
        byte[] zeroAssessment = ForgeCanonical(assessment.CanonicalUtf8.ToArray(), root =>
        {
            root["assessed_at_unix_ms"] = 0;
            root["expires_at_unix_ms"] = ProviderReadinessObservationModule.ObservationLifetimeMilliseconds;
        }, "assessment_payload_digest_sha256");
        byte[] highAssessment = ForgeCanonical(assessment.CanonicalUtf8.ToArray(), root =>
        {
            long high = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
            root["assessed_at_unix_ms"] = high;
            root["expires_at_unix_ms"] = high;
        }, "assessment_payload_digest_sha256");

        Assert.Equal("assessment_binding_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.ValidateCurrent(zeroAssessment, 1)).Code);
        Assert.Equal("assessment_binding_invalid", Assert.Throws<ProviderRoutingReadinessEvidenceException>(() =>
            ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                highAssessment, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())).Code);
    }

    private static ValueTask<ProviderReadinessObservation> ReadyObservation(
        ProviderReadinessProvider provider,
        long observedAt) => ProviderReadinessObservationModule.ObserveAsync(
            new FakeAdapter(
                provider,
                provider == ProviderReadinessProvider.OpenRouter
                    ? ProviderReadinessAdapterResult.Ready(
                        3,
                        OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                        OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                        "same_account_bound")
                    : ProviderReadinessAdapterResult.Ready(
                        1,
                        OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                        OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                        "not_applicable")),
            new FrozenProviderReadinessClock(observedAt), CancellationToken.None);

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static byte[] ForgeCanonical(byte[] canonical, Action<JsonObject> mutate, string digestProperty)
    {
        JsonObject root = JsonNode.Parse(canonical)!.AsObject();
        CryptographicOperations.ZeroMemory(canonical);
        mutate(root);
        root.Remove(digestProperty);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root[digestProperty] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static OllamaLoopbackRuntimeBinding Binding() => new(
        777,
        new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks,
        SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath,
        SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
        SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity,
        777);

    private static OllamaTagsExpectedModel ExpectedTagsModel() => new(
        SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
        SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
        SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes,
        SnowGlobePinnedOllamaRecordingModule.ArtifactFormat,
        SnowGlobePinnedOllamaRecordingModule.ModelFamily,
        null,
        SnowGlobePinnedOllamaRecordingModule.QuantizationLevel,
        SnowGlobePinnedOllamaRecordingModule.ContextWindowTokens);

    private static byte[] ValidTags() => JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
    {
        ["models"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["name"] = SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
                ["model"] = SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
                ["modified_at"] = "2026-08-24T12:00:00Z",
                ["size"] = SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes,
                ["digest"] = SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
                ["details"] = new Dictionary<string, object?>
                {
                    ["parent_model"] = string.Empty,
                    ["format"] = SnowGlobePinnedOllamaRecordingModule.ArtifactFormat,
                    ["family"] = SnowGlobePinnedOllamaRecordingModule.ModelFamily,
                    ["families"] = new[] { SnowGlobePinnedOllamaRecordingModule.ModelFamily },
                    ["parameter_size"] = "4.7B",
                    ["quantization_level"] = SnowGlobePinnedOllamaRecordingModule.QuantizationLevel,
                    ["context_length"] = 262_144,
                    ["embedding_length"] = 2_560
                },
                ["capabilities"] = new[] { "completion" }
            }
        }
    });

    private sealed class FrozenProviderReadinessClock(long nowMilliseconds) : IProviderReadinessClock
    {
        public long NowMilliseconds => nowMilliseconds;
    }

    private sealed class FakeAdapter : IProviderReadinessObservationAdapter
    {
        private readonly ProviderReadinessAdapterResult? _result;
        private readonly Exception? _exception;

        internal FakeAdapter(ProviderReadinessProvider provider, ProviderReadinessAdapterResult result)
        {
            Provider = provider;
            _result = result;
        }

        internal FakeAdapter(ProviderReadinessProvider provider, Exception exception)
        {
            Provider = provider;
            _exception = exception;
        }

        public ProviderReadinessProvider Provider { get; }
        public int CallCount { get; private set; }

        public ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (_exception is not null) throw _exception;
            return ValueTask.FromResult(_result!);
        }
    }

    private sealed class CountingRuntimeVerifier : IOllamaLoopbackRuntimeVerifier
    {
        private readonly OllamaLoopbackRuntimeCheckPoint? _rejectedCheckPoint;

        internal CountingRuntimeVerifier(bool rejectFirst = false) => _rejectedCheckPoint = rejectFirst
            ? OllamaLoopbackRuntimeCheckPoint.BeforeDispatch : null;
        internal CountingRuntimeVerifier(OllamaLoopbackRuntimeCheckPoint rejectedCheckPoint) =>
            _rejectedCheckPoint = rejectedCheckPoint;
        internal List<OllamaLoopbackRuntimeCheckPoint> CheckPoints { get; } = [];

        public OllamaLoopbackRuntimeVerification Verify(
            OllamaLoopbackRuntimeBinding binding,
            OllamaLoopbackRuntimeCheckPoint checkPoint,
            OllamaLoopbackConnectionIdentity? connection)
        {
            CheckPoints.Add(checkPoint);
            return _rejectedCheckPoint == checkPoint
                ? OllamaLoopbackRuntimeVerification.Reject("dynamic-not-exposed")
                : OllamaLoopbackRuntimeVerification.Pass;
        }
    }

    private sealed class RecordingHandler(byte[] body) : HttpMessageHandler
    {
        internal int CallCount { get; private set; }
        internal List<HttpMethod> Methods { get; } = [];
        internal List<string> Uris { get; } = [];
        internal List<string?> AuthorizationSchemes { get; } = [];
        internal List<bool> HadContent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Methods.Add(request.Method);
            Uris.Add(request.RequestUri!.AbsoluteUri);
            AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
            HadContent.Add(request.Content is not null);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version11,
                RequestMessage = request,
                Content = new ByteArrayContent(body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class TrailerAfterEofHandler(byte[] body) : HttpMessageHandler
    {
        internal bool TrailerWasPublished { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version11,
                RequestMessage = request
            };
            response.Content = new StreamContent(new EofCallbackStream(body, () =>
            {
                response.TrailingHeaders.TryAddWithoutValidation("x-snow-globe-test-trailer", "present");
                TrailerWasPublished = true;
            }));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return Task.FromResult(response);
        }
    }

    private sealed class EofCallbackStream(byte[] body, Action onEof) : Stream
    {
        private readonly MemoryStream _inner = new(body, writable: false);
        private int _reported;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            ReportEof(read);
            return read;
        }
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken);
            ReportEof(read);
            return read;
        }
        private void ReportEof(int read)
        {
            if (read == 0 && Interlocked.Exchange(ref _reported, 1) == 0) onEof();
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ChangingMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _later;
        private int _getSpanCallCount;

        internal ChangingMemoryManager(byte[] first)
        {
            _first = first.ToArray();
            _later = Enumerable.Repeat((byte)' ', first.Length).ToArray();
        }

        internal int GetSpanCallCount => _getSpanCallCount;
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_first.Length);
        public override Span<byte> GetSpan() => Interlocked.Increment(ref _getSpanCallCount) == 1 ? _first : _later;
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_later);
        }
    }
}
