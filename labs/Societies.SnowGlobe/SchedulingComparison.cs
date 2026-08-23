using System.Collections.ObjectModel;

namespace Societies.SnowGlobe;

public enum SnowGlobeSchedulingMode
{
    SharedSnapshotSequential,
    ControlledParallel
}

/// <summary>
/// An offline fixture response. Latency is a logical recorded unit, never wall-clock timing,
/// so the scheduling comparison remains deterministic and replayable on every machine.
/// </summary>
public sealed record SnowGlobeRecordedResponse(
    SnowGlobeObservation ExpectedObservation,
    SnowGlobeActionProposal? Proposal,
    int LatencyUnits,
    string? FailureReason = null);

public sealed class SnowGlobeInferenceException : Exception
{
    public SnowGlobeInferenceException(string reason) : base(reason) => Reason = reason;

    public string Reason { get; }
}

/// <summary>
/// Provider-neutral adapter for recorded/mock responses. It accepts only an exact value observation
/// and cannot observe or mutate the world.
/// </summary>
public sealed class RecordedInferenceAdapter : ISnowGlobeInferenceAdapter
{
    private readonly IReadOnlyDictionary<(int Tick, string AgentId), SnowGlobeRecordedResponse> _responses;

    public RecordedInferenceAdapter(IEnumerable<SnowGlobeRecordedResponse> responses)
    {
        ArgumentNullException.ThrowIfNull(responses);
        Dictionary<(int Tick, string AgentId), SnowGlobeRecordedResponse> indexed = new();
        foreach (SnowGlobeRecordedResponse response in responses)
        {
            (int Tick, string AgentId) key = (response.ExpectedObservation.Tick, response.ExpectedObservation.AgentId);
            if (!indexed.TryAdd(key, response))
            {
                throw new ArgumentException("Recorded responses must be unique per tick and agent.", nameof(responses));
            }
        }

        _responses = new ReadOnlyDictionary<(int Tick, string AgentId), SnowGlobeRecordedResponse>(indexed);
    }

    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_responses.TryGetValue((observation.Tick, observation.AgentId), out SnowGlobeRecordedResponse? response))
        {
            throw new SnowGlobeInferenceException("recorded_response_missing");
        }

        if (response.ExpectedObservation != observation)
        {
            throw new SnowGlobeInferenceException("recorded_observation_mismatch");
        }

        if (response.FailureReason is not null)
        {
            throw new SnowGlobeInferenceException(response.FailureReason);
        }

        if (response.LatencyUnits <= 0 || response.Proposal is null)
        {
            throw new SnowGlobeInferenceException("recorded_response_malformed");
        }

        return ValueTask.FromResult(response.Proposal!);
    }

    public int LatencyUnitsFor(SnowGlobeObservation observation) =>
        _responses.TryGetValue((observation.Tick, observation.AgentId), out SnowGlobeRecordedResponse? response)
            ? response.LatencyUnits
            : 1;
}

public sealed record SnowGlobeFrozenProposal(
    string AgentId,
    SnowGlobeActionProposal? Proposal,
    int LatencyUnits,
    string? FailureReason)
{
    public bool IsFailure => FailureReason is not null;
}

public sealed record SnowGlobeRoundTrace(
    int Tick,
    IReadOnlyList<SnowGlobeObservation> Observations,
    IReadOnlyList<SnowGlobeFrozenProposal> Proposals);

public sealed class SnowGlobeComparisonMetrics
{
    public int Ticks { get; internal set; }
    public int InferenceCalls { get; internal set; }
    public int AcceptedActions { get; internal set; }
    public int RejectedActions { get; internal set; }
    public int SharedSnapshotRounds { get; internal set; }
    public int ControlledParallelRounds { get; internal set; }
    public int TotalRecordedLatencyUnits { get; internal set; }
    public int CriticalPathLatencyUnits { get; internal set; }
    public int ThroughputMilliActionsPerLatencyUnit { get; internal set; }
    /// <summary>Minimum scheduled deliberation count divided by maximum count, scaled to 1000.</summary>
    public int DispatchCoveragePermille { get; internal set; }
    public IReadOnlyDictionary<string, int> DeliberationTurnsByAgent { get; internal set; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
}

public sealed record SnowGlobeComparisonRunResult(
    string StateDigest,
    string EventDigest,
    SnowGlobeComparisonMetrics Metrics,
    IReadOnlyList<SnowGlobeRoundTrace> Rounds);

/// <summary>
/// Deliberation scheduling research seam. It always freezes a shared round snapshot and proposals
/// before entering the unchanged deterministic validation/commit surface in ordinal agent-ID order.
/// </summary>
public sealed class SharedSnapshotInferenceScheduler
{
    private readonly ISnowGlobeInferenceAdapter _inference;
    private readonly SnowGlobeSchedulingMode _mode;

