using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Societies.SnowGlobe;

namespace Societies.SnowGlobe.BenchmarkCli;

internal static class PinnedBenchmarkContract
{
    internal const string Endpoint = "http://127.0.0.1:11435/";
    internal const string RuntimeModelReference = "qwen3.5:4b";
    internal const string NormalizedModelIdentity = "qwen3.5-4b";
    internal const string QuantizationIdentity = "q4_k_m";
    internal const string ArtifactDigestSha256 = "2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd";
    // Runtime authorization binds the exact /api/tags size. This is distinct from the
    // 3,389,971,840-byte model-layer blob and the 3,389,984,444-byte five-file store total.
    internal const long ArtifactSizeBytes = 3_389_983_735;
    internal const long ParameterCount = 4_659_865_088;
    internal const string RuntimeExecutablePath = @"E:\AIModels\OllamaRuntimeRepair\runtime-v0.32.14\ollama.exe";
    internal const string RuntimeExecutableSha256 = "11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54";
    internal const string RuntimeProcessIdentity = "ollama-v0.32.14-sha11d7729c";
    internal const string NvidiaSmiPath = @"C:\Windows\System32\nvidia-smi.exe";
    internal const string NvidiaSmiSha256 = "8221f288cc777249a019031eb11cb75db25f3ba919e3d53836f9447366e0dfb6";
    internal const string GpuUuid = "GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19";
    internal const int GpuTotalVramMiB = 8192;
    internal const string RelativeEvidencePath = "artifacts/snowglobe/local-model/qwen3.5-4b-frozen-benchmark-v1.json";

