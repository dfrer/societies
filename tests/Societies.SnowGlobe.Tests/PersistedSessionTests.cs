using System.Collections;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class PersistedSessionTests
{
    [Fact]
    public async Task MixedParticipantControlAndReopen_PreserveExactDurableSession()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_mixed/v1", seed: 301, agentCount: 2);
        try
        {
            SnowGlobeParticipantCommand before;
            SnowGlobeParticipantCommand between;
            SnowGlobeParticipantCommandReceipt beforeReceipt;
            SnowGlobeParticipantCommandReceipt betweenReceipt;
            SnowGlobeObserverSnapshot beforeClose;
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                before = Command(session, "participant-01", "before", SnowGlobeActionKind.Idle);
                beforeReceipt = await session.SubmitParticipantCommandAsync(before);
                Assert.True(beforeReceipt.Accepted);
                Assert.True((await session.StepAsync(new SnowGlobeObserverStepCommand(1))).Applied);

                between = Command(session, "participant-01", "between", SnowGlobeActionKind.Idle);
                betweenReceipt = await session.SubmitParticipantCommandAsync(between);
                Assert.True(betweenReceipt.Accepted);
                Assert.True((await session.ResumeAsync()).Applied);
                Assert.True((await session.AdvanceAsync()).Applied);
                beforeClose = session.Inspect().Snapshot!;
            }

            byte[] ledgerBeforeRetry = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                AssertSnapshotEqual(beforeClose, reopened.Inspect().Snapshot!);
                Assert.Equal(beforeReceipt, await reopened.SubmitParticipantCommandAsync(before));
                Assert.Equal(betweenReceipt, await reopened.SubmitParticipantCommandAsync(between));
                Assert.Equal(ledgerBeforeRetry, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
                Assert.True((await reopened.ResumeAsync()).Applied);
                Assert.True((await reopened.AdvanceAsync()).Applied);

                SnowGlobeRunReconstruction durable = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity);
                SnowGlobeObserverSnapshot live = reopened.Inspect().Snapshot!;
                Assert.Equal(durable.World.StateDigest(), live.StateDigest);
                Assert.Equal(durable.World.EventDigest(), live.EventDigest);
                Assert.Equal(durable.World.Tick, live.Tick);
                Assert.Equal(durable.World.Events.Count, live.EventHistoryCount);
                Assert.Equal(beforeReceipt, durable.ParticipantReceipts[new("participant-01", "before")]);
                Assert.Equal(betweenReceipt, durable.ParticipantReceipts[new("participant-01", "between")]);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task AcceptedStaleAndDomainRejectedCommands_ArePairScopedAndIdempotentAcrossReopen()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_receipts/v1", seed: 302, agentCount: 1);
        try
        {
            SnowGlobeParticipantCommand accepted;
            SnowGlobeParticipantCommand stale;
            SnowGlobeParticipantCommand domain;
            SnowGlobeParticipantCommandReceipt acceptedReceipt;
            SnowGlobeParticipantCommandReceipt staleReceipt;
            SnowGlobeParticipantCommandReceipt domainReceipt;
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                accepted = Command(session, "participant-01", "accepted", SnowGlobeActionKind.Idle);
                acceptedReceipt = await session.SubmitParticipantCommandAsync(accepted);
                SnowGlobeObserverSnapshot current = session.Inspect().Snapshot!;
                stale = Command(session, "participant-01", "stale", SnowGlobeActionKind.Idle) with { ExpectedTick = current.Tick + 1 };
                domain = Command(session, "participant-01", "domain", SnowGlobeActionKind.BuildShelter);
                staleReceipt = await session.SubmitParticipantCommandAsync(stale);
                domainReceipt = await session.SubmitParticipantCommandAsync(domain);
                Assert.True((await session.ResumeAsync()).Applied);
            }

            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                int bytesBefore = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length;
                Assert.Equal(acceptedReceipt, await reopened.SubmitParticipantCommandAsync(accepted));
                Assert.Equal(staleReceipt, await reopened.SubmitParticipantCommandAsync(stale));
                Assert.Equal(domainReceipt, await reopened.SubmitParticipantCommandAsync(domain));
                Assert.Equal("command_id_conflict", (await reopened.SubmitParticipantCommandAsync(accepted with { Quantity = 1 })).RejectionReason);
                Assert.Equal(bytesBefore, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length);

                Assert.True((await reopened.PauseAsync()).Applied);
                SnowGlobeObserverSnapshot current = reopened.Inspect().Snapshot!;
                SnowGlobeParticipantCommand otherParticipant = accepted with
                {
                    ParticipantId = "participant-02",
                    ExpectedTick = current.Tick,
                    ExpectedStateDigest = current.StateDigest,
                    ExpectedEventDigest = current.EventDigest
                };
                Assert.True((await reopened.SubmitParticipantCommandAsync(otherParticipant)).Accepted);
            }

            SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity);
            Assert.Equal("stale_tick", staleReceipt.RejectionReason);
            Assert.Equal("insufficient_resources_or_invalid_action", domainReceipt.RejectionReason);
            Assert.Equal(4, reconstruction.ParticipantReceipts.Count);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ParticipantTransientAdmissions_AreCanonicalBoundedAndNeverWritten()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_admission/v1", seed: 311, agentCount: 1);
        try
        {
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity));
            SnowGlobeParticipantCommand command = Command(session, "participant-01", "transient", SnowGlobeActionKind.Idle);
            Assert.Equal("must_be_paused", (await session.SubmitParticipantCommandAsync(command)).RejectionReason);
            Assert.Equal("participant_command_malformed", (await session.SubmitParticipantCommandAsync(null)).RejectionReason);
            Assert.Equal("participant_id_invalid", (await session.SubmitParticipantCommandAsync(command with { ParticipantId = "UPPER" })).RejectionReason);
            using (CancellationTokenSource cancelled = new())
            {
                cancelled.Cancel();
                Assert.Equal("operation_cancelled", (await session.SubmitParticipantCommandAsync(command, cancelled.Token)).RejectionReason);
            }
            Assert.Equal(0, SnowGlobeRunStore.Read(root).EntryCount);

            Assert.True((await session.PauseAsync()).Applied);
            SnowGlobeObserverSnapshot anchor = session.Inspect().Snapshot!;
            for (int index = 0; index < SnowGlobeRunStore.MaximumParticipantEvaluations; index++)
            {
                SnowGlobeParticipantCommand stale = new(
                    "participant-01",
                    $"capacity-{index:D3}",
                    anchor.Tick + 1,
                    anchor.StateDigest,
                    anchor.EventDigest,
                    "agent-00",
                    SnowGlobeActionKind.Idle,
                    0);
                Assert.Equal("stale_tick", (await session.SubmitParticipantCommandAsync(stale)).RejectionReason);
            }

            SnowGlobeParticipantCommand overflow = command with { IdempotencyKey = "overflow" };
            int bytesBefore = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length;
            Assert.Equal("idempotency_store_saturated", (await session.SubmitParticipantCommandAsync(overflow)).RejectionReason);
            Assert.Equal("idempotency_store_saturated", (await session.SubmitParticipantCommandAsync(overflow)).RejectionReason);
            Assert.Equal(bytesBefore, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length);
            Assert.Equal(SnowGlobeRunStore.MaximumParticipantEvaluations, SnowGlobeRunStore.Read(root).ParticipantEvaluationRecords.Count);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("snow_globe_run_store/v2")]
    [InlineData("snow_globe_run_store/v3")]
    public void MutableReopen_RejectsLegacyV2AndV3BeforeAnyWrite(string schemaVersion)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = schemaVersion == SnowGlobeRunStore.LegacySchemaVersion
                ? WriteEmptyV2(root)
                : WriteEmptyV3(root);
            Dictionary<string, byte[]> before = Directory.GetFiles(root)
                .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);

            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(root, identity, new IdleAdapter(identity.AdapterIdentity)));

            Assert.False(File.Exists(Path.Combine(root, ".writer.lock")));
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(root).Select(Path.GetFileName).Order());
            foreach ((string file, byte[] bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, file)));
            Assert.Equal(identity, SnowGlobeRunStore.Read(root).Identity);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task BlockedInference_MakesInspectAndParticipantBusyWithoutWriting()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_blocked/v1", seed: 303, agentCount: 1);
        BlockingAdapter adapter = new(identity.AdapterIdentity);
        try
        {
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, adapter);
            SnowGlobeParticipantCommand command = Command(session, "participant-01", "busy", SnowGlobeActionKind.Idle);
            Task<SnowGlobeObserverControlResult> advancing = session.AdvanceAsync();
            await adapter.Entered;

            Assert.Equal("operation_in_progress", session.Inspect().RejectionReason);
            SnowGlobeParticipantCommandReceipt busy = await session.SubmitParticipantCommandAsync(command);
            Assert.Equal("operation_in_progress", busy.RejectionReason);
            Assert.Null(busy.ParticipantId);
            Assert.Null(busy.IdempotencyKey);
            Assert.Null(busy.ResultingTick);
            Assert.Null(busy.ResultingEventSequence);
            Assert.Null(busy.ResultingStateDigest);
            Assert.Null(busy.ResultingEventDigest);
            using (CancellationTokenSource cancelled = new())
            {
                cancelled.Cancel();
                SnowGlobeParticipantCommandReceipt cancelledWhileBusy = await session.SubmitParticipantCommandAsync(command, cancelled.Token);
                Assert.Equal("operation_in_progress", cancelledWhileBusy.RejectionReason);
                Assert.Null(cancelledWhileBusy.ParticipantId);
                Assert.Null(cancelledWhileBusy.IdempotencyKey);
                Assert.Null(cancelledWhileBusy.ResultingTick);
                Assert.Null(cancelledWhileBusy.ResultingStateDigest);
            }
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
            Assert.Empty(SnowGlobeRunStore.Read(root).ParticipantEvaluationRecords);

            adapter.Release();
            Assert.True((await advancing).Applied);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(SessionFailure.Adapter, 0)]
    [InlineData(SessionFailure.Adapter, 1)]
    [InlineData(SessionFailure.Adapter, 2)]
    [InlineData(SessionFailure.Cancellation, 0)]
    [InlineData(SessionFailure.Cancellation, 1)]
    [InlineData(SessionFailure.Cancellation, 2)]
    [InlineData(SessionFailure.Validation, 0)]
    [InlineData(SessionFailure.Validation, 1)]
    [InlineData(SessionFailure.Validation, 2)]
    public async Task ManagedFailureAtEveryOrdinal_RestoresCheckpointAndSessionRemainsReusable(SessionFailure failure, int ordinal)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity($"persisted_session_failure_{failure.ToString().ToLowerInvariant()}_{ordinal}/v1", seed: 304, agentCount: 3);
        using CancellationTokenSource cancellation = new();
        OneShotFailingAdapter adapter = new(identity.AdapterIdentity, failure, ordinal, cancellation);
        try
        {
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, adapter);
            SnowGlobeObserverSnapshot before = session.Inspect().Snapshot!;
            SnowGlobeObserverControlResult failed = await session.AdvanceAsync(cancellationToken: cancellation.Token);

            Assert.False(failed.Applied);
            Assert.Equal(failure == SessionFailure.Cancellation ? "operation_cancelled" : "scheduler_failure", failed.RejectionReason);
            AssertSnapshotEqual(before, failed.Snapshot!);
            AssertSnapshotEqual(before, session.Inspect().Snapshot!);
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
            Assert.False(session.IsFailedClosed);

            SnowGlobeObserverControlResult retry = await session.AdvanceAsync();
            Assert.True(retry.Applied);
            Assert.Equal(1, retry.Snapshot!.Tick);
            Assert.Equal(3, retry.Snapshot.EventHistoryCount);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ParticipantThenScheduledRun_HasOneContiguousReplayGrammar()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_grammar/v1", seed: 305, agentCount: 2);
        try
        {
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                Assert.True((await session.SubmitParticipantCommandAsync(Command(session, "participant-01", "first", SnowGlobeActionKind.Idle))).Accepted);
                Assert.True((await session.ResumeAsync()).Applied);
                Assert.True((await session.AdvanceAsync()).Applied);
            }

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            int[] sequences = ledger.Records.Select(record => record.Sequence)
                .Concat(ledger.ParticipantEvaluationRecords.Select(record => record.Sequence))
                .Order()
                .ToArray();
            Assert.Equal(Enumerable.Range(0, ledger.EntryCount), sequences);
            Assert.Equal(SnowGlobeLedgerKind.ParticipantEvaluation, ledger.ParticipantEvaluationRecords.Single().Kind);
            Assert.Equal(1, ledger.ParticipantEvaluationRecords.Single().Sequence);
            Assert.Equal(SnowGlobeLedgerKind.PauseTransition, ledger.Records.OrderBy(record => record.Sequence).First().Kind);
            Assert.Equal(SnowGlobeLedgerKind.Checkpoint, ledger.Records.OrderBy(record => record.Sequence).Last().Kind);
            SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(ledger, identity);
            Assert.Equal(3, reconstruction.World.Events.Count);
            Assert.Equal(Enumerable.Range(0, 3), reconstruction.World.Events.Select(entry => entry.Sequence));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Inspection_IsBoundedPagedDetachedAndUsesCachedFullDigests()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_pages/v1", seed: 306, agentCount: 64);
        try
        {
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity));
            Assert.True((await session.AdvanceAsync(2)).Applied);
            SnowGlobeObserverDiagnostics before = session.GetDiagnostics();
            SnowGlobeObserverSnapshot first = session.Inspect(0).Snapshot!;
            SnowGlobeObserverSnapshot middle = session.Inspect(32).Snapshot!;
            SnowGlobeObserverSnapshot last = session.Inspect(96).Snapshot!;

            Assert.Equal(128, first.EventHistoryCount);
            Assert.Equal(32, first.CanonicalEvents.Count);
            Assert.Equal(32, first.NextEventCursor);
            Assert.Equal(32, middle.CanonicalEvents.Count);
            Assert.Equal(64, middle.NextEventCursor);
            Assert.Equal(32, last.CanonicalEvents.Count);
            Assert.Null(last.NextEventCursor);
            Assert.Equal(first.StateDigest, last.StateDigest);
            Assert.Equal(first.EventDigest, last.EventDigest);
            Assert.Equal("event_cursor_invalid", session.Inspect(129).RejectionReason);
            Assert.Throws<NotSupportedException>(() => ((IList)first.CanonicalEvents).RemoveAt(0));
            SnowGlobeObserverDiagnostics after = session.GetDiagnostics();
            Assert.Equal(before.FullHistoryDigestRefreshes, after.FullHistoryDigestRefreshes);
            Assert.Equal(before.ProjectedEventEntries + 96, after.ProjectedEventEntries);

            Assert.True((await session.AdvanceAsync()).Applied);
            Assert.Equal(128, first.EventHistoryCount);
            Assert.Equal(2, first.Tick);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PostAppendLiveMismatch_FailsClosedUntilCleanReopen()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_fail_closed/v1", seed: 307, agentCount: 1);
        try
        {
            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                session.AfterDurableMutationForTesting = world => world.AdvanceTick();
                SnowGlobeObserverControlResult result = await session.AdvanceAsync();
                Assert.False(result.Applied);
                Assert.Equal("session_coherence_lost", result.RejectionReason);
                Assert.Null(result.Snapshot);
                Assert.True(session.IsFailedClosed);
                Assert.Equal("session_coherence_lost", session.Inspect().RejectionReason);
                Assert.Equal("session_coherence_lost", (await session.PauseAsync()).RejectionReason);
                SnowGlobeParticipantCommandReceipt malformedAfterFailure = await session.SubmitParticipantCommandAsync(null);
                Assert.Equal("session_coherence_lost", malformedAfterFailure.RejectionReason);
                Assert.Null(malformedAfterFailure.ResultingTick);
                Assert.Null(malformedAfterFailure.ResultingStateDigest);
            }

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(root, identity, new IdleAdapter(identity.AdapterIdentity));
            Assert.False(reopened.IsFailedClosed);
            Assert.Equal(1, reopened.Inspect().Snapshot!.Tick);
            Assert.True((await reopened.AdvanceAsync()).Applied);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PostAppendLedgerTamper_FailsClosedAndNeverServesPartialState()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_tamper/v1", seed: 308, agentCount: 1);
        try
        {
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity));
            session.AfterDurableMutationForTesting = _ => File.AppendAllText(Path.Combine(root, "ledger.jsonl"), "tamper\n", Encoding.UTF8);
            SnowGlobeObserverControlResult result = await session.AdvanceAsync();

            Assert.False(result.Applied);
            Assert.Equal("session_coherence_lost", result.RejectionReason);
            Assert.Null(result.Snapshot);
            Assert.True(session.IsFailedClosed);
            Assert.False(session.Inspect().Accepted);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(SessionAppendFault.Scheduled)]
    [InlineData(SessionAppendFault.Participant)]
    public async Task PreWriteAppendFault_PoisonsSessionAndCleanReopenUsesUnchangedBytes(SessionAppendFault path)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            $"persisted_session_poison_{path.ToString().ToLowerInvariant()}/v1",
            seed: 312,
            agentCount: 1);
        byte[] before;
        SnowGlobeParticipantCommand command;
        SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
            root,
            identity,
            new IdleAdapter(identity.AdapterIdentity),
            isPaused: true);
        try
        {
            before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            command = Command(session, "participant-01", "poisoned", SnowGlobeActionKind.Idle);
            session.BeforeLedgerAppendFlushForTesting = () => throw new IOException("deterministic_pre_write_fault");

            if (path == SessionAppendFault.Scheduled)
            {
                SnowGlobeObserverControlResult failed = await session.StepAsync(new SnowGlobeObserverStepCommand(1));
                Assert.Equal("session_coherence_lost", failed.RejectionReason);
                Assert.Null(failed.Snapshot);
            }
            else
            {
                SnowGlobeParticipantCommandReceipt failed = await session.SubmitParticipantCommandAsync(command);
                Assert.Equal("session_coherence_lost", failed.RejectionReason);
                Assert.Null(failed.ResultingStateDigest);
            }

            Assert.True(session.IsFailedClosed);
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal("session_coherence_lost", session.Inspect().RejectionReason);
            Assert.Null(session.Inspect().Snapshot);
            Assert.Equal("session_coherence_lost", (await session.StepAsync(new SnowGlobeObserverStepCommand(1))).RejectionReason);
            Assert.Equal("session_coherence_lost", (await session.SubmitParticipantCommandAsync(command)).RejectionReason);
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally
        {
            session.Dispose();
        }

        try
        {
            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root,
                identity,
                new IdleAdapter(identity.AdapterIdentity));
            Assert.Equal(0, reopened.Inspect().Snapshot!.Tick);
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            if (path == SessionAppendFault.Scheduled)
                Assert.True((await reopened.StepAsync(new SnowGlobeObserverStepCommand(1))).Applied);
            else
                Assert.True((await reopened.SubmitParticipantCommandAsync(command)).Accepted);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AdapterProvenanceMismatch_FailsBeforeCreateOrReopenArtifactsChange()
    {
        string parent = NewTemporaryDirectory();
        string createRoot = Path.Combine(parent, "not-created");
        string reopenRoot = Path.Combine(parent, "reopen");
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_provenance/v1", seed: 313, agentCount: 1);
        try
        {
            Assert.Equal(ScriptedInferenceAdapter.Identity, new ScriptedInferenceAdapter().AdapterIdentity);
            Assert.Equal(SnowGlobeResilienceFallbackAdapter.Identity, new SnowGlobeResilienceFallbackAdapter().AdapterIdentity);
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.CreateNew(
                createRoot,
                identity,
                new IdleAdapter("wrong_adapter/v1")));
            Assert.False(Directory.Exists(createRoot));
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.CreateNew(
                createRoot,
                identity,
                new IdleAdapter("NON CANONICAL")));
            Assert.False(Directory.Exists(createRoot));

            Directory.CreateDirectory(reopenRoot);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(reopenRoot, identity)) { }
            SnowGlobeRunLedger emptyLedger = SnowGlobeRunStore.Read(reopenRoot);
            Assert.Equal(identity.AdapterIdentity, new SnowGlobeReplayAdapter(emptyLedger, identity.AdapterIdentity).AdapterIdentity);
            File.Delete(Path.Combine(reopenRoot, ".writer.lock"));
            Dictionary<string, byte[]> before = Directory.GetFiles(reopenRoot)
                .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);

            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(
                reopenRoot,
                identity,
                new IdleAdapter("wrong_adapter/v1")));
            Assert.False(File.Exists(Path.Combine(reopenRoot, ".writer.lock")));
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(reopenRoot).Select(path => Path.GetFileName(path)!).Order());
            foreach ((string file, byte[] bytes) in before)
                Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(reopenRoot, file)));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public async Task ReentrantDisposeFromInference_DoesNotDeadlockAndEventuallyReleasesStore()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_reentrant_dispose/v1", seed: 314, agentCount: 1);
        ReentrantDisposeAdapter adapter = new(identity.AdapterIdentity);
        SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, adapter);
        adapter.Session = session;
        try
        {
            SnowGlobeObserverControlResult result = await session.AdvanceAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(result.Applied);
            Assert.Equal("operation_cancelled", result.RejectionReason);
            Assert.Equal(0, result.Snapshot!.Tick);
            Assert.True(session.IsDisposed);
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
            Assert.Throws<ObjectDisposedException>(() => session.Inspect());

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root,
                identity,
                new IdleAdapter(identity.AdapterIdentity));
            Assert.Equal(0, reopened.Inspect().Snapshot!.Tick);
        }
        finally
        {
            session.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDispose_IsNonthrowingIdempotentAndReleasesStore(bool blockedInference)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            blockedInference ? "persisted_session_dispose_blocked/v1" : "persisted_session_dispose_idle/v1",
            seed: blockedInference ? 315 : 316,
            agentCount: 1);
        BlockingAdapter? blockingAdapter = blockedInference ? new BlockingAdapter(identity.AdapterIdentity) : null;
        ISnowGlobeIdentifiedInferenceAdapter adapter = blockingAdapter is not null
            ? blockingAdapter
            : new IdleAdapter(identity.AdapterIdentity);
        SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
            root,
            identity,
            adapter);
        Task<SnowGlobeObserverControlResult>? advancing = null;
        try
        {
            if (blockingAdapter is not null)
            {
                advancing = session.AdvanceAsync();
                await blockingAdapter.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            }

            using ManualResetEventSlim start = new(false);
            Task[] disposers = Enumerable.Range(0, 64)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    session.Dispose();
                    session.Dispose();
                }))
                .ToArray();
            start.Set();
            await Task.WhenAll(disposers).WaitAsync(TimeSpan.FromSeconds(5));

            if (advancing is not null)
            {
                SnowGlobeObserverControlResult cancelled = await advancing.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(cancelled.Applied);
                Assert.Equal("operation_cancelled", cancelled.RejectionReason);
                Assert.Equal(0, cancelled.Snapshot!.Tick);
            }

            Assert.True(session.IsDisposed);
            for (int index = 0; index < 16; index++) session.Dispose();
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
            Assert.Throws<ObjectDisposedException>(() => session.Inspect());

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root,
                identity,
                new IdleAdapter(identity.AdapterIdentity));
            Assert.Equal(0, reopened.Inspect().Snapshot!.Tick);
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
        }
        finally
        {
            blockingAdapter?.Release();
            session.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EmptyRunBoundsExactIdentityAndDisposal_AreFailClosedAndRecoverable()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("persisted_session_empty/v1", seed: 309, agentCount: 1);
        try
        {
            SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity));
            SnowGlobeObserverSnapshot empty = session.Inspect().Snapshot!;
            Assert.Equal(0, empty.Tick);
            Assert.Equal(0, empty.EventHistoryCount);
            Assert.Empty(empty.CanonicalEvents);
            Assert.Equal("step_count_must_be_positive", (await session.AdvanceAsync(0)).RejectionReason);
            Assert.Equal("step_count_exceeds_bound", (await session.AdvanceAsync(SnowGlobeObserverShell.MaximumStepTicks + 1)).RejectionReason);
            Assert.Equal("must_be_paused", (await session.StepAsync(new SnowGlobeObserverStepCommand(1))).RejectionReason);
            Assert.Equal("step_command_malformed", (await session.StepAsync(null)).RejectionReason);
            session.Dispose();
            session.Dispose();
            Assert.Throws<ObjectDisposedException>(() => session.Inspect());

            SnowGlobeRunIdentity wrong = identity with { AdapterIdentity = "wrong_adapter/v1" };
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(root, wrong, new IdleAdapter(wrong.AdapterIdentity)));
            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(root, identity, new IdleAdapter(identity.AdapterIdentity));
            Assert.Equal(empty.StateDigest, reopened.Inspect().Snapshot!.StateDigest);
            Assert.Empty(SnowGlobeRunStore.Read(root).Records);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static SnowGlobeParticipantCommand Command(
        SnowGlobePersistedSession session,
        string participantId,
        string key,
        SnowGlobeActionKind action,
        int quantity = 0)
    {
        SnowGlobeObserverSnapshot snapshot = session.Inspect().Snapshot!;
        return new SnowGlobeParticipantCommand(
            participantId,
            key,
            snapshot.Tick,
            snapshot.StateDigest,
            snapshot.EventDigest,
            "agent-00",
            action,
            quantity);
    }

    private static void AssertSnapshotEqual(SnowGlobeObserverSnapshot expected, SnowGlobeObserverSnapshot actual)
    {
        Assert.Equal(expected.IsPaused, actual.IsPaused);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.AvailableWood, actual.AvailableWood);
        Assert.Equal(expected.AvailableStone, actual.AvailableStone);
        Assert.Equal(expected.StockpileWood, actual.StockpileWood);
        Assert.Equal(expected.StockpileStone, actual.StockpileStone);
        Assert.Equal(expected.Agents, actual.Agents);
        Assert.Equal(expected.Structures, actual.Structures);
        Assert.Equal(expected.EventHistoryCount, actual.EventHistoryCount);
        Assert.Equal(expected.EventCursor, actual.EventCursor);
        Assert.Equal(expected.NextEventCursor, actual.NextEventCursor);
        Assert.Equal(expected.CanonicalEvents, actual.CanonicalEvents);
        Assert.Equal(expected.StateDigest, actual.StateDigest);
        Assert.Equal(expected.EventDigest, actual.EventDigest);
    }

    private static SnowGlobeRunIdentity WriteEmptyV2(string root)
    {
        SnowGlobeRunIdentity identity = new(
            SnowGlobeRunStore.LegacySchemaVersion,
            SnowGlobePersistedRun.RulesIdentity,
            SnowGlobePersistedRun.PromptIdentity,
            "frozen_v2_session/v1",
            310,
            1);
        string header = "{\"schema_version\":\"snow_globe_run_store/v2\",\"rules_identity\":\"snow_globe_domain_rules/v1\",\"prompt_identity\":\"normalized_values_only/no_participant_text/v1\",\"adapter_identity\":\"frozen_v2_session/v1\",\"seed\":310,\"agent_count\":1}";
        File.WriteAllText(Path.Combine(root, "run.json"), header, new UTF8Encoding(false));
        File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), Array.Empty<byte>());
        return identity;
    }

    private static SnowGlobeRunIdentity WriteEmptyV3(string root)
    {
        SnowGlobeRunIdentity identity = new(
            SnowGlobeRunStore.PreviousSchemaVersion,
            SnowGlobePersistedRun.RulesIdentity,
            SnowGlobePersistedRun.PromptIdentity,
            "frozen_v3_session/v1",
            311,
            1,
            SnowGlobeRunStore.ParticipantCommandIdentity);
        string header = "{\"schema_version\":\"snow_globe_run_store/v3\",\"rules_identity\":\"snow_globe_domain_rules/v1\",\"prompt_identity\":\"normalized_values_only/no_participant_text/v1\",\"adapter_identity\":\"frozen_v3_session/v1\",\"seed\":311,\"agent_count\":1,\"participant_command_identity\":\"snow_globe_participant_command/v1\"}";
        File.WriteAllText(Path.Combine(root, "run.json"), header, new UTF8Encoding(false));
        File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), Array.Empty<byte>());
        return identity;
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-session-" + Guid.NewGuid().ToString("N"));
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
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public string AdapterIdentity { get; } = adapterIdentity;

        public async ValueTask<SnowGlobeActionProposal> ProposeAsync(
            SnowGlobeObservation observation,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle);
        }

        public void Release() => _release.TrySetResult(true);
    }

    private sealed class ReentrantDisposeAdapter(string adapterIdentity) : ISnowGlobeIdentifiedInferenceAdapter
    {
        public string AdapterIdentity { get; } = adapterIdentity;
        public SnowGlobePersistedSession? Session { get; set; }

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(
            SnowGlobeObservation observation,
            CancellationToken cancellationToken)
        {
            Session!.Dispose();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }
    }

    private sealed class OneShotFailingAdapter(
        string adapterIdentity,
        SessionFailure failure,
        int failingOrdinal,
        CancellationTokenSource cancellation) : ISnowGlobeIdentifiedInferenceAdapter
    {
        private int _calls;
        private int _failed;
        public string AdapterIdentity { get; } = adapterIdentity;

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(
            SnowGlobeObservation observation,
            CancellationToken cancellationToken)
        {
            int ordinal = _calls++;
            if (ordinal == failingOrdinal && Interlocked.Exchange(ref _failed, 1) == 0)
            {
                return failure switch
                {
                    SessionFailure.Adapter => ValueTask.FromException<SnowGlobeActionProposal>(new InvalidOperationException("fixture_failure")),
                    SessionFailure.Cancellation => Cancel(cancellation, cancellationToken),
                    SessionFailure.Validation => ValueTask.FromResult(new SnowGlobeActionProposal("arbitrary text", SnowGlobeActionKind.Idle)),
                    _ => throw new ArgumentOutOfRangeException(nameof(failure))
                };
            }
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }

        private static ValueTask<SnowGlobeActionProposal> Cancel(
            CancellationTokenSource cancellation,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation did not propagate.");
        }
    }

    public enum SessionFailure { Adapter, Cancellation, Validation }
    public enum SessionAppendFault { Scheduled, Participant }
}
