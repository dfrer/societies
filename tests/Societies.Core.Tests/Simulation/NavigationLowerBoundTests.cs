using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class NavigationLowerBoundTests
    {
        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void GeometricDistanceField_IsConservativeAcrossShippedScenarios(string scenarioId)
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve(scenarioId);
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeNavigationGrid grid = new(world.WorldMap, Array.Empty<Vector2I>(), rulesVersion: 1);
            TerrainCell originCell = world.WorldMap.GetNearestCell(world.SettlementSpawn.AnchorPosition);
            Vector3 origin = originCell.WorldPosition + new Vector3(
                world.WorldMap.CellSizeMeters * 0.31f,
                0.0f,
                -world.WorldMap.CellSizeMeters * 0.27f);
            PrototypeNavigationGrid.GeometricDistanceField field = grid.BuildGeometricDistanceField(origin);
            TerrainCell[] destinations = world.WorldMap.Cells
                .Where(cell => grid.GetCell(cell.GridX, cell.GridY).IsWalkable)
                .Where((_, index) => index % 79 == 0)
                .Take(24)
                .ToArray();

            Assert.NotEmpty(destinations);
            foreach (TerrainCell destinationCell in destinations)
            {
                Vector3 destination = destinationCell.WorldPosition + new Vector3(
                    -world.WorldMap.CellSizeMeters * 0.23f,
                    0.0f,
                    world.WorldMap.CellSizeMeters * 0.19f);
                bool reachable = grid.TryFindPath(origin, destination, out PrototypePathPlan? plan);
                bool bounded = field.TryGetExactEndpointDistanceLowerBound(origin, destination, out float lowerBound);

                Assert.Equal(reachable, bounded);
                if (reachable)
                {
                    Assert.True(
                        lowerBound <= plan!.TotalDistanceMeters,
                        $"{scenarioId}: bound {lowerBound} exceeded exact distance {plan.TotalDistanceMeters} " +
                        $"for ({originCell.GridX},{originCell.GridY})->({destinationCell.GridX},{destinationCell.GridY}).");
                }
            }
        }

        [Fact]
        public void GeometricDistanceField_HandlesSameCellOffsetsBlockedAndDisconnectedCells()
        {
            WorldMapState map = BuildMap(
                ".#.",
                ".#.",
                ".#.");
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 1);
            Vector3 origin = map.GetCell(0, 1).WorldPosition + new Vector3(0.42f, 0.0f, -0.41f);
            PrototypeNavigationGrid.GeometricDistanceField field = grid.BuildGeometricDistanceField(origin);
            Vector3 sameCellDestination = map.GetCell(0, 1).WorldPosition + new Vector3(-0.43f, 0.0f, 0.4f);

            Assert.True(grid.TryFindPath(origin, sameCellDestination, out PrototypePathPlan? sameCellPlan));
            Assert.True(field.TryGetExactEndpointDistanceLowerBound(origin, sameCellDestination, out float sameCellBound));
            Assert.Equal(BitConverter.SingleToInt32Bits(sameCellPlan!.TotalDistanceMeters), BitConverter.SingleToInt32Bits(sameCellBound));

            Assert.False(field.TryGetExactEndpointDistanceLowerBound(
                origin,
                map.GetCell(1, 1).WorldPosition,
                out _));
            Assert.False(field.TryGetExactEndpointDistanceLowerBound(
                origin,
                map.GetCell(2, 1).WorldPosition,
                out _));

            Vector3 blockedOrigin = map.GetCell(1, 1).WorldPosition;
            PrototypeNavigationGrid.GeometricDistanceField blockedField = grid.BuildGeometricDistanceField(blockedOrigin);
            Assert.False(blockedField.TryGetExactEndpointDistanceLowerBound(
                blockedOrigin,
                map.GetCell(0, 1).WorldPosition,
                out _));
        }

        [Fact]
        public void GeometricDistanceField_RemainsConservativeWhenBuiltPathsChangeTheChosenRoute()
        {
            WorldMapState map = BuildMap(
                ".......",
                ".......",
                ".......");
            Vector3 origin = map.GetCell(0, 1).WorldPosition + new Vector3(0.37f, 0.0f, 0.31f);
            Vector3 destination = map.GetCell(6, 1).WorldPosition + new Vector3(-0.39f, 0.0f, -0.33f);
            HashSet<Vector2I> builtPathCells = new()
            {
                new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0), new(6, 0)
            };
            PrototypeNavigationGrid grid = new(map, builtPathCells, rulesVersion: 2);
            PrototypeNavigationGrid.GeometricDistanceField field = grid.BuildGeometricDistanceField(origin);

            Assert.True(grid.TryFindPath(origin, destination, out PrototypePathPlan? plan));
            Assert.True(field.TryGetExactEndpointDistanceLowerBound(origin, destination, out float lowerBound));
            Assert.True(lowerBound <= plan!.TotalDistanceMeters);
        }

        [Fact]
        public void GeometricDistanceField_UsesMaterializedFloatLegsForNonBinaryCellSizes()
        {
            WorldMapState map = BuildMapWithCellSize(0.1f, ".......");
            Vector3 origin = map.GetCell(0, 0).WorldPosition;
            Vector3 destination = map.GetCell(6, 0).WorldPosition;
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 1);
            PrototypeNavigationGrid.GeometricDistanceField field = grid.BuildGeometricDistanceField(origin);

            Assert.True(grid.TryFindPath(origin, destination, out PrototypePathPlan? plan));
            Assert.True(field.TryGetExactEndpointDistanceLowerBound(origin, destination, out float lowerBound));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(plan!.TotalDistanceMeters),
                BitConverter.SingleToInt32Bits(lowerBound));
        }

        [Fact]
        public void GeometricDistanceField_RejectsAnExactOriginFromAnotherCell()
        {
            WorldMapState map = BuildMap("...");
            Vector3 origin = map.GetCell(0, 0).WorldPosition;
            PrototypeNavigationGrid grid = new(map, Array.Empty<Vector2I>(), rulesVersion: 1);
            PrototypeNavigationGrid.GeometricDistanceField field = grid.BuildGeometricDistanceField(origin);

            Assert.False(field.TryGetExactEndpointDistanceLowerBound(
                origin + new Vector3(0.1f, 0.0f, 0.0f),
                map.GetCell(2, 0).WorldPosition,
                out _));
            Assert.False(field.TryGetExactEndpointDistanceLowerBound(
                map.GetCell(1, 0).WorldPosition,
                map.GetCell(2, 0).WorldPosition,
                out _));
        }

        private static WorldMapState BuildMap(params string[] rows)
        {
            return BuildMapWithCellSize(1.0f, rows);
        }

        private static WorldMapState BuildMapWithCellSize(float cellSize, params string[] rows)
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
                        WorldPosition = new Vector3(
                            origin + (x * cellSize),
                            0.0f,
                            origin + (y * cellSize)),
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
