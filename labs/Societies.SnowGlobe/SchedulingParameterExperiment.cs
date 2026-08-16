using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public enum SnowGlobePlanningCadence
{
    EveryTick = 1,
    EveryOtherTick = 2
}

public sealed record SnowGlobeParameterExperimentCell(
    int AgentCount,
    SnowGlobePlanningCadence Cadence);

public sealed record SnowGlobeParameterExperimentCellResult(
    SnowGlobeParameterExperimentCell Cell,
    int WorldTicks,
    SnowGlobeWorld SharedSnapshotSequentialWorld,
    SnowGlobeComparisonRunResult SharedSnapshotSequential,
    int SequentialPeakInFlightRequests,
    SnowGlobeWorld ControlledParallelWorld,
    SnowGlobeComparisonRunResult ControlledParallel,
    int ControlledParallelPeakInFlightRequests);

public sealed record SnowGlobeParameterExperimentResult(
    IReadOnlyList<SnowGlobeParameterExperimentCellResult> Cells);

/// <summary>
/// Reversible, mock-only parameter experiment. It reuses the existing scheduler for each frozen
/// deliberation round; a slower cadence advances deterministic idle world ticks between rounds.
/// </summary>
public static class SnowGlobeSchedulingParameterExperiment
{
    public const int PlanningRounds = 4;
    public const int MaximumInFlightRequests = 16;
    public static readonly IReadOnlyList<int> AgentCounts = Array.AsReadOnly(new[] { 4, 8, 16 });
    public static readonly IReadOnlyList<SnowGlobePlanningCadence> Cadences =
        Array.AsReadOnly(new[] { SnowGlobePlanningCadence.EveryTick, SnowGlobePlanningCadence.EveryOtherTick });
    public static readonly IReadOnlyList<SnowGlobeParameterExperimentCell> CanonicalCells = Array.AsReadOnly(
        AgentCounts.SelectMany(agentCount => Cadences.Select(cadence => new SnowGlobeParameterExperimentCell(agentCount, cadence))).ToArray());

    public static async Task<SnowGlobeParameterExperimentResult> RunMatrixAsync(CancellationToken cancellationToken = default)
    {
        List<SnowGlobeParameterExperimentCellResult> cells = new();
        foreach (SnowGlobeParameterExperimentCell cell in CanonicalCells)
        {
            cells.Add(await RunCellAsync(cell, cancellationToken));
        }

        return new SnowGlobeParameterExperimentResult(cells.AsReadOnly());
    }

