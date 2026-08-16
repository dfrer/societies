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
    public async Task ConditionalCommit_AtomicallyOrdersCompetingExpectedIdentitiesAndPreservesDigests()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 13, agentCount: 1);
        SnowGlobeWorldIdentity identity = world.CaptureIdentity();
        using Barrier ready = new(2);

        Task<SnowGlobeExpectedCommitResult> first = Task.Run(() =>
        {
            ready.SignalAndWait();
            return world.ValidateAndCommitIfIdentityMatches(identity.Tick, identity.StateDigest, identity.EventDigest,
                new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        });
        Task<SnowGlobeExpectedCommitResult> second = Task.Run(() =>
        {
            ready.SignalAndWait();
            return world.ValidateAndCommitIfIdentityMatches(identity.Tick, identity.StateDigest, identity.EventDigest,
                new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        });

        SnowGlobeExpectedCommitResult[] attempts = await Task.WhenAll(first, second);
        SnowGlobeExpectedCommitResult accepted = Assert.Single(attempts.Where(attempt => attempt.IdentityMatched));
        SnowGlobeExpectedCommitResult rejected = Assert.Single(attempts.Where(attempt => !attempt.IdentityMatched));
        Assert.True(accepted.CommitResult.Accepted);
        Assert.NotNull(accepted.CommittedEvent);
        Assert.False(rejected.CommitResult.Accepted);
        Assert.Null(rejected.CommittedEvent);
        Assert.Single(world.Events);

        SnowGlobeWorld replayed = SnowGlobeWorld.Create(seed: 13, agentCount: 1);
        replayed.Replay(world.Events.Single());
        Assert.Equal(replayed.StateDigest(), world.StateDigest());
        Assert.Equal(replayed.EventDigest(), world.EventDigest());
    }

    [Fact]
    public void PublicCollections_AreDetachedReadOnlyValuesAndCannotMutateTheWorld()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 14, agentCount: 1);
        Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle)).Accepted);
        Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 12)).Accepted);
        Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherStone, 6)).Accepted);
        Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.BuildShelter)).Accepted);
        string stateBefore = world.StateDigest();
        string eventsBefore = world.EventDigest();

        SnowGlobeAgentRecord detachedAgent = world.Agents.Single();
        SnowGlobeStructure detachedStructure = world.Structures.Single();
        detachedAgent.CompletedActions = 999;
        detachedStructure.Durability = 999;
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)world.Agents).Add(new SnowGlobeAgentRecord("agent-99", 99)));
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)world.Events).RemoveAt(0));

        Assert.Equal(4, world.Agents.Single().CompletedActions);
        Assert.Equal(10, world.Structures.Single().Durability);
        Assert.Equal(stateBefore, world.StateDigest());
        Assert.Equal(eventsBefore, world.EventDigest());
    }

    [Fact]
    public async Task PublicCollectionCaptures_RemainEnumerableDuringConcurrentMutations()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 15, agentCount: 1);
        using CancellationTokenSource completed = new();
        Task writer = Task.Run(() =>
        {
            for (int index = 0; index < 128; index++)
            {
                Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle)).Accepted);
            }
            completed.Cancel();
        });

        while (!completed.IsCancellationRequested)
        {
            SnowGlobeEvent[] events = world.Events.ToArray();
            SnowGlobeAgentRecord[] agents = world.Agents.ToArray();
            SnowGlobeStructure[] structures = world.Structures.ToArray();
            Assert.Single(agents);
            Assert.Empty(structures);
            Assert.All(events, entry => Assert.Equal("agent-00", entry.AgentId));
        }

        await writer;
        Assert.Equal(128, world.Events.Count);
    }

    [Fact]
    public void MutationRevision_IsMonotonicAcrossCommitTickAndReplay()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 16, agentCount: 1);
        long initial = world.CaptureIdentity().Revision;
        Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle)).Accepted);
        long afterCommit = world.CaptureIdentity().Revision;
        world.AdvanceTick();
        long afterTick = world.CaptureIdentity().Revision;
        SnowGlobeWorld replay = SnowGlobeWorld.Create(seed: 16, agentCount: 1);
        replay.Replay(world.Events.Single());

        Assert.Equal(initial + 1, afterCommit);
        Assert.Equal(afterCommit + 1, afterTick);
        Assert.Equal(1, replay.CaptureIdentity().Revision);
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
