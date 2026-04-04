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
            Assert.Equal(PrototypeWorkerPhase.Idle, worker.Phase);
            Assert.Equal(Vector3.Zero, worker.Position);
            Assert.Equal(0, worker.CarryAmount);
            Assert.Equal(string.Empty, worker.TargetResourceNodeName);
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
