using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class PersistedSessionV5PauseTests
{
    [Fact]
    public void PublicCreateAndReopenExposeExactThreeAndFourArgumentOverloadsWithoutOptionalPause()
    {
        foreach (string methodName in new[] { nameof(SnowGlobePersistedSession.CreateNew), nameof(SnowGlobePersistedSession.Reopen) })
        {
            System.Reflection.MethodInfo[] methods = typeof(SnowGlobePersistedSession)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(method => method.Name == methodName)
                .OrderBy(method => method.GetParameters().Length)
                .ToArray();
            Assert.Equal(new[] { 3, 4 }, methods.Select(method => method.GetParameters().Length));
            Assert.All(methods.SelectMany(method => method.GetParameters()), parameter => Assert.False(parameter.IsOptional));
            Assert.Equal(typeof(bool), methods[1].GetParameters()[3].ParameterType);
        }
    }

    [Fact]
    public void CreateAndReopenOverloads_DeriveOnlyV5DurablePause()
    {
        string unpausedRoot = NewTemporaryDirectory();
        string pausedRoot = NewTemporaryDirectory();
        string explicitFalseRoot = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity unpausedIdentity = Identity("session_v5_unpaused/v1");
            using (SnowGlobePersistedSession created = SnowGlobePersistedSession.CreateNew(
                unpausedRoot, unpausedIdentity, new IdleAdapter(unpausedIdentity.AdapterIdentity)))
            {
                Assert.False(created.IsPaused);
                Assert.Empty(SnowGlobeRunStore.Read(unpausedRoot).Records);
            }
            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                unpausedRoot, unpausedIdentity, new IdleAdapter(unpausedIdentity.AdapterIdentity)))
                Assert.False(reopened.IsPaused);

            SnowGlobeRunIdentity pausedIdentity = Identity("session_v5_initially_paused/v1");
            using (SnowGlobePersistedSession created = SnowGlobePersistedSession.CreateNew(
                pausedRoot, pausedIdentity, new IdleAdapter(pausedIdentity.AdapterIdentity), isPaused: true))
            {
                Assert.True(created.IsPaused);
                SnowGlobeLedgerRecord transition = Assert.Single(SnowGlobeRunStore.Read(pausedRoot).Records);
                Assert.Equal("Pause", transition.Action);
            }
            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                pausedRoot, pausedIdentity, new IdleAdapter(pausedIdentity.AdapterIdentity)))
                Assert.True(reopened.IsPaused);

            SnowGlobeRunIdentity explicitFalseIdentity = Identity("session_v5_initial_false/v1");
            using (SnowGlobePersistedSession created = SnowGlobePersistedSession.CreateNew(
                explicitFalseRoot, explicitFalseIdentity, new IdleAdapter(explicitFalseIdentity.AdapterIdentity), isPaused: false))
            {
                Assert.False(created.IsPaused);
                Assert.Empty(SnowGlobeRunStore.Read(explicitFalseRoot).Records);
            }
        }
        finally
        {
            Directory.Delete(unpausedRoot, recursive: true);
            Directory.Delete(pausedRoot, recursive: true);
            Directory.Delete(explicitFalseRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PauseResumeAndNoOps_AreDurableAndNoOpsDoNotConsumeBytes()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity("session_v5_pause_resume/v1");
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
                root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                SnowGlobeWorldIdentity worldBefore = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).CaptureIdentity();
                SnowGlobeObserverControlResult paused = await session.PauseAsync();
                Assert.True(paused.Applied);
                Assert.True(paused.Snapshot!.IsPaused);
                Dictionary<string, byte[]> afterPause = ArtifactBytes(root);

                SnowGlobeObserverControlResult pauseNoOp = await session.PauseAsync();
                Assert.True(pauseNoOp.Applied);
                Assert.True(pauseNoOp.Snapshot!.IsPaused);
                AssertArtifactBytesEqual(afterPause, ArtifactBytes(root));

                SnowGlobeObserverControlResult resumed = await session.ResumeAsync();
                Assert.True(resumed.Applied);
                Assert.False(resumed.Snapshot!.IsPaused);
                Dictionary<string, byte[]> afterResume = ArtifactBytes(root);

                SnowGlobeObserverControlResult resumeNoOp = await session.ResumeAsync();
                Assert.True(resumeNoOp.Applied);
                Assert.False(resumeNoOp.Snapshot!.IsPaused);
                AssertArtifactBytesEqual(afterResume, ArtifactBytes(root));
                Assert.Equal(worldBefore, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).CaptureIdentity());
            }

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity));
            Assert.False(reopened.IsPaused);
            Assert.Equal(new[] { "Pause", "Resume" }, SnowGlobeRunStore.Read(root).Records.Select(record => record.Action));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MixedPauseParticipantPausedStepResumeAdvance_ReconstructsExactlyAndKeepsReceiptIdempotent()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity("session_v5_mixed/v1", agentCount: 2);
            SnowGlobeParticipantCommand command;
            SnowGlobeParticipantCommandReceipt accepted;
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
                root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                SnowGlobeObserverSnapshot initial = session.Inspect().Snapshot!;
                command = Command(initial, "mixed-command");
                Dictionary<string, byte[]> beforeUnpausedAttempt = ArtifactBytes(root);
                Assert.Equal("must_be_paused", (await session.SubmitParticipantCommandAsync(command)).RejectionReason);
                AssertArtifactBytesEqual(beforeUnpausedAttempt, ArtifactBytes(root));

                Assert.True((await session.PauseAsync()).Applied);
                accepted = await session.SubmitParticipantCommandAsync(command);
                Assert.True(accepted.Accepted);
                Assert.True((await session.StepAsync(new SnowGlobeObserverStepCommand(1))).Applied);
                Assert.True(session.IsPaused);
                Assert.True((await session.ResumeAsync()).Applied);

                Dictionary<string, byte[]> beforeIdempotentRetry = ArtifactBytes(root);
                Assert.Equal(accepted, await session.SubmitParticipantCommandAsync(command));
                AssertArtifactBytesEqual(beforeIdempotentRetry, ArtifactBytes(root));
                Assert.True((await session.AdvanceAsync()).Applied);
                Assert.False(session.IsPaused);
            }

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeInternalRunReconstruction durable = SnowGlobePersistedRun.ReconstructInternal(ledger, identity);
            Assert.False(durable.IsDurablyPaused);
            Assert.Equal(2, durable.Public.World.Tick);
            Assert.Equal(accepted, durable.Public.ParticipantReceipts[new("participant-01", "mixed-command")]);
            Assert.Equal(new[] { "Pause", "Resume" }, ledger.Records
                .Where(record => record.Kind == SnowGlobeLedgerKind.PauseTransition)
                .Select(record => record.Action));

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity));
            Assert.False(reopened.IsPaused);
            Assert.Equal(2, reopened.Inspect().Snapshot!.Tick);
            Dictionary<string, byte[]> beforeRetry = ArtifactBytes(root);
            Assert.Equal(accepted, await reopened.SubmitParticipantCommandAsync(command));
            AssertArtifactBytesEqual(beforeRetry, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FourArgumentReopen_IsReadOnlyRejectedForV5AndThreeArgumentV4DefaultsRunning()
    {
        string v5Root = NewTemporaryDirectory();
        string v4Root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity v5 = Identity("session_v5_reopen_overload/v1");
            using (SnowGlobePersistedSession.CreateNew(v5Root, v5, new IdleAdapter(v5.AdapterIdentity), isPaused: true)) { }
            Dictionary<string, byte[]> before = ArtifactBytes(v5Root);
            using (FileStream heldLease = new(
                Path.Combine(v5Root, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(
                    v5Root, v5, new IdleAdapter(v5.AdapterIdentity), isPaused: true));
                Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(
                    v5Root, v5, new IdleAdapter(v5.AdapterIdentity), isPaused: false));
            }
            AssertArtifactBytesEqual(before, ArtifactBytes(v5Root));

            SnowGlobeRunIdentity v4 = Identity("session_v4_reopen_compat/v1") with
            {
                SchemaVersion = SnowGlobeRunStore.V4SchemaVersion
            };
            using (SnowGlobeRunStore.CreateV4Fixture(v4Root, v4)) { }
            using (SnowGlobePersistedSession omitted = SnowGlobePersistedSession.Reopen(
                v4Root, v4, new IdleAdapter(v4.AdapterIdentity)))
            {
                Assert.False(omitted.IsPaused);
                Assert.True((await omitted.PauseAsync()).Applied);
                Assert.True(omitted.IsPaused);
                Assert.Empty(SnowGlobeRunStore.Read(v4Root).Records);
            }
            using (SnowGlobePersistedSession explicitPaused = SnowGlobePersistedSession.Reopen(
                v4Root, v4, new IdleAdapter(v4.AdapterIdentity), isPaused: true))
                Assert.True(explicitPaused.IsPaused);
            using (SnowGlobePersistedSession omittedAgain = SnowGlobePersistedSession.Reopen(
                v4Root, v4, new IdleAdapter(v4.AdapterIdentity)))
                Assert.False(omittedAgain.IsPaused);
        }
        finally
        {
            Directory.Delete(v5Root, recursive: true);
            Directory.Delete(v4Root, recursive: true);
        }
    }

    [Fact]
    public async Task PauseAppendUncertainty_PoisonsSessionAndCleanReopenUsesPriorState()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity("session_v5_pause_uncertainty/v1");
            SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
                root, identity, new IdleAdapter(identity.AdapterIdentity));
            Dictionary<string, byte[]> before = ArtifactBytes(root);
            try
            {
                session.BeforeLedgerAppendFlushForTesting = () => throw new IOException("deterministic_pause_prewrite_fault");
                SnowGlobeObserverControlResult result = await session.PauseAsync();
                Assert.False(result.Applied);
                Assert.Equal("session_coherence_lost", result.RejectionReason);
                Assert.Null(result.Snapshot);
                Assert.True(session.IsFailedClosed);
                AssertArtifactBytesEqual(before, ArtifactBytes(root));
            }
            finally { session.Dispose(); }

            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                Assert.False(reopened.IsPaused);
                Assert.Empty(SnowGlobeRunStore.Read(root).Records);
                Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConcurrentAdvanceMakesPauseBusyWithoutWritingOrPoisoning()
    {
        string root = NewTemporaryDirectory();
        BlockingAdapter? adapter = null;
        try
        {
            SnowGlobeRunIdentity identity = Identity("session_v5_pause_busy/v1", agentCount: 1);
            adapter = new BlockingAdapter(identity.AdapterIdentity);
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, adapter))
            {
                Task<SnowGlobeObserverControlResult> advancing = session.AdvanceAsync();
                await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Dictionary<string, byte[]> before = ArtifactBytes(root);

                SnowGlobeObserverControlResult busy = await session.PauseAsync();
                Assert.False(busy.Applied);
                Assert.Equal("operation_in_progress", busy.RejectionReason);
                Assert.Null(busy.Snapshot);
                Assert.False(session.IsFailedClosed);
                Assert.False(session.IsPaused);
                AssertArtifactBytesEqual(before, ArtifactBytes(root));

                adapter.Release.TrySetResult(true);
                Assert.True((await advancing.WaitAsync(TimeSpan.FromSeconds(5))).Applied);
            }
        }
        finally
        {
            adapter?.Release.TrySetResult(true);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CapacityFailureDoesNotWriteChangePauseOrPoison()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity("session_v5_pause_capacity/v1");
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true);
            Assert.True(session.IsPaused);
            session.ExhaustRunStoreCapacityForTesting();
            Dictionary<string, byte[]> before = ArtifactBytes(root);

            SnowGlobeObserverControlResult result = await session.ResumeAsync();
            Assert.False(result.Applied);
            Assert.Equal("run_store_capacity_exhausted", result.RejectionReason);
            Assert.True(result.Snapshot!.IsPaused);
            Assert.True(session.IsPaused);
            Assert.False(session.IsFailedClosed);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));

            Assert.True((await session.PauseAsync()).Applied);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void PersistedInspectorRemainsInertAndRecoveryProvenanceRemainsV4Only()
    {
        string runningRoot = NewTemporaryDirectory();
        string pausedRoot = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity running = Identity("session_v5_inspector_running/v1");
            using (SnowGlobePersistedSession.CreateNew(runningRoot, running, new IdleAdapter(running.AdapterIdentity))) { }
            SnowGlobeRunIdentity paused = Identity("session_v5_inspector_paused/v1");
            using (SnowGlobePersistedSession.CreateNew(pausedRoot, paused, new IdleAdapter(paused.AdapterIdentity), isPaused: true)) { }

            Assert.True(SnowGlobePersistedRunInspector.Inspect(runningRoot, running).Snapshot!.IsPaused);
            Assert.True(SnowGlobePersistedRunInspector.Inspect(pausedRoot, paused).Snapshot!.IsPaused);
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult provenance =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(pausedRoot, paused);
            Assert.True(provenance.Accepted);
            Assert.Null(provenance.Receipt);
        }
        finally
        {
            Directory.Delete(runningRoot, recursive: true);
            Directory.Delete(pausedRoot, recursive: true);
        }
    }

    private static SnowGlobeParticipantCommand Command(SnowGlobeObserverSnapshot snapshot, string idempotencyKey) => new(
        "participant-01",
        idempotencyKey,
        snapshot.Tick,
        snapshot.StateDigest,
        snapshot.EventDigest,
        "agent-00",
        SnowGlobeActionKind.Idle,
        0);

    private static SnowGlobeRunIdentity Identity(string adapterIdentity, int agentCount = 1) =>
        SnowGlobePersistedRun.Identity(adapterIdentity, seed: 240823, agentCount: agentCount);

    private static Dictionary<string, byte[]> ArtifactBytes(string root) => Directory.GetFiles(root)
        .ToDictionary(
            path => Path.GetFileName(path)!,
            path => Path.GetFileName(path) == ".writer.lock"
                ? BitConverter.GetBytes(new FileInfo(path).Length)
                : File.ReadAllBytes(path),
            StringComparer.Ordinal);

    private static void AssertArtifactBytesEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach ((string name, byte[] bytes) in expected) Assert.Equal(bytes, actual[name]);
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-session-v5-pause-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class IdleAdapter(string adapterIdentity) : ISnowGlobeIdentifiedInferenceAdapter
    {
        public string AdapterIdentity { get; } = adapterIdentity;

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(
            SnowGlobeObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }
    }

    private sealed class BlockingAdapter(string adapterIdentity) : ISnowGlobeIdentifiedInferenceAdapter
    {
        public string AdapterIdentity { get; } = adapterIdentity;
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<SnowGlobeActionProposal> ProposeAsync(
            SnowGlobeObservation observation,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle);
        }
    }
}
