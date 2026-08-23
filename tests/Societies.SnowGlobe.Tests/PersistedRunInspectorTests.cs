using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class PersistedRunInspectorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void EmptyCurrentRun_ProducesAnInertDetachedSnapshot()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity);

            Assert.True(result.Accepted);
            Assert.Null(result.RejectionReason);
            Assert.True(result.Snapshot!.IsPaused);
            Assert.Equal(0, result.Snapshot.Tick);
            Assert.Empty(result.Snapshot.CanonicalEvents);
            Assert.Null(result.Snapshot.NextEventCursor);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PopulatedCurrentRun_UsesBoundedPagesAndDetachedValues()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await WriteTicksAsync(root, identity, 5);

            SnowGlobeObserverInspectionResult first = SnowGlobePersistedRunInspector.Inspect(root, identity);
            SnowGlobeObserverInspectionResult final = SnowGlobePersistedRunInspector.Inspect(root, identity, 32);

            Assert.True(first.Accepted);
            Assert.True(first.Snapshot!.IsPaused);
            Assert.Equal(SnowGlobeObserverShell.MaximumInspectionEventWindow, first.Snapshot.CanonicalEvents.Count);
            Assert.Equal(32, first.Snapshot.NextEventCursor);
            Assert.True(final.Accepted);
            Assert.Equal(8, final.Snapshot!.CanonicalEvents.Count);
            Assert.Null(final.Snapshot.NextEventCursor);
            Assert.NotSame(first.Snapshot.Agents, final.Snapshot.Agents);
            Assert.NotSame(first.Snapshot.CanonicalEvents, final.Snapshot.CanonicalEvents);
            Assert.Equal(first.Snapshot.StateDigest, final.Snapshot.StateDigest);
            Assert.Equal(first.Snapshot.EventDigest, final.Snapshot.EventDigest);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FrozenV2AndV3Runs_AreInspectedWithoutCreatingAWriterLease()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity v2 = new(
                SnowGlobeRunStore.LegacySchemaVersion,
                SnowGlobePersistedRun.RulesIdentity,
                SnowGlobePersistedRun.PromptIdentity,
                "persisted_run_inspector_v2_adapter/v1",
                SnowGlobeScenario.FixedSeed,
                SnowGlobeScenario.FixedAgentCount);
            WriteFrozenHeaderAndEmptyLedger(root, v2);
            AssertInspectionAcceptedWithoutLease(root, v2);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            SnowGlobeRunIdentity v3 = new(
                SnowGlobeRunStore.PreviousSchemaVersion,
                SnowGlobePersistedRun.RulesIdentity,
                SnowGlobePersistedRun.PromptIdentity,
                "persisted_run_inspector_v3_adapter/v1",
                SnowGlobeScenario.FixedSeed,
                SnowGlobeScenario.FixedAgentCount,
                SnowGlobeRunStore.ParticipantCommandIdentity);
            WriteFrozenHeaderAndEmptyLedger(root, v3);
            AssertInspectionAcceptedWithoutLease(root, v3);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ExactIdentityMismatch_ReturnsOnlyTheStableRejection()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(
                root,
                identity with { AdapterIdentity = "persisted_run_inspector_other_adapter/v1" });

            Assert.False(result.Accepted);
            Assert.Equal("run_identity_mismatch", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(41)]
    public async Task InvalidEventCursor_ReturnsOnlyTheStableRejection(int cursor)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await WriteTicksAsync(root, identity, 5);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, cursor);

            Assert.False(result.Accepted);
            Assert.Equal("event_cursor_invalid", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("payload", 4096)]
    [InlineData("commit", 0)]
    public async Task PendingV4Evidence_ExposesOnlyThePriorCommittedStateAndNeverContinues(
        string interruption,
        int bytesBeforeFailure)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                interruption == "payload" ? RunStoreWriteKind.ScheduledPayload : RunStoreWriteKind.CommitMarker,
                bytesBeforeFailure);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateV4Fixture(root, identity, faulting))
            {
                await Assert.ThrowsAsync<IOException>(async () =>
                    await SnowGlobePersistedRun.RunAsync(
                        SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(identity.AdapterIdentity), store, 1));
            }

            Dictionary<string, byte[]> before = ArtifactBytes(root);
            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity);

            Assert.True(result.Accepted);
            Assert.True(result.Snapshot!.IsPaused);
            Assert.Equal(0, result.Snapshot.Tick);
            Assert.Empty(result.Snapshot.CanonicalEvents);
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void OrdinaryV4Run_ReportsBoundedNoDurableRecoveryReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult first =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult second =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);

            Assert.True(first.Accepted);
            Assert.Null(first.RejectionReason);
            SnowGlobePersistedRunRecoveryProvenanceReceipt receipt = Assert.IsType<SnowGlobePersistedRunRecoveryProvenanceReceipt>(first.Receipt);
            Assert.NotSame(receipt, second.Receipt);
            Assert.Equal("snow_globe_persisted_run_recovery_provenance_receipt/v1", receipt.ReceiptSchemaIdentity);
            Assert.Equal(SnowGlobePersistedRunRecoveryDisposition.NoDurableRecovery, receipt.Disposition);
            Assert.Equal(0, receipt.CommittedTick);
            Assert.Equal(0, receipt.CommittedEventCount);
            Assert.All(new[] { receipt.RunIdentityChecksum, receipt.EvidenceChecksum, receipt.CommittedStateDigest, receipt.CommittedEventDigest },
                value => Assert.Matches("^[0-9a-f]{64}$", value));
            Assert.Null(receipt.SourceSegmentIndex);
            Assert.Null(receipt.SourceFrameIndex);
            Assert.Null(receipt.SourcePrepareChecksum);
            Assert.Null(receipt.SourceLedgerLength);
            Assert.Null(receipt.SourceLedgerChecksum);
            Assert.Null(receipt.SourceMarkerLength);
            Assert.Null(receipt.SourceMarkerChecksum);
            Assert.Null(receipt.ContinuationChecksum);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("payload", 0, SnowGlobePersistedRunRecoveryDisposition.AbandonedIncompleteScheduledTick, 0)]
    [InlineData("commit", 0, SnowGlobePersistedRunRecoveryDisposition.AdoptedCompleteScheduledTick, 1)]
    public async Task DurableV4Recovery_ReportsAuthenticatedDispositionAndSourceBinding(
        string interruption,
        int bytesBeforeFailure,
        SnowGlobePersistedRunRecoveryDisposition expectedDisposition,
        int expectedTick)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await CreateInterruptedV4RunAsync(
                root,
                identity,
                interruption == "payload" ? RunStoreWriteKind.ScheduledPayload : RunStoreWriteKind.CommitMarker,
                bytesBeforeFailure);
            Dictionary<string, byte[]> sourceBeforeContinuation = ArtifactBytes(root);
            using (SnowGlobeRunStore.OpenForAppend(root)) { }

            RunStoreContinuationMarker continuation = JsonSerializer.Deserialize<RunStoreContinuationMarker>(
                File.ReadAllLines(Path.Combine(root, "commits.0001.jsonl")).Single(), JsonOptions)!;
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);

            Assert.True(result.Accepted);
            SnowGlobePersistedRunRecoveryProvenanceReceipt receipt = Assert.IsType<SnowGlobePersistedRunRecoveryProvenanceReceipt>(result.Receipt);
            Assert.Equal(expectedDisposition, receipt.Disposition);
            Assert.Equal(expectedTick, receipt.CommittedTick);
            Assert.Equal(continuation.SourceSegmentIndex, receipt.SourceSegmentIndex);
            Assert.Equal(continuation.SourceFrameIndex, receipt.SourceFrameIndex);
            Assert.Equal(continuation.SourcePrepareChecksum, receipt.SourcePrepareChecksum);
            Assert.Equal(continuation.SourceLedgerLength, receipt.SourceLedgerLength);
            Assert.Equal(continuation.SourceLedgerChecksum, receipt.SourceLedgerChecksum);
            Assert.Equal(continuation.SourceMarkerLength, receipt.SourceMarkerLength);
            Assert.Equal(continuation.SourceMarkerChecksum, receipt.SourceMarkerChecksum);
            Assert.Equal(continuation.Checksum, receipt.ContinuationChecksum);
            Assert.All(new[]
            {
                receipt.RunIdentityChecksum, receipt.EvidenceChecksum, receipt.CommittedStateDigest, receipt.CommittedEventDigest,
                receipt.SourcePrepareChecksum!, receipt.SourceLedgerChecksum!, receipt.SourceMarkerChecksum!, receipt.ContinuationChecksum!
            }, value => Assert.Matches("^[0-9a-f]{64}$", value));
            Assert.Equal(sourceBeforeContinuation["ledger.jsonl"], File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal(sourceBeforeContinuation["commits.jsonl"], File.ReadAllBytes(Path.Combine(root, "commits.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PendingCompleteV4Tail_ReportsNoDurableRecoveryAndPerformsNoContinuation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await CreateInterruptedV4RunAsync(root, identity, RunStoreWriteKind.CommitMarker, 0);
            Dictionary<string, byte[]> before = ArtifactBytes(root);

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);

            Assert.True(result.Accepted);
            Assert.Equal(SnowGlobePersistedRunRecoveryDisposition.NoDurableRecovery, result.Receipt!.Disposition);
            Assert.Equal(0, result.Receipt.CommittedTick);
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void LegacyV2AndV3Runs_AreAcceptedWithoutFabricatingV4RecoveryProvenance()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity v2 = new(
                SnowGlobeRunStore.LegacySchemaVersion,
                SnowGlobePersistedRun.RulesIdentity,
                SnowGlobePersistedRun.PromptIdentity,
                "persisted_run_recovery_provenance_v2/v1",
                SnowGlobeScenario.FixedSeed,
                SnowGlobeScenario.FixedAgentCount);
            WriteFrozenHeaderAndEmptyLedger(root, v2);
            Assert.Null(SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, v2).Receipt);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            SnowGlobeRunIdentity v3 = v2 with
            {
                SchemaVersion = SnowGlobeRunStore.PreviousSchemaVersion,
                AdapterIdentity = "persisted_run_recovery_provenance_v3/v1",
                ParticipantCommandIdentity = SnowGlobeRunStore.ParticipantCommandIdentity
            };
            WriteFrozenHeaderAndEmptyLedger(root, v3);
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, v3);
            Assert.True(result.Accepted);
            Assert.Null(result.Receipt);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CorruptForkedOrDriftingRecoveryEvidence_ReturnsNoReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await CreateInterruptedV4RunAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 0);
            using (SnowGlobeRunStore.OpenForAppend(root)) { }

            RunStoreContinuationMarker continuation = JsonSerializer.Deserialize<RunStoreContinuationMarker>(
                File.ReadAllLines(Path.Combine(root, "commits.0001.jsonl")).Single(), JsonOptions)!;
            RunStoreContinuationMarker forkedUnsigned = continuation with { SourceSegmentIndex = continuation.SourceSegmentIndex + 1, Checksum = string.Empty };
            RunStoreContinuationMarker forked = forkedUnsigned with { Checksum = ContinuationChecksum(forkedUnsigned) };
            File.WriteAllText(Path.Combine(root, "commits.0001.jsonl"), JsonSerializer.Serialize(forked, JsonOptions) + "\n", Encoding.UTF8);

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult corrupt =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);
            Assert.False(corrupt.Accepted);
            Assert.Equal("run_store_invalid", corrupt.RejectionReason);
            Assert.Null(corrupt.Receipt);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            File.AppendAllText(Path.Combine(root, "ledger.jsonl"), "not-json\n", Encoding.UTF8);
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult malformed =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);
            Assert.False(malformed.Accepted);
            Assert.Equal("run_store_invalid", malformed.RejectionReason);
            Assert.Null(malformed.Receipt);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            SnowGlobePersistedRunRecoveryProvenanceInspectionResult drifting =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity,
                    new HeaderMutatingFileSystem(PhysicalRunStoreFileSystem.Instance));
            Assert.False(drifting.Accepted);
            Assert.Equal("run_store_unstable", drifting.RejectionReason);
            Assert.Null(drifting.Receipt);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("prepare", "commits.jsonl", 0, "record_type")]
    [InlineData("commit", "commits.jsonl", 1, "payload_checksum")]
    [InlineData("continuation", "commits.0001.jsonl", 0, "source_prepare_checksum")]
    public async Task RequiredNullMarkerFields_ReturnRunStoreInvalidWithoutAReceipt(
        string markerKind,
        string markerFileName,
        int markerLine,
        string propertyName)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            if (markerKind == "continuation")
            {
                await CreateInterruptedV4RunAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 0);
                using (SnowGlobeRunStore.OpenForAppend(root)) { }
            }
            else
            {
                await WriteTicksAsync(root, identity, 1);
            }
            ReplaceMarkerPropertyWithNull(Path.Combine(root, markerFileName), markerLine, propertyName);

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task NullContinuationFieldIntroducedBeforeSecondRead_ReturnsUnstableWithoutAReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            await CreateInterruptedV4RunAsync(root, identity, RunStoreWriteKind.ScheduledPayload, 0);
            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            string continuationPath = Path.Combine(root, "commits.0001.jsonl");

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(
                    root,
                    identity,
                    new ContinuationNullAfterFirstReadFileSystem(PhysicalRunStoreFileSystem.Instance, continuationPath));

            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CorruptRun_ReturnsNoSnapshotOrParserText()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            File.AppendAllText(Path.Combine(root, "ledger.jsonl"), "not-json\n", Encoding.UTF8);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void UnavailableRun_ReturnsNoSnapshotOrFilesystemDetail()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(
                root, identity, 0, new UnavailableReadFileSystem(PhysicalRunStoreFileSystem.Instance));

            Assert.False(result.Accepted);
            Assert.Equal("run_store_unavailable", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void BetweenReadMutation_FailsClosedAsUnstable()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            IRunStoreFileSystem mutating = new HeaderMutatingFileSystem(PhysicalRunStoreFileSystem.Instance);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, mutating);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Inspection_UsesNoWriterLeaseOrArtifactMutation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            Dictionary<string, byte[]> before = ArtifactBytes(root);
            TrackingFileSystem files = new(PhysicalRunStoreFileSystem.Instance);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);

            Assert.True(result.Accepted);
            Assert.True(files.Reads > 0);
            Assert.Equal(0, files.CreateDirectoryCalls);
            Assert.Equal(0, files.CreateFileCalls);
            Assert.Equal(0, files.AppendFileCalls);
            Assert.Equal(0, files.LeaseCalls);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RecoveryProvenanceInspection_UsesNoWriterLeaseOrArtifactMutation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            Dictionary<string, byte[]> before = ArtifactBytes(root);
            TrackingFileSystem files = new(PhysicalRunStoreFileSystem.Instance);

            SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
                SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity, files);

            Assert.True(result.Accepted);
            Assert.NotNull(result.Receipt);
            Assert.True(files.Reads > 0);
            Assert.Equal(0, files.CreateDirectoryCalls);
            Assert.Equal(0, files.CreateFileCalls);
            Assert.Equal(0, files.AppendFileCalls);
            Assert.Equal(0, files.LeaseCalls);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DurableControlStatus_V5RunningPauseAndResume_AreDistinctFromTheInertSnapshot()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using SnowGlobePersistedSession session = SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity));

            AssertDurableControlStatus(root, identity, SnowGlobePersistedSessionControlState.Running);
            SnowGlobeObserverInspectionResult inert = SnowGlobePersistedRunInspector.Inspect(root, identity);
            Assert.True(inert.Accepted);
            Assert.True(inert.Snapshot!.IsPaused);

            Assert.True((await session.PauseAsync()).Applied);
            AssertDurableControlStatus(root, identity, SnowGlobePersistedSessionControlState.Paused);

            Assert.True((await session.ResumeAsync()).Applied);
            AssertDurableControlStatus(root, identity, SnowGlobePersistedSessionControlState.Running);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_LegacyAndV4Inputs_AreAcceptedWithoutFabricatingAState()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity v2 = new(
                SnowGlobeRunStore.LegacySchemaVersion,
                SnowGlobePersistedRun.RulesIdentity,
                SnowGlobePersistedRun.PromptIdentity,
                "persisted_control_status_v2_adapter/v1",
                SnowGlobeScenario.FixedSeed,
                SnowGlobeScenario.FixedAgentCount);
            WriteFrozenHeaderAndEmptyLedger(root, v2);
            SnowGlobePersistedSessionControlStatusInspectionResult v2Result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, v2);
            Assert.True(v2Result.Accepted);
            Assert.Null(v2Result.Receipt);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            SnowGlobeRunIdentity v3 = new(
                SnowGlobeRunStore.PreviousSchemaVersion,
                SnowGlobePersistedRun.RulesIdentity,
                SnowGlobePersistedRun.PromptIdentity,
                "persisted_control_status_v3_adapter/v1",
                SnowGlobeScenario.FixedSeed,
                SnowGlobeScenario.FixedAgentCount,
                SnowGlobeRunStore.ParticipantCommandIdentity);
            WriteFrozenHeaderAndEmptyLedger(root, v3);
            SnowGlobePersistedSessionControlStatusInspectionResult v3Result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, v3);
            Assert.True(v3Result.Accepted);
            Assert.Null(v3Result.Receipt);

            Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            SnowGlobeRunIdentity v4 = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, v4)) { }
            SnowGlobePersistedSessionControlStatusInspectionResult v4Result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, v4);
            Assert.True(v4Result.Accepted);
            Assert.Null(v4Result.Receipt);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_IdentityMismatchAndBetweenReadDrift_FailClosedWithoutAReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }

            SnowGlobePersistedSessionControlStatusInspectionResult mismatch =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity with { AdapterIdentity = "other/v1" });
            Assert.False(mismatch.Accepted);
            Assert.Equal("run_identity_mismatch", mismatch.RejectionReason);
            Assert.Null(mismatch.Receipt);

            SnowGlobePersistedSessionControlStatusInspectionResult drift =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(
                    root, identity, new HeaderMutatingFileSystem(PhysicalRunStoreFileSystem.Instance));
            Assert.False(drift.Accepted);
            Assert.Equal("run_store_unstable", drift.RejectionReason);
            Assert.Null(drift.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_UncommittedPauseEvidenceReportsThePriorDurableState()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.CommitMarker, 0);
            SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root, faulting);
            try { Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true)); }
            finally { store.Dispose(); }

            AssertDurableControlStatus(root, identity, SnowGlobePersistedSessionControlState.Running);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_PartialPauseEvidence_FailsClosedWithoutAReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.PausePayload, 1);
            SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root, faulting);
            try { Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true)); }
            finally { store.Dispose(); }

            SnowGlobePersistedSessionControlStatusInspectionResult result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity);
            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_NoncanonicalPauseCommitTail_FailsClosedWithoutAReceipt()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance, RunStoreWriteKind.CommitMarker, 1);
            SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root, faulting);
            try { Assert.Throws<IOException>(() => store.AppendPauseTransition(paused: true)); }
            finally { store.Dispose(); }

            SnowGlobePersistedSessionControlStatusInspectionResult result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity);
            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Receipt);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DurableControlStatus_UsesNoWriterLeaseOrArtifactMutation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }
            Dictionary<string, byte[]> before = ArtifactBytes(root);
            TrackingFileSystem files = new(PhysicalRunStoreFileSystem.Instance);

            SnowGlobePersistedSessionControlStatusInspectionResult result =
                SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity, files);

            Assert.True(result.Accepted);
            Assert.NotNull(result.Receipt);
            Assert.True(files.Reads > 0);
            Assert.Equal(0, files.CreateDirectoryCalls);
            Assert.Equal(0, files.CreateFileCalls);
            Assert.Equal(0, files.AppendFileCalls);
            Assert.Equal(0, files.LeaseCalls);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task WriteTicksAsync(string root, SnowGlobeRunIdentity identity, int ticks)
    {
        using SnowGlobeRunStore store = SnowGlobeRunStore.CreateV4Fixture(root, identity);
        await SnowGlobePersistedRun.RunAsync(
            SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(identity.AdapterIdentity), store, ticks);
    }

    private static async Task CreateInterruptedV4RunAsync(
        string root,
        SnowGlobeRunIdentity identity,
        RunStoreWriteKind interruption,
        int bytesBeforeFailure)
    {
        IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
            PhysicalRunStoreFileSystem.Instance, interruption, bytesBeforeFailure);
        SnowGlobeRunStore? store = SnowGlobeRunStore.CreateV4Fixture(root, identity, faulting);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(
                SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(identity.AdapterIdentity), store, 1));
        }
        finally { store.Dispose(); }
    }

    private static SnowGlobeRunIdentity NewIdentity() =>
        SnowGlobePersistedRun.Identity("persisted_run_inspector_adapter/v1") with
        {
            SchemaVersion = SnowGlobeRunStore.V4SchemaVersion
        };

    private static SnowGlobeRunIdentity NewV5Identity() =>
        SnowGlobePersistedRun.Identity("persisted_control_status_adapter/v1");

    private static void AssertDurableControlStatus(
        string root,
        SnowGlobeRunIdentity identity,
        SnowGlobePersistedSessionControlState expectedState)
    {
        SnowGlobePersistedSessionControlStatusInspectionResult result =
            SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity);
        Assert.True(result.Accepted);
        Assert.Null(result.RejectionReason);
        SnowGlobePersistedSessionControlStatusReceipt receipt = Assert.IsType<SnowGlobePersistedSessionControlStatusReceipt>(result.Receipt);
        SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root), identity);
        SnowGlobeWorldIdentity committed = reconstruction.World.CaptureIdentity();
        Assert.Equal("snow_globe_persisted_session_control_status_receipt/v1", receipt.ReceiptSchemaIdentity);
        Assert.Equal(expectedState, receipt.State);
        Assert.Equal(committed.Tick, receipt.CommittedTick);
        Assert.Equal(committed.EventCount, receipt.CommittedEventCount);
        Assert.Equal(committed.StateDigest, receipt.CommittedStateDigest);
        Assert.Equal(committed.EventDigest, receipt.CommittedEventDigest);
        Assert.Equal(SnowGlobeRunStore.CanonicalIdentityChecksum(identity), receipt.RunIdentityChecksum);
        Assert.Matches("^[0-9a-f]{64}$", receipt.EvidenceChecksum);
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-persisted-run-inspector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFrozenHeaderAndEmptyLedger(string root, SnowGlobeRunIdentity identity)
    {
        byte[] header = identity.SchemaVersion == SnowGlobeRunStore.LegacySchemaVersion
            ? Encoding.UTF8.GetBytes(
                $"{{\"schema_version\":\"{identity.SchemaVersion}\",\"rules_identity\":\"{identity.RulesIdentity}\",\"prompt_identity\":\"{identity.PromptIdentity}\",\"adapter_identity\":\"{identity.AdapterIdentity}\",\"seed\":{identity.Seed},\"agent_count\":{identity.AgentCount}}}")
            : JsonSerializer.SerializeToUtf8Bytes(identity, JsonOptions);
        File.WriteAllBytes(Path.Combine(root, "run.json"), header);
        File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), Array.Empty<byte>());
    }

    private static void AssertInspectionAcceptedWithoutLease(string root, SnowGlobeRunIdentity identity)
    {
        Assert.False(File.Exists(Path.Combine(root, ".writer.lock")));
        Dictionary<string, byte[]> before = ArtifactBytes(root);
        SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity);
        Assert.True(result.Accepted);
        Assert.True(result.Snapshot!.IsPaused);
        Assert.False(File.Exists(Path.Combine(root, ".writer.lock")));
        AssertArtifactBytesEqual(before, ArtifactBytes(root));
    }

    private static Dictionary<string, byte[]> ArtifactBytes(string root) => Directory
        .EnumerateFiles(root)
        .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertArtifactBytesEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(name => name, StringComparer.Ordinal), actual.Keys.OrderBy(name => name, StringComparer.Ordinal));
        foreach ((string name, byte[] bytes) in expected) Assert.Equal(bytes, actual[name]);
    }

    private static void ReplaceMarkerPropertyWithNull(string path, int lineIndex, string propertyName)
    {
        string[] lines = File.ReadAllLines(path);
        JsonObject marker = JsonNode.Parse(lines[lineIndex])?.AsObject()
            ?? throw new InvalidOperationException("Test marker is not a JSON object.");
        if (!marker.ContainsKey(propertyName)) throw new InvalidOperationException("Test marker property is absent.");
        marker[propertyName] = null;
        lines[lineIndex] = marker.ToJsonString(JsonOptions);
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    private static string ContinuationChecksum(RunStoreContinuationMarker marker) => Digest(Encoding.UTF8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.PreviousCommitChecksum}|{marker.SourceSegmentIndex}|{marker.SourceFrameIndex}|{marker.SourcePrepareChecksum}|{marker.SourceLedgerLength}|{marker.SourceLedgerChecksum}|{marker.SourceMarkerLength}|{marker.SourceMarkerChecksum}|{marker.Disposition}"));

    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class IdleAdapter(string adapterIdentity) : ISnowGlobeIdentifiedInferenceAdapter
    {
        public string AdapterIdentity { get; } = adapterIdentity;

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle, 0));
        }
    }

    private sealed class TrackingFileSystem(IRunStoreFileSystem inner) : IRunStoreFileSystem
    {
        public int Reads { get; private set; }
        public int CreateDirectoryCalls { get; private set; }
        public int CreateFileCalls { get; private set; }
        public int AppendFileCalls { get; private set; }
        public int LeaseCalls { get; private set; }

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) { Reads++; return inner.ReadFile(path, maximumBytes, description); }
        public void CreateDirectory(string path) { CreateDirectoryCalls++; inner.CreateDirectory(path); }
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) { CreateFileCalls++; inner.CreateFile(path, bytes, kind); }
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) { AppendFileCalls++; inner.AppendFile(path, bytes, kind); }
        public IDisposable AcquireExclusiveLease(string path) { LeaseCalls++; return inner.AcquireExclusiveLease(path); }
    }

    private sealed class HeaderMutatingFileSystem(IRunStoreFileSystem inner) : IRunStoreFileSystem
    {
        private int _headerReads;

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description)
        {
            if (Path.GetFileName(path) == "run.json" && Interlocked.Increment(ref _headerReads) == 2)
                File.WriteAllBytes(path, [(byte)' ', .. File.ReadAllBytes(path)]);
            return inner.ReadFile(path, maximumBytes, description);
        }
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.CreateFile(path, bytes, kind);
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.AppendFile(path, bytes, kind);
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
    }

    private sealed class ContinuationNullAfterFirstReadFileSystem(
        IRunStoreFileSystem inner,
        string continuationPath) : IRunStoreFileSystem
    {
        private int _continuationReads;

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description)
        {
            if (string.Equals(path, continuationPath, StringComparison.Ordinal)
                && Interlocked.Increment(ref _continuationReads) == 2)
            {
                ReplaceMarkerPropertyWithNull(continuationPath, 0, "source_prepare_checksum");
            }
            return inner.ReadFile(path, maximumBytes, description);
        }
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.CreateFile(path, bytes, kind);
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.AppendFile(path, bytes, kind);
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
    }

    private sealed class UnavailableReadFileSystem(IRunStoreFileSystem inner) : IRunStoreFileSystem
    {
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => throw new IOException("test-only filesystem interruption");
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.CreateFile(path, bytes, kind);
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.AppendFile(path, bytes, kind);
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
    }
}
