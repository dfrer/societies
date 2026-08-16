using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Societies.SnowGlobe;

public sealed record LocalModelAdapterPreflightRequest
{
    public string? Endpoint { get; init; }
    public string? SharedServerIdentity { get; init; }
    public string? AdapterIdentity { get; init; }
    public string? PromptIdentity { get; init; }
    public string? ModelIdentity { get; init; }
    public string? QuantizationIdentity { get; init; }
    public int ContextWindowTokens { get; init; }
    public string? AuthenticationMode { get; init; }
    public IReadOnlyList<string>? CredentialReferences { get; init; }
    public LocalModelResourceBudgets? Budgets { get; init; }
    public LocalModelBenchmarkRequirements? Benchmark { get; init; }
}

public sealed record LocalModelResourceBudgets
{
    public int RequestBytes { get; init; }
    public int OutputBytes { get; init; }
    public int QueueDepth { get; init; }
    public int RequestTimeoutMilliseconds { get; init; }
    public int TotalQueueWaitMilliseconds { get; init; }
}

public sealed record LocalModelBenchmarkRequirements
{
    public int WarmupRequestCount { get; init; }
    public int MeasuredRequestCount { get; init; }
}

public enum LocalModelMetricsSampleOutcome
{
    Success,
    Failure,
    Fallback
}

/// <summary>
/// Metrics-only evidence. It deliberately has no prompt, response, provider payload, credential,
/// model output, or arbitrary metadata field.
/// </summary>
public sealed record LocalModelMetricsSample
{
    public int Sequence { get; init; }
    public double RequestLatencyMilliseconds { get; init; }
    public double QueueWaitMilliseconds { get; init; }
    public int RequestBytes { get; init; }
    public int OutputBytes { get; init; }
    public LocalModelMetricsSampleOutcome Outcome { get; init; }
}

public sealed record LocalModelMetricsSampleEnvelope
{
    public string? SchemaVersion { get; init; }
    public IReadOnlyList<LocalModelMetricsSample>? Samples { get; init; }
}

public sealed record LocalModelPreflightError(string Field, string Code);

/// <summary>
/// Auditable declaration of the preflight's exact side-effect behavior. The implementation only
/// validates caller-owned values and bytes; it has no transport, model, GPU, file, or environment dependency.
/// </summary>
public sealed record LocalModelOfflineAudit(
    int SocketsOpened,
    int DownloadsAttempted,
    int ModelInvocations,
    int GpuProbes,
    int FileReads,
    int FileWrites,
    int EnvironmentReads);

public sealed record LocalModelBenchmarkPlan(
    string SchemaVersion,
    string ContractId,
    string Endpoint,
    int Port,
    string SharedServerIdentity,
    string AdapterIdentity,
    string PromptIdentity,
    string ModelIdentity,
    string QuantizationIdentity,
    int ContextWindowTokens,
    LocalModelResourceBudgets Budgets,
    int VramBudgetMiB,
    int WarmupRequestCount,
    int MeasuredRequestCount,
    string MetricsSampleSchema,
    string MetricsSampleDigestAlgorithm,
    string MetricsSampleContentPolicy,
    string LatencyPercentileMethod,
    string QueueWaitAggregationMethod,
    int MaximumFailureCount,
    int MaximumFallbackCount,
    string ServerTopology,
    string ResponseAuthority,
    bool DeterministicCommitValidationRequired,
    bool FollowRedirects,
    bool AutomaticRetries,
    bool CredentialsPermitted,
    bool EnvironmentReadsPermitted,
    bool PaidCallsPermitted,
    bool ExecutionAuthorized);

public sealed record LocalModelPreflightResult(
    bool IsValid,
    IReadOnlyList<LocalModelPreflightError> Errors,
    LocalModelBenchmarkPlan? BenchmarkPlan,
    LocalModelOfflineAudit OfflineAudit);

public sealed record LocalModelBenchmarkEvidence
{
    public string? ContractId { get; init; }
    public string? ModelIdentity { get; init; }
    public string? QuantizationIdentity { get; init; }
    public int ContextWindowTokens { get; init; }
    public double StaticVramMiB { get; init; }
    public double PeakVramMiB { get; init; }
    public int WarmupRequestCount { get; init; }
    public int MeasuredRequestCount { get; init; }
    public int MetricsSampleCount { get; init; }
    public string? CanonicalMetricsSampleDigestSha256 { get; init; }
    public byte[]? CanonicalMetricsSamplesUtf8 { get; init; }
    public double P50LatencyMilliseconds { get; init; }
    public double P95LatencyMilliseconds { get; init; }
    public double P99LatencyMilliseconds { get; init; }
    public double MaximumRequestLatencyMilliseconds { get; init; }
    public double ThroughputTokensPerSecond { get; init; }
    public int FailureCount { get; init; }
    public int FallbackCount { get; init; }
    public int QueueBound { get; init; }
    public int PeakQueueDepth { get; init; }
    public int PeakRequestBytes { get; init; }
    public int PeakOutputBytes { get; init; }
    public double PeakQueueWaitMilliseconds { get; init; }
    public double TotalQueueWaitMilliseconds { get; init; }
}