    public static async Task<SnowGlobeParameterExperimentCellResult> RunCellAsync(
        SnowGlobeParameterExperimentCell cell,
        CancellationToken cancellationToken = default)
    {
        ValidateCell(cell);
        IReadOnlyList<SnowGlobeRecordedResponse> fixture = CreateFixture(cell);
        int worldTicks = PlanningRounds * (int)cell.Cadence;
        SnowGlobeWorld sequentialWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, cell.AgentCount);
        (SnowGlobeComparisonRunResult sequential, int sequentialPeak) = await RunModeAsync(
            sequentialWorld, fixture, cell, SnowGlobeSchedulingMode.SharedSnapshotSequential, cancellationToken);
        SnowGlobeWorld parallelWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, cell.AgentCount);
        (SnowGlobeComparisonRunResult parallel, int parallelPeak) = await RunModeAsync(
            parallelWorld, fixture, cell, SnowGlobeSchedulingMode.ControlledParallel, cancellationToken);
        return new SnowGlobeParameterExperimentCellResult(cell, worldTicks, sequentialWorld, sequential, sequentialPeak, parallelWorld, parallel, parallelPeak);
    }

    public static SnowGlobeWorld Replay(SnowGlobeParameterExperimentCell cell, int worldTicks, IEnumerable<SnowGlobeEvent> eventLog)
    {
        ValidateCell(cell);
        if (worldTicks != PlanningRounds * (int)cell.Cadence)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTicks));
        }

        SnowGlobeWorld replayed = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, cell.AgentCount);
        foreach (SnowGlobeEvent entry in eventLog.OrderBy(item => item.Sequence))
        {
            replayed.Replay(entry);
        }

        while (replayed.Tick < worldTicks)
        {
            replayed.AdvanceTick();
        }

        return replayed;
    }

    public static IReadOnlyList<SnowGlobeRecordedResponse> CreateFixture(SnowGlobeParameterExperimentCell cell)
    {
        ValidateCell(cell);
        SnowGlobeWorld fixtureWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, cell.AgentCount);
        List<SnowGlobeRecordedResponse> responses = new(cell.AgentCount * PlanningRounds);
        for (int planningRound = 0; planningRound < PlanningRounds; planningRound++)
        {
            SnowGlobeObservation[] snapshot = fixtureWorld.Agents.Select(agent => agent.AgentId)
                .OrderBy(agentId => agentId, StringComparer.Ordinal).Select(fixtureWorld.Observe).ToArray();
            foreach (SnowGlobeObservation observation in snapshot)
            {
                responses.Add(new SnowGlobeRecordedResponse(
                    observation,
                    CreatePlannedProposal(observation, planningRound),
                    1 + observation.HomeSlot % 3));
            }

            foreach (SnowGlobeObservation observation in snapshot)
            {
                fixtureWorld.ValidateAndCommit(CreatePlannedProposal(observation, planningRound));
            }

            AdvanceCadence(fixtureWorld, cell.Cadence);
        }

        return responses.AsReadOnly();
    }

    private static async Task<(SnowGlobeComparisonRunResult Result, int PeakInFlight)> RunModeAsync(
        SnowGlobeWorld world,
        IReadOnlyList<SnowGlobeRecordedResponse> fixture,
        SnowGlobeParameterExperimentCell cell,
        SnowGlobeSchedulingMode mode,
        CancellationToken cancellationToken)
    {
        InstrumentedRecordedInferenceAdapter adapter = new(new RecordedInferenceAdapter(fixture), cell.AgentCount, mode == SnowGlobeSchedulingMode.ControlledParallel);
        SnowGlobeComparisonMetrics metrics = new();
        Dictionary<string, int> turns = world.Agents.ToDictionary(agent => agent.AgentId, _ => 0, StringComparer.Ordinal);
        List<SnowGlobeRoundTrace> rounds = new();
        for (int planningRound = 0; planningRound < PlanningRounds; planningRound++)
        {
            SnowGlobeComparisonRunResult round = await new SharedSnapshotInferenceScheduler(adapter, mode)
                .RunAsync(world, ticks: 1, cancellationToken);
            Accumulate(metrics, turns, rounds, round);
            AdvanceCadence(world, cell.Cadence, alreadyAdvancedOnce: true);
        }

        metrics.Ticks = PlanningRounds * (int)cell.Cadence;
        metrics.SharedSnapshotRounds = PlanningRounds;
        metrics.ControlledParallelRounds = mode == SnowGlobeSchedulingMode.ControlledParallel ? PlanningRounds : 0;
        metrics.TotalRecordedLatencyUnits = fixture.Sum(response => response.LatencyUnits);
        metrics.CriticalPathLatencyUnits = mode == SnowGlobeSchedulingMode.SharedSnapshotSequential
            ? metrics.TotalRecordedLatencyUnits
            : fixture.GroupBy(response => response.ExpectedObservation.Tick).Sum(round => round.Max(response => response.LatencyUnits));
        metrics.DeliberationTurnsByAgent = new ReadOnlyDictionary<string, int>(turns);
        metrics.DispatchCoveragePermille = 1000;
        metrics.ThroughputMilliActionsPerLatencyUnit = metrics.CriticalPathLatencyUnits == 0
            ? 0
            : metrics.AcceptedActions * 1000 / metrics.CriticalPathLatencyUnits;
        return (new SnowGlobeComparisonRunResult(world.StateDigest(), world.EventDigest(), metrics, rounds.AsReadOnly()), adapter.PeakInFlightRequests);
    }

    private static void Accumulate(
        SnowGlobeComparisonMetrics target,
        Dictionary<string, int> turns,
        List<SnowGlobeRoundTrace> rounds,
        SnowGlobeComparisonRunResult round)
    {
        SnowGlobeComparisonMetrics source = round.Metrics;
        target.InferenceCalls += source.InferenceCalls;
        target.AcceptedActions += source.AcceptedActions;
        target.RejectedActions += source.RejectedActions;
        target.TotalRecordedLatencyUnits += source.TotalRecordedLatencyUnits;
        target.CriticalPathLatencyUnits += source.CriticalPathLatencyUnits;
        foreach ((string agentId, int count) in source.DeliberationTurnsByAgent)
        {
            turns[agentId] += count;
        }

        rounds.AddRange(round.Rounds.Select(trace => trace with { Tick = trace.Observations[0].Tick }));
    }

    private static void AdvanceCadence(SnowGlobeWorld world, SnowGlobePlanningCadence cadence, bool alreadyAdvancedOnce = false)
    {
        int remainingTicks = (int)cadence - (alreadyAdvancedOnce ? 1 : 0);
        for (int index = 0; index < remainingTicks; index++)
        {
            world.AdvanceTick();
        }
    }

    private static SnowGlobeActionProposal CreatePlannedProposal(SnowGlobeObservation observation, int planningRound)
    {
        SnowGlobeActionKind action = planningRound switch
        {
            0 => SnowGlobeActionKind.GatherWood,
            1 => SnowGlobeActionKind.GatherStone,
            2 when observation.AgentId == "agent-00" => SnowGlobeActionKind.BuildShelter,
            2 when observation.AgentId == "agent-01" => SnowGlobeActionKind.BuildStorage,
            3 when observation.AgentId == "agent-02" => SnowGlobeActionKind.MaintainShelter,
            _ => SnowGlobeActionKind.Idle
        };
        int quantity = action switch
        {
            SnowGlobeActionKind.GatherWood => 4,
            SnowGlobeActionKind.GatherStone => 2,
            SnowGlobeActionKind.MaintainShelter => 2,
            _ => 0
        };
        return new SnowGlobeActionProposal(observation.AgentId, action, quantity);
    }

    private static void ValidateCell(SnowGlobeParameterExperimentCell cell)
    {
        if (!AgentCounts.Contains(cell.AgentCount) || !Cadences.Contains(cell.Cadence))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }
    }

    /// <summary>
    /// A deterministic mock-only gate measures the active request cohort. Parallel rounds release
    /// only after all cell agents are pending; sequential requests complete immediately.
    /// </summary>
    private sealed class InstrumentedRecordedInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        private readonly RecordedInferenceAdapter _recorded;
        private readonly int _expectedCohort;
        private readonly bool _gateCohort;
        private readonly object _gate = new();
        private readonly List<PendingProposal> _pending = new();
        private int _inFlight;

        public InstrumentedRecordedInferenceAdapter(RecordedInferenceAdapter recorded, int expectedCohort, bool gateCohort)
        {
            _recorded = recorded;
            _expectedCohort = expectedCohort;
            _gateCohort = gateCohort;
        }

        public int PeakInFlightRequests { get; private set; }

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            SnowGlobeActionProposal proposal = _recorded.ProposeAsync(observation, cancellationToken).Result;
            if (!_gateCohort)
            {
                lock (_gate)
                {
                    PeakInFlightRequests = Math.Max(PeakInFlightRequests, ++_inFlight);
                    _inFlight--;
                }

                return ValueTask.FromResult(proposal);
            }

            TaskCompletionSource<SnowGlobeActionProposal> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<PendingProposal>? release = null;
            lock (_gate)
            {
                _pending.Add(new PendingProposal(completion, proposal));
                PeakInFlightRequests = Math.Max(PeakInFlightRequests, ++_inFlight);
                if (_pending.Count == _expectedCohort)
                {
                    release = new List<PendingProposal>(_pending);
                    _pending.Clear();
                    _inFlight = 0;
                }
            }

            if (release is not null)
            {
                foreach (PendingProposal pending in release)
                {
                    pending.Completion.TrySetResult(pending.Proposal);
                }
            }

            return new ValueTask<SnowGlobeActionProposal>(completion.Task);
        }

        private sealed record PendingProposal(TaskCompletionSource<SnowGlobeActionProposal> Completion, SnowGlobeActionProposal Proposal);
    }
}

