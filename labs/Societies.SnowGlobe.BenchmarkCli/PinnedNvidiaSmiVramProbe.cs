using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Societies.SnowGlobe;

namespace Societies.SnowGlobe.BenchmarkCli;

internal sealed record BoundedProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal interface IBoundedProcessRunner
{
    ValueTask<BoundedProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        int maximumCharactersPerStream,
        CancellationToken cancellationToken);
}

internal sealed class SystemBoundedProcessRunner : IBoundedProcessRunner
{
    private const int TerminationWaitMilliseconds = 5000;

    public async ValueTask<BoundedProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        int maximumCharactersPerStream,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start()) throw new LocalModelBenchmarkException("nvidia_smi_start_failed");
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        try
        {
            stdout = ReadBoundedAsync(
                process.StandardOutput, maximumCharactersPerStream, cancellationToken);
            stderr = ReadBoundedAsync(
                process.StandardError, maximumCharactersPerStream, cancellationToken);
            Task exit = process.WaitForExitAsync(cancellationToken);
            Task first = await Task.WhenAny(exit, stdout, stderr).ConfigureAwait(false);
            if (ReferenceEquals(first, stdout)) await stdout.ConfigureAwait(false);
            if (ReferenceEquals(first, stderr)) await stderr.ConfigureAwait(false);
            await exit.ConfigureAwait(false);
            return new BoundedProcessResult(
                process.ExitCode,
                await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false));
        }
        catch
        {
            await TerminateAndObserveAsync(process, stdout, stderr).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[Math.Min(1024, maximumCharacters + 1)];
        StringWriter output = new(CultureInfo.InvariantCulture);
        while (true)
        {
            int remaining = maximumCharacters - output.GetStringBuilder().Length;
            int requested = Math.Min(buffer.Length, remaining + 1);
            int read = await reader.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            if (read == 0) return output.ToString();
            if (read > remaining) throw new LocalModelBenchmarkException("nvidia_smi_output_too_large");
            output.Write(buffer, 0, read);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static async Task TerminateAndObserveAsync(
        Process process,
        Task<string>? stdout,
        Task<string>? stderr)
    {
        TryKill(process);
        try
        {
            await process.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromMilliseconds(TerminationWaitMilliseconds)).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new LocalModelBenchmarkException("nvidia_smi_termination_failed", exception);
        }

        await ObserveAsync(stdout).ConfigureAwait(false);
        await ObserveAsync(stderr).ConfigureAwait(false);
    }

    private static async Task ObserveAsync(Task<string>? task)
    {
        if (task is null) return;
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The originating bounded-output or cancellation failure remains authoritative.
        }
    }
}

internal sealed record PinnedProcessSnapshot(
    int ProcessId,
    string ExecutablePath,
    long StartTimeUtcTicks,
    bool HasExited);

internal interface IPinnedProcessInspector
{
    PinnedProcessSnapshot Read(int processId);
}

internal sealed class SystemPinnedProcessInspector : IPinnedProcessInspector
{
    public PinnedProcessSnapshot Read(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return new PinnedProcessSnapshot(
                process.Id,
                process.MainModule?.FileName ?? string.Empty,
                process.StartTime.ToUniversalTime().Ticks,
                process.HasExited);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new LocalModelBenchmarkException("ollama_process_identity_unavailable", exception);
        }
    }
}

/// <summary>
/// Windows WDDM reports per-process used GPU memory as N/A. This probe therefore binds each reading to
/// the exact live Ollama PID/start-time/executable and artifact digest before and after each query, while conservatively sampling the
/// aggregate used memory for one exact GPU UUID. It does not claim per-process attribution or an unsampled peak.
/// </summary>
internal sealed class PinnedNvidiaSmiVramProbe : ILocalModelBenchmarkVramProbe
{
    private static readonly string[] QueryArguments =
    {
        "--query-gpu=uuid,memory.total,memory.used",
        "--format=csv,noheader,nounits"
    };
    private readonly string _nvidiaSmiPath;
    private readonly string _gpuUuid;
    private readonly int _gpuTotalVramMiB;
    private readonly int _processId;
    private readonly string _runtimeExecutablePath;
    private readonly long _processStartTimeUtcTicks;
    private readonly IPinnedProcessInspector _processInspector;
    private readonly IBoundedProcessRunner _processRunner;

    private PinnedNvidiaSmiVramProbe(
        string nvidiaSmiPath,
        string gpuUuid,
        int gpuTotalVramMiB,
        int processId,
        string runtimeExecutablePath,
        long processStartTimeUtcTicks,
        IPinnedProcessInspector processInspector,
        IBoundedProcessRunner processRunner)
    {
        _nvidiaSmiPath = nvidiaSmiPath;
        _gpuUuid = gpuUuid;
        _gpuTotalVramMiB = gpuTotalVramMiB;
        _processId = processId;
        _runtimeExecutablePath = runtimeExecutablePath;
        _processStartTimeUtcTicks = processStartTimeUtcTicks;
        _processInspector = processInspector;
        _processRunner = processRunner;
    }