public sealed record LocalModelBenchmarkValidationResult(
    bool IsAccepted,
    IReadOnlyList<LocalModelPreflightError> Errors,
    string ResponseAuthority,
    bool DeterministicCommitValidationRequired);

/// <summary>
/// Pure, offline preflight for a future provider-neutral adapter connected to one shared local model server.
/// This type deliberately cannot execute a plan. A model response remains an untrusted proposal and can
/// affect simulation state only through the existing deterministic validation/commit boundary.
/// </summary>
public static class LocalModelAdapterPreflight
{
    public const string SchemaVersion = "societies_local_model_benchmark/v1";
    public const string ResponseAuthority = "untrusted_proposal_only";
    public const string ServerTopology = "single_shared_server";
    public const string MetricsSampleSchema = "societies_local_model_metrics_sample/v1";
    public const string MetricsSampleDigestAlgorithm = "sha256";
    public const string MetricsSampleContentPolicy = "timing_size_outcome_only_no_prompt_response_or_provider_payload";
    public const string LatencyPercentileMethod = "nearest_rank_ceiling";
    public const string QueueWaitAggregationMethod = "sequence_order_ieee754_sum";
    public const int MaximumJsonBytes = 16 * 1024;
    public const int MaximumJsonDepth = 8;
    public const int MaximumMetricsSampleJsonBytes = 2 * 1024 * 1024;
    public const int MaximumMetricsSampleJsonDepth = 4;
    public const int MinimumPort = 1024;
    public const int MaximumPort = 65535;
    public const int MaximumRequestBytes = 1024 * 1024;
    public const int MaximumOutputBytes = 256 * 1024;
    public const int MaximumQueueDepth = 16;
    public const int MaximumRequestTimeoutMilliseconds = 120_000;
    public const int MaximumTotalQueueWaitMilliseconds = 300_000;
    public const int MaximumContextWindowTokens = 32_768;
    public const int MaximumWarmupRequestCount = 100;
    public const int MinimumMeasuredRequestCount = 10;
    public const int MaximumMeasuredRequestCount = 10_000;
    public const int VramBudgetMiB = 8 * 1024;

    private const int MaximumEndpointCharacters = 64;
    private const int MaximumIdentityCharacters = 64;
    private static readonly Regex CanonicalEndpointPattern = new(
        @"\Ahttp://(?:(?<ipv4>127\.0\.0\.1)|(?<ipv6>\[::1\])):(?<port>0|[1-9][0-9]{0,4})/\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex CanonicalIdentityPattern = new(
        @"\A[a-z0-9](?:[a-z0-9._-]{0,62}[a-z0-9])?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = MaximumJsonDepth,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonSerializerOptions MetricsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = MaximumMetricsSampleJsonDepth,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<LocalModelMetricsSampleOutcome>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) }
    };
    private static readonly LocalModelOfflineAudit NoIo = new(0, 0, 0, 0, 0, 0, 0);

    public static LocalModelPreflightResult ValidateJson(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length == 0)
        {
            return Invalid("$", "json_required");
        }

        if (utf8Json.Length > MaximumJsonBytes)
        {
            return Invalid("$", "json_too_large");
        }

        string? structuralError = InspectJsonStructure(utf8Json);
        if (structuralError is not null)
        {
            return Invalid("$", structuralError);
        }

