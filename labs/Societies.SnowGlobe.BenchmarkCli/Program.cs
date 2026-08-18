using System.Globalization;
using System.Security.Cryptography;
using Societies.SnowGlobe;
using Societies.SnowGlobe.BenchmarkCli;

return await BenchmarkProgram.RunAsync(args);

internal static class BenchmarkProgram
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2
            || !string.Equals(args[0], "--pid", StringComparison.Ordinal)
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int processId)
            || processId <= 0)
        {
            Console.Error.WriteLine("BENCHMARK_FAILED code=arguments_invalid");
            return 2;
        }

        LocalModelBenchmarkEvidence? completedEvidence = null;
        try
        {
            string evidencePath = PinnedBenchmarkContract.ResolveEvidencePath(Directory.GetCurrentDirectory());
            if (File.Exists(evidencePath))
            {
                throw new LocalModelBenchmarkException("evidence_already_exists");
            }

            LocalModelBenchmarkPlan plan = PinnedBenchmarkContract.CreatePlan();
            OllamaRuntimeAuthorization runtime = PinnedBenchmarkContract.CreateRuntimeAuthorization(processId);
            PinnedNvidiaSmiVramProbe probe = PinnedNvidiaSmiVramProbe.Create(processId);
            WindowsOllamaLoopbackConnectionOwnerResolver ownerResolver =
                WindowsOllamaLoopbackConnectionOwnerResolver.Create(processId);
            await using OllamaLoopbackHttpTransport transport = new(plan.Endpoint, processId, ownerResolver);
            LocalModelBenchmarkExecutionCapability capability =
                LocalModelBenchmarkExecutionCapability.AuthorizeSingleUse(plan, runtime);
            OllamaBenchmarkRunResult result = await new OllamaBenchmarkRunner(transport, probe)
                .RunAsync(plan, capability).ConfigureAwait(false);
            completedEvidence = result.Evidence;
            CanonicalBenchmarkEvidenceWriter.WriteNew(evidencePath, plan, result);
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"BENCHMARK_ACCEPTED evidence={PinnedBenchmarkContract.RelativeEvidencePath} samples={result.Evidence.MeasuredRequestCount} p50_ms={result.Evidence.P50LatencyMilliseconds:F3} p95_ms={result.Evidence.P95LatencyMilliseconds:F3} p99_ms={result.Evidence.P99LatencyMilliseconds:F3} throughput_tok_s={result.Evidence.ThroughputTokensPerSecond:F3} peak_vram_mib={result.ObservedSampledPeakVramMiB:F0}"));
            return 0;
        }
        catch (LocalModelBenchmarkException exception)
        {
            Console.Error.WriteLine($"BENCHMARK_FAILED code={exception.Code}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("BENCHMARK_FAILED code=cancelled");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"BENCHMARK_FAILED code=invocation_failure type={exception.GetType().Name}");
            return 1;
        }
        finally
        {
            if (completedEvidence?.CanonicalMetricsSamplesUtf8 is { } metrics)
            {
                CryptographicOperations.ZeroMemory(metrics);
            }
        }
    }
}
