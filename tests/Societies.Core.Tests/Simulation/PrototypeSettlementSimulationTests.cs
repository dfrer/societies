using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeSettlementSimulationTests
    {
        [Fact]
        public void Advance_BalancedBasin_BuildsHutAndProducesMeals()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            List<PrototypeSettlementEvent> events = AdvanceSimulation(simulation, resources, 1200);

            Assert.Contains(simulation.Structures, structure => structure.StructureKindId == "hut" && structure.IsBuilt);
            Assert.True(simulation.CentralDepot.GetCount("meals") > 0 || simulation.ProducedResources.GetValueOrDefault("meals") > 0);
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.SettlementBuildCompleted);
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.SettlementProcessCompleted);
        }

        [Fact]
        public void Advance_ExtractedResourcesFlowThroughCacheAndDepot()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            List<PrototypeSettlementEvent> events = AdvanceSimulation(simulation, resources, 900);

            Assert.True(
                events.Any(entry => entry.EventType == PrototypeEventTypes.SettlementCacheDeposit) ||
                events.Any(entry => entry.EventType == PrototypeEventTypes.SettlementHaulCompleted));
            Assert.True(simulation.ProducedResources.Values.Sum() > 0 || simulation.CentralDepot.Items.Values.Sum() > scenario.StartingStock.Values.Sum());
        }

        [Fact]
        public void Advance_FoodPoorHighlands_CollapsesUnderShortage()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("food_poor_highlands");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            simulation.CentralDepot.Items.Clear();
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            AdvanceSimulation(simulation, resources, 1200);

            Assert.Equal(PrototypeSettlementClassification.Collapsed, simulation.Classification);
            Assert.Contains(simulation.Citizens, citizen => citizen.Phase == PrototypeWorkerPhase.Incapacitated);
        }

        [Fact]
        public void BuildQueueSelectionAndPause_AffectCurrentEntry()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);

            Assert.True(simulation.SelectNextBuildQueueEntry());
            string selectedBeforePause = simulation.SelectedBuildQueueStatusText;
            Assert.True(simulation.ToggleSelectedBuildQueuePause());
            string selectedAfterPause = simulation.SelectedBuildQueueStatusText;

            Assert.NotEqual(selectedBeforePause, selectedAfterPause);
            Assert.Contains("paused", selectedAfterPause, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CaptureAndLoadState_RoundTripsCitizensAndStructures()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("wetland_builder");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            AdvanceSimulation(simulation, resources, 240);
            PrototypeSettlementSnapshot snapshot = simulation.CaptureSnapshot(420);

            PrototypeSettlementSimulation restored = new(scenario, bundle.RoleQuotas.Roles, world);
            restored.LoadState(snapshot);

            Assert.Equal(simulation.Citizens.Count, restored.Citizens.Count);
            Assert.Equal(simulation.Structures.Count, restored.Structures.Count);
            Assert.Equal(simulation.CentralDepot.Items, restored.CentralDepot.Items);
            Assert.Equal(simulation.Classification, restored.Classification);
        }

        [Fact]
        public void LoadState_LegacyWetlandCacheDoesNotLoseHarvestedResource()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);
            PrototypeResourceSiteState wetlandSite = resources.First(site =>
                world.WorldMap.GetNearestCell(site.Position).Biome == BiomeType.Wetland);
            PrototypeSettlementSnapshot snapshot = simulation.CaptureSnapshot(0);

            PrototypeResourceStoreSnapshot cacheSnapshot = snapshot.SiteCaches.Single(cache =>
                string.Equals(cache.LinkedClusterId, wetlandSite.ClusterId, StringComparison.Ordinal));
            cacheSnapshot.Position = PrototypeSerializableVector3.FromVector3(wetlandSite.Position);

            foreach (PrototypeWorkerSnapshot citizenSnapshot in snapshot.Citizens)
            {
                citizenSnapshot.Phase = PrototypeWorkerPhase.Incapacitated.ToString();
            }

            PrototypeWorkerSnapshot legacyHarvester = snapshot.Citizens
                .OrderBy(citizen => citizen.WorkerId, StringComparer.Ordinal)
                .First();
            legacyHarvester.Phase = PrototypeWorkerPhase.Harvesting.ToString();
            legacyHarvester.Position = PrototypeSerializableVector3.FromVector3(wetlandSite.Position);
            legacyHarvester.TargetPosition = PrototypeSerializableVector3.FromVector3(wetlandSite.Position);
            legacyHarvester.TargetResourceNodeName = wetlandSite.NodeName;
            legacyHarvester.CurrentOrderId = $"extract.{wetlandSite.NodeName}";
            legacyHarvester.CurrentOrderKind = PrototypeWorkOrderKind.Extract.ToString();
            legacyHarvester.CarryItemId = string.Empty;
            legacyHarvester.CarryAmount = 0;
            legacyHarvester.TicksRemaining = 1;
            legacyHarvester.PhaseDurationTicks = 1;
            legacyHarvester.Nutrition = 100.0f;
            legacyHarvester.Fatigue = 0.0f;

            PrototypeSettlementSimulation restored = new(scenario, bundle.RoleQuotas.Roles, world);
            restored.LoadState(snapshot);
            PrototypeResourceStoreState restoredCache = restored.SiteCaches.Single(cache =>
                string.Equals(cache.LinkedClusterId, wetlandSite.ClusterId, StringComparison.Ordinal));
            TerrainCell restoredCacheCell = world.WorldMap.GetNearestCell(restoredCache.Position);
            Assert.NotEqual(BiomeType.Wetland, restoredCacheCell.Biome);
            Assert.True(restoredCacheCell.SlopeDegrees <= 18.0f);

            PrototypeSettlementTickResult result = restored.Advance(resources, 8.0f, PrototypeWeather.Clear);
            PrototypeWorkerState restoredHarvester = restored.Citizens.Single(citizen =>
                string.Equals(citizen.WorkerId, legacyHarvester.WorkerId, StringComparison.Ordinal));

            Assert.Empty(result.HarvestRequests);
            Assert.Equal(0, restoredHarvester.CarryAmount);
            Assert.Equal(PrototypeWorkerPhase.Idle, restoredHarvester.Phase);
            Assert.Equal("navigation.unreachable", restoredHarvester.LastFailureReason);
        }

        [Fact]
        public void LongHaulQuarry_PlansAndBuildsRemoteLogisticsInfrastructure()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("long_haul_quarry");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            Assert.Contains(simulation.BuildQueue, entry => entry.StructureKindId == "remote_stockpile");
            Assert.Contains(simulation.BuildQueue, entry => entry.StructureKindId == "path_segment");

            AdvanceSimulation(simulation, resources, 1400);

            Assert.True(simulation.PathSegments.Any(segment => segment.IsBuilt) || simulation.RemoteDepots.Any(depot => depot.IsBuilt));
            Assert.True(simulation.AverageRouteLengthMeters > 0.0f);
        }

        [Fact]
        public void BalancedBasin_BuildsUsablePathsAndTracksRouteHeat()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            AdvanceSimulation(simulation, resources, 1000);

            Assert.True(simulation.PathCoverageRatio >= 0.0f);
            Assert.True(simulation.PathHeatByCell.Count > 0);
            Assert.True(simulation.AverageTravelWorkRatio >= 0.0f);
        }

        private static List<PrototypeSettlementEvent> AdvanceSimulation(
            PrototypeSettlementSimulation simulation,
            List<PrototypeResourceSiteState> resources,
            int ticks)
        {
            List<PrototypeSettlementEvent> events = new();
            float currentHour = 8.0f;

            for (int i = 0; i < ticks; i++)
            {
                PrototypeSettlementTickResult result = simulation.Advance(resources, currentHour, PrototypeWeather.Clear);
                events.AddRange(result.Events);

                foreach (PrototypeHarvestRequest request in result.HarvestRequests)
                {
                    int index = resources.FindIndex(site => site.NodeName == request.TargetNodeName);
                    Assert.True(index >= 0, $"Missing resource node {request.TargetNodeName}");

                    PrototypeResourceSiteState site = resources[index];
                    if (site.UnitsRemaining < request.Amount)
                    {
                        simulation.OnHarvestFailed(request.WorkerId);
                        continue;
                    }

                    resources[index] = site with
                    {
                        UnitsRemaining = site.UnitsRemaining - request.Amount
                    };
                }

                currentHour = AdvanceHour(currentHour, 1.0f / 20.0f, 600.0f);
            }

            return events;
        }

        private static float AdvanceHour(float currentHour, double tickIntervalSeconds, double dayLengthSeconds)
        {
            double hoursPerTick = 24.0 * tickIntervalSeconds / dayLengthSeconds;
            float next = (float)(currentHour + hoursPerTick);
            while (next >= 24.0f)
            {
                next -= 24.0f;
            }
            return next;
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
