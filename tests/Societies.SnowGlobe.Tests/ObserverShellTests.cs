using System.Collections;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ObserverShellTests
{
    [Fact]
    public async Task PauseResumeStepAndInspect_UsesExplicitPausedTickControl()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeObserverShell shell = CreateShell(world, new ScriptedInferenceAdapter());

        SnowGlobeObserverInspectionResult initial = shell.Inspect();
        Assert.True(initial.Accepted);
        Assert.False(initial.Snapshot!.IsPaused);
        Assert.Equal(0, initial.Snapshot.Tick);

        SnowGlobeObserverControlResult paused = await shell.PauseAsync();
        Assert.True(paused.Applied);
        Assert.True(paused.Snapshot!.IsPaused);

        string stateBeforeAdvance = world.StateDigest();
        SnowGlobeObserverControlResult ordinaryAdvance = await shell.AdvanceAsync();
        Assert.False(ordinaryAdvance.Applied);
        Assert.Equal("paused", ordinaryAdvance.RejectionReason);
        Assert.Equal(stateBeforeAdvance, world.StateDigest());

        SnowGlobeObserverControlResult step = await shell.StepAsync(new SnowGlobeObserverStepCommand(1));
        Assert.True(step.Applied);
        Assert.True(step.Snapshot!.IsPaused);
        Assert.Equal(1, step.Snapshot.Tick);
        Assert.Equal(SnowGlobeScenario.FixedAgentCount, step.Snapshot.EventHistoryCount);
        Assert.Equal(SnowGlobeScenario.FixedAgentCount, step.Snapshot.CanonicalEvents.Count);

        SnowGlobeObserverControlResult resumed = await shell.ResumeAsync();
        Assert.True(resumed.Applied);
        Assert.False(resumed.Snapshot!.IsPaused);
        SnowGlobeObserverControlResult advance = await shell.AdvanceAsync();
        Assert.True(advance.Applied);
        Assert.Equal(2, advance.Snapshot!.Tick);
        Assert.Equal(16, advance.Snapshot.EventHistoryCount);
        Assert.Equal(16, advance.Snapshot.CanonicalEvents.Count);
    }

    [Fact]
    public async Task PausedStepping_MatchesDirectScheduledExecutionAndCanonicalReplay()
    {
        SnowGlobeWorld directWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        await new SequentialInferenceScheduler(new ScriptedInferenceAdapter()).RunAsync(directWorld, SnowGlobeScenario.FixedTicks);

        SnowGlobeWorld observedWorld = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeObserverShell shell = CreateShell(observedWorld, new ScriptedInferenceAdapter());
        Assert.True((await shell.PauseAsync()).Applied);
        SnowGlobeObserverControlResult stepped = await shell.StepAsync(new SnowGlobeObserverStepCommand(SnowGlobeScenario.FixedTicks));

        Assert.True(stepped.Applied);
        SnowGlobeObserverSnapshot snapshot = stepped.Snapshot!;
        Assert.Equal(directWorld.StateDigest(), snapshot.StateDigest);
        Assert.Equal(directWorld.EventDigest(), snapshot.EventDigest);
        Assert.Equal(directWorld.Events.Count, snapshot.CanonicalEvents.Count);
        Assert.Equal(directWorld.Events.Select(Canonical), snapshot.CanonicalEvents.Select(entry => entry.Canonical));

        SnowGlobeWorld replayed = SnowGlobeScenario.ReplayFixedSeed(directWorld.Events);
        Assert.Equal(snapshot.StateDigest, replayed.StateDigest());
        Assert.Equal(snapshot.EventDigest, replayed.EventDigest());
    }

    [Fact]
    public async Task InvalidStepCommands_FailClosedWithoutWorldMutation()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 99, agentCount: 1);
        SnowGlobeObserverShell shell = CreateShell(world, new ScriptedInferenceAdapter());
        Assert.True((await shell.PauseAsync()).Applied);
        string stateBefore = world.StateDigest();
        string eventsBefore = world.EventDigest();
        SnowGlobeObserverDiagnostics diagnosticsBefore = shell.GetDiagnostics();

        (SnowGlobeObserverStepCommand? Command, string Reason)[] invalidCommands =
        {
            (null, "step_command_malformed"),
            (new SnowGlobeObserverStepCommand(null), "step_command_malformed"),
            (new SnowGlobeObserverStepCommand(0), "step_count_must_be_positive"),
            (new SnowGlobeObserverStepCommand(-1), "step_count_must_be_positive"),
            (new SnowGlobeObserverStepCommand(SnowGlobeObserverShell.MaximumStepTicks + 1), "step_count_exceeds_bound"),
            (new SnowGlobeObserverStepCommand(int.MaxValue), "step_count_exceeds_bound")
        };

        foreach ((SnowGlobeObserverStepCommand? command, string reason) in invalidCommands)
        {
            SnowGlobeObserverControlResult result = await shell.StepAsync(command);
            Assert.False(result.Applied);
            Assert.Equal(reason, result.RejectionReason);
            Assert.Equal(stateBefore, world.StateDigest());
            Assert.Equal(eventsBefore, world.EventDigest());
            Assert.Equal(0, result.Snapshot!.Tick);
            Assert.Equal(diagnosticsBefore.FullHistoryDigestRefreshes, shell.GetDiagnostics().FullHistoryDigestRefreshes);
        }
    }

    [Fact]
    public async Task Inspection_IsDetachedImmutableValueOnlySnapshot()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, SnowGlobeScenario.FixedAgentCount);
        SnowGlobeObserverShell shell = CreateShell(world, new ScriptedInferenceAdapter());
        Assert.True((await shell.AdvanceAsync()).Applied);
        SnowGlobeObserverSnapshot snapshot = shell.Inspect().Snapshot!;

        Assert.NotEmpty(snapshot.Agents);
        Assert.NotEmpty(snapshot.CanonicalEvents);
        Assert.All(snapshot.Agents, agent => Assert.IsType<SnowGlobeObserverAgentSnapshot>(agent));
        Assert.All(snapshot.CanonicalEvents, entry => Assert.IsType<SnowGlobeObserverEventSnapshot>(entry));
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot.Agents).Add(new SnowGlobeObserverAgentSnapshot("agent-99", 99, 0)));
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot.CanonicalEvents).RemoveAt(0));

        await shell.AdvanceAsync();
        SnowGlobeObserverSnapshot later = shell.Inspect().Snapshot!;
        Assert.Equal(1, snapshot.Tick);
        Assert.Equal(SnowGlobeScenario.FixedAgentCount, snapshot.CanonicalEvents.Count);
        Assert.Equal(2, later.Tick);
        Assert.Equal(16, later.CanonicalEvents.Count);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task AdapterFailureAtAnyOrdinalTurn_LeavesAdvanceAndStepWorldAndInspectionUnchanged(bool pausedStep, int failingCall)
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 88, agentCount: 3);
        SnowGlobeObserverShell shell = CreateShell(world, new FailingOrdinalInferenceAdapter(failingCall));
        if (pausedStep)
        {
            Assert.True((await shell.PauseAsync()).Applied);
        }

        SnowGlobeObserverSnapshot before = shell.Inspect().Snapshot!;
        SnowGlobeObserverControlResult result = pausedStep
            ? await shell.StepAsync(new SnowGlobeObserverStepCommand(1))
            : await shell.AdvanceAsync();
        SnowGlobeObserverSnapshot after = shell.Inspect().Snapshot!;

        Assert.False(result.Applied);
        Assert.Equal("scheduler_failure", result.RejectionReason);
        AssertSnapshotEqual(before, result.Snapshot!);
        AssertSnapshotEqual(before, after);
        Assert.Equal(before.StateDigest, world.StateDigest());
        Assert.Equal(before.EventDigest, world.EventDigest());
        Assert.Equal(before.Tick, world.Tick);
        Assert.Equal(before.EventHistoryCount, world.Events.Count);
        Assert.All(world.Agents, agent => Assert.Equal(0, agent.CompletedActions));
    }

    [Fact]
    public async Task InspectionPagesRemainBoundedAcrossRepeatedGrowthAndPreserveFullHistoryDigest()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 77, agentCount: 64);
        SnowGlobeObserverShell shell = CreateShell(world, new IdleInferenceAdapter());

        SnowGlobeObserverControlResult first = await shell.AdvanceAsync();
        SnowGlobeObserverControlResult second = await shell.AdvanceAsync();
        Assert.True(first.Applied);
        Assert.True(second.Applied);
        Assert.Equal(64, first.Snapshot!.EventHistoryCount);
        Assert.Equal(SnowGlobeObserverShell.MaximumInspectionEventWindow, first.Snapshot.CanonicalEvents.Count);
        Assert.Equal(128, second.Snapshot!.EventHistoryCount);
        Assert.Equal(SnowGlobeObserverShell.MaximumInspectionEventWindow, second.Snapshot.CanonicalEvents.Count);
        Assert.Equal(SnowGlobeObserverShell.MaximumInspectionEventWindow, second.Snapshot.NextEventCursor);

        SnowGlobeObserverInspectionResult middlePage = shell.Inspect(SnowGlobeObserverShell.MaximumInspectionEventWindow);
        SnowGlobeObserverInspectionResult finalPage = shell.Inspect(96);
        Assert.True(middlePage.Accepted);
        Assert.Equal(32, middlePage.Snapshot!.CanonicalEvents.Count);
        Assert.Equal(32, middlePage.Snapshot.EventCursor);
        Assert.Equal(64, middlePage.Snapshot.NextEventCursor);
        Assert.True(finalPage.Accepted);
        Assert.Equal(32, finalPage.Snapshot!.CanonicalEvents.Count);
        Assert.Equal(96, finalPage.Snapshot.EventCursor);
        Assert.Null(finalPage.Snapshot.NextEventCursor);
        Assert.Equal(world.EventDigest(), finalPage.Snapshot.EventDigest);
        Assert.False(shell.Inspect(129).Accepted);
        Assert.Equal("event_cursor_invalid", shell.Inspect(129).RejectionReason);
    }

    [Fact]
    public async Task RepeatedInspectionProjectsOnlyRequestedPagesAndReusesTheCachedHistoryDigest()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 79, agentCount: 64);
        SnowGlobeObserverShell shell = CreateShell(world, new IdleInferenceAdapter());
        Assert.True((await shell.AdvanceAsync()).Applied);
        Assert.True((await shell.AdvanceAsync()).Applied);
        SnowGlobeObserverDiagnostics before = shell.GetDiagnostics();
        string expectedDigest = world.EventDigest();

        foreach (int cursor in new[] { 0, 32, 64, 96 })
        {
            SnowGlobeObserverInspectionResult inspection = shell.Inspect(cursor);
            Assert.True(inspection.Accepted);
            Assert.Equal(32, inspection.Snapshot!.CanonicalEvents.Count);
            Assert.Equal(expectedDigest, inspection.Snapshot.EventDigest);
        }

        SnowGlobeObserverDiagnostics after = shell.GetDiagnostics();
        Assert.Equal(before.FullHistoryDigestRefreshes, after.FullHistoryDigestRefreshes);
        Assert.Equal(before.ProjectedEventEntries + 128, after.ProjectedEventEntries);

        Assert.True((await shell.PauseAsync()).Applied);
        Assert.False((await shell.StepAsync(new SnowGlobeObserverStepCommand(0))).Applied);
        Assert.Equal(after.FullHistoryDigestRefreshes, shell.GetDiagnostics().FullHistoryDigestRefreshes);
    }

    [Fact]
    public async Task InspectionProjection_UsesCachedIdentityWithoutFullHistoryRefreshOrUnboundedEventCopies()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 83, agentCount: 64);
        SnowGlobeObserverShell shell = CreateShell(world, new IdleInferenceAdapter());
        Assert.True((await shell.AdvanceAsync()).Applied);
        Assert.True((await shell.AdvanceAsync()).Applied);
        SnowGlobeObserverDiagnostics before = shell.GetDiagnostics();

        foreach (int cursor in new[] { 0, 32, 64, 96 })
        {
            SnowGlobeObserverInspectionResult page = shell.Inspect(cursor);
            Assert.True(page.Accepted);
            Assert.Equal(32, page.Snapshot!.CanonicalEvents.Count);
        }

        SnowGlobeObserverDiagnostics after = shell.GetDiagnostics();
        Assert.Equal(before.FullHistoryDigestRefreshes, after.FullHistoryDigestRefreshes);
        Assert.Equal(before.ProjectedEventEntries + 128, after.ProjectedEventEntries);
    }

    [Fact]
    public async Task ExternalWorldMutation_InvalidatesShellOwnershipWithoutServingStaleCachedDigests()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 80, agentCount: 1);
        SnowGlobeObserverShell shell = CreateShell(world, new IdleInferenceAdapter());
        SnowGlobeObserverDiagnostics before = shell.GetDiagnostics();
        world.AdvanceTick();

        SnowGlobeObserverInspectionResult inspection = shell.Inspect();
        SnowGlobeObserverControlResult pause = await shell.PauseAsync();
        Assert.False(inspection.Accepted);
        Assert.Equal("world_ownership_lost", inspection.RejectionReason);
        Assert.Null(inspection.Snapshot);
        Assert.False(pause.Applied);
        Assert.Equal("world_ownership_lost", pause.RejectionReason);
        Assert.Null(pause.Snapshot);
        Assert.Equal(before, shell.GetDiagnostics());
    }

    [Fact]
    public async Task ExternalCommitAfterCandidateApply_FailsClosedAndNeverCachesCandidateOnlyDigests()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 81, agentCount: 1);
        SnowGlobeObserverShell shell = new(world, new SequentialInferenceScheduler(new IdleInferenceAdapter()));
        shell.AfterLiveApplyForTesting = () => world.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        SnowGlobeObserverDiagnostics before = shell.GetDiagnostics();

        SnowGlobeObserverControlResult result = await shell.AdvanceAsync();

        Assert.False(result.Applied);
        Assert.Equal("world_ownership_lost", result.RejectionReason);
        Assert.Null(result.Snapshot);
        Assert.Equal(2, world.Events.Count);
        Assert.Equal(1, world.Tick);
        Assert.Equal(new[] { 0, 1 }, world.Events.Select(entry => entry.Tick));
        Assert.False(shell.HasExclusiveWorldOwnership);
        Assert.False(shell.Inspect().Accepted);
        Assert.Equal("world_ownership_lost", shell.Inspect().RejectionReason);
        Assert.Equal(before, shell.GetDiagnostics());
    }

    [Fact]
    public async Task SuccessfulCommit_CachesExactLiveIdentityForCoherentPostCommitInspection()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 82, agentCount: 1);
        SnowGlobeObserverShell shell = CreateShell(world, new IdleInferenceAdapter());

        SnowGlobeObserverControlResult result = await shell.AdvanceAsync();
        SnowGlobeObserverInspectionResult inspection = shell.Inspect();

        Assert.True(result.Applied);
        Assert.True(inspection.Accepted);
        Assert.True(shell.HasExclusiveWorldOwnership);
        Assert.Equal(world.Tick, inspection.Snapshot!.Tick);
        Assert.Equal(world.Events.Count, inspection.Snapshot.EventHistoryCount);
        Assert.Equal(world.StateDigest(), inspection.Snapshot.StateDigest);
        Assert.Equal(world.EventDigest(), inspection.Snapshot.EventDigest);
    }

    [Fact]
    public async Task ConcurrentControls_FailClosedUntilTheInFlightCompleteTickReleasesTheGate()
    {
        BlockingInferenceAdapter inference = new();
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 51, agentCount: 1);
        SnowGlobeObserverShell shell = CreateShell(world, inference);
        SnowGlobeObserverDiagnostics before = shell.GetDiagnostics();

        Task<SnowGlobeObserverControlResult> advancing = shell.AdvanceAsync();
        await inference.WaitForCallAsync();

        SnowGlobeObserverControlResult pauseDuringTick = await shell.PauseAsync();
        SnowGlobeObserverInspectionResult inspectionDuringTick = shell.Inspect();
        Assert.False(pauseDuringTick.Applied);
        Assert.Equal("operation_in_progress", pauseDuringTick.RejectionReason);
        Assert.Null(pauseDuringTick.Snapshot);
        Assert.False(inspectionDuringTick.Accepted);
        Assert.Equal("operation_in_progress", inspectionDuringTick.RejectionReason);
        Assert.Null(inspectionDuringTick.Snapshot);
        Assert.Equal(before, shell.GetDiagnostics());

        inference.Complete();
        SnowGlobeObserverControlResult advance = await advancing;
        Assert.True(advance.Applied);
        Assert.Equal(1, advance.Snapshot!.Tick);
        Assert.True((await shell.PauseAsync()).Applied);
    }

    private static SnowGlobeObserverShell CreateShell(SnowGlobeWorld world, ISnowGlobeInferenceAdapter inference) =>
        new(world, new SequentialInferenceScheduler(inference));

    private static string Canonical(SnowGlobeEvent entry) =>
        $"{entry.Tick}|{entry.Sequence}|{entry.AgentId}|{entry.Action}|{entry.Quantity}|{entry.StructureId ?? string.Empty}";

    private static void AssertSnapshotEqual(SnowGlobeObserverSnapshot expected, SnowGlobeObserverSnapshot actual)
    {
        Assert.Equal(expected.IsPaused, actual.IsPaused);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.AvailableWood, actual.AvailableWood);
        Assert.Equal(expected.AvailableStone, actual.AvailableStone);
        Assert.Equal(expected.StockpileWood, actual.StockpileWood);
        Assert.Equal(expected.StockpileStone, actual.StockpileStone);
        Assert.Equal(expected.EventHistoryCount, actual.EventHistoryCount);
        Assert.Equal(expected.EventCursor, actual.EventCursor);
        Assert.Equal(expected.NextEventCursor, actual.NextEventCursor);
        Assert.Equal(expected.StateDigest, actual.StateDigest);
        Assert.Equal(expected.EventDigest, actual.EventDigest);
        Assert.Equal(expected.Agents, actual.Agents);
        Assert.Equal(expected.Structures, actual.Structures);
        Assert.Equal(expected.CanonicalEvents, actual.CanonicalEvents);
    }

    private sealed class FailingOrdinalInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        private readonly int _failingCall;
        private int _calls;

        public FailingOrdinalInferenceAdapter(int failingCall) => _failingCall = failingCall;

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            if (_calls++ == _failingCall)
            {
                return ValueTask.FromException<SnowGlobeActionProposal>(new InvalidOperationException("fixture_failure"));
            }

            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }
    }

    private sealed class IdleInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
    }

    private sealed class BlockingInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        private readonly TaskCompletionSource<SnowGlobeActionProposal> _proposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            _called.TrySetResult(true);
            return new ValueTask<SnowGlobeActionProposal>(_proposal.Task);
        }

        public Task WaitForCallAsync() => _called.Task;

        public void Complete() => _proposal.TrySetResult(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
    }
}
