using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Societies.SnowGlobe;

public sealed class LocalPremiumComparisonException : Exception
{
    internal LocalPremiumComparisonException(string code, Exception? innerException = null)
        : base(code, innerException)
    {
        if (!LocalPremiumComparisonErrors.IsAllowlisted(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Code = code;
    }

    public string Code { get; }
}

public sealed class LocalPremiumComparisonReport
{
    private readonly byte[] _canonicalUtf8;

    internal LocalPremiumComparisonReport(byte[] canonicalUtf8, string status)
    {
        ArgumentNullException.ThrowIfNull(canonicalUtf8);
        _canonicalUtf8 = canonicalUtf8.ToArray();
        CanonicalJson = Encoding.UTF8.GetString(_canonicalUtf8);
        CanonicalDigestSha256 = LocalPremiumComparisonHash.Sha256(_canonicalUtf8);
        Status = status;
    }

    /// <summary>A fresh detached copy is returned on every access.</summary>
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();

    public string CanonicalJson { get; }

    public string CanonicalDigestSha256 { get; }

    public string Status { get; }
}

/// <summary>
/// Pure offline evaluation of the one frozen local compatibility cell against the currently empty
/// live-premium evidence lane. It performs no discovery, I/O, inference, financial, or world operation.
/// </summary>
public static class LocalPremiumComparison
{
    public const int MaximumLocalCellBytes = 16 * 1024;
    public const int MaximumLocalCellDepth = 8;
    public const int MaximumReportBytes = 32 * 1024;

    public static LocalPremiumComparisonReport Evaluate(ReadOnlyMemory<byte> canonicalLocalCellUtf8) =>
        Evaluate(canonicalLocalCellUtf8, AbsentPremiumComparisonEvidenceAdapter.Instance);

    internal static LocalPremiumComparisonReport Evaluate(
        ReadOnlyMemory<byte> canonicalLocalCellUtf8,
        IPremiumComparisonEvidenceAdapter premiumEvidenceAdapter)
    {
        ArgumentNullException.ThrowIfNull(premiumEvidenceAdapter);

        if (canonicalLocalCellUtf8.IsEmpty)
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellRequired);
        }

        if (canonicalLocalCellUtf8.Length > MaximumLocalCellBytes)
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellTooLarge);
        }

        byte[] snapshot = canonicalLocalCellUtf8.ToArray();
        byte[]? canonicalSamples = null;
        try
        {
            InspectStructure(snapshot);
            ValidatePropertyNames(snapshot);

            FrozenLocalBenchmarkCell cell;
            try
            {
                cell = JsonSerializer.Deserialize<FrozenLocalBenchmarkCell>(snapshot, FrozenLocalBenchmarkRegistry.JsonOptions)
                    ?? throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
            }
            catch (LocalPremiumComparisonException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid, exception);
            }
            catch (NotSupportedException exception)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid, exception);
            }

            LocalModelBenchmarkPlan plan = FrozenLocalBenchmarkRegistry.CreatePlan();
            FrozenLocalBenchmarkRegistry.ValidateRegistry(plan);
            FrozenLocalBenchmarkRegistry.ValidateFrozenBindings(cell, plan);

            IReadOnlyList<LocalModelMetricsSample>? samples = cell.CanonicalMetricsSamples?.Samples;
            if (samples is null)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellBindingMismatch);
            }

            try
            {
                canonicalSamples = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(samples);
            }
            catch (ArgumentException exception)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalBenchmarkEvidenceRejected, exception);
            }

            LocalModelBenchmarkEvidence evidence = FrozenLocalBenchmarkRegistry.ToBenchmarkEvidence(cell, canonicalSamples);
            LocalModelBenchmarkValidationResult validation =
                LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, evidence);
            if (!validation.IsAccepted)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalBenchmarkEvidenceRejected);
            }

            byte[] canonicalCell = FrozenLocalBenchmarkRegistry.SerializeCanonical(cell, canonicalSamples);
            try
            {
                if (!snapshot.AsSpan().SequenceEqual(canonicalCell))
                {
                    throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellNotCanonical);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonicalCell);
            }

            if (!string.Equals(
                LocalPremiumComparisonHash.Sha256(snapshot),
                FrozenLocalBenchmarkRegistry.LocalEvidenceDigestSha256,
                StringComparison.Ordinal))
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalArtifactDigestMismatch);
            }

            PremiumComparisonEvidenceSnapshot premium = premiumEvidenceAdapter.Inspect();
            if (premium.Classification is not (
                PremiumComparisonEvidenceClassification.Absent
                or PremiumComparisonEvidenceClassification.OfflineFixture))
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.PremiumEvidenceAdapterInvalid);
            }

            return BuildReport(cell);
        }
        finally
        {
            if (canonicalSamples is not null)
            {
                CryptographicOperations.ZeroMemory(canonicalSamples);
            }

            CryptographicOperations.ZeroMemory(snapshot);
        }
    }

    private static LocalPremiumComparisonReport BuildReport(FrozenLocalBenchmarkCell cell)
    {
        byte[] payload = WriteReport(cell, payloadDigestSha256: null);
        string payloadDigest = LocalPremiumComparisonHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);

        byte[] report = WriteReport(cell, payloadDigest);
        if (report.Length == 0 || report.Length > MaximumReportBytes)
        {
            CryptographicOperations.ZeroMemory(report);
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.ReportSizeInvalid);
        }

        return new LocalPremiumComparisonReport(report, FrozenLocalBenchmarkRegistry.Status);
    }

    private static byte[] WriteReport(FrozenLocalBenchmarkCell cell, string? payloadDigestSha256)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", FrozenLocalBenchmarkRegistry.ComparisonSchemaVersion);
            writer.WriteString("comparison_contract_id", FrozenLocalBenchmarkRegistry.ComparisonContractId);
            writer.WriteString("status", FrozenLocalBenchmarkRegistry.Status);
            writer.WritePropertyName("local");
            writer.WriteStartObject();
            writer.WriteString("evidence_digest_sha256", FrozenLocalBenchmarkRegistry.LocalEvidenceDigestSha256);
            writer.WriteString("benchmark_contract_id", cell.ContractId);
            writer.WriteString("workload_identity", FrozenLocalBenchmarkRegistry.WorkloadIdentity);
            writer.WriteString("model_identity", cell.ModelIdentity);
            writer.WriteString("quantization_identity", FrozenLocalBenchmarkRegistry.QuantizationIdentity);
            writer.WriteString("prompt_identity", OllamaBenchmarkRunner.PromptIdentity);
            writer.WriteString("response_schema_identity", FrozenLocalBenchmarkRegistry.ResponseSchemaIdentity);
            writer.WriteString("metrics_sample_schema", LocalModelAdapterPreflight.MetricsSampleSchema);
            writer.WriteString("metrics_sample_digest_sha256", cell.CanonicalMetricsSampleDigestSha256);
            writer.WriteNumber("context_window_tokens", cell.ContextWindowTokens);
            writer.WriteNumber("output_token_limit", cell.OutputTokenLimit);
            writer.WriteNumber("warmup_request_count", cell.WarmupRequestCount);
            writer.WriteNumber("measured_request_count", cell.MeasuredRequestCount);
            writer.WriteNumber("static_vram_mib", cell.StaticVramMiB);
            writer.WriteNumber("observed_sampled_peak_vram_mib", cell.ObservedSampledPeakVramMiB);
            writer.WriteNumber("maximum_allowed_observed_vram_mib", cell.MaximumAllowedObservedVramMiB);
            writer.WriteNumber("vram_sample_count", cell.VramSampleCount);
            writer.WriteNumber("p50_latency_milliseconds", cell.P50LatencyMilliseconds);
            writer.WriteNumber("p95_latency_milliseconds", cell.P95LatencyMilliseconds);
            writer.WriteNumber("p99_latency_milliseconds", cell.P99LatencyMilliseconds);
            writer.WriteNumber("maximum_request_latency_milliseconds", cell.MaximumRequestLatencyMilliseconds);
            writer.WriteNumber("throughput_tokens_per_second", cell.ThroughputTokensPerSecond);
            writer.WriteNumber("failure_count", cell.FailureCount);
            writer.WriteNumber("fallback_count", cell.FallbackCount);
            writer.WriteNumber("queue_bound", cell.QueueBound);
            writer.WriteNumber("peak_queue_depth", cell.PeakQueueDepth);
            writer.WriteNumber("peak_request_bytes", cell.PeakRequestBytes);
            writer.WriteNumber("peak_output_bytes", cell.PeakOutputBytes);
            writer.WriteNumber("peak_queue_wait_milliseconds", cell.PeakQueueWaitMilliseconds);
            writer.WriteNumber("total_queue_wait_milliseconds", cell.TotalQueueWaitMilliseconds);
            writer.WriteEndObject();
            writer.WriteNull("premium");
            writer.WriteNull("premium_cost");
            writer.WriteNull("performance_delta");
            writer.WritePropertyName("missing_gate_codes");
            writer.WriteStartArray();
            foreach (string code in FrozenLocalBenchmarkRegistry.MissingGateCodes)
            {
                writer.WriteStringValue(code);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("claim_limitations");
            writer.WriteStartArray();
            foreach (string limitation in FrozenLocalBenchmarkRegistry.ClaimLimitations)
            {
                writer.WriteStringValue(limitation);
            }

            writer.WriteEndArray();
            if (payloadDigestSha256 is not null)
            {
                writer.WriteString("report_payload_digest_sha256", payloadDigestSha256);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void InspectStructure(ReadOnlySpan<byte> utf8Json)
    {
        Stack<HashSet<string>> objectProperties = new();
        try
        {
            Utf8JsonReader reader = new(utf8Json, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumLocalCellDepth + 1
            });
            while (reader.Read())
            {
                if ((reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                    && reader.CurrentDepth >= MaximumLocalCellDepth)
                {
                    throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellTooDeep);
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.PropertyName:
                        if (objectProperties.Count == 0 || !objectProperties.Peek().Add(reader.GetString()!))
                        {
                            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellDuplicateProperty);
                        }

                        break;
                    case JsonTokenType.EndObject:
                        if (objectProperties.Count == 0)
                        {
                            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
                        }

                        objectProperties.Pop();
                        break;
                }
            }

            if (objectProperties.Count != 0)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
            }
        }
        catch (LocalPremiumComparisonException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid, exception);
        }
    }

    private static void ValidatePropertyNames(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumLocalCellDepth
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
            }

            EnsureOnlyProperties(root, FrozenLocalBenchmarkRegistry.RootPropertyNames);
            if (!root.TryGetProperty("canonical_metrics_samples", out JsonElement envelope)
                || envelope.ValueKind != JsonValueKind.Object)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
            }

            EnsureOnlyProperties(envelope, FrozenLocalBenchmarkRegistry.EnvelopePropertyNames);
            if (!envelope.TryGetProperty("samples", out JsonElement samples)
                || samples.ValueKind != JsonValueKind.Array)
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
            }

            foreach (JsonElement sample in samples.EnumerateArray())
            {
                if (sample.ValueKind != JsonValueKind.Object)
                {
                    throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid);
                }

                EnsureOnlyProperties(sample, FrozenLocalBenchmarkRegistry.SamplePropertyNames);
            }
        }
        catch (LocalPremiumComparisonException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellJsonInvalid, exception);
        }
    }

    private static void EnsureOnlyProperties(JsonElement value, IReadOnlySet<string> allowed)
    {
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellUnknownProperty);
            }
        }
    }
}

