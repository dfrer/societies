using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Societies.Tests
{
    public partial class PerfRunner : Node
    {
        private const string MetricsEnvironmentVariable = "SOCIETIES_PERF_METRICS";
        private const string OutputEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";
        private const int MaximumMeasuredTicks = 4096;
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private bool _ownsOutputDirectory;

#if DEBUG
        private static readonly bool ManagedReleaseBuild = false;
        private const string ManagedBuildConfiguration = "Debug";
#else
        private static readonly bool ManagedReleaseBuild = true;
        private const string ManagedBuildConfiguration = "Release";
#endif

        public override void _Ready()
        {
            int exitCode = 1;
            PerformanceRunConfiguration? configuration = null;
            string exactInvocation = BuildExactInvocation();

            try
            {
                configuration = ParseConfiguration(OS.GetCmdlineUserArgs());
                exitCode = Execute(configuration, exactInvocation);
            }
            catch (Exception exception)
            {
                GD.PushError($"Performance runner failed: {exception}");
                if (_ownsOutputDirectory)
                {
                    TryWriteFailureResult(configuration, exactInvocation, exception);
                }
            }

            GetTree().Quit(exitCode);
        }

        private int Execute(PerformanceRunConfiguration configuration, string exactInvocation)
        {
            if (Directory.Exists(configuration.OutputDirectory) || File.Exists(configuration.OutputDirectory))
            {
                throw new IOException($"Performance output path already exists: {configuration.OutputDirectory}");
            }

            Directory.CreateDirectory(configuration.OutputDirectory);
            _ownsOutputDirectory = true;

            string? previousMetricsSetting = System.Environment.GetEnvironmentVariable(MetricsEnvironmentVariable);
            string? previousOutputDirectory = System.Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            GameManager? manager = null;

            try
            {
                System.Environment.SetEnvironmentVariable(
                    MetricsEnvironmentVariable,
                    configuration.MetricsEnabled ? "1" : null);
                System.Environment.SetEnvironmentVariable(OutputEnvironmentVariable, configuration.OutputDirectory);

                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn")
                    ?? throw new InvalidOperationException("Main scene failed to load.");
                manager = packedScene.Instantiate() as GameManager
                    ?? throw new InvalidOperationException("Main scene root is not GameManager.");
                manager.ConfigurePerformanceStartup(
                    configuration.ScenarioId,
                    configuration.SimulationSeed,
                    configuration.CitizenCount);
                manager.SetProcess(false);

                long sceneSetupStart = Stopwatch.GetTimestamp();
                AddChild(manager);
                double sceneSetupMilliseconds = Stopwatch.GetElapsedTime(sceneSetupStart).TotalMilliseconds;
                double bootstrapMilliseconds = manager.PerformanceBootstrapMilliseconds
                    ?? throw new InvalidOperationException("GameManager did not publish the performance bootstrap interval.");

                ValidateRuntimeIdentity(manager, configuration);

                RuntimeMetricsCollector? runtimeMetrics = manager.RuntimeMetrics;
                if (configuration.MetricsEnabled != (runtimeMetrics != null))
                {
                    throw new InvalidOperationException("Runtime metrics enablement did not match the requested mode.");
                }

                double warmupMilliseconds = 0.0;
                if (configuration.WarmupTicks > 0)
                {
                    long warmupStart = Stopwatch.GetTimestamp();
                    manager.StepSimulationTicks(configuration.WarmupTicks);
                    warmupMilliseconds = Stopwatch.GetElapsedTime(warmupStart).TotalMilliseconds;
                }
                runtimeMetrics?.Reset();

                long measuredStartSimulationTick = manager.SimulationTick;
                var externalTickSamples = new double[configuration.MeasuredTicks];
                long measuredIntervalStart = Stopwatch.GetTimestamp();
                for (int index = 0; index < externalTickSamples.Length; index++)
                {
                    long tickStart = Stopwatch.GetTimestamp();
                    manager.StepSimulationTicks(1);
                    externalTickSamples[index] = Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds;
                }
                double measuredIntervalMilliseconds = Stopwatch.GetElapsedTime(measuredIntervalStart).TotalMilliseconds;

                long expectedFinalTick = measuredStartSimulationTick + configuration.MeasuredTicks;
                if (manager.SimulationTick != expectedFinalTick)
                {
                    throw new InvalidOperationException(
                        $"Measured run ended at tick {manager.SimulationTick}; expected {expectedFinalTick}.");
                }
                if (!externalTickSamples.Any(sample => sample > 0.0))
                {
                    throw new InvalidOperationException("External timing produced no non-zero tick sample.");
                }

                PerformanceSampleStatistics externalStatistics = PerformanceRunStatistics.Compute(externalTickSamples);
                PerformanceBudgetAssessment budget = PerformanceRunBudgets.EvaluateForRun(
                    externalStatistics,
                    bootstrapMilliseconds,
                    configuration.ScenarioId,
                    configuration.SimulationSeed,
                    configuration.CitizenCount,
                    configuration.MeasuredTicks,
                    configuration.MetricsEnabled,
                    ManagedReleaseBuild);
                configuration.BudgetProfile = budget.Profile;

                long artifactStart = Stopwatch.GetTimestamp();
                string legacySnapshotPath = manager.SaveSnapshotToDisk();
                double artifactSerializationMilliseconds = Stopwatch.GetElapsedTime(artifactStart).TotalMilliseconds;
                if (string.IsNullOrWhiteSpace(legacySnapshotPath) || !File.Exists(legacySnapshotPath))
                {
                    throw new InvalidOperationException("Core snapshot serialization did not produce the legacy snapshot artifact.");
                }

                PerformanceRunArtifacts artifacts = BuildArtifactPaths(configuration);
                ValidateCoreArtifacts(artifacts);
                PerformanceRunHashes hashes = BuildHashes(artifacts);

                PerformanceDiagnosticsSummary? diagnostics = null;
                PerformanceSampleStatistics? internalStatistics = null;
                if (configuration.MetricsEnabled)
                {
                    (diagnostics, internalStatistics) = ValidateEnabledRuntimeMetrics(
                        runtimeMetrics!,
                        artifacts.RuntimeMetricsCsv!,
                        configuration);
                }
                else
                {
                    if (runtimeMetrics != null)
                    {
                        throw new InvalidOperationException("Metrics-disabled run allocated a collector.");
                    }
                    if (artifacts.RuntimeMetricsCsv != null && File.Exists(artifacts.RuntimeMetricsCsv))
                    {
                        throw new InvalidOperationException("Metrics-disabled run emitted a runtime metrics CSV.");
                    }
                    artifacts.RuntimeMetricsCsv = null;
                }

                string status = budget.SafetyPassed == false
                    ? configuration.AllowSafetyFailure ? "safety_failure_allowed" : "safety_failure"
                    : budget.TargetPassed == false
                        ? "target_missed"
                        : configuration.MetricsEnabled
                            ? "diagnostic_complete"
                            : budget.HasTargetGate || budget.HasSafetyGate
                                ? "pass"
                                : "characterization_complete";

                var intervals = new PerformanceRunIntervals
                {
                    SceneSetupMilliseconds = sceneSetupMilliseconds,
                    BootstrapMilliseconds = bootstrapMilliseconds,
                    WarmupMilliseconds = warmupMilliseconds,
                    MeasuredTicksMilliseconds = measuredIntervalMilliseconds,
                    CoreArtifactSerializationMilliseconds = artifactSerializationMilliseconds,
                    SceneSetupBoundary = "GameManager AddChild entry through completed _Ready",
                    BootstrapBoundary = "PrototypeRuntimeSession.Initialize entry through first synchronized runtime presentation and metrics snapshot",
                    WarmupBoundary = configuration.WarmupTicks > 0
                        ? "unmeasured deterministic simulation ticks; collector reset afterward"
                        : "none",
                    MeasurementBoundary = "one external Stopwatch sample around each StepSimulationTicks(1) call",
                    ArtifactBoundary = "SaveSnapshotToDisk only; performance report serialization excluded"
                };
                PerformanceRunEnvironment environment = BuildEnvironment();
                var result = new PerformanceRunResult
                {
                    CapturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Status = status,
                    ExactInvocation = exactInvocation,
                    Configuration = configuration,
                    Environment = environment,
                    Intervals = intervals,
                    ExternalTickStatistics = externalStatistics,
                    InternalTickStatistics = internalStatistics,
                    Diagnostics = diagnostics,
                    Budget = budget,
                    MeasuredStartSimulationTick = measuredStartSimulationTick,
                    FinalSimulationTick = manager.SimulationTick,
                    Hashes = hashes,
                    Artifacts = artifacts,
                    Notes = BuildNotes(configuration)
                };

                WriteResultArtifacts(result);
                GD.Print(
                    $"PERF {configuration.RunId}: {status}; p95={externalStatistics.P95Milliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
                    $"hash={hashes.DeterministicStateAndEventSha256}");

                return budget.SafetyPassed == false && !configuration.AllowSafetyFailure ? 2 : 0;
            }
            finally
            {
                if (manager != null && GodotObject.IsInstanceValid(manager))
                {
                    manager.GetParent()?.RemoveChild(manager);
                    manager.Free();
                }

                System.Environment.SetEnvironmentVariable(MetricsEnvironmentVariable, previousMetricsSetting);
                System.Environment.SetEnvironmentVariable(OutputEnvironmentVariable, previousOutputDirectory);
            }
        }

        private static PerformanceRunConfiguration ParseConfiguration(IReadOnlyList<string> arguments)
        {
            if (arguments.Count == 0 || arguments.Count % 2 != 0)
            {
                throw new ArgumentException("Performance runner arguments must be supplied as --name value pairs.");
            }

            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "--scenario",
                "--seed",
                "--citizens",
                "--ticks",
                "--warmup-ticks",
                "--metrics",
                "--output-dir",
                "--run-id",
                "--git-sha",
                "--git-dirty",
                "--allow-safety-failure"
            };
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Count; index += 2)
            {
                string key = arguments[index];
                if (!allowed.Contains(key))
                {
                    throw new ArgumentException($"Unknown performance runner argument '{key}'.");
                }
                if (!values.TryAdd(key, arguments[index + 1]))
                {
                    throw new ArgumentException($"Duplicate performance runner argument '{key}'.");
                }
            }

            foreach (string key in allowed)
            {
                if (!values.ContainsKey(key))
                {
                    throw new ArgumentException($"Missing required performance runner argument '{key}'.");
                }
            }

            string scenarioId = RequireText(values["--scenario"], "scenario id");
            string runId = RequireSafeRunId(values["--run-id"]);
            string gitSha = RequireText(values["--git-sha"], "git SHA");
            int seed = ParseInt(values["--seed"], "seed");
            int citizens = ParseInt(values["--citizens"], "citizens");
            int ticks = ParseInt(values["--ticks"], "ticks");
            int warmupTicks = ParseInt(values["--warmup-ticks"], "warmup ticks");
            if (citizens is < 1 or > 256)
            {
                throw new ArgumentOutOfRangeException("citizens", "Citizen count must be between 1 and 256.");
            }
            if (ticks is < 1 or > MaximumMeasuredTicks)
            {
                throw new ArgumentOutOfRangeException("ticks", $"Measured ticks must be between 1 and {MaximumMeasuredTicks}.");
            }
            if (warmupTicks is < 0 or > 100000)
            {
                throw new ArgumentOutOfRangeException("warmup-ticks", "Warmup ticks must be between 0 and 100000.");
            }

            bool metricsEnabled = values["--metrics"] switch
            {
                "on" => true,
                "off" => false,
                _ => throw new ArgumentException("Metrics mode must be 'on' or 'off'.")
            };

            return new PerformanceRunConfiguration
            {
                ScenarioId = scenarioId,
                SimulationSeed = seed,
                CitizenCount = citizens,
                WarmupTicks = warmupTicks,
                MeasuredTicks = ticks,
                MetricsEnabled = metricsEnabled,
                AllowSafetyFailure = ParseBool(values["--allow-safety-failure"], "allow safety failure"),
                OutputDirectory = Path.GetFullPath(RequireText(values["--output-dir"], "output directory")),
                RunId = runId,
                GitSha = gitSha,
                GitDirty = ParseBool(values["--git-dirty"], "git dirty"),
                WarmupMode = warmupTicks > 0 ? "simulation_ticks" : "none",
                CacheWarmupEnabled = false,
                BudgetProfile = PerformanceBudgetProfile.Characterization
            };
        }

        private static void ValidateRuntimeIdentity(GameManager manager, PerformanceRunConfiguration configuration)
        {
            if (!string.Equals(manager.CurrentScenarioId, configuration.ScenarioId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime scenario '{manager.CurrentScenarioId}' does not match requested '{configuration.ScenarioId}'.");
            }
            if (manager.SimulationSeed != configuration.SimulationSeed)
            {
                throw new InvalidOperationException(
                    $"Runtime seed {manager.SimulationSeed} does not match requested {configuration.SimulationSeed}.");
            }
            if (manager.CitizenCount != configuration.CitizenCount)
            {
                throw new InvalidOperationException(
                    $"Runtime citizen count {manager.CitizenCount} does not match requested {configuration.CitizenCount}.");
            }
            if (manager.SimulationTick != 0)
            {
                throw new InvalidOperationException($"Performance run must start at tick 0, not {manager.SimulationTick}.");
            }
        }

        private static PerformanceRunArtifacts BuildArtifactPaths(PerformanceRunConfiguration configuration)
        {
            string root = configuration.OutputDirectory;
            return new PerformanceRunArtifacts
            {
                Snapshot = Path.Combine(root, "snapshot-v2.json"),
                EventLog = Path.Combine(root, "event-log-v2.json"),
                RunSummary = Path.Combine(root, "run-summary-v2.json"),
                WorldSummary = Path.Combine(root, "world-summary-v2.json"),
                DeterministicMetricsCsv = Path.Combine(root, "metrics-timeseries-v2.csv"),
                RuntimeMetricsCsv = Path.Combine(root, "runtime-batch-metrics-v3.csv"),
                PerformanceResults = Path.Combine(root, "perf-results.json"),
                PerformanceMatrix = Path.Combine(root, "perf-matrix.csv"),
                PerformanceSummary = Path.Combine(root, "perf-summary.txt"),
                ValidationManifest = Path.Combine(root, "validation-manifest.json")
            };
        }

        private static void ValidateCoreArtifacts(PerformanceRunArtifacts artifacts)
        {
            foreach (string path in new[]
            {
                artifacts.Snapshot,
                artifacts.EventLog,
                artifacts.RunSummary,
                artifacts.WorldSummary,
                artifacts.DeterministicMetricsCsv
            })
            {
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException($"Expected core artifact was not written: {path}");
                }
            }
        }

        private static PerformanceRunHashes BuildHashes(PerformanceRunArtifacts artifacts)
        {
            byte[] snapshotBytes = File.ReadAllBytes(artifacts.Snapshot);
            byte[] eventLogBytes = File.ReadAllBytes(artifacts.EventLog);
            using IncrementalHash combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            combined.AppendData(snapshotBytes);
            combined.AppendData(Utf8NoBom.GetBytes("\n--event-log--\n"));
            combined.AppendData(eventLogBytes);

            return new PerformanceRunHashes
            {
                SnapshotSha256 = ToLowerHex(SHA256.HashData(snapshotBytes)),
                EventLogSha256 = ToLowerHex(SHA256.HashData(eventLogBytes)),
                DeterministicStateAndEventSha256 = ToLowerHex(combined.GetHashAndReset())
            };
        }

        private static (PerformanceDiagnosticsSummary Diagnostics, PerformanceSampleStatistics InternalStatistics)
            ValidateEnabledRuntimeMetrics(
                RuntimeMetricsCollector metrics,
                string runtimeMetricsPath,
                PerformanceRunConfiguration configuration)
        {
            if (metrics.DroppedBatchCount != 0)
            {
                throw new InvalidOperationException($"Runtime metrics dropped {metrics.DroppedBatchCount} batches.");
            }

            RuntimeMetricsBatch[] batches = metrics.SnapshotBatches();
            if (batches.Length != configuration.MeasuredTicks)
            {
                throw new InvalidOperationException(
                    $"Runtime metrics contain {batches.Length} batches for {configuration.MeasuredTicks} measured ticks.");
            }

            for (int index = 0; index < batches.Length; index++)
            {
                RuntimeMetricsBatch batch = batches[index];
                if (batch.Sequence != index ||
                    batch.Kind != RuntimeMetricsBatchKind.ManualStep ||
                    batch.CompletedTicks != 1 ||
                    batch.EndSimulationTick - batch.StartSimulationTick != 1)
                {
                    throw new InvalidOperationException($"Runtime metrics batch {index} is not a one-tick manual sample.");
                }
                if (batch.PathPlanCacheHitsTotal + batch.PathPlanCacheMissesTotal != batch.PathPlanLookupsTotal)
                {
                    throw new InvalidOperationException($"Runtime metrics batch {index} violates path lookup accounting.");
                }
                if (batch.WorkerCountLast != configuration.CitizenCount)
                {
                    throw new InvalidOperationException($"Runtime metrics batch {index} has the wrong worker count.");
                }
            }

            if (!batches.Any(batch => batch.MaximumTickMilliseconds > 0.0))
            {
                throw new InvalidOperationException("Runtime metrics contain no non-zero tick measurement.");
            }
            ValidateRuntimeMetricsCsv(runtimeMetricsPath, configuration.MeasuredTicks);

            long pathLookups = batches.Sum(batch => batch.PathPlanLookupsTotal);
            long pathHits = batches.Sum(batch => batch.PathPlanCacheHitsTotal);
            long pathMisses = batches.Sum(batch => batch.PathPlanCacheMissesTotal);
            long idleCitizens = batches.Sum(batch => batch.IdleCitizensConsideringWorkOrdersTotal);
            long candidateOrders = batches.Sum(batch => batch.CandidateOrdersEvaluatedTotal);
            var diagnostics = new PerformanceDiagnosticsSummary
            {
                BatchCount = batches.Length,
                DroppedBatchCount = metrics.DroppedBatchCount,
                PathPlanLookups = pathLookups,
                PathPlanCacheHits = pathHits,
                PathPlanCacheMisses = pathMisses,
                PathPlanCacheHitRate = pathLookups > 0 ? (double)pathHits / pathLookups : null,
                PathPlanCacheSizeLast = batches[^1].PathPlanCacheSizeLast,
                WorkerCountLast = batches[^1].WorkerCountLast,
                IdleCitizensConsideringWorkOrders = idleCitizens,
                CandidateOrdersEvaluated = candidateOrders,
                CandidateOrdersPerIdleCitizen = idleCitizens > 0 ? (double)candidateOrders / idleCitizens : null,
                NavigationInvalidations = batches.Sum(batch => batch.NavigationInvalidationsTotal),
                WorkOrdersGenerated = batches.Sum(batch => batch.WorkOrdersGeneratedTotal),
                WorkOrdersGeneratedUncapped = batches.Sum(batch => batch.WorkOrdersGeneratedUncappedTotal),
                WorkOrdersClaimed = batches.Sum(batch => batch.WorkOrdersClaimedTotal),
                WorkOrdersRemainingLast = batches[^1].WorkOrdersRemainingLast,
                Phases = new PerformancePhaseSummary
                {
                    SimulationTickMilliseconds = batches.Sum(batch => batch.Phases.SimulationTickMilliseconds),
                    SessionAdvanceMilliseconds = batches.Sum(batch => batch.Phases.SessionAdvanceMilliseconds),
                    BuildWorkOrdersMilliseconds = batches.Sum(batch => batch.Phases.BuildWorkOrdersMilliseconds),
                    HarvestApplyMilliseconds = batches.Sum(batch => batch.Phases.HarvestApplyMilliseconds),
                    SceneSyncMilliseconds = batches.Sum(batch => batch.Phases.SceneSyncMilliseconds),
                    UpdateHudMilliseconds = batches.Sum(batch => batch.Phases.UpdateHudMilliseconds),
                    NavigationRebuildMilliseconds = batches.Sum(batch => batch.Phases.NavigationRebuildMilliseconds)
                }
            };
            PerformanceSampleStatistics internalStatistics = PerformanceRunStatistics.Compute(
                batches.Select(batch => batch.MaximumTickMilliseconds).ToArray());
            return (diagnostics, internalStatistics);
        }

        private static void ValidateRuntimeMetricsCsv(string path, int measuredTicks)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Metrics-enabled run did not export runtime-batch-metrics-v3.csv.");
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length != measuredTicks + 1)
            {
                throw new InvalidOperationException(
                    $"Runtime metrics CSV contains {lines.Length - 1} rows for {measuredTicks} measured ticks.");
            }

            string[] header = lines[0].Split(',');
            if (header.Length != 28 || !header.Contains("navigation_rebuild_ms", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Runtime metrics CSV schema is not the expected 28-column V3 schema.");
            }

            for (int index = 1; index < lines.Length; index++)
            {
                string[] columns = lines[index].Split(',');
                if (columns.Length != 28 ||
                    !string.Equals(columns[1], "manual_step", StringComparison.Ordinal) ||
                    !string.Equals(columns[4], "1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Runtime metrics CSV row {index} is not a one-tick manual sample.");
                }
            }
        }

        private static PerformanceRunEnvironment BuildEnvironment()
        {
            Godot.Collections.Dictionary version = Engine.GetVersionInfo();
            string godotVersion = version.ContainsKey("string")
                ? version["string"].AsString()
                : version.ToString();
            return new PerformanceRunEnvironment
            {
                MachineName = System.Environment.MachineName,
                LogicalProcessorCount = System.Environment.ProcessorCount,
                OperatingSystem = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                DotnetRuntime = RuntimeInformation.FrameworkDescription,
                GodotVersion = godotVersion,
                ManagedBuildConfiguration = ManagedBuildConfiguration,
                GodotDebugBuild = OS.IsDebugBuild()
            };
        }

        private static List<string> BuildNotes(PerformanceRunConfiguration configuration)
        {
            var notes = new List<string>
            {
                "Warmup ticks advance deterministic simulation state; eager route-cache warmup is disabled.",
                "This is a single-run indicator. The release gate requires the median of three comparable metrics-off runs.",
                "Core artifact serialization and runner report serialization are excluded from measured tick samples."
            };
            if (!ManagedReleaseBuild)
            {
                notes.Add("Managed Debug assembly detected; timing is characterization only and is not a Release baseline.");
            }
            if (configuration.MetricsEnabled)
            {
                notes.Add("Metrics-enabled timing is diagnostic; the equivalent metrics-off run is the primary timing source.");
            }
            return notes;
        }

        private static void WriteResultArtifacts(PerformanceRunResult result)
        {
            WriteJson(result.Artifacts.PerformanceResults, result);
            File.WriteAllText(result.Artifacts.PerformanceMatrix, BuildMatrixCsv(result), Utf8NoBom);
            File.WriteAllText(result.Artifacts.PerformanceSummary, BuildSummary(result), Utf8NoBom);

            var manifest = new PerformanceValidationManifest
            {
                CapturedUtc = result.CapturedUtc,
                Status = result.Status,
                ExactInvocation = result.ExactInvocation,
                AssessmentScope = result.AssessmentScope,
                GitSha = result.Configuration.GitSha,
                GitDirty = result.Configuration.GitDirty,
                Configuration = result.Configuration,
                Environment = result.Environment,
                Intervals = result.Intervals,
                Hashes = result.Hashes,
                Budget = result.Budget,
                Artifacts = result.Artifacts
            };
            WriteJson(result.Artifacts.ValidationManifest, manifest);
        }

        private static string BuildMatrixCsv(PerformanceRunResult result)
        {
            const string header =
                "run_id,status,scenario,seed,citizens,warmup_ticks,measured_ticks,metrics_enabled,managed_build," +
                "budget_profile,bootstrap_ms,warmup_ms,p50_ms,p95_ms,p99_ms,max_ms,mean_ms,total_ms," +
                "serialization_ms,path_lookups,path_hits,path_misses,cache_size_last,candidates_per_idle," +
                "invalidations,navigation_rebuild_ms,work_orders_generated,work_orders_generated_uncapped," +
                "work_orders_claimed,work_orders_remaining_last,state_event_sha256,target_passed,safety_passed," +
                "snapshot_path,event_log_path,runtime_metrics_csv,perf_results_path";
            PerformanceDiagnosticsSummary? diagnostics = result.Diagnostics;
            string row = string.Join(',', new[]
            {
                Csv(result.Configuration.RunId),
                Csv(result.Status),
                Csv(result.Configuration.ScenarioId),
                result.Configuration.SimulationSeed.ToString(CultureInfo.InvariantCulture),
                result.Configuration.CitizenCount.ToString(CultureInfo.InvariantCulture),
                result.Configuration.WarmupTicks.ToString(CultureInfo.InvariantCulture),
                result.Configuration.MeasuredTicks.ToString(CultureInfo.InvariantCulture),
                result.Configuration.MetricsEnabled ? "true" : "false",
                Csv(result.Environment.ManagedBuildConfiguration),
                Csv(result.Budget.Profile.ToString()),
                Format(result.Intervals.BootstrapMilliseconds),
                Format(result.Intervals.WarmupMilliseconds),
                Format(result.ExternalTickStatistics.P50Milliseconds),
                Format(result.ExternalTickStatistics.P95Milliseconds),
                Format(result.ExternalTickStatistics.P99Milliseconds),
                Format(result.ExternalTickStatistics.MaximumMilliseconds),
                Format(result.ExternalTickStatistics.MeanMilliseconds),
                Format(result.ExternalTickStatistics.TotalMilliseconds),
                Format(result.Intervals.CoreArtifactSerializationMilliseconds),
                diagnostics?.PathPlanLookups.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.PathPlanCacheHits.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.PathPlanCacheMisses.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.PathPlanCacheSizeLast?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.CandidateOrdersPerIdleCitizen is double candidateRatio ? Format(candidateRatio) : string.Empty,
                diagnostics?.NavigationInvalidations.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics != null ? Format(diagnostics.Phases.NavigationRebuildMilliseconds) : string.Empty,
                diagnostics?.WorkOrdersGenerated.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.WorkOrdersGeneratedUncapped.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.WorkOrdersClaimed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.WorkOrdersRemainingLast?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.Hashes.DeterministicStateAndEventSha256,
                FormatNullableBool(result.Budget.TargetPassed),
                FormatNullableBool(result.Budget.SafetyPassed),
                Csv(result.Artifacts.Snapshot),
                Csv(result.Artifacts.EventLog),
                Csv(result.Artifacts.RuntimeMetricsCsv ?? string.Empty),
                Csv(result.Artifacts.PerformanceResults)
            });
            return $"{header}{System.Environment.NewLine}{row}{System.Environment.NewLine}";
        }

        private static string BuildSummary(PerformanceRunResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Societies performance run: {result.Configuration.RunId}");
            builder.AppendLine($"Status: {result.Status}");
            builder.AppendLine($"Configuration: {result.Configuration.ScenarioId}, seed {result.Configuration.SimulationSeed}, {result.Configuration.CitizenCount} citizens, {result.Configuration.MeasuredTicks} measured ticks, metrics {(result.Configuration.MetricsEnabled ? "on" : "off")}");
            builder.AppendLine($"Managed build: {result.Environment.ManagedBuildConfiguration}");
            builder.AppendLine($"Bootstrap: {Format(result.Intervals.BootstrapMilliseconds)} ms");
            builder.AppendLine($"Ticks: p50 {Format(result.ExternalTickStatistics.P50Milliseconds)} ms, p95 {Format(result.ExternalTickStatistics.P95Milliseconds)} ms, p99 {Format(result.ExternalTickStatistics.P99Milliseconds)} ms, max {Format(result.ExternalTickStatistics.MaximumMilliseconds)} ms, total {Format(result.ExternalTickStatistics.TotalMilliseconds)} ms");
            builder.AppendLine($"Budget profile: {result.Budget.Profile}; target {FormatNullableBool(result.Budget.TargetPassed)}; safety {FormatNullableBool(result.Budget.SafetyPassed)}");
            builder.AppendLine($"Deterministic state+event hash: {result.Hashes.DeterministicStateAndEventSha256}");
            builder.AppendLine($"Output: {result.Configuration.OutputDirectory}");
            return builder.ToString();
        }

        private static void WriteJson<T>(string path, T value)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), Utf8NoBom);
        }

        private static void TryWriteFailureResult(
            PerformanceRunConfiguration? configuration,
            string exactInvocation,
            Exception exception)
        {
            if (configuration == null || !Directory.Exists(configuration.OutputDirectory))
            {
                return;
            }

            try
            {
                var failure = new PerformanceFailureResult
                {
                    CapturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    ErrorType = exception.GetType().FullName ?? exception.GetType().Name,
                    ErrorMessage = exception.Message,
                    ExactInvocation = exactInvocation,
                    Configuration = configuration
                };
                WriteJson(Path.Combine(configuration.OutputDirectory, "perf-results.json"), failure);
            }
            catch (Exception writeException)
            {
                GD.PushError($"Could not write performance failure artifact: {writeException.Message}");
            }
        }

        private static string BuildExactInvocation()
        {
            string userArguments = string.Join(" ", OS.GetCmdlineUserArgs().Select(QuoteArgument));
            return $"godot --headless --path src/societies res://tests/PerfRunner.tscn -- {userArguments}";
        }

        private static string QuoteArgument(string value)
        {
            return value.Any(char.IsWhiteSpace) || value.Contains('"')
                ? $"\"{value.Replace("\"", "\\\"")}\""
                : value;
        }

        private static string RequireText(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"A non-empty {label} is required.");
            }
            return value;
        }

        private static string RequireSafeRunId(string value)
        {
            string runId = RequireText(value, "run id");
            if (runId.Length > 96 ||
                runId.Contains("..", StringComparison.Ordinal) ||
                runId.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            {
                throw new ArgumentException("Run id may contain only letters, digits, '.', '_' and '-' and may not contain '..'.");
            }
            return runId;
        }

        private static int ParseInt(string value, string label)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException($"Invalid {label} value '{value}'.");
            }
            return parsed;
        }

        private static bool ParseBool(string value, string label)
        {
            return value switch
            {
                "true" => true,
                "false" => false,
                _ => throw new ArgumentException($"Invalid {label} value '{value}'; expected true or false.")
            };
        }

        private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string FormatNullableBool(bool? value) => value.HasValue ? value.Value ? "true" : "false" : "not_applicable";

        private static string Csv(string value)
        {
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        private static string ToLowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
