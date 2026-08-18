using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Societies.SnowGlobe;
using Societies.SnowGlobe.BenchmarkCli;
using Xunit;

namespace Societies.SnowGlobe.BenchmarkCli.Tests;

public sealed class PinnedBenchmarkCliTests : IDisposable
{
    private readonly List<string> _scratchDirectories = new();

    [Fact]
    public void PinnedPlanAndRuntimeMatchTheFrozenCell()
    {
        LocalModelBenchmarkPlan plan = PinnedBenchmarkContract.CreatePlan();
        OllamaRuntimeAuthorization runtime = PinnedBenchmarkContract.CreateRuntimeAuthorization(4242);

        Assert.Equal("http://127.0.0.1:11435/", plan.Endpoint);
        Assert.Equal("qwen3.5-4b", plan.ModelIdentity);
        Assert.Equal("q4_k_m", plan.QuantizationIdentity);
        Assert.Equal(4096, plan.ContextWindowTokens);
        Assert.Equal(16 * 1024, plan.Budgets.RequestBytes);
        Assert.Equal(8 * 1024, plan.Budgets.OutputBytes);
        Assert.Equal(1, plan.Budgets.QueueDepth);
        Assert.Equal(1, plan.WarmupRequestCount);
        Assert.Equal(10, plan.MeasuredRequestCount);
        Assert.False(plan.AutomaticRetries);
        Assert.False(plan.FollowRedirects);
        Assert.False(plan.CredentialsPermitted);
        Assert.Equal("qwen3.5:4b", runtime.RuntimeModelReference);
        Assert.Equal("2a654d98e6fba55d452b7043684e9b57a947e393bbffa62485a7aac05ee4eefd", runtime.ArtifactDigestSha256);
        Assert.Equal(3_389_983_735, runtime.ArtifactSizeBytes);
        Assert.NotEqual(3_389_971_840, runtime.ArtifactSizeBytes); // Model-layer blob size is not the tags artifact size.
        Assert.NotEqual(3_389_984_444, runtime.ArtifactSizeBytes); // Store total is separate evidence.
        Assert.Equal("gguf", runtime.ArtifactFormat);
        Assert.Equal("qwen35", runtime.ModelFamily);
        Assert.Equal("4.7B", runtime.ParameterSize);
        Assert.Equal("Q4_K_M", runtime.QuantizationLevel);
        Assert.Equal(96, OllamaBenchmarkRunner.OutputTokenLimit);
    }

    [Fact]
    public async Task ProbeBindsExactPidStartPathDigestAndGpuUuid()
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        FakeInspector inspector = new(new PinnedProcessSnapshot(processId, runtimePath, startTicks, false));
        FakeRunner runner = new(new BoundedProcessResult(
            0,
            $"{PinnedBenchmarkContract.GpuUuid}, {PinnedBenchmarkContract.GpuTotalVramMiB}, 6357\r\n",
            string.Empty));
        PinnedNvidiaSmiVramProbe probe = PinnedNvidiaSmiVramProbe.CreateForTesting(
            processId, runtimePath, startTicks, inspector, runner);
        LocalModelBenchmarkPlan plan = PinnedBenchmarkContract.CreatePlan();
        OllamaRuntimeAuthorization runtime = PinnedBenchmarkContract.CreateRuntimeAuthorization(processId);

        LocalModelVramReading reading = await probe.ReadAsync(plan, runtime, CancellationToken.None);

