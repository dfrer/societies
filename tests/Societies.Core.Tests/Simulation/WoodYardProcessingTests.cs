using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class WoodYardProcessingTests
    {
        [Fact]
        public void OneLogWithFuelAndTimberDemand_ReservesTheWoodYardForOneProcessClaim()
        {
            (PrototypeSettlementSimulation simulation, List<PrototypeResourceSiteState> resources, PrototypeStructureState woodYard) =
                CreateSimulation(activeCitizens: 2);
            Assert.True(woodYard.InputStore.Add("logs", 1));

            PrototypeWorkerState[] processClaimers = Array.Empty<PrototypeWorkerState>();
            for (int tick = 0; tick < 20 && processClaimers.Length == 0; tick++)
            {
                _ = simulation.Advance(resources, 8.0f + tick / 500.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.Shelter);
                processClaimers = simulation.Workers
                    .Where(worker => worker.CurrentOrderKind == PrototypeWorkOrderKind.Process &&
                        string.Equals(worker.TargetStructureId, woodYard.StructureId, StringComparison.Ordinal))
                    .ToArray();
            }

            Assert.Single(processClaimers);
            Assert.Equal("process.wood_yard_1.timber", processClaimers[0].CurrentOrderId);
        }

        [Fact]
        public void OneOutputSlot_RejectsFirewoodAndPermitsSafeTimberFallback()
        {
            (PrototypeSettlementSimulation simulation, List<PrototypeResourceSiteState> resources, PrototypeStructureState woodYard) =
                CreateSimulation(activeCitizens: 1);
            woodYard.OutputStore.Capacity = 1;
            Assert.True(woodYard.InputStore.Add("logs", 1));

            _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.FoodAndFuel);

            PrototypeWorkerState claimant = Assert.Single(simulation.Workers.Where(worker =>
                worker.CurrentOrderKind == PrototypeWorkOrderKind.Process &&
                string.Equals(worker.TargetStructureId, woodYard.StructureId, StringComparison.Ordinal)));
            Assert.Equal("process.wood_yard_1.timber", claimant.CurrentOrderId);
        }

        [Fact]
        public void OneLogInputReservation_RejectsSecondRecipeEvenWhenOutputCapacityIsAvailable()
        {
            (PrototypeSettlementSimulation simulation, List<PrototypeResourceSiteState> resources, PrototypeStructureState woodYard) =
                CreateSimulation(activeCitizens: 2);
            woodYard.OutputStore.Capacity = 3;
            Assert.True(woodYard.InputStore.Add("logs", 1));

            _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.FoodAndFuel);

            PrototypeWorkerState claimant = Assert.Single(simulation.Workers.Where(worker =>
                worker.CurrentOrderKind == PrototypeWorkOrderKind.Process &&
                string.Equals(worker.TargetStructureId, woodYard.StructureId, StringComparison.Ordinal)));
            Assert.Equal("process.wood_yard_1.firewood", claimant.CurrentOrderId);
        }

        [Fact]
        public void TwoLogsAndThreeOutputSlots_AllowBothWoodYardRecipesToClaimSafely()
        {
            (PrototypeSettlementSimulation simulation, List<PrototypeResourceSiteState> resources, PrototypeStructureState woodYard) =
                CreateSimulation(activeCitizens: 2);
            woodYard.OutputStore.Capacity = 3;
            Assert.True(woodYard.InputStore.Add("logs", 2));

            _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.Shelter);

            string[] processOrderIds = simulation.Workers
                .Where(worker => worker.CurrentOrderKind == PrototypeWorkOrderKind.Process &&
                    string.Equals(worker.TargetStructureId, woodYard.StructureId, StringComparison.Ordinal))
                .Select(worker => worker.CurrentOrderId)
                .OrderBy(orderId => orderId, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "process.wood_yard_1.firewood", "process.wood_yard_1.timber" }, processOrderIds);
        }

        [Fact]
        public void WoodYardRecipes_ProduceExpectedFirewoodAndTimberAmounts()
        {
            (PrototypeSettlementSimulation firewoodSimulation, List<PrototypeResourceSiteState> firewoodResources, PrototypeStructureState firewoodYard) =
                CreateSimulation(activeCitizens: 1);
            Assert.True(firewoodYard.InputStore.Add("logs", 1));
            List<PrototypeSettlementEvent> firewoodEvents = Advance(firewoodSimulation, firewoodResources, 80, PrototypeSettlementDirective.FoodAndFuel);

            Assert.Equal(2, firewoodSimulation.ProducedResources.GetValueOrDefault("firewood"));
            Assert.Equal(1, firewoodSimulation.ConsumedResources.GetValueOrDefault("logs"));
            Assert.Contains(firewoodEvents, entry => entry.EventType == PrototypeEventTypes.SettlementProcessCompleted);

            (PrototypeSettlementSimulation timberSimulation, List<PrototypeResourceSiteState> timberResources, PrototypeStructureState timberYard) =
                CreateSimulation(activeCitizens: 1);
            timberSimulation.Structures.Single(structure => structure.StructureKindId == "central_hearth").HearthFuel = 1000;
            Assert.True(timberYard.InputStore.Add("logs", 1));
            List<PrototypeSettlementEvent> timberEvents = Advance(timberSimulation, timberResources, 80, PrototypeSettlementDirective.Shelter);

            Assert.Equal(1, timberSimulation.ProducedResources.GetValueOrDefault("timber"));
            Assert.Equal(1, timberSimulation.ConsumedResources.GetValueOrDefault("logs"));
            Assert.Contains(timberEvents, entry => entry.EventType == PrototypeEventTypes.SettlementProcessCompleted);
        }

        [Fact]
        public void WoodYardOutputCapacityBlock_IsAtomicWithoutPhantomProduction()
        {
            (PrototypeSettlementSimulation simulation, List<PrototypeResourceSiteState> resources, PrototypeStructureState woodYard) =
                CreateSimulation(activeCitizens: 1);
            foreach (PrototypeBuildQueueEntry entry in simulation.BuildQueue)
            {
                entry.IsPaused = true;
            }
            woodYard.OutputStore.Capacity = 2;
            Assert.True(woodYard.InputStore.Add("logs", 1));

            for (int tick = 0; tick < 20 && simulation.Workers.All(worker => worker.CurrentOrderKind != PrototypeWorkOrderKind.Process); tick++)
            {
                _ = simulation.Advance(resources, 8.0f + tick / 500.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.FoodAndFuel);
            }

            Assert.Contains(simulation.Workers, worker => worker.CurrentOrderId == "process.wood_yard_1.firewood");
            woodYard.OutputStore.Capacity = 1;

            List<PrototypeSettlementEvent> events = Advance(simulation, resources, 80, PrototypeSettlementDirective.FoodAndFuel);

            Assert.Equal(1, woodYard.InputStore.GetCount("logs"));
            Assert.Equal(0, woodYard.OutputStore.GetCount("firewood"));
            Assert.Equal(0, simulation.ProducedResources.GetValueOrDefault("firewood"));
            Assert.Equal(0, simulation.ConsumedResources.GetValueOrDefault("logs"));
            Assert.DoesNotContain(events, entry => entry.EventType == PrototypeEventTypes.SettlementProcessCompleted);
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.SettlementBlocked);
        }

        private static (PrototypeSettlementSimulation Simulation, List<PrototypeResourceSiteState> Resources, PrototypeStructureState WoodYard) CreateSimulation(int activeCitizens)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("empty_stores");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            simulation.CentralDepot.Items.Clear();
            foreach (PrototypeWorkerState citizen in simulation.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal))
            {
                citizen.Needs.Nutrition = 100.0f;
                citizen.Needs.Fatigue = 0.0f;
            }

            foreach (PrototypeWorkerState citizen in simulation.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).Skip(activeCitizens))
            {
                citizen.Phase = PrototypeWorkerPhase.Incapacitated;
            }

            PrototypeStructureState woodYard = simulation.Structures.Single(structure => structure.StructureKindId == "wood_yard");
            return (simulation, new List<PrototypeResourceSiteState>(), woodYard);
        }

        private static List<PrototypeSettlementEvent> Advance(
            PrototypeSettlementSimulation simulation,
            List<PrototypeResourceSiteState> resources,
            int ticks,
            PrototypeSettlementDirective directive)
        {
            List<PrototypeSettlementEvent> events = new();
            float hour = 8.0f;
            for (int tick = 0; tick < ticks; tick++)
            {
                PrototypeSettlementTickResult result = simulation.Advance(resources, hour, PrototypeWeather.Clear, directive: directive);
                events.AddRange(result.Events);
                hour += 1.0f / 500.0f;
            }

            return events;
        }

        private static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world) =>
            world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();

        private static PrototypeCatalogBundle LoadCatalogs() =>
            PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());

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
