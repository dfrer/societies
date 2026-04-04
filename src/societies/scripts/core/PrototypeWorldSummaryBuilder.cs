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
            return new PrototypeWorldSummary
            {
                SchemaVersion = 2,
                ScenarioId = session.Scenario.Id,
                ScenarioDisplayName = session.Scenario.DisplayName,
                SimulationSeed = session.SimulationSeed,
                SimulationTick = session.SimulationTick,
                WorldSize = terrain?.WorldSize ?? 0.0f,
                GroundHeight = terrain?.GroundHeight ?? 0.0f,
                TerrainMode = "flat",
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
        public int SchemaVersion { get; set; } = 2;

        public string ScenarioId { get; set; } = string.Empty;

        public string ScenarioDisplayName { get; set; } = string.Empty;

        public int SimulationSeed { get; set; }

        public long SimulationTick { get; set; }

        public float WorldSize { get; set; }

        public float GroundHeight { get; set; }

        public string TerrainMode { get; set; } = string.Empty;

        public Dictionary<string, int> ResourceNodeCounts { get; set; } = new();

        public Dictionary<string, int> RemainingResourceUnits { get; set; } = new();

        public int WorkerCount { get; set; }
    }
}
