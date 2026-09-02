using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Societies.Tests
{
    /// <summary>Fixed player/camera route through the real accepted scene with split timing and replay modes.</summary>
    public partial class AcceptedSceneBaselineRunner : Node
    {
        private static readonly string[] MovementActions =
            { "move_right", "move_forward", "move_left", "move_backward" };
        private static readonly UTF8Encoding Utf8NoBom = new(false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private CommonWindowRouteRecorder? _activeRouteRecorder;

        public override void _Ready() => RunAsync();

        public override void _Process(double delta) =>
            _activeRouteRecorder?.RecordPostManagerProcess();

        public override void _PhysicsProcess(double delta) =>
            _activeRouteRecorder?.RecordPostPhysicsStep();

        private async void RunAsync()
        {
            string outputDirectory = ResolveArgument("--output-dir");
            string artifactSchema = ResolveOptionalArgument("--artifact-schema", AcceptedSceneBaselineContract.Schema);
            string routeId = ResolveOptionalArgument("--route-id", AcceptedSceneBaselineContract.RouteId);
            string resultPath = Path.Combine(outputDirectory, ResolveOptionalArgument("--artifact-file-name", "accepted-scene-baseline-trial-v4.json"));
            Directory.CreateDirectory(outputDirectory);
            AcceptedSceneBaselineTrialArtifact? artifact = null;
            var failureEvidence = new AcceptedSceneBaselineFailureEvidence();
            try
            {
                artifact = await CaptureAsync(outputDirectory, failureEvidence);
                artifact.Schema = artifactSchema;
                artifact.Route.RouteId = routeId;
                failureEvidence.Stage = "artifact_serialization";
                WriteJson(resultPath, artifact);
                failureEvidence.Stage = "artifact_validation";
                AcceptedSceneBaselineContract.ValidateArtifact(artifact, artifactSchema, routeId);
                GD.Print($"ACCEPTED_SCENE_BASELINE {artifact.Status} {resultPath}");
                GetTree().Quit(artifact.Timing.Classification == "safety_failure" ? 2 : 0);
            }
            catch (Exception exception)
            {
                WriteJson(resultPath, new
                {
                    schema = artifactSchema,
                    status = "failed",
                    errorType = exception.GetType().FullName,
                    errorMessage = exception.Message,
                    evidence = new
                    {
                        completedArtifact = artifact,
                        failure = failureEvidence
                    }
                });
                GD.PrintErr($"ACCEPTED_SCENE_BASELINE FAIL: {exception}");
                GetTree().Quit(1);
            }
        }

        private async Task<AcceptedSceneBaselineTrialArtifact> CaptureAsync(
            string outputDirectory,
            AcceptedSceneBaselineFailureEvidence failureEvidence)
        {
            failureEvidence.Stage = "argument_resolution";
            int trialIndex = ResolveIntArgument("--trial-index", 1, 3);
            int warmupFrames = ResolveIntArgument("--warmup-frames", 1, 3600);
            int measuredFrames = ResolveIntArgument("--measured-frames", 40, 3600);
            string trialMode = ResolveArgument("--trial-mode");
            int fixedFps = ResolveIntArgument("--fixed-fps", 0, 60);
            bool realtimePerformance = trialMode == AcceptedSceneBaselineContract.RealtimePerformanceMode;
            bool fixedDeltaIdentity = trialMode == AcceptedSceneBaselineContract.FixedDeltaIdentityMode;
            Require(realtimePerformance || fixedDeltaIdentity, "Accepted-scene trial mode is invalid.");
            Require(realtimePerformance ? fixedFps == 0 : fixedFps == 60,
                "Accepted-scene fixed-FPS value does not match its trial mode.");
            string baseSha = ResolveArgument("--base-sha");
            string sourceSha = ResolveArgument("--source-sha");
            string sourceTree = ResolveArgument("--source-tree");
            string sourceStateIdentity = ResolveArgument("--source-state-identity");
            bool sourceDirty = bool.Parse(ResolveArgument("--source-dirty"));
            bool dirtySourceOverrideUsed = bool.Parse(ResolveArgument("--dirty-source-override"));
            string managedAssemblySha256 = ResolveArgument("--managed-assembly-sha256");
            bool requireCauseway = bool.Parse(ResolveOptionalArgument("--require-causeway", "false"));
            failureEvidence.TrialIndex = trialIndex;
            failureEvidence.TrialMode = trialMode;
            failureEvidence.FixedFps = fixedFps;
            failureEvidence.BaseSha = baseSha;
            failureEvidence.SourceSha = sourceSha;
            failureEvidence.SourceTree = sourceTree;
            failureEvidence.SourceStateIdentity = sourceStateIdentity;
            failureEvidence.SourceDirty = sourceDirty;
            failureEvidence.DirtySourceOverrideUsed = dirtySourceOverrideUsed;
            failureEvidence.Stage = "execution_identity_validation";
            ExecutionIdentity execution = CaptureExecutionIdentity();
            Require(execution.VerifiedExportRelease,
                "Accepted-scene timing route requires verified Godot ExportRelease execution.");
            AcceptedSceneBaselineEnvironment environment = CaptureEnvironmentIdentity();

            System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
            failureEvidence.Stage = "accepted_scene_loading";
            PackedScene packedScene = GD.Load<PackedScene>(AcceptedSceneBaselineContract.ScenePath)
                ?? throw new InvalidOperationException("Accepted scene failed to load.");
            Require(packedScene.ResourcePath == AcceptedSceneBaselineContract.ScenePath,
                "Accepted scene resource path mismatch.");
            GameManager manager = InstantiateAcceptedSceneManager(packedScene);
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            if (fixedDeltaIdentity)
            {
                manager.SetProcess(false);
            }

            try
            {
                failureEvidence.Stage = "accepted_scene_validation";
                PrototypeCatalogBundle catalogs = PrototypeCatalogLoader.LoadFromJsonTextProvider(fileName =>
                    ReadProjectResourceText($"res://data/{fileName}"));
                PrototypeScenarioDefinition scenario = catalogs.Scenarios.Resolve(AcceptedSceneBaselineContract.ScenarioId);
                AcceptedSceneBaselineContract.ValidateCatalog(scenario);
                ValidateManagerSelection(manager);

                PrototypeRuntimeSnapshot initialSnapshot = manager.CaptureSnapshot();
                ValidateEmptyRuntime(initialSnapshot);
                if (requireCauseway)
                {
                    Require(scenario.Causeway != null && initialSnapshot.SchemaVersion == 12 && initialSnapshot.Causeway != null &&
                        initialSnapshot.Causeway.CausewayIntegrity is > 0 and < 100 && initialSnapshot.Causeway.ReservedDryTimber > 0,
                        "Packet 02 route did not start from the authoritative causeway state.");
                }
                string initialIdentity = SnapshotIdentity(initialSnapshot);
                VoxelWorldPresenter presenter = manager.GetNode<VoxelWorldPresenter>("World/VoxelWorldPresenter");
                (int initialBodies, int initialShapes) = CountPresenterCollisions(presenter);

                if (fixedDeltaIdentity)
                {
                    await StartFixedDeltaManagerAtProcessBoundaryAsync(manager);
                }
                failureEvidence.Stage = "warmup";
                await AdvanceFramesAsync(player, warmupFrames);
                PrototypeRuntimeSnapshot measurementStartSnapshot = manager.CaptureSnapshot();
                string measurementStartIdentity = SnapshotIdentity(measurementStartSnapshot);
                failureEvidence.Stage = "primary_route_capture";
                RouteCapture primaryRoute = await CaptureRouteAsync(
                    manager, player, measuredFrames, captureTiming: realtimePerformance);
                PrototypeRuntimeSnapshot measurementEndSnapshot = manager.CaptureSnapshot();
                string measurementEndIdentity = SnapshotIdentity(measurementEndSnapshot);
                Require(CountPresenterCollisions(presenter) == (initialBodies, initialShapes),
                    "Warmup or fixed player route changed direct presenter collision counts.");
                RequireInstrumentationAbsent(measurementEndSnapshot);

                PrototypeCausewayCommandResult? causewayCommand = null;
                string causewayBeforeCommandIdentity = string.Empty;
                string causewayAfterCommandIdentity = string.Empty;
                if (requireCauseway)
                {
                    failureEvidence.Stage = "causeway_command";
                    causewayBeforeCommandIdentity = CausewayIdentity(measurementEndSnapshot);
                    causewayCommand = manager.ExecuteCausewayIntent(new PrototypeCausewayCommand
                    {
                        ActorId = "player",
                        ExpectedRevision = measurementEndSnapshot.Causeway!.Revision,
                        Kind = PrototypeCausewayCommandKind.ContributeCommunityTimber,
                        Quantity = 1
                    });
                    Require(causewayCommand.Accepted &&
                        causewayCommand.EventType == PrototypeEventTypes.CausewayMaterialCommitted &&
                        causewayCommand.PreviousRevision == 0 && causewayCommand.Revision == 1,
                        $"Packet 02 fixed causeway command failed: {causewayCommand.Rejection}.");
                    causewayAfterCommandIdentity = CausewayIdentity(manager.CaptureSnapshot());
                    Require(causewayBeforeCommandIdentity != causewayAfterCommandIdentity,
                        "Packet 02 fixed causeway command did not change authoritative causeway state.");
                }

                VoxelCoord target = FixedEditTarget();
                failureEvidence.Stage = "voxel_edit";
                RequireFixedEditTarget(measurementEndSnapshot, target);
                VoxelEditResult edit = manager.ApplyVoxelPlayerIntent(VoxelEditKind.Remove, target);
                Require(edit.Accepted && edit.WorldRevision == 1,
                    $"Fixed route voxel edit failed: {edit.Rejection}.");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                PrototypeRuntimeSnapshot afterEditSnapshot = manager.CaptureSnapshot();
                string afterEditIdentity = SnapshotIdentity(afterEditSnapshot);
                string causewayAfterEditIdentity = requireCauseway
                    ? CausewayIdentity(afterEditSnapshot) : string.Empty;
                if (requireCauseway)
                {
                    Require(causewayAfterCommandIdentity == causewayAfterEditIdentity,
                        "Voxel edit changed authoritative causeway state.");
                }
                (int afterEditBodies, int afterEditShapes) = CountPresenterCollisions(presenter);

                failureEvidence.Stage = "persistence_reload";
                string snapshotPath = manager.SaveSnapshotToDisk();
                Require(!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath),
                    "Accepted route did not persist a snapshot.");
                Require(manager.LoadLatestSnapshotFromDisk(),
                    "Accepted route failed to reload its persisted snapshot.");
                PrototypeRuntimeSnapshot reloadedSnapshot = manager.CaptureSnapshot();
                string reloadedIdentity = SnapshotIdentity(reloadedSnapshot);
                string causewayReloadedIdentity = requireCauseway
                    ? CausewayIdentity(reloadedSnapshot) : string.Empty;
                if (requireCauseway)
                {
                    Require(causewayAfterEditIdentity == causewayReloadedIdentity,
                        "Reloaded causeway state differs from the post-edit causeway state.");
                }
                RequireSnapshotIdentity(
                    afterEditSnapshot,
                    afterEditIdentity,
                    reloadedSnapshot,
                    "Reloaded authoritative state identity differs from the edit state.",
                    failureEvidence);

                RouteCapture? replayRoute = null;
                string replayedMeasurementStartIdentity = string.Empty;
                string replayedMeasurementEndIdentity = string.Empty;
                string replayedIdentity = string.Empty;
                string replayedCausewayAfterCommandIdentity = string.Empty;
                string replayedCausewayAfterEditIdentity = string.Empty;
                if (fixedDeltaIdentity)
                {
                    failureEvidence.Stage = "fixed_delta_replay_setup";
                    await QuiesceAndFreeManagerAsync(manager);
                    manager = InstantiateAcceptedSceneManager(packedScene);
                    player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
                    manager.SetProcess(false);
                    PrototypeRuntimeSnapshot replayedInitialSnapshot = manager.CaptureSnapshot();
                    RequireSnapshotIdentity(
                        initialSnapshot,
                        initialIdentity,
                        replayedInitialSnapshot,
                        "Scenario reset did not reproduce the deterministic starting state.",
                        failureEvidence);
                    await StartFixedDeltaManagerAtProcessBoundaryAsync(manager);
                    await AdvanceFramesAsync(player, warmupFrames);
                    PrototypeRuntimeSnapshot replayedMeasurementStartSnapshot = manager.CaptureSnapshot();
                    replayedMeasurementStartIdentity = SnapshotIdentity(replayedMeasurementStartSnapshot);
                    RequireSnapshotIdentity(
                        measurementStartSnapshot,
                        measurementStartIdentity,
                        replayedMeasurementStartSnapshot,
                        "Fixed warmup did not reproduce measurement-start identity.",
                        failureEvidence);
                    failureEvidence.Stage = "fixed_delta_replay_route";
                    replayRoute = await CaptureRouteAsync(
                        manager, player, measuredFrames, captureTiming: false);
                    PrototypeRuntimeSnapshot replayedMeasurementEndSnapshot = manager.CaptureSnapshot();
                    replayedMeasurementEndIdentity = SnapshotIdentity(replayedMeasurementEndSnapshot);
                    RequireSnapshotIdentity(
                        measurementEndSnapshot,
                        measurementEndIdentity,
                        replayedMeasurementEndSnapshot,
                        "Fixed player/camera route did not reproduce measurement-end identity.",
                        failureEvidence);
                    if (requireCauseway)
                    {
                        PrototypeCausewayCommandResult replayedCauseway =
                            manager.ExecuteCausewayIntent(new PrototypeCausewayCommand
                            {
                                ActorId = "player",
                                ExpectedRevision = replayedMeasurementEndSnapshot.Causeway!.Revision,
                                Kind = PrototypeCausewayCommandKind.ContributeCommunityTimber,
                                Quantity = 1
                            });
                        Require(replayedCauseway.Accepted && replayedCauseway.PreviousRevision == 0 &&
                            replayedCauseway.Revision == 1 &&
                            replayedCauseway.EventType == PrototypeEventTypes.CausewayMaterialCommitted,
                            "Fixed route replay causeway command failed or changed identity.");
                        replayedCausewayAfterCommandIdentity = CausewayIdentity(manager.CaptureSnapshot());
                        Require(causewayAfterCommandIdentity == replayedCausewayAfterCommandIdentity,
                            "Fixed route replay causeway command did not reproduce authoritative causeway state.");
                    }
                    VoxelEditResult replayed = manager.ApplyVoxelPlayerIntent(VoxelEditKind.Remove, target);
                    Require(replayed.Accepted && replayed.WorldRevision == 1, "Fixed route replay edit failed.");
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    PrototypeRuntimeSnapshot replayedAfterEditSnapshot = manager.CaptureSnapshot();
                    replayedIdentity = SnapshotIdentity(replayedAfterEditSnapshot);
                    replayedCausewayAfterEditIdentity = requireCauseway
                        ? CausewayIdentity(replayedAfterEditSnapshot) : string.Empty;
                    if (requireCauseway)
                    {
                        Require(causewayAfterCommandIdentity == replayedCausewayAfterEditIdentity,
                            "Fixed route replay voxel edit changed or failed to reproduce causeway state.");
                    }
                    RequireSnapshotIdentity(
                        afterEditSnapshot,
                        afterEditIdentity,
                        replayedAfterEditSnapshot,
                        "Replayed authoritative state identity differs from the edit state.",
                        failureEvidence);
                }

                failureEvidence.Stage = "statistics_and_artifact_build";
                PerformanceSampleStatistics frameStatistics = realtimePerformance
                    ? PerformanceRunStatistics.Compute(primaryRoute.ProcessFrameIntervalMilliseconds)
                    : default;
                PerformanceSampleStatistics physicsStatistics = realtimePerformance
                    ? PerformanceRunStatistics.Compute(primaryRoute.PhysicsFrameIntervalMilliseconds)
                    : default;
                PerformanceSampleStatistics activeFrameStatistics = realtimePerformance
                    ? PerformanceRunStatistics.Compute(primaryRoute.ProcessFrameIntervalMilliseconds
                        .Where((_, index) => primaryRoute.ProcessRoutePhaseCodes[index] == 1).ToArray())
                    : default;
                PerformanceSampleStatistics activePhysicsStatistics = realtimePerformance
                    ? PerformanceRunStatistics.Compute(primaryRoute.PhysicsFrameIntervalMilliseconds
                        .Where((_, index) => primaryRoute.PhysicsRoutePhaseCodes[index] == 1).ToArray())
                    : default;
                PerformanceSampleStatistics backlogStatistics = realtimePerformance
                    ? PerformanceRunStatistics.Compute(
                        primaryRoute.PendingBacklogTicks.Select(value => (double)value).ToArray())
                    : default;
                string rawClassification = realtimePerformance
                    ? AcceptedSceneBaselineContract.ClassifyP95(
                        frameStatistics.P95Milliseconds, physicsStatistics.P95Milliseconds)
                    : "not_applicable_identity_only";
                bool claimEligible = realtimePerformance && execution.VerifiedExportRelease &&
                    environment.Headless &&
                    environment.IdentitySha256 ==
                        AcceptedSceneBaselineContract.ComputeEnvironmentIdentity(environment) &&
                    !sourceDirty && !dirtySourceOverrideUsed;
                string classification = realtimePerformance
                    ? claimEligible ? rawClassification : "not_applied_characterization_only"
                    : "not_applicable_identity_only";
                AcceptedSceneBaselineCausewayTransition? causewayEvidence = requireCauseway
                    ? new AcceptedSceneBaselineCausewayTransition
                    {
                        CommandKind = nameof(PrototypeCausewayCommandKind.ContributeCommunityTimber),
                        CommandQuantity = 1,
                        Accepted = causewayCommand!.Accepted,
                        EventType = causewayCommand.EventType,
                        PreviousRevision = causewayCommand.PreviousRevision,
                        Revision = causewayCommand.Revision,
                        BeforeCommandStateIdentity = causewayBeforeCommandIdentity,
                        AfterCommandStateIdentity = causewayAfterCommandIdentity,
                        AfterVoxelEditStateIdentity = causewayAfterEditIdentity,
                        ReloadedStateIdentity = causewayReloadedIdentity,
                        ReplayedAfterCommandStateIdentity = replayedCausewayAfterCommandIdentity,
                        ReplayedAfterVoxelEditStateIdentity = replayedCausewayAfterEditIdentity
                    }
                    : null;

                return BuildArtifact(
                    trialIndex, warmupFrames, measuredFrames, trialMode, fixedFps, baseSha, sourceSha, sourceTree,
                    sourceStateIdentity, sourceDirty, dirtySourceOverrideUsed, managedAssemblySha256,
                    execution, environment, scenario, initialSnapshot, initialIdentity, measurementStartIdentity,
                    measurementEndIdentity, replayedMeasurementStartIdentity, replayedMeasurementEndIdentity,
                    primaryRoute, replayRoute, frameStatistics, physicsStatistics,
                    activeFrameStatistics, activePhysicsStatistics, backlogStatistics,
                    initialBodies, initialShapes,
                    afterEditBodies, afterEditShapes, target, edit, afterEditIdentity, reloadedIdentity,
                    replayedIdentity, causewayEvidence, classification, rawClassification, claimEligible);
            }
            finally
            {
                await QuiesceAndFreeManagerAsync(manager);
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", null);
            }
        }

        /// <summary>
        /// Removes the accepted-scene manager only after its update and input callbacks are
        /// quiesced. TreeExited is the lifecycle boundary; arbitrary frame waits can race the
        /// native UI teardown that follows an immediate SceneTree.Quit.
        /// </summary>
        private async Task QuiesceAndFreeManagerAsync(GameManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            if (!GodotObject.IsInstanceValid(manager) || manager.IsQueuedForDeletion())
            {
                return;
            }

            PlayerCharacter? player = manager.GetNodeOrNull<PlayerCharacter>("World/Players/LocalPlayer");
            SignalAwaiter? treeExited = null;
            bool treeExitSubscribed = false;
            foreach (AcceptedSceneBaselineShutdownStep step in AcceptedSceneBaselineShutdownContract.Steps)
            {
                switch (step)
                {
                    case AcceptedSceneBaselineShutdownStep.StopRouteRecorder:
                        _activeRouteRecorder?.Stop();
                        _activeRouteRecorder = null;
                        break;
                    case AcceptedSceneBaselineShutdownStep.ResetSyntheticInput:
                        ResetActions();
                        break;
                    case AcceptedSceneBaselineShutdownStep.DisableManagerCallbacks:
                        manager.SetProcess(false);
                        manager.SetPhysicsProcess(false);
                        manager.SetProcessInput(false);
                        manager.SetProcessUnhandledInput(false);
                        break;
                    case AcceptedSceneBaselineShutdownStep.DisablePlayerCallbacks:
                        if (player != null)
                        {
                            player.SetProcess(false);
                            player.SetPhysicsProcess(false);
                            player.SetProcessInput(false);
                            player.SetProcessUnhandledInput(false);
                        }
                        break;
                    case AcceptedSceneBaselineShutdownStep.SubscribeToManagerTreeExited:
                        treeExited = ToSignal(manager, Node.SignalName.TreeExited);
                        treeExitSubscribed = true;
                        break;
                    case AcceptedSceneBaselineShutdownStep.QueueManagerForFree:
                        manager.QueueFree();
                        break;
                    case AcceptedSceneBaselineShutdownStep.AwaitManagerTreeExited:
                        Require(treeExitSubscribed,
                            "Accepted-scene shutdown did not subscribe to manager TreeExited.");
                        await treeExited!;
                        break;
                    case AcceptedSceneBaselineShutdownStep.VerifyManagerOutsideTree:
                        Require(!manager.IsInsideTree(),
                            "Accepted-scene manager remained in the scene tree after its TreeExited boundary.");
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported accepted-scene shutdown step {step}.");
                }
            }
        }

        private GameManager InstantiateAcceptedSceneManager(PackedScene packedScene)
        {
            GameManager manager = packedScene.Instantiate<GameManager>();
            AddChild(manager);
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            player.SetProcessInput(false);
            player.SetProcessUnhandledInput(false);
            manager.SetProcessInput(false);
            manager.SetProcessUnhandledInput(false);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            return manager;
        }

        private async Task StartFixedDeltaManagerAtProcessBoundaryAsync(GameManager manager)
        {
            Require(!manager.IsProcessing(),
                "Fixed-delta manager must remain paused until its explicit scheduler boundary.");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            manager.SetProcess(true);
        }

        private async Task AdvanceFramesAsync(PlayerCharacter player, int frameCount)
        {
            Node3D cameraPivot = player.GetNode<Node3D>("CameraPivot");
            for (int frame = 0; frame < frameCount; frame++)
            {
                ResetActions();
                cameraPivot.RotationDegrees = Vector3.Zero;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            ResetActions();
        }

        private async Task<RouteCapture> CaptureRouteAsync(
            GameManager manager, PlayerCharacter player, int frameCount, bool captureTiming)
        {
            Require(_activeRouteRecorder == null, "Accepted route recorder is already active.");
            Require(frameCount >= AcceptedSceneBaselineContract.ActiveRoutePhysicsSteps,
                "Accepted route window is too short for four ten-step movement legs.");
            Require(!GetTree().Paused && manager.IsProcessing() && player.IsPhysicsProcessing(),
                "Accepted route requires live manager process and player physics callbacks.");
            Node3D cameraPivot = player.GetNode<Node3D>("CameraPivot");
            ResetActions();
            Vector3 startPosition = player.GlobalPosition;
            ulong processFrameStart = Engine.GetProcessFrames();
            ulong physicsFrameStart = Engine.GetPhysicsFrames();
            int previousProcessPriority = ProcessPriority;
            int previousPhysicsPriority = ProcessPhysicsPriority;
            ProcessPriority = int.MaxValue;
            ProcessPhysicsPriority = int.MaxValue;
            var recorder = new CommonWindowRouteRecorder(
                GetTree(), manager, player, cameraPivot, frameCount, captureTiming);
            _activeRouteRecorder = recorder;
            recorder.Start();
            try
            {
                while (!recorder.WindowComplete)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                recorder.Stop();
                ResetActions();
                Require(recorder.IsComplete,
                    $"Common-window capture was incomplete: process intervals " +
                    $"{recorder.ProcessFrameIntervalMilliseconds.Count}, physics intervals " +
                    $"{recorder.PhysicsFrameIntervalMilliseconds.Count}, backlog " +
                    $"{recorder.PendingBacklogTicks.Count}, overflow={recorder.ProcessOverflow}.");

                double minimumDisplacement = double.PositiveInfinity;
                double previousX = startPosition.X;
                double previousY = startPosition.Y;
                double previousZ = startPosition.Z;
                foreach (AcceptedSceneBaselineRouteCheckpoint checkpoint in recorder.Checkpoints)
                {
                    double dx = checkpoint.PlayerX - previousX;
                    double dy = checkpoint.PlayerY - previousY;
                    double dz = checkpoint.PlayerZ - previousZ;
                    minimumDisplacement = Math.Min(
                        minimumDisplacement, Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)));
                    previousX = checkpoint.PlayerX;
                    previousY = checkpoint.PlayerY;
                    previousZ = checkpoint.PlayerZ;
                }

                return new RouteCapture(
                    new AcceptedSceneBaselineRouteTrace
                    {
                        SceneTreePaused = GetTree().Paused,
                        ManagerProcessActive = manager.IsProcessing(),
                        PlayerPhysicsProcessActive = player.IsPhysicsProcessing(),
                        ProcessFrameStart = processFrameStart,
                        ProcessFrameEnd = Engine.GetProcessFrames(),
                        PhysicsFrameStart = physicsFrameStart,
                        PhysicsFrameEnd = Engine.GetPhysicsFrames(),
                        StartPlayerX = startPosition.X,
                        StartPlayerY = startPosition.Y,
                        StartPlayerZ = startPosition.Z,
                        MinimumObservedLegDisplacementMeters = minimumDisplacement,
                        LegCheckpoints = recorder.Checkpoints
                    },
                    captureTiming ? recorder.ProcessFrameTimestamps : new List<long>(),
                    captureTiming ? recorder.ProcessFrameOrdinals : new List<ulong>(),
                    captureTiming ? recorder.ProcessFrameIntervalMilliseconds : new List<double>(),
                    captureTiming ? recorder.ProcessRoutePhaseCodes : new List<byte>(),
                    captureTiming ? recorder.ProcessLegCodes : new List<byte>(),
                    captureTiming ? recorder.PhysicsFrameTimestamps : new List<long>(),
                    captureTiming ? recorder.PhysicsFrameOrdinals : new List<ulong>(),
                    captureTiming ? recorder.PhysicsFrameIntervalMilliseconds : new List<double>(),
                    captureTiming ? recorder.PhysicsRoutePhaseCodes : new List<byte>(),
                    captureTiming ? recorder.PhysicsLegCodes : new List<byte>(),
                    captureTiming ? recorder.PendingBacklogOrdinals : new List<ulong>(),
                    captureTiming ? recorder.PendingBacklogTicks : new List<long>());
            }
            finally
            {
                recorder.Stop();
                _activeRouteRecorder = null;
                ProcessPriority = previousProcessPriority;
                ProcessPhysicsPriority = previousPhysicsPriority;
                ResetActions();
            }
        }

        private static double ToMilliseconds(long elapsedTicks)
        {
            Require(elapsedTicks > 0, "Monotonic timing interval did not advance.");
            double milliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            Require(double.IsFinite(milliseconds) && milliseconds > 0.0,
                "Monotonic timing interval is missing or non-finite.");
            return milliseconds;
        }

        private static void ApplyRouteInputBoundary(int physicsStep, Node3D cameraPivot)
        {
            AcceptedSceneBaselineRouteInputContract.ApplyAtPhysicsBoundary(
                physicsStep, MovementActions, SetImmediateActionState);
            if (physicsStep is not (0 or 10 or 20 or 30 or 40)) return;
            cameraPivot.RotationDegrees = physicsStep switch
            {
                < 10 => new Vector3(-8.0f, 0.0f, 0.0f),
                < 20 => new Vector3(-12.0f, 18.0f, 0.0f),
                < 30 => new Vector3(-8.0f, 0.0f, 0.0f),
                < 40 => new Vector3(-12.0f, -18.0f, 0.0f),
                _ => Vector3.Zero
            };
        }

        private static void SetImmediateActionState(string action, bool pressed)
        {
            if (pressed)
            {
                Input.ActionPress(action);
                return;
            }
            Input.ActionRelease(action);
        }

        private static void ResetActions()
        {
            foreach (string action in MovementActions) SetImmediateActionState(action, false);
        }

        private static AcceptedSceneBaselineTrialArtifact BuildArtifact(
            int trialIndex, int warmupFrames, int measuredFrames, string trialMode, int fixedFps,
            string baseSha, string sourceSha,
            string sourceTree, string sourceStateIdentity, bool sourceDirty, bool dirtySourceOverrideUsed,
            string managedAssemblySha256, ExecutionIdentity execution,
            AcceptedSceneBaselineEnvironment environment, PrototypeScenarioDefinition scenario,
            PrototypeRuntimeSnapshot initialSnapshot, string initialIdentity, string measurementStartIdentity,
            string measurementEndIdentity, string replayedMeasurementStartIdentity,
            string replayedMeasurementEndIdentity, RouteCapture primaryRoute, RouteCapture? replayRoute,
            PerformanceSampleStatistics frameStatistics,
            PerformanceSampleStatistics physicsStatistics,
            PerformanceSampleStatistics activeFrameStatistics,
            PerformanceSampleStatistics activePhysicsStatistics,
            PerformanceSampleStatistics backlogStatistics,
            int initialBodies, int initialShapes, int afterEditBodies, int afterEditShapes,
            VoxelCoord target, VoxelEditResult edit, string afterEditIdentity, string reloadedIdentity,
            string replayedIdentity, AcceptedSceneBaselineCausewayTransition? causewayEvidence,
            string classification, string rawClassification, bool claimEligible)
        {
            bool realtimePerformance = trialMode == AcceptedSceneBaselineContract.RealtimePerformanceMode;
            return new AcceptedSceneBaselineTrialArtifact
            {
                Status = realtimePerformance
                    ? claimEligible
                        ? rawClassification == "safety_failure" ? "characterized_safety_failure" : "characterized"
                        : "smoke_characterized_dirty_source"
                    : sourceDirty || dirtySourceOverrideUsed
                        ? "identity_replay_verified_dirty_source_smoke" : "identity_replay_verified",
                Route = new AcceptedSceneBaselineRoute
                {
                    BaseSha = baseSha, SourceSha = sourceSha, SourceTree = sourceTree,
                    SourceStateIdentity = sourceStateIdentity, SourceDirty = sourceDirty,
                    DirtySourceOverrideUsed = dirtySourceOverrideUsed,
                    ManagedAssemblyConfiguration = execution.ManagedAssemblyConfiguration,
                    ManagedAssemblySha256 = managedAssemblySha256,
                    GodotDebugBuild = execution.GodotDebugBuild,
                    GodotReleaseFeature = execution.GodotReleaseFeature,
                    GodotTemplateFeature = execution.GodotTemplateFeature,
                    GodotEditorFeature = execution.GodotEditorFeature,
                    VerifiedExportReleaseExecution = execution.VerifiedExportRelease,
                    TrialMode = trialMode,
                    FixedFps = fixedFps,
                    TrialIndex = trialIndex, WarmupFrameCount = warmupFrames, MeasuredFrameCount = measuredFrames
                },
                Environment = environment,
                Scenario = new AcceptedSceneBaselineScenario
                {
                    ScenarioId = scenario.Id, SimulationSeed = scenario.SimulationSeed, WorldModel = scenario.WorldModel,
                    DeclaredInitialCitizens = scenario.InitialCitizens, RuntimeCitizenCount = initialSnapshot.Workers.Count,
                    DeclaredInitialResourceCount = scenario.InitialTrees + scenario.InitialRocks +
                        scenario.InitialBerryBushes + scenario.InitialClayDeposits + scenario.InitialReedBeds,
                    RuntimeResourceCount = initialSnapshot.Resources.Count,
                    DeclaredInitialStructureCount = scenario.StartingStructures.Count,
                    RuntimeStructureCount = initialSnapshot.Settlement?.Structures.Count ?? 0,
                    DeclaredInitialBuildQueueCount = scenario.StartingBuildQueue.Count,
                    RuntimeBuildQueueCount = initialSnapshot.Settlement?.BuildQueue.Count ?? 0,
                    StressPopulationOverride = scenario.StressPopulationOverride,
                    InitialStateIdentity = initialIdentity, WorldIdentity = initialSnapshot.WorldHash,
                    VoxelStateIdentity = initialSnapshot.VoxelWorld?.RootHash ?? string.Empty
                },
                RouteExecution = new AcceptedSceneBaselineRouteExecution
                {
                    Primary = primaryRoute.Trace,
                    Replay = replayRoute?.Trace
                },
                Timing = new AcceptedSceneBaselineTiming
                {
                    FrameIntervals = new AcceptedSceneBaselineIntervalSeries
                    {
                        MetricId = AcceptedSceneBaselineContract.ProcessFrameCadenceMetric,
                        TimestampFrequencyHertz = Stopwatch.Frequency,
                        RawTimestamps = primaryRoute.ProcessFrameTimestamps,
                        RawSignalOrdinals = primaryRoute.ProcessFrameOrdinals,
                        RawIntervalMilliseconds = primaryRoute.ProcessFrameIntervalMilliseconds,
                        RawSampleRoutePhaseCodes = primaryRoute.ProcessRoutePhaseCodes,
                        RawSampleLegCodes = primaryRoute.ProcessLegCodes,
                        Statistics = frameStatistics,
                        ActiveRouteSampleCount = primaryRoute.ProcessRoutePhaseCodes.Count(value => value == 1),
                        ActiveRouteStatistics = activeFrameStatistics
                    },
                    PhysicsIntervals = new AcceptedSceneBaselineIntervalSeries
                    {
                        MetricId = AcceptedSceneBaselineContract.PhysicsFrameCadenceMetric,
                        TimestampFrequencyHertz = Stopwatch.Frequency,
                        RawTimestamps = primaryRoute.PhysicsFrameTimestamps,
                        RawSignalOrdinals = primaryRoute.PhysicsFrameOrdinals,
                        RawIntervalMilliseconds = primaryRoute.PhysicsFrameIntervalMilliseconds,
                        RawSampleRoutePhaseCodes = primaryRoute.PhysicsRoutePhaseCodes,
                        RawSampleLegCodes = primaryRoute.PhysicsLegCodes,
                        Statistics = physicsStatistics,
                        ActiveRouteSampleCount = primaryRoute.PhysicsRoutePhaseCodes.Count(value => value == 1),
                        ActiveRouteStatistics = activePhysicsStatistics
                    },
                    AssessedP95Milliseconds = realtimePerformance
                        ? Math.Max(frameStatistics.P95Milliseconds, physicsStatistics.P95Milliseconds) : 0.0,
                    Classification = classification, RawThresholdClassification = rawClassification,
                    TargetSafetyClaimEligible = claimEligible
                },
                Collisions = new AcceptedSceneBaselineCollision
                {
                    InitialBodyCount = initialBodies, InitialShapeCount = initialShapes,
                    AfterEditBodyCount = afterEditBodies, AfterEditShapeCount = afterEditShapes
                },
                Backlog = new AcceptedSceneBaselineBacklog
                {
                    SampleCount = realtimePerformance ? primaryRoute.PendingBacklogTicks.Count : 0,
                    RawProcessFrameOrdinals = primaryRoute.PendingBacklogOrdinals,
                    RawPendingSimulationTickSamples = primaryRoute.PendingBacklogTicks,
                    P50PendingSimulationTicks = backlogStatistics.P50Milliseconds,
                    P95PendingSimulationTicks = backlogStatistics.P95Milliseconds,
                    MaximumPendingSimulationTicks = backlogStatistics.MaximumMilliseconds
                },
                Edit = new AcceptedSceneBaselineEdit
                {
                    X = target.X, Y = target.Y, Z = target.Z, Before = nameof(VoxelMaterialId.Soil),
                    After = nameof(VoxelMaterialId.Air), Accepted = edit.Accepted, WorldRevision = edit.WorldRevision
                },
                Persistence = new AcceptedSceneBaselinePersistence
                {
                    InstrumentationExcludedFromAuthority = true, InitialStateIdentity = initialIdentity,
                    MeasurementStartStateIdentity = measurementStartIdentity,
                    MeasurementEndStateIdentity = measurementEndIdentity,
                    ReplayedMeasurementStartStateIdentity = replayedMeasurementStartIdentity,
                    ReplayedMeasurementEndStateIdentity = replayedMeasurementEndIdentity,
                    SnapshotWritten = true, SnapshotReloaded = true, RouteReplayed = replayRoute != null,
                    AfterEditStateIdentity = afterEditIdentity, ReloadedStateIdentity = reloadedIdentity,
                    ReplayedStateIdentity = replayedIdentity
                },
                Causeway = causewayEvidence,
                Limitations = new List<string>
                {
                    realtimePerformance
                        ? "Headless ExportRelease process-frame and physics-frame callback-start wall-clock cadence; no CPU phase, GPU, render-thread, or whole-engine duration timing."
                        : "Fixed-delta ExportRelease identity/replay evidence; timing and backlog thresholds are not assessed.",
                    "Dirty-source override results are smoke evidence and cannot classify target or hard safety.",
                    "Not human acceptance, hosted CI evidence, accessibility acceptance, or release readiness.",
                    "The 51.9392 ms value is historical context only and is not reclassified."
                }
            };
        }

        private static ExecutionIdentity CaptureExecutionIdentity()
        {
            string configuration = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? string.Empty;
            bool debugBuild = OS.IsDebugBuild();
            bool releaseFeature = OS.HasFeature("release");
            bool templateFeature = OS.HasFeature("template");
            bool editorFeature = OS.HasFeature("editor");
            return new ExecutionIdentity(
                configuration, debugBuild, releaseFeature, templateFeature, editorFeature,
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    configuration, debugBuild, releaseFeature, templateFeature, editorFeature));
        }

        private static AcceptedSceneBaselineEnvironment CaptureEnvironmentIdentity()
        {
            string displayServer = NormalizeEnvironmentValue(DisplayServer.GetName(), "display server");
            bool headless = displayServer.Equals("headless", StringComparison.OrdinalIgnoreCase);
            string unavailable = headless ? "unavailable_headless" : "unavailable";
            string renderingMethod = ProjectSettings.GetSetting(
                "rendering/renderer/rendering_method", unavailable).AsString();
            string audioDriver;
            try
            {
                audioDriver = AudioServer.GetDriverName();
            }
            catch
            {
                audioDriver = unavailable;
            }

            var environment = new AcceptedSceneBaselineEnvironment
            {
                GodotVersion = NormalizeEnvironmentValue(
                    Engine.GetVersionInfo()["string"].AsString(), "Godot version"),
                OsName = NormalizeEnvironmentValue(OS.GetName(), "OS name"),
                OsDescription = NormalizeEnvironmentValue(RuntimeInformation.OSDescription, "OS description"),
                OsVersion = NormalizeEnvironmentValue(System.Environment.OSVersion.VersionString, "OS version"),
                OsArchitecture = NormalizeEnvironmentValue(
                    RuntimeInformation.OSArchitecture.ToString(), "OS architecture"),
                ProcessArchitecture = NormalizeEnvironmentValue(
                    RuntimeInformation.ProcessArchitecture.ToString(), "process architecture"),
                DotnetRuntime = NormalizeEnvironmentValue(
                    RuntimeInformation.FrameworkDescription, ".NET runtime"),
                CpuModel = NormalizeEnvironmentValue(OS.GetProcessorName(), "CPU model"),
                LogicalProcessorCount = System.Environment.ProcessorCount,
                DisplayServer = displayServer,
                RenderingMethod = NormalizeEnvironmentValue(renderingMethod, "rendering method", unavailable),
                RenderingDriver = unavailable,
                RenderingAdapter = unavailable,
                ViewportWidth = headless ? "unavailable_headless" : "unavailable",
                ViewportHeight = headless ? "unavailable_headless" : "unavailable",
                AudioDriver = NormalizeEnvironmentValue(audioDriver, "audio driver", unavailable),
                Headless = headless,
                PhysicsTicksPerSecond = Engine.PhysicsTicksPerSecond,
                MaxFps = Engine.MaxFps,
                TimeScale = Engine.TimeScale,
                PhysicsJitterFix = Engine.PhysicsJitterFix,
                MaxPhysicsStepsPerFrame = Engine.MaxPhysicsStepsPerFrame
            };
            environment.IdentitySha256 = AcceptedSceneBaselineContract.ComputeEnvironmentIdentity(environment);
            return environment;
        }

        private static string NormalizeEnvironmentValue(
            string? value,
            string label,
            string? fallback = null)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();
            Require(!string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 160 &&
                !normalized.Any(char.IsControl),
                $"Accepted-scene {label} is empty, unbounded, or contains control characters.");
            return normalized;
        }

        private static void ValidateManagerSelection(GameManager manager)
        {
            Require(manager.CurrentScenarioId == AcceptedSceneBaselineContract.ScenarioId,
                "Real accepted scene selected the wrong scenario.");
            Require(manager.SimulationSeed == AcceptedSceneBaselineContract.SimulationSeed,
                "Real accepted scene selected the wrong simulation seed.");
            Require(manager.UsesVoxelWorld, "Real accepted scene did not select voxel authority.");
            Require(manager.CitizenCount == 0, "Real accepted scene unexpectedly contains citizens.");
        }

        private static void ValidateEmptyRuntime(PrototypeRuntimeSnapshot snapshot)
        {
            Require(snapshot.ScenarioId == AcceptedSceneBaselineContract.ScenarioId, "Snapshot scenario mismatch.");
            Require(snapshot.SimulationSeed == AcceptedSceneBaselineContract.SimulationSeed,
                "Snapshot simulation seed mismatch.");
            Require(snapshot.WorldModel == AcceptedSceneBaselineContract.WorldModel, "Snapshot world model mismatch.");
            Require(snapshot.Workers.Count == 0 && snapshot.Resources.Count == 0,
                "Accepted runtime must contain no citizens or initial resources.");
            Require((snapshot.Settlement?.Structures.Count ?? 0) == 0 &&
                (snapshot.Settlement?.BuildQueue.Count ?? 0) == 0,
                "Accepted runtime must contain no initial structures or build queue.");
            Require(snapshot.VoxelWorld != null && snapshot.VoxelWorld.WorldRevision == 0 &&
                snapshot.VoxelWorld.Events.Count == 0,
                "Accepted runtime must start from an unedited voxel state.");
        }

        private static void RequireInstrumentationAbsent(PrototypeRuntimeSnapshot snapshot)
        {
            string json = PrototypePersistenceService.SerializeSnapshot(snapshot);
            foreach (string forbidden in new[]
            {
                "pendingSimulationBacklogTicks", "cpuFrameMilliseconds", "physicsFrameMilliseconds",
                AcceptedSceneBaselineContract.RouteId
            })
            {
                Require(!json.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Instrumentation field '{forbidden}' entered the authoritative snapshot.");
            }
        }

        private static VoxelCoord FixedEditTarget() => new(
            AcceptedSceneBaselineContract.FixedEditX,
            AcceptedSceneBaselineContract.FixedEditY,
            AcceptedSceneBaselineContract.FixedEditZ);

        private static void RequireFixedEditTarget(PrototypeRuntimeSnapshot snapshot, VoxelCoord target)
        {
            VoxelWorldModule world = VoxelWorldModule.Restore(snapshot.VoxelWorld
                ?? throw new InvalidOperationException("Accepted scene snapshot has no voxel world."));
            Require(world.GetMaterial(target) == VoxelMaterialId.Soil &&
                world.GetMaterial(target with { Y = target.Y + 1 }) == VoxelMaterialId.Air,
                "Fixed route edit target no longer identifies exposed soil.");
        }

        private static (int Bodies, int Shapes) CountPresenterCollisions(VoxelWorldPresenter presenter)
        {
            StaticBody3D[] bodies = presenter.GetChildren().OfType<StaticBody3D>().ToArray();
            return (bodies.Length,
                bodies.SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()).Count());
        }

        private static string SnapshotIdentity(PrototypeRuntimeSnapshot snapshot) =>
            AcceptedSceneBaselineContract.Sha256(PrototypePersistenceService.SerializeSnapshot(snapshot));

        private static string CausewayIdentity(PrototypeRuntimeSnapshot snapshot)
        {
            PrototypeCausewayStateSnapshot causeway = snapshot.Causeway ??
                throw new InvalidOperationException("Packet 02 route snapshot is missing causeway state.");
            return AcceptedSceneBaselineContract.Sha256(
                JsonSerializer.Serialize(causeway));
        }

        private static void RequireSnapshotIdentity(
            PrototypeRuntimeSnapshot expectedSnapshot,
            string expectedIdentity,
            PrototypeRuntimeSnapshot actualSnapshot,
            string message,
            AcceptedSceneBaselineFailureEvidence failureEvidence)
        {
            string actualIdentity = SnapshotIdentity(actualSnapshot);
            if (string.Equals(expectedIdentity, actualIdentity, StringComparison.Ordinal))
            {
                return;
            }

            IReadOnlyList<string> differences = AcceptedSceneBaselineContract.DescribeSnapshotDifferences(
                expectedSnapshot,
                actualSnapshot);
            string detail = differences.Count == 0
                ? "serialized snapshots differ without a decoded field difference"
                : string.Join("; ", differences);
            failureEvidence.MismatchDiagnostics.Clear();
            failureEvidence.MismatchDiagnostics.AddRange(differences.Count == 0
                ? new[] { "serialized snapshots differ without a decoded field difference" }
                : differences);
            throw new InvalidOperationException(
                $"{message} Expected identity {expectedIdentity}, actual {actualIdentity}. Differences: {detail}");
        }

        private static string ReadProjectResourceText(string resourcePath)
        {
            using Godot.FileAccess? file = Godot.FileAccess.Open(
                resourcePath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Error openError = Godot.FileAccess.GetOpenError();
                throw new FileNotFoundException(
                    $"Missing prototype catalog resource '{resourcePath}' (Godot error {openError}).",
                    resourcePath);
            }
            return file.GetAsText();
        }

        private static string ResolveArgument(string name)
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == name) return arguments[index + 1];
            }
            throw new ArgumentException($"Missing required runner argument {name}.");
        }

        private static string ResolveOptionalArgument(string name, string fallback)
        {
            string[] arguments = OS.GetCmdlineUserArgs().ToArray();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : fallback;
        }

        private static int ResolveIntArgument(string name, int minimum, int maximum)
        {
            string value = ResolveArgument(name);
            if (!int.TryParse(value, out int result) || result < minimum || result > maximum)
            {
                throw new ArgumentOutOfRangeException(name,
                    $"{name} must be between {minimum} and {maximum}.");
            }
            return result;
        }

        private static void WriteJson(string path, object value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), Utf8NoBom);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed record RouteCapture(
            AcceptedSceneBaselineRouteTrace Trace,
            List<long> ProcessFrameTimestamps,
            List<ulong> ProcessFrameOrdinals,
            List<double> ProcessFrameIntervalMilliseconds,
            List<byte> ProcessRoutePhaseCodes,
            List<byte> ProcessLegCodes,
            List<long> PhysicsFrameTimestamps,
            List<ulong> PhysicsFrameOrdinals,
            List<double> PhysicsFrameIntervalMilliseconds,
            List<byte> PhysicsRoutePhaseCodes,
            List<byte> PhysicsLegCodes,
            List<ulong> PendingBacklogOrdinals,
            List<long> PendingBacklogTicks);

        private sealed class CommonWindowRouteRecorder
        {
            private readonly SceneTree _tree;
            private readonly GameManager _manager;
            private readonly PlayerCharacter _player;
            private readonly Node3D _cameraPivot;
            private readonly int _physicsIntervalCount;
            private readonly bool _captureTiming;
            private readonly Callable _processFrameCallable;
            private readonly Callable _physicsFrameCallable;
            private bool _started;
            private int _physicsStepsStarted;
            private int _lastPostPhysicsStep;
            private byte _previousProcessPhaseCode;
            private byte _previousProcessLegCode;

            public CommonWindowRouteRecorder(
                SceneTree tree,
                GameManager manager,
                PlayerCharacter player,
                Node3D cameraPivot,
                int physicsIntervalCount,
                bool captureTiming)
            {
                _tree = tree;
                _manager = manager;
                _player = player;
                _cameraPivot = cameraPivot;
                _physicsIntervalCount = physicsIntervalCount;
                _captureTiming = captureTiming;
                _processFrameCallable = Callable.From(RecordProcessFrame);
                _physicsFrameCallable = Callable.From(RecordPhysicsFrame);
                ProcessFrameTimestamps = new List<long>();
                ProcessFrameOrdinals = new List<ulong>();
                ProcessFrameIntervalMilliseconds = new List<double>();
                ProcessRoutePhaseCodes = new List<byte>();
                ProcessLegCodes = new List<byte>();
                PhysicsFrameTimestamps = new List<long>(physicsIntervalCount + 1);
                PhysicsFrameOrdinals = new List<ulong>(physicsIntervalCount + 1);
                PhysicsFrameIntervalMilliseconds = new List<double>(physicsIntervalCount);
                PhysicsRoutePhaseCodes = new List<byte>(physicsIntervalCount);
                PhysicsLegCodes = new List<byte>(physicsIntervalCount);
                PendingBacklogOrdinals = new List<ulong>();
                PendingBacklogTicks = new List<long>();
                Checkpoints = new List<AcceptedSceneBaselineRouteCheckpoint>(4);
            }

            public List<long> ProcessFrameTimestamps { get; }
            public List<ulong> ProcessFrameOrdinals { get; }
            public List<double> ProcessFrameIntervalMilliseconds { get; }
            public List<byte> ProcessRoutePhaseCodes { get; }
            public List<byte> ProcessLegCodes { get; }
            public List<long> PhysicsFrameTimestamps { get; }
            public List<ulong> PhysicsFrameOrdinals { get; }
            public List<double> PhysicsFrameIntervalMilliseconds { get; }
            public List<byte> PhysicsRoutePhaseCodes { get; }
            public List<byte> PhysicsLegCodes { get; }
            public List<ulong> PendingBacklogOrdinals { get; }
            public List<long> PendingBacklogTicks { get; }
            public List<AcceptedSceneBaselineRouteCheckpoint> Checkpoints { get; }
            public bool WindowStarted { get; private set; }
            public bool WindowComplete { get; private set; }
            public bool ProcessOverflow { get; private set; }

            public bool IsComplete =>
                WindowComplete && Checkpoints.Count == 4 &&
                (!_captureTiming ||
                    !ProcessOverflow &&
                    ProcessFrameIntervalMilliseconds.Count > 0 &&
                    ProcessFrameIntervalMilliseconds.Count <=
                        AcceptedSceneBaselineContract.MaximumProcessFrameIntervals &&
                    ProcessFrameTimestamps.Count == ProcessFrameIntervalMilliseconds.Count + 1 &&
                    ProcessFrameOrdinals.Count == ProcessFrameTimestamps.Count &&
                    ProcessRoutePhaseCodes.Count == ProcessFrameIntervalMilliseconds.Count &&
                    ProcessLegCodes.Count == ProcessFrameIntervalMilliseconds.Count &&
                    PhysicsFrameTimestamps.Count == _physicsIntervalCount + 1 &&
                    PhysicsFrameOrdinals.Count == PhysicsFrameTimestamps.Count &&
                    PhysicsFrameIntervalMilliseconds.Count == _physicsIntervalCount &&
                    PhysicsRoutePhaseCodes.Count == _physicsIntervalCount &&
                    PhysicsLegCodes.Count == _physicsIntervalCount &&
                    PendingBacklogOrdinals.Count == ProcessFrameIntervalMilliseconds.Count &&
                    PendingBacklogTicks.Count == ProcessFrameIntervalMilliseconds.Count);

            public void Start()
            {
                Require(!_started, "Consecutive signal timing recorder was started more than once.");
                if (_captureTiming)
                {
                    _tree.Connect(SceneTree.SignalName.ProcessFrame, _processFrameCallable);
                }
                try
                {
                    _tree.Connect(SceneTree.SignalName.PhysicsFrame, _physicsFrameCallable);
                    _started = true;
                }
                catch
                {
                    if (_captureTiming)
                    {
                        _tree.Disconnect(SceneTree.SignalName.ProcessFrame, _processFrameCallable);
                    }
                    throw;
                }
            }

            public void Stop()
            {
                if (!_started)
                {
                    return;
                }
                if (_captureTiming)
                {
                    _tree.Disconnect(SceneTree.SignalName.ProcessFrame, _processFrameCallable);
                }
                _tree.Disconnect(SceneTree.SignalName.PhysicsFrame, _physicsFrameCallable);
                _started = false;
            }

            private void RecordProcessFrame()
            {
                if (!WindowStarted || WindowComplete)
                {
                    return;
                }
                if (ProcessFrameIntervalMilliseconds.Count >=
                    AcceptedSceneBaselineContract.MaximumProcessFrameIntervals)
                {
                    ProcessOverflow = true;
                    return;
                }

                long timestamp = Stopwatch.GetTimestamp();
                (byte phaseCode, byte legCode) = TagsForPhysicsStep(_physicsStepsStarted - 1);
                if (ProcessFrameTimestamps.Count > 0)
                {
                    ProcessFrameIntervalMilliseconds.Add(
                        ToMilliseconds(timestamp - ProcessFrameTimestamps[^1]));
                    ProcessRoutePhaseCodes.Add(_previousProcessPhaseCode);
                    ProcessLegCodes.Add(_previousProcessLegCode);
                }
                ProcessFrameTimestamps.Add(timestamp);
                ProcessFrameOrdinals.Add(Engine.GetProcessFrames());
                _previousProcessPhaseCode = phaseCode;
                _previousProcessLegCode = legCode;
            }

            private void RecordPhysicsFrame()
            {
                if (WindowComplete)
                {
                    return;
                }

                long timestamp = Stopwatch.GetTimestamp();
                ulong ordinal = Engine.GetPhysicsFrames();
                if (!WindowStarted)
                {
                    WindowStarted = true;
                    if (_captureTiming)
                    {
                        PhysicsFrameTimestamps.Add(timestamp);
                        PhysicsFrameOrdinals.Add(ordinal);
                    }
                }
                else if (_captureTiming)
                {
                    PhysicsFrameIntervalMilliseconds.Add(
                        ToMilliseconds(timestamp - PhysicsFrameTimestamps[^1]));
                    PhysicsFrameTimestamps.Add(timestamp);
                    PhysicsFrameOrdinals.Add(ordinal);
                    (byte phaseCode, byte legCode) = TagsForPhysicsStep(_physicsStepsStarted - 1);
                    PhysicsRoutePhaseCodes.Add(phaseCode);
                    PhysicsLegCodes.Add(legCode);
                }

                if (_physicsStepsStarted == _physicsIntervalCount)
                {
                    WindowComplete = true;
                    ResetActions();
                    return;
                }

                ApplyRouteInputBoundary(_physicsStepsStarted, _cameraPivot);
                _physicsStepsStarted++;
            }

            public void RecordPostManagerProcess()
            {
                if (!_captureTiming || !WindowStarted || WindowComplete ||
                    ProcessFrameOrdinals.Count < 2)
                {
                    return;
                }

                ulong ordinal = Engine.GetProcessFrames();
                if (ProcessFrameOrdinals[^1] != ordinal ||
                    (PendingBacklogOrdinals.Count > 0 && PendingBacklogOrdinals[^1] == ordinal))
                {
                    return;
                }
                PendingBacklogOrdinals.Add(ordinal);
                PendingBacklogTicks.Add(_manager.PendingSimulationBacklogTicks);
            }

            public void RecordPostPhysicsStep()
            {
                if (!WindowStarted || _physicsStepsStarted == _lastPostPhysicsStep)
                {
                    return;
                }
                _lastPostPhysicsStep = _physicsStepsStarted;
                if (_physicsStepsStarted is not (10 or 20 or 30 or 40))
                {
                    return;
                }

                int legIndex = (_physicsStepsStarted / 10) - 1;
                Vector3 position = _player.GlobalPosition;
                Vector3 cameraRotation = _cameraPivot.RotationDegrees;
                Checkpoints.Add(new AcceptedSceneBaselineRouteCheckpoint
                {
                    LegId = MovementActions[legIndex],
                    CompletedFrameCount = _physicsStepsStarted,
                    PlayerX = position.X,
                    PlayerY = position.Y,
                    PlayerZ = position.Z,
                    CameraPitchDegrees = cameraRotation.X,
                    CameraYawDegrees = cameraRotation.Y,
                    CameraRollDegrees = cameraRotation.Z
                });
            }

            private static (byte PhaseCode, byte LegCode) TagsForPhysicsStep(int physicsStep)
            {
                if (physicsStep < 0 || physicsStep >= AcceptedSceneBaselineContract.ActiveRoutePhysicsSteps)
                {
                    return (0, 0);
                }
                return (1, (byte)((physicsStep / 10) + 1));
            }
        }

        private sealed record ExecutionIdentity(
            string ManagedAssemblyConfiguration,
            bool GodotDebugBuild,
            bool GodotReleaseFeature,
            bool GodotTemplateFeature,
            bool GodotEditorFeature,
            bool VerifiedExportRelease);
    }
}
