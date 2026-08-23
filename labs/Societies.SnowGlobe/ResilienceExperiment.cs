using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Offline fixture failure classes; no provider protocol or runtime boundary.</summary>
public enum SnowGlobeResilienceFixtureKind
{
    InferenceTimeout,
    MalformedResponse,
    AdapterCrash,
    QueueSaturation,
    ConflictingResourceClaims
}

public sealed record SnowGlobeResilienceCell(int AgentCount, SnowGlobeResilienceFixtureKind Fixture);

public sealed class SnowGlobeResilienceMetrics
{
    public int TaskAttempts { get; internal set; }
    public int PrimaryCompletedActions { get; internal set; }
    public int ProgressQuantity { get; internal set; }
    public int RejectedAttempts { get; internal set; }
    public int RepairAttempts { get; internal set; }
    public int RepairSuccesses { get; internal set; }
    public int FallbackActions { get; internal set; }
    public int InferenceFailures { get; internal set; }
    public int QueueSaturatedRequests { get; internal set; }
    public int QueueCapacity { get; internal set; }
    public int PeakQueuedRequests { get; internal set; }
    public int PeakInFlightRequests { get; internal set; }
    public int DispatchCoveragePermille { get; internal set; }
    public IReadOnlyDictionary<string, int> TurnsByAgent { get; internal set; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
    public int TaskCompletionPermille => TaskAttempts == 0 ? 0 : PrimaryCompletedActions * 1000 / TaskAttempts;
}

public sealed record SnowGlobeResilienceCellResult(
    SnowGlobeResilienceCell Cell, SnowGlobeWorld World, SnowGlobeResilienceMetrics Metrics,
    string RepeatStateDigest, string RepeatEventDigest, string ReplayStateDigest, string ReplayEventDigest,
    int? FirstDivergenceTick);

public sealed record SnowGlobeResilienceExperimentResult(IReadOnlyList<SnowGlobeResilienceCellResult> Cells);

/// <summary>
/// Bounded, file-I/O-free failure experiment. Every cell freezes its shared snapshot before
/// ordinal commit. A failed primary gets one repair; a rejected repair gets one Idle fallback.
/// </summary>
public static class SnowGlobeResilienceExperiment
{
    public const int MaximumAgents = 16;
    public const int QueueCapacity = 4;
    public static readonly IReadOnlyList<int> AgentCounts = Array.AsReadOnly(new[] { 8, 16 });
    public static readonly IReadOnlyList<SnowGlobeResilienceFixtureKind> Fixtures = Array.AsReadOnly(new[]
    {
        SnowGlobeResilienceFixtureKind.InferenceTimeout,
        SnowGlobeResilienceFixtureKind.MalformedResponse,
        SnowGlobeResilienceFixtureKind.AdapterCrash,
        SnowGlobeResilienceFixtureKind.QueueSaturation,
        SnowGlobeResilienceFixtureKind.ConflictingResourceClaims
    });
    public static readonly IReadOnlyList<SnowGlobeResilienceCell> CanonicalCells = Array.AsReadOnly(
        AgentCounts.SelectMany(count => Fixtures.Select(fixture => new SnowGlobeResilienceCell(count, fixture))).ToArray());

    public static SnowGlobeResilienceExperimentResult RunMatrix() => new(CanonicalCells.Select(RunCell).ToArray());

    public static SnowGlobeResilienceCellResult RunCell(SnowGlobeResilienceCell cell)
    {
        ValidateCell(cell);
        SnowGlobeWorld world = RunOnce(cell, out SnowGlobeResilienceMetrics metrics);
        SnowGlobeWorld repeated = RunOnce(cell, out SnowGlobeResilienceMetrics repeatedMetrics);
        SnowGlobeWorld replay = Replay(cell, world.Events);
        if (world.StateDigest() != repeated.StateDigest() || world.EventDigest() != repeated.EventDigest()
            || world.StateDigest() != replay.StateDigest() || world.EventDigest() != replay.EventDigest()
            || !MetricsEqual(metrics, repeatedMetrics))
            throw new InvalidOperationException("Resilience cell diverged during repeat or replay.");
        return new(cell, world, metrics, repeated.StateDigest(), repeated.EventDigest(), replay.StateDigest(), replay.EventDigest(), null);
    }

