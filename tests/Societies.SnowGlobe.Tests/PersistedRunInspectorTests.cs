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
            TrackingFileSystem files = new(
                new HeaderMutatingFileSystem(PhysicalRunStoreFileSystem.Instance));

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Snapshot);
            Assert.Equal(files.OpenedFileCounts.Count, files.DisposedHandleCount);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TrailingDirectorySeparator_RemainsAcceptedAfterLexicalCanonicalization()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(
                root + Path.DirectorySeparatorChar,
                identity);

            Assert.True(result.Accepted);
            Assert.Null(result.RejectionReason);
            Assert.NotNull(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("ledger.jsonl")]
    [InlineData("commits.jsonl")]
    public void InPlaceLedgerOrMarkerMutation_FailsClosedAsUnstable(string artifactName)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            InPlaceMutatingFileSystem files = new(PhysicalRunStoreFileSystem.Instance, Path.Combine(root, artifactName));

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);

            Assert.True(files.Mutated);
            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ByteIdenticalPathReplacement_CannotSwitchTheSecondEvidenceSource()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            string headerPath = Path.Combine(root, "run.json");
            byte[] expectedCurrentPathBytes = File.ReadAllBytes(headerPath);
            PinnedOriginalMutationFileSystem files = new(PhysicalRunStoreFileSystem.Instance, headerPath);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);

            Assert.True(files.ReplacedAndMutatedPinnedOriginal);
            Assert.Equal(expectedCurrentPathBytes, File.ReadAllBytes(headerPath));
            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("link_policy")]
    public void BetweenReadLayoutOrLinkPolicyDrift_FailsClosedAsUnstable(string drift)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            LayoutDriftingFileSystem files = new(PhysicalRunStoreFileSystem.Instance, root, drift);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);

            Assert.True(files.Drifted);
            Assert.False(result.Accepted);
            Assert.Equal("run_store_unstable", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("artifact")]
    [InlineData("directory")]
    public void InitialPhysicalLinks_AreRejectedBeforeArtifactReads(string linkKind)
    {
        string container = NewTemporaryDirectory();
        string? runPath = null;
        try
        {
            string realRun = Path.Combine(container, "real-run");
            Directory.CreateDirectory(realRun);
            SnowGlobeRunIdentity identity = NewIdentity();
            WriteFrozenHeaderAndEmptyLedger(realRun, identity);

            if (linkKind == "artifact")
            {
                string outsideHeader = Path.Combine(container, "outside-run.json");
                File.Move(Path.Combine(realRun, "run.json"), outsideHeader);
                if (!TryCreateFileSymbolicLink(Path.Combine(realRun, "run.json"), outsideHeader)) return;
                runPath = realRun;
            }
            else
            {
                string linkedRun = Path.Combine(container, "linked-run");
                if (!TryCreateDirectorySymbolicLink(linkedRun, realRun)) return;
                runPath = linkedRun;
            }

            TrackingFileSystem files = new(PhysicalRunStoreFileSystem.Instance);
            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(runPath, identity, 0, files);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Snapshot);
            Assert.Empty(files.OpenedFileCounts);
        }
        finally { Directory.Delete(container, recursive: true); }
    }

    [Fact]
    public void PhysicalAncestorLink_IsRejectedBeforeRunDirectoryAccessWhenSupported()
    {
        string container = NewTemporaryDirectory();
        try
        {
            string realAncestor = Path.Combine(container, "real-ancestor");
            string realRun = Path.Combine(realAncestor, "run");
            Directory.CreateDirectory(realRun);
            SnowGlobeRunIdentity identity = NewIdentity();
            WriteFrozenHeaderAndEmptyLedger(realRun, identity);
            string linkedAncestor = Path.Combine(container, "linked-ancestor");
            if (!TryCreateDirectorySymbolicLink(linkedAncestor, realAncestor)) return;

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(
                Path.Combine(linkedAncestor, "run"),
                identity);

            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(container, recursive: true); }
    }

    [Fact]
    public void LinkAncestor_IsCheckedBeforeAnyDescendantMetadataOrAccess()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateV4Fixture(root, identity)) { }
            string linkedAncestor = Directory.GetParent(root)!.FullName;
            RootToLeafLinkPolicyFileSystem files = new(
                PhysicalRunStoreFileSystem.Instance,
                linkedAncestor);

            SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(
                root,
                identity,
                0,
                files);

            Assert.True(files.LinkedAncestorChecked);
            Assert.False(files.DescendantAccessed);
            Assert.False(result.Accepted);
            Assert.Equal("run_store_invalid", result.RejectionReason);
            Assert.Null(result.Snapshot);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(SnowGlobeRunStore.LegacySchemaVersion)]
    [InlineData(SnowGlobeRunStore.PreviousSchemaVersion)]
    [InlineData(SnowGlobeRunStore.V4SchemaVersion)]
    [InlineData(SnowGlobeRunStore.SchemaVersion)]
    public void MalformedTypedHeaders_AreInvalidAndDisposeTheHeaderHandleAcrossAllSurfaces(string schemaVersion)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentityForSchema(schemaVersion);
            WriteMalformedTypedHeader(root, identity);

            foreach (string surface in new[] { "inspect", "recovery", "durable_control" })
            {
                TrackingFileSystem files = new(PhysicalRunStoreFileSystem.Instance);
                (bool accepted, string? reason, bool payloadPresent) = surface switch
                {
                    "inspect" => InspectMalformed(root, identity, files),
                    "recovery" => InspectMalformedRecovery(root, identity, files),
                    "durable_control" => InspectMalformedDurableControl(root, identity, files),
                    _ => throw new InvalidOperationException("Unknown inspector surface.")
                };

                Assert.False(accepted);
                Assert.Equal("run_store_invalid", reason);
                Assert.False(payloadPresent);
                Assert.Equal(1, files.OpenedFileCounts["run.json"]);
                Assert.Equal(1, files.ZeroOffsetReadCounts["run.json"]);
                Assert.Equal(1, files.DisposedHandleCount);
                Assert.Single(files.OpenedFileCounts);
            }
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
            AssertPinnedArtifactReads(files, "commits.jsonl", "ledger.jsonl", "run.json");
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
            AssertPinnedArtifactReads(files, "commits.jsonl", "ledger.jsonl", "run.json");
            Assert.Equal(0, files.CreateDirectoryCalls);
            Assert.Equal(0, files.CreateFileCalls);
            Assert.Equal(0, files.AppendFileCalls);
            Assert.Equal(0, files.LeaseCalls);
            AssertArtifactBytesEqual(before, ArtifactBytes(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PinnedReadHandles_DoNotBlockValidAppendPauseOrResume()
    {
        string root = NewTemporaryDirectory();
        BlockingFirstReadFileSystem? files = null;
        try
        {
            SnowGlobeRunIdentity identity = NewV5Identity();
            using (SnowGlobePersistedSession.CreateNew(root, identity, new IdleAdapter(identity.AdapterIdentity))) { }
            files = new BlockingFirstReadFileSystem(PhysicalRunStoreFileSystem.Instance);

            Task<SnowGlobeObserverInspectionResult> inspection = Task.Run(() =>
                SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files));
            Assert.True(files.FirstReadStarted.Wait(TimeSpan.FromSeconds(5)));

            using (SnowGlobePersistedSession session = SnowGlobePersistedSession.Reopen(
                root, identity, new IdleAdapter(identity.AdapterIdentity)))
            {
                Assert.True((await session.AdvanceAsync()).Applied);
                Assert.True((await session.PauseAsync()).Applied);
                Assert.True((await session.ResumeAsync()).Applied);
            }

            files.ReleaseRead.Set();
            SnowGlobeObserverInspectionResult result = await inspection.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(result.Accepted);
            Assert.Null(result.RejectionReason);
            Assert.NotNull(result.Snapshot);
        }
        finally
        {
            files?.ReleaseRead.Set();
            Directory.Delete(root, recursive: true);
        }
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
            AssertPinnedArtifactReads(files, "commits.jsonl", "ledger.jsonl", "run.json");
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

    private static SnowGlobeRunIdentity NewIdentityForSchema(string schemaVersion) => schemaVersion switch
    {
        SnowGlobeRunStore.LegacySchemaVersion => new(
            schemaVersion,
            SnowGlobePersistedRun.RulesIdentity,
            SnowGlobePersistedRun.PromptIdentity,
            "persisted_malformed_header_v2_adapter/v1",
            SnowGlobeScenario.FixedSeed,
            SnowGlobeScenario.FixedAgentCount),
        SnowGlobeRunStore.PreviousSchemaVersion => new(
            schemaVersion,
            SnowGlobePersistedRun.RulesIdentity,
            SnowGlobePersistedRun.PromptIdentity,
            "persisted_malformed_header_v3_adapter/v1",
            SnowGlobeScenario.FixedSeed,
            SnowGlobeScenario.FixedAgentCount,
            SnowGlobeRunStore.ParticipantCommandIdentity),
        SnowGlobeRunStore.V4SchemaVersion => NewIdentity(),
        SnowGlobeRunStore.SchemaVersion => NewV5Identity(),
        _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion))
    };

    private static (bool Accepted, string? Reason, bool PayloadPresent) InspectMalformed(
        string root,
        SnowGlobeRunIdentity identity,
        IRunStoreReadFileSystem files)
    {
        SnowGlobeObserverInspectionResult result = SnowGlobePersistedRunInspector.Inspect(root, identity, 0, files);
        return (result.Accepted, result.RejectionReason, result.Snapshot is not null);
    }

    private static (bool Accepted, string? Reason, bool PayloadPresent) InspectMalformedRecovery(
        string root,
        SnowGlobeRunIdentity identity,
        IRunStoreReadFileSystem files)
    {
        SnowGlobePersistedRunRecoveryProvenanceInspectionResult result =
            SnowGlobePersistedRunInspector.InspectRecoveryProvenance(root, identity, files);
        return (result.Accepted, result.RejectionReason, result.Receipt is not null);
    }

    private static (bool Accepted, string? Reason, bool PayloadPresent) InspectMalformedDurableControl(
        string root,
        SnowGlobeRunIdentity identity,
        IRunStoreReadFileSystem files)
    {
        SnowGlobePersistedSessionControlStatusInspectionResult result =
            SnowGlobePersistedRunInspector.InspectDurableControlStatus(root, identity, files);
        return (result.Accepted, result.RejectionReason, result.Receipt is not null);
    }

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

    private static void WriteMalformedTypedHeader(string root, SnowGlobeRunIdentity identity)
    {
        byte[] header;
        if (identity.SchemaVersion == SnowGlobeRunStore.LegacySchemaVersion)
        {
            header = Encoding.UTF8.GetBytes(
                $"{{\"schema_version\":\"{identity.SchemaVersion}\",\"rules_identity\":\"{identity.RulesIdentity}\",\"prompt_identity\":\"{identity.PromptIdentity}\",\"adapter_identity\":\"{identity.AdapterIdentity}\",\"seed\":\"not-an-integer\",\"agent_count\":{identity.AgentCount}}}");
        }
        else
        {
            JsonObject malformed = JsonSerializer.SerializeToNode(identity, JsonOptions)!.AsObject();
            malformed["seed"] = "not-an-integer";
            header = Encoding.UTF8.GetBytes(malformed.ToJsonString(JsonOptions));
        }
        File.WriteAllBytes(Path.Combine(root, "run.json"), header);
        File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), Array.Empty<byte>());
        if (identity.SchemaVersion is SnowGlobeRunStore.V4SchemaVersion or SnowGlobeRunStore.SchemaVersion)
            File.WriteAllBytes(Path.Combine(root, "commits.jsonl"), Array.Empty<byte>());
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

    private static void AssertPinnedArtifactReads(TrackingFileSystem files, params string[] expectedNames)
    {
        Assert.Equal(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal),
            files.OpenedFileCounts.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(files.OpenedFileCounts.Values, count => Assert.Equal(1, count));
        Assert.All(expectedNames, name => Assert.Equal(2, files.ZeroOffsetReadCounts[name]));
        Assert.Equal(expectedNames.Length, files.DisposedHandleCount);
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            OperatingSystem.IsWindows()
            && exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            OperatingSystem.IsWindows()
            && exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
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
        public int DisposedHandleCount { get; private set; }
        public Dictionary<string, int> OpenedFileCounts { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> ZeroOffsetReadCounts { get; } = new(StringComparer.Ordinal);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public IRunStoreReadHandle OpenReadFile(string path)
        {
            string name = Path.GetFileName(path);
            OpenedFileCounts[name] = OpenedFileCounts.GetValueOrDefault(name) + 1;
            ZeroOffsetReadCounts.TryAdd(name, 0);
            return new TrackingReadHandle(
                inner.OpenReadFile(path),
                offset =>
                {
                    Reads++;
                    if (offset == 0) ZeroOffsetReadCounts[name]++;
                },
                () => DisposedHandleCount++);
        }
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
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return Path.GetFileName(path) == "run.json"
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset == 0 && Interlocked.Increment(ref _headerReads) == 2)
                        File.WriteAllBytes(path, [(byte)' ', .. File.ReadAllBytes(path)]);
                })
                : handle;
        }
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
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return string.Equals(path, continuationPath, StringComparison.Ordinal)
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset == 0 && Interlocked.Increment(ref _continuationReads) == 2)
                        ReplaceMarkerPropertyWithNull(continuationPath, 0, "source_prepare_checksum");
                })
                : handle;
        }
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
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public IRunStoreReadHandle OpenReadFile(string path) => throw new IOException("test-only filesystem interruption");
        public byte[] ReadFile(string path, int maximumBytes, string description) => throw new IOException("test-only filesystem interruption");
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.CreateFile(path, bytes, kind);
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.AppendFile(path, bytes, kind);
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
    }

    private sealed class InPlaceMutatingFileSystem(
        IRunStoreReadFileSystem inner,
        string targetPath) : IRunStoreReadFileSystem
    {
        private int _targetPasses;
        public bool Mutated { get; private set; }

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);

        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return string.Equals(path, targetPath, StringComparison.Ordinal)
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset == 0 && Interlocked.Increment(ref _targetPasses) == 2)
                    {
                        File.WriteAllBytes(targetPath, [(byte)'x']);
                        Mutated = true;
                    }
                })
                : handle;
        }
    }

    private sealed class PinnedOriginalMutationFileSystem(
        IRunStoreReadFileSystem inner,
        string headerPath) : IRunStoreReadFileSystem
    {
        private int _headerPasses;
        public bool ReplacedAndMutatedPinnedOriginal { get; private set; }

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);

        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return string.Equals(path, headerPath, StringComparison.Ordinal)
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset != 0 || Interlocked.Increment(ref _headerPasses) != 2) return;
                    byte[] original = File.ReadAllBytes(headerPath);
                    string displaced = Path.Combine(
                        Path.GetTempPath(),
                        "societies-pinned-run-original-" + Guid.NewGuid().ToString("N"));
                    File.Move(headerPath, displaced);
                    try
                    {
                        File.WriteAllBytes(headerPath, original);
                        File.WriteAllBytes(displaced, [(byte)' ', .. original]);
                        ReplacedAndMutatedPinnedOriginal = true;
                    }
                    finally
                    {
                        if (File.Exists(displaced)) File.Delete(displaced);
                    }
                })
                : handle;
        }
    }

    private sealed class LayoutDriftingFileSystem(
        IRunStoreReadFileSystem inner,
        string root,
        string drift) : IRunStoreReadFileSystem
    {
        private int _headerPasses;
        private bool _reportLedgerLink;
        public bool Drifted { get; private set; }

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);

        public FileAttributes GetAttributes(string path)
        {
            FileAttributes attributes = inner.GetAttributes(path);
            return _reportLedgerLink && Path.GetFileName(path) == "ledger.jsonl"
                ? attributes | FileAttributes.ReparsePoint
                : attributes;
        }

        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return Path.GetFileName(path) == "run.json"
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset != 0 || Interlocked.Increment(ref _headerPasses) != 2) return;
                    switch (drift)
                    {
                        case "add":
                            File.WriteAllBytes(Path.Combine(root, "unexpected.bin"), [(byte)1]);
                            break;
                        case "remove":
                            File.Delete(Path.Combine(root, "ledger.jsonl"));
                            break;
                        case "link_policy":
                            _reportLedgerLink = true;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown test drift.");
                    }
                    Drifted = true;
                })
                : handle;
        }
    }

    private sealed class RootToLeafLinkPolicyFileSystem(
        IRunStoreReadFileSystem inner,
        string linkedAncestor) : IRunStoreReadFileSystem
    {
        public bool LinkedAncestorChecked { get; private set; }
        public bool DescendantAccessed { get; private set; }

        public bool DirectoryExists(string path)
        {
            ThrowOnLinkedOrDescendantAccess(path);
            return inner.DirectoryExists(path);
        }

        public IReadOnlyList<string> EnumerateEntryNames(string directory)
        {
            ThrowOnDescendantAccess(directory);
            return inner.EnumerateEntryNames(directory);
        }

        public bool FileExists(string path)
        {
            ThrowOnDescendantAccess(path);
            return inner.FileExists(path);
        }

        public FileAttributes GetAttributes(string path)
        {
            if (SamePath(path, linkedAncestor))
            {
                LinkedAncestorChecked = true;
                return inner.GetAttributes(path) | FileAttributes.ReparsePoint;
            }
            ThrowOnDescendantAccess(path);
            return inner.GetAttributes(path);
        }

        public IRunStoreReadHandle OpenReadFile(string path)
        {
            ThrowOnDescendantAccess(path);
            return inner.OpenReadFile(path);
        }

        public byte[] ReadFile(string path, int maximumBytes, string description)
        {
            ThrowOnDescendantAccess(path);
            return inner.ReadFile(path, maximumBytes, description);
        }

        private void ThrowOnLinkedOrDescendantAccess(string path)
        {
            if (SamePath(path, linkedAncestor))
                throw new InvalidOperationException("DirectoryExists followed a linked ancestor.");
            ThrowOnDescendantAccess(path);
        }

        private void ThrowOnDescendantAccess(string path)
        {
            string relative = Path.GetRelativePath(linkedAncestor, path);
            if (relative == "." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative == "..") return;
            DescendantAccessed = true;
            throw new InvalidOperationException("Descendant access occurred before ancestor-link rejection.");
        }

        private static bool SamePath(string left, string right) => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed class BlockingFirstReadFileSystem(IRunStoreReadFileSystem inner) : IRunStoreReadFileSystem
    {
        private int _blocked;
        public ManualResetEventSlim FirstReadStarted { get; } = new(false);
        public ManualResetEventSlim ReleaseRead { get; } = new(false);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);

        public IRunStoreReadHandle OpenReadFile(string path)
        {
            IRunStoreReadHandle handle = inner.OpenReadFile(path);
            return Path.GetFileName(path) == "ledger.jsonl"
                ? new CallbackReadHandle(handle, (_, offset) =>
                {
                    if (offset != 0 || Interlocked.CompareExchange(ref _blocked, 1, 0) != 0) return;
                    FirstReadStarted.Set();
                    if (!ReleaseRead.Wait(TimeSpan.FromSeconds(10)))
                        throw new IOException("Timed out waiting for the concurrent writer test.");
                })
                : handle;
        }
    }

    private sealed class CallbackReadHandle(
        IRunStoreReadHandle inner,
        BeforeReadCallback beforeRead) : IRunStoreReadHandle
    {
        public int Read(Span<byte> destination, long fileOffset)
        {
            beforeRead(destination, fileOffset);
            return inner.Read(destination, fileOffset);
        }

        public void Dispose() => inner.Dispose();
    }

    private sealed class TrackingReadHandle(
        IRunStoreReadHandle inner,
        Action<long> beforeRead,
        Action onDispose) : IRunStoreReadHandle
    {
        private int _disposed;

        public int Read(Span<byte> destination, long fileOffset)
        {
            beforeRead(fileOffset);
            return inner.Read(destination, fileOffset);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { inner.Dispose(); }
            finally { onDispose(); }
        }
    }

    private delegate void BeforeReadCallback(Span<byte> destination, long fileOffset);
}
