using Godot;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypePersistenceTests
    {
        [Fact]
        public void SnapshotSerialization_RoundTripsState()
        {
            PrototypeRuntimeSnapshot snapshot = new()
            {
                WorldSeed = 777,
                WorldGenerationAttempt = 2,
                WorldHash = "world-hash",
                SimulationSeed = 42,
                SimulationTick = 120,
                CurrentHour = 9.75f,
                CurrentWeather = "Rain",
                TimeUntilNextWeatherShift = 61.5f,
                WeatherRandomState = 123456u,
                PlayerPosition = PrototypeSerializableVector3.FromVector3(new Vector3(1.0f, 2.0f, 3.0f)),
                SettlementAnchorPosition = PrototypeSerializableVector3.FromVector3(new Vector3(4.0f, 5.0f, 6.0f)),
                Inventory = new Dictionary<string, int> { ["wood"] = 4, ["stone"] = 2 },
                Stockpile = new Dictionary<string, int> { ["berry"] = 6, ["campfire"] = 1 },
                Workers = new List<PrototypeWorkerSnapshot>
                {
                    new()
                    {
                        WorkerId = "worker_1",
                        DisplayName = "Worker 1",
                        PreferredResourceId = "wood",
                        Phase = "MovingToStockpile",
                        TargetResourceNodeName = "wood_3",
                        CarryItemId = "wood",
                        CarryAmount = 1,
                        TicksRemaining = 5,
                        PhaseDurationTicks = 18,
                        Position = PrototypeSerializableVector3.FromVector3(new Vector3(4.0f, 0.0f, -2.0f)),
                        HomePosition = PrototypeSerializableVector3.FromVector3(new Vector3(0.0f, 0.0f, 0.0f)),
                        TargetPosition = PrototypeSerializableVector3.FromVector3(new Vector3(0.0f, 0.0f, 0.0f)),
                        TargetLabel = "Settlement",
                        ActivityText = "Returning with Wood"
                    }
                },
                Resources = new List<PrototypeResourceSnapshot>
                {
                    new()
                    {
                        ResourceId = "wood",
                        UnitsRemaining = 5,
                        Position = PrototypeSerializableVector3.FromVector3(new Vector3(10.0f, 0.0f, -5.0f))
                    }
                }
            };

            string json = PrototypePersistenceService.SerializeSnapshot(snapshot);
            PrototypeRuntimeSnapshot restored = PrototypePersistenceService.DeserializeSnapshot(json);

            Assert.Equal(777, restored.WorldSeed);
            Assert.Equal(2, restored.WorldGenerationAttempt);
            Assert.Equal("world-hash", restored.WorldHash);
            Assert.Equal(42, restored.SimulationSeed);
            Assert.Equal(120, restored.SimulationTick);
            Assert.Equal("Rain", restored.CurrentWeather);
            Assert.Equal(61.5f, restored.TimeUntilNextWeatherShift);
            Assert.Equal(123456u, restored.WeatherRandomState);
            Assert.Equal(4, restored.Inventory["wood"]);
            Assert.Equal(6, restored.Stockpile["berry"]);
            Assert.Single(restored.Workers);
            Assert.Equal("MovingToStockpile", restored.Workers[0].Phase);
            Assert.Equal("Settlement", restored.Workers[0].TargetLabel);
            Assert.Equal("Returning with Wood", restored.Workers[0].ActivityText);
            Assert.Single(restored.Resources);
            Assert.Equal(new Vector3(10.0f, 0.0f, -5.0f), restored.Resources[0].Position.ToVector3());
        }

        [Fact]
        public void EventLogSerialization_RoundTripsEntries()
        {
            PrototypeEventLog eventLog = new();
            eventLog.Record(10, PrototypeEventTypes.WeatherToggled, "Weather set to Rain");
            eventLog.Record(12, PrototypeEventTypes.PlayerCraftSucceeded, "Crafted Stone Axe");

            string json = PrototypePersistenceService.SerializeEventLog(eventLog);
            var restored = PrototypePersistenceService.DeserializeEventLog(json);

            Assert.Equal(2, restored.Count);
            Assert.Equal(PrototypeEventTypes.WeatherToggled, restored[0].EventType);
            Assert.Equal(12, restored[1].Tick);
        }

        [Fact]
        public void RunSummarySerialization_RoundTripsState()
        {
            PrototypeRunSummary summary = new()
            {
                WorldSeed = 777,
                TerrainMode = "heightfield_v1",
                BuildableCellRatio = 0.62f,
                BiomeCellCounts = new Dictionary<string, int> { ["Meadow"] = 20, ["Forest"] = 12 },
                SimulationSeed = 99,
                SimulationTick = 320,
                StartHour = 8.0f,
                StartTimeText = "08:00",
                EndHour = 14.5f,
                EndTimeText = "14:30",
                FinalWeather = "Clear",
                PlayerInventory = new Dictionary<string, int> { ["stone_axe"] = 1 },
                Stockpile = new Dictionary<string, int> { ["campfire"] = 1 },
                RemainingResourcesByType = new Dictionary<string, int> { ["wood"] = 18 },
                WorkersByPhase = new Dictionary<string, int> { ["Idle"] = 2, ["Crafting"] = 1 },
                CraftedItemCounts = new Dictionary<string, int> { ["campfire"] = 1, ["stone_axe"] = 1 },
                EventCountsByType = new Dictionary<string, int> { [PrototypeEventTypes.AiCraftCompleted] = 1 }
            };

            string json = PrototypePersistenceService.SerializeRunSummary(summary);
            PrototypeRunSummary restored = PrototypePersistenceService.DeserializeRunSummary(json);

            Assert.Equal(777, restored.WorldSeed);
            Assert.Equal("heightfield_v1", restored.TerrainMode);
            Assert.Equal(0.62f, restored.BuildableCellRatio);
            Assert.Equal(20, restored.BiomeCellCounts["Meadow"]);
            Assert.Equal(99, restored.SimulationSeed);
            Assert.Equal("14:30", restored.EndTimeText);
            Assert.Equal(1, restored.CraftedItemCounts["campfire"]);
            Assert.Equal(1, restored.EventCountsByType[PrototypeEventTypes.AiCraftCompleted]);
        }
    }
}
