using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Compact world summary emitted alongside the prototype snapshot for repeatable run analysis.
    /// </summary>
    public static class PrototypeWorldSummaryBuilder
    {
        public static PrototypeWorldSummary Build(
            PrototypeRuntimeSession session,
            TerrainGenerator? terrain,
            IReadOnlyList<PrototypeResourceSnapshot> resources)
        {
            WorldGenerationResult? world = session.World;
            Dictionary<string, int> biomeCellCounts = world?.WorldMap.Cells
                .GroupBy(cell => cell.Biome.ToString())
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count()) ?? new Dictionary<string, int>();
            int buildableCellCount = world?.WorldMap.Cells.Count(cell => cell.IsBuildable) ?? 0;
            int totalCellCount = world?.WorldMap.Cells.Count ?? 0;

            return new PrototypeWorldSummary
            {
                SchemaVersion = 3,
                ScenarioId = session.Scenario.Id,
                ScenarioDisplayName = session.Scenario.DisplayName,
                WorldSeed = session.WorldSeed,
                SimulationSeed = session.SimulationSeed,
                SimulationTick = session.SimulationTick,
                WorldSize = terrain?.WorldSize ?? 0.0f,
                GroundHeight = terrain?.GroundHeight ?? 0.0f,
                GridWidth = world?.WorldMap.GridWidth ?? 0,
                GridHeight = world?.WorldMap.GridHeight ?? 0,
                CellSizeMeters = world?.WorldMap.CellSizeMeters ?? 0.0f,
                TerrainMode = world == null ? "flat" : "heightfield_v1",
                BiomeCellCounts = biomeCellCounts,
                BuildableCellCount = buildableCellCount,
                BuildableCellRatio = totalCellCount == 0 ? 0.0f : buildableCellCount / (float)totalCellCount,
                MeanElevation = world?.WorldMap.Cells.Average(cell => cell.ElevationMeters) ?? 0.0f,
                MaxElevation = world?.WorldMap.Cells.Max(cell => cell.ElevationMeters) ?? 0.0f,
                AverageMovementCost = world?.WorldMap.Cells.Average(cell => cell.MovementCost) ?? 0.0f,
                StarterResourceDistances = world?.StarterResourceDistances.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, float>(),
                AverageClusterDistances = world?.AverageClusterDistances.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, float>(),
                SettlementAnchorPosition = PrototypeSerializableVector3.FromVector3(session.SettlementAnchorPosition),
                WorldHash = world?.WorldHash ?? string.Empty,
                ResourceNodeCounts = resources
                    .GroupBy(resource => resource.ResourceId)
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Count()),
                RemainingResourceUnits = resources
                    .GroupBy(resource => resource.ResourceId)
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Sum(resource => resource.UnitsRemaining)),
                WorkerCount = session.Workers.Count
            };
        }
    }

    public sealed class PrototypeWorldSummary
    {
        public int SchemaVersion { get; set; } = 3;

        public string ScenarioId { get; set; } = string.Empty;

        public string ScenarioDisplayName { get; set; } = string.Empty;

        public int WorldSeed { get; set; }

        public int SimulationSeed { get; set; }

        public long SimulationTick { get; set; }

        public float WorldSize { get; set; }

        public float GroundHeight { get; set; }

        public int GridWidth { get; set; }

        public int GridHeight { get; set; }

        public float CellSizeMeters { get; set; }

        public string TerrainMode { get; set; } = string.Empty;

        public Dictionary<string, int> BiomeCellCounts { get; set; } = new();

        public int BuildableCellCount { get; set; }

        public float BuildableCellRatio { get; set; }

        public float MeanElevation { get; set; }

        public float MaxElevation { get; set; }

        public float AverageMovementCost { get; set; }

        public Dictionary<string, float> StarterResourceDistances { get; set; } = new();

        public Dictionary<string, float> AverageClusterDistances { get; set; } = new();

        public PrototypeSerializableVector3 SettlementAnchorPosition { get; set; }

        public string WorldHash { get; set; } = string.Empty;

        public Dictionary<string, int> ResourceNodeCounts { get; set; } = new();

        public Dictionary<string, int> RemainingResourceUnits { get; set; } = new();

        public int WorkerCount { get; set; }
    }
}
