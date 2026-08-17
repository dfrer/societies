using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OllamaBenchmarkRunnerTests
{
    [Fact]
    public async Task AuthorizedCanonicalRun_ProducesAcceptedProvenanceBoundSampledEvidence()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        SequenceVramProbe probe = new(index => index == 1 ? 6900 : index == 2 ? 5120 : 6144);
        FakeTransport transport = SuccessTransport(plan, runtime);
        OllamaBenchmarkRunner runner = new(transport, probe, new FixedStepClock());

        OllamaBenchmarkRunResult result = await runner.RunAsync(plan, Capability(plan, runtime));

        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, result.Evidence).IsAccepted);
        Assert.Equal(plan.WarmupRequestCount + plan.MeasuredRequestCount + 1, transport.CallCount);
        Assert.Equal(HttpMethod.Get, transport.Requests[0].Method);
        Assert.Equal("/api/tags", transport.Requests[0].RequestUri.AbsolutePath);
        Assert.Equal(1, transport.Requests.Count(request => request.RequestUri.AbsolutePath == "/api/tags"));
        Assert.Equal(runtime.RuntimeModelReference, result.Provenance.RuntimeModelReference);
        Assert.Equal(runtime.ArtifactDigestSha256, result.Provenance.ArtifactDigestSha256);
        Assert.Equal(runtime.ArtifactSizeBytes, result.Provenance.ArtifactSizeBytes);
        Assert.Equal(runtime.ArtifactFormat, result.Provenance.ArtifactFormat);
        Assert.Equal(runtime.ModelFamily, result.Provenance.ModelFamily);
        Assert.Equal(runtime.ParameterSize, result.Provenance.ParameterSize);
        Assert.Equal(runtime.QuantizationLevel, result.Provenance.QuantizationLevel);
        Assert.Equal(runtime.OllamaProcessIdentity, result.Provenance.OllamaProcessIdentity);
        Assert.Equal(runtime.OllamaProcessId, result.Provenance.OllamaProcessId);
        Assert.Equal(plan.ModelIdentity, result.Evidence.ModelIdentity);
        Assert.NotEqual(result.Evidence.ModelIdentity, result.Provenance.RuntimeModelReference);
        Assert.Equal(6900, result.ObservedSampledPeakVramMiB);
        Assert.Equal(6900, result.Evidence.PeakVramMiB);
        Assert.Equal(5120, result.Evidence.StaticVramMiB);
        Assert.Equal(plan.VramBudgetMiB * 0.85, result.MaximumAllowedObservedVramMiB);
        Assert.Equal(OllamaBenchmarkRunner.OutputTokenLimit, result.OutputTokenLimit);
        Assert.True(result.ClientSideSerializationEnforced);
        Assert.False(result.ExternalServerStartupConfigurationVerified);
        Assert.Equal(OllamaBenchmarkRunner.ExternalStartupConfigurationClaim, result.ExternalServerStartupConfigurationClaim);
        Assert.Equal(
            plan.WarmupRequestCount + plan.MeasuredRequestCount + 1,
            result.VramSampleCount);
        Assert.Equal(0, result.Evidence.FailureCount);
        Assert.Equal(0, result.Evidence.FallbackCount);
        Assert.Equal(40, result.Evidence.ThroughputTokensPerSecond);
        Assert.Equal(1, result.Evidence.PeakQueueDepth);
        Assert.Equal(10, result.Evidence.TotalQueueWaitMilliseconds);

        LocalModelBenchmarkTransportRequest generate = transport.Requests.First(request => request.Method == HttpMethod.Post);
        using JsonDocument body = JsonDocument.Parse(generate.BodyUtf8);
        Assert.Equal("qwen3.5:4b", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(96, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(plan.ContextWindowTokens, body.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(body.RootElement.GetProperty("think").GetBoolean());
        Assert.DoesNotContain("secret-marker", Encoding.UTF8.GetString(generate.BodyUtf8), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capability_IsRequiredPlanBoundSingleUse_AndQueueRejectionDoesNotConsume()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        FakeTransport transport = SuccessTransport(plan, runtime);
        OllamaBenchmarkRunner runner = new(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock());

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(plan, null!));
        Assert.Equal(0, transport.CallCount);

        LocalModelBenchmarkExecutionCapability mismatched = Capability(plan, runtime);
        LocalModelBenchmarkPlan anotherPlan = ValidPlan(endpoint: "http://127.0.0.1:11435/");
        LocalModelBenchmarkException mismatch = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            runner.RunAsync(anotherPlan, mismatched));
        Assert.Equal("execution_capability_mismatch", mismatch.Code);
        Assert.False(mismatched.IsConsumed);
        Assert.Equal(0, transport.CallCount);

        LocalModelBenchmarkExecutionCapability oneShot = Capability(plan, runtime);
        await runner.RunAsync(plan, oneShot);
        int completedCalls = transport.CallCount;
        Assert.True(oneShot.IsConsumed);
        LocalModelBenchmarkException reuse = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => runner.RunAsync(plan, oneShot));
        Assert.Equal("execution_capability_reused", reuse.Code);
        Assert.Equal(completedCalls, transport.CallCount);

        using CancellationTokenSource alreadyCancelled = new();
        alreadyCancelled.Cancel();
        LocalModelBenchmarkExecutionCapability cancelledCapability = Capability(plan, runtime);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(plan, cancelledCapability, alreadyCancelled.Token));
        Assert.False(cancelledCapability.IsConsumed);
        Assert.Equal(completedCalls, transport.CallCount);
    }

    [Theory]
    [InlineData("gpt-oss:120b-cloud")]
    [InlineData("glm-4.7:cloud")]
    [InlineData("glm-4.7:CLOUD")]
    public void CapabilityIssuance_RejectsCloudRuntimeReferencesBeforeTransport(string runtimeModelReference)
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime() with { RuntimeModelReference = runtimeModelReference };

        LocalModelBenchmarkException exception = Assert.Throws<LocalModelBenchmarkException>(() => Capability(plan, runtime));

        Assert.Equal("cloud_model_rejected", exception.Code);
    }

    [Fact]
    public async Task UppercaseQuantSuffixRuntimeTag_IsAcceptedExactly()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime() with
        {
            RuntimeModelReference = "qwen3.5:4b-q4_K_M"
        };
        FakeTransport transport = SuccessTransport(plan, runtime);

        OllamaBenchmarkRunResult result = await new OllamaBenchmarkRunner(
            transport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));

        Assert.Equal(runtime.RuntimeModelReference, result.Provenance.RuntimeModelReference);
        Assert.All(
            transport.Requests.Where(request => request.Method == HttpMethod.Post),
            request => Assert.Contains(runtime.RuntimeModelReference, Encoding.UTF8.GetString(request.BodyUtf8), StringComparison.Ordinal));
    }

    [Fact]
    public void CapabilityIssuance_RejectsNonGgufAndSentinelRuntimeMetadata()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        foreach (OllamaRuntimeAuthorization runtime in new[]
        {
            ValidRuntime() with { ArtifactFormat = "safetensors" },
            ValidRuntime() with { ModelFamily = "unknown" },
            ValidRuntime() with { ParameterSize = "none" },
            ValidRuntime() with { QuantizationLevel = "unspecified" }
        })
        {
            LocalModelBenchmarkException exception = Assert.Throws<LocalModelBenchmarkException>(() => Capability(plan, runtime));
            Assert.Equal("runtime_authorization_invalid", exception.Code);
        }
    }

    [Fact]
    public void CapabilityIssuance_RejectsRuntimeQuantizationNotBoundToPlan()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime() with { QuantizationLevel = "Q8_0" };

        LocalModelBenchmarkException exception = Assert.Throws<LocalModelBenchmarkException>(() => Capability(plan, runtime));

        Assert.Equal("runtime_quantization_not_bound_to_plan", exception.Code);
    }

    [Theory]
    [MemberData(nameof(InvalidTags))]
    public async Task TagsMissingAliasDuplicateMalformedUnknownCloudAndProvenanceMismatch_AreRejected(
        string tagsJson,
        string expectedCode)
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        tagsJson = tagsJson.Replace("DIGEST", runtime.ArtifactDigestSha256, StringComparison.Ordinal);
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            Assert.Equal("/api/tags", request.RequestUri.AbsolutePath);
            await Task.Yield();
            return JsonResponse(request.RequestUri, tagsJson);
        });
        OllamaBenchmarkRunner runner = new(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock());

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            runner.RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(1, transport.CallCount);
    }

    public static IEnumerable<object[]> InvalidTags()
    {
        string valid = ValidTags(ValidRuntime()).Replace(new string('a', 64), "DIGEST", StringComparison.Ordinal);
        yield return new object[] { "{\"models\":[]}", "tags_models_missing" };
        yield return new object[] { valid.Replace("qwen3.5:4b\",\"model\":\"qwen3.5:4b", "qwen3.5:4b\",\"model\":\"alias:4b", StringComparison.Ordinal), "runtime_model_alias_rejected" };
        int arrayStart = valid.IndexOf('[') + 1;
        string modelObject = valid[arrayStart..valid.LastIndexOf(']')];
        yield return new object[] { "{\"models\":[" + modelObject + "," + modelObject + "]}", "runtime_model_duplicate" };
        string digestAlias = modelObject.Replace("qwen3.5:4b", "alias:4b", StringComparison.Ordinal);
        yield return new object[] { "{\"models\":[" + modelObject + "," + digestAlias + "]}", "runtime_digest_alias_rejected" };
        yield return new object[] { valid[..^1], "tags_json_invalid" };
        yield return new object[] { valid.Replace("{\"models\"", "{\"cloud\":true,\"models\"", StringComparison.Ordinal), "tags_json_invalid" };
        yield return new object[] { valid.Replace("\"digest\":\"DIGEST\"", "\"digest\":\"DIGEST\",\"digest\":\"DIGEST\"", StringComparison.Ordinal), "tags_json_duplicate_property" };
        yield return new object[] { valid.Replace("3400000000", "3400000001", StringComparison.Ordinal), "runtime_provenance_mismatch" };
        yield return new object[] { valid.Replace("DIGEST", new string('b', 64), StringComparison.Ordinal), "runtime_provenance_mismatch" };
        yield return new object[] { valid.Replace("\"format\":\"gguf\"", "\"format\":\"safetensors\"", StringComparison.Ordinal), "tags_model_invalid" };
        yield return new object[] { valid.Replace("\"family\":\"qwen35\"", "\"family\":\"unknown\"", StringComparison.Ordinal), "tags_model_invalid" };
        yield return new object[] { valid.Replace("4.66B", "unknown", StringComparison.Ordinal), "tags_model_invalid" };
        yield return new object[] { valid.Replace("Q4_K_M", "unknown", StringComparison.Ordinal), "tags_model_invalid" };
        yield return new object[] { valid.Replace("\"family\":\"qwen35\"", "\"family\":\"qwen3\"", StringComparison.Ordinal), "runtime_provenance_mismatch" };
        yield return new object[] { valid.Replace("4.66B", "4.67B", StringComparison.Ordinal), "runtime_provenance_mismatch" };
        yield return new object[] { valid.Replace("Q4_K_M", "Q8_0", StringComparison.Ordinal), "runtime_provenance_mismatch" };
    }

    [Theory]
    [MemberData(nameof(InvalidGenerateResponses))]
    public async Task GenerateMalformedDuplicateUnknownDeepAndWrongModel_AreRejected(
        string generateJson,
        string expectedCode)
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            await Task.Yield();
            return request.Method == HttpMethod.Get
                ? JsonResponse(request.RequestUri, ValidTags(runtime))
                : JsonResponse(request.RequestUri, generateJson);
        });

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(2, transport.CallCount);
    }

    public static IEnumerable<object[]> InvalidGenerateResponses()
    {
        string valid = ValidGenerate(ValidRuntime().RuntimeModelReference);
        yield return new object[] { valid[..^1], "response_json_invalid" };
        yield return new object[]
        {
            valid.Replace("\"eval_count\":20", "\"eval_count\":20,\"eval_count\":20", StringComparison.Ordinal),
            "response_json_duplicate_property"
        };
        yield return new object[] { "{\"unknown\":true," + valid[1..], "response_json_invalid" };
        yield return new object[] { "{\"extra\":{\"a\":{\"b\":{\"c\":{}}}}," + valid[1..], "response_json_too_deep" };
        yield return new object[]
        {
            valid.Replace("qwen3.5:4b", "qwen3.5:8b", StringComparison.Ordinal),
            "runtime_model_response_mismatch"
        };
    }

    [Theory]
    [InlineData(97, 100, 1000000000L, 1000000L, 100000000L, 500000000L, "stop")]
    [InlineData(20, 4097, 1000000000L, 1000000L, 100000000L, 500000000L, "stop")]
    [InlineData(20, 100, 1000000001L, 1000000L, 100000000L, 500000000L, "stop")]
    [InlineData(20, 100, 500000000L, 100000000L, 200000000L, 300000000L, "stop")]
    [InlineData(20, 100, 1000000000L, 1000000L, 100000000L, 500000000L, "length")]
    public async Task CounterInflationAndNonStopCompletion_AreRejected(
        int evalCount,
        int promptCount,
        long totalDuration,
        long loadDuration,
        long promptDuration,
        long evalDuration,
        string doneReason)
    {
        LocalModelBenchmarkPlan plan = ValidPlan(requestTimeoutMilliseconds: 1000);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            await Task.Yield();
            return request.Method == HttpMethod.Get
                ? JsonResponse(request.RequestUri, ValidTags(runtime))
                : JsonResponse(request.RequestUri, ValidGenerate(
                    runtime.RuntimeModelReference,
                    evalCount,
                    promptCount,
                    totalDuration,
                    loadDuration,
                    promptDuration,
                    evalDuration,
                    doneReason));
        });

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal("response_counter_or_content_invalid", exception.Code);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task SampledVram_RejectsWrongDigestPidTimeoutAndAboveEightyFivePercent_WhileBoundaryPasses()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        foreach ((SequenceVramProbe Probe, string Code) candidate in new[]
        {
            (new SequenceVramProbe(_ => 1000, digestOverride: new string('b', 64)), "vram_reading_identity_invalid"),
            (new SequenceVramProbe(_ => 1000, processIdentityOverride: "ollama-other-v1"), "vram_reading_identity_invalid"),
            (new SequenceVramProbe(_ => 1000, processIdOverride: runtime.OllamaProcessId + 1), "vram_reading_identity_invalid"),
            (new SequenceVramProbe(_ => plan.VramBudgetMiB * 0.85 + 0.001), "vram_headroom_exceeded")
        })
        {
            FakeTransport transport = SuccessTransport(plan, runtime);
            LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
                new OllamaBenchmarkRunner(transport, candidate.Probe, new FixedStepClock())
                    .RunAsync(plan, Capability(plan, runtime)));
            Assert.Equal(candidate.Code, exception.Code);
            Assert.Equal(2, transport.CallCount);
        }

        SequenceVramProbe timeoutProbe = new(async (index, currentRuntime, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        FakeTransport timeoutTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkException timeout = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(timeoutTransport, timeoutProbe, new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));
        Assert.Equal("vram_read_timeout", timeout.Code);
        Assert.Equal(2, timeoutTransport.CallCount);

        SequenceVramProbe boundaryProbe = new(_ => plan.VramBudgetMiB * 0.85);
        OllamaBenchmarkRunResult boundary = await new OllamaBenchmarkRunner(
            SuccessTransport(plan, runtime), boundaryProbe, new FixedStepClock())
            .RunAsync(plan, Capability(plan, runtime));
        Assert.Equal(plan.VramBudgetMiB * 0.85, boundary.ObservedSampledPeakVramMiB);
        Assert.Equal(
            plan.WarmupRequestCount + plan.MeasuredRequestCount + 1,
            boundary.VramSampleCount);
        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, boundary.Evidence).IsAccepted);
    }

    [Fact]
    public async Task NeverCompletingTransport_PoisonsEndpointAndSecondRunMakesNoTransportCall()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(endpoint: "http://127.0.0.1:12010/", queueDepth: 2);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<LocalModelBenchmarkTransportResponse> stalledResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> stalledRequestEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int postCount = 0;
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(request.RequestUri, ValidTags(runtime));
            }
            if (Interlocked.Increment(ref postCount) == 1)
            {
                await Task.Delay(2, cancellationToken);
                return JsonResponse(request.RequestUri, ValidGenerate(runtime.RuntimeModelReference));
            }
            stalledRequestEntered.TrySetResult(true);
            return await stalledResponse.Task;
        });
        SequenceVramProbe probe = new(index =>
            index <= 2 ? 1000 : plan.VramBudgetMiB * 0.85 + 0.001);
        Task<OllamaBenchmarkRunResult> run = new OllamaBenchmarkRunner(
            transport,
            probe,
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        await stalledRequestEntered.Task;

        FakeTransport secondTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability secondCapability = Capability(plan, runtime);
        Task<OllamaBenchmarkRunResult> secondRun = new OllamaBenchmarkRunner(
            secondTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, secondCapability);

        Task completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(run, completed);
        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => run);
        Assert.Equal("vram_headroom_exceeded", exception.Code);
        Assert.False(stalledResponse.Task.IsCompleted);
        Assert.Equal(3, transport.CallCount);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));

        LocalModelBenchmarkException poisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            secondRun);
        Assert.Equal("endpoint_poisoned", poisoned.Code);
        Assert.False(secondCapability.IsConsumed);
        Assert.Equal(0, secondTransport.CallCount);

        FakeTransport futureTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability futureCapability = Capability(plan, runtime);
        LocalModelBenchmarkException futurePoisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(futureTransport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, futureCapability));
        Assert.Equal("endpoint_poisoned", futurePoisoned.Code);
        Assert.False(futureCapability.IsConsumed);
        Assert.Equal(0, futureTransport.CallCount);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));
    }

    [Fact]
    public async Task NeverCompletingTagsTransport_IsBoundedAndPoisonsQueuedAndFutureAdmissions()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(
            endpoint: "http://127.0.0.1:12012/",
            queueDepth: 2,
            requestTimeoutMilliseconds: 50);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<LocalModelBenchmarkTransportResponse> stalledTags =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> tagsEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport firstTransport = new(async (request, cancellationToken) =>
        {
            tagsEntered.TrySetResult(true);
            return await stalledTags.Task;
        });
        Task<OllamaBenchmarkRunResult> firstRun = new OllamaBenchmarkRunner(
            firstTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        await tagsEntered.Task;

        FakeTransport queuedTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability queuedCapability = Capability(plan, runtime);
        Task<OllamaBenchmarkRunResult> queuedRun = new OllamaBenchmarkRunner(
            queuedTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, queuedCapability);
        Task completed = await Task.WhenAny(firstRun, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(firstRun, completed);
        LocalModelBenchmarkException timeout = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => firstRun);
        Assert.Equal("request_timeout", timeout.Code);
        LocalModelBenchmarkException queuedPoisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => queuedRun);
        Assert.Equal("endpoint_poisoned", queuedPoisoned.Code);
        Assert.False(queuedCapability.IsConsumed);
        Assert.Equal(0, queuedTransport.CallCount);
        Assert.False(stalledTags.Task.IsCompleted);
        Assert.Equal(1, firstTransport.CallCount);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));

        FakeTransport futureTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability futureCapability = Capability(plan, runtime);
        LocalModelBenchmarkException futurePoisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(futureTransport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, futureCapability));
        Assert.Equal("endpoint_poisoned", futurePoisoned.Code);
        Assert.False(futureCapability.IsConsumed);
        Assert.Equal(0, futureTransport.CallCount);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));
    }

    [Fact]
    public async Task NeverCompletingBodyRead_IsBoundedDisposedAndPoisonsQueuedAdmission()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(
            endpoint: "http://127.0.0.1:12013/",
            queueDepth: 2,
            requestTimeoutMilliseconds: 50);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        NeverCompletingReadStream stalledBody = new();
        FakeTransport firstTransport = new(async (request, cancellationToken) =>
        {
            await Task.Yield();
            return new LocalModelBenchmarkTransportResponse(
                HttpStatusCode.OK,
                request.RequestUri,
                IPAddress.Loopback,
                "application/json",
                null,
                stalledBody);
        });
        Task<OllamaBenchmarkRunResult> firstRun = new OllamaBenchmarkRunner(
            firstTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        await stalledBody.ReadStarted;

        FakeTransport queuedTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability queuedCapability = Capability(plan, runtime);
        Task<OllamaBenchmarkRunResult> queuedRun = new OllamaBenchmarkRunner(
            queuedTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, queuedCapability);
        Task completed = await Task.WhenAny(firstRun, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(firstRun, completed);
        LocalModelBenchmarkException timeout = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => firstRun);
        Assert.Equal("request_timeout", timeout.Code);
        Assert.True(stalledBody.IsDisposed);
        LocalModelBenchmarkException queuedPoisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => queuedRun);
        Assert.Equal("endpoint_poisoned", queuedPoisoned.Code);
        Assert.False(queuedCapability.IsConsumed);
        Assert.Equal(0, queuedTransport.CallCount);
        Assert.Equal(1, firstTransport.CallCount);
        Assert.Equal(0, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));

        byte[] capturedReadStorage = Assert.IsType<byte[]>(stalledBody.CapturedArray);
        byte[] protectedRentedStorage = ArrayPool<byte>.Shared.Rent(capturedReadStorage.Length);
        try
        {
            Assert.NotSame(capturedReadStorage, protectedRentedStorage);
            protectedRentedStorage.AsSpan().Fill(0x5a);
            stalledBody.CompleteLateWrite(0xc3);
            await stalledBody.LateWriteCompleted;
            Assert.All(protectedRentedStorage, value => Assert.Equal((byte)0x5a, value));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedRentedStorage);
            ArrayPool<byte>.Shared.Return(protectedRentedStorage);
        }
    }

    [Fact]
    public async Task CancellationIgnoringTransport_QuiescingInsideDrainIsDisposedWithoutPoison()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(endpoint: "http://127.0.0.1:12011/");
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TrackingDisposable owner = new();
        int postCount = 0;
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get) return JsonResponse(request.RequestUri, ValidTags(runtime));
            if (Interlocked.Increment(ref postCount) == 1)
            {
                await Task.Delay(2, cancellationToken);
                return JsonResponse(request.RequestUri, ValidGenerate(runtime.RuntimeModelReference));
            }
            await Task.Delay(50, CancellationToken.None);
            byte[] body = Encoding.UTF8.GetBytes(ValidGenerate(runtime.RuntimeModelReference));
            return new LocalModelBenchmarkTransportResponse(
                HttpStatusCode.OK,
                request.RequestUri,
                IPAddress.Loopback,
                "application/json",
                body.Length,
                new MemoryStream(body, writable: false),
                owner: owner);
        });
        SequenceVramProbe probe = new(index =>
            index <= 2 ? 1000 : plan.VramBudgetMiB * 0.85 + 0.001);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, probe, new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal("vram_headroom_exceeded", exception.Code);
        Assert.True(owner.IsDisposed);
        FakeTransport nextTransport = SuccessTransport(plan, runtime);
        OllamaBenchmarkRunResult next = await new OllamaBenchmarkRunner(
            nextTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, next.Evidence).IsAccepted);
    }

    [Fact]
    public async Task ResponseCompletingJustAfterDrain_IsDisposedBySinglePoisonedEndpointObserver()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(endpoint: "http://127.0.0.1:12014/");
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<LocalModelBenchmarkTransportResponse> lateResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int postCount = 0;
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get) return JsonResponse(request.RequestUri, ValidTags(runtime));
            if (Interlocked.Increment(ref postCount) == 1)
            {
                await Task.Delay(2, cancellationToken);
                return JsonResponse(request.RequestUri, ValidGenerate(runtime.RuntimeModelReference));
            }
            return await lateResponse.Task;
        });
        SequenceVramProbe probe = new(index =>
            index <= 2 ? 1000 : plan.VramBudgetMiB * 0.85 + 0.001);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, probe, new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal("vram_headroom_exceeded", exception.Code);
        Assert.False(lateResponse.Task.IsCompleted);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));
        TrackingDisposable owner = new();
        byte[] body = "{}"u8.ToArray();
        lateResponse.TrySetResult(new LocalModelBenchmarkTransportResponse(
            HttpStatusCode.OK,
            new Uri(plan.Endpoint + "api/generate"),
            IPAddress.Loopback,
            "application/json",
            body.Length,
            new MemoryStream(body, writable: false),
            owner: owner));
        for (int attempt = 0; attempt < 100 && !owner.IsDisposed; attempt++)
        {
            await Task.Delay(10);
        }
        Assert.True(owner.IsDisposed);
        Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));

        FakeTransport futureTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability futureCapability = Capability(plan, runtime);
        LocalModelBenchmarkException poisoned = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(futureTransport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, futureCapability));
        Assert.Equal("endpoint_poisoned", poisoned.Code);
        Assert.False(futureCapability.IsConsumed);
        Assert.Equal(0, futureTransport.CallCount);
    }

    [Fact]
    public async Task ResponseCompletedBeforeObserverInstallation_BlockingDisposeCannotHoldAdmissionLocks()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(
            endpoint: "http://127.0.0.1:12015/",
            queueDepth: 2,
            requestTimeoutMilliseconds: 50,
            totalQueueWaitMilliseconds: 1000);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<LocalModelBenchmarkTransportResponse> lateResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> requestEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingTrackingDisposable owner = new();
        FakeTransport firstTransport = new(async (request, cancellationToken) =>
        {
            requestEntered.TrySetResult(true);
            return await lateResponse.Task;
        });
        int installationHookCalls = 0;
        OllamaBenchmarkRunner firstRunner = new(
            firstTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock(),
            () =>
            {
                Interlocked.Increment(ref installationHookCalls);
                byte[] body = "{}"u8.ToArray();
                Assert.True(lateResponse.TrySetResult(new LocalModelBenchmarkTransportResponse(
                    HttpStatusCode.OK,
                    new Uri(plan.Endpoint + "api/tags"),
                    IPAddress.Loopback,
                    "application/json",
                    body.Length,
                    new MemoryStream(body, writable: false),
                    owner: owner)));
            });
        Task<OllamaBenchmarkRunResult> firstRun =
            firstRunner.RunAsync(plan, Capability(plan, runtime));
        await requestEntered.Task;

        FakeTransport queuedTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability queuedCapability = Capability(plan, runtime);
        Task<OllamaBenchmarkRunResult> queuedRun = new OllamaBenchmarkRunner(
            queuedTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, queuedCapability);

        try
        {
            await owner.DisposeEntered.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(owner.DisposeCompleted.IsCompleted);

            Assert.Same(firstRun, await Task.WhenAny(firstRun, Task.Delay(TimeSpan.FromSeconds(1))));
            Assert.Same(queuedRun, await Task.WhenAny(queuedRun, Task.Delay(TimeSpan.FromSeconds(1))));
            LocalModelBenchmarkException firstFailure =
                await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => firstRun);
            Assert.Equal("request_timeout", firstFailure.Code);
            LocalModelBenchmarkException queuedFailure =
                await Assert.ThrowsAsync<LocalModelBenchmarkException>(() => queuedRun);
            Assert.Equal("endpoint_poisoned", queuedFailure.Code);
            Assert.False(queuedCapability.IsConsumed);
            Assert.Equal(0, queuedTransport.CallCount);
            Assert.Equal(1, installationHookCalls);
            Assert.Equal(1, OllamaEndpointAdmissionRegistry.RetainedLateResponseObserverCount(plan.Endpoint));

            FakeTransport futureTransport = SuccessTransport(plan, runtime);
            LocalModelBenchmarkExecutionCapability futureCapability = Capability(plan, runtime);
            LocalModelBenchmarkException futureFailure =
                await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
                    new OllamaBenchmarkRunner(
                        futureTransport,
                        new SequenceVramProbe(_ => 1000),
                        new FixedStepClock()).RunAsync(plan, futureCapability));
            Assert.Equal("endpoint_poisoned", futureFailure.Code);
            Assert.False(futureCapability.IsConsumed);
            Assert.Equal(0, futureTransport.CallCount);
        }
        finally
        {
            owner.ReleaseDispose();
        }

        await owner.DisposeCompleted.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(owner.IsDisposed);
        Assert.Equal(1, owner.DisposeCount);
        Assert.Equal(1, owner.MaximumConcurrentDisposals);
        Assert.Equal(0, owner.ActiveDisposals);
    }

    [Fact]
    public async Task ProcessWideAdmission_AcrossRunnerInstances_SaturatesThenMeasuresStableWait()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(
            endpoint: "http://127.0.0.1:12020/",
            queueDepth: 2,
            totalQueueWaitMilliseconds: 1000);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<bool> releaseFirstTags = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> firstTagsEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport firstTransport = SuccessTransport(plan, runtime, async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                firstTagsEntered.TrySetResult(true);
                await releaseFirstTags.Task.WaitAsync(cancellationToken);
            }
        });
        FakeTransport secondTransport = SuccessTransport(plan, runtime);
        FakeTransport rejectedTransport = SuccessTransport(plan, runtime);
        FixedStepClock clock = new();
        Task<OllamaBenchmarkRunResult> first = new OllamaBenchmarkRunner(
            firstTransport, new SequenceVramProbe(_ => 1000), clock)
            .RunAsync(plan, Capability(plan, runtime));
        await firstTagsEntered.Task;

        Task<OllamaBenchmarkRunResult> second = new OllamaBenchmarkRunner(
            secondTransport, new SequenceVramProbe(_ => 1000), clock)
            .RunAsync(plan, Capability(plan, runtime));
        LocalModelBenchmarkExecutionCapability rejectedCapability = Capability(plan, runtime);
        LocalModelBenchmarkException saturated = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(rejectedTransport, new SequenceVramProbe(_ => 1000), clock)
                .RunAsync(plan, rejectedCapability));
        Assert.Equal("queue_saturated", saturated.Code);
        Assert.False(rejectedCapability.IsConsumed);
        Assert.Equal(0, rejectedTransport.CallCount);

        releaseFirstTags.TrySetResult(true);
        OllamaBenchmarkRunResult[] results = await Task.WhenAll(first, second);
        Assert.Equal(2, results[0].Evidence.PeakQueueDepth);
        Assert.Equal(2, results[1].Evidence.PeakQueueDepth);
        Assert.Equal(10, results[1].Evidence.TotalQueueWaitMilliseconds);
        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, results[1].Evidence).IsAccepted);
    }

    [Fact]
    public async Task ProcessWideAdmission_TimeoutAndCancellationMakeNoTransportCallAndDoNotConsumeCapability()
    {
        LocalModelBenchmarkPlan plan = ValidPlan(
            endpoint: "http://127.0.0.1:12021/",
            queueDepth: 2,
            totalQueueWaitMilliseconds: 40);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport holderTransport = SuccessTransport(plan, runtime, async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }
        });
        Task<OllamaBenchmarkRunResult> holder = new OllamaBenchmarkRunner(
            holderTransport, new SequenceVramProbe(_ => 1000))
            .RunAsync(plan, Capability(plan, runtime));
        await entered.Task;

        FakeTransport timeoutTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability timeoutCapability = Capability(plan, runtime);
        LocalModelBenchmarkException timeout = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(timeoutTransport, new SequenceVramProbe(_ => 1000))
                .RunAsync(plan, timeoutCapability));
        Assert.Equal("queue_wait_timeout", timeout.Code);
        Assert.False(timeoutCapability.IsConsumed);
        Assert.Equal(0, timeoutTransport.CallCount);

        FakeTransport cancellationTransport = SuccessTransport(plan, runtime);
        LocalModelBenchmarkExecutionCapability cancellationCapability = Capability(plan, runtime);
        using CancellationTokenSource cancelled = new(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new OllamaBenchmarkRunner(cancellationTransport, new SequenceVramProbe(_ => 1000))
                .RunAsync(plan, cancellationCapability, cancelled.Token));
        Assert.False(cancellationCapability.IsConsumed);
        Assert.Equal(0, cancellationTransport.CallCount);

        release.TrySetResult(true);
        await holder;

        FakeTransport afterRetirementTransport = SuccessTransport(plan, runtime);
        OllamaBenchmarkRunResult afterRetirement = await new OllamaBenchmarkRunner(
            afterRetirementTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        Assert.Equal(2, afterRetirement.Evidence.PeakQueueDepth);
        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, afterRetirement.Evidence).IsAccepted);
    }

    [Fact]
    public async Task ProcessWideAdmission_MixedCapacityRejectsBeforeEnqueueAndRemainsFixedAfterRetirement()
    {
        const string endpoint = "http://127.0.0.1:12022/";
        LocalModelBenchmarkPlan plan = ValidPlan(endpoint: endpoint, queueDepth: 2);
        LocalModelBenchmarkPlan larger = ValidPlan(endpoint: endpoint, queueDepth: 3);
        LocalModelBenchmarkPlan smaller = ValidPlan(endpoint: endpoint, queueDepth: 1);
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTransport holderTransport = SuccessTransport(plan, runtime, async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }
        });
        Task<OllamaBenchmarkRunResult> holder = new OllamaBenchmarkRunner(
            holderTransport,
            new SequenceVramProbe(_ => 1000),
            new FixedStepClock()).RunAsync(plan, Capability(plan, runtime));
        await entered.Task;

        FakeTransport largerTransport = SuccessTransport(larger, runtime);
        LocalModelBenchmarkExecutionCapability largerCapability = Capability(larger, runtime);
        LocalModelBenchmarkException activeMismatch = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(largerTransport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(larger, largerCapability));
        Assert.Equal("queue_capacity_mismatch", activeMismatch.Code);
        Assert.False(largerCapability.IsConsumed);
        Assert.Equal(0, largerTransport.CallCount);

        release.TrySetResult(true);
        OllamaBenchmarkRunResult holderResult = await holder;
        Assert.Equal(1, holderResult.Evidence.PeakQueueDepth);

        FakeTransport smallerTransport = SuccessTransport(smaller, runtime);
        LocalModelBenchmarkExecutionCapability smallerCapability = Capability(smaller, runtime);
        LocalModelBenchmarkException laterMismatch = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(smallerTransport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(smaller, smallerCapability));
        Assert.Equal("queue_capacity_mismatch", laterMismatch.Code);
        Assert.False(smallerCapability.IsConsumed);
        Assert.Equal(0, smallerTransport.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task HttpFailureStatus_IsRejectedWithoutRetry(HttpStatusCode statusCode)
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            await Task.Yield();
            byte[] body = "{}"u8.ToArray();
            return new LocalModelBenchmarkTransportResponse(
                statusCode,
                request.RequestUri,
                IPAddress.Loopback,
                "application/json",
                body.Length,
                new MemoryStream(body, writable: false));
        });

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal("http_status_rejected", exception.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task OversizedTagsBody_IsRejectedBeforeReadAndResponseOwnerIsDisposed()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        OllamaRuntimeAuthorization runtime = ValidRuntime();
        TrackingDisposable owner = new();
        FakeTransport transport = new(async (request, cancellationToken) =>
        {
            await Task.Yield();
            return new LocalModelBenchmarkTransportResponse(
                HttpStatusCode.OK,
                request.RequestUri,
                IPAddress.Loopback,
                "application/json",
                plan.Budgets.OutputBytes + 1,
                new MemoryStream("{}"u8.ToArray(), writable: false),
                owner: owner);
        });

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            new OllamaBenchmarkRunner(transport, new SequenceVramProbe(_ => 1000), new FixedStepClock())
                .RunAsync(plan, Capability(plan, runtime)));

        Assert.Equal("response_byte_budget_exceeded", exception.Code);
        Assert.True(owner.IsDisposed);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData("http://localhost:11434/")]
    [InlineData("http://0.0.0.0:11434/")]
    [InlineData("http://127.0.0.1:11434/api/")]
    [InlineData("https://127.0.0.1:11434/")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://127.0.0.1:11434/?x=1")]
    public void ProductionTransport_RejectsNoncanonicalOrigins(string endpoint) =>
        Assert.Throws<ArgumentException>(() => new OllamaLoopbackHttpTransport(endpoint));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionTransport_UsesExactLoopbackPeerAndHttpRequests_WithoutCredentialsProxyOrRedirect(bool ipv6)
    {
        if (ipv6 && !Socket.OSSupportsIPv6) return;
        IPAddress address = ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
        await using LoopbackTestServer server = await LoopbackTestServer.StartAsync(address, request =>
        {
            string json = request.Path == "/api/tags" ? "{\"models\":[]}" : "{}";
            return new ServerReply(200, json);
        });
        await using OllamaLoopbackHttpTransport transport = new(server.Endpoint);
        Uri origin = new(server.Endpoint);

        await using (LocalModelBenchmarkTransportResponse tags = await transport.SendAsync(
            new LocalModelBenchmarkTransportRequest(HttpMethod.Get, new Uri(origin, "api/tags"), Array.Empty<byte>()),
            CancellationToken.None))
        {
            Assert.Equal(address, tags.RemoteAddress);
            Assert.Equal("{\"models\":[]}", await new StreamReader(tags.Body, Encoding.UTF8).ReadToEndAsync());
        }
        await using (LocalModelBenchmarkTransportResponse generate = await transport.SendAsync(
            new LocalModelBenchmarkTransportRequest(HttpMethod.Post, new Uri(origin, "api/generate"), "{}"u8.ToArray()),
            CancellationToken.None))
        {
            Assert.Equal(address, generate.RemoteAddress);
            Assert.Equal("{}", await new StreamReader(generate.Body, Encoding.UTF8).ReadToEndAsync());
        }

        await server.WaitForRequestsAsync(2);
        Assert.Collection(server.Requests,
            tags =>
            {
                Assert.Equal("GET", tags.Method);
                Assert.Equal("/api/tags", tags.Path);
                Assert.Equal("HTTP/1.1", tags.HttpVersion);
                Assert.Equal(new Uri(server.Endpoint).Authority, tags.Headers["host"]);
                Assert.DoesNotContain("content-type", tags.Headers.Keys);
                Assert.Empty(tags.Body);
                AssertNoSensitiveHeaders(tags);
            },
            generate =>
            {
                Assert.Equal("POST", generate.Method);
                Assert.Equal("/api/generate", generate.Path);
                Assert.Equal("HTTP/1.1", generate.HttpVersion);
                Assert.Equal(new Uri(server.Endpoint).Authority, generate.Headers["host"]);
                Assert.Equal("{}", Encoding.UTF8.GetString(generate.Body));
                Assert.Equal("application/json; charset=utf-8", generate.Headers["content-type"]);
                AssertNoSensitiveHeaders(generate);
            });
    }

    [Fact]
    public async Task ProductionTransport_RejectsWrongMethodPathAndBodyBeforeNetwork()
    {
        await using LoopbackTestServer server = await LoopbackTestServer.StartAsync(
            IPAddress.Loopback,
            request => new ServerReply(200, "{}"));
        await using OllamaLoopbackHttpTransport transport = new(server.Endpoint);
        Uri origin = new(server.Endpoint);

        LocalModelBenchmarkException wrongPath = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            transport.SendAsync(
                new LocalModelBenchmarkTransportRequest(HttpMethod.Get, new Uri(origin, "api/generate"), Array.Empty<byte>()),
                CancellationToken.None).AsTask());
        LocalModelBenchmarkException getWithBody = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            transport.SendAsync(
                new LocalModelBenchmarkTransportRequest(HttpMethod.Get, new Uri(origin, "api/tags"), "{}"u8.ToArray()),
                CancellationToken.None).AsTask());
        LocalModelBenchmarkException postWithoutBody = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            transport.SendAsync(
                new LocalModelBenchmarkTransportRequest(HttpMethod.Post, new Uri(origin, "api/generate"), Array.Empty<byte>()),
                CancellationToken.None).AsTask());

        Assert.Equal("transport_request_not_exact", wrongPath.Code);
        Assert.Equal("transport_request_not_exact", getWithBody.Code);
        Assert.Equal("transport_request_not_exact", postWithoutBody.Code);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task ProductionTransport_DoesNotFollowRedirectAndFailsAfterDisposal()
    {
        await using LoopbackTestServer redirectTarget = await LoopbackTestServer.StartAsync(
            IPAddress.Loopback,
            request => new ServerReply(200, "{\"models\":[]}"));
        await using LoopbackTestServer originServer = await LoopbackTestServer.StartAsync(
            IPAddress.Loopback,
            request => new ServerReply(302, "{}", redirectTarget.Endpoint + "api/tags"));
        OllamaLoopbackHttpTransport transport = new(originServer.Endpoint);
        Uri tagsUri = new(new Uri(originServer.Endpoint), "api/tags");

        await using (LocalModelBenchmarkTransportResponse response = await transport.SendAsync(
            new LocalModelBenchmarkTransportRequest(HttpMethod.Get, tagsUri, Array.Empty<byte>()),
            CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.NotNull(response.RedirectLocation);
            Assert.Equal(tagsUri, response.EffectiveRequestUri);
        }
        await originServer.WaitForRequestsAsync(1);
        Assert.Empty(redirectTarget.Requests);
        await transport.DisposeAsync();
        await Assert.ThrowsAnyAsync<ObjectDisposedException>(() => transport.SendAsync(
            new LocalModelBenchmarkTransportRequest(HttpMethod.Get, tagsUri, Array.Empty<byte>()),
            CancellationToken.None).AsTask());
    }

    private static void AssertNoSensitiveHeaders(ServerRequest request)
    {
        Assert.DoesNotContain("authorization", request.Headers.Keys);
        Assert.DoesNotContain("proxy-authorization", request.Headers.Keys);
        Assert.DoesNotContain("cookie", request.Headers.Keys);
    }

    private static LocalModelBenchmarkPlan ValidPlan(
        string endpoint = "http://127.0.0.1:11434/",
        int queueDepth = 1,
        int totalQueueWaitMilliseconds = 1000,
        int requestTimeoutMilliseconds = 1000)
    {
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(new LocalModelAdapterPreflightRequest
        {
            Endpoint = endpoint,
            SharedServerIdentity = OllamaBenchmarkRunner.SharedServerIdentity,
            AdapterIdentity = OllamaBenchmarkRunner.AdapterIdentity,
            PromptIdentity = OllamaBenchmarkRunner.PromptIdentity,
            ModelIdentity = "qwen3.5-4b",
            QuantizationIdentity = "q4_k_m",
            ContextWindowTokens = 4096,
            AuthenticationMode = "none",
            CredentialReferences = Array.Empty<string>(),
            Budgets = new LocalModelResourceBudgets
            {
                RequestBytes = 16 * 1024,
                OutputBytes = 8 * 1024,
                QueueDepth = queueDepth,
                RequestTimeoutMilliseconds = requestTimeoutMilliseconds,
                TotalQueueWaitMilliseconds = totalQueueWaitMilliseconds
            },
            Benchmark = new LocalModelBenchmarkRequirements
            {
                WarmupRequestCount = 1,
                MeasuredRequestCount = LocalModelAdapterPreflight.MinimumMeasuredRequestCount
            }
        });
        Assert.True(result.IsValid);
        return result.BenchmarkPlan!;
    }

    private static OllamaRuntimeAuthorization ValidRuntime() => new()
    {
        RuntimeModelReference = "qwen3.5:4b",
        ArtifactDigestSha256 = new string('a', 64),
        ArtifactSizeBytes = 3_400_000_000,
        ArtifactFormat = "gguf",
        ModelFamily = "qwen35",
        ParameterSize = "4.66B",
        QuantizationLevel = "Q4_K_M",
        OllamaProcessIdentity = "ollama-local-v1",
        OllamaProcessId = 4242
    };

    private static LocalModelBenchmarkExecutionCapability Capability(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime) =>
        LocalModelBenchmarkExecutionCapability.AuthorizeSingleUse(plan, runtime);

    private static FakeTransport SuccessTransport(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        Func<LocalModelBenchmarkTransportRequest, CancellationToken, Task>? before = null) =>
        new(async (request, cancellationToken) =>
        {
            if (before is not null) await before(request, cancellationToken);
            if (request.Method == HttpMethod.Post) await Task.Delay(2, cancellationToken);
            return request.Method == HttpMethod.Get
                ? JsonResponse(request.RequestUri, ValidTags(runtime))
                : JsonResponse(request.RequestUri, ValidGenerate(runtime.RuntimeModelReference));
        });

    private static LocalModelBenchmarkTransportResponse JsonResponse(Uri requestUri, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        IPAddress address = requestUri.Host.Contains(':', StringComparison.Ordinal)
            ? IPAddress.IPv6Loopback
            : IPAddress.Loopback;
        return new LocalModelBenchmarkTransportResponse(
            HttpStatusCode.OK,
            requestUri,
            address,
            "application/json",
            bytes.Length,
            new MemoryStream(bytes, writable: false));
    }

    private static string ValidTags(OllamaRuntimeAuthorization runtime) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["models"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["name"] = runtime.RuntimeModelReference,
                    ["model"] = runtime.RuntimeModelReference,
                    ["modified_at"] = "2026-08-16T12:00:00Z",
                    ["size"] = runtime.ArtifactSizeBytes,
                    ["digest"] = runtime.ArtifactDigestSha256,
                    ["details"] = new Dictionary<string, object?>
                    {
                        ["format"] = runtime.ArtifactFormat,
                        ["family"] = runtime.ModelFamily,
                        ["families"] = new[] { runtime.ModelFamily },
                        ["parameter_size"] = runtime.ParameterSize,
                        ["quantization_level"] = runtime.QuantizationLevel
                    }
                }
            }
        });

    private static string ValidGenerate(
        string runtimeModelReference,
        int evalCount = 20,
        int promptCount = 100,
        long totalDuration = 1_000_000_000,
        long loadDuration = 1_000_000,
        long promptDuration = 100_000_000,
        long evalDuration = 500_000_000,
        string doneReason = "stop") =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["model"] = runtimeModelReference,
            ["created_at"] = "2026-08-16T12:00:00Z",
            ["response"] = "{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":1}",
            ["done"] = true,
            ["done_reason"] = doneReason,
            ["context"] = new[] { 1, 2 },
            ["total_duration"] = totalDuration,
            ["load_duration"] = loadDuration,
            ["prompt_eval_count"] = promptCount,
            ["prompt_eval_duration"] = promptDuration,
            ["eval_count"] = evalCount,
            ["eval_duration"] = evalDuration
        });

    private sealed class FakeTransport : ILocalModelBenchmarkTransport
    {
        private readonly object _gate = new();
        private readonly Func<LocalModelBenchmarkTransportRequest, CancellationToken, Task<LocalModelBenchmarkTransportResponse>> _handler;
        private readonly List<LocalModelBenchmarkTransportRequest> _requests = new();

        internal FakeTransport(
            Func<LocalModelBenchmarkTransportRequest, CancellationToken, Task<LocalModelBenchmarkTransportResponse>> handler) =>
            _handler = handler;

        internal int CallCount
        {
            get
            {
                lock (_gate) return _requests.Count;
            }
        }

        internal IReadOnlyList<LocalModelBenchmarkTransportRequest> Requests
        {
            get
            {
                lock (_gate) return _requests.ToArray();
            }
        }

        public ValueTask<LocalModelBenchmarkTransportResponse> SendAsync(
            LocalModelBenchmarkTransportRequest request,
            CancellationToken cancellationToken)
        {
            LocalModelBenchmarkTransportRequest captured = new(
                request.Method,
                request.RequestUri,
                request.BodyUtf8.ToArray());
            lock (_gate) _requests.Add(captured);
            return new ValueTask<LocalModelBenchmarkTransportResponse>(_handler(request, cancellationToken));
        }
    }

    private sealed class SequenceVramProbe : ILocalModelBenchmarkVramProbe
    {
        private readonly Func<int, OllamaRuntimeAuthorization, CancellationToken, Task<double>> _read;
        private readonly string? _digestOverride;
        private readonly string? _processIdentityOverride;
        private readonly int? _processIdOverride;
        private int _readCount;

        internal SequenceVramProbe(
            Func<int, double> read,
            string? digestOverride = null,
            string? processIdentityOverride = null,
            int? processIdOverride = null)
            : this(
                (index, _, _) => Task.FromResult(read(index)),
                digestOverride,
                processIdentityOverride,
                processIdOverride)
        {
        }

        internal SequenceVramProbe(
            Func<int, OllamaRuntimeAuthorization, CancellationToken, Task<double>> read,
            string? digestOverride = null,
            string? processIdentityOverride = null,
            int? processIdOverride = null)
        {
            _read = read;
            _digestOverride = digestOverride;
            _processIdentityOverride = processIdentityOverride;
            _processIdOverride = processIdOverride;
        }

        public async ValueTask<LocalModelVramReading> ReadAsync(
            LocalModelBenchmarkPlan plan,
            OllamaRuntimeAuthorization runtime,
            CancellationToken cancellationToken)
        {
            int index = Interlocked.Increment(ref _readCount);
            double usedMiB = await _read(index, runtime, cancellationToken);
            return new LocalModelVramReading(
                plan.ContractId,
                plan.ModelIdentity,
                _digestOverride ?? runtime.ArtifactDigestSha256,
                _processIdentityOverride ?? runtime.OllamaProcessIdentity,
                _processIdOverride ?? runtime.OllamaProcessId,
                usedMiB);
        }
    }

    private sealed class FixedStepClock : ILocalModelBenchmarkClock
    {
        public long GetTimestamp() => 0;
        public double GetElapsedMilliseconds(long startTimestamp, long endTimestamp) => 10;
    }

    private sealed class TrackingDisposable : IDisposable
    {
        private int _disposed;
        internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    }

    private sealed class BlockingTrackingDisposable : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private readonly TaskCompletionSource<bool> _disposeEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _disposeCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeDisposals;
        private int _disposeCount;
        private int _disposed;
        private int _maximumConcurrentDisposals;

        internal Task DisposeEntered => _disposeEntered.Task;
        internal Task DisposeCompleted => _disposeCompleted.Task;
        internal int ActiveDisposals => Volatile.Read(ref _activeDisposals);
        internal int DisposeCount => Volatile.Read(ref _disposeCount);
        internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        internal int MaximumConcurrentDisposals => Volatile.Read(ref _maximumConcurrentDisposals);

        public void Dispose()
        {
            int active = Interlocked.Increment(ref _activeDisposals);
            Interlocked.Increment(ref _disposeCount);
            int observedMaximum = Volatile.Read(ref _maximumConcurrentDisposals);
            while (active > observedMaximum)
            {
                int prior = Interlocked.CompareExchange(
                    ref _maximumConcurrentDisposals,
                    active,
                    observedMaximum);
                if (prior == observedMaximum) break;
                observedMaximum = prior;
            }
            _disposeEntered.TrySetResult(true);
            try
            {
                _release.Wait();
                Interlocked.Exchange(ref _disposed, 1);
            }
            finally
            {
                Interlocked.Decrement(ref _activeDisposals);
                _disposeCompleted.TrySetResult(true);
            }
        }

        internal void ReleaseDispose() => _release.Set();
    }

    private sealed class NeverCompletingReadStream : Stream
    {
        private readonly TaskCompletionSource<int> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Memory<byte> _capturedBuffer;
        private int _disposed;

        internal Task ReadStarted => _readStarted.Task;
        internal Task LateWriteCompleted => _never.Task;
        internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        internal byte[]? CapturedArray =>
            MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)_capturedBuffer, out ArraySegment<byte> segment)
                ? segment.Array
                : null;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _capturedBuffer = buffer;
            _readStarted.TrySetResult(true);
            return new ValueTask<int>(_never.Task);
        }

        internal void CompleteLateWrite(byte value)
        {
            _capturedBuffer.Span[0] = value;
            _never.TrySetResult(1);
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Exchange(ref _disposed, 1);
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed record ServerReply(int StatusCode, string Body, string? RedirectLocation = null);

    private sealed record ServerRequest(
        string Method,
        string Path,
        string HttpVersion,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Body);

    private sealed class LoopbackTestServer : IAsyncDisposable
    {
        private const int MaximumHeaderBytes = 16 * 1024;
        private const int MaximumBodyBytes = 32 * 1024;
        private readonly object _gate = new();
        private readonly TcpListener _listener;
        private readonly Func<ServerRequest, ServerReply> _handler;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly SemaphoreSlim _requestArrived = new(0);
        private readonly List<ServerRequest> _requests = new();
        private readonly Task _acceptLoop;

        private LoopbackTestServer(IPAddress address, Func<ServerRequest, ServerReply> handler)
        {
            _handler = handler;
            _listener = new TcpListener(address, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Endpoint = address.AddressFamily == AddressFamily.InterNetwork
                ? $"http://127.0.0.1:{port}/"
                : $"http://[::1]:{port}/";
            _acceptLoop = AcceptLoopAsync();
        }

        internal string Endpoint { get; }

        internal IReadOnlyList<ServerRequest> Requests
        {
            get
            {
                lock (_gate) return _requests.ToArray();
            }
        }

        internal static Task<LoopbackTestServer> StartAsync(
            IPAddress address,
            Func<ServerRequest, ServerReply> handler) =>
            Task.FromResult(new LoopbackTestServer(address, handler));

        internal async Task WaitForRequestsAsync(int count)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (true)
            {
                lock (_gate)
                {
                    if (_requests.Count >= count) return;
                }
                await _requestArrived.WaitAsync(timeout.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
            {
            }
            catch (SocketException) when (_shutdown.IsCancellationRequested)
            {
            }
            _requestArrived.Dispose();
            _shutdown.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                using NetworkStream stream = client.GetStream();
                ServerRequest request = await ReadRequestAsync(stream, _shutdown.Token);
                lock (_gate) _requests.Add(request);
                _requestArrived.Release();
                ServerReply reply = _handler(request);
                await WriteReplyAsync(stream, reply, _shutdown.Token);
            }
        }

        private static async Task<ServerRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            List<byte> headerBytes = new();
            byte[] one = new byte[1];
            while (headerBytes.Count < MaximumHeaderBytes)
            {
                int read = await stream.ReadAsync(one.AsMemory(), cancellationToken);
                if (read == 0) throw new InvalidDataException("Unexpected end of HTTP headers.");
                headerBytes.Add(one[0]);
                int count = headerBytes.Count;
                if (count >= 4
                    && headerBytes[count - 4] == '\r'
                    && headerBytes[count - 3] == '\n'
                    && headerBytes[count - 2] == '\r'
                    && headerBytes[count - 1] == '\n')
                {
                    break;
                }
            }
            if (headerBytes.Count == MaximumHeaderBytes) throw new InvalidDataException("HTTP headers exceeded test bound.");

            string[] lines = Encoding.ASCII.GetString(headerBytes.ToArray())
                .Split("\r\n", StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length != 3) throw new InvalidDataException("Malformed HTTP request line.");
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines.Skip(1))
            {
                if (line.Length == 0) break;
                int separator = line.IndexOf(':');
                if (separator <= 0 || !headers.TryAdd(
                    line[..separator].Trim(),
                    line[(separator + 1)..].Trim()))
                {
                    throw new InvalidDataException("Malformed or duplicate HTTP header.");
                }
            }

            int contentLength = 0;
            if (headers.TryGetValue("content-length", out string? lengthText)
                && (!int.TryParse(lengthText, out contentLength)
                    || contentLength < 0
                    || contentLength > MaximumBodyBytes))
            {
                throw new InvalidDataException("Invalid HTTP content length.");
            }
            if (headers.ContainsKey("transfer-encoding")) throw new InvalidDataException("Unexpected chunked test request.");

            byte[] body = new byte[contentLength];
            int offset = 0;
            while (offset < body.Length)
            {
                int read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken);
                if (read == 0) throw new InvalidDataException("Unexpected end of HTTP body.");
                offset += read;
            }
            return new ServerRequest(requestLine[0], requestLine[1], requestLine[2], headers, body);
        }

        private static async Task WriteReplyAsync(
            Stream stream,
            ServerReply reply,
            CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(reply.Body);
            string reason = reply.StatusCode switch
            {
                200 => "OK",
                302 => "Found",
                _ => "Test Status"
            };
            StringBuilder header = new();
            header.Append("HTTP/1.1 ").Append(reply.StatusCode).Append(' ').Append(reason).Append("\r\n")
                .Append("Content-Type: application/json\r\n")
                .Append("Content-Length: ").Append(body.Length).Append("\r\n")
                .Append("Connection: close\r\n");
            if (reply.RedirectLocation is not null)
            {
                header.Append("Location: ").Append(reply.RedirectLocation).Append("\r\n");
            }
            header.Append("\r\n");
            byte[] headerUtf8 = Encoding.ASCII.GetBytes(header.ToString());
            await stream.WriteAsync(headerUtf8, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }

}
