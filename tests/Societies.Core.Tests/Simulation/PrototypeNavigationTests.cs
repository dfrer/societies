using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeNavigationTests
    {
        [Fact]
        public void FindPath_IsDeterministicForSameWorldAndQuery()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeNavigationGrid grid = new(world.WorldMap, new HashSet<Vector2I>(), rulesVersion: 1);

            Vector3 start = world.SettlementSpawn.AnchorPosition;
            Vector3 destination = world.ResourceClusters.First(cluster => cluster.ResourceId == "logs").CenterPosition;

            PrototypePathPlan first = grid.FindPath(start, destination);
            PrototypePathPlan second = grid.FindPath(start, destination);

            Assert.Equal(first.TotalCost, second.TotalCost, 3);
            Assert.Equal(first.TotalDistanceMeters, second.TotalDistanceMeters, 3);
            Assert.Equal(first.Cells, second.Cells);
        }

        [Fact]
        public void BuiltPathCells_ReduceRouteCost()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeNavigationGrid baseGrid = new(world.WorldMap, new HashSet<Vector2I>(), rulesVersion: 1);

            Vector3 start = world.SettlementSpawn.AnchorPosition;
            Vector3 destination = world.ResourceClusters.First(cluster => cluster.ResourceId == "logs").CenterPosition;
            PrototypePathPlan baseline = baseGrid.FindPath(start, destination);

            HashSet<Vector2I> builtPathCells = baseline.Cells.ToHashSet();
            PrototypeNavigationGrid improvedGrid = new(world.WorldMap, builtPathCells, rulesVersion: 2);
            PrototypePathPlan improved = improvedGrid.FindPath(start, destination);

            Assert.True(improved.TotalCost < baseline.TotalCost);
            Assert.True(improved.EstimatedTravelTicks <= baseline.EstimatedTravelTicks);
        }

        [Fact]
        public void TryFindPath_RejectsBlockedEndpointsAndDisconnectedDestinations()
        {
            WorldMapState blockedEndpointMap = BuildMap(
                ".#.",
                "...",
                "...");
            PrototypeNavigationGrid blockedEndpointGrid = new(blockedEndpointMap, new HashSet<Vector2I>(), rulesVersion: 1);

            Assert.False(blockedEndpointGrid.TryFindPath(
                blockedEndpointMap.GetCell(1, 0).WorldPosition,
                blockedEndpointMap.GetCell(1, 0).WorldPosition,
                out PrototypePathPlan? blockedSameCellPlan));
            Assert.Null(blockedSameCellPlan);
            Assert.False(blockedEndpointGrid.TryFindPath(
                blockedEndpointMap.GetCell(0, 0).WorldPosition,
                blockedEndpointMap.GetCell(1, 0).WorldPosition,
                out PrototypePathPlan? blockedDestinationPlan));
            Assert.Null(blockedDestinationPlan);
            Assert.False(blockedEndpointGrid.TryFindPath(
                blockedEndpointMap.GetCell(1, 0).WorldPosition,
                blockedEndpointMap.GetCell(0, 0).WorldPosition,
                out PrototypePathPlan? blockedStartPlan));
            Assert.Null(blockedStartPlan);

            WorldMapState disconnectedMap = BuildMap(
                ".#.",
                ".#.",
                ".#.");
            PrototypeNavigationGrid disconnectedGrid = new(disconnectedMap, new HashSet<Vector2I>(), rulesVersion: 2);

            Assert.False(disconnectedGrid.TryFindPath(
                disconnectedMap.GetCell(0, 1).WorldPosition,
                disconnectedMap.GetCell(2, 1).WorldPosition,
                out PrototypePathPlan? disconnectedPlan));
            Assert.Null(disconnectedPlan);
        }

        [Fact]
        public void TryFindPath_PreventsDiagonalCornerCutting()
        {
            WorldMapState blockedCornerMap = BuildMap(
                ".#",
                "#.");
            PrototypeNavigationGrid blockedCornerGrid = new(blockedCornerMap, new HashSet<Vector2I>(), rulesVersion: 1);

            Assert.False(blockedCornerGrid.TryFindPath(
                blockedCornerMap.GetCell(0, 0).WorldPosition,
                blockedCornerMap.GetCell(1, 1).WorldPosition,
                out _));

            WorldMapState openCornerMap = BuildMap(
                "..",
                "..");
            PrototypeNavigationGrid openCornerGrid = new(openCornerMap, new HashSet<Vector2I>(), rulesVersion: 1);

            Assert.True(openCornerGrid.TryFindPath(
                openCornerMap.GetCell(0, 0).WorldPosition,
                openCornerMap.GetCell(1, 1).WorldPosition,
                out PrototypePathPlan? openCornerPlan));
            Assert.Equal(
                new[] { new Vector2I(0, 0), new Vector2I(1, 1) },
                openCornerPlan!.Cells);
        }

        [Fact]
        public void TryFindPath_CostMatchesDijkstraWithBuiltPathDiscounts()
        {
            WorldMapState map = BuildMap(
                ".....",
                ".#...",
                ".#.#.",
                "...#.",
                ".....");
            HashSet<Vector2I> builtPathCells = new()
            {
                new(1, 0),
                new(2, 0),
                new(3, 1),
                new(4, 2)
            };
            PrototypeNavigationGrid grid = new(map, builtPathCells, rulesVersion: 4);
            Vector2I start = new(0, 0);
            Vector2I destination = new(4, 4);

            Assert.True(grid.TryFindPath(
                map.GetCell(start.X, start.Y).WorldPosition,
                map.GetCell(destination.X, destination.Y).WorldPosition,
                out PrototypePathPlan? plan));

            float expectedCost = ComputeDijkstraCost(grid, map, start, destination);
            Assert.Equal(expectedCost, plan!.TotalCost, 4);
        }

        [Fact]
        public void TryFindPath_RepeatsReachabilityCellsWaypointsAndCost()
        {
            WorldMapState map = BuildMap(
                "....",
                ".#..",
                "....",
                "....");
            PrototypeNavigationGrid grid = new(map, new HashSet<Vector2I>(), rulesVersion: 7);
            Vector3 start = map.GetCell(0, 0).WorldPosition;
            Vector3 destination = map.GetCell(3, 3).WorldPosition;

            Assert.True(grid.TryFindPath(start, destination, out PrototypePathPlan? first));
            Assert.True(grid.TryFindPath(start, destination, out PrototypePathPlan? second));
            Assert.Equal(first!.Cells, second!.Cells);
            Assert.Equal(first.Waypoints, second.Waypoints);
            Assert.Equal(first.TotalCost, second.TotalCost, 5);
            Assert.Equal(first.TotalDistanceMeters, second.TotalDistanceMeters, 5);

            WorldMapState disconnectedMap = BuildMap(
                ".#.",
                ".#.",
                ".#.");
            PrototypeNavigationGrid disconnectedGrid = new(disconnectedMap, new HashSet<Vector2I>(), rulesVersion: 7);
            Assert.False(disconnectedGrid.TryFindPath(
                disconnectedMap.GetCell(0, 0).WorldPosition,
                disconnectedMap.GetCell(2, 2).WorldPosition,
                out _));
            Assert.False(disconnectedGrid.TryFindPath(
                disconnectedMap.GetCell(0, 0).WorldPosition,
                disconnectedMap.GetCell(2, 2).WorldPosition,
                out _));
        }

        [Fact]
        public void TryMaterializePath_PreservesExactEndpointsForCachedCellRoute()
        {
            WorldMapState map = BuildMap(
                "....",
                "....");
            PrototypeNavigationGrid grid = new(map, new HashSet<Vector2I>(), rulesVersion: 3);
            Vector3 canonicalStart = map.GetCell(0, 0).WorldPosition;
            Vector3 canonicalDestination = map.GetCell(3, 1).WorldPosition;
            Assert.True(grid.TryFindPath(canonicalStart, canonicalDestination, out PrototypePathPlan? canonicalPlan));

            Vector3 exactStart = canonicalStart + new Vector3(0.21f, 0.13f, 0.16f);
            Vector3 exactDestination = canonicalDestination + new Vector3(-0.18f, 0.09f, -0.12f);
            Assert.True(grid.TryMaterializePath(
                exactStart,
                exactDestination,
                canonicalPlan!.Cells,
                out PrototypePathPlan? rematerializedPlan));

            Assert.True(rematerializedPlan!.Waypoints[0].IsEqualApprox(exactStart));
            Assert.True(rematerializedPlan.Waypoints[^1].IsEqualApprox(exactDestination));
            Assert.Equal(canonicalPlan.Cells, rematerializedPlan.Cells);
            Assert.Equal(canonicalPlan.Query, rematerializedPlan.Query);
        }

        private static WorldMapState BuildMap(params string[] rows)
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
                        MovementCost = 1.0f + (((x * 3) + y) % 4 * 0.15f),
                        SlopeDegrees = 0.0f,
                        Biome = blocked ? BiomeType.Wetland : BiomeType.Meadow,
                        IsBuildable = !blocked
                    };
                }
            }

            return new WorldMapState(width, height, cellSize, worldSize, cells);
        }

        private static float ComputeDijkstraCost(
            PrototypeNavigationGrid grid,
            WorldMapState map,
            Vector2I start,
            Vector2I destination)
        {
            Dictionary<Vector2I, float> distances = new() { [start] = 0.0f };
            HashSet<Vector2I> visited = new();

            while (true)
            {
                KeyValuePair<Vector2I, float>? currentEntry = distances
                    .Where(pair => !visited.Contains(pair.Key))
                    .OrderBy(pair => pair.Value)
                    .ThenBy(pair => pair.Key.Y)
                    .ThenBy(pair => pair.Key.X)
                    .Cast<KeyValuePair<Vector2I, float>?>()
                    .FirstOrDefault();
                Assert.True(currentEntry.HasValue, "Dijkstra reference expected the destination to be reachable.");
                Vector2I current = currentEntry!.Value.Key;
                float currentCost = currentEntry.Value.Value;
                if (current == destination)
                {
                    return currentCost;
                }

                visited.Add(current);
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        Vector2I next = new(current.X + offsetX, current.Y + offsetY);
                        TerrainCell? nextTerrain = map.TryGetCell(next.X, next.Y);
                        if (nextTerrain == null || !grid.GetCell(next.X, next.Y).IsWalkable)
                        {
                            continue;
                        }

                        bool diagonal = offsetX != 0 && offsetY != 0;
                        if (diagonal &&
                            (!grid.GetCell(current.X + offsetX, current.Y).IsWalkable ||
                             !grid.GetCell(current.X, current.Y + offsetY).IsWalkable))
                        {
                            continue;
                        }

                        float stepDistance = diagonal ? 1.4142135f : 1.0f;
                        float candidateCost = currentCost + (stepDistance * grid.GetTraversalMultiplier(next.X, next.Y));
                        if (!distances.TryGetValue(next, out float existingCost) || candidateCost < existingCost)
                        {
                            distances[next] = candidateCost;
                        }
                    }
                }
            }
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string? current = baseDirectory;

            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            throw new DirectoryNotFoundException($"Could not find src/societies/data from '{baseDirectory}'.");
        }
    }
}
