using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Societies.Simulation
{
    public sealed partial class PrototypeSettlementSimulation
    {

        private const float CitizenTravelUnitsPerTick = 0.78f;
        private const int MinimumTravelTicks = 4;
        private const int HarvestTicks = 10;
        private const int DepositTicks = 4;
        private const int EatTicks = 10;
        private const int SleepTicks = 28;
        private const int HearthBurnIntervalTicks = 80;
        private const int PathBuildTicks = 6;

        private static readonly IReadOnlyDictionary<string, int> HutCost = new Dictionary<string, int>
        {
            ["timber"] = 6,
            ["thatch"] = 4
        };

        private static readonly IReadOnlyDictionary<string, int> StorehouseCost = new Dictionary<string, int>
        {
            ["timber"] = 8,
            ["brick"] = 6
        };

        private static readonly IReadOnlyDictionary<string, int> DryingRackCost = new Dictionary<string, int>
        {
            ["timber"] = 4,
            ["stone"] = 2
        };

        private static readonly IReadOnlyDictionary<string, int> KilnCost = new Dictionary<string, int>
        {
            ["timber"] = 4,
            ["stone"] = 4
        };

        private static readonly IReadOnlyDictionary<string, int> RemoteDepotCost = new Dictionary<string, int>
        {
            ["timber"] = 6,
            ["stone"] = 2
        };

        private readonly PrototypeScenarioDefinition _scenario;
        private readonly WorldGenerationResult _world;
        private readonly Dictionary<string, PrototypeResourceStoreState> _siteCaches = new(StringComparer.Ordinal);
        private readonly List<PrototypeStructureState> _structures = new();
        private readonly List<PrototypePathSegmentState> _pathSegments = new();
        private readonly List<PrototypeRemoteDepotState> _remoteDepots = new();
        private readonly List<PrototypeBuildQueueEntry> _buildQueue = new();
        private readonly List<PrototypeWorkerState> _citizens = new();
        private readonly Dictionary<string, int> _producedResources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _consumedResources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _blockedReasonCounts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _structureCompletionTicks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _resourceNodeClusterMap = new(StringComparer.Ordinal);
        private readonly Dictionary<PrototypePathCacheKey, PrototypePathPlan> _pathCache = new();
        private readonly Dictionary<string, int> _routeBacklogTicksByKind = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _depotThroughputByDepot = new(StringComparer.Ordinal);
        private readonly Dictionary<Vector2I, int> _pathHeatByCell = new();
        private readonly PrototypeResourceStoreState _centralDepot;
        private PrototypeNavigationGrid? _navigationGrid;
        private int _navigationRulesVersion = 1;
        private int _completedRouteCount;
        private float _completedRouteDistanceMeters;
        private int _completedRouteTravelTicks;
        private int _travelTicksAccumulated;
        private int _workTicksAccumulated;
        private int _selectedBuildQueueIndex;
        private int _hearthLitTicks;
        private int _totalTicks;
        private readonly bool _uncappedOrders;
        public PrototypeSettlementSimulation(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas,
            WorldGenerationResult world,
            bool uncappedOrders = false)
        {
            _scenario = scenario;
            _world = world;
            _uncappedOrders = uncappedOrders;
            _centralDepot = CreateStore("central_depot", "Central Depot", 120, GetStructurePosition("central_depot", 0));
            SeedStartingStock();
            InitializeSiteCaches();
            InitializeStructures();
            RebuildNavigation();
            InitializeInfrastructurePlans();
            RebuildNavigation();
            InitializeCitizens(roleQuotas);
            UpdateClassification();
        }
        public IReadOnlyList<PrototypeWorkerState> Workers => _citizens;

        public IReadOnlyList<PrototypeWorkerState> Citizens => _citizens;

        public IReadOnlyList<PrototypeStructureState> Structures => _structures;

        public IReadOnlyList<PrototypePathSegmentState> PathSegments => _pathSegments;

        public IReadOnlyList<PrototypeRemoteDepotState> RemoteDepots => _remoteDepots;

        public IReadOnlyList<PrototypeBuildQueueEntry> BuildQueue => _buildQueue;

        public IReadOnlyDictionary<string, int> ProducedResources => _producedResources;

        public IReadOnlyDictionary<string, int> ConsumedResources => _consumedResources;

        public IReadOnlyDictionary<string, int> BlockedReasonCounts => _blockedReasonCounts;

        public IReadOnlyDictionary<string, long> StructureCompletionTicks => _structureCompletionTicks;

        public IReadOnlyDictionary<string, int> RouteBacklogTicksByKind => _routeBacklogTicksByKind;

        public IReadOnlyDictionary<string, int> DepotThroughputByDepot => _depotThroughputByDepot;

        public IReadOnlyDictionary<Vector2I, int> PathHeatByCell => _pathHeatByCell;

        public PrototypeResourceStoreState CentralDepot => _centralDepot;

        public sealed class PrototypeSettlementDiagnosticsState
        {
            public int TotalTicksMeasured;
            public int WorkOrdersGenerated;
            public int WorkOrdersClaimed;
            public int WorkOrdersRemaining;
            public int PathPlanLookups;
            public int PathPlanCacheHits;
            public int CitizensEvaluated;
            public int PeakOrdersThisSession;
        }

        private readonly PrototypeSettlementDiagnosticsState _diagnostics = new();
        private int _workOrdersGeneratedThisTick;
        private int _workOrdersRemainingAfterAssignment;
        private int _pathPlanLookupsThisTick;
        private int _pathPlanCacheHitsThisTick;
        private int _citizensEvaluatedThisTick;

        public PrototypeSettlementDiagnosticsState Diagnostics => _diagnostics;

        public PrototypeSettlementClassification Classification { get; private set; } = PrototypeSettlementClassification.Strained;

        public int HearthLitTicks => _hearthLitTicks;

        public int TotalTicks => _totalTicks;

        public int SelectedBuildQueueIndex => _selectedBuildQueueIndex;

        public string SelectedBuildQueueStatusText
        {
            get
            {
                if (_buildQueue.Count == 0)
                {
                    return "Build Queue: empty";
                }

                PrototypeBuildQueueEntry entry = _buildQueue[Mathf.Clamp(_selectedBuildQueueIndex, 0, _buildQueue.Count - 1)];
                string state = entry.IsCompleted
                    ? "complete"
                    : entry.IsPaused ? "paused" : "active";
                return $"Build Queue Focus: {entry.DisplayName} ({state})";
            }
        }

        public int BedCapacity => _structures.Where(structure => structure.IsBuilt).Sum(structure => structure.BedCapacity);

        public int BedCoveragePercent => _citizens.Count == 0
            ? 100
            : Mathf.RoundToInt(Mathf.Clamp(BedCapacity / (float)_citizens.Count, 0.0f, 1.0f) * 100.0f);

        public int MealCoveragePercent => _citizens.Count == 0
            ? 100
            : Mathf.RoundToInt(Mathf.Clamp(_centralDepot.GetCount("meals") / (float)_citizens.Count, 0.0f, 1.0f) * 100.0f);

        public int HearthFuel => GetStructure("central_hearth_1")?.HearthFuel ?? 0;

        public float AverageRouteLengthMeters => _completedRouteCount <= 0
            ? 0.0f
            : _completedRouteDistanceMeters / _completedRouteCount;

        public float AverageTravelWorkRatio => _workTicksAccumulated <= 0
            ? _travelTicksAccumulated
            : _travelTicksAccumulated / (float)_workTicksAccumulated;

        public float PathCoverageRatio
        {
            get
            {
                int traversableCells = _world.WorldMap.Cells.Count(cell => cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f);
                if (traversableCells <= 0)
                {
                    return 0.0f;
                }

                int builtPathCells = _pathSegments.Count(segment => segment.IsBuilt);
                return builtPathCells / (float)traversableCells;
            }
        }

        public IReadOnlyList<PrototypeResourceStoreState> SiteCaches => _siteCaches.Values.OrderBy(store => store.StoreId).ToList();
        public PrototypeSettlementTickResult Advance(
            IReadOnlyList<PrototypeResourceSiteState> resources,
            float currentHour,
            PrototypeWeather weather)
        {
            PrototypeSettlementTickResult result = new();
            _totalTicks++;

            _workOrdersGeneratedThisTick = 0;
            _workOrdersRemainingAfterAssignment = 0;
            _pathPlanLookupsThisTick = 0;
            _pathPlanCacheHitsThisTick = 0;
            _citizensEvaluatedThisTick = 0;

            foreach (PrototypeResourceSiteState resource in resources)
            {
                if (!_resourceNodeClusterMap.ContainsKey(resource.NodeName))
                {
                    _resourceNodeClusterMap[resource.NodeName] = resource.ClusterId;
                }
            }

            ApplyEnvironmentalUpkeep(currentHour, weather, result);
            UpdateStructureStates();
            EnsureDynamicInfrastructurePlans();

            foreach (PrototypeWorkerState citizen in _citizens)
            {
                AdvanceCitizenNeeds(citizen, currentHour, weather, result);
            }

            List<PrototypeWorkOrder> availableOrders = BuildWorkOrders(resources, currentHour, weather);
            _workOrdersGeneratedThisTick = availableOrders.Count;
            UpdateRouteBacklogMetrics(availableOrders);

            foreach (PrototypeWorkerState citizen in _citizens.OrderBy(candidate => candidate.WorkerId, StringComparer.Ordinal))
            {
                AdvanceCitizen(citizen, resources, currentHour, weather, result, availableOrders);
            }

            _workOrdersRemainingAfterAssignment = availableOrders.Count;
            _diagnostics.TotalTicksMeasured++;
            _diagnostics.WorkOrdersGenerated = _workOrdersGeneratedThisTick;
            _diagnostics.WorkOrdersClaimed = _workOrdersGeneratedThisTick - availableOrders.Count;
            _diagnostics.WorkOrdersRemaining = _workOrdersRemainingAfterAssignment;
            _diagnostics.PathPlanLookups = _pathPlanLookupsThisTick;
            _diagnostics.PathPlanCacheHits = _pathPlanCacheHitsThisTick;
            _diagnostics.CitizensEvaluated = _citizensEvaluatedThisTick;
            if (_workOrdersGeneratedThisTick > _diagnostics.PeakOrdersThisSession)
            {
                _diagnostics.PeakOrdersThisSession = _workOrdersGeneratedThisTick;
            }

            UpdateClassification();
            return result;
        }
        public void OnHarvestFailed(string workerId)
        {
            PrototypeWorkerState? citizen = _citizens.FirstOrDefault(candidate => candidate.WorkerId == workerId);
            if (citizen == null)
            {
                return;
            }

            citizen.LastFailureReason = "harvest.failed";
            ClearCitizenCarry(citizen);
            BeginIdle(citizen, "Harvest failed");
        }
        public void CopyStockpileTo(InventoryComponent stockpileView)
        {
            stockpileView.ReplaceContents(BuildSettlementSummary());
        }
        public PrototypeSettlementSnapshot CaptureSnapshot(long simulationTick)
        {
            return new PrototypeSettlementSnapshot
            {
                CentralDepot = CaptureStore(_centralDepot),
                SiteCaches = _siteCaches.Values
                    .OrderBy(store => store.StoreId, StringComparer.Ordinal)
                    .Select(CaptureStore)
                    .ToList(),
                Structures = _structures
                    .OrderBy(structure => structure.StructureId, StringComparer.Ordinal)
                    .Select(CaptureStructure)
                    .ToList(),
                Citizens = _citizens
                    .OrderBy(citizen => citizen.WorkerId, StringComparer.Ordinal)
                    .Select(CaptureCitizen)
                    .ToList(),
                PathSegments = _pathSegments
                    .OrderBy(segment => segment.StructureId, StringComparer.Ordinal)
                    .Select(segment => new PrototypePathSegmentSnapshot
                    {
                        StructureId = segment.StructureId,
                        CorridorId = segment.CorridorId,
                        GridX = segment.GridX,
                        GridY = segment.GridY,
                        Position = PrototypeSerializableVector3.FromVector3(segment.Position),
                        IsBuilt = segment.IsBuilt,
                        UtilizationCount = segment.UtilizationCount
                    })
                    .ToList(),
                RemoteDepots = _remoteDepots
                    .OrderBy(depot => depot.StructureId, StringComparer.Ordinal)
                    .Select(depot => new PrototypeRemoteDepotSnapshot
                    {
                        StructureId = depot.StructureId,
                        ClusterId = depot.ClusterId,
                        ResourceId = depot.ResourceId,
                        Position = PrototypeSerializableVector3.FromVector3(depot.Position),
                        GridX = depot.GridX,
                        GridY = depot.GridY,
                        IsBuilt = depot.IsBuilt,
                        DistanceToCentralDepot = depot.DistanceToCentralDepot,
                        ThroughputCount = depot.ThroughputCount
                    })
                    .ToList(),
                RouteHeatCells = _pathHeatByCell
                    .OrderBy(pair => pair.Key.X)
                    .ThenBy(pair => pair.Key.Y)
                    .Select(pair => new PrototypeRouteHeatCellSnapshot
                    {
                        GridX = pair.Key.X,
                        GridY = pair.Key.Y,
                        Position = PrototypeSerializableVector3.FromVector3(_world.WorldMap.GetCell(pair.Key.X, pair.Key.Y).WorldPosition),
                        UsageCount = pair.Value
                    })
                    .ToList(),
                BuildQueue = _buildQueue
                    .OrderBy(entry => entry.Priority)
                    .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
                    .Select(entry => new PrototypeBuildQueueEntrySnapshot
                    {
                        EntryId = entry.EntryId,
                        StructureKindId = entry.StructureKindId,
                        DisplayName = entry.DisplayName,
                        IsPaused = entry.IsPaused,
                        IsCompleted = entry.IsCompleted,
                        Priority = entry.Priority,
                        StructureId = entry.StructureId
                    })
                    .ToList(),
                ProducedResources = new Dictionary<string, int>(_producedResources, StringComparer.Ordinal),
                ConsumedResources = new Dictionary<string, int>(_consumedResources, StringComparer.Ordinal),
                BlockedReasonCounts = new Dictionary<string, int>(_blockedReasonCounts, StringComparer.Ordinal),
                StructureCompletionTicks = new Dictionary<string, long>(_structureCompletionTicks, StringComparer.Ordinal),
                SelectedBuildQueueIndex = _selectedBuildQueueIndex,
                HearthLitTicks = _hearthLitTicks,
                TotalTicks = _totalTicks,
                Classification = Classification.ToString().ToLowerInvariant(),
                LogisticsMetrics = new PrototypeLogisticsMetricsState
                {
                    CompletedRouteCount = _completedRouteCount,
                    TotalCompletedRouteDistanceMeters = _completedRouteDistanceMeters,
                    TotalCompletedRouteTicks = _completedRouteTravelTicks,
                    TravelTicksAccumulated = _travelTicksAccumulated,
                    WorkTicksAccumulated = _workTicksAccumulated,
                    PathCoverageRatio = PathCoverageRatio,
                    DepotThroughputByDepot = new Dictionary<string, int>(_depotThroughputByDepot, StringComparer.Ordinal),
                    RouteBacklogTicksByKind = new Dictionary<string, int>(_routeBacklogTicksByKind, StringComparer.Ordinal)
                }
            };
        }
        public void LoadState(PrototypeSettlementSnapshot snapshot)
        {
            RestoreStore(_centralDepot, snapshot.CentralDepot);

            _siteCaches.Clear();
            foreach (PrototypeResourceStoreSnapshot storeSnapshot in snapshot.SiteCaches)
            {
                PrototypeResourceStoreState store = RestoreStoreSnapshot(storeSnapshot);
                _siteCaches[store.StoreId] = store;
            }

            _structures.Clear();
            _pathSegments.Clear();
            _remoteDepots.Clear();
            foreach (PrototypeStructureSnapshot structureSnapshot in snapshot.Structures)
            {
                _structures.Add(new PrototypeStructureState
                {
                    StructureId = structureSnapshot.StructureId,
                    StructureKindId = structureSnapshot.StructureKindId,
                    DisplayName = structureSnapshot.DisplayName,
                    Position = structureSnapshot.Position.ToVector3(),
                    GridX = structureSnapshot.GridX,
                    GridY = structureSnapshot.GridY,
                    CorridorId = structureSnapshot.CorridorId,
                    LinkedClusterId = structureSnapshot.LinkedClusterId,
                    IsBuilt = structureSnapshot.IsBuilt,
                    IsBlocked = structureSnapshot.IsBlocked,
                    BlockedReason = structureSnapshot.BlockedReason,
                    AssignedBeds = structureSnapshot.AssignedBeds,
                    BedCapacity = structureSnapshot.BedCapacity,
                    Progress = structureSnapshot.Progress,
                    ActiveTicks = structureSnapshot.ActiveTicks,
                    BlockedTicks = structureSnapshot.BlockedTicks,
                    InputStore = RestoreStoreSnapshot(structureSnapshot.InputStore),
                    OutputStore = RestoreStoreSnapshot(structureSnapshot.OutputStore),
                    HearthFuel = structureSnapshot.HearthFuel
                });
            }

            foreach (PrototypePathSegmentSnapshot segmentSnapshot in snapshot.PathSegments)
            {
                _pathSegments.Add(new PrototypePathSegmentState
                {
                    StructureId = segmentSnapshot.StructureId,
                    CorridorId = segmentSnapshot.CorridorId,
                    GridX = segmentSnapshot.GridX,
                    GridY = segmentSnapshot.GridY,
                    Position = segmentSnapshot.Position.ToVector3(),
                    IsBuilt = segmentSnapshot.IsBuilt,
                    UtilizationCount = segmentSnapshot.UtilizationCount
                });
            }

            foreach (PrototypeRemoteDepotSnapshot depotSnapshot in snapshot.RemoteDepots)
            {
                _remoteDepots.Add(new PrototypeRemoteDepotState
                {
                    StructureId = depotSnapshot.StructureId,
                    ClusterId = depotSnapshot.ClusterId,
                    ResourceId = depotSnapshot.ResourceId,
                    Position = depotSnapshot.Position.ToVector3(),
                    GridX = depotSnapshot.GridX,
                    GridY = depotSnapshot.GridY,
                    IsBuilt = depotSnapshot.IsBuilt,
                    DistanceToCentralDepot = depotSnapshot.DistanceToCentralDepot,
                    ThroughputCount = depotSnapshot.ThroughputCount
                });
            }

            _pathHeatByCell.Clear();
            foreach (PrototypeRouteHeatCellSnapshot heatCell in snapshot.RouteHeatCells)
            {
                _pathHeatByCell[new Vector2I(heatCell.GridX, heatCell.GridY)] = heatCell.UsageCount;
            }

            _buildQueue.Clear();
            foreach (PrototypeBuildQueueEntrySnapshot entrySnapshot in snapshot.BuildQueue)
            {
                _buildQueue.Add(new PrototypeBuildQueueEntry
                {
                    EntryId = entrySnapshot.EntryId,
                    StructureKindId = entrySnapshot.StructureKindId,
                    DisplayName = entrySnapshot.DisplayName,
                    IsPaused = entrySnapshot.IsPaused,
                    IsCompleted = entrySnapshot.IsCompleted,
                    Priority = entrySnapshot.Priority,
                    StructureId = entrySnapshot.StructureId
                });
            }

            _citizens.Clear();
            foreach (PrototypeWorkerSnapshot citizenSnapshot in snapshot.Citizens)
            {
                _citizens.Add(RestoreCitizen(citizenSnapshot));
            }

            ReplaceCounts(_producedResources, snapshot.ProducedResources);
            ReplaceCounts(_consumedResources, snapshot.ConsumedResources);
            ReplaceCounts(_blockedReasonCounts, snapshot.BlockedReasonCounts);
            _structureCompletionTicks.Clear();
            foreach ((string key, long value) in snapshot.StructureCompletionTicks)
            {
                _structureCompletionTicks[key] = value;
            }

            _selectedBuildQueueIndex = Mathf.Clamp(snapshot.SelectedBuildQueueIndex, 0, Math.Max(_buildQueue.Count - 1, 0));
            _hearthLitTicks = snapshot.HearthLitTicks;
            _totalTicks = snapshot.TotalTicks;
            _completedRouteCount = snapshot.LogisticsMetrics.CompletedRouteCount;
            _completedRouteDistanceMeters = snapshot.LogisticsMetrics.TotalCompletedRouteDistanceMeters;
            _completedRouteTravelTicks = snapshot.LogisticsMetrics.TotalCompletedRouteTicks;
            _travelTicksAccumulated = snapshot.LogisticsMetrics.TravelTicksAccumulated;
            _workTicksAccumulated = snapshot.LogisticsMetrics.WorkTicksAccumulated;
            ReplaceCounts(_depotThroughputByDepot, snapshot.LogisticsMetrics.DepotThroughputByDepot);
            ReplaceCounts(_routeBacklogTicksByKind, snapshot.LogisticsMetrics.RouteBacklogTicksByKind);
            Classification = Enum.TryParse<PrototypeSettlementClassification>(snapshot.Classification, true, out PrototypeSettlementClassification parsed)
                ? parsed
                : PrototypeSettlementClassification.Strained;
            RebuildNavigation();
        }
        public bool SelectNextBuildQueueEntry()
        {
            if (_buildQueue.Count == 0)
            {
                return false;
            }

            _selectedBuildQueueIndex = (_selectedBuildQueueIndex + 1) % _buildQueue.Count;
            return true;
        }
        public bool ToggleSelectedBuildQueuePause()
        {
            if (_buildQueue.Count == 0)
            {
                return false;
            }

            PrototypeBuildQueueEntry entry = _buildQueue[Mathf.Clamp(_selectedBuildQueueIndex, 0, _buildQueue.Count - 1)];
            if (entry.IsCompleted)
            {
                return false;
            }

            entry.IsPaused = !entry.IsPaused;
            return true;
        }
        private PrototypeResourceStoreSnapshot CaptureStore(PrototypeResourceStoreState store)
        {
            return new PrototypeResourceStoreSnapshot
            {
                StoreId = store.StoreId,
                DisplayName = store.DisplayName,
                Capacity = store.Capacity,
                Position = PrototypeSerializableVector3.FromVector3(store.Position),
                LinkedClusterId = store.LinkedClusterId,
                Items = new Dictionary<string, int>(store.Items, StringComparer.Ordinal)
            };
        }
        private PrototypeStructureSnapshot CaptureStructure(PrototypeStructureState structure)
        {
            return new PrototypeStructureSnapshot
            {
                StructureId = structure.StructureId,
                StructureKindId = structure.StructureKindId,
                DisplayName = structure.DisplayName,
                Position = PrototypeSerializableVector3.FromVector3(structure.Position),
                GridX = structure.GridX,
                GridY = structure.GridY,
                CorridorId = structure.CorridorId,
                LinkedClusterId = structure.LinkedClusterId,
                IsBuilt = structure.IsBuilt,
                IsBlocked = structure.IsBlocked,
                BlockedReason = structure.BlockedReason,
                AssignedBeds = structure.AssignedBeds,
                BedCapacity = structure.BedCapacity,
                Progress = structure.Progress,
                ActiveTicks = structure.ActiveTicks,
                BlockedTicks = structure.BlockedTicks,
                InputStore = CaptureStore(structure.InputStore),
                OutputStore = CaptureStore(structure.OutputStore),
                HearthFuel = structure.HearthFuel
            };
        }
        private static void ReplaceCounts<TKey>(IDictionary<TKey, int> destination, IReadOnlyDictionary<TKey, int> source) where TKey : notnull
        {
            destination.Clear();
            foreach ((TKey key, int value) in source)
            {
                destination[key] = value;
            }
        }
        private static void IncrementCount(Dictionary<string, int> counts, string key, int amount)
        {
            counts[key] = counts.GetValueOrDefault(key) + amount;
        }
        private void UpdateClassification()
        {
            int criticalCitizens = _citizens.Count(citizen => citizen.Phase == PrototypeWorkerPhase.Incapacitated || citizen.Needs.IsNutritionCritical);
            bool stableFood = _centralDepot.GetCount("meals") >= _citizens.Count / 2;
            bool stableFuel = HearthFuel >= Math.Max(2, _citizens.Count / 3);
            bool stableBeds = BedCoveragePercent >= 50;
            int haulBacklog = _routeBacklogTicksByKind
                .Where(pair => pair.Key.Contains("haul", StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (criticalCitizens >= Math.Max(2, (_citizens.Count + 1) / 2))
            {
                Classification = PrototypeSettlementClassification.Collapsed;
                return;
            }

            if (stableFood && stableFuel && stableBeds && AverageTravelWorkRatio <= 1.35f && haulBacklog < 160)
            {
                Classification = PrototypeSettlementClassification.Stable;
                return;
            }

            Classification = PrototypeSettlementClassification.Strained;
        }

    }
}