    internal static LocalModelBenchmarkPlan CreatePlan()
    {
        LocalModelPreflightResult preflight = LocalModelAdapterPreflight.Validate(new LocalModelAdapterPreflightRequest
        {
            Endpoint = Endpoint,
            SharedServerIdentity = OllamaBenchmarkRunner.SharedServerIdentity,
            AdapterIdentity = OllamaBenchmarkRunner.AdapterIdentity,
            PromptIdentity = OllamaBenchmarkRunner.PromptIdentity,
            ModelIdentity = NormalizedModelIdentity,
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
            throw new LocalModelBenchmarkException("pinned_plan_invalid");
        }
        return preflight.BenchmarkPlan;
    }

    internal static OllamaRuntimeAuthorization CreateRuntimeAuthorization(int processId) => new()
    {
        RuntimeModelReference = RuntimeModelReference,
        ArtifactDigestSha256 = ArtifactDigestSha256,
        ArtifactSizeBytes = ArtifactSizeBytes,
        ArtifactFormat = "gguf",
        ModelFamily = "qwen35",
        ParameterSize = "4.7B",
        QuantizationLevel = "Q4_K_M",
        OllamaProcessIdentity = RuntimeProcessIdentity,
        OllamaProcessId = processId
    };

    internal static string ResolveEvidencePath(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string path = Path.GetFullPath(Path.Combine(root, RelativeEvidencePath));
        using PinnedEvidenceDirectoryLease lease = PinnedEvidenceDirectoryLease.Acquire(path);
        ReverifyEvidencePath(path);
        return path;
    }

    internal static void ReverifyEvidencePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        DirectoryInfo? localModel = Directory.GetParent(fullPath);
        DirectoryInfo? snowglobe = localModel?.Parent;
        DirectoryInfo? artifacts = snowglobe?.Parent;
        DirectoryInfo? root = artifacts?.Parent;
        if (root is null
            || !string.Equals(localModel!.Name, "local-model", StringComparison.Ordinal)
            || !string.Equals(snowglobe!.Name, "snowglobe", StringComparison.Ordinal)
            || !string.Equals(artifacts!.Name, "artifacts", StringComparison.Ordinal)
            || !string.Equals(Path.GetFileName(fullPath), Path.GetFileName(RelativeEvidencePath), StringComparison.Ordinal)
            || !string.Equals(
                fullPath,
                Path.GetFullPath(Path.Combine(root.FullName, RelativeEvidencePath)),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalModelBenchmarkException("evidence_path_outside_bound");
        }

        VerifyRepositoryRoot(root.FullName);
        VerifyDirectory(artifacts.FullName, "evidence_directory_invalid");
        VerifyDirectory(snowglobe.FullName, "evidence_directory_invalid");
        VerifyDirectory(localModel.FullName, "evidence_directory_invalid");
        FileAttributes? evidenceAttributes = TryGetAttributes(fullPath);
        if (evidenceAttributes.HasValue && evidenceAttributes.Value.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new LocalModelBenchmarkException("evidence_path_reparse_point_rejected");
        }
    }

    internal static void VerifyRepositoryRoot(string root)
    {
        VerifyDirectory(root, "repository_root_not_verified");
        VerifyGitMarker(Path.Combine(root, ".git"));
        VerifyMarkerFile(root, "CURRENT_BUILD.md");
        VerifyMarkerFile(root, "labs", "Societies.SnowGlobe", "Societies.SnowGlobe.csproj");
        VerifyDirectory(root, "repository_root_not_verified");
    }

    private static void VerifyGitMarker(string path)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue) throw new LocalModelBenchmarkException("repository_root_not_verified");
        RejectReparsePoint(attributes.Value);
        if (attributes.Value.HasFlag(FileAttributes.Directory)) return;
        FileInfo marker = new(path);
        if (marker.Length <= 0 || marker.Length > 4096)
        {
            throw new LocalModelBenchmarkException("repository_root_not_verified");
        }
    }

    private static void VerifyMarkerFile(string root, params string[] segments)
    {
        string current = root;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            current = Path.Combine(current, segments[index]);
            VerifyDirectory(current, "repository_root_not_verified");
        }

        string path = Path.Combine(current, segments[^1]);
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue || attributes.Value.HasFlag(FileAttributes.Directory))
        {
            throw new LocalModelBenchmarkException("repository_root_not_verified");
        }
        RejectReparsePoint(attributes.Value);
        if (new FileInfo(path).Length <= 0)
        {
            throw new LocalModelBenchmarkException("repository_root_not_verified");
        }
    }

    private static void VerifyDirectory(string path, string invalidCode)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue || !attributes.Value.HasFlag(FileAttributes.Directory))
        {
            throw new LocalModelBenchmarkException(invalidCode);
        }
        RejectReparsePoint(attributes.Value);
    }

    private static void RejectReparsePoint(FileAttributes attributes)
    {
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new LocalModelBenchmarkException("evidence_path_reparse_point_rejected");
        }
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    internal static string Sha256File(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal static class CanonicalBenchmarkEvidenceWriter
{
    internal const string SchemaVersion = "societies_ollama_benchmark_evidence/v1";

    internal static void WriteNew(
        string path,
        LocalModelBenchmarkPlan plan,
        OllamaBenchmarkRunResult result,
        Action? afterDirectoryLeaseAcquiredForTesting = null)
    {
        LocalModelBenchmarkValidationResult validation =
            LocalModelAdapterPreflight.ValidateBenchmarkEvidence(plan, result.Evidence);
        if (!validation.IsAccepted || result.Evidence.CanonicalMetricsSamplesUtf8 is not { Length: > 0 } samples)
        {
            throw new LocalModelBenchmarkException("benchmark_evidence_rejected_before_write");
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            LocalModelBenchmarkEvidence evidence = result.Evidence;
            OllamaVerifiedModelProvenance provenance = result.Provenance;
            writer.WriteStartObject();
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("contract_id", evidence.ContractId);
            writer.WriteString("endpoint", plan.Endpoint);
            writer.WriteString("model_identity", evidence.ModelIdentity);
            writer.WriteString("runtime_model_reference", provenance.RuntimeModelReference);
            writer.WriteString("artifact_digest_sha256", provenance.ArtifactDigestSha256);
            writer.WriteNumber("artifact_size_bytes", provenance.ArtifactSizeBytes);
            writer.WriteString("artifact_format", provenance.ArtifactFormat);
            writer.WriteString("model_family", provenance.ModelFamily);
            writer.WriteNumber("parameter_count", PinnedBenchmarkContract.ParameterCount);
            writer.WriteString("runtime_parameter_size", provenance.ParameterSize);
            writer.WriteString("quantization_level", provenance.QuantizationLevel);
            writer.WriteString("runtime_executable_sha256", PinnedBenchmarkContract.RuntimeExecutableSha256);
            writer.WriteString("nvidia_smi_sha256", PinnedBenchmarkContract.NvidiaSmiSha256);
            writer.WriteString("gpu_uuid", PinnedBenchmarkContract.GpuUuid);
            writer.WriteNumber("gpu_total_vram_mib", PinnedBenchmarkContract.GpuTotalVramMiB);
            writer.WriteString("ollama_process_identity", provenance.OllamaProcessIdentity);
            writer.WriteNumber("ollama_process_id", provenance.OllamaProcessId);
            writer.WriteString("vram_measurement_scope", "aggregate_gpu_used_mib_exact_uuid_pid_liveness_bound");
            writer.WriteString("vram_measurement_limit", "bounded_samples_do_not_guarantee_unsampled_transient_peak");
            writer.WriteNumber("context_window_tokens", evidence.ContextWindowTokens);
            writer.WriteNumber("output_token_limit", result.OutputTokenLimit);
            writer.WriteNumber("warmup_request_count", evidence.WarmupRequestCount);
            writer.WriteNumber("measured_request_count", evidence.MeasuredRequestCount);
            writer.WriteNumber("static_vram_mib", evidence.StaticVramMiB);
            writer.WriteNumber("observed_sampled_peak_vram_mib", result.ObservedSampledPeakVramMiB);
            writer.WriteNumber("maximum_allowed_observed_vram_mib", result.MaximumAllowedObservedVramMiB);
            writer.WriteNumber("vram_sample_count", result.VramSampleCount);
            writer.WriteNumber("p50_latency_milliseconds", evidence.P50LatencyMilliseconds);
            writer.WriteNumber("p95_latency_milliseconds", evidence.P95LatencyMilliseconds);
            writer.WriteNumber("p99_latency_milliseconds", evidence.P99LatencyMilliseconds);
            writer.WriteNumber("maximum_request_latency_milliseconds", evidence.MaximumRequestLatencyMilliseconds);
            writer.WriteNumber("throughput_tokens_per_second", evidence.ThroughputTokensPerSecond);
            writer.WriteNumber("failure_count", evidence.FailureCount);
            writer.WriteNumber("fallback_count", evidence.FallbackCount);
            writer.WriteNumber("queue_bound", evidence.QueueBound);
            writer.WriteNumber("peak_queue_depth", evidence.PeakQueueDepth);
            writer.WriteNumber("peak_request_bytes", evidence.PeakRequestBytes);
            writer.WriteNumber("peak_output_bytes", evidence.PeakOutputBytes);
            writer.WriteNumber("peak_queue_wait_milliseconds", evidence.PeakQueueWaitMilliseconds);
            writer.WriteNumber("total_queue_wait_milliseconds", evidence.TotalQueueWaitMilliseconds);
            writer.WriteString("canonical_metrics_sample_digest_sha256", evidence.CanonicalMetricsSampleDigestSha256);
            writer.WritePropertyName("canonical_metrics_samples");
            writer.WriteRawValue(samples, skipInputValidation: false);
            writer.WriteString("response_authority", validation.ResponseAuthority);
            writer.WriteBoolean("deterministic_commit_validation_required", validation.DeterministicCommitValidationRequired);
            writer.WriteBoolean("client_side_serialization_enforced", result.ClientSideSerializationEnforced);
            writer.WriteBoolean("external_server_startup_configuration_verified", result.ExternalServerStartupConfigurationVerified);
            writer.WriteString("external_server_startup_configuration_claim", result.ExternalServerStartupConfigurationClaim);
            writer.WriteEndObject();
        }

        // The held native handles omit FILE_SHARE_DELETE, so every verified directory from the
        // repository root through local-model remains pinned until CreateNew and the durable flush finish.
        using PinnedEvidenceDirectoryLease lease = PinnedEvidenceDirectoryLease.Acquire(path);
        afterDirectoryLeaseAcquiredForTesting?.Invoke();
        using FileStream output = new(
            lease.EvidencePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough);
        output.Write(buffer.WrittenSpan);
        output.Flush(flushToDisk: true);
    }
}
