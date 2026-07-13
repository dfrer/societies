using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Societies.Simulation
{
    public sealed partial class PrototypeSettlementSimulation
    {

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

            if (!TryFindPathPlan(_world.SettlementSpawn.AnchorPosition, destination, out PrototypePathPlan? corridorPlan))
            {
                return;
            }

            int basePriority = _buildQueue.Count + priorityOffset + 10;
            int structureIndex = _pathSegments.Count;

            foreach (Vector2I cell in corridorPlan!.Cells.Skip(1).SkipLast(1))
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
        private void EnsureDynamicInfrastructurePlans()
        {
            EnsureRemoteDepotPlans();
            EnsurePriorityPathPlans();
        }

        public PrototypePerformanceProbeSnapshot CapturePerformanceProbeState()
        {
            PrototypePathQuery? preChangeQuery = _forcedPreChangeQuery;
            PrototypePathQuery? postChangeQuery = _forcedPostChangeQuery;
            Vector2I? changedCell = _forcedChangedCell;
            bool pathSegmentIsBuilt = _pathSegments.Any(candidate =>
                string.Equals(candidate.StructureId, _forcedPathSegmentStructureId, StringComparison.Ordinal) &&
                candidate.IsBuilt);
            return new PrototypePerformanceProbeSnapshot(
                _pathCache.Count,
                _totalTicks,
                _navigationRulesVersion,
                _pathCache.Keys.All(key => key.RulesVersion == _navigationRulesVersion),
                _totalNavigationInvalidations,
                _lastPathPlanRulesVersion,
                new PrototypeForcedInvalidationProbeSnapshot(
                    _forcedInvalidationPrepared,
                    _forcedInvalidationCommitted,
                    _forcedPathSegmentStructureId,
                    _forcedPathSegmentWasBuiltBefore,
                    pathSegmentIsBuilt,
                    changedCell?.X,
                    changedCell?.Y,
                    _forcedInvalidationCompletionTick,
                    _forcedInvalidationVersionBeforeCommit,
                    _forcedInvalidationVersionAfterCommit,
                    _forcedInvalidationsBeforeCommit,
                    _forcedInvalidationsAfterCommit,
                    _forcedCacheEntriesBeforeRebuild,
                    _forcedCacheEntriesImmediatelyAfterRebuild,
                    _forcedFirstLookupObserved,
                    _forcedFirstLookupWasCacheMiss,
                    _forcedFirstLookupUsedNewVersion,
                    preChangeQuery?.StartGridX,
                    preChangeQuery?.StartGridY,
                    preChangeQuery?.EndGridX,
                    preChangeQuery?.EndGridY,
                    preChangeQuery?.RulesVersion,
                    _forcedPreChangePlanVersion,
                    postChangeQuery?.RulesVersion,
                    _forcedPostChangePlanVersion,
                    _forcedExactEndpointsMatch,
                    _forcedChangedCellIncluded,
                    _forcedPreChangePlanCost,
                    _forcedPostChangePlanCost,
                    _forcedCommitToFirstLookupMilliseconds));
        }

        public int ClearDerivedPathCacheForPerformance()
        {
            int clearedEntryCount = _pathCache.Count;
            _pathCache.Clear();
            return clearedEntryCount;
        }

        public bool TryPrepareForcedPathCompletionForPerformance(out string structureId)
        {
            structureId = string.Empty;
            if (!string.IsNullOrEmpty(_preparedForcedPathSegmentStructureId))
            {
                return false;
            }

            PrototypePathSegmentState? segment = _pathSegments
                .Where(candidate => !candidate.IsBuilt)
                .OrderBy(candidate => candidate.StructureId, StringComparer.Ordinal)
                .FirstOrDefault(candidate => GetStructure(candidate.StructureId)?.IsBuilt == false);
            if (segment == null)
            {
                return false;
            }

            _forcedPathSegmentStructureId = segment.StructureId;
            _preparedForcedPathSegmentStructureId = segment.StructureId;
            _forcedPathSegmentWasBuiltBefore = segment.IsBuilt;
            _forcedInvalidationPrepared = true;
            _forcedChangedCell = new Vector2I(segment.GridX, segment.GridY);
            _forcedProbeStartPosition = _world.SettlementSpawn.AnchorPosition;
            _forcedProbeDestinationPosition = segment.Position;

            TerrainCell probeStartCell = _world.WorldMap.GetNearestCell(_forcedProbeStartPosition);
            TerrainCell probeDestinationCell = _world.WorldMap.GetNearestCell(_forcedProbeDestinationPosition);
            _pathCache.Remove(new PrototypePathCacheKey(
                probeStartCell.GridX,
                probeStartCell.GridY,
                probeDestinationCell.GridX,
                probeDestinationCell.GridY,
                _navigationRulesVersion));

            if (!TryFindPathPlan(
                _forcedProbeStartPosition,
                _forcedProbeDestinationPosition,
                out PrototypePathPlan? preChangePlan))
            {
                _forcedInvalidationPrepared = false;
                return false;
            }

            PrototypePathPlan resolvedPreChangePlan = preChangePlan!;
            _forcedPreChangeQuery = resolvedPreChangePlan.Query;
            _forcedPreChangePlanVersion = resolvedPreChangePlan.Query.RulesVersion;
            _forcedPreChangeExactEndpointsMatch =
                resolvedPreChangePlan.Waypoints.Count > 0 &&
                resolvedPreChangePlan.Waypoints[0].IsEqualApprox(_forcedProbeStartPosition) &&
                resolvedPreChangePlan.Waypoints[^1].IsEqualApprox(_forcedProbeDestinationPosition);
            _forcedPreChangePlanCost = resolvedPreChangePlan.TotalCost;
            ResetForcedInvalidationCommitEvidence();

            structureId = segment.StructureId;
            return true;
        }

        private void ResetForcedInvalidationCommitEvidence()
        {
            _forcedInvalidationCommitted = false;
            _forcedInvalidationVersionBeforeCommit = null;
            _forcedInvalidationVersionAfterCommit = null;
            _forcedInvalidationsBeforeCommit = null;
            _forcedInvalidationsAfterCommit = null;
            _forcedCacheEntriesBeforeRebuild = null;
            _forcedCacheEntriesImmediatelyAfterRebuild = null;
            _forcedInvalidationCompletionTick = null;
            _forcedInvalidationStartTimestamp = 0;
            _forcedFirstLookupObserved = false;
            _forcedFirstLookupWasCacheMiss = false;
            _forcedFirstLookupUsedNewVersion = false;
            _forcedPostChangeQuery = null;
            _forcedPostChangePlanVersion = null;
            _forcedExactEndpointsMatch = false;
            _forcedChangedCellIncluded = false;
            _forcedPostChangePlanCost = null;
            _forcedCommitToFirstLookupMilliseconds = null;
        }

        private void CommitPreparedForcedPathCompletion(
            PrototypeSettlementTickResult result,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            if (string.IsNullOrEmpty(_preparedForcedPathSegmentStructureId))
            {
                return;
            }

            PrototypeWorkerState actor = _citizens
                .OrderBy(candidate => candidate.WorkerId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("A forced path completion requires at least one citizen.");
            string preparedStructureId = _preparedForcedPathSegmentStructureId;
            string originalTargetStructureId = actor.TargetStructureId;
            _forcedInvalidationCompletionTick = _totalTicks;
            _forcedInvalidationCommitInProgress = true;
            actor.TargetStructureId = preparedStructureId;
            try
            {
                if (!CompleteBuild(actor, result, runtimeMetrics))
                {
                    throw new InvalidOperationException(
                        $"The prepared path segment '{preparedStructureId}' did not complete.");
                }
            }
            finally
            {
                actor.TargetStructureId = originalTargetStructureId;
                _forcedInvalidationCommitInProgress = false;
                _preparedForcedPathSegmentStructureId = string.Empty;
            }
        }

        private void RebuildNavigation()
        {
            _pathCache.Clear();
            _geometricDistanceFieldsThisTick.Clear();
            HashSet<Vector2I> builtPathCells = _pathSegments
                .Where(segment => segment.IsBuilt)
                .Select(segment => new Vector2I(segment.GridX, segment.GridY))
                .ToHashSet();
            _navigationGrid = new PrototypeNavigationGrid(
                _world.WorldMap,
                builtPathCells,
                _navigationRulesVersion,
                priorGrid: _navigationGrid);
        }

        private float ComputeRouteDistanceLowerBound(Vector3 startPosition, Vector3 destinationPosition)
        {
            float straightLineLowerBound = PrototypeOrderSelectionMath.ComputeStraightLineDistanceLowerBound(
                startPosition,
                destinationPosition,
                _world.WorldMap.Cells.Count);
            _navigationGrid ??= new PrototypeNavigationGrid(
                _world.WorldMap,
                new HashSet<Vector2I>(),
                _navigationRulesVersion);

            if (!_geometricDistanceFieldsThisTick.TryGetValue(startPosition, out PrototypeNavigationGrid.GeometricDistanceField? field))
            {
                field = _navigationGrid.BuildGeometricDistanceField(startPosition);
                _geometricDistanceFieldsThisTick[startPosition] = field;
            }

            return field.TryGetExactEndpointDistanceLowerBound(
                    startPosition,
                    destinationPosition,
                    out float topologyLowerBound)
                ? Math.Max(straightLineLowerBound, topologyLowerBound)
                : straightLineLowerBound;
        }

        private bool ShouldBuildGeometricDistanceField(
            Vector3 startPosition,
            IEnumerable<Vector3> destinationPositions)
        {
            TerrainCell startCell = _world.WorldMap.GetNearestCell(startPosition);
            HashSet<PrototypePathCacheKey> missingKeys = new();
            foreach (Vector3 destinationPosition in destinationPositions)
            {
                TerrainCell destinationCell = _world.WorldMap.GetNearestCell(destinationPosition);
                PrototypePathCacheKey key = new(
                    startCell.GridX,
                    startCell.GridY,
                    destinationCell.GridX,
                    destinationCell.GridY,
                    _navigationRulesVersion);
                if (!_pathCache.ContainsKey(key) &&
                    missingKeys.Add(key) &&
                    missingKeys.Count >= MinimumMissingPathsForGeometricDistanceField)
                {
                    return true;
                }
            }

            return false;
        }
        private void InvalidateNavigation(RuntimeMetricsCollector? runtimeMetrics)
        {
            bool captureForcedLookup = _forcedInvalidationCommitInProgress;
            RuntimeMetricsPhaseToken navigationRebuildPhase = runtimeMetrics?.BeginPhase(RuntimeMetricsPhase.NavigationRebuild) ?? default;
            try
            {
                if (captureForcedLookup)
                {
                    _forcedInvalidationVersionBeforeCommit = _navigationRulesVersion;
                    _forcedInvalidationsBeforeCommit = _totalNavigationInvalidations;
                    _forcedCacheEntriesBeforeRebuild = _pathCache.Count;
                    _forcedInvalidationStartTimestamp = Stopwatch.GetTimestamp();
                }

                _navigationInvalidationsThisTick++;
                _totalNavigationInvalidations++;
                _navigationRulesVersion++;
                RebuildNavigation();

                if (captureForcedLookup)
                {
                    _forcedInvalidationVersionAfterCommit = _navigationRulesVersion;
                    _forcedInvalidationsAfterCommit = _totalNavigationInvalidations;
                    _forcedCacheEntriesImmediatelyAfterRebuild = _pathCache.Count;
                }
            }
            finally
            {
                navigationRebuildPhase.Complete();
            }

            if (captureForcedLookup)
            {
                CaptureFirstPostInvalidationLookup();
            }
        }

        private void CaptureFirstPostInvalidationLookup()
        {
            if (!TryFindPathPlan(
                _forcedProbeStartPosition,
                _forcedProbeDestinationPosition,
                out PrototypePathPlan? postChangePlan))
            {
                throw new InvalidOperationException("The prepared forced-invalidation probe became unreachable after a path-cost-only rebuild.");
            }

            PrototypePathPlan resolvedPostChangePlan = postChangePlan!;
            _forcedPostChangeQuery = resolvedPostChangePlan.Query;
            _forcedPostChangePlanVersion = resolvedPostChangePlan.Query.RulesVersion;
            _forcedPostChangePlanCost = resolvedPostChangePlan.TotalCost;
            _forcedFirstLookupObserved = true;
            _forcedFirstLookupWasCacheMiss = !_lastPathPlanLookupWasCacheHit;
            _forcedFirstLookupUsedNewVersion =
                resolvedPostChangePlan.Query.RulesVersion == _navigationRulesVersion &&
                _forcedInvalidationVersionAfterCommit == _navigationRulesVersion;
            _forcedInvalidationCommitted = true;
            _forcedCommitToFirstLookupMilliseconds = Stopwatch
                .GetElapsedTime(_forcedInvalidationStartTimestamp)
                .TotalMilliseconds;

            PrototypePathQuery? preChangeQuery = _forcedPreChangeQuery;
            bool queryCellsMatch = preChangeQuery.HasValue &&
                preChangeQuery.Value.StartGridX == resolvedPostChangePlan.Query.StartGridX &&
                preChangeQuery.Value.StartGridY == resolvedPostChangePlan.Query.StartGridY &&
                preChangeQuery.Value.EndGridX == resolvedPostChangePlan.Query.EndGridX &&
                preChangeQuery.Value.EndGridY == resolvedPostChangePlan.Query.EndGridY;
            bool postChangeEndpointsMatch =
                resolvedPostChangePlan.Waypoints.Count > 0 &&
                resolvedPostChangePlan.Waypoints[0].IsEqualApprox(_forcedProbeStartPosition) &&
                resolvedPostChangePlan.Waypoints[^1].IsEqualApprox(_forcedProbeDestinationPosition);
            _forcedExactEndpointsMatch =
                queryCellsMatch &&
                _forcedPreChangeExactEndpointsMatch &&
                postChangeEndpointsMatch;
            _forcedChangedCellIncluded = _forcedChangedCell is Vector2I changedCell &&
                resolvedPostChangePlan.Cells.Contains(changedCell);
        }
        private bool TryFindPathPlan(
            Vector3 startPosition,
            Vector3 destinationPosition,
            out PrototypePathPlan? plan,
            PathPlanLookupPurpose purpose = PathPlanLookupPurpose.General)
        {
            _pathPlanLookupsThisTick++;
            bool isSelectorLookup = purpose == PathPlanLookupPurpose.GenericOrderSelection;
            if (isSelectorLookup)
            {
                _selectorExactPathQueriesThisTick++;
            }
            _navigationGrid ??= new PrototypeNavigationGrid(_world.WorldMap, new HashSet<Vector2I>(), _navigationRulesVersion);

            TerrainCell startCell = _world.WorldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _world.WorldMap.GetNearestCell(destinationPosition);
            PrototypePathCacheKey cacheKey = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, _navigationRulesVersion);
            if (_pathCache.TryGetValue(cacheKey, out PrototypePathCacheEntry? cachedEntry))
            {
                _pathPlanCacheHitsThisTick++;
                if (isSelectorLookup)
                {
                    _selectorPathCacheHitsThisTick++;
                }
                _lastPathPlanLookupWasCacheHit = true;
                _lastPathPlanRulesVersion = cachedEntry.Query.RulesVersion;
                if (!cachedEntry.IsReachable)
                {
                    plan = null;
                    return false;
                }

                return _navigationGrid.TryMaterializePath(
                    startPosition,
                    destinationPosition,
                    cachedEntry.Cells,
                    out plan);
            }

            _pathPlanCacheMissesThisTick++;
            if (isSelectorLookup)
            {
                _selectorPathCacheMissesThisTick++;
            }
            bool isReachable = _navigationGrid.TryFindPath(startPosition, destinationPosition, out plan);
            PrototypePathQuery query = new(
                startCell.GridX,
                startCell.GridY,
                destinationCell.GridX,
                destinationCell.GridY,
                _navigationRulesVersion);
            _pathCache[cacheKey] = new PrototypePathCacheEntry
            {
                Query = query,
                IsReachable = isReachable,
                Cells = plan?.Cells.ToList() ?? new List<Vector2I>()
            };
            _lastPathPlanLookupWasCacheHit = false;
            _lastPathPlanRulesVersion = query.RulesVersion;
            return isReachable;
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
        private bool TryComputeRouteDistance(
            Vector3 startPosition,
            Vector3 destinationPosition,
            out float distanceMeters,
            PathPlanLookupPurpose purpose = PathPlanLookupPurpose.General)
        {
            if (TryFindPathPlan(startPosition, destinationPosition, out PrototypePathPlan? plan, purpose))
            {
                distanceMeters = plan!.TotalDistanceMeters;
                return true;
            }

            distanceMeters = 0.0f;
            return false;
        }
        private float ComputeRouteDistance(Vector3 startPosition, Vector3 destinationPosition)
        {
            return TryComputeRouteDistance(startPosition, destinationPosition, out float distanceMeters)
                ? distanceMeters
                : float.PositiveInfinity;
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
        private int GetPathCorridorBudget() => Math.Max(1, _scenario.PathBuildPolicy?.CorridorBudget ?? 3);
        private bool ShouldPausePathBuildsDuringCriticalShortage() => _scenario.PathBuildPolicy?.PauseDuringCriticalShortage ?? true;
        private float GetRemoteDepotActivationDistance() => Math.Max(12.0f, _scenario.RemoteDepotPolicy?.ActivationDistanceMeters ?? 55.0f);
        private float GetRemoteDepotPlacementRadius() => Math.Max(6.0f, _scenario.RemoteDepotPolicy?.PlacementRadiusMeters ?? 12.0f);

    }
}
