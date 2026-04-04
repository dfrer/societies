using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeCatalogLoaderTests
    {
        [Fact]
        public void LoadFromDirectory_LoadsExpectedDefaultScenarioAndCoreCounts()
        {
            PrototypeCatalogBundle bundle = PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());

            PrototypeScenarioDefinition scenario = bundle.Scenarios.ResolveDefault();

            Assert.Equal("balanced_basin", scenario.Id);
            Assert.Equal(1337, scenario.SimulationSeed);
            Assert.Equal(36, scenario.InitialTrees);
            Assert.Equal(24, scenario.InitialRocks);
            Assert.Equal(14, scenario.InitialBerryBushes);
            Assert.Equal(3, scenario.InitialWorkers);
            Assert.Equal(2.0f, scenario.WorldGen.CellSizeMeters);
            Assert.Equal(10.0f, scenario.WorldGen.HeightAmplitude);
            Assert.Equal(7, scenario.ResourceClusters.WoodClusters);
            Assert.Equal(5, scenario.ResourceClusters.StoneClusters);
            Assert.Equal(4, scenario.ResourceClusters.BerryClusters);
        }

        [Fact]
        public void LoadFromDirectory_ContainsPlannedV2ScenarioAndStructureCatalogEntries()
        {
            PrototypeCatalogBundle bundle = PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());

            Assert.Contains(bundle.Scenarios.Scenarios, scenario => scenario.Id == "wetland_builder");
            Assert.Contains(bundle.Resources.Resources, resource => resource.Id == "firewood");
            Assert.Contains(bundle.Structures.Structures, structure => structure.Id == "kiln");
            Assert.Contains(bundle.RoleQuotas.Roles, role => role.RoleId == "haulers");
            Assert.Equal(1.0d, bundle.RoleQuotas.Roles.Sum(role => role.Share), 3);
            Assert.All(bundle.Scenarios.Scenarios, scenario =>
            {
                Assert.True(scenario.WorldGen.CellSizeMeters > 0.0f);
                Assert.True(scenario.WorldGen.MaxSettlementPlacementAttempts > 0);
                Assert.True(scenario.ResourceClusters.WoodClusters > 0);
                Assert.True(scenario.ResourceClusters.StoneClusters > 0);
                Assert.True(scenario.ResourceClusters.BerryClusters > 0);
            });
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
