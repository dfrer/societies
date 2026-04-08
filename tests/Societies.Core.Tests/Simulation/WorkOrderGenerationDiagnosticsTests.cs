using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class WorkOrderGenerationDiagnosticsTests
    {
        /// <summary>
        /// Verifies that WorkOrdersGeneratedUncapped always tracks the raw generation count
        /// before the frontier cap is applied, and is never negative.
        /// This is the primary diagnostic for frontier-cap pressure.
        /// </summary>
        [Fact]
        public void Diagnostics_UncappedField_NeverNegative()
        {
            var bundle = LoadCatalogs();
            var scenario = bundle.Scenarios.Resolve("balanced_basin");
            var world = PrototypeWorldGenerator.Generate(scenario);
            var sim = new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world);
            var resources = BuildResourceSites(world);

            for (int i = 0; i < 120; i++)
            {
                _ = sim.Advance(resources, 8.0f, PrototypeWeather.Clear);
                Assert.True(
                    sim.Diagnostics.WorkOrdersGeneratedUncapped >= 0,
                    $"WorkOrdersGeneratedUncapped was negative on tick {i}: {sim.Diagnostics.WorkOrdersGeneratedUncapped}");
            }
        }

        /// <summary>
        /// Verifies that WorkOrdersGeneratedUncapped >= WorkOrdersGenerated on every tick,
        /// because the uncapped count is the raw generation and the generated count is post-cap.
        /// </summary>
        [Fact]
        public void Diagnostics_UncappedField_AtLeastAsLargeAsCapped()
        {
            var bundle = LoadCatalogs();
            var scenario = bundle.Scenarios.Resolve("balanced_basin");
            var world = PrototypeWorldGenerator.Generate(scenario);
            var sim = new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world);
            var resources = BuildResourceSites(world);

            for (int i = 0; i < 120; i++)
            {
                _ = sim.Advance(resources, 8.0f, PrototypeWeather.Clear);
                Assert.True(
                    sim.Diagnostics.WorkOrdersGeneratedUncapped >= sim.Diagnostics.WorkOrdersGenerated,
                    $"Tick {i}: uncapped({sim.Diagnostics.WorkOrdersGeneratedUncapped}) < capped({sim.Diagnostics.WorkOrdersGenerated}) - diagnostics contract violated");
            }
        }

        /// <summary>
        /// Verifies that on the first tick, uncapped >= 1 (orders are always generated).
        /// </summary>
        [Fact]
        public void Diagnostics_UncappedField_PositiveOnFirstTick()
        {
            var bundle = LoadCatalogs();
            var scenario = bundle.Scenarios.Resolve("balanced_basin");
            var world = PrototypeWorldGenerator.Generate(scenario);
            var sim = new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world);
            var resources = BuildResourceSites(world);

            _ = sim.Advance(resources, 8.0f, PrototypeWeather.Clear);
            Assert.True(
                sim.Diagnostics.WorkOrdersGeneratedUncapped > 0,
                "Work orders should be generated on the first tick");
        }

        private static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world) =>
            world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();

        private static PrototypeCatalogBundle LoadCatalogs() =>
            PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDir());

        private static string GetCatalogDir()
        {
            string? cur = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(cur))
            {
                string c = Path.Combine(cur, "src", "societies", "data");
                if (Directory.Exists(c)) return c;
                var p = Directory.GetParent(cur);
                cur = p?.FullName;
            }
            throw new DirectoryNotFoundException("Cannot find src/societies/data");
        }
    }
}
