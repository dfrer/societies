using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class LocalPremiumComparisonTests
{
    private const string ExpectedArtifactDigest = "961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d";
    private const string FrozenArtifact = """{"schema_version":"societies_ollama_benchmark_evidence/v1","contract_id":"9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0","endpoint":"http://127.0.0.1:11435/","model_identity":"qwen3.5-4b","runtime_model_reference":"qwen3.5:4b","artifact_digest_sha256":"2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd","artifact_size_bytes":3389983735,"artifact_format":"gguf","model_family":"qwen35","parameter_count":4659865088,"runtime_parameter_size":"4.7B","quantization_level":"Q4_K_M","runtime_executable_sha256":"11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54","nvidia_smi_sha256":"8221f288cc777249a019031eb11cb75db25f3ba919e3d53836f9447366e0dfb6","gpu_uuid":"GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19","gpu_total_vram_mib":8192,"ollama_process_identity":"ollama-v0.32.14-sha11d7729c","ollama_process_id":20808,"vram_measurement_scope":"aggregate_gpu_used_mib_exact_uuid_pid_liveness_bound","vram_measurement_limit":"bounded_samples_do_not_guarantee_unsampled_transient_peak","context_window_tokens":4096,"output_token_limit":96,"warmup_request_count":1,"measured_request_count":10,"static_vram_mib":6351,"observed_sampled_peak_vram_mib":6432,"maximum_allowed_observed_vram_mib":6963.2,"vram_sample_count":179,"p50_latency_milliseconds":887.0709,"p95_latency_milliseconds":1036.6006,"p99_latency_milliseconds":1036.6006,"maximum_request_latency_milliseconds":1036.6006,"throughput_tokens_per_second":57.63771812273952,"failure_count":0,"fallback_count":0,"queue_bound":1,"peak_queue_depth":1,"peak_request_bytes":801,"peak_output_bytes":873,"peak_queue_wait_milliseconds":0.5206,"total_queue_wait_milliseconds":0.5206,"canonical_metrics_sample_digest_sha256":"e2ab348567fbc35fe6e7dbf850c4ec2fabbe1136142a81855758e1d2ee01401b","canonical_metrics_samples":{"schema_version":"societies_local_model_metrics_sample/v1","samples":[{"sequence":0,"request_latency_milliseconds":887.0709,"queue_wait_milliseconds":0.5206,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":1,"request_latency_milliseconds":1028.6039,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":873,"outcome":"success"},{"sequence":2,"request_latency_milliseconds":919.3544,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":3,"request_latency_milliseconds":883.252,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":4,"request_latency_milliseconds":1036.6006,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":873,"outcome":"success"},{"sequence":5,"request_latency_milliseconds":883.3145,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":6,"request_latency_milliseconds":937.7458,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":7,"request_latency_milliseconds":886.0139,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":8,"request_latency_milliseconds":884.1511,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"},{"sequence":9,"request_latency_milliseconds":910.4994,"queue_wait_milliseconds":0,"request_bytes":801,"output_bytes":872,"outcome":"success"}]},"response_authority":"untrusted_proposal_only","deterministic_commit_validation_required":true,"client_side_serialization_enforced":true,"external_server_startup_configuration_verified":false,"external_server_startup_configuration_claim":"unverified_external_startup_configuration_no_cache_batching_or_speculation_claim"}""";
    private const string ExpectedReportDigest = "845c429f3d1f90da13111affb2adf5480e6bbb72aa8a95e04de07730080dadce";
    private const string GoldenReport = """{"schema_version":"snow_globe_local_premium_comparison/v1","comparison_contract_id":"5ca8f57d8dd4fb5de18a1179c1a8acf25eef944ac7350f30514f097932d95227","status":"insufficient_live_premium_evidence","local":{"evidence_digest_sha256":"961b54b7d8cfb2aead566579499adb3aa21f1d85bfbe0b7c6fc504a8adc40e0d","benchmark_contract_id":"9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0","workload_identity":"snow-globe-ollama-qwen3.5-4b-frozen-cell-v1","model_identity":"qwen3.5-4b","quantization_identity":"q4_k_m","prompt_identity":"snow-globe-proposal-v1","response_schema_identity":"snow-globe-action-proposal-schema-v1","metrics_sample_schema":"societies_local_model_metrics_sample/v1","metrics_sample_digest_sha256":"e2ab348567fbc35fe6e7dbf850c4ec2fabbe1136142a81855758e1d2ee01401b","context_window_tokens":4096,"output_token_limit":96,"warmup_request_count":1,"measured_request_count":10,"static_vram_mib":6351,"observed_sampled_peak_vram_mib":6432,"maximum_allowed_observed_vram_mib":6963.2,"vram_sample_count":179,"p50_latency_milliseconds":887.0709,"p95_latency_milliseconds":1036.6006,"p99_latency_milliseconds":1036.6006,"maximum_request_latency_milliseconds":1036.6006,"throughput_tokens_per_second":57.63771812273952,"failure_count":0,"fallback_count":0,"queue_bound":1,"peak_queue_depth":1,"peak_request_bytes":801,"peak_output_bytes":873,"peak_queue_wait_milliseconds":0.5206,"total_queue_wait_milliseconds":0.5206},"premium":null,"premium_cost":null,"performance_delta":null,"missing_gate_codes":["live_premium_profile_not_approved","live_premium_evidence_absent","live_premium_operational_metrics_absent","live_premium_cost_evidence_absent"],"claim_limitations":["frozen_local_cell_only","local_compatibility_fit_and_latency_only","sampled_vram_does_not_prove_unsampled_transient_peak","no_live_premium_evidence","no_quality_or_intelligence_conclusion","no_cost_conclusion","no_winner_selection"],"report_payload_digest_sha256":"b9b9396f775555b4da401b56d3a57a86937336dab0e1c42e6305708ba3212e0c"}""";

    [Fact]
    public void ExactFrozenArtifact_ProducesGoldenDetachedCanonicalReport()
    {
        byte[] artifact = ArtifactBytes();
        Assert.Equal(3_618, artifact.Length);
        Assert.Equal(ExpectedArtifactDigest, Sha256(artifact));

        LocalPremiumComparisonReport first = LocalPremiumComparison.Evaluate(artifact);
        LocalPremiumComparisonReport second = LocalPremiumComparison.Evaluate(artifact);

        Assert.Equal("insufficient_live_premium_evidence", first.Status);
        Assert.Equal(GoldenReport, first.CanonicalJson);
        Assert.Equal(Encoding.UTF8.GetBytes(GoldenReport), first.CanonicalUtf8.ToArray());
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(ExpectedReportDigest, first.CanonicalDigestSha256);
        Assert.Equal(ExpectedReportDigest, Sha256(first.CanonicalUtf8.Span));
        Assert.InRange(first.CanonicalUtf8.Length, 1, LocalPremiumComparison.MaximumReportBytes);
        Assert.NotEqual(0xef, first.CanonicalUtf8.Span[0]);
        Assert.Equal((byte)'}', first.CanonicalUtf8.Span[^1]);
    }

    [Fact]
    public void EveryFrozenBindingCategoryMutation_FailsClosed()
    {
        string valid = Encoding.UTF8.GetString(ArtifactBytes());
        string[] mutations =
        {
            ReplaceOnce(valid, "societies_ollama_benchmark_evidence/v1", "societies_ollama_benchmark_evidence/v2"),
            ReplaceOnce(valid, "9f0e0988984d70e6448615517c7dab1c607c5604520e1b26a5933680ebcd57b0", new string('f', 64)),
            ReplaceOnce(valid, "http://127.0.0.1:11435/", "http://127.0.0.1:11434/"),
            ReplaceOnce(valid, "\"model_identity\":\"qwen3.5-4b\"", "\"model_identity\":\"other-model\""),
            ReplaceOnce(valid, "\"runtime_model_reference\":\"qwen3.5:4b\"", "\"runtime_model_reference\":\"qwen3.5:8b\""),
            ReplaceOnce(valid, "2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd", new string('a', 64)),
            ReplaceOnce(valid, "\"artifact_size_bytes\":3389983735", "\"artifact_size_bytes\":3389983736"),
            ReplaceOnce(valid, "\"artifact_format\":\"gguf\"", "\"artifact_format\":\"other\""),
            ReplaceOnce(valid, "\"model_family\":\"qwen35\"", "\"model_family\":\"other\""),
            ReplaceOnce(valid, "\"parameter_count\":4659865088", "\"parameter_count\":4659865089"),
            ReplaceOnce(valid, "\"runtime_parameter_size\":\"4.7B\"", "\"runtime_parameter_size\":\"4.8B\""),
            ReplaceOnce(valid, "\"quantization_level\":\"Q4_K_M\"", "\"quantization_level\":\"Q8_0\""),
            ReplaceOnce(valid, "11d7729cb18bb4876ad91a14fbe9ba3b6985eaabc3475a62d47d874be24a9b54", new string('b', 64)),
            ReplaceOnce(valid, "8221f288cc777249a019031eb11cb75db25f3ba919e3d53836f9447366e0dfb6", new string('c', 64)),
            ReplaceOnce(valid, "GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19", "GPU-00000000-0000-0000-0000-000000000000"),
            ReplaceOnce(valid, "\"gpu_total_vram_mib\":8192", "\"gpu_total_vram_mib\":8193"),
            ReplaceOnce(valid, "\"ollama_process_identity\":\"ollama-v0.32.14-sha11d7729c\"", "\"ollama_process_identity\":\"other\""),
            ReplaceOnce(valid, "\"ollama_process_id\":20808", "\"ollama_process_id\":20809"),
            ReplaceOnce(valid, "aggregate_gpu_used_mib_exact_uuid_pid_liveness_bound", "other_scope"),
            ReplaceOnce(valid, "bounded_samples_do_not_guarantee_unsampled_transient_peak", "other_limit"),
            ReplaceOnce(valid, "\"context_window_tokens\":4096", "\"context_window_tokens\":4097"),
            ReplaceOnce(valid, "\"output_token_limit\":96", "\"output_token_limit\":97"),
            ReplaceOnce(valid, "\"warmup_request_count\":1", "\"warmup_request_count\":2"),
            ReplaceOnce(valid, "\"measured_request_count\":10", "\"measured_request_count\":11"),
            ReplaceOnce(valid, "\"static_vram_mib\":6351", "\"static_vram_mib\":6352"),
            ReplaceOnce(valid, "\"observed_sampled_peak_vram_mib\":6432", "\"observed_sampled_peak_vram_mib\":6433"),
            ReplaceOnce(valid, "\"maximum_allowed_observed_vram_mib\":6963.2", "\"maximum_allowed_observed_vram_mib\":6963.3"),
            ReplaceOnce(valid, "\"vram_sample_count\":179", "\"vram_sample_count\":180"),
            ReplaceOnce(valid, "\"p50_latency_milliseconds\":887.0709", "\"p50_latency_milliseconds\":887.071"),
            ReplaceOnce(valid, "\"p95_latency_milliseconds\":1036.6006", "\"p95_latency_milliseconds\":1036.6007"),
            ReplaceOnce(valid, "\"p99_latency_milliseconds\":1036.6006", "\"p99_latency_milliseconds\":1036.6007"),
            ReplaceOnce(valid, "\"maximum_request_latency_milliseconds\":1036.6006", "\"maximum_request_latency_milliseconds\":1036.6007"),
            ReplaceOnce(valid, "\"throughput_tokens_per_second\":57.63771812273952", "\"throughput_tokens_per_second\":58"),
            ReplaceOnce(valid, "\"failure_count\":0", "\"failure_count\":1"),
            ReplaceOnce(valid, "\"fallback_count\":0", "\"fallback_count\":1"),
            ReplaceOnce(valid, "\"queue_bound\":1", "\"queue_bound\":2"),
            ReplaceOnce(valid, "\"peak_queue_depth\":1", "\"peak_queue_depth\":2"),
            ReplaceOnce(valid, "\"peak_request_bytes\":801", "\"peak_request_bytes\":802"),
            ReplaceOnce(valid, "\"peak_output_bytes\":873", "\"peak_output_bytes\":874"),
            ReplaceOnce(valid, "\"peak_queue_wait_milliseconds\":0.5206", "\"peak_queue_wait_milliseconds\":0.5207"),
            ReplaceOnce(valid, "\"total_queue_wait_milliseconds\":0.5206", "\"total_queue_wait_milliseconds\":0.5207"),
            ReplaceOnce(valid, "e2ab348567fbc35fe6e7dbf850c4ec2fabbe1136142a81855758e1d2ee01401b", new string('d', 64)),
            ReplaceOnce(valid, "societies_local_model_metrics_sample/v1", "societies_local_model_metrics_sample/v2"),
            ReplaceOnce(valid, "\"sequence\":0", "\"sequence\":10"),
            ReplaceOnce(valid, "\"outcome\":\"success\"", "\"outcome\":\"failure\""),
            ReplaceOnce(valid, "\"response_authority\":\"untrusted_proposal_only\"", "\"response_authority\":\"trusted\""),
            ReplaceOnce(valid, "\"deterministic_commit_validation_required\":true", "\"deterministic_commit_validation_required\":false"),
            ReplaceOnce(valid, "\"client_side_serialization_enforced\":true", "\"client_side_serialization_enforced\":false"),
            ReplaceOnce(valid, "\"external_server_startup_configuration_verified\":false", "\"external_server_startup_configuration_verified\":true"),
            ReplaceOnce(valid, OllamaBenchmarkRunner.ExternalStartupConfigurationClaim, "other_claim")
        };

        Assert.All(mutations, mutation => AssertAllowlistedFailure(Encoding.UTF8.GetBytes(mutation)));
    }

    [Fact]
    public void MalformedOversizedDeepDuplicateUnknownNonCanonicalNonFiniteAndCountMutations_FailClosed()
    {
        string valid = Encoding.UTF8.GetString(ArtifactBytes());
        string lastSample = ",{\"sequence\":9,\"request_latency_milliseconds\":910.4994,\"queue_wait_milliseconds\":0,\"request_bytes\":801,\"output_bytes\":872,\"outcome\":\"success\"}";
        string extraSample = ",{\"sequence\":10,\"request_latency_milliseconds\":910.4994,\"queue_wait_milliseconds\":0,\"request_bytes\":801,\"output_bytes\":872,\"outcome\":\"success\"}";
        byte[][] invalid =
        {
            Array.Empty<byte>(),
            new byte[LocalPremiumComparison.MaximumLocalCellBytes + 1],
            Encoding.UTF8.GetBytes(valid[..^1]),
            Encoding.UTF8.GetBytes(valid.Replace("{\"schema_version\":", "{\"schema_version\":\"duplicate\",\"schema_version\":", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace("\"sequence\":0", "\"sequence\":0,\"sequence\":0", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace("{\"schema_version\":", "{\"unknown\":0,\"schema_version\":", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace("{\"schema_version\":", "{\"deep\":[[[[[[[[[0]]]]]]]]],\"schema_version\":", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(" " + valid),
            Encoding.UTF8.GetBytes(valid + "\n"),
            Encoding.UTF8.GetBytes(valid.Replace("887.0709", "NaN", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace("887.0709", "1e9999", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace(lastSample, string.Empty, StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(valid.Replace(lastSample + "]}", lastSample + extraSample + "]}", StringComparison.Ordinal))
        };

        Assert.All(invalid, AssertAllowlistedFailure);
    }

    [Fact]
    public async Task InputAndReportBytesAreDetached_AndConcurrentEvaluationIsStable()
    {
        byte[] callerOwned = ArtifactBytes();
        LocalPremiumComparisonReport report = LocalPremiumComparison.Evaluate(callerOwned);
        Array.Fill(callerOwned, (byte)0);
        Assert.Equal(GoldenReport, report.CanonicalJson);

        ReadOnlyMemory<byte> exposedCopy = report.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(exposedCopy, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal(Encoding.UTF8.GetBytes(GoldenReport), report.CanonicalUtf8.ToArray());

        byte[] stableInput = ArtifactBytes();
        Task<string>[] calls = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => LocalPremiumComparison.Evaluate(stableInput).CanonicalDigestSha256))
            .ToArray();
        string[] digests = await Task.WhenAll(calls);
        Assert.All(digests, digest => Assert.Equal(ExpectedReportDigest, digest));
    }

    [Fact]
    public void AbsentAndOfflineFixtureEvidenceRemainInsufficientAndCannotChangeTheReport()
    {
        byte[] artifact = ArtifactBytes();
        LocalPremiumComparisonReport absent = LocalPremiumComparison.Evaluate(
            artifact,
            AbsentPremiumComparisonEvidenceAdapter.Instance);
        LocalPremiumComparisonReport fixture = LocalPremiumComparison.Evaluate(
            artifact,
            new OfflineFixturePremiumComparisonEvidenceAdapter());
        CountingOfflineAdapter spy = new();
        LocalPremiumComparisonReport spied = LocalPremiumComparison.Evaluate(artifact, spy);

        Assert.Equal("insufficient_live_premium_evidence", absent.Status);
        Assert.Equal(absent.CanonicalJson, fixture.CanonicalJson);
        Assert.Equal(absent.CanonicalJson, spied.CanonicalJson);
        Assert.Equal(1, spy.InspectionCount);
        Assert.Throws<LocalPremiumComparisonException>(() =>
            LocalPremiumComparison.Evaluate(artifact, new InvalidClassificationAdapter()));
    }

    [Fact]
    public void PublicAndEvidenceSeamSurfacesExposeNoTransportCredentialJournalOrProviderCapability()
    {
        MethodInfo[] methods = typeof(LocalPremiumComparison).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(2, methods.Length);
        Assert.All(methods, method =>
        {
            Assert.Equal("Evaluate", method.Name);
            Assert.Equal(typeof(LocalPremiumComparisonReport), method.ReturnType);
            Assert.All(method.GetParameters(), parameter => Assert.Equal(typeof(ReadOnlyMemory<byte>), parameter.ParameterType));
            Assert.DoesNotContain(method.GetParameters(), parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
            Assert.False(typeof(Task).IsAssignableFrom(method.ReturnType));
        });
        Assert.Equal(new[] { 1, 2 }, methods.Select(static method => method.GetParameters().Length).Order());

        Type[] ownedTypes =
        {
            typeof(LocalPremiumComparison),
            typeof(LocalPremiumComparisonReport),
            typeof(IPremiumComparisonEvidenceAdapter),
            typeof(AbsentPremiumComparisonEvidenceAdapter),
            typeof(OfflineFixturePremiumComparisonEvidenceAdapter),
            typeof(PremiumComparisonEvidenceSnapshot)
        };
        string surface = string.Join('|', ownedTypes.SelectMany(type =>
                type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(member => member.ToString()));
        foreach (string forbidden in new[] { "HttpClient", "HttpRequest", "Stream", "Socket", "Credential", "Journal", "Provider", "Financial", "World" })
        {
            Assert.DoesNotContain(forbidden, surface, StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(typeof(IPremiumCognitionProvider).IsAssignableFrom(typeof(OfflineFixturePremiumComparisonEvidenceAdapter)));
        Assert.False(typeof(IFinancialJournal).IsAssignableFrom(typeof(OfflineFixturePremiumComparisonEvidenceAdapter)));
    }

    [Fact]
    public void ReportHasExactNullComparisonFieldsFixedGatesAndNoForbiddenFieldsOrRawData()
    {
        LocalPremiumComparisonReport report = LocalPremiumComparison.Evaluate(ArtifactBytes());
        using JsonDocument document = JsonDocument.Parse(report.CanonicalUtf8);
        JsonElement root = document.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("premium").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("premium_cost").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("performance_delta").ValueKind);
        Assert.False(root.TryGetProperty("premium_profile", out _));
        Assert.False(root.TryGetProperty("premium_metrics", out _));
        Assert.Equal(4, root.GetProperty("missing_gate_codes").GetArrayLength());
        Assert.Equal(7, root.GetProperty("claim_limitations").GetArrayLength());

        HashSet<string> propertyNames = new(StringComparer.Ordinal);
        CollectPropertyNames(root, propertyNames);
        foreach (string forbidden in new[]
        {
            "endpoint", "credential", "credentials", "byok", "account", "auth_nonce", "raw_prompt",
            "raw_response", "proposal", "world", "path", "host_id", "timestamp", "winner", "quality", "intelligence"
        })
        {
            Assert.DoesNotContain(forbidden, propertyNames);
        }

        Assert.DoesNotContain("http://", report.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GPU-39cacb24", report.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("20808", report.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("agent_id=", report.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoRunOverloadPublishesValidatedLocalCognitionQualityButNoPremiumDeltaOrConclusion()
    {
        OllamaRecordingExecutionArtifact recording = await RecordingArtifact();
        LocalPremiumComparisonReport report = LocalPremiumComparison.Evaluate(ArtifactBytes(), recording.CanonicalUtf8);
        Assert.Equal("4ca3205e6c2eb04d76dd413af6115fe805d0621bb0ec97dc1fb8af0cd79eb9c4", report.CanonicalDigestSha256);
        Assert.Equal(OllamaRecordingExecutionArtifactModule.SchemaVersion, recording.SchemaVersion);

        Assert.Equal("insufficient_live_premium_evidence", report.Status);
        Assert.InRange(report.CanonicalUtf8.Length, 1, LocalPremiumComparison.MaximumReportBytes);
        Assert.Equal(report.CanonicalDigestSha256, Sha256(report.CanonicalUtf8.Span));
        using JsonDocument document = JsonDocument.Parse(report.CanonicalUtf8);
        JsonElement root = document.RootElement;
        Assert.Equal("snow_globe_local_premium_comparison/v4", root.GetProperty("schema_version").GetString());
        Assert.Equal("68fb8075422b21d5a762787368948c97a2c9e891e6089bdd1059c18df3a8877b",
            root.GetProperty("comparison_contract_id").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("premium").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("premium_cost").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("performance_delta").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("quality_delta").ValueKind);
        JsonElement local = root.GetProperty("local");
        Assert.True(local.TryGetProperty("benchmark_run", out JsonElement benchmarkRun));
        Assert.True(local.TryGetProperty("recording_run", out JsonElement recordingRun));
        Assert.True(local.TryGetProperty("cognition_quality", out JsonElement cognitionQuality));
        Assert.Equal(ExpectedArtifactDigest, benchmarkRun.GetProperty("evidence_digest_sha256").GetString());
        Assert.False(recordingRun.GetProperty("same_run_as_benchmark").GetBoolean());
        Assert.Equal(recording.CanonicalDigestSha256, recordingRun.GetProperty("artifact_digest_sha256").GetString());
        Assert.Equal(recording.ScoreSummaryDigestSha256, recordingRun.GetProperty("score_summary_digest_sha256").GetString());
        Assert.Equal(recording.ScoreSummary!.CanonicalJson, cognitionQuality.GetRawText());
        Assert.Contains("local_benchmark_and_recording_are_separate_runs", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
        Assert.Contains("no_cross_run_latency_per_quality_or_price_conclusion", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
        Assert.Contains("validated_local_recording_artifact_v6_only", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
        Assert.Contains("normalized_proposals_retained_but_not_compared_by_this_legacy_report", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
        Assert.DoesNotContain("validated_local_recording_artifact_v5_only", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
        Assert.DoesNotContain("validated_local_recording_artifact_v4_only", root.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));

        Assert.DoesNotContain("winner\"", report.CanonicalJson, StringComparison.OrdinalIgnoreCase);
    }

    [HistoricalEvidenceFact(OllamaRecordingExecutionArtifactModule.LegacyRelativeArtifactPath)]
    public void TwoRunOverloadAcceptsTheImmutableHistoricalV4ArtifactWithoutMutation()
    {
        byte[] historical = ReadHistoricalV4Artifact();
        try
        {
            Assert.Equal("fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e", Sha256(historical));
            LocalPremiumComparisonReport report = LocalPremiumComparison.Evaluate(ArtifactBytes(), historical);
            using JsonDocument document = JsonDocument.Parse(report.CanonicalUtf8);
            Assert.Equal("insufficient_live_premium_evidence", report.Status);
            Assert.Equal(8_916, report.CanonicalUtf8.Length);
            Assert.Equal("19f7053418471c8c70bdb9fffbfcca042f5bd87c24796a28227a672558990e56", report.CanonicalDigestSha256);
            Assert.Equal("3c3ff4a3e97344afb80d2a6283827e3d846c73e0b7730765c5d09601db6d4acc",
                document.RootElement.GetProperty("report_payload_digest_sha256").GetString());
            Assert.Equal("snow_globe_local_premium_comparison/v2", document.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("835079e77e13aa1b2153c2badc9c23042099d40e1d09b5112c276bc9b61ada68",
                document.RootElement.GetProperty("comparison_contract_id").GetString());
            Assert.Contains("validated_local_recording_artifact_v4_only",
                document.RootElement.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
            Assert.DoesNotContain("validated_local_recording_artifact_v5_only",
                document.RootElement.GetProperty("claim_limitations").EnumerateArray().Select(static item => item.GetString()));
            Assert.Equal("fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e",
                document.RootElement.GetProperty("local").GetProperty("recording_run").GetProperty("artifact_digest_sha256").GetString());
            Assert.Equal("fecf71cbe8cc268dadb603d29735a816bc0152ccc79b4ea44c5a91d7e7616d3e", Sha256(historical));
        }
        finally { CryptographicOperations.ZeroMemory(historical); }
    }

    [Fact]
    public async Task TwoRunOverloadRejectsV1ThroughV3TerminalMalformedOversizedAndDetachedMutations()
    {
        OllamaRecordingExecutionArtifact recording = await RecordingArtifact();
        byte[] benchmark = ArtifactBytes();
        byte[] stableRecording = recording.CanonicalUtf8.ToArray();
        LocalPremiumComparisonReport report = LocalPremiumComparison.Evaluate(benchmark, stableRecording);
        benchmark[0] = 0; stableRecording[0] = 0;
        Assert.Equal((byte)'{', report.CanonicalUtf8.Span[0]);

        foreach (int version in new[] { 1, 2, 3 })
        {
            string recordingJson = Encoding.UTF8.GetString(recording.CanonicalUtf8.Span);
            byte[] historical = Encoding.UTF8.GetBytes(recordingJson.Replace(
                OllamaRecordingExecutionArtifactModule.SchemaVersion,
                $"snow_globe_ollama_recording_execution_artifact/v{version}", StringComparison.Ordinal));
            Assert.Equal("local_recording_artifact_rejected", Assert.Throws<LocalPremiumComparisonException>(() => LocalPremiumComparison.Evaluate(ArtifactBytes(), historical)).Code);
        }
        byte[] future = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(recording.CanonicalUtf8.Span).Replace(
            OllamaRecordingExecutionArtifactModule.SchemaVersion,
            "snow_globe_ollama_recording_execution_artifact/v7", StringComparison.Ordinal));
        Assert.Equal("local_recording_artifact_rejected", Assert.Throws<LocalPremiumComparisonException>(() => LocalPremiumComparison.Evaluate(ArtifactBytes(), future)).Code);

        Assert.Equal("local_recording_artifact_required", Assert.Throws<LocalPremiumComparisonException>(() => LocalPremiumComparison.Evaluate(ArtifactBytes(), ReadOnlyMemory<byte>.Empty)).Code);
        Assert.Equal("local_recording_artifact_too_large", Assert.Throws<LocalPremiumComparisonException>(() => LocalPremiumComparison.Evaluate(ArtifactBytes(), new byte[OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes + 1])).Code);
        Assert.Equal("local_recording_artifact_rejected", Assert.Throws<LocalPremiumComparisonException>(() => LocalPremiumComparison.Evaluate(ArtifactBytes(), "{}"u8.ToArray())).Code);
    }

    private static void CollectPropertyNames(JsonElement value, ISet<string> names)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                names.Add(property.Name);
                CollectPropertyNames(property.Value, names);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) CollectPropertyNames(item, names);
        }
    }

    private static void AssertAllowlistedFailure(byte[] bytes)
    {
        LocalPremiumComparisonException exception = Assert.Throws<LocalPremiumComparisonException>(() =>
            LocalPremiumComparison.Evaluate(bytes));
        Assert.True(LocalPremiumComparisonErrors.IsAllowlisted(exception.Code), exception.Code);
        Assert.NotEqual("insufficient_live_premium_evidence", exception.Code);
    }

    private static string ReplaceOnce(string value, string oldValue, string newValue)
    {
        int index = value.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.True(index >= 0, oldValue);
        return string.Concat(value.AsSpan(0, index), newValue, value.AsSpan(index + oldValue.Length));
    }

    private static byte[] ArtifactBytes() => Encoding.UTF8.GetBytes(FrozenArtifact);

    private static byte[] ReadHistoricalV4Artifact()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(repositoryRoot, OllamaRecordingExecutionArtifactModule.LegacyRelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllBytes(path);
    }

    private static async Task<OllamaRecordingExecutionArtifact> RecordingArtifact()
    {
        const string root = @"C:\offline-local-premium-comparison-recording";
        long startTicks = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;
        InMemoryOllamaRecordingArtifactStore store = new();
        OllamaRecordingCompositionTests.TestTransportFactory factory = new(TestWrappers.Valid());
        SnowGlobePinnedOllamaRecordingModule inner = new(new ComparisonClock(), factory);
        SnowGlobeOllamaRecordingCompositionModule module = new(root, inner, store);
        return (await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, startTicks), "comparison-recording-v1"))).Artifact!;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 12 && current is not null; depth++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "CURRENT_BUILD.md"))
                && File.Exists(Path.Combine(current.FullName, "labs", "Societies.SnowGlobe", "Societies.SnowGlobe.csproj")))
                return current.FullName;
        }
        throw new InvalidOperationException("repository_root_not_found");
    }

    private sealed class ComparisonClock : ICognitionQualityRecordingSessionClock { public long NowMilliseconds => 1; }

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed class CountingOfflineAdapter : IPremiumComparisonEvidenceAdapter
    {
        public int InspectionCount { get; private set; }

        public PremiumComparisonEvidenceSnapshot Inspect()
        {
            InspectionCount++;
            return new(PremiumComparisonEvidenceClassification.OfflineFixture);
        }
    }

    private sealed class InvalidClassificationAdapter : IPremiumComparisonEvidenceAdapter
    {
        public PremiumComparisonEvidenceSnapshot Inspect() => new((PremiumComparisonEvidenceClassification)999);
    }
}
