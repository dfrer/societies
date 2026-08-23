using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class RunStoreV5PauseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void V5CreateAndTransitions_HaveExactWireShapeAndDoNotChangeWorldAuthority()
    {
        string root = NewTemporaryDirectory();
        try
        {
            Assert.Equal("snow_globe_run_store/v5", SnowGlobeRunStore.SchemaVersion);
            Assert.Equal("snow_globe_run_store/v4", SnowGlobeRunStore.V4SchemaVersion);
            Assert.Equal("snow_globe_run_store/v3", SnowGlobeRunStore.PreviousSchemaVersion);
            Assert.Equal(Enumerable.Range(0, 7), Enum.GetValues<SnowGlobeLedgerKind>().Select(value => (int)value));
            Assert.Equal(1, RunStoreV4Storage.MaximumRecoveryCount);
            Assert.Equal(2, RunStoreV4Storage.MaximumSegments);

            SnowGlobeRunIdentity identity = Identity("runstore_v5_shape/v1");
            SnowGlobeWorldIdentity initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount).CaptureIdentity();
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                Assert.Equal(RunStorePauseAppendResult.Appended, store.AppendPauseTransition(paused: true));
                Assert.Equal(RunStorePauseAppendResult.AlreadyInTargetState, store.AppendPauseTransition(paused: true));
                Assert.Equal(RunStorePauseAppendResult.Appended, store.AppendPauseTransition(paused: false));
            }

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeLedgerRecord[] transitions = ledger.Records.ToArray();
            Assert.Equal(2, transitions.Length);
            Assert.Equal(new[] { "Pause", "Resume" }, transitions.Select(record => record.Action));
            Assert.All(transitions, record =>
            {
                Assert.Equal(SnowGlobeLedgerKind.PauseTransition, record.Kind);
                Assert.Equal(0, record.Tick);
                Assert.Empty(record.AgentId);
                Assert.Equal(0, record.Quantity);
                Assert.Null(record.Accepted);
                Assert.Null(record.RejectionReason);
                Assert.Null(record.StructureId);
                Assert.Equal(initial.StateDigest, record.StateDigest);
                Assert.Equal(initial.EventDigest, record.EventDigest);
                Assert.Matches("^[0-9a-f]{64}$", record.Checksum);
                Assert.Matches("^[0-9a-f]{64}$", record.HeaderChecksum);
            });

            SnowGlobeInternalRunReconstruction reconstruction = SnowGlobePersistedRun.ReconstructInternal(ledger, identity);
            Assert.False(reconstruction.IsDurablyPaused);
            Assert.Equal(initial, reconstruction.Public.World.CaptureIdentity());

            RunStorePrepareMarker[] prepares = File.ReadLines(Path.Combine(root, "commits.jsonl"))
                .Where((_, index) => index % 2 == 0)
                .Select(line => JsonSerializer.Deserialize<RunStorePrepareMarker>(line, JsonOptions)!)
                .ToArray();
            Assert.Equal(2, prepares.Length);
            Assert.All(prepares, prepare =>
            {
                Assert.Equal(RunStoreFrameKind.PauseTransition.ToString(), prepare.FrameKind);
                Assert.Equal(1, prepare.EntryCount);
                Assert.Empty(prepare.PayloadPrefixManifest);
            });
            Assert.Equal(
                new[] { ".writer.lock", "commits.jsonl", "ledger.jsonl", "run.json" },
                Directory.GetFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void PauseEvidence_IsV5OnlyAndCreateNewRejectsV4WithoutArtifacts()
    {
        string root = NewTemporaryDirectory();
        string source = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity v5 = Identity("runstore_v5_schema_gate/v1");
            SnowGlobeRunIdentity v4 = v5 with { SchemaVersion = SnowGlobeRunStore.V4SchemaVersion };
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.CreateNew(root, v4));
            Assert.Empty(Directory.GetFileSystemEntries(root));

            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(source, v5))
                Assert.Equal(RunStorePauseAppendResult.Appended, store.AppendPauseTransition(paused: true));
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(source);
            foreach (string schema in new[]
            {
                SnowGlobeRunStore.V4SchemaVersion,
                SnowGlobeRunStore.PreviousSchemaVersion,
                SnowGlobeRunStore.LegacySchemaVersion
            })
            {
                SnowGlobeRunIdentity legacy = ledger.Identity with
                {
                    SchemaVersion = schema,
                    ParticipantCommandIdentity = schema == SnowGlobeRunStore.LegacySchemaVersion
                        ? null
                        : SnowGlobeRunStore.ParticipantCommandIdentity
                };
                SnowGlobeLedgerRecord checksumValidLegacyTransition = SignedTransition(
                    legacy,
                    SnowGlobeWorld.Create(legacy.Seed, legacy.AgentCount),
                    sequence: 0,
                    action: "Pause");
                Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.Reconstruct(
                    new SnowGlobeRunLedger(legacy, new[] { checksumValidLegacyTransition })));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(source, recursive: true);
        }
    }

    [Fact]
    public async Task ReconstructionRejectsRedundantPauseAndTransitionInsideScheduledGrammar()
    {
        SnowGlobeRunIdentity identity = Identity("runstore_v5_transition_grammar/v1", agentCount: 1);
        SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
        SnowGlobeLedgerRecord firstPause = SignedTransition(identity, initial, 0, "Pause");
        SnowGlobeLedgerRecord redundantPause = SignedTransition(identity, initial, 1, "Pause");
        Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.Reconstruct(
            new SnowGlobeRunLedger(identity, new[] { firstPause, redundantPause })));

        string root = NewTemporaryDirectory();
        try
        {
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(initial, new IdleAdapter(), store, 1);
            SnowGlobeRunLedger scheduled = SnowGlobeRunStore.Read(root);
            SnowGlobeLedgerRecord embedded = SignedTransition(
                identity,
                SnowGlobeWorld.Create(identity.Seed, identity.AgentCount),
                1,
                "Pause");
            List<SnowGlobeLedgerRecord> records = scheduled.Records
                .Select(record => record.Sequence == 0 ? record : record with { Sequence = record.Sequence + 1 })
                .ToList();
            records.Insert(1, embedded);
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.Reconstruct(
                new SnowGlobeRunLedger(identity, records)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("prepare", 0, PauseInterruptionOutcome.PriorWithoutContinuation)]
    [InlineData("prepare", int.MaxValue, PauseInterruptionOutcome.AbandonedWithContinuation)]
    [InlineData("payload", 0, PauseInterruptionOutcome.AbandonedWithContinuation)]
    [InlineData("payload", int.MaxValue, PauseInterruptionOutcome.AbandonedWithContinuation)]
    [InlineData("payload", 1, PauseInterruptionOutcome.Invalid)]
    [InlineData("commit", 1, PauseInterruptionOutcome.Invalid)]
    [InlineData("commit", int.MaxValue, PauseInterruptionOutcome.CommittedWithoutContinuation)]
    public void InterruptedPauseFrame_UsesStrictBoundedRecovery(
        string interruption,
        int bytesBeforeFailure,
        PauseInterruptionOutcome outcome)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunStore? store = null;
        try
        {
            RunStoreWriteKind kind = interruption switch
            {
                "prepare" => RunStoreWriteKind.PrepareMarker,
                "payload" => RunStoreWriteKind.PausePayload,
                "commit" => RunStoreWriteKind.CommitMarker,
                _ => throw new ArgumentOutOfRangeException(nameof(interruption))
            };
            SnowGlobeRunIdentity identity = Identity($"runstore_v5_interruption_{interruption}_{bytesBeforeFailure}/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, kind, bytesBeforeFailure);
            store = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true));
            Assert.True(store.IsPoisoned);
            store.Dispose();
            store = null;

            if (outcome == PauseInterruptionOutcome.Invalid)
            {
                Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
                Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
                return;
            }

            SnowGlobeInternalRunReconstruction beforeOpen = SnowGlobePersistedRun.ReconstructInternal(SnowGlobeRunStore.Read(root), identity);
            Assert.Equal(outcome == PauseInterruptionOutcome.CommittedWithoutContinuation, beforeOpen.IsDurablyPaused);
            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            SnowGlobeInternalRunReconstruction afterOpen = SnowGlobePersistedRun.ReconstructInternal(SnowGlobeRunStore.Read(root), identity);
            Assert.Equal(outcome == PauseInterruptionOutcome.CommittedWithoutContinuation, afterOpen.IsDurablyPaused);
            bool hasContinuation = File.Exists(Path.Combine(root, "ledger.0001.jsonl"));
            Assert.Equal(outcome == PauseInterruptionOutcome.AbandonedWithContinuation, hasContinuation);
        }
        finally
        {
            store?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompletePendingPausePayload_IsSemanticallyValidatedBeforeAbandonment()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity("runstore_v5_pending_validation/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.PausePayload, int.MaxValue);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity, faulting))
                Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true));

            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            SnowGlobeLedgerRecord original = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(File.ReadAllLines(ledgerPath).Single(), JsonOptions)!;
            SnowGlobeLedgerRecord unsignedRecord = original with { Action = "Resume", Checksum = string.Empty };
            SnowGlobeLedgerRecord record = unsignedRecord with { Checksum = LedgerChecksum(unsignedRecord) };
            byte[] payload = SerializeLine(record);
            File.WriteAllBytes(ledgerPath, payload);

            string markerPath = Path.Combine(root, "commits.jsonl");
            RunStorePrepareMarker originalPrepare = JsonSerializer.Deserialize<RunStorePrepareMarker>(File.ReadAllLines(markerPath).Single(), JsonOptions)!;
            RunStorePrepareMarker unsignedPrepare = originalPrepare with
            {
                PayloadLength = payload.Length,
                PayloadChecksum = Digest(payload),
                Checksum = string.Empty
            };
            RunStorePrepareMarker prepare = unsignedPrepare with { Checksum = PrepareChecksum(unsignedPrepare) };
            File.WriteAllBytes(markerPath, SerializeLine(prepare));

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CommittedPauseFrame_RejectsInnerRecordAndOuterMarkerCorruptionWithoutRewrite()
    {
        string innerRoot = NewTemporaryDirectory();
        string outerRoot = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity innerIdentity = Identity("runstore_v5_inner_corruption/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(innerRoot, innerIdentity))
                Assert.Equal(RunStorePauseAppendResult.Appended, store.AppendPauseTransition(paused: true));
            string innerLedgerPath = Path.Combine(innerRoot, "ledger.jsonl");
            byte[] innerBytes = File.ReadAllBytes(innerLedgerPath);
            string innerText = Encoding.UTF8.GetString(innerBytes).Replace("\"Pause\"", "\"PausE\"", StringComparison.Ordinal);
            File.WriteAllText(innerLedgerPath, innerText, new UTF8Encoding(false));
            byte[] corruptInner = File.ReadAllBytes(innerLedgerPath);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(innerRoot));
            Assert.Equal(corruptInner, File.ReadAllBytes(innerLedgerPath));

            SnowGlobeRunIdentity outerIdentity = Identity("runstore_v5_outer_corruption/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(outerRoot, outerIdentity))
                Assert.Equal(RunStorePauseAppendResult.Appended, store.AppendPauseTransition(paused: true));
            string markerPath = Path.Combine(outerRoot, "commits.jsonl");
            string[] markerLines = File.ReadAllLines(markerPath);
            RunStorePrepareMarker prepare = JsonSerializer.Deserialize<RunStorePrepareMarker>(markerLines[0], JsonOptions)!;
            markerLines[0] = JsonSerializer.Serialize(prepare with { Checksum = new string('0', 64) }, JsonOptions);
            File.WriteAllText(markerPath, string.Join('\n', markerLines) + "\n", new UTF8Encoding(false));
            byte[] corruptOuter = File.ReadAllBytes(markerPath);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(outerRoot));
            Assert.Equal(corruptOuter, File.ReadAllBytes(markerPath));
        }
        finally
        {
            Directory.Delete(innerRoot, recursive: true);
            Directory.Delete(outerRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledRecovery_PreservesPriorDurablePauseState(bool initiallyPaused)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity($"runstore_v5_scheduled_recovery_{initiallyPaused.ToString().ToLowerInvariant()}/v1", agentCount: 1);
            using (SnowGlobeRunStore initial = SnowGlobeRunStore.CreateNew(root, identity))
            {
                if (initiallyPaused)
                    Assert.Equal(RunStorePauseAppendResult.Appended, initial.AppendPauseTransition(paused: true));
            }

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.ScheduledPayload, 0);
            using (SnowGlobeRunStore interrupted = SnowGlobeRunStore.OpenForAppend(root, faulting))
            {
                SnowGlobeWorld world = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), interrupted, 1));
            }

            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            SnowGlobeInternalRunReconstruction reconstruction = SnowGlobePersistedRun.ReconstructInternal(SnowGlobeRunStore.Read(root), identity);
            Assert.Equal(initiallyPaused, reconstruction.IsDurablyPaused);
            Assert.Equal(0, reconstruction.Public.World.Tick);
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult provenance =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);
            Assert.True(provenance.Accepted);
            Assert.Null(provenance.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PauseAndScheduledRecovery_ShareTheSingleContinuationBudget(bool pauseRecoveryFirst)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = Identity($"runstore_v5_shared_recovery_{pauseRecoveryFirst.ToString().ToLowerInvariant()}/v1", agentCount: 1);
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }

            if (pauseRecoveryFirst)
            {
                CreatePendingPause(root, RunStoreWriteKind.PausePayload, 0);
                using (SnowGlobeRunStore.OpenForAppend(root)) { }
                await CreatePendingScheduledAsync(root);
            }
            else
            {
                await CreatePendingScheduledAsync(root);
                using (SnowGlobeRunStore.OpenForAppend(root)) { }
                CreatePendingPause(root, RunStoreWriteKind.PausePayload, 0);
            }

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.False(File.Exists(Path.Combine(root, "ledger.0002.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0002.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void CreatePendingPause(string root, RunStoreWriteKind kind, int bytesBeforeFailure)
    {
        IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
            PhysicalRunStoreFileSystem.Instance, kind, bytesBeforeFailure);
        using SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root, faulting);
        Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true));
    }

    private static async Task CreatePendingScheduledAsync(string root)
    {
        IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
            PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.ScheduledPayload, 0);
        using SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root, faulting);
        SnowGlobeWorld world = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
        await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), store, world.Tick + 1));
    }

    private static SnowGlobeRunIdentity Identity(string adapterIdentity, int agentCount = 2) =>
        SnowGlobePersistedRun.Identity(adapterIdentity, seed: 240823, agentCount: agentCount);

    private static SnowGlobeLedgerRecord SignedTransition(
        SnowGlobeRunIdentity identity,
        SnowGlobeWorld world,
        int sequence,
        string action)
    {
        SnowGlobeLedgerRecord unsigned = new(
            sequence,
            SnowGlobeLedgerKind.PauseTransition,
            world.Tick,
            string.Empty,
            action,
            0,
            null,
            null,
            null,
            world.StateDigest(),
            world.EventDigest(),
            string.Empty,
            SnowGlobeRunStore.CanonicalIdentityChecksum(identity));
        return unsigned with { Checksum = LedgerChecksum(unsigned) };
    }

    private static byte[] SerializeLine<T>(T value) =>
        [.. JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), (byte)'\n'];

    private static string LedgerChecksum(SnowGlobeLedgerRecord record) => Digest(Encoding.UTF8.GetBytes(
        $"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}|{record.HeaderChecksum}"));

    private static string PrepareChecksum(RunStorePrepareMarker marker) => Digest(Encoding.UTF8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.FrameIndex}|{marker.PreviousCommitChecksum}|{marker.FrameKind}|{marker.FirstSequence}|{marker.EntryCount}|{marker.PayloadLength}|{marker.PayloadChecksum}|{marker.PayloadPrefixManifest}"));

    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-runstore-v5-pause-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class IdleAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
        }
    }

    public enum PauseInterruptionOutcome
    {
        PriorWithoutContinuation,
        AbandonedWithContinuation,
        Invalid,
        CommittedWithoutContinuation
    }
}
