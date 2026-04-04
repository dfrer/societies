using Godot;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeRunSummaryBuilderTests
    {
        [Fact]
        public void Build_AggregatesResourceWorkerCraftAndEventCounts()
        {
            PrototypeRuntimeSnapshot snapshot = new()
            {
                SimulationSeed = 1337,
                SimulationTick = 200,
                CurrentHour = 12.5f,
                CurrentWeather = "Rain",
                Inventory = new Dictionary<string, int>
                {
                    ["stone_axe"] = 1
                },
                Stockpile = new Dictionary<string, int>
                {
                    ["wood"] = 4,
                    ["campfire"] = 1
                },
                Workers = new List<PrototypeWorkerSnapshot>
                {
                    new() { WorkerId = "worker_1", Phase = "Idle" },
                    new() { WorkerId = "worker_2", Phase = "Crafting" },
                    new() { WorkerId = "worker_3", Phase = "Idle" }
                },
                Resources = new List<PrototypeResourceSnapshot>
                {
                    new() { ResourceId = "wood", UnitsRemaining = 8, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) },
                    new() { ResourceId = "wood", UnitsRemaining = 3, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) },
                    new() { ResourceId = "stone", UnitsRemaining = 5, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) }
                }
            };

            List<PrototypeEventRecord> eventRecords = new()
            {
                new() { Tick = 10, EventType = PrototypeEventTypes.AiDepositCompleted, Message = "deposit" },
                new() { Tick = 11, EventType = PrototypeEventTypes.AiDepositCompleted, Message = "deposit" },
                new() { Tick = 20, EventType = PrototypeEventTypes.AiCraftCompleted, Message = "craft" }
            };

            PrototypeWorldSummary worldSummary = new()
            {
                WorldSeed = 777,
                TerrainMode = "heightfield_v1",
                BuildableCellRatio = 0.61f,
                BiomeCellCounts = new Dictionary<string, int>
                {
                    ["Forest"] = 12,
                    ["Meadow"] = 18
                }
            };

            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(snapshot, eventRecords, 8.0f, "balanced_basin", "Balanced Basin", worldSummary);

            Assert.Equal(777, summary.WorldSeed);
            Assert.Equal("heightfield_v1", summary.TerrainMode);
            Assert.Equal(0.61f, summary.BuildableCellRatio);
            Assert.Equal(18, summary.BiomeCellCounts["Meadow"]);
            Assert.Equal(1337, summary.SimulationSeed);
            Assert.Equal("08:00", summary.StartTimeText);
            Assert.Equal("12:30", summary.EndTimeText);
            Assert.Equal(11, summary.RemainingResourcesByType["wood"]);
            Assert.Equal(2, summary.WorkersByPhase["Idle"]);
            Assert.Equal(1, summary.CraftedItemCounts["campfire"]);
            Assert.Equal(1, summary.CraftedItemCounts["stone_axe"]);
            Assert.Equal(2, summary.EventCountsByType[PrototypeEventTypes.AiDepositCompleted]);
        }
    }
}
