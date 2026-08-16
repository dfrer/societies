namespace Societies.SnowGlobe;

/// <summary>
/// Value-only data crossing the inference boundary. No inference adapter receives a world reference.
/// </summary>
public sealed record SnowGlobeObservation(
    string AgentId,
    int HomeSlot,
    int Tick,
    int AvailableWood,
    int AvailableStone,
    int StockpileWood,
    int StockpileStone,
    int ShelterCount,
    int StorageCount);

public interface ISnowGlobeInferenceAdapter
{
    ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken);
}

/// <summary>
/// Offline deterministic adapter used by tests and baseline scheduling experiments.
/// It intentionally is not a model client and has no mutable world reference.
/// </summary>
public sealed class ScriptedInferenceAdapter : ISnowGlobeInferenceAdapter
{
    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SnowGlobeActionKind action = observation.Tick switch
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
        return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, action, quantity));
    }
}

public sealed class SnowGlobeRunMetrics
{
    public int Ticks { get; internal set; }
    public int SequentialQueueTurns { get; internal set; }
    public int InferenceCalls { get; internal set; }
    public int ProposalCount { get; internal set; }
    public int AcceptedActions { get; internal set; }
    public int RejectedActions { get; internal set; }
}

public sealed record SnowGlobeRunResult(string StateDigest, string EventDigest, SnowGlobeRunMetrics Metrics);

/// <summary>
/// The baseline scheduler deliberately awaits one observation/proposal/commit turn at a time.
/// Later planners may vary inference scheduling, but must retain this deterministic commit surface.
/// </summary>
public sealed class SequentialInferenceScheduler
{
    private readonly ISnowGlobeInferenceAdapter _inference;

    public SequentialInferenceScheduler(ISnowGlobeInferenceAdapter inference)
    {
        _inference = inference;
    }

    public async Task<SnowGlobeRunResult> RunAsync(SnowGlobeWorld world, int ticks, CancellationToken cancellationToken = default)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        SnowGlobeRunMetrics metrics = new();
        for (int tick = 0; tick < ticks; tick++)
        {
            foreach (string agentId in world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SnowGlobeObservation observation = world.Observe(agentId);
                SnowGlobeActionProposal proposal = await _inference.ProposeAsync(observation, cancellationToken);
                metrics.SequentialQueueTurns++;
                metrics.InferenceCalls++;
                metrics.ProposalCount++;

                SnowGlobeCommitResult result = world.ValidateAndCommit(proposal);
                if (result.Accepted)
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

        return new SnowGlobeRunResult(world.StateDigest(), world.EventDigest(), metrics);
    }
}