    public SharedSnapshotInferenceScheduler(ISnowGlobeInferenceAdapter inference, SnowGlobeSchedulingMode mode)
    {
        _inference = inference;
        _mode = mode;
    }

    public async Task<SnowGlobeComparisonRunResult> RunAsync(SnowGlobeWorld world, int ticks, CancellationToken cancellationToken = default)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        SnowGlobeComparisonMetrics metrics = new();
        Dictionary<string, int> turnsByAgent = world.Agents.ToDictionary(agent => agent.AgentId, _ => 0, StringComparer.Ordinal);
        List<SnowGlobeRoundTrace> rounds = new();
        for (int tick = 0; tick < ticks; tick++)
        {
            SnowGlobeObservation[] snapshot = world.Agents.Select(agent => agent.AgentId)
                .OrderBy(agentId => agentId, StringComparer.Ordinal).Select(world.Observe).ToArray();
            SnowGlobeFrozenProposal[] frozen = await DeliberateAsync(snapshot, cancellationToken);
            rounds.Add(new SnowGlobeRoundTrace(tick, Array.AsReadOnly(snapshot), Array.AsReadOnly(frozen)));

            metrics.SharedSnapshotRounds++;
            metrics.ControlledParallelRounds += _mode == SnowGlobeSchedulingMode.ControlledParallel ? 1 : 0;
            metrics.TotalRecordedLatencyUnits += frozen.Sum(item => item.LatencyUnits);
            metrics.CriticalPathLatencyUnits += _mode == SnowGlobeSchedulingMode.SharedSnapshotSequential
                ? frozen.Sum(item => item.LatencyUnits)
                : frozen.Max(item => item.LatencyUnits);

            foreach (SnowGlobeFrozenProposal frozenProposal in frozen.OrderBy(item => item.AgentId, StringComparer.Ordinal))
            {
                metrics.InferenceCalls++;
                turnsByAgent[frozenProposal.AgentId]++;
                if (frozenProposal.IsFailure || frozenProposal.Proposal is null ||
                    !string.Equals(frozenProposal.AgentId, frozenProposal.Proposal.AgentId, StringComparison.Ordinal))
                {
                    metrics.RejectedActions++;
                    continue;
                }

                SnowGlobeCommitResult commit = world.ValidateAndCommit(frozenProposal.Proposal);
                if (commit.Accepted)
                {
                    metrics.AcceptedActions++;
                }
                else
                {
                    metrics.RejectedActions++;
                }
            }

            world.AdvanceTick();
            metrics.Ticks++;
        }

        metrics.DeliberationTurnsByAgent = new ReadOnlyDictionary<string, int>(turnsByAgent);
        int minimumTurns = turnsByAgent.Values.Min();
        int maximumTurns = turnsByAgent.Values.Max();
        metrics.DispatchCoveragePermille = maximumTurns == 0 ? 0 : minimumTurns * 1000 / maximumTurns;
        metrics.ThroughputMilliActionsPerLatencyUnit = metrics.CriticalPathLatencyUnits == 0 ? 0 :
            metrics.AcceptedActions * 1000 / metrics.CriticalPathLatencyUnits;
        return new SnowGlobeComparisonRunResult(world.StateDigest(), world.EventDigest(), metrics, rounds.AsReadOnly());
    }

    private async Task<SnowGlobeFrozenProposal[]> DeliberateAsync(IReadOnlyList<SnowGlobeObservation> snapshot, CancellationToken cancellationToken)
    {
        if (_mode == SnowGlobeSchedulingMode.SharedSnapshotSequential)
        {
            List<SnowGlobeFrozenProposal> proposals = new(snapshot.Count);
            foreach (SnowGlobeObservation observation in snapshot)
            {
                proposals.Add(await CaptureAsync(observation, cancellationToken));
            }

            return proposals.ToArray();
        }

        Task<SnowGlobeFrozenProposal>[] pending = snapshot.Select(observation => CaptureAsync(observation, cancellationToken)).ToArray();
        return (await Task.WhenAll(pending)).OrderBy(item => item.AgentId, StringComparer.Ordinal).ToArray();
    }

    private async Task<SnowGlobeFrozenProposal> CaptureAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        int recordedLatency = _inference is RecordedInferenceAdapter recorded ? recorded.LatencyUnitsFor(observation) : 1;
        int latency = Math.Max(0, recordedLatency);
        try
        {
            return new SnowGlobeFrozenProposal(observation.AgentId, await _inference.ProposeAsync(observation, cancellationToken), latency, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SnowGlobeInferenceException exception)
        {
            return new SnowGlobeFrozenProposal(observation.AgentId, null, latency, exception.Reason);
        }
        catch (Exception)
        {
            return new SnowGlobeFrozenProposal(observation.AgentId, null, latency, "adapter_failure");
        }
    }
}
