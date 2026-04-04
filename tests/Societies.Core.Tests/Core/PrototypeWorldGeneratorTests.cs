using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeWorldGeneratorTests
    {
        [Fact]
        public void Generate_SameScenarioAndSeed_IsDeterministic()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");

            WorldGenerationResult first = PrototypeWorldGenerator.Generate(scenario);
            WorldGenerationResult second = PrototypeWorldGenerator.Generate(scenario);

            Assert.Equal(first.WorldSeed, second.WorldSeed);
            Assert.Equal(first.WorldHash, second.WorldHash);
            Assert.Equal(first.SettlementSpawn.AnchorPosition, second.SettlementSpawn.AnchorPosition);
            Assert.Equal(first.ResourceClusters.Select(cluster => cluster.CenterPosition), second.ResourceClusters.Select(cluster => cluster.CenterPosition));
        }

        [Fact]
        public void Generate_PreservesScenarioResourceCountsAndBiomePlacementRules()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            WorldGenerationResult result = PrototypeWorldGenerator.Generate(scenario);

            Assert.Equal(scenario.InitialTrees, result.ResourceSpawns.Count(spawn => spawn.ResourceId == "wood"));
            Assert.Equal(scenario.InitialRocks, result.ResourceSpawns.Count(spawn => spawn.ResourceId == "stone"));
            Assert.Equal(scenario.InitialBerryBushes, result.ResourceSpawns.Count(spawn => spawn.ResourceId == "berry"));

            foreach (PrototypeResourceSpawn spawn in result.ResourceSpawns)
            {
                TerrainCell cell = result.WorldMap.GetNearestCell(spawn.Position);
                switch (spawn.ResourceId)
                {
                    case "wood":
                        Assert.Equal(BiomeType.Forest, cell.Biome);
                        break;
                    case "stone":
                        Assert.Equal(BiomeType.RockyUpland, cell.Biome);
                        break;
                    case "berry":
                        Assert.Equal(BiomeType.Meadow, cell.Biome);
                        Assert.True(result.WorldMap.HasAdjacentBiome(cell.GridX, cell.GridY, BiomeType.Forest));
                        break;
                }
            }
        }

        [Fact]
        public void Generate_StarterSettlementSatisfiesPlacementGuarantees()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            WorldGenerationResult result = PrototypeWorldGenerator.Generate(scenario);
            TerrainCell anchorCell = result.WorldMap.GetNearestCell(result.SettlementSpawn.AnchorPosition);

            Assert.True(anchorCell.IsBuildable);
            Assert.Equal(BiomeType.Meadow, anchorCell.Biome);
            Assert.InRange(result.StarterResourceDistances["wood"], 0.0f, 40.0f);
            Assert.InRange(result.StarterResourceDistances["berry"], 0.0f, 50.0f);
            Assert.InRange(result.StarterResourceDistances["stone"], 0.0f, 65.0f);
        }

        [Fact]
        public void Generate_ScenariosMeetDifferentiationEnvelopes()
        {
            PrototypeScenarioCatalog catalog = LoadCatalogs().Scenarios;
            WorldGenerationResult balanced = PrototypeWorldGenerator.Generate(catalog.Resolve("balanced_basin"));
            WorldGenerationResult quarry = PrototypeWorldGenerator.Generate(catalog.Resolve("long_haul_quarry"));
            WorldGenerationResult highlands = PrototypeWorldGenerator.Generate(catalog.Resolve("food_poor_highlands"));
            WorldGenerationResult wetland = PrototypeWorldGenerator.Generate(catalog.Resolve("wetland_builder"));

            float balancedBuildableRatio = ComputeBuildableRatio(balanced);
            float highlandsBuildableRatio = ComputeBuildableRatio(highlands);
            float wetlandBuildableRatio = ComputeBuildableRatio(wetland);
            float wetlandWetRatio = ComputeBiomeRatio(wetland, BiomeType.Wetland);
            float balancedStoneDistance = balanced.AverageClusterDistances["stone"];
            float quarryStoneDistance = quarry.AverageClusterDistances["stone"];

            Assert.True(highlandsBuildableRatio < 0.40f, $"Expected highlands buildable ratio < 0.40 but was {highlandsBuildableRatio:0.000}");
            Assert.True(wetlandWetRatio > 0.18f, $"Expected wetland ratio > 0.18 but was {wetlandWetRatio:0.000}");
            Assert.True(quarryStoneDistance >= balancedStoneDistance * 1.4f, $"Expected quarry stone distance >= {balancedStoneDistance * 1.4f:0.000} but was {quarryStoneDistance:0.000}");
            Assert.True(balancedBuildableRatio > wetlandBuildableRatio, $"Expected basin buildable ratio > wetland buildable ratio but {balancedBuildableRatio:0.000} <= {wetlandBuildableRatio:0.000}");
        }

        private static float ComputeBuildableRatio(WorldGenerationResult result)
        {
            int buildableCells = result.WorldMap.Cells.Count(cell => cell.IsBuildable);
            return buildableCells / (float)result.WorldMap.Cells.Count;
        }

        private static float ComputeBiomeRatio(WorldGenerationResult result, BiomeType biome)
        {
            int matching = result.WorldMap.Cells.Count(cell => cell.Biome == biome);
            return matching / (float)result.WorldMap.Cells.Count;
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
