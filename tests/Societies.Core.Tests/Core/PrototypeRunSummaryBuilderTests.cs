using Godot;
using Societies.Simulation;
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
                    ["firewood"] = 4,
                    ["beds"] = 2,
                    ["meals"] = 3
                },
                Workers = new List<PrototypeWorkerSnapshot>
                {
                    new() { WorkerId = "citizen_1", Phase = "Idle" },
                    new() { WorkerId = "citizen_2", Phase = "Processing" },
                    new() { WorkerId = "citizen_3", Phase = "Idle" }
                },
                Resources = new List<PrototypeResourceSnapshot>
                {
                    new() { ResourceId = "logs", UnitsRemaining = 8, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) },
                    new() { ResourceId = "logs", UnitsRemaining = 3, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) },
                    new() { ResourceId = "stone", UnitsRemaining = 5, Position = PrototypeSerializableVector3.FromVector3(Vector3.Zero) }
                },
                Settlement = new PrototypeSettlementSnapshot
                {
                    Classification = "stable",
                    LogisticsMetrics = new PrototypeLogisticsMetricsState
                    {
                        CompletedRouteCount = 4,
                        TotalCompletedRouteDistanceMeters = 64.0f,
                        TravelTicksAccumulated = 44,
                        WorkTicksAccumulated = 40,
                        PathCoverageRatio = 0.15f,
                        DepotThroughputByDepot = new Dictionary<string, int> { ["remote_stockpile_1.output"] = 5 },
                        RouteBacklogTicksByKind = new Dictionary<string, int> { ["haultodepot"] = 7 }
                    }
                }
            };

            List<PrototypeEventRecord> eventRecords = new()
            {
                new() { Tick = 10, EventType = PrototypeEventTypes.AiDepositCompleted, Message = "deposit" },
                new() { Tick = 11, EventType = PrototypeEventTypes.AiDepositCompleted, Message = "deposit" },
                new() { Tick = 20, EventType = PrototypeEventTypes.SettlementProcessCompleted, Message = "process" }
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
            Assert.Equal(11, summary.RemainingResourcesByType["logs"]);
            Assert.Equal(2, summary.WorkersByPhase["Idle"]);
            Assert.Equal(1, summary.CraftedItemCounts["stone_axe"]);
            Assert.Equal("stable", summary.SettlementClassification);
            Assert.Equal(2, summary.EventCountsByType[PrototypeEventTypes.AiDepositCompleted]);
            Assert.Equal(16.0f, summary.AverageRouteLengthMeters);
            Assert.Equal(0.15f, summary.PathCoverageRatio);
            Assert.Equal(5, summary.DepotThroughputByDepot["remote_stockpile_1.output"]);
        }
    }
}
