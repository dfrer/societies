using System.Buffers.Binary;
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

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    public async Task ExposureMonitor_IgnoresBoundWildcardTupleWithoutAConnectedPeer(
        string wildcardAddress)
    {
        const int processId = 22920;
        IPAddress wildcard = IPAddress.Parse(wildcardAddress);
        WindowsTcpOwnerRow boundWildcard = new(
            wildcard,
            61081,
            wildcard,
            0,
            State: 100,
            processId);
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            new FakeWindowsTcpOwnerTable(boundWildcard));

        await monitor.VerifyNoNonLoopbackExposureAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(2, "0.0.0.0", 61081, "0.0.0.0", 0, "non_loopback_listener_detected")]
    [InlineData(5, "127.0.0.1", 61081, "203.0.113.42", 443, "non_loopback_peer_detected")]
    [InlineData(2, "::", 61081, "::", 0, "non_loopback_listener_detected")]
    [InlineData(5, "::1", 61081, "2001:db8::42", 443, "non_loopback_peer_detected")]
    public async Task ExposureMonitor_FailsClosedForActualNonLoopbackListenerOrPeer(
        int state,
        string localAddress,
        int localPort,
        string remoteAddress,
        int remotePort,
        string expectedCode)
    {
        const int processId = 22920;
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            new FakeWindowsTcpOwnerTable(new WindowsTcpOwnerRow(
                IPAddress.Parse(localAddress),
                localPort,
                IPAddress.Parse(remoteAddress),
                remotePort,
                state,
                processId)));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            monitor.VerifyNoNonLoopbackExposureAsync(CancellationToken.None).AsTask());

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task ExposureMonitor_IgnoresLoopbackListenerAndOtherProcessRows()
    {
        const int processId = 22920;
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            new FakeWindowsTcpOwnerTable(
                new WindowsTcpOwnerRow(IPAddress.Loopback, 11435, IPAddress.Any, 0, 2, processId),
                // IP Helper documents a LISTEN row's remote tuple as having no meaning.
                new WindowsTcpOwnerRow(IPAddress.IPv6Loopback, 11435, IPAddress.Parse("2001:db8::42"), 443, 2, processId),
                new WindowsTcpOwnerRow(IPAddress.IPv6Loopback, 50000, IPAddress.IPv6Loopback, 11435, 5, processId),
                new WindowsTcpOwnerRow(IPAddress.Loopback, 50000, IPAddress.Parse("203.0.113.42"), 443, 5, processId + 1)));

        await monitor.VerifyNoNonLoopbackExposureAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(5, "127.0.0.1", 50000, "0.0.0.0", 0)]
    [InlineData(100, "0.0.0.0", 0, "0.0.0.0", 0)]
    [InlineData(101, "0.0.0.0", 61081, "0.0.0.0", 0)]
    public async Task ExposureMonitor_FailsClosedForMalformedStateTuple(
        int state,
        string localAddress,
        int localPort,
        string remoteAddress,
        int remotePort)
    {
        const int processId = 22920;
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            new FakeWindowsTcpOwnerTable(new WindowsTcpOwnerRow(
                IPAddress.Parse(localAddress),
                localPort,
                IPAddress.Parse(remoteAddress),
                remotePort,
                state,
                processId)));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            monitor.VerifyNoNonLoopbackExposureAsync(CancellationToken.None).AsTask());

        Assert.Equal("tcp_exposure_row_invalid", exception.Code);
    }

    [Fact]
    public async Task ExposureMonitor_ConcurrentSamplerCancelsRunOnObservedViolation()
    {
        const int processId = 22920;
        SequenceWindowsTcpOwnerTable table = new(
            Array.Empty<WindowsTcpOwnerRow>(),
            new[]
            {
                new WindowsTcpOwnerRow(
                    IPAddress.IPv6Loopback,
                    61081,
                    IPAddress.Parse("2001:db8::42"),
                    443,
                    5,
                    processId)
            });
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            table,
            TimeSpan.FromMilliseconds(1),
            maximumSamples: 20,
            cleanupTimeout: TimeSpan.FromSeconds(1));
        TaskCompletionSource operationCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            monitor.RunWhileMonitoringAsync(
                async cancellationToken =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                        return 0;
                    }
                    finally
                    {
                        operationCancelled.TrySetResult();
                    }
                },
                CancellationToken.None));

        Assert.Equal("non_loopback_peer_detected", exception.Code);
        await operationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(2, table.ReadCount);
    }

    [Fact]
    public async Task ExposureMonitor_ConcurrentSamplerReturnsHonestBoundedSampleLabel()
    {
        const int processId = 22920;
        CountingWindowsTcpOwnerTable table = new();
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            table,
            TimeSpan.FromMilliseconds(1),
            maximumSamples: 20,
            cleanupTimeout: TimeSpan.FromSeconds(1));

        WindowsTcpExposureSampledRun<string> result = await monitor.RunWhileMonitoringAsync(
            async cancellationToken =>
            {
                await table.WaitForReadCountAsync(2, cancellationToken);
                return "accepted";
            },
            CancellationToken.None);

        Assert.Equal("accepted", result.Result);
        Assert.True(result.SampleCount >= 3);
        Assert.Equal(result.SampleCount, table.ReadCount);
        Assert.Equal(
            "bounded_samples_do_not_guarantee_unsampled_transient_exposure",
            result.MeasurementLimit);
    }

    [Fact]
    public async Task ExposureMonitor_OperationCleanupTimeoutFailsClosed()
    {
        const int processId = 22920;
        SequenceWindowsTcpOwnerTable table = new(
            Array.Empty<WindowsTcpOwnerRow>(),
            new[]
            {
                new WindowsTcpOwnerRow(
                    IPAddress.Loopback,
                    61081,
                    IPAddress.Parse("203.0.113.42"),
                    443,
                    5,
                    processId)
            });
        WindowsTcpExposureMonitor monitor = WindowsTcpExposureMonitor.CreateForTesting(
            processId,
            table,
            TimeSpan.FromMilliseconds(1),
            maximumSamples: 20,
            cleanupTimeout: TimeSpan.FromMilliseconds(25));
        TaskCompletionSource<int> releaseOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);

        LocalModelBenchmarkException exception;
        try
        {
            exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
                monitor.RunWhileMonitoringAsync(
                    _ => releaseOperation.Task,
                    CancellationToken.None));
        }
        finally
        {
            releaseOperation.TrySetResult(0);
        }

        Assert.Equal("tcp_exposure_operation_cleanup_timeout", exception.Code);
    }

    [Fact]
    public async Task SystemTcpOwnerTable_StrictlyParsesAndAggregatesIpv4AndIpv6()
    {
        const int processId = 22920;
        IPAddress scopedLocal = new(IPAddress.Parse("fe80::1").GetAddressBytes(), 7);
        FakeWindowsExtendedTcpTableApi api = new(
            BuildIpv4OwnerTable(new WindowsTcpOwnerRow(
                IPAddress.Loopback, 11435, IPAddress.Any, 0, 2, processId)),
            BuildIpv6OwnerTable(new WindowsTcpOwnerRow(
                scopedLocal, 61081, IPAddress.Parse("2001:db8::42"), 443, 5, processId)));
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(api);

        IReadOnlyList<WindowsTcpOwnerRow> rows = await table.ReadRowsAsync(CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(IPAddress.Loopback, rows[0].LocalAddress);
        Assert.Equal(11435, rows[0].LocalPort);
        Assert.Equal(IPAddress.Any, rows[0].RemoteAddress);
        Assert.Equal(0, rows[0].RemotePort);
        Assert.Equal(scopedLocal, rows[1].LocalAddress);
        Assert.Equal(IPAddress.Parse("2001:db8::42"), rows[1].RemoteAddress);
        Assert.Equal(443, rows[1].RemotePort);
        Assert.Equal(new[] { 2, 2, 23, 23 }, api.AddressFamilies);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(23)]
    public async Task SystemTcpOwnerTable_FailsClosedWhenEitherFamilyQueryFails(int failingFamily)
    {
        FakeWindowsExtendedTcpTableApi api = new(
            BuildIpv4OwnerTable(),
            BuildIpv6OwnerTable(),
            failingFamily);
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(api);

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            table.ReadRowsAsync(CancellationToken.None).AsTask());

        Assert.Equal("tcp_owner_table_query_failed", exception.Code);
        Assert.Contains(failingFamily, api.AddressFamilies);
    }

    [Fact]
    public async Task SystemTcpOwnerTable_WindowsSmokeReadsBothCurrentTablesWithoutOpeningSockets()
    {
        if (!OperatingSystem.IsWindows()) return;

        SystemWindowsTcpOwnerTable table = new();
        IReadOnlyList<WindowsTcpOwnerRow> rows =
            await table.ReadRowsAsync(CancellationToken.None);

        Assert.NotNull(rows);
        Assert.All(rows, row => Assert.True(
            row.LocalAddress.AddressFamily is
                System.Net.Sockets.AddressFamily.InterNetwork or
                System.Net.Sockets.AddressFamily.InterNetworkV6));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task SystemTcpOwnerTable_AcceptsOnlyBoundedNativeAlignmentTail(int tailBytes)
    {
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(
            new FakeWindowsExtendedTcpTableApi(
                BuildIpv4OwnerTableWithTail(tailBytes),
                BuildIpv6OwnerTableWithTail(tailBytes)));

        IReadOnlyList<WindowsTcpOwnerRow> rows =
            await table.ReadRowsAsync(CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task SystemTcpOwnerTable_EnforcesAggregateRowBoundAcrossBothFamilies()
    {
        WindowsTcpOwnerRow ipv4Bound = new(
            IPAddress.Any, 61081, IPAddress.Any, 0, 100, 22920);
        WindowsTcpOwnerRow ipv6Bound = new(
            IPAddress.IPv6Any, 61081, IPAddress.IPv6Any, 0, 100, 22920);
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(
            new FakeWindowsExtendedTcpTableApi(
                BuildIpv4OwnerTable(Enumerable.Repeat(ipv4Bound, 16_384).ToArray()),
                BuildIpv6OwnerTable(ipv6Bound)));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            table.ReadRowsAsync(CancellationToken.None).AsTask());

        Assert.Equal("tcp_owner_table_query_failed", exception.Code);
    }

    [Fact]
    public async Task SystemTcpOwnerTable_RejectsOversizedFamilyBeforeAllocation()
    {
        byte[] oversized = new byte[2 * 1024 * 1024 + 1];
        BinaryPrimitives.WriteUInt32LittleEndian(oversized, 0u);
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(
            new FakeWindowsExtendedTcpTableApi(oversized, BuildIpv6OwnerTable()));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            table.ReadRowsAsync(CancellationToken.None).AsTask());

        Assert.Equal("tcp_owner_table_query_failed", exception.Code);
    }

    [Theory]
    [InlineData("truncated-row")]
    [InlineData("excess-tail")]
    [InlineData("ignored-port-high-bits")]
    [InlineData("invalid-state")]
    [InlineData("invalid-pid")]
    public async Task SystemTcpOwnerTable_HandlesDocumentedPortBitsAndRejectsMalformedNativeRows(
        string mode)
    {
        byte[] ipv4 = BuildIpv4OwnerTable(new WindowsTcpOwnerRow(
            IPAddress.Loopback, 11435, IPAddress.Any, 0, 2, 22920));
        switch (mode)
        {
            case "truncated-row":
                Array.Resize(ref ipv4, ipv4.Length - 9);
                break;
            case "excess-tail":
                Array.Resize(ref ipv4, ipv4.Length + 1);
                break;
            case "ignored-port-high-bits":
                BinaryPrimitives.WriteUInt32LittleEndian(
                    ipv4.AsSpan(12, 4),
                    BinaryPrimitives.ReadUInt32LittleEndian(ipv4.AsSpan(12, 4)) | 0xabcd_0000u);
                SystemWindowsTcpOwnerTable highBitsTable = SystemWindowsTcpOwnerTable.CreateForTesting(
                    new FakeWindowsExtendedTcpTableApi(ipv4, BuildIpv6OwnerTable()));
                IReadOnlyList<WindowsTcpOwnerRow> highBitsRows =
                    await highBitsTable.ReadRowsAsync(CancellationToken.None);
                Assert.Equal(11435, Assert.Single(highBitsRows).LocalPort);
                return;
            case "invalid-state":
                BinaryPrimitives.WriteUInt32LittleEndian(ipv4.AsSpan(4, 4), 99u);
                break;
            case "invalid-pid":
                BinaryPrimitives.WriteUInt32LittleEndian(ipv4.AsSpan(24, 4), uint.MaxValue);
                break;
            default:
                throw new InvalidOperationException("Unexpected malformed-row mode.");
        }
        SystemWindowsTcpOwnerTable table = SystemWindowsTcpOwnerTable.CreateForTesting(
            new FakeWindowsExtendedTcpTableApi(ipv4, BuildIpv6OwnerTable()));

        LocalModelBenchmarkException exception = await Assert.ThrowsAsync<LocalModelBenchmarkException>(() =>
            table.ReadRowsAsync(CancellationToken.None).AsTask());

        Assert.Equal("tcp_owner_table_query_failed", exception.Code);
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
    public void EvidenceLease_OpensWhenAnExistingReaderDeniesDeleteSharing()
    {
        string root = CreateTestRepositoryRoot();
        string path = Path.Combine(root, PinnedBenchmarkContract.RelativeEvidencePath);

        using SafeFileHandle existingReader = CreateFileForMutationAttempt(
            root,
            0x00000080u, // FILE_READ_ATTRIBUTES
            FileShare.Read,
            IntPtr.Zero,
            3,
            0x02000000u | 0x00200000u,
            IntPtr.Zero);
        Assert.False(existingReader.IsInvalid);

        string resolved = PinnedBenchmarkContract.ResolveEvidencePath(root);

        Assert.Equal(Path.GetFullPath(path), resolved, ignoreCase: true);
        Assert.True(Directory.Exists(Path.GetDirectoryName(resolved)!));
    }

    [Fact]
    public void EvidenceLease_ResolvesTheActualWorktreeBeforeModelPreflight()
    {
        string root = FindRepositoryRoot(Directory.GetCurrentDirectory());

        string resolved = PinnedBenchmarkContract.ResolveEvidencePath(root);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, PinnedBenchmarkContract.RelativeEvidencePath)),
            resolved,
            ignoreCase: true);
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

    private static string FindRepositoryRoot(string startingDirectory)
    {
        for (DirectoryInfo? candidate = new(Path.GetFullPath(startingDirectory)); candidate is not null; candidate = candidate.Parent)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "CURRENT_BUILD.md"))
                && (Directory.Exists(Path.Combine(candidate.FullName, ".git"))
                    || File.Exists(Path.Combine(candidate.FullName, ".git")))
                && File.Exists(Path.Combine(candidate.FullName, "labs", "Societies.SnowGlobe", "Societies.SnowGlobe.csproj")))
            {
                return candidate.FullName;
            }
        }

        throw new InvalidOperationException("The test process did not start beneath the Societies worktree.");
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

    private static byte[] BuildIpv4OwnerTable(params WindowsTcpOwnerRow[] rows) =>
        BuildIpv4OwnerTableWithTail(8, rows);

    private static byte[] BuildIpv4OwnerTableWithTail(
        int alignmentTailBytes,
        params WindowsTcpOwnerRow[] rows)
    {
        const int rowBytes = 24;
        if (alignmentTailBytes is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(alignmentTailBytes));
        }
        byte[] table = new byte[sizeof(uint) + rows.Length * rowBytes + alignmentTailBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(table, checked((uint)rows.Length));
        for (int index = 0; index < rows.Length; index++)
        {
            WindowsTcpOwnerRow row = rows[index];
            byte[] localAddress = row.LocalAddress.GetAddressBytes();
            byte[] remoteAddress = row.RemoteAddress.GetAddressBytes();
            if (localAddress.Length != 4 || remoteAddress.Length != 4)
            {
                throw new ArgumentException("IPv4 table rows must contain only IPv4 addresses.");
            }

            Span<byte> destination = table.AsSpan(sizeof(uint) + index * rowBytes, rowBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, checked((uint)row.State));
            localAddress.CopyTo(destination.Slice(4, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), EncodeNetworkPort(row.LocalPort));
            remoteAddress.CopyTo(destination.Slice(12, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(16, 4), EncodeNetworkPort(row.RemotePort));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), checked((uint)row.OwningProcessId));
        }
        return table;
    }

    private static byte[] BuildIpv6OwnerTable(params WindowsTcpOwnerRow[] rows) =>
        BuildIpv6OwnerTableWithTail(8, rows);

    private static byte[] BuildIpv6OwnerTableWithTail(
        int alignmentTailBytes,
        params WindowsTcpOwnerRow[] rows)
    {
        const int rowBytes = 56;
        if (alignmentTailBytes is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(alignmentTailBytes));
        }
        byte[] table = new byte[sizeof(uint) + rows.Length * rowBytes + alignmentTailBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(table, checked((uint)rows.Length));
        for (int index = 0; index < rows.Length; index++)
        {
            WindowsTcpOwnerRow row = rows[index];
            byte[] localAddress = row.LocalAddress.GetAddressBytes();
            byte[] remoteAddress = row.RemoteAddress.GetAddressBytes();
            if (localAddress.Length != 16 || remoteAddress.Length != 16)
            {
                throw new ArgumentException("IPv6 table rows must contain only IPv6 addresses.");
            }

            Span<byte> destination = table.AsSpan(sizeof(uint) + index * rowBytes, rowBytes);
            localAddress.CopyTo(destination[..16]);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(16, 4), EncodeNetworkScopeId(row.LocalAddress.ScopeId));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), EncodeNetworkPort(row.LocalPort));
            remoteAddress.CopyTo(destination.Slice(24, 16));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(40, 4), EncodeNetworkScopeId(row.RemoteAddress.ScopeId));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), EncodeNetworkPort(row.RemotePort));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(48, 4), checked((uint)row.State));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(52, 4), checked((uint)row.OwningProcessId));
        }
        return table;
    }

    private static uint EncodeNetworkPort(int port)
    {
        if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
        return unchecked((ushort)IPAddress.HostToNetworkOrder((short)port));
    }

    private static uint EncodeNetworkScopeId(long scopeId)
    {
        if (scopeId is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(scopeId));
        }
        return unchecked((uint)IPAddress.HostToNetworkOrder((int)(uint)scopeId));
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

    private sealed class SequenceWindowsTcpOwnerTable(params WindowsTcpOwnerRow[][] snapshots) : IWindowsTcpOwnerTable
    {
        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Interlocked.Increment(ref _readCount) - 1;
            WindowsTcpOwnerRow[] rows = snapshots[Math.Min(index, snapshots.Length - 1)];
            return ValueTask.FromResult<IReadOnlyList<WindowsTcpOwnerRow>>(rows);
        }
    }

    private sealed class CountingWindowsTcpOwnerTable : IWindowsTcpOwnerTable
    {
        private int _readCount;

        internal int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<IReadOnlyList<WindowsTcpOwnerRow>> ReadRowsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _readCount);
            return ValueTask.FromResult<IReadOnlyList<WindowsTcpOwnerRow>>(
                Array.Empty<WindowsTcpOwnerRow>());
        }

        internal async Task WaitForReadCountAsync(int expectedCount, CancellationToken cancellationToken)
        {
            while (ReadCount < expectedCount)
            {
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    private sealed class FakeWindowsExtendedTcpTableApi : IWindowsExtendedTcpTableApi
    {
        private const int ErrorAccessDenied = 5;
        private const int ErrorInsufficientBuffer = 122;
        private readonly byte[] _ipv4Table;
        private readonly byte[] _ipv6Table;
        private readonly int? _failingFamily;
        private readonly List<int> _addressFamilies = new();

        internal FakeWindowsExtendedTcpTableApi(
            byte[] ipv4Table,
            byte[] ipv6Table,
            int? failingFamily = null)
        {
            _ipv4Table = ipv4Table;
            _ipv6Table = ipv6Table;
            _failingFamily = failingFamily;
        }

        internal IReadOnlyList<int> AddressFamilies => _addressFamilies.ToArray();

        public int GetExtendedTcpTable(
            IntPtr tcpTable,
            ref int size,
            bool order,
            int addressFamily,
            int tableClass,
            int reserved)
        {
            _addressFamilies.Add(addressFamily);
            if (order || tableClass != 5 || reserved != 0 || _failingFamily == addressFamily)
            {
                return ErrorAccessDenied;
            }

            byte[] table = addressFamily switch
            {
                2 => _ipv4Table,
                23 => _ipv6Table,
                _ => throw new InvalidOperationException("Unexpected address family.")
            };
            if (tcpTable == IntPtr.Zero)
            {
                size = table.Length;
                return ErrorInsufficientBuffer;
            }
            if (size < table.Length)
            {
                size = table.Length;
                return ErrorInsufficientBuffer;
            }

            Marshal.Copy(table, 0, tcpTable, table.Length);
            size = table.Length;
            return 0;
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
