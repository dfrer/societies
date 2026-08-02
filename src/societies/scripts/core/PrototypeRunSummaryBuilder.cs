using System.Collections.Generic;
using System.Linq;
using Societies.Simulation;

namespace Societies.Core
{
    /// <summary>
    /// Builds compact machine-readable summaries for prototype validation runs.
    /// </summary>
    public static class PrototypeRunSummaryBuilder
    {
        public static PrototypeRunSummary Build(
            PrototypeRuntimeSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> eventRecords,
            float startHour)
        {
            return Build(snapshot, eventRecords, startHour, snapshot.ScenarioId, string.Empty, null);
        }

        public static PrototypeRunSummary Build(
            PrototypeRuntimeSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> eventRecords,
            float startHour,
            string scenarioId,
            string scenarioDisplayName,
            PrototypeWorldSummary? worldSummary)
        {
            Dictionary<string, int> craftedItemCounts = BuildCraftedItemCounts(snapshot);
            PrototypeLogisticsMetricsState logisticsMetrics = snapshot.Settlement?.LogisticsMetrics ?? new PrototypeLogisticsMetricsState();
            PrototypeRuntimeTelemetrySnapshot telemetry = snapshot.Telemetry ?? new PrototypeRuntimeTelemetrySnapshot();
            PrototypeCrisisStateSnapshot? crisis = snapshot.Crisis;
            PrototypeCivicPolicySnapshot civicPolicy =
                snapshot.CivicPolicy ?? new PrototypeCivicPolicySnapshot();

            return new PrototypeRunSummary
            {
                SchemaVersion = snapshot.SchemaVersion,
                ScenarioId = scenarioId,
                ScenarioDisplayName = scenarioDisplayName,
                SettlementClassification = ClassifySettlement(snapshot),
                WorldSeed = worldSummary?.WorldSeed ?? snapshot.WorldSeed,
                TerrainMode = worldSummary?.TerrainMode ?? string.Empty,
                BuildableCellRatio = worldSummary?.BuildableCellRatio ?? 0.0f,
                BiomeCellCounts = worldSummary?.BiomeCellCounts.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, int>(),
                SimulationSeed = snapshot.SimulationSeed,
                SimulationTick = snapshot.SimulationTick,
                StartHour = startHour,
                StartTimeText = FormatTime(startHour),
                EndHour = snapshot.CurrentHour,
                EndTimeText = FormatTime(snapshot.CurrentHour),
                FinalWeather = snapshot.CurrentWeather,
                PlayerInventory = OrderCounts(snapshot.Inventory),
                Stockpile = OrderCounts(snapshot.Stockpile),
                RemainingResourcesByType = OrderCounts(
                    snapshot.Resources
                        .GroupBy(resource => resource.ResourceId)
                        .ToDictionary(group => group.Key, group => group.Sum(resource => resource.UnitsRemaining))),
                WorkersByPhase = OrderCounts(
                    snapshot.Workers
                        .GroupBy(worker => worker.Phase)
                        .ToDictionary(group => group.Key, group => group.Count())),
                CraftedItemCounts = craftedItemCounts,
                ProducedResources = OrderCounts(snapshot.Settlement?.ProducedResources ?? new Dictionary<string, int>()),
                ConsumedResources = OrderCounts(snapshot.Settlement?.ConsumedResources ?? new Dictionary<string, int>()),
                BlockedReasonCounts = OrderCounts(snapshot.Settlement?.BlockedReasonCounts ?? new Dictionary<string, int>()),
                BuiltStructuresByKind = BuildStructureCounts(snapshot),
                MealCoveragePercent = ComputeMealCoverage(snapshot),
                BedCoveragePercent = ComputeBedCoverage(snapshot),
                HearthFuel = snapshot.Stockpile.GetValueOrDefault("hearth_fuel", 0),
                HearthLitTicks = snapshot.Settlement?.HearthLitTicks ?? 0,
                BuildQueueStatus = BuildQueueStatus(snapshot),
                CollapseReason = InferCollapseReason(snapshot),
                AverageRouteLengthMeters = logisticsMetrics.AverageRouteLengthMeters(),
                AverageTravelWorkRatio = logisticsMetrics.AverageTravelWorkRatio(),
                PathCoverageRatio = logisticsMetrics.PathCoverageRatio,
                DepotThroughputByDepot = OrderCounts(logisticsMetrics.DepotThroughputByDepot),
                RouteBacklogTicksByKind = OrderCounts(logisticsMetrics.RouteBacklogTicksByKind),
                EventCountsByType = OrderCounts(
                    eventRecords
                        .GroupBy(record => record.EventType)
                        .ToDictionary(group => group.Key, group => group.Count())),
                CrisisElapsedTicks = crisis?.ElapsedTicks ?? 0,
                CrisisDeadlineTicks = crisis?.DeadlineTicks ?? 0,
                CrisisElapsedSeconds = crisis == null || crisis.TicksPerSecond <= 0
                    ? 0.0d
                    : crisis.ElapsedTicks / (double)crisis.TicksPerSecond,
                StabilityHoldTicks = crisis?.StableHoldTicks ?? 0,
                CollapseHoldTicks = crisis?.CollapseHoldTicks ?? 0,
                CrisisOutcome = crisis?.Outcome.ToString().ToLowerInvariant() ?? string.Empty,
                CrisisFailureReason = FormatCollapseCause(crisis?.CollapseCause ?? PrototypeCrisisCollapseCause.None),
                TerminalEventEmitted = crisis?.TerminalEventEmitted ?? false,
                FirstDirectiveTick = telemetry.FirstDirectiveTick,
                FirstContributionTick = telemetry.FirstContributionTick,
                DirectiveChanges = telemetry.DirectiveChanges,
                FinalDirective = snapshot.Directive?.DirectiveId ?? "neutral",
                ContributionsByResource = OrderLongCounts(
                    snapshot.ContributionCountsByResource ?? new Dictionary<string, long>()),
                PeakIncapacitatedCitizens = telemetry.PeakIncapacitatedCitizens,
                MinimumMeals = telemetry.MinimumMeals,
                MinimumHearthFuel = telemetry.MinimumHearthFuel,
                MaximumBedCoveragePercent = telemetry.MaximumBedCoveragePercent,
                FinalCapableCitizens = telemetry.FinalCapableCitizens,
                FinalIncapacitatedCitizens = telemetry.FinalIncapacitatedCitizens,
                FinalMeals = telemetry.FinalMeals,
                FinalHearthFuel = telemetry.FinalHearthFuel,
                FinalBedCoveragePercent = telemetry.FinalBedCoveragePercent,
                StabilityHoldEntries = telemetry.StabilityHoldEntries,
                StabilityHoldBreaks = telemetry.StabilityHoldBreaks,
                CollapseHoldEntries = telemetry.CollapseHoldEntries,
                CollapseHoldBreaks = telemetry.CollapseHoldBreaks,
                CivicPolicy = new PrototypeCivicPolicySnapshot
                {
                    PolicyId = civicPolicy.PolicyId,
                    SelectedTick = civicPolicy.SelectedTick,
                    Version = civicPolicy.Version,
                    WindowStartTick = civicPolicy.WindowStartTick,
                    WindowEndTick = civicPolicy.WindowEndTick
                }
            };
        }