        try
        {
            LocalModelAdapterPreflightRequest? request = JsonSerializer.Deserialize<LocalModelAdapterPreflightRequest>(utf8Json, JsonOptions);
            return request is null ? Invalid("$", "json_invalid") : Validate(request);
        }
        catch (JsonException)
        {
            return Invalid("$", "json_invalid");
        }
        catch (NotSupportedException)
        {
            return Invalid("$", "json_invalid");
        }
    }

    public static LocalModelPreflightResult Validate(LocalModelAdapterPreflightRequest? request)
    {
        if (request is null)
        {
            return Invalid("$", "request_required");
        }

        List<LocalModelPreflightError> errors = new();
        int port = ValidateEndpoint(request.Endpoint, errors);
        ValidateIdentity(request.SharedServerIdentity, "shared_server_identity", errors);
        ValidateIdentity(request.AdapterIdentity, "adapter_identity", errors);
        ValidateIdentity(request.PromptIdentity, "prompt_identity", errors);
        ValidateIdentity(request.ModelIdentity, "model_identity", errors);
        ValidateIdentity(request.QuantizationIdentity, "quantization_identity", errors);

        if (request.ContextWindowTokens <= 0 || request.ContextWindowTokens > MaximumContextWindowTokens)
        {
            errors.Add(new("context_window_tokens", "context_window_tokens_out_of_range"));
        }

        if (!string.Equals(request.AuthenticationMode, "none", StringComparison.Ordinal))
        {
            errors.Add(new("authentication_mode", "authentication_must_be_none"));
        }

        if (request.CredentialReferences is null)
        {
            errors.Add(new("credential_references", "credential_references_required"));
        }
        else if (request.CredentialReferences.Count != 0)
        {
            errors.Add(new("credential_references", "credentials_forbidden"));
        }

        ValidateBudgets(request.Budgets, errors);
        ValidateBenchmarkRequirements(request.Benchmark, errors);
        if (errors.Count != 0)
        {
            return new(false, errors.AsReadOnly(), null, NoIo);
        }

        LocalModelResourceBudgets budgets = request.Budgets!;
        LocalModelBenchmarkRequirements benchmark = request.Benchmark!;
        string contractId = ComputeContractId(
            request.Endpoint!, port, request.SharedServerIdentity!, request.AdapterIdentity!, request.PromptIdentity!,
            request.ModelIdentity!, request.QuantizationIdentity!, request.ContextWindowTokens, budgets,
            benchmark.WarmupRequestCount, benchmark.MeasuredRequestCount);
        LocalModelBenchmarkPlan plan = new(
            SchemaVersion,
            contractId,
            request.Endpoint!,
            port,
            request.SharedServerIdentity!,
            request.AdapterIdentity!,
            request.PromptIdentity!,
            request.ModelIdentity!,
            request.QuantizationIdentity!,
            request.ContextWindowTokens,
            budgets,
            VramBudgetMiB,
            benchmark.WarmupRequestCount,
            benchmark.MeasuredRequestCount,
            MetricsSampleSchema,
            MetricsSampleDigestAlgorithm,
            MetricsSampleContentPolicy,
            LatencyPercentileMethod,
            QueueWaitAggregationMethod,
            MaximumFailureCount: 0,
            MaximumFallbackCount: 0,
            ServerTopology,
            ResponseAuthority,
            DeterministicCommitValidationRequired: true,
            FollowRedirects: false,
            AutomaticRetries: false,
            CredentialsPermitted: false,
            EnvironmentReadsPermitted: false,
            PaidCallsPermitted: false,
            ExecutionAuthorized: false);
        return new(true, Array.Empty<LocalModelPreflightError>(), plan, NoIo);
    }

    public static byte[] CreateCanonicalMetricsSamples(IReadOnlyList<LocalModelMetricsSample>? samples)
    {
        if (samples is null
            || samples.Count < MinimumMeasuredRequestCount
            || samples.Count > MaximumMeasuredRequestCount)
        {
            throw new ArgumentOutOfRangeException(nameof(samples), "Metrics samples must satisfy the measured-request count bounds.");
        }

        for (int index = 0; index < samples.Count; index++)
        {
            LocalModelMetricsSample? sample = samples[index];
            if (sample is null || sample.Sequence != index || !IsValidMetricsSample(sample))
            {
                throw new ArgumentException("Metrics samples must be contiguous, ordered, finite, metrics-only records.", nameof(samples));
            }
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", MetricsSampleSchema);
            writer.WritePropertyName("samples");
            writer.WriteStartArray();
            foreach (LocalModelMetricsSample sample in samples)
            {
                writer.WriteStartObject();
                writer.WriteNumber("sequence", sample.Sequence);
                writer.WriteNumber("request_latency_milliseconds", sample.RequestLatencyMilliseconds);
                writer.WriteNumber("queue_wait_milliseconds", sample.QueueWaitMilliseconds);
                writer.WriteNumber("request_bytes", sample.RequestBytes);
                writer.WriteNumber("output_bytes", sample.OutputBytes);
                writer.WriteString("outcome", OutcomeName(sample.Outcome));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (buffer.WrittenCount > MaximumMetricsSampleJsonBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(samples), "Canonical metrics samples exceed the byte bound.");
        }

        return buffer.WrittenSpan.ToArray();
    }

    public static LocalModelBenchmarkValidationResult ValidateBenchmarkEvidence(
        LocalModelBenchmarkPlan? plan,
        LocalModelBenchmarkEvidence? evidence)
    {
        List<LocalModelPreflightError> errors = new();
        if (!IsCoherentPlan(plan))
        {
            errors.Add(new("benchmark_plan", "benchmark_plan_invalid"));
            return BenchmarkInvalid(errors);
        }

        if (evidence is null)
        {
            errors.Add(new("benchmark_evidence", "benchmark_evidence_required"));
            return BenchmarkInvalid(errors);
        }

        LocalModelBenchmarkPlan validPlan = plan!;
        if (!string.Equals(evidence.ContractId, validPlan.ContractId, StringComparison.Ordinal))
        {
            errors.Add(new("contract_id", "contract_id_mismatch"));
        }

        if (!string.Equals(evidence.ModelIdentity, validPlan.ModelIdentity, StringComparison.Ordinal))
        {
            errors.Add(new("model_identity", "model_identity_mismatch"));
        }

        if (!string.Equals(evidence.QuantizationIdentity, validPlan.QuantizationIdentity, StringComparison.Ordinal))
        {
            errors.Add(new("quantization_identity", "quantization_identity_mismatch"));
        }

        if (evidence.ContextWindowTokens != validPlan.ContextWindowTokens)
        {
            errors.Add(new("context_window_tokens", "context_window_tokens_mismatch"));
        }

        if (!IsPositiveFinite(evidence.StaticVramMiB))
        {
            errors.Add(new("static_vram_mib", "static_vram_invalid"));
        }

        if (!IsPositiveFinite(evidence.PeakVramMiB))
        {
            errors.Add(new("peak_vram_mib", "peak_vram_invalid"));
        }
        else
        {
            if (IsPositiveFinite(evidence.StaticVramMiB) && evidence.PeakVramMiB < evidence.StaticVramMiB)
            {
                errors.Add(new("peak_vram_mib", "peak_vram_below_static"));
            }

            if (evidence.PeakVramMiB > validPlan.VramBudgetMiB)
            {
                errors.Add(new("peak_vram_mib", "vram_budget_exceeded"));
            }
        }

        if (evidence.WarmupRequestCount != validPlan.WarmupRequestCount)
        {
            errors.Add(new("warmup_request_count", "warmup_count_mismatch"));
        }

        if (evidence.MeasuredRequestCount != validPlan.MeasuredRequestCount)
        {
            errors.Add(new("measured_request_count", "measured_count_mismatch"));
        }

        IReadOnlyList<LocalModelMetricsSample>? samples = ValidateMetricsSamples(evidence, validPlan, errors);
        if (samples is not null)
        {
            ValidateDerivedMetrics(evidence, DeriveMetrics(samples), errors);
        }

        ValidateLatencyEvidence(evidence, validPlan, errors);
        if (!IsPositiveFinite(evidence.ThroughputTokensPerSecond))
        {
            errors.Add(new("throughput_tokens_per_second", "throughput_invalid"));
        }

        if (evidence.FailureCount < 0 || evidence.FallbackCount < 0
            || (long)evidence.FailureCount + evidence.FallbackCount > evidence.MeasuredRequestCount)
        {
            errors.Add(new("failure_fallback_counts", "failure_fallback_counts_incoherent"));
        }
        else
        {
            if (evidence.FailureCount > validPlan.MaximumFailureCount)
            {
                errors.Add(new("failure_count", "failure_budget_exceeded"));
            }

            if (evidence.FallbackCount > validPlan.MaximumFallbackCount)
            {
                errors.Add(new("fallback_count", "fallback_budget_exceeded"));
            }
        }

        if (evidence.QueueBound != validPlan.Budgets.QueueDepth)
        {
            errors.Add(new("queue_bound", "queue_bound_mismatch"));
        }

        if (evidence.PeakQueueDepth <= 0 || evidence.PeakQueueDepth > validPlan.Budgets.QueueDepth)
        {
            errors.Add(new("peak_queue_depth", "queue_depth_budget_exceeded"));
        }

        if (evidence.PeakRequestBytes <= 0 || evidence.PeakRequestBytes > validPlan.Budgets.RequestBytes)
        {
            errors.Add(new("peak_request_bytes", "request_byte_budget_exceeded"));
        }

        if (evidence.PeakOutputBytes <= 0 || evidence.PeakOutputBytes > validPlan.Budgets.OutputBytes)
        {
            errors.Add(new("peak_output_bytes", "output_byte_budget_exceeded"));
        }

        bool peakQueueWaitValid = IsNonNegativeFinite(evidence.PeakQueueWaitMilliseconds);
        bool totalQueueWaitValid = IsNonNegativeFinite(evidence.TotalQueueWaitMilliseconds);
        if (!peakQueueWaitValid)
        {
            errors.Add(new("peak_queue_wait_milliseconds", "peak_queue_wait_invalid"));
        }

        if (!totalQueueWaitValid)
        {
            errors.Add(new("total_queue_wait_milliseconds", "total_queue_wait_invalid"));
        }

        if (peakQueueWaitValid && totalQueueWaitValid
            && evidence.PeakQueueWaitMilliseconds > evidence.TotalQueueWaitMilliseconds)
        {
            errors.Add(new("queue_wait_milliseconds", "peak_queue_wait_above_total"));
        }

        if (totalQueueWaitValid
            && evidence.TotalQueueWaitMilliseconds > validPlan.Budgets.TotalQueueWaitMilliseconds)
        {
            errors.Add(new("total_queue_wait_milliseconds", "total_queue_wait_budget_exceeded"));
        }

        return new(errors.Count == 0, errors.AsReadOnly(), ResponseAuthority, true);
    }

    private static int ValidateEndpoint(string? endpoint, List<LocalModelPreflightError> errors)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            errors.Add(new("endpoint", "endpoint_required"));
            return 0;
        }

        if (endpoint.Length > MaximumEndpointCharacters)
        {
            errors.Add(new("endpoint", "endpoint_too_long"));
            return 0;
        }

        Match match = CanonicalEndpointPattern.Match(endpoint);
        if (!match.Success)
        {
            errors.Add(new("endpoint", "endpoint_not_canonical_loopback_http"));
            return 0;
        }

        if (!int.TryParse(match.Groups["port"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            || port < MinimumPort
            || port > MaximumPort)
        {
            errors.Add(new("endpoint", "endpoint_port_out_of_range"));
            return 0;
        }

        return port;
    }

    private static void ValidateIdentity(string? value, string field, List<LocalModelPreflightError> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(new(field, "identity_required"));
            return;
        }

        if (value.Length > MaximumIdentityCharacters || !CanonicalIdentityPattern.IsMatch(value))
        {
            errors.Add(new(field, "identity_not_canonical"));
        }
    }

    private static void ValidateBudgets(LocalModelResourceBudgets? budgets, List<LocalModelPreflightError> errors)
    {
        if (budgets is null)
        {
            errors.Add(new("budgets", "budgets_required"));
            return;
        }

        if (budgets.RequestBytes <= 0 || budgets.RequestBytes > MaximumRequestBytes)
        {
            errors.Add(new("budgets.request_bytes", "request_bytes_out_of_range"));
        }

        if (budgets.OutputBytes <= 0 || budgets.OutputBytes > MaximumOutputBytes)
        {
            errors.Add(new("budgets.output_bytes", "output_bytes_out_of_range"));
        }

        if (budgets.QueueDepth <= 0 || budgets.QueueDepth > MaximumQueueDepth)
        {
            errors.Add(new("budgets.queue_depth", "queue_depth_out_of_range"));
        }

        if (budgets.RequestTimeoutMilliseconds <= 0 || budgets.RequestTimeoutMilliseconds > MaximumRequestTimeoutMilliseconds)
        {
            errors.Add(new("budgets.request_timeout_milliseconds", "request_timeout_out_of_range"));
        }

        if (budgets.TotalQueueWaitMilliseconds <= 0 || budgets.TotalQueueWaitMilliseconds > MaximumTotalQueueWaitMilliseconds)
        {
            errors.Add(new("budgets.total_queue_wait_milliseconds", "total_queue_wait_out_of_range"));
        }
    }

    private static void ValidateBenchmarkRequirements(
        LocalModelBenchmarkRequirements? benchmark,
        List<LocalModelPreflightError> errors)
    {
        if (benchmark is null)
        {
            errors.Add(new("benchmark", "benchmark_requirements_required"));
            return;
        }

        if (benchmark.WarmupRequestCount <= 0 || benchmark.WarmupRequestCount > MaximumWarmupRequestCount)
        {
            errors.Add(new("benchmark.warmup_request_count", "warmup_count_out_of_range"));
        }

        if (benchmark.MeasuredRequestCount < MinimumMeasuredRequestCount
            || benchmark.MeasuredRequestCount > MaximumMeasuredRequestCount)
        {
            errors.Add(new("benchmark.measured_request_count", "measured_count_out_of_range"));
        }
    }

    private static void ValidateLatencyEvidence(
        LocalModelBenchmarkEvidence evidence,
        LocalModelBenchmarkPlan plan,
        List<LocalModelPreflightError> errors)
    {
        bool percentilesValid = IsPositiveFinite(evidence.P50LatencyMilliseconds)
            && IsPositiveFinite(evidence.P95LatencyMilliseconds)
            && IsPositiveFinite(evidence.P99LatencyMilliseconds);
        if (!percentilesValid)
        {
            errors.Add(new("latency_percentiles", "latency_percentiles_invalid"));
        }
        else if (evidence.P50LatencyMilliseconds > evidence.P95LatencyMilliseconds
            || evidence.P95LatencyMilliseconds > evidence.P99LatencyMilliseconds)
        {
            errors.Add(new("latency_percentiles", "latency_percentiles_incoherent"));
        }

        bool maximumLatencyValid = IsNonNegativeFinite(evidence.MaximumRequestLatencyMilliseconds);
        if (!maximumLatencyValid)
        {
            errors.Add(new("maximum_request_latency_milliseconds", "maximum_request_latency_invalid"));
        }

        if (percentilesValid && maximumLatencyValid
            && evidence.P99LatencyMilliseconds > evidence.MaximumRequestLatencyMilliseconds)
        {
            errors.Add(new("maximum_request_latency_milliseconds", "maximum_request_latency_below_p99"));
        }

        if (maximumLatencyValid
            && evidence.MaximumRequestLatencyMilliseconds > plan.Budgets.RequestTimeoutMilliseconds)
        {
            errors.Add(new("maximum_request_latency_milliseconds", "request_timeout_budget_exceeded"));
        }
    }

    private static IReadOnlyList<LocalModelMetricsSample>? ValidateMetricsSamples(
        LocalModelBenchmarkEvidence evidence,
        LocalModelBenchmarkPlan plan,
        List<LocalModelPreflightError> errors)
    {
        byte[]? suppliedUtf8 = evidence.CanonicalMetricsSamplesUtf8;
        if (suppliedUtf8 is null || suppliedUtf8.Length == 0)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_required"));
            return null;
        }

        if (suppliedUtf8.Length > MaximumMetricsSampleJsonBytes)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_too_large"));
            return null;
        }

        byte[] canonicalUtf8 = suppliedUtf8.ToArray();
        try
        {
            return ValidateMetricsSamplesSnapshot(canonicalUtf8, evidence, plan, errors);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalUtf8);
        }
    }

    private static IReadOnlyList<LocalModelMetricsSample>? ValidateMetricsSamplesSnapshot(
        byte[] canonicalUtf8,
        LocalModelBenchmarkEvidence evidence,
        LocalModelBenchmarkPlan plan,
        List<LocalModelPreflightError> errors)
    {
        string? structuralError = InspectJsonStructure(
            canonicalUtf8,
            MaximumMetricsSampleJsonDepth,
            countedObjectDepth: 2,
            maximumObjectCount: MaximumMeasuredRequestCount);
        if (structuralError is not null)
        {
            string code = structuralError switch
            {
                "json_too_deep" => "metrics_samples_too_deep",
                "json_duplicate_property" => "metrics_samples_duplicate_property",
                "json_too_many_objects" => "metrics_sample_count_out_of_range",
                _ => "metrics_samples_invalid"
            };
            errors.Add(new("canonical_metrics_samples_utf8", code));
            return null;
        }

        LocalModelMetricsSampleEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LocalModelMetricsSampleEnvelope>(canonicalUtf8, MetricsJsonOptions);
        }
        catch (JsonException)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_invalid"));
            return null;
        }
        catch (NotSupportedException)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_invalid"));
            return null;
        }

        if (envelope is null
            || !string.Equals(envelope.SchemaVersion, MetricsSampleSchema, StringComparison.Ordinal)
            || envelope.Samples is null)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_schema_invalid"));
            return null;
        }

        IReadOnlyList<LocalModelMetricsSample> samples = envelope.Samples;
        if (samples.Count < MinimumMeasuredRequestCount
            || samples.Count > MaximumMeasuredRequestCount
            || samples.Count != evidence.MetricsSampleCount
            || samples.Count != evidence.MeasuredRequestCount
            || samples.Count != plan.MeasuredRequestCount)
        {
            errors.Add(new("metrics_sample_count", "metrics_sample_count_mismatch"));
            return null;
        }

        for (int index = 0; index < samples.Count; index++)
        {
            LocalModelMetricsSample? sample = samples[index];
            if (sample is null || sample.Sequence != index || !IsValidMetricsSample(sample))
            {
                errors.Add(new("canonical_metrics_samples_utf8", "metrics_sample_content_invalid"));
                return null;
            }
        }

        byte[] canonical;
        try
        {
            canonical = CreateCanonicalMetricsSamples(samples);
        }
        catch (ArgumentException)
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_sample_content_invalid"));
            return null;
        }

        if (!canonicalUtf8.AsSpan().SequenceEqual(canonical))
        {
            errors.Add(new("canonical_metrics_samples_utf8", "metrics_samples_not_canonical"));
            return null;
        }

        string computedDigest = Sha256(canonicalUtf8);
        if (!string.Equals(evidence.CanonicalMetricsSampleDigestSha256, computedDigest, StringComparison.Ordinal))
        {
            errors.Add(new("canonical_metrics_sample_digest_sha256", "metrics_sample_digest_mismatch"));
            return null;
        }

        return samples;
    }

    private static void ValidateDerivedMetrics(
        LocalModelBenchmarkEvidence evidence,
        DerivedMetrics derived,
        List<LocalModelPreflightError> errors)
    {
        if (evidence.P50LatencyMilliseconds != derived.P50LatencyMilliseconds
            || evidence.P95LatencyMilliseconds != derived.P95LatencyMilliseconds
            || evidence.P99LatencyMilliseconds != derived.P99LatencyMilliseconds
            || evidence.MaximumRequestLatencyMilliseconds != derived.MaximumRequestLatencyMilliseconds)
        {
            errors.Add(new("latency_aggregates", "latency_aggregates_mismatch"));
        }

        if (evidence.PeakQueueWaitMilliseconds != derived.PeakQueueWaitMilliseconds
            || evidence.TotalQueueWaitMilliseconds != derived.TotalQueueWaitMilliseconds)
        {
            errors.Add(new("queue_wait_aggregates", "queue_wait_aggregates_mismatch"));
        }

        if (evidence.PeakRequestBytes != derived.PeakRequestBytes
            || evidence.PeakOutputBytes != derived.PeakOutputBytes)
        {
            errors.Add(new("byte_aggregates", "byte_aggregates_mismatch"));
        }

        if (evidence.FailureCount != derived.FailureCount
            || evidence.FallbackCount != derived.FallbackCount)
        {
            errors.Add(new("outcome_aggregates", "outcome_aggregates_mismatch"));
        }
    }

    private static DerivedMetrics DeriveMetrics(IReadOnlyList<LocalModelMetricsSample> samples)
    {
        double[] sortedLatency = samples
            .Select(sample => sample.RequestLatencyMilliseconds)
            .OrderBy(value => value)
            .ToArray();
        double totalQueueWait = 0;
        foreach (LocalModelMetricsSample sample in samples)
        {
            totalQueueWait += sample.QueueWaitMilliseconds;
        }

        return new DerivedMetrics(
            Percentile(sortedLatency, 0.50),
            Percentile(sortedLatency, 0.95),
            Percentile(sortedLatency, 0.99),
            sortedLatency[^1],
            samples.Max(sample => sample.QueueWaitMilliseconds),
            totalQueueWait,
            samples.Max(sample => sample.RequestBytes),
            samples.Max(sample => sample.OutputBytes),
            samples.Count(sample => sample.Outcome == LocalModelMetricsSampleOutcome.Failure),
            samples.Count(sample => sample.Outcome == LocalModelMetricsSampleOutcome.Fallback));
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double fraction)
    {
        int index = checked((int)Math.Ceiling(sortedValues.Count * fraction) - 1);
        return sortedValues[Math.Max(index, 0)];
    }

    private static bool IsValidMetricsSample(LocalModelMetricsSample sample) =>
        sample.Sequence >= 0
        && IsPositiveFinite(sample.RequestLatencyMilliseconds)
        && sample.RequestLatencyMilliseconds <= MaximumRequestTimeoutMilliseconds
        && IsNonNegativeFinite(sample.QueueWaitMilliseconds)
        && sample.QueueWaitMilliseconds <= MaximumTotalQueueWaitMilliseconds
        && sample.RequestBytes > 0
        && sample.RequestBytes <= MaximumRequestBytes
        && sample.OutputBytes > 0
        && sample.OutputBytes <= MaximumOutputBytes
        && Enum.IsDefined(sample.Outcome);

    private static string OutcomeName(LocalModelMetricsSampleOutcome outcome) => outcome switch
    {
        LocalModelMetricsSampleOutcome.Success => "success",
        LocalModelMetricsSampleOutcome.Failure => "failure",
        LocalModelMetricsSampleOutcome.Fallback => "fallback",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    private static bool IsCoherentPlan(LocalModelBenchmarkPlan? plan)
    {
        if (plan is null
            || !string.Equals(plan.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || plan.VramBudgetMiB != VramBudgetMiB
            || plan.MaximumFailureCount != 0
            || plan.MaximumFallbackCount != 0
            || !string.Equals(plan.MetricsSampleSchema, MetricsSampleSchema, StringComparison.Ordinal)
            || !string.Equals(plan.MetricsSampleDigestAlgorithm, MetricsSampleDigestAlgorithm, StringComparison.Ordinal)
            || !string.Equals(plan.MetricsSampleContentPolicy, MetricsSampleContentPolicy, StringComparison.Ordinal)
            || !string.Equals(plan.LatencyPercentileMethod, LatencyPercentileMethod, StringComparison.Ordinal)
            || !string.Equals(plan.QueueWaitAggregationMethod, QueueWaitAggregationMethod, StringComparison.Ordinal)
            || !string.Equals(plan.ServerTopology, ServerTopology, StringComparison.Ordinal)
            || !string.Equals(plan.ResponseAuthority, ResponseAuthority, StringComparison.Ordinal)
            || !plan.DeterministicCommitValidationRequired
            || plan.FollowRedirects
            || plan.AutomaticRetries
            || plan.CredentialsPermitted
            || plan.EnvironmentReadsPermitted
            || plan.PaidCallsPermitted
            || plan.ExecutionAuthorized)
        {
            return false;
        }

        List<LocalModelPreflightError> errors = new();
        int port = ValidateEndpoint(plan.Endpoint, errors);
        ValidateIdentity(plan.SharedServerIdentity, "shared_server_identity", errors);
        ValidateIdentity(plan.AdapterIdentity, "adapter_identity", errors);
        ValidateIdentity(plan.PromptIdentity, "prompt_identity", errors);
        ValidateIdentity(plan.ModelIdentity, "model_identity", errors);
        ValidateIdentity(plan.QuantizationIdentity, "quantization_identity", errors);
        ValidateBudgets(plan.Budgets, errors);
        ValidateBenchmarkRequirements(new LocalModelBenchmarkRequirements
        {
            WarmupRequestCount = plan.WarmupRequestCount,
            MeasuredRequestCount = plan.MeasuredRequestCount
        }, errors);
        if (plan.Port != port
            || plan.ContextWindowTokens <= 0
            || plan.ContextWindowTokens > MaximumContextWindowTokens
            || errors.Count != 0)
        {
            return false;
        }

        string expectedContractId = ComputeContractId(
            plan.Endpoint, plan.Port, plan.SharedServerIdentity, plan.AdapterIdentity, plan.PromptIdentity,
            plan.ModelIdentity, plan.QuantizationIdentity, plan.ContextWindowTokens, plan.Budgets,
            plan.WarmupRequestCount, plan.MeasuredRequestCount);
        return string.Equals(plan.ContractId, expectedContractId, StringComparison.Ordinal);
    }

    private static string ComputeContractId(
        string endpoint,
        int port,
        string sharedServerIdentity,
        string adapterIdentity,
        string promptIdentity,
        string modelIdentity,
        string quantizationIdentity,
        int contextWindowTokens,
        LocalModelResourceBudgets budgets,
        int warmupRequestCount,
        int measuredRequestCount)
    {
        string material = string.Join('\n', new[]
        {
            SchemaVersion,
            endpoint,
            port.ToString(CultureInfo.InvariantCulture),
            sharedServerIdentity,
            adapterIdentity,
            promptIdentity,
            modelIdentity,
            quantizationIdentity,
            contextWindowTokens.ToString(CultureInfo.InvariantCulture),
            budgets.RequestBytes.ToString(CultureInfo.InvariantCulture),
            budgets.OutputBytes.ToString(CultureInfo.InvariantCulture),
            budgets.QueueDepth.ToString(CultureInfo.InvariantCulture),
            budgets.RequestTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture),
            budgets.TotalQueueWaitMilliseconds.ToString(CultureInfo.InvariantCulture),
            VramBudgetMiB.ToString(CultureInfo.InvariantCulture),
            warmupRequestCount.ToString(CultureInfo.InvariantCulture),
            measuredRequestCount.ToString(CultureInfo.InvariantCulture),
            MetricsSampleSchema,
            MetricsSampleDigestAlgorithm,
            MetricsSampleContentPolicy,
            LatencyPercentileMethod,
            QueueWaitAggregationMethod,
            ServerTopology,
            ResponseAuthority,
            "redirects=false",
            "retries=false",
            "credentials=false",
            "environment=false",
            "paid_calls=false",
            "execution=false"
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string? InspectJsonStructure(
        ReadOnlySpan<byte> utf8Json,
        int maximumDepth = MaximumJsonDepth,
        int countedObjectDepth = -1,
        int maximumObjectCount = int.MaxValue)
    {
        Stack<HashSet<string>> objectProperties = new();
        int countedObjects = 0;
        try
        {
            Utf8JsonReader reader = new(utf8Json, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maximumDepth + 1
            });
            while (reader.Read())
            {
                if ((reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                    && reader.CurrentDepth >= maximumDepth)
                {
                    return "json_too_deep";
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        if (reader.CurrentDepth == countedObjectDepth && ++countedObjects > maximumObjectCount)
                        {
                            return "json_too_many_objects";
                        }

                        objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.PropertyName:
                        if (objectProperties.Count == 0 || !objectProperties.Peek().Add(reader.GetString()!))
                        {
                            return "json_duplicate_property";
                        }

                        break;
                    case JsonTokenType.EndObject:
                        objectProperties.Pop();
                        break;
                }
            }

            return objectProperties.Count == 0 ? null : "json_invalid";
        }
        catch (JsonException)
        {
            return "json_invalid";
        }
    }

    private static LocalModelPreflightResult Invalid(string field, string code) =>
        new(false, Array.AsReadOnly(new[] { new LocalModelPreflightError(field, code) }), null, NoIo);

    private static LocalModelBenchmarkValidationResult BenchmarkInvalid(List<LocalModelPreflightError> errors) =>
        new(false, new ReadOnlyCollection<LocalModelPreflightError>(errors), ResponseAuthority, true);

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    private static bool IsNonNegativeFinite(double value) => double.IsFinite(value) && value >= 0;

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed record DerivedMetrics(
        double P50LatencyMilliseconds,
        double P95LatencyMilliseconds,
        double P99LatencyMilliseconds,
        double MaximumRequestLatencyMilliseconds,
        double PeakQueueWaitMilliseconds,
        double TotalQueueWaitMilliseconds,
        int PeakRequestBytes,
        int PeakOutputBytes,
        int FailureCount,
        int FallbackCount);
}
