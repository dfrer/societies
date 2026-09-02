using Societies.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Societies.Tests
{
    public static class AcceptedSceneBaselineContract
    {
        public const string Schema = "societies_accepted_scene_baseline/v4";
        public const string Packet02Schema = "societies_accepted_scene_baseline/v5";
        public const string BundleSchema = "societies_accepted_scene_baseline_bundle/v4";
        public const string RouteId = "snow-globe-voxel-four-leg-edit-reload-replay/v4";
        public const string Packet02RouteId = "snow-globe-voxel-causeway-state-edit-reload-replay/v5";
        public const string EnvironmentSchema = "societies_accepted_scene_environment/v1";
        public const string ProcessFrameCadenceMetric = "process_frame_start_interval_ms";
        public const string PhysicsFrameCadenceMetric = "physics_frame_start_interval_ms";
        public const string RealtimePerformanceMode = "realtime_performance";
        public const string FixedDeltaIdentityMode = "fixed_delta_identity";
        public const string ScenePath = "res://scenes/snow_globe_voxel_foundation.tscn";
        public const string ScenarioId = "snow_globe_voxel";
        public const string WorldModel = PrototypeWorldModels.Voxel;
        public const int SimulationSeed = 260827;
        public const int FixedEditX = -2;
        public const int FixedEditY = 12;
        public const int FixedEditZ = -2;
        public const int ExpectedInitialCollisionBodies = 64;
        public const int ExpectedInitialCollisionShapes = 12777;
        public const int ExpectedAfterEditCollisionBodies = 64;
        public const int ExpectedAfterEditCollisionShapes = 12781;
        public const double ProductTargetP95Milliseconds = 16.67;
        public const double HardSafetyP95Milliseconds = 33.33;
        public const double HistoricalSafetyMissMilliseconds = 51.9392;
        public const double MinimumLegDisplacementMeters = 0.25;
        public const int TrialCount = 3;
        public const int ActiveRoutePhysicsSteps = 40;
        public const int MaximumProcessFrameIntervals = 7200;

        public static string ClassifyP95(double frameP95Milliseconds, double physicsP95Milliseconds)
        {
            ValidateMetric(frameP95Milliseconds, nameof(frameP95Milliseconds));
            ValidateMetric(physicsP95Milliseconds, nameof(physicsP95Milliseconds));
            double assessed = Math.Max(frameP95Milliseconds, physicsP95Milliseconds);
            if (assessed > HardSafetyP95Milliseconds)
            {
                return "safety_failure";
            }
            return assessed <= ProductTargetP95Milliseconds ? "target_passed" : "target_missed";
        }

        public static string Sha256(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        public static IReadOnlyList<string> DescribeSnapshotDifferences(
            PrototypeRuntimeSnapshot expected,
            PrototypeRuntimeSnapshot actual,
            int maximumDifferences = 24)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(actual);
            if (maximumDifferences <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDifferences));
            }

            using JsonDocument expectedDocument = JsonDocument.Parse(
                PrototypePersistenceService.SerializeSnapshot(expected));
            using JsonDocument actualDocument = JsonDocument.Parse(
                PrototypePersistenceService.SerializeSnapshot(actual));
            var differences = new List<string>();
            CollectJsonDifferences(
                expectedDocument.RootElement,
                actualDocument.RootElement,
                "$",
                differences,
                maximumDifferences);
            return differences;
        }

        public static void ValidateCatalog(PrototypeScenarioDefinition scenario)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            Require(scenario.Id == ScenarioId, "Accepted scenario id mismatch.");
            Require(scenario.SimulationSeed == SimulationSeed, "Accepted scenario seed mismatch.");
            Require(scenario.WorldModel == WorldModel, "Accepted scenario world model mismatch.");
            Require(scenario.InitialCitizens == 0, "Accepted scenario must declare zero initial citizens.");
            Require(scenario.InitialTrees == 0 && scenario.InitialRocks == 0 && scenario.InitialBerryBushes == 0 &&
                scenario.InitialClayDeposits == 0 && scenario.InitialReedBeds == 0,
                "Accepted voxel scenario must not declare initial resources.");
            Require(scenario.StressPopulationOverride == 0, "Accepted scenario must not declare a stress population.");
            Require(scenario.InitialHearthFuel == 0 && scenario.StartingStock.Count == 0,
                "Accepted scenario must not declare initial stock.");
            Require(scenario.StartingStructures.Count == 0, "Accepted scenario must not declare initial structures.");
            Require(scenario.StartingBuildQueue.Count == 0, "Accepted scenario must not declare a build queue.");
            Require(scenario.Crisis == null, "Accepted voxel scenario must not declare crisis state.");
        }

        public static void ValidateArtifact(
            AcceptedSceneBaselineTrialArtifact artifact,
            string? expectedSchema = null,
            string? expectedRouteId = null)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            expectedSchema ??= Schema;
            expectedRouteId ??= RouteId;
            bool packet02 = string.Equals(expectedSchema, Packet02Schema, StringComparison.Ordinal) &&
                string.Equals(expectedRouteId, Packet02RouteId, StringComparison.Ordinal);
            Require(artifact.Schema == expectedSchema, "Artifact schema mismatch.");
            Require(artifact.Route.RouteId == expectedRouteId && artifact.Route.ScenePath == ScenePath,
                "Artifact route identity mismatch.");
            Require(artifact.Route.TrialIndex is >= 1 and <= TrialCount, "Artifact trial index is out of range.");
            Require(artifact.Route.WarmupFrameCount > 0 && artifact.Route.MeasuredFrameCount > 0,
                "Artifact frame counts must be positive.");
            ValidateEnvironment(artifact.Environment);
            Require(!string.IsNullOrWhiteSpace(artifact.Route.BaseSha) &&
                !string.IsNullOrWhiteSpace(artifact.Route.SourceSha) &&
                !string.IsNullOrWhiteSpace(artifact.Route.SourceTree) &&
                !string.IsNullOrWhiteSpace(artifact.Route.SourceStateIdentity) &&
                artifact.Route.ManagedAssemblyConfiguration == PerformanceExecutionContract.ExportReleaseAssemblyConfiguration &&
                !string.IsNullOrWhiteSpace(artifact.Route.ManagedAssemblySha256) &&
                artifact.Route.VerifiedExportReleaseExecution,
                "Artifact source identity is missing.");
            bool realtime = artifact.Route.TrialMode == RealtimePerformanceMode;
            bool fixedDelta = artifact.Route.TrialMode == FixedDeltaIdentityMode;
            Require(realtime || fixedDelta, "Artifact trial mode is invalid.");
            Require(realtime ? artifact.Route.FixedFps == 0 : artifact.Route.FixedFps == 60,
                "Artifact fixed-FPS mode does not match its declared trial mode.");
            Require(artifact.Scenario.ScenarioId == ScenarioId && artifact.Scenario.SimulationSeed == SimulationSeed &&
                artifact.Scenario.WorldModel == WorldModel && artifact.Scenario.DeclaredInitialCitizens == 0 &&
                artifact.Scenario.RuntimeCitizenCount == 0, "Artifact scenario characterization mismatch.");
            Require(artifact.Scenario.DeclaredInitialResourceCount == 0 &&
                artifact.Scenario.RuntimeResourceCount == 0 &&
                artifact.Scenario.DeclaredInitialStructureCount == 0 &&
                artifact.Scenario.RuntimeStructureCount == 0 &&
                artifact.Scenario.DeclaredInitialBuildQueueCount == 0 &&
                artifact.Scenario.RuntimeBuildQueueCount == 0 &&
                artifact.Scenario.StressPopulationOverride == 0, "Artifact catalog exclusions mismatch.");
            Require(!string.IsNullOrWhiteSpace(artifact.Scenario.InitialStateIdentity),
                "Artifact initial state identity is missing.");
            ValidateRouteTrace(artifact.RouteExecution.Primary, artifact.Route.MeasuredFrameCount, "primary");
            if (fixedDelta)
            {
                Require(artifact.RouteExecution.Replay != null, "Fixed-delta identity trial is missing replay trace.");
                ValidateRouteTrace(artifact.RouteExecution.Replay!, artifact.Route.MeasuredFrameCount, "replay");
                RequireTraceEquality(artifact.RouteExecution.Primary, artifact.RouteExecution.Replay!);
            }
            else
            {
                Require(artifact.RouteExecution.Replay == null, "Real-time trial must not masquerade as replay evidence.");
            }

            if (realtime)
            {
                int processFrameCount = artifact.Timing.FrameIntervals.RawIntervalMilliseconds.Count;
                Require(processFrameCount > 0 && processFrameCount <= MaximumProcessFrameIntervals,
                    "Process-frame cadence sample count is missing or exceeds its fixed bound.");
                ValidateIntervalSeries(
                    artifact.Timing.FrameIntervals, processFrameCount, ProcessFrameCadenceMetric);
                ValidateIntervalSeries(
                    artifact.Timing.PhysicsIntervals,
                    artifact.Route.MeasuredFrameCount,
                    PhysicsFrameCadenceMetric);
                Require(artifact.Timing.FrameIntervals.MetricId == ProcessFrameCadenceMetric &&
                    artifact.Timing.PhysicsIntervals.MetricId == PhysicsFrameCadenceMetric,
                    "Cadence metric identity mismatch.");
                Require(artifact.Timing.FrameIntervals.RawTimestamps[0] >=
                        artifact.Timing.PhysicsIntervals.RawTimestamps[0] &&
                    artifact.Timing.FrameIntervals.RawTimestamps[^1] <=
                        artifact.Timing.PhysicsIntervals.RawTimestamps[^1],
                    "Process-frame cadence samples escape the common physics-bounded wall-clock window.");
                Require(artifact.RouteExecution.Primary.ProcessFrameStart <=
                        artifact.Timing.FrameIntervals.RawSignalOrdinals[0] &&
                    artifact.RouteExecution.Primary.ProcessFrameEnd >=
                        artifact.Timing.FrameIntervals.RawSignalOrdinals[^1],
                    "Process-frame trace bounds do not cover the raw process cadence ordinals.");
                ValidatePhysicsRouteTags(
                    artifact.Timing.PhysicsIntervals, artifact.Route.MeasuredFrameCount);
                string rawClassification = ClassifyP95(
                    artifact.Timing.FrameIntervals.Statistics.P95Milliseconds,
                    artifact.Timing.PhysicsIntervals.Statistics.P95Milliseconds);
                double assessed = Math.Max(
                    artifact.Timing.FrameIntervals.Statistics.P95Milliseconds,
                    artifact.Timing.PhysicsIntervals.Statistics.P95Milliseconds);
                Require(artifact.Timing.AssessedP95Milliseconds == assessed,
                    "Assessed p95 must exactly equal max(frame p95, physics p95).");
                Require(artifact.Timing.RawThresholdClassification == rawClassification,
                    "Artifact raw timing classification mismatch.");
                bool expectedEligibility = artifact.Route.VerifiedExportReleaseExecution &&
                    artifact.Environment.Headless &&
                    artifact.Environment.IdentitySha256 == ComputeEnvironmentIdentity(artifact.Environment) &&
                    !artifact.Route.SourceDirty && !artifact.Route.DirtySourceOverrideUsed;
                Require(artifact.Timing.TargetSafetyClaimEligible == expectedEligibility,
                    "Artifact target/safety claim eligibility is not exact.");
                Require(expectedEligibility
                        ? artifact.Timing.Classification == rawClassification &&
                          artifact.Status == (rawClassification == "safety_failure"
                              ? "characterized_safety_failure" : "characterized")
                        : artifact.Timing.Classification == "not_applied_characterization_only" &&
                          artifact.Status == "smoke_characterized_dirty_source",
                    "Artifact target/safety classification or status mismatch.");
                ValidateBacklog(
                    artifact.Backlog,
                    artifact.Timing.FrameIntervals.RawSignalOrdinals.Skip(1).ToArray());
            }
            else
            {
                Require(artifact.Timing.FrameIntervals.RawTimestamps.Count == 0 &&
                    artifact.Timing.FrameIntervals.RawSignalOrdinals.Count == 0 &&
                    artifact.Timing.FrameIntervals.RawIntervalMilliseconds.Count == 0 &&
                    artifact.Timing.FrameIntervals.RawSampleRoutePhaseCodes.Count == 0 &&
                    artifact.Timing.FrameIntervals.RawSampleLegCodes.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.RawTimestamps.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.RawSignalOrdinals.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.RawIntervalMilliseconds.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.RawSampleRoutePhaseCodes.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.RawSampleLegCodes.Count == 0 &&
                    artifact.Timing.FrameIntervals.Statistics.Count == 0 &&
                    artifact.Timing.FrameIntervals.ActiveRouteStatistics.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.Statistics.Count == 0 &&
                    artifact.Timing.PhysicsIntervals.ActiveRouteStatistics.Count == 0 &&
                    artifact.Timing.AssessedP95Milliseconds == 0.0 &&
                    artifact.Timing.Classification == "not_applicable_identity_only" &&
                    artifact.Timing.RawThresholdClassification == "not_applicable_identity_only" &&
                    !artifact.Timing.TargetSafetyClaimEligible,
                    "Fixed-delta identity trial contains performance claims or raw timing samples.");
                Require(artifact.Backlog.RawPendingSimulationTickSamples.Count == 0,
                    "Fixed-delta identity trial contains backlog characterization samples.");
                Require(artifact.Backlog.RawProcessFrameOrdinals.Count == 0,
                    "Fixed-delta identity trial contains backlog process ordinals.");
                Require(artifact.Status == (artifact.Route.SourceDirty || artifact.Route.DirtySourceOverrideUsed
                        ? "identity_replay_verified_dirty_source_smoke" : "identity_replay_verified"),
                    "Fixed-delta identity status mismatch.");
            }
            Require(artifact.Timing.ProductTargetP95Milliseconds == ProductTargetP95Milliseconds &&
                artifact.Timing.HardSafetyP95Milliseconds == HardSafetyP95Milliseconds &&
                artifact.Timing.DurationMetricsAvailability == "not_measured" &&
                artifact.Timing.HistoricalContextMilliseconds == HistoricalSafetyMissMilliseconds &&
                artifact.Timing.HistoricalContextClassification == "historical_context_only",
                "Artifact performance thresholds or historical labeling mismatch.");
            Require(artifact.Collisions.InitialBodyCount == ExpectedInitialCollisionBodies &&
                artifact.Collisions.InitialShapeCount == ExpectedInitialCollisionShapes &&
                artifact.Collisions.AfterEditBodyCount == ExpectedAfterEditCollisionBodies &&
                artifact.Collisions.AfterEditShapeCount == ExpectedAfterEditCollisionShapes,
                "Artifact direct presenter collision counts mismatch.");
            Require(artifact.Edit.Accepted && artifact.Edit.X == FixedEditX && artifact.Edit.Y == FixedEditY &&
                artifact.Edit.Z == FixedEditZ && artifact.Edit.Before == nameof(VoxelMaterialId.Soil) &&
                artifact.Edit.After == nameof(VoxelMaterialId.Air) && artifact.Edit.WorldRevision == 1,
                "Artifact fixed voxel edit mismatch.");
            Require(artifact.Persistence.InstrumentationExcludedFromAuthority,
                "Instrumentation fields entered authoritative state.");
            Require(!string.IsNullOrWhiteSpace(artifact.Persistence.AfterEditStateIdentity) &&
                artifact.Persistence.AfterEditStateIdentity == artifact.Persistence.ReloadedStateIdentity,
                "Edit persistence/reload identity mismatch.");
            Require(artifact.Persistence.SnapshotWritten && artifact.Persistence.SnapshotReloaded,
                "Persistence/reload evidence is incomplete.");
            if (packet02)
            {
                AcceptedSceneBaselineCausewayTransition causeway = artifact.Causeway ??
                    throw new InvalidOperationException("Packet 02 artifact is missing causeway command evidence.");
                Require(causeway.Accepted &&
                    causeway.CommandKind == nameof(PrototypeCausewayCommandKind.ContributeCommunityTimber) &&
                    causeway.CommandQuantity == 1 &&
                    causeway.EventType == PrototypeEventTypes.CausewayMaterialCommitted &&
                    causeway.PreviousRevision == 0 && causeway.Revision == 1,
                    "Packet 02 causeway command identity is not the fixed accepted transition.");
                Require(!string.IsNullOrWhiteSpace(causeway.BeforeCommandStateIdentity) &&
                    !string.IsNullOrWhiteSpace(causeway.AfterCommandStateIdentity) &&
                    causeway.BeforeCommandStateIdentity != causeway.AfterCommandStateIdentity &&
                    causeway.AfterCommandStateIdentity == causeway.AfterVoxelEditStateIdentity &&
                    causeway.AfterCommandStateIdentity == causeway.ReloadedStateIdentity,
                    "Packet 02 causeway state did not remain equal across command, voxel edit, and reload.");
                if (fixedDelta)
                {
                    Require(causeway.AfterCommandStateIdentity == causeway.ReplayedAfterCommandStateIdentity &&
                        causeway.AfterCommandStateIdentity == causeway.ReplayedAfterVoxelEditStateIdentity,
                        "Packet 02 causeway state did not reproduce across the fixed-delta replay.");
                }
                else
                {
                    Require(string.IsNullOrEmpty(causeway.ReplayedAfterCommandStateIdentity) &&
                        string.IsNullOrEmpty(causeway.ReplayedAfterVoxelEditStateIdentity),
                        "Real-time Packet 02 evidence contains a fixed-delta causeway replay claim.");
                }
            }
            else
            {
                Require(artifact.Causeway == null,
                    "Packet 01 artifact unexpectedly contains Packet 02 causeway command evidence.");
            }
            if (fixedDelta)
            {
                Require(artifact.Persistence.RouteReplayed &&
                    artifact.Persistence.MeasurementStartStateIdentity ==
                        artifact.Persistence.ReplayedMeasurementStartStateIdentity &&
                    artifact.Persistence.MeasurementEndStateIdentity ==
                        artifact.Persistence.ReplayedMeasurementEndStateIdentity &&
                    artifact.Persistence.AfterEditStateIdentity == artifact.Persistence.ReplayedStateIdentity,
                    "Fixed-delta persistence/replay identity mismatch.");
            }
            else
            {
                Require(!artifact.Persistence.RouteReplayed &&
                    string.IsNullOrEmpty(artifact.Persistence.ReplayedMeasurementStartStateIdentity) &&
                    string.IsNullOrEmpty(artifact.Persistence.ReplayedMeasurementEndStateIdentity) &&
                    string.IsNullOrEmpty(artifact.Persistence.ReplayedStateIdentity),
                    "Real-time trial contains fixed-delta replay claims.");
            }
        }

        private static void ValidateIntervalSeries(
            AcceptedSceneBaselineIntervalSeries series, int expectedCount, string label)
        {
            Require(series.TimestampFrequencyHertz > 0, $"{label} timestamp frequency is invalid.");
            Require(series.RawTimestamps.Count == expectedCount + 1 &&
                series.RawSignalOrdinals.Count == expectedCount + 1 &&
                series.RawIntervalMilliseconds.Count == expectedCount &&
                series.RawSampleRoutePhaseCodes.Count == expectedCount &&
                series.RawSampleLegCodes.Count == expectedCount,
                $"{label} raw series count is invalid or unbounded.");
            for (int index = 0; index < expectedCount; index++)
            {
                long start = series.RawTimestamps[index];
                long end = series.RawTimestamps[index + 1];
                Require(end > start, $"{label} timestamps must be strictly monotonic.");
                Require(series.RawSignalOrdinals[index + 1] - series.RawSignalOrdinals[index] == 1,
                    $"{label} raw signal ordinals must be consecutive unit steps.");
                double expectedMilliseconds =
                    (end - start) * 1000.0 / series.TimestampFrequencyHertz;
                double actualMilliseconds = series.RawIntervalMilliseconds[index];
                Require(double.IsFinite(actualMilliseconds) && actualMilliseconds > 0.0 &&
                    actualMilliseconds == expectedMilliseconds,
                    $"{label} raw interval is missing, non-finite, or inconsistent with timestamps.");
            }
            Require(series.RawIntervalMilliseconds.Distinct().Count() > 1,
                $"{label} raw interval series is degenerate.");
            PerformanceSampleStatistics recomputed =
                PerformanceRunStatistics.Compute(series.RawIntervalMilliseconds);
            RequireStatisticsEqual(series.Statistics, recomputed, label);
            Require(series.RawSampleRoutePhaseCodes.All(value => value is 0 or 1) &&
                series.RawSampleLegCodes.All(value => value <= 4),
                $"{label} route phase/leg tags are invalid.");
            double[] activeSamples = series.RawIntervalMilliseconds
                .Where((_, index) => series.RawSampleRoutePhaseCodes[index] == 1)
                .ToArray();
            Require(activeSamples.Length > 0 && series.ActiveRouteSampleCount == activeSamples.Length,
                $"{label} active-route subset is missing or miscounted.");
            RequireStatisticsEqual(
                series.ActiveRouteStatistics,
                PerformanceRunStatistics.Compute(activeSamples),
                $"{label} active-route subset");
        }

        private static void ValidatePhysicsRouteTags(
            AcceptedSceneBaselineIntervalSeries series,
            int measuredFrames)
        {
            Require(measuredFrames >= ActiveRoutePhysicsSteps,
                "Measured physics window cannot contain the required four-leg route.");
            for (int index = 0; index < measuredFrames; index++)
            {
                byte expectedPhase = index < ActiveRoutePhysicsSteps ? (byte)1 : (byte)0;
                byte expectedLeg = index < ActiveRoutePhysicsSteps ? (byte)((index / 10) + 1) : (byte)0;
                Require(series.RawSampleRoutePhaseCodes[index] == expectedPhase &&
                    series.RawSampleLegCodes[index] == expectedLeg,
                    $"Physics cadence sample {index} route phase/leg tag mismatch.");
            }
        }

        private static void ValidateBacklog(
            AcceptedSceneBaselineBacklog backlog,
            IReadOnlyList<ulong> expectedProcessOrdinals)
        {
            int expectedCount = expectedProcessOrdinals.Count;
            Require(backlog.RawPendingSimulationTickSamples.Count == expectedCount,
                "Scheduler backlog raw sample count mismatch.");
            Require(backlog.RawProcessFrameOrdinals.SequenceEqual(expectedProcessOrdinals),
                "Scheduler backlog samples are not bound to the process cadence interval endpoints.");
            Require(backlog.RawPendingSimulationTickSamples.All(value => value >= 0),
                "Scheduler backlog samples must be non-negative.");
            PerformanceSampleStatistics recomputed = PerformanceRunStatistics.Compute(
                backlog.RawPendingSimulationTickSamples.Select(value => (double)value).ToArray());
            Require(backlog.SampleCount == expectedCount &&
                backlog.P50PendingSimulationTicks == recomputed.P50Milliseconds &&
                backlog.P95PendingSimulationTicks == recomputed.P95Milliseconds &&
                backlog.MaximumPendingSimulationTicks == recomputed.MaximumMilliseconds,
                "Scheduler backlog statistics do not derive from raw samples.");
        }

        private static void RequireStatisticsEqual(
            PerformanceSampleStatistics actual, PerformanceSampleStatistics expected, string label)
        {
            Require(actual == expected, $"{label} statistics do not derive from raw intervals.");
            Require(actual.Count > 0, $"{label} sample count must be positive.");
            foreach (double value in new[]
            {
                actual.MeanMilliseconds, actual.P50Milliseconds, actual.P95Milliseconds,
                actual.P99Milliseconds, actual.MaximumMilliseconds, actual.TotalMilliseconds
            })
            {
                Require(double.IsFinite(value) && value > 0.0,
                    $"{label} statistics must be finite and positive.");
            }
        }

        private static void ValidateRouteTrace(AcceptedSceneBaselineRouteTrace trace, int measuredFrames, string label)
        {
            Require(!trace.SceneTreePaused && trace.ManagerProcessActive && trace.PlayerPhysicsProcessActive,
                $"{label} route did not execute in live process/physics state.");
            Require(trace.ProcessFrameEnd > trace.ProcessFrameStart &&
                trace.PhysicsFrameEnd > trace.PhysicsFrameStart &&
                trace.PhysicsFrameEnd - trace.PhysicsFrameStart >= (ulong)measuredFrames,
                $"{label} route frame counters did not advance through the measured route.");
            Require(trace.LegCheckpoints.Count == 4, $"{label} route checkpoint count mismatch.");
            string[] expectedLegs = { "move_right", "move_forward", "move_left", "move_backward" };
            (double Pitch, double Yaw)[] expectedCamera =
                { (-8.0, 0.0), (-12.0, 18.0), (-8.0, 0.0), (-12.0, -18.0) };
            double previousX = trace.StartPlayerX;
            double previousY = trace.StartPlayerY;
            double previousZ = trace.StartPlayerZ;
            double minimum = double.PositiveInfinity;
            for (int index = 0; index < trace.LegCheckpoints.Count; index++)
            {
                AcceptedSceneBaselineRouteCheckpoint checkpoint = trace.LegCheckpoints[index];
                Require(checkpoint.LegId == expectedLegs[index] && checkpoint.CompletedFrameCount == (index + 1) * 10,
                    $"{label} route checkpoint identity mismatch.");
                Require(Math.Abs(checkpoint.CameraPitchDegrees - expectedCamera[index].Pitch) <= 0.001 &&
                    Math.Abs(checkpoint.CameraYawDegrees - expectedCamera[index].Yaw) <= 0.001 &&
                    Math.Abs(checkpoint.CameraRollDegrees) <= 0.001,
                    $"{label} route camera checkpoint mismatch.");
                foreach (double value in new[]
                {
                    checkpoint.PlayerX, checkpoint.PlayerY, checkpoint.PlayerZ,
                    checkpoint.CameraPitchDegrees, checkpoint.CameraYawDegrees, checkpoint.CameraRollDegrees
                })
                {
                    Require(double.IsFinite(value), $"{label} route checkpoint contains a non-finite value.");
                }
                double dx = checkpoint.PlayerX - previousX;
                double dy = checkpoint.PlayerY - previousY;
                double dz = checkpoint.PlayerZ - previousZ;
                minimum = Math.Min(minimum, Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)));
                previousX = checkpoint.PlayerX;
                previousY = checkpoint.PlayerY;
                previousZ = checkpoint.PlayerZ;
            }
            Require(trace.MinimumRequiredLegDisplacementMeters == MinimumLegDisplacementMeters &&
                trace.MinimumObservedLegDisplacementMeters == minimum &&
                minimum >= MinimumLegDisplacementMeters,
                $"{label} route did not prove minimum per-leg player displacement.");
        }

        private static void RequireTraceEquality(
            AcceptedSceneBaselineRouteTrace primary, AcceptedSceneBaselineRouteTrace replay)
        {
            Require(primary.StartPlayerX == replay.StartPlayerX &&
                primary.StartPlayerY == replay.StartPlayerY &&
                primary.StartPlayerZ == replay.StartPlayerZ &&
                primary.MinimumObservedLegDisplacementMeters == replay.MinimumObservedLegDisplacementMeters &&
                primary.LegCheckpoints.SequenceEqual(replay.LegCheckpoints),
                "Fixed-delta replay route checkpoints differ from the primary route.");
        }

        private static void ValidateMetric(double value, string label)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new InvalidOperationException($"{label} metric must be finite and non-negative.");
            }
        }

        public static string ComputeEnvironmentIdentity(AcceptedSceneBaselineEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);
            string normalized = string.Join("\n", new[]
            {
                $"schema={environment.Schema}",
                $"godotVersion={environment.GodotVersion}",
                $"osName={environment.OsName}",
                $"osDescription={environment.OsDescription}",
                $"osVersion={environment.OsVersion}",
                $"osArchitecture={environment.OsArchitecture}",
                $"processArchitecture={environment.ProcessArchitecture}",
                $"dotnetRuntime={environment.DotnetRuntime}",
                $"cpuModel={environment.CpuModel}",
                $"logicalProcessorCount={environment.LogicalProcessorCount}",
                $"displayServer={environment.DisplayServer}",
                $"renderingMethod={environment.RenderingMethod}",
                $"renderingDriver={environment.RenderingDriver}",
                $"renderingAdapter={environment.RenderingAdapter}",
                $"viewportWidth={environment.ViewportWidth}",
                $"viewportHeight={environment.ViewportHeight}",
                $"audioDriver={environment.AudioDriver}",
                $"headless={environment.Headless.ToString().ToLowerInvariant()}",
                $"physicsTicksPerSecond={environment.PhysicsTicksPerSecond}",
                $"maxFps={environment.MaxFps}",
                $"timeScale={environment.TimeScale.ToString("R", CultureInfo.InvariantCulture)}",
                $"physicsJitterFix={environment.PhysicsJitterFix.ToString("R", CultureInfo.InvariantCulture)}",
                $"maxPhysicsStepsPerFrame={environment.MaxPhysicsStepsPerFrame}"
            });
            return Sha256(normalized);
        }

        private static void ValidateEnvironment(AcceptedSceneBaselineEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);
            Require(environment.Schema == EnvironmentSchema && environment.Headless,
                "Accepted-scene environment identity must be complete and explicitly headless.");
            foreach (string value in new[]
            {
                environment.GodotVersion, environment.OsName, environment.OsDescription,
                environment.OsVersion, environment.OsArchitecture, environment.ProcessArchitecture,
                environment.DotnetRuntime, environment.CpuModel, environment.DisplayServer,
                environment.RenderingMethod, environment.RenderingDriver, environment.RenderingAdapter,
                environment.ViewportWidth, environment.ViewportHeight, environment.AudioDriver
            })
            {
                Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 160 &&
                    !value.Any(char.IsControl),
                    "Accepted-scene environment string is empty, unbounded, or contains control characters.");
            }
            Require(environment.LogicalProcessorCount > 0 && environment.LogicalProcessorCount <= 4096 &&
                environment.PhysicsTicksPerSecond > 0 && environment.PhysicsTicksPerSecond <= 1000 &&
                environment.MaxFps >= 0 && environment.MaxFps <= 100000 &&
                double.IsFinite(environment.TimeScale) && environment.TimeScale > 0.0 &&
                double.IsFinite(environment.PhysicsJitterFix) && environment.PhysicsJitterFix >= 0.0 &&
                environment.MaxPhysicsStepsPerFrame > 0 && environment.MaxPhysicsStepsPerFrame <= 1000,
                "Accepted-scene environment numeric field is invalid or unbounded.");
            Require(environment.DisplayServer.Equals("headless", StringComparison.OrdinalIgnoreCase),
                "Accepted-scene evidence must use the headless display server.");
            Require(environment.ViewportWidth == "unavailable_headless" &&
                environment.ViewportHeight == "unavailable_headless",
                "Accepted-scene headless viewport identity is invalid.");
            Require(environment.IdentitySha256.Length == 64 &&
                environment.IdentitySha256 == ComputeEnvironmentIdentity(environment),
                "Accepted-scene environment identity hash mismatch.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CollectJsonDifferences(
            JsonElement expected,
            JsonElement actual,
            string path,
            List<string> differences,
            int maximumDifferences)
        {
            if (differences.Count >= maximumDifferences)
            {
                return;
            }
            if (expected.ValueKind != actual.ValueKind)
            {
                differences.Add($"{path}: expected {expected.ValueKind}, actual {actual.ValueKind}");
                return;
            }

            if (expected.ValueKind == JsonValueKind.Object)
            {
                Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
                    .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
                foreach (JsonProperty expectedProperty in expected.EnumerateObject())
                {
                    if (differences.Count >= maximumDifferences)
                    {
                        return;
                    }
                    string propertyPath = $"{path}.{expectedProperty.Name}";
                    if (!actualProperties.Remove(expectedProperty.Name, out JsonElement actualProperty))
                    {
                        differences.Add($"{propertyPath}: missing from actual snapshot");
                        continue;
                    }
                    CollectJsonDifferences(
                        expectedProperty.Value,
                        actualProperty,
                        propertyPath,
                        differences,
                        maximumDifferences);
                }
                foreach (string propertyName in actualProperties.Keys.OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (differences.Count >= maximumDifferences)
                    {
                        return;
                    }
                    differences.Add($"{path}.{propertyName}: unexpected in actual snapshot");
                }
                return;
            }

            if (expected.ValueKind == JsonValueKind.Array)
            {
                JsonElement.ArrayEnumerator expectedItems = expected.EnumerateArray();
                JsonElement.ArrayEnumerator actualItems = actual.EnumerateArray();
                int expectedLength = expected.GetArrayLength();
                int actualLength = actual.GetArrayLength();
                if (expectedLength != actualLength)
                {
                    differences.Add($"{path}.Length: expected {expectedLength}, actual {actualLength}");
                }
                int sharedLength = Math.Min(expectedLength, actualLength);
                for (int index = 0; index < sharedLength && differences.Count < maximumDifferences; index++)
                {
                    expectedItems.MoveNext();
                    actualItems.MoveNext();
                    CollectJsonDifferences(
                        expectedItems.Current,
                        actualItems.Current,
                        $"{path}[{index}]",
                        differences,
                        maximumDifferences);
                }
                return;
            }

            string expectedValue = RenderDifferenceValue(expected);
            string actualValue = RenderDifferenceValue(actual);
            if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
            {
                differences.Add($"{path}: expected {expectedValue}, actual {actualValue}");
            }
        }

        private static string RenderDifferenceValue(JsonElement value)
        {
            const int maximumRenderedValueLength = 128;
            if (value.ValueKind == JsonValueKind.String)
            {
                string text = value.GetString() ?? string.Empty;
                return $"<redacted-string length={text.Length} sha256={Sha256(text)}>";
            }

            string raw = value.GetRawText();
            if (raw.Length <= maximumRenderedValueLength)
            {
                return raw;
            }

            string summary = $"<redacted-scalar length={raw.Length} sha256={Sha256(raw)}>";
            Require(summary.Length <= maximumRenderedValueLength,
                "Rendered snapshot difference scalar summary exceeded its fixed bound.");
            return summary;
        }
    }

    /// <summary>
    /// Defines the complete synthetic movement state applied at each accepted-scene physics boundary.
    /// The runner delivers this state directly to the Input singleton so the immediately following
    /// PlayerCharacter physics callback cannot depend on render-frame input-event flushing.
    /// </summary>
    public static class AcceptedSceneBaselineRouteInputContract
    {
        public const string DeliveryBoundary = "immediate_input_singleton_state_at_physics_frame";

        public static void ApplyAtPhysicsBoundary(
            int physicsStep,
            IReadOnlyList<string> movementActions,
            Action<string, bool> setActionState)
        {
            ArgumentNullException.ThrowIfNull(movementActions);
            ArgumentNullException.ThrowIfNull(setActionState);
            if (physicsStep is not (0 or 10 or 20 or 30 or 40))
            {
                return;
            }

            string? activeAction = physicsStep switch
            {
                < 10 => movementActions[0],
                < 20 => movementActions[1],
                < 30 => movementActions[2],
                < 40 => movementActions[3],
                _ => null
            };
            foreach (string action in movementActions)
            {
                setActionState(action, action == activeAction);
            }
        }
    }

    /// <summary>
    /// Canonical comparison seam for the fields the wrapper requires to be byte-for-byte equal
    /// across real-time and fixed-delta primary route captures.
    /// </summary>
    public static class AcceptedSceneBaselineCrossModeRouteContract
    {
        private static readonly JsonSerializerOptions CheckpointIdentityJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static string GetPrimaryCheckpointIdentity(AcceptedSceneBaselineRouteTrace trace)
        {
            ArgumentNullException.ThrowIfNull(trace);
            return JsonSerializer.Serialize(new
            {
                start = new[] { trace.StartPlayerX, trace.StartPlayerY, trace.StartPlayerZ },
                minimum = trace.MinimumObservedLegDisplacementMeters,
                checkpoints = trace.LegCheckpoints
            }, CheckpointIdentityJsonOptions);
        }

        public static void RequirePrimaryCheckpointIdentity(
            IReadOnlyList<AcceptedSceneBaselineRouteTrace> traces)
        {
            ArgumentNullException.ThrowIfNull(traces);
            if (traces.Count == 0)
            {
                throw new InvalidOperationException("Cross-mode route comparison requires at least one primary trace.");
            }

            string expected = GetPrimaryCheckpointIdentity(traces[0]);
            for (int index = 1; index < traces.Count; index++)
            {
                if (!string.Equals(expected, GetPrimaryCheckpointIdentity(traces[index]), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Primary route player/camera checkpoints differ across real-time and fixed-delta trials.");
                }
            }
        }
    }

    public enum AcceptedSceneBaselineShutdownStep
    {
        StopRouteRecorder,
        ResetSyntheticInput,
        DisableManagerCallbacks,
        DisablePlayerCallbacks,
        SubscribeToManagerTreeExited,
        QueueManagerForFree,
        AwaitManagerTreeExited,
        VerifyManagerOutsideTree
    }

    /// <summary>Test-runner lifecycle contract that prevents accepted-scene teardown from relying on frame timing.</summary>
    public static class AcceptedSceneBaselineShutdownContract
    {
        private static readonly AcceptedSceneBaselineShutdownStep[] OrderedSteps =
        {
            AcceptedSceneBaselineShutdownStep.StopRouteRecorder,
            AcceptedSceneBaselineShutdownStep.ResetSyntheticInput,
            AcceptedSceneBaselineShutdownStep.DisableManagerCallbacks,
            AcceptedSceneBaselineShutdownStep.DisablePlayerCallbacks,
            AcceptedSceneBaselineShutdownStep.SubscribeToManagerTreeExited,
            AcceptedSceneBaselineShutdownStep.QueueManagerForFree,
            AcceptedSceneBaselineShutdownStep.AwaitManagerTreeExited,
            AcceptedSceneBaselineShutdownStep.VerifyManagerOutsideTree
        };

        public static IReadOnlyList<AcceptedSceneBaselineShutdownStep> Steps => OrderedSteps;
    }

    public sealed class AcceptedSceneBaselineFailureEvidence
    {
        public string Stage { get; set; } = "runner_initialization";
        public string RouteId { get; set; } = AcceptedSceneBaselineContract.RouteId;
        public string ScenePath { get; set; } = AcceptedSceneBaselineContract.ScenePath;
        public int? TrialIndex { get; set; }
        public string TrialMode { get; set; } = string.Empty;
        public int? FixedFps { get; set; }
        public string BaseSha { get; set; } = string.Empty;
        public string SourceSha { get; set; } = string.Empty;
        public string SourceTree { get; set; } = string.Empty;
        public string SourceStateIdentity { get; set; } = string.Empty;
        public bool? SourceDirty { get; set; }
        public bool? DirtySourceOverrideUsed { get; set; }
        public List<string> MismatchDiagnostics { get; set; } = new();
    }

    public sealed class AcceptedSceneBaselineTrialArtifact
    {
        public string Schema { get; set; } = AcceptedSceneBaselineContract.Schema;
        public string Status { get; set; } = string.Empty;
        public AcceptedSceneBaselineRoute Route { get; set; } = new();
        public AcceptedSceneBaselineEnvironment Environment { get; set; } = new();
        public AcceptedSceneBaselineScenario Scenario { get; set; } = new();
        public AcceptedSceneBaselineRouteExecution RouteExecution { get; set; } = new();
        public AcceptedSceneBaselineTiming Timing { get; set; } = new();
        public AcceptedSceneBaselineCollision Collisions { get; set; } = new();
        public AcceptedSceneBaselineBacklog Backlog { get; set; } = new();
        public AcceptedSceneBaselineEdit Edit { get; set; } = new();
        public AcceptedSceneBaselinePersistence Persistence { get; set; } = new();
        public AcceptedSceneBaselineCausewayTransition? Causeway { get; set; }
        public List<string> Limitations { get; set; } = new();
    }

    public sealed class AcceptedSceneBaselineEnvironment
    {
        public string Schema { get; set; } = AcceptedSceneBaselineContract.EnvironmentSchema;
        public string GodotVersion { get; set; } = string.Empty;
        public string OsName { get; set; } = string.Empty;
        public string OsDescription { get; set; } = string.Empty;
        public string OsVersion { get; set; } = string.Empty;
        public string OsArchitecture { get; set; } = string.Empty;
        public string ProcessArchitecture { get; set; } = string.Empty;
        public string DotnetRuntime { get; set; } = string.Empty;
        public string CpuModel { get; set; } = string.Empty;
        public int LogicalProcessorCount { get; set; }
        public string DisplayServer { get; set; } = string.Empty;
        public string RenderingMethod { get; set; } = string.Empty;
        public string RenderingDriver { get; set; } = string.Empty;
        public string RenderingAdapter { get; set; } = string.Empty;
        public string ViewportWidth { get; set; } = string.Empty;
        public string ViewportHeight { get; set; } = string.Empty;
        public string AudioDriver { get; set; } = string.Empty;
        public bool Headless { get; set; }
        public int PhysicsTicksPerSecond { get; set; }
        public int MaxFps { get; set; }
        public double TimeScale { get; set; }
        public double PhysicsJitterFix { get; set; }
        public int MaxPhysicsStepsPerFrame { get; set; }
        public string IdentitySha256 { get; set; } = string.Empty;
    }

    public sealed class AcceptedSceneBaselineRoute
    {
        public string RouteId { get; set; } = AcceptedSceneBaselineContract.RouteId;
        public string ScenePath { get; set; } = AcceptedSceneBaselineContract.ScenePath;
        public string BaseSha { get; set; } = string.Empty;
        public string SourceSha { get; set; } = string.Empty;
        public string SourceTree { get; set; } = string.Empty;
        public string SourceStateIdentity { get; set; } = string.Empty;
        public bool SourceDirty { get; set; }
        public bool DirtySourceOverrideUsed { get; set; }
        public string ManagedAssemblyConfiguration { get; set; } =
            PerformanceExecutionContract.ExportReleaseAssemblyConfiguration;
        public string ManagedAssemblySha256 { get; set; } = string.Empty;
        public bool GodotDebugBuild { get; set; }
        public bool GodotReleaseFeature { get; set; }
        public bool GodotTemplateFeature { get; set; }
        public bool GodotEditorFeature { get; set; }
        public bool VerifiedExportReleaseExecution { get; set; }
        public string TrialMode { get; set; } = string.Empty;
        public int FixedFps { get; set; }
        public int TrialIndex { get; set; }
        public int WarmupFrameCount { get; set; }
        public int MeasuredFrameCount { get; set; }
        public string StartingStateIdentityAlgorithm { get; set; } = "sha256(canonical-authoritative-snapshot-json)";
        public string FixedPlayerCameraRoute { get; set; } =
            "settle; 10 physics frames each move_right, move_forward, move_left, move_backward; fixed camera quarter poses; idle remainder";
        public string FixedEditIdentity { get; set; } = "remove-soil-at--2,12,-2-through-GameManager/v1";
    }

    public sealed class AcceptedSceneBaselineScenario
    {
        public string ScenarioId { get; set; } = string.Empty;
        public int SimulationSeed { get; set; }
        public string WorldModel { get; set; } = string.Empty;
        public int DeclaredInitialCitizens { get; set; }
        public int RuntimeCitizenCount { get; set; }
        public int DeclaredInitialResourceCount { get; set; }
        public int RuntimeResourceCount { get; set; }
        public int DeclaredInitialStructureCount { get; set; }
        public int RuntimeStructureCount { get; set; }
        public int DeclaredInitialBuildQueueCount { get; set; }
        public int RuntimeBuildQueueCount { get; set; }
        public int StressPopulationOverride { get; set; }
        public string InitialStateIdentity { get; set; } = string.Empty;
        public string WorldIdentity { get; set; } = string.Empty;
        public string VoxelStateIdentity { get; set; } = string.Empty;
    }

    public sealed class AcceptedSceneBaselineTiming
    {
        public string TimingSource { get; set; } =
            "System.Diagnostics.Stopwatch.GetTimestamp captured at consecutive SceneTree callback starts.";
        public string FrameIntervalDefinition { get; set; } =
            "process_frame_start_interval_ms: scheduling-inclusive wall-clock cadence between consecutive SceneTree process-frame callback starts inside the physics-bounded window.";
        public string PhysicsIntervalDefinition { get; set; } =
            "physics_frame_start_interval_ms: scheduling-inclusive wall-clock cadence between consecutive SceneTree physics-frame callback starts.";
        public string ExcludedMetricStatement { get; set; } =
            "CPU phase duration, GameManager tick CPU duration, GPU duration, render-thread duration, and whole-engine work duration are not measured or claimed.";
        public string DurationMetricsAvailability { get; set; } = "not_measured";
        public AcceptedSceneBaselineIntervalSeries FrameIntervals { get; set; } = new();
        public AcceptedSceneBaselineIntervalSeries PhysicsIntervals { get; set; } = new();
        public double AssessedP95Milliseconds { get; set; }
        public double ProductTargetP95Milliseconds { get; set; } = AcceptedSceneBaselineContract.ProductTargetP95Milliseconds;
        public double HardSafetyP95Milliseconds { get; set; } = AcceptedSceneBaselineContract.HardSafetyP95Milliseconds;
        public string Classification { get; set; } = string.Empty;
        public string RawThresholdClassification { get; set; } = string.Empty;
        public bool TargetSafetyClaimEligible { get; set; }
        public double HistoricalContextMilliseconds { get; set; } = AcceptedSceneBaselineContract.HistoricalSafetyMissMilliseconds;
        public string HistoricalContextClassification { get; set; } = "historical_context_only";
    }

    public sealed class AcceptedSceneBaselineCollision
    {
        public string Definition { get; set; } =
            "Direct child StaticBody3D and CollisionShape3D counts under World/VoxelWorldPresenter.";
        public int InitialBodyCount { get; set; }
        public int InitialShapeCount { get; set; }
        public int AfterEditBodyCount { get; set; }
        public int AfterEditShapeCount { get; set; }
    }

    public sealed class AcceptedSceneBaselineBacklog
    {
        public string Definition { get; set; } =
            "GameManager fixed-step accumulator whole simulation ticks sampled by a late-priority test callback after GameManager _Process for the same process-frame ordinal.";
        public int SampleCount { get; set; }
        public List<ulong> RawProcessFrameOrdinals { get; set; } = new();
        public List<long> RawPendingSimulationTickSamples { get; set; } = new();
        public double P50PendingSimulationTicks { get; set; }
        public double P95PendingSimulationTicks { get; set; }
        public double MaximumPendingSimulationTicks { get; set; }
    }

    public sealed class AcceptedSceneBaselineIntervalSeries
    {
        public string MetricId { get; set; } = string.Empty;
        public string RouteTagEncoding { get; set; } =
            "phase: 0=neutral, 1=active; leg: 0=neutral, 1=right, 2=forward, 3=left, 4=backward";
        public long TimestampFrequencyHertz { get; set; }
        public List<long> RawTimestamps { get; set; } = new();
        public List<ulong> RawSignalOrdinals { get; set; } = new();
        public List<double> RawIntervalMilliseconds { get; set; } = new();
        public List<byte> RawSampleRoutePhaseCodes { get; set; } = new();
        public List<byte> RawSampleLegCodes { get; set; } = new();
        public PerformanceSampleStatistics Statistics { get; set; }
        public int ActiveRouteSampleCount { get; set; }
        public PerformanceSampleStatistics ActiveRouteStatistics { get; set; }
    }

    public sealed class AcceptedSceneBaselineRouteExecution
    {
        public AcceptedSceneBaselineRouteTrace Primary { get; set; } = new();
        public AcceptedSceneBaselineRouteTrace? Replay { get; set; }
    }

    public sealed class AcceptedSceneBaselineRouteTrace
    {
        public bool SceneTreePaused { get; set; }
        public bool ManagerProcessActive { get; set; }
        public bool PlayerPhysicsProcessActive { get; set; }
        public ulong ProcessFrameStart { get; set; }
        public ulong ProcessFrameEnd { get; set; }
        public ulong PhysicsFrameStart { get; set; }
        public ulong PhysicsFrameEnd { get; set; }
        public double StartPlayerX { get; set; }
        public double StartPlayerY { get; set; }
        public double StartPlayerZ { get; set; }
        public double MinimumRequiredLegDisplacementMeters { get; set; } =
            AcceptedSceneBaselineContract.MinimumLegDisplacementMeters;
        public double MinimumObservedLegDisplacementMeters { get; set; }
        public List<AcceptedSceneBaselineRouteCheckpoint> LegCheckpoints { get; set; } = new();
    }

    public sealed record AcceptedSceneBaselineRouteCheckpoint
    {
        public string LegId { get; init; } = string.Empty;
        public int CompletedFrameCount { get; init; }
        public double PlayerX { get; init; }
        public double PlayerY { get; init; }
        public double PlayerZ { get; init; }
        public double CameraPitchDegrees { get; init; }
        public double CameraYawDegrees { get; init; }
        public double CameraRollDegrees { get; init; }
    }

    public sealed class AcceptedSceneBaselineEdit
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string Before { get; set; } = string.Empty;
        public string After { get; set; } = string.Empty;
        public bool Accepted { get; set; }
        public long WorldRevision { get; set; }
    }

    public sealed class AcceptedSceneBaselinePersistence
    {
        public bool InstrumentationExcludedFromAuthority { get; set; }
        public string InitialStateIdentity { get; set; } = string.Empty;
        public string MeasurementStartStateIdentity { get; set; } = string.Empty;
        public string MeasurementEndStateIdentity { get; set; } = string.Empty;
        public string ReplayedMeasurementStartStateIdentity { get; set; } = string.Empty;
        public string ReplayedMeasurementEndStateIdentity { get; set; } = string.Empty;
        public bool SnapshotWritten { get; set; }
        public bool SnapshotReloaded { get; set; }
        public bool RouteReplayed { get; set; }
        public string AfterEditStateIdentity { get; set; } = string.Empty;
        public string ReloadedStateIdentity { get; set; } = string.Empty;
        public string ReplayedStateIdentity { get; set; } = string.Empty;
    }

    public sealed class AcceptedSceneBaselineCausewayTransition
    {
        public string CommandKind { get; set; } = string.Empty;
        public int CommandQuantity { get; set; }
        public bool Accepted { get; set; }
        public string EventType { get; set; } = string.Empty;
        public long PreviousRevision { get; set; }
        public long Revision { get; set; }
        public string BeforeCommandStateIdentity { get; set; } = string.Empty;
        public string AfterCommandStateIdentity { get; set; } = string.Empty;
        public string AfterVoxelEditStateIdentity { get; set; } = string.Empty;
        public string ReloadedStateIdentity { get; set; } = string.Empty;
        public string ReplayedAfterCommandStateIdentity { get; set; } = string.Empty;
        public string ReplayedAfterVoxelEditStateIdentity { get; set; } = string.Empty;
    }
}