        private static string ClassifySettlement(PrototypeRuntimeSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Settlement?.Classification))
            {
                return snapshot.Settlement.Classification;
            }

            if (snapshot.Stockpile.GetValueOrDefault("meals", 0) > 0 &&
                snapshot.Stockpile.GetValueOrDefault("hearth_fuel", 0) > 0 &&
                snapshot.Stockpile.GetValueOrDefault("beds", 0) > 0)
            {
                return "stable";
            }

            if (snapshot.Stockpile.Values.Sum() > 0 || snapshot.Inventory.Values.Sum() > 0)
            {
                return "strained";
            }

            return "collapsed";
        }

        private static Dictionary<string, int> BuildCraftedItemCounts(PrototypeRuntimeSnapshot snapshot)
        {
            Dictionary<string, int> combinedInventory = snapshot.Inventory
                .Concat(snapshot.Stockpile)
                .GroupBy(pair => pair.Key)
                .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));

            return OrderCounts(
                CraftingSystem
                    .GetCraftedItemIds()
                    .Where(itemId => combinedInventory.TryGetValue(itemId, out int count) && count > 0)
                    .ToDictionary(itemId => itemId, itemId => combinedInventory[itemId]));
        }

        private static Dictionary<string, int> BuildStructureCounts(PrototypeRuntimeSnapshot snapshot)
        {
            if (snapshot.Settlement == null)
            {
                return new Dictionary<string, int>();
            }

            return OrderCounts(
                snapshot.Settlement.Structures
                    .Where(structure => structure.IsBuilt)
                    .GroupBy(structure => structure.StructureKindId)
                    .ToDictionary(group => group.Key, group => group.Count()));
        }

        private static int ComputeMealCoverage(PrototypeRuntimeSnapshot snapshot)
        {
            int citizenCount = snapshot.Settlement?.Citizens.Count ?? snapshot.Workers.Count;
            if (citizenCount <= 0)
            {
                return 100;
            }

            return (int)System.Math.Round(System.Math.Clamp(snapshot.Stockpile.GetValueOrDefault("meals", 0) / (double)citizenCount, 0.0d, 1.0d) * 100.0d);
        }

        private static int ComputeBedCoverage(PrototypeRuntimeSnapshot snapshot)
        {
            int citizenCount = snapshot.Settlement?.Citizens.Count ?? snapshot.Workers.Count;
            if (citizenCount <= 0)
            {
                return 100;
            }

            return (int)System.Math.Round(System.Math.Clamp(snapshot.Stockpile.GetValueOrDefault("beds", 0) / (double)citizenCount, 0.0d, 1.0d) * 100.0d);
        }

        private static string BuildQueueStatus(PrototypeRuntimeSnapshot snapshot)
        {
            if (snapshot.Settlement == null || snapshot.Settlement.BuildQueue.Count == 0)
            {
                return "Build Queue: empty";
            }

            int index = System.Math.Clamp(snapshot.Settlement.SelectedBuildQueueIndex, 0, snapshot.Settlement.BuildQueue.Count - 1);
            PrototypeBuildQueueEntrySnapshot entry = snapshot.Settlement.BuildQueue[index];
            string state = entry.IsCompleted
                ? "complete"
                : entry.IsPaused ? "paused" : "active";
            return $"Build Queue Focus: {entry.DisplayName} ({state})";
        }

        private static string InferCollapseReason(PrototypeRuntimeSnapshot snapshot)
        {
            string classification = ClassifySettlement(snapshot);
            if (!string.Equals(classification, "collapsed", System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            int meals = snapshot.Stockpile.GetValueOrDefault("meals", 0) + snapshot.Stockpile.GetValueOrDefault("berries", 0);
            int beds = snapshot.Stockpile.GetValueOrDefault("beds", 0);
            int hearthFuel = snapshot.Stockpile.GetValueOrDefault("hearth_fuel", 0);
            int criticalCitizens = snapshot.Workers.Count(worker => string.Equals(worker.Phase, PrototypeWorkerPhase.Incapacitated.ToString(), System.StringComparison.OrdinalIgnoreCase));
            Dictionary<string, int> backlogTicks = snapshot.Settlement?.LogisticsMetrics.RouteBacklogTicksByKind ?? new Dictionary<string, int>();
            int haulBacklog = backlogTicks
                .Where(pair => pair.Key.Contains("haul", System.StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (haulBacklog > 240)
            {
                return "logistics.backlog";
            }

            if (meals <= 0 || criticalCitizens > 0)
            {
                return "food.shortage";
            }

            if (beds <= 0)
            {
                return "housing.shortage";
            }

            if (hearthFuel <= 0)
            {
                return "fuel.shortage";
            }

            return "mixed.shortage";
        }

        private static Dictionary<string, int> OrderCounts(IReadOnlyDictionary<string, int> counts)
        {
            return counts
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private static Dictionary<string, long> OrderLongCounts(IReadOnlyDictionary<string, long> counts)
        {
            return counts
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private static string FormatCollapseCause(PrototypeCrisisCollapseCause cause)
        {
            return cause switch
            {
                PrototypeCrisisCollapseCause.None => string.Empty,
                PrototypeCrisisCollapseCause.IncapacitatedHold => "incapacitated_hold",
                PrototypeCrisisCollapseCause.Deadline => "deadline",
                _ => cause.ToString().ToLowerInvariant()
            };
        }

        private static string FormatTime(float currentHour)
        {
            int hours = (int)System.Math.Floor(currentHour);
            int minutes = (int)System.Math.Round((currentHour - hours) * 60.0);

            if (minutes >= 60)
            {
                hours += 1;
                minutes = 0;
            }

            hours = hours % 24;
            return $"{hours:00}:{minutes:00}";
        }
    }

    internal static class PrototypeLogisticsMetricsExtensions
    {
        public static float AverageRouteLengthMeters(this PrototypeLogisticsMetricsState metrics)
        {
            return metrics.CompletedRouteCount <= 0
                ? 0.0f
                : metrics.TotalCompletedRouteDistanceMeters / metrics.CompletedRouteCount;
        }

        public static float AverageTravelWorkRatio(this PrototypeLogisticsMetricsState metrics)
        {
            return metrics.WorkTicksAccumulated <= 0
                ? metrics.TravelTicksAccumulated
                : metrics.TravelTicksAccumulated / (float)metrics.WorkTicksAccumulated;
        }
    }
}