    internal static PinnedNvidiaSmiVramProbe Create(int processId)
    {
        VerifyFileHash(PinnedBenchmarkContract.RuntimeExecutablePath, PinnedBenchmarkContract.RuntimeExecutableSha256, "runtime_executable_hash_mismatch");
        VerifyFileHash(PinnedBenchmarkContract.NvidiaSmiPath, PinnedBenchmarkContract.NvidiaSmiSha256, "nvidia_smi_hash_mismatch");
        SystemPinnedProcessInspector inspector = new();
        PinnedProcessSnapshot snapshot = inspector.Read(processId);
        ValidateProcessSnapshot(snapshot, processId, PinnedBenchmarkContract.RuntimeExecutablePath, expectedStartTimeUtcTicks: null);
        return new PinnedNvidiaSmiVramProbe(
            PinnedBenchmarkContract.NvidiaSmiPath,
            PinnedBenchmarkContract.GpuUuid,
            PinnedBenchmarkContract.GpuTotalVramMiB,
            processId,
            Path.GetFullPath(PinnedBenchmarkContract.RuntimeExecutablePath),
            snapshot.StartTimeUtcTicks,
            inspector,
            new SystemBoundedProcessRunner());
    }

    internal static PinnedNvidiaSmiVramProbe CreateForTesting(
        int processId,
        string runtimeExecutablePath,
        long processStartTimeUtcTicks,
        IPinnedProcessInspector processInspector,
        IBoundedProcessRunner processRunner) =>
        new(
            PinnedBenchmarkContract.NvidiaSmiPath,
            PinnedBenchmarkContract.GpuUuid,
            PinnedBenchmarkContract.GpuTotalVramMiB,
            processId,
            Path.GetFullPath(runtimeExecutablePath),
            processStartTimeUtcTicks,
            processInspector,
            processRunner);

    public async ValueTask<LocalModelVramReading> ReadAsync(
        LocalModelBenchmarkPlan plan,
        OllamaRuntimeAuthorization runtime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runtime.OllamaProcessId != _processId
            || !string.Equals(runtime.ArtifactDigestSha256, PinnedBenchmarkContract.ArtifactDigestSha256, StringComparison.Ordinal)
            || !string.Equals(runtime.OllamaProcessIdentity, PinnedBenchmarkContract.RuntimeProcessIdentity, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException("vram_probe_runtime_binding_mismatch");
        }

        PinnedProcessSnapshot snapshot = _processInspector.Read(_processId);
        ValidateProcessSnapshot(snapshot, _processId, _runtimeExecutablePath, _processStartTimeUtcTicks);
        BoundedProcessResult query = await _processRunner.RunAsync(
            _nvidiaSmiPath,
            QueryArguments,
            maximumCharactersPerStream: 4096,
            cancellationToken).ConfigureAwait(false);
        PinnedProcessSnapshot postQuerySnapshot = _processInspector.Read(_processId);
        ValidateProcessSnapshot(postQuerySnapshot, _processId, _runtimeExecutablePath, _processStartTimeUtcTicks);
        if (query.ExitCode != 0 || !string.IsNullOrWhiteSpace(query.StandardError))
        {
            throw new LocalModelBenchmarkException("nvidia_smi_query_failed");
        }

        double usedMiB = ParseExactGpuReading(query.StandardOutput, _gpuUuid, _gpuTotalVramMiB);
        return new LocalModelVramReading(
            plan.ContractId,
            plan.ModelIdentity,
            runtime.ArtifactDigestSha256,
            runtime.OllamaProcessIdentity,
            runtime.OllamaProcessId,
            usedMiB);
    }

    private static void VerifyFileHash(string path, string expectedSha256, string errorCode)
    {
        if (!File.Exists(path)
            || !string.Equals(PinnedBenchmarkContract.Sha256File(path), expectedSha256, StringComparison.Ordinal))
        {
            throw new LocalModelBenchmarkException(errorCode);
        }
    }

    private static void ValidateProcessSnapshot(
        PinnedProcessSnapshot snapshot,
        int expectedProcessId,
        string expectedExecutablePath,
        long? expectedStartTimeUtcTicks)
    {
        if (snapshot.HasExited
            || snapshot.ProcessId != expectedProcessId
            || !string.Equals(
                Path.GetFullPath(snapshot.ExecutablePath),
                Path.GetFullPath(expectedExecutablePath),
                StringComparison.OrdinalIgnoreCase)
            || expectedStartTimeUtcTicks.HasValue && snapshot.StartTimeUtcTicks != expectedStartTimeUtcTicks.Value)
        {
            throw new LocalModelBenchmarkException("ollama_process_identity_changed");
        }
    }

    private static double ParseExactGpuReading(string output, string expectedGpuUuid, int expectedTotalMiB)
    {
        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 1) throw new LocalModelBenchmarkException("nvidia_smi_gpu_cardinality_invalid");
        string[] fields = lines[0].Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != 3
            || !string.Equals(fields[0], expectedGpuUuid, StringComparison.Ordinal)
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int totalMiB)
            || totalMiB != expectedTotalMiB
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int usedMiB)
            || usedMiB <= 0
            || usedMiB > totalMiB)
        {
            throw new LocalModelBenchmarkException("nvidia_smi_gpu_reading_invalid");
        }
        return usedMiB;
    }
}