    public static SnowGlobeWorld Replay(SnowGlobeResilienceCell cell, IEnumerable<SnowGlobeEvent> eventLog)
    {
        ValidateCell(cell);
        SnowGlobeWorld replay = SnowGlobeWorld.Create(FixedSeed(cell), cell.AgentCount);
        foreach (SnowGlobeEvent entry in eventLog.OrderBy(entry => entry.Sequence)) replay.Replay(entry);
        replay.AdvanceTick();
        return replay;
    }

    private static SnowGlobeWorld RunOnce(SnowGlobeResilienceCell cell, out SnowGlobeResilienceMetrics metrics)
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(FixedSeed(cell), cell.AgentCount);
        metrics = new SnowGlobeResilienceMetrics { QueueCapacity = cell.Fixture == SnowGlobeResilienceFixtureKind.QueueSaturation ? QueueCapacity : cell.AgentCount };
        Dictionary<string, int> turns = world.Agents.ToDictionary(agent => agent.AgentId, _ => 0, StringComparer.Ordinal);
        SnowGlobeObservation[] snapshot = world.Agents.OrderBy(agent => agent.AgentId, StringComparer.Ordinal).Select(agent => world.Observe(agent.AgentId)).ToArray();
        FrozenAttempt[] frozen = new FrozenAttempt[snapshot.Length];
        for (int index = 0; index < snapshot.Length; index++)
        {
            frozen[index] = CapturePrimary(cell, snapshot[index], metrics);
        }
        foreach (FrozenAttempt attempt in frozen.OrderBy(attempt => attempt.Observation.AgentId, StringComparer.Ordinal))
        {
            metrics.TaskAttempts++;
            turns[attempt.Observation.AgentId]++;
            if (attempt.FailureReason is null && attempt.Proposal is not null)
            {
                SnowGlobeCommitResult primary = world.ValidateAndCommit(attempt.Proposal);
                if (primary.Accepted)
                {
                    metrics.PrimaryCompletedActions++;
                    metrics.ProgressQuantity += ProgressQuantity(attempt.Proposal);
                    continue;
                }
                metrics.RejectedAttempts++;
            }
            else
            {
                metrics.RejectedAttempts++;
                metrics.InferenceFailures++;
            }

            metrics.RepairAttempts++;
            SnowGlobeCommitResult repair = world.ValidateAndCommit(RepairFor(cell, attempt.Observation, attempt.Proposal));
            if (repair.Accepted) metrics.RepairSuccesses++;
            else
            {
                metrics.RejectedAttempts++;
                SnowGlobeCommitResult fallback = world.ValidateAndCommit(new SnowGlobeActionProposal(attempt.Observation.AgentId, SnowGlobeActionKind.Idle));
                if (!fallback.Accepted) throw new InvalidOperationException("Deterministic fallback must be valid.");
                metrics.FallbackActions++;
            }
        }
        world.AdvanceTick();
        metrics.PeakInFlightRequests = 1;
        metrics.DispatchCoveragePermille = 1000;
        metrics.TurnsByAgent = new ReadOnlyDictionary<string, int>(turns);
        return world;
    }

