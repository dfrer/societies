using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>
/// Immutable, file-I/O-free canonical bytes for the fixed scheduling comparison. This is evidence
/// serialization only: it does not execute, schedule, persist, or otherwise alter the experiment.
/// </summary>
public sealed class SnowGlobeEvaluationReport
{
    internal SnowGlobeEvaluationReport(byte[] canonicalUtf8)
    {
        _canonicalUtf8 = canonicalUtf8;
        CanonicalJson = Encoding.UTF8.GetString(canonicalUtf8);
    }

    private readonly byte[] _canonicalUtf8;

    public string CanonicalJson { get; }

    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8;
}

public static class SnowGlobeSchedulingEvaluationReportBuilder
{
    public const string SchemaVersion = "snow_globe_scheduling_evaluation/v1";
    public const string TimingSemantics = "logical_not_wall_clock";

    public static SnowGlobeEvaluationReport Build(SnowGlobeSchedulingComparisonResult comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ValidateFixedScenario(comparison);

        bool stateEquivalent = string.Equals(
            comparison.SharedSnapshotSequential.StateDigest,
            comparison.ControlledParallel.StateDigest,
            StringComparison.Ordinal);
        bool eventEquivalent = string.Equals(
            comparison.SharedSnapshotSequential.EventDigest,
            comparison.ControlledParallel.EventDigest,
            StringComparison.Ordinal);
        bool replayEquivalent = ReplaysMatch(comparison);
        if (!stateEquivalent || !eventEquivalent || !replayEquivalent)
        {
            throw new InvalidOperationException("Cannot report a non-equivalent scheduling comparison.");
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteStartObject("scenario");
            writer.WriteString("id", "fixed_seed_eight_agent_scheduling_comparison");
            writer.WriteNumber("seed", SnowGlobeScenario.FixedSeed);
            writer.WriteNumber("agent_count", SnowGlobeScenario.FixedAgentCount);
            writer.WriteNumber("ticks", SnowGlobeScenario.FixedTicks);
            writer.WriteEndObject();
            writer.WriteString("timing_semantics", TimingSemantics);
            writer.WriteStartObject("equivalence");
            writer.WriteBoolean("state", stateEquivalent);
            writer.WriteBoolean("event", eventEquivalent);
            writer.WriteBoolean("replay", replayEquivalent);
            writer.WriteEndObject();
            writer.WriteStartArray("modes");
            WriteMode(writer, "shared_snapshot_sequential", comparison.SharedSnapshotSequential);
            WriteMode(writer, "controlled_parallel", comparison.ControlledParallel);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return new SnowGlobeEvaluationReport(buffer.WrittenSpan.ToArray());
    }

    private static void WriteMode(Utf8JsonWriter writer, string mode, SnowGlobeComparisonRunResult result)
    {
        SnowGlobeComparisonMetrics metrics = result.Metrics;
        writer.WriteStartObject();
        writer.WriteString("mode", mode);
        writer.WriteString("state_digest", result.StateDigest);
        writer.WriteString("event_digest", result.EventDigest);
        writer.WriteNumber("accepted_actions", metrics.AcceptedActions);
        writer.WriteNumber("rejected_actions", metrics.RejectedActions);
        writer.WriteNumber("logical_latency_units", metrics.TotalRecordedLatencyUnits);
        writer.WriteNumber("critical_path_latency_units", metrics.CriticalPathLatencyUnits);
        writer.WriteNumber("throughput_milli_actions_per_logical_latency_unit", metrics.ThroughputMilliActionsPerLatencyUnit);
        writer.WriteNumber("dispatch_coverage_permille", metrics.DispatchCoveragePermille);
        writer.WriteEndObject();
    }

    private static bool ReplaysMatch(SnowGlobeSchedulingComparisonResult comparison)
    {
        SnowGlobeWorld sequentialReplay = SnowGlobeScenario.ReplayFixedSeed(comparison.SharedSnapshotSequentialWorld.Events);
        SnowGlobeWorld parallelReplay = SnowGlobeScenario.ReplayFixedSeed(comparison.ControlledParallelWorld.Events);
        return string.Equals(sequentialReplay.StateDigest(), comparison.SharedSnapshotSequential.StateDigest, StringComparison.Ordinal)
            && string.Equals(sequentialReplay.EventDigest(), comparison.SharedSnapshotSequential.EventDigest, StringComparison.Ordinal)
            && string.Equals(parallelReplay.StateDigest(), comparison.ControlledParallel.StateDigest, StringComparison.Ordinal)
            && string.Equals(parallelReplay.EventDigest(), comparison.ControlledParallel.EventDigest, StringComparison.Ordinal);
    }

    private static void ValidateFixedScenario(SnowGlobeSchedulingComparisonResult comparison)
    {
        ValidateWorldAndRun(
            comparison.SharedSnapshotSequentialWorld,
            comparison.SharedSnapshotSequential,
            controlledParallel: false);
        ValidateWorldAndRun(
            comparison.ControlledParallelWorld,
            comparison.ControlledParallel,
            controlledParallel: true);
    }

    private static void ValidateWorldAndRun(
        SnowGlobeWorld world,
        SnowGlobeComparisonRunResult run,
        bool controlledParallel)
    {
        SnowGlobeComparisonMetrics metrics = run.Metrics;
        int expectedCalls = SnowGlobeScenario.FixedAgentCount * SnowGlobeScenario.FixedTicks;
        if (world.Seed != SnowGlobeScenario.FixedSeed
            || world.Tick != SnowGlobeScenario.FixedTicks
            || world.Agents.Count != SnowGlobeScenario.FixedAgentCount
            || !string.Equals(world.StateDigest(), run.StateDigest, StringComparison.Ordinal)
            || !string.Equals(world.EventDigest(), run.EventDigest, StringComparison.Ordinal)
            || metrics.Ticks != SnowGlobeScenario.FixedTicks
            || metrics.InferenceCalls != expectedCalls
            || metrics.AcceptedActions < 0
            || metrics.RejectedActions < 0
            || metrics.AcceptedActions + metrics.RejectedActions != metrics.InferenceCalls
            || metrics.SharedSnapshotRounds != SnowGlobeScenario.FixedTicks
            || metrics.ControlledParallelRounds != (controlledParallel ? SnowGlobeScenario.FixedTicks : 0)
            || metrics.TotalRecordedLatencyUnits < 0
            || metrics.CriticalPathLatencyUnits < 0
            || metrics.ThroughputMilliActionsPerLatencyUnit < 0
            || metrics.DispatchCoveragePermille < 0
            || metrics.DispatchCoveragePermille > 1000
            || run.Rounds.Count != SnowGlobeScenario.FixedTicks
            || !HasExpectedDispatchCoverage(metrics, expectedCalls)
            || !HasExpectedRoundShape(run.Rounds))
        {
            throw new InvalidOperationException("The canonical evaluation report only supports the fixed scheduling scenario.");
        }
    }

    private static bool HasExpectedDispatchCoverage(SnowGlobeComparisonMetrics metrics, int expectedCalls)
    {
        return metrics.DeliberationTurnsByAgent.Count == SnowGlobeScenario.FixedAgentCount
            && metrics.DeliberationTurnsByAgent.Values.All(turns => turns >= 0)
            && metrics.DeliberationTurnsByAgent.Values.Sum() == expectedCalls
            && metrics.DeliberationTurnsByAgent.Values.All(turns => turns == SnowGlobeScenario.FixedTicks)
            && metrics.DispatchCoveragePermille == 1000;
    }

    private static bool HasExpectedRoundShape(IReadOnlyList<SnowGlobeRoundTrace> rounds)
    {
        return rounds.Select((round, index) => round.Tick == index
                && round.Observations.Count == SnowGlobeScenario.FixedAgentCount
                && round.Proposals.Count == SnowGlobeScenario.FixedAgentCount)
            .All(valid => valid);
    }
}
