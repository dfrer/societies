using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ResilienceExperimentTests
{
    [Fact]
    public void Matrix_IsBoundedRepeatableReplayEquivalentAndDispatchFairForEightAndSixteenAgents()
    {
        SnowGlobeResilienceExperimentResult first = SnowGlobeResilienceExperiment.RunMatrix();
        SnowGlobeResilienceExperimentResult second = SnowGlobeResilienceExperiment.RunMatrix();
        Assert.Equal(10, first.Cells.Count);
        Assert.Equal(first.Cells.Select(cell => cell.Cell), second.Cells.Select(cell => cell.Cell));
        foreach ((SnowGlobeResilienceCellResult cell, SnowGlobeResilienceCellResult repeat) in first.Cells.Zip(second.Cells))
        {
            Assert.Contains(cell.Cell.AgentCount, new[] { 8, 16 });
            Assert.Equal(cell.World.StateDigest(), repeat.World.StateDigest());
            Assert.Equal(cell.World.EventDigest(), repeat.World.EventDigest());
            Assert.Equal(cell.World.StateDigest(), cell.ReplayStateDigest);
            Assert.Equal(cell.World.EventDigest(), cell.ReplayEventDigest);
            Assert.Null(cell.FirstDivergenceTick);
            Assert.Equal(1, cell.Metrics.PeakInFlightRequests);
            Assert.InRange(cell.Metrics.PeakQueuedRequests, 1, cell.Metrics.QueueCapacity);
            Assert.Equal(1000, cell.Metrics.DispatchCoveragePermille);
            Assert.All(cell.Metrics.TurnsByAgent.Values, turns => Assert.Equal(1, turns));
        }
    }

    [Fact]
    public void TimeoutMalformedAndQueueOverflow_DoNotPartiallyMutateFailedPrimaryActions()
    {
        foreach (SnowGlobeResilienceFixtureKind fixture in new[]
                 {
                     SnowGlobeResilienceFixtureKind.InferenceTimeout,
                     SnowGlobeResilienceFixtureKind.MalformedResponse,
                     SnowGlobeResilienceFixtureKind.AdapterCrash,
                     SnowGlobeResilienceFixtureKind.QueueSaturation
                 })
        {
            SnowGlobeResilienceCellResult cell = SnowGlobeResilienceExperiment.RunCell(new SnowGlobeResilienceCell(16, fixture));
            Assert.Equal(16, cell.World.Events.Count);
            Assert.Equal(cell.Metrics.PrimaryCompletedActions, cell.World.Events.Count(entry => entry.Action == SnowGlobeActionKind.GatherWood));
            Assert.Equal(cell.Metrics.TaskAttempts - cell.Metrics.PrimaryCompletedActions, cell.World.Events.Count(entry => entry.Action == SnowGlobeActionKind.Idle));
            Assert.Equal(cell.Metrics.RepairAttempts, cell.Metrics.RepairSuccesses);
            Assert.Equal(0, cell.Metrics.FallbackActions);
        }
    }

    [Fact]
    public void ConflictingClaims_UseOrdinalWinnerOneRepairThenFallbackWithoutStarvation()
    {
        SnowGlobeResilienceCellResult cell = SnowGlobeResilienceExperiment.RunCell(new SnowGlobeResilienceCell(16, SnowGlobeResilienceFixtureKind.ConflictingResourceClaims));
        Assert.Equal("agent-00", cell.World.Events.First().AgentId);
        Assert.Equal(SnowGlobeActionKind.GatherWood, cell.World.Events.First().Action);
        Assert.Equal(1, cell.Metrics.PrimaryCompletedActions);
        Assert.Equal(64, cell.Metrics.ProgressQuantity);
        Assert.Equal(15, cell.Metrics.RepairAttempts);
        Assert.Equal(0, cell.Metrics.RepairSuccesses);
        Assert.Equal(15, cell.Metrics.FallbackActions);
        Assert.Equal(30, cell.Metrics.RejectedAttempts);
        Assert.All(cell.World.Events.Skip(1), entry => Assert.Equal(SnowGlobeActionKind.Idle, entry.Action));
    }

    [Fact]
    public void Report_IsCanonicalAndFailsClosedOnIncoherentMetrics()
    {
        SnowGlobeResilienceExperimentResult experiment = SnowGlobeResilienceExperiment.RunMatrix();
        SnowGlobeResilienceExperimentReport first = SnowGlobeResilienceExperimentReportBuilder.Build(experiment);
        SnowGlobeResilienceExperimentReport second = SnowGlobeResilienceExperimentReportBuilder.Build(SnowGlobeResilienceExperiment.RunMatrix());
        Assert.Equal(first.CanonicalUtf8.ToArray(), second.CanonicalUtf8.ToArray());
        Assert.Equal("2B67C1A72F33B2E109BB45D391A189DE041DBAE4CC36FA3D68366E9C16408E6C", Convert.ToHexString(SHA256.HashData(first.CanonicalUtf8.Span)));
        using JsonDocument document = JsonDocument.Parse(first.CanonicalUtf8);
        Assert.Equal("snow_globe_resilience_experiment/v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal(10, document.RootElement.GetProperty("cells").GetArrayLength());
        SnowGlobeResilienceMetrics metrics = experiment.Cells[0].Metrics;
        SetMetric(metrics, nameof(SnowGlobeResilienceMetrics.PeakQueuedRequests), metrics.QueueCapacity + 1);
        Assert.Throws<InvalidOperationException>(() => SnowGlobeResilienceExperimentReportBuilder.Build(experiment));
    }

    [Fact]
    public void Report_RejectsReflectionFabricatedProgressBeforeCanonicalReportBytes()
    {
        SnowGlobeResilienceExperimentResult experiment = SnowGlobeResilienceExperiment.RunMatrix();
        SnowGlobeResilienceMetrics metrics = experiment.Cells[0].Metrics;
        SetMetric(metrics, nameof(SnowGlobeResilienceMetrics.ProgressQuantity), 999999);

        Assert.Throws<InvalidOperationException>(() => SnowGlobeResilienceExperimentReportBuilder.Build(experiment));
    }

    [Fact]
    public void Report_RejectsEveryReflectionMutatedSerializedMetricBeforeCanonicalReportBytes()
    {
        string[] metricProperties =
        {
            nameof(SnowGlobeResilienceMetrics.TaskAttempts),
            nameof(SnowGlobeResilienceMetrics.PrimaryCompletedActions),
            nameof(SnowGlobeResilienceMetrics.ProgressQuantity),
            nameof(SnowGlobeResilienceMetrics.RejectedAttempts),
            nameof(SnowGlobeResilienceMetrics.RepairAttempts),
            nameof(SnowGlobeResilienceMetrics.RepairSuccesses),
            nameof(SnowGlobeResilienceMetrics.FallbackActions),
            nameof(SnowGlobeResilienceMetrics.InferenceFailures),
            nameof(SnowGlobeResilienceMetrics.QueueSaturatedRequests),
            nameof(SnowGlobeResilienceMetrics.QueueCapacity),
            nameof(SnowGlobeResilienceMetrics.PeakQueuedRequests),
            nameof(SnowGlobeResilienceMetrics.PeakInFlightRequests),
            nameof(SnowGlobeResilienceMetrics.DispatchCoveragePermille)
        };

        for (int cellIndex = 0; cellIndex < SnowGlobeResilienceExperiment.CanonicalCells.Count; cellIndex++)
        {
            foreach (string propertyName in metricProperties)
            {
                SnowGlobeResilienceExperimentResult experiment = SnowGlobeResilienceExperiment.RunMatrix();
                SnowGlobeResilienceMetrics metrics = experiment.Cells[cellIndex].Metrics;
                int value = (int)typeof(SnowGlobeResilienceMetrics).GetProperty(propertyName)!.GetValue(metrics)!;
                SetMetric(metrics, propertyName, value + 1);

                Assert.Throws<InvalidOperationException>(() => SnowGlobeResilienceExperimentReportBuilder.Build(experiment));
            }
        }
    }

    [Fact]
    public void Report_RejectsFabricatedDispatchTurnsBeforeCanonicalReportBytes()
    {
        SnowGlobeResilienceExperimentResult experiment = SnowGlobeResilienceExperiment.RunMatrix();
        SnowGlobeResilienceMetrics metrics = experiment.Cells[0].Metrics;
        typeof(SnowGlobeResilienceMetrics).GetProperty(nameof(SnowGlobeResilienceMetrics.TurnsByAgent))!
            .SetValue(metrics, new Dictionary<string, int> { ["fabricated-agent"] = 1 });

        Assert.Throws<InvalidOperationException>(() => SnowGlobeResilienceExperimentReportBuilder.Build(experiment));
    }

    [Fact]
    public void Report_RejectsFabricatedDivergenceAndRepeatReplayEvidenceBeforeCanonicalReportBytes()
    {
        SnowGlobeResilienceExperimentResult experiment = SnowGlobeResilienceExperiment.RunMatrix();
        SnowGlobeResilienceCellResult first = experiment.Cells[0];

        AssertRejected(experiment, first with { FirstDivergenceTick = 0 });
        AssertRejected(experiment, first with { RepeatStateDigest = "fabricated-repeat" });
        AssertRejected(experiment, first with { RepeatEventDigest = "fabricated-repeat-event" });
        AssertRejected(experiment, first with { ReplayStateDigest = "fabricated-replay-state" });
        AssertRejected(experiment, first with { ReplayEventDigest = "fabricated-replay" });
    }

    private static void AssertRejected(SnowGlobeResilienceExperimentResult experiment, SnowGlobeResilienceCellResult replacement)
    {
        SnowGlobeResilienceExperimentResult incoherent = new(new[] { replacement }.Concat(experiment.Cells.Skip(1)).ToArray());
        Assert.Throws<InvalidOperationException>(() => SnowGlobeResilienceExperimentReportBuilder.Build(incoherent));
    }

    private static void SetMetric(SnowGlobeResilienceMetrics metrics, string propertyName, int value) =>
        typeof(SnowGlobeResilienceMetrics).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.SetValue(metrics, value);
}
