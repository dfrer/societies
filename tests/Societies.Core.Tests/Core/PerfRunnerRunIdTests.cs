using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Societies.Tests;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PerfRunnerRunIdTests
    {
        private static readonly Regex AnsiControlSequence = new(
            "\\x1B\\[[0-?]*[ -/]*[@-~]",
            RegexOptions.CultureInvariant);

        [Fact]
        public void PerformancePairGenerator_WritesCompatibleBoundedRunIdentityAndValidatorRejectsUnsafeIds()
        {
            const string descriptor =
                "20260731-155639-550-balanced_basin-seed1337-c3-t300-w2-mnatural_warm-afull_artifacts-r1-f3b2eb7b";
            const string expectedOffRunId = "perf-5827f9a0bbcd3117e152c08a-off";
            const string expectedOnRunId = "perf-5827f9a0bbcd3117e152c08a-on";
            const string rejectedUnboundedCanonicalRunId =
                "20260731-155639-550-balanced_basin-seed1337-c3-t300-w2-mnatural_warm-afull_artifacts-r1-f3b2eb7b-off";
            string firstOutputRoot = Path.Combine(Path.GetTempPath(), $"societies-run-id-{Guid.NewGuid():N}");
            string secondOutputRoot = Path.Combine(Path.GetTempPath(), $"societies-run-id-{Guid.NewGuid():N}");
            string descriptorOnlyOutputRoot = Path.Combine(Path.GetTempPath(), $"societies-run-id-{Guid.NewGuid():N}");
            string outputOnlyRoot = Path.Combine(Path.GetTempPath(), $"societies-run-id-{Guid.NewGuid():N}");
            string mixedOutputRoot = Path.Combine(Path.GetTempPath(), $"societies-run-id-{Guid.NewGuid():N}");

            try
            {
                Assert.Equal("diagnostic", NormalizeDiagnostic("\u001b[31;1mdiagnostic\u001b[0m"));
                AssertGeneratorContractRejected(
                    descriptorOnlyOutputRoot,
                    "RunIdContractDescriptor and RunIdContractOutputRoot must be supplied together.",
                    "-RunIdContractDescriptor", descriptor);
                AssertGeneratorContractRejected(
                    outputOnlyRoot,
                    "RunIdContractDescriptor and RunIdContractOutputRoot must be supplied together.",
                    "-RunIdContractOutputRoot", outputOnlyRoot);
                AssertGeneratorContractRejected(
                    mixedOutputRoot,
                    "Run-ID contract mode cannot be combined with production parameter(s): ReleaseExport.",
                    "-RunIdContractDescriptor", descriptor,
                    "-RunIdContractOutputRoot", mixedOutputRoot,
                    "-ReleaseExport");

                JsonElement first = RunGeneratorContract(descriptor, firstOutputRoot);
                JsonElement second = RunGeneratorContract(descriptor, secondOutputRoot);

                Assert.Equal(96, descriptor.Length);
                Assert.Equal(1, first.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(descriptor, first.GetProperty("descriptor").GetString());
                Assert.Equal("sha256_96bit_descriptor_fingerprint", first.GetProperty("algorithm").GetString());
                Assert.Equal("offline_contract", first.GetProperty("source").GetProperty("kind").GetString());
                Assert.Equal(expectedOffRunId, first.GetProperty("offRunId").GetString());
                Assert.Equal(expectedOnRunId, first.GetProperty("onRunId").GetString());
                Assert.True(expectedOffRunId.Length <= 96);
                Assert.True(expectedOnRunId.Length <= 96);
                Assert.NotEqual(expectedOffRunId, expectedOnRunId);
                Assert.Equal(first.GetProperty("offRunId").GetString(), second.GetProperty("offRunId").GetString());
                Assert.Equal(first.GetProperty("onRunId").GetString(), second.GetProperty("onRunId").GetString());
                Assert.Equal(expectedOffRunId, PerfRunner.RequireSafeRunId(expectedOffRunId));
                Assert.Equal(expectedOnRunId, PerfRunner.RequireSafeRunId(expectedOnRunId));
                Assert.Throws<ArgumentException>(() => PerfRunner.RequireSafeRunId(rejectedUnboundedCanonicalRunId));
                Assert.Throws<ArgumentException>(() => PerfRunner.RequireSafeRunId("../perf-run"));
                Assert.Throws<ArgumentException>(() => PerfRunner.RequireSafeRunId("perf/run"));
                Assert.Throws<ArgumentException>(() => PerfRunner.RequireSafeRunId(new string('a', 97)));
            }
            finally
            {
                DeleteDirectoryIfPresent(firstOutputRoot);
                DeleteDirectoryIfPresent(secondOutputRoot);
                DeleteDirectoryIfPresent(descriptorOnlyOutputRoot);
                DeleteDirectoryIfPresent(outputOnlyRoot);
                DeleteDirectoryIfPresent(mixedOutputRoot);
            }
        }

        private static JsonElement RunGeneratorContract(string descriptor, string outputRoot)
        {
            string scriptPath = FindRepositoryFile("scripts", "run-performance-pair.ps1");
            PowerShellResult result = RunPowerShell(
                scriptPath,
                "-RunIdContractDescriptor", descriptor,
                "-RunIdContractOutputRoot", outputRoot);
            Assert.True(result.ExitCode == 0, $"PowerShell contract failed: {result.StandardError}{result.StandardOutput}");

            string identityPath = Path.Combine(outputRoot, "run-identity.json");
            Assert.True(File.Exists(identityPath), $"Generator did not write {identityPath}. Output: {result.StandardOutput}");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(identityPath));
            return document.RootElement.Clone();
        }

        private static void AssertGeneratorContractRejected(
            string outputRoot,
            string expectedDiagnostic,
            params string[] arguments)
        {
            string scriptPath = FindRepositoryFile("scripts", "run-performance-pair.ps1");
            PowerShellResult result = RunPowerShell(scriptPath, arguments);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(expectedDiagnostic, NormalizeDiagnostic(result.StandardOutput + result.StandardError));
            Assert.False(Directory.Exists(outputRoot), $"Rejected contract invocation created output: {result.StandardOutput}{result.StandardError}");
        }

        private static string NormalizeDiagnostic(string value) => AnsiControlSequence.Replace(value, string.Empty);

        private static PowerShellResult RunPowerShell(string scriptPath, params string[] scriptArguments)
        {
            using Process process = StartPowerShell(scriptPath, scriptArguments);
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(standardOutputTask, standardErrorTask);
            return new PowerShellResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
        }

        private static Process StartPowerShell(string scriptPath, params string[] scriptArguments)
        {
            string[] candidates = OperatingSystem.IsWindows()
                ? new[] { "pwsh", "powershell" }
                : new[] { "pwsh" };
            Win32Exception? lastStartException = null;
            foreach (string candidate in candidates)
            {
                var startInfo = new ProcessStartInfo(candidate)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);
                foreach (string argument in scriptArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                try
                {
                    return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {candidate}.");
                }
                catch (Win32Exception exception)
                {
                    lastStartException = exception;
                }
            }

            throw new InvalidOperationException("PowerShell was not available for the generator contract test.", lastStartException);
        }

        private static string FindRepositoryFile(params string[] segments)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not locate the repository performance-pair script.");
        }

        private static void DeleteDirectoryIfPresent(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
