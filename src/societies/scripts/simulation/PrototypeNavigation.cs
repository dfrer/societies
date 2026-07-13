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
        private readonly NeighborTopology _neighborTopology;
        private readonly float[] _traversalMultipliers;
        private readonly float _minimumTraversalMultiplier;
        private readonly ConcurrentBag<AStarWorkspace> _workspacePool = new();

        public PrototypeNavigationGrid(
            WorldMapState worldMap,
            IReadOnlyCollection<Vector2I> builtPathCells,
            int rulesVersion,
            PrototypeNavigationGrid? priorGrid = null)
        {
            _worldMap = worldMap;
            RulesVersion = rulesVersion;
            _gridWidth = worldMap.GridWidth;
            _gridHeight = worldMap.GridHeight;
            HashSet<Vector2I> builtPathCellSet = new(builtPathCells);
            _cells = BuildCells(worldMap, builtPathCellSet);
            _neighborTopology = CanReuseNeighborTopology(priorGrid, worldMap, _cells)
                    ? priorGrid!._neighborTopology
                    : BuildNeighborTopology(_cells, _gridWidth, _gridHeight);
            _traversalMultipliers = BuildTraversalMultipliers(_cells);
            _minimumTraversalMultiplier = _cells
                .Select((cell, index) => (cell, index))
                .Where(entry => entry.cell.IsWalkable)
                .Select(entry => _traversalMultipliers[entry.index])
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

        public GeometricDistanceField BuildGeometricDistanceField(Vector3 originPosition)
        {
            TerrainCell originCell = _worldMap.GetNearestCell(originPosition);
            int originIndex = ToUncheckedIndex(originCell.GridX, originCell.GridY);
            float[] distances = new float[_cells.Length];
            Array.Fill(distances, float.PositiveInfinity);

            if (!IsWalkable(originCell.GridX, originCell.GridY))
            {
                return new GeometricDistanceField(this, originPosition, originIndex, distances);
            }

            PriorityQueue<int, (float Distance, int GridY, int GridX, long Sequence)> frontier = new();
            long sequence = 0;
            distances[originIndex] = 0.0f;
            int originNeighborEnd = _neighborTopology.Offsets[originIndex + 1];
            for (int neighborIndex = _neighborTopology.Offsets[originIndex]; neighborIndex < originNeighborEnd; neighborIndex++)
            {
                NeighborSlot next = _neighborTopology.Neighbors[neighborIndex];
                float initialDistance = HorizontalDistance(originPosition, _cells[next.CellIndex].WorldPosition);
                if (initialDistance >= distances[next.CellIndex])
                {
                    continue;
                }

                distances[next.CellIndex] = initialDistance;
                frontier.Enqueue(
                    next.CellIndex,
                    (initialDistance, next.GridY, next.GridX, sequence++));
            }

            while (frontier.Count > 0)
            {
                frontier.TryDequeue(
                    out int currentIndex,
                    out (float Distance, int GridY, int GridX, long Sequence) currentPriority);
                if (currentPriority.Distance != distances[currentIndex])
                {
                    continue;
                }

                float currentDistance = distances[currentIndex];
                int neighborEnd = _neighborTopology.Offsets[currentIndex + 1];
                for (int neighborIndex = _neighborTopology.Offsets[currentIndex]; neighborIndex < neighborEnd; neighborIndex++)
                {
                    NeighborSlot next = _neighborTopology.Neighbors[neighborIndex];
                    float candidateDistance = currentDistance + next.GeometricDistanceMeters;
                    if (candidateDistance >= distances[next.CellIndex])
                    {
                        continue;
                    }

                    distances[next.CellIndex] = candidateDistance;
                    frontier.Enqueue(
                        next.CellIndex,
                        (candidateDistance, next.GridY, next.GridX, sequence++));
                }
            }

            return new GeometricDistanceField(this, originPosition, originIndex, distances);
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
            return _traversalMultipliers[ToCheckedIndex(gridX, gridY)];
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

                    int neighborEnd = _neighborTopology.Offsets[currentIndex + 1];
                    for (int neighborIndex = _neighborTopology.Offsets[currentIndex]; neighborIndex < neighborEnd; neighborIndex++)
                    {
                        NeighborSlot next = _neighborTopology.Neighbors[neighborIndex];
                        float newCost = workspace.GetCost(currentIndex) + (next.StepDistance * _traversalMultipliers[next.CellIndex]);
                        if (workspace.TryGetCost(next.CellIndex, generation, out float existingCost) && existingCost <= newCost)
                        {
                            continue;
                        }

                        workspace.SetCost(next.CellIndex, newCost, generation);
                        float heuristic = EstimateHeuristic(next.GridX, next.GridY, destination.GridX, destination.GridY);
                        float priority = newCost + heuristic;
                        workspace.Frontier.Enqueue(next.CellIndex, (priority, heuristic, next.GridY, next.GridX, sequence++));
                        workspace.SetParent(next.CellIndex, currentIndex);
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

        private static float[] BuildTraversalMultipliers(IReadOnlyList<PrototypeNavigationGridCell> cells)
        {
            float[] traversalMultipliers = new float[cells.Count];
            for (int index = 0; index < cells.Count; index++)
            {
                PrototypeNavigationGridCell cell = cells[index];
                traversalMultipliers[index] = cell.HasBuiltPath
                    ? cell.MovementCost * BuiltPathCostMultiplier
                    : cell.MovementCost;
            }

            return traversalMultipliers;
        }

        private static NeighborTopology BuildNeighborTopology(
            IReadOnlyList<PrototypeNavigationGridCell> cells,
            int gridWidth,
            int gridHeight)
        {
            int[] offsets = new int[cells.Count + 1];
            List<NeighborSlot> neighbors = new(checked(cells.Count * 8));
            for (int currentIndex = 0; currentIndex < cells.Count; currentIndex++)
            {
                offsets[currentIndex] = neighbors.Count;
                int currentY = currentIndex / gridWidth;
                int currentX = currentIndex - (currentY * gridWidth);
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
                        if (nextX < 0 || nextY < 0 || nextX >= gridWidth || nextY >= gridHeight)
                        {
                            continue;
                        }

                        int nextIndex = (nextY * gridWidth) + nextX;
                        if (!cells[nextIndex].IsWalkable)
                        {
                            continue;
                        }

                        bool isDiagonal = offsetX != 0 && offsetY != 0;
                        if (isDiagonal &&
                            (!cells[(currentY * gridWidth) + nextX].IsWalkable ||
                             !cells[(nextY * gridWidth) + currentX].IsWalkable))
                        {
                            continue;
                        }

                        float stepDistance = isDiagonal ? 1.4142135f : 1.0f;
                        float geometricDistanceMeters = HorizontalDistance(
                            cells[currentIndex].WorldPosition,
                            cells[nextIndex].WorldPosition);
                        neighbors.Add(new NeighborSlot(
                            nextIndex,
                            nextX,
                            nextY,
                            stepDistance,
                            geometricDistanceMeters));
                    }
                }
            }

            offsets[cells.Count] = neighbors.Count;
            return new NeighborTopology(offsets, neighbors.ToArray());
        }

        private static bool CanReuseNeighborTopology(
            PrototypeNavigationGrid? priorGrid,
            WorldMapState worldMap,
            IReadOnlyList<PrototypeNavigationGridCell> cells)
        {
            if (priorGrid == null ||
                !ReferenceEquals(priorGrid._worldMap, worldMap) ||
                priorGrid._gridWidth != worldMap.GridWidth ||
                priorGrid._gridHeight != worldMap.GridHeight ||
                priorGrid._cells.Length != cells.Count)
            {
                return false;
            }

            for (int index = 0; index < cells.Count; index++)
            {
                if (priorGrid._cells[index].IsWalkable != cells[index].IsWalkable)
                {
                    return false;
                }
            }

            return true;
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

        private sealed class NeighborTopology
        {
            public NeighborTopology(int[] offsets, NeighborSlot[] neighbors)
            {
                Offsets = offsets;
                Neighbors = neighbors;
            }

            public int[] Offsets { get; }

            public NeighborSlot[] Neighbors { get; }
        }

        private readonly record struct NeighborSlot(
            int CellIndex,
            int GridX,
            int GridY,
            float StepDistance,
            float GeometricDistanceMeters);

        public sealed class GeometricDistanceField
        {
            private readonly PrototypeNavigationGrid _grid;
            private readonly Vector3 _originPosition;
            private readonly int _originIndex;
            private readonly float[] _distances;

            internal GeometricDistanceField(
                PrototypeNavigationGrid grid,
                Vector3 originPosition,
                int originIndex,
                float[] distances)
            {
                _grid = grid;
                _originPosition = originPosition;
                _originIndex = originIndex;
                _distances = distances;
            }

            public bool TryGetExactEndpointDistanceLowerBound(
                Vector3 originPosition,
                Vector3 destinationPosition,
                out float distanceMeters)
            {
                TerrainCell originCell = _grid._worldMap.GetNearestCell(originPosition);
                TerrainCell destinationCell = _grid._worldMap.GetNearestCell(destinationPosition);
                int originIndex = _grid.ToUncheckedIndex(originCell.GridX, originCell.GridY);
                int destinationIndex = _grid.ToUncheckedIndex(destinationCell.GridX, destinationCell.GridY);

                if (originPosition != _originPosition ||
                    originIndex != _originIndex ||
                    !_grid.IsWalkable(originCell.GridX, originCell.GridY) ||
                    !_grid.IsWalkable(destinationCell.GridX, destinationCell.GridY) ||
                    !float.IsFinite(_distances[destinationIndex]))
                {
                    distanceMeters = 0.0f;
                    return false;
                }

                if (destinationIndex == _originIndex)
                {
                    distanceMeters = HorizontalDistance(originPosition, destinationPosition);
                    return true;
                }

                distanceMeters = _distances[destinationIndex] +
                    HorizontalDistance(destinationCell.WorldPosition, destinationPosition);
                return true;
            }
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
