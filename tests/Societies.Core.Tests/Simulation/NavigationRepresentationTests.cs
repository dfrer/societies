using Godot;
using Societies.Simulation;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class NavigationRepresentationTests
    {
        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void DensePooledAStar_MatchesDictionaryOracleAcrossScenarioQueriesAndBuiltPaths(string scenarioId)
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve(scenarioId);
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            List<Vector3> endpoints = world.WorldMap.Cells
                .Where(IsWalkable)
                .Where((_, index) => index % Math.Max(1, world.WorldMap.Cells.Count / 10) == 0)
                .Select(cell => cell.WorldPosition)
                .Take(10)
                .ToList();
            endpoints.Insert(0, world.SettlementSpawn.AnchorPosition);
            endpoints.AddRange(world.ResourceSpawns.Take(8).Select(resource => resource.Position));

            CompareQueries(world.WorldMap, Array.Empty<Vector2I>(), endpoints);

            HashSet<Vector2I> builtPathCells = world.WorldMap.Cells
                .Where(IsWalkable)
                .Where(cell => ((cell.GridX * 7) + (cell.GridY * 11)) % 17 == 0)
                .Select(cell => new Vector2I(cell.GridX, cell.GridY))
                .ToHashSet();
            CompareQueries(world.WorldMap, builtPathCells, endpoints);
        }

        [Fact]
        public void DensePooledAStar_MatchesDictionaryOracleForBlockedUnreachableCornerAndTieCases()
        {
            WorldMapState[] maps =
            {
                BuildMap(".#.", "...", "..."),
                BuildMap(".#.", ".#.", ".#."),
                BuildMap(".#", "#."),
                BuildMap("..", ".."),
                BuildMap(".....", ".....", ".....", ".....", ".....", uniformCost: true)
            };

            foreach (WorldMapState map in maps)
            {
                List<Vector3> endpoints = map.Cells.Select(cell => cell.WorldPosition).ToList();
                CompareQueries(map, Array.Empty<Vector2I>(), endpoints);
            }
        }

        [Fact]
        public void DensePooledAStar_ReusedWorkspaceMatchesDictionaryOracleAcrossGenerationWrap()
        {
            WorldMapState map = BuildMap(".....", ".#...", ".....", "...#.", ".....");
            PrototypeNavigationGrid optimized = new(map, Array.Empty<Vector2I>(), rulesVersion: 9);
            DictionaryNavigationOracle reference = new(map, Array.Empty<Vector2I>(), rulesVersion: 9);
            Vector3 start = map.GetCell(0, 0).WorldPosition;
            Vector3 destination = map.GetCell(4, 4).WorldPosition;

            AssertEquivalent(reference, optimized, start, destination);
            SetPooledWorkspaceGeneration(optimized, int.MaxValue);
            AssertEquivalent(reference, optimized, destination, start);
            AssertEquivalent(reference, optimized, start, destination);
        }

        [Fact]
        public void DensePooledAStar_IsExactUnderConcurrentWorkspaceReuse()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            HashSet<Vector2I> builtPathCells = world.WorldMap.Cells
                .Where(IsWalkable)
                .Where(cell => (cell.GridX + cell.GridY) % 13 == 0)
                .Select(cell => new Vector2I(cell.GridX, cell.GridY))
                .ToHashSet();
            PrototypeNavigationGrid optimized = new(world.WorldMap, builtPathCells, rulesVersion: 12);
            DictionaryNavigationOracle reference = new(world.WorldMap, builtPathCells, rulesVersion: 12);
            Vector3 start = world.SettlementSpawn.AnchorPosition;
            Vector3[] destinations = world.WorldMap.Cells
                .Where(IsWalkable)
                .Where((_, index) => index % Math.Max(1, world.WorldMap.Cells.Count / 16) == 0)
                .Take(16)
                .Select(cell => cell.WorldPosition)
                .ToArray();
            OracleResult[] expected = destinations.Select(destination => reference.TryFindPath(start, destination)).ToArray();
            ConcurrentQueue<string> mismatches = new();

            Parallel.For(0, 128, iteration =>
            {
                int queryIndex = iteration % destinations.Length;
                OracleResult actual = Capture(optimized, start, destinations[queryIndex]);
                string? mismatch = DescribeMismatch(expected[queryIndex], actual);
                if (mismatch != null)
                {
                    mismatches.Enqueue($"iteration {iteration}: {mismatch}");
                }
            });

            Assert.True(mismatches.IsEmpty, string.Join(System.Environment.NewLine, mismatches));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(3, 0)]
        [InlineData(0, 2)]
        public void PublicCellAccessors_RejectOutOfBoundsCoordinatesWithoutAliasing(int gridX, int gridY)
        {
            WorldMapState map = BuildMap("...", "...");
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 1);

            Assert.Throws<KeyNotFoundException>(() => grid.GetCell(gridX, gridY));
            Assert.Throws<KeyNotFoundException>(() => grid.GetTraversalMultiplier(gridX, gridY));
        }

        private static void CompareQueries(
            WorldMapState map,
            IReadOnlyCollection<Vector2I> builtPathCells,
            IReadOnlyList<Vector3> endpoints)
        {
            PrototypeNavigationGrid optimized = new(map, builtPathCells, rulesVersion: 5);
            DictionaryNavigationOracle reference = new(map, builtPathCells, rulesVersion: 5);
            Vector3 anchor = endpoints[0];

            foreach (Vector3 endpoint in endpoints)
            {
                AssertEquivalent(reference, optimized, anchor, endpoint);
                AssertEquivalent(reference, optimized, endpoint, anchor);
                AssertEquivalent(reference, optimized, anchor, endpoint);
            }
        }

        private static void AssertEquivalent(
            DictionaryNavigationOracle reference,
            PrototypeNavigationGrid optimized,
            Vector3 start,
            Vector3 destination)
        {
            OracleResult expected = reference.TryFindPath(start, destination);
            OracleResult actual = Capture(optimized, start, destination);
            string? mismatch = DescribeMismatch(expected, actual);
            Assert.True(mismatch == null, mismatch);
        }

        private static OracleResult Capture(PrototypeNavigationGrid grid, Vector3 start, Vector3 destination)
        {
            bool reachable = grid.TryFindPath(start, destination, out PrototypePathPlan? plan);
            return new OracleResult(reachable, plan);
        }

        private static string? DescribeMismatch(OracleResult expected, OracleResult actual)
        {
            if (expected.Reachable != actual.Reachable)
            {
                return $"reachability expected={expected.Reachable} actual={actual.Reachable}";
            }

            if (!expected.Reachable)
            {
                return actual.Plan == null ? null : "unreachable result unexpectedly materialized a plan";
            }

            PrototypePathPlan expectedPlan = expected.Plan!;
            PrototypePathPlan actualPlan = actual.Plan!;
            if (expectedPlan.Query != actualPlan.Query)
            {
                return $"query expected={expectedPlan.Query} actual={actualPlan.Query}";
            }

            if (!expectedPlan.Cells.SequenceEqual(actualPlan.Cells))
            {
                return $"cells expected={string.Join(';', expectedPlan.Cells)} actual={string.Join(';', actualPlan.Cells)}";
            }

            if (expectedPlan.Waypoints.Count != actualPlan.Waypoints.Count)
            {
                return $"waypoint count expected={expectedPlan.Waypoints.Count} actual={actualPlan.Waypoints.Count}";
            }

            for (int index = 0; index < expectedPlan.Waypoints.Count; index++)
            {
                Vector3 expectedWaypoint = expectedPlan.Waypoints[index];
                Vector3 actualWaypoint = actualPlan.Waypoints[index];
                if (!SameBits(expectedWaypoint.X, actualWaypoint.X) ||
                    !SameBits(expectedWaypoint.Y, actualWaypoint.Y) ||
                    !SameBits(expectedWaypoint.Z, actualWaypoint.Z))
                {
                    return $"waypoint {index} differs: expected={expectedWaypoint} actual={actualWaypoint}";
                }
            }

            if (!SameBits(expectedPlan.TotalDistanceMeters, actualPlan.TotalDistanceMeters))
            {
                return $"distance bits expected={BitConverter.SingleToInt32Bits(expectedPlan.TotalDistanceMeters)} actual={BitConverter.SingleToInt32Bits(actualPlan.TotalDistanceMeters)}";
            }

            if (!SameBits(expectedPlan.TotalCost, actualPlan.TotalCost))
            {
                return $"cost bits expected={BitConverter.SingleToInt32Bits(expectedPlan.TotalCost)} actual={BitConverter.SingleToInt32Bits(actualPlan.TotalCost)}";
            }

            return expectedPlan.EstimatedTravelTicks == actualPlan.EstimatedTravelTicks
                ? null
                : $"travel ticks expected={expectedPlan.EstimatedTravelTicks} actual={actualPlan.EstimatedTravelTicks}";
        }

        private static bool SameBits(float left, float right) =>
            BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);

        private static bool IsWalkable(TerrainCell cell) =>
            cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f;

        private static void SetPooledWorkspaceGeneration(PrototypeNavigationGrid grid, int generation)
        {
            FieldInfo poolField = typeof(PrototypeNavigationGrid).GetField("_workspacePool", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object pool = poolField.GetValue(grid)!;
            MethodInfo tryTake = pool.GetType().GetMethod("TryTake")!;
            object?[] takeArguments = { null };
            Assert.True((bool)tryTake.Invoke(pool, takeArguments)!);
            object workspace = takeArguments[0]!;
            FieldInfo generationField = workspace.GetType().GetField("_generation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            generationField.SetValue(workspace, generation);
            pool.GetType().GetMethod("Add")!.Invoke(pool, new[] { workspace });
        }

        private static WorldMapState BuildMap(bool uniformCost = false, params string[] rows) =>
            BuildMap(rows, uniformCost);

        private static WorldMapState BuildMap(string row1, string row2, bool uniformCost) =>
            BuildMap(new[] { row1, row2 }, uniformCost);

        private static WorldMapState BuildMap(string row1, string row2, string row3, string row4, string row5, bool uniformCost) =>
            BuildMap(new[] { row1, row2, row3, row4, row5 }, uniformCost);

        private static WorldMapState BuildMap(params string[] rows) => BuildMap(rows, uniformCost: false);

        private static WorldMapState BuildMap(string[] rows, bool uniformCost)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            const float cellSize = 1.0f;
            float worldSize = Math.Max(width, height) * cellSize;
            float origin = -(worldSize * 0.5f) + (cellSize * 0.5f);
            TerrainCell[] cells = new TerrainCell[width * height];

            for (int y = 0; y < height; y++)
            {
                Assert.Equal(width, rows[y].Length);
                for (int x = 0; x < width; x++)
                {
                    bool blocked = rows[y][x] == '#';
                    cells[(y * width) + x] = new TerrainCell
                    {
                        GridX = x,
                        GridY = y,
                        WorldPosition = new Vector3(origin + x, 0.0f, origin + y),
                        MovementCost = uniformCost ? 1.0f : 1.0f + ((((x * 3) + y) % 4) * 0.15f),
                        SlopeDegrees = 0.0f,
                        Biome = blocked ? BiomeType.Wetland : BiomeType.Meadow,
                        IsBuildable = !blocked
                    };
                }
            }

            return new WorldMapState(width, height, cellSize, worldSize, cells);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }

        private readonly record struct OracleResult(bool Reachable, PrototypePathPlan? Plan);

        private sealed class DictionaryNavigationOracle
        {
            private const float BuiltPathCostMultiplier = 0.55f;

            private readonly WorldMapState _worldMap;
            private readonly Dictionary<Vector2I, PrototypeNavigationGridCell> _cells;
            private readonly float _minimumTraversalMultiplier;
            private readonly int _rulesVersion;

            public DictionaryNavigationOracle(WorldMapState worldMap, IReadOnlyCollection<Vector2I> builtPathCells, int rulesVersion)
            {
                _worldMap = worldMap;
                _rulesVersion = rulesVersion;
                HashSet<Vector2I> built = new(builtPathCells);
                _cells = new Dictionary<Vector2I, PrototypeNavigationGridCell>();
                foreach (TerrainCell cell in worldMap.Cells)
                {
                    Vector2I key = new(cell.GridX, cell.GridY);
                    _cells[key] = new PrototypeNavigationGridCell
                    {
                        GridX = cell.GridX,
                        GridY = cell.GridY,
                        WorldPosition = cell.WorldPosition,
                        MovementCost = cell.MovementCost,
                        IsWalkable = NavigationRepresentationTests.IsWalkable(cell),
                        HasBuiltPath = built.Contains(key)
                    };
                }

                _minimumTraversalMultiplier = _cells.Values
                    .Where(cell => cell.IsWalkable)
                    .Select(cell => cell.HasBuiltPath ? cell.MovementCost * BuiltPathCostMultiplier : cell.MovementCost)
                    .DefaultIfEmpty(1.0f)
                    .Min();
            }

            public OracleResult TryFindPath(Vector3 startPosition, Vector3 destinationPosition)
            {
                TerrainCell startCell = _worldMap.GetNearestCell(startPosition);
                TerrainCell destinationCell = _worldMap.GetNearestCell(destinationPosition);
                PrototypePathQuery query = new(startCell.GridX, startCell.GridY, destinationCell.GridX, destinationCell.GridY, _rulesVersion);
                if (!IsWalkable(startCell.GridX, startCell.GridY) || !IsWalkable(destinationCell.GridX, destinationCell.GridY))
                {
                    return new OracleResult(false, null);
                }

                if (startCell.GridX == destinationCell.GridX && startCell.GridY == destinationCell.GridY)
                {
                    float directDistance = HorizontalDistance(startPosition, destinationPosition);
                    float multiplier = GetTraversalMultiplier(startCell.GridX, startCell.GridY);
                    return new OracleResult(true, new PrototypePathPlan
                    {
                        Query = query,
                        Cells = new List<Vector2I> { new(startCell.GridX, startCell.GridY) },
                        Waypoints = new List<Vector3> { startPosition, destinationPosition },
                        TotalDistanceMeters = directDistance,
                        TotalCost = directDistance * multiplier,
                        EstimatedTravelTicks = Math.Max(4, Mathf.CeilToInt((directDistance * multiplier) / 0.78f))
                    });
                }

                if (!TryRunAStar(startCell, destinationCell, out List<Vector2I>? cells))
                {
                    return new OracleResult(false, null);
                }

                return new OracleResult(true, Materialize(startPosition, destinationPosition, query, cells!));
            }

            private bool TryRunAStar(TerrainCell start, TerrainCell destination, out List<Vector2I>? path)
            {
                Vector2I startKey = new(start.GridX, start.GridY);
                Vector2I destinationKey = new(destination.GridX, destination.GridY);
                PriorityQueue<Vector2I, (float EstimatedTotal, float Heuristic, int GridY, int GridX, long Sequence)> frontier = new();
                Dictionary<Vector2I, Vector2I> cameFrom = new();
                Dictionary<Vector2I, float> costSoFar = new() { [startKey] = 0.0f };
                long sequence = 0;
                frontier.Enqueue(startKey, (0.0f, 0.0f, startKey.Y, startKey.X, sequence++));

                while (frontier.Count > 0)
                {
                    Vector2I current = frontier.Dequeue();
                    if (current == destinationKey)
                    {
                        break;
                    }

                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                            {
                                continue;
                            }

                            Vector2I next = new(current.X + offsetX, current.Y + offsetY);
                            float stepDistance = offsetX != 0 && offsetY != 0 ? 1.4142135f : 1.0f;
                            if (!_cells.TryGetValue(next, out PrototypeNavigationGridCell? nextCell) || !nextCell.IsWalkable)
                            {
                                continue;
                            }

                            if (offsetX != 0 && offsetY != 0 &&
                                (!IsWalkable(next.X, current.Y) || !IsWalkable(current.X, next.Y)))
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

            private PrototypePathPlan Materialize(
                Vector3 startPosition,
                Vector3 destinationPosition,
                PrototypePathQuery query,
                IReadOnlyList<Vector2I> cellPath)
            {
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

            private float GetTraversalMultiplier(int x, int y)
            {
                PrototypeNavigationGridCell cell = _cells[new Vector2I(x, y)];
                return cell.HasBuiltPath ? cell.MovementCost * BuiltPathCostMultiplier : cell.MovementCost;
            }

            private float EstimateHeuristic(Vector2I current, Vector2I destination)
            {
                int deltaX = Math.Abs(current.X - destination.X);
                int deltaY = Math.Abs(current.Y - destination.Y);
                int diagonalSteps = Math.Min(deltaX, deltaY);
                int straightSteps = Math.Max(deltaX, deltaY) - diagonalSteps;
                return ((diagonalSteps * 1.4142135f) + straightSteps) * _minimumTraversalMultiplier;
            }

            private bool IsWalkable(int x, int y) =>
                _cells.TryGetValue(new Vector2I(x, y), out PrototypeNavigationGridCell? cell) && cell.IsWalkable;

            private static float HorizontalDistance(Vector3 a, Vector3 b) =>
                new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));

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
}
