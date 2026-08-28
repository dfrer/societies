using Godot;
using Societies.Multiplayer;
using Societies.Presentation;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Societies.Core
{
    /// <summary>
    /// Prototype runtime orchestrator. Scene setup and presentation stay here; deterministic
    /// simulation state lives in <see cref="PrototypeRuntimeSession"/>.
    /// </summary>
    public partial class GameManager : Node
    {
        private const double TickIntervalSeconds = PrototypeSimulationTime.TickIntervalSeconds;
        private const int MaxTicksPerFrame = 12;
        private const double BacklogWarningCooldownSeconds = 5.0;
        private const string RuntimeMetricsEnvironmentVariable = "SOCIETIES_PERF_METRICS";
        private const string DefaultScenarioId = "balanced_basin";
        private const string ExactBranchAndBoundSelectorMode = "exact_branch_and_bound";
        private const string ExhaustiveReferenceSelectorMode = "exhaustive_reference";
        private const string ExactBoundedExtractionMode = "exact_bounded";
        private const string ExhaustiveReferenceExtractionMode = "exhaustive_reference";
        private const string CachedDistanceOnlyRouteDistanceMode = "cached_distance_only";
        private const string FullMaterializationReferenceRouteDistanceMode = "full_materialization_reference";

        public static GameManager? Instance { get; private set; }

        [Export] private bool _autoStartSinglePlayer = true;
        [Export] private string _scenarioId = DefaultScenarioId;
        [Export] private int _simulationSeed = 1337;
        [Export] private int _initialTrees = 36;
        [Export] private int _initialRocks = 24;
        [Export] private int _initialBerryBushes = 14;
        [Export] private int _initialWorkers = 3;

        private NetworkManager? _networkManager;
        private EntityManager? _entityManager;
        private TerrainGenerator? _terrain;
        private EnvironmentController? _environmentController;
        private PrototypeHud? _hud;
        private PlayerCharacter? _player;
        private ObserverCameraRig? _observerRig;
        private Node3D? _worldRoot;
        private Node3D? _playersRoot;
        private Node3D? _agentsRoot;
        private Node3D? _entitiesRoot;
        private Node3D? _environmentRoot;
        private Node? _systemsRoot;
        private PrototypeCatalogBundle? _catalogs;
        private PrototypeScenarioDefinition? _scenario;
        private PrototypeRunArtifactManager? _artifactManager;
        private PrototypeRuntimeSession? _runtimeSession;
        private PrototypeSettlementScenePresenter? _scenePresenter;
        private VoxelWorldPresenter? _voxelPresenter;
        private VoxelWorldcraftPresenter? _worldcraftPresenter;
        private bool _worldcraftBuildMode;
        private string _selectedWorldcraftPieceId = "wood_floor";
        private int _worldcraftRotation;
        private bool _voxelInventoryOpen;
        private readonly InventoryComponent _fallbackInventory = new();
        private readonly FixedStepAccumulator _fixedStepAccumulator = new(TickIntervalSeconds, MaxTicksPerFrame);
        private readonly PrototypeContributionInteraction _contributionInteraction = new();
        private PrototypeCognitionModule _civicCognitionModule = new();
        private readonly RuntimeMetricsCollector? _runtimeMetrics = CreateRuntimeMetricsCollector();
        private double _backlogWarningCooldownSeconds;
        private CameraMode _cameraMode = CameraMode.Player;
        private TerrainOverlayMode _overlayMode = TerrainOverlayMode.None;
        private PrototypeWorldSummary? _lastWorldSummary;
        private long _lastPresentedResourceRevision = -1;
        private int _selectedCitizenInspectionIndex;
        private int _selectedStructureInspectionIndex;
        private bool _hasPerformanceStartupOverride;
        private string? _performanceScenarioIdOverride;
        private int _performanceSimulationSeedOverride;
        private int _performanceCitizenCountOverride;
        private PrototypeOrderSelectionMode _performanceOrderSelectionModeOverride = PrototypeOrderSelectionMode.ExactBranchAndBound;
        private PrototypeExtractionPlanningMode _performanceExtractionPlanningModeOverride = PrototypeExtractionPlanningMode.ExactBounded;
        private PrototypeRouteDistanceMode _performanceRouteDistanceModeOverride = PrototypeRouteDistanceMode.CachedDistanceOnly;
        private bool _readyStarted;
        private bool _visualCaptureConfigured;
        private string _selectedVisualCapturePresetId = string.Empty;

        public bool IsGameRunning { get; private set; }

        public int SimulationSeed => _runtimeSession?.SimulationSeed ?? _simulationSeed;

        public long SimulationTick => _runtimeSession?.SimulationTick ?? 0;

        public int CitizenCount => _runtimeSession?.Workers.Count ?? _initialWorkers;

        public PrototypeOrderSelectionMode CurrentOrderSelectionMode =>
            _runtimeSession?.OrderSelectionMode ?? PrototypeOrderSelectionMode.ExactBranchAndBound;

        public PrototypeExtractionPlanningMode CurrentExtractionPlanningMode =>
            _runtimeSession?.ExtractionPlanningMode ?? PrototypeExtractionPlanningMode.ExactBounded;

        public PrototypeRouteDistanceMode CurrentRouteDistanceMode =>
            _runtimeSession?.RouteDistanceMode ?? PrototypeRouteDistanceMode.CachedDistanceOnly;

        public long CachedRouteDistanceFastPathHits =>
            _runtimeSession?.CachedRouteDistanceFastPathHits ?? 0;

        public PrototypeSettlementDirective CurrentDirective =>
            _runtimeSession?.ActiveDirective ?? PrototypeSettlementDirective.Neutral;

        public int CivicCognitionDecisionCount => _runtimeSession?.EventLog.Entries.Count(entry =>
            entry.EventType == PrototypeEventTypes.CivicCognitionDecision) ?? 0;

        public int RuntimeEventCount => _runtimeSession?.EventLog.Entries.Count ?? 0;

        public double? PerformanceBootstrapMilliseconds { get; private set; }

        public InventoryComponent Inventory => _runtimeSession?.Inventory ?? _fallbackInventory;

        public InventoryComponent Stockpile => _runtimeSession?.Stockpile ?? _fallbackInventory;

        public Vector3 CentralDepotPosition => _runtimeSession?.CentralDepotPosition ?? Vector3.Zero;

        public CameraMode CurrentCameraMode => _cameraMode;

        public TerrainOverlayMode CurrentOverlayMode => _overlayMode;

        public string CurrentScenarioId => _scenario?.Id ?? _scenarioId;

        public int CurrentWorldSeed => _runtimeSession?.WorldSeed ?? 0;

        public bool UsesVoxelWorld => _runtimeSession?.UsesVoxelWorld == true;

        public long VoxelWorldRevision => _runtimeSession?.VoxelWorldRevision ?? 0;

        public bool IsWorldcraftBuildMode => _worldcraftBuildMode;

        public string SelectedWorldcraftPieceId => _selectedWorldcraftPieceId;

        public bool IsVoxelInventoryOpen => _voxelInventoryOpen;

        public RuntimeMetricsCollector? RuntimeMetrics => _runtimeMetrics;

        public bool IsVisualCaptureConfigured => _visualCaptureConfigured;

        public IEnumerable<string> VisualCapturePresetIds => PrototypeVisualCaptureConfiguration.PresetIds;

        public PrototypeVisualCaptureMetadata VisualCaptureMetadata => new(
            CurrentScenarioId,
            SimulationSeed,
            SimulationTick,
            PrototypeVisualCaptureConfiguration.LightingHour,
            PrototypeVisualCaptureConfiguration.LightingMultiplier,
            _selectedVisualCapturePresetId,
            _runtimeSession?.Crisis?.IsTerminal == true,
            _runtimeSession?.CurrentHour ?? PrototypeVisualCaptureConfiguration.LightingHour);

        public PrototypeVisualCapturePoseMetadata VisualCapturePoseMetadata
        {
            get
            {
                Camera3D? camera = _cameraMode == CameraMode.Player
                    ? _player?.GetNodeOrNull<Camera3D>("CameraPivot/Camera3D")
                    : _observerRig?.GetNodeOrNull<Camera3D>("Camera3D");
                return new PrototypeVisualCapturePoseMetadata(
                    _cameraMode.ToString(),
                    camera?.GlobalPosition ?? Vector3.Zero,
                    camera?.GlobalRotation ?? Vector3.Zero,
                    _player?.GlobalPosition ?? Vector3.Zero,
                    _player?.GlobalRotation ?? Vector3.Zero);
            }
        }

        public string SelectedVisualCaptureCitizenId => GetSelectedCitizen()?.WorkerId ?? string.Empty;

        public override void _Ready()
        {
            _readyStarted = true;
            Instance = this;

            EnsureSceneStructure();
            LoadCatalogs();
            ConfigureLocalSession();
            EnsureWorldShell();
            StartNewPrototypeRun(resetPlayerPosition: true);

            if (_autoStartSinglePlayer)
            {
                RecordEvent(PrototypeEventTypes.SessionStarted, "Started local prototype session");
            }

            RecordEvent(PrototypeEventTypes.RuntimeReady, "Societies Prototype V2 M3 initialized");
            UpdateHud();
            GD.Print("Societies Prototype V2 M3 initialized");
        }

        public override void _Process(double delta)
        {
            // Canonical visual capture settles rendered frames between images. Those frames must
            // never advance simulation state; AdvanceVisualCaptureToTick is the sole tick path.
            if (!IsGameRunning || _visualCaptureConfigured)
            {
                return;
            }

            _backlogWarningCooldownSeconds = Math.Max(0.0, _backlogWarningCooldownSeconds - delta);
            int ticksToProcess = _fixedStepAccumulator.Consume(delta);
            int ticksAttempted = 0;
            try
            {
                RunTickBatch(ticksToProcess, RuntimeMetricsBatchKind.RenderedFrame, ref ticksAttempted);
            }
            catch
            {
                // Attempted ticks keep their interval. Work that never started returns to the
                // backlog for a future rendered frame.
                int unattemptedTicks = ticksToProcess - ticksAttempted;
                _fixedStepAccumulator.RestoreUnprocessedTicks(unattemptedTicks);
                throw;
            }

            if (_fixedStepAccumulator.HasBacklog && _backlogWarningCooldownSeconds <= 0.0)
            {
                GD.PushWarning(
                    $"Simulation backlog: {_fixedStepAccumulator.PendingWholeTicks} ticks remain after the {MaxTicksPerFrame}-tick frame cap.");
                _backlogWarningCooldownSeconds = BacklogWarningCooldownSeconds;
            }

            UpdateWorldcraftPreview();

        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && !mouseButton.DoubleClick &&
                TryHandleVoxelPointerInput(mouseButton))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (@event.IsActionPressed("toggle_inventory"))
            {
                if (_runtimeSession?.UsesVoxelWorld == true) SetVoxelInventoryOpen(!_voxelInventoryOpen);
                else _hud?.ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_runtimeSession?.UsesVoxelWorld == true)
            {
                if (_voxelInventoryOpen)
                {
                    if (keyEvent.Keycode == Key.Escape)
                    {
                        SetVoxelInventoryOpen(false);
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }
                switch (keyEvent.Keycode)
                {
                    case Key.B:
                        _worldcraftBuildMode = !_worldcraftBuildMode;
                        if (!_worldcraftBuildMode)
                        {
                            _worldcraftPresenter?.HideGhost();
                            _hud?.SetVoxelPlacementEvaluation(null, false);
                        }
                        _hud?.SetStatusText(_worldcraftBuildMode ? "Build mode" : "Gather mode");
                        UpdateHud(); GetViewport().SetInputAsHandled(); return;
                    case Key.R:
                        WorldcraftPieceDefinition? selected = VoxelWorldcraftCatalog.FindPiece(_selectedWorldcraftPieceId);
                        if (_worldcraftBuildMode && selected?.Rotates == true) _worldcraftRotation = (_worldcraftRotation + 1) % 4;
                        UpdateHud(); GetViewport().SetInputAsHandled(); return;
                    case Key.Key1:
                        _ = SelectWorldcraftPiece("wood_floor"); GetViewport().SetInputAsHandled(); return;
                    case Key.Key2:
                        _ = SelectWorldcraftPiece("wood_wall"); GetViewport().SetInputAsHandled(); return;
                    case Key.Key3:
                        _ = SelectWorldcraftPiece("wood_post"); GetViewport().SetInputAsHandled(); return;
                    case Key.X:
                        _ = TryDismantleTargetedWorldcraftPiece(); GetViewport().SetInputAsHandled(); return;
                    case Key.Key4:
                    case Key.Key5:
                    case Key.Key6:
                    case Key.F3:
                    case Key.F4:
                    case Key.F5:
                    case Key.F10:
                    case Key.F11:
                    case Key.F12:
                        GetViewport().SetInputAsHandled(); return;
                }
            }

            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    TryCraftRecipe("stone_axe");
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key2:
                    SelectDirective(PrototypeSettlementDirective.FoodAndFuel);
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key3:
                    SelectDirective(PrototypeSettlementDirective.Shelter);
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key4:
                    SelectCivicPolicy(PrototypeCivicPolicy.ProtectWetland);
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key5:
                    SelectCivicPolicy(PrototypeCivicPolicy.DrawDownWetland);
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key6:
                    ResolveInspectedCitizenOfflineCognition();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F3:
                    SelectNextInspectedCitizen();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F4:
                    SelectNextInspectedStructure();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F5:
                    ToggleWeatherState();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F6:
                    SaveSnapshotToDisk();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F7:
                    ResetPrototypeRun();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F8:
                    ToggleCameraMode();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F9:
                    LoadLatestSnapshotFromDisk();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F10:
                    CycleOverlayMode();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F11:
                    SelectNextBuildQueueEntry();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F12:
                    ToggleSelectedBuildQueuePause();
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        public void StepSimulationTicks(int tickCount)
        {
            if (_visualCaptureConfigured)
            {
                throw new InvalidOperationException(
                    "Visual capture ticks must be advanced through AdvanceVisualCaptureToTick.");
            }

            int ticksAttempted = 0;
            RunTickBatch(Math.Max(0, tickCount), RuntimeMetricsBatchKind.ManualStep, ref ticksAttempted);
        }

        /// <summary>Sets the immutable W2 capture scenario before this node enters the scene tree.</summary>
        public void ConfigureVisualCaptureStartup()
        {
            if (_readyStarted || IsInsideTree())
            {
                throw new InvalidOperationException("Visual capture startup must be configured before the game manager enters the scene tree.");
            }

            _scenarioId = PrototypeVisualCaptureConfiguration.ScenarioId;
            _simulationSeed = PrototypeVisualCaptureConfiguration.SimulationSeed;
            _visualCaptureConfigured = true;
        }

        /// <summary>Sets a catalog scenario before this node enters the scene tree.</summary>
        public void ConfigureScenarioStartup(string scenarioId)
        {
            if (_readyStarted)
            {
                throw new InvalidOperationException("Scenario startup must be configured before the game manager enters the scene tree.");
            }
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
            }

            _scenarioId = scenarioId;
        }

        /// <summary>Resets a ready manager to the catalog-owned canonical capture scenario.</summary>
        public bool ApplyVisualCaptureScenario()
        {
            if (!_readyStarted || _runtimeSession == null || _environmentController == null)
            {
                return false;
            }

            _visualCaptureConfigured = true;
            _environmentController.StartHour = PrototypeVisualCaptureConfiguration.LightingHour;
            SetScenario(PrototypeVisualCaptureConfiguration.ScenarioId);
            ApplyVisualCaptureLighting();
            return SelectVisualCapturePreset("arrival");
        }

        /// <summary>Advances only through authoritative simulation ticks; capture lighting remains presentation-only.</summary>
        public bool AdvanceVisualCaptureToTick(long targetTick)
        {
            if (!_visualCaptureConfigured || _runtimeSession == null || targetTick < SimulationTick)
            {
                return false;
            }

            while (SimulationTick < targetTick)
            {
                long remaining = targetTick - SimulationTick;
                int ticksAttempted = 0;
                RunTickBatch((int)Math.Min(remaining, 512), RuntimeMetricsBatchKind.ManualStep, ref ticksAttempted);
            }

            ApplyVisualCaptureLighting();
            return true;
        }

        public bool SelectVisualCapturePreset(string presetId)
        {
            if (!_visualCaptureConfigured || _runtimeSession == null ||
                !PrototypeVisualCaptureConfiguration.TryGetPreset(presetId, out PrototypeVisualCapturePreset preset))
            {
                return false;
            }

            Vector3 focus = _runtimeSession.SettlementAnchorPosition;
            if (preset.Id == "citizen_inspection")
            {
                focus = GetSelectedCitizen()?.Position ?? focus;
            }

            Vector3 cameraPosition = focus + preset.CameraOffset;
            Vector3 lookAt = focus + preset.LookAtOffset;
            _cameraMode = preset.CameraKind == PrototypeVisualCaptureCameraKind.Player
                ? CameraMode.Player
                : CameraMode.Observer;
            BindPlayerToRuntime();
            bool applied = preset.CameraKind == PrototypeVisualCaptureCameraKind.Player
                ? _player?.ApplyCaptureCameraPose(cameraPosition, lookAt, preset.FieldOfView) == true
                : _observerRig?.ApplyCapturePose(cameraPosition, lookAt, preset.FieldOfView) == true;
            if (applied)
            {
                _selectedVisualCapturePresetId = preset.Id;
            }

            return applied;
        }

        /// <summary>Selects the same stable worker that the capture camera will inspect.</summary>
        public bool SelectVisualCaptureInspectionCitizen()
        {
            if (!_visualCaptureConfigured || _runtimeSession == null || _runtimeSession.Workers.Count == 0)
            {
                return false;
            }

            _selectedCitizenInspectionIndex = _runtimeSession.Workers
                .Select((worker, index) => (worker, index))
                .OrderBy(candidate => candidate.worker.WorkerId, StringComparer.Ordinal)
                .First()
                .index;
            _hud?.SetStatusText($"Inspecting {_runtimeSession.Workers[_selectedCitizenInspectionIndex].DisplayName}");
            UpdateHud();
            return true;
        }

        /// <summary>Places the visual-capture player body in deterministic central-depot interaction range.</summary>
        public bool PositionVisualCapturePlayerAtDepot()
        {
            if (!_visualCaptureConfigured || _runtimeSession == null || _player == null)
            {
                return false;
            }

            _player.GlobalPosition = CentralDepotPosition;
            return _player.GlobalPosition.DistanceTo(CentralDepotPosition) <= _player.ContributionRangeMeters;
        }

        /// <summary>Exercises the canonical player's contribution input path with a fixed capture input frame.</summary>
        public bool SubmitVisualCaptureContribution()
        {
            if (!_visualCaptureConfigured || _runtimeSession == null || _player == null ||
                !PositionVisualCapturePlayerAtDepot())
            {
                return false;
            }

            int initialStockpileLogs = Stockpile.GetCount("logs");
            Inventory.AddItem("logs", PrototypeVisualCaptureConfiguration.ContributionLogQuantity);
            _player.ProcessInteractionInput(PrototypeVisualCaptureConfiguration.ContributionInputFrame);
            return Inventory.GetCount("logs") == 0 &&
                Stockpile.GetCount("logs") == initialStockpileLogs + PrototypeVisualCaptureConfiguration.ContributionLogQuantity &&
                _hud?.StatusText.Contains("Contributed", StringComparison.Ordinal) == true;
        }

        public PrototypeDirectiveChangeResult SelectDirective(PrototypeSettlementDirective directive)
        {
            if (_runtimeSession == null)
            {
                return new PrototypeDirectiveChangeResult(
                    PrototypeSettlementDirective.Neutral,
                    PrototypeSettlementDirective.Neutral,
                    false,
                    false,
                    "runtime_unavailable");
            }

            PrototypeDirectiveChangeResult result = _runtimeSession.SetDirective(directive);
            if (result.Succeeded)
            {
                string displayName = PrototypeSettlementDirectiveCatalog.GetDisplayName(result.CurrentDirective);
                NotifyStatus(result.Changed ? $"Directive set: {displayName}" : $"Directive already set: {displayName}");
            }

            return result;
        }

        /// <summary>
        /// Routes a player civic choice through the runtime's single authoritative policy command.
        /// Presentation owns neither civic state nor wetland consequences.
        /// </summary>
        public PrototypeCivicPolicyCommandResult SelectCivicPolicy(PrototypeCivicPolicy policy)
        {
            if (_runtimeSession == null)
            {
                return new PrototypeCivicPolicyCommandResult(
                    false,
                    "runtime_unavailable",
                    new PrototypeCivicPolicySnapshot());
            }

            PrototypeCivicPolicySnapshot current = _runtimeSession.CivicPolicy;
            PrototypeCivicPolicyCommandResult result = _runtimeSession.SelectCivicPolicy(new(
                policy,
                current.Version,
                _runtimeSession.SimulationTick));
            if (result.Succeeded)
            {
                string displayName = policy == PrototypeCivicPolicy.ProtectWetland ? "Protect" : "Drawdown";
                NotifyStatus($"Civic policy selected: {displayName}");
            }
            else
            {
                NotifyStatus($"Civic policy rejected: {result.FailureReason}");
            }

            return result;
        }

        /// <summary>
        /// Applies the existing offline cognition fallback once for the inspected citizen. It
        /// publishes no provider request and exposes only the existing bounded event outcome.
        /// </summary>
        public bool ResolveInspectedCitizenOfflineCognition()
        {
            PrototypeWorkerState? selectedCitizen = GetSelectedCitizen();
            if (_runtimeSession == null || selectedCitizen == null)
            {
                NotifyStatus("Civic cognition rejected: no inspected citizen");
                return false;
            }

            // The event history is restored with schema-v9 artifacts, so it is the
            // authoritative one-author-action guard across uninterrupted and resumed runs.
            if (CivicCognitionDecisionCount > 0)
            {
                NotifyStatus("Civic cognition rejected: already applied");
                return false;
            }

            try
            {
                PrototypeCognitionObservation observation = _civicCognitionModule.PublishObservation(
                    _runtimeSession,
                    selectedCitizen.WorkerId);
                PrototypeCognitionResolution resolution = _civicCognitionModule.Resolve(
                    _runtimeSession,
                    observation,
                    PrototypeCognitionEvidence.Unavailable());
                if (!resolution.Accepted ||
                    resolution.Source != PrototypeCognitionDecisionSource.DeterministicFallback ||
                    !_civicCognitionModule.Apply(_runtimeSession, resolution))
                {
                    NotifyStatus($"Civic cognition rejected: {resolution.ErrorCode}");
                    return false;
                }

                NotifyStatus("Civic cognition: deterministic_fallback | civic.cognition.decision");
                return true;
            }
            catch (PrototypeCognitionException exception)
            {
                NotifyStatus($"Civic cognition rejected: {exception.Code}");
                return false;
            }
        }

        internal PrototypePerformanceProbeSnapshot CapturePerformanceProbeState()
        {
            return _runtimeSession?.CapturePerformanceProbeState()
                ?? throw new InvalidOperationException("The runtime session is unavailable.");
        }

        internal int ClearDerivedPathCacheForPerformance()
        {
            return _runtimeSession?.ClearDerivedPathCacheForPerformance()
                ?? throw new InvalidOperationException("The runtime session is unavailable.");
        }

        internal bool TryPrepareForcedPathCompletionForPerformance(out string structureId)
        {
            if (_runtimeSession == null)
            {
                structureId = string.Empty;
                return false;
            }

            return _runtimeSession.TryPrepareForcedPathCompletionForPerformance(out structureId);
        }

        internal void ConfigurePerformanceStartup(
            string scenarioId,
            int simulationSeed,
            int citizenCount,
            string selectorMode = ExactBranchAndBoundSelectorMode,
            string extractionPlanningMode = ExactBoundedExtractionMode,
            string routeDistanceMode = CachedDistanceOnlyRouteDistanceMode)
        {
            if (_readyStarted || IsInsideTree())
            {
                throw new InvalidOperationException("Performance startup must be configured before the game manager first enters the scene tree.");
            }

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("A scenario id is required.", nameof(scenarioId));
            }

            if (citizenCount is < 1 or > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(citizenCount), citizenCount, "Citizen count must be between 1 and 256.");
            }

            PrototypeOrderSelectionMode orderSelectionMode = selectorMode switch
            {
                ExactBranchAndBoundSelectorMode => PrototypeOrderSelectionMode.ExactBranchAndBound,
                ExhaustiveReferenceSelectorMode => PrototypeOrderSelectionMode.ExhaustiveReference,
                _ => throw new ArgumentException(
                    "Selector mode must be 'exact_branch_and_bound' or 'exhaustive_reference'.",
                    nameof(selectorMode))
            };
            PrototypeExtractionPlanningMode resolvedExtractionPlanningMode = extractionPlanningMode switch
            {
                ExactBoundedExtractionMode => PrototypeExtractionPlanningMode.ExactBounded,
                ExhaustiveReferenceExtractionMode => PrototypeExtractionPlanningMode.ExhaustiveReference,
                _ => throw new ArgumentException(
                    "Extraction planning mode must be 'exact_bounded' or 'exhaustive_reference'.",
                    nameof(extractionPlanningMode))
            };
            PrototypeRouteDistanceMode resolvedRouteDistanceMode = routeDistanceMode switch
            {
                CachedDistanceOnlyRouteDistanceMode => PrototypeRouteDistanceMode.CachedDistanceOnly,
                FullMaterializationReferenceRouteDistanceMode => PrototypeRouteDistanceMode.FullMaterializationReference,
                _ => throw new ArgumentException(
                    "Route-distance mode must be 'cached_distance_only' or 'full_materialization_reference'.",
                    nameof(routeDistanceMode))
            };

            _hasPerformanceStartupOverride = true;
            _performanceScenarioIdOverride = scenarioId;
            _performanceSimulationSeedOverride = simulationSeed;
            _performanceCitizenCountOverride = citizenCount;
            _performanceOrderSelectionModeOverride = orderSelectionMode;
            _performanceExtractionPlanningModeOverride = resolvedExtractionPlanningMode;
            _performanceRouteDistanceModeOverride = resolvedRouteDistanceMode;
        }

        public bool TryCraftRecipe(string recipeId)
        {
            if (_runtimeSession == null)
            {
                return false;
            }

            bool crafted = _runtimeSession.TryCraftRecipe(recipeId, out string statusText);
            _hud?.SetStatusText(statusText);
            UpdateHud();
            return crafted;
        }

        public bool SelectNextBuildQueueEntry()
        {
            if (_runtimeSession == null || !_runtimeSession.SelectNextBuildQueueEntry())
            {
                return false;
            }

            _hud?.SetStatusText(_runtimeSession.SelectedBuildQueueStatusText);
            UpdateHud();
            return true;
        }

        public bool ToggleSelectedBuildQueuePause()
        {
            if (_runtimeSession == null || !_runtimeSession.ToggleSelectedBuildQueuePause())
            {
                return false;
            }

            _hud?.SetStatusText(_runtimeSession.SelectedBuildQueueStatusText);
            UpdateHud();
            return true;
        }

        public bool SelectNextInspectedCitizen()
        {
            if (_runtimeSession == null || _runtimeSession.Workers.Count == 0)
            {
                return false;
            }

            _selectedCitizenInspectionIndex = (_selectedCitizenInspectionIndex + 1) % _runtimeSession.Workers.Count;
            _hud?.SetStatusText($"Inspecting {_runtimeSession.Workers[_selectedCitizenInspectionIndex].DisplayName}");
            UpdateHud();
            return true;
        }

        public bool SelectNextInspectedStructure()
        {
            if (_runtimeSession == null || _runtimeSession.Structures.Count == 0)
            {
                return false;
            }

            _selectedStructureInspectionIndex = (_selectedStructureInspectionIndex + 1) % _runtimeSession.Structures.Count;
            _hud?.SetStatusText($"Inspecting {_runtimeSession.Structures[_selectedStructureInspectionIndex].DisplayName}");
            UpdateHud();
            return true;
        }

        public void ResetPrototypeRun()
        {
            ResetVoxelInteractionState();
            StartNewPrototypeRun(resetPlayerPosition: true);
            RecordEvent(PrototypeEventTypes.RuntimeReset, $"Reset prototype run with seed {SimulationSeed}");
            _hud?.SetStatusText("Prototype run reset");
            UpdateHud();
        }

        public void SetScenario(string scenarioId, bool restart = true)
        {
            ResetVoxelInteractionState();
            PrototypeScenarioDefinition scenario = ResolveScenarioDefinition(scenarioId);
            _scenario = scenario;
            ApplyScenarioDefaults(scenario);

            if (restart)
            {
                StartNewPrototypeRun(resetPlayerPosition: true);
                RecordEvent(PrototypeEventTypes.WorldSeeded, $"Scenario switched to {scenario.Id}");
                _hud?.SetStatusText($"Scenario set to {scenario.DisplayName}");
                UpdateHud();
            }
        }

        public void ToggleWeatherState()
        {
            if (_runtimeSession == null)
            {
                return;
            }

            string statusText = _runtimeSession.ToggleWeatherState();
            ApplyRuntimeStateToScene();
            _hud?.SetStatusText(statusText);
            UpdateHud();
        }

        public PrototypeRuntimeSnapshot CaptureSnapshot()
        {
            if (_runtimeSession == null || _scenePresenter == null)
            {
                return new PrototypeRuntimeSnapshot();
            }

            return _runtimeSession.CaptureSnapshot(_player?.Position ?? Vector3.Zero);
        }

        public string SaveSnapshotToDisk()
        {
            if (_runtimeSession == null || _artifactManager == null || _scenePresenter == null)
            {
                return string.Empty;
            }

            if (!_runtimeSession.SupportsRuntimeSnapshotPersistence)
            {
                string statusText = _runtimeSession.RuntimeSnapshotPersistenceDeferralMessage;
                _hud?.SetStatusText(statusText);
                return string.Empty;
            }

            PrototypeRuntimeSnapshot snapshot = CaptureSnapshot();
            CaptureMetricsSnapshot();
            PrototypeWorldSummary worldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, _runtimeSession.ActiveResourceSnapshots);
            string snapshotPath = _artifactManager.SaveArtifacts(_runtimeSession, snapshot, worldSummary, _runtimeMetrics);
            _lastWorldSummary = worldSummary;
            RecordEvent(PrototypeEventTypes.SnapshotSaved, $"Saved snapshot to {Path.GetFileName(snapshotPath)}");
            _hud?.SetStatusText($"Saved snapshot to {Path.GetFileName(snapshotPath)}");
            return snapshotPath;
        }

        public bool LoadLatestSnapshotFromDisk()
        {
            _artifactManager ??= new PrototypeRunArtifactManager();
            PrototypeLoadedArtifacts? loadedArtifacts = _artifactManager.LoadLatestArtifacts();
            if (loadedArtifacts == null)
            {
                return false;
            }
            ApplyLoadedArtifacts(loadedArtifacts.Value, Path.GetFileName(_artifactManager.GetArtifactPaths().LegacySnapshotPath));
            return true;
        }

        internal void ApplyLoadedArtifactsForTest(PrototypeLoadedArtifacts artifacts) => ApplyLoadedArtifacts(artifacts, "test-snapshot.json");

        private void ApplyLoadedArtifacts(PrototypeLoadedArtifacts artifacts, string sourceFileName)
        {
            PrototypeScenarioDefinition scenario = ResolveScenarioDefinition(artifacts.Snapshot.ScenarioId);
            PrototypeRuntimeSession candidateSession = BuildRuntimeSession(scenario);
            candidateSession.ApplySnapshot(artifacts.Snapshot);
            candidateSession.RestoreArtifacts(artifacts.EventLog, artifacts.RunSummary);
            EnsureWorldShell();
            ResetVoxelInteractionState();
            _scenario = scenario;
            ApplyScenarioDefaults(scenario);
            _runtimeSession = candidateSession;
            ResetCivicCognitionAction();
            ResetFrameScheduler();

            _scenePresenter?.ResetDynamicNodes();
            ApplyWorldToScene();
            ApplyRuntimeStateToScene();
            _scenePresenter?.SyncWorkers(_runtimeSession.Workers);
            UpdateSettlementPresentationFromSession();

            if (_player != null)
            {
                _player.Velocity = Vector3.Zero;
                _player.Position = _runtimeSession.ResolvePlayerPositionAfterSnapshot(
                    artifacts.Snapshot.PlayerPosition.ToVector3(), _player.GetGroundingFootOffset());
            }

            BindPlayerToRuntime();
            CaptureMetricsSnapshot();
            _lastWorldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, _runtimeSession.ActiveResourceSnapshots);
            RecordEvent(PrototypeEventTypes.SnapshotLoaded, $"Loaded snapshot from {sourceFileName}");
            NotifyStatus($"Loaded snapshot from {sourceFileName}");
        }

        private void EnsureSceneStructure()
        {
            _networkManager = GetNodeOrNull<NetworkManager>("NetworkManager");
            if (_networkManager == null)
            {
                _networkManager = new NetworkManager { Name = "NetworkManager" };
                AddChild(_networkManager);
            }

            _entityManager = GetNodeOrNull<EntityManager>("EntityManager");
            if (_entityManager == null)
            {
                _entityManager = new EntityManager { Name = "EntityManager" };
                AddChild(_entityManager);
            }

            _worldRoot = GetOrCreateChild<Node3D>(this, "World");
            _playersRoot = GetOrCreateChild<Node3D>(_worldRoot, "Players");
            _agentsRoot = GetOrCreateChild<Node3D>(_worldRoot, "Agents");
            _entitiesRoot = GetOrCreateChild<Node3D>(_worldRoot, "Entities");
            _environmentRoot = GetOrCreateChild<Node3D>(_worldRoot, "Environment");
            _systemsRoot = GetOrCreateChild<Node>(_worldRoot, "Systems");
            _hud = GetOrCreateChild<PrototypeHud>(this, "UI");
        }

        private void LoadCatalogs()
        {
            const string resourceDirectory = "res://data";
            string dataDirectory = ProjectSettings.GlobalizePath("res://data");
            string catalogLocation = dataDirectory;

            try
            {
                if (OS.HasFeature("editor") && Directory.Exists(dataDirectory))
                {
                    _catalogs = PrototypeCatalogLoader.LoadFromDirectory(dataDirectory);
                }
                else
                {
                    catalogLocation = resourceDirectory;
                    _catalogs = PrototypeCatalogLoader.LoadFromJsonTextProvider(fileName =>
                        ReadProjectResourceText($"{resourceDirectory}/{fileName}"));
                }
            }
            catch (Exception ex)
            {
                if (_hasPerformanceStartupOverride)
                {
                    throw new InvalidOperationException(
                        $"Performance startup requires the validated catalog at '{catalogLocation}'.",
                        ex);
                }

                GD.PushWarning($"Failed to load prototype catalogs from {catalogLocation}: {ex.Message}. Falling back to built-in legacy defaults.");
                _catalogs = CreateFallbackCatalogBundle();
            }

            if (!_hasPerformanceStartupOverride)
            {
                _scenario = ResolveScenarioDefinition(_scenarioId);
                ApplyScenarioDefaults(_scenario);
                return;
            }

            PrototypeScenarioDefinition requestedScenario = _catalogs!.Scenarios.Resolve(_performanceScenarioIdOverride!);
            PrototypeScenarioDefinition configuredScenario = JsonSerializer.Deserialize<PrototypeScenarioDefinition>(
                JsonSerializer.Serialize(requestedScenario))
                ?? throw new InvalidOperationException($"Failed to clone performance scenario '{requestedScenario.Id}'.");
            configuredScenario.SimulationSeed = _performanceSimulationSeedOverride;
            configuredScenario.InitialCitizens = _performanceCitizenCountOverride;

            _scenario = configuredScenario;
            ApplyScenarioDefaults(configuredScenario);
        }

        private static string ReadProjectResourceText(string resourcePath)
        {
            using Godot.FileAccess? file = Godot.FileAccess.Open(resourcePath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Error openError = Godot.FileAccess.GetOpenError();
                throw new FileNotFoundException(
                    $"Missing prototype catalog resource '{resourcePath}' (Godot error {openError}).",
                    resourcePath);
            }

            return file.GetAsText();
        }

        private void ConfigureLocalSession()
        {
            if (_autoStartSinglePlayer)
            {
                _networkManager?.StartLocalSession();
                IsGameRunning = true;
            }
        }

        private void EnsureWorldShell()
        {
            if (_playersRoot == null || _agentsRoot == null || _entitiesRoot == null || _environmentRoot == null || _systemsRoot == null)
            {
                return;
            }

            _artifactManager ??= new PrototypeRunArtifactManager();

            _terrain = _systemsRoot.GetNodeOrNull<TerrainGenerator>("Terrain");
            if (_terrain == null)
            {
                _terrain = new TerrainGenerator { Name = "Terrain" };
                _systemsRoot.AddChild(_terrain);
            }

            if (_scenario != null)
            {
                _terrain.WorldSize = _scenario.WorldSize;
            }

            _terrain.RebuildTerrain();

            _environmentController = _environmentRoot.GetNodeOrNull<EnvironmentController>("Environment");
            if (_environmentController == null)
            {
                _environmentController = new EnvironmentController { Name = "Environment" };
                _environmentRoot.AddChild(_environmentController);
            }

            if (_visualCaptureConfigured)
            {
                _environmentController.StartHour = PrototypeVisualCaptureConfiguration.LightingHour;
            }

            _scenePresenter = new PrototypeSettlementScenePresenter(
                _agentsRoot,
                _entitiesRoot,
                _environmentRoot,
                _terrain);
            if (_scenario?.WorldModel != PrototypeWorldModels.Voxel)
            {
                _scenePresenter.EnsureSettlementHub();
            }

            _voxelPresenter = _worldRoot?.GetNodeOrNull<VoxelWorldPresenter>("VoxelWorldPresenter");
            if (_scenario?.WorldModel == PrototypeWorldModels.Voxel && _voxelPresenter == null)
            {
                _voxelPresenter = new VoxelWorldPresenter { Name = "VoxelWorldPresenter" };
                _worldRoot!.AddChild(_voxelPresenter);
            }

            _player = _playersRoot.GetNodeOrNull<PlayerCharacter>("LocalPlayer");
            if (_player == null || !IsInstanceValid(_player))
            {
                _player = new PlayerCharacter
                {
                    Name = "LocalPlayer"
                };
                _playersRoot.AddChild(_player);
                _player.Position = _terrain.GetPlayerSpawnPoint();
            }

            _player.HarvestRequested -= OnPlayerHarvestRequested;
            _player.HarvestRequested += OnPlayerHarvestRequested;
            _player.ContributionRequested -= OnPlayerContributionRequested;
            _player.ContributionRequested += OnPlayerContributionRequested;
            _player.Terrain = _terrain;
            _player.SetControlEnabled(_cameraMode == CameraMode.Player);

            _observerRig = _playersRoot.GetNodeOrNull<ObserverCameraRig>("ObserverCamera");
            if (_observerRig == null || !IsInstanceValid(_observerRig))
            {
                _observerRig = new ObserverCameraRig
                {
                    Name = "ObserverCamera"
                };
                _playersRoot.AddChild(_observerRig);
            }

            _observerRig.SetControlEnabled(_cameraMode == CameraMode.Observer);

            if (_hud != null)
            {
                _hud.VoxelPieceRequested -= OnVoxelPieceRequested;
                _hud.VoxelPieceRequested += OnVoxelPieceRequested;
                _hud.VoxelFieldPackShortcutRequested -= OnVoxelFieldPackShortcutRequested;
                _hud.VoxelFieldPackShortcutRequested += OnVoxelFieldPackShortcutRequested;
                PrototypeHudPresenter.Initialize(_hud);
            }
        }

        private void StartNewPrototypeRun(bool resetPlayerPosition)
        {
            ResetVoxelInteractionState();
            EnsureWorldShell();

            if (_environmentController == null || _scenePresenter == null || _scenario == null)
            {
                return;
            }

            CreateRuntimeSession(_scenario);
            long performanceBootstrapStartTimestamp = _hasPerformanceStartupOverride
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0;
            _runtimeSession!.Initialize(_environmentController.StartHour);
            ResetCivicCognitionAction();
            ResetFrameScheduler();

            _scenePresenter.ResetDynamicNodes();
            ApplyWorldToScene();
            _lastWorldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, _runtimeSession.ActiveResourceSnapshots);

            RecordEvent(PrototypeEventTypes.WorldSeeded, $"Spawned world for scenario {_runtimeSession.Scenario.Id} using world seed {_runtimeSession.WorldSeed}");

            if (resetPlayerPosition && _player != null)
            {
                _player.ResetForPrototypeRun(BuildPlayerSpawnPoint());
            }

            BindPlayerToRuntime();
            ApplyRuntimeStateToScene();
            ApplyVisualCaptureLighting();
            _scenePresenter.SyncWorkers(_runtimeSession.Workers);
            UpdateSettlementPresentationFromSession();
            CaptureMetricsSnapshot();
            PerformanceBootstrapMilliseconds = _hasPerformanceStartupOverride
                ? System.Diagnostics.Stopwatch.GetElapsedTime(performanceBootstrapStartTimestamp).TotalMilliseconds
                : null;

            _selectedCitizenInspectionIndex = 0;
            _selectedStructureInspectionIndex = 0;
            NotifyStatus("Prototype V2 M3 ready");
        }

        private void ResetFrameScheduler()
        {
            _fixedStepAccumulator.Reset();
            _backlogWarningCooldownSeconds = 0.0;
            _runtimeMetrics?.Reset();
            _contributionInteraction.Reset();
        }

        private void CreateRuntimeSession(PrototypeScenarioDefinition scenario)
        {
            _scenario = scenario;
            ApplyScenarioDefaults(scenario);

            if (_terrain != null)
            {
                _terrain.WorldSize = scenario.WorldSize;
                _scenePresenter?.UpdateTerrain(_terrain);
            }

            _runtimeSession = BuildRuntimeSession(scenario);
            BindPlayerToRuntime();
        }

        private PrototypeRuntimeSession BuildRuntimeSession(PrototypeScenarioDefinition scenario)
        {
            PrototypeOrderSelectionMode orderSelectionMode = _hasPerformanceStartupOverride
                ? _performanceOrderSelectionModeOverride
                : PrototypeOrderSelectionMode.ExactBranchAndBound;
            PrototypeExtractionPlanningMode extractionPlanningMode = _hasPerformanceStartupOverride
                ? _performanceExtractionPlanningModeOverride
                : PrototypeExtractionPlanningMode.ExactBounded;
            PrototypeRouteDistanceMode routeDistanceMode = _hasPerformanceStartupOverride
                ? _performanceRouteDistanceModeOverride
                : PrototypeRouteDistanceMode.CachedDistanceOnly;
            return new PrototypeRuntimeSession(
                scenario,
                _catalogs?.RoleQuotas.Roles,
                orderSelectionMode,
                extractionPlanningMode,
                routeDistanceMode,
                _catalogs?.Resources.Resources);
        }

        private void BindPlayerToRuntime()
        {
            if (_player != null)
            {
                _player.Terrain = _runtimeSession?.UsesVoxelWorld == true ? null : _terrain;
                _player.ContributionDepotPosition = CentralDepotPosition;
                _player.SetFirstPersonBodyHidden(_runtimeSession?.UsesVoxelWorld == true);
                _player.SetControlEnabled(_cameraMode == CameraMode.Player);
            }

            _observerRig?.SetControlEnabled(_cameraMode == CameraMode.Observer);
        }

        private void RunTickBatch(
            int requestedTicks,
            RuntimeMetricsBatchKind batchKind,
            ref int ticksAttempted)
        {
            RuntimeMetricsCollector? metrics = _runtimeMetrics;
            if (metrics == null)
            {
                for (int tick = 0; tick < requestedTicks; tick++)
                {
                    ticksAttempted++;
                    ProcessSimulationTick(metrics: null);
                }

                UpdateHud();
                return;
            }

            metrics.BeginBatch(batchKind, SimulationTick);
            try
            {
                for (int tick = 0; tick < requestedTicks; tick++)
                {
                    RuntimeMetricsPhaseToken tickPhase = metrics.BeginPhase(RuntimeMetricsPhase.SimulationTick);
                    ticksAttempted++;
                    try
                    {
                        ProcessSimulationTick(metrics);
                    }
                    finally
                    {
                        tickPhase.Complete();
                    }

                    metrics.RecordCompletedTick(_runtimeSession?.LastTickRuntimeDiagnostics ?? default);
                }

                RuntimeMetricsPhaseToken hudPhase = metrics.BeginPhase(RuntimeMetricsPhase.UpdateHud);
                try
                {
                    UpdateHud();
                }
                finally
                {
                    hudPhase.Complete();
                }

                metrics.EndBatch(SimulationTick);
            }
            catch
            {
                metrics.AbortBatch();
                throw;
            }
        }

        private void ProcessSimulationTick(RuntimeMetricsCollector? metrics)
        {
            if (_runtimeSession == null || _environmentController == null || _scenePresenter == null)
            {
                throw new InvalidOperationException("Runtime tick dependencies are unavailable.");
            }

            PrototypeRuntimeTickResult tickResult;
            if (metrics == null)
            {
                tickResult = _runtimeSession.Advance(
                    (float)TickIntervalSeconds,
                    _environmentController.DayLengthSeconds);
            }
            else
            {
                RuntimeMetricsPhaseToken sessionPhase = metrics.BeginPhase(RuntimeMetricsPhase.SessionAdvance);
                try
                {
                    tickResult = _runtimeSession.Advance(
                        (float)TickIntervalSeconds,
                        _environmentController.DayLengthSeconds,
                        metrics);
                }
                finally
                {
                    sessionPhase.Complete();
                }
            }

            RuntimeMetricsPhaseToken scenePhase = metrics?.BeginPhase(RuntimeMetricsPhase.SceneSync) ?? default;
            try
            {
                ApplyRuntimeStateToScene();
                SyncResourcePresentationIfChanged();
                _scenePresenter.SyncWorkers(_runtimeSession.Workers);
                UpdateSettlementPresentationFromSession();
            }
            finally
            {
                scenePhase.Complete();
            }

            if (tickResult.ShouldCaptureMetrics)
            {
                CaptureMetricsSnapshot();
            }
        }

        private void ApplyRuntimeStateToScene()
        {
            if (_runtimeSession == null)
            {
                return;
            }

            PrototypeWeather weather = _runtimeSession.CurrentWeather;
            float sunlightMultiplier = PrototypeWeatherService.GetSunlightMultiplier(weather);
            _environmentController?.ApplyState(_runtimeSession.CurrentHour, sunlightMultiplier);
            _environmentController?.ApplyWeatherState(weather, _runtimeSession.TimeUntilNextWeatherShift);
            ApplyVisualCaptureLighting();
        }

        private void ApplyVisualCaptureLighting()
        {
            if (_visualCaptureConfigured)
            {
                _environmentController?.SetPresentationLighting(
                    PrototypeVisualCaptureConfiguration.LightingHour,
                    PrototypeVisualCaptureConfiguration.LightingMultiplier);
            }
        }

        private void UpdateSettlementPresentationFromSession()
        {
            if (_scenePresenter == null || _runtimeSession == null || _runtimeSession.UsesVoxelWorld)
            {
                return;
            }

            _scenePresenter.UpdateSettlementPresentation(
                _runtimeSession.Stockpile.Items,
                _runtimeSession.Workers,
                _runtimeSession.Structures,
                _runtimeSession.SettlementClassification,
                _runtimeSession.SelectedBuildQueueStatusText,
                _runtimeSession.MealCoveragePercent,
                _runtimeSession.BedCoveragePercent,
                _runtimeSession.HearthFuel,
                _overlayMode,
                _runtimeSession.PathSegments,
                _runtimeSession.RemoteDepots,
                _runtimeSession.RouteHeatCells,
                _runtimeSession.ActiveDirective,
                _runtimeSession.Crisis);
        }

        private void UpdateSettlementPresentationFromSessionOrFallback()
        {
            if (_scenePresenter == null || _runtimeSession?.UsesVoxelWorld == true)
            {
                return;
            }

            _scenePresenter.UpdateSettlementPresentation(
                _runtimeSession?.Stockpile.Items ?? new Dictionary<string, int>(),
                _runtimeSession?.Workers ?? System.Array.Empty<PrototypeWorkerState>(),
                _runtimeSession?.Structures ?? System.Array.Empty<PrototypeStructureState>(),
                _runtimeSession?.SettlementClassification ?? PrototypeSettlementClassification.Strained,
                _runtimeSession?.SelectedBuildQueueStatusText ?? "Build Queue: empty",
                _runtimeSession?.MealCoveragePercent ?? 0,
                _runtimeSession?.BedCoveragePercent ?? 0,
                _runtimeSession?.HearthFuel ?? 0,
                _overlayMode,
                _runtimeSession?.PathSegments ?? System.Array.Empty<PrototypePathSegmentState>(),
                _runtimeSession?.RemoteDepots ?? System.Array.Empty<PrototypeRemoteDepotState>(),
                _runtimeSession?.RouteHeatCells ?? System.Array.Empty<PrototypeRouteHeatCellState>(),
                _runtimeSession?.ActiveDirective ?? PrototypeSettlementDirective.Neutral,
                _runtimeSession?.Crisis);
        }

        private void NotifyStatus(string message)
        {
            _hud?.SetStatusText(message);
            UpdateHud();
        }

        private void CaptureMetricsSnapshot()
        {
            if (_runtimeSession == null)
            {
                return;
            }

            _runtimeSession.CaptureMetrics();
        }

        private void UpdateHud()
        {
            if (_hud == null || _entityManager == null)
            {
                return;
            }

            if (_runtimeSession?.UsesVoxelWorld == true)
            {
                _hud.SetVoxelFoundationMode(true);
                _hud.SetVoxelWorldcraftState(Inventory, _worldcraftBuildMode, _selectedWorldcraftPieceId, _worldcraftRotation, _runtimeSession.ConstructionRevision);
                UpdateSettlementPresentationFromSessionOrFallback();
                return;
            }

            _hud.SetVoxelFoundationMode(false);

            string timeText = _runtimeSession != null
                ? FormatTime(_runtimeSession.CurrentHour)
                : FormatTime(_environmentController?.CurrentHour ?? 8.0f);
            string weatherText = _runtimeSession?.CurrentWeatherName ?? "Unknown";
            string interactionText = _cameraMode == CameraMode.Observer
                ? "Observer mode active - press F8 to return to the player"
                : _player?.GetInteractionText() ?? "Look at a resource node and press E";
            string sessionMode = _networkManager?.IsLocalSession == true ? "Local" : "Network";

            PrototypeHudPresenter.Apply(
                _hud,
                Mathf.RoundToInt((float)Engine.GetFramesPerSecond()),
                _entityManager.EntityCount,
                timeText,
                weatherText,
                sessionMode,
                SimulationTick,
                Inventory,
                _runtimeSession?.Stockpile.Items ?? new Dictionary<string, int>(),
                _runtimeSession?.Workers ?? System.Array.Empty<PrototypeWorkerState>(),
                _runtimeSession?.Structures ?? System.Array.Empty<PrototypeStructureState>(),
                _runtimeSession?.SettlementClassification ?? PrototypeSettlementClassification.Strained,
                _runtimeSession?.SelectedBuildQueueStatusText ?? "Build Queue: empty",
                _runtimeSession?.MealCoveragePercent ?? 0,
                _runtimeSession?.BedCoveragePercent ?? 0,
                _runtimeSession?.HearthFuel ?? 0,
                _runtimeSession?.AverageRouteLengthMeters ?? 0.0f,
                _runtimeSession?.AverageTravelWorkRatio ?? 0.0f,
                _runtimeSession?.PathCoverageRatio ?? 0.0f,
                _runtimeSession?.RouteBacklogTicksByKind ?? new Dictionary<string, int>(),
                interactionText,
                GetSelectedCitizen(),
                GetSelectedStructure(),
                _runtimeSession?.Scenario.Id,
                _runtimeSession?.WorldSeed,
                _cameraMode,
                _overlayMode,
                _lastWorldSummary,
                _runtimeSession?.ActiveDirective ?? PrototypeSettlementDirective.Neutral,
                _runtimeSession?.Crisis,
                _runtimeSession?.ContributionCountsByResource,
                _runtimeSession?.Wetland,
                GetSelectedCitizenInterest(),
                GetCurrentCivicPolicy());

            UpdateSettlementPresentationFromSessionOrFallback();
        }

        private void RecordEvent(string eventType, string message)
        {
            _runtimeSession?.RecordEvent(eventType, message);
        }

        private void ApplyWorldToScene()
        {
            if (_runtimeSession == null || _terrain == null || _scenePresenter == null)
            {
                return;
            }

            if (_runtimeSession.UsesVoxelWorld)
            {
                _scenePresenter.ClearForVoxelWorld();
                _terrain.ClearWorldPresentation();
                _terrain.Visible = false;
                if (_voxelPresenter == null || !IsInstanceValid(_voxelPresenter))
                {
                    _voxelPresenter = GetOrCreateChild<VoxelWorldPresenter>(_worldRoot!, "VoxelWorldPresenter");
                }
                if (_worldcraftPresenter == null || !IsInstanceValid(_worldcraftPresenter))
                {
                    _worldcraftPresenter = GetOrCreateChild<VoxelWorldcraftPresenter>(_worldRoot!, "VoxelWorldcraftPresenter");
                }
                _voxelPresenter.SetActive(true);
                _voxelPresenter.Apply(_runtimeSession.CaptureVoxelProjection());
                _worldcraftPresenter.SetActive(true);
                _worldcraftPresenter.ApplyPieces(_runtimeSession.ConstructionPieces);
                return;
            }

            if (_runtimeSession.World == null)
            {
                return;
            }

            _terrain.Visible = true;
            if (_voxelPresenter != null)
            {
                _voxelPresenter.SetActive(false);
            }
            _worldcraftPresenter?.SetActive(false);
            _terrain.ApplyWorld(_runtimeSession.World.WorldMap, _overlayMode);
            _scenePresenter.UpdateTerrain(_terrain);
            _scenePresenter.ApplyWorld(_runtimeSession.World);
            SyncResourcePresentationIfChanged(force: true);

            if (_observerRig != null)
            {
                _observerRig.FocusOn(_runtimeSession.SettlementAnchorPosition);
            }
        }

        private static string FormatTime(float currentHour)
        {
            int hours = Mathf.FloorToInt(currentHour);
            int minutes = Mathf.FloorToInt((currentHour - hours) * 60.0f);
            return $"{hours:00}:{minutes:00}";
        }

        private Vector3 BuildPlayerSpawnPoint()
        {
            if (_runtimeSession == null)
            {
                return Vector3.Zero;
            }

            Vector3 desiredPosition = _runtimeSession.SettlementAnchorPosition + new Vector3(0.0f, 0.0f, -8.0f);
            if (_runtimeSession.UsesVoxelWorld)
            {
                return _runtimeSession.GetVoxelSafePlayerSpawnPoint();
            }

            if (_terrain == null || _runtimeSession.World == null)
            {
                return Vector3.Zero;
            }
            return _terrain.GetPlayerSpawnPoint(desiredPosition);
        }

        private void ToggleCameraMode()
        {
            _cameraMode = _cameraMode == CameraMode.Player
                ? CameraMode.Observer
                : CameraMode.Player;

            BindPlayerToRuntime();

            if (_cameraMode == CameraMode.Observer)
            {
                _observerRig?.FocusOn(_runtimeSession?.SettlementAnchorPosition ?? Vector3.Zero);
            }

            _hud?.SetStatusText(_cameraMode == CameraMode.Observer ? "Observer camera enabled" : "Player camera enabled");
            UpdateHud();
        }

        private void CycleOverlayMode()
        {
            _overlayMode = _overlayMode switch
            {
                TerrainOverlayMode.None => TerrainOverlayMode.Biome,
                TerrainOverlayMode.Biome => TerrainOverlayMode.Buildability,
                TerrainOverlayMode.Buildability => TerrainOverlayMode.MovementCost,
                TerrainOverlayMode.MovementCost => TerrainOverlayMode.RouteHeat,
                TerrainOverlayMode.RouteHeat => TerrainOverlayMode.BuiltPaths,
                TerrainOverlayMode.BuiltPaths => TerrainOverlayMode.RemoteDepots,
                _ => TerrainOverlayMode.None
            };

            _terrain?.SetOverlayMode(_overlayMode);
            _hud?.SetStatusText($"Terrain overlay: {_overlayMode}");
            UpdateHud();
        }

        private void ApplyScenarioDefaults(PrototypeScenarioDefinition scenario)
        {
            _scenarioId = scenario.Id;
            _simulationSeed = scenario.SimulationSeed;
            _initialTrees = scenario.InitialTrees;
            _initialRocks = scenario.InitialRocks;
            _initialBerryBushes = scenario.InitialBerryBushes;
            _initialWorkers = scenario.InitialWorkers;

            if (_terrain != null)
            {
                _terrain.WorldSize = scenario.WorldSize;
            }
        }

        private PrototypeScenarioDefinition ResolveScenarioDefinition(string? scenarioId)
        {
            if (_catalogs == null)
            {
                return CreateFallbackCatalogBundle().Scenarios.ResolveDefault();
            }

            if (!string.IsNullOrWhiteSpace(scenarioId))
            {
                try
                {
                    return _catalogs.Scenarios.Resolve(scenarioId);
                }
                catch (InvalidOperationException)
                {
                    GD.PushWarning($"Unknown scenario '{scenarioId}', falling back to '{_catalogs.Scenarios.DefaultScenarioId}'.");
                }
            }

            return _catalogs.Scenarios.ResolveDefault();
        }

        public VoxelWorldProjection CaptureVoxelProjection()
        {
            if (_runtimeSession?.UsesVoxelWorld != true)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            return _runtimeSession.CaptureVoxelProjection();
        }

        /// <summary>Compatibility adapter; player removal always crosses the gather/inventory/event path.</summary>
        public VoxelEditResult ApplyVoxelPlayerIntent(
            VoxelEditKind kind,
            VoxelCoord coord,
            VoxelMaterialId placeMaterial = VoxelMaterialId.Wood)
        {
            if (_runtimeSession?.UsesVoxelWorld != true)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            if (kind == VoxelEditKind.Place)
            {
                return new VoxelEditResult { Rejection = VoxelEditRejection.UnsupportedPlacement, WorldRevision = _runtimeSession.VoxelWorldRevision };
            }
            if (kind != VoxelEditKind.Remove)
            {
                return new VoxelEditResult { Rejection = VoxelEditRejection.InvalidEditKind, WorldRevision = _runtimeSession.VoxelWorldRevision };
            }
            WorldcraftGatherResult gather = ApplyVoxelGatherIntent(coord);
            return gather.VoxelEdit ?? new VoxelEditResult
            {
                Rejection = gather.Rejection == WorldcraftRejection.EventCapacityReached
                    ? VoxelEditRejection.EventCapacityReached
                    : VoxelEditRejection.NonSurfaceEdit,
                WorldRevision = _runtimeSession.VoxelWorldRevision
            };
        }

        public WorldcraftGatherResult ApplyVoxelGatherIntent(VoxelCoord coord)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            WorldcraftGatherResult result = _runtimeSession.GatherVoxel(coord);
            if (result.Accepted)
            {
                _voxelPresenter?.Apply(_runtimeSession.CaptureVoxelProjection(result.VoxelEdit!.DirtyChunks));
                _hud?.SetStatusText($"+1 {InventoryComponent.FormatItemName(result.ItemId)} · packed safely"); UpdateHud();
            }
            else _hud?.SetStatusText(result.Rejection == WorldcraftRejection.InventoryFull ? "Pack full · free a stack before gathering" : "Gather unavailable · aim at an exposed material");
            return result;
        }

        public WorldcraftCommandResult ApplyWorldcraftPlacementIntent(VoxelCoord anchor)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            WorldcraftPlacementCommand command = new() { ActorId = "player", Tick = _runtimeSession.SimulationTick, ExpectedConstructionRevision = _runtimeSession.ConstructionRevision, PieceId = _selectedWorldcraftPieceId, Anchor = anchor, RotationQuarterTurns = _worldcraftRotation, ActorCell = PlayerVoxelCell() };
            WorldcraftCommandResult result = _runtimeSession.PlaceWorldcraftPiece(command);
            if (result.Accepted) { _worldcraftPresenter?.ApplyPieces(_runtimeSession.ConstructionPieces); _hud?.SetStatusText($"Placed {VoxelWorldcraftCatalog.FindPiece(result.Piece!.PieceId)!.DisplayName} · materials recorded"); UpdateHud(); }
            else _hud?.SetStatusText($"Build rejected · {DescribeWorldcraftRejection(result.Rejection)}");
            return result;
        }

        public WorldcraftPlacementEvaluation EvaluateWorldcraftPlacementIntent(VoxelCoord anchor)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            return EvaluateWorldcraftPlacementPresentationProbe(anchor);
        }

        public override void _Input(InputEvent @event)
        {
            // GUI controls consume Tab while focused before _UnhandledInput runs. Handle the
            // field-pack pair at the pre-GUI input phase so the advertised shortcut is reliable.
            if (_runtimeSession?.UsesVoxelWorld != true || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (@event.IsActionPressed("toggle_inventory") || keyEvent.Keycode == Key.Tab || keyEvent.PhysicalKeycode == Key.Tab)
            {
                SetVoxelInventoryOpen(!_voxelInventoryOpen);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_voxelInventoryOpen && keyEvent.Keycode == Key.Escape)
            {
                SetVoxelInventoryOpen(false);
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Non-mutating authoritative evaluator seam for preview and UI diagnostics.</summary>
        public WorldcraftPlacementEvaluation EvaluateWorldcraftPlacementPresentationProbe(
            VoxelCoord anchor,
            long? expectedConstructionRevision = null,
            long? tick = null)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            WorldcraftPlacementEvaluation evaluation = _runtimeSession.EvaluateWorldcraftPlacement(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = tick ?? _runtimeSession.SimulationTick,
                ExpectedConstructionRevision = expectedConstructionRevision ?? _runtimeSession.ConstructionRevision,
                PieceId = _selectedWorldcraftPieceId, Anchor = anchor,
                RotationQuarterTurns = _worldcraftRotation, ActorCell = PlayerVoxelCell()
            });
            _hud?.SetVoxelPlacementEvaluation(evaluation, _worldcraftBuildMode);
            return evaluation;
        }

        public WorldcraftCommandResult ApplyWorldcraftDismantleIntent(string pieceInstanceId)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            WorldcraftCommandResult result = _runtimeSession.DismantleWorldcraftPiece(new WorldcraftDismantleCommand
            {
                ActorId = "player", Tick = _runtimeSession.SimulationTick,
                ExpectedConstructionRevision = _runtimeSession.ConstructionRevision,
                PieceInstanceId = pieceInstanceId, ActorCell = PlayerVoxelCell()
            });
            if (result.Accepted)
            {
                _worldcraftPresenter?.ApplyPieces(_runtimeSession.ConstructionPieces);
                _hud?.SetStatusText($"Recovered {VoxelWorldcraftCatalog.FindPiece(result.Piece!.PieceId)!.DisplayName} · materials returned"); UpdateHud();
            }
            else _hud?.SetStatusText($"Dismantle unavailable · {DescribeWorldcraftRejection(result.Rejection)}");
            return result;
        }

        public bool SelectWorldcraftPiece(string pieceId)
        {
            if (_runtimeSession?.UsesVoxelWorld != true || _voxelInventoryOpen || VoxelWorldcraftCatalog.FindPiece(pieceId) == null) return false;
            _selectedWorldcraftPieceId = pieceId; _worldcraftRotation = 0; _worldcraftBuildMode = true; UpdateHud(); return true;
        }

        private void OnVoxelPieceRequested(string pieceId) => _ = SelectWorldcraftPiece(pieceId);

        private void OnVoxelFieldPackShortcutRequested(bool toggle)
        {
            if (_runtimeSession?.UsesVoxelWorld == true)
            {
                SetVoxelInventoryOpen(toggle ? !_voxelInventoryOpen : false);
            }
        }

        private bool TryHandleVoxelPointerInput(InputEventMouseButton mouseButton)
        {
            if (_runtimeSession?.UsesVoxelWorld != true ||
                _voxelInventoryOpen ||
                mouseButton.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            {
                return false;
            }

            bool build = _worldcraftBuildMode && mouseButton.ButtonIndex == MouseButton.Right;
            RayCast3D? ray = build ? _player?.GetBuildPreviewRay() : _player?.GetGatherRay();
            if (ray == null)
            {
                return false;
            }

            ray.ForceRaycastUpdate();
            if (!ray.IsColliding())
            {
                return false;
            }

            Vector3 collisionPoint = ray.GetCollisionPoint();
            Vector3 collisionNormal = ray.GetCollisionNormal();
            Vector3 cellPoint = collisionPoint + collisionNormal * (build ? 0.01f : -0.01f);
            VoxelCoord coord = new(
                Mathf.FloorToInt(cellPoint.X),
                Mathf.FloorToInt(cellPoint.Y),
                Mathf.FloorToInt(cellPoint.Z));
            if (build) _ = ApplyWorldcraftPlacementIntent(coord);
            else if (mouseButton.ButtonIndex == MouseButton.Left) _ = ApplyVoxelGatherIntent(coord);
            else return false;
            return true;
        }

        private VoxelCoord PlayerVoxelCell()
        {
            Vector3 position = _player?.GlobalPosition ?? Vector3.Zero;
            return new VoxelCoord(Mathf.FloorToInt(position.X), Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.Z));
        }

        private void UpdateWorldcraftPreview()
        {
            if (_voxelInventoryOpen || _runtimeSession?.UsesVoxelWorld != true || _worldcraftPresenter == null)
            { _worldcraftPresenter?.HideGhost(); return; }
            RayCast3D? ray = _worldcraftBuildMode ? _player?.GetBuildPreviewRay() : _player?.GetGatherRay();
            if (ray == null)
            {
                _worldcraftPresenter.HideGhost();
                _hud?.SetVoxelPlacementEvaluation(null, _worldcraftBuildMode);
                return;
            }
            ray.ForceRaycastUpdate();
            if (!ray.IsColliding())
            {
                _worldcraftPresenter.HideGhost();
                _hud?.SetVoxelPlacementEvaluation(null, _worldcraftBuildMode);
                return;
            }
            if (!_worldcraftBuildMode)
            {
                _worldcraftPresenter.HideGhost();
                if (VoxelWorldcraftPresenter.TryResolvePieceInstance(ray.GetCollider(), out _))
                    _hud?.SetVoxelGatherTargetFocus("TARGET · built piece · X dismantles and returns its materials");
                else
                    _hud?.SetVoxelGatherTargetFocus("GATHER TARGET · left-click the exposed material");
                return;
            }
            Vector3 point = ray.GetCollisionPoint() + ray.GetCollisionNormal() * 0.01f;
            VoxelCoord anchor = new(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y), Mathf.FloorToInt(point.Z));
            WorldcraftPlacementEvaluation evaluation = EvaluateWorldcraftPlacementPresentationProbe(anchor);
            _worldcraftPresenter.ShowGhost(evaluation);
            _hud?.SetVoxelPlacementEvaluation(evaluation, true);
        }

        private bool TryDismantleTargetedWorldcraftPiece()
        {
            RayCast3D? ray = _player?.GetGatherRay();
            if (ray == null) return false;
            ray.ForceRaycastUpdate();
            if (!ray.IsColliding() || !VoxelWorldcraftPresenter.TryResolvePieceInstance(ray.GetCollider(), out string instanceId))
            { _hud?.SetStatusText("Aim at a built piece to dismantle"); return false; }
            return ApplyWorldcraftDismantleIntent(instanceId).Accepted;
        }

        public void SetVoxelInventoryOpen(bool open)
        {
            if (_runtimeSession?.UsesVoxelWorld != true) return;
            _voxelInventoryOpen = open;
            _hud?.SetVoxelInventoryVisible(open);
            _player?.SetInputSuppressed(open);
            if (open)
            {
                _worldcraftPresenter?.HideGhost();
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else if (_cameraMode == CameraMode.Player)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            UpdateHud();
        }

        private void ResetVoxelInteractionState()
        {
            _worldcraftBuildMode = false;
            _selectedWorldcraftPieceId = "wood_floor";
            _worldcraftRotation = 0;
            _voxelInventoryOpen = false;
            _worldcraftPresenter?.HideGhost();
            _hud?.SetVoxelInventoryVisible(false);
            _hud?.SetVoxelPlacementEvaluation(null, false);
            _player?.SetInputSuppressed(false);
        }

        private static string DescribeWorldcraftRejection(WorldcraftRejection rejection) => rejection switch
        {
            WorldcraftRejection.Occupied => "space occupied",
            WorldcraftRejection.Unsupported => "needs support",
            WorldcraftRejection.OutOfRange => "move closer",
            WorldcraftRejection.InsufficientMaterials => "need materials",
            WorldcraftRejection.StaleRevision or WorldcraftRejection.TickMismatch => "world changed; try again",
            WorldcraftRejection.InventoryFull => "pack full",
            _ => "that action is unavailable"
        };

        private void OnPlayerHarvestRequested(string siteId, int amount)
        {
            if (_runtimeSession == null || !_runtimeSession.TryHarvestForPlayer(siteId, amount, out string itemId, out int harvestedAmount))
            {
                _hud?.SetStatusText("Resource unavailable");
                return;
            }

            SyncResourcePresentationIfChanged();
            _hud?.SetStatusText($"Harvested {InventoryComponent.FormatItemName(itemId)} x{harvestedAmount}");
            UpdateHud();
        }

        public PrototypeContributionBatchResult TryContributeAllAtDepot(
            Vector3 playerPosition,
            ulong inputFrame)
        {
            float interactionRange = _player?.ContributionRangeMeters ?? 4.5f;
            return _contributionInteraction.Execute(
                _runtimeSession,
                playerPosition,
                interactionRange,
                inputFrame);
        }

        private void OnPlayerContributionRequested(Vector3 playerPosition, ulong inputFrame)
        {
            PrototypeContributionBatchResult result = TryContributeAllAtDepot(playerPosition, inputFrame);
            if (result.Succeeded)
            {
                string summary = string.Join(
                    ", ",
                    result.Results.Select(item =>
                        $"{InventoryComponent.FormatItemName(item.ResourceId)} x{item.AppliedQuantity}"));
                _hud?.SetStatusText($"Contributed {summary}");
                UpdateSettlementPresentationFromSession();
                UpdateHud();
                return;
            }

            if (result.FailureReason == "duplicate_input")
            {
                return;
            }

            string statusText = result.FailureReason switch
            {
                "empty_inventory" => "No resources to contribute",
                "no_eligible_resources" => "No eligible raw resources to contribute",
                "out_of_range" => "Move closer to the central depot",
                "stockpile_rejected" => "Central depot cannot accept those resources",
                _ => "Contribution rejected"
            };
            _hud?.SetStatusText(statusText);
        }

        private void SyncResourcePresentationIfChanged(bool force = false)
        {
            if (_runtimeSession == null || _scenePresenter == null)
            {
                return;
            }

            long revision = _runtimeSession.ResourceRevision;
            if (!force && revision == _lastPresentedResourceRevision)
            {
                return;
            }

            _scenePresenter.SyncResources(_runtimeSession.ResourceSnapshots);
            _lastPresentedResourceRevision = revision;
        }

        private static T GetOrCreateChild<T>(Node parent, string name) where T : Node, new()
        {
            T? existing = parent.GetNodeOrNull<T>(name);
            if (existing != null)
            {
                return existing;
            }

            T node = new() { Name = name };
            parent.AddChild(node);
            return node;
        }

        private static RuntimeMetricsCollector? CreateRuntimeMetricsCollector()
        {
            string? enabled = System.Environment.GetEnvironmentVariable(RuntimeMetricsEnvironmentVariable);
            return string.Equals(enabled, "1", StringComparison.Ordinal)
                ? new RuntimeMetricsCollector()
                : null;
        }

        private PrototypeCatalogBundle CreateFallbackCatalogBundle()
        {
            return new PrototypeCatalogBundle
            {
                Scenarios = new PrototypeScenarioCatalog
                {
                    DefaultScenarioId = DefaultScenarioId,
                    Scenarios = new List<PrototypeScenarioDefinition>
                    {
                        new()
                        {
                            Id = DefaultScenarioId,
                            DisplayName = "Balanced Basin",
                            SimulationSeed = _simulationSeed,
                            InitialTrees = _initialTrees,
                            InitialRocks = _initialRocks,
                            InitialBerryBushes = _initialBerryBushes,
                            InitialCitizens = _initialWorkers,
                            WorldSize = _terrain?.WorldSize ?? 500.0f,
                            StartingStock = new Dictionary<string, int>
                            {
                                ["logs"] = 10,
                                ["stone"] = 8,
                                ["berries"] = 8,
                                ["firewood"] = 6,
                                ["meals"] = 2
                            },
                            StartingStructures = new List<string>
                            {
                                "central_hearth",
                                "central_depot",
                                "cookfire",
                                "wood_yard"
                            },
                            StartingBuildQueue = new List<string>
                            {
                                "drying_rack",
                                "hut",
                                "storehouse",
                                "kiln"
                            }
                        }
                    }
                },
                Resources = new PrototypeResourceCatalog
                {
                    Resources = new List<PrototypeResourceDefinition>
                    {
                        new() { Id = "logs", DisplayName = "Logs", Category = "raw" },
                        new() { Id = "stone", DisplayName = "Stone", Category = "raw" },
                        new() { Id = "berries", DisplayName = "Berries", Category = "raw" },
                        new() { Id = "clay", DisplayName = "Clay", Category = "raw" },
                        new() { Id = "reeds", DisplayName = "Reeds", Category = "raw" },
                        new() { Id = "timber", DisplayName = "Timber", Category = "processed" },
                        new() { Id = "firewood", DisplayName = "Firewood", Category = "processed" },
                        new() { Id = "thatch", DisplayName = "Thatch", Category = "processed" },
                        new() { Id = "brick", DisplayName = "Brick", Category = "processed" },
                        new() { Id = "meals", DisplayName = "Meals", Category = "processed" },
                        new() { Id = "stone_axe", DisplayName = "Stone Axe", Category = "crafted" }
                    }
                },
                Structures = new PrototypeStructureCatalog
                {
                    Structures = new List<PrototypeStructureDefinition>
                    {
                        new() { Id = "central_hearth", DisplayName = "Central Hearth", Category = "core" },
                        new() { Id = "central_depot", DisplayName = "Central Depot", Category = "core" },
                        new() { Id = "cookfire", DisplayName = "Cookfire", Category = "processing" },
                        new() { Id = "wood_yard", DisplayName = "Wood Yard", Category = "processing" },
                        new() { Id = "drying_rack", DisplayName = "Drying Rack", Category = "processing" },
                        new() { Id = "kiln", DisplayName = "Kiln", Category = "processing" },
                        new() { Id = "storehouse", DisplayName = "Storehouse", Category = "storage" },
                        new() { Id = "hut", DisplayName = "Hut", Category = "housing" },
                        new() { Id = "remote_stockpile", DisplayName = "Remote Stockpile", Category = "infrastructure" },
                        new() { Id = "path_segment", DisplayName = "Path Segment", Category = "infrastructure" }
                    }
                },
                RoleQuotas = new PrototypeRoleQuotaCatalog
                {
                    Roles = new List<PrototypeRoleQuotaDefinition>
                    {
                        new() { RoleId = "logger", Share = 0.18d },
                        new() { RoleId = "mason", Share = 0.14d },
                        new() { RoleId = "forager", Share = 0.18d },
                        new() { RoleId = "hauler", Share = 0.20d },
                        new() { RoleId = "processor", Share = 0.14d },
                        new() { RoleId = "builder", Share = 0.08d },
                        new() { RoleId = "generalist", Share = 0.08d }
                    }
                }
            };
        }

        private PrototypeWorkerState? GetSelectedCitizen()
        {
            if (_runtimeSession == null || _runtimeSession.Workers.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(_selectedCitizenInspectionIndex, 0, _runtimeSession.Workers.Count - 1);
            return _runtimeSession.Workers[index];
        }

        private PrototypeCitizenInterest? GetSelectedCitizenInterest()
        {
            PrototypeWorkerState? selectedCitizen = GetSelectedCitizen();
            if (_runtimeSession == null || selectedCitizen == null)
            {
                return null;
            }

            return PrototypeCitizenInterestEvaluator.Evaluate(
                selectedCitizen,
                GetCurrentCivicPolicy());
        }

        private PrototypeCivicPolicy GetCurrentCivicPolicy() => _runtimeSession == null
            ? PrototypeCivicPolicy.Neutral
            : PrototypeCivicPolicyCatalog.ParseId(_runtimeSession.CivicPolicy.PolicyId);

        private void ResetCivicCognitionAction()
        {
            _civicCognitionModule = new PrototypeCognitionModule();
        }

        private PrototypeStructureState? GetSelectedStructure()
        {
            if (_runtimeSession == null || _runtimeSession.Structures.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(_selectedStructureInspectionIndex, 0, _runtimeSession.Structures.Count - 1);
            return _runtimeSession.Structures[index];
        }

        public override void _ExitTree()
        {
            if (_networkManager != null)
            {
                _networkManager.Disconnect();
            }

            if (_player != null)
            {
                _player.HarvestRequested -= OnPlayerHarvestRequested;
                _player.ContributionRequested -= OnPlayerContributionRequested;
            }

            Instance = null;
        }
    }
}
