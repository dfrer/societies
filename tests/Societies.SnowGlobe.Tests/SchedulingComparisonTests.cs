using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class SchedulingComparisonTests
{
    [Fact]
    public async Task RecordedScriptedControl_UsesIdenticalFrozenSnapshotsAndDeterministicOrderedCommits()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();

        Assert.Equal(comparison.SharedSnapshotSequential.Rounds.Count, comparison.ControlledParallel.Rounds.Count);
        foreach ((SnowGlobeRoundTrace sequentialRound, SnowGlobeRoundTrace parallelRound) in comparison.SharedSnapshotSequential.Rounds.Zip(comparison.ControlledParallel.Rounds))
        {
            Assert.Equal(sequentialRound.Tick, parallelRound.Tick);
            Assert.Equal(sequentialRound.Observations, parallelRound.Observations);
            Assert.Equal(sequentialRound.Proposals, parallelRound.Proposals);
        }

        Assert.Equal(comparison.SharedSnapshotSequential.StateDigest, comparison.ControlledParallel.StateDigest);
        Assert.Equal(comparison.SharedSnapshotSequential.EventDigest, comparison.ControlledParallel.EventDigest);
        Assert.Equal(comparison.SharedSnapshotSequentialWorld.Events, comparison.ControlledParallelWorld.Events);
        SnowGlobeWorld replayed = SnowGlobeScenario.ReplayFixedSeed(comparison.ControlledParallelWorld.Events);
        Assert.Equal(comparison.ControlledParallel.StateDigest, replayed.StateDigest());
        Assert.Equal(comparison.ControlledParallel.EventDigest, replayed.EventDigest());
    }

    [Fact]
    public async Task ControlledParallel_ReportsDeterministicLatencyThroughputAndDispatchCoverage()
    {
        SnowGlobeSchedulingComparisonResult comparison = await SnowGlobeSchedulingComparisonScenario.RunFixedSeedAsync();
        SnowGlobeComparisonMetrics sequential = comparison.SharedSnapshotSequential.Metrics;
        SnowGlobeComparisonMetrics parallel = comparison.ControlledParallel.Metrics;

        Assert.Equal(32, sequential.InferenceCalls);
        Assert.Equal(32, parallel.InferenceCalls);
        Assert.Equal(32, parallel.AcceptedActions);
        Assert.Equal(0, parallel.RejectedActions);
        Assert.Equal(4, sequential.SharedSnapshotRounds);
        Assert.Equal(0, sequential.ControlledParallelRounds);
        Assert.Equal(4, parallel.ControlledParallelRounds);
        Assert.Equal(sequential.TotalRecordedLatencyUnits, parallel.TotalRecordedLatencyUnits);
        Assert.True(parallel.CriticalPathLatencyUnits < sequential.CriticalPathLatencyUnits);
        Assert.True(parallel.ThroughputMilliActionsPerLatencyUnit > sequential.ThroughputMilliActionsPerLatencyUnit);
        Assert.Equal(1000, parallel.DispatchCoveragePermille);
        Assert.All(parallel.DeliberationTurnsByAgent.Values, turns => Assert.Equal(4, turns));
    }

    [Fact]
    public async Task ControlledParallel_UsesMultipleInFlightCallsAndOutOfOrderCompletion_WhileSequentialIsOneAtATime()
    {
        SnowGlobeRecordedResponse[] fixture = SnowGlobeSchedulingComparisonScenario.CreateFixedSeedFixture()
            .Where(response => response.ExpectedObservation.Tick == 0).ToArray();
        CoordinatedRecordedInferenceAdapter parallelAdapter = new(fixture);
        SnowGlobeWorld parallelWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        Task<SnowGlobeComparisonRunResult> parallelRun = new SharedSnapshotInferenceScheduler(
            parallelAdapter, SnowGlobeSchedulingMode.ControlledParallel).RunAsync(parallelWorld, ticks: 1);

        await parallelAdapter.WaitForStartedCallsAsync(8);
        Assert.Equal(8, parallelAdapter.MaxInFlight);
        while (parallelAdapter.CompleteNext())
        {
        }

        SnowGlobeComparisonRunResult parallel = await parallelRun;
        Assert.Equal(new[] { "agent-07", "agent-06", "agent-05", "agent-04", "agent-03", "agent-02", "agent-01", "agent-00" }, parallelAdapter.CompletionAgentIds);

        CoordinatedRecordedInferenceAdapter sequentialAdapter = new(fixture);
        SnowGlobeWorld sequentialWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        Task<SnowGlobeComparisonRunResult> sequentialRun = new SharedSnapshotInferenceScheduler(
            sequentialAdapter, SnowGlobeSchedulingMode.SharedSnapshotSequential).RunAsync(sequentialWorld, ticks: 1);
        for (int started = 1; started <= 8; started++)
        {
            await sequentialAdapter.WaitForStartedCallsAsync(started);
            Assert.Equal(1, sequentialAdapter.MaxInFlight);
            Assert.True(sequentialAdapter.CompleteNext());
        }

        SnowGlobeComparisonRunResult sequential = await sequentialRun;
        Assert.Equal(parallel.StateDigest, sequential.StateDigest);
        Assert.Equal(parallel.EventDigest, sequential.EventDigest);
        Assert.Equal(parallelWorld.Events, sequentialWorld.Events);
        Assert.Equal(parallel.Rounds.Single().Observations, sequential.Rounds.Single().Observations);
        Assert.Equal(parallel.Rounds.Single().Proposals, sequential.Rounds.Single().Proposals);
    }

    public static IEnumerable<object[]> RecordedFailureFixtures()
    {
        SnowGlobeObservation observation = SnowGlobeWorld.Create(seed: 50, agentCount: 1).Observe("agent-00");
        yield return new object[] { "recorded_response_missing", Array.Empty<SnowGlobeRecordedResponse>() };
        yield return new object[] { "recorded_observation_mismatch", new[] { new SnowGlobeRecordedResponse(observation with { StockpileWood = 1 }, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), 1) } };
        yield return new object[] { "recorded_response_malformed", new[] { new SnowGlobeRecordedResponse(observation, null, 1) } };
        yield return new object[] { "recorded_response_malformed", new[] { new SnowGlobeRecordedResponse(observation, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), 0) } };
        yield return new object[] { "wrong_agent", new[] { new SnowGlobeRecordedResponse(observation, new SnowGlobeActionProposal("agent-99", SnowGlobeActionKind.GatherWood, 4), 1) } };
        yield return new object[] { "fixture_failure", new[] { new SnowGlobeRecordedResponse(observation, new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), 1, "fixture_failure") } };
    }

    [Theory]
    [MemberData(nameof(RecordedFailureFixtures))]
    public async Task RecordedFailureFixtures_FailClosedWithoutActionMutationAndWithFiniteMetrics(string expectedReason, SnowGlobeRecordedResponse[] responses)
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 50, agentCount: 1);
        SnowGlobeComparisonRunResult result = await new SharedSnapshotInferenceScheduler(
            new RecordedInferenceAdapter(responses), SnowGlobeSchedulingMode.ControlledParallel).RunAsync(world, ticks: 1);

        AssertNoActionCommit(world, result);
        SnowGlobeFrozenProposal proposal = result.Rounds.Single().Proposals.Single();
        if (expectedReason == "wrong_agent")
        {
            Assert.False(proposal.IsFailure);
            Assert.Equal("agent-99", proposal.Proposal!.AgentId);
        }
        else
        {
            Assert.Equal(expectedReason, proposal.FailureReason);
        }
    }

    [Fact]
    public async Task GenericAdapterException_FailsClosedWithoutActionMutationAndWithFiniteMetrics()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 51, agentCount: 1);
        SnowGlobeComparisonRunResult result = await new SharedSnapshotInferenceScheduler(
            new ThrowingInferenceAdapter(), SnowGlobeSchedulingMode.ControlledParallel).RunAsync(world, ticks: 1);

        AssertNoActionCommit(world, result);
        Assert.Equal("adapter_failure", result.Rounds.Single().Proposals.Single().FailureReason);
    }

    private static void AssertNoActionCommit(SnowGlobeWorld world, SnowGlobeComparisonRunResult result)
    {
        Assert.Equal(1, result.Metrics.RejectedActions);
        Assert.Equal(0, result.Metrics.AcceptedActions);
        Assert.Empty(world.Events);
        Assert.Equal(64, world.AvailableWood);
        Assert.Equal(0, world.StockpileWood);
        Assert.Empty(world.Structures);
        Assert.Equal(0, world.Agents.Single().CompletedActions);
        Assert.InRange(result.Metrics.TotalRecordedLatencyUnits, 0, int.MaxValue);
        Assert.InRange(result.Metrics.CriticalPathLatencyUnits, 0, int.MaxValue);
        Assert.InRange(result.Metrics.ThroughputMilliActionsPerLatencyUnit, 0, int.MaxValue);
        Assert.InRange(result.Metrics.DispatchCoveragePermille, 0, 1000);
    }

    private sealed class ThrowingInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken) =>
            ValueTask.FromException<SnowGlobeActionProposal>(new InvalidOperationException("fixture_generic_failure"));
    }

    private sealed class CoordinatedRecordedInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        private readonly Dictionary<(int Tick, string AgentId), SnowGlobeRecordedResponse> _responses;
        private readonly List<PendingResponse> _pending = new();
        private readonly List<string> _completionAgentIds = new();
        private readonly object _gate = new();
        private TaskCompletionSource<bool> _startedSignal = NewSignal();
        private int _startedCalls;
        private int _inFlight;

        public CoordinatedRecordedInferenceAdapter(IEnumerable<SnowGlobeRecordedResponse> responses) =>
            _responses = responses.ToDictionary(response => (response.ExpectedObservation.Tick, response.ExpectedObservation.AgentId));

        public int MaxInFlight { get; private set; }
        public IReadOnlyList<string> CompletionAgentIds => _completionAgentIds.AsReadOnly();

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_responses.TryGetValue((observation.Tick, observation.AgentId), out SnowGlobeRecordedResponse? response) || response.ExpectedObservation != observation)
            {
                throw new SnowGlobeInferenceException("async_recorded_fixture_mismatch");
            }

            PendingResponse pending = new(response, new TaskCompletionSource<SnowGlobeActionProposal>(TaskCreationOptions.RunContinuationsAsynchronously));
            lock (_gate)
            {
                _pending.Add(pending);
                _startedCalls++;
                _inFlight++;
                MaxInFlight = Math.Max(MaxInFlight, _inFlight);
                _startedSignal.TrySetResult(true);
                _startedSignal = NewSignal();
            }

            return new ValueTask<SnowGlobeActionProposal>(AwaitResponseAsync(pending, cancellationToken));
        }

        public async Task WaitForStartedCallsAsync(int count, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                Task signal;
                lock (_gate)
                {
                    if (_startedCalls >= count)
                    {
                        return;
                    }

                    signal = _startedSignal.Task;
                }

                await signal.WaitAsync(cancellationToken);
            }
        }

        public bool CompleteNext()
        {
            PendingResponse? next;
            lock (_gate)
            {
                next = _pending.OrderByDescending(item => item.Response.ExpectedObservation.HomeSlot).FirstOrDefault();
                if (next is null)
                {
                    return false;
                }

                _pending.Remove(next);
                _completionAgentIds.Add(next.Response.ExpectedObservation.AgentId);
            }

            next.Completion.TrySetResult(next.Response.Proposal!);
            return true;
        }

        private async Task<SnowGlobeActionProposal> AwaitResponseAsync(PendingResponse pending, CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<SnowGlobeActionProposal>)state!).TrySetCanceled(), pending.Completion);
            try
            {
                return await pending.Completion.Task;
            }
            finally
            {
                lock (_gate)
                {
                    _inFlight--;
                }
            }
        }

        private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed record PendingResponse(SnowGlobeRecordedResponse Response, TaskCompletionSource<SnowGlobeActionProposal> Completion);
    }
}