internal enum PremiumComparisonEvidenceClassification
{
    Absent,
    OfflineFixture
}

internal readonly record struct PremiumComparisonEvidenceSnapshot(
    PremiumComparisonEvidenceClassification Classification);

internal interface IPremiumComparisonEvidenceAdapter
{
    PremiumComparisonEvidenceSnapshot Inspect();
}

internal sealed class AbsentPremiumComparisonEvidenceAdapter : IPremiumComparisonEvidenceAdapter
{
    internal static AbsentPremiumComparisonEvidenceAdapter Instance { get; } = new();

    private AbsentPremiumComparisonEvidenceAdapter()
    {
    }

    public PremiumComparisonEvidenceSnapshot Inspect() =>
        new(PremiumComparisonEvidenceClassification.Absent);
}

/// <summary>Test-only evidence classification. It is intentionally incapable of representing live evidence.</summary>
internal sealed class OfflineFixturePremiumComparisonEvidenceAdapter : IPremiumComparisonEvidenceAdapter
{
    public PremiumComparisonEvidenceSnapshot Inspect() =>
        new(PremiumComparisonEvidenceClassification.OfflineFixture);
}

internal static class FrozenLocalBenchmarkRegistry
{
    internal const string ComparisonSchemaVersion = "snow_globe_local_premium_comparison/v1";
    internal const string ComparisonContractId = "5ca8f57d8dd4fb5de18a1179c1a8acf25eef944ac7350f30514f097932d95227";
    internal const string Status = "insufficient_live_premium_evidence";
    internal const string LocalEvidenceDigestSha256 = "961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d";
    internal const string LocalEvidenceSchemaVersion = "societies_ollama_benchmark_evidence/v1";
    internal const string LocalBenchmarkContractId = "9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0";
    internal const string WorkloadIdentity = "snow-globe-ollama-qwen3.5-4b-frozen-cell-v1";
    internal const string ResponseSchemaIdentity = "snow-globe-action-proposal-schema-v1";
    internal const string RuntimeModelReference = "qwen3.5:4b";
    internal const string ModelIdentity = "qwen3.5-4b";
    internal const string QuantizationIdentity = "q4_k_m";
    internal const string MetricsDigestSha256 = "e2ab348567fbc35fe6e7dbf850c4ec2fabbe1136142a81855758e1d2ee01401b";