public sealed class SnowGlobeParameterExperimentReport
{
    internal SnowGlobeParameterExperimentReport(byte[] canonicalUtf8)
    {
        _canonicalUtf8 = canonicalUtf8;
        CanonicalJson = Encoding.UTF8.GetString(canonicalUtf8);
    }

    private readonly byte[] _canonicalUtf8;
    public string CanonicalJson { get; }
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8;
}

public static class SnowGlobeParameterExperimentReportBuilder
{
    public const string SchemaVersion = "snow_globe_parameter_experiment/v1";

    public static SnowGlobeParameterExperimentReport Build(SnowGlobeParameterExperimentResult experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ValidateExperiment(experiment);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("timing_semantics", SnowGlobeSchedulingEvaluationReportBuilder.TimingSemantics);
            writer.WriteNumber("planning_rounds", SnowGlobeSchedulingParameterExperiment.PlanningRounds);
            writer.WriteStartArray("agent_counts");
            foreach (int count in SnowGlobeSchedulingParameterExperiment.AgentCounts) writer.WriteNumberValue(count);
            writer.WriteEndArray();
            writer.WriteStartArray("planning_cadences");
            foreach (SnowGlobePlanningCadence cadence in SnowGlobeSchedulingParameterExperiment.Cadences) writer.WriteStringValue(CadenceName(cadence));
            writer.WriteEndArray();
            writer.WriteNumber("maximum_in_flight_requests", SnowGlobeSchedulingParameterExperiment.MaximumInFlightRequests);
            writer.WriteStartArray("cells");
            foreach (SnowGlobeParameterExperimentCellResult cell in experiment.Cells)
            {
                WriteCell(writer, cell);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return new SnowGlobeParameterExperimentReport(buffer.WrittenSpan.ToArray());
    }

    private static void WriteCell(Utf8JsonWriter writer, SnowGlobeParameterExperimentCellResult cell)
    {
        writer.WriteStartObject();
        writer.WriteNumber("agent_count", cell.Cell.AgentCount);
        writer.WriteString("cadence", CadenceName(cell.Cell.Cadence));
        writer.WriteNumber("world_ticks", cell.WorldTicks);
        writer.WriteNumber("sequential_peak_in_flight_requests", cell.SequentialPeakInFlightRequests);
        writer.WriteNumber("controlled_parallel_peak_in_flight_requests", cell.ControlledParallelPeakInFlightRequests);
        writer.WriteStartObject("equivalence");
        writer.WriteBoolean("state", true);
        writer.WriteBoolean("event", true);
        writer.WriteBoolean("replay", true);
        writer.WriteEndObject();
        writer.WriteStartArray("modes");
        WriteMode(writer, "shared_snapshot_sequential", cell.SharedSnapshotSequential);
        WriteMode(writer, "controlled_parallel", cell.ControlledParallel);
        writer.WriteEndArray();
        writer.WriteEndObject();
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

    private static void ValidateExperiment(SnowGlobeParameterExperimentResult experiment)
    {
        if (!experiment.Cells.Select(cell => cell.Cell).SequenceEqual(SnowGlobeSchedulingParameterExperiment.CanonicalCells))
        {
            throw new InvalidOperationException("Parameter experiment cells must be the complete canonical matrix in order.");
        }

        foreach (SnowGlobeParameterExperimentCellResult cell in experiment.Cells)
        {
            int expectedTicks = SnowGlobeSchedulingParameterExperiment.PlanningRounds * (int)cell.Cell.Cadence;
            int expectedTotalLatency = ExpectedTotalLogicalLatency(cell.Cell.AgentCount);
            ValidateRun(cell.SharedSnapshotSequentialWorld, cell.SharedSnapshotSequential, cell.Cell, expectedTicks, expectedTotalLatency, false);
            ValidateRun(cell.ControlledParallelWorld, cell.ControlledParallel, cell.Cell, expectedTicks, expectedTotalLatency, true);
            SnowGlobeWorld sequentialReplay = SnowGlobeSchedulingParameterExperiment.Replay(cell.Cell, expectedTicks, cell.SharedSnapshotSequentialWorld.Events);
            SnowGlobeWorld parallelReplay = SnowGlobeSchedulingParameterExperiment.Replay(cell.Cell, expectedTicks, cell.ControlledParallelWorld.Events);
            if (cell.WorldTicks != expectedTicks
                || cell.SequentialPeakInFlightRequests != 1
                || cell.ControlledParallelPeakInFlightRequests != cell.Cell.AgentCount
                || cell.ControlledParallelPeakInFlightRequests > SnowGlobeSchedulingParameterExperiment.MaximumInFlightRequests
                || cell.SharedSnapshotSequential.StateDigest != cell.ControlledParallel.StateDigest
                || cell.SharedSnapshotSequential.EventDigest != cell.ControlledParallel.EventDigest
                || cell.SharedSnapshotSequential.Metrics.TotalRecordedLatencyUnits != cell.ControlledParallel.Metrics.TotalRecordedLatencyUnits
                || sequentialReplay.StateDigest() != cell.SharedSnapshotSequential.StateDigest
                || parallelReplay.StateDigest() != cell.ControlledParallel.StateDigest)
            {
                throw new InvalidOperationException("Parameter experiment comparison is not equivalent.");
            }
        }
    }

    private static void ValidateRun(SnowGlobeWorld world, SnowGlobeComparisonRunResult run, SnowGlobeParameterExperimentCell cell, int expectedTicks, int expectedTotalLatency, bool controlledParallel)
    {
        SnowGlobeComparisonMetrics metrics = run.Metrics;
        int expectedCalls = cell.AgentCount * SnowGlobeSchedulingParameterExperiment.PlanningRounds;
        if (world.Seed != SnowGlobeScenario.FixedSeed || world.Tick != expectedTicks || world.Agents.Count != cell.AgentCount
            || world.StateDigest() != run.StateDigest || world.EventDigest() != run.EventDigest
            || metrics.Ticks != expectedTicks || metrics.InferenceCalls != expectedCalls
            || metrics.AcceptedActions < 0 || metrics.RejectedActions < 0 || metrics.AcceptedActions + metrics.RejectedActions != expectedCalls
            || metrics.SharedSnapshotRounds != SnowGlobeSchedulingParameterExperiment.PlanningRounds
            || metrics.ControlledParallelRounds != (controlledParallel ? SnowGlobeSchedulingParameterExperiment.PlanningRounds : 0)
            || metrics.TotalRecordedLatencyUnits != expectedTotalLatency
            || metrics.CriticalPathLatencyUnits != (controlledParallel ? ExpectedParallelCriticalPathLatency(cell.AgentCount) : expectedTotalLatency)
            || metrics.ThroughputMilliActionsPerLatencyUnit != (metrics.CriticalPathLatencyUnits == 0 ? 0 : metrics.AcceptedActions * 1000 / metrics.CriticalPathLatencyUnits)
            || !HasExpectedDispatchCoverage(metrics, cell.AgentCount)
            || !HasExpectedRoundShape(run.Rounds, cell.AgentCount, cell.Cadence))
        {
            throw new InvalidOperationException("Parameter experiment run is incoherent.");
        }
    }

    private static int ExpectedTotalLogicalLatency(int agentCount) =>
        SnowGlobeSchedulingParameterExperiment.PlanningRounds * Enumerable.Range(0, agentCount).Sum(index => 1 + index % 3);

    private static int ExpectedParallelCriticalPathLatency(int agentCount) =>
        SnowGlobeSchedulingParameterExperiment.PlanningRounds * Enumerable.Range(0, agentCount).Max(index => 1 + index % 3);

    private static bool HasExpectedDispatchCoverage(SnowGlobeComparisonMetrics metrics, int agentCount)
    {
        return metrics.DeliberationTurnsByAgent.Count == agentCount
            && metrics.DeliberationTurnsByAgent.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key).SequenceEqual(Enumerable.Range(0, agentCount).Select(index => $"agent-{index:D2}"))
            && metrics.DeliberationTurnsByAgent.Values.All(turns => turns == SnowGlobeSchedulingParameterExperiment.PlanningRounds)
            && metrics.DispatchCoveragePermille == 1000;
    }

    private static bool HasExpectedRoundShape(IReadOnlyList<SnowGlobeRoundTrace> rounds, int agentCount, SnowGlobePlanningCadence cadence)
    {
        return rounds.Select((round, index) => round.Tick == index * (int)cadence
                && round.Observations.Count == agentCount
                && round.Proposals.Count == agentCount)
            .All(valid => valid);
    }

    private static string CadenceName(SnowGlobePlanningCadence cadence) => cadence switch
    {
        SnowGlobePlanningCadence.EveryTick => "every_tick",
        SnowGlobePlanningCadence.EveryOtherTick => "every_other_tick",
        _ => throw new ArgumentOutOfRangeException(nameof(cadence))
    };
}
