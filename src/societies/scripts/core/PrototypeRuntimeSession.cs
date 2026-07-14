using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        private PrototypeResourceLedger? _resourceLedger;
        private PrototypeCrisisState? _crisisState;
        private int _simulationSeed;
        private readonly PrototypeOrderSelectionMode _orderSelectionMode;
        private readonly PrototypeExtractionPlanningMode _extractionPlanningMode;
        private readonly PrototypeRouteDistanceMode _routeDistanceMode;

        public PrototypeRuntimeSession(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition>? roleQuotas = null,
            PrototypeOrderSelectionMode orderSelectionMode = PrototypeOrderSelectionMode.ExactBranchAndBound,
            PrototypeExtractionPlanningMode extractionPlanningMode = PrototypeExtractionPlanningMode.ExactBounded,
            PrototypeRouteDistanceMode routeDistanceMode = PrototypeRouteDistanceMode.CachedDistanceOnly)
        {
            Scenario = scenario;
            Inventory = new InventoryComponent();
            Stockpile = new InventoryComponent();
            EventLog = new PrototypeEventLog();
            MetricsTracker = new PrototypeMetricsTracker();
            _simulationSeed = scenario.SimulationSeed;
            _roleQuotas = roleQuotas?.ToList() ?? new List<PrototypeRoleQuotaDefinition>();
            _orderSelectionMode = orderSelectionMode;
            _extractionPlanningMode = extractionPlanningMode;
            _routeDistanceMode = routeDistanceMode;
        }

        public PrototypeScenarioDefinition Scenario { get; }

        public InventoryComponent Inventory { get; }

        public InventoryComponent Stockpile { get; }

        public PrototypeEventLog EventLog { get; }

        public PrototypeMetricsTracker MetricsTracker { get; }

        public PrototypeCrisisState? Crisis => _crisisState;

        public bool SupportsRuntimeSnapshotPersistence => Scenario.Crisis == null;

        public long SimulationTick { get; private set; }

        public float CurrentHour { get; private set; }

        public float RunStartHour { get; private set; }

        public int SimulationSeed => _simulationSeed;

        public PrototypeOrderSelectionMode OrderSelectionMode => _orderSelectionMode;

        public PrototypeExtractionPlanningMode ExtractionPlanningMode => _extractionPlanningMode;

        public PrototypeRouteDistanceMode RouteDistanceMode => _routeDistanceMode;

        public long CachedRouteDistanceFastPathHits =>
            _settlementSimulation?.CachedRouteDistanceFastPathHits ?? 0;

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
                        CandidateOrdersEvaluated = diagnostics.CandidateOrdersEvaluated,
                        SelectorCandidatesBounded = diagnostics.SelectorCandidatesBounded,
                        SelectorCandidatesExactScored = diagnostics.SelectorCandidatesExactScored,
                        SelectorCandidatesPruned = diagnostics.SelectorCandidatesPruned,
                        SelectorExactPathQueries = diagnostics.SelectorExactPathQueries,
                        SelectorPathCacheHits = diagnostics.SelectorPathCacheHits,
                        SelectorPathCacheMisses = diagnostics.SelectorPathCacheMisses,
                        SelectorSelectedRouteReuses = diagnostics.SelectorSelectedRouteReuses
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

        public IReadOnlyList<PrototypeResourceSnapshot> ResourceSnapshots =>
            _resourceLedger?.CaptureSnapshots() ?? System.Array.Empty<PrototypeResourceSnapshot>();

        public IReadOnlyList<PrototypeResourceSnapshot> ActiveResourceSnapshots =>
            _resourceLedger?.CaptureSnapshots(includeDepleted: false) ?? System.Array.Empty<PrototypeResourceSnapshot>();

        public long ResourceRevision => _resourceLedger?.Revision ?? 0;

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
            _resourceLedger = PrototypeResourceLedger.Create(_world);
            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed);
            _settlementSimulation = new PrototypeSettlementSimulation(
                Scenario,
                _roleQuotas,
                _world,
                orderSelectionMode: _orderSelectionMode,
                extractionPlanningMode: _extractionPlanningMode,
                routeDistanceMode: _routeDistanceMode);
            _crisisState = Scenario.Crisis == null ? null : new PrototypeCrisisState(Scenario.Crisis);
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
            RuntimeMetricsCollector? runtimeMetrics = null,
            bool simulationPaused = false)
        {
            if (simulationPaused)
            {
                return new PrototypeRuntimeTickResult(
                    new PrototypeSettlementTickResult(),
                    System.Array.Empty<PrototypeHarvestResult>(),
                    false);
            }

            SimulationTick++;
            CurrentHour = AdvanceHour(CurrentHour, tickIntervalSeconds, dayLengthSeconds);

            if (_weatherSimulation != null && _weatherSimulation.Advance(tickIntervalSeconds))
            {
                RecordEvent(PrototypeEventTypes.WeatherShifted, $"Weather shifted to {CurrentWeatherName}");
            }

            IReadOnlyList<PrototypeResourceSiteState> resources = _resourceLedger?.CaptureActiveSites() ?? System.Array.Empty<PrototypeResourceSiteState>();
            PrototypeSettlementTickResult settlementResult = _settlementSimulation?.Advance(resources, CurrentHour, CurrentWeather, runtimeMetrics) ?? new PrototypeSettlementTickResult();
            IReadOnlyList<PrototypeHarvestResult> harvestResults = ApplyAiHarvestRequests(settlementResult.HarvestRequests, runtimeMetrics);

            RecordSettlementEvents(settlementResult.Events);
            SyncSettlementViews();
            if (_crisisState != null && _settlementSimulation != null)
            {
                _crisisState.Advance(new PrototypeCrisisObservation(
                    _settlementSimulation.Workers.Count,
                    _settlementSimulation.CapableCitizenCount,
                    _settlementSimulation.MealCount,
                    _settlementSimulation.HearthFuel,
                    _settlementSimulation.BedCoveragePercent));
            }

            return new PrototypeRuntimeTickResult(
                settlementResult,
                harvestResults,
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

        public PrototypeHarvestResult HarvestForPlayer(string siteId, int amount)
        {
            if (_resourceLedger == null || amount <= 0 || string.IsNullOrWhiteSpace(siteId))
            {
                return new PrototypeHarvestResult("player", siteId, string.Empty, amount, 0, false, "invalid_command");
            }

            PrototypeResourceSnapshot? site = ResourceSnapshots.FirstOrDefault(candidate => candidate.SiteId == siteId);
            if (site == null)
            {
                return new PrototypeHarvestResult("player", siteId, string.Empty, amount, 0, false, "site_missing");
            }

            if (Inventory.GetCount(site.ResourceId) > int.MaxValue - amount)
            {
                return new PrototypeHarvestResult("player", siteId, site.ResourceId, amount, 0, false, "inventory_overflow");
            }

            PrototypeHarvestResult result = _resourceLedger.Apply(new PrototypeHarvestCommand("player", siteId, site.ResourceId, amount));
            if (result.Succeeded)
            {
                Inventory.AddItem(result.ResourceId, result.AppliedQuantity);
                RecordPlayerHarvest(result.ResourceId, result.AppliedQuantity);
            }

            return result;
        }

        public bool TryHarvestForPlayer(string siteId, int amount, out string itemId, out int harvestedAmount)
        {
            PrototypeHarvestResult result = HarvestForPlayer(siteId, amount);
            itemId = result.ResourceId;
            harvestedAmount = result.AppliedQuantity;
            return result.Succeeded;
        }

        private IReadOnlyList<PrototypeHarvestResult> ApplyAiHarvestRequests(
            IReadOnlyList<PrototypeHarvestRequest> requests,
            RuntimeMetricsCollector? runtimeMetrics = null)
        {
            List<PrototypeHarvestResult> results = new(requests.Count);
            RuntimeMetricsPhaseToken harvestPhase = runtimeMetrics?.BeginPhase(RuntimeMetricsPhase.HarvestApply) ?? default;
            try
            {
                foreach (PrototypeHarvestRequest request in requests)
                {
                    PrototypeHarvestCommand command = new(
                        request.WorkerId,
                        request.TargetNodeName,
                        request.ResourceId,
                        request.Amount);
                    PrototypeHarvestResult result = _resourceLedger?.Apply(command) ?? new PrototypeHarvestResult(
                        command.ActorId,
                        command.SiteId,
                        command.ResourceId,
                        command.RequestedQuantity,
                        0,
                        false,
                        "ledger_unavailable");
                    results.Add(result);
                    if (result.Succeeded)
                    {
                        RecordAiHarvestSucceeded(request.WorkerDisplayName, result.ResourceId, result.AppliedQuantity);
                    }
                    else
                    {
                        OnHarvestFailed(request.WorkerId, request.WorkerDisplayName, request.ResourceId);
                    }
                }
            }
            finally
            {
                harvestPhase.Complete();
            }

            return results;
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

        public void CaptureMetrics()
        {
            MetricsTracker.Capture(
                SimulationTick,
                CurrentHour,
                CurrentWeatherName,
                Inventory.Items,
                Stockpile.Items,
                Workers,
                ActiveResourceSnapshots,
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

        public PrototypeRuntimeSnapshot CaptureSnapshot(Vector3 playerPosition)
        {
            if (!SupportsRuntimeSnapshotPersistence)
            {
                throw new InvalidOperationException(
                    "Runtime snapshot persistence for crisis scenarios is deferred until schema v7.");
            }

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
                SchemaVersion = 6,
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
                Resources = ResourceSnapshots.ToList(),
                Settlement = _settlementSimulation?.CaptureSnapshot(SimulationTick) ?? new PrototypeSettlementSnapshot()
            };
        }

        public void ApplySnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            if (!SupportsRuntimeSnapshotPersistence)
            {
                throw new InvalidDataException(
                    "Runtime snapshot persistence for crisis scenarios is deferred until schema v7.");
            }

            ValidateSnapshot(snapshot);
            if (!string.Equals(snapshot.ScenarioId, Scenario.Id, System.StringComparison.Ordinal) ||
                snapshot.SimulationTick < 0 || !float.IsFinite(snapshot.CurrentHour) || !float.IsFinite(snapshot.TimeUntilNextWeatherShift))
            {
                throw new InvalidDataException("Runtime snapshot metadata is malformed or targets a different scenario.");
            }

            WorldGenerationResult candidateWorld = PrototypeWorldGenerator.Regenerate(Scenario, snapshot.WorldSeed, snapshot.WorldGenerationAttempt);
            if (!string.Equals(candidateWorld.WorldHash, snapshot.WorldHash, System.StringComparison.Ordinal))
            {
                throw new InvalidDataException("Runtime snapshot world hash does not match the regenerated world.");
            }

            PrototypeResourceLedger candidateLedger = PrototypeResourceLedger.Restore(candidateWorld, snapshot);
            int derivedNavigationRulesVersion = 1 + snapshot.Settlement!.PathSegments.Count(segment => segment.IsBuilt);
            if (snapshot.SchemaVersion == 6 && snapshot.Settlement.NavigationRulesVersion != derivedNavigationRulesVersion)
            {
                throw new InvalidDataException(
                    $"Runtime snapshot navigation rules version {snapshot.Settlement.NavigationRulesVersion} does not match built path derivation {derivedNavigationRulesVersion}.");
            }
            PrototypeWeather candidateWeather = ParseWeatherStrict(snapshot.CurrentWeather);
            PrototypeWeatherSimulation candidateWeatherSimulation = new(snapshot.SimulationSeed, candidateWeather);
            candidateWeatherSimulation.SetState(candidateWeather, snapshot.TimeUntilNextWeatherShift, snapshot.WeatherRandomState);
            PrototypeSettlementSimulation candidateSettlement = new(
                Scenario,
                _roleQuotas,
                candidateWorld,
                orderSelectionMode: _orderSelectionMode,
                extractionPlanningMode: _extractionPlanningMode,
                routeDistanceMode: _routeDistanceMode);
            candidateSettlement.LoadState(snapshot.Settlement, derivedNavigationRulesVersion);

            _simulationSeed = snapshot.SimulationSeed;
            SimulationTick = snapshot.SimulationTick;
            CurrentHour = snapshot.CurrentHour;
            RunStartHour = snapshot.CurrentHour;
            _world = candidateWorld;
            _resourceLedger = candidateLedger;
            _weatherSimulation = candidateWeatherSimulation;
            _settlementSimulation = candidateSettlement;
            Inventory.ReplaceContents(snapshot.Inventory);
            Stockpile.ReplaceContents(snapshot.Stockpile);
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

        private static void ValidateSnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            if (snapshot.Inventory == null || snapshot.Stockpile == null || snapshot.Workers == null ||
                snapshot.Resources == null || snapshot.Settlement == null)
            {
                throw new InvalidDataException("Runtime snapshot required collections cannot be null.");
            }

            if (snapshot.WorldGenerationAttempt < 0 || snapshot.SimulationTick < 0 || !float.IsFinite(snapshot.CurrentHour) || snapshot.CurrentHour < 0.0f || snapshot.CurrentHour >= 24.0f ||
                !float.IsFinite(snapshot.TimeUntilNextWeatherShift) || snapshot.TimeUntilNextWeatherShift < 0.0f)
            {
                throw new InvalidDataException("Runtime snapshot core time and tick state is invalid.");
            }
            ValidateVector(snapshot.PlayerPosition, "player position");
            ValidateVector(snapshot.SettlementAnchorPosition, "settlement anchor position");

            ValidateCountMap(snapshot.Inventory, "inventory");
            ValidateCountMap(snapshot.Stockpile, "stockpile");

            PrototypeSettlementSnapshot settlement = snapshot.Settlement;
            if (settlement.CentralDepot == null || settlement.SiteCaches == null || settlement.Structures == null ||
                settlement.Citizens == null || settlement.PathSegments == null || settlement.RemoteDepots == null ||
                settlement.RouteHeatCells == null || settlement.BuildQueue == null || settlement.ProducedResources == null ||
                settlement.ConsumedResources == null || settlement.BlockedReasonCounts == null ||
                settlement.StructureCompletionTicks == null || settlement.LogisticsMetrics == null ||
                settlement.LogisticsMetrics.DepotThroughputByDepot == null || settlement.LogisticsMetrics.RouteBacklogTicksByKind == null)
            {
                throw new InvalidDataException("Runtime snapshot settlement contains a null required collection or state object.");
            }

            if (settlement.NavigationRulesVersion <= 0 || settlement.HearthLitTicks < 0 || settlement.TotalTicks < 0 ||
                settlement.TotalTicks != snapshot.SimulationTick || settlement.SelectedBuildQueueIndex < 0 ||
                (settlement.BuildQueue.Count == 0 ? settlement.SelectedBuildQueueIndex != 0 : settlement.SelectedBuildQueueIndex >= settlement.BuildQueue.Count) ||
                !Enum.TryParse(settlement.Classification, true, out PrototypeSettlementClassification classification) ||
                !Enum.IsDefined(typeof(PrototypeSettlementClassification), classification))
            {
                throw new InvalidDataException("Runtime snapshot settlement scalar or classification state is invalid.");
            }

            if (settlement.PathSegments.Any(item => item == null) ||
                settlement.RemoteDepots.Any(item => item == null) ||
                settlement.RouteHeatCells.Any(item => item == null) ||
                settlement.BuildQueue.Any(item => item == null))
            {
                throw new InvalidDataException("Runtime snapshot settlement contains a null collection element.");
            }

            ValidateStore(settlement.CentralDepot, "central depot");
            RequireUniqueIds(settlement.SiteCaches, store => store.StoreId, "site cache");
            foreach (PrototypeResourceStoreSnapshot store in settlement.SiteCaches)
            {
                ValidateStore(store, "site cache");
            }
            RequireUniqueIds(settlement.Structures, structure => structure.StructureId, "structure");
            foreach (PrototypeStructureSnapshot structure in settlement.Structures)
            {
                if (structure == null || structure.InputStore == null || structure.OutputStore == null)
                {
                    throw new InvalidDataException("Runtime snapshot structure or structure store cannot be null.");
                }
                if (structure.AssignedBeds < 0 || structure.BedCapacity < 0 || structure.ActiveTicks < 0 ||
                    structure.BlockedTicks < 0 || structure.HearthFuel < 0 || !float.IsFinite(structure.Progress) ||
                    structure.Progress < 0.0f || structure.Progress > 1.0f)
                {
                    throw new InvalidDataException($"Runtime snapshot structure '{structure.StructureId}' has invalid counters or progress.");
                }
                ValidateVector(structure.Position, $"structure '{structure.StructureId}' position");
                ValidateStore(structure.InputStore, $"structure '{structure.StructureId}' input store");
                ValidateStore(structure.OutputStore, $"structure '{structure.StructureId}' output store");
            }

            RequireUniqueIds(settlement.PathSegments, segment => segment.StructureId, "path segment");
            foreach (PrototypePathSegmentSnapshot segment in settlement.PathSegments)
            {
                if (segment.UtilizationCount < 0)
                {
                    throw new InvalidDataException($"Runtime snapshot path segment '{segment.StructureId}' has a negative utilization count.");
                }
                ValidateVector(segment.Position, $"path segment '{segment.StructureId}' position");
            }

            RequireUniqueIds(settlement.RemoteDepots, depot => depot.StructureId, "remote depot");
            foreach (PrototypeRemoteDepotSnapshot depot in settlement.RemoteDepots)
            {
                if (depot.ThroughputCount < 0 || !float.IsFinite(depot.DistanceToCentralDepot) || depot.DistanceToCentralDepot < 0.0f)
                {
                    throw new InvalidDataException($"Runtime snapshot remote depot '{depot.StructureId}' has invalid metrics.");
                }
                ValidateVector(depot.Position, $"remote depot '{depot.StructureId}' position");
            }

            HashSet<(int GridX, int GridY)> heatCells = new();
            foreach (PrototypeRouteHeatCellSnapshot heatCell in settlement.RouteHeatCells)
            {
                if (heatCell.UsageCount < 0 || !heatCells.Add((heatCell.GridX, heatCell.GridY)))
                {
                    throw new InvalidDataException("Runtime snapshot route heat contains a negative or duplicate cell.");
                }
                ValidateVector(heatCell.Position, "route heat position");
            }

            RequireUniqueIds(settlement.BuildQueue, entry => entry.EntryId, "build queue entry");

            ValidateCountMap(settlement.ProducedResources, "produced resources");
            ValidateCountMap(settlement.ConsumedResources, "consumed resources");
            ValidateCountMap(settlement.BlockedReasonCounts, "blocked reasons");
            ValidateCountMap(settlement.LogisticsMetrics.DepotThroughputByDepot, "depot throughput");
            ValidateCountMap(settlement.LogisticsMetrics.RouteBacklogTicksByKind, "route backlog");
            PrototypeLogisticsMetricsState logistics = settlement.LogisticsMetrics;
            if (logistics.CompletedRouteCount < 0 || logistics.TotalCompletedRouteTicks < 0 || logistics.TravelTicksAccumulated < 0 ||
                logistics.WorkTicksAccumulated < 0 || !float.IsFinite(logistics.TotalCompletedRouteDistanceMeters) ||
                logistics.TotalCompletedRouteDistanceMeters < 0.0f || !float.IsFinite(logistics.PathCoverageRatio) ||
                logistics.PathCoverageRatio < 0.0f || logistics.PathCoverageRatio > 1.0f)
            {
                throw new InvalidDataException("Runtime snapshot logistics metrics contain invalid counters or ratios.");
            }
            if (settlement.StructureCompletionTicks.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
            {
                throw new InvalidDataException("Runtime snapshot structure completion ticks contain an invalid entry.");
            }

            Dictionary<string, PrototypeWorkerSnapshot> topWorkers = IndexWorkers(snapshot.Workers, "top-level workers");
            Dictionary<string, PrototypeWorkerSnapshot> citizens = IndexWorkers(settlement.Citizens, "settlement citizens");
            if (!topWorkers.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(citizens.Keys))
            {
                throw new InvalidDataException("Runtime snapshot top-level workers and settlement citizens must have identical worker ids.");
            }

            foreach ((string workerId, PrototypeWorkerSnapshot worker) in topWorkers)
            {
                PrototypeWorkerSnapshot citizen = citizens[workerId];
                if (!string.Equals(JsonSerializer.Serialize(worker), JsonSerializer.Serialize(citizen), StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Runtime snapshot mirrored worker '{workerId}' is inconsistent.");
                }
            }
        }

        private static Dictionary<string, PrototypeWorkerSnapshot> IndexWorkers(
            IEnumerable<PrototypeWorkerSnapshot> workers,
            string label)
        {
            Dictionary<string, PrototypeWorkerSnapshot> indexed = new(StringComparer.Ordinal);
            foreach (PrototypeWorkerSnapshot worker in workers)
            {
                if (worker == null || string.IsNullOrWhiteSpace(worker.WorkerId) || worker.RecentEvents == null || worker.RouteWaypoints == null ||
                    worker.RecentEvents.Any(entry => entry == null) ||
                    !indexed.TryAdd(worker.WorkerId, worker))
                {
                    throw new InvalidDataException($"Runtime snapshot {label} contain a null, malformed, or duplicate worker.");
                }
                if (!Enum.TryParse(worker.RoleId, true, out PrototypeCitizenRole role) || !Enum.IsDefined(typeof(PrototypeCitizenRole), role) ||
                    !Enum.TryParse(worker.Phase, true, out PrototypeWorkerPhase phase) || !Enum.IsDefined(typeof(PrototypeWorkerPhase), phase) ||
                    (!string.IsNullOrWhiteSpace(worker.CurrentOrderKind) &&
                     (!Enum.TryParse(worker.CurrentOrderKind, true, out PrototypeWorkOrderKind orderKind) || !Enum.IsDefined(typeof(PrototypeWorkOrderKind), orderKind))))
                {
                    throw new InvalidDataException($"Runtime snapshot worker '{worker.WorkerId}' contains an invalid role, phase, or order kind.");
                }
                if (worker.CarryAmount < 0 || worker.TicksRemaining < 0 || worker.PhaseDurationTicks < 0 || worker.HomeBedCapacity < 0 ||
                    worker.TravelTicksAccumulated < 0 || worker.WorkTicksAccumulated < 0 || worker.CurrentRouteTravelTicks < 0 ||
                    worker.CurrentWaypointIndex < 0 || worker.CachedRouteVersion < 0 || !float.IsFinite(worker.Nutrition) ||
                    worker.Nutrition < 0.0f || worker.Nutrition > 100.0f || !float.IsFinite(worker.Fatigue) ||
                    worker.Fatigue < 0.0f || worker.Fatigue > 100.0f || !float.IsFinite(worker.CurrentRouteLengthMeters) ||
                    worker.CurrentRouteLengthMeters < 0.0f || !float.IsFinite(worker.CurrentRouteCost) || worker.CurrentRouteCost < 0.0f ||
                    (worker.CarryAmount > 0 && string.IsNullOrWhiteSpace(worker.CarryItemId)))
                {
                    throw new InvalidDataException($"Runtime snapshot worker '{worker.WorkerId}' contains invalid counters, needs, carry, or route state.");
                }
                ValidateVector(worker.Position, $"worker '{worker.WorkerId}' position");
                ValidateVector(worker.HomePosition, $"worker '{worker.WorkerId}' home position");
                ValidateVector(worker.TargetPosition, $"worker '{worker.WorkerId}' target position");
                foreach (PrototypeSerializableVector3 waypoint in worker.RouteWaypoints)
                {
                    ValidateVector(waypoint, $"worker '{worker.WorkerId}' route waypoint");
                }
            }
            return indexed;
        }

        private static void ValidateStore(PrototypeResourceStoreSnapshot store, string label)
        {
            if (store == null || string.IsNullOrWhiteSpace(store.StoreId) || store.Items == null || store.Capacity < 0)
            {
                throw new InvalidDataException($"Runtime snapshot {label} is malformed.");
            }
            ValidateVector(store.Position, $"{label} position");
            ValidateCountMap(store.Items, label);
        }

        private static void ValidateCountMap(IReadOnlyDictionary<string, int> counts, string label)
        {
            if (counts.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
            {
                throw new InvalidDataException($"Runtime snapshot {label} contains a blank item id or negative count.");
            }
        }

        private static void ValidateVector(PrototypeSerializableVector3 vector, string label)
        {
            if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z))
            {
                throw new InvalidDataException($"Runtime snapshot {label} contains a non-finite component.");
            }
        }

        private static void RequireUniqueIds<T>(IEnumerable<T> items, Func<T, string> selectId, string label)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (T item in items)
            {
                if (item is null)
                {
                    throw new InvalidDataException($"Runtime snapshot {label} contains a null entry.");
                }
                string id = selectId(item);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    throw new InvalidDataException($"Runtime snapshot {label} ids must be nonblank and unique.");
                }
            }
        }

        private static PrototypeWeather ParseWeatherStrict(string weatherName)
        {
            if (string.Equals(weatherName, PrototypeWeatherService.GetName(PrototypeWeather.Clear), System.StringComparison.Ordinal))
            {
                return PrototypeWeather.Clear;
            }

            if (string.Equals(weatherName, PrototypeWeatherService.GetName(PrototypeWeather.Rain), System.StringComparison.Ordinal))
            {
                return PrototypeWeather.Rain;
            }

            throw new InvalidDataException($"Runtime snapshot weather '{weatherName}' is not a known exact weather value.");
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
        IReadOnlyList<PrototypeHarvestResult> HarvestResults,
        bool ShouldCaptureMetrics);
}
