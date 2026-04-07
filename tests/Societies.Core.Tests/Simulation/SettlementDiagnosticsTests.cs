using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class SettlementDiagnosticsTests
    {
        [Fact]
        public void Diagnostics_TracksWorkOrdersGeneratedEachTick()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            PrototypeSettlementTickResult result = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.True(simulation.Diagnostics.WorkOrdersGenerated > 0, "Should generate work orders on the first tick");
            Assert.True(simulation.Diagnostics.CitizensEvaluated == scenario.InitialCitizens, "Should evaluate all citizens");
        }

        [Fact]
        public void Diagnostics_WorkOrdersClaimedPlusRemainingEqualsGenerated()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            int totalGenerated = 0;
            int totalClaimed = 0;
            int totalRemaining = 0;

            for (int i = 0; i < 60; i++)
            {
                _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
                totalGenerated += simulation.Diagnostics.WorkOrdersGenerated;
                totalClaimed += simulation.Diagnostics.WorkOrdersClaimed;
                totalRemaining += simulation.Diagnostics.WorkOrdersRemaining;

                Assert.True(
                    simulation.Diagnostics.WorkOrdersGenerated == simulation.Diagnostics.WorkOrdersClaimed + simulation.Diagnostics.WorkOrdersRemaining,
                    $"Generated ({simulation.Diagnostics.WorkOrdersGenerated}) must equal claimed ({simulation.Diagnostics.WorkOrdersClaimed}) + remaining ({simulation.Diagnostics.WorkOrdersRemaining}) on tick {i}");
            }

            Assert.True(totalGenerated > 0);
            Assert.True(totalClaimed > 0);
        }

        [Fact]
        public void Diagnostics_PathPlanLookups_IncreaseDuringAssignment()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.True(simulation.Diagnostics.PathPlanLookups > 0, "Path plan lookups should occur during citizen assignment");
            Assert.True(
                simulation.Diagnostics.PathPlanCacheHits <= simulation.Diagnostics.PathPlanLookups,
                "Cache hits cannot exceed total lookups");
        }

        [Fact]
        public void Diagnostics_PeakOrdersTracksMaximumAcrossSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            for (int i = 0; i < 120; i++)
            {
                _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

                Assert.True(
                    simulation.Diagnostics.PeakOrdersThisSession >= simulation.Diagnostics.WorkOrdersGenerated,
                    "Peak orders must always be >= current orders");
            }

            Assert.True(simulation.Diagnostics.PeakOrdersThisSession > 0, "Should have observed at least some orders");
            Assert.True(
                simulation.Diagnostics.PeakOrdersThisSession >= simulation.Diagnostics.WorkOrdersGenerated,
                "Peak should still be >= current tick's orders");
        }

        [Fact]
        public void Diagnostics_CitizensEvaluatedMatchesWorkerCount()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.True(
                simulation.Workers.Count == simulation.Diagnostics.CitizensEvaluated,
                $"Expected {simulation.Workers.Count} citizens evaluated, got {simulation.Diagnostics.CitizensEvaluated}");
        }

        private static PrototypeSettlementSimulation New(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas,
            WorldGenerationResult world)
        {
            return new(scenario, roleQuotas, world);
        }

        private static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world)
        {
            return world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();
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
