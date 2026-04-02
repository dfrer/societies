using Godot;
using System;
using Societies.Multiplayer;
using Societies.Simulation;
using Societies.UI;

namespace Societies.Core
{
    /// <summary>
    /// Prototype 1 bootstrapper. Builds a local-first playable slice while preserving
    /// the long-term separation between core, simulation, and networking systems.
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager? Instance { get; private set; }

        [Export] private bool _autoStartSinglePlayer = true;
        [Export] private int _initialTrees = 36;
        [Export] private int _initialRocks = 24;
        [Export] private int _initialBerryBushes = 14;

        private NetworkManager? _networkManager;
        private EntityManager? _entityManager;
        private TerrainGenerator? _terrain;
        private DayNightCycle? _dayNightCycle;
        private WeatherController? _weatherController;
        private PrototypeHud? _hud;
        private PlayerCharacter? _player;
        private InventoryComponent _inventory = new();
        private Node3D? _worldRoot;
        private Node3D? _playersRoot;
        private Node3D? _entitiesRoot;
        private Node3D? _environmentRoot;
        private Node? _systemsRoot;

        private double _tickAccumulator;
        private const double TickIntervalSeconds = 1.0 / 20.0;

        public bool IsGameRunning { get; private set; }

        public override void _Ready()
        {
            Instance = this;

            EnsureSceneStructure();
            ConfigureLocalSession();
            BuildPrototypeWorld();

            _inventory.Changed += OnInventoryChanged;
            UpdateHud();

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
                    TryCraft("stone_axe");
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Key2:
                    TryCraft("campfire");
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F5:
                    _weatherController?.ToggleWeather();
                    UpdateHud();
                    GetViewport().SetInputAsHandled();
                    break;
            }
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
            }
        }

        private void BuildPrototypeWorld()
        {
            if (_worldRoot == null || _playersRoot == null || _entitiesRoot == null || _environmentRoot == null || _systemsRoot == null)
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

            _player = _playersRoot.GetNodeOrNull<PlayerCharacter>("LocalPlayer");

            if (_entitiesRoot.GetChildCount() == 0)
            {
                SpawnResourceSet("wood", _initialTrees);
                SpawnResourceSet("stone", _initialRocks);
                SpawnResourceSet("berry", _initialBerryBushes);
            }

            if (_player == null || !IsInstanceValid(_player))
            {
                _player = new PlayerCharacter
                {
                    Name = "LocalPlayer",
                    Inventory = _inventory,
                    Terrain = _terrain
                };
                _player.GlobalPosition = _terrain.GetPlayerSpawnPoint();
                _playersRoot.AddChild(_player);
            }

            if (_player != null)
            {
                _player.Inventory = _inventory;
                _player.Terrain = _terrain;
            }

            _hud?.SetHelpText(
                "WASD move  Shift sprint  Space jump  Mouse look  E harvest\n" +
                "Tab inventory  1 craft Stone Axe  2 craft Campfire  F5 toggle weather  Esc mouse"
            );
            _hud?.SetStatusText("Prototype 1 ready");
        }

        private void SpawnResourceSet(string resourceId, int count)
        {
            if (_terrain == null || _entitiesRoot == null)
            {
                return;
            }

            RandomNumberGenerator rng = new();
            rng.Randomize();

            for (int i = 0; i < count; i++)
            {
                ResourceNode node = new()
                {
                    Name = $"{resourceId}_{i + 1}",
                    ResourceId = resourceId,
                    UnitsRemaining = resourceId == "berry" ? rng.RandiRange(3, 5) : rng.RandiRange(4, 7)
                };

                node.GlobalPosition = _terrain.GetRandomResourcePoint(rng);
                _entitiesRoot.AddChild(node);
            }
        }

        private void ProcessSimulationTick()
        {
            if (_dayNightCycle != null && _weatherController != null)
            {
                _dayNightCycle.SetWeatherLightMultiplier(_weatherController.SunlightMultiplier);
            }

            // TODO: Move time progression into a headless simulation service for multiplayer/server mode.
        }

        private void TryCraft(string recipeId)
        {
            if (CraftingSystem.TryCraft(recipeId, _inventory, out CraftingRecipe? recipe))
            {
                _hud?.SetStatusText($"Crafted {recipe!.DisplayName}");
            }
            else
            {
                _hud?.SetStatusText(CraftingSystem.GetFailureText(recipeId, _inventory));
            }

            UpdateHud();
        }

        private void OnInventoryChanged()
        {
            UpdateHud();
        }

        private void UpdateHud()
        {
            if (_hud == null || _entityManager == null)
            {
                return;
            }

            string timeText = _dayNightCycle != null ? _dayNightCycle.GetTimeText() : "--:--";
            string weatherText = _weatherController?.CurrentWeatherName ?? "Unknown";
            string interactionText = _player?.GetInteractionText() ?? "Look at a resource node and press E";

            _hud.SetDebugText(
                "Societies Prototype 1\n" +
                $"FPS: {Engine.GetFramesPerSecond():F0}\n" +
                $"Entities: {_entityManager.EntityCount}\n" +
                $"Time: {timeText}\n" +
                $"Weather: {weatherText}\n" +
                $"Mode: {(_networkManager?.IsLocalSession == true ? "Local" : "Network")}"
            );
            _hud.SetInventoryText(_inventory.GetSummaryText());
            _hud.SetCraftingText(CraftingSystem.GetRecipeSummary(_inventory));
            _hud.SetInteractionText(interactionText);
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

        public override void _ExitTree()
        {
            if (_networkManager != null)
            {
                _networkManager.Disconnect();
            }

            _inventory.Changed -= OnInventoryChanged;
            Instance = null;
        }
    }
}
