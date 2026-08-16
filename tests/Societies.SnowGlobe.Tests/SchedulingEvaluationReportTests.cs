using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class SchedulingEvaluationReportTests
{
    [Fact]
    public async Task FixedComparison_RepeatedRunsProduceByteIdenticalCanonicalReport()
    {
        SnowGlobeEvaluationReport first = SnowGlobeSchedulingEvaluationReportBuilder.Build(
            await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync());
        SnowGlobeEvaluationReport second = SnowGlobeSchedulingEvaluationReportBuilder.Build(
            await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync());

        Assert.Equal(first.CanonicalUtf8.ToArray(), second.CanonicalUtf8.ToArray());
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.DoesNotContain("timestamp", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duration", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("machine", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", first.CanonicalJson);
    }

    [Fact]
    public async Task FixedComparison_ReportHasRequiredVersionedFieldsInStableCanonicalOrder()
    {
        SnowGlobeEvaluationReport report = SnowGlobeSchedulingEvaluationReportBuilder.Build(
            await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync());
        using JsonDocument document = JsonDocument.Parse(report.CanonicalUtf8);
        JsonElement root = document.RootElement;

        Assert.Equal(new[] { "schema_version", "scenario", "timing_semantics", "equivalence", "modes" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("snow_globe_scheduling_evaluation/v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("logical_not_wall_clock", root.GetProperty("timing_semantics").GetString());
        JsonElement scenario = root.GetProperty("scenario");
        Assert.Equal(new[] { "id", "seed", "agent_count", "ticks" }, scenario.EnumerateObject().Select(property => property.Name));
        Assert.Equal(SnowGlobeScenario.FixedSeed, scenario.GetProperty("seed").GetInt32());
        Assert.Equal(8, scenario.GetProperty("agent_count").GetInt32());
        Assert.Equal(SnowGlobeScenario.FixedTicks, scenario.GetProperty("ticks").GetInt32());
        Assert.All(root.GetProperty("equivalence").EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));
        Assert.Equal(new[] { "shared_snapshot_sequential", "controlled_parallel" }, root.GetProperty("modes").EnumerateArray().Select(mode => mode.GetProperty("mode").GetString()));
    }

    [Fact]
    public async Task FixedComparison_ReportCarriesExactDigestsMetricsAndEquivalenceVerdicts()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        SnowGlobeEvaluationReport report = SnowGlobeSchedulingEvaluationReportBuilder.Build(comparison);
        using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetBytes(report.CanonicalJson));
        JsonElement[] modes = document.RootElement.GetProperty("modes").EnumerateArray().ToArray();

        AssertMode(modes[0], "shared_snapshot_sequential", comparison.SharedSnapshotSequential);
        AssertMode(modes[1], "controlled_parallel", comparison.ControlledParallel);
        Assert.True(document.RootElement.GetProperty("equivalence").GetProperty("state").GetBoolean());
        Assert.True(document.RootElement.GetProperty("equivalence").GetProperty("event").GetBoolean());
        Assert.True(document.RootElement.GetProperty("equivalence").GetProperty("replay").GetBoolean());
    }

    [Fact]
    public async Task FixedV1Report_MatchesIndependentCanonicalGoldenContract()
    {
        SnowGlobeEvaluationReport report = SnowGlobeSchedulingEvaluationReportBuilder.Build(
            await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync());
        byte[] utf8 = report.CanonicalUtf8.ToArray();

        Assert.False(utf8.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.NotEqual((byte)'\n', utf8[^1]);
        Assert.NotEqual((byte)'\r', utf8[^1]);
        Assert.Equal("20bde9bd80da960f27ebb892576924a604128d01f2659bbb50cfded53a64103c",
            Convert.ToHexString(SHA256.HashData(utf8)).ToLowerInvariant());

        using JsonDocument document = JsonDocument.Parse(utf8);
        JsonElement root = document.RootElement;
        Assert.Equal(new[] { "schema_version", "scenario", "timing_semantics", "equivalence", "modes" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("snow_globe_scheduling_evaluation/v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("logical_not_wall_clock", root.GetProperty("timing_semantics").GetString());
        JsonElement scenario = root.GetProperty("scenario");
        Assert.Equal(new[] { "id", "seed", "agent_count", "ticks" }, scenario.EnumerateObject().Select(property => property.Name));
        Assert.Equal("fixed_seed_eight_agent_scheduling_comparison", scenario.GetProperty("id").GetString());
        Assert.Equal(240815, scenario.GetProperty("seed").GetInt32());
        Assert.Equal(8, scenario.GetProperty("agent_count").GetInt32());
        Assert.Equal(4, scenario.GetProperty("ticks").GetInt32());
        Assert.Equal(new[] { "state", "event", "replay" }, root.GetProperty("equivalence").EnumerateObject().Select(property => property.Name));
        Assert.All(root.GetProperty("equivalence").EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));

        JsonElement[] modes = root.GetProperty("modes").EnumerateArray().ToArray();
        Assert.Equal(2, modes.Length);
        AssertGoldenMode(modes[0], "shared_snapshot_sequential", 60, 60, 533);
        AssertGoldenMode(modes[1], "controlled_parallel", 60, 12, 2666);
    }

    [Fact]
    public async Task NonEquivalentComparison_IsRejectedBeforeReportBytesAreProduced()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        SnowGlobeRecordedResponse[] alteredFixture = SnowGlobeSchedulingComparisonScenario.CreateFixedSeedFixture()
            .Select(response => response.ExpectedObservation is { Tick: 0, AgentId: "agent-00" }
                ? response with { Proposal = new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle) }
                : response)
            .ToArray();
        SnowGlobeWorld alteredWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeComparisonRunResult alteredParallel = await new SharedSnapshotInferenceScheduler(
            new RecordedInferenceAdapter(alteredFixture), SnowGlobeSchedulingMode.ControlledParallel)
            .RunAsync(alteredWorld, SnowGlobeScenario.FixedTicks);
        SnowGlobeSchedulingComparisonResult nonEquivalent = comparison with
        {
            ControlledParallelWorld = alteredWorld,
            ControlledParallel = alteredParallel
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SnowGlobeSchedulingEvaluationReportBuilder.Build(nonEquivalent));

        Assert.Equal("Cannot report a non-equivalent scheduling comparison.", exception.Message);
    }

    [Fact]
    public async Task AdvancedWorld_IsRejectedBeforeReportBytesAreProduced()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        comparison.ControlledParallelWorld.AdvanceTick();

        AssertFixedScenarioRejected(comparison);
    }

    [Fact]
    public async Task SwappedModeResults_AreRejectedBeforeReportBytesAreProduced()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        SnowGlobeSchedulingComparisonResult swapped = comparison with
        {
            SharedSnapshotSequential = comparison.ControlledParallel,
            ControlledParallel = comparison.SharedSnapshotSequential
        };

        AssertFixedScenarioRejected(swapped);
    }

    [Fact]
    public async Task MismatchedWorldDigest_IsRejectedBeforeReportBytesAreProduced()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        SnowGlobeSchedulingComparisonResult mismatched = comparison with
        {
            SharedSnapshotSequential = comparison.SharedSnapshotSequential with { EventDigest = "not-the-world-event-digest" }
        };

        AssertFixedScenarioRejected(mismatched);
    }

    private static void AssertMode(JsonElement mode, string expectedMode, SnowGlobeComparisonRunResult result)
    {
        SnowGlobeComparisonMetrics metrics = result.Metrics;
        Assert.Equal(new[]
        {
            "mode", "state_digest", "event_digest", "accepted_actions", "rejected_actions", "logical_latency_units",
            "critical_path_latency_units", "throughput_milli_actions_per_logical_latency_unit", "dispatch_coverage_permille"
        }, mode.EnumerateObject().Select(property => property.Name));
        Assert.Equal(expectedMode, mode.GetProperty("mode").GetString());
        Assert.Equal(result.StateDigest, mode.GetProperty("state_digest").GetString());
        Assert.Equal(result.EventDigest, mode.GetProperty("event_digest").GetString());
        Assert.Equal(metrics.AcceptedActions, mode.GetProperty("accepted_actions").GetInt32());
        Assert.Equal(metrics.RejectedActions, mode.GetProperty("rejected_actions").GetInt32());
        Assert.Equal(metrics.TotalRecordedLatencyUnits, mode.GetProperty("logical_latency_units").GetInt32());
        Assert.Equal(metrics.CriticalPathLatencyUnits, mode.GetProperty("critical_path_latency_units").GetInt32());
        Assert.Equal(metrics.ThroughputMilliActionsPerLatencyUnit, mode.GetProperty("throughput_milli_actions_per_logical_latency_unit").GetInt32());
        Assert.Equal(metrics.DispatchCoveragePermille, mode.GetProperty("dispatch_coverage_permille").GetInt32());
    }

    private static void AssertGoldenMode(JsonElement mode, string expectedMode, int expectedLatency, int expectedCriticalPath, int expectedThroughput)
    {
        Assert.Equal(new[]
        {
            "mode", "state_digest", "event_digest", "accepted_actions", "rejected_actions", "logical_latency_units",
            "critical_path_latency_units", "throughput_milli_actions_per_logical_latency_unit", "dispatch_coverage_permille"
        }, mode.EnumerateObject().Select(property => property.Name));
        Assert.Equal(expectedMode, mode.GetProperty("mode").GetString());
        Assert.Equal("0d6e988a09e35f40a8324bd949352db4660d601fe30588c5c1bc9bad3f037a61", mode.GetProperty("state_digest").GetString());
        Assert.Equal("de47366cab297821789144de67a10eaa524b82ae8276e7e2f2b30271c68e509c", mode.GetProperty("event_digest").GetString());
        Assert.Equal(32, mode.GetProperty("accepted_actions").GetInt32());
        Assert.Equal(0, mode.GetProperty("rejected_actions").GetInt32());
        Assert.Equal(expectedLatency, mode.GetProperty("logical_latency_units").GetInt32());
        Assert.Equal(expectedCriticalPath, mode.GetProperty("critical_path_latency_units").GetInt32());
        Assert.Equal(expectedThroughput, mode.GetProperty("throughput_milli_actions_per_logical_latency_unit").GetInt32());
        Assert.Equal(1000, mode.GetProperty("dispatch_coverage_permille").GetInt32());
    }

    private static void AssertFixedScenarioRejected(SnowGlobeSchedulingComparisonResult comparison)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SnowGlobeSchedulingEvaluationReportBuilder.Build(comparison));
        Assert.Equal("The canonical evaluation report only supports the fixed scheduling scenario.", exception.Message);
    }
}