    private static FrozenAttempt CapturePrimary(SnowGlobeResilienceCell cell, SnowGlobeObservation observation, SnowGlobeResilienceMetrics metrics)
    {
        if (cell.Fixture == SnowGlobeResilienceFixtureKind.QueueSaturation)
        {
            metrics.PeakQueuedRequests = Math.Min(cell.AgentCount, QueueCapacity);
            if (observation.HomeSlot >= QueueCapacity)
            {
                metrics.QueueSaturatedRequests++;
                return new(observation, null, "queue_saturated");
            }
        }
        else metrics.PeakQueuedRequests = Math.Max(metrics.PeakQueuedRequests, 1);
        if (cell.Fixture == SnowGlobeResilienceFixtureKind.InferenceTimeout && observation.HomeSlot == 0) return new(observation, null, "inference_timeout");
        if (cell.Fixture == SnowGlobeResilienceFixtureKind.MalformedResponse && observation.HomeSlot == 0) return new(observation, null, "response_malformed");
        if (cell.Fixture == SnowGlobeResilienceFixtureKind.AdapterCrash && observation.HomeSlot == 0) return new(observation, null, "adapter_crash");
        int quantity = cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims ? 64 : 1;
        return new(observation, new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.GatherWood, quantity), null);
    }

    private static SnowGlobeActionProposal RepairFor(SnowGlobeResilienceCell cell, SnowGlobeObservation observation, SnowGlobeActionProposal? primary) =>
        cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims && primary is not null
            ? primary : new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle);
    private static int ProgressQuantity(SnowGlobeActionProposal proposal) => proposal.Action == SnowGlobeActionKind.GatherWood ? proposal.Quantity : 0;
    private static int FixedSeed(SnowGlobeResilienceCell cell) => SnowGlobeScenario.FixedSeed + cell.AgentCount * 10 + (int)cell.Fixture;
    private static void ValidateCell(SnowGlobeResilienceCell cell)
    {
        if (!AgentCounts.Contains(cell.AgentCount) || !Fixtures.Contains(cell.Fixture)) throw new ArgumentOutOfRangeException(nameof(cell));
    }
    private static bool MetricsEqual(SnowGlobeResilienceMetrics left, SnowGlobeResilienceMetrics right) =>
        left.TaskAttempts == right.TaskAttempts && left.PrimaryCompletedActions == right.PrimaryCompletedActions && left.ProgressQuantity == right.ProgressQuantity
        && left.RejectedAttempts == right.RejectedAttempts && left.RepairAttempts == right.RepairAttempts && left.RepairSuccesses == right.RepairSuccesses
        && left.FallbackActions == right.FallbackActions && left.InferenceFailures == right.InferenceFailures && left.QueueSaturatedRequests == right.QueueSaturatedRequests
        && left.QueueCapacity == right.QueueCapacity && left.PeakQueuedRequests == right.PeakQueuedRequests && left.PeakInFlightRequests == right.PeakInFlightRequests
        && left.DispatchCoveragePermille == right.DispatchCoveragePermille && left.TurnsByAgent.SequenceEqual(right.TurnsByAgent);
    private sealed record FrozenAttempt(SnowGlobeObservation Observation, SnowGlobeActionProposal? Proposal, string? FailureReason);
}

public sealed class SnowGlobeResilienceExperimentReport
{
    internal SnowGlobeResilienceExperimentReport(byte[] canonicalUtf8)
    {
        _canonicalUtf8 = canonicalUtf8;
        CanonicalJson = Encoding.UTF8.GetString(canonicalUtf8);
    }
    private readonly byte[] _canonicalUtf8;
    public string CanonicalJson { get; }
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8;
}

