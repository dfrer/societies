using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class SnowGlobeLabTests
{
    [Fact]
    public async Task FixedSeedEightAgentScenario_BuildsAndMaintainsInfrastructure()
    {
        (SnowGlobeWorld world, SnowGlobeRunResult result) = await SnowGlobeScenario.RunFixedSeedAsync();

        Assert.Equal(SnowGlobeScenario.FixedAgentCount, world.Agents.Count);
        Assert.Contains(world.Structures, structure => structure.Kind == SnowGlobeStructureKind.Shelter);
        Assert.Contains(world.Structures, structure => structure.Kind == SnowGlobeStructureKind.Storage);
        Assert.Equal(12, world.Structures.Single(structure => structure.Kind == SnowGlobeStructureKind.Shelter).Durability);
        Assert.Equal(32, result.Metrics.InferenceCalls);
        Assert.Equal(32, result.Metrics.SequentialQueueTurns);
        Assert.Equal(32, result.Metrics.AcceptedActions);
        Assert.Equal(0, result.Metrics.RejectedActions);
    }

    [Fact]
    public async Task FixedSeedScenario_RepeatsAndReplaysWithIdenticalDigests()
    {
        (SnowGlobeWorld firstWorld, SnowGlobeRunResult first) = await SnowGlobeScenario.RunFixedSeedAsync();
        (SnowGlobeWorld secondWorld, SnowGlobeRunResult second) = await SnowGlobeScenario.RunFixedSeedAsync();
        SnowGlobeWorld replayed = SnowGlobeScenario.ReplayFixedSeed(firstWorld.Events);

        Assert.Equal(first.StateDigest, second.StateDigest);
        Assert.Equal(first.EventDigest, second.EventDigest);
        Assert.Equal(first.StateDigest, replayed.StateDigest());
        Assert.Equal(first.EventDigest, replayed.EventDigest());
        Assert.Equal(firstWorld.Events, secondWorld.Events);
    }

    [Fact]
    public void InvalidProposal_IsRejectedWithoutWorldOrEventMutation()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 7, agentCount: 1);
        string stateBefore = world.StateDigest();
        string eventsBefore = world.EventDigest();

        SnowGlobeCommitResult result = world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.BuildStorage));

        Assert.False(result.Accepted);
        Assert.Equal("insufficient_resources_or_invalid_action", result.RejectionReason);
        Assert.Equal(stateBefore, world.StateDigest());
        Assert.Equal(eventsBefore, world.EventDigest());
        Assert.Empty(world.Events);
    }

    [Fact]
    public async Task SequentialScheduler_UsesValueOnlyObservationBoundaryAndStableAgentOrder()
    {
        RecordingInferenceAdapter inference = new();
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 99, agentCount: 3);
        SequentialInferenceScheduler scheduler = new(inference);

        SnowGlobeRunResult result = await scheduler.RunAsync(world, ticks: 1);

        Assert.Equal(new[] { "agent-00", "agent-01", "agent-02" }, inference.ObservedAgentIds);
        Assert.All(inference.Observations, observation => Assert.Equal(0, observation.Tick));
        Assert.Equal(3, result.Metrics.InferenceCalls);
        Assert.Equal(3, result.Metrics.AcceptedActions);
        Assert.Equal(3, world.Events.Count);
    }

    private sealed class RecordingInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        public List<string> ObservedAgentIds { get; } = new();
        public List<SnowGlobeObservation> Observations { get; } = new();

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            ObservedAgentIds.Add(observation.AgentId);
            Observations.Add(observation);
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }
    }
}