    private const string Endpoint = "http://127.0.0.1:11435/";
    private const string ModelArtifactDigestSha256 = "2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd";
    private const long ModelArtifactSizeBytes = 3_389_983_735;
    private const string RuntimeExecutableDigestSha256 = "11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54";
    private const string NvidiaSmiDigestSha256 = "8221f288cc777249a019031eb11cb75db25f3ba919e3d53836f9447366e0dfb6";
    private const string GpuIdentity = "GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19";
    private const string RuntimeProcessIdentity = "ollama-v0.32.14-sha11d7729c";

    internal static readonly IReadOnlyList<string> MissingGateCodes = Array.AsReadOnly(new[]
    {
        "live_premium_profile_not_approved",
        "live_premium_evidence_absent",
        "live_premium_operational_metrics_absent",
        "live_premium_cost_evidence_absent"
    });

    internal static readonly IReadOnlyList<string> ClaimLimitations = Array.AsReadOnly(new[]
    {
        "frozen_local_cell_only",
        "local_compatibility_fit_and_latency_only",
        "sampled_vram_does_not_prove_unsampled_transient_peak",
        "no_live_premium_evidence",
        "no_quality_or_intelligence_conclusion",
        "no_cost_conclusion",
        "no_winner_selection"
    });

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = LocalPremiumComparison.MaximumLocalCellDepth,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter<LocalModelMetricsSampleOutcome>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)
        }
    };

    internal static readonly IReadOnlySet<string> RootPropertyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema_version", "contract_id", "endpoint", "model_identity", "runtime_model_reference",
        "artifact_digest_sha256", "artifact_size_bytes", "artifact_format", "model_family", "parameter_count",
        "runtime_parameter_size", "quantization_level", "runtime_executable_sha256", "nvidia_smi_sha256",
        "gpu_uuid", "gpu_total_vram_mib", "ollama_process_identity", "ollama_process_id",
        "vram_measurement_scope", "vram_measurement_limit", "context_window_tokens", "output_token_limit",
        "warmup_request_count", "measured_request_count", "static_vram_mib", "observed_sampled_peak_vram_mib",
        "maximum_allowed_observed_vram_mib", "vram_sample_count", "p50_latency_milliseconds",
        "p95_latency_milliseconds", "p99_latency_milliseconds", "maximum_request_latency_milliseconds",
        "throughput_tokens_per_second", "failure_count", "fallback_count", "queue_bound", "peak_queue_depth",
        "peak_request_bytes", "peak_output_bytes", "peak_queue_wait_milliseconds",
        "total_queue_wait_milliseconds", "canonical_metrics_sample_digest_sha256", "canonical_metrics_samples",
        "response_authority", "deterministic_commit_validation_required", "client_side_serialization_enforced",
        "external_server_startup_configuration_verified", "external_server_startup_configuration_claim"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> EnvelopePropertyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema_version", "samples"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> SamplePropertyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "sequence", "request_latency_milliseconds", "queue_wait_milliseconds", "request_bytes", "output_bytes", "outcome"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static LocalModelBenchmarkPlan CreatePlan()
    {
        LocalModelPreflightResult preflight = LocalModelAdapterPreflight.Validate(new LocalModelAdapterPreflightRequest
        {
            Endpoint = Endpoint,
            SharedServerIdentity = OllamaBenchmarkRunner.SharedServerIdentity,
            AdapterIdentity = OllamaBenchmarkRunner.AdapterIdentity,
            PromptIdentity = OllamaBenchmarkRunner.PromptIdentity,
            ModelIdentity = ModelIdentity,
            QuantizationIdentity = QuantizationIdentity,
            ContextWindowTokens = 4096,
            AuthenticationMode = "none",
            CredentialReferences = Array.Empty<string>(),
            Budgets = new LocalModelResourceBudgets
            {
                RequestBytes = 16 * 1024,
                OutputBytes = 8 * 1024,
                QueueDepth = 1,
                RequestTimeoutMilliseconds = 120_000,
                TotalQueueWaitMilliseconds = 30_000
            },
            Benchmark = new LocalModelBenchmarkRequirements
            {
                WarmupRequestCount = 1,
                MeasuredRequestCount = 10
            }
        });
        if (!preflight.IsValid || preflight.BenchmarkPlan is null)
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.RegistryContractInvalid);
        }

        return preflight.BenchmarkPlan;
    }

    internal static void ValidateRegistry(LocalModelBenchmarkPlan plan)
    {
        List<string> materialParts = new()
        {
            ComparisonSchemaVersion,
            $"status={Status}",
            $"maximum_local_cell_bytes={LocalPremiumComparison.MaximumLocalCellBytes}",
            $"maximum_local_cell_depth={LocalPremiumComparison.MaximumLocalCellDepth}",
            $"maximum_report_bytes={LocalPremiumComparison.MaximumReportBytes}",
            LocalEvidenceDigestSha256,
            LocalEvidenceSchemaVersion,
            LocalBenchmarkContractId,
            WorkloadIdentity,
            OllamaBenchmarkRunner.SharedServerIdentity,
            OllamaBenchmarkRunner.AdapterIdentity,
            OllamaBenchmarkRunner.PromptIdentity,
            ResponseSchemaIdentity,
            LocalModelAdapterPreflight.MetricsSampleSchema,
            MetricsDigestSha256,
            ModelIdentity,
            QuantizationIdentity,
            "context_window_tokens=4096",
            "output_token_limit=96",
            "warmup_request_count=1",
            "measured_request_count=10",
            "premium=null",
            "premium_cost=null",
            "performance_delta=null"
        };
        materialParts.AddRange(MissingGateCodes.Select(code => $"missing_gate={code}"));
        materialParts.AddRange(ClaimLimitations.Select(limitation => $"claim_limitation={limitation}"));
        string material = string.Join('\n', materialParts);
        if (!string.Equals(plan.ContractId, LocalBenchmarkContractId, StringComparison.Ordinal)
            || !string.Equals(LocalPremiumComparisonHash.Sha256(Encoding.UTF8.GetBytes(material)), ComparisonContractId, StringComparison.Ordinal))
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.RegistryContractInvalid);
        }
    }

    internal static void ValidateFrozenBindings(FrozenLocalBenchmarkCell cell, LocalModelBenchmarkPlan plan)
    {
        if (!string.Equals(cell.SchemaVersion, LocalEvidenceSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(cell.ContractId, LocalBenchmarkContractId, StringComparison.Ordinal)
            || !string.Equals(cell.Endpoint, Endpoint, StringComparison.Ordinal)
            || !string.Equals(cell.ModelIdentity, ModelIdentity, StringComparison.Ordinal)
            || !string.Equals(cell.RuntimeModelReference, RuntimeModelReference, StringComparison.Ordinal)
            || !string.Equals(cell.ArtifactDigestSha256, ModelArtifactDigestSha256, StringComparison.Ordinal)
            || cell.ArtifactSizeBytes != ModelArtifactSizeBytes
            || !string.Equals(cell.ArtifactFormat, "gguf", StringComparison.Ordinal)
            || !string.Equals(cell.ModelFamily, "qwen35", StringComparison.Ordinal)
            || cell.ParameterCount != 4_659_865_088
            || !string.Equals(cell.RuntimeParameterSize, "4.7B", StringComparison.Ordinal)
            || !string.Equals(cell.QuantizationLevel, "Q4_K_M", StringComparison.Ordinal)
            || !string.Equals(cell.RuntimeExecutableSha256, RuntimeExecutableDigestSha256, StringComparison.Ordinal)
            || !string.Equals(cell.NvidiaSmiSha256, NvidiaSmiDigestSha256, StringComparison.Ordinal)
            || !string.Equals(cell.GpuUuid, GpuIdentity, StringComparison.Ordinal)
            || cell.GpuTotalVramMiB != 8192
            || !string.Equals(cell.OllamaProcessIdentity, RuntimeProcessIdentity, StringComparison.Ordinal)
            || cell.OllamaProcessId != 20808
            || !string.Equals(cell.VramMeasurementScope, "aggregate_gpu_used_mib_exact_uuid_pid_liveness_bound", StringComparison.Ordinal)
            || !string.Equals(cell.VramMeasurementLimit, "bounded_samples_do_not_guarantee_unsampled_transient_peak", StringComparison.Ordinal)
            || cell.ContextWindowTokens != plan.ContextWindowTokens
            || cell.OutputTokenLimit != OllamaBenchmarkRunner.OutputTokenLimit
            || cell.WarmupRequestCount != plan.WarmupRequestCount
            || cell.MeasuredRequestCount != plan.MeasuredRequestCount
            || cell.MaximumAllowedObservedVramMiB != 6963.2
            || cell.VramSampleCount != 179
            || !string.Equals(cell.ResponseAuthority, LocalModelAdapterPreflight.ResponseAuthority, StringComparison.Ordinal)
            || !cell.DeterministicCommitValidationRequired
            || !cell.ClientSideSerializationEnforced
            || cell.ExternalServerStartupConfigurationVerified
            || !string.Equals(
                cell.ExternalServerStartupConfigurationClaim,
                OllamaBenchmarkRunner.ExternalStartupConfigurationClaim,
                StringComparison.Ordinal))
        {
            throw new LocalPremiumComparisonException(LocalPremiumComparisonErrors.LocalCellBindingMismatch);
        }
    }

    internal static LocalModelBenchmarkEvidence ToBenchmarkEvidence(FrozenLocalBenchmarkCell cell, byte[] canonicalSamples) => new()
    {
        ContractId = cell.ContractId,
        ModelIdentity = cell.ModelIdentity,
        QuantizationIdentity = QuantizationIdentity,
        ContextWindowTokens = cell.ContextWindowTokens,
        StaticVramMiB = cell.StaticVramMiB,
        PeakVramMiB = cell.ObservedSampledPeakVramMiB,
        WarmupRequestCount = cell.WarmupRequestCount,
        MeasuredRequestCount = cell.MeasuredRequestCount,
        MetricsSampleCount = cell.CanonicalMetricsSamples!.Samples!.Count,
        CanonicalMetricsSampleDigestSha256 = cell.CanonicalMetricsSampleDigestSha256,
        CanonicalMetricsSamplesUtf8 = canonicalSamples,
        P50LatencyMilliseconds = cell.P50LatencyMilliseconds,
        P95LatencyMilliseconds = cell.P95LatencyMilliseconds,
        P99LatencyMilliseconds = cell.P99LatencyMilliseconds,
        MaximumRequestLatencyMilliseconds = cell.MaximumRequestLatencyMilliseconds,
        ThroughputTokensPerSecond = cell.ThroughputTokensPerSecond,
        FailureCount = cell.FailureCount,
        FallbackCount = cell.FallbackCount,
        QueueBound = cell.QueueBound,
        PeakQueueDepth = cell.PeakQueueDepth,
        PeakRequestBytes = cell.PeakRequestBytes,
        PeakOutputBytes = cell.PeakOutputBytes,
        PeakQueueWaitMilliseconds = cell.PeakQueueWaitMilliseconds,
        TotalQueueWaitMilliseconds = cell.TotalQueueWaitMilliseconds
    };

    internal static byte[] SerializeCanonical(FrozenLocalBenchmarkCell cell, ReadOnlySpan<byte> canonicalSamples)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", cell.SchemaVersion);
            writer.WriteString("contract_id", cell.ContractId);
            writer.WriteString("endpoint", cell.Endpoint);
            writer.WriteString("model_identity", cell.ModelIdentity);
            writer.WriteString("runtime_model_reference", cell.RuntimeModelReference);
            writer.WriteString("artifact_digest_sha256", cell.ArtifactDigestSha256);
            writer.WriteNumber("artifact_size_bytes", cell.ArtifactSizeBytes);
            writer.WriteString("artifact_format", cell.ArtifactFormat);
            writer.WriteString("model_family", cell.ModelFamily);
            writer.WriteNumber("parameter_count", cell.ParameterCount);
            writer.WriteString("runtime_parameter_size", cell.RuntimeParameterSize);
            writer.WriteString("quantization_level", cell.QuantizationLevel);
            writer.WriteString("runtime_executable_sha256", cell.RuntimeExecutableSha256);
            writer.WriteString("nvidia_smi_sha256", cell.NvidiaSmiSha256);
            writer.WriteString("gpu_uuid", cell.GpuUuid);
            writer.WriteNumber("gpu_total_vram_mib", cell.GpuTotalVramMiB);
            writer.WriteString("ollama_process_identity", cell.OllamaProcessIdentity);
            writer.WriteNumber("ollama_process_id", cell.OllamaProcessId);
            writer.WriteString("vram_measurement_scope", cell.VramMeasurementScope);
            writer.WriteString("vram_measurement_limit", cell.VramMeasurementLimit);
            writer.WriteNumber("context_window_tokens", cell.ContextWindowTokens);
            writer.WriteNumber("output_token_limit", cell.OutputTokenLimit);
            writer.WriteNumber("warmup_request_count", cell.WarmupRequestCount);
            writer.WriteNumber("measured_request_count", cell.MeasuredRequestCount);
            writer.WriteNumber("static_vram_mib", cell.StaticVramMiB);
            writer.WriteNumber("observed_sampled_peak_vram_mib", cell.ObservedSampledPeakVramMiB);
            writer.WriteNumber("maximum_allowed_observed_vram_mib", cell.MaximumAllowedObservedVramMiB);
            writer.WriteNumber("vram_sample_count", cell.VramSampleCount);
            writer.WriteNumber("p50_latency_milliseconds", cell.P50LatencyMilliseconds);
            writer.WriteNumber("p95_latency_milliseconds", cell.P95LatencyMilliseconds);
            writer.WriteNumber("p99_latency_milliseconds", cell.P99LatencyMilliseconds);
            writer.WriteNumber("maximum_request_latency_milliseconds", cell.MaximumRequestLatencyMilliseconds);
            writer.WriteNumber("throughput_tokens_per_second", cell.ThroughputTokensPerSecond);
            writer.WriteNumber("failure_count", cell.FailureCount);
            writer.WriteNumber("fallback_count", cell.FallbackCount);
            writer.WriteNumber("queue_bound", cell.QueueBound);
            writer.WriteNumber("peak_queue_depth", cell.PeakQueueDepth);
            writer.WriteNumber("peak_request_bytes", cell.PeakRequestBytes);
            writer.WriteNumber("peak_output_bytes", cell.PeakOutputBytes);
            writer.WriteNumber("peak_queue_wait_milliseconds", cell.PeakQueueWaitMilliseconds);
            writer.WriteNumber("total_queue_wait_milliseconds", cell.TotalQueueWaitMilliseconds);
            writer.WriteString("canonical_metrics_sample_digest_sha256", cell.CanonicalMetricsSampleDigestSha256);
            writer.WritePropertyName("canonical_metrics_samples");
            writer.WriteRawValue(canonicalSamples, skipInputValidation: false);
            writer.WriteString("response_authority", cell.ResponseAuthority);
            writer.WriteBoolean("deterministic_commit_validation_required", cell.DeterministicCommitValidationRequired);
            writer.WriteBoolean("client_side_serialization_enforced", cell.ClientSideSerializationEnforced);
            writer.WriteBoolean("external_server_startup_configuration_verified", cell.ExternalServerStartupConfigurationVerified);
            writer.WriteString("external_server_startup_configuration_claim", cell.ExternalServerStartupConfigurationClaim);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}

