using Godot;
using Societies.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Authoritative deterministic runtime state for the local prototype session.
    /// Godot scene code should treat this as the simulation kernel and keep view logic outside it.
    /// </summary>
    public sealed class PrototypeRuntimeSession
    {
        private readonly int _defaultWorkerCount;
        private PrototypeWeatherSimulation? _weatherSimulation;
        private PrototypeSettlementSimulation? _settlementSimulation;
        private WorldGenerationResult? _world;
        private int _simulationSeed;

        public PrototypeRuntimeSession(PrototypeScenarioDefinition scenario)
        {
            Scenario = scenario;
            Inventory = new InventoryComponent();
            Stockpile = new InventoryComponent();
            EventLog = new PrototypeEventLog();
            MetricsTracker = new PrototypeMetricsTracker();
            _simulationSeed = scenario.SimulationSeed;
            _defaultWorkerCount = scenario.InitialWorkers;
        }

        public PrototypeScenarioDefinition Scenario { get; }

        public InventoryComponent Inventory { get; }

        public InventoryComponent Stockpile { get; }

        public PrototypeEventLog EventLog { get; }

        public PrototypeMetricsTracker MetricsTracker { get; }

        public long SimulationTick { get; private set; }

        public float CurrentHour { get; private set; }

        public float RunStartHour { get; private set; }

        public int SimulationSeed => _simulationSeed;

        public PrototypeWeather CurrentWeather => _weatherSimulation?.CurrentWeather ?? PrototypeWeather.Clear;

        public string CurrentWeatherName => PrototypeWeatherService.GetName(CurrentWeather);

        public float TimeUntilNextWeatherShift => _weatherSimulation?.TimeUntilNextShift ?? 0.0f;

        public uint WeatherRandomState => _weatherSimulation?.RandomState ?? 0u;

        public IReadOnlyList<PrototypeWorkerState> Workers => _settlementSimulation?.Workers ?? System.Array.Empty<PrototypeWorkerState>();

        public WorldGenerationResult? World => _world;

        public int WorldSeed => _world?.WorldSeed ?? 0;

        public int WorldGenerationAttempt => _world?.WorldGenerationAttempt ?? 0;

        public string WorldHash => _world?.WorldHash ?? string.Empty;

        public Vector3 SettlementAnchorPosition => _world?.SettlementSpawn.AnchorPosition ?? Vector3.Zero;

        public void Initialize(float startHour)
        {
            SimulationTick = 0;
            CurrentHour = startHour;
            RunStartHour = startHour;
            _simulationSeed = Scenario.SimulationSeed;

            EventLog.Clear();
            MetricsTracker.Clear();
            Inventory.ReplaceContents(new Dictionary<string, int>());
            Stockpile.ReplaceContents(new Dictionary<string, int>());
            _world = PrototypeWorldGenerator.Generate(Scenario);
            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed);
            _settlementSimulation = new PrototypeSettlementSimulation(Stockpile, _defaultWorkerCount, SettlementAnchorPosition, _world.WorldMap);
        }

        public bool TryCraftRecipe(string recipeId, out string statusText)
        {
            bool crafted = CraftingSystem.TryCraft(recipeId, Inventory, out CraftingRecipe? recipe);
            statusText = crafted
                ? $"Crafted {recipe!.DisplayName}"
                : CraftingSystem.GetFailureText(recipeId, Inventory);

            RecordEvent(crafted ? PrototypeEventTypes.PlayerCraftSucceeded : PrototypeEventTypes.PlayerCraftFailed, statusText);
            return crafted;
        }

        public string ToggleWeatherState()
        {
            if (_weatherSimulation == null)
            {
                return "Weather simulation unavailable";
            }

            _weatherSimulation.ToggleWeather();
            string statusText = $"Weather set to {CurrentWeatherName}";
            RecordEvent(PrototypeEventTypes.WeatherToggled, statusText);
            return statusText;
        }

        public PrototypeRuntimeTickResult Advance(
            float tickIntervalSeconds,
            float dayLengthSeconds,
            IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            SimulationTick++;
            CurrentHour = PrototypeClockService.AdvanceHour(CurrentHour, tickIntervalSeconds, dayLengthSeconds);

            if (_weatherSimulation != null && _weatherSimulation.Advance(tickIntervalSeconds))
            {
                RecordEvent(PrototypeEventTypes.WeatherShifted, $"Weather shifted to {CurrentWeatherName}");
            }

            PrototypeSettlementTickResult settlementResult = _settlementSimulation?.Advance(resources) ?? new PrototypeSettlementTickResult();
            return new PrototypeRuntimeTickResult(
                settlementResult,
                SimulationTick % 20 == 0);
        }

        public void RecordSettlementEvents(IEnumerable<PrototypeSettlementEvent> settlementEvents)
        {
            foreach (PrototypeSettlementEvent settlementEvent in settlementEvents)
            {
                RecordEvent(settlementEvent.EventType, settlementEvent.Message);
            }
        }

        public void OnHarvestFailed(string workerId, string workerDisplayName, string resourceId)
        {
            _settlementSimulation?.OnHarvestFailed(workerId);
            RecordEvent(PrototypeEventTypes.AiHarvestFailed, $"{workerDisplayName} could not harvest {resourceId}");
        }

        public void RecordAiHarvestSucceeded(string workerDisplayName, string itemId, int harvestedAmount)
        {
            RecordEvent(PrototypeEventTypes.AiHarvestSucceeded, $"{workerDisplayName} harvested {itemId} x{harvestedAmount}");
        }

        public void RecordPlayerHarvest(string itemId, int amount)
        {
            RecordEvent(
                PrototypeEventTypes.PlayerHarvestSucceeded,
                $"Harvested {InventoryComponent.FormatItemName(itemId)} x{amount}");
        }

        public void CaptureMetrics(IReadOnlyList<PrototypeResourceSnapshot> resources)
        {
            MetricsTracker.Capture(
                SimulationTick,
                CurrentHour,
                CurrentWeatherName,
                Inventory.Items,
                Stockpile.Items,
                Workers,
                resources);
        }

        public PrototypeRuntimeSnapshot CaptureSnapshot(
            Vector3 playerPosition,
            IReadOnlyList<PrototypeResourceSnapshot> resources)
        {
            List<PrototypeWorkerSnapshot> workers = Workers
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
                    PhaseDurationTicks = worker.PhaseDurationTicks,
                    Position = PrototypeSerializableVector3.FromVector3(worker.Position),
                    HomePosition = PrototypeSerializableVector3.FromVector3(worker.HomePosition),
                    TargetPosition = PrototypeSerializableVector3.FromVector3(worker.TargetPosition),
                    TargetLabel = worker.TargetLabel,
                    ActivityText = worker.ActivityText
                })
                .ToList();

            return new PrototypeRuntimeSnapshot
            {
                SchemaVersion = 3,
                ScenarioId = Scenario.Id,
                WorldSeed = WorldSeed,
                WorldGenerationAttempt = WorldGenerationAttempt,
                WorldHash = WorldHash,
                SimulationSeed = _simulationSeed,
                SimulationTick = SimulationTick,
                CurrentHour = CurrentHour,
                CurrentWeather = CurrentWeatherName,
                TimeUntilNextWeatherShift = TimeUntilNextWeatherShift,
                WeatherRandomState = WeatherRandomState,
                PlayerPosition = PrototypeSerializableVector3.FromVector3(playerPosition),
                SettlementAnchorPosition = PrototypeSerializableVector3.FromVector3(SettlementAnchorPosition),
                Inventory = new Dictionary<string, int>(Inventory.Items),
                Stockpile = new Dictionary<string, int>(Stockpile.Items),
                Workers = workers,
                Resources = resources.ToList()
            };
        }

        public void ApplySnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            _simulationSeed = snapshot.SimulationSeed;
            SimulationTick = snapshot.SimulationTick;
            CurrentHour = snapshot.CurrentHour;
            RunStartHour = snapshot.CurrentHour;

            Inventory.ReplaceContents(snapshot.Inventory);
            Stockpile.ReplaceContents(snapshot.Stockpile);
            _world = PrototypeWorldGenerator.Regenerate(Scenario, snapshot.WorldSeed, snapshot.WorldGenerationAttempt);

            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed, ParseWeather(snapshot.CurrentWeather));
            _weatherSimulation.SetState(ParseWeather(snapshot.CurrentWeather), snapshot.TimeUntilNextWeatherShift, snapshot.WeatherRandomState);

            int workerCount = snapshot.Workers.Count > 0 ? snapshot.Workers.Count : _defaultWorkerCount;
            _settlementSimulation = new PrototypeSettlementSimulation(Stockpile, workerCount, SettlementAnchorPosition, _world.WorldMap);
            _settlementSimulation.LoadState(snapshot.Workers, SettlementAnchorPosition, _world.WorldMap);
            MetricsTracker.Clear();
        }

        public void RestoreArtifacts(
            IReadOnlyList<PrototypeEventRecord> eventRecords,
            PrototypeRunSummary? runSummary)
        {
            EventLog.ReplaceEntries(eventRecords);
            RunStartHour = runSummary?.StartHour ?? CurrentHour;
        }

        public void RecordEvent(string eventType, string message)
        {
            EventLog.Record(SimulationTick, eventType, message);
        }

        private static PrototypeWeather ParseWeather(string weatherName)
        {
            return string.Equals(weatherName, PrototypeWeatherService.GetName(PrototypeWeather.Rain), System.StringComparison.OrdinalIgnoreCase)
                ? PrototypeWeather.Rain
                : PrototypeWeather.Clear;
        }
    }

    public readonly record struct PrototypeRuntimeTickResult(
        PrototypeSettlementTickResult SettlementResult,
        bool ShouldCaptureMetrics);
}
