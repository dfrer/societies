using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class LocalModelAdapterPreflightTests
{
    [Fact]
    public void ValidIpv4Json_ProducesDeterministicNonExecutableSingleServerPlan()
    {
        byte[] json = Encoding.UTF8.GetBytes(ValidJson("http://127.0.0.1:11434/"));

        LocalModelPreflightResult first = LocalModelAdapterPreflight.ValidateJson(json);
        LocalModelPreflightResult second = LocalModelAdapterPreflight.ValidateJson(json);

        Assert.True(first.IsValid);
        Assert.Empty(first.Errors);
        LocalModelBenchmarkPlan plan = Assert.IsType<LocalModelBenchmarkPlan>(first.BenchmarkPlan);
        Assert.Equal(plan, second.BenchmarkPlan);
        Assert.Equal(64, plan.ContractId.Length);
        Assert.All(plan.ContractId, character => Assert.True(char.IsAsciiHexDigit(character) && !char.IsUpper(character)));
        Assert.Equal("http://127.0.0.1:11434/", plan.Endpoint);
        Assert.Equal(11434, plan.Port);
        Assert.Equal("single_shared_server", plan.ServerTopology);
        Assert.Equal("untrusted_proposal_only", plan.ResponseAuthority);
        Assert.True(plan.DeterministicCommitValidationRequired);
        Assert.False(plan.FollowRedirects);
        Assert.False(plan.AutomaticRetries);
        Assert.False(plan.CredentialsPermitted);
        Assert.False(plan.EnvironmentReadsPermitted);
        Assert.False(plan.PaidCallsPermitted);
        Assert.False(plan.ExecutionAuthorized);
        Assert.Equal(8192, plan.VramBudgetMiB);
        Assert.Equal("societies_local_model_metrics_sample/v1", plan.MetricsSampleSchema);
        Assert.Equal("sha256", plan.MetricsSampleDigestAlgorithm);
        Assert.Equal("timing_size_outcome_only_no_prompt_response_or_provider_payload", plan.MetricsSampleContentPolicy);
        Assert.Equal("nearest_rank_ceiling", plan.LatencyPercentileMethod);
        Assert.Equal("sequence_order_ieee754_sum", plan.QueueWaitAggregationMethod);
        AssertNoIo(first.OfflineAudit);
    }

    [Fact]
    public void CanonicalIpv6LoopbackAndInclusivePortBounds_AreAccepted()
    {
        LocalModelPreflightResult ipv6 = LocalModelAdapterPreflight.Validate(ValidRequest("http://[::1]:11434/"));
        LocalModelPreflightResult lowerBound = LocalModelAdapterPreflight.Validate(ValidRequest("http://127.0.0.1:1024/"));
        LocalModelPreflightResult upperBound = LocalModelAdapterPreflight.Validate(ValidRequest("http://127.0.0.1:65535/"));

        Assert.True(ipv6.IsValid);
        Assert.Equal(11434, ipv6.BenchmarkPlan!.Port);
        Assert.True(lowerBound.IsValid);
        Assert.True(upperBound.IsValid);
    }

    [Theory]
    [InlineData("https://127.0.0.1:11434/")]
    [InlineData("http://localhost:11434/")]
    [InlineData("http://0.0.0.0:11434/")]
    [InlineData("http://[::]:11434/")]
    [InlineData("http://127.0.0.2:11434/")]
    [InlineData("http://192.168.1.10:11434/")]
    [InlineData("http://user@127.0.0.1:11434/")]
    [InlineData("http://127.0.0.1:11434/model")]
    [InlineData("http://127.0.0.1:11434/?model=x")]
    [InlineData("http://127.0.0.1:11434/#fragment")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("HTTP://127.0.0.1:11434/")]
    [InlineData("http://127.0.0.1:011434/")]
    [InlineData("http://[::ffff:127.0.0.1]:11434/")]
    [InlineData(" http://127.0.0.1:11434/")]
    public void AmbiguousOrNonCanonicalEndpoints_FailClosed(string endpoint)
    {
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(ValidRequest(endpoint));

        Assert.False(result.IsValid);
        Assert.Null(result.BenchmarkPlan);
        Assert.Contains(result.Errors, error => error.Code == "endpoint_not_canonical_loopback_http");
        AssertNoIo(result.OfflineAudit);
    }

    [Theory]
    [InlineData("http://127.0.0.1:0/")]
    [InlineData("http://127.0.0.1:1023/")]
    public void PrivilegedOrZeroPorts_FailTheExplicitPortBudget(string endpoint)
    {
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(ValidRequest(endpoint));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "endpoint_port_out_of_range");
    }

    [Fact]
    public void MissingNonCanonicalOrOversizedIdentities_FailWithoutEchoingInput()
    {
        string oversized = new('x', 65);
        LocalModelAdapterPreflightRequest request = ValidRequest() with
        {
            SharedServerIdentity = null,
            AdapterIdentity = "Adapter With Spaces",
            PromptIdentity = oversized,
            ModelIdentity = "model/provider",
            QuantizationIdentity = ""
        };

        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(request);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.StartsWith("identity_", error.Code));
        Assert.DoesNotContain(result.Errors, error => error.Field.Contains(oversized, StringComparison.Ordinal));
    }

    [Fact]
    public void AuthenticationOrCredentialReferences_AreAlwaysRejectedAndNeverReflected()
    {
        const string secretReference = "SUPER_SECRET_API_TOKEN";
        LocalModelAdapterPreflightRequest request = ValidRequest() with
        {
            AuthenticationMode = "bearer",
            CredentialReferences = new[] { secretReference }
        };

        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "authentication_must_be_none");
        Assert.Contains(result.Errors, error => error.Code == "credentials_forbidden");
        Assert.DoesNotContain(secretReference, string.Join('|', result.Errors.Select(error => $"{error.Field}:{error.Code}")), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingCredentialDeclaration_FailsClosedInsteadOfAssumingNone()
    {
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(ValidRequest() with { CredentialReferences = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "credential_references_required");
    }

    [Fact]
    public void EveryResourceBudget_IsPositiveAndCapped()
    {
        LocalModelAdapterPreflightRequest baseline = ValidRequest();
        LocalModelResourceBudgets budgets = baseline.Budgets!;
        LocalModelResourceBudgets[] invalid =
        {
            budgets with { RequestBytes = 0 },
            budgets with { RequestBytes = LocalModelAdapterPreflight.MaximumRequestBytes + 1 },
            budgets with { OutputBytes = 0 },
            budgets with { OutputBytes = LocalModelAdapterPreflight.MaximumOutputBytes + 1 },
            budgets with { QueueDepth = 0 },
            budgets with { QueueDepth = LocalModelAdapterPreflight.MaximumQueueDepth + 1 },
            budgets with { RequestTimeoutMilliseconds = 0 },
            budgets with { RequestTimeoutMilliseconds = LocalModelAdapterPreflight.MaximumRequestTimeoutMilliseconds + 1 },
            budgets with { TotalQueueWaitMilliseconds = 0 },
            budgets with { TotalQueueWaitMilliseconds = LocalModelAdapterPreflight.MaximumTotalQueueWaitMilliseconds + 1 }
        };

        Assert.All(invalid, candidate =>
        {
            LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(baseline with { Budgets = candidate });
            Assert.False(result.IsValid);
            Assert.Null(result.BenchmarkPlan);
            Assert.Contains(result.Errors, error => error.Field.StartsWith("budgets.", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void BenchmarkShapeAndContextAreExplicitlyBounded()
    {
        LocalModelAdapterPreflightRequest baseline = ValidRequest();
        LocalModelAdapterPreflightRequest[] invalid =
        {
            baseline with { ContextWindowTokens = 0 },
            baseline with { ContextWindowTokens = LocalModelAdapterPreflight.MaximumContextWindowTokens + 1 },
            baseline with { Benchmark = baseline.Benchmark! with { WarmupRequestCount = 0 } },
            baseline with { Benchmark = baseline.Benchmark! with { WarmupRequestCount = LocalModelAdapterPreflight.MaximumWarmupRequestCount + 1 } },
            baseline with { Benchmark = baseline.Benchmark! with { MeasuredRequestCount = LocalModelAdapterPreflight.MinimumMeasuredRequestCount - 1 } },
            baseline with { Benchmark = baseline.Benchmark! with { MeasuredRequestCount = LocalModelAdapterPreflight.MaximumMeasuredRequestCount + 1 } }
        };

        Assert.All(invalid, candidate => Assert.False(LocalModelAdapterPreflight.Validate(candidate).IsValid));
    }

    [Fact]
    public void MalformedUnknownNonFiniteAndTrailingJson_FailBeforeAPlanExists()
    {
        byte[][] invalid =
        {
            Encoding.UTF8.GetBytes("{"),
            Encoding.UTF8.GetBytes(ValidJson().Replace("4096", "NaN", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(ValidJson().Replace("\n}", ",\n  \"unexpected\": true\n}", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(ValidJson() + " trailing"),
            Encoding.UTF8.GetBytes(ValidJson().Replace("\"none\"", "\"none\",", StringComparison.Ordinal))
        };

        Assert.All(invalid, json =>
        {
            LocalModelPreflightResult result = LocalModelAdapterPreflight.ValidateJson(json);
            Assert.False(result.IsValid);
            Assert.Null(result.BenchmarkPlan);
            AssertNoIo(result.OfflineAudit);
        });
    }

    [Fact]
    public void DuplicateJsonProperties_AreRejectedBeforeLastValueCanWin()
    {
        string duplicate = ValidJson().Replace(
            "\"endpoint\": \"http://127.0.0.1:11434/\"",
            "\"endpoint\": \"http://127.0.0.1:11434/\",\n  \"endpoint\": \"http://127.0.0.1:11435/\"",
            StringComparison.Ordinal);

        LocalModelPreflightResult result = LocalModelAdapterPreflight.ValidateJson(Encoding.UTF8.GetBytes(duplicate));

        Assert.False(result.IsValid);
        Assert.Equal("json_duplicate_property", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void OversizedOrDeepJson_IsRejectedBeforeDeserialization()
    {
        byte[] oversized = new byte[LocalModelAdapterPreflight.MaximumJsonBytes + 1];
        string deep = "{\"x\":" + new string('[', LocalModelAdapterPreflight.MaximumJsonDepth + 1)
            + "0" + new string(']', LocalModelAdapterPreflight.MaximumJsonDepth + 1) + "}";

        LocalModelPreflightResult oversizedResult = LocalModelAdapterPreflight.ValidateJson(oversized);
        LocalModelPreflightResult deepResult = LocalModelAdapterPreflight.ValidateJson(Encoding.UTF8.GetBytes(deep));

        Assert.Equal("json_too_large", Assert.Single(oversizedResult.Errors).Code);
        Assert.Equal("json_too_deep", Assert.Single(deepResult.Errors).Code);
        Assert.Null(oversizedResult.BenchmarkPlan);
        Assert.Null(deepResult.BenchmarkPlan);
    }

    [Fact]
    public void PublicPreflightSurface_IsSynchronousValueOnlyAndReportsExactNoIoCounts()
    {
        MethodInfo[] methods = typeof(LocalModelAdapterPreflight).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(ValidRequest("http://127.0.0.1:65535/"));

        Assert.All(methods, method =>
        {
            Assert.False(typeof(Task).IsAssignableFrom(method.ReturnType));
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                parameter.ParameterType.Namespace?.StartsWith("System.Net", StringComparison.Ordinal) == true);
        });
        AssertNoIo(result.OfflineAudit);
        Assert.False(result.BenchmarkPlan!.ExecutionAuthorized);
    }

    [Fact]
    public void CoherentMeasuredEvidence_IsAcceptedButStillCarriesNoSimulationAuthority()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();

        LocalModelBenchmarkValidationResult result = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, ValidEvidence(plan));

        Assert.True(result.IsAccepted);
        Assert.Empty(result.Errors);
        Assert.Equal("untrusted_proposal_only", result.ResponseAuthority);
        Assert.True(result.DeterministicCommitValidationRequired);
    }

    [Fact]
    public void NonFiniteMetrics_AreRejected()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);
        LocalModelBenchmarkEvidence[] invalid =
        {
            baseline with { StaticVramMiB = double.NaN },
            baseline with { PeakVramMiB = double.PositiveInfinity },
            baseline with { P50LatencyMilliseconds = double.NaN },
            baseline with { P95LatencyMilliseconds = double.NegativeInfinity },
            baseline with { P99LatencyMilliseconds = double.PositiveInfinity },
            baseline with { MaximumRequestLatencyMilliseconds = double.NaN },
            baseline with { ThroughputTokensPerSecond = double.NaN },
            baseline with { PeakQueueWaitMilliseconds = double.PositiveInfinity },
            baseline with { TotalQueueWaitMilliseconds = double.PositiveInfinity }
        };

        Assert.All(invalid, evidence => Assert.False(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence).IsAccepted));
    }

    [Fact]
    public void IncoherentIdentitiesCountsPercentilesAndVramFailClosed()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);
        LocalModelBenchmarkEvidence[] invalid =
        {
            baseline with { ContractId = new string('0', 64) },
            baseline with { ModelIdentity = "other-model" },
            baseline with { QuantizationIdentity = "q8_0" },
            baseline with { ContextWindowTokens = plan.ContextWindowTokens / 2 },
            baseline with { WarmupRequestCount = plan.WarmupRequestCount - 1 },
            baseline with { MeasuredRequestCount = plan.MeasuredRequestCount - 1 },
            baseline with { MetricsSampleCount = plan.MeasuredRequestCount - 1 },
            baseline with { PeakVramMiB = baseline.StaticVramMiB - 1 },
            baseline with { P50LatencyMilliseconds = 0 },
            baseline with { P50LatencyMilliseconds = 50, P95LatencyMilliseconds = 40, P99LatencyMilliseconds = 60 },
            baseline with { P99LatencyMilliseconds = baseline.MaximumRequestLatencyMilliseconds + 1 },
            baseline with { PeakQueueWaitMilliseconds = baseline.TotalQueueWaitMilliseconds + 1 },
            baseline with { FailureCount = plan.MeasuredRequestCount + 1 },
            baseline with { QueueBound = plan.Budgets.QueueDepth - 1 }
        };

        Assert.All(invalid, evidence => Assert.False(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence).IsAccepted));
    }

    [Fact]
    public void VramLatencyIoQueueFailureAndFallbackBudgetsFailClosed()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);
        LocalModelBenchmarkEvidence[] invalid =
        {
            baseline with { PeakVramMiB = plan.VramBudgetMiB + 0.01 },
            baseline with { MaximumRequestLatencyMilliseconds = plan.Budgets.RequestTimeoutMilliseconds + 0.01 },
            baseline with { PeakRequestBytes = plan.Budgets.RequestBytes + 1 },
            baseline with { PeakOutputBytes = plan.Budgets.OutputBytes + 1 },
            baseline with { PeakQueueDepth = plan.Budgets.QueueDepth + 1 },
            baseline with { TotalQueueWaitMilliseconds = plan.Budgets.TotalQueueWaitMilliseconds + 0.01 },
            baseline with { FailureCount = 1 },
            baseline with { FallbackCount = 1 }
        };

        Assert.All(invalid, evidence => Assert.False(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence).IsAccepted));
    }

    [Fact]
    public void ExactHardBudgetBoundaries_AreAccepted()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelMetricsSample[] samples = ValidSamples(plan)
            .Select(sample => sample with
            {
                RequestLatencyMilliseconds = plan.Budgets.RequestTimeoutMilliseconds,
                QueueWaitMilliseconds = (double)plan.Budgets.TotalQueueWaitMilliseconds / plan.MeasuredRequestCount
            })
            .ToArray();
        LocalModelBenchmarkEvidence boundary = EvidenceForSamples(plan, samples);

        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, boundary).IsAccepted);
    }

    [Fact]
    public void ManySmallQueueWaits_CannotHideATotalBudgetOverrun()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelMetricsSample[] samples = ValidSamples(plan)
            .Select(sample => sample with
            {
                QueueWaitMilliseconds =
                    (double)plan.Budgets.TotalQueueWaitMilliseconds / plan.MeasuredRequestCount + 1
            })
            .ToArray();
        LocalModelBenchmarkEvidence evidence = EvidenceForSamples(plan, samples);

        LocalModelBenchmarkValidationResult result = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence);

        Assert.False(result.IsAccepted);
        Assert.True(evidence.PeakQueueWaitMilliseconds < plan.Budgets.TotalQueueWaitMilliseconds);
        Assert.Contains(result.Errors, error => error.Code == "total_queue_wait_budget_exceeded");
    }

    [Fact]
    public void OnePercentLatencyOutlier_CannotHideBehindAnInBudgetP99()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelMetricsSample[] samples = ValidSamples(plan)
            .Select((sample, index) => sample with
            {
                RequestLatencyMilliseconds = index == plan.MeasuredRequestCount - 1
                    ? plan.Budgets.RequestTimeoutMilliseconds + 1
                    : 10
            })
            .ToArray();
        LocalModelBenchmarkEvidence evidence = EvidenceForSamples(plan, samples);

        LocalModelBenchmarkValidationResult result = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence);

        Assert.False(result.IsAccepted);
        Assert.True(evidence.P99LatencyMilliseconds < plan.Budgets.RequestTimeoutMilliseconds);
        Assert.Contains(result.Errors, error => error.Code == "request_timeout_budget_exceeded");
    }

    [Fact]
    public void MetricsSampleDigestAndCount_AreCanonicalAndContainNoRawPayloadSurface()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);
        LocalModelMetricsSample[] changedSamples = ValidSamples(plan).ToArray();
        changedSamples[0] = changedSamples[0] with { RequestBytes = changedSamples[0].RequestBytes - 1 };
        byte[] changedCanonical = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(changedSamples);
        LocalModelBenchmarkEvidence[] invalid =
        {
            baseline with { MetricsSampleCount = plan.MeasuredRequestCount - 1 },
            baseline with { CanonicalMetricsSampleDigestSha256 = new string('a', 64) },
            baseline with { CanonicalMetricsSampleDigestSha256 = baseline.CanonicalMetricsSampleDigestSha256!.ToUpperInvariant() },
            baseline with { CanonicalMetricsSampleDigestSha256 = new string('0', 64) },
            baseline with { CanonicalMetricsSampleDigestSha256 = "abc" },
            baseline with { CanonicalMetricsSamplesUtf8 = changedCanonical },
            baseline with { CanonicalMetricsSamplesUtf8 = null }
        };

        Assert.All(invalid, evidence =>
            Assert.False(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence).IsAccepted));
        Assert.DoesNotContain(
            typeof(LocalModelBenchmarkEvidence).GetProperties(),
            property => new[] { "prompt", "response", "provider", "payload", "secret", "credential" }
                .Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MalformedOversizedDeepDuplicateNonFiniteAndRawPayloadSamples_AreRejected()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);
        string canonical = Encoding.UTF8.GetString(baseline.CanonicalMetricsSamplesUtf8!);
        string tooMany = $"{{\"schema_version\":\"{LocalModelAdapterPreflight.MetricsSampleSchema}\",\"samples\":["
            + string.Join(',', Enumerable.Repeat("{}", LocalModelAdapterPreflight.MaximumMeasuredRequestCount + 1))
            + "]}";
        byte[][] invalid =
        {
            Encoding.UTF8.GetBytes("{"),
            new byte[LocalModelAdapterPreflight.MaximumMetricsSampleJsonBytes + 1],
            Encoding.UTF8.GetBytes(tooMany),
            Encoding.UTF8.GetBytes(
                $"{{\"schema_version\":\"{LocalModelAdapterPreflight.MetricsSampleSchema}\",\"samples\":[{{\"sequence\":0,\"x\":[[[[[0]]]]]}}]}}"),
            Encoding.UTF8.GetBytes(canonical.Replace(
                "\"sequence\":0",
                "\"sequence\":0,\"sequence\":0",
                StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(canonical.Replace(
                "\"request_latency_milliseconds\":18.5",
                "\"request_latency_milliseconds\":NaN",
                StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(canonical.Replace(
                "\"request_latency_milliseconds\":18.5",
                "\"request_latency_milliseconds\":1e9999",
                StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(canonical.Replace(
                "\"outcome\":\"success\"",
                "\"outcome\":\"success\",\"prompt\":\"forbidden\"",
                StringComparison.Ordinal))
        };

        Assert.All(invalid, bytes =>
        {
            LocalModelBenchmarkValidationResult result = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(
                plan,
                baseline with
                {
                    CanonicalMetricsSamplesUtf8 = bytes,
                    CanonicalMetricsSampleDigestSha256 = Sha256(bytes)
                });
            Assert.False(result.IsAccepted);
        });
    }

    [Fact]
    public void CanonicalSampleBytesAndDigest_AreDeterministicAndSampleChangesAreVisible()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelMetricsSample[] firstSamples = ValidSamples(plan).ToArray();
        LocalModelMetricsSample[] secondSamples = ValidSamples(plan).ToArray();

        byte[] first = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(firstSamples);
        byte[] second = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(secondSamples);
        secondSamples[^1] = secondSamples[^1] with { QueueWaitMilliseconds = secondSamples[^1].QueueWaitMilliseconds + 1 };
        byte[] changed = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(secondSamples);

        Assert.Equal(first, second);
        Assert.Equal(Sha256(first), Sha256(second));
        Assert.NotEqual(first, changed);
        Assert.NotEqual(Sha256(first), Sha256(changed));
    }

    [Fact]
    public void CanonicalSampleCountBounds_AreEnforcedAndTheMaximumPlanRemainsBounded()
    {
        LocalModelAdapterPreflightRequest request = ValidRequest() with
        {
            Benchmark = ValidRequest().Benchmark! with
            {
                MeasuredRequestCount = LocalModelAdapterPreflight.MaximumMeasuredRequestCount
            }
        };
        LocalModelBenchmarkPlan plan = LocalModelAdapterPreflight.Validate(request).BenchmarkPlan!;
        LocalModelMetricsSample[] maximum = Enumerable.Range(0, plan.MeasuredRequestCount)
            .Select(index => new LocalModelMetricsSample
            {
                Sequence = index,
                RequestLatencyMilliseconds = 1,
                QueueWaitMilliseconds = 0,
                RequestBytes = 1,
                OutputBytes = 1,
                Outcome = LocalModelMetricsSampleOutcome.Success
            })
            .ToArray();

        LocalModelBenchmarkEvidence evidence = EvidenceForSamples(plan, maximum);

        Assert.InRange(evidence.CanonicalMetricsSamplesUtf8!.Length, 1, LocalModelAdapterPreflight.MaximumMetricsSampleJsonBytes);
        Assert.True(LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence).IsAccepted);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(maximum.Take(LocalModelAdapterPreflight.MinimumMeasuredRequestCount - 1).ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(maximum.Append(maximum[^1] with
            {
                Sequence = LocalModelAdapterPreflight.MaximumMeasuredRequestCount
            }).ToArray()));
    }

    [Fact]
    public void QueueAndLatencyAggregateOrdering_IsRequired()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkEvidence baseline = ValidEvidence(plan);

        LocalModelBenchmarkValidationResult queue = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(
            plan,
            baseline with { PeakQueueWaitMilliseconds = 11, TotalQueueWaitMilliseconds = 10 });
        LocalModelBenchmarkValidationResult latency = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(
            plan,
            baseline with { P99LatencyMilliseconds = 51, MaximumRequestLatencyMilliseconds = 50 });

        Assert.Contains(queue.Errors, error => error.Code == "peak_queue_wait_above_total");
        Assert.Contains(queue.Errors, error => error.Code == "queue_wait_aggregates_mismatch");
        Assert.Contains(latency.Errors, error => error.Code == "maximum_request_latency_below_p99");
        Assert.Contains(latency.Errors, error => error.Code == "latency_aggregates_mismatch");
    }

    [Fact]
    public void TamperedPlanCannotAuthorizeEvidence()
    {
        LocalModelBenchmarkPlan plan = ValidPlan();
        LocalModelBenchmarkPlan[] tampered =
        {
            plan with { ContractId = new string('f', 64) },
            plan with { FollowRedirects = true },
            plan with { AutomaticRetries = true },
            plan with { CredentialsPermitted = true },
            plan with { EnvironmentReadsPermitted = true },
            plan with { PaidCallsPermitted = true },
            plan with { ExecutionAuthorized = true },
            plan with { ResponseAuthority = "trusted_command" },
            plan with { VramBudgetMiB = 16 * 1024 },
            plan with { MetricsSampleSchema = "other/v1" },
            plan with { MetricsSampleDigestAlgorithm = "sha512" },
            plan with { MetricsSampleContentPolicy = "raw_payloads_allowed" },
            plan with { LatencyPercentileMethod = "interpolated" },
            plan with { QueueWaitAggregationMethod = "unordered" },
            plan with { Budgets = plan.Budgets with { RequestTimeoutMilliseconds = plan.Budgets.RequestTimeoutMilliseconds + 1 } },
            plan with { Budgets = plan.Budgets with { TotalQueueWaitMilliseconds = plan.Budgets.TotalQueueWaitMilliseconds + 1 } }
        };

        Assert.All(tampered, candidate =>
        {
            LocalModelBenchmarkValidationResult result = LocalModelAdapterPreflight.ValidateBenchmarkEvidence(candidate, ValidEvidence(candidate));
            Assert.False(result.IsAccepted);
            Assert.Equal("benchmark_plan_invalid", Assert.Single(result.Errors).Code);
        });
    }

    [Fact]
    public void HardBudgetChanges_AreBoundIntoDeterministicContractIds()
    {
        LocalModelAdapterPreflightRequest baseline = ValidRequest();
        LocalModelBenchmarkPlan first = LocalModelAdapterPreflight.Validate(baseline).BenchmarkPlan!;
        LocalModelBenchmarkPlan timeoutChanged = LocalModelAdapterPreflight.Validate(baseline with
        {
            Budgets = baseline.Budgets! with
            {
                RequestTimeoutMilliseconds = baseline.Budgets.RequestTimeoutMilliseconds + 1
            }
        }).BenchmarkPlan!;
        LocalModelBenchmarkPlan totalWaitChanged = LocalModelAdapterPreflight.Validate(baseline with
        {
            Budgets = baseline.Budgets! with
            {
                TotalQueueWaitMilliseconds = baseline.Budgets.TotalQueueWaitMilliseconds + 1
            }
        }).BenchmarkPlan!;

        Assert.NotEqual(first.ContractId, timeoutChanged.ContractId);
        Assert.NotEqual(first.ContractId, totalWaitChanged.ContractId);
        Assert.NotEqual(timeoutChanged.ContractId, totalWaitChanged.ContractId);
    }

    [Fact]
    public void InvalidInputErrorOrderIsDeterministic()
    {
        LocalModelAdapterPreflightRequest request = new();

        LocalModelPreflightResult first = LocalModelAdapterPreflight.Validate(request);
        LocalModelPreflightResult second = LocalModelAdapterPreflight.Validate(request);

        Assert.Equal(first.Errors.Select(error => (error.Field, error.Code)), second.Errors.Select(error => (error.Field, error.Code)));
        Assert.Null(first.BenchmarkPlan);
        AssertNoIo(first.OfflineAudit);
    }

    private static LocalModelAdapterPreflightRequest ValidRequest(string endpoint = "http://127.0.0.1:11434/") => new()
    {
        Endpoint = endpoint,
        SharedServerIdentity = "snow-globe-shared-server-v1",
        AdapterIdentity = "provider-neutral-chat-v1",
        PromptIdentity = "snow-globe-proposal-v1",
        ModelIdentity = "local-model-7b",
        QuantizationIdentity = "q4_k_m",
        ContextWindowTokens = 4096,
        AuthenticationMode = "none",
        CredentialReferences = Array.Empty<string>(),
        Budgets = new LocalModelResourceBudgets
        {
            RequestBytes = 64 * 1024,
            OutputBytes = 16 * 1024,
            QueueDepth = 4,
            RequestTimeoutMilliseconds = 30_000,
            TotalQueueWaitMilliseconds = 60_000
        },
        Benchmark = new LocalModelBenchmarkRequirements
        {
            WarmupRequestCount = 3,
            MeasuredRequestCount = 100
        }
    };

    private static LocalModelBenchmarkPlan ValidPlan()
    {
        LocalModelPreflightResult result = LocalModelAdapterPreflight.Validate(ValidRequest());
        Assert.True(result.IsValid);
        return result.BenchmarkPlan!;
    }

    private static LocalModelBenchmarkEvidence ValidEvidence(LocalModelBenchmarkPlan plan) =>
        EvidenceForSamples(plan, ValidSamples(plan));

    private static LocalModelBenchmarkEvidence EvidenceForSamples(
        LocalModelBenchmarkPlan plan,
        IReadOnlyList<LocalModelMetricsSample> samples)
    {
        byte[] canonical = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(samples);
        double[] latency = samples.Select(sample => sample.RequestLatencyMilliseconds).OrderBy(value => value).ToArray();
        return new LocalModelBenchmarkEvidence
        {
            ContractId = plan.ContractId,
            ModelIdentity = plan.ModelIdentity,
            QuantizationIdentity = plan.QuantizationIdentity,
            ContextWindowTokens = plan.ContextWindowTokens,
            StaticVramMiB = 5120,
            PeakVramMiB = 7168,
            WarmupRequestCount = plan.WarmupRequestCount,
            MeasuredRequestCount = plan.MeasuredRequestCount,
            MetricsSampleCount = samples.Count,
            CanonicalMetricsSampleDigestSha256 = Sha256(canonical),
            CanonicalMetricsSamplesUtf8 = canonical,
            P50LatencyMilliseconds = Percentile(latency, 0.50),
            P95LatencyMilliseconds = Percentile(latency, 0.95),
            P99LatencyMilliseconds = Percentile(latency, 0.99),
            MaximumRequestLatencyMilliseconds = latency[^1],
            ThroughputTokensPerSecond = 31.5,
            FailureCount = samples.Count(sample => sample.Outcome == LocalModelMetricsSampleOutcome.Failure),
            FallbackCount = samples.Count(sample => sample.Outcome == LocalModelMetricsSampleOutcome.Fallback),
            QueueBound = plan.Budgets.QueueDepth,
            PeakQueueDepth = plan.Budgets.QueueDepth,
            PeakRequestBytes = samples.Max(sample => sample.RequestBytes),
            PeakOutputBytes = samples.Max(sample => sample.OutputBytes),
            PeakQueueWaitMilliseconds = samples.Max(sample => sample.QueueWaitMilliseconds),
            TotalQueueWaitMilliseconds = samples.Sum(sample => sample.QueueWaitMilliseconds)
        };
    }

    private static IReadOnlyList<LocalModelMetricsSample> ValidSamples(LocalModelBenchmarkPlan plan) =>
        Enumerable.Range(0, plan.MeasuredRequestCount)
            .Select(index => new LocalModelMetricsSample
            {
                Sequence = index,
                RequestLatencyMilliseconds = index switch
                {
                    < 50 => 18.5,
                    < 95 => 37.25,
                    < 99 => 49.75,
                    _ => 52
                },
                QueueWaitMilliseconds = 12.5,
                RequestBytes = 32 * 1024,
                OutputBytes = 8 * 1024,
                Outcome = LocalModelMetricsSampleOutcome.Success
            })
            .ToArray();

    private static double Percentile(IReadOnlyList<double> sortedValues, double fraction) =>
        sortedValues[(int)Math.Ceiling(sortedValues.Count * fraction) - 1];

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string ValidJson(string endpoint = "http://127.0.0.1:11434/") => $$"""
        {
          "endpoint": "{{endpoint}}",
          "shared_server_identity": "snow-globe-shared-server-v1",
          "adapter_identity": "provider-neutral-chat-v1",
          "prompt_identity": "snow-globe-proposal-v1",
          "model_identity": "local-model-7b",
          "quantization_identity": "q4_k_m",
          "context_window_tokens": 4096,
          "authentication_mode": "none",
          "credential_references": [],
          "budgets": {
            "request_bytes": 65536,
            "output_bytes": 16384,
            "queue_depth": 4,
            "request_timeout_milliseconds": 30000,
            "total_queue_wait_milliseconds": 60000
          },
          "benchmark": {
            "warmup_request_count": 3,
            "measured_request_count": 100
          }
        }
        """;

    private static void AssertNoIo(LocalModelOfflineAudit audit)
    {
        Assert.Equal(new LocalModelOfflineAudit(0, 0, 0, 0, 0, 0, 0), audit);
    }
}
