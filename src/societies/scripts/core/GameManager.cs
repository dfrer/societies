using Godot;
using Societies.Multiplayer;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Prototype 1 bootstrapper. Builds the playable slice while routing repeatable
    /// simulation work through deterministic services that can be tested outside the scene tree.
    /// </summary>
    public partial class GameManager : Node
    {
        private const double TickIntervalSeconds = 1.0 / 20.0;
        private const string DefaultRunOutputDirectory = "user://prototype_runs";
        private const string SnapshotFileName = "latest-snapshot.json";
        private const string EventLogFileName = "latest-event-log.json";
        private const string RunSummaryFileName = "latest-run-summary.json";
        private const string RunOutputDirectoryEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";
        private static readonly Vector3 SettlementAnchorPosition = Vector3.Zero;

        public static GameManager? Instance { get; private set; }

        [Export] private bool _autoStartSinglePlayer = true;
        [Export] private int _simulationSeed = 1337;
        [Export] private int _initialTrees = 36;
        [Export] private int _initialRocks = 24;
        [Export] private int _initialBerryBushes = 14;
        [Export] private int _initialWorkers = 3;

        private NetworkManager? _networkManager;
        private EntityManager? _entityManager;
        private TerrainGenerator? _terrain;
        private DayNightCycle? _dayNightCycle;
        private WeatherController? _weatherController;
        private PrototypeHud? _hud;
        private PlayerCharacter? _player;
        private readonly InventoryComponent _inventory = new();
        private readonly InventoryComponent _stockpile = new();
        private readonly PrototypeEventLog _eventLog = new();
        private Node3D? _worldRoot;
        private Node3D? _playersRoot;
        private Node3D? _agentsRoot;
        private Node3D? _entitiesRoot;
        private Node3D? _environmentRoot;
        private Node? _systemsRoot;
        private PrototypeWeatherSimulation? _weatherSimulation;
        private PrototypeSettlementSimulation? _settlementSimulation;
        private readonly Dictionary<string, PrototypeWorkerAgent> _workerNodes = new();
        private double _tickAccumulator;
        private long _simulationTick;
        private float _currentHour;
        private float _runStartHour;

        public bool IsGameRunning { get; private set; }

        public int SimulationSeed => _simulationSeed;

        public long SimulationTick => _simulationTick;

        public InventoryComponent Inventory => _inventory;

        public override void _Ready()
        {
            Instance = this;

            EnsureSceneStructure();
            ConfigureLocalSession();
            BuildPrototypeWorld();

            _inventory.Changed += OnInventoryChanged;
            _stockpile.Changed += OnStockpileChanged;
            UpdateHud();

            RecordEvent(PrototypeEventTypes.RuntimeReady, "Societies Prototype 1 initialized");
            GD.Print("Societies Prototype 1 initialized");
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
                case Key.Key2:
                    TryCraftRecipe("campfire");
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
                case Key.F9:
                    LoadLatestSnapshotFromDisk();
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
            bool crafted = CraftingSystem.TryCraft(recipeId, _inventory, out CraftingRecipe? recipe);
            string statusText = crafted
                ? $"Crafted {recipe!.DisplayName}"
                : CraftingSystem.GetFailureText(recipeId, _inventory);

            _hud?.SetStatusText(statusText);
            RecordEvent(crafted ? PrototypeEventTypes.PlayerCraftSucceeded : PrototypeEventTypes.PlayerCraftFailed, statusText);
            UpdateHud();
            return crafted;
        }

        public void ResetPrototypeRun()
        {
            if (_entitiesRoot == null || _agentsRoot == null)
            {
                return;
            }

            _eventLog.Clear();
            _inventory.ReplaceContents(new Dictionary<string, int>());
            _stockpile.ReplaceContents(new Dictionary<string, int>());
            ClearChildren(_entitiesRoot);
            ClearChildren(_agentsRoot);
            _workerNodes.Clear();

            _simulationTick = 0;
            _tickAccumulator = 0.0;
            _weatherSimulation = null;
            _settlementSimulation = null;

            BuildPrototypeWorld();

            if (_terrain != null && _player != null)
            {
                _player.ResetForPrototypeRun(_terrain.GetPlayerSpawnPoint());
            }

            _runStartHour = _currentHour;
            RecordEvent(PrototypeEventTypes.RuntimeReset, $"Reset prototype run with seed {_simulationSeed}");
            _hud?.SetStatusText("Prototype run reset");
            UpdateHud();
        }

        public void ToggleWeatherState()
        {
            if (_weatherSimulation == null)
            {
                return;
            }

            _weatherSimulation.ToggleWeather();
            ApplySimulationStateToScene();

            string statusText = $"Weather set to {PrototypeWeatherService.GetName(_weatherSimulation.CurrentWeather)}";
            _hud?.SetStatusText(statusText);
            RecordEvent(PrototypeEventTypes.WeatherToggled, statusText);
            UpdateHud();
        }

        public PrototypeRuntimeSnapshot CaptureSnapshot()
        {
            List<PrototypeResourceSnapshot> resources = _entitiesRoot == null
                ? new List<PrototypeResourceSnapshot>()
                : _entitiesRoot
                    .GetChildren()
                    .OfType<ResourceNode>()
                    .OrderBy(node => node.Name.ToString())
                    .Select(node => new PrototypeResourceSnapshot
                    {
                        ResourceId = node.ResourceId,
                        UnitsRemaining = node.UnitsRemaining,
                        Position = PrototypeSerializableVector3.FromVector3(node.Position)
                    })
                    .ToList();

            List<PrototypeWorkerSnapshot> workers = _settlementSimulation?.Workers
                .OrderBy(worker => worker.WorkerId)
                .Select(worker => new PrototypeWorkerSnapshot
                {
                    WorkerId = worker.WorkerId,
                    DisplayName = worker.DisplayName,
                    PreferredResourceId = worker.PreferredResourceId,
                    Phase = worker.Phase.ToString(),
                    TargetResourceNodeName = worker.TargetResourceNodeName,
                    CarryItemId = worker.CarryItemId,
                    CarryAmount = worker.CarryAmount,
                    TicksRemaining = worker.TicksRemaining,
                    Position = PrototypeSerializableVector3.FromVector3(worker.Position)
                })
                .ToList() ?? new List<PrototypeWorkerSnapshot>();

            return new PrototypeRuntimeSnapshot
            {
                SimulationSeed = _simulationSeed,
                SimulationTick = _simulationTick,
                CurrentHour = _currentHour,
                CurrentWeather = _weatherSimulation != null
                    ? PrototypeWeatherService.GetName(_weatherSimulation.CurrentWeather)
                    : PrototypeWeatherService.GetName(PrototypeWeather.Clear),
                TimeUntilNextWeatherShift = _weatherSimulation?.TimeUntilNextShift ?? 0.0f,
                WeatherRandomState = _weatherSimulation?.RandomState ?? 0u,
                PlayerPosition = PrototypeSerializableVector3.FromVector3(_player?.Position ?? Vector3.Zero),
                Inventory = new Dictionary<string, int>(_inventory.Items),
                Stockpile = new Dictionary<string, int>(_stockpile.Items),
                Workers = workers,
                Resources = resources
            };
        }

        public string SaveSnapshotToDisk()
        {
            string snapshotPath = GetLatestSnapshotPath();
            string eventLogPath = GetLatestEventLogPath();
            string runSummaryPath = GetLatestRunSummaryPath();

            RecordEvent(PrototypeEventTypes.SnapshotSaved, $"Saved snapshot to {Path.GetFileName(snapshotPath)}");
            PrototypeRuntimeSnapshot snapshot = CaptureSnapshot();

            PrototypePersistenceService.SaveSnapshot(snapshotPath, snapshot);
            PrototypePersistenceService.SaveEventLog(eventLogPath, _eventLog);
            PrototypePersistenceService.SaveRunSummary(
                runSummaryPath,
                PrototypeRunSummaryBuilder.Build(snapshot, _eventLog.Entries, _runStartHour));

            _hud?.SetStatusText($"Saved snapshot to {Path.GetFileName(snapshotPath)}");
            return snapshotPath;
        }

        public bool LoadLatestSnapshotFromDisk()
        {
            string snapshotPath = GetLatestSnapshotPath();
            if (!File.Exists(snapshotPath))
            {
                _hud?.SetStatusText("No snapshot found");
                return false;
            }

            PrototypeRuntimeSnapshot snapshot = PrototypePersistenceService.LoadSnapshot(snapshotPath);
            LoadRunArtifactsForSnapshot(snapshot);
            ApplySnapshot(snapshot);
            RecordEvent(PrototypeEventTypes.SnapshotLoaded, $"Loaded snapshot from {Path.GetFileName(snapshotPath)}");
            _hud?.SetStatusText($"Loaded snapshot from {Path.GetFileName(snapshotPath)}");
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

        private void ConfigureLocalSession()
        {
            if (_autoStartSinglePlayer)
            {
                _networkManager?.StartLocalSession();
                IsGameRunning = true;
                RecordEvent(PrototypeEventTypes.SessionStarted, "Started local prototype session");
            }
        }

        private void BuildPrototypeWorld()
        {
            if (_worldRoot == null || _playersRoot == null || _agentsRoot == null || _entitiesRoot == null || _environmentRoot == null || _systemsRoot == null)
            {
                return;
            }

            _terrain = _systemsRoot.GetNodeOrNull<TerrainGenerator>("Terrain");
            if (_terrain == null)
            {
                _terrain = new TerrainGenerator { Name = "Terrain" };
                _systemsRoot.AddChild(_terrain);
            }

            _dayNightCycle = _environmentRoot.GetNodeOrNull<DayNightCycle>("DayNightCycle");
            if (_dayNightCycle == null)
            {
                _dayNightCycle = new DayNightCycle { Name = "DayNightCycle" };
                _environmentRoot.AddChild(_dayNightCycle);
            }

            _weatherController = _environmentRoot.GetNodeOrNull<WeatherController>("Weather");
            if (_weatherController == null)
            {
                _weatherController = new WeatherController { Name = "Weather" };
                _environmentRoot.AddChild(_weatherController);
            }

            InitializeSimulationStateIfNeeded();
            InitializeSettlementSimulationIfNeeded();

            if (_entitiesRoot.GetChildCount() == 0)
            {
                DeterministicRandom rng = new(_simulationSeed);
                SpawnResourceSet("wood", _initialTrees, rng);
                SpawnResourceSet("stone", _initialRocks, rng);
                SpawnResourceSet("berry", _initialBerryBushes, rng);
                RecordEvent(PrototypeEventTypes.WorldSeeded, $"Spawned prototype resources using seed {_simulationSeed}");
            }

            _player = _playersRoot.GetNodeOrNull<PlayerCharacter>("LocalPlayer");

            if (_player == null || !IsInstanceValid(_player))
            {
                _player = new PlayerCharacter
                {
                    Name = "LocalPlayer",
                    Inventory = _inventory,
                    Terrain = _terrain
                };
                _playersRoot.AddChild(_player);
                _player.Position = _terrain.GetPlayerSpawnPoint();
            }

            if (_player != null)
            {
                _player.Harvested -= OnPlayerHarvested;
                _player.Harvested += OnPlayerHarvested;
                _player.Inventory = _inventory;
                _player.Terrain = _terrain;
            }

            _hud?.SetHelpText(PrototypeHudTextBuilder.BuildHelpText());
            _hud?.SetStatusText("Prototype 1 ready");
            ApplySimulationStateToScene();
            SyncWorkerNodes();
        }

        private void InitializeSimulationStateIfNeeded()
        {
            if (_dayNightCycle == null || _weatherController == null || _weatherSimulation != null)
            {
                return;
            }

            _currentHour = _dayNightCycle.StartHour;
            _runStartHour = _currentHour;
            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed);
            ApplySimulationStateToScene();
        }

        private void InitializeSettlementSimulationIfNeeded()
        {
            if (_settlementSimulation != null)
            {
                return;
            }

            _settlementSimulation = new PrototypeSettlementSimulation(_stockpile, _initialWorkers, SettlementAnchorPosition);
        }

        private void SpawnResourceSet(string resourceId, int count, DeterministicRandom rng)
        {
            if (_terrain == null || _entitiesRoot == null)
            {
                return;
            }

            List<PrototypeResourceSpawn> plan = PrototypeResourceSpawnPlanner.CreatePlan(resourceId, count, _terrain.GetSpawnBounds(), rng);
            for (int i = 0; i < plan.Count; i++)
            {
                SpawnResourceNode(plan[i].ResourceId, plan[i].Position, plan[i].UnitsRemaining, i + 1);
            }
        }

        private void SpawnResourceNode(string resourceId, Vector3 position, int unitsRemaining, int sequence)
        {
            if (_entitiesRoot == null)
            {
                return;
            }

            ResourceNode node = new()
            {
                Name = $"{resourceId}_{sequence}",
                ResourceId = resourceId,
                UnitsRemaining = unitsRemaining
            };
            _entitiesRoot.AddChild(node);
            node.Position = position;
        }

        private void ProcessSimulationTick()
        {
            _simulationTick++;

            if (_dayNightCycle != null)
            {
                _currentHour = PrototypeClockService.AdvanceHour(_currentHour, (float)TickIntervalSeconds, _dayNightCycle.DayLengthSeconds);
            }

            if (_weatherSimulation != null && _weatherSimulation.Advance((float)TickIntervalSeconds))
            {
                RecordEvent(PrototypeEventTypes.WeatherShifted, $"Weather shifted to {PrototypeWeatherService.GetName(_weatherSimulation.CurrentWeather)}");
            }

            ProcessSettlementSimulationTick();
            ApplySimulationStateToScene();
        }

        private void ApplySimulationStateToScene()
        {
            PrototypeWeather weather = _weatherSimulation?.CurrentWeather ?? PrototypeWeather.Clear;
            float sunlightMultiplier = PrototypeWeatherService.GetSunlightMultiplier(weather);
            float timeUntilNextShift = _weatherSimulation?.TimeUntilNextShift ?? 0.0f;

            _dayNightCycle?.ApplyState(_currentHour, sunlightMultiplier);
            _weatherController?.ApplyState(weather, timeUntilNextShift);
        }

        private void ProcessSettlementSimulationTick()
        {
            if (_settlementSimulation == null || _entitiesRoot == null)
            {
                return;
            }

            List<PrototypeResourceSiteState> resources = _entitiesRoot
                .GetChildren()
                .OfType<ResourceNode>()
                .OrderBy(node => node.Name.ToString())
                .Select(node => new PrototypeResourceSiteState(
                    node.Name.ToString(),
                    node.ResourceId,
                    node.Position,
                    node.UnitsRemaining))
                .ToList();

            PrototypeSettlementTickResult result = _settlementSimulation.Advance(resources);

            foreach (PrototypeHarvestRequest request in result.HarvestRequests)
            {
                ApplyHarvestRequest(request);
            }

            foreach (PrototypeSettlementEvent settlementEvent in result.Events)
            {
                RecordEvent(settlementEvent.EventType, settlementEvent.Message);
            }

            SyncWorkerNodes();
        }

        private void ApplyHarvestRequest(PrototypeHarvestRequest request)
        {
            if (_entitiesRoot == null)
            {
                return;
            }

            ResourceNode? node = _entitiesRoot
                .GetChildren()
                .OfType<ResourceNode>()
                .FirstOrDefault(candidate => candidate.Name.ToString() == request.TargetNodeName);

            if (node == null || !node.TryHarvest(request.Amount, out string itemId, out int harvestedAmount))
            {
                _settlementSimulation?.OnHarvestFailed(request.WorkerId);
                RecordEvent(PrototypeEventTypes.AiHarvestFailed, $"{request.WorkerDisplayName} could not harvest {request.ResourceId}");
                return;
            }

            RecordEvent(PrototypeEventTypes.AiHarvestSucceeded, $"{request.WorkerDisplayName} harvested {itemId} x{harvestedAmount}");
        }

        private void ApplySnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            _simulationSeed = snapshot.SimulationSeed;
            _simulationTick = snapshot.SimulationTick;
            _tickAccumulator = 0.0;
            _currentHour = snapshot.CurrentHour;
            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed, ParseWeather(snapshot.CurrentWeather));
            _weatherSimulation.SetState(ParseWeather(snapshot.CurrentWeather), snapshot.TimeUntilNextWeatherShift, snapshot.WeatherRandomState);

            _inventory.ReplaceContents(snapshot.Inventory);
            _stockpile.ReplaceContents(snapshot.Stockpile);
            InitializeSettlementSimulationIfNeeded();
            _settlementSimulation?.LoadState(snapshot.Workers, SettlementAnchorPosition);

            if (_player != null)
            {
                _player.Velocity = Vector3.Zero;
                _player.Position = snapshot.PlayerPosition.ToVector3();
            }

            ReplaceResourceNodes(snapshot.Resources);
            ApplySimulationStateToScene();
            SyncWorkerNodes();
            UpdateHud();
        }

        private void ReplaceResourceNodes(IReadOnlyList<PrototypeResourceSnapshot> resourceSnapshots)
        {
            if (_entitiesRoot == null)
            {
                return;
            }

            foreach (Node child in _entitiesRoot.GetChildren())
            {
                child.Free();
            }

            Dictionary<string, int> counters = new();
            foreach (PrototypeResourceSnapshot snapshot in resourceSnapshots)
            {
                int sequence = counters.TryGetValue(snapshot.ResourceId, out int current) ? current + 1 : 1;
                counters[snapshot.ResourceId] = sequence;
                SpawnResourceNode(snapshot.ResourceId, snapshot.Position.ToVector3(), snapshot.UnitsRemaining, sequence);
            }
        }

        private void OnInventoryChanged()
        {
            UpdateHud();
        }

        private void OnPlayerHarvested(string itemId, int amount)
        {
            string message = $"Harvested {InventoryComponent.FormatItemName(itemId)} x{amount}";
            _hud?.SetStatusText(message);
            RecordEvent(PrototypeEventTypes.PlayerHarvestSucceeded, message);
            UpdateHud();
        }

        private void OnStockpileChanged()
        {
            UpdateHud();
        }

        private void SyncWorkerNodes()
        {
            if (_agentsRoot == null || _settlementSimulation == null)
            {
                return;
            }

            HashSet<string> activeWorkerIds = _settlementSimulation.Workers
                .Select(worker => worker.WorkerId)
                .ToHashSet();

            foreach ((string workerId, PrototypeWorkerAgent node) in _workerNodes.ToList())
            {
                if (activeWorkerIds.Contains(workerId))
                {
                    continue;
                }

                if (IsInstanceValid(node))
                {
                    node.QueueFree();
                }

                _workerNodes.Remove(workerId);
            }

            foreach (PrototypeWorkerState worker in _settlementSimulation.Workers.OrderBy(candidate => candidate.WorkerId))
            {
                if (!_workerNodes.TryGetValue(worker.WorkerId, out PrototypeWorkerAgent? node) || !IsInstanceValid(node))
                {
                    node = new PrototypeWorkerAgent
                    {
                        Name = worker.WorkerId
                    };
                    _agentsRoot.AddChild(node);
                    _workerNodes[worker.WorkerId] = node;
                }

                node.ApplyState(worker);
            }
        }

        private void UpdateHud()
        {
            if (_hud == null || _entityManager == null)
            {
                return;
            }

            string timeText = PrototypeClockService.FormatTime(_currentHour);
            string weatherText = _weatherSimulation != null
                ? PrototypeWeatherService.GetName(_weatherSimulation.CurrentWeather)
                : "Unknown";
            string interactionText = _player?.GetInteractionText() ?? "Look at a resource node and press E";
            string sessionMode = _networkManager?.IsLocalSession == true ? "Local" : "Network";

            _hud.SetDebugText(
                PrototypeHudTextBuilder.BuildDebugText(
                    Mathf.RoundToInt((float)Engine.GetFramesPerSecond()),
                    _entityManager.EntityCount,
                    timeText,
                    weatherText,
                    sessionMode,
                    _simulationTick));
            _hud.SetInventoryText(_inventory.GetSummaryText());
            _hud.SetCraftingText(CraftingSystem.GetRecipeSummary(_inventory));
            _hud.SetSettlementText(
                PrototypeHudTextBuilder.BuildSettlementText(
                    _stockpile.Items,
                    _settlementSimulation?.Workers ?? System.Array.Empty<PrototypeWorkerState>()));
            _hud.SetInteractionText(interactionText);
        }

        private void RecordEvent(string eventType, string message)
        {
            _eventLog.Record(_simulationTick, eventType, message);
        }

        private string GetLatestSnapshotPath()
        {
            return Path.Combine(GetRunOutputDirectoryPath(), SnapshotFileName);
        }

        private string GetLatestEventLogPath()
        {
            return Path.Combine(GetRunOutputDirectoryPath(), EventLogFileName);
        }

        private string GetLatestRunSummaryPath()
        {
            return Path.Combine(GetRunOutputDirectoryPath(), RunSummaryFileName);
        }

        private string GetRunOutputDirectoryPath()
        {
            string? overrideDirectory = System.Environment.GetEnvironmentVariable(RunOutputDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideDirectory))
            {
                return Path.GetFullPath(overrideDirectory);
            }

            return ProjectSettings.GlobalizePath(DefaultRunOutputDirectory);
        }

        private void LoadRunArtifactsForSnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            string eventLogPath = GetLatestEventLogPath();
            _eventLog.Clear();

            if (File.Exists(eventLogPath))
            {
                _eventLog.ReplaceEntries(PrototypePersistenceService.LoadEventLog(eventLogPath));
            }

            string runSummaryPath = GetLatestRunSummaryPath();
            if (File.Exists(runSummaryPath))
            {
                _runStartHour = PrototypePersistenceService.LoadRunSummary(runSummaryPath).StartHour;
                return;
            }

            _runStartHour = snapshot.CurrentHour;
        }

        private static PrototypeWeather ParseWeather(string weatherName)
        {
            return string.Equals(weatherName, PrototypeWeatherService.GetName(PrototypeWeather.Rain), StringComparison.OrdinalIgnoreCase)
                ? PrototypeWeather.Rain
                : PrototypeWeather.Clear;
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

        private static void ClearChildren(Node parent)
        {
            foreach (Node child in parent.GetChildren())
            {
                child.Free();
            }
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

            _inventory.Changed -= OnInventoryChanged;
            _stockpile.Changed -= OnStockpileChanged;
            Instance = null;
        }
    }
}
