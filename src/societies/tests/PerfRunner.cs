using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private const string ColdCacheMode = "cold";
        private const string NaturalWarmCacheMode = "natural_warm";
        private const string ForcedInvalidationCacheMode = "forced_invalidation";
        private const int MaximumMeasuredTicks = 4096;
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private bool _ownsOutputDirectory;
        private PerformanceCacheEvidence? _latestCacheEvidence;

#if DEBUG
        private const string ManagedBuildConfiguration = "Debug";
#else
        private const string ManagedBuildConfiguration = "Release";
#endif

        public override void _Ready()
        {
            int exitCode = 1;
            PerformanceRunConfiguration? configuration = null;
            string exactInvocation = BuildRawInvocation();

            try
            {
                configuration = ParseConfiguration(OS.GetCmdlineUserArgs());
                exactInvocation = BuildExactInvocation(configuration);
                exitCode = Execute(configuration, exactInvocation);
            }
            catch (Exception exception)
            {
                GD.PushError($"Performance runner failed: {exception}");
                if (_ownsOutputDirectory)
                {
                    TryWriteFailureResult(configuration, exactInvocation, exception, _latestCacheEvidence);
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

                var cacheEvidence = new PerformanceCacheEvidence
                {
                    CacheMode = configuration.CacheMode,
                    PreparationStrategy = GetCachePreparationStrategy(configuration),
                    AfterBootstrap = manager.CapturePerformanceProbeState()
                };
                _latestCacheEvidence = cacheEvidence;

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
                cacheEvidence.AfterNaturalWarmup = manager.CapturePerformanceProbeState();

                long cachePreparationStart = Stopwatch.GetTimestamp();
                switch (configuration.CacheMode)
                {
                    case ColdCacheMode:
                        cacheEvidence.ClearedEntryCount = manager.ClearDerivedPathCacheForPerformance();
                        break;
                    case NaturalWarmCacheMode:
                        break;
                    case ForcedInvalidationCacheMode:
                        if (!manager.TryPrepareForcedPathCompletionForPerformance(out string forcedStructureId) ||
                            string.IsNullOrWhiteSpace(forcedStructureId))
                        {
                            throw new InvalidOperationException(
                                "Forced-invalidation mode could not prepare a deterministic path-segment completion.");
                        }
                        cacheEvidence.PreparedForcedPathSegmentStructureId = forcedStructureId;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported cache mode '{configuration.CacheMode}'.");
                }
                double cachePreparationMilliseconds = Stopwatch.GetElapsedTime(cachePreparationStart).TotalMilliseconds;
                cacheEvidence.BeforeMeasurement = manager.CapturePerformanceProbeState();
                ValidatePreparedCacheEvidence(configuration, cacheEvidence);
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
                cacheEvidence.AfterMeasurement = manager.CapturePerformanceProbeState();
                if (configuration.CacheMode == ForcedInvalidationCacheMode)
                {
                    cacheEvidence.ForcedInvalidation = new PerformanceForcedInvalidationEvidence
                    {
                        NavigationInvalidationCount =
                            cacheEvidence.AfterMeasurement.TotalNavigationInvalidations -
                            cacheEvidence.BeforeMeasurement.TotalNavigationInvalidations,
                        Probe = cacheEvidence.AfterMeasurement.ForcedInvalidation
                    };
                }
                ValidateCompletedCacheEvidence(configuration, cacheEvidence);

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
                PerformanceRunEnvironment environment = BuildEnvironment();
                PerformanceBudgetAssessment budget = PerformanceRunBudgets.EvaluateForRun(
                    externalStatistics,
                    bootstrapMilliseconds,
                    configuration.ScenarioId,
                    configuration.SimulationSeed,
                    configuration.CitizenCount,
                    configuration.MeasuredTicks,
                    configuration.MetricsEnabled,
                    environment.VerifiedReleaseExecution);
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
                    CachePreparationMilliseconds = cachePreparationMilliseconds,
                    MeasuredTicksMilliseconds = measuredIntervalMilliseconds,
                    CoreArtifactSerializationMilliseconds = artifactSerializationMilliseconds,
                    SceneSetupBoundary = "GameManager AddChild entry through completed _Ready",
                    BootstrapBoundary = "PrototypeRuntimeSession.Initialize entry through first synchronized runtime presentation and metrics snapshot",
                    WarmupBoundary = configuration.WarmupTicks > 0
                        ? "unmeasured deterministic simulation ticks; collector reset afterward"
                        : "none",
                    CachePreparationBoundary = GetCachePreparationBoundary(configuration),
                    MeasurementBoundary = "one external Stopwatch sample around each StepSimulationTicks(1) call",
                    ArtifactBoundary = "SaveSnapshotToDisk only; performance report serialization excluded"
                };
                var result = new PerformanceRunResult
                {
                    CapturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Status = status,
                    ExactInvocation = exactInvocation,
                    Configuration = configuration,
                    Environment = environment,
                    Intervals = intervals,
                    CacheEvidence = cacheEvidence,
                    ExternalTickStatistics = externalStatistics,
                    InternalTickStatistics = internalStatistics,
                    Diagnostics = diagnostics,
                    Budget = budget,
                    MeasuredStartSimulationTick = measuredStartSimulationTick,
                    FinalSimulationTick = manager.SimulationTick,
                    Hashes = hashes,
                    Artifacts = artifacts,
                    Notes = BuildNotes(configuration, environment)
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
                "--execution-route",
                "--project-path",
                "--runner-executable",
                "--cache-mode",
                "--comparison-group",
                "--trial-index",
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
            string comparisonGroup = RequireSafeIdentifier(values["--comparison-group"], "comparison group", 96);
            string gitSha = RequireText(values["--git-sha"], "git SHA");
            string executionRoute = values["--execution-route"] switch
            {
                "editor_scene" => "editor_scene",
                "export_release" => "export_release",
                _ => throw new ArgumentException("Execution route must be 'editor_scene' or 'export_release'.")
            };
            string projectPath = Path.GetFullPath(RequireText(values["--project-path"], "project path"));
            if (!File.Exists(Path.Combine(projectPath, "project.godot")))
            {
                throw new DirectoryNotFoundException(
                    $"The declared project path does not contain project.godot: {projectPath}");
            }
            string runnerExecutablePath = Path.GetFullPath(
                RequireText(values["--runner-executable"], "runner executable path"));
            if (!File.Exists(runnerExecutablePath))
            {
                throw new FileNotFoundException("The declared runner executable does not exist.", runnerExecutablePath);
            }
            int seed = ParseInt(values["--seed"], "seed");
            int citizens = ParseInt(values["--citizens"], "citizens");
            int ticks = ParseInt(values["--ticks"], "ticks");
            int warmupTicks = ParseInt(values["--warmup-ticks"], "warmup ticks");
            int trialIndex = ParseInt(values["--trial-index"], "trial index");
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
            if (trialIndex is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException("trial-index", "Trial index must be between 1 and 100.");
            }

            string cacheMode = values["--cache-mode"] switch
            {
                ColdCacheMode => ColdCacheMode,
                NaturalWarmCacheMode => NaturalWarmCacheMode,
                ForcedInvalidationCacheMode => ForcedInvalidationCacheMode,
                _ => throw new ArgumentException(
                    "Cache mode must be 'cold', 'natural_warm', or 'forced_invalidation'.")
            };
            if ((cacheMode is NaturalWarmCacheMode or ForcedInvalidationCacheMode) && warmupTicks == 0)
            {
                throw new ArgumentException($"Cache mode '{cacheMode}' requires at least one natural warmup tick.");
            }
            if (cacheMode == ForcedInvalidationCacheMode && ticks != 1)
            {
                throw new ArgumentException("Forced-invalidation mode requires exactly one measured tick.");
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
                ExecutionRoute = executionRoute,
                ProjectPath = projectPath,
                RunnerExecutablePath = runnerExecutablePath,
                CacheMode = cacheMode,
                ComparisonGroup = comparisonGroup,
                TrialIndex = trialIndex,
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

        private static void ValidatePreparedCacheEvidence(
            PerformanceRunConfiguration configuration,
            PerformanceCacheEvidence evidence)
        {
            if (configuration.CacheWarmupEnabled)
            {
                throw new InvalidOperationException("Eager/all-pairs cache warmup must remain disabled.");
            }
            if (!string.Equals(evidence.CacheMode, configuration.CacheMode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cache evidence mode does not match the requested configuration.");
            }
            if (evidence.AfterBootstrap.PathCacheEntryCount != 0)
            {
                throw new InvalidOperationException(
                    $"Fresh bootstrap retained {evidence.AfterBootstrap.PathCacheEntryCount} derived path-cache entries; expected zero.");
            }
            if (evidence.AfterBootstrap.SimulationTick != 0 ||
                evidence.AfterNaturalWarmup.SimulationTick != configuration.WarmupTicks ||
                evidence.BeforeMeasurement.SimulationTick != configuration.WarmupTicks)
            {
                throw new InvalidOperationException("Cache evidence snapshots do not match the configured warmup tick boundary.");
            }
            if (!evidence.AfterBootstrap.AllPathCacheKeysMatchNavigationRulesVersion ||
                !evidence.AfterNaturalWarmup.AllPathCacheKeysMatchNavigationRulesVersion ||
                !evidence.BeforeMeasurement.AllPathCacheKeysMatchNavigationRulesVersion)
            {
                throw new InvalidOperationException(
                    "A pre-measurement cache snapshot contains an entry from a stale navigation-rules version.");
            }

            switch (configuration.CacheMode)
            {
                case ColdCacheMode:
                    if (evidence.ClearedEntryCount != evidence.AfterNaturalWarmup.PathCacheEntryCount ||
                        evidence.BeforeMeasurement.PathCacheEntryCount != 0)
                    {
                        throw new InvalidOperationException(
                            "Cold mode must clear every derived path-cache entry before measurement.");
                    }
                    if (configuration.WarmupTicks > 0 &&
                        evidence.AfterNaturalWarmup.PathCacheEntryCount <= 0)
                    {
                        throw new InvalidOperationException(
                            "Cold control requires natural warmup to populate the cache before it is cleared.");
                    }
                    if (evidence.BeforeMeasurement.NavigationRulesVersion !=
                            evidence.AfterNaturalWarmup.NavigationRulesVersion ||
                        evidence.BeforeMeasurement.TotalNavigationInvalidations !=
                            evidence.AfterNaturalWarmup.TotalNavigationInvalidations ||
                        evidence.BeforeMeasurement.LastPathPlanRulesVersion !=
                            evidence.AfterNaturalWarmup.LastPathPlanRulesVersion)
                    {
                        throw new InvalidOperationException(
                            "Clearing the derived path cache must not change navigation or last-plan state.");
                    }
                    break;
                case NaturalWarmCacheMode:
                    if (evidence.ClearedEntryCount != 0 ||
                        evidence.AfterNaturalWarmup.PathCacheEntryCount <= 0 ||
                        evidence.BeforeMeasurement.PathCacheEntryCount !=
                        evidence.AfterNaturalWarmup.PathCacheEntryCount ||
                        evidence.BeforeMeasurement.NavigationRulesVersion !=
                            evidence.AfterNaturalWarmup.NavigationRulesVersion ||
                        evidence.BeforeMeasurement.TotalNavigationInvalidations !=
                            evidence.AfterNaturalWarmup.TotalNavigationInvalidations ||
                        evidence.BeforeMeasurement.LastPathPlanRulesVersion !=
                            evidence.AfterNaturalWarmup.LastPathPlanRulesVersion)
                    {
                        throw new InvalidOperationException(
                            "Natural-warm mode must retain the positive cache produced by deterministic warmup ticks.");
                    }
                    break;
                case ForcedInvalidationCacheMode:
                    PrototypeForcedInvalidationProbeSnapshot prepared = evidence.BeforeMeasurement.ForcedInvalidation;
                    if (evidence.ClearedEntryCount != 0 ||
                        evidence.AfterNaturalWarmup.PathCacheEntryCount <= 0 ||
                        evidence.BeforeMeasurement.PathCacheEntryCount <= 0 ||
                        !prepared.Prepared ||
                        prepared.Committed ||
                        prepared.PathSegmentWasBuiltBefore ||
                        prepared.PathSegmentIsBuiltAfter ||
                        string.IsNullOrWhiteSpace(prepared.PathSegmentStructureId) ||
                        !string.Equals(
                            prepared.PathSegmentStructureId,
                            evidence.PreparedForcedPathSegmentStructureId,
                            StringComparison.Ordinal) ||
                        evidence.BeforeMeasurement.NavigationRulesVersion !=
                            evidence.AfterNaturalWarmup.NavigationRulesVersion ||
                        evidence.BeforeMeasurement.TotalNavigationInvalidations !=
                            evidence.AfterNaturalWarmup.TotalNavigationInvalidations ||
                        evidence.BeforeMeasurement.LastPathPlanRulesVersion !=
                            evidence.BeforeMeasurement.NavigationRulesVersion)
                    {
                        throw new InvalidOperationException(
                            "Forced-invalidation mode must retain a positive cache and prepare one uncommitted path completion.");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported cache mode '{configuration.CacheMode}'.");
            }
        }

        private static void ValidateCompletedCacheEvidence(
            PerformanceRunConfiguration configuration,
            PerformanceCacheEvidence evidence)
        {
            if (evidence.AfterMeasurement.NavigationRulesVersion < evidence.BeforeMeasurement.NavigationRulesVersion)
            {
                throw new InvalidOperationException("Navigation-rules version moved backward during measurement.");
            }
            if (evidence.AfterMeasurement.SimulationTick !=
                (long)configuration.WarmupTicks + configuration.MeasuredTicks)
            {
                throw new InvalidOperationException("Post-measurement cache evidence has the wrong simulation tick.");
            }
            if (!evidence.AfterMeasurement.AllPathCacheKeysMatchNavigationRulesVersion)
            {
                throw new InvalidOperationException(
                    "Post-measurement cache evidence contains an entry from a stale navigation-rules version.");
            }
            if (configuration.CacheMode != ForcedInvalidationCacheMode)
            {
                if (evidence.ForcedInvalidation != null)
                {
                    throw new InvalidOperationException("A non-forced cache mode emitted forced-invalidation evidence.");
                }
                if (!PerformanceExecutionContract.HasConsistentNaturalNavigationInvalidations(
                        evidence.BeforeMeasurement.NavigationRulesVersion,
                        evidence.AfterMeasurement.NavigationRulesVersion,
                        evidence.BeforeMeasurement.TotalNavigationInvalidations,
                        evidence.AfterMeasurement.TotalNavigationInvalidations))
                {
                    throw new InvalidOperationException(
                        "Cold and natural-warm measurements require equal nonnegative navigation-version and invalidation-count deltas.");
                }
                return;
            }

            PerformanceForcedInvalidationEvidence forced = evidence.ForcedInvalidation
                ?? throw new InvalidOperationException("Forced-invalidation evidence is missing.");
            PrototypeForcedInvalidationProbeSnapshot prepared = evidence.BeforeMeasurement.ForcedInvalidation;
            PrototypeForcedInvalidationProbeSnapshot probe = forced.Probe;
            if (!probe.Prepared || !probe.Committed ||
                string.IsNullOrWhiteSpace(probe.PathSegmentStructureId) ||
                probe.PathSegmentWasBuiltBefore ||
                !probe.PathSegmentIsBuiltAfter ||
                !probe.VersionBeforeCommit.HasValue ||
                !probe.VersionAfterCommit.HasValue ||
                probe.VersionAfterCommit.Value != probe.VersionBeforeCommit.Value + 1 ||
                !probe.CompletionTick.HasValue ||
                probe.CompletionTick.Value != (long)configuration.WarmupTicks + 1L ||
                forced.NavigationInvalidationCount != 1 ||
                !probe.TotalInvalidationsBeforeCommit.HasValue ||
                !probe.TotalInvalidationsAfterCommit.HasValue ||
                probe.TotalInvalidationsAfterCommit != probe.TotalInvalidationsBeforeCommit + 1 ||
                !probe.CacheEntriesBeforeRebuild.HasValue ||
                probe.CacheEntriesBeforeRebuild.Value <= 0 ||
                !probe.CacheEntriesImmediatelyAfterRebuild.HasValue ||
                probe.CacheEntriesImmediatelyAfterRebuild.Value != 0 ||
                !probe.FirstPostChangeLookupObserved ||
                !probe.FirstPostChangeLookupWasCacheMiss ||
                !probe.FirstPostChangeLookupUsedNewVersion ||
                !probe.ChangedCellGridX.HasValue ||
                !probe.ChangedCellGridY.HasValue ||
                !probe.ProbeStartGridX.HasValue ||
                !probe.ProbeStartGridY.HasValue ||
                !probe.ProbeEndGridX.HasValue ||
                !probe.ProbeEndGridY.HasValue ||
                probe.PreChangeQueryVersion != probe.VersionBeforeCommit.Value ||
                probe.PreChangePlanVersion != probe.VersionBeforeCommit.Value ||
                probe.PostChangeQueryVersion != probe.VersionAfterCommit.Value ||
                probe.PostChangePlanVersion != probe.VersionAfterCommit.Value ||
                !probe.ExactEndpointsMatch ||
                !probe.ChangedCellIncludedInPostChangePlan ||
                !probe.PreChangePlanCost.HasValue ||
                !probe.PostChangePlanCost.HasValue ||
                !double.IsFinite(probe.PreChangePlanCost.Value) ||
                !double.IsFinite(probe.PostChangePlanCost.Value) ||
                probe.PreChangePlanCost.Value < 0.0 ||
                probe.PostChangePlanCost.Value < 0.0 ||
                probe.PostChangePlanCost.Value >= probe.PreChangePlanCost.Value ||
                !probe.CommitToFirstLookupMilliseconds.HasValue ||
                !double.IsFinite(probe.CommitToFirstLookupMilliseconds.Value) ||
                probe.CommitToFirstLookupMilliseconds.Value < 0.0)
            {
                throw new InvalidOperationException(
                    "Forced-invalidation probe did not prove an exact post-change cache miss and correct rebuilt route.");
            }
            if (!string.Equals(
                    evidence.PreparedForcedPathSegmentStructureId,
                    probe.PathSegmentStructureId,
                    StringComparison.Ordinal) ||
                !string.Equals(prepared.PathSegmentStructureId, probe.PathSegmentStructureId, StringComparison.Ordinal) ||
                prepared.ChangedCellGridX != probe.ChangedCellGridX ||
                prepared.ChangedCellGridY != probe.ChangedCellGridY ||
                prepared.ProbeStartGridX != probe.ProbeStartGridX ||
                prepared.ProbeStartGridY != probe.ProbeStartGridY ||
                prepared.ProbeEndGridX != probe.ProbeEndGridX ||
                prepared.ProbeEndGridY != probe.ProbeEndGridY ||
                prepared.PreChangeQueryVersion != probe.PreChangeQueryVersion ||
                prepared.PreChangePlanVersion != probe.PreChangePlanVersion ||
                prepared.PreChangePlanCost != probe.PreChangePlanCost ||
                probe.VersionBeforeCommit != evidence.BeforeMeasurement.NavigationRulesVersion ||
                probe.TotalInvalidationsBeforeCommit != evidence.BeforeMeasurement.TotalNavigationInvalidations ||
                probe.VersionAfterCommit != evidence.AfterMeasurement.NavigationRulesVersion ||
                probe.TotalInvalidationsAfterCommit != evidence.AfterMeasurement.TotalNavigationInvalidations)
            {
                throw new InvalidOperationException(
                    "Forced-invalidation preparation and completion evidence do not describe one continuous probe.");
            }
            if (evidence.AfterMeasurement.NavigationRulesVersion != probe.VersionAfterCommit.Value ||
                evidence.AfterMeasurement.LastPathPlanRulesVersion != probe.VersionAfterCommit.Value)
            {
                throw new InvalidOperationException(
                    "Post-measurement navigation state does not match the forced probe's committed version.");
            }
        }

        private static string GetCachePreparationStrategy(PerformanceRunConfiguration configuration)
        {
            return configuration.CacheMode switch
            {
                ColdCacheMode when configuration.WarmupTicks == 0 =>
                    "fresh_session_then_confirm_empty_derived_path_cache",
                ColdCacheMode => "natural_warmup_then_clear_derived_path_cache",
                NaturalWarmCacheMode => "natural_warmup_then_retain_derived_path_cache",
                ForcedInvalidationCacheMode =>
                    "natural_warmup_then_seed_exact_probe_and_prepare_path_completion",
                _ => throw new InvalidOperationException($"Unsupported cache mode '{configuration.CacheMode}'.")
            };
        }

        private static string GetCachePreparationBoundary(PerformanceRunConfiguration configuration)
        {
            return configuration.CacheMode switch
            {
                ColdCacheMode when configuration.WarmupTicks == 0 =>
                    "confirm and clear the fresh session's empty derived path cache before measured ticks",
                ColdCacheMode => "clear derived path cache after natural warmup and before measured ticks",
                NaturalWarmCacheMode => "retain naturally populated path cache without eager/all-pairs prefill",
                ForcedInvalidationCacheMode =>
                    "seed one exact anchor-to-path-cell probe and prepare its path-segment completion before the measured tick",
                _ => throw new InvalidOperationException($"Unsupported cache mode '{configuration.CacheMode}'.")
            };
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
            RuntimeMetricsBatch firstBatch = batches[0];
            if (configuration.CacheMode == ColdCacheMode && firstBatch.PathPlanCacheMissesTotal <= 0)
            {
                throw new InvalidOperationException("Cold mode's first measured batch produced no cache miss.");
            }
            if (configuration.CacheMode == NaturalWarmCacheMode && firstBatch.PathPlanCacheHitsTotal <= 0)
            {
                throw new InvalidOperationException("Natural-warm mode's first measured batch produced no cache hit.");
            }
            if (configuration.CacheMode == ForcedInvalidationCacheMode &&
                (firstBatch.NavigationInvalidationsTotal != 1 ||
                    firstBatch.Phases.NavigationRebuildMilliseconds <= 0.0))
            {
                throw new InvalidOperationException(
                    "Forced-invalidation mode's measured batch did not contain exactly one timed rebuild.");
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
                FirstMeasuredBatchPathPlanLookups = batches[0].PathPlanLookupsTotal,
                FirstMeasuredBatchPathPlanCacheHits = batches[0].PathPlanCacheHitsTotal,
                FirstMeasuredBatchPathPlanCacheMisses = batches[0].PathPlanCacheMissesTotal,
                FirstMeasuredBatchNavigationInvalidations = batches[0].NavigationInvalidationsTotal,
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
            string managedAssemblyConfiguration =
                Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyConfigurationAttribute>()?
                    .Configuration ?? string.Empty;
            bool godotDebugBuild = OS.IsDebugBuild();
            bool godotReleaseFeature = OS.HasFeature("release");
            bool godotTemplateFeature = OS.HasFeature("template");
            bool godotEditorFeature = OS.HasFeature("editor");
            return new PerformanceRunEnvironment
            {
                MachineName = System.Environment.MachineName,
                LogicalProcessorCount = System.Environment.ProcessorCount,
                OperatingSystem = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                DotnetRuntime = RuntimeInformation.FrameworkDescription,
                GodotVersion = godotVersion,
                ProcessExecutablePath = Path.GetFullPath(OS.GetExecutablePath()),
                ManagedBuildConfiguration = ManagedBuildConfiguration,
                ManagedAssemblyConfiguration = managedAssemblyConfiguration,
                GodotDebugBuild = godotDebugBuild,
                GodotReleaseFeature = godotReleaseFeature,
                GodotTemplateFeature = godotTemplateFeature,
                GodotEditorFeature = godotEditorFeature,
                VerifiedReleaseExecution = PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    managedAssemblyConfiguration,
                    godotDebugBuild,
                    godotReleaseFeature,
                    godotTemplateFeature,
                    godotEditorFeature)
            };
        }

        private static List<string> BuildNotes(
            PerformanceRunConfiguration configuration,
            PerformanceRunEnvironment environment)
        {
            var notes = new List<string>
            {
                $"Cache mode '{configuration.CacheMode}' uses preparation strategy '{GetCachePreparationStrategy(configuration)}'.",
                "Warmup ticks advance deterministic simulation state; eager/all-pairs route-cache warmup is disabled.",
                $"Comparison group '{configuration.ComparisonGroup}', trial {configuration.TrialIndex}.",
                "This is a single-run indicator. The release gate requires the median of three comparable metrics-off runs.",
                "Core artifact serialization and runner report serialization are excluded from measured tick samples."
            };
            switch (configuration.CacheMode)
            {
                case ColdCacheMode:
                    notes.Add("Cold mode clears every derived path-cache entry before measurement without changing deterministic simulation state.");
                    break;
                case NaturalWarmCacheMode:
                    notes.Add("Natural-warm mode retains only routes populated by deterministic simulation ticks; no eager cache prefill occurs.");
                    break;
                case ForcedInvalidationCacheMode:
                    notes.Add("Forced-invalidation mode measures one tick that commits a prepared path segment and proves an exact post-change cache miss and rebuilt route.");
                    break;
            }
            if (!environment.VerifiedReleaseExecution)
            {
                notes.Add("Verified Godot ExportRelease execution was not detected; timing is characterization only and is not a Release baseline.");
            }
            else
            {
                notes.Add("Managed ExportRelease assembly and Godot release-template feature contract verified.");
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
                CacheEvidence = result.CacheEvidence,
                Hashes = result.Hashes,
                Budget = result.Budget,
                Artifacts = result.Artifacts
            };
            WriteJson(result.Artifacts.ValidationManifest, manifest);
        }

        private static string BuildMatrixCsv(PerformanceRunResult result)
        {
            const string header =
                "run_id,status,scenario,seed,citizens,warmup_ticks,measured_ticks,metrics_enabled,execution_route," +
                "managed_build,managed_assembly_configuration,verified_release_execution," +
                "budget_profile,bootstrap_ms,warmup_ms,p50_ms,p95_ms,p99_ms,max_ms,mean_ms,total_ms," +
                "serialization_ms,path_lookups,path_hits,path_misses,cache_size_last,candidates_per_idle," +
                "invalidations,navigation_rebuild_ms,work_orders_generated,work_orders_generated_uncapped," +
                "work_orders_claimed,work_orders_remaining_last,state_event_sha256,target_passed,safety_passed," +
                "snapshot_path,event_log_path,runtime_metrics_csv,perf_results_path," +
                "cache_mode,comparison_group,trial_index,cache_preparation_strategy,cache_preparation_ms,cleared_cache_entries," +
                "cache_entries_after_bootstrap,cache_entries_after_natural_warmup,cache_entries_before_measurement," +
                "cache_entries_after_measurement,navigation_version_before_measurement,navigation_version_after_measurement," +
                "forced_segment_id,forced_invalidation_count,forced_version_before,forced_version_after," +
                "forced_first_lookup_observed,forced_first_lookup_miss,forced_exact_endpoints_match," +
                "forced_changed_cell_in_post_plan,forced_commit_to_first_lookup_ms," +
                "first_batch_path_lookups,first_batch_path_hits,first_batch_path_misses,first_batch_invalidations," +
                "forced_segment_was_built_before,forced_segment_is_built_after,forced_completion_tick," +
                "forced_cache_entries_after_rebuild,forced_first_lookup_used_new_version," +
                "forced_pre_query_version,forced_pre_plan_version,forced_post_query_version,forced_post_plan_version," +
                "forced_pre_plan_cost,forced_post_plan_cost";
            PerformanceDiagnosticsSummary? diagnostics = result.Diagnostics;
            PerformanceForcedInvalidationEvidence? forced = result.CacheEvidence.ForcedInvalidation;
            PrototypeForcedInvalidationProbeSnapshot? forcedProbe = forced?.Probe;
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
                Csv(result.Configuration.ExecutionRoute),
                Csv(result.Environment.ManagedBuildConfiguration),
                Csv(result.Environment.ManagedAssemblyConfiguration),
                result.Environment.VerifiedReleaseExecution ? "true" : "false",
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
                Csv(result.Artifacts.PerformanceResults),
                Csv(result.Configuration.CacheMode),
                Csv(result.Configuration.ComparisonGroup),
                result.Configuration.TrialIndex.ToString(CultureInfo.InvariantCulture),
                Csv(result.CacheEvidence.PreparationStrategy),
                Format(result.Intervals.CachePreparationMilliseconds),
                result.CacheEvidence.ClearedEntryCount.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.AfterBootstrap.PathCacheEntryCount.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.AfterNaturalWarmup.PathCacheEntryCount.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.BeforeMeasurement.PathCacheEntryCount.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.AfterMeasurement.PathCacheEntryCount.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.BeforeMeasurement.NavigationRulesVersion.ToString(CultureInfo.InvariantCulture),
                result.CacheEvidence.AfterMeasurement.NavigationRulesVersion.ToString(CultureInfo.InvariantCulture),
                Csv(forcedProbe?.PathSegmentStructureId ?? string.Empty),
                forced?.NavigationInvalidationCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.VersionBeforeCommit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.VersionAfterCommit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.FirstPostChangeLookupObserved ? "true" : "false") : string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.FirstPostChangeLookupWasCacheMiss ? "true" : "false") : string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.ExactEndpointsMatch ? "true" : "false") : string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.ChangedCellIncludedInPostChangePlan ? "true" : "false") : string.Empty,
                forcedProbe?.CommitToFirstLookupMilliseconds is double recoveryMilliseconds
                    ? Format(recoveryMilliseconds)
                    : string.Empty,
                diagnostics?.FirstMeasuredBatchPathPlanLookups.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.FirstMeasuredBatchPathPlanCacheHits.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.FirstMeasuredBatchPathPlanCacheMisses.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics?.FirstMeasuredBatchNavigationInvalidations.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.PathSegmentWasBuiltBefore ? "true" : "false") : string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.PathSegmentIsBuiltAfter ? "true" : "false") : string.Empty,
                forcedProbe?.CompletionTick?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.CacheEntriesImmediatelyAfterRebuild?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe.HasValue ? (forcedProbe.Value.FirstPostChangeLookupUsedNewVersion ? "true" : "false") : string.Empty,
                forcedProbe?.PreChangeQueryVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.PreChangePlanVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.PostChangeQueryVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.PostChangePlanVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                forcedProbe?.PreChangePlanCost is double preChangeCost ? Format(preChangeCost) : string.Empty,
                forcedProbe?.PostChangePlanCost is double postChangeCost ? Format(postChangeCost) : string.Empty
            });
            return $"{header}{System.Environment.NewLine}{row}{System.Environment.NewLine}";
        }

        private static string BuildSummary(PerformanceRunResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Societies performance run: {result.Configuration.RunId}");
            builder.AppendLine($"Status: {result.Status}");
            builder.AppendLine($"Configuration: {result.Configuration.ScenarioId}, seed {result.Configuration.SimulationSeed}, {result.Configuration.CitizenCount} citizens, {result.Configuration.MeasuredTicks} measured ticks, metrics {(result.Configuration.MetricsEnabled ? "on" : "off")}");
            builder.AppendLine($"Comparison: {result.Configuration.ComparisonGroup}, trial {result.Configuration.TrialIndex}; cache mode {result.Configuration.CacheMode}");
            builder.AppendLine($"Execution route: {result.Configuration.ExecutionRoute}; runner: {result.Configuration.RunnerExecutablePath}");
            builder.AppendLine($"Managed build: {result.Environment.ManagedBuildConfiguration}; assembly configuration: {result.Environment.ManagedAssemblyConfiguration}; verified release: {result.Environment.VerifiedReleaseExecution}");
            builder.AppendLine($"Bootstrap: {Format(result.Intervals.BootstrapMilliseconds)} ms");
            builder.AppendLine($"Warmup: {Format(result.Intervals.WarmupMilliseconds)} ms; cache preparation: {Format(result.Intervals.CachePreparationMilliseconds)} ms ({result.CacheEvidence.PreparationStrategy})");
            builder.AppendLine(
                $"Cache entries: bootstrap {result.CacheEvidence.AfterBootstrap.PathCacheEntryCount}, " +
                $"after warmup {result.CacheEvidence.AfterNaturalWarmup.PathCacheEntryCount}, " +
                $"before measurement {result.CacheEvidence.BeforeMeasurement.PathCacheEntryCount}, " +
                $"after measurement {result.CacheEvidence.AfterMeasurement.PathCacheEntryCount}; " +
                $"cleared {result.CacheEvidence.ClearedEntryCount}");
            builder.AppendLine($"Ticks: p50 {Format(result.ExternalTickStatistics.P50Milliseconds)} ms, p95 {Format(result.ExternalTickStatistics.P95Milliseconds)} ms, p99 {Format(result.ExternalTickStatistics.P99Milliseconds)} ms, max {Format(result.ExternalTickStatistics.MaximumMilliseconds)} ms, total {Format(result.ExternalTickStatistics.TotalMilliseconds)} ms");
            if (result.CacheEvidence.ForcedInvalidation is PerformanceForcedInvalidationEvidence forced)
            {
                builder.AppendLine(
                    $"Forced invalidation: segment {forced.Probe.PathSegmentStructureId}; " +
                    $"version {forced.Probe.VersionBeforeCommit}->{forced.Probe.VersionAfterCommit}; " +
                    $"invalidations {forced.NavigationInvalidationCount}; exact endpoints {forced.Probe.ExactEndpointsMatch}; " +
                    $"changed cell included {forced.Probe.ChangedCellIncludedInPostChangePlan}; " +
                    $"commit-to-first-lookup {Format(forced.Probe.CommitToFirstLookupMilliseconds ?? 0.0)} ms");
            }
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
            Exception exception,
            PerformanceCacheEvidence? cacheEvidence)
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
                    Configuration = configuration,
                    CacheEvidence = cacheEvidence
                };
                WriteJson(Path.Combine(configuration.OutputDirectory, "perf-results.json"), failure);
            }
            catch (Exception writeException)
            {
                GD.PushError($"Could not write performance failure artifact: {writeException.Message}");
            }
        }

        private static string BuildExactInvocation(PerformanceRunConfiguration configuration)
        {
            string userArguments = string.Join(" ", OS.GetCmdlineUserArgs().Select(QuoteArgument));
            string prefix = configuration.ExecutionRoute == "export_release"
                ? $"{QuoteArgument(configuration.RunnerExecutablePath)} --headless --"
                : $"{QuoteArgument(configuration.RunnerExecutablePath)} --headless --path {QuoteArgument(configuration.ProjectPath)} res://tests/PerfRunner.tscn --";
            return $"{prefix} {userArguments}";
        }

        private static string BuildRawInvocation()
        {
            string userArguments = string.Join(" ", OS.GetCmdlineUserArgs().Select(QuoteArgument));
            return $"{QuoteArgument(OS.GetExecutablePath())} -- {userArguments}";
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
            return RequireSafeIdentifier(value, "run id", 96);
        }

        private static string RequireSafeIdentifier(string value, string label, int maximumLength)
        {
            string identifier = RequireText(value, label);
            if (identifier.Length > maximumLength ||
                identifier.Contains("..", StringComparison.Ordinal) ||
                identifier.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            {
                throw new ArgumentException(
                    $"{label} may contain only letters, digits, '.', '_' and '-' and may not contain '..'.");
            }
            return identifier;
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
