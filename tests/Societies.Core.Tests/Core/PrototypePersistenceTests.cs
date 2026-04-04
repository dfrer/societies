using Godot;
using Societies.Simulation;
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
                Inventory = new Dictionary<string, int> { ["logs"] = 4, ["stone"] = 2 },
                Stockpile = new Dictionary<string, int> { ["berries"] = 6, ["meals"] = 1 },
                Workers = new List<PrototypeWorkerSnapshot>
                {
                    new()
                    {
                        WorkerId = "citizen_1",
                        DisplayName = "Citizen 1",
                        PreferredResourceId = "logs",
                        RoleId = "Logger",
                        Phase = "MovingToDepot",
                        TargetResourceNodeName = "logs_3",
                        CarryItemId = "logs",
                        CarryAmount = 1,
                        TicksRemaining = 5,
                        PhaseDurationTicks = 18,
                        Position = PrototypeSerializableVector3.FromVector3(new Vector3(4.0f, 0.0f, -2.0f)),
                        HomePosition = PrototypeSerializableVector3.FromVector3(new Vector3(0.0f, 0.0f, 0.0f)),
                        TargetPosition = PrototypeSerializableVector3.FromVector3(new Vector3(0.0f, 0.0f, 0.0f)),
                        TargetLabel = "Settlement",
                        ActivityText = "Returning with Logs",
                        Nutrition = 64.0f,
                        Fatigue = 22.0f,
                        CurrentOrderId = "haul_1",
                        CurrentOrderKind = "HaulToDepot",
                        CurrentOrderReason = "remote resource delivery",
                        RecentEvents = new List<string> { "Harvested logs" },
                        TravelTicksAccumulated = 12,
                        WorkTicksAccumulated = 8,
                        CurrentRouteLengthMeters = 18.5f,
                        CurrentRouteCost = 22.0f,
                        CurrentRouteTravelTicks = 28,
                        CurrentWaypointIndex = 2,
                        CachedRouteVersion = 3,
                        RouteSourceGridX = 4,
                        RouteSourceGridY = 5,
                        RouteDestinationGridX = 7,
                        RouteDestinationGridY = 6,
                        RouteWaypoints = new List<PrototypeSerializableVector3>
                        {
                            PrototypeSerializableVector3.FromVector3(new Vector3(4.0f, 0.0f, -2.0f)),
                            PrototypeSerializableVector3.FromVector3(new Vector3(1.0f, 0.0f, -1.0f))
                        }
                    }
                },
                Resources = new List<PrototypeResourceSnapshot>
                {
                    new()
                    {
                        ResourceId = "logs",
                        UnitsRemaining = 5,
                        Position = PrototypeSerializableVector3.FromVector3(new Vector3(10.0f, 0.0f, -5.0f)),
                        ClusterId = "logs_cluster_1"
                    }
                },
                Settlement = new PrototypeSettlementSnapshot
                {
                    Classification = "stable",
                    LogisticsMetrics = new PrototypeLogisticsMetricsState
                    {
                        CompletedRouteCount = 3,
                        TotalCompletedRouteDistanceMeters = 48.0f,
                        TotalCompletedRouteTicks = 72,
                        TravelTicksAccumulated = 30,
                        WorkTicksAccumulated = 20,
                        PathCoverageRatio = 0.12f,
                        DepotThroughputByDepot = new Dictionary<string, int> { ["remote_stockpile_1.output"] = 4 },
                        RouteBacklogTicksByKind = new Dictionary<string, int> { ["haultodepot"] = 6 }
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
            Assert.Equal(4, restored.Inventory["logs"]);
            Assert.Equal(6, restored.Stockpile["berries"]);
            Assert.Single(restored.Workers);
            Assert.Equal("MovingToDepot", restored.Workers[0].Phase);
            Assert.Equal("Settlement", restored.Workers[0].TargetLabel);
            Assert.Equal("Returning with Logs", restored.Workers[0].ActivityText);
            Assert.Equal("Logger", restored.Workers[0].RoleId);
            Assert.Equal("HaulToDepot", restored.Workers[0].CurrentOrderKind);
            Assert.Equal(12, restored.Workers[0].TravelTicksAccumulated);
            Assert.Equal(18.5f, restored.Workers[0].CurrentRouteLengthMeters);
            Assert.Single(restored.Resources);
            Assert.Equal(new Vector3(10.0f, 0.0f, -5.0f), restored.Resources[0].Position.ToVector3());
            Assert.Equal("logs_cluster_1", restored.Resources[0].ClusterId);
            Assert.NotNull(restored.Settlement);
            Assert.Equal("stable", restored.Settlement!.Classification);
            Assert.Equal(3, restored.Settlement.LogisticsMetrics.CompletedRouteCount);
            Assert.Equal(0.12f, restored.Settlement.LogisticsMetrics.PathCoverageRatio);
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
                Stockpile = new Dictionary<string, int> { ["meals"] = 1, ["hearth_fuel"] = 2 },
                RemainingResourcesByType = new Dictionary<string, int> { ["logs"] = 18 },
                WorkersByPhase = new Dictionary<string, int> { ["Idle"] = 2, ["Processing"] = 1 },
                CraftedItemCounts = new Dictionary<string, int> { ["stone_axe"] = 1 },
                EventCountsByType = new Dictionary<string, int> { [PrototypeEventTypes.SettlementProcessCompleted] = 1 }
                ,
                AverageRouteLengthMeters = 21.5f,
                AverageTravelWorkRatio = 1.12f,
                PathCoverageRatio = 0.09f,
                DepotThroughputByDepot = new Dictionary<string, int> { ["remote_stockpile_1.output"] = 3 },
                RouteBacklogTicksByKind = new Dictionary<string, int> { ["haultodepot"] = 5 }
            };

            string json = PrototypePersistenceService.SerializeRunSummary(summary);
            PrototypeRunSummary restored = PrototypePersistenceService.DeserializeRunSummary(json);

            Assert.Equal(777, restored.WorldSeed);
            Assert.Equal("heightfield_v1", restored.TerrainMode);
            Assert.Equal(0.62f, restored.BuildableCellRatio);
            Assert.Equal(20, restored.BiomeCellCounts["Meadow"]);
            Assert.Equal(99, restored.SimulationSeed);
            Assert.Equal("14:30", restored.EndTimeText);
            Assert.Equal(1, restored.CraftedItemCounts["stone_axe"]);
            Assert.Equal(1, restored.EventCountsByType[PrototypeEventTypes.SettlementProcessCompleted]);
            Assert.Equal(21.5f, restored.AverageRouteLengthMeters);
            Assert.Equal(3, restored.DepotThroughputByDepot["remote_stockpile_1.output"]);
        }
    }
}
