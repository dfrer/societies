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
        private readonly float _minimumTraversalMultiplier;

        public PrototypeNavigationGrid(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells, int rulesVersion)
        {
            _worldMap = worldMap;
            RulesVersion = rulesVersion;
            _builtPathCells = new HashSet<Vector2I>(builtPathCells);
            _cells = BuildCells(worldMap, _builtPathCells);
            _minimumTraversalMultiplier = _cells.Values
                .Where(cell => cell.IsWalkable)
                .Select(cell => cell.HasBuiltPath
                    ? cell.MovementCost * BuiltPathCostMultiplier
                    : cell.MovementCost)
                .DefaultIfEmpty(1.0f)
                .Min();
        }

        public int RulesVersion { get; }

        public PrototypeNavigationGridCell GetCell(int x, int y) => _cells[new Vector2I(x, y)];

        public TerrainCell GetTerrainCell(Vector3 worldPosition) => _worldMap.GetNearestCell(worldPosition);

        public bool TryFindPath(
            Vector3 startPosition,
            Vector3 destinationPosition,
            out PrototypePathPlan? plan)
        {
            TerrainCell startCell = _worldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _worldMap.GetNearestCell(destinationPosition);
            PrototypePathQuery query = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, RulesVersion);

            if (!IsWalkable(startCell.GridX, startCell.GridY) ||
                !IsWalkable(destinationCell.GridX, destinationCell.GridY))
            {
                plan = null;
                return false;
            }

            if (startCell.GridX == destinationCell.GridX && startCell.GridY == destinationCell.GridY)
            {
                float directDistance = HorizontalDistance(startPosition, destinationPosition);
                plan = new PrototypePathPlan
                {
                    Query = query,
                    Cells = new List<Vector2I> { new(startCell.GridX, startCell.GridY) },
                    Waypoints = new List<Vector3> { startPosition, destinationPosition },
                    TotalDistanceMeters = directDistance,
                    TotalCost = directDistance * GetTraversalMultiplier(startCell.GridX, startCell.GridY),
                    EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt((directDistance * GetTraversalMultiplier(startCell.GridX, startCell.GridY)) / 0.78f))
                };
                return true;
            }

            if (!TryRunAStar(startCell, destinationCell, out List<Vector2I>? cellPath))
            {
                plan = null;
                return false;
            }

            plan = MaterializePath(startPosition, destinationPosition, query, cellPath!);
            return true;
        }

        public bool TryMaterializePath(
            Vector3 startPosition,
            Vector3 destinationPosition,
            IReadOnlyList<Vector2I> cellPath,
            out PrototypePathPlan? plan)
        {
            TerrainCell startCell = _worldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _worldMap.GetNearestCell(destinationPosition);
            PrototypePathQuery query = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, RulesVersion);
            Vector2I startKey = new(startCell.GridX, startCell.GridY);
            Vector2I destinationKey = new(destinationCell.GridX, destinationCell.GridY);

            if (!IsWalkable(startCell.GridX, startCell.GridY) ||
                !IsWalkable(destinationCell.GridX, destinationCell.GridY) ||
                cellPath.Count == 0 ||
                cellPath[0] != startKey ||
                cellPath[^1] != destinationKey)
            {
                plan = null;
                return false;
            }

            plan = MaterializePath(startPosition, destinationPosition, query, cellPath);
            return true;
        }

        public PrototypePathPlan FindPath(Vector3 startPosition, Vector3 destinationPosition)
        {
            if (TryFindPath(startPosition, destinationPosition, out PrototypePathPlan? plan))
            {
                return plan!;
            }

            TerrainCell startCell = _worldMap.GetNearestCell(startPosition);
            TerrainCell destinationCell = _worldMap.GetNearestCell(destinationPosition);
            throw new InvalidOperationException(
                $"No walkable path exists from ({startCell.GridX},{startCell.GridY}) to ({destinationCell.GridX},{destinationCell.GridY}).");
        }

        public bool TryEstimatePathDistance(
            Vector3 startPosition,
            Vector3 destinationPosition,
            out float distanceMeters)
        {
            if (TryFindPath(startPosition, destinationPosition, out PrototypePathPlan? plan))
            {
                distanceMeters = plan!.TotalDistanceMeters;
                return true;
            }

            distanceMeters = 0.0f;
            return false;
        }

        public float EstimatePathDistance(Vector3 startPosition, Vector3 destinationPosition)
        {
            return FindPath(startPosition, destinationPosition).TotalDistanceMeters;
        }

        private PrototypePathPlan MaterializePath(
            Vector3 startPosition,
            Vector3 destinationPosition,
            PrototypePathQuery query,
            IReadOnlyList<Vector2I> cellPath)
        {
            if (cellPath.Count == 1)
            {
                float directDistance = HorizontalDistance(startPosition, destinationPosition);
                return new PrototypePathPlan
                {
                    Query = query,
                    Cells = cellPath.ToList(),
                    Waypoints = new List<Vector3> { startPosition, destinationPosition },
                    TotalDistanceMeters = directDistance,
                    TotalCost = directDistance * GetTraversalMultiplier(query.StartGridX, query.StartGridY),
                    EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt((directDistance * GetTraversalMultiplier(query.StartGridX, query.StartGridY)) / 0.78f))
                };
            }

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
            totalCost += finalDistance * GetTraversalMultiplier(query.EndGridX, query.EndGridY);
            waypoints.Add(destinationPosition);

            return new PrototypePathPlan
            {
                Query = query,
                Cells = cellPath.ToList(),
                Waypoints = CompressWaypoints(waypoints),
                TotalDistanceMeters = totalDistance,
                TotalCost = totalCost,
                EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt(totalCost / 0.78f))
            };
        }

        public float GetTraversalMultiplier(int gridX, int gridY)
        {
            Vector2I key = new(gridX, gridY);
            PrototypeNavigationGridCell cell = _cells[key];
            return cell.HasBuiltPath
                ? cell.MovementCost * BuiltPathCostMultiplier
                : cell.MovementCost;
        }

        private bool TryRunAStar(TerrainCell start, TerrainCell destination, out List<Vector2I>? path)
        {
            Vector2I startKey = new(start.GridX, start.GridY);
            Vector2I destinationKey = new(destination.GridX, destination.GridY);
            PriorityQueue<Vector2I, (float EstimatedTotal, float Heuristic, int GridY, int GridX, long Sequence)> frontier = new();
            Dictionary<Vector2I, Vector2I> cameFrom = new();
            Dictionary<Vector2I, float> costSoFar = new()
            {
                [startKey] = 0.0f
            };
            long sequence = 0;

            frontier.Enqueue(startKey, (0.0f, 0.0f, startKey.Y, startKey.X, sequence++));

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

                    if (IsDiagonal(current, next) && !CanTraverseDiagonal(current, next))
                    {
                        continue;
                    }

                    float newCost = costSoFar[current] + (stepDistance * GetTraversalMultiplier(next.X, next.Y));
                    if (costSoFar.TryGetValue(next, out float existingCost) && existingCost <= newCost)
                    {
                        continue;
                    }

                    costSoFar[next] = newCost;
                    float heuristic = EstimateHeuristic(next, destinationKey);
                    float priority = newCost + heuristic;
                    frontier.Enqueue(next, (priority, heuristic, next.Y, next.X, sequence++));
                    cameFrom[next] = current;
                }
            }

            if (!costSoFar.ContainsKey(destinationKey))
            {
                path = null;
                return false;
            }

            path = new List<Vector2I>();
            Vector2I cursor = destinationKey;
            path.Add(cursor);
            while (cursor != startKey)
            {
                cursor = cameFrom[cursor];
                path.Add(cursor);
            }

            path.Reverse();
            return true;
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

        private float EstimateHeuristic(Vector2I current, Vector2I destination)
        {
            int deltaX = Math.Abs(current.X - destination.X);
            int deltaY = Math.Abs(current.Y - destination.Y);
            int diagonalSteps = Math.Min(deltaX, deltaY);
            int straightSteps = Math.Max(deltaX, deltaY) - diagonalSteps;
            return ((diagonalSteps * 1.4142135f) + straightSteps) * _minimumTraversalMultiplier;
        }

        private bool IsWalkable(int gridX, int gridY)
        {
            return _cells.TryGetValue(new Vector2I(gridX, gridY), out PrototypeNavigationGridCell? cell) &&
                cell.IsWalkable;
        }

        private static bool IsDiagonal(Vector2I current, Vector2I next)
        {
            return current.X != next.X && current.Y != next.Y;
        }

        private bool CanTraverseDiagonal(Vector2I current, Vector2I next)
        {
            return IsWalkable(next.X, current.Y) && IsWalkable(current.X, next.Y);
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
