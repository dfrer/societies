using Societies.Simulation;
using System.Collections.Generic;
using System.Globalization;
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
            IReadOnlyList<PrototypeResourceSnapshot> resources)
        {
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
                RemainingResourceUnits = resources.Sum(resource => resource.UnitsRemaining)
            });
        }

        public string BuildCsv()
        {
            StringBuilder builder = new();
            builder.AppendLine("simulation_tick,current_hour,weather,inventory_total,stockpile_total,worker_count,active_worker_count,resource_node_count,remaining_resource_units");

            foreach (PrototypeMetricsFrame frame in _frames)
            {
                builder.Append(frame.SimulationTick.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.CurrentHour.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(frame.WeatherName);
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
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    public sealed class PrototypeMetricsFrame
    {
        public long SimulationTick { get; set; }

        public float CurrentHour { get; set; }

        public string WeatherName { get; set; } = string.Empty;

        public int InventoryTotal { get; set; }

        public int StockpileTotal { get; set; }

        public int WorkerCount { get; set; }

        public int ActiveWorkerCount { get; set; }

        public int ResourceNodeCount { get; set; }

        public int RemainingResourceUnits { get; set; }
    }
}
