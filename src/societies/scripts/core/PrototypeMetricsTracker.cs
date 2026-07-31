using Societies.Simulation;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Societies.Core
{
    /// <summary>
    /// Lightweight metric capture for prototype runs. The current implementation stays intentionally
    /// compact so it can be emitted on every run before deeper V2 balancing data lands.
    /// </summary>
    public sealed class PrototypeMetricsTracker
    {
        private readonly List<PrototypeMetricsFrame> _frames = new();

        public IReadOnlyList<PrototypeMetricsFrame> Frames => _frames;

        public void Clear()
        {
            _frames.Clear();
        }

        public void Capture(
            long simulationTick,
            float currentHour,
            string weatherName,
            IReadOnlyDictionary<string, int> inventory,
            IReadOnlyDictionary<string, int> stockpile,
            IReadOnlyList<PrototypeWorkerState> workers,
            IReadOnlyList<PrototypeResourceSnapshot> resources,
            PrototypeSettlementClassification settlementClassification,
            int mealCoveragePercent,
            int bedCoveragePercent,
            int hearthFuel,
            int builtStructureCount,
            int blockedStructureCount,
            float averageRouteLengthMeters,
            float averageTravelWorkRatio,
            float pathCoverageRatio,
            IReadOnlyDictionary<string, int> depotThroughputByDepot,
            IReadOnlyDictionary<string, int> routeBacklogTicksByKind,
            PrototypeCrisisState? crisis = null,
            PrototypeSettlementDirective activeDirective = PrototypeSettlementDirective.Neutral,
            IReadOnlyDictionary<string, long>? contributionCountsByResource = null,
            PrototypeRuntimeTelemetrySnapshot? telemetry = null)
        {
            telemetry ??= new PrototypeRuntimeTelemetrySnapshot();
            _frames.Add(new PrototypeMetricsFrame
            {
                SimulationTick = simulationTick,
                CurrentHour = currentHour,
                WeatherName = weatherName,
                InventoryTotal = inventory.Values.Sum(),
                StockpileTotal = stockpile.Values.Sum(),
                WorkerCount = workers.Count,
                ActiveWorkerCount = workers.Count(worker => worker.Phase != PrototypeWorkerPhase.Idle),
                ResourceNodeCount = resources.Count,
                RemainingResourceUnits = resources.Sum(resource => resource.UnitsRemaining),
                SettlementClassification = settlementClassification.ToString().ToLowerInvariant(),
                MealCoveragePercent = mealCoveragePercent,
                BedCoveragePercent = bedCoveragePercent,
                HearthFuel = hearthFuel,
                BuiltStructureCount = builtStructureCount,
                BlockedStructureCount = blockedStructureCount,
                AverageRouteLengthMeters = averageRouteLengthMeters,
                AverageTravelWorkRatio = averageTravelWorkRatio,
                PathCoverageRatio = pathCoverageRatio,
                DepotThroughputTotal = depotThroughputByDepot.Values.Sum(),
                RouteBacklogTickTotal = routeBacklogTicksByKind.Values.Sum(),
                CrisisElapsedTicks = crisis?.ElapsedTicks ?? 0,
                CrisisDeadlineTicks = crisis?.DeadlineTicks ?? 0,
                StabilityHoldTicks = crisis?.StableHoldTicks ?? 0,
                CollapseHoldTicks = crisis?.CollapseHoldTicks ?? 0,
                CrisisOutcome = crisis == null ? string.Empty : crisis.Outcome.ToString().ToLowerInvariant(),
                CrisisFailureReason = FormatCollapseCause(crisis?.CollapseCause ?? PrototypeCrisisCollapseCause.None),
                TerminalEventEmitted = crisis?.TerminalEventEmitted ?? false,
                FirstDirectiveTick = telemetry.FirstDirectiveTick,
                FirstContributionTick = telemetry.FirstContributionTick,
                DirectiveChanges = telemetry.DirectiveChanges,
                FinalDirective = PrototypeSettlementDirectiveCatalog.GetId(activeDirective),
                ContributionsByResource = FormatLongCounts(contributionCountsByResource),
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
                CollapseHoldBreaks = telemetry.CollapseHoldBreaks
            });
        }

        public string BuildCsv()
        {
            StringBuilder builder = new();
            builder.AppendLine("simulation_tick,current_hour,weather,inventory_total,stockpile_total,worker_count,active_worker_count,resource_node_count,remaining_resource_units,settlement_classification,meal_coverage_percent,bed_coverage_percent,hearth_fuel,built_structure_count,blocked_structure_count,average_route_length_meters,average_travel_work_ratio,path_coverage_ratio,depot_throughput_total,route_backlog_tick_total,crisis_elapsed_ticks,crisis_deadline_ticks,stability_hold_ticks,collapse_hold_ticks,crisis_outcome,crisis_failure_reason,terminal_event_emitted,first_directive_tick,first_contribution_tick,directive_changes,final_directive,contributions_by_resource,peak_incapacitated_citizens,minimum_meals,minimum_hearth_fuel,maximum_bed_coverage_percent,final_capable_citizens,final_incapacitated_citizens,final_meals,final_hearth_fuel,final_bed_coverage_percent,stability_hold_entries,stability_hold_breaks,collapse_hold_entries,collapse_hold_breaks");

            foreach (PrototypeMetricsFrame frame in _frames)
            {
                builder.Append(frame.SimulationTick.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CurrentHour.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendCsvField(builder, frame.WeatherName);
                builder.Append(',');
                builder.Append(frame.InventoryTotal.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.StockpileTotal.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.WorkerCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.ActiveWorkerCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.ResourceNodeCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.RemainingResourceUnits.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendCsvField(builder, frame.SettlementClassification);
                builder.Append(',');
                builder.Append(frame.MealCoveragePercent.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.BedCoveragePercent.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.HearthFuel.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.BuiltStructureCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.BlockedStructureCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.AverageRouteLengthMeters.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.AverageTravelWorkRatio.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.PathCoverageRatio.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.DepotThroughputTotal.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.RouteBacklogTickTotal.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CrisisElapsedTicks.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CrisisDeadlineTicks.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.StabilityHoldTicks.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CollapseHoldTicks.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendCsvField(builder, frame.CrisisOutcome);
                builder.Append(',');
                AppendCsvField(builder, frame.CrisisFailureReason);
                builder.Append(',');
                builder.Append(frame.TerminalEventEmitted ? "true" : "false");
                builder.Append(',');
                builder.Append(frame.FirstDirectiveTick?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                builder.Append(',');
                builder.Append(frame.FirstContributionTick?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                builder.Append(',');
                builder.Append(frame.DirectiveChanges.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendCsvField(builder, frame.FinalDirective);
                builder.Append(',');
                AppendCsvField(builder, frame.ContributionsByResource);
                builder.Append(',');
                builder.Append(frame.PeakIncapacitatedCitizens.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.MinimumMeals.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.MinimumHearthFuel.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.MaximumBedCoveragePercent.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.FinalCapableCitizens.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.FinalIncapacitatedCitizens.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.FinalMeals.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.FinalHearthFuel.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.FinalBedCoveragePercent.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.StabilityHoldEntries.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.StabilityHoldBreaks.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CollapseHoldEntries.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CollapseHoldBreaks.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void AppendCsvField(StringBuilder builder, string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                builder.Append('"');
                builder.Append(field.Replace("\"", "\"\""));
                builder.Append('"');
            }
            else
            {
                builder.Append(field);
            }
        }

        private static string FormatLongCounts(IReadOnlyDictionary<string, long>? counts)
        {
            return counts == null
                ? string.Empty
                : string.Join(
                    ';',
                    counts
                        .Where(pair => pair.Value > 0)
                        .OrderBy(pair => pair.Key, System.StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key}:{pair.Value.ToString(CultureInfo.InvariantCulture)}"));
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
    }

    public sealed class PrototypeMetricsFrame : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _workerCount;

        public long SimulationTick { get; set; }

        public float CurrentHour { get; set; }

        public string WeatherName { get; set; } = string.Empty;

        public int InventoryTotal { get; set; }

        public int StockpileTotal { get; set; }

        public int WorkerCount
        {
            get => _workerCount;
            set
            {
                if (_workerCount == value)
                {
                    return;
                }

                _workerCount = value;
                OnPropertyChanged(nameof(WorkerCount));
            }
        }

        public int ActiveWorkerCount { get; set; }

        public int ResourceNodeCount { get; set; }

        public int RemainingResourceUnits { get; set; }

        public string SettlementClassification { get; set; } = string.Empty;

        public int MealCoveragePercent { get; set; }

        public int BedCoveragePercent { get; set; }

        public int HearthFuel { get; set; }

        public int BuiltStructureCount { get; set; }

        public int BlockedStructureCount { get; set; }

        public float AverageRouteLengthMeters { get; set; }

        public float AverageTravelWorkRatio { get; set; }

        public float PathCoverageRatio { get; set; }

        public int DepotThroughputTotal { get; set; }

        public int RouteBacklogTickTotal { get; set; }

        public int CrisisElapsedTicks { get; set; }

        public int CrisisDeadlineTicks { get; set; }

        public int StabilityHoldTicks { get; set; }

        public int CollapseHoldTicks { get; set; }

        public string CrisisOutcome { get; set; } = string.Empty;

        public string CrisisFailureReason { get; set; } = string.Empty;

        public bool TerminalEventEmitted { get; set; }

        public long? FirstDirectiveTick { get; set; }

        public long? FirstContributionTick { get; set; }

        public int DirectiveChanges { get; set; }

        public string FinalDirective { get; set; } = "neutral";

        public string ContributionsByResource { get; set; } = string.Empty;

        public int PeakIncapacitatedCitizens { get; set; }

        public int MinimumMeals { get; set; }

        public int MinimumHearthFuel { get; set; }

        public int MaximumBedCoveragePercent { get; set; }

        public int FinalCapableCitizens { get; set; }

        public int FinalIncapacitatedCitizens { get; set; }

        public int FinalMeals { get; set; }

        public int FinalHearthFuel { get; set; }

        public int FinalBedCoveragePercent { get; set; }

        public int StabilityHoldEntries { get; set; }

        public int StabilityHoldBreaks { get; set; }

        public int CollapseHoldEntries { get; set; }

        public int CollapseHoldBreaks { get; set; }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