public static class SnowGlobeResilienceExperimentReportBuilder
{
    public const string SchemaVersion = "snow_globe_resilience_experiment/v1";
    public const string ConflictPolicy = "ordinal_agent_id_first_valid_claim_then_one_repair_then_idle_fallback";
    public static SnowGlobeResilienceExperimentReport Build(SnowGlobeResilienceExperimentResult experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        Validate(experiment);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("timing_semantics", SnowGlobeSchedulingEvaluationReportBuilder.TimingSemantics);
            writer.WriteString("conflict_policy", ConflictPolicy);
            writer.WriteNumber("maximum_agents", SnowGlobeResilienceExperiment.MaximumAgents);
            writer.WriteNumber("queue_capacity", SnowGlobeResilienceExperiment.QueueCapacity);
            writer.WriteStartArray("cells");
            foreach (SnowGlobeResilienceCellResult cell in experiment.Cells) WriteCell(writer, cell);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return new(buffer.WrittenSpan.ToArray());
    }
    private static void WriteCell(Utf8JsonWriter writer, SnowGlobeResilienceCellResult cell)
    {
        SnowGlobeResilienceMetrics metrics = cell.Metrics;
        writer.WriteStartObject();
        writer.WriteNumber("agent_count", cell.Cell.AgentCount);
        writer.WriteString("fixture", FixtureName(cell.Cell.Fixture));
        writer.WriteString("state_digest", cell.World.StateDigest());
        writer.WriteString("event_digest", cell.World.EventDigest());
        writer.WriteNumber("task_attempts", metrics.TaskAttempts);
        writer.WriteNumber("primary_completed_actions", metrics.PrimaryCompletedActions);
        writer.WriteNumber("task_completion_permille", metrics.TaskCompletionPermille);
        writer.WriteNumber("progress_quantity", metrics.ProgressQuantity);
        writer.WriteNumber("rejected_attempts", metrics.RejectedAttempts);
        writer.WriteNumber("repair_attempts", metrics.RepairAttempts);
        writer.WriteNumber("repair_successes", metrics.RepairSuccesses);
        writer.WriteNumber("fallback_actions", metrics.FallbackActions);
        writer.WriteNumber("inference_failures", metrics.InferenceFailures);
        writer.WriteNumber("queue_saturated_requests", metrics.QueueSaturatedRequests);
        writer.WriteNumber("queue_capacity", metrics.QueueCapacity);
        writer.WriteNumber("peak_queued_requests", metrics.PeakQueuedRequests);
        writer.WriteNumber("peak_in_flight_requests", metrics.PeakInFlightRequests);
        writer.WriteNumber("dispatch_coverage_permille", metrics.DispatchCoveragePermille);
        writer.WriteNull("first_divergence_tick");
        writer.WriteBoolean("repeat_equivalent", true);
        writer.WriteBoolean("replay_equivalent", true);
        writer.WriteEndObject();
    }
    private static void Validate(SnowGlobeResilienceExperimentResult experiment)
    {
        if (!experiment.Cells.Select(cell => cell.Cell).SequenceEqual(SnowGlobeResilienceExperiment.CanonicalCells))
            throw new InvalidOperationException("Resilience experiment cells must be the complete canonical matrix in order.");
        foreach (SnowGlobeResilienceCellResult cell in experiment.Cells)
        {
            CanonicalResilienceEvidence evidence = DeriveCanonicalEvidence(cell.Cell, cell.World.Events);
            SnowGlobeWorld replay = SnowGlobeResilienceExperiment.Replay(cell.Cell, cell.World.Events);
            SnowGlobeResilienceCellResult repeated = SnowGlobeResilienceExperiment.RunCell(cell.Cell);
            if (cell.World.Agents.Count != cell.Cell.AgentCount || cell.World.Tick != 1
                || cell.World.StateDigest() != replay.StateDigest() || cell.World.EventDigest() != replay.EventDigest()
                || cell.FirstDivergenceTick != repeated.FirstDivergenceTick
                || cell.RepeatStateDigest != repeated.RepeatStateDigest || cell.RepeatEventDigest != repeated.RepeatEventDigest
                || cell.ReplayStateDigest != repeated.ReplayStateDigest || cell.ReplayEventDigest != repeated.ReplayEventDigest
                || !MetricsMatchCanonicalEvidence(cell.Metrics, evidence))
                throw new InvalidOperationException("Resilience experiment data is incoherent.");
        }
    }

    private static CanonicalResilienceEvidence DeriveCanonicalEvidence(SnowGlobeResilienceCell cell, IReadOnlyList<SnowGlobeEvent> events)
    {
        int failures = CellFailureCount(cell);
        int repairAttempts = cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims ? cell.AgentCount - 1 : failures;
        int repairSuccesses = cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims ? 0 : repairAttempts;
        int fallbackActions = repairAttempts - repairSuccesses;
        int rejectedAttempts = cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims ? repairAttempts * 2 : failures;
        int queueSaturatedRequests = cell.Fixture == SnowGlobeResilienceFixtureKind.QueueSaturation ? failures : 0;
        int expectedQueueCapacity = cell.Fixture == SnowGlobeResilienceFixtureKind.QueueSaturation
            ? SnowGlobeResilienceExperiment.QueueCapacity
            : cell.AgentCount;
        if (events.Count != cell.AgentCount || !events.Select(entry => entry.Sequence).SequenceEqual(Enumerable.Range(0, cell.AgentCount)))
            throw new InvalidOperationException("Resilience event trace is not contiguous.");

        for (int index = 0; index < cell.AgentCount; index++)
        {
            bool gathers = IsPrimaryGather(cell, index);
            SnowGlobeEvent entry = events[index];
            if (entry.Tick != 0 || entry.AgentId != $"agent-{index:D2}"
                || entry.Action != (gathers ? SnowGlobeActionKind.GatherWood : SnowGlobeActionKind.Idle)
                || entry.Quantity != (gathers ? PrimaryQuantity(cell) : 0) || entry.StructureId is not null)
                throw new InvalidOperationException("Resilience event trace does not match its fixture.");
        }

        int primaryCompletedActions = events.Count(entry => entry.Action == SnowGlobeActionKind.GatherWood);
        int progressQuantity = events.Where(entry => entry.Action == SnowGlobeActionKind.GatherWood).Sum(entry => entry.Quantity);
        return new CanonicalResilienceEvidence(
            cell.AgentCount, primaryCompletedActions, progressQuantity, rejectedAttempts, repairAttempts, repairSuccesses,
            fallbackActions, failures, queueSaturatedRequests, expectedQueueCapacity,
            cell.Fixture == SnowGlobeResilienceFixtureKind.QueueSaturation ? SnowGlobeResilienceExperiment.QueueCapacity : 1);
    }

