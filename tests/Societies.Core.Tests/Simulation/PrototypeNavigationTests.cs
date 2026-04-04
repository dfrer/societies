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
