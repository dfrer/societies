using Godot;
using Societies.Core;
using Societies.Simulation;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeMetricsTrackerTests
    {
        [Fact]
        public void BuildCsv_EmptyFrames_ProducesHeaderOnly()
        {
            var tracker = new PrototypeMetricsTracker();
            string csv = tracker.BuildCsv();

            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.Equal("simulation_tick,current_hour,weather,inventory_total,stockpile_total,worker_count,active_worker_count,resource_node_count,remaining_resource_units,settlement_classification,meal_coverage_percent,bed_coverage_percent,hearth_fuel,built_structure_count,blocked_structure_count,average_route_length_meters,average_travel_work_ratio,path_coverage_ratio,depot_throughput_total,route_backlog_tick_total", lines[0].Trim());
        }

        [Fact]
        public void BuildCsv_SingleFrame_ProducesHeaderPlusOneDataLine()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 100, hour: 8.5f, weather: "Clear", workers: 3, classification: "stable");

            string csv = tracker.BuildCsv();
            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length);
            string data = lines[1];
            // tick=100, hour=8.5, weather=Clear
            Assert.StartsWith("100,", data);
            Assert.Contains(",Clear,", data);
            // classification field (10th from tick) should be stable
            string[] fields = data.Split(',');
            Assert.Equal("stable", fields[9]);
            // route_backlog_tick_total should be 1 (from routeBacklogTicksByKind: ["haul"] = 1)
            Assert.Equal("1", fields[fields.Length - 1]);
        }

        [Fact]
        public void BuildCsv_WeatherNameWithComma_IsQuoted()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Clear, Windy", workers: 0, classification: "stable");

            string csv = tracker.BuildCsv();
            string dataLine = csv.Split('\n')[1];
            // The weather field should be quoted since it contains a comma
            Assert.Contains("\"Clear, Windy\"", dataLine);
        }

        [Fact]
        public void BuildCsv_WeatherNameWithQuote_IsDoubleEscaped()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "He said \"hello\"", workers: 0, classification: "stable");

            string csv = tracker.BuildCsv();
            string dataLine = csv.Split('\n')[1];
            Assert.Contains("\"He said \"\"hello\"\"\"", dataLine);
        }

        [Fact]
        public void BuildCsv_WeatherNameWithNewline_IsQuoted()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Line1\nLine2", workers: 0, classification: "strained");

            string csv = tracker.BuildCsv();
            // The raw CSV must contain the properly quoted field with escaped newline inside
            Assert.Contains("\"Line1\nLine2\"", csv);
        }

        [Fact]
        public void BuildCsv_ClassificationWithComma_NotPresentWhenEnumParseFails()
        {
            // "very, stable" is not a valid enum value, so Enum.TryParse falls back to Strained.
            // The output will contain "strained" (lowercase), not "very, stable".
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Clear", workers: 0, classification: "very, stable");

            string csv = tracker.BuildCsv();
            // Verify data was captured (header + data row)
            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            // Classification field shows "strained" since the parse fell back
            string[] fields = lines[1].Split(',');
            Assert.Equal("strained", fields[9]);
        }

        [Fact]
        public void BuildCsv_CommaInTextField_IsQuoted()
        {
            // Test CSV field escaping by capturing with a weather name containing a comma
            // This bypasses the classification parsing issue
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Sunny, Hot", workers: 0, classification: "stable");

            string csv = tracker.BuildCsv();
            Assert.Contains("\"Sunny, Hot\"", csv);
        }

        [Fact]
        public void BuildCsv_MultipleFrames_ProducesCorrectRowCount()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 0, hour: 0f, weather: "Clear", workers: 1, classification: "stable");
            CaptureFrame(tracker, tick: 20, hour: 1f, weather: "Rain", workers: 2, classification: "strained");
            CaptureFrame(tracker, tick: 40, hour: 2f, weather: "Clear", workers: 3, classification: "stable");

            string csv = tracker.BuildCsv();
            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, lines.Length); // 1 header + 3 data
        }

        [Fact]
        public void BuildCsv_ZeroValues_RendersCorrectly()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 0, hour: 0f, weather: "Clear", workers: 0, classification: "stable");

            string csv = tracker.BuildCsv();
            string dataLine = csv.Split('\n')[1];
            string[] fields = dataLine.Split(',');

            Assert.Equal("0", fields[0]);   // simulation_tick
            Assert.Equal("Clear", fields[2]); // weather (string field)
            Assert.Equal("10", fields[3]);   // inventory_total
            Assert.Equal("5", fields[4]);    // stockpile_total
            Assert.Equal("0", fields[5]);    // worker_count
            Assert.Equal("0", fields[6]);    // active_worker_count
            Assert.Equal("1", fields[18]);   // depot_throughput_total
            Assert.Equal("1", fields[19]);   // route_backlog_tick_total
        }

        [Fact]
        public void BuildCsv_LargeNumbers_UsesInvariantCulture()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1234567890123, hour: 12.345f, weather: "Clear", workers: 50, classification: "stable");
            // Override specific fields by capturing a second frame with large values
            // (the helper uses reasonable defaults; this test checks no localized commas appear)
            string csv = tracker.BuildCsv();
            string dataLine = csv.Split('\n')[1];
            // Verify no localized formatting (no commas as thousand separators in tick)
            Assert.DoesNotContain("1,234,567,890,123", dataLine);
        }

        [Fact]
        public void Clear_RemovesAllFrames()
        {
            var tracker = new PrototypeMetricsTracker();
            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Clear", workers: 1, classification: "stable");
            tracker.Clear();

            string csv = tracker.BuildCsv();
            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines); // header only
        }

        [Fact]
        public void Capture_IncreasesFrameCount()
        {
            var tracker = new PrototypeMetricsTracker();
            Assert.Empty(tracker.Frames);

            CaptureFrame(tracker, tick: 1, hour: 0f, weather: "Clear", workers: 1, classification: "stable");
            Assert.Single(tracker.Frames);

            CaptureFrame(tracker, tick: 2, hour: 0.1f, weather: "Rain", workers: 2, classification: "stable");
            Assert.Equal(2, tracker.Frames.Count);
        }

        private static void CaptureFrame(
            PrototypeMetricsTracker tracker,
            long tick,
            float hour,
            string weather,
            int workers,
            string classification)
        {
            tracker.Capture(
                simulationTick: tick,
                currentHour: hour,
                weatherName: weather,
                inventory: new Dictionary<string, int> { ["logs"] = 10 },
                stockpile: new Dictionary<string, int> { ["meals"] = 5 },
                workers: MakeWorkers(workers),
                resources: new List<PrototypeResourceSnapshot>
                {
                    new() { ResourceId = "logs", UnitsRemaining = 10, Position = default, ClusterId = "c1" }
                },
                settlementClassification: Enum.TryParse<PrototypeSettlementClassification>(classification, ignoreCase: true, out var cls) ? cls : PrototypeSettlementClassification.Strained,
                mealCoveragePercent: 50,
                bedCoveragePercent: 50,
                hearthFuel: 5,
                builtStructureCount: 2,
                blockedStructureCount: 0,
                averageRouteLengthMeters: 10.0f,
                averageTravelWorkRatio: 1.0f,
                pathCoverageRatio: 0.1f,
                depotThroughputByDepot: new Dictionary<string, int> { ["d1"] = 1 },
                routeBacklogTicksByKind: new Dictionary<string, int> { ["haul"] = 1 });
        }

        private static PrototypeWorkerState[] MakeWorkers(int count)
        {
            if (count == 0) return Array.Empty<PrototypeWorkerState>();
            var workers = new PrototypeWorkerState[count];
            for (int i = 0; i < count; i++)
            {
                workers[i] = new PrototypeWorkerState
                {
                    WorkerId = $"w{i}",
                    Phase = PrototypeWorkerPhase.Idle
                };
            }
            return workers;
        }
    }
}