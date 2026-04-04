using Godot;
using Societies.Multiplayer;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace Societies.Core
{
    /// <summary>
    /// Prototype runtime orchestrator. Scene setup and presentation stay here; deterministic
    /// simulation state lives in <see cref="PrototypeRuntimeSession"/>.
    /// </summary>
    public partial class GameManager : Node
    {
        private const double TickIntervalSeconds = 1.0 / 20.0;
        private const string DefaultScenarioId = "balanced_basin";

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
        private readonly InventoryComponent _fallbackInventory = new();
        private double _tickAccumulator;
        private CameraMode _cameraMode = CameraMode.Player;
        private TerrainOverlayMode _overlayMode = TerrainOverlayMode.None;
        private PrototypeWorldSummary? _lastWorldSummary;
        private int _selectedCitizenInspectionIndex;
        private int _selectedStructureInspectionIndex;

        public bool IsGameRunning { get; private set; }

        public int SimulationSeed => _runtimeSession?.SimulationSeed ?? _simulationSeed;

        public long SimulationTick => _runtimeSession?.SimulationTick ?? 0;

        public InventoryComponent Inventory => _runtimeSession?.Inventory ?? _fallbackInventory;

        public CameraMode CurrentCameraMode => _cameraMode;

        public TerrainOverlayMode CurrentOverlayMode => _overlayMode;

        public string CurrentScenarioId => _scenario?.Id ?? _scenarioId;

        public int CurrentWorldSeed => _runtimeSession?.WorldSeed ?? 0;

        public override void _Ready()
        {
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
            if (!IsGameRunning)
            {
                return;
            }

            _tickAccumulator += delta;
            while (_tickAccumulator >= TickIntervalSeconds)
            {
                _tickAccumulator -= TickIntervalSeconds;
                ProcessSimulationTick();
            }

            UpdateHud();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (@event.IsActionPressed("toggle_inventory"))
            {
                _hud?.ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }

            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    TryCraftRecipe("stone_axe");
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
            for (int i = 0; i < tickCount; i++)
            {
                ProcessSimulationTick();
            }

            UpdateHud();
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
            StartNewPrototypeRun(resetPlayerPosition: true);
            RecordEvent(PrototypeEventTypes.RuntimeReset, $"Reset prototype run with seed {SimulationSeed}");
            _hud?.SetStatusText("Prototype run reset");
            UpdateHud();
        }

        public void SetScenario(string scenarioId, bool restart = true)
        {
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

            return _runtimeSession.CaptureSnapshot(
                _player?.Position ?? Vector3.Zero,
                _scenePresenter.CaptureResourceSnapshots());
        }

        public string SaveSnapshotToDisk()
        {
            if (_runtimeSession == null || _artifactManager == null || _scenePresenter == null)
            {
                return string.Empty;
            }

            PrototypeArtifactPaths artifactPaths = _artifactManager.GetArtifactPaths();
            RecordEvent(PrototypeEventTypes.SnapshotSaved, $"Saved snapshot to {Path.GetFileName(artifactPaths.LegacySnapshotPath)}");

            PrototypeRuntimeSnapshot snapshot = CaptureSnapshot();
            CaptureMetricsSnapshot();
            PrototypeWorldSummary worldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, snapshot.Resources);
            _lastWorldSummary = worldSummary;

            string snapshotPath = _artifactManager.SaveArtifacts(_runtimeSession, snapshot, worldSummary);
            _hud?.SetStatusText($"Saved snapshot to {Path.GetFileName(snapshotPath)}");
            return snapshotPath;
        }

        public bool LoadLatestSnapshotFromDisk()
        {
            _artifactManager ??= new PrototypeRunArtifactManager();
            PrototypeLoadedArtifacts? loadedArtifacts = _artifactManager.LoadLatestArtifacts();
            if (loadedArtifacts == null)
            {
                _hud?.SetStatusText("No snapshot found");
                return false;
            }

            EnsureWorldShell();

            PrototypeLoadedArtifacts artifacts = loadedArtifacts.Value;
            PrototypeScenarioDefinition scenario = ResolveScenarioDefinition(artifacts.Snapshot.ScenarioId);
            CreateRuntimeSession(scenario);

            _tickAccumulator = 0.0;
            _runtimeSession!.ApplySnapshot(artifacts.Snapshot);
            _runtimeSession.RestoreArtifacts(artifacts.EventLog, artifacts.RunSummary);

            _scenePresenter?.ResetDynamicNodes();
            ApplyWorldToScene();
            _scenePresenter?.ReplaceResourceNodes(artifacts.Snapshot.Resources);
            ApplyRuntimeStateToScene();
            _scenePresenter?.SyncWorkers(_runtimeSession.Workers);
            _scenePresenter?.UpdateSettlementPresentation(
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
                _runtimeSession.RouteHeatCells);

            if (_player != null)
            {
                _player.Velocity = Vector3.Zero;
                _player.Position = artifacts.Snapshot.PlayerPosition.ToVector3();
            }

            BindPlayerToRuntime();
            CaptureMetricsSnapshot();
            _lastWorldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, artifacts.Snapshot.Resources);
            RecordEvent(PrototypeEventTypes.SnapshotLoaded, $"Loaded snapshot from {Path.GetFileName(_artifactManager.GetArtifactPaths().LegacySnapshotPath)}");
            _hud?.SetStatusText($"Loaded snapshot from {Path.GetFileName(_artifactManager.GetArtifactPaths().LegacySnapshotPath)}");
            UpdateHud();
            return true;
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
            string dataDirectory = ProjectSettings.GlobalizePath("res://data");

            try
            {
                _catalogs = PrototypeCatalogLoader.LoadFromDirectory(dataDirectory);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Failed to load prototype catalogs from {dataDirectory}: {ex.Message}. Falling back to built-in legacy defaults.");
                _catalogs = CreateFallbackCatalogBundle();
            }

            _scenario = ResolveScenarioDefinition(_scenarioId);
            ApplyScenarioDefaults(_scenario);
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

            _scenePresenter = new PrototypeSettlementScenePresenter(
                _agentsRoot,
                _entitiesRoot,
                _environmentRoot,
                _terrain);
            _scenePresenter.EnsureSettlementHub();

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

            _player.Harvested -= OnPlayerHarvested;
            _player.Harvested += OnPlayerHarvested;
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
                PrototypeHudPresenter.Initialize(_hud);
            }
        }

        private void StartNewPrototypeRun(bool resetPlayerPosition)
        {
            EnsureWorldShell();

            if (_environmentController == null || _scenePresenter == null || _scenario == null)
            {
                return;
            }

            CreateRuntimeSession(_scenario);
            _runtimeSession!.Initialize(_environmentController.StartHour);
            _tickAccumulator = 0.0;

            _scenePresenter.ResetDynamicNodes();
            ApplyWorldToScene();
            _lastWorldSummary = PrototypeWorldSummaryBuilder.Build(_runtimeSession, _terrain, _scenePresenter.CaptureResourceSnapshots());

            RecordEvent(PrototypeEventTypes.WorldSeeded, $"Spawned world for scenario {_runtimeSession.Scenario.Id} using world seed {_runtimeSession.WorldSeed}");

            if (resetPlayerPosition && _player != null && _terrain != null)
            {
                _player.ResetForPrototypeRun(BuildPlayerSpawnPoint());
            }

            BindPlayerToRuntime();
            ApplyRuntimeStateToScene();
            _scenePresenter.SyncWorkers(_runtimeSession.Workers);
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
                _runtimeSession.RouteHeatCells);
            CaptureMetricsSnapshot();

            _selectedCitizenInspectionIndex = 0;
            _selectedStructureInspectionIndex = 0;
            _hud?.SetStatusText("Prototype V2 M3 ready");
        }

        private void CreateRuntimeSession(PrototypeScenarioDefinition scenario)
        {
            DetachRuntimeObservers();
            _scenario = scenario;
            ApplyScenarioDefaults(scenario);

            if (_terrain != null)
            {
                _terrain.WorldSize = scenario.WorldSize;
                _scenePresenter?.UpdateTerrain(_terrain);
            }

            _runtimeSession = new PrototypeRuntimeSession(scenario, _catalogs?.RoleQuotas.Roles);
            AttachRuntimeObservers();
            BindPlayerToRuntime();
        }

        private void BindPlayerToRuntime()
        {
            if (_player != null)
            {
                _player.Inventory = _runtimeSession?.Inventory ?? _fallbackInventory;
                _player.Terrain = _terrain;
                _player.SetControlEnabled(_cameraMode == CameraMode.Player);
            }

            _observerRig?.SetControlEnabled(_cameraMode == CameraMode.Observer);
        }

        private void ProcessSimulationTick()
        {
            if (_runtimeSession == null || _environmentController == null || _scenePresenter == null)
            {
                return;
            }

            PrototypeRuntimeTickResult tickResult = _runtimeSession.Advance(
                (float)TickIntervalSeconds,
                _environmentController.DayLengthSeconds,
                _scenePresenter.CaptureResourceSites());

            foreach (PrototypeHarvestRequest request in tickResult.SettlementResult.HarvestRequests)
            {
                if (_scenePresenter.ApplyHarvestRequest(request, out string itemId, out int harvestedAmount))
                {
                    _runtimeSession.RecordAiHarvestSucceeded(request.WorkerDisplayName, itemId, harvestedAmount);
                }
                else
                {
                    _runtimeSession.OnHarvestFailed(request.WorkerId, request.WorkerDisplayName, request.ResourceId);
                }
            }

            _runtimeSession.RecordSettlementEvents(tickResult.SettlementResult.Events);
            ApplyRuntimeStateToScene();
            _scenePresenter.SyncWorkers(_runtimeSession.Workers);
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
                _runtimeSession.RouteHeatCells);

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
        }

        private void CaptureMetricsSnapshot()
        {
            if (_runtimeSession == null || _scenePresenter == null)
            {
                return;
            }

            _runtimeSession.CaptureMetrics(_scenePresenter.CaptureResourceSnapshots());
        }

        private void UpdateHud()
        {
            if (_hud == null || _entityManager == null)
            {
                return;
            }

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
                _lastWorldSummary);

            _scenePresenter?.UpdateSettlementPresentation(
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
                _runtimeSession?.RouteHeatCells ?? System.Array.Empty<PrototypeRouteHeatCellState>());
        }

        private void RecordEvent(string eventType, string message)
        {
            _runtimeSession?.RecordEvent(eventType, message);
        }

        private void ApplyWorldToScene()
        {
            if (_runtimeSession?.World == null || _terrain == null || _scenePresenter == null)
            {
                return;
            }

            _terrain.ApplyWorld(_runtimeSession.World.WorldMap, _overlayMode);
            _scenePresenter.UpdateTerrain(_terrain);
            _scenePresenter.ApplyWorld(_runtimeSession.World);

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
            if (_terrain == null || _runtimeSession?.World == null)
            {
                return Vector3.Zero;
            }

            Vector3 desiredPosition = _runtimeSession.SettlementAnchorPosition + new Vector3(0.0f, 0.0f, -8.0f);
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

        private void AttachRuntimeObservers()
        {
            if (_runtimeSession == null)
            {
                return;
            }

            _runtimeSession.Inventory.Changed += OnInventoryChanged;
            _runtimeSession.Stockpile.Changed += OnStockpileChanged;
        }

        private void DetachRuntimeObservers()
        {
            if (_runtimeSession == null)
            {
                return;
            }

            _runtimeSession.Inventory.Changed -= OnInventoryChanged;
            _runtimeSession.Stockpile.Changed -= OnStockpileChanged;
        }

        private void OnInventoryChanged()
        {
            UpdateHud();
        }

        private void OnStockpileChanged()
        {
            UpdateHud();
        }

        private void OnPlayerHarvested(string itemId, int amount)
        {
            string message = $"Harvested {InventoryComponent.FormatItemName(itemId)} x{amount}";
            _hud?.SetStatusText(message);
            _runtimeSession?.RecordPlayerHarvest(itemId, amount);
            UpdateHud();
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
                _player.Harvested -= OnPlayerHarvested;
            }

            DetachRuntimeObservers();
            Instance = null;
        }
    }
}
