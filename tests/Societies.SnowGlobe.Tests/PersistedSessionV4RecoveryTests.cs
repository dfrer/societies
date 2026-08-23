using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

/// <summary>
/// Public-session conformance over physical v4 interruption artifacts. The internal RunStore
/// filesystem seam is deliberately confined to the artifact helpers below; recovery itself is
/// always exercised through SnowGlobePersistedSession.Reopen.
/// </summary>
public sealed class PersistedSessionV4RecoveryTests
{
    [Fact]
    public async Task Reopen_AbandonsAuthenticatedNonemptyScheduledPrefix_PreservesReceiptAndProgresses()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            "persisted_session_v4_prefix_recovery/v1", seed: 401, agentCount: 64);
        try
        {
            (SnowGlobeParticipantCommand command, SnowGlobeParticipantCommandReceipt receipt) =
                await CreatePausedSessionReceiptAsync(root, identity, "prefix-receipt");
            await CreateInterruptedScheduledArtifactAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 30000);

            byte[] sourceLedger = ReadSourceLedger(root);
            byte[] sourceMarkers = ReadSourceMarkers(root);
            (RunStoreCommitMarker previousCommit, RunStorePrepareMarker prepare) = ReadPendingMarkers(root);
            byte[] scheduledPrefix = sourceLedger[previousCommit.LedgerEndOffset..];
            SnowGlobeRunReconstruction prior = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity);
            Assert.Equal(0, prior.World.Tick);
            Assert.Equal(receipt, prior.ParticipantReceipts[new(command.ParticipantId!, command.IdempotencyKey!)]);
            Assert.NotEmpty(scheduledPrefix);
            Assert.True(scheduledPrefix.Length < prepare.PayloadLength);
            Assert.Contains($"{scheduledPrefix.Length}:{Digest(scheduledPrefix)}", prepare.PayloadPrefixManifest, StringComparison.Ordinal);

            using SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true);

            AssertSourceUnchanged(root, sourceLedger, sourceMarkers);
            Assert.True(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.True(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
            AssertSnapshotMatchesReconstruction(reopened.Inspect().Snapshot!, root, identity, isPaused: true);
            Assert.Equal(receipt, await reopened.SubmitParticipantCommandAsync(command));
            Assert.Equal(receipt, SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity)
                .ParticipantReceipts[new(command.ParticipantId!, command.IdempotencyKey!)]);

            SnowGlobeObserverControlResult advanced = await reopened.StepAsync(new SnowGlobeObserverStepCommand(1));
            Assert.True(advanced.Applied);
            Assert.Equal(1, advanced.Snapshot!.Tick);
            AssertSnapshotMatchesReconstruction(advanced.Snapshot, root, identity, isPaused: true);
            AssertSourceUnchanged(root, sourceLedger, sourceMarkers);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reopen_AdoptsCompleteScheduledPayloadOnlyAfterContinuation_PreservesReceiptAndIsStable()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            "persisted_session_v4_commit_gap_recovery/v1", seed: 402, agentCount: 1);
        try
        {
            (SnowGlobeParticipantCommand command, SnowGlobeParticipantCommandReceipt receipt) =
                await CreatePausedSessionReceiptAsync(root, identity, "commit-gap-receipt");
            await CreateInterruptedScheduledArtifactAsync(root, identity, RunStoreWriteKind.CommitMarker, 0);

            byte[] sourceLedger = ReadSourceLedger(root);
            byte[] sourceMarkers = ReadSourceMarkers(root);
            (RunStoreCommitMarker previousCommit, RunStorePrepareMarker prepare) = ReadPendingMarkers(root);
            Assert.Equal(prepare.PayloadLength, sourceLedger.Length - previousCommit.LedgerEndOffset);
            Assert.Equal(0, SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity).World.Tick);

            SnowGlobeObserverSnapshot recoveredSnapshot;
            using (SnowGlobePersistedSession reopened = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                // The mutable session is not returned until its append open durably records recovery.
                Assert.True(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
                Assert.True(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
                recoveredSnapshot = reopened.Inspect().Snapshot!;
                Assert.Equal(1, recoveredSnapshot.Tick);
                AssertSnapshotMatchesReconstruction(recoveredSnapshot, root, identity, isPaused: true);
                Assert.Equal(receipt, await reopened.SubmitParticipantCommandAsync(command));
                Assert.Equal(receipt, SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity)
                    .ParticipantReceipts[new(command.ParticipantId!, command.IdempotencyKey!)]);
            }

            AssertSourceUnchanged(root, sourceLedger, sourceMarkers);
            byte[] continuationLedger = File.ReadAllBytes(Path.Combine(root, "ledger.0001.jsonl"));
            byte[] continuationMarkers = File.ReadAllBytes(Path.Combine(root, "commits.0001.jsonl"));
            using (SnowGlobePersistedSession reopenedAgain = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                AssertSnapshotsEqual(recoveredSnapshot, reopenedAgain.Inspect().Snapshot!);
                Assert.Equal(receipt, await reopenedAgain.SubmitParticipantCommandAsync(command));
            }

            AssertSourceUnchanged(root, sourceLedger, sourceMarkers);
            Assert.Equal(continuationLedger, File.ReadAllBytes(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.Equal(continuationMarkers, File.ReadAllBytes(Path.Combine(root, "commits.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("invalid_utf8")]
    [InlineData("nul")]
    [InlineData("garbage")]
    public async Task Reopen_RejectsUnauthenticatedPendingResidueBeforeWriterOwnership(string mutation)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            $"persisted_session_v4_unauthenticated_{mutation}/v1", seed: 403, agentCount: 64);
        try
        {
            await CreateEmptySessionAsync(root, identity);
            await CreateInterruptedScheduledArtifactAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 30000);
            AppendBytes(Path.Combine(root, "ledger.jsonl"), mutation switch
            {
                "invalid_utf8" => [0xff, 0xfe],
                "nul" => [0x00],
                "garbage" => Encoding.UTF8.GetBytes("{\"noncanonical\":"),
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            });
            Dictionary<string, byte[]> before = CaptureDirectoryBytes(root);

            using (FileStream heldWriterLease = new(
                Path.Combine(root, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(
                    root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true));
            }

            AssertDirectoryBytesUnchanged(root, before);
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reopen_RejectsSecondScheduledInterruptionAfterOneSuccessfulRecoveryWithoutFurtherMutation()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(
            "persisted_session_v4_second_recovery_bound/v1", seed: 404, agentCount: 1);
        try
        {
            await CreateEmptySessionAsync(root, identity);
            await CreateInterruptedScheduledArtifactAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 0);
            using (SnowGlobePersistedSession recovered = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true))
            {
                Assert.Equal(0, recovered.Inspect().Snapshot!.Tick);
                Assert.True(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            }

            await CreateInterruptedScheduledArtifactAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 0);
            Dictionary<string, byte[]> before = CaptureDirectoryBytes(root);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true));

            AssertDirectoryBytesUnchanged(root, before);
            Assert.False(File.Exists(Path.Combine(root, "ledger.0002.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0002.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task CreateEmptySessionAsync(string root, SnowGlobeRunIdentity identity)
    {
        using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
            root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true);
        await Task.CompletedTask;
    }

    private static async Task<(SnowGlobeParticipantCommand Command, SnowGlobeParticipantCommandReceipt Receipt)>
        CreatePausedSessionReceiptAsync(string root, SnowGlobeRunIdentity identity, string idempotencyKey)
    {
        using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(
            root, identity, new IdleAdapter(identity.AdapterIdentity), isPaused: true);
        SnowGlobeObserverSnapshot snapshot = session.Inspect().Snapshot!;
        SnowGlobeParticipantCommand command = new(
            "participant-01", idempotencyKey, snapshot.Tick, snapshot.StateDigest, snapshot.EventDigest,
            "agent-00", SnowGlobeActionKind.Idle, 0);
        SnowGlobeParticipantCommandReceipt receipt = await session.SubmitParticipantCommandAsync(command);
        Assert.True(receipt.Accepted);
        return (command, receipt);
    }

    // Internal only: composes the exact physical interruption. Public tests enter recovery only via Reopen.
    private static async Task CreateInterruptedScheduledArtifactAsync(
        string root,
        SnowGlobeRunIdentity identity,
        RunStoreWriteKind faultKind,
        int bytesBeforeFailure)
    {
        IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
            PhysicalRunStoreFileSystem.Instance, faultKind, bytesBeforeFailure);
        SnowGlobeRunStore? interrupted = SnowGlobeRunStore.OpenForAppend(root, faulting);
        try
        {
            SnowGlobeWorld prior = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(
                prior, new IdleAdapter(identity.AdapterIdentity), interrupted, prior.Tick + 1));
            Assert.True(interrupted.IsPoisoned);
        }
        finally { interrupted.Dispose(); }
    }

    private static void AssertSnapshotMatchesReconstruction(
        SnowGlobeObserverSnapshot actual,
        string root,
        SnowGlobeRunIdentity identity,
        bool isPaused)
    {
        SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity);
        SnowGlobeWorldIdentity worldIdentity = reconstruction.World.CaptureIdentity();
        SnowGlobeObserverSnapshot expected = SnowGlobeObserverShell.CreateDetachedSnapshot(
            reconstruction.World, isPaused, 0, worldIdentity.StateDigest, worldIdentity.EventDigest,
            worldIdentity.Revision, out _)!;
        AssertSnapshotsEqual(expected, actual);
    }

    private static void AssertSnapshotsEqual(SnowGlobeObserverSnapshot expected, SnowGlobeObserverSnapshot actual)
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

    private static byte[] ReadSourceLedger(string root) => File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
    private static byte[] ReadSourceMarkers(string root) => File.ReadAllBytes(Path.Combine(root, "commits.jsonl"));

    private static (RunStoreCommitMarker PreviousCommit, RunStorePrepareMarker Prepare) ReadPendingMarkers(string root)
    {
        string[] lines = File.ReadAllLines(Path.Combine(root, "commits.jsonl"));
        return (
            JsonSerializer.Deserialize<RunStoreCommitMarker>(lines[^2], JsonOptions)!,
            JsonSerializer.Deserialize<RunStorePrepareMarker>(lines[^1], JsonOptions)!);
    }

    private static void AssertSourceUnchanged(string root, byte[] ledger, byte[] markers)
    {
        Assert.Equal(ledger, ReadSourceLedger(root));
        Assert.Equal(markers, ReadSourceMarkers(root));
    }

    private static void AppendBytes(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
    }

    private static Dictionary<string, byte[]> CaptureDirectoryBytes(string root) => Directory.GetFiles(root)
        .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AssertDirectoryBytesUnchanged(string root, IReadOnlyDictionary<string, byte[]> expected)
    {
        Dictionary<string, byte[]> actual = CaptureDirectoryBytes(root);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach ((string name, byte[] bytes) in expected) Assert.Equal(bytes, actual[name]);
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-session-v4-recovery-" + Guid.NewGuid().ToString("N"));
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
}