internal static class LocalPremiumComparisonErrors
{
    internal const string LocalCellRequired = "local_cell_required";
    internal const string LocalCellTooLarge = "local_cell_too_large";
    internal const string LocalCellTooDeep = "local_cell_too_deep";
    internal const string LocalCellDuplicateProperty = "local_cell_duplicate_property";
    internal const string LocalCellUnknownProperty = "local_cell_unknown_property";
    internal const string LocalCellJsonInvalid = "local_cell_json_invalid";
    internal const string LocalCellNotCanonical = "local_cell_not_canonical";
    internal const string LocalCellBindingMismatch = "local_cell_binding_mismatch";
    internal const string LocalBenchmarkEvidenceRejected = "local_benchmark_evidence_rejected";
    internal const string LocalArtifactDigestMismatch = "local_artifact_digest_mismatch";
    internal const string RegistryContractInvalid = "registry_contract_invalid";
    internal const string PremiumEvidenceAdapterInvalid = "premium_evidence_adapter_invalid";
    internal const string ReportSizeInvalid = "report_size_invalid";

    internal static bool IsAllowlisted(string code) => code is
        LocalCellRequired
        or LocalCellTooLarge
        or LocalCellTooDeep
        or LocalCellDuplicateProperty
        or LocalCellUnknownProperty
        or LocalCellJsonInvalid
        or LocalCellNotCanonical
        or LocalCellBindingMismatch
        or LocalBenchmarkEvidenceRejected
        or LocalArtifactDigestMismatch
        or RegistryContractInvalid
        or PremiumEvidenceAdapterInvalid
        or ReportSizeInvalid;
}

