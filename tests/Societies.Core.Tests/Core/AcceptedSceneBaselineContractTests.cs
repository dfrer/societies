using Societies.Tests;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class AcceptedSceneBaselineContractTests
    {
        [Fact]
        public void ClassifyP95_UsesHardProductAndSafetyBoundaries()
        {
            Assert.Equal("target_passed", AcceptedSceneBaselineContract.ClassifyP95(16.67, 8.0));
            Assert.Equal("target_missed", AcceptedSceneBaselineContract.ClassifyP95(16.6701, 8.0));
            Assert.Equal("target_missed", AcceptedSceneBaselineContract.ClassifyP95(33.33, 8.0));
            Assert.Equal("safety_failure", AcceptedSceneBaselineContract.ClassifyP95(33.3301, 8.0));
            Assert.Equal("safety_failure", AcceptedSceneBaselineContract.ClassifyP95(8.0, 33.3301));
        }

        [Fact]
        public void ClassifyP95_RejectsMissingOrNonFiniteMetrics()
        {
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ClassifyP95(double.NaN, 1.0));
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ClassifyP95(1.0, double.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ClassifyP95(-0.1, 1.0));
        }

        [Fact]
        public void RouteContract_PinsAcceptedSceneSeedEditAndHistoricalLabel()
        {
            Assert.Equal("res://scenes/snow_globe_voxel_foundation.tscn",
                AcceptedSceneBaselineContract.ScenePath);
            Assert.Equal("snow_globe_voxel", AcceptedSceneBaselineContract.ScenarioId);
            Assert.Equal(260827, AcceptedSceneBaselineContract.SimulationSeed);
            Assert.Equal((-2, 12, -2), (
                AcceptedSceneBaselineContract.FixedEditX,
                AcceptedSceneBaselineContract.FixedEditY,
                AcceptedSceneBaselineContract.FixedEditZ));
            VoxelWorldModule world = new(AcceptedSceneBaselineContract.SimulationSeed);
            VoxelCoord target = new(
                AcceptedSceneBaselineContract.FixedEditX,
                AcceptedSceneBaselineContract.FixedEditY,
                AcceptedSceneBaselineContract.FixedEditZ);
            Assert.Equal(VoxelMaterialId.Soil, world.GetMaterial(target));
            Assert.Equal(VoxelMaterialId.Air, world.GetMaterial(target with { Y = target.Y + 1 }));
            Assert.Equal(51.9392, AcceptedSceneBaselineContract.HistoricalSafetyMissMilliseconds);

            PrototypeRuntimeSnapshot expected = new()
            {
                ScenarioId = AcceptedSceneBaselineContract.ScenarioId,
                SimulationTick = 5,
                PlayerPosition = new PrototypeSerializableVector3 { X = 1.0f, Y = 2.0f, Z = 3.0f }
            };
            PrototypeRuntimeSnapshot actual = new()
            {
                ScenarioId = AcceptedSceneBaselineContract.ScenarioId,
                SimulationTick = 6,
                PlayerPosition = new PrototypeSerializableVector3 { X = 1.0f, Y = 2.0f, Z = 4.0f }
            };
            IReadOnlyList<string> differences =
                AcceptedSceneBaselineContract.DescribeSnapshotDifferences(expected, actual);
            Assert.Contains(differences, difference =>
                difference.Contains("$.SimulationTick", StringComparison.Ordinal));
            Assert.Contains(differences, difference =>
                difference.Contains("$.PlayerPosition.Z", StringComparison.Ordinal));
        }

        [Fact]
        public void DescribeSnapshotDifferences_RedactsAndBoundsStringScalars()
        {
            string expectedSecret = new string('x', 4096) + "expected-secret";
            string actualSecret = new string('y', 4096) + "actual-secret";
            PrototypeRuntimeSnapshot expected = new() { ScenarioId = expectedSecret };
            PrototypeRuntimeSnapshot actual = new() { ScenarioId = actualSecret };

            string difference = Assert.Single(
                AcceptedSceneBaselineContract.DescribeSnapshotDifferences(expected, actual));

            Assert.DoesNotContain(expectedSecret, difference, StringComparison.Ordinal);
            Assert.DoesNotContain(actualSecret, difference, StringComparison.Ordinal);
            Assert.Contains(AcceptedSceneBaselineContract.Sha256(expectedSecret), difference,
                StringComparison.Ordinal);
            Assert.Contains(AcceptedSceneBaselineContract.Sha256(actualSecret), difference,
                StringComparison.Ordinal);
            Assert.Contains("<redacted-string length=", difference, StringComparison.Ordinal);
            Assert.True(difference.Length < 320, "Rendered scalar difference exceeded its fixed bound.");
        }

        [Fact]
        public void BacklogAccessor_IsReadOnlyAndExcludedFromAuthoritativeSnapshotContract()
        {
            PropertyInfo property = Assert.IsAssignableFrom<PropertyInfo>(typeof(GameManager).GetProperty(
                nameof(GameManager.PendingSimulationBacklogTicks)));
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
            Assert.Null(typeof(PrototypeRuntimeSnapshot).GetProperty(
                nameof(GameManager.PendingSimulationBacklogTicks)));
            Assert.DoesNotContain(typeof(PrototypeRuntimeSnapshot).GetProperties(), candidate =>
                candidate.Name.Contains("PendingSimulationBacklog", StringComparison.Ordinal));
        }

        [Fact]
        public void ShutdownContract_QuiescesCallbacksBeforeAwaitingTreeExit()
        {
            Assert.Equal(new[]
            {
                AcceptedSceneBaselineShutdownStep.StopRouteRecorder,
                AcceptedSceneBaselineShutdownStep.ResetSyntheticInput,
                AcceptedSceneBaselineShutdownStep.DisableManagerCallbacks,
                AcceptedSceneBaselineShutdownStep.DisablePlayerCallbacks,
                AcceptedSceneBaselineShutdownStep.SubscribeToManagerTreeExited,
                AcceptedSceneBaselineShutdownStep.QueueManagerForFree,
                AcceptedSceneBaselineShutdownStep.AwaitManagerTreeExited,
                AcceptedSceneBaselineShutdownStep.VerifyManagerOutsideTree
            }, AcceptedSceneBaselineShutdownContract.Steps);
        }

        [Fact]
        public void RouteInputContract_AppliesCompleteImmediateStateAtEachLegBoundary()
        {
            string[] actions = { "move_right", "move_forward", "move_left", "move_backward" };
            Assert.Equal("immediate_input_singleton_state_at_physics_frame",
                AcceptedSceneBaselineRouteInputContract.DeliveryBoundary);

            foreach ((int step, string? activeAction) in new[]
            {
                (0, "move_right"), (10, "move_forward"), (20, "move_left"),
                (30, "move_backward"), (40, null)
            })
            {
                var state = new Dictionary<string, bool>();
                AcceptedSceneBaselineRouteInputContract.ApplyAtPhysicsBoundary(
                    step, actions, (action, pressed) => state[action] = pressed);

                Assert.Equal(actions.Length, state.Count);
                foreach (string action in actions)
                {
                    Assert.Equal(action == activeAction, state[action]);
                }
            }

            var nonBoundaryState = new Dictionary<string, bool>();
            AcceptedSceneBaselineRouteInputContract.ApplyAtPhysicsBoundary(
                1, actions, (action, pressed) => nonBoundaryState[action] = pressed);
            Assert.Empty(nonBoundaryState);
        }

        [Fact]
        public void CrossModeRouteContract_RejectsFormerQueuedInputCheckpointDivergence()
        {
            AcceptedSceneBaselineRouteTrace realtime = BuildRouteTrace();
            AcceptedSceneBaselineRouteTrace fixedDelta = BuildRouteTrace();

            AcceptedSceneBaselineCrossModeRouteContract.RequirePrimaryCheckpointIdentity(
                new[] { realtime, fixedDelta });

            realtime.StartPlayerX = 0.5;
            fixedDelta.StartPlayerX = 0.5;
            realtime.LegCheckpoints[0] = realtime.LegCheckpoints[0] with { PlayerX = 1.2583333253860474 };
            fixedDelta.LegCheckpoints[0] = fixedDelta.LegCheckpoints[0] with { PlayerX = 1.4750001430511475 };

            Assert.NotEqual(
                AcceptedSceneBaselineCrossModeRouteContract.GetPrimaryCheckpointIdentity(realtime),
                AcceptedSceneBaselineCrossModeRouteContract.GetPrimaryCheckpointIdentity(fixedDelta));
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineCrossModeRouteContract.RequirePrimaryCheckpointIdentity(
                    new[] { realtime, fixedDelta }));
        }

        [Fact]
        public void ValidateArtifact_FailsClosedOnRouteIdentityAndCollisionDrift()
        {
            AcceptedSceneBaselineTrialArtifact artifact = ValidArtifact();
            AcceptedSceneBaselineContract.ValidateArtifact(artifact);

            artifact.Route.RouteId = "neighboring-route";
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));
            artifact.Route.RouteId = AcceptedSceneBaselineContract.RouteId;
            artifact.Collisions.InitialShapeCount++;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.Scenario.RuntimeResourceCount = 1;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));
        }

        [Fact]
        public void ValidateArtifact_AcceptsExplicitPacket02V5RouteWithoutChangingV4Defaults()
        {
            AcceptedSceneBaselineTrialArtifact artifact = ValidArtifact();
            artifact.Schema = AcceptedSceneBaselineContract.Packet02Schema;
            artifact.Route.RouteId = AcceptedSceneBaselineContract.Packet02RouteId;
            artifact.Causeway = ValidCausewayEvidence();

            AcceptedSceneBaselineContract.ValidateArtifact(
                artifact,
                AcceptedSceneBaselineContract.Packet02Schema,
                AcceptedSceneBaselineContract.Packet02RouteId);
            Assert.Throws<InvalidOperationException>(() => AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact.Causeway.AfterVoxelEditStateIdentity = "tampered";
            Assert.Throws<InvalidOperationException>(() => AcceptedSceneBaselineContract.ValidateArtifact(
                artifact,
                AcceptedSceneBaselineContract.Packet02Schema,
                AcceptedSceneBaselineContract.Packet02RouteId));
        }

        [Fact]
        public void ValidateArtifact_FailsClosedOnRawTimingEligibilityAndRouteExecution()
        {
            AcceptedSceneBaselineTrialArtifact artifact = ValidArtifact();
            artifact.Timing.FrameIntervals.RawIntervalMilliseconds[0] += 0.5;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.Timing.FrameIntervals.RawSignalOrdinals[1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.Timing.AssessedP95Milliseconds += 0.5;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.Route.DirtySourceOverrideUsed = true;
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.RouteExecution.Primary.LegCheckpoints[0] =
                artifact.RouteExecution.Primary.LegCheckpoints[0] with { PlayerX = 0.01 };
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));
        }

        [Fact]
        public void ValidateArtifact_AcceptsFewerConsecutiveProcessIntervalsAsSafetyFailure()
        {
            AcceptedSceneBaselineTrialArtifact artifact = ValidArtifact();
            Assert.True(
                artifact.Timing.FrameIntervals.RawIntervalMilliseconds.Count <
                artifact.Timing.PhysicsIntervals.RawIntervalMilliseconds.Count);
            artifact.Timing.PhysicsIntervals = BuildIntervalSeries(
                artifact.Route.MeasuredFrameCount,
                40_000,
                AcceptedSceneBaselineContract.PhysicsFrameCadenceMetric);
            artifact.Timing.AssessedP95Milliseconds = Math.Max(
                artifact.Timing.FrameIntervals.Statistics.P95Milliseconds,
                artifact.Timing.PhysicsIntervals.Statistics.P95Milliseconds);
            artifact.Timing.Classification = "safety_failure";
            artifact.Timing.RawThresholdClassification = "safety_failure";
            artifact.Status = "characterized_safety_failure";

            AcceptedSceneBaselineContract.ValidateArtifact(artifact);

            Assert.Equal("safety_failure", artifact.Timing.Classification);
            Assert.Equal("characterized_safety_failure", artifact.Status);
        }

        [Fact]
        public void ValidateArtifact_FailsClosedOnIncompleteOrTamperedEnvironmentIdentity()
        {
            AcceptedSceneBaselineTrialArtifact artifact = ValidArtifact();
            artifact.Environment.IdentitySha256 = new string('0', 64);
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));

            artifact = ValidArtifact();
            artifact.Environment.CpuModel = string.Empty;
            artifact.Environment.IdentitySha256 =
                AcceptedSceneBaselineContract.ComputeEnvironmentIdentity(artifact.Environment);
            Assert.Throws<InvalidOperationException>(() =>
                AcceptedSceneBaselineContract.ValidateArtifact(artifact));
        }

        private static AcceptedSceneBaselineTrialArtifact ValidArtifact()
        {
            const int measuredFrames = 40;
            const int processFrames = 20;
            AcceptedSceneBaselineIntervalSeries frameIntervals = BuildIntervalSeries(
                processFrames, 5_000, AcceptedSceneBaselineContract.ProcessFrameCadenceMetric);
            AcceptedSceneBaselineIntervalSeries physicsIntervals = BuildIntervalSeries(
                measuredFrames, 11_000, AcceptedSceneBaselineContract.PhysicsFrameCadenceMetric);
            var backlogSamples = Enumerable.Range(0, processFrames)
                .Select(index => (long)(index % 2)).ToList();
            PerformanceSampleStatistics backlogStatistics = PerformanceRunStatistics.Compute(
                backlogSamples.Select(value => (double)value).ToArray());
            return new AcceptedSceneBaselineTrialArtifact
            {
                Status = "characterized",
                Route = new AcceptedSceneBaselineRoute
                {
                    BaseSha = new string('c', 40),
                    SourceSha = new string('a', 40),
                    SourceTree = new string('d', 40),
                    SourceStateIdentity = new string('b', 64),
                    ManagedAssemblySha256 = new string('e', 64),
                    VerifiedExportReleaseExecution = true,
                    GodotReleaseFeature = true,
                    GodotTemplateFeature = true,
                    TrialMode = AcceptedSceneBaselineContract.RealtimePerformanceMode,
                    FixedFps = 0,
                    TrialIndex = 1,
                    WarmupFrameCount = 3,
                    MeasuredFrameCount = measuredFrames
                },
                Environment = BuildEnvironment(),
                Scenario = new AcceptedSceneBaselineScenario
                {
                    ScenarioId = AcceptedSceneBaselineContract.ScenarioId,
                    SimulationSeed = AcceptedSceneBaselineContract.SimulationSeed,
                    WorldModel = AcceptedSceneBaselineContract.WorldModel,
                    InitialStateIdentity = "initial",
                    WorldIdentity = "world",
                    VoxelStateIdentity = "voxel"
                },
                RouteExecution = new AcceptedSceneBaselineRouteExecution
                {
                    Primary = BuildRouteTrace()
                },
                Timing = new AcceptedSceneBaselineTiming
                {
                    FrameIntervals = frameIntervals,
                    PhysicsIntervals = physicsIntervals,
                    AssessedP95Milliseconds = Math.Max(
                        frameIntervals.Statistics.P95Milliseconds,
                        physicsIntervals.Statistics.P95Milliseconds),
                    Classification = "target_passed",
                    RawThresholdClassification = "target_passed",
                    TargetSafetyClaimEligible = true
                },
                Collisions = new AcceptedSceneBaselineCollision
                {
                    InitialBodyCount = AcceptedSceneBaselineContract.ExpectedInitialCollisionBodies,
                    InitialShapeCount = AcceptedSceneBaselineContract.ExpectedInitialCollisionShapes,
                    AfterEditBodyCount = AcceptedSceneBaselineContract.ExpectedAfterEditCollisionBodies,
                    AfterEditShapeCount = AcceptedSceneBaselineContract.ExpectedAfterEditCollisionShapes
                },
                Backlog = new AcceptedSceneBaselineBacklog
                {
                    SampleCount = processFrames,
                    RawProcessFrameOrdinals = frameIntervals.RawSignalOrdinals.Skip(1).ToList(),
                    RawPendingSimulationTickSamples = backlogSamples,
                    P50PendingSimulationTicks = backlogStatistics.P50Milliseconds,
                    P95PendingSimulationTicks = backlogStatistics.P95Milliseconds,
                    MaximumPendingSimulationTicks = backlogStatistics.MaximumMilliseconds
                },
                Edit = new AcceptedSceneBaselineEdit
                {
                    X = AcceptedSceneBaselineContract.FixedEditX,
                    Y = AcceptedSceneBaselineContract.FixedEditY,
                    Z = AcceptedSceneBaselineContract.FixedEditZ,
                    Before = nameof(VoxelMaterialId.Soil),
                    After = nameof(VoxelMaterialId.Air),
                    Accepted = true,
                    WorldRevision = 1
                },
                Persistence = new AcceptedSceneBaselinePersistence
                {
                    InstrumentationExcludedFromAuthority = true,
                    InitialStateIdentity = "initial",
                    MeasurementStartStateIdentity = "route-start",
                    MeasurementEndStateIdentity = "route-end",
                    SnapshotWritten = true,
                    SnapshotReloaded = true,
                    RouteReplayed = false,
                    AfterEditStateIdentity = "edited",
                    ReloadedStateIdentity = "edited"
                }
            };
        }

        private static AcceptedSceneBaselineIntervalSeries BuildIntervalSeries(
            int count,
            long baseTicks,
            string metricId)
        {
            const long frequency = 1_000_000;
            var timestamps = new List<long> { 1_000_000 };
            var ordinals = new List<ulong> { 5_000 };
            var intervals = new List<double>(count);
            var phaseCodes = new List<byte>(count);
            var legCodes = new List<byte>(count);
            for (int index = 0; index < count; index++)
            {
                long deltaTicks = baseTicks + index;
                timestamps.Add(timestamps[^1] + deltaTicks);
                ordinals.Add(ordinals[^1] + 1);
                intervals.Add(deltaTicks * 1000.0 / frequency);
                bool active = index < AcceptedSceneBaselineContract.ActiveRoutePhysicsSteps;
                phaseCodes.Add(active ? (byte)1 : (byte)0);
                legCodes.Add(active ? (byte)((index / 10) + 1) : (byte)0);
            }
            double[] activeIntervals = intervals
                .Where((_, index) => phaseCodes[index] == 1).ToArray();
            return new AcceptedSceneBaselineIntervalSeries
            {
                MetricId = metricId,
                TimestampFrequencyHertz = frequency,
                RawTimestamps = timestamps,
                RawSignalOrdinals = ordinals,
                RawIntervalMilliseconds = intervals,
                RawSampleRoutePhaseCodes = phaseCodes,
                RawSampleLegCodes = legCodes,
                Statistics = PerformanceRunStatistics.Compute(intervals),
                ActiveRouteSampleCount = activeIntervals.Length,
                ActiveRouteStatistics = PerformanceRunStatistics.Compute(activeIntervals)
            };
        }

        private static AcceptedSceneBaselineEnvironment BuildEnvironment()
        {
            var environment = new AcceptedSceneBaselineEnvironment
            {
                GodotVersion = "4.6.2.stable.mono",
                OsName = "Windows",
                OsDescription = "Microsoft Windows 11",
                OsVersion = "10.0.26100.0",
                OsArchitecture = "X64",
                ProcessArchitecture = "X64",
                DotnetRuntime = ".NET 8.0.20",
                CpuModel = "Bounded Test CPU",
                LogicalProcessorCount = 8,
                DisplayServer = "headless",
                RenderingMethod = "gl_compatibility",
                RenderingDriver = "unavailable_headless",
                RenderingAdapter = "unavailable_headless",
                ViewportWidth = "unavailable_headless",
                ViewportHeight = "unavailable_headless",
                AudioDriver = "Dummy",
                Headless = true,
                PhysicsTicksPerSecond = 30,
                MaxFps = 0,
                TimeScale = 1.0,
                PhysicsJitterFix = 0.5,
                MaxPhysicsStepsPerFrame = 8
            };
            environment.IdentitySha256 =
                AcceptedSceneBaselineContract.ComputeEnvironmentIdentity(environment);
            return environment;
        }

        private static AcceptedSceneBaselineRouteTrace BuildRouteTrace() => new()
        {
            SceneTreePaused = false,
            ManagerProcessActive = true,
            PlayerPhysicsProcessActive = true,
            ProcessFrameStart = 4_999,
            ProcessFrameEnd = 5_021,
            PhysicsFrameStart = 4_999,
            PhysicsFrameEnd = 5_040,
            StartPlayerX = 0.0,
            StartPlayerY = 1.0,
            StartPlayerZ = 0.0,
            MinimumObservedLegDisplacementMeters = 1.0,
            LegCheckpoints = new List<AcceptedSceneBaselineRouteCheckpoint>
            {
                new() { LegId = "move_right", CompletedFrameCount = 10, PlayerX = 1.0,
                    PlayerY = 1.0, PlayerZ = 0.0, CameraPitchDegrees = -7.999999523162842 },
                new() { LegId = "move_forward", CompletedFrameCount = 20, PlayerX = 1.0,
                    PlayerY = 1.0, PlayerZ = 1.0, CameraPitchDegrees = -12.0, CameraYawDegrees = 18.0 },
                new() { LegId = "move_left", CompletedFrameCount = 30, PlayerX = 0.0,
                    PlayerY = 1.0, PlayerZ = 1.0, CameraPitchDegrees = -7.999999523162842 },
                new() { LegId = "move_backward", CompletedFrameCount = 40, PlayerX = 0.0,
                    PlayerY = 1.0, PlayerZ = 0.0, CameraPitchDegrees = -12.0, CameraYawDegrees = -18.0 }
            }
        };

        private static AcceptedSceneBaselineCausewayTransition ValidCausewayEvidence() => new()
        {
            CommandKind = nameof(PrototypeCausewayCommandKind.ContributeCommunityTimber),
            CommandQuantity = 1,
            Accepted = true,
            EventType = PrototypeEventTypes.CausewayMaterialCommitted,
            PreviousRevision = 0,
            Revision = 1,
            BeforeCommandStateIdentity = "causeway-before",
            AfterCommandStateIdentity = "causeway-after",
            AfterVoxelEditStateIdentity = "causeway-after",
            ReloadedStateIdentity = "causeway-after"
        };
    }
}
