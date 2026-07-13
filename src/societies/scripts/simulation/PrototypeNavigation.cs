using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        private readonly PrototypeNavigationGridCell[] _cells;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly float _minimumTraversalMultiplier;
        private readonly ConcurrentBag<AStarWorkspace> _workspacePool = new();

        public PrototypeNavigationGrid(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells, int rulesVersion)
        {
            _worldMap = worldMap;
            RulesVersion = rulesVersion;
            _gridWidth = worldMap.GridWidth;
            _gridHeight = worldMap.GridHeight;
            HashSet<Vector2I> builtPathCellSet = new(builtPathCells);
            _cells = BuildCells(worldMap, builtPathCellSet);
            _minimumTraversalMultiplier = _cells
                .Where(cell => cell.IsWalkable)
                .Select(cell => cell.HasBuiltPath
                    ? cell.MovementCost * BuiltPathCostMultiplier
                    : cell.MovementCost)
                .DefaultIfEmpty(1.0f)
                .Min();
        }

        public int RulesVersion { get; }

        public PrototypeNavigationGridCell GetCell(int x, int y) => _cells[ToCheckedIndex(x, y)];

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
            PrototypeNavigationGridCell cell = _cells[ToCheckedIndex(gridX, gridY)];
            return cell.HasBuiltPath
                ? cell.MovementCost * BuiltPathCostMultiplier
                : cell.MovementCost;
        }

        private bool TryRunAStar(TerrainCell start, TerrainCell destination, out List<Vector2I>? path)
        {
            int startIndex = ToUncheckedIndex(start.GridX, start.GridY);
            int destinationIndex = ToUncheckedIndex(destination.GridX, destination.GridY);
            AStarWorkspace workspace = RentWorkspace();
            try
            {
                int generation = workspace.BeginSearch();
                long sequence = 0;
                workspace.SetCost(startIndex, 0.0f, generation);
                workspace.Frontier.Enqueue(
                    startIndex,
                    (0.0f, 0.0f, start.GridY, start.GridX, sequence++));

                while (workspace.Frontier.Count > 0)
                {
                    int currentIndex = workspace.Frontier.Dequeue();
                    if (currentIndex == destinationIndex)
                    {
                        break;
                    }

                    int currentY = currentIndex / _gridWidth;
                    int currentX = currentIndex - (currentY * _gridWidth);
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                            {
                                continue;
                            }

                            int nextX = currentX + offsetX;
                            int nextY = currentY + offsetY;
                            if (!IsInBounds(nextX, nextY))
                            {
                                continue;
                            }

                            int nextIndex = ToUncheckedIndex(nextX, nextY);
                            PrototypeNavigationGridCell nextCell = _cells[nextIndex];
                            if (!nextCell.IsWalkable)
                            {
                                continue;
                            }

                            bool isDiagonal = offsetX != 0 && offsetY != 0;
                            if (isDiagonal &&
                                (!IsWalkable(nextX, currentY) || !IsWalkable(currentX, nextY)))
                            {
                                continue;
                            }

                            float stepDistance = isDiagonal ? 1.4142135f : 1.0f;
                            float traversalMultiplier = nextCell.HasBuiltPath
                                ? nextCell.MovementCost * BuiltPathCostMultiplier
                                : nextCell.MovementCost;
                            float newCost = workspace.GetCost(currentIndex) + (stepDistance * traversalMultiplier);
                            if (workspace.TryGetCost(nextIndex, generation, out float existingCost) && existingCost <= newCost)
                            {
                                continue;
                            }

                            workspace.SetCost(nextIndex, newCost, generation);
                            float heuristic = EstimateHeuristic(nextX, nextY, destination.GridX, destination.GridY);
                            float priority = newCost + heuristic;
                            workspace.Frontier.Enqueue(nextIndex, (priority, heuristic, nextY, nextX, sequence++));
                            workspace.SetParent(nextIndex, currentIndex);
                        }
                    }
                }

                if (!workspace.HasCost(destinationIndex, generation))
                {
                    path = null;
                    return false;
                }

                path = new List<Vector2I>();
                int cursor = destinationIndex;
                while (true)
                {
                    int cursorY = cursor / _gridWidth;
                    int cursorX = cursor - (cursorY * _gridWidth);
                    path.Add(new Vector2I(cursorX, cursorY));
                    if (cursor == startIndex)
                    {
                        break;
                    }

                    cursor = workspace.GetParent(cursor);
                }

                path.Reverse();
                return true;
            }
            finally
            {
                _workspacePool.Add(workspace);
            }
        }

        private static PrototypeNavigationGridCell[] BuildCells(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells)
        {
            PrototypeNavigationGridCell[] cells = new PrototypeNavigationGridCell[worldMap.GridWidth * worldMap.GridHeight];
            foreach (TerrainCell cell in worldMap.Cells)
            {
                Vector2I key = new(cell.GridX, cell.GridY);
                bool walkable = cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f;
                cells[(cell.GridY * worldMap.GridWidth) + cell.GridX] = new PrototypeNavigationGridCell
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

        private float EstimateHeuristic(int currentX, int currentY, int destinationX, int destinationY)
        {
            int deltaX = Math.Abs(currentX - destinationX);
            int deltaY = Math.Abs(currentY - destinationY);
            int diagonalSteps = Math.Min(deltaX, deltaY);
            int straightSteps = Math.Max(deltaX, deltaY) - diagonalSteps;
            return ((diagonalSteps * 1.4142135f) + straightSteps) * _minimumTraversalMultiplier;
        }

        private bool IsWalkable(int gridX, int gridY)
        {
            return IsInBounds(gridX, gridY) && _cells[ToUncheckedIndex(gridX, gridY)].IsWalkable;
        }

        private bool IsInBounds(int gridX, int gridY)
        {
            return gridX >= 0 && gridY >= 0 && gridX < _gridWidth && gridY < _gridHeight;
        }

        private int ToCheckedIndex(int gridX, int gridY)
        {
            if (!IsInBounds(gridX, gridY))
            {
                throw new KeyNotFoundException($"No navigation cell exists at ({gridX},{gridY}).");
            }

            return ToUncheckedIndex(gridX, gridY);
        }

        private int ToUncheckedIndex(int gridX, int gridY) => (gridY * _gridWidth) + gridX;

        private AStarWorkspace RentWorkspace()
        {
            return _workspacePool.TryTake(out AStarWorkspace? workspace)
                ? workspace
                : new AStarWorkspace(_cells.Length);
        }

        private sealed class AStarWorkspace
        {
            private readonly float[] _costs;
            private readonly int[] _costGenerations;
            private readonly int[] _parents;
            private int _generation;

            public AStarWorkspace(int cellCount)
            {
                _costs = new float[cellCount];
                _costGenerations = new int[cellCount];
                _parents = new int[cellCount];
            }

            public PriorityQueue<int, (float EstimatedTotal, float Heuristic, int GridY, int GridX, long Sequence)> Frontier { get; } = new();

            public int BeginSearch()
            {
                Frontier.Clear();
                if (_generation == int.MaxValue)
                {
                    Array.Clear(_costGenerations);
                    _generation = 1;
                }
                else
                {
                    _generation++;
                }

                return _generation;
            }

            public bool HasCost(int index, int generation) => _costGenerations[index] == generation;

            public bool TryGetCost(int index, int generation, out float cost)
            {
                if (_costGenerations[index] == generation)
                {
                    cost = _costs[index];
                    return true;
                }

                cost = 0.0f;
                return false;
            }

            public float GetCost(int index) => _costs[index];

            public void SetCost(int index, float cost, int generation)
            {
                _costs[index] = cost;
                _costGenerations[index] = generation;
            }

            public int GetParent(int index) => _parents[index];

            public void SetParent(int index, int parentIndex) => _parents[index] = parentIndex;
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
