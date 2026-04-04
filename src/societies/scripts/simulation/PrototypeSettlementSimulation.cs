using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// Deterministic M3 settlement simulation with route-aware logistics, path infrastructure,
    /// remote depots, and basic nutrition/fatigue pressure.
    /// </summary>
    public sealed class PrototypeSettlementSimulation
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

        public PrototypeSettlementSimulation(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas,
            WorldGenerationResult world)
        {
            _scenario = scenario;
            _world = world;
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
            UpdateRouteBacklogMetrics(availableOrders);

            foreach (PrototypeWorkerState citizen in _citizens.OrderBy(candidate => candidate.WorkerId, StringComparer.Ordinal))
            {
                AdvanceCitizen(citizen, resources, currentHour, weather, result, availableOrders);
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

        private void SeedStartingStock()
        {
            foreach ((string itemId, int amount) in _scenario.StartingStock)
            {
                if (amount > 0)
                {
                    _centralDepot.Add(itemId, amount);
                }
            }
        }

        private void InitializeSiteCaches()
        {
            foreach (ResourceClusterState cluster in _world.ResourceClusters)
            {
                PrototypeResourceStoreState cache = CreateStore(
                    $"cache.{cluster.ClusterId}",
                    $"{InventoryComponent.FormatItemName(cluster.ResourceId)} Cache",
                    18,
                    cluster.CenterPosition,
                    cluster.ResourceId);
                cache.LinkedClusterId = cluster.ClusterId;
                _siteCaches[cache.StoreId] = cache;
            }
        }

        private void InitializeStructures()
        {
            int hutIndex = 0;

            foreach (string structureKindId in _scenario.StartingStructures)
            {
                PrototypeStructureState structure = CreateStructure(structureKindId, structureKindId == "hut" ? hutIndex++ : 0, isBuilt: true);
                _structures.Add(structure);
            }

            foreach ((string structureKindId, int queueIndex) in _scenario.StartingBuildQueue.Select((value, index) => (value, index)))
            {
                int structureIndex = structureKindId == "hut"
                    ? hutIndex++
                    : _structures.Count(structure => structure.StructureKindId == structureKindId);

                PrototypeStructureState structure = CreateStructure(structureKindId, structureIndex, isBuilt: false);
                _structures.Add(structure);
                _buildQueue.Add(new PrototypeBuildQueueEntry
                {
                    EntryId = $"build_{queueIndex + 1}",
                    StructureKindId = structureKindId,
                    DisplayName = structure.DisplayName,
                    Priority = queueIndex,
                    StructureId = structure.StructureId
                });
            }
        }

        private void InitializeInfrastructurePlans()
        {
            EnsureRemoteDepotPlans();
            EnsurePriorityPathPlans();
        }

        private void EnsureRemoteDepotPlans()
        {
            foreach (ResourceClusterState cluster in _world.ResourceClusters
                .Where(candidate => candidate.DistanceFromSettlement > GetRemoteDepotActivationDistance() && candidate.ResourceId is "logs" or "stone" or "clay" or "reeds")
                .OrderByDescending(candidate => candidate.DistanceFromSettlement))
            {
                if (_remoteDepots.Any(depot => string.Equals(depot.ClusterId, cluster.ClusterId, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!TryFindRemoteDepotPlacement(cluster, out TerrainCell? placementCell))
                {
                    continue;
                }

                int depotIndex = _remoteDepots.Count + 1;
                string structureId = $"remote_stockpile_{depotIndex}";
                Vector3 position = placementCell!.WorldPosition;
                PrototypeStructureState structure = new()
                {
                    StructureId = structureId,
                    StructureKindId = "remote_stockpile",
                    DisplayName = $"Remote Depot {depotIndex}",
                    Position = position,
                    GridX = placementCell.GridX,
                    GridY = placementCell.GridY,
                    LinkedClusterId = cluster.ClusterId,
                    InputStore = CreateStore($"{structureId}.input", $"Remote Depot {depotIndex} Input", 32, position),
                    OutputStore = CreateStore($"{structureId}.output", $"Remote Depot {depotIndex} Stock", 42, position),
                    IsBuilt = false
                };

                structure.OutputStore.LinkedClusterId = cluster.ClusterId;
                _structures.Add(structure);
                _remoteDepots.Add(new PrototypeRemoteDepotState
                {
                    StructureId = structureId,
                    ClusterId = cluster.ClusterId,
                    ResourceId = cluster.ResourceId,
                    Position = position,
                    GridX = placementCell.GridX,
                    GridY = placementCell.GridY,
                    IsBuilt = false,
                    DistanceToCentralDepot = cluster.DistanceFromSettlement
                });

                _buildQueue.Add(new PrototypeBuildQueueEntry
                {
                    EntryId = $"remote_depot_{depotIndex}",
                    StructureKindId = "remote_stockpile",
                    DisplayName = structure.DisplayName,
                    Priority = _buildQueue.Count + depotIndex,
                    StructureId = structureId
                });
            }
        }

        private void EnsurePriorityPathPlans()
        {
            List<ResourceClusterState> targets = new();

            ResourceClusterState? food = _world.ResourceClusters
                .Where(cluster => cluster.ResourceId == "berries")
                .OrderBy(cluster => cluster.DistanceFromSettlement)
                .FirstOrDefault();
            if (food != null)
            {
                targets.Add(food);
            }

            ResourceClusterState? logs = _world.ResourceClusters
                .Where(cluster => cluster.ResourceId == "logs")
                .OrderBy(cluster => cluster.DistanceFromSettlement)
                .FirstOrDefault();
            if (logs != null)
            {
                targets.Add(logs);
            }

            ResourceClusterState? stoneOrClay = _world.ResourceClusters
                .Where(cluster => cluster.ResourceId is "stone" or "clay")
                .OrderBy(cluster => cluster.DistanceFromSettlement)
                .FirstOrDefault();
            if (stoneOrClay != null)
            {
                targets.Add(stoneOrClay);
            }

            foreach ((ResourceClusterState target, int index) in targets
                .DistinctBy(cluster => cluster.ClusterId)
                .Take(GetPathCorridorBudget())
                .Select((cluster, idx) => (cluster, idx)))
            {
                EnsurePathCorridor($"corridor.{target.ResourceId}", target.CenterPosition, $"Path to {InventoryComponent.FormatItemName(target.ResourceId)}", index);
            }

            foreach ((PrototypeRemoteDepotState depot, int index) in _remoteDepots.Select((value, idx) => (value, idx)))
            {
                EnsurePathCorridor($"corridor.depot.{depot.ClusterId}", depot.Position, $"Path to {InventoryComponent.FormatItemName(depot.ResourceId)} depot", index + targets.Count);
            }
        }

        private void EnsurePathCorridor(string corridorId, Vector3 destination, string displayName, int priorityOffset)
        {
            if (_pathSegments.Any(segment => string.Equals(segment.CorridorId, corridorId, StringComparison.Ordinal)))
            {
                return;
            }

            PrototypePathPlan corridorPlan = FindPathPlan(_world.SettlementSpawn.AnchorPosition, destination);
            int basePriority = _buildQueue.Count + priorityOffset + 10;
            int structureIndex = _pathSegments.Count;

            foreach (Vector2I cell in corridorPlan.Cells.Skip(1).SkipLast(1))
            {
                TerrainCell terrainCell = _world.WorldMap.GetCell(cell.X, cell.Y);
                string structureId = $"path_segment_{structureIndex + 1}";
                PrototypeStructureState structure = new()
                {
                    StructureId = structureId,
                    StructureKindId = "path_segment",
                    DisplayName = displayName,
                    Position = terrainCell.WorldPosition,
                    GridX = cell.X,
                    GridY = cell.Y,
                    CorridorId = corridorId,
                    IsBuilt = false,
                    InputStore = CreateStore($"{structureId}.input", $"{displayName} Input", 0, terrainCell.WorldPosition),
                    OutputStore = CreateStore($"{structureId}.output", $"{displayName} Output", 0, terrainCell.WorldPosition)
                };

                _structures.Add(structure);
                _pathSegments.Add(new PrototypePathSegmentState
                {
                    StructureId = structureId,
                    CorridorId = corridorId,
                    GridX = cell.X,
                    GridY = cell.Y,
                    Position = terrainCell.WorldPosition,
                    IsBuilt = false
                });
                _buildQueue.Add(new PrototypeBuildQueueEntry
                {
                    EntryId = $"path_{structureIndex + 1}",
                    StructureKindId = "path_segment",
                    DisplayName = displayName,
                    Priority = basePriority + structureIndex,
                    StructureId = structureId
                });

                structureIndex++;
            }
        }

        private bool TryFindRemoteDepotPlacement(ResourceClusterState cluster, out TerrainCell? placementCell)
        {
            placementCell = _world.WorldMap.Cells
                .Where(cell => cell.IsBuildable && cell.Biome != BiomeType.Wetland)
                .Where(cell => cell.WorldPosition.DistanceTo(cluster.CenterPosition) <= GetRemoteDepotPlacementRadius())
                .OrderBy(cell => cell.WorldPosition.DistanceTo(cluster.CenterPosition))
                .ThenBy(cell => cell.SlopeDegrees)
                .FirstOrDefault();

            return placementCell != null;
        }

        private void InitializeCitizens(IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas)
        {
            List<PrototypeCitizenRole> seededRoles = BuildRolePlan(roleQuotas, _scenario.InitialCitizens);

            for (int index = 0; index < _scenario.InitialCitizens; index++)
            {
                Vector3 homePosition = ProjectToSurface(GetCitizenHomePosition(index, _scenario.InitialCitizens));
                PrototypeCitizenRole role = index < seededRoles.Count ? seededRoles[index] : PrototypeCitizenRole.Generalist;
                _citizens.Add(new PrototypeWorkerState
                {
                    WorkerId = $"citizen_{index + 1}",
                    DisplayName = $"Citizen {index + 1}",
                    Role = role,
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = 8,
                    PhaseDurationTicks = 8,
                    Position = homePosition,
                    HomePosition = homePosition,
                    TargetPosition = homePosition,
                    TargetLabel = "Settlement",
                    ActivityText = "Waiting for work",
                    HomeBedCapacity = 0,
                    Needs = new PrototypeNeedState
                    {
                        Nutrition = 72.0f + (index % 4) * 4.0f,
                        Fatigue = 12.0f + (index % 3) * 3.0f
                    }
                });
            }
        }

        private void AdvanceCitizenNeeds(
            PrototypeWorkerState citizen,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result)
        {
            if (citizen.Phase == PrototypeWorkerPhase.Incapacitated)
            {
                citizen.Needs.Nutrition = Mathf.Max(0.0f, citizen.Needs.Nutrition - 0.06f);
                citizen.Needs.Fatigue = Mathf.Min(100.0f, citizen.Needs.Fatigue + 0.02f);
                return;
            }

            citizen.Needs.Nutrition = Mathf.Max(0.0f, citizen.Needs.Nutrition - GetNutritionDecay(citizen.Phase));
            citizen.Needs.Fatigue = Mathf.Clamp(citizen.Needs.Fatigue + GetFatigueDelta(citizen.Phase, currentHour, weather), 0.0f, 100.0f);

            if (citizen.Needs.IsNutritionCritical && _centralDepot.GetCount("meals") == 0 && _centralDepot.GetCount("berries") == 0)
            {
                citizen.Phase = PrototypeWorkerPhase.Incapacitated;
                citizen.ActivityText = "Starving";
                citizen.LastFailureReason = "food.shortage";
                AddRecentEvent(citizen, "Food critical");
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.NeedCritical,
                    $"{citizen.DisplayName} became nonproductive because no food was available"));
            }
        }

        private void ApplyEnvironmentalUpkeep(float currentHour, PrototypeWeather weather, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? hearth = GetStructure("central_hearth_1");
            if (hearth == null)
            {
                return;
            }

            if (_totalTicks % HearthBurnIntervalTicks == 0 && hearth.HearthFuel > 0)
            {
                hearth.HearthFuel = Math.Max(0, hearth.HearthFuel - 1);
                IncrementCount(_consumedResources, "firewood", 1);
            }

            if (hearth.HearthFuel > 0)
            {
                _hearthLitTicks++;
            }

            if ((weather == PrototypeWeather.Rain || IsNight(currentHour)) && hearth.HearthFuel <= 0)
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementShortage,
                    "Central hearth is unfueled during adverse conditions"));
            }
        }

        private void UpdateStructureStates()
        {
            foreach (PrototypeStructureState structure in _structures)
            {
                structure.IsBlocked = false;
                structure.BlockedReason = string.Empty;

                if (structure.StructureKindId == "remote_stockpile" && structure.IsBuilt)
                {
                    structure.OutputStore.Capacity = 64;
                }
            }

            if (GetStructure("storehouse_1")?.IsBuilt == true)
            {
                _centralDepot.Capacity = 220;
            }
        }

        private void AdvanceCitizen(
            PrototypeWorkerState citizen,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result,
            List<PrototypeWorkOrder> availableOrders)
        {
            TrackCitizenPhaseTick(citizen);

            switch (citizen.Phase)
            {
                case PrototypeWorkerPhase.MovingToResource:
                case PrototypeWorkerPhase.MovingToCache:
                case PrototypeWorkerPhase.MovingToDepot:
                case PrototypeWorkerPhase.MovingToStructure:
                    if (!AdvanceTravelPhase(citizen))
                    {
                        return;
                    }

                    ResolveMovementArrival(citizen, result);
                    return;

                case PrototypeWorkerPhase.Harvesting:
                case PrototypeWorkerPhase.DepositingToCache:
                case PrototypeWorkerPhase.DepositingToDepot:
                case PrototypeWorkerPhase.DepositingToStructure:
                case PrototypeWorkerPhase.Processing:
                case PrototypeWorkerPhase.Building:
                case PrototypeWorkerPhase.Refueling:
                case PrototypeWorkerPhase.Eating:
                case PrototypeWorkerPhase.Sleeping:
                    if (!AdvanceStationaryPhase(citizen, citizen.TargetPosition))
                    {
                        return;
                    }

                    ResolveStationaryCompletion(citizen, result);
                    return;

                case PrototypeWorkerPhase.Incapacitated:
                    citizen.TargetPosition = citizen.HomePosition;
                    citizen.Position = citizen.Position.MoveToward(citizen.HomePosition, CitizenTravelUnitsPerTick * 0.5f);
                    return;

                case PrototypeWorkerPhase.Idle:
                default:
                    citizen.Position = citizen.Position.MoveToward(citizen.HomePosition, CitizenTravelUnitsPerTick * 0.25f);
                    break;
            }

            if (TryAssignNeedDrivenOrder(citizen, currentHour, weather, result))
            {
                return;
            }

            PrototypeWorkOrder? order = availableOrders
                .OrderByDescending(candidate => ScoreOrder(citizen, candidate))
                .ThenBy(candidate => candidate.OrderId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (order == null)
            {
                BeginIdle(citizen, "Waiting for work");
                return;
            }

            availableOrders.Remove(order);
            BeginOrder(citizen, order, result);
        }

        private void ResolveMovementArrival(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            if (citizen.Navigation.CurrentRouteLengthMeters > 0.0f)
            {
                _completedRouteCount++;
                _completedRouteDistanceMeters += citizen.Navigation.CurrentRouteLengthMeters;
                _completedRouteTravelTicks += citizen.Navigation.CurrentRouteTravelTicks;
            }

            switch (citizen.CurrentOrderKind)
            {
                case PrototypeWorkOrderKind.Extract:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Harvesting,
                        HarvestTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Harvesting {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for depot hauling");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToDepot,
                            _centralDepot.Position,
                            "Central Depot",
                            $"Delivering {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToDepot,
                        DepositTicks,
                        _centralDepot.Position,
                        "Central Depot",
                        $"Depositing {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                    return;

                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for remote hauling");
                            return;
                        }

                        Vector3 destinationPosition = GetStorePosition(citizen.DestinationStoreId);
                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            destinationPosition,
                            citizen.TargetLabel,
                            $"Consolidating {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToStructure,
                        DepositTicks,
                        GetStorePosition(citizen.DestinationStoreId),
                        citizen.TargetLabel,
                        $"Stocking {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.HaulToStructure:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for structure hauling");
                            return;
                        }

                        PrototypeStructureState? destinationStructure = GetStructure(citizen.TargetStructureId);
                        if (destinationStructure == null)
                        {
                            FailCitizenOrder(citizen, "haul.structure.missing", result, $"{citizen.DisplayName} could not find the destination structure");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            destinationStructure.Position,
                            destinationStructure.DisplayName,
                            $"Carrying {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToStructure,
                        DepositTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Supplying {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.Process:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Processing,
                        GetProcessingTicks(citizen.TargetStructureId, citizen.CarryItemId),
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Working at {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Building,
                        GetBuildTicks(citizen.TargetStructureId),
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Building {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.RefuelHearth:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "fuel.source.empty", result, $"{citizen.DisplayName} could not collect firewood for the hearth");
                            return;
                        }

                        PrototypeStructureState? hearth = GetStructure(citizen.TargetStructureId);
                        if (hearth == null)
                        {
                            FailCitizenOrder(citizen, "fuel.hearth.missing", result, $"{citizen.DisplayName} could not find the hearth");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            hearth.Position,
                            hearth.DisplayName,
                            "Refueling the hearth");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Refueling,
                        DepositTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Refueling the hearth");
                    return;

                case PrototypeWorkOrderKind.Eat:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Eating,
                        EatTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Eating");
                    return;

                case PrototypeWorkOrderKind.Sleep:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Sleeping,
                        SleepTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Sleeping");
                    return;
            }
        }

        private void ResolveStationaryCompletion(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            switch (citizen.CurrentOrderKind)
            {
                case PrototypeWorkOrderKind.Extract when citizen.Phase == PrototypeWorkerPhase.Harvesting:
                    citizen.CarryItemId = InferHarvestItemFromNode(citizen.TargetResourceNodeName);
                    citizen.CarryAmount = 1;
                    result.HarvestRequests.Add(new PrototypeHarvestRequest(
                        citizen.WorkerId,
                        citizen.DisplayName,
                        citizen.TargetResourceNodeName,
                        citizen.CarryItemId,
                        1,
                        ExtractClusterId(citizen.TargetResourceNodeName)));
                    AddRecentEvent(citizen, $"Harvested {citizen.CarryItemId}");

                    PrototypeResourceStoreState? cache = ResolveCacheForCitizen(citizen);
                    if (cache == null)
                    {
                        FailCitizenOrder(citizen, "cache.missing", result, $"{citizen.DisplayName} could not find a site cache");
                        return;
                    }

                    BeginTravel(
                        citizen,
                        PrototypeWorkerPhase.MovingToCache,
                        cache.Position,
                        cache.DisplayName,
                        $"Carrying {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                    citizen.Phase = PrototypeWorkerPhase.MovingToCache;
                    return;

                case PrototypeWorkOrderKind.Extract:
                    PrototypeResourceStoreState? destinationCache = ResolveCacheForCitizen(citizen);
                    if (destinationCache == null || !destinationCache.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "cache.full", result, $"{citizen.DisplayName} could not deposit into the site cache");
                        return;
                    }

                    IncrementCount(_producedResources, citizen.CarryItemId, citizen.CarryAmount);
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementCacheDeposit,
                        $"{citizen.DisplayName} cached {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                    if (_centralDepot.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        if (!string.IsNullOrWhiteSpace(citizen.SourceStoreId) && citizen.SourceStoreId.Contains("remote_stockpile", StringComparison.Ordinal))
                        {
                            IncrementCount(_depotThroughputByDepot, citizen.SourceStoreId, citizen.CarryAmount);
                            PrototypeRemoteDepotState? sourceDepot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, citizen.TargetStructureId, StringComparison.Ordinal));
                            if (sourceDepot != null)
                            {
                                sourceDepot.ThroughputCount += citizen.CarryAmount;
                            }
                        }

                        result.Events.Add(new PrototypeSettlementEvent(
                            PrototypeEventTypes.SettlementHaulCompleted,
                            $"{citizen.DisplayName} delivered {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount} to the depot"));
                        ClearCitizenCarry(citizen);
                        BeginIdle(citizen, "Ready for work");
                        return;
                    }

                    FailCitizenOrder(citizen, "depot.full", result, $"{citizen.DisplayName} could not unload to the depot");
                    return;

                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                    PrototypeResourceStoreState? remoteDestination = GetStore(citizen.DestinationStoreId);
                    if (remoteDestination == null || !remoteDestination.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "remote.depot.full", result, $"{citizen.DisplayName} could not supply the remote depot");
                        return;
                    }

                    IncrementCount(_depotThroughputByDepot, citizen.DestinationStoreId, citizen.CarryAmount);
                    PrototypeRemoteDepotState? destinationDepot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, citizen.TargetStructureId, StringComparison.Ordinal));
                    if (destinationDepot != null)
                    {
                        destinationDepot.ThroughputCount += citizen.CarryAmount;
                    }
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementHaulCompleted,
                        $"{citizen.DisplayName} stocked {citizen.TargetLabel} with {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.HaulToStructure:
                    PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
                    if (structure == null || !structure.InputStore.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "structure.input.full", result, $"{citizen.DisplayName} could not supply {citizen.TargetLabel}");
                        return;
                    }

                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementStructureSupplied,
                        $"{citizen.DisplayName} supplied {citizen.TargetLabel} with {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Process:
                    if (!CompleteProcessing(citizen, result))
                    {
                        return;
                    }

                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                    if (!CompleteBuild(citizen, result))
                    {
                        return;
                    }

                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.RefuelHearth:
                    PrototypeStructureState? hearth = GetStructure(citizen.TargetStructureId);
                    if (hearth == null)
                    {
                        FailCitizenOrder(citizen, "fuel.hearth.missing", result, $"{citizen.DisplayName} could not refuel the hearth");
                        return;
                    }

                    hearth.HearthFuel += citizen.CarryAmount;
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementHearthRefueled,
                        $"{citizen.DisplayName} refueled the hearth with firewood x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Eat:
                    CompleteEating(citizen, result);
                    BeginIdle(citizen, "Fed");
                    return;

                case PrototypeWorkOrderKind.Sleep:
                    CompleteSleeping(citizen);
                    BeginIdle(citizen, "Rested");
                    return;
            }
        }

        private List<PrototypeWorkOrder> BuildWorkOrders(
            IReadOnlyList<PrototypeResourceSiteState> resources,
            float currentHour,
            PrototypeWeather weather)
        {
            Dictionary<string, int> committedCarries = _citizens
                .Where(citizen => citizen.CarryAmount > 0)
                .GroupBy(citizen => citizen.CarryItemId)
                .ToDictionary(group => group.Key, group => group.Sum(citizen => citizen.CarryAmount), StringComparer.Ordinal);

            List<PrototypeWorkOrder> orders = new();
            AddRefuelOrders(orders);
            AddHaulOrdersFromStores(orders);
            AddProductionOrders(orders);
            AddBuildOrders(orders);
            AddReserveExtractionOrders(orders, resources, committedCarries, currentHour, weather);
            return RemoveClaimedOrders(orders);
        }

        private void AddRefuelOrders(List<PrototypeWorkOrder> orders)
        {
            PrototypeStructureState? hearth = GetStructure("central_hearth_1");
            if (hearth == null)
            {
                return;
            }

            int desiredFuel = Math.Max(4, _citizens.Count / 2);
            int deficit = Math.Max(0, desiredFuel - hearth.HearthFuel);
            int depotFirewood = _centralDepot.GetCount("firewood");

            for (int index = 0; index < Math.Min(deficit, depotFirewood); index++)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"refuel_{index + 1}",
                    Kind = PrototypeWorkOrderKind.RefuelHearth,
                    Priority = 1200,
                    ResourceId = "firewood",
                    SourceStoreId = _centralDepot.StoreId,
                    StructureId = hearth.StructureId,
                    Label = hearth.DisplayName,
                    Reason = "hearth fuel reserve",
                    TargetPosition = _centralDepot.Position,
                    Amount = 1
                });
            }
        }

        private void AddHaulOrdersFromStores(List<PrototypeWorkOrder> orders)
        {
            foreach (PrototypeResourceStoreState cache in _siteCaches.Values.OrderBy(store => store.StoreId, StringComparer.Ordinal))
            {
                PrototypeStructureState? remoteDepot = GetRemoteDepotStructure(cache.LinkedClusterId, requireBuilt: true);

                foreach ((string itemId, int amount) in cache.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.cache.{cache.StoreId}.{itemId}.{index}",
                            Kind = remoteDepot == null ? PrototypeWorkOrderKind.HaulToDepot : PrototypeWorkOrderKind.HaulToRemoteDepot,
                            Priority = GetHaulPriority(itemId),
                            ResourceId = itemId,
                            SourceStoreId = cache.StoreId,
                            DestinationStoreId = remoteDepot?.OutputStore.StoreId ?? _centralDepot.StoreId,
                            StructureId = remoteDepot?.StructureId ?? string.Empty,
                            Label = remoteDepot?.DisplayName ?? "Central Depot",
                            Reason = remoteDepot == null ? "remote resource delivery" : "consolidate at remote depot",
                            TargetPosition = cache.Position,
                            Amount = 1
                        });
                    }
                }
            }

            foreach (PrototypeRemoteDepotState depot in _remoteDepots.Where(candidate => candidate.IsBuilt).OrderBy(candidate => candidate.StructureId, StringComparer.Ordinal))
            {
                PrototypeStructureState? structure = GetStructure(depot.StructureId);
                if (structure == null)
                {
                    continue;
                }

                foreach ((string itemId, int amount) in structure.OutputStore.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.remote.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulFromRemoteDepot,
                            Priority = GetHaulPriority(itemId) + 10,
                            ResourceId = itemId,
                            SourceStoreId = structure.OutputStore.StoreId,
                            DestinationStoreId = _centralDepot.StoreId,
                            StructureId = structure.StructureId,
                            Label = "Central Depot",
                            Reason = "remote depot transfer",
                            TargetPosition = structure.Position,
                            Amount = 1
                        });
                    }
                }
            }

            foreach (PrototypeStructureState structure in _structures.Where(structure => structure.IsBuilt))
            {
                if (structure.StructureKindId == "remote_stockpile")
                {
                    continue;
                }

                foreach ((string itemId, int amount) in structure.OutputStore.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.output.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulToDepot,
                            Priority = GetHaulPriority(itemId) + 20,
                            ResourceId = itemId,
                            SourceStoreId = structure.OutputStore.StoreId,
                            DestinationStoreId = _centralDepot.StoreId,
                            StructureId = structure.StructureId,
                            Label = "Central Depot",
                            Reason = $"collect {structure.DisplayName} output",
                            TargetPosition = structure.Position,
                            Amount = 1
                        });
                    }
                }
            }
        }

        private void AddProductionOrders(List<PrototypeWorkOrder> orders)
        {
            PrototypeStructureState? woodYard = _structures.FirstOrDefault(structure => structure.StructureKindId == "wood_yard" && structure.IsBuilt);
            if (woodYard != null)
            {
                AddWoodYardOrders(orders, woodYard);
            }

            PrototypeStructureState? cookfire = _structures.FirstOrDefault(structure => structure.StructureKindId == "cookfire" && structure.IsBuilt);
            if (cookfire != null)
            {
                AddCookfireOrders(orders, cookfire);
            }

            PrototypeStructureState? dryingRack = _structures.FirstOrDefault(structure => structure.StructureKindId == "drying_rack" && structure.IsBuilt);
            if (dryingRack != null)
            {
                AddProcessingOrders(orders, dryingRack, "reeds", 2, "thatch", 1, 780, "Turn reeds into thatch");
            }

            PrototypeStructureState? kiln = _structures.FirstOrDefault(structure => structure.StructureKindId == "kiln" && structure.IsBuilt);
            if (kiln != null)
            {
                AddKilnOrders(orders, kiln);
            }
        }

        private void AddWoodYardOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState woodYard)
        {
            int firewoodShortfall = Math.Max(0, GetFirewoodTarget() - (_centralDepot.GetCount("firewood") + woodYard.OutputStore.GetCount("firewood")));
            int timberNeed = GetPendingConstructionRequirement("timber") - (_centralDepot.GetCount("timber") + woodYard.OutputStore.GetCount("timber"));

            AddStoreSupplyOrders(orders, woodYard, "logs", Math.Max(4, firewoodShortfall + Math.Max(0, timberNeed)));

            if (woodYard.InputStore.GetCount("logs") > 0 && woodYard.OutputStore.AvailableCapacity > 0)
            {
                string outputId = firewoodShortfall > 0 ? "firewood" : "timber";
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{woodYard.StructureId}.{outputId}",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = firewoodShortfall > 0 ? 930 : 760,
                    ResourceId = outputId,
                    StructureId = woodYard.StructureId,
                    Label = woodYard.DisplayName,
                    Reason = firewoodShortfall > 0 ? "fuel shortage" : "construction lumber",
                    TargetPosition = woodYard.Position,
                    Amount = 1
                });
            }
        }

        private void AddCookfireOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState cookfire)
        {
            int mealShortfall = Math.Max(0, GetMealTarget() - (_centralDepot.GetCount("meals") + cookfire.OutputStore.GetCount("meals")));
            AddStoreSupplyOrders(orders, cookfire, "berries", Math.Max(2, mealShortfall * 2));
            AddStoreSupplyOrders(orders, cookfire, "firewood", Math.Max(1, mealShortfall));

            if (cookfire.InputStore.GetCount("berries") >= 2 &&
                cookfire.InputStore.GetCount("firewood") >= 1 &&
                cookfire.OutputStore.AvailableCapacity >= 2)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{cookfire.StructureId}.meals",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = 980,
                    ResourceId = "meals",
                    StructureId = cookfire.StructureId,
                    Label = cookfire.DisplayName,
                    Reason = "meal shortage",
                    TargetPosition = cookfire.Position,
                    Amount = 1
                });
            }
        }

        private void AddProcessingOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState structure, string inputId, int inputAmount, string outputId, int outputAmount, int priority, string reason)
        {
            AddStoreSupplyOrders(orders, structure, inputId, Math.Max(inputAmount, GetPendingConstructionRequirement(outputId)));

            if (structure.InputStore.GetCount(inputId) >= inputAmount && structure.OutputStore.AvailableCapacity >= outputAmount)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{structure.StructureId}.{outputId}",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = priority,
                    ResourceId = outputId,
                    StructureId = structure.StructureId,
                    Label = structure.DisplayName,
                    Reason = reason,
                    TargetPosition = structure.Position,
                    Amount = 1
                });
            }
        }

        private void AddKilnOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState kiln)
        {
            int brickNeed = Math.Max(0, GetPendingConstructionRequirement("brick") - (_centralDepot.GetCount("brick") + kiln.OutputStore.GetCount("brick")));
            if (brickNeed <= 0)
            {
                return;
            }

            AddStoreSupplyOrders(orders, kiln, "stone", brickNeed);
            AddStoreSupplyOrders(orders, kiln, "clay", brickNeed);
            AddStoreSupplyOrders(orders, kiln, "firewood", brickNeed);

            if (kiln.InputStore.GetCount("stone") >= 1 &&
                kiln.InputStore.GetCount("clay") >= 1 &&
                kiln.InputStore.GetCount("firewood") >= 1 &&
                kiln.OutputStore.AvailableCapacity >= 1)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{kiln.StructureId}.brick",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = 740,
                    ResourceId = "brick",
                    StructureId = kiln.StructureId,
                    Label = kiln.DisplayName,
                    Reason = "construction brick",
                    TargetPosition = kiln.Position,
                    Amount = 1
                });
            }
        }

        private void AddBuildOrders(List<PrototypeWorkOrder> orders)
        {
            foreach (PrototypeBuildQueueEntry entry in _buildQueue.Where(candidate => !candidate.IsPaused && !candidate.IsCompleted).OrderBy(candidate => candidate.Priority))
            {
                PrototypeStructureState? structure = GetStructure(entry.StructureId);
                if (structure == null)
                {
                    continue;
                }

                IReadOnlyDictionary<string, int> cost = GetConstructionCost(structure.StructureKindId);
                foreach ((string itemId, int amount) in cost)
                {
                    int shortfall = Math.Max(0, amount - structure.InputStore.GetCount(itemId));
                    for (int index = 0; index < shortfall && _centralDepot.GetCount(itemId) > 0; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"supply.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulToStructure,
                            Priority = structure.StructureKindId == "hut" ? 860 : 700,
                            ResourceId = itemId,
                            SourceStoreId = _centralDepot.StoreId,
                            DestinationStoreId = structure.InputStore.StoreId,
                            StructureId = structure.StructureId,
                            Label = structure.DisplayName,
                            Reason = $"construction of {structure.DisplayName}",
                            TargetPosition = _centralDepot.Position,
                            Amount = 1
                        });
                    }
                }

                if (cost.All(pair => structure.InputStore.GetCount(pair.Key) >= pair.Value))
                {
                    if (structure.StructureKindId == "path_segment" && ShouldPausePathBuildsDuringCriticalShortage() && HasCriticalShortage())
                    {
                        continue;
                    }

                    PrototypeWorkOrderKind buildKind = structure.StructureKindId switch
                    {
                        "path_segment" => PrototypeWorkOrderKind.BuildPath,
                        "remote_stockpile" => PrototypeWorkOrderKind.EstablishRemoteDepot,
                        _ => PrototypeWorkOrderKind.Build
                    };

                    orders.Add(new PrototypeWorkOrder
                    {
                        OrderId = $"build.{structure.StructureId}",
                        Kind = buildKind,
                        Priority = structure.StructureKindId switch
                        {
                            "hut" => 880,
                            "remote_stockpile" => 760,
                            "path_segment" => 610,
                            _ => 720
                        },
                        StructureId = structure.StructureId,
                        Label = structure.DisplayName,
                        Reason = structure.StructureKindId switch
                        {
                            "remote_stockpile" => "remote depot ready",
                            "path_segment" => "path corridor ready",
                            _ => "construction ready"
                        },
                        TargetPosition = structure.Position,
                        Amount = 1
                    });
                }
            }
        }

        private void AddReserveExtractionOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            IReadOnlyDictionary<string, int> committedCarries,
            float currentHour,
            PrototypeWeather weather)
        {
            AddExtractionOrders(orders, resources, "logs", Math.Max(0, GetLogTarget() - GetAccessibleResourceCount("logs", committedCarries)), 640);
            AddExtractionOrders(orders, resources, "berries", Math.Max(0, GetBerryTarget() - GetAccessibleResourceCount("berries", committedCarries)), 900);
            AddExtractionOrders(orders, resources, "reeds", Math.Max(0, GetPendingConstructionRequirement("thatch") - GetAccessibleResourceCount("reeds", committedCarries)), 700);
            AddExtractionOrders(orders, resources, "stone", Math.Max(0, GetPendingConstructionRequirement("stone") - GetAccessibleResourceCount("stone", committedCarries)), 620);
            AddExtractionOrders(orders, resources, "clay", Math.Max(0, GetPendingConstructionRequirement("clay") - GetAccessibleResourceCount("clay", committedCarries)), 620);
        }

        private void AddExtractionOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            string resourceId,
            int desiredUnits,
            int priority)
        {
            if (desiredUnits <= 0)
            {
                return;
            }

            IEnumerable<PrototypeResourceSiteState> sites = resources
                .Where(site => site.ResourceId == resourceId && site.UnitsRemaining > 0)
                .OrderBy(site => ComputeRouteDistance(_world.SettlementSpawn.AnchorPosition, site.Position))
                .ThenBy(site => site.NodeName, StringComparer.Ordinal);

            int created = 0;
            foreach (PrototypeResourceSiteState site in sites)
            {
                if (created >= desiredUnits)
                {
                    break;
                }

                float routeDistance = ComputeRouteDistance(_centralDepot.Position, site.Position);
                bool hasRemoteDepot = GetRemoteDepot(site.ClusterId, requireBuilt: true) != null;
                bool hasBuiltCorridor = _pathSegments.Any(segment => segment.IsBuilt && string.Equals(segment.CorridorId, $"corridor.{resourceId}", StringComparison.Ordinal));
                int adjustedPriority = priority;
                if (routeDistance > GetRemoteDepotActivationDistance() && !hasRemoteDepot)
                {
                    adjustedPriority -= 140;
                }

                if (hasBuiltCorridor)
                {
                    adjustedPriority += 40;
                }

                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"extract.{site.NodeName}",
                    Kind = PrototypeWorkOrderKind.Extract,
                    Priority = adjustedPriority,
                    ResourceId = resourceId,
                    TargetNodeName = site.NodeName,
                    ClusterId = site.ClusterId,
                    Label = PrototypeSettlementLayout.GetResourceTargetLabel(resourceId),
                    Reason = $"reserve target for {InventoryComponent.FormatItemName(resourceId)}",
                    TargetPosition = site.Position,
                    Amount = 1
                });
                created++;
            }
        }

        private void AddStoreSupplyOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState structure, string resourceId, int desiredAmount)
        {
            int available = _centralDepot.GetCount(resourceId);
            int shortfall = Math.Max(0, desiredAmount - structure.InputStore.GetCount(resourceId));
            int count = Math.Min(shortfall, available);

            for (int index = 0; index < count; index++)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"supply.{structure.StructureId}.{resourceId}.op.{index}",
                    Kind = PrototypeWorkOrderKind.HaulToStructure,
                    Priority = GetSupplyPriority(structure.StructureKindId, resourceId),
                    ResourceId = resourceId,
                    SourceStoreId = _centralDepot.StoreId,
                    DestinationStoreId = structure.InputStore.StoreId,
                    StructureId = structure.StructureId,
                    Label = structure.DisplayName,
                    Reason = $"supply {structure.DisplayName}",
                    TargetPosition = _centralDepot.Position,
                    Amount = 1
                });
            }
        }

        private List<PrototypeWorkOrder> RemoveClaimedOrders(List<PrototypeWorkOrder> orders)
        {
            HashSet<string> claimedOrderIds = _citizens
                .Where(citizen => !string.IsNullOrWhiteSpace(citizen.CurrentOrderId) && citizen.Phase != PrototypeWorkerPhase.Idle && citizen.Phase != PrototypeWorkerPhase.Incapacitated)
                .Select(citizen => citizen.CurrentOrderId)
                .ToHashSet(StringComparer.Ordinal);

            return orders
                .Where(order => !claimedOrderIds.Contains(order.OrderId))
                .ToList();
        }

        private bool TryAssignNeedDrivenOrder(
            PrototypeWorkerState citizen,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result)
        {
            if (citizen.Needs.NeedsFood)
            {
                string? foodId = _centralDepot.GetCount("meals") > 0
                    ? "meals"
                    : _centralDepot.GetCount("berries") > 0 ? "berries" : null;
                if (foodId != null)
                {
                    BeginOrder(citizen, new PrototypeWorkOrder
                    {
                        OrderId = $"eat.{citizen.WorkerId}.{_totalTicks}",
                        Kind = PrototypeWorkOrderKind.Eat,
                        Priority = 1400,
                        ResourceId = foodId,
                        Label = "Central Hearth",
                        Reason = citizen.Needs.IsNutritionCritical ? "critical nutrition" : "food need",
                        TargetPosition = GetStructure("central_hearth_1")?.Position ?? _world.SettlementSpawn.AnchorPosition,
                        Amount = 1
                    }, result);
                    return true;
                }
            }

            if (citizen.Needs.NeedsSleep || (IsNight(currentHour) && citizen.Needs.Fatigue >= 48.0f))
            {
                Vector3 sleepTarget = GetSleepPosition(citizen);
                string label = citizen.HomeBedCapacity > 0 ? "Hut" : "Hearthside Bedroll";
                BeginOrder(citizen, new PrototypeWorkOrder
                {
                    OrderId = $"sleep.{citizen.WorkerId}.{_totalTicks}",
                    Kind = PrototypeWorkOrderKind.Sleep,
                    Priority = 1300,
                    Label = label,
                    Reason = citizen.Needs.IsExhausted ? "critical fatigue" : "rest cycle",
                    TargetPosition = sleepTarget,
                    Amount = 1
                }, result);
                return true;
            }

            return false;
        }

        private void BeginOrder(PrototypeWorkerState citizen, PrototypeWorkOrder order, PrototypeSettlementTickResult result)
        {
            citizen.CurrentOrderId = order.OrderId;
            citizen.CurrentOrderKind = order.Kind;
            citizen.CurrentOrderReason = order.Reason;
            citizen.TargetStructureId = order.StructureId;
            citizen.TargetResourceNodeName = order.TargetNodeName;
            citizen.SourceStoreId = order.SourceStoreId;
            citizen.DestinationStoreId = order.DestinationStoreId;
            citizen.TargetLabel = order.Label;
            citizen.TargetPosition = order.TargetPosition;
            citizen.CarryItemId = citizen.CarryAmount > 0 ? citizen.CarryItemId : order.ResourceId;

            switch (order.Kind)
            {
                case PrototypeWorkOrderKind.Extract:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToResource, order.TargetPosition, order.Label, $"Heading to {order.Label}");
                    break;
                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                case PrototypeWorkOrderKind.HaulToStructure:
                    BeginTravel(citizen, GetSourceTravelPhase(order.SourceStoreId), GetStorePosition(order.SourceStoreId), GetStoreLabel(order.SourceStoreId), $"Collecting {InventoryComponent.FormatItemName(order.ResourceId)}");
                    break;
                case PrototypeWorkOrderKind.Process:
                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                case PrototypeWorkOrderKind.Eat:
                case PrototypeWorkOrderKind.Sleep:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToStructure, order.TargetPosition, order.Label, $"Heading to {order.Label}");
                    break;
                case PrototypeWorkOrderKind.RefuelHearth:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToDepot, GetStorePosition(order.SourceStoreId), GetStoreLabel(order.SourceStoreId), "Collecting firewood");
                    break;
                case PrototypeWorkOrderKind.Repath:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToStructure, order.TargetPosition, order.Label, "Repathing");
                    break;
            }

            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementWorkAssigned,
                $"{citizen.DisplayName} accepted {order.Kind} for {order.Reason}"));
        }

        private bool CompleteProcessing(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
            if (structure == null)
            {
                FailCitizenOrder(citizen, "process.structure.missing", result, $"{citizen.DisplayName} could not find {citizen.TargetLabel}");
                return false;
            }

            string outputId = citizen.CarryItemId;
            switch (structure.StructureKindId)
            {
                case "wood_yard":
                    if (!structure.InputStore.Remove("logs", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.logs.missing", "Wood yard lacks logs");
                        return false;
                    }

                    if (outputId == "firewood")
                    {
                        structure.OutputStore.Add("firewood", 2);
                        IncrementCount(_producedResources, "firewood", 2);
                    }
                    else
                    {
                        structure.OutputStore.Add("timber", 1);
                        IncrementCount(_producedResources, "timber", 1);
                    }

                    IncrementCount(_consumedResources, "logs", 1);
                    break;

                case "cookfire":
                    if (!structure.InputStore.Remove("berries", 2) || !structure.InputStore.Remove("firewood", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.meals.blocked", "Cookfire lacks berries or firewood");
                        return false;
                    }

                    structure.OutputStore.Add("meals", 2);
                    IncrementCount(_producedResources, "meals", 2);
                    IncrementCount(_consumedResources, "berries", 2);
                    IncrementCount(_consumedResources, "firewood", 1);
                    break;

                case "drying_rack":
                    if (!structure.InputStore.Remove("reeds", 2))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.reeds.missing", "Drying rack lacks reeds");
                        return false;
                    }

                    structure.OutputStore.Add("thatch", 1);
                    IncrementCount(_producedResources, "thatch", 1);
                    IncrementCount(_consumedResources, "reeds", 2);
                    break;

                case "kiln":
                    if (!structure.InputStore.Remove("stone", 1) ||
                        !structure.InputStore.Remove("clay", 1) ||
                        !structure.InputStore.Remove("firewood", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.brick.blocked", "Kiln lacks stone, clay, or firewood");
                        return false;
                    }

                    structure.OutputStore.Add("brick", 1);
                    IncrementCount(_producedResources, "brick", 1);
                    IncrementCount(_consumedResources, "stone", 1);
                    IncrementCount(_consumedResources, "clay", 1);
                    IncrementCount(_consumedResources, "firewood", 1);
                    break;
            }

            structure.ActiveTicks++;
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementProcessCompleted,
                $"{citizen.DisplayName} completed work at {structure.DisplayName}"));
            return true;
        }

        private bool CompleteBuild(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
            if (structure == null)
            {
                FailCitizenOrder(citizen, "build.structure.missing", result, $"{citizen.DisplayName} could not find the build site");
                return false;
            }

            IReadOnlyDictionary<string, int> cost = GetConstructionCost(structure.StructureKindId);
            foreach ((string itemId, int amount) in cost)
            {
                if (!structure.InputStore.Remove(itemId, amount))
                {
                    FailBlockedStructure(structure, citizen, result, "build.inputs.missing", $"{structure.DisplayName} lacks construction materials");
                    return false;
                }

                IncrementCount(_consumedResources, itemId, amount);
            }

            structure.IsBuilt = true;
            structure.Progress = 1.0f;
            structure.IsBlocked = false;
            structure.BlockedReason = string.Empty;
            if (structure.StructureKindId == "hut")
            {
                structure.BedCapacity = 2;
                AssignBeds();
            }

            if (structure.StructureKindId == "path_segment")
            {
                PrototypePathSegmentState? segment = _pathSegments.FirstOrDefault(candidate => string.Equals(candidate.StructureId, structure.StructureId, StringComparison.Ordinal));
                if (segment != null)
                {
                    segment.IsBuilt = true;
                }

                InvalidateNavigation();
            }

            if (structure.StructureKindId == "remote_stockpile")
            {
                PrototypeRemoteDepotState? depot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, structure.StructureId, StringComparison.Ordinal));
                if (depot != null)
                {
                    depot.IsBuilt = true;
                }
            }

            PrototypeBuildQueueEntry? entry = _buildQueue.FirstOrDefault(candidate => candidate.StructureId == structure.StructureId);
            if (entry != null)
            {
                entry.IsCompleted = true;
            }

            _structureCompletionTicks[structure.StructureId] = _totalTicks;
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementBuildCompleted,
                $"{citizen.DisplayName} completed {structure.DisplayName}"));
            if (structure.StructureKindId == "path_segment")
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementPathBuilt,
                    $"{citizen.DisplayName} completed a path segment for {structure.CorridorId}"));
            }

            if (structure.StructureKindId == "remote_stockpile")
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementRemoteDepotEstablished,
                    $"{citizen.DisplayName} established {structure.DisplayName}"));
            }
            return true;
        }

        private void CompleteEating(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            if (_centralDepot.Remove("meals", 1))
            {
                citizen.Needs.Nutrition = Mathf.Min(100.0f, citizen.Needs.Nutrition + 42.0f);
                IncrementCount(_consumedResources, "meals", 1);
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementNeedRecovered,
                    $"{citizen.DisplayName} ate a meal"));
            }
            else if (_centralDepot.Remove("berries", 1))
            {
                citizen.Needs.Nutrition = Mathf.Min(100.0f, citizen.Needs.Nutrition + 22.0f);
                IncrementCount(_consumedResources, "berries", 1);
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementNeedRecovered,
                    $"{citizen.DisplayName} survived on raw berries"));
            }

            if (citizen.Phase == PrototypeWorkerPhase.Incapacitated && citizen.Needs.Nutrition > 18.0f)
            {
                citizen.Phase = PrototypeWorkerPhase.Idle;
            }
        }

        private void CompleteSleeping(PrototypeWorkerState citizen)
        {
            float restBonus = citizen.HomeBedCapacity > 0 ? 44.0f : 24.0f;
            citizen.Needs.Fatigue = Mathf.Max(0.0f, citizen.Needs.Fatigue - restBonus);
            AddRecentEvent(citizen, citizen.HomeBedCapacity > 0 ? "Slept in hut" : "Slept by hearth");
        }

        private void AssignBeds()
        {
            int remainingBeds = _structures.Where(structure => structure.IsBuilt && structure.StructureKindId == "hut").Sum(structure => structure.BedCapacity);

            foreach (PrototypeWorkerState citizen in _citizens.OrderBy(citizen => citizen.WorkerId, StringComparer.Ordinal))
            {
                citizen.HomeBedCapacity = remainingBeds > 0 ? 1 : 0;
                if (remainingBeds > 0)
                {
                    remainingBeds--;
                }
            }
        }

        private Dictionary<string, int> BuildSettlementSummary()
        {
            Dictionary<string, int> summary = new(StringComparer.Ordinal);

            foreach ((string itemId, int amount) in _centralDepot.Items)
            {
                summary[itemId] = amount;
            }

            summary["beds"] = BedCapacity;
            summary["hearth_fuel"] = HearthFuel;
            summary["huts"] = _structures.Count(structure => structure.StructureKindId == "hut" && structure.IsBuilt);
            summary["storehouses"] = _structures.Count(structure => structure.StructureKindId == "storehouse" && structure.IsBuilt);
            summary["remote_depots"] = _remoteDepots.Count(depot => depot.IsBuilt);
            summary["path_segments"] = _pathSegments.Count(segment => segment.IsBuilt);
            return summary;
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

        private PrototypeStructureState CreateStructure(string structureKindId, int structureIndex, bool isBuilt)
        {
            string displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(InventoryComponent.FormatItemName(structureKindId));
            string structureId = $"{structureKindId}_{structureIndex + 1}";
            Vector3 position = GetStructurePosition(structureKindId, structureIndex);

            PrototypeStructureState structure = new()
            {
                StructureId = structureId,
                StructureKindId = structureKindId,
                DisplayName = displayName,
                Position = position,
                GridX = _world.WorldMap.GetNearestCell(position).GridX,
                GridY = _world.WorldMap.GetNearestCell(position).GridY,
                IsBuilt = isBuilt,
                BedCapacity = structureKindId == "hut" && isBuilt ? 2 : 0,
                InputStore = CreateStore($"{structureId}.input", $"{displayName} Input", 24, position),
                OutputStore = CreateStore($"{structureId}.output", $"{displayName} Output", 24, position)
            };

            if (structureKindId == "central_hearth")
            {
                structure.HearthFuel = 2;
            }

            return structure;
        }

        private PrototypeResourceStoreState CreateStore(string id, string displayName, int capacity, Vector3 position, params string[] allowedItems)
        {
            PrototypeResourceStoreState store = new()
            {
                StoreId = id,
                DisplayName = displayName,
                Capacity = capacity,
                Position = position
            };

            foreach (string allowedItem in allowedItems)
            {
                store.AllowedResourceIds.Add(allowedItem);
            }

            return store;
        }

        private void EnsureDynamicInfrastructurePlans()
        {
            EnsureRemoteDepotPlans();
            EnsurePriorityPathPlans();
        }

        private void RebuildNavigation()
        {
            _pathCache.Clear();
            HashSet<Vector2I> builtPathCells = _pathSegments
                .Where(segment => segment.IsBuilt)
                .Select(segment => new Vector2I(segment.GridX, segment.GridY))
                .ToHashSet();
            _navigationGrid = new PrototypeNavigationGrid(_world.WorldMap, builtPathCells, _navigationRulesVersion);
        }

        private void InvalidateNavigation()
        {
            _navigationRulesVersion++;
            RebuildNavigation();
        }

        private PrototypePathPlan FindPathPlan(Vector3 startPosition, Vector3 destinationPosition)
        {
            _navigationGrid ??= new PrototypeNavigationGrid(_world.WorldMap, new HashSet<Vector2I>(), _navigationRulesVersion);

            TerrainCell startCell = _world.WorldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _world.WorldMap.GetNearestCell(destinationPosition);
            PrototypePathCacheKey cacheKey = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, _navigationRulesVersion);
            if (_pathCache.TryGetValue(cacheKey, out PrototypePathPlan? cachedPlan))
            {
                return cachedPlan;
            }

            PrototypePathPlan plan = _navigationGrid.FindPath(startPosition, destinationPosition);
            _pathCache[cacheKey] = plan;
            return plan;
        }

        private void RegisterPathUsage(PrototypePathPlan plan)
        {
            foreach (Vector2I cell in plan.Cells)
            {
                _pathHeatByCell[cell] = _pathHeatByCell.GetValueOrDefault(cell) + 1;
            }

            foreach (PrototypePathSegmentState segment in _pathSegments.Where(candidate => candidate.IsBuilt))
            {
                if (plan.Cells.Contains(new Vector2I(segment.GridX, segment.GridY)))
                {
                    segment.UtilizationCount++;
                }
            }
        }

        private float ComputeRouteDistance(Vector3 startPosition, Vector3 destinationPosition)
        {
            return FindPathPlan(startPosition, destinationPosition).TotalDistanceMeters;
        }

        private void UpdateRouteBacklogMetrics(IReadOnlyList<PrototypeWorkOrder> backlog)
        {
            Dictionary<string, int> currentBacklog = backlog
                .GroupBy(order => order.Kind)
                .ToDictionary(group => group.Key.ToString().ToLowerInvariant(), group => group.Count(), StringComparer.Ordinal);

            foreach (string key in _routeBacklogTicksByKind.Keys.Concat(currentBacklog.Keys).Distinct(StringComparer.Ordinal).ToList())
            {
                _routeBacklogTicksByKind[key] = currentBacklog.ContainsKey(key)
                    ? _routeBacklogTicksByKind.GetValueOrDefault(key) + 1
                    : 0;
            }
        }

        private bool HasCriticalShortage()
        {
            return MealCoveragePercent <= 20 || HearthFuel <= 0;
        }

        private PrototypeRemoteDepotState? GetRemoteDepot(string clusterId, bool requireBuilt = false)
        {
            PrototypeRemoteDepotState? depot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.ClusterId, clusterId, StringComparison.Ordinal));
            if (depot == null)
            {
                return null;
            }

            return !requireBuilt || depot.IsBuilt ? depot : null;
        }

        private PrototypeStructureState? GetRemoteDepotStructure(string clusterId, bool requireBuilt = false)
        {
            PrototypeRemoteDepotState? depot = GetRemoteDepot(clusterId, requireBuilt);
            return depot == null ? null : GetStructure(depot.StructureId);
        }

        private void TrackCitizenPhaseTick(PrototypeWorkerState citizen)
        {
            if (citizen.Phase is PrototypeWorkerPhase.MovingToResource or
                PrototypeWorkerPhase.MovingToCache or
                PrototypeWorkerPhase.MovingToDepot or
                PrototypeWorkerPhase.MovingToStructure)
            {
                citizen.TravelTicksAccumulated++;
                _travelTicksAccumulated++;
                return;
            }

            if (citizen.Phase is PrototypeWorkerPhase.Harvesting or
                PrototypeWorkerPhase.DepositingToCache or
                PrototypeWorkerPhase.DepositingToDepot or
                PrototypeWorkerPhase.DepositingToStructure or
                PrototypeWorkerPhase.Processing or
                PrototypeWorkerPhase.Building or
                PrototypeWorkerPhase.Refueling)
            {
                citizen.WorkTicksAccumulated++;
                _workTicksAccumulated++;
            }
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

        private PrototypeWorkerSnapshot CaptureCitizen(PrototypeWorkerState citizen)
        {
            return new PrototypeWorkerSnapshot
            {
                WorkerId = citizen.WorkerId,
                DisplayName = citizen.DisplayName,
                RoleId = citizen.Role.ToString(),
                Phase = citizen.Phase.ToString(),
                TargetResourceNodeName = citizen.TargetResourceNodeName,
                TargetStructureId = citizen.TargetStructureId,
                SourceStoreId = citizen.SourceStoreId,
                DestinationStoreId = citizen.DestinationStoreId,
                CarryItemId = citizen.CarryItemId,
                CarryAmount = citizen.CarryAmount,
                TicksRemaining = citizen.TicksRemaining,
                PhaseDurationTicks = citizen.PhaseDurationTicks,
                Position = PrototypeSerializableVector3.FromVector3(citizen.Position),
                HomePosition = PrototypeSerializableVector3.FromVector3(citizen.HomePosition),
                TargetPosition = PrototypeSerializableVector3.FromVector3(citizen.TargetPosition),
                TargetLabel = citizen.TargetLabel,
                ActivityText = citizen.ActivityText,
                Nutrition = citizen.Needs.Nutrition,
                Fatigue = citizen.Needs.Fatigue,
                LastFailureReason = citizen.LastFailureReason,
                CurrentOrderId = citizen.CurrentOrderId,
                CurrentOrderKind = citizen.CurrentOrderKind?.ToString() ?? string.Empty,
                CurrentOrderReason = citizen.CurrentOrderReason,
                HomeBedCapacity = citizen.HomeBedCapacity,
                RecentEvents = citizen.RecentEvents.ToList(),
                TravelTicksAccumulated = citizen.TravelTicksAccumulated,
                WorkTicksAccumulated = citizen.WorkTicksAccumulated,
                CurrentRouteLengthMeters = citizen.Navigation.CurrentRouteLengthMeters,
                CurrentRouteCost = citizen.Navigation.CurrentRouteCost,
                CurrentRouteTravelTicks = citizen.Navigation.CurrentRouteTravelTicks,
                CurrentWaypointIndex = citizen.Navigation.CurrentWaypointIndex,
                CachedRouteVersion = citizen.Navigation.CachedRouteVersion,
                RouteSourceGridX = citizen.Navigation.SourceGridX,
                RouteSourceGridY = citizen.Navigation.SourceGridY,
                RouteDestinationGridX = citizen.Navigation.DestinationGridX,
                RouteDestinationGridY = citizen.Navigation.DestinationGridY,
                RouteWaypoints = citizen.Navigation.RouteWaypoints.ToList()
            };
        }

        private static PrototypeResourceStoreState RestoreStoreSnapshot(PrototypeResourceStoreSnapshot snapshot)
        {
            PrototypeResourceStoreState store = new()
            {
                StoreId = snapshot.StoreId,
                DisplayName = snapshot.DisplayName,
                Capacity = snapshot.Capacity,
                Position = snapshot.Position.ToVector3(),
                LinkedClusterId = snapshot.LinkedClusterId
            };

            foreach ((string itemId, int amount) in snapshot.Items)
            {
                store.Items[itemId] = amount;
            }

            return store;
        }

        private static void RestoreStore(PrototypeResourceStoreState store, PrototypeResourceStoreSnapshot snapshot)
        {
            store.DisplayName = snapshot.DisplayName;
            store.Capacity = snapshot.Capacity;
            store.Position = snapshot.Position.ToVector3();
            store.LinkedClusterId = snapshot.LinkedClusterId;
            ReplaceCounts(store.Items, snapshot.Items);
        }

        private static PrototypeWorkerState RestoreCitizen(PrototypeWorkerSnapshot snapshot)
        {
            PrototypeCitizenRole role = Enum.TryParse(snapshot.RoleId, true, out PrototypeCitizenRole parsedRole)
                ? parsedRole
                : PrototypeCitizenRole.Generalist;
            PrototypeWorkerPhase phase = Enum.TryParse(snapshot.Phase, true, out PrototypeWorkerPhase parsedPhase)
                ? parsedPhase
                : PrototypeWorkerPhase.Idle;
            PrototypeWorkOrderKind? orderKind = Enum.TryParse(snapshot.CurrentOrderKind, true, out PrototypeWorkOrderKind parsedOrder)
                ? parsedOrder
                : null;

            return new PrototypeWorkerState
            {
                WorkerId = snapshot.WorkerId,
                DisplayName = snapshot.DisplayName,
                Role = role,
                Phase = phase,
                TargetResourceNodeName = snapshot.TargetResourceNodeName,
                TargetStructureId = snapshot.TargetStructureId,
                SourceStoreId = snapshot.SourceStoreId,
                DestinationStoreId = snapshot.DestinationStoreId,
                CarryItemId = snapshot.CarryItemId,
                CarryAmount = snapshot.CarryAmount,
                TicksRemaining = snapshot.TicksRemaining,
                PhaseDurationTicks = snapshot.PhaseDurationTicks,
                Position = snapshot.Position.ToVector3(),
                HomePosition = snapshot.HomePosition.ToVector3(),
                TargetPosition = snapshot.TargetPosition.ToVector3(),
                TargetLabel = snapshot.TargetLabel,
                ActivityText = snapshot.ActivityText,
                Needs = new PrototypeNeedState
                {
                    Nutrition = snapshot.Nutrition,
                    Fatigue = snapshot.Fatigue
                },
                LastFailureReason = snapshot.LastFailureReason,
                CurrentOrderId = snapshot.CurrentOrderId,
                CurrentOrderKind = orderKind,
                CurrentOrderReason = snapshot.CurrentOrderReason,
                HomeBedCapacity = snapshot.HomeBedCapacity,
                RecentEvents = snapshot.RecentEvents.ToList(),
                TravelTicksAccumulated = snapshot.TravelTicksAccumulated,
                WorkTicksAccumulated = snapshot.WorkTicksAccumulated,
                Navigation = new PrototypeCitizenNavigationState
                {
                    CurrentWaypointIndex = snapshot.CurrentWaypointIndex,
                    CurrentRouteLengthMeters = snapshot.CurrentRouteLengthMeters,
                    CurrentRouteCost = snapshot.CurrentRouteCost,
                    CurrentRouteTravelTicks = snapshot.CurrentRouteTravelTicks,
                    CachedRouteVersion = snapshot.CachedRouteVersion,
                    SourceGridX = snapshot.RouteSourceGridX,
                    SourceGridY = snapshot.RouteSourceGridY,
                    DestinationGridX = snapshot.RouteDestinationGridX,
                    DestinationGridY = snapshot.RouteDestinationGridY,
                    RouteWaypoints = snapshot.RouteWaypoints.ToList()
                }
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

        private void BeginIdle(PrototypeWorkerState citizen, string activityText)
        {
            citizen.CurrentOrderId = string.Empty;
            citizen.CurrentOrderKind = null;
            citizen.CurrentOrderReason = string.Empty;
            citizen.TargetResourceNodeName = string.Empty;
            citizen.TargetStructureId = string.Empty;
            citizen.SourceStoreId = string.Empty;
            citizen.DestinationStoreId = string.Empty;
            citizen.Navigation = new PrototypeCitizenNavigationState();
            BeginStationaryPhase(citizen, PrototypeWorkerPhase.Idle, 6, citizen.HomePosition, "Settlement", activityText);
        }

        private void BeginTravel(PrototypeWorkerState citizen, PrototypeWorkerPhase phase, Vector3 targetPosition, string targetLabel, string activityText)
        {
            PrototypePathPlan plan = FindPathPlan(citizen.Position, targetPosition);
            citizen.Phase = phase;
            citizen.TargetPosition = targetPosition;
            citizen.TargetLabel = targetLabel;
            citizen.ActivityText = activityText;
            TerrainCell sourceCell = _world.WorldMap.GetNearestCell(citizen.Position);
            TerrainCell destinationCell = _world.WorldMap.GetNearestCell(targetPosition);
            citizen.Navigation = new PrototypeCitizenNavigationState
            {
                CurrentWaypointIndex = plan.Waypoints.Count > 1 ? 1 : 0,
                CurrentRouteLengthMeters = plan.TotalDistanceMeters,
                CurrentRouteCost = plan.TotalCost,
                CurrentRouteTravelTicks = plan.EstimatedTravelTicks,
                CachedRouteVersion = _navigationRulesVersion,
                SourceGridX = sourceCell.GridX,
                SourceGridY = sourceCell.GridY,
                DestinationGridX = destinationCell.GridX,
                DestinationGridY = destinationCell.GridY,
                RouteWaypoints = plan.Waypoints
                    .Select(PrototypeSerializableVector3.FromVector3)
                    .ToList()
            };
            citizen.PhaseDurationTicks = CalculateTravelTicks(plan);
            citizen.TicksRemaining = citizen.PhaseDurationTicks;
            RegisterPathUsage(plan);
        }

        private void BeginStationaryPhase(PrototypeWorkerState citizen, PrototypeWorkerPhase phase, int durationTicks, Vector3 position, string targetLabel, string activityText)
        {
            citizen.Phase = phase;
            citizen.Position = position;
            citizen.TargetPosition = position;
            citizen.TargetLabel = targetLabel;
            citizen.ActivityText = activityText;
            citizen.PhaseDurationTicks = durationTicks;
            citizen.TicksRemaining = durationTicks;
        }

        private static bool AdvanceStationaryPhase(PrototypeWorkerState citizen, Vector3 position)
        {
            citizen.Position = position;
            if (citizen.TicksRemaining > 0)
            {
                citizen.TicksRemaining--;
            }

            return citizen.TicksRemaining <= 0;
        }

        private static bool AdvanceTravelPhase(PrototypeWorkerState citizen)
        {
            List<Vector3> route = citizen.Navigation.RouteWaypoints
                .Select(waypoint => waypoint.ToVector3())
                .ToList();
            Vector3 waypointTarget = citizen.TargetPosition;
            if (route.Count > 0 && citizen.Navigation.CurrentWaypointIndex < route.Count)
            {
                waypointTarget = route[citizen.Navigation.CurrentWaypointIndex];
            }

            float routeCostMultiplier = citizen.Navigation.CurrentRouteLengthMeters <= 0.01f
                ? 1.0f
                : Mathf.Clamp(citizen.Navigation.CurrentRouteCost / citizen.Navigation.CurrentRouteLengthMeters, 0.45f, 2.4f);
            float step = CitizenTravelUnitsPerTick / routeCostMultiplier;
            Vector3 nextHorizontalPosition = new Vector3(citizen.Position.X, 0.0f, citizen.Position.Z)
                .MoveToward(new Vector3(waypointTarget.X, 0.0f, waypointTarget.Z), step);
            citizen.Position = new Vector3(nextHorizontalPosition.X, Mathf.Lerp(citizen.Position.Y, waypointTarget.Y, 0.35f), nextHorizontalPosition.Z);
            if (citizen.TicksRemaining > 0)
            {
                citizen.TicksRemaining--;
            }

            if (GetHorizontalDistance(citizen.Position, waypointTarget) <= 0.15f && route.Count > 0 && citizen.Navigation.CurrentWaypointIndex < route.Count - 1)
            {
                citizen.Navigation.CurrentWaypointIndex++;
            }

            if (citizen.TicksRemaining <= 0 || GetHorizontalDistance(citizen.Position, citizen.TargetPosition) <= 0.15f)
            {
                citizen.Position = citizen.TargetPosition;
                citizen.TicksRemaining = 0;
                return true;
            }

            return false;
        }

        private static int CalculateTravelTicks(PrototypePathPlan plan)
        {
            return Math.Max(MinimumTravelTicks, plan.EstimatedTravelTicks);
        }

        private static float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));
        }

        private static float GetNutritionDecay(PrototypeWorkerPhase phase)
        {
            return phase switch
            {
                PrototypeWorkerPhase.MovingToResource or PrototypeWorkerPhase.MovingToCache or PrototypeWorkerPhase.MovingToDepot or PrototypeWorkerPhase.MovingToStructure => 0.10f,
                PrototypeWorkerPhase.Harvesting or PrototypeWorkerPhase.Building or PrototypeWorkerPhase.Processing => 0.12f,
                PrototypeWorkerPhase.Sleeping => 0.04f,
                _ => 0.08f
            };
        }

        private float GetFatigueDelta(PrototypeWorkerPhase phase, float currentHour, PrototypeWeather weather)
        {
            if (phase == PrototypeWorkerPhase.Sleeping)
            {
                float recovery = -1.55f;
                if ((weather == PrototypeWeather.Rain || IsNight(currentHour)) && HearthFuel <= 0)
                {
                    recovery = -0.85f;
                }

                return recovery;
            }

            if (phase == PrototypeWorkerPhase.Idle || phase == PrototypeWorkerPhase.Eating)
            {
                return 0.04f;
            }

            if (phase == PrototypeWorkerPhase.Incapacitated)
            {
                return 0.02f;
            }

            return 0.12f;
        }

        private static bool IsNight(float currentHour) => currentHour >= 20.0f || currentHour < 6.0f;

        private float ScoreOrder(PrototypeWorkerState citizen, PrototypeWorkOrder order)
        {
            float distancePenalty = ComputeRouteDistance(citizen.Position, order.TargetPosition) * 0.75f;
            float roleBonus = GetRoleBonus(citizen.Role, order);
            return order.Priority + roleBonus - distancePenalty;
        }

        private static float GetRoleBonus(PrototypeCitizenRole role, PrototypeWorkOrder order)
        {
            return role switch
            {
                PrototypeCitizenRole.Logger when order.ResourceId is "logs" or "firewood" or "timber" => 18.0f,
                PrototypeCitizenRole.Mason when order.ResourceId is "stone" or "clay" or "brick" => 18.0f,
                PrototypeCitizenRole.Forager when order.ResourceId is "berries" or "reeds" or "meals" => 18.0f,
                PrototypeCitizenRole.Hauler when order.Kind is PrototypeWorkOrderKind.HaulToDepot or PrototypeWorkOrderKind.HaulToRemoteDepot or PrototypeWorkOrderKind.HaulFromRemoteDepot or PrototypeWorkOrderKind.HaulToStructure or PrototypeWorkOrderKind.RefuelHearth => 22.0f,
                PrototypeCitizenRole.Processor when order.Kind == PrototypeWorkOrderKind.Process => 22.0f,
                PrototypeCitizenRole.Builder when order.Kind is PrototypeWorkOrderKind.Build or PrototypeWorkOrderKind.BuildPath or PrototypeWorkOrderKind.EstablishRemoteDepot => 22.0f,
                PrototypeCitizenRole.Generalist => 8.0f,
                _ => 0.0f
            };
        }

        private int GetAccessibleResourceCount(string resourceId, IReadOnlyDictionary<string, int> committedCarries) =>
            _siteCaches.Values.Sum(store => store.GetCount(resourceId)) +
            _centralDepot.GetCount(resourceId) +
            _structures.Sum(structure => structure.OutputStore.GetCount(resourceId)) +
            committedCarries.GetValueOrDefault(resourceId);

        private int GetMealTarget() => Math.Max(8, _citizens.Count * 3);
        private int GetFirewoodTarget() => Math.Max(6, _citizens.Count * 2);
        private int GetLogTarget() => Math.Max(8, GetPendingConstructionRequirement("timber") + GetPendingConstructionRequirement("firewood") + 4);
        private int GetBerryTarget() => Math.Max(6, _citizens.Count * 2);
        private int GetPathCorridorBudget() => Math.Max(1, _scenario.PathBuildPolicy?.CorridorBudget ?? 3);
        private bool ShouldPausePathBuildsDuringCriticalShortage() => _scenario.PathBuildPolicy?.PauseDuringCriticalShortage ?? true;
        private float GetRemoteDepotActivationDistance() => Math.Max(12.0f, _scenario.RemoteDepotPolicy?.ActivationDistanceMeters ?? 55.0f);
        private float GetRemoteDepotPlacementRadius() => Math.Max(6.0f, _scenario.RemoteDepotPolicy?.PlacementRadiusMeters ?? 12.0f);

        private int GetPendingConstructionRequirement(string resourceId)
        {
            int total = 0;
            foreach (PrototypeBuildQueueEntry entry in _buildQueue.Where(candidate => !candidate.IsPaused && !candidate.IsCompleted))
            {
                IReadOnlyDictionary<string, int> cost = GetConstructionCost(entry.StructureKindId);
                total += cost.GetValueOrDefault(resourceId);
                if (entry.StructureKindId == "kiln" && resourceId is "firewood" or "clay" or "stone")
                {
                    total += 4;
                }
            }

            return total;
        }

        private static int GetHaulPriority(string itemId) => itemId switch
        {
            "meals" => 1020,
            "firewood" => 980,
            "berries" => 920,
            "timber" => 760,
            "thatch" => 740,
            "brick" => 720,
            _ => 680
        };

        private static int GetSupplyPriority(string structureKindId, string resourceId) => structureKindId switch
        {
            "cookfire" => resourceId == "firewood" ? 950 : 940,
            "wood_yard" => 700,
            "drying_rack" => 760,
            "kiln" => 720,
            "hut" => 880,
            "remote_stockpile" => 730,
            _ => 700
        };

        private static int GetProcessingTicks(string structureId, string outputId) => outputId switch
        {
            "firewood" => 18,
            "timber" => 20,
            "meals" => 18,
            "thatch" => 20,
            "brick" => 26,
            _ => 20
        };

        private int GetBuildTicks(string structureId) => GetStructure(structureId)?.StructureKindId switch
        {
            "hut" => 40,
            "drying_rack" => 34,
            "storehouse" => 44,
            "kiln" => 42,
            "remote_stockpile" => 36,
            "path_segment" => PathBuildTicks,
            _ => 36
        };

        private static IReadOnlyDictionary<string, int> GetConstructionCost(string structureKindId) => structureKindId switch
        {
            "hut" => HutCost,
            "storehouse" => StorehouseCost,
            "drying_rack" => DryingRackCost,
            "kiln" => KilnCost,
            "remote_stockpile" => RemoteDepotCost,
            _ => new Dictionary<string, int>()
        };

        private PrototypeStructureState? GetStructure(string structureId) => _structures.FirstOrDefault(structure => string.Equals(structure.StructureId, structureId, StringComparison.Ordinal));

        private PrototypeResourceStoreState? GetStore(string storeId)
        {
            if (string.Equals(storeId, _centralDepot.StoreId, StringComparison.Ordinal))
            {
                return _centralDepot;
            }

            if (_siteCaches.TryGetValue(storeId, out PrototypeResourceStoreState? cache))
            {
                return cache;
            }

            foreach (PrototypeStructureState structure in _structures)
            {
                if (string.Equals(structure.InputStore.StoreId, storeId, StringComparison.Ordinal))
                {
                    return structure.InputStore;
                }

                if (string.Equals(structure.OutputStore.StoreId, storeId, StringComparison.Ordinal))
                {
                    return structure.OutputStore;
                }
            }

            return null;
        }

        private PrototypeResourceStoreState? ResolveCacheForCitizen(PrototypeWorkerState citizen)
        {
            string clusterId = ExtractClusterId(citizen.TargetResourceNodeName);
            return string.IsNullOrWhiteSpace(clusterId) ? null : GetStore($"cache.{clusterId}");
        }

        private string ExtractClusterId(string nodeName) => _resourceNodeClusterMap.TryGetValue(nodeName, out string? clusterId) ? clusterId : string.Empty;

        private bool TryPickupFromStore(PrototypeWorkerState citizen, string sourceStoreId)
        {
            PrototypeResourceStoreState? source = GetStore(sourceStoreId);
            if (source == null || string.IsNullOrWhiteSpace(citizen.CarryItemId))
            {
                return false;
            }

            if (!source.Remove(citizen.CarryItemId, 1))
            {
                return false;
            }

            citizen.CarryAmount = 1;
            return true;
        }

        private static string InferHarvestItemFromNode(string nodeName) => nodeName.Split('_')[0] switch
        {
            "logs" => "logs",
            "stone" => "stone",
            "berries" => "berries",
            "clay" => "clay",
            "reeds" => "reeds",
            _ => "logs"
        };

        private static void ClearCitizenCarry(PrototypeWorkerState citizen)
        {
            citizen.CarryItemId = string.Empty;
            citizen.CarryAmount = 0;
        }

        private PrototypeWorkerPhase GetSourceTravelPhase(string sourceStoreId) =>
            string.Equals(sourceStoreId, _centralDepot.StoreId, StringComparison.Ordinal)
                ? PrototypeWorkerPhase.MovingToDepot
                : sourceStoreId.StartsWith("cache.", StringComparison.Ordinal)
                    ? PrototypeWorkerPhase.MovingToCache
                    : PrototypeWorkerPhase.MovingToStructure;

        private Vector3 GetStorePosition(string storeId) => GetStore(storeId)?.Position ?? _world.SettlementSpawn.AnchorPosition;
        private string GetStoreLabel(string storeId) => GetStore(storeId)?.DisplayName ?? "Store";

        private void FailCitizenOrder(PrototypeWorkerState citizen, string blockedReason, PrototypeSettlementTickResult result, string message)
        {
            citizen.LastFailureReason = blockedReason;
            AddRecentEvent(citizen, blockedReason);
            IncrementCount(_blockedReasonCounts, blockedReason, 1);
            result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.SettlementBlocked, message));
            ClearCitizenCarry(citizen);
            BeginIdle(citizen, "Blocked");
        }

        private void FailBlockedStructure(PrototypeStructureState structure, PrototypeWorkerState citizen, PrototypeSettlementTickResult result, string blockedReason, string message)
        {
            structure.IsBlocked = true;
            structure.BlockedReason = blockedReason;
            structure.BlockedTicks++;
            FailCitizenOrder(citizen, blockedReason, result, message);
        }

        private static void IncrementCount(Dictionary<string, int> counts, string key, int amount)
        {
            counts[key] = counts.GetValueOrDefault(key) + amount;
        }

        private static void AddRecentEvent(PrototypeWorkerState citizen, string text)
        {
            citizen.RecentEvents.Add(text);
            if (citizen.RecentEvents.Count > 4)
            {
                citizen.RecentEvents.RemoveAt(0);
            }
        }

        private Vector3 GetSleepPosition(PrototypeWorkerState citizen) =>
            citizen.HomeBedCapacity > 0
                ? citizen.HomePosition
                : ProjectToSurface(_world.SettlementSpawn.AnchorPosition + new Vector3(0.0f, 0.0f, 2.6f));

        private Vector3 GetStructurePosition(string structureKindId, int structureIndex)
        {
            Vector3 anchor = _world.SettlementSpawn.AnchorPosition;
            Vector3 offset = structureKindId switch
            {
                "central_hearth" => new Vector3(0.0f, 0.0f, 0.85f),
                "central_depot" => new Vector3(-2.2f, 0.0f, 0.8f),
                "cookfire" => new Vector3(1.8f, 0.0f, -1.2f),
                "wood_yard" => new Vector3(2.9f, 0.0f, 1.4f),
                "drying_rack" => new Vector3(-4.4f, 0.0f, 2.2f),
                "kiln" => new Vector3(4.8f, 0.0f, 2.6f),
                "storehouse" => new Vector3(-4.2f, 0.0f, -2.6f),
                "hut" => GetHutOffset(structureIndex),
                _ => Vector3.Zero
            };

            return ProjectToSurface(anchor + offset);
        }

        private static Vector3 GetHutOffset(int structureIndex)
        {
            float angle = (-Mathf.Pi * 0.40f) + (structureIndex * 0.85f);
            float radius = 5.4f + (structureIndex * 0.2f);
            return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
        }

        private Vector3 GetCitizenHomePosition(int citizenIndex, int citizenCount)
        {
            float angle = (Mathf.Tau * citizenIndex / Math.Max(citizenCount, 1)) - (Mathf.Pi * 0.5f);
            return _world.SettlementSpawn.AnchorPosition + new Vector3(Mathf.Cos(angle) * 3.8f, 0.0f, Mathf.Sin(angle) * 3.8f);
        }

        private Vector3 ProjectToSurface(Vector3 position) => _world.WorldMap.ProjectToSurface(position);

        private static List<PrototypeCitizenRole> BuildRolePlan(IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas, int citizenCount)
        {
            List<PrototypeCitizenRole> roles = new(citizenCount);
            foreach ((PrototypeCitizenRole role, int count) in roleQuotas.Select(role => (ParseRole(role.RoleId), Math.Max(1, (int)Math.Round(role.Share * citizenCount)))))
            {
                for (int index = 0; index < count && roles.Count < citizenCount; index++)
                {
                    roles.Add(role);
                }
            }

            while (roles.Count < citizenCount)
            {
                roles.Add(PrototypeCitizenRole.Generalist);
            }

            return roles;
        }

        private static PrototypeCitizenRole ParseRole(string roleId) => roleId.ToLowerInvariant() switch
        {
            "logger" => PrototypeCitizenRole.Logger,
            "mason" => PrototypeCitizenRole.Mason,
            "forager" => PrototypeCitizenRole.Forager,
            "hauler" => PrototypeCitizenRole.Hauler,
            "processor" => PrototypeCitizenRole.Processor,
            "builder" => PrototypeCitizenRole.Builder,
            _ => PrototypeCitizenRole.Generalist
        };
    }
}
