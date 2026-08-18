using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Societies.SnowGlobe;

namespace Societies.SnowGlobe.BenchmarkCli;

internal enum WindowsTcpExposureViolationKind
{
    NonLoopbackListener,
    NonLoopbackConnectedPeer
}

internal sealed record WindowsTcpExposureViolation(
    WindowsTcpExposureViolationKind Kind,
    WindowsTcpOwnerRow Row);

internal sealed record WindowsTcpExposureSampledRun<T>(
    T Result,
    int SampleCount,
    string MeasurementLimit);

/// <summary>
/// Classifies only TCP states that prove an externally reachable listener or an
/// established non-loopback peer. A wildcard/zero remote tuple in an
/// unconnected state is not a peer and must never be reported as outbound.
/// </summary>
internal static class WindowsTcpExposureClassifier
{
    private const int ListenState = 2;
    private const int EstablishedState = 5;
    private const int BoundState = 100;

    internal static WindowsTcpExposureViolation? Classify(WindowsTcpOwnerRow row)
    {
        ValidateRow(row);
        if (row.State == ListenState
            && !IPAddress.IsLoopback(row.LocalAddress))
        {
            return new WindowsTcpExposureViolation(
                WindowsTcpExposureViolationKind.NonLoopbackListener,
                row);
        }

        if (row.State == EstablishedState
            && !IPAddress.IsLoopback(row.RemoteAddress)
            && !IsUnspecified(row.RemoteAddress))
        {
            return new WindowsTcpExposureViolation(
                WindowsTcpExposureViolationKind.NonLoopbackConnectedPeer,
                row);
        }

        return null;
    }

    private static void ValidateRow(WindowsTcpOwnerRow row)
    {
        if (row.LocalAddress is null
            || row.RemoteAddress is null
            || row.LocalAddress.AddressFamily != row.RemoteAddress.AddressFamily
            || row.LocalAddress.AddressFamily is not (
                AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            || row.LocalPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort
            || row.RemotePort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort
            || row.State is not (>= 1 and <= 12) and not BoundState
            || row.OwningProcessId < 0)
        {
            throw new LocalModelBenchmarkException("tcp_exposure_row_invalid");
        }

        if (row.State == ListenState
            && row.LocalPort == 0
            || row.State == EstablishedState
                && (row.LocalPort == 0
                    || row.RemotePort == 0
                    || IsUnspecified(row.LocalAddress)
                    || IsUnspecified(row.RemoteAddress))
            || row.State == BoundState
                && (row.LocalPort == 0
                    || row.RemotePort != 0
                    || !IsUnspecified(row.RemoteAddress)))
        {
            throw new LocalModelBenchmarkException("tcp_exposure_row_invalid");
        }
    }

    private static bool IsUnspecified(IPAddress address) =>
        address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
}

internal sealed class WindowsTcpExposureMonitor
{
    internal const string MeasurementLimit =
        "bounded_samples_do_not_guarantee_unsampled_transient_exposure";

    private const int MaximumRows = 16_384;
    private const int ProductionMaximumSamples = 65_536;
    private static readonly TimeSpan ProductionSamplingInterval = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan ProductionCleanupTimeout = TimeSpan.FromSeconds(5);
    private readonly int _processId;
    private readonly IWindowsTcpOwnerTable _ownerTable;
    private readonly TimeSpan _samplingInterval;
    private readonly int _maximumSamples;
    private readonly TimeSpan _cleanupTimeout;

    private WindowsTcpExposureMonitor(
        int processId,
        IWindowsTcpOwnerTable ownerTable,
        TimeSpan samplingInterval,
        int maximumSamples,
        TimeSpan cleanupTimeout)
    {
        _processId = processId;
        _ownerTable = ownerTable;
        _samplingInterval = samplingInterval;
        _maximumSamples = maximumSamples;
        _cleanupTimeout = cleanupTimeout;
    }

    internal static WindowsTcpExposureMonitor Create(int processId) =>
        CreateForTesting(
            processId,
            new SystemWindowsTcpOwnerTable(),
            ProductionSamplingInterval,
            ProductionMaximumSamples,
            ProductionCleanupTimeout);

