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
            _pathPlanLookupsThisTick++;
            _navigationGrid ??= new PrototypeNavigationGrid(_world.WorldMap, new HashSet<Vector2I>(), _navigationRulesVersion);

            TerrainCell startCell = _world.WorldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _world.WorldMap.GetNearestCell(destinationPosition);
            PrototypePathCacheKey cacheKey = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, _navigationRulesVersion);
            if (_pathCache.TryGetValue(cacheKey, out PrototypePathPlan? cachedPlan))
            {
                _pathPlanCacheHitsThisTick++;
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
