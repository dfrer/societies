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
        private readonly IReadOnlyList<PrototypeRoleQuotaDefinition> _roleQuotas;
        private PrototypeWeatherSimulation? _weatherSimulation;
        private PrototypeSettlementSimulation? _settlementSimulation;
        private WorldGenerationResult? _world;
        private int _simulationSeed;

        public PrototypeRuntimeSession(PrototypeScenarioDefinition scenario, IReadOnlyList<PrototypeRoleQuotaDefinition>? roleQuotas = null)
        {
            Scenario = scenario;
            Inventory = new InventoryComponent();
            Stockpile = new InventoryComponent();
            EventLog = new PrototypeEventLog();
            MetricsTracker = new PrototypeMetricsTracker();
            _simulationSeed = scenario.SimulationSeed;
            _roleQuotas = roleQuotas?.ToList() ?? new List<PrototypeRoleQuotaDefinition>();
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

        public IReadOnlyList<PrototypeStructureState> Structures => _settlementSimulation?.Structures ?? System.Array.Empty<PrototypeStructureState>();

        public IReadOnlyList<PrototypePathSegmentState> PathSegments => _settlementSimulation?.PathSegments ?? System.Array.Empty<PrototypePathSegmentState>();

        public IReadOnlyList<PrototypeRemoteDepotState> RemoteDepots => _settlementSimulation?.RemoteDepots ?? System.Array.Empty<PrototypeRemoteDepotState>();

        public IReadOnlyList<PrototypeBuildQueueEntry> BuildQueue => _settlementSimulation?.BuildQueue ?? System.Array.Empty<PrototypeBuildQueueEntry>();

        public PrototypeSettlementClassification SettlementClassification => _settlementSimulation?.Classification ?? PrototypeSettlementClassification.Strained;

        public int BedCoveragePercent => _settlementSimulation?.BedCoveragePercent ?? 0;

        public int MealCoveragePercent => _settlementSimulation?.MealCoveragePercent ?? 0;

        public int HearthFuel => _settlementSimulation?.HearthFuel ?? 0;

        public int HearthLitTicks => _settlementSimulation?.HearthLitTicks ?? 0;

        public float AverageRouteLengthMeters => _settlementSimulation?.AverageRouteLengthMeters ?? 0.0f;

        public float AverageTravelWorkRatio => _settlementSimulation?.AverageTravelWorkRatio ?? 0.0f;

        public float PathCoverageRatio => _settlementSimulation?.PathCoverageRatio ?? 0.0f;

        public IReadOnlyDictionary<string, int> DepotThroughputByDepot => _settlementSimulation?.DepotThroughputByDepot ?? new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> RouteBacklogTicksByKind => _settlementSimulation?.RouteBacklogTicksByKind ?? new Dictionary<string, int>();

        public RuntimeTickDiagnostics LastTickRuntimeDiagnostics
        {
            get
            {
                PrototypeSettlementSimulation.PrototypeSettlementDiagnosticsState? diagnostics = _settlementSimulation?.Diagnostics;
                return diagnostics == null
                    ? default
                    : new RuntimeTickDiagnostics(
                        diagnostics.WorkOrdersGenerated,
                        diagnostics.WorkOrdersGeneratedUncapped,
                        diagnostics.WorkOrdersClaimed,
                        diagnostics.WorkOrdersRemaining,
                        diagnostics.PathPlanLookups,
                        diagnostics.PathPlanCacheHits,
                        diagnostics.CitizensEvaluated)
                    {
                        PathPlanCacheMisses = diagnostics.PathPlanCacheMisses,
                        PathPlanCacheSize = diagnostics.PathPlanCacheSize,
                        NavigationInvalidations = diagnostics.NavigationInvalidations,
                        WorkerCount = diagnostics.WorkerCount,
                        IdleCitizensConsideringWorkOrders = diagnostics.IdleCitizensConsideringWorkOrders,
                        CandidateOrdersEvaluated = diagnostics.CandidateOrdersEvaluated
                    };
            }
        }

        public IReadOnlyList<PrototypeRouteHeatCellState> RouteHeatCells =>
            _settlementSimulation == null || _world == null
                ? System.Array.Empty<PrototypeRouteHeatCellState>()
                : _settlementSimulation.PathHeatByCell
                    .OrderBy(pair => pair.Key.X)
                    .ThenBy(pair => pair.Key.Y)
                    .Select(pair => new PrototypeRouteHeatCellState
                    {
                        GridX = pair.Key.X,
                        GridY = pair.Key.Y,
                        Position = _world.WorldMap.GetCell(pair.Key.X, pair.Key.Y).WorldPosition,
                        UsageCount = pair.Value
                    })
                    .ToList();

        public string SelectedBuildQueueStatusText => _settlementSimulation?.SelectedBuildQueueStatusText ?? "Build Queue: empty";

        public WorldGenerationResult? World => _world;

        public int WorldSeed => _world?.WorldSeed ?? 0;

        public int WorldGenerationAttempt => _world?.WorldGenerationAttempt ?? 0;

        public string WorldHash => _world?.WorldHash ?? string.Empty;

        public Vector3 SettlementAnchorPosition => _world?.SettlementSpawn.AnchorPosition ?? Vector3.Zero;

        public PrototypePerformanceProbeSnapshot CapturePerformanceProbeState()
        {
            return _settlementSimulation?.CapturePerformanceProbeState() ?? default;
        }

        public int ClearDerivedPathCacheForPerformance()
        {
            return _settlementSimulation?.ClearDerivedPathCacheForPerformance() ?? 0;
        }

        public bool TryPrepareForcedPathCompletionForPerformance(out string structureId)
        {
            if (_settlementSimulation == null)
            {
                structureId = string.Empty;
                return false;
            }

            return _settlementSimulation.TryPrepareForcedPathCompletionForPerformance(out structureId);
        }

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
            _settlementSimulation = new PrototypeSettlementSimulation(Scenario, _roleQuotas, _world);
            SyncSettlementViews();
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
            IReadOnlyList<PrototypeResourceSiteState> resources,
            RuntimeMetricsCollector? runtimeMetrics = null)
        {
            SimulationTick++;
            CurrentHour = AdvanceHour(CurrentHour, tickIntervalSeconds, dayLengthSeconds);

            if (_weatherSimulation != null && _weatherSimulation.Advance(tickIntervalSeconds))
            {
                RecordEvent(PrototypeEventTypes.WeatherShifted, $"Weather shifted to {CurrentWeatherName}");
            }

            PrototypeSettlementTickResult settlementResult = _settlementSimulation?.Advance(resources, CurrentHour, CurrentWeather, runtimeMetrics) ?? new PrototypeSettlementTickResult();
            SyncSettlementViews();
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
            SyncSettlementViews();
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

        public bool SelectNextBuildQueueEntry()
        {
            if (_settlementSimulation == null || !_settlementSimulation.SelectNextBuildQueueEntry())
            {
                return false;
            }

            RecordEvent(PrototypeEventTypes.BuildQueueChanged, _settlementSimulation.SelectedBuildQueueStatusText);
            return true;
        }

        public bool ToggleSelectedBuildQueuePause()
        {
            if (_settlementSimulation == null || !_settlementSimulation.ToggleSelectedBuildQueuePause())
            {
                return false;
            }

            RecordEvent(PrototypeEventTypes.BuildQueueChanged, _settlementSimulation.SelectedBuildQueueStatusText);
            return true;
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
                resources,
                SettlementClassification,
                MealCoveragePercent,
                BedCoveragePercent,
                HearthFuel,
                Structures.Count(structure => structure.IsBuilt),
                Structures.Count(structure => structure.IsBlocked),
                AverageRouteLengthMeters,
                AverageTravelWorkRatio,
                PathCoverageRatio,
                DepotThroughputByDepot,
                RouteBacklogTicksByKind);
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
                    RoleId = worker.Role.ToString(),
                    Phase = worker.Phase.ToString(),
                    TargetResourceNodeName = worker.TargetResourceNodeName,
                    TargetStructureId = worker.TargetStructureId,
                    SourceStoreId = worker.SourceStoreId,
                    DestinationStoreId = worker.DestinationStoreId,
                    CarryItemId = worker.CarryItemId,
                    CarryAmount = worker.CarryAmount,
                    TicksRemaining = worker.TicksRemaining,
                    PhaseDurationTicks = worker.PhaseDurationTicks,
                    Position = PrototypeSerializableVector3.FromVector3(worker.Position),
                    HomePosition = PrototypeSerializableVector3.FromVector3(worker.HomePosition),
                    TargetPosition = PrototypeSerializableVector3.FromVector3(worker.TargetPosition),
                    TargetLabel = worker.TargetLabel,
                    ActivityText = worker.ActivityText,
                    Nutrition = worker.Needs.Nutrition,
                    Fatigue = worker.Needs.Fatigue,
                    LastFailureReason = worker.LastFailureReason,
                    CurrentOrderId = worker.CurrentOrderId,
                    CurrentOrderKind = worker.CurrentOrderKind?.ToString() ?? string.Empty,
                    CurrentOrderReason = worker.CurrentOrderReason,
                    HomeBedCapacity = worker.HomeBedCapacity,
                    RecentEvents = worker.RecentEvents.ToList(),
                    TravelTicksAccumulated = worker.TravelTicksAccumulated,
                    WorkTicksAccumulated = worker.WorkTicksAccumulated,
                    CurrentRouteLengthMeters = worker.Navigation.CurrentRouteLengthMeters,
                    CurrentRouteCost = worker.Navigation.CurrentRouteCost,
                    CurrentRouteTravelTicks = worker.Navigation.CurrentRouteTravelTicks,
                    CurrentWaypointIndex = worker.Navigation.CurrentWaypointIndex,
                    CachedRouteVersion = worker.Navigation.CachedRouteVersion,
                    RouteSourceGridX = worker.Navigation.SourceGridX,
                    RouteSourceGridY = worker.Navigation.SourceGridY,
                    RouteDestinationGridX = worker.Navigation.DestinationGridX,
                    RouteDestinationGridY = worker.Navigation.DestinationGridY,
                    RouteWaypoints = worker.Navigation.RouteWaypoints.ToList()
                })
                .ToList();

            return new PrototypeRuntimeSnapshot
            {
                SchemaVersion = 5,
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
                Resources = resources.ToList(),
                Settlement = _settlementSimulation?.CaptureSnapshot(SimulationTick) ?? new PrototypeSettlementSnapshot()
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

            _settlementSimulation = new PrototypeSettlementSimulation(Scenario, _roleQuotas, _world);
            _settlementSimulation.LoadState(snapshot.Settlement ?? new PrototypeSettlementSnapshot());
            SyncSettlementViews();
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

        private void SyncSettlementViews()
        {
            _settlementSimulation?.CopyStockpileTo(Stockpile);
        }

        private static PrototypeWeather ParseWeather(string weatherName)
        {
            return string.Equals(weatherName, PrototypeWeatherService.GetName(PrototypeWeather.Rain), System.StringComparison.OrdinalIgnoreCase)
                ? PrototypeWeather.Rain
                : PrototypeWeather.Clear;
        }

        private static float AdvanceHour(float currentHour, double tickIntervalSeconds, double dayLengthSeconds)
        {
            double hoursPerTick = 24.0 * tickIntervalSeconds / dayLengthSeconds;
            float next = (float)(currentHour + hoursPerTick);
            while (next >= 24.0f)
            {
                next -= 24.0f;
            }
            return next;
        }
    }

    public readonly record struct PrototypeRuntimeTickResult(
        PrototypeSettlementTickResult SettlementResult,
        bool ShouldCaptureMetrics);
}
