using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// Pure deterministic pathfinding over the current prototype heightfield.
    /// </summary>
    public sealed class PrototypeNavigationGrid
    {
        private const float BuiltPathCostMultiplier = 0.55f;

        private readonly WorldMapState _worldMap;
        private readonly Dictionary<Vector2I, PrototypeNavigationGridCell> _cells;
        private readonly HashSet<Vector2I> _builtPathCells;

        public PrototypeNavigationGrid(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells, int rulesVersion)
        {
            _worldMap = worldMap;
            RulesVersion = rulesVersion;
            _builtPathCells = new HashSet<Vector2I>(builtPathCells);
            _cells = BuildCells(worldMap, _builtPathCells);
        }

        public int RulesVersion { get; }

        public PrototypeNavigationGridCell GetCell(int x, int y) => _cells[new Vector2I(x, y)];

        public TerrainCell GetTerrainCell(Vector3 worldPosition) => _worldMap.GetNearestCell(worldPosition);

        public PrototypePathPlan FindPath(Vector3 startPosition, Vector3 destinationPosition)
        {
            TerrainCell startCell = _worldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _worldMap.GetNearestCell(destinationPosition);
            PrototypePathQuery query = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, RulesVersion);

            if (startCell.GridX == destinationCell.GridX && startCell.GridY == destinationCell.GridY)
            {
                float directDistance = HorizontalDistance(startPosition, destinationPosition);
                return new PrototypePathPlan
                {
                    Query = query,
                    Cells = new List<Vector2I> { new(startCell.GridX, startCell.GridY) },
                    Waypoints = new List<Vector3> { startPosition, destinationPosition },
                    TotalDistanceMeters = directDistance,
                    TotalCost = directDistance * GetTraversalMultiplier(startCell.GridX, startCell.GridY),
                    EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt((directDistance * GetTraversalMultiplier(startCell.GridX, startCell.GridY)) / 0.78f))
                };
            }

            List<Vector2I> cellPath = RunAStar(startCell, destinationCell);
            List<Vector3> waypoints = new(cellPath.Count + 2) { startPosition };
            float totalDistance = 0.0f;
            float totalCost = 0.0f;
            Vector3 previous = startPosition;

            foreach (Vector2I cellCoord in cellPath.Skip(1))
            {
                Vector3 worldPoint = _worldMap.GetCell(cellCoord.X, cellCoord.Y).WorldPosition;
                waypoints.Add(worldPoint);
                float segmentDistance = HorizontalDistance(previous, worldPoint);
                totalDistance += segmentDistance;
                totalCost += segmentDistance * GetTraversalMultiplier(cellCoord.X, cellCoord.Y);
                previous = worldPoint;
            }

            float finalDistance = HorizontalDistance(previous, destinationPosition);
            totalDistance += finalDistance;
            totalCost += finalDistance * GetTraversalMultiplier(destinationCell.GridX, destinationCell.GridY);
            waypoints.Add(destinationPosition);

            return new PrototypePathPlan
            {
                Query = query,
                Cells = cellPath,
                Waypoints = CompressWaypoints(waypoints),
                TotalDistanceMeters = totalDistance,
                TotalCost = totalCost,
                EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt(totalCost / 0.78f))
            };
        }

        public float EstimatePathDistance(Vector3 startPosition, Vector3 destinationPosition)
        {
            return FindPath(startPosition, destinationPosition).TotalDistanceMeters;
        }

        public float GetTraversalMultiplier(int gridX, int gridY)
        {
            Vector2I key = new(gridX, gridY);
            PrototypeNavigationGridCell cell = _cells[key];
            return cell.HasBuiltPath
                ? cell.MovementCost * BuiltPathCostMultiplier
                : cell.MovementCost;
        }

        private List<Vector2I> RunAStar(TerrainCell start, TerrainCell destination)
        {
            Vector2I startKey = new(start.GridX, start.GridY);
            Vector2I destinationKey = new(destination.GridX, destination.GridY);
            PriorityQueue<Vector2I, float> frontier = new();
            Dictionary<Vector2I, Vector2I> cameFrom = new();
            Dictionary<Vector2I, float> costSoFar = new()
            {
                [startKey] = 0.0f
            };

            frontier.Enqueue(startKey, 0.0f);

            while (frontier.Count > 0)
            {
                Vector2I current = frontier.Dequeue();
                if (current == destinationKey)
                {
                    break;
                }

                foreach ((Vector2I next, float stepDistance) in EnumerateNeighbors(current))
                {
                    if (!_cells.TryGetValue(next, out PrototypeNavigationGridCell? nextCell) || !nextCell.IsWalkable)
                    {
                        continue;
                    }

                    float newCost = costSoFar[current] + (stepDistance * GetTraversalMultiplier(next.X, next.Y));
                    if (costSoFar.TryGetValue(next, out float existingCost) && existingCost <= newCost)
                    {
                        continue;
                    }

                    costSoFar[next] = newCost;
                    float priority = newCost + EstimateHeuristic(next, destinationKey);
                    frontier.Enqueue(next, priority);
                    cameFrom[next] = current;
                }
            }

            if (!cameFrom.ContainsKey(destinationKey))
            {
                return new List<Vector2I> { startKey, destinationKey };
            }

            List<Vector2I> path = new();
            Vector2I cursor = destinationKey;
            path.Add(cursor);
            while (cursor != startKey)
            {
                cursor = cameFrom[cursor];
                path.Add(cursor);
            }

            path.Reverse();
            return path;
        }

        private static Dictionary<Vector2I, PrototypeNavigationGridCell> BuildCells(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells)
        {
            Dictionary<Vector2I, PrototypeNavigationGridCell> cells = new();
            foreach (TerrainCell cell in worldMap.Cells)
            {
                Vector2I key = new(cell.GridX, cell.GridY);
                bool walkable = cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f;
                cells[key] = new PrototypeNavigationGridCell
                {
                    GridX = cell.GridX,
                    GridY = cell.GridY,
                    WorldPosition = cell.WorldPosition,
                    MovementCost = cell.MovementCost,
                    IsWalkable = walkable,
                    HasBuiltPath = builtPathCells.Contains(key)
                };
            }

            return cells;
        }

        private static IEnumerable<(Vector2I next, float stepDistance)> EnumerateNeighbors(Vector2I current)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    float stepDistance = (offsetX != 0 && offsetY != 0) ? 1.4142135f : 1.0f;
                    yield return (new Vector2I(current.X + offsetX, current.Y + offsetY), stepDistance);
                }
            }
        }

        private static float EstimateHeuristic(Vector2I current, Vector2I destination)
        {
            return new Vector2(current.X, current.Y).DistanceTo(new Vector2(destination.X, destination.Y));
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));
        }

        private static IReadOnlyList<Vector3> CompressWaypoints(List<Vector3> rawWaypoints)
        {
            if (rawWaypoints.Count <= 2)
            {
                return rawWaypoints;
            }

            List<Vector3> compressed = new() { rawWaypoints[0] };
            Vector2 previousDirection = Vector2.Zero;

            for (int index = 1; index < rawWaypoints.Count - 1; index++)
            {
                Vector3 previous = rawWaypoints[index - 1];
                Vector3 current = rawWaypoints[index];
                Vector3 next = rawWaypoints[index + 1];

                Vector2 direction = new Vector2(next.X - current.X, next.Z - current.Z).Normalized();
                Vector2 prior = new Vector2(current.X - previous.X, current.Z - previous.Z).Normalized();

                if (index == 1)
                {
                    previousDirection = prior;
                }

                if (previousDirection.DistanceTo(direction) > 0.05f)
                {
                    compressed.Add(current);
                    previousDirection = direction;
                }
            }

            compressed.Add(rawWaypoints[^1]);
            return compressed;
        }
    }
}