    internal static WindowsTcpExposureMonitor CreateForTesting(
        int processId,
        IWindowsTcpOwnerTable ownerTable,
        TimeSpan? samplingInterval = null,
        int maximumSamples = ProductionMaximumSamples,
        TimeSpan? cleanupTimeout = null)
    {
        TimeSpan interval = samplingInterval ?? ProductionSamplingInterval;
        TimeSpan cleanup = cleanupTimeout ?? ProductionCleanupTimeout;
        if (processId <= 0
            || ownerTable is null
            || interval <= TimeSpan.Zero
            || interval > TimeSpan.FromSeconds(1)
            || maximumSamples < 3
            || maximumSamples > ProductionMaximumSamples
            || cleanup <= TimeSpan.Zero
            || cleanup > ProductionCleanupTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        return new WindowsTcpExposureMonitor(
            processId,
            ownerTable,
            interval,
            maximumSamples,
            cleanup);
    }

    internal async ValueTask VerifyNoNonLoopbackExposureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<WindowsTcpOwnerRow> rows =
            await _ownerTable.ReadRowsAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (rows.Count > MaximumRows)
        {
            throw new LocalModelBenchmarkException("tcp_exposure_table_too_large");
        }

        foreach (WindowsTcpOwnerRow row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.OwningProcessId != _processId)
            {
                continue;
            }

            WindowsTcpExposureViolation? violation = WindowsTcpExposureClassifier.Classify(row);
            if (violation is null)
            {
                continue;
            }

            throw new LocalModelBenchmarkException(violation.Kind switch
            {
                WindowsTcpExposureViolationKind.NonLoopbackListener => "non_loopback_listener_detected",
                WindowsTcpExposureViolationKind.NonLoopbackConnectedPeer => "non_loopback_peer_detected",
                _ => "tcp_exposure_classification_invalid"
            });
        }
    }

    internal async Task<WindowsTcpExposureSampledRun<T>> RunWhileMonitoringAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        int sampleCount = 0;
        await VerifyAndCountAsync(
            () => Interlocked.Increment(ref sampleCount),
            cancellationToken).ConfigureAwait(false);

        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task monitorTask = MonitorUntilCancelledAsync(
            () => Interlocked.Increment(ref sampleCount),
            () => Volatile.Read(ref sampleCount),
            linked.Token);
        Task<T> operationTask;
        try
        {
            operationTask = operation(linked.Token)
                ?? throw new LocalModelBenchmarkException("tcp_exposure_operation_invalid");
        }
        catch
        {
            linked.Cancel();
            Exception? startupShutdownFailure =
                await AwaitMonitorShutdownAsync(monitorTask).ConfigureAwait(false);
            if (startupShutdownFailure is not null)
            {
                ExceptionDispatchInfo.Capture(startupShutdownFailure).Throw();
            }
            throw;
        }

        Task first = await Task.WhenAny(operationTask, monitorTask).ConfigureAwait(false);
        if (ReferenceEquals(first, monitorTask))
        {
            Exception? monitorFailure = await CaptureTaskFailureAsync(monitorTask).ConfigureAwait(false);
            linked.Cancel();
            if (!await AwaitCleanupAsync(operationTask).ConfigureAwait(false))
            {
                throw new LocalModelBenchmarkException("tcp_exposure_operation_cleanup_timeout");
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (monitorFailure is null)
            {
                throw new LocalModelBenchmarkException("tcp_exposure_monitor_stopped_unexpectedly");
            }
            ExceptionDispatchInfo.Capture(monitorFailure).Throw();
        }

        linked.Cancel();
        Exception? shutdownFailure = await AwaitMonitorShutdownAsync(monitorTask).ConfigureAwait(false);
        if (shutdownFailure is not null)
        {
            ExceptionDispatchInfo.Capture(shutdownFailure).Throw();
        }

        T result = await operationTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await VerifyAndCountAsync(
            () => Interlocked.Increment(ref sampleCount),
            cancellationToken).ConfigureAwait(false);
        return new WindowsTcpExposureSampledRun<T>(
            result,
            sampleCount,
            MeasurementLimit);
    }

    private async Task MonitorUntilCancelledAsync(
        Action incrementSampleCount,
        Func<int> readSampleCount,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(_samplingInterval, cancellationToken).ConfigureAwait(false);
            if (readSampleCount() >= _maximumSamples - 1)
            {
                throw new LocalModelBenchmarkException("tcp_exposure_sample_limit_exceeded");
            }
            await VerifyAndCountAsync(incrementSampleCount, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask VerifyAndCountAsync(
        Action incrementSampleCount,
        CancellationToken cancellationToken)
    {
        await VerifyNoNonLoopbackExposureAsync(cancellationToken).ConfigureAwait(false);
        incrementSampleCount();
    }

    private async Task<Exception?> AwaitMonitorShutdownAsync(Task monitorTask)
    {
        Task completed = await Task.WhenAny(
            monitorTask,
            Task.Delay(_cleanupTimeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, monitorTask))
        {
            return new LocalModelBenchmarkException("tcp_exposure_monitor_cleanup_timeout");
        }
        return await CaptureTaskFailureAsync(monitorTask).ConfigureAwait(false);
    }

    private async Task<bool> AwaitCleanupAsync(Task operationTask)
    {
        Task completed = await Task.WhenAny(
            operationTask,
            Task.Delay(_cleanupTimeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, operationTask))
        {
            return false;
        }
        _ = await CaptureTaskFailureAsync(operationTask).ConfigureAwait(false);
        return true;
    }

    private static async Task<Exception?> CaptureTaskFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
