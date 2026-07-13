using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class NavigationDistanceReplayTests
    {
        [Fact]
        public void Constructor_DefaultsToCachedDistanceOnlyAndExposesReferenceMode()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation optimized = new(scenario, bundle.RoleQuotas.Roles, world);
            PrototypeSettlementSimulation reference = new(
                scenario,
                bundle.RoleQuotas.Roles,
                world,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);

            Assert.Equal(PrototypeRouteDistanceMode.CachedDistanceOnly, optimized.RouteDistanceMode);
            Assert.Equal(PrototypeRouteDistanceMode.FullMaterializationReference, reference.RouteDistanceMode);
            Assert.Equal(0, optimized.CachedRouteDistanceFastPathHits);
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        [Theory]
        [InlineData(".", 1.0f, 0, 0, 0, 0, 0.41f, -0.39f, -0.42f, 0.38f)]
        [InlineData(".....", 1.0f, 0, 0, 4, 0, 0.31f, 0.22f, -0.29f, -0.24f)]
        [InlineData(".../.../...", 1.0f, 0, 0, 2, 2, 0.33f, 0.27f, -0.31f, -0.26f)]
        [InlineData("...../.###./.....", 1.0f, 0, 1, 4, 1, 0.28f, -0.32f, -0.27f, 0.34f)]
        [InlineData(".......", 0.1f, 0, 0, 6, 0, 0.021f, -0.019f, -0.023f, 0.017f)]
        public void DistanceReplay_MatchesFullMaterializationBitExactly(
            string encodedMap,
            float cellSize,
            int startX,
            int startY,
            int destinationX,
            int destinationY,
            float startOffsetX,
            float startOffsetZ,
            float destinationOffsetX,
            float destinationOffsetZ)
        {
            WorldMapState map = BuildMap(cellSize, encodedMap.Split('/'));
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 4);
            Vector3 start = map.GetCell(startX, startY).WorldPosition + new Vector3(startOffsetX, 0.17f, startOffsetZ);
            Vector3 destination = map.GetCell(destinationX, destinationY).WorldPosition +
                new Vector3(destinationOffsetX, -0.11f, destinationOffsetZ);

            Assert.True(grid.TryFindPath(start, destination, out PrototypePathPlan? original));
            Assert.True(grid.TryMaterializePath(start, destination, original!.Cells, out PrototypePathPlan? materialized));
            Assert.True(grid.TryComputeMaterializedPathDistance(start, destination, original.Cells, out float replayed));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(materialized!.TotalDistanceMeters),
                BitConverter.SingleToInt32Bits(replayed));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DistanceReplay_MatchesBuiltPathRouteVersionsBitExactly(bool useBuiltPaths)
        {
            WorldMapState map = BuildMap(
                1.0f,
                ".......",
                ".......",
                ".......");
            Vector3 start = map.GetCell(0, 1).WorldPosition + new Vector3(0.37f, 0.0f, 0.31f);
            Vector3 destination = map.GetCell(6, 1).WorldPosition + new Vector3(-0.39f, 0.0f, -0.33f);
            IReadOnlyCollection<Vector2I> builtPathCells = useBuiltPaths
                ? new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(2, 0), new Vector2I(3, 0), new Vector2I(4, 0), new Vector2I(5, 0), new Vector2I(6, 0) }
                : Array.Empty<Vector2I>();
            PrototypeNavigationGrid grid = new(map, builtPathCells, rulesVersion: useBuiltPaths ? 2 : 1);

            Assert.True(grid.TryFindPath(start, destination, out PrototypePathPlan? plan));
            Assert.True(grid.TryComputeMaterializedPathDistance(start, destination, plan!.Cells, out float replayed));
            Assert.Equal(BitConverter.SingleToInt32Bits(plan.TotalDistanceMeters), BitConverter.SingleToInt32Bits(replayed));
        }

        [Fact]
        public void DistanceReplay_MatchesMaterializationRejectionForBlockedDisconnectedAndMalformedPaths()
        {
            WorldMapState map = BuildMap(
                1.0f,
                ".#.",
                ".#.",
                ".#.");
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 1);
            Vector3 left = map.GetCell(0, 1).WorldPosition;
            Vector3 blocked = map.GetCell(1, 1).WorldPosition;
            Vector3 disconnected = map.GetCell(2, 1).WorldPosition;

            Assert.False(grid.TryFindPath(left, disconnected, out _));
            AssertReplayMatchesMaterialization(grid, left, disconnected, Array.Empty<Vector2I>());
            AssertReplayMatchesMaterialization(grid, left, blocked, new[] { new Vector2I(0, 1), new Vector2I(1, 1) });
            AssertReplayMatchesMaterialization(grid, left, disconnected, new[] { new Vector2I(0, 0), new Vector2I(2, 1) });
            AssertReplayMatchesMaterialization(grid, left, disconnected, new[] { new Vector2I(0, 1), new Vector2I(2, 0) });
        }

        private static void AssertReplayMatchesMaterialization(
            PrototypeNavigationGrid grid,
            Vector3 start,
            Vector3 destination,
            IReadOnlyList<Vector2I> cells)
        {
            bool materialized = grid.TryMaterializePath(start, destination, cells, out PrototypePathPlan? plan);
            bool replayed = grid.TryComputeMaterializedPathDistance(start, destination, cells, out float distance);

            Assert.Equal(materialized, replayed);
            if (materialized)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(plan!.TotalDistanceMeters), BitConverter.SingleToInt32Bits(distance));
            }
        }

        private static WorldMapState BuildMap(float cellSize, params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
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
                        WorldPosition = new Vector3(origin + (x * cellSize), 0.0f, origin + (y * cellSize)),
                        MovementCost = 1.0f + (((x * 3) + y) % 4 * 0.15f),
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
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }
    }
}
