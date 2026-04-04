using Godot;
using Societies.Simulation;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeSettlementSimulationTests
    {
        [Fact]
        public void Advance_DepositsHarvestedResourcesIntoStockpile()
        {
            InventoryComponent stockpile = new();
            PrototypeSettlementSimulation simulation = new(stockpile, 1, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("wood_1", "wood", new Vector3(12.0f, 0.0f, 0.0f), 10)
            };

            List<PrototypeSettlementEvent> events = AdvanceSimulation(simulation, resources, 80);

            Assert.True(stockpile.GetCount("wood") > 0);
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.AiDepositCompleted);
            Assert.Single(simulation.Workers);
            Assert.Equal("wood", simulation.Workers[0].PreferredResourceId);
        }

        [Fact]
        public void Advance_MovesWorkersThroughWorldInsteadOfTeleporting()
        {
            InventoryComponent stockpile = new();
            PrototypeSettlementSimulation simulation = new(stockpile, 1, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("wood_1", "wood", new Vector3(12.0f, 0.0f, 0.0f), 10)
            };

            PrototypeWorkerState initialWorker = Assert.Single(simulation.Workers);
            Vector3 initialPosition = initialWorker.Position;

            AdvanceSimulation(simulation, resources, 24);

            PrototypeWorkerState worker = Assert.Single(simulation.Workers);
            Assert.Equal(PrototypeWorkerPhase.MovingToResource, worker.Phase);
            Assert.True(worker.Position.DistanceTo(initialPosition) > 0.5f);
            Assert.True(worker.Position.DistanceTo(resources[0].Position) > 0.5f);
            Assert.Contains("Gathering wood", worker.ActivityText);
            Assert.Equal("Tree", worker.TargetLabel);
        }

        [Fact]
        public void Advance_WithWoodAndStoneWorkers_CraftsCampfire()
        {
            InventoryComponent stockpile = new();
            PrototypeSettlementSimulation simulation = new(stockpile, 2, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("wood_1", "wood", new Vector3(10.0f, 0.0f, 0.0f), 20),
                new PrototypeResourceSiteState("stone_1", "stone", new Vector3(-10.0f, 0.0f, 0.0f), 20)
            };

            List<PrototypeSettlementEvent> events = AdvanceSimulation(simulation, resources, 420);

            Assert.Equal(1, stockpile.GetCount("campfire"));
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.AiCraftCompleted);
            Assert.All(simulation.Workers, worker => Assert.False(string.IsNullOrWhiteSpace(worker.DisplayName)));
        }

        [Fact]
        public void OnHarvestFailed_ReturnsWorkerToIdleAtStockpile()
        {
            InventoryComponent stockpile = new();
            PrototypeSettlementSimulation simulation = new(stockpile, 1, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("wood_1", "wood", new Vector3(8.0f, 0.0f, 0.0f), 10)
            };

            AdvanceSimulation(simulation, resources, 40);
            simulation.OnHarvestFailed("worker_1");

            PrototypeWorkerState worker = Assert.Single(simulation.Workers);
            Assert.Equal(PrototypeWorkerPhase.MovingToStockpile, worker.Phase);
            Assert.True(worker.Position.DistanceTo(worker.HomePosition) > 0.1f);
            Assert.Equal(0, worker.CarryAmount);
            Assert.Equal(string.Empty, worker.TargetResourceNodeName);
            Assert.Contains("empty-handed", worker.ActivityText);
        }

        [Fact]
        public void Advance_WhenResourcesExhausted_DoesNotGenerateNegativeCounts()
        {
            InventoryComponent stockpile = new();
            PrototypeSettlementSimulation simulation = new(stockpile, 1, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("wood_1", "wood", new Vector3(12.0f, 0.0f, 0.0f), 1)
            };

            List<PrototypeSettlementEvent> events = AdvanceSimulation(simulation, resources, 220);

            Assert.All(resources, site => Assert.True(site.UnitsRemaining >= 0));
            Assert.Equal(1, stockpile.GetCount("wood"));
            Assert.Contains(events, entry => entry.EventType == PrototypeEventTypes.AiTaskAssigned);
        }

        [Fact]
        public void Advance_PrioritizesCampfireIngredientsBeforeFoodReserve()
        {
            InventoryComponent stockpile = new();
            stockpile.AddItem("wood", 3);
            stockpile.AddItem("berry", 4);

            PrototypeSettlementSimulation simulation = new(stockpile, 1, Vector3.Zero);
            List<PrototypeResourceSiteState> resources = new()
            {
                new PrototypeResourceSiteState("stone_1", "stone", new Vector3(8.0f, 0.0f, 0.0f), 8),
                new PrototypeResourceSiteState("berry_1", "berry", new Vector3(4.0f, 0.0f, 0.0f), 8)
            };

            AdvanceSimulation(simulation, resources, 20);

            PrototypeWorkerState worker = Assert.Single(simulation.Workers);
            Assert.Equal("stone_1", worker.TargetResourceNodeName);
            Assert.Contains("campfire", worker.ActivityText.ToLowerInvariant());
        }

        private static List<PrototypeSettlementEvent> AdvanceSimulation(
            PrototypeSettlementSimulation simulation,
            List<PrototypeResourceSiteState> resources,
            int ticks)
        {
            List<PrototypeSettlementEvent> events = new();

            for (int i = 0; i < ticks; i++)
            {
                PrototypeSettlementTickResult result = simulation.Advance(resources);
                events.AddRange(result.Events);

                foreach (PrototypeHarvestRequest request in result.HarvestRequests)
                {
                    int index = resources.FindIndex(site => site.NodeName == request.TargetNodeName);
                    Assert.True(index >= 0, $"Missing resource node {request.TargetNodeName}");

                    PrototypeResourceSiteState site = resources[index];
                    resources[index] = site with
                    {
                        UnitsRemaining = site.UnitsRemaining - request.Amount
                    };
                }
            }

            return events;
        }
    }
}
