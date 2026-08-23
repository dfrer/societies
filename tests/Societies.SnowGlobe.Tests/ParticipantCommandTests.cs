using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ParticipantCommandTests
{
    [Fact]
    public async Task AcceptedCommand_UsesValidatorReplayAndReturnsDetachedCanonicalReceipt()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 201, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        SnowGlobeParticipantCommand command = Command(shell, "cmd-01", SnowGlobeActionKind.GatherWood, 3);

        SnowGlobeParticipantCommandReceipt receipt = await shell.SubmitParticipantCommandAsync(command);

        Assert.True(receipt.Accepted);
        Assert.Null(receipt.RejectionReason);
        Assert.False(receipt.ShellOwnershipLost);
        Assert.Equal(0, receipt.ResultingTick);
        Assert.Equal(0, receipt.ResultingEventSequence);
        Assert.Equal(world.StateDigest(), receipt.ResultingStateDigest);
        Assert.Equal(world.EventDigest(), receipt.ResultingEventDigest);
        Assert.Equal("participant-01", receipt.ParticipantId);
        Assert.Equal("cmd-01", receipt.IdempotencyKey);

        SnowGlobeWorld replayed = SnowGlobeWorld.Create(seed: 201, agentCount: 1);
        replayed.Replay(world.Events.Single());
        Assert.Equal(receipt.ResultingStateDigest, replayed.StateDigest());
        Assert.Equal(receipt.ResultingEventDigest, replayed.EventDigest());
        Assert.Single(world.Events);
    }

    [Fact]
    public async Task ValidatorAndStaleRejections_DoNotMutateTheLiveWorld()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 202, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        SnowGlobeObserverSnapshot snapshot = shell.Inspect().Snapshot!;
        string initialState = world.StateDigest();
        string initialEvents = world.EventDigest();

        SnowGlobeParticipantCommandReceipt validator = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-validator", SnowGlobeActionKind.BuildShelter, 0));
        SnowGlobeParticipantCommandReceipt staleTick = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-tick", SnowGlobeActionKind.Idle, 0, tick: 1));
        SnowGlobeParticipantCommandReceipt staleState = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-state", SnowGlobeActionKind.Idle, 0, stateDigest: new string('0', 64)));
        SnowGlobeParticipantCommandReceipt staleEvents = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-events", SnowGlobeActionKind.Idle, 0, eventDigest: new string('0', 64)));

        Assert.Equal("insufficient_resources_or_invalid_action", validator.RejectionReason);
        Assert.Equal("stale_tick", staleTick.RejectionReason);
        Assert.Equal("stale_state_digest", staleState.RejectionReason);
        Assert.Equal("stale_event_digest", staleEvents.RejectionReason);
        Assert.Equal(initialState, world.StateDigest());
        Assert.Equal(initialEvents, world.EventDigest());
        Assert.Equal(snapshot.Tick, world.Tick);
        Assert.Empty(world.Events);
    }

    [Fact]
    public async Task MalformedUnknownAndUndefinedInputs_FailClosed()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 203, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        SnowGlobeParticipantCommand valid = Command(shell, "cmd-valid", SnowGlobeActionKind.Idle, 0);
        (SnowGlobeParticipantCommand? Command, string Reason)[] cases =
        {
            (null, "participant_command_malformed"),
            (valid with { ParticipantId = "\u2603" }, "participant_id_invalid"),
            (valid with { ParticipantId = new string('a', SnowGlobeObserverShell.MaximumParticipantIdLength + 1) }, "participant_id_invalid"),
            (valid with { IdempotencyKey = "UPPER" }, "idempotency_key_invalid"),
            (valid with { ExpectedStateDigest = new string('A', 64) }, "expected_state_digest_invalid"),
            (valid with { TargetAgentId = "agent-99" }, "unknown_agent"),
            (valid with { Action = (SnowGlobeActionKind)999 }, "action_invalid"),
            (valid with { Quantity = null }, "quantity_invalid"),
            (valid with { Quantity = -1, Action = SnowGlobeActionKind.GatherWood }, "quantity_must_be_positive")
        };

        for (int index = 0; index < cases.Length; index++)
        {
            (SnowGlobeParticipantCommand? command, string reason) = cases[index];
            if (command is not null && reason != "idempotency_key_invalid") command = command with { IdempotencyKey = $"cmd-case-{index:D2}" };
            SnowGlobeParticipantCommandReceipt result = await shell.SubmitParticipantCommandAsync(command);
            Assert.False(result.Accepted);
            Assert.Equal(reason, result.RejectionReason);
            Assert.Empty(world.Events);
        }

        SnowGlobeParticipantCommand rawParticipant = valid with { ParticipantId = "participant\nraw", IdempotencyKey = "key-transient" };
        SnowGlobeParticipantCommandReceipt sanitizedParticipant = await shell.SubmitParticipantCommandAsync(rawParticipant);
        Assert.Equal("participant_id_invalid", sanitizedParticipant.RejectionReason);
        Assert.Null(sanitizedParticipant.ParticipantId);
        Assert.Equal("key-transient", sanitizedParticipant.IdempotencyKey);
        Assert.True((await shell.SubmitParticipantCommandAsync(valid with { IdempotencyKey = "key-transient" })).Accepted);

        SnowGlobeParticipantCommand rawKey = valid with { IdempotencyKey = "key\u2603" };
        SnowGlobeParticipantCommandReceipt sanitizedKey = await shell.SubmitParticipantCommandAsync(rawKey);
        Assert.Equal("idempotency_key_invalid", sanitizedKey.RejectionReason);
        Assert.Equal("participant-01", sanitizedKey.ParticipantId);
        Assert.Null(sanitizedKey.IdempotencyKey);
    }

    [Fact]
    public async Task AdmittedSemanticRejections_AreDurableAcrossLaterWorldChanges()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 210, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        SnowGlobeParticipantCommand stale = Command(shell, "cmd-stale", SnowGlobeActionKind.Idle, 0, tick: 1);
        SnowGlobeParticipantCommand validator = Command(shell, "cmd-validator", SnowGlobeActionKind.BuildShelter, 0);

        SnowGlobeParticipantCommandReceipt firstStale = await shell.SubmitParticipantCommandAsync(stale);
        SnowGlobeParticipantCommandReceipt firstValidator = await shell.SubmitParticipantCommandAsync(validator);
        Assert.Equal("stale_tick", firstStale.RejectionReason);
        Assert.Equal("insufficient_resources_or_invalid_action", firstValidator.RejectionReason);

        Assert.True((await shell.ResumeAsync()).Applied);
        Assert.True((await shell.AdvanceAsync()).Applied);

        SnowGlobeParticipantCommandReceipt staleRetry = await shell.SubmitParticipantCommandAsync(stale);
        SnowGlobeParticipantCommandReceipt validatorRetry = await shell.SubmitParticipantCommandAsync(validator);
        SnowGlobeParticipantCommandReceipt conflict = await shell.SubmitParticipantCommandAsync(stale with { Quantity = 1 });
        Assert.Same(firstStale, staleRetry);
        Assert.Same(firstValidator, validatorRetry);
        Assert.Equal("command_id_conflict", conflict.RejectionReason);
        Assert.Single(world.Events);
    }

    [Fact]
    public async Task TransientAdmissions_AreNotStoredAndCanBeRetried()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 211, agentCount: 1);
        SnowGlobeObserverShell shell = new(world, new SequentialInferenceScheduler(new IdleInferenceAdapter()));
        SnowGlobeParticipantCommand command = Command(shell, "cmd-transient", SnowGlobeActionKind.Idle, 0);

        SnowGlobeParticipantCommandReceipt unpaused = await shell.SubmitParticipantCommandAsync(command);
        Assert.Equal("must_be_paused", unpaused.RejectionReason);
        Assert.True((await shell.PauseAsync()).Applied);
        using (CancellationTokenSource cancelled = new())
        {
            cancelled.Cancel();
            SnowGlobeParticipantCommandReceipt cancellation = await shell.SubmitParticipantCommandAsync(command, cancelled.Token);
            Assert.Equal("operation_cancelled", cancellation.RejectionReason);
        }

        SnowGlobeParticipantCommandReceipt accepted = await shell.SubmitParticipantCommandAsync(command);
        Assert.True(accepted.Accepted);
        Assert.Single(world.Events);
    }

    [Fact]
    public async Task DuplicateConflictAndSaturation_AreBoundedAndNeverReapply()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 204, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        SnowGlobeParticipantCommand first = Command(shell, "cmd-duplicate", SnowGlobeActionKind.Idle, 0);
        SnowGlobeParticipantCommandReceipt accepted = await shell.SubmitParticipantCommandAsync(first);
        SnowGlobeParticipantCommandReceipt duplicate = await shell.SubmitParticipantCommandAsync(first);
        SnowGlobeParticipantCommandReceipt conflict = await shell.SubmitParticipantCommandAsync(first with { Quantity = 1 });

        Assert.True(accepted.Accepted);
        Assert.Same(accepted, duplicate);
        Assert.Equal("command_id_conflict", conflict.RejectionReason);
        Assert.Single(world.Events);

        for (int index = 1; index < SnowGlobeObserverShell.MaximumParticipantCommandReceipts; index++)
        {
            SnowGlobeParticipantCommandReceipt next = await shell.SubmitParticipantCommandAsync(Command(shell, $"cmd-{index:D3}", SnowGlobeActionKind.Idle, 0));
            Assert.True(next.Accepted);
        }

        SnowGlobeParticipantCommandReceipt saturated = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-overflow", SnowGlobeActionKind.Idle, 0));
        SnowGlobeParticipantCommandReceipt saturatedRetry = await shell.SubmitParticipantCommandAsync(Command(shell, "cmd-overflow", SnowGlobeActionKind.Idle, 0));
        Assert.False(saturated.Accepted);
        Assert.Equal("idempotency_store_saturated", saturated.RejectionReason);
        Assert.Equal("idempotency_store_saturated", saturatedRetry.RejectionReason);
        Assert.Equal(SnowGlobeObserverShell.MaximumParticipantCommandReceipts, world.Events.Count);
    }

    [Fact]
    public async Task CommandsRequirePausedStateAndRespectCancellation()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 205, agentCount: 1);
        SnowGlobeObserverShell shell = new(world, new SequentialInferenceScheduler(new IdleInferenceAdapter()));
        SnowGlobeParticipantCommand command = Command(shell, "cmd-paused", SnowGlobeActionKind.Idle, 0);

        SnowGlobeParticipantCommandReceipt running = await shell.SubmitParticipantCommandAsync(command);
        Assert.Equal("must_be_paused", running.RejectionReason);
        Assert.True((await shell.PauseAsync()).Applied);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        SnowGlobeParticipantCommandReceipt cancellation = await shell.SubmitParticipantCommandAsync(command, cancelled.Token);
        Assert.Equal("operation_cancelled", cancellation.RejectionReason);
        Assert.Empty(world.Events);
    }

    [Fact]
    public async Task ConcurrentOperationsAndOwnershipInterference_FailClosed()
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(seed: 206, agentCount: 1);
        SnowGlobeObserverShell shell = CreatePausedShell(world);
        using ManualResetEventSlim candidateApplied = new(false);
        using ManualResetEventSlim release = new(false);
        shell.AfterParticipantCandidateApplyForTesting = () =>
        {
            candidateApplied.Set();
            release.Wait();
        };

        SnowGlobeParticipantCommand busyCommand = Command(shell, "cmd-busy", SnowGlobeActionKind.Idle, 0);
        SnowGlobeParticipantCommand concurrentCommand = Command(shell, "cmd-concurrent", SnowGlobeActionKind.Idle, 0);
        Task<SnowGlobeParticipantCommandReceipt> inFlight = Task.Run(async () => await shell.SubmitParticipantCommandAsync(concurrentCommand));
        Assert.True(candidateApplied.Wait(TimeSpan.FromSeconds(5)));
        SnowGlobeParticipantCommandReceipt busy = await shell.SubmitParticipantCommandAsync(busyCommand);
        Assert.Equal("operation_in_progress", busy.RejectionReason);
        release.Set();
        Assert.True((await inFlight).Accepted);
        SnowGlobeParticipantCommandReceipt busyRetry = await shell.SubmitParticipantCommandAsync(busyCommand);
        Assert.Equal("stale_state_digest", busyRetry.RejectionReason);

        SnowGlobeWorld beforeWorld = SnowGlobeWorld.Create(seed: 207, agentCount: 1);
        SnowGlobeObserverShell beforeShell = CreatePausedShell(beforeWorld);
        SnowGlobeParticipantCommand beforeCommand = Command(beforeShell, "cmd-before", SnowGlobeActionKind.Idle, 0);
        beforeWorld.AdvanceTick();
        SnowGlobeParticipantCommandReceipt before = await beforeShell.SubmitParticipantCommandAsync(beforeCommand);
        Assert.Equal("world_ownership_lost", before.RejectionReason);

        SnowGlobeWorld middleWorld = SnowGlobeWorld.Create(seed: 208, agentCount: 1);
        SnowGlobeObserverShell middleShell = CreatePausedShell(middleWorld);
        middleShell.AfterParticipantCandidateApplyForTesting = middleWorld.AdvanceTick;
        SnowGlobeParticipantCommandReceipt middle = await middleShell.SubmitParticipantCommandAsync(Command(middleShell, "cmd-middle", SnowGlobeActionKind.Idle, 0));
        Assert.Equal("world_ownership_lost", middle.RejectionReason);
        Assert.Empty(middleWorld.Events);

        SnowGlobeWorld boundaryWorld = SnowGlobeWorld.Create(seed: 212, agentCount: 1);
        SnowGlobeObserverShell boundaryShell = CreatePausedShell(boundaryWorld);
        SnowGlobeParticipantCommand boundaryCommand = Command(boundaryShell, "cmd-boundary", SnowGlobeActionKind.Idle, 0);
        boundaryShell.BeforeParticipantConditionalCommitForTesting = () =>
            boundaryWorld.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        SnowGlobeParticipantCommandReceipt boundary = await boundaryShell.SubmitParticipantCommandAsync(boundaryCommand);
        Assert.False(boundary.Accepted);
        Assert.Equal("world_ownership_lost", boundary.RejectionReason);
        Assert.Single(boundaryWorld.Events);
        Assert.Equal("agent-00", boundaryWorld.Events.Single().AgentId);
        SnowGlobeParticipantCommandReceipt boundaryRetry = await boundaryShell.SubmitParticipantCommandAsync(boundaryCommand);
        Assert.Equal("world_ownership_lost", boundaryRetry.RejectionReason);
        Assert.Single(boundaryWorld.Events);

        SnowGlobeWorld afterWorld = SnowGlobeWorld.Create(seed: 209, agentCount: 1);
        SnowGlobeObserverShell afterShell = CreatePausedShell(afterWorld);
        afterShell.AfterLiveApplyForTesting = () => afterWorld.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle));
        SnowGlobeParticipantCommand afterCommand = Command(afterShell, "cmd-after", SnowGlobeActionKind.Idle, 0);
        SnowGlobeParticipantCommandReceipt after = await afterShell.SubmitParticipantCommandAsync(afterCommand);
        Assert.True(after.Accepted);
        Assert.True(after.ShellOwnershipLost);
        Assert.Null(after.RejectionReason);
        Assert.Equal(0, after.ResultingTick);
        Assert.Equal(0, after.ResultingEventSequence);
        Assert.Equal(2, afterWorld.Events.Count);
        SnowGlobeWorld expectedMicroCheckpoint = SnowGlobeWorld.Create(seed: 209, agentCount: 1);
        Assert.True(expectedMicroCheckpoint.ValidateAndCommit(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle)).Accepted);
        Assert.Equal(expectedMicroCheckpoint.StateDigest(), after.ResultingStateDigest);
        Assert.Equal(expectedMicroCheckpoint.EventDigest(), after.ResultingEventDigest);
        Assert.NotEqual(afterWorld.EventDigest(), after.ResultingEventDigest);
        SnowGlobeParticipantCommandReceipt afterRetry = await afterShell.SubmitParticipantCommandAsync(afterCommand);
        SnowGlobeParticipantCommandReceipt afterConflict = await afterShell.SubmitParticipantCommandAsync(afterCommand with { Quantity = 1 });
        Assert.Same(after, afterRetry);
        Assert.Equal("command_id_conflict", afterConflict.RejectionReason);
    }

    private static SnowGlobeObserverShell CreatePausedShell(SnowGlobeWorld world)
    {
        SnowGlobeObserverShell shell = new(world, new SequentialInferenceScheduler(new IdleInferenceAdapter()));
        Assert.True(shell.PauseAsync().GetAwaiter().GetResult().Applied);
        return shell;
    }

    private static SnowGlobeParticipantCommand Command(
        SnowGlobeObserverShell shell,
        string key,
        SnowGlobeActionKind action,
        int quantity,
        int? tick = null,
        string? stateDigest = null,
        string? eventDigest = null)
    {
        SnowGlobeObserverSnapshot snapshot = shell.Inspect().Snapshot!;
        return new SnowGlobeParticipantCommand(
            "participant-01",
            key,
            tick ?? snapshot.Tick,
            stateDigest ?? snapshot.StateDigest,
            eventDigest ?? snapshot.EventDigest,
            "agent-00",
            action,
            quantity);
    }

    private sealed class IdleInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
    }
}