    private static bool MetricsMatchCanonicalEvidence(SnowGlobeResilienceMetrics metrics, CanonicalResilienceEvidence evidence) =>
        metrics.TaskAttempts == evidence.TaskAttempts
        && metrics.PrimaryCompletedActions == evidence.PrimaryCompletedActions
        && metrics.TaskCompletionPermille == evidence.PrimaryCompletedActions * 1000 / evidence.TaskAttempts
        && metrics.ProgressQuantity == evidence.ProgressQuantity
        && metrics.RejectedAttempts == evidence.RejectedAttempts
        && metrics.RepairAttempts == evidence.RepairAttempts
        && metrics.RepairSuccesses == evidence.RepairSuccesses
        && metrics.FallbackActions == evidence.FallbackActions
        && metrics.InferenceFailures == evidence.InferenceFailures
        && metrics.QueueSaturatedRequests == evidence.QueueSaturatedRequests
        && metrics.QueueCapacity == evidence.QueueCapacity
        && metrics.PeakQueuedRequests == evidence.PeakQueuedRequests
        && metrics.PeakInFlightRequests == 1
        && metrics.DispatchCoveragePermille == 1000
        && metrics.TurnsByAgent.Count == evidence.TaskAttempts
        && Enumerable.Range(0, evidence.TaskAttempts).All(index =>
            metrics.TurnsByAgent.TryGetValue($"agent-{index:D2}", out int turns) && turns == 1);

    private static int CellFailureCount(SnowGlobeResilienceCell cell) => cell.Fixture switch
    {
        SnowGlobeResilienceFixtureKind.InferenceTimeout or SnowGlobeResilienceFixtureKind.MalformedResponse or SnowGlobeResilienceFixtureKind.AdapterCrash => 1,
        SnowGlobeResilienceFixtureKind.QueueSaturation => cell.AgentCount - SnowGlobeResilienceExperiment.QueueCapacity,
        SnowGlobeResilienceFixtureKind.ConflictingResourceClaims => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(cell))
    };

    private static bool IsPrimaryGather(SnowGlobeResilienceCell cell, int agentIndex) => cell.Fixture switch
    {
        SnowGlobeResilienceFixtureKind.InferenceTimeout or SnowGlobeResilienceFixtureKind.MalformedResponse or SnowGlobeResilienceFixtureKind.AdapterCrash => agentIndex != 0,
        SnowGlobeResilienceFixtureKind.QueueSaturation => agentIndex < SnowGlobeResilienceExperiment.QueueCapacity,
        SnowGlobeResilienceFixtureKind.ConflictingResourceClaims => agentIndex == 0,
        _ => throw new ArgumentOutOfRangeException(nameof(cell))
    };

    private static int PrimaryQuantity(SnowGlobeResilienceCell cell) =>
        cell.Fixture == SnowGlobeResilienceFixtureKind.ConflictingResourceClaims ? 64 : 1;

    private sealed record CanonicalResilienceEvidence(
        int TaskAttempts, int PrimaryCompletedActions, int ProgressQuantity, int RejectedAttempts, int RepairAttempts,
        int RepairSuccesses, int FallbackActions, int InferenceFailures, int QueueSaturatedRequests, int QueueCapacity,
        int PeakQueuedRequests);
    private static string FixtureName(SnowGlobeResilienceFixtureKind fixture) => fixture switch
    {
        SnowGlobeResilienceFixtureKind.InferenceTimeout => "inference_timeout",
        SnowGlobeResilienceFixtureKind.MalformedResponse => "malformed_response",
        SnowGlobeResilienceFixtureKind.AdapterCrash => "adapter_crash",
        SnowGlobeResilienceFixtureKind.QueueSaturation => "queue_saturation",
        SnowGlobeResilienceFixtureKind.ConflictingResourceClaims => "conflicting_resource_claims",
        _ => throw new ArgumentOutOfRangeException(nameof(fixture))
    };
}