internal static class LocalPremiumComparisonHash
{
    internal static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}

internal sealed record FrozenLocalBenchmarkCell
{
    public string? SchemaVersion { get; init; }
    public string? ContractId { get; init; }
    public string? Endpoint { get; init; }
    public string? ModelIdentity { get; init; }
    public string? RuntimeModelReference { get; init; }
    public string? ArtifactDigestSha256 { get; init; }
    public long ArtifactSizeBytes { get; init; }
    public string? ArtifactFormat { get; init; }
    public string? ModelFamily { get; init; }
    public long ParameterCount { get; init; }
    public string? RuntimeParameterSize { get; init; }
    public string? QuantizationLevel { get; init; }
    public string? RuntimeExecutableSha256 { get; init; }
    public string? NvidiaSmiSha256 { get; init; }
    [JsonPropertyName("gpu_uuid")]
    public string? GpuUuid { get; init; }
    [JsonPropertyName("gpu_total_vram_mib")]
    public int GpuTotalVramMiB { get; init; }
    public string? OllamaProcessIdentity { get; init; }
    public int OllamaProcessId { get; init; }
    public string? VramMeasurementScope { get; init; }
    public string? VramMeasurementLimit { get; init; }
    public int ContextWindowTokens { get; init; }
    public int OutputTokenLimit { get; init; }
    public int WarmupRequestCount { get; init; }
    public int MeasuredRequestCount { get; init; }
    [JsonPropertyName("static_vram_mib")]
    public double StaticVramMiB { get; init; }
    [JsonPropertyName("observed_sampled_peak_vram_mib")]
    public double ObservedSampledPeakVramMiB { get; init; }
    [JsonPropertyName("maximum_allowed_observed_vram_mib")]
    public double MaximumAllowedObservedVramMiB { get; init; }
    public int VramSampleCount { get; init; }
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
    public string? CanonicalMetricsSampleDigestSha256 { get; init; }
    public LocalModelMetricsSampleEnvelope? CanonicalMetricsSamples { get; init; }
    public string? ResponseAuthority { get; init; }
    public bool DeterministicCommitValidationRequired { get; init; }
    public bool ClientSideSerializationEnforced { get; init; }
    public bool ExternalServerStartupConfigurationVerified { get; init; }
    public string? ExternalServerStartupConfigurationClaim { get; init; }
}
