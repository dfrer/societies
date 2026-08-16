using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class SchedulingParameterExperimentTests
{
    [Fact]
    public async Task Matrix_UsesMatchedRecordedFixturesFrozenRoundsOrderedCommitAndReplayForEveryCell()
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();

        Assert.Equal(6, experiment.Cells.Count);
        Assert.Equal(
            new[] { (4, SnowGlobePlanningCadence.EveryTick), (4, SnowGlobePlanningCadence.EveryOtherTick), (8, SnowGlobePlanningCadence.EveryTick), (8, SnowGlobePlanningCadence.EveryOtherTick), (16, SnowGlobePlanningCadence.EveryTick), (16, SnowGlobePlanningCadence.EveryOtherTick) },
            experiment.Cells.Select(cell => (cell.Cell.AgentCount, cell.Cell.Cadence)));

        foreach (SnowGlobeParameterExperimentCellResult cell in experiment.Cells)
        {
            int expectedWorldTicks = SnowGlobeSchedulingParameterExperiment.PlanningRounds * (int)cell.Cell.Cadence;
            int expectedCalls = cell.Cell.AgentCount * SnowGlobeSchedulingParameterExperiment.PlanningRounds;
            IReadOnlyList<SnowGlobeRecordedResponse> firstFixture = SnowGlobeSchedulingParameterExperiment.CreateFixture(cell.Cell);
            IReadOnlyList<SnowGlobeRecordedResponse> secondFixture = SnowGlobeSchedulingParameterExperiment.CreateFixture(cell.Cell);

            Assert.Equal(firstFixture, secondFixture);
            Assert.Equal(expectedWorldTicks, cell.WorldTicks);
            Assert.Equal(expectedWorldTicks, cell.SharedSnapshotSequentialWorld.Tick);
            Assert.Equal(expectedWorldTicks, cell.ControlledParallelWorld.Tick);
            Assert.Equal(cell.SharedSnapshotSequential.StateDigest, cell.ControlledParallel.StateDigest);
            Assert.Equal(cell.SharedSnapshotSequential.EventDigest, cell.ControlledParallel.EventDigest);
            Assert.Equal(cell.SharedSnapshotSequentialWorld.Events, cell.ControlledParallelWorld.Events);
            Assert.Equal(SnowGlobeSchedulingParameterExperiment.PlanningRounds, cell.SharedSnapshotSequential.Rounds.Count);
            foreach ((SnowGlobeRoundTrace sequential, SnowGlobeRoundTrace parallel) in cell.SharedSnapshotSequential.Rounds.Zip(cell.ControlledParallel.Rounds))
            {
                Assert.Equal(sequential.Observations, parallel.Observations);
                Assert.Equal(sequential.Proposals, parallel.Proposals);
            }

            SnowGlobeWorld replay = SnowGlobeSchedulingParameterExperiment.Replay(cell.Cell, expectedWorldTicks, cell.ControlledParallelWorld.Events);
            Assert.Equal(cell.ControlledParallel.StateDigest, replay.StateDigest());
            Assert.Equal(cell.ControlledParallel.EventDigest, replay.EventDigest());
            AssertMetrics(cell.SharedSnapshotSequential.Metrics, expectedCalls, controlledParallel: false);
            AssertMetrics(cell.ControlledParallel.Metrics, expectedCalls, controlledParallel: true);
            Assert.Equal(1, cell.SequentialPeakInFlightRequests);
            Assert.Equal(cell.Cell.AgentCount, cell.ControlledParallelPeakInFlightRequests);
            Assert.Equal(cell.SharedSnapshotSequential.Metrics.TotalRecordedLatencyUnits, cell.ControlledParallel.Metrics.TotalRecordedLatencyUnits);
            Assert.Equal(cell.SharedSnapshotSequential.Metrics.TotalRecordedLatencyUnits, cell.SharedSnapshotSequential.Metrics.CriticalPathLatencyUnits);
            Assert.True(cell.ControlledParallel.Metrics.CriticalPathLatencyUnits <= cell.ControlledParallel.Metrics.TotalRecordedLatencyUnits);
            Assert.True(cell.Cell.AgentCount <= SnowGlobeSchedulingParameterExperiment.MaximumInFlightRequests);
        }
    }

    [Fact]
    public async Task MatrixReport_IsCanonicalOrderedAndByteIdenticalAcrossRepeatedRuns()
    {
        SnowGlobeParameterExperimentReport first = SnowGlobeParameterExperimentReportBuilder.Build(
            await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync());
        SnowGlobeParameterExperimentReport second = SnowGlobeParameterExperimentReportBuilder.Build(
            await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync());

        Assert.Equal(first.CanonicalUtf8.ToArray(), second.CanonicalUtf8.ToArray());
        using JsonDocument document = JsonDocument.Parse(first.CanonicalUtf8);
        JsonElement root = document.RootElement;
        Assert.Equal(new[] { "schema_version", "timing_semantics", "planning_rounds", "agent_counts", "planning_cadences", "maximum_in_flight_requests", "cells" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("snow_globe_parameter_experiment/v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("logical_not_wall_clock", root.GetProperty("timing_semantics").GetString());
        Assert.Equal(4, root.GetProperty("planning_rounds").GetInt32());
        Assert.Equal(new[] { 4, 8, 16 }, root.GetProperty("agent_counts").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(new[] { "every_tick", "every_other_tick" }, root.GetProperty("planning_cadences").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(16, root.GetProperty("maximum_in_flight_requests").GetInt32());
        Assert.Equal(6, root.GetProperty("cells").GetArrayLength());
        Assert.All(root.GetProperty("cells").EnumerateArray(), cell =>
        {
            Assert.Equal(new[] { "agent_count", "cadence", "world_ticks", "sequential_peak_in_flight_requests", "controlled_parallel_peak_in_flight_requests", "equivalence", "modes" }, cell.EnumerateObject().Select(property => property.Name));
            Assert.All(cell.GetProperty("equivalence").EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));
        });
    }

    [Fact]
    public void UnsupportedParameterCell_FailsBeforeSchedulingOrFixtureCreation()
    {
        SnowGlobeParameterExperimentCell unsupportedAgentCount = new(3, SnowGlobePlanningCadence.EveryTick);
        SnowGlobeParameterExperimentCell unsupportedCadence = new(4, (SnowGlobePlanningCadence)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => SnowGlobeSchedulingParameterExperiment.CreateFixture(unsupportedAgentCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowGlobeSchedulingParameterExperiment.CreateFixture(unsupportedCadence));
    }

    [Fact]
    public async Task IncoherentParameterResult_FailsClosedBeforeCanonicalReportBytes()
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();
        SnowGlobeParameterExperimentCellResult first = experiment.Cells[0] with
        {
            ControlledParallel = experiment.Cells[0].ControlledParallel with { EventDigest = "mismatched-event" }
        };
        SnowGlobeParameterExperimentResult incoherent = new(new[] { first }.Concat(experiment.Cells.Skip(1)).ToArray());

        Assert.Throws<InvalidOperationException>(() => SnowGlobeParameterExperimentReportBuilder.Build(incoherent));
    }

    [Fact]
    public async Task MissingDuplicateOrReorderedCells_AreRejectedInsteadOfChangingCanonicalOutput()
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();
        SnowGlobeParameterExperimentResult missing = new(experiment.Cells.Take(5).ToArray());
        SnowGlobeParameterExperimentResult duplicate = new(new[] { experiment.Cells[0], experiment.Cells[0] }.Concat(experiment.Cells.Skip(2)).ToArray());
        SnowGlobeParameterExperimentResult reordered = new(experiment.Cells.Reverse().ToArray());

        AssertCanonicalMatrixRejected(missing);
        AssertCanonicalMatrixRejected(duplicate);
        AssertCanonicalMatrixRejected(reordered);
    }

    [Theory]
    [InlineData("critical_path")]
    [InlineData("throughput")]
    [InlineData("coverage")]
    public async Task ImpossibleDerivedMetrics_AreRejectedBeforeCanonicalReportBytes(string metric)
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();
        SnowGlobeComparisonMetrics metrics = experiment.Cells[0].ControlledParallel.Metrics;
        switch (metric)
        {
            case "critical_path":
                SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.CriticalPathLatencyUnits), metrics.TotalRecordedLatencyUnits + 1);
                break;
            case "throughput":
                SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.ThroughputMilliActionsPerLatencyUnit), 0);
                break;
            case "coverage":
                SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.DispatchCoveragePermille), 999);
                break;
        }

        Assert.Throws<InvalidOperationException>(() => SnowGlobeParameterExperimentReportBuilder.Build(experiment));
    }

    [Fact]
    public async Task FormulaConsistentButFalseSequentialCriticalPath_IsRejectedBeforeCanonicalReportBytes()
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();
        SnowGlobeComparisonMetrics metrics = experiment.Cells[0].SharedSnapshotSequential.Metrics;
        int falseCriticalPath = metrics.TotalRecordedLatencyUnits - 1;
        SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.CriticalPathLatencyUnits), falseCriticalPath);
        SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.ThroughputMilliActionsPerLatencyUnit), metrics.AcceptedActions * 1000 / falseCriticalPath);

        Assert.Throws<InvalidOperationException>(() => SnowGlobeParameterExperimentReportBuilder.Build(experiment));
    }

    [Fact]
    public async Task ZeroedMatchedLogicalLatencyMetrics_AreRejectedBeforeCanonicalReportBytes()
    {
        SnowGlobeParameterExperimentResult experiment = await SnowGlobeSchedulingParameterExperiment.RunMatrixAsync();
        foreach (SnowGlobeComparisonMetrics metrics in new[]
                 {
                     experiment.Cells[0].SharedSnapshotSequential.Metrics,
                     experiment.Cells[0].ControlledParallel.Metrics
                 })
        {
            SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.TotalRecordedLatencyUnits), 0);
            SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.CriticalPathLatencyUnits), 0);
            SetMetric(metrics, nameof(SnowGlobeComparisonMetrics.ThroughputMilliActionsPerLatencyUnit), 0);
        }

        Assert.Throws<InvalidOperationException>(() => SnowGlobeParameterExperimentReportBuilder.Build(experiment));
    }

    private static void AssertMetrics(SnowGlobeComparisonMetrics metrics, int expectedCalls, bool controlledParallel)
    {
        Assert.Equal(expectedCalls, metrics.InferenceCalls);
        Assert.Equal(expectedCalls, metrics.AcceptedActions + metrics.RejectedActions);
        Assert.Equal(SnowGlobeSchedulingParameterExperiment.PlanningRounds, metrics.SharedSnapshotRounds);
        Assert.Equal(controlledParallel ? SnowGlobeSchedulingParameterExperiment.PlanningRounds : 0, metrics.ControlledParallelRounds);
        Assert.Equal(1000, metrics.DispatchCoveragePermille);
        Assert.All(metrics.DeliberationTurnsByAgent.Values, turns => Assert.Equal(SnowGlobeSchedulingParameterExperiment.PlanningRounds, turns));
        Assert.InRange(metrics.TotalRecordedLatencyUnits, 0, int.MaxValue);
        Assert.InRange(metrics.CriticalPathLatencyUnits, 0, int.MaxValue);
        Assert.InRange(metrics.ThroughputMilliActionsPerLatencyUnit, 0, int.MaxValue);
    }

    private static void AssertCanonicalMatrixRejected(SnowGlobeParameterExperimentResult experiment)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SnowGlobeParameterExperimentReportBuilder.Build(experiment));
        Assert.Equal("Parameter experiment cells must be the complete canonical matrix in order.", exception.Message);
    }

    private static void SetMetric(SnowGlobeComparisonMetrics metrics, string propertyName, int value)
    {
        typeof(SnowGlobeComparisonMetrics).GetProperty(propertyName)!.SetValue(metrics, value);
    }
}
