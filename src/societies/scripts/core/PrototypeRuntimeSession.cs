using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("Societies.Core.Tests")]

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
        private VoxelWorldModule? _voxelWorld;
        private WorldcraftConstructionState? _worldcraft;
        private IPrototypeRuntimeTerrainQuery? _terrainQuery;
        private PrototypeResourceLedger? _resourceLedger;
        private PrototypeCrisisState? _crisisState;
        private int _simulationSeed;
        private readonly PrototypeOrderSelectionMode _orderSelectionMode;
        private readonly PrototypeExtractionPlanningMode _extractionPlanningMode;
        private readonly PrototypeRouteDistanceMode _routeDistanceMode;
        private readonly string _selectedWorldModel;
        private readonly HashSet<string> _eligibleContributionResourceIds;
        private readonly Dictionary<string, long> _contributionCountsByResource = new(StringComparer.Ordinal);
        private PrototypeSettlementDirective _activeDirective = PrototypeSettlementDirective.Neutral;
        private PrototypeCivicPolicyState _civicPolicy = new();
        private PrototypeWetlandState _wetland = new();
        private PrototypeRuntimeTelemetrySnapshot _telemetry = new();
        private IReadOnlyList<PrototypeResourceSiteState> _cachedPlanningResources =
            System.Array.Empty<PrototypeResourceSiteState>();
        private long _cachedPlanningResourceRevision = -1;
        private bool _cachedPlanningResourcesExcludeReeds;

        public PrototypeRuntimeSession(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition>? roleQuotas = null,
            PrototypeOrderSelectionMode orderSelectionMode = PrototypeOrderSelectionMode.ExactBranchAndBound,
            PrototypeExtractionPlanningMode extractionPlanningMode = PrototypeExtractionPlanningMode.ExactBounded,
            PrototypeRouteDistanceMode routeDistanceMode = PrototypeRouteDistanceMode.CachedDistanceOnly,
            IReadOnlyList<PrototypeResourceDefinition>? resourceDefinitions = null)
        {
            Scenario = scenario;
            _selectedWorldModel = scenario.WorldModel;
            if (!string.Equals(_selectedWorldModel, PrototypeWorldModels.Heightfield, StringComparison.Ordinal) &&
                !string.Equals(_selectedWorldModel, PrototypeWorldModels.Voxel, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Scenario '{scenario.Id}' selects unsupported world model '{_selectedWorldModel}'.", nameof(scenario));
            }
            Inventory = new InventoryComponent();
            Stockpile = new InventoryComponent();
            EventLog = new PrototypeEventLog();
            MetricsTracker = new PrototypeMetricsTracker();
            _simulationSeed = scenario.SimulationSeed;
            _roleQuotas = roleQuotas?.ToList() ?? new List<PrototypeRoleQuotaDefinition>();
            _orderSelectionMode = orderSelectionMode;
            _extractionPlanningMode = extractionPlanningMode;
            _routeDistanceMode = routeDistanceMode;
            _eligibleContributionResourceIds = resourceDefinitions?
                .Where(resource => string.Equals(resource.Category, "raw", StringComparison.OrdinalIgnoreCase))
                .Select(resource => resource.Id)
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        }

        public PrototypeScenarioDefinition Scenario { get; }

        public InventoryComponent Inventory { get; }

        public InventoryComponent Stockpile { get; }

        public PrototypeEventLog EventLog { get; }

        public PrototypeMetricsTracker MetricsTracker { get; }

        public PrototypeCrisisState? Crisis => _crisisState;

        public PrototypeSettlementDirective ActiveDirective => _activeDirective;

        public PrototypeCivicPolicySnapshot CivicPolicy => _civicPolicy.CaptureSnapshot();

        public PrototypeWetlandSnapshot Wetland => _wetland.CaptureSnapshot();

        public bool UsesVoxelWorld => string.Equals(_selectedWorldModel, PrototypeWorldModels.Voxel, StringComparison.Ordinal);

        public long VoxelWorldRevision => _voxelWorld?.WorldRevision ?? 0;

        public string VoxelStateHash => _voxelWorld?.RootHash ?? string.Empty;

        public long ConstructionRevision => _worldcraft?.Revision ?? 0;

        public IReadOnlyList<WorldcraftPieceSnapshot> ConstructionPieces =>
            _worldcraft?.CapturePieces() ?? System.Array.Empty<WorldcraftPieceSnapshot>();

        public IReadOnlyList<string> VoxelHotbarItems => VoxelWorldcraftCatalog.HotbarOrder;

        public VoxelMaterialId GetVoxelMaterial(VoxelCoord coord)
        {
            if (_voxelWorld == null)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            return _voxelWorld.GetMaterial(coord);
        }

        /// <summary>Only authoritative entry point for a bounded voxel mutation.</summary>
        public VoxelEditResult ExecuteVoxelEdit(VoxelEditCommand command)
        {
            if (_voxelWorld == null)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            if (command == null)
            {
                return new VoxelEditResult
                {
                    Rejection = VoxelEditRejection.InvalidActor,
                    WorldRevision = _voxelWorld.WorldRevision
                };
            }

            if (command.Tick != SimulationTick)
            {
                return new VoxelEditResult
                {
                    Rejection = VoxelEditRejection.TickMismatch,
                    WorldRevision = _voxelWorld.WorldRevision
                };
            }
            if (_worldcraft != null && !_worldcraft.CanTrackVoxelEdit)
                return new VoxelEditResult { Rejection = VoxelEditRejection.EventCapacityReached, WorldRevision = _voxelWorld.WorldRevision };
            if (_worldcraft != null && command.Kind == VoxelEditKind.Remove && _worldcraft.WouldOrphanAfterVoxelRemoval(_voxelWorld, command.Coord))
                return new VoxelEditResult { Rejection = VoxelEditRejection.OrphanedConstruction, WorldRevision = _voxelWorld.WorldRevision };
            if (_worldcraft != null && command.Kind == VoxelEditKind.Place && _worldcraft.IsOccupiedByPiece(command.Coord))
                return new VoxelEditResult { Rejection = VoxelEditRejection.PieceOccupied, WorldRevision = _voxelWorld.WorldRevision };

            VoxelEditResult result = _voxelWorld.Execute(command);
            if (result.Accepted) _worldcraft?.RecordVoxelEdit(result.Change!);
            return result;
        }

        /// <summary>Atomically removes one exposed harvestable voxel and grants its catalog material.</summary>
        public WorldcraftGatherResult GatherVoxel(VoxelCoord coord, string actorId = "player")
        {
            if (_voxelWorld == null || _worldcraft == null) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            if (string.IsNullOrWhiteSpace(actorId)) return new WorldcraftGatherResult { Rejection = WorldcraftRejection.InvalidActor };
            if (!_worldcraft.CanRecordEvent) return new WorldcraftGatherResult { Rejection = WorldcraftRejection.EventCapacityReached };
            VoxelMaterialId material = _voxelWorld.GetMaterial(coord);
            string? itemId = VoxelWorldcraftCatalog.ItemFor(material);
            if (itemId == null || !Inventory.CanAddItem(itemId, 1))
            {
                return new WorldcraftGatherResult { Rejection = itemId == null ? WorldcraftRejection.OutOfBounds : WorldcraftRejection.InventoryFull, ItemId = itemId ?? string.Empty };
            }
            if (_worldcraft.WouldOrphanAfterVoxelRemoval(_voxelWorld, coord))
            {
                return new WorldcraftGatherResult { Rejection = WorldcraftRejection.OrphanedSupport, ItemId = itemId };
            }
            VoxelEditResult result = _voxelWorld.Execute(new VoxelEditCommand
            {
                ActorId = actorId, Tick = SimulationTick, ExpectedWorldRevision = _voxelWorld.WorldRevision,
                Kind = VoxelEditKind.Remove, Coord = coord, ExpectedBefore = material, After = VoxelMaterialId.Air
            });
            if (result.Accepted && !Inventory.TryAddItem(itemId, 1))
            {
                throw new InvalidOperationException("Validated voxel gather could not reserve bounded inventory capacity.");
            }
            if (result.Accepted) { _worldcraft.RecordGather(SimulationTick, coord, itemId, result.WorldRevision); RecordEvent("worldcraft.gather", $"{itemId}:{coord.X},{coord.Y},{coord.Z}"); }
            return new WorldcraftGatherResult { Accepted = result.Accepted, Rejection = result.Accepted ? WorldcraftRejection.None : WorldcraftRejection.OutOfBounds, VoxelEdit = result, VoxelRejection = result.Rejection, ItemId = itemId };
        }

        public WorldcraftPlacementEvaluation EvaluateWorldcraftPlacement(WorldcraftPlacementCommand command)
        {
            if (_voxelWorld == null || _worldcraft == null) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            if (command == null || command.Tick != SimulationTick)
                return new WorldcraftPlacementEvaluation { Rejection = WorldcraftRejection.TickMismatch };
            return _worldcraft.Evaluate(command, _voxelWorld, Inventory);
        }

        public WorldcraftCommandResult PlaceWorldcraftPiece(WorldcraftPlacementCommand command)
        {
            if (_voxelWorld == null || _worldcraft == null) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            if (command == null || command.Tick != SimulationTick) return new() { Rejection = WorldcraftRejection.TickMismatch, ConstructionRevision = _worldcraft.Revision };
            WorldcraftPlacementEvaluation evaluation = _worldcraft.Evaluate(command, _voxelWorld, Inventory);
            if (!evaluation.IsValid) return new() { Rejection = evaluation.Rejection, ConstructionRevision = _worldcraft.Revision };
            WorldcraftPieceDefinition definition = evaluation.Definition!;
            if (!Inventory.TryRemoveItems(definition.Cost)) throw new InvalidOperationException("Validated construction cost vanished before commit.");
            WorldcraftPieceSnapshot piece = _worldcraft.Place(command, _voxelWorld.WorldRevision);
            RecordEvent("worldcraft.place", $"{piece.InstanceId}:{piece.PieceId}");
            return new() { Accepted = true, ConstructionRevision = _worldcraft.Revision, Piece = piece, ChangedItemIds = definition.Cost.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray() };
        }

        public WorldcraftCommandResult DismantleWorldcraftPiece(WorldcraftDismantleCommand command)
        {
            if (_voxelWorld == null || _worldcraft == null) throw new InvalidOperationException("The active scenario does not own a voxel world.");
            if (command == null || string.IsNullOrWhiteSpace(command.ActorId)) return new() { Rejection = WorldcraftRejection.InvalidActor, ConstructionRevision = _worldcraft.Revision };
            if (command.Tick != SimulationTick) return new() { Rejection = WorldcraftRejection.TickMismatch, ConstructionRevision = _worldcraft.Revision };
            if (command.ExpectedConstructionRevision != _worldcraft.Revision) return new() { Rejection = WorldcraftRejection.StaleRevision, ConstructionRevision = _worldcraft.Revision };
            if (!_worldcraft.CanRecordEvent) return new() { Rejection = WorldcraftRejection.EventCapacityReached, ConstructionRevision = _worldcraft.Revision };
            if (!_worldcraft.TryGet(command.PieceInstanceId, out WorldcraftPieceSnapshot? piece)) return new() { Rejection = WorldcraftRejection.UnknownPieceInstance, ConstructionRevision = _worldcraft.Revision };
            int distance = Math.Max(Math.Abs(piece!.Anchor.X - command.ActorCell.X), Math.Max(Math.Abs(piece.Anchor.Y - command.ActorCell.Y), Math.Abs(piece.Anchor.Z - command.ActorCell.Z)));
            if (distance > VoxelWorldcraftCatalog.BuildRangeCells) return new() { Rejection = WorldcraftRejection.OutOfRange, ConstructionRevision = _worldcraft.Revision };
            if (_worldcraft.WouldOrphanAfterDismantle(_voxelWorld, piece.InstanceId)) return new() { Rejection = WorldcraftRejection.OrphanedSupport, ConstructionRevision = _worldcraft.Revision };
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!;
            if (!Inventory.CanAddItems(definition.Cost)) return new() { Rejection = WorldcraftRejection.InventoryFull, ConstructionRevision = _worldcraft.Revision };
            if (!Inventory.TryAddItems(definition.Cost)) throw new InvalidOperationException("Validated dismantle recovery could not reserve bounded inventory capacity.");
            _worldcraft.Dismantle(piece, command.Tick, _voxelWorld.WorldRevision); RecordEvent("worldcraft.dismantle", piece.InstanceId);
            return new() { Accepted = true, ConstructionRevision = _worldcraft.Revision, Piece = piece, ChangedItemIds = definition.Cost.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray() };
        }

        public VoxelWorldProjection CaptureVoxelProjection(IEnumerable<VoxelChunkCoord>? scope = null)
        {
            if (_voxelWorld == null)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            return _voxelWorld.CaptureProjection(scope);
        }

        public IReadOnlyList<VoxelWalkableSpan> CaptureVoxelWalkableSpans()
        {
            if (_voxelWorld == null)
            {
                throw new InvalidOperationException("The active scenario does not own a voxel world.");
            }

            return _voxelWorld.CaptureWalkableSpans();
        }

        public IReadOnlyList<PrototypeCitizenInterest> CaptureCitizenInterests()
        {
            return PrototypeCitizenInterestEvaluator.Capture(Workers, _civicPolicy.Policy);
        }

        public PrototypeCognitionObservation PublishCivicCognitionObservation(string citizenId)
        {
            return new PrototypeCognitionModule().PublishObservation(this, citizenId);
        }

        public PrototypeDirectiveSnapshot CaptureDirectiveSnapshot()
        {
            return new PrototypeDirectiveSnapshot
            {
                DirectiveId = PrototypeSettlementDirectiveCatalog.GetId(_activeDirective)
            };
        }

        public bool SupportsRuntimeSnapshotPersistence => true;

        public string RuntimeSnapshotPersistenceDeferralMessage => string.Empty;

        public IReadOnlyDictionary<string, long> ContributionCountsByResource => _contributionCountsByResource;

        public long TotalContributedQuantity => _contributionCountsByResource.Values.Sum();

        public PrototypeRuntimeTelemetrySnapshot CaptureTelemetrySnapshot()
        {
            return CloneTelemetry(_telemetry);
        }

        public int CentralDepotOccupiedQuantity => _settlementSimulation?.CentralDepot.Occupied ?? 0;

        public Vector3 CentralDepotPosition =>
            _settlementSimulation?.CentralDepot.Position ?? PrototypeSettlementLayout.GetStockpileWorldPosition(SettlementAnchorPosition);

        public IReadOnlyDictionary<string, int> ConsumedResources =>
            _settlementSimulation?.ConsumedResources ?? new Dictionary<string, int>();

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

        public int WorldSeed => _terrainQuery?.WorldSeed ?? 0;

        public int WorldGenerationAttempt => _terrainQuery?.WorldGenerationAttempt ?? 0;

        public string WorldHash => _terrainQuery?.WorldHash ?? string.Empty;

        public Vector3 SettlementAnchorPosition => _terrainQuery?.SettlementAnchorPosition ?? Vector3.Zero;

        public Vector3 ProjectToTerrainSurface(Vector3 horizontalPosition) =>
            _terrainQuery?.ProjectToSurface(horizontalPosition) ?? horizontalPosition;

        public Vector3 GetVoxelSafePlayerSpawnPoint()
        {
            if (_terrainQuery is not VoxelRuntimeTerrainQuery voxelTerrain)
            {
                throw new InvalidOperationException("Safe voxel spawn is unavailable for a heightfield runtime.");
            }

            VoxelSafeSpawn spawn = voxelTerrain.FindSafePlayerSpawn();
            return new Vector3(spawn.X + 0.5f, spawn.SurfaceY + 2.0f, spawn.Z + 0.5f);
        }

        internal Vector3 ResolvePlayerPositionAfterSnapshot(Vector3 savedPosition, float playerFootOffset)
        {
            if (_terrainQuery is not VoxelRuntimeTerrainQuery)
            {
                return savedPosition;
            }

            if (!float.IsFinite(playerFootOffset) || playerFootOffset <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(playerFootOffset), "Player foot offset must be finite and positive.");
            }

            int x = Mathf.FloorToInt(savedPosition.X);
            int z = Mathf.FloorToInt(savedPosition.Z);
            bool insideFiniteWorld = x >= VoxelWorldModule.MinX && x < VoxelWorldModule.MaxXExclusive &&
                z >= VoxelWorldModule.MinZ && z < VoxelWorldModule.MaxZExclusive;
            float surfaceY = insideFiniteWorld ? ProjectToTerrainSurface(savedPosition).Y : float.PositiveInfinity;
            return insideFiniteWorld && savedPosition.Y - playerFootOffset >= surfaceY - 0.05f
                ? savedPosition
                : GetVoxelSafePlayerSpawnPoint();
        }

        public PrototypePerformanceProbeSnapshot CapturePerformanceProbeState()
        {
            return _settlementSimulation?.CapturePerformanceProbeState() ?? default;
        }

        internal PrototypeSettlementSnapshot CaptureSettlementSnapshotForTesting()
        {
            return _settlementSimulation?.CaptureSnapshot(SimulationTick) ?? new PrototypeSettlementSnapshot();
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
            _contributionCountsByResource.Clear();
            _activeDirective = PrototypeSettlementDirective.Neutral;
            _civicPolicy = new PrototypeCivicPolicyState();
            _wetland = new PrototypeWetlandState();
            _telemetry = new PrototypeRuntimeTelemetrySnapshot();
            Inventory.ReplaceContents(new Dictionary<string, int>());
            Stockpile.ReplaceContents(new Dictionary<string, int>());
            _world = null;
            _voxelWorld = null;
            _worldcraft = null;
            _terrainQuery = null;
            _resourceLedger = null;
            _settlementSimulation = null;

            if (UsesVoxelWorld)
            {
                _voxelWorld = new VoxelWorldModule(Scenario.SimulationSeed);
                Inventory.ConfigureBoundedStorage(VoxelWorldcraftCatalog.HotbarSlots, VoxelWorldcraftCatalog.StackLimit, VoxelWorldcraftCatalog.HotbarOrder);
                _worldcraft = new WorldcraftConstructionState();
                _terrainQuery = new VoxelRuntimeTerrainQuery(_voxelWorld);
            }
            else
            {
                _world = PrototypeWorldGenerator.Generate(Scenario);
                _terrainQuery = new HeightfieldRuntimeTerrainQuery(_world);
                _resourceLedger = PrototypeResourceLedger.Create(_world);
                _settlementSimulation = new PrototypeSettlementSimulation(
                    Scenario,
                    _roleQuotas,
                    _world,
                    orderSelectionMode: _orderSelectionMode,
                    extractionPlanningMode: _extractionPlanningMode,
                    routeDistanceMode: _routeDistanceMode);
            }
            InvalidatePlanningResourceProjection();
            _weatherSimulation = new PrototypeWeatherSimulation(_simulationSeed);
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

            IReadOnlyList<PrototypeResourceSiteState> resources = CaptureResourceSitesForPlanning();
            PrototypeSettlementTickResult settlementResult = _settlementSimulation?.Advance(
                resources,
                CurrentHour,
                CurrentWeather,
                runtimeMetrics,
                _activeDirective) ?? new PrototypeSettlementTickResult();
            IReadOnlyList<PrototypeHarvestResult> harvestResults = ApplyAiHarvestRequests(settlementResult.HarvestRequests, runtimeMetrics);

            RecordSettlementEvents(settlementResult.Events);
            SyncSettlementViews();
            if (_crisisState != null && _settlementSimulation != null)
            {
                int previousStableHoldTicks = _crisisState.StableHoldTicks;
                int previousCollapseHoldTicks = _crisisState.CollapseHoldTicks;
                _crisisState.Advance(new PrototypeCrisisObservation(
                    _settlementSimulation.Workers.Count,
                    _settlementSimulation.CapableCitizenCount,
                    _settlementSimulation.MealCount,
                    _settlementSimulation.HearthFuel,
                    _settlementSimulation.BedCoveragePercent));
                RecordCrisisTelemetryAndTransitions(previousStableHoldTicks, previousCollapseHoldTicks);
                RecordCrisisTerminalOutcomeIfNeeded();
            }

            return new PrototypeRuntimeTickResult(
                settlementResult,
                harvestResults,
                SimulationTick % 20 == 0);
        }

        public PrototypeDirectiveChangeResult SetDirective(PrototypeSettlementDirective directive)
        {
            PrototypeSettlementDirective previous = _activeDirective;
            if (!Enum.IsDefined(typeof(PrototypeSettlementDirective), directive))
            {
                return new PrototypeDirectiveChangeResult(previous, previous, false, false, "invalid_directive");
            }

            if (directive == previous)
            {
                return new PrototypeDirectiveChangeResult(previous, previous, true, false, string.Empty);
            }

            if (_telemetry.DirectiveChanges == int.MaxValue)
            {
                return new PrototypeDirectiveChangeResult(
                    previous,
                    previous,
                    false,
                    false,
                    "counter_overflow");
            }

            _activeDirective = directive;
            _telemetry.FirstDirectiveTick ??= SimulationTick;
            _telemetry.DirectiveChanges = checked(_telemetry.DirectiveChanges + 1);
            _telemetry.FinalDirectiveId = PrototypeSettlementDirectiveCatalog.GetId(directive);
            RecordEvent(
                PrototypeEventTypes.SettlementDirectiveChanged,
                $"Directive changed from {PrototypeSettlementDirectiveCatalog.GetDisplayName(previous)} to {PrototypeSettlementDirectiveCatalog.GetDisplayName(directive)}");
            return new PrototypeDirectiveChangeResult(previous, directive, true, true, string.Empty);
        }

        public PrototypeCivicPolicyCommandResult SelectCivicPolicy(
            PrototypeCivicPolicyCommand command)
        {
            string failureReason = _civicPolicy.ValidateSelection(command, SimulationTick);
            if (failureReason.Length != 0)
            {
                return new PrototypeCivicPolicyCommandResult(false, failureReason, _civicPolicy.CaptureSnapshot());
            }

            IReadOnlyList<PrototypeCitizenInterest> interests =
                PrototypeCitizenInterestEvaluator.Capture(Workers, command.RequestedPolicy);
            string preferenceSummary = PrototypeCitizenInterestEvaluator.BuildAggregateSummary(interests);
            PrototypeWetlandState selectedWetland = PrototypeWetlandState.CreateForSelection(
                command.RequestedPolicy,
                SimulationTick,
                policyVersion: 1);
            PrototypeCivicPolicyCommandResult result = _civicPolicy.CommitSelection(command, SimulationTick);
            if (!result.Succeeded)
            {
                return result;
            }

            _wetland = selectedWetland;

            RecordEvent(
                PrototypeEventTypes.CivicPolicySelected,
                PrototypeCivicPolicyCatalog.BuildSelectionMessage(command.RequestedPolicy));
            RecordEvent(PrototypeEventTypes.CivicPreferenceSummary, preferenceSummary);
            PrototypeWetlandSnapshot wetland = _wetland.CaptureSnapshot();
            RecordEvent(
                PrototypeEventTypes.CivicWetlandQuotaApplied,
                PrototypeWetlandCatalog.BuildQuotaAppliedMessage(wetland));
            RecordEvent(
                PrototypeEventTypes.CivicWetlandTransition,
                PrototypeWetlandCatalog.BuildTransitionMessage(
                    "policy_selection",
                    PrototypeWetlandCatalog.NeutralHealth,
                    PrototypeWetlandHealthBand.Strained,
                    wetland));
            return result;
        }

        private void RecordCrisisTerminalOutcomeIfNeeded()
        {
            if (_crisisState == null || !_crisisState.TryMarkTerminalEventEmitted())
            {
                return;
            }

            string eventType = _crisisState.Outcome == PrototypeCrisisOutcome.Stable
                ? PrototypeEventTypes.CrisisStabilized
                : PrototypeEventTypes.CrisisCollapsed;
            RecordEvent(eventType, _crisisState.BuildTerminalSummary());
        }

        private void RecordCrisisTelemetryAndTransitions(
            int previousStableHoldTicks,
            int previousCollapseHoldTicks)
        {
            if (_crisisState == null || !_crisisState.HasObservation)
            {
                return;
            }

            PrototypeCrisisObservation observation = _crisisState.LastObservation;
            if (!_telemetry.HasCrisisObservation)
            {
                _telemetry.HasCrisisObservation = true;
                _telemetry.MinimumMeals = observation.Meals;
                _telemetry.MinimumHearthFuel = observation.HearthFuel;
                _telemetry.MaximumBedCoveragePercent = observation.BedCoveragePercent;
            }
            else
            {
                _telemetry.MinimumMeals = Math.Min(_telemetry.MinimumMeals, observation.Meals);
                _telemetry.MinimumHearthFuel = Math.Min(_telemetry.MinimumHearthFuel, observation.HearthFuel);
                _telemetry.MaximumBedCoveragePercent = Math.Max(
                    _telemetry.MaximumBedCoveragePercent,
                    observation.BedCoveragePercent);
            }

            _telemetry.PeakIncapacitatedCitizens = Math.Max(
                _telemetry.PeakIncapacitatedCitizens,
                observation.IncapacitatedCitizens);
            _telemetry.FinalCapableCitizens = observation.CapableCitizens;
            _telemetry.FinalIncapacitatedCitizens = observation.IncapacitatedCitizens;
            _telemetry.FinalMeals = observation.Meals;
            _telemetry.FinalHearthFuel = observation.HearthFuel;
            _telemetry.FinalBedCoveragePercent = observation.BedCoveragePercent;

            if (previousStableHoldTicks == 0 && _crisisState.StableHoldTicks > 0)
            {
                _telemetry.StabilityHoldEntries = IncrementSaturating(
                    _telemetry.StabilityHoldEntries);
                RecordEvent(
                    PrototypeEventTypes.CrisisStabilityHoldEntered,
                    $"Stability hold entered at {_crisisState.StableHoldTicks}/{_crisisState.Definition.StableHoldTicks} ticks");
            }
            else if (previousStableHoldTicks > 0 && _crisisState.StableHoldTicks == 0)
            {
                _telemetry.StabilityHoldBreaks = IncrementSaturating(
                    _telemetry.StabilityHoldBreaks);
                RecordEvent(
                    PrototypeEventTypes.CrisisStabilityHoldBroken,
                    $"Stability hold broken after {previousStableHoldTicks}/{_crisisState.Definition.StableHoldTicks} ticks");
            }

            if (previousCollapseHoldTicks == 0 && _crisisState.CollapseHoldTicks > 0)
            {
                _telemetry.CollapseHoldEntries = IncrementSaturating(
                    _telemetry.CollapseHoldEntries);
                RecordEvent(
                    PrototypeEventTypes.CrisisCollapseHoldEntered,
                    $"Collapse hold entered at {_crisisState.CollapseHoldTicks}/{_crisisState.Definition.CollapseHoldTicks} ticks");
            }
            else if (previousCollapseHoldTicks > 0 && _crisisState.CollapseHoldTicks == 0)
            {
                _telemetry.CollapseHoldBreaks = IncrementSaturating(
                    _telemetry.CollapseHoldBreaks);
                RecordEvent(
                    PrototypeEventTypes.CrisisCollapseHoldBroken,
                    $"Collapse hold broken after {previousCollapseHoldTicks}/{_crisisState.Definition.CollapseHoldTicks} ticks");
            }
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

            if (!_wetland.CanApplyHarvest(site.ResourceId, amount))
            {
                return new PrototypeHarvestResult(
                    "player",
                    siteId,
                    site.ResourceId,
                    amount,
                    0,
                    false,
                    "wetland_quota_exhausted");
            }

            PrototypeHarvestResult result = _resourceLedger.Apply(new PrototypeHarvestCommand("player", siteId, site.ResourceId, amount));
            if (result.Succeeded)
            {
                Inventory.AddItem(result.ResourceId, result.AppliedQuantity);
                RecordPlayerHarvest(result.ResourceId, result.AppliedQuantity);
                ApplyWetlandHarvestConsequence(result.ResourceId, result.AppliedQuantity);
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

        public PrototypeContributionResult ContributeToStockpile(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !_eligibleContributionResourceIds.Contains(itemId))
            {
                return ContributionFailure(itemId, amount, "invalid_item");
            }

            if (amount <= 0)
            {
                return ContributionFailure(itemId, amount, "invalid_amount");
            }

            if (_settlementSimulation == null)
            {
                return ContributionFailure(itemId, amount, "runtime_unavailable");
            }

            if (Inventory.GetCount(itemId) < amount)
            {
                return ContributionFailure(itemId, amount, "insufficient_quantity");
            }

            KeyValuePair<string, int>[] transfer = { new(itemId, amount) };
            PrototypeContributionBatchResult batchResult = ApplyContributionBatch(transfer);
            return batchResult.Succeeded
                ? batchResult.Results[0]
                : ContributionFailure(itemId, amount, batchResult.FailureReason);
        }

        public PrototypeContributionBatchResult ContributeAllEligibleToStockpile()
        {
            KeyValuePair<string, int>[] transfers = Inventory.Items
                .Where(item => item.Value > 0 && _eligibleContributionResourceIds.Contains(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToArray();
            if (transfers.Length == 0)
            {
                string reason = Inventory.Items.Count == 0 ? "empty_inventory" : "no_eligible_resources";
                return new PrototypeContributionBatchResult(Array.Empty<PrototypeContributionResult>(), false, reason);
            }

            return ApplyContributionBatch(transfers);
        }

        private PrototypeContributionBatchResult ApplyContributionBatch(
            IReadOnlyList<KeyValuePair<string, int>> transfers)
        {
            if (_settlementSimulation == null)
            {
                return new PrototypeContributionBatchResult(Array.Empty<PrototypeContributionResult>(), false, "runtime_unavailable");
            }

            if (!CanAccumulateContributionCounts(transfers))
            {
                return new PrototypeContributionBatchResult(
                    Array.Empty<PrototypeContributionResult>(),
                    false,
                    "counter_overflow");
            }

            if (!_settlementSimulation.CanDepositToCentralDepot(transfers, out string rejectionReason))
            {
                return new PrototypeContributionBatchResult(Array.Empty<PrototypeContributionResult>(), false, rejectionReason);
            }

            Dictionary<string, int> remainingInventory = new(Inventory.Items, StringComparer.Ordinal);
            foreach ((string itemId, int amount) in transfers)
            {
                int remaining = remainingInventory.GetValueOrDefault(itemId) - amount;
                if (remaining < 0)
                {
                    return new PrototypeContributionBatchResult(Array.Empty<PrototypeContributionResult>(), false, "insufficient_quantity");
                }

                if (remaining == 0)
                {
                    remainingInventory.Remove(itemId);
                }
                else
                {
                    remainingInventory[itemId] = remaining;
                }
            }

            if (!_settlementSimulation.TryDepositToCentralDepot(transfers, out rejectionReason))
            {
                return new PrototypeContributionBatchResult(Array.Empty<PrototypeContributionResult>(), false, rejectionReason);
            }

            Inventory.ReplaceContents(remainingInventory);
            _telemetry.FirstContributionTick ??= SimulationTick;
            List<PrototypeContributionResult> results = new(transfers.Count);
            foreach ((string itemId, int amount) in transfers)
            {
                _contributionCountsByResource[itemId] = checked(
                    _contributionCountsByResource.GetValueOrDefault(itemId) + amount);
                RecordEvent(
                    PrototypeEventTypes.PlayerContributionSucceeded,
                    $"Contributed {InventoryComponent.FormatItemName(itemId)} x{amount} to the central depot");
                results.Add(new PrototypeContributionResult(itemId, amount, amount, true, string.Empty));
            }

            SyncSettlementViews();
            return new PrototypeContributionBatchResult(results, true, string.Empty);
        }

        private bool CanAccumulateContributionCounts(
            IReadOnlyList<KeyValuePair<string, int>> transfers)
        {
            try
            {
                long total = 0;
                foreach (long existingCount in _contributionCountsByResource.Values)
                {
                    total = checked(total + existingCount);
                }

                Dictionary<string, long> pendingByResource = new(StringComparer.Ordinal);
                foreach ((string itemId, int amount) in transfers)
                {
                    if (amount <= 0)
                    {
                        return false;
                    }

                    pendingByResource[itemId] = checked(
                        pendingByResource.GetValueOrDefault(itemId) + amount);
                    _ = checked(
                        _contributionCountsByResource.GetValueOrDefault(itemId) +
                        pendingByResource[itemId]);
                    total = checked(total + amount);
                }

                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static PrototypeContributionResult ContributionFailure(string itemId, int amount, string reason)
        {
            return new PrototypeContributionResult(itemId, amount, 0, false, reason);
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
                    PrototypeHarvestResult result;
                    if (!_wetland.CanApplyHarvest(command.ResourceId, command.RequestedQuantity))
                    {
                        result = new PrototypeHarvestResult(
                            command.ActorId,
                            command.SiteId,
                            command.ResourceId,
                            command.RequestedQuantity,
                            0,
                            false,
                            "wetland_quota_exhausted");
                    }
                    else
                    {
                        result = _resourceLedger?.Apply(command) ?? new PrototypeHarvestResult(
                            command.ActorId,
                            command.SiteId,
                            command.ResourceId,
                            command.RequestedQuantity,
                            0,
                            false,
                            "ledger_unavailable");
                    }
                    results.Add(result);
                    if (result.Succeeded)
                    {
                        RecordAiHarvestSucceeded(request.WorkerDisplayName, result.ResourceId, result.AppliedQuantity);
                        ApplyWetlandHarvestConsequence(result.ResourceId, result.AppliedQuantity);
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

        internal IReadOnlyList<PrototypeResourceSiteState> CaptureResourceSitesForPlanning()
        {
            if (_resourceLedger == null)
            {
                return System.Array.Empty<PrototypeResourceSiteState>();
            }

            IReadOnlyList<PrototypeResourceSiteState> resources = _resourceLedger.CaptureActiveSites();
            bool excludeReeds = _wetland.Policy != PrototypeCivicPolicy.Neutral &&
                _wetland.RemainingReedQuota <= 0;
            if (_cachedPlanningResourceRevision == _resourceLedger.Revision &&
                _cachedPlanningResourcesExcludeReeds == excludeReeds)
            {
                return _cachedPlanningResources;
            }

            if (!excludeReeds)
            {
                _cachedPlanningResources = resources;
            }
            else
            {
                int filteredCount = 0;
                for (int index = 0; index < resources.Count; index++)
                {
                    if (!string.Equals(
                        resources[index].ResourceId,
                        PrototypeWetlandCatalog.ReedResourceId,
                        StringComparison.Ordinal))
                    {
                        filteredCount++;
                    }
                }

                PrototypeResourceSiteState[] filtered = new PrototypeResourceSiteState[filteredCount];
                int writeIndex = 0;
                for (int index = 0; index < resources.Count; index++)
                {
                    PrototypeResourceSiteState resource = resources[index];
                    if (!string.Equals(
                        resource.ResourceId,
                        PrototypeWetlandCatalog.ReedResourceId,
                        StringComparison.Ordinal))
                    {
                        filtered[writeIndex++] = resource;
                    }
                }

                _cachedPlanningResources = System.Array.AsReadOnly(filtered);
            }

            _cachedPlanningResourceRevision = _resourceLedger.Revision;
            _cachedPlanningResourcesExcludeReeds = excludeReeds;
            return _cachedPlanningResources;
        }

        private void ApplyWetlandHarvestConsequence(string resourceId, int amount)
        {
            if (!string.Equals(resourceId, PrototypeWetlandCatalog.ReedResourceId, StringComparison.Ordinal))
            {
                return;
            }
            if (_wetland.Policy == PrototypeCivicPolicy.Neutral)
            {
                return;
            }

            PrototypeWetlandTransition transition = _wetland.CommitSuccessfulReedHarvest(amount);
            if (_wetland.RemainingReedQuota == 0)
            {
                InvalidatePlanningResourceProjection();
            }
            PrototypeWetlandSnapshot wetland = _wetland.CaptureSnapshot();
            RecordEvent(
                PrototypeEventTypes.CivicWetlandQuotaConsumed,
                PrototypeWetlandCatalog.BuildQuotaConsumedMessage(wetland, amount));
            if (transition.BandChanged)
            {
                RecordEvent(
                    PrototypeEventTypes.CivicWetlandTransition,
                    PrototypeWetlandCatalog.BuildTransitionMessage(
                        "reed_harvest",
                        transition.PreviousHealth,
                        transition.PreviousBand,
                        wetland));
            }
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
                RouteBacklogTicksByKind,
                _crisisState,
                _activeDirective,
                _contributionCountsByResource,
                _telemetry);
        }

        public PrototypeRuntimeSnapshot CaptureSnapshot(Vector3 playerPosition)
        {
            if (_terrainQuery == null || _weatherSimulation == null ||
                (!UsesVoxelWorld && (_world == null || _resourceLedger == null || _settlementSimulation == null)) ||
                (UsesVoxelWorld && _voxelWorld == null))
            {
                throw new InvalidOperationException("Runtime session must be initialized before capturing a snapshot.");
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
            PrototypeSettlementSnapshot settlement = _settlementSimulation?.CaptureSnapshot(SimulationTick) ?? new PrototypeSettlementSnapshot
            {
                TotalTicks = checked((int)SimulationTick)
            };
            CanonicalizeSettlementDictionaries(settlement);

            return new PrototypeRuntimeSnapshot
            {
                SchemaVersion = UsesVoxelWorld ? 11 : 9,
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
                Inventory = OrderDictionary(Inventory.Items),
                Stockpile = OrderDictionary(Stockpile.Items),
                Workers = workers,
                Resources = ResourceSnapshots.ToList(),
                Settlement = settlement,
                Directive = CaptureDirectiveSnapshot(),
                ContributionCountsByResource = _contributionCountsByResource
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                Crisis = _crisisState?.CaptureSnapshot(),
                Telemetry = CaptureTelemetrySnapshot(),
                CivicPolicy = _civicPolicy.CaptureSnapshot(),
                Wetland = _wetland.CaptureSnapshot(),
                WorldModel = _terrainQuery.WorldModel,
                VoxelWorld = _voxelWorld?.CaptureSnapshot(),
                Construction = _worldcraft?.CaptureSnapshot()
            };
        }

        public void ApplySnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            if (!string.Equals(snapshot.ScenarioId, Scenario.Id, System.StringComparison.Ordinal) ||
                snapshot.SimulationTick < 0 || !float.IsFinite(snapshot.CurrentHour) || !float.IsFinite(snapshot.TimeUntilNextWeatherShift))
            {
                throw new InvalidDataException("Runtime snapshot metadata is malformed or targets a different scenario.");
            }

            string expectedWorldModel = UsesVoxelWorld ? PrototypeWorldModels.Voxel : PrototypeWorldModels.Heightfield;
            if (!string.Equals(snapshot.WorldModel, expectedWorldModel, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Runtime snapshot world model does not match the active scenario.");
            }

            WorldGenerationResult? candidateWorld = null;
            VoxelWorldModule? candidateVoxelWorld = null;
            WorldcraftConstructionState? candidateWorldcraft = null;
            IPrototypeRuntimeTerrainQuery candidateTerrainQuery;
            PrototypeResourceLedger? candidateLedger = null;
            PrototypeSettlementSimulation? candidateSettlement = null;
            if (UsesVoxelWorld)
            {
                if (snapshot.SchemaVersion is not (10 or 11))
                {
                    throw new InvalidDataException("Voxel scenarios require a schema-v10 or schema-v11 runtime snapshot.");
                }

                try
                {
                    candidateVoxelWorld = VoxelWorldModule.Restore(
                        snapshot.VoxelWorld ?? throw new InvalidDataException("Voxel snapshot payload is missing."));
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException("Voxel snapshot payload is invalid.", exception);
                }

                candidateTerrainQuery = new VoxelRuntimeTerrainQuery(candidateVoxelWorld);
                try
                {
                    candidateWorldcraft = snapshot.SchemaVersion == 11
                        ? WorldcraftConstructionState.Restore(snapshot.Construction ?? throw new InvalidDataException("Voxel construction payload is missing."), candidateVoxelWorld, snapshot.Inventory, snapshot.SimulationTick)
                        : new WorldcraftConstructionState(candidateVoxelWorld.WorldRevision);
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException("Voxel construction snapshot payload is invalid.", exception);
                }
                int expectedSeed = _voxelWorld?.Seed ?? Scenario.SimulationSeed;
                if (snapshot.WorldSeed != expectedSeed || candidateVoxelWorld.Seed != snapshot.WorldSeed ||
                    snapshot.WorldGenerationAttempt != 0 ||
                    !string.Equals(candidateTerrainQuery.WorldHash, snapshot.WorldHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Runtime snapshot voxel identity does not match the active scenario.");
                }
            }
            else
            {
                candidateWorld = PrototypeWorldGenerator.Regenerate(Scenario, snapshot.WorldSeed, snapshot.WorldGenerationAttempt);
                candidateTerrainQuery = new HeightfieldRuntimeTerrainQuery(candidateWorld);
                if (!string.Equals(candidateWorld.WorldHash, snapshot.WorldHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Runtime snapshot world hash does not match the regenerated world.");
                }

                candidateLedger = PrototypeResourceLedger.Restore(candidateWorld, snapshot);
                int derivedNavigationRulesVersion = 1 + snapshot.Settlement!.PathSegments.Count(segment => segment.IsBuilt);
                if (snapshot.SchemaVersion >= 6 && snapshot.Settlement.NavigationRulesVersion != derivedNavigationRulesVersion)
                {
                    throw new InvalidDataException(
                        $"Runtime snapshot navigation rules version {snapshot.Settlement.NavigationRulesVersion} does not match built path derivation {derivedNavigationRulesVersion}.");
                }

                candidateSettlement = new PrototypeSettlementSimulation(
                    Scenario,
                    _roleQuotas,
                    candidateWorld,
                    orderSelectionMode: _orderSelectionMode,
                    extractionPlanningMode: _extractionPlanningMode,
                    routeDistanceMode: _routeDistanceMode);
                candidateSettlement.LoadState(snapshot.Settlement, derivedNavigationRulesVersion);
            }

            PrototypeWeather candidateWeather = ParseWeatherStrict(snapshot.CurrentWeather);
            PrototypeWeatherSimulation candidateWeatherSimulation = new(snapshot.SimulationSeed, candidateWeather);
            candidateWeatherSimulation.SetState(candidateWeather, snapshot.TimeUntilNextWeatherShift, snapshot.WeatherRandomState);

            PrototypeSettlementDirective candidateDirective = PrototypeSettlementDirective.Neutral;
            Dictionary<string, long> candidateContributionCounts = new(StringComparer.Ordinal);
            PrototypeCrisisState? candidateCrisis = null;
            PrototypeRuntimeTelemetrySnapshot candidateTelemetry = new();
            PrototypeCivicPolicyState candidateCivicPolicy = new();
            PrototypeWetlandState candidateWetland = new();
            if (snapshot.SchemaVersion >= 7)
            {
                candidateDirective = ParseDirectiveStrict(snapshot.Directive!.DirectiveId);
                candidateContributionCounts = snapshot.ContributionCountsByResource
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                ValidateContributionCounts(candidateContributionCounts);
                candidateTelemetry = CloneTelemetry(snapshot.Telemetry!);
                ValidateTelemetry(
                    candidateTelemetry,
                    snapshot.SimulationTick,
                    candidateDirective,
                    candidateContributionCounts,
                    snapshot.Crisis);

                if (Scenario.Crisis == null)
                {
                    if (snapshot.Crisis != null)
                    {
                        throw new InvalidDataException("A crisis-free scenario cannot restore crisis state.");
                    }
                }
                else
                {
                    if (snapshot.Crisis == null)
                    {
                        throw new InvalidDataException($"Schema v{snapshot.SchemaVersion} crisis scenario snapshot is missing crisis state.");
                    }

                    candidateCrisis = new PrototypeCrisisState(Scenario.Crisis);
                    try
                    {
                        candidateCrisis.Restore(snapshot.Crisis);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new InvalidDataException("Runtime snapshot crisis state is malformed.", exception);
                    }
                }

                if (snapshot.SchemaVersion >= 8)
                {
                    candidateCivicPolicy = PrototypeCivicPolicyState.PrepareRestore(snapshot.CivicPolicy!);
                    if (candidateCivicPolicy.SelectedTick > snapshot.SimulationTick)
                    {
                        throw new InvalidDataException(
                            "Runtime snapshot civic policy selection tick exceeds the simulation tick.");
                    }
                }

                candidateWetland = snapshot.SchemaVersion >= 9
                    ? PrototypeWetlandState.PrepareRestore(snapshot.Wetland!, candidateCivicPolicy)
                    : PrototypeWetlandState.MigrateFromCivicPolicy(candidateCivicPolicy);
            }
            else
            {
                ValidateLegacyV5V6Defaults(snapshot);
                if (Scenario.Crisis != null)
                {
                    throw new InvalidDataException("Legacy schema snapshots cannot target a crisis scenario.");
                }
            }

            if (UsesVoxelWorld)
            {
                Inventory.ReplaceContentsAndConfigureBoundedStorage(snapshot.Inventory,
                    VoxelWorldcraftCatalog.HotbarSlots, VoxelWorldcraftCatalog.StackLimit, VoxelWorldcraftCatalog.HotbarOrder);
            }

            _simulationSeed = snapshot.SimulationSeed;
            SimulationTick = snapshot.SimulationTick;
            CurrentHour = snapshot.CurrentHour;
            RunStartHour = snapshot.CurrentHour;
            _world = candidateWorld;
            _voxelWorld = candidateVoxelWorld;
            _worldcraft = candidateWorldcraft;
            _terrainQuery = candidateTerrainQuery;
            _resourceLedger = candidateLedger;
            InvalidatePlanningResourceProjection();
            _weatherSimulation = candidateWeatherSimulation;
            _settlementSimulation = candidateSettlement;
            _crisisState = candidateCrisis;
            _activeDirective = candidateDirective;
            _civicPolicy = candidateCivicPolicy;
            _wetland = candidateWetland;
            _telemetry = candidateTelemetry;
            if (!UsesVoxelWorld) Inventory.ReplaceContents(snapshot.Inventory);
            Stockpile.ReplaceContents(snapshot.Stockpile);
            _contributionCountsByResource.Clear();
            foreach ((string resourceId, long count) in candidateContributionCounts)
            {
                _contributionCountsByResource.Add(resourceId, count);
            }
            SyncSettlementViews();
            MetricsTracker.Clear();
        }

        private void InvalidatePlanningResourceProjection()
        {
            _cachedPlanningResources = System.Array.Empty<PrototypeResourceSiteState>();
            _cachedPlanningResourceRevision = -1;
            _cachedPlanningResourcesExcludeReeds = false;
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

        internal void RecordCivicCognitionDecision(
            PrototypeCognitionDecisionSource source,
            PrototypeCognitionProposal proposal)
        {
            RecordEvent(PrototypeEventTypes.CivicCognitionDecision,
                PrototypeCognitionModule.BuildEventMessage(source, proposal));
        }

        private void SyncSettlementViews()
        {
            _settlementSimulation?.CopyStockpileTo(Stockpile);
        }

        private static void ValidateSnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            if (snapshot.SchemaVersion is not (5 or 6 or 7 or 8 or 9 or 10 or 11))
            {
                throw new InvalidDataException(
                    $"Unsupported runtime snapshot schema {snapshot.SchemaVersion}; expected 5, 6, 7, 8, 9, 10, or 11.");
            }

            if (snapshot.Inventory == null || snapshot.Stockpile == null || snapshot.Workers == null ||
                snapshot.Resources == null || snapshot.Settlement == null ||
                snapshot.Directive == null || snapshot.ContributionCountsByResource == null ||
                snapshot.Telemetry == null ||
                (snapshot.SchemaVersion >= 8 && snapshot.CivicPolicy == null) ||
                (snapshot.SchemaVersion >= 9 && snapshot.Wetland == null) ||
                (snapshot.SchemaVersion is 10 or 11 && (snapshot.WorldModel != PrototypeWorldModels.Voxel || snapshot.VoxelWorld == null)) ||
                (snapshot.SchemaVersion == 11 && snapshot.Construction == null))
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

            if (snapshot.SchemaVersion is 10 or 11)
            {
                PrototypeVoxelSnapshotValidator.ValidateCanonicalShell(snapshot);
                return;
            }

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

        private void ValidateContributionCounts(IReadOnlyDictionary<string, long> counts)
        {
            if (counts.Any(pair =>
                    string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Value <= 0 ||
                    !_eligibleContributionResourceIds.Contains(pair.Key)))
            {
                throw new InvalidDataException(
                    "Runtime snapshot contribution counters contain an ineligible resource or non-positive count.");
            }

            try
            {
                _ = counts.Values.Aggregate(0L, (total, value) => checked(total + value));
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException(
                    "Runtime snapshot contribution counters exceed the aggregate long limit.",
                    exception);
            }
        }

        private static int IncrementSaturating(int value)
        {
            return value == int.MaxValue ? int.MaxValue : checked(value + 1);
        }

        private static void ValidateLegacyV5V6Defaults(PrototypeRuntimeSnapshot snapshot)
        {
            if (!string.Equals(snapshot.Directive!.DirectiveId, "neutral", StringComparison.Ordinal) ||
                snapshot.ContributionCountsByResource.Count != 0 ||
                snapshot.Crisis != null ||
                !IsDefaultTelemetry(snapshot.Telemetry!))
            {
                throw new InvalidDataException(
                    $"Schema v{snapshot.SchemaVersion} snapshot contains state that belongs to schema v7.");
            }
        }

        private static void ValidateTelemetry(
            PrototypeRuntimeTelemetrySnapshot telemetry,
            long simulationTick,
            PrototypeSettlementDirective directive,
            IReadOnlyDictionary<string, long> contributionCounts,
            PrototypeCrisisStateSnapshot? crisis)
        {
            string expectedDirectiveId = PrototypeSettlementDirectiveCatalog.GetId(directive);
            if (!string.Equals(telemetry.FinalDirectiveId, expectedDirectiveId, StringComparison.Ordinal) ||
                telemetry.DirectiveChanges < 0 ||
                telemetry.StabilityHoldEntries < 0 || telemetry.StabilityHoldBreaks < 0 ||
                telemetry.CollapseHoldEntries < 0 || telemetry.CollapseHoldBreaks < 0 ||
                telemetry.StabilityHoldBreaks > telemetry.StabilityHoldEntries ||
                telemetry.CollapseHoldBreaks > telemetry.CollapseHoldEntries ||
                !IsValidFirstTick(telemetry.FirstDirectiveTick, simulationTick) ||
                !IsValidFirstTick(telemetry.FirstContributionTick, simulationTick) ||
                (telemetry.DirectiveChanges == 0) != (telemetry.FirstDirectiveTick == null) ||
                (contributionCounts.Count == 0) != (telemetry.FirstContributionTick == null))
            {
                throw new InvalidDataException("Runtime snapshot telemetry command history is inconsistent.");
            }

            if (!telemetry.HasCrisisObservation)
            {
                if (crisis?.HasObservation == true ||
                    telemetry.PeakIncapacitatedCitizens != 0 ||
                    telemetry.MinimumMeals != 0 ||
                    telemetry.MinimumHearthFuel != 0 ||
                    telemetry.MaximumBedCoveragePercent != 0 ||
                    telemetry.FinalCapableCitizens != 0 ||
                    telemetry.FinalIncapacitatedCitizens != 0 ||
                    telemetry.FinalMeals != 0 ||
                    telemetry.FinalHearthFuel != 0 ||
                    telemetry.FinalBedCoveragePercent != 0 ||
                    telemetry.StabilityHoldEntries != 0 ||
                    telemetry.StabilityHoldBreaks != 0 ||
                    telemetry.CollapseHoldEntries != 0 ||
                    telemetry.CollapseHoldBreaks != 0)
                {
                    throw new InvalidDataException("Runtime snapshot telemetry has crisis aggregates without an observation.");
                }

                return;
            }

            if (crisis == null || !crisis.HasObservation)
            {
                throw new InvalidDataException("Runtime snapshot telemetry requires matching crisis observation state.");
            }

            PrototypeCrisisObservation final = crisis.LastObservation;
            if (telemetry.PeakIncapacitatedCitizens < final.IncapacitatedCitizens ||
                telemetry.MinimumMeals < 0 || telemetry.MinimumMeals > final.Meals ||
                telemetry.MinimumHearthFuel < 0 || telemetry.MinimumHearthFuel > final.HearthFuel ||
                telemetry.MaximumBedCoveragePercent < final.BedCoveragePercent ||
                telemetry.MaximumBedCoveragePercent > 100 ||
                telemetry.FinalCapableCitizens != final.CapableCitizens ||
                telemetry.FinalIncapacitatedCitizens != final.IncapacitatedCitizens ||
                telemetry.FinalMeals != final.Meals ||
                telemetry.FinalHearthFuel != final.HearthFuel ||
                telemetry.FinalBedCoveragePercent != final.BedCoveragePercent ||
                (crisis.StableHoldTicks > 0 && telemetry.StabilityHoldEntries == 0) ||
                (crisis.CollapseHoldTicks > 0 && telemetry.CollapseHoldEntries == 0))
            {
                throw new InvalidDataException("Runtime snapshot crisis telemetry does not match the final observation.");
            }
        }

        private static bool IsValidFirstTick(long? tick, long simulationTick)
        {
            return tick == null || (tick.Value >= 0 && tick.Value <= simulationTick);
        }

        private static bool IsDefaultTelemetry(PrototypeRuntimeTelemetrySnapshot telemetry)
        {
            return telemetry.FirstDirectiveTick == null &&
                telemetry.FirstContributionTick == null &&
                telemetry.DirectiveChanges == 0 &&
                string.Equals(telemetry.FinalDirectiveId, "neutral", StringComparison.Ordinal) &&
                !telemetry.HasCrisisObservation &&
                telemetry.PeakIncapacitatedCitizens == 0 &&
                telemetry.MinimumMeals == 0 &&
                telemetry.MinimumHearthFuel == 0 &&
                telemetry.MaximumBedCoveragePercent == 0 &&
                telemetry.FinalCapableCitizens == 0 &&
                telemetry.FinalIncapacitatedCitizens == 0 &&
                telemetry.FinalMeals == 0 &&
                telemetry.FinalHearthFuel == 0 &&
                telemetry.FinalBedCoveragePercent == 0 &&
                telemetry.StabilityHoldEntries == 0 &&
                telemetry.StabilityHoldBreaks == 0 &&
                telemetry.CollapseHoldEntries == 0 &&
                telemetry.CollapseHoldBreaks == 0;
        }

        private static PrototypeRuntimeTelemetrySnapshot CloneTelemetry(
            PrototypeRuntimeTelemetrySnapshot telemetry)
        {
            return new PrototypeRuntimeTelemetrySnapshot
            {
                FirstDirectiveTick = telemetry.FirstDirectiveTick,
                FirstContributionTick = telemetry.FirstContributionTick,
                DirectiveChanges = telemetry.DirectiveChanges,
                FinalDirectiveId = telemetry.FinalDirectiveId,
                HasCrisisObservation = telemetry.HasCrisisObservation,
                PeakIncapacitatedCitizens = telemetry.PeakIncapacitatedCitizens,
                MinimumMeals = telemetry.MinimumMeals,
                MinimumHearthFuel = telemetry.MinimumHearthFuel,
                MaximumBedCoveragePercent = telemetry.MaximumBedCoveragePercent,
                FinalCapableCitizens = telemetry.FinalCapableCitizens,
                FinalIncapacitatedCitizens = telemetry.FinalIncapacitatedCitizens,
                FinalMeals = telemetry.FinalMeals,
                FinalHearthFuel = telemetry.FinalHearthFuel,
                FinalBedCoveragePercent = telemetry.FinalBedCoveragePercent,
                StabilityHoldEntries = telemetry.StabilityHoldEntries,
                StabilityHoldBreaks = telemetry.StabilityHoldBreaks,
                CollapseHoldEntries = telemetry.CollapseHoldEntries,
                CollapseHoldBreaks = telemetry.CollapseHoldBreaks
            };
        }

        private static void CanonicalizeSettlementDictionaries(PrototypeSettlementSnapshot settlement)
        {
            settlement.CentralDepot.Items = OrderDictionary(settlement.CentralDepot.Items);
            foreach (PrototypeResourceStoreSnapshot store in settlement.SiteCaches)
            {
                store.Items = OrderDictionary(store.Items);
            }
            foreach (PrototypeStructureSnapshot structure in settlement.Structures)
            {
                structure.InputStore.Items = OrderDictionary(structure.InputStore.Items);
                structure.OutputStore.Items = OrderDictionary(structure.OutputStore.Items);
            }

            settlement.ProducedResources = OrderDictionary(settlement.ProducedResources);
            settlement.ConsumedResources = OrderDictionary(settlement.ConsumedResources);
            settlement.BlockedReasonCounts = OrderDictionary(settlement.BlockedReasonCounts);
            settlement.StructureCompletionTicks = OrderDictionary(settlement.StructureCompletionTicks);
            settlement.LogisticsMetrics.DepotThroughputByDepot =
                OrderDictionary(settlement.LogisticsMetrics.DepotThroughputByDepot);
            settlement.LogisticsMetrics.RouteBacklogTicksByKind =
                OrderDictionary(settlement.LogisticsMetrics.RouteBacklogTicksByKind);
        }

        private static Dictionary<string, TValue> OrderDictionary<TValue>(
            IReadOnlyDictionary<string, TValue> values)
        {
            return values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        private static PrototypeSettlementDirective ParseDirectiveStrict(string directiveId)
        {
            return directiveId switch
            {
                "neutral" => PrototypeSettlementDirective.Neutral,
                "food_and_fuel" => PrototypeSettlementDirective.FoodAndFuel,
                "shelter" => PrototypeSettlementDirective.Shelter,
                _ => throw new InvalidDataException($"Runtime snapshot directive '{directiveId}' is invalid.")
            };
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
