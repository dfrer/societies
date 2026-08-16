namespace Societies.SnowGlobe;

public sealed record SnowGlobeSchedulingComparisonResult(
    SnowGlobeWorld SharedSnapshotSequentialWorld,
    SnowGlobeComparisonRunResult SharedSnapshotSequential,
    SnowGlobeWorld ControlledParallelWorld,
    SnowGlobeComparisonRunResult ControlledParallel);

public static class SnowGlobeSchedulingComparisonScenario
{
    public static async Task<SnowGlobeSchedulingComparisonResult> RunFixedSeedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SnowGlobeRecordedResponse> fixture = CreateFixedSeedFixture();
        SnowGlobeWorld sequentialWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeComparisonRunResult sequential = await new SharedSnapshotInferenceScheduler(
            new RecordedInferenceAdapter(fixture), SnowGlobeSchedulingMode.SharedSnapshotSequential)
            .RunAsync(sequentialWorld, SnowGlobeScenario.FixedTicks, cancellationToken);

        SnowGlobeWorld parallelWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeComparisonRunResult parallel = await new SharedSnapshotInferenceScheduler(
            new RecordedInferenceAdapter(fixture), SnowGlobeSchedulingMode.ControlledParallel)
            .RunAsync(parallelWorld, SnowGlobeScenario.FixedTicks, cancellationToken);

        return new SnowGlobeSchedulingComparisonResult(sequentialWorld, sequential, parallelWorld, parallel);
    }

    public static IReadOnlyList<SnowGlobeRecordedResponse> CreateFixedSeedFixture()
    {
        SnowGlobeWorld fixtureWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        List<SnowGlobeRecordedResponse> responses = new(SnowGlobeScenario.FixedAgentCount * SnowGlobeScenario.FixedTicks);
        ScriptedInferenceAdapter scripted = new();
        for (int tick = 0; tick < SnowGlobeScenario.FixedTicks; tick++)
        {
            SnowGlobeObservation[] snapshot = fixtureWorld.Agents.Select(agent => agent.AgentId)
                .OrderBy(agentId => agentId, StringComparer.Ordinal).Select(fixtureWorld.Observe).ToArray();
            foreach (SnowGlobeObservation observation in snapshot)
            {
                SnowGlobeActionProposal proposal = scripted.ProposeAsync(observation, CancellationToken.None).Result;
                responses.Add(new SnowGlobeRecordedResponse(observation, proposal, 1 + observation.HomeSlot % 3));
            }

            foreach (SnowGlobeObservation observation in snapshot)
            {
                SnowGlobeActionProposal proposal = scripted.ProposeAsync(observation, CancellationToken.None).Result;
                if (!fixtureWorld.ValidateAndCommit(proposal).Accepted)
                {
                    throw new InvalidOperationException("The scripted comparison fixture must be valid under ordered commit.");
                }
            }

            fixtureWorld.AdvanceTick();
        }

        return responses.AsReadOnly();
    }
}