        Assert.Equal(6357, reading.UsedMiB);
        Assert.Equal(plan.ContractId, reading.ContractId);
        Assert.Equal(runtime.ArtifactDigestSha256, reading.ArtifactDigestSha256);
        Assert.Equal(runtime.OllamaProcessIdentity, reading.OllamaProcessIdentity);
        Assert.Equal(processId, reading.OllamaProcessId);
        Assert.Equal(PinnedBenchmarkContract.NvidiaSmiPath, runner.ExecutablePath);
        Assert.Equal(new[]
        {
            "--query-gpu=uuid,memory.total,memory.used",
            "--format=csv,noheader,nounits"
        }, runner.Arguments);
        Assert.Equal(4096, runner.MaximumCharactersPerStream);
    }

    [Theory]
    [InlineData("GPU-wrong, 8192, 1000", "nvidia_smi_gpu_reading_invalid")]
    [InlineData("GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19, 16384, 1000", "nvidia_smi_gpu_reading_invalid")]
    [InlineData("GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19, 8192, 9000", "nvidia_smi_gpu_reading_invalid")]
    [InlineData("GPU-39cacb24-199b-3985-4cbf-c55b3b84ed19, 8192, 1000\nGPU-other, 8192, 1", "nvidia_smi_gpu_cardinality_invalid")]
    public async Task ProbeRejectsAmbiguousOrMismatchedGpuEvidence(string output, string expectedCode)
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        PinnedNvidiaSmiVramProbe probe = PinnedNvidiaSmiVramProbe.CreateForTesting(
            processId,
            runtimePath,
            startTicks,
            new FakeInspector(new PinnedProcessSnapshot(processId, runtimePath, startTicks, false)),
            new FakeRunner(new BoundedProcessResult(0, output, string.Empty)));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            probe.ReadAsync(
                PinnedBenchmarkContract.CreatePlan(),
                PinnedBenchmarkContract.CreateRuntimeAuthorization(processId),
                CancellationToken.None).AsTask());

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task ProbeRejectsPidReuseBeforeStartingNvidiaSmi()
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        FakeRunner runner = new(new BoundedProcessResult(0, $"{PinnedBenchmarkContract.GpuUuid}, 8192, 1000", string.Empty));
        PinnedNvidiaSmiVramProbe probe = PinnedNvidiaSmiVramProbe.CreateForTesting(
            processId,
            runtimePath,
            startTicks,
            new FakeInspector(new PinnedProcessSnapshot(processId, runtimePath, startTicks + 1, false)),
            runner);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            probe.ReadAsync(
                PinnedBenchmarkContract.CreatePlan(),
                PinnedBenchmarkContract.CreateRuntimeAuthorization(processId),
                CancellationToken.None).AsTask());

        Assert.Equal("ollama_process_identity_changed", exception.Code);
        Assert.Null(runner.ExecutablePath);
    }

    [Theory]
    [InlineData(true, 0, "same")]
    [InlineData(false, 1, "same")]
    [InlineData(false, 0, "changed")]
    public async Task ProbeRejectsProcessExitReuseOrPathChangeDuringNvidiaSmi(
        bool postQueryExited,
        long postQueryStartTickDelta,
        string postQueryPathMode)
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        string postQueryPath = postQueryPathMode == "same"
            ? runtimePath
            : Path.Combine(Path.GetDirectoryName(runtimePath)!, "other-ollama.exe");
        SequenceInspector inspector = new(
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false),
            new PinnedProcessSnapshot(
                processId,
                postQueryPath,
                startTicks + postQueryStartTickDelta,
                postQueryExited));
        FakeRunner runner = new(new BoundedProcessResult(
            0,
            $"{PinnedBenchmarkContract.GpuUuid}, 8192, 6357",
            string.Empty));
        PinnedNvidiaSmiVramProbe probe = PinnedNvidiaSmiVramProbe.CreateForTesting(
            processId,
            runtimePath,
            startTicks,
            inspector,
            runner);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            probe.ReadAsync(
                PinnedBenchmarkContract.CreatePlan(),
                PinnedBenchmarkContract.CreateRuntimeAuthorization(processId),
                CancellationToken.None).AsTask());

        Assert.Equal("ollama_process_identity_changed", exception.Code);
        Assert.Equal(2, inspector.ReadCount);
        Assert.NotNull(runner.ExecutablePath);
    }

    [Fact]
    public async Task ConnectionOwnerResolverAcceptsExactlyOneServerSideEstablishedRowForPinnedProcess()
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        OllamaLoopbackConnection connection = CreateExactConnection();
        SequenceInspector inspector = new(
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false),
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false));
        FakeWindowsTcpOwnerTable table = new(
            // The client-side row has the same two endpoints but must not be accepted.
            new WindowsTcpOwnerRow(
                connection.ClientAddress,
                connection.ClientPort,
                connection.ServerAddress,
                connection.ServerPort,
                5,
                processId),
            CreateExactOwnerRow(connection, processId));
        WindowsOllamaLoopbackConnectionOwnerResolver resolver =
            WindowsOllamaLoopbackConnectionOwnerResolver.CreateForTesting(
                processId, runtimePath, startTicks, inspector, table);

        int resolvedProcessId = await resolver.ResolveServerProcessIdAsync(
            connection,
            CancellationToken.None);

        Assert.Equal(processId, resolvedProcessId);
        Assert.Equal(2, inspector.ReadCount);
        Assert.Equal(1, table.ReadCount);
    }

    [Theory]
    [InlineData("missing", "ollama_connection_owner_missing")]
    [InlineData("wrong-owner", "ollama_connection_owner_mismatch")]
    [InlineData("duplicate", "ollama_connection_owner_cardinality_invalid")]
    public async Task ConnectionOwnerResolverFailsClosedForMissingWrongOrAmbiguousOwner(
        string mode,
        string expectedCode)
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        OllamaLoopbackConnection connection = CreateExactConnection();
        WindowsTcpOwnerRow exact = CreateExactOwnerRow(connection, processId);
        WindowsTcpOwnerRow[] rows = mode switch
        {
            "missing" => Array.Empty<WindowsTcpOwnerRow>(),
            "wrong-owner" => new[] { exact with { OwningProcessId = processId + 1 } },
            "duplicate" => new[] { exact, exact },
            _ => throw new InvalidOperationException("Unexpected test mode.")
        };
        SequenceInspector inspector = new(
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false),
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false));
        WindowsOllamaLoopbackConnectionOwnerResolver resolver =
            WindowsOllamaLoopbackConnectionOwnerResolver.CreateForTesting(
                processId,
                runtimePath,
                startTicks,
                inspector,
                new FakeWindowsTcpOwnerTable(rows));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            resolver.ResolveServerProcessIdAsync(connection, CancellationToken.None).AsTask());

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(2, inspector.ReadCount);
    }

    [Theory]
    [InlineData(true, 0, "same")]
    [InlineData(false, 1, "same")]
    [InlineData(false, 0, "changed")]
    public async Task ConnectionOwnerResolverRejectsExitPidReuseOrPathChangeDuringLookup(
        bool postLookupExited,
        long postLookupStartTickDelta,
        string postLookupPathMode)
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        string postLookupPath = postLookupPathMode == "same"
            ? runtimePath
            : Path.Combine(Path.GetDirectoryName(runtimePath)!, "other-ollama.exe");
        OllamaLoopbackConnection connection = CreateExactConnection();
        SequenceInspector inspector = new(
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false),
            new PinnedProcessSnapshot(
                processId,
                postLookupPath,
                startTicks + postLookupStartTickDelta,
                postLookupExited));
        WindowsOllamaLoopbackConnectionOwnerResolver resolver =
            WindowsOllamaLoopbackConnectionOwnerResolver.CreateForTesting(
                processId,
                runtimePath,
                startTicks,
                inspector,
                new FakeWindowsTcpOwnerTable(CreateExactOwnerRow(connection, processId)));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            resolver.ResolveServerProcessIdAsync(connection, CancellationToken.None).AsTask());

        Assert.Equal("ollama_process_identity_changed", exception.Code);
        Assert.Equal(2, inspector.ReadCount);
    }

    [Fact]
    public async Task ConnectionOwnerResolverCancellationIsBoundedAndDoesNotAcceptAReading()
    {
        const int processId = 4242;
        const long startTicks = 638_000_000_000_000_000;
        string runtimePath = Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath);
        SequenceInspector inspector = new(
            new PinnedProcessSnapshot(processId, runtimePath, startTicks, false));
        BlockingWindowsTcpOwnerTable table = new();
        WindowsOllamaLoopbackConnectionOwnerResolver resolver =
            WindowsOllamaLoopbackConnectionOwnerResolver.CreateForTesting(
                processId, runtimePath, startTicks, inspector, table);
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
        Task<int> resolving = resolver.ResolveServerProcessIdAsync(
            CreateExactConnection(),
            cancellation.Token).AsTask();
        await table.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolving.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Equal(1, table.ReadCount);
        Assert.Equal(1, inspector.ReadCount);
    }

    [Fact]
    public void EvidencePath_NormalRepositoryCreatesAndVerifiesOnlyTheBoundedDirectoryChain()
    {
        string root = CreateTestRepositoryRoot();

        string path = PinnedBenchmarkContract.ResolveEvidencePath(root);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, PinnedBenchmarkContract.RelativeEvidencePath)),
            path,
            ignoreCase: true);
        Assert.True(Directory.Exists(Path.Combine(root, "artifacts", "snowglobe", "local-model")));
        PinnedBenchmarkContract.ReverifyEvidencePath(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void EvidencePath_ReparseAncestorFailsClosedWithoutOutsideWrite_WhenSupported()
    {
        string root = CreateTestRepositoryRoot();
        string outside = CreateTestScratchDirectory("outside");
        string artifacts = Path.Combine(root, "artifacts");
        if (!TryCreateDirectorySymbolicLink(artifacts, outside)) return;

        LocalModelBenchmarkException exception = Assert.Throws<LocalModelBenchmarkException>(() =>
            PinnedBenchmarkContract.ResolveEvidencePath(root));

        Assert.Equal("evidence_path_reparse_point_rejected", exception.Code);
        Assert.False(Directory.Exists(Path.Combine(outside, "snowglobe")));
        Assert.False(File.Exists(Path.Combine(outside, "snowglobe", "local-model", Path.GetFileName(PinnedBenchmarkContract.RelativeEvidencePath))));
    }

    [Fact]
    public void Writer_RechecksReparseAncestorImmediatelyBeforeCreate_WhenSupported()
    {
        string root = CreateTestRepositoryRoot();
        string outside = CreateTestScratchDirectory("writer-outside");
        string artifacts = Path.Combine(root, "artifacts");
        if (!TryCreateDirectorySymbolicLink(artifacts, outside)) return;
        OllamaBenchmarkRunResult result = CreateValidRunResult(out LocalModelBenchmarkPlan plan);
        string path = Path.Combine(root, PinnedBenchmarkContract.RelativeEvidencePath);

        LocalModelBenchmarkException exception = Assert.Throws<LocalModelBenchmarkException>(() =>
            CanonicalBenchmarkEvidenceWriter.WriteNew(path, plan, result));

        Assert.Equal("evidence_path_reparse_point_rejected", exception.Code);
        Assert.False(File.Exists(Path.Combine(outside, "snowglobe", "local-model", Path.GetFileName(path))));
    }

    [Fact]
    public void Writer_HeldDirectoryLeasePreventsAncestorRenameAndSwap()
    {
        string root = CreateTestRepositoryRoot();
        string path = PinnedBenchmarkContract.ResolveEvidencePath(root);
        string localModel = Path.GetDirectoryName(path)!;
        string movedLocalModel = localModel + "-moved";
        string outside = CreateTestScratchDirectory("lease-swap-outside");
        Exception? renameFailure = null;
        Exception? swapFailure = null;
        OllamaBenchmarkRunResult result = CreateValidRunResult(out LocalModelBenchmarkPlan plan);

        CanonicalBenchmarkEvidenceWriter.WriteNew(path, plan, result, () =>
        {
            renameFailure = Record.Exception(() => Directory.Move(localModel, movedLocalModel));
            swapFailure = Record.Exception(() => Directory.CreateSymbolicLink(localModel, outside));
        });

        Assert.NotNull(renameFailure);
        Assert.True(renameFailure is IOException or UnauthorizedAccessException);
        Assert.NotNull(swapFailure);
        Assert.True(swapFailure is IOException or UnauthorizedAccessException);
        Assert.True(Directory.Exists(localModel));
        Assert.False(Directory.Exists(movedLocalModel));
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(outside, Path.GetFileName(path))));
    }

    [Fact]
    public void Writer_HeldDirectoryLeaseRejectsInPlaceMutationHandleAndStillWrites()
    {
        const int ErrorSharingViolation = 32;
        string root = CreateTestRepositoryRoot();
        string path = PinnedBenchmarkContract.ResolveEvidencePath(root);
        string localModel = Path.GetDirectoryName(path)!;
        string outside = CreateTestScratchDirectory("lease-in-place-outside");
        bool mutationHandleWasInvalid = false;
        int mutationOpenError = 0;
        OllamaBenchmarkRunResult result = CreateValidRunResult(out LocalModelBenchmarkPlan plan);

        CanonicalBenchmarkEvidenceWriter.WriteNew(path, plan, result, () =>
        {
            using SafeFileHandle mutationHandle = CreateFileForMutationAttempt(
                localModel,
                0x40000000u, // GENERIC_WRITE is required by FSCTL_SET_REPARSE_POINT.
                FileShare.Read | FileShare.Write | FileShare.Delete,
                IntPtr.Zero,
                3,
                0x02000000u | 0x00200000u,
                IntPtr.Zero);
            mutationHandleWasInvalid = mutationHandle.IsInvalid;
            mutationOpenError = Marshal.GetLastWin32Error();
        });

        Assert.True(mutationHandleWasInvalid);
        Assert.Equal(ErrorSharingViolation, mutationOpenError);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(outside, Path.GetFileName(path))));
    }

    [Fact]
    public async Task SystemBoundedProcessRunner_ReturnsExactBoundedStdoutStderrAndNonzeroExit()
    {
        SystemBoundedProcessRunner runner = new();

        BoundedProcessResult bounded = await RunPowerShellAsync(
            runner,
            "[Console]::Out.Write('out'); [Console]::Error.Write('err')",
            maximumCharactersPerStream: 3,
            CancellationToken.None);
        BoundedProcessResult nonzero = await RunPowerShellAsync(
            runner,
            "[Console]::Out.Write('x'); exit 7",
            maximumCharactersPerStream: 8,
            CancellationToken.None);

        Assert.Equal(0, bounded.ExitCode);
        Assert.Equal("out", bounded.StandardOutput);
        Assert.Equal("err", bounded.StandardError);
        Assert.Equal(7, nonzero.ExitCode);
        Assert.Equal("x", nonzero.StandardOutput);
        Assert.Equal(string.Empty, nonzero.StandardError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemBoundedProcessRunner_RejectsOversizedStdoutOrStderr(bool useStandardError)
    {
        SystemBoundedProcessRunner runner = new();
        string stream = useStandardError ? "Error" : "Out";

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            RunPowerShellAsync(
                runner,
                $"[Console]::{stream}.Write('x' * 65)",
                maximumCharactersPerStream: 64,
                CancellationToken.None).AsTask());

        Assert.Equal("nvidia_smi_output_too_large", exception.Code);
    }

    [Fact]
    public async Task SystemBoundedProcessRunner_CancellationTerminatesAttributableChildWithoutOrphan()
    {
        SystemBoundedProcessRunner runner = new();
        string scratch = CreateTestScratchDirectory("process-tree");
        string identityPath = Path.Combine(scratch, "child-identity.txt");
        string escapedIdentityPath = identityPath.Replace("'", "''", StringComparison.Ordinal);
        string pingPath = Path.Combine(Environment.SystemDirectory, "ping.exe");
        string escapedPingPath = pingPath.Replace("'", "''", StringComparison.Ordinal);
        string script =
            $"$child=Start-Process -FilePath '{escapedPingPath}' -ArgumentList '-t','127.0.0.1' -WindowStyle Hidden -PassThru; " +
            $"[IO.File]::WriteAllText('{escapedIdentityPath}', ($child.Id.ToString() + '|' + $child.StartTime.ToUniversalTime().Ticks.ToString())); " +
            "Start-Sleep -Seconds 30";
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        Task<BoundedProcessResult> running = RunPowerShellAsync(
            runner, script, maximumCharactersPerStream: 256, timeout.Token).AsTask();
        await WaitForFileAsync(identityPath, timeout.Token);
        string[] identity = File.ReadAllText(identityPath).Split('|');
        int childProcessId = int.Parse(identity[0], System.Globalization.CultureInfo.InvariantCulture);
        long childStartTicks = long.Parse(identity[1], System.Globalization.CultureInfo.InvariantCulture);

        timeout.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.True(await WaitForExactProcessExitAsync(childProcessId, childStartTicks));
    }

    [Fact]
    public void WriterCreatesCanonicalMetricsOnlyEvidenceWithoutRawModelText()
    {
        OllamaBenchmarkRunResult result = CreateValidRunResult(out LocalModelBenchmarkPlan plan);
        string root = CreateTestRepositoryRoot();
        string path = PinnedBenchmarkContract.ResolveEvidencePath(root);

        CanonicalBenchmarkEvidenceWriter.WriteNew(path, plan, result);
        byte[] first = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Equal(CanonicalBenchmarkEvidenceWriter.SchemaVersion, document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal(10, document.RootElement.GetProperty("canonical_metrics_samples").GetProperty("samples").GetArrayLength());
        Assert.DoesNotContain("secret-marker", Encoding.UTF8.GetString(first), StringComparison.Ordinal);
        Assert.Throws<IOException>(() => CanonicalBenchmarkEvidenceWriter.WriteNew(path, plan, result));
    }

    private static OllamaBenchmarkRunResult CreateValidRunResult(out LocalModelBenchmarkPlan plan)
    {
        plan = PinnedBenchmarkContract.CreatePlan();
        OllamaRuntimeAuthorization runtime = PinnedBenchmarkContract.CreateRuntimeAuthorization(4242);
        LocalModelMetricsSample[] samples = Enumerable.Range(0, 10).Select(index => new LocalModelMetricsSample
        {
            Sequence = index,
            RequestLatencyMilliseconds = 100 + index,
            QueueWaitMilliseconds = 0,
            RequestBytes = 512,
            OutputBytes = 256,
            Outcome = LocalModelMetricsSampleOutcome.Success
        }).ToArray();
        byte[] canonical = LocalModelAdapterPreflight.CreateCanonicalMetricsSamples(samples);
        LocalModelBenchmarkEvidence evidence = new()
        {
            ContractId = plan.ContractId,
            ModelIdentity = plan.ModelIdentity,
            QuantizationIdentity = plan.QuantizationIdentity,
            ContextWindowTokens = plan.ContextWindowTokens,
            StaticVramMiB = 6000,
            PeakVramMiB = 6357,
            WarmupRequestCount = 1,
            MeasuredRequestCount = 10,
            MetricsSampleCount = 10,
            CanonicalMetricsSampleDigestSha256 = Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant(),
            CanonicalMetricsSamplesUtf8 = canonical,
            P50LatencyMilliseconds = 104,
            P95LatencyMilliseconds = 109,
            P99LatencyMilliseconds = 109,
            MaximumRequestLatencyMilliseconds = 109,
            ThroughputTokensPerSecond = 75,
            FailureCount = 0,
            FallbackCount = 0,
            QueueBound = 1,
            PeakQueueDepth = 1,
            PeakRequestBytes = 512,
            PeakOutputBytes = 256,
            PeakQueueWaitMilliseconds = 0,
            TotalQueueWaitMilliseconds = 0
        };
        return new OllamaBenchmarkRunResult(
            evidence,
            new OllamaVerifiedModelProvenance(
                runtime.RuntimeModelReference,
                runtime.ArtifactDigestSha256,
                runtime.ArtifactSizeBytes,
                runtime.ArtifactFormat,
                runtime.ModelFamily,
                runtime.ParameterSize,
                runtime.QuantizationLevel,
                runtime.OllamaProcessIdentity,
                runtime.OllamaProcessId),
            6357,
            6963.2,
            96,
            20,
            true,
            false,
            OllamaBenchmarkRunner.ExternalStartupConfigurationClaim);
    }

    private static OllamaLoopbackConnection CreateExactConnection() => new(
        IPAddress.Loopback,
        53_123,
        IPAddress.Loopback,
        11_435);

    private static WindowsTcpOwnerRow CreateExactOwnerRow(
        OllamaLoopbackConnection connection,
        int processId) =>
        new(
            connection.ServerAddress,
            connection.ServerPort,
            connection.ClientAddress,
            connection.ClientPort,
            5,
            processId);

    public void Dispose()
    {
        foreach (string path in _scratchDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DeleteBoundedScratchDirectory(path);
        }
    }

    private string CreateTestRepositoryRoot()
    {
        string root = CreateTestScratchDirectory("repository");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, "CURRENT_BUILD.md"), "# test repository identity");
        string projectDirectory = Path.Combine(root, "labs", "Societies.SnowGlobe");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Societies.SnowGlobe.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return root;
    }

    private string CreateTestScratchDirectory(string purpose)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "societies-benchmark-cli-tests",
            purpose + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _scratchDirectories.Add(path);
        return path;
    }

    private static void DeleteBoundedScratchDirectory(string path)
    {
        string scratchRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "societies-benchmark-cli-tests")));
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!string.Equals(Directory.GetParent(fullPath)?.FullName, scratchRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a test path outside the bounded scratch root.");
        }
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath)) return;
        DeleteTreeWithoutFollowingReparsePoints(fullPath);
    }

    private static void DeleteTreeWithoutFollowingReparsePoints(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            File.Delete(path);
            return;
        }
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            Directory.Delete(path);
            return;
        }

        foreach (string child in Directory.EnumerateFileSystemEntries(path))
        {
            DeleteTreeWithoutFollowingReparsePoints(child);
        }
        Directory.Delete(path);
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return false;
        }
    }

    private static string PowerShellPath => Path.Combine(
        Environment.SystemDirectory,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private static ValueTask<BoundedProcessResult> RunPowerShellAsync(
        SystemBoundedProcessRunner runner,
        string script,
        int maximumCharactersPerStream,
        CancellationToken cancellationToken) =>
        runner.RunAsync(
            PowerShellPath,
            new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script },
            maximumCharactersPerStream,
            cancellationToken);

    private static async Task WaitForFileAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(25, cancellationToken);
        }
    }

    private static async Task<bool> WaitForExactProcessExitAsync(int processId, long startTimeUtcTicks)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != startTimeUtcTicks) return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            await Task.Delay(25);
        }
        return false;
    }

    private sealed class FakeInspector(PinnedProcessSnapshot snapshot) : IPinnedProcessInspector
    {
        public PinnedProcessSnapshot Read(int processId) => snapshot;
    }

    private sealed class SequenceInspector(params PinnedProcessSnapshot[] snapshots) : IPinnedProcessInspector
    {
        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);

        public PinnedProcessSnapshot Read(int processId)
        {
            int index = Interlocked.Increment(ref _readCount) - 1;
            if (index >= snapshots.Length) throw new InvalidOperationException("No process snapshot remains.");
            return snapshots[index];
        }
    }

    private sealed class FakeRunner(BoundedProcessResult result) : IBoundedProcessRunner
    {
        internal string? ExecutablePath { get; private set; }
        internal IReadOnlyList<string>? Arguments { get; private set; }
        internal int MaximumCharactersPerStream { get; private set; }

        public ValueTask<BoundedProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            int maximumCharactersPerStream,
            CancellationToken cancellationToken)
        {
            ExecutablePath = executablePath;
            Arguments = arguments.ToArray();
            MaximumCharactersPerStream = maximumCharactersPerStream;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeWindowsTcpOwnerTable(params WindowsTcpOwnerRow[] rows) : IWindowsTcpOwnerTable
    {
        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _readCount);
            return ValueTask.FromResult<IReadOnlyList<WindowsTcpOwnerRow>>(rows);
        }
    }

    private sealed class BlockingWindowsTcpOwnerTable : IWindowsTcpOwnerTable
    {
        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);
        internal TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _readCount);
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-aware wait unexpectedly completed.");
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileForMutationAttempt(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
