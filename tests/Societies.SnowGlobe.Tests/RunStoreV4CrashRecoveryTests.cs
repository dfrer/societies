using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class RunStoreV4CrashRecoveryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void FrozenHistoricalV3ScheduledRun_ReadsAndReconstructsButRemainsReadOnly()
    {
        const string headerBase64 = "eyJzY2hlbWFfdmVyc2lvbiI6InNub3dfZ2xvYmVfcnVuX3N0b3JlL3YzIiwicnVsZXNfaWRlbnRpdHkiOiJzbm93X2dsb2JlX2RvbWFpbl9ydWxlcy92MSIsInByb21wdF9pZGVudGl0eSI6Im5vcm1hbGl6ZWRfdmFsdWVzX29ubHkvbm9fcGFydGljaXBhbnRfdGV4dC92MSIsImFkYXB0ZXJfaWRlbnRpdHkiOiJmcm96ZW5fdjNfc2NoZWR1bGVkX2FkYXB0ZXIvdjEiLCJzZWVkIjoyNDA4MTUsImFnZW50X2NvdW50IjoxLCJwYXJ0aWNpcGFudF9jb21tYW5kX2lkZW50aXR5Ijoic25vd19nbG9iZV9wYXJ0aWNpcGFudF9jb21tYW5kL3YxIn0=";
        const string ledgerBase64 = "eyJzZXF1ZW5jZSI6MCwia2luZCI6MCwidGljayI6MCwiYWdlbnRfaWQiOiJhZ2VudC0wMCIsImFjdGlvbiI6IklkbGUiLCJxdWFudGl0eSI6MCwiYWNjZXB0ZWQiOm51bGwsInJlamVjdGlvbl9yZWFzb24iOm51bGwsInN0cnVjdHVyZV9pZCI6bnVsbCwic3RhdGVfZGlnZXN0IjpudWxsLCJldmVudF9kaWdlc3QiOm51bGwsImNoZWNrc3VtIjoiNDllMDg3MTYyNDY3ZDMwNThhMjE2YTU4ZTU5ODYwN2NiYmNmMmUyMWE2MDFmZjYxYTA5NmMyNmI2MTRiNjQxMyIsImhlYWRlcl9jaGVja3N1bSI6IjdmMzhhN2Q3MGZlNjFmNTNiODQ3ZDNjZWQ1YjlkZTBlODFiMjg4YTBlNDgzZTU2YWNhZDM0ZTQzMjJhZTM1MDQifQp7InNlcXVlbmNlIjoxLCJraW5kIjoxLCJ0aWNrIjowLCJhZ2VudF9pZCI6ImFnZW50LTAwIiwiYWN0aW9uIjoiSWRsZSIsInF1YW50aXR5IjowLCJhY2NlcHRlZCI6bnVsbCwicmVqZWN0aW9uX3JlYXNvbiI6bnVsbCwic3RydWN0dXJlX2lkIjpudWxsLCJzdGF0ZV9kaWdlc3QiOm51bGwsImV2ZW50X2RpZ2VzdCI6bnVsbCwiY2hlY2tzdW0iOiJkY2IzN2JiNTE0OTc0NTQ3YzA0OTk3MzcyZTBiNTJmZWYxNzgzZjZiYWZkZThmZWEyNzAzY2M2NDdiMjk3MjdiIiwiaGVhZGVyX2NoZWNrc3VtIjoiN2YzOGE3ZDcwZmU2MWY1M2I4NDdkM2NlZDViOWRlMGU4MWIyODhhMGU0ODNlNTZhY2FkMzRlNDMyMmFlMzUwNCJ9Cnsic2VxdWVuY2UiOjIsImtpbmQiOjIsInRpY2siOjAsImFnZW50X2lkIjoiYWdlbnQtMDAiLCJhY3Rpb24iOiJJZGxlIiwicXVhbnRpdHkiOjAsImFjY2VwdGVkIjp0cnVlLCJyZWplY3Rpb25fcmVhc29uIjpudWxsLCJzdHJ1Y3R1cmVfaWQiOm51bGwsInN0YXRlX2RpZ2VzdCI6bnVsbCwiZXZlbnRfZGlnZXN0IjpudWxsLCJjaGVja3N1bSI6IjhlMTBlNWViOGE3ZTMzYzkyYzc0ODI3NzFkYjE2Y2U1Njc0NmFiMzE4OGFmMzI3NmNlNDk0NmQzNjA0YTA1MDkiLCJoZWFkZXJfY2hlY2tzdW0iOiI3ZjM4YTdkNzBmZTYxZjUzYjg0N2QzY2VkNWI5ZGUwZTgxYjI4OGEwZTQ4M2U1NmFjYWQzNGU0MzIyYWUzNTA0In0KeyJzZXF1ZW5jZSI6Mywia2luZCI6MywidGljayI6MCwiYWdlbnRfaWQiOiJhZ2VudC0wMCIsImFjdGlvbiI6IklkbGUiLCJxdWFudGl0eSI6MCwiYWNjZXB0ZWQiOnRydWUsInJlamVjdGlvbl9yZWFzb24iOm51bGwsInN0cnVjdHVyZV9pZCI6bnVsbCwic3RhdGVfZGlnZXN0IjpudWxsLCJldmVudF9kaWdlc3QiOm51bGwsImNoZWNrc3VtIjoiMmI3NjI5YmY4MmU1N2JmNzUwMjQyZTY5NzViN2U0ZGNlNmQ0NDc4Y2EyNTMzNzEwMmJiYzc5NDA0ODczZGE2YSIsImhlYWRlcl9jaGVja3N1bSI6IjdmMzhhN2Q3MGZlNjFmNTNiODQ3ZDNjZWQ1YjlkZTBlODFiMjg4YTBlNDgzZTU2YWNhZDM0ZTQzMjJhZTM1MDQifQp7InNlcXVlbmNlIjo0LCJraW5kIjo0LCJ0aWNrIjoxLCJhZ2VudF9pZCI6IiIsImFjdGlvbiI6IiIsInF1YW50aXR5IjowLCJhY2NlcHRlZCI6bnVsbCwicmVqZWN0aW9uX3JlYXNvbiI6bnVsbCwic3RydWN0dXJlX2lkIjpudWxsLCJzdGF0ZV9kaWdlc3QiOiJmMWI5NGRlZmY3YmU5MzkwMTUyOTZlOTI1M2NiYzE4ZTBlYWFhNjdmMGRlMjNjZmRkODZmYTRmMTNmYmQ4YWM1IiwiZXZlbnRfZGlnZXN0IjoiY2U1N2FiMWFkNDA5OWMzN2YwNDRkNTg2N2IzNTkzYmUyNzU4ZGYzMDIzNTM3ZjkzMDFkZjM3NWQ3ZWNhMzYxMCIsImNoZWNrc3VtIjoiODBhODg2MmZlOGYzMDZhYjcyMzZmNDY3MDM2M2YzY2NmMzQ5YjYyZGVmZGRiNTg0YzZhM2EzMTQzMThlN2ZlYSIsImhlYWRlcl9jaGVja3N1bSI6IjdmMzhhN2Q3MGZlNjFmNTNiODQ3ZDNjZWQ1YjlkZTBlODFiMjg4YTBlNDgzZTU2YWNhZDM0ZTQzMjJhZTM1MDQifQo=";
        string root = NewTemporaryDirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(root, "run.json"), Convert.FromBase64String(headerBase64));
            File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), Convert.FromBase64String(ledgerBase64));
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);

            Assert.Equal(SnowGlobeRunStore.PreviousSchemaVersion, ledger.Identity.SchemaVersion);
            Assert.Equal(5, ledger.EntryCount);
            Assert.Equal(1, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger).Tick);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
            Assert.False(File.Exists(Path.Combine(root, ".writer.lock")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CreateNew_EmitsV4WithEmptyFramedSegment()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_create_adapter/v1");
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }

            using JsonDocument header = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "run.json")));
            Assert.Equal("snow_globe_run_store/v4", header.RootElement.GetProperty("schema_version").GetString());
            Assert.Empty(File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(root, "commits.jsonl")));
            Assert.Equal(0, SnowGlobeRunStore.Read(root).EntryCount);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public async Task InterruptedScheduledPayload_ReopensAtPriorTickInNewContinuationWithoutChangingOriginalBytes(int bytesBeforeFailure)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_partial_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                bytesBeforeFailure);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(live, new IdleAdapter(), interrupted, 1));
                Assert.True(interrupted.IsPoisoned);
            }
            finally { interrupted.Dispose(); }

            byte[] originalLedger = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            byte[] originalMarkers = File.ReadAllBytes(Path.Combine(root, "commits.jsonl"));
            RunStorePrepareMarker prepared = JsonSerializer.Deserialize<RunStorePrepareMarker>(File.ReadAllLines(Path.Combine(root, "commits.jsonl")).Single(), JsonOptions)!;
            if (bytesBeforeFailure != 0)
            {
                Assert.NotEmpty(originalLedger);
                Assert.True(originalLedger.Length < prepared.PayloadLength);
                Assert.Contains($"{originalLedger.Length}:{Digest(originalLedger)}", prepared.PayloadPrefixManifest, StringComparison.Ordinal);
            }
            Assert.Equal(0, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).Tick);

            using SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root);
            Assert.Equal(originalLedger, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal(originalMarkers, File.ReadAllBytes(Path.Combine(root, "commits.jsonl")));
            Assert.True(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.True(File.Exists(Path.Combine(root, "commits.0001.jsonl")));

            SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            await SnowGlobePersistedRun.RunAsync(resumed, new IdleAdapter(), reopened, 1);
            Assert.Equal(1, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).Tick);
            Assert.Equal(originalLedger, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal(originalMarkers, File.ReadAllBytes(Path.Combine(root, "commits.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public async Task InterruptedCommitMarker_RecoversTheCompleteScheduledTickAndContinuesWithoutChangingOriginalBytes(int bytesBeforeFailure)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_commit_gap_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.CommitMarker,
                bytesBeforeFailure);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(live, new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }

            byte[] originalLedger = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            byte[] originalMarkers = File.ReadAllBytes(Path.Combine(root, "commits.jsonl"));
            SnowGlobeWorld recoveredRead = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            Assert.Equal(0, recoveredRead.Tick);

            using SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root);
            Assert.Equal(originalLedger, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal(originalMarkers, File.ReadAllBytes(Path.Combine(root, "commits.jsonl")));
            SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            Assert.Equal(1, resumed.Tick);
            await SnowGlobePersistedRun.RunAsync(resumed, new IdleAdapter(), reopened, 2);
            Assert.Equal(2, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).Tick);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("invalid_utf8")]
    [InlineData("nul")]
    [InlineData("garbage")]
    public async Task InterruptedScheduledPayload_WithUnauthenticatedPartialRecord_FailsClosed(string mutation)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_partial_corruption_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                4096);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(
                    SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }

            byte[] residue = mutation switch
            {
                "invalid_utf8" => [0xff, 0xfe],
                "nul" => [0x00],
                "garbage" => Encoding.UTF8.GetBytes("{\"noncanonical\":"),
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            using (FileStream stream = new(Path.Combine(root, "ledger.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read))
                stream.Write(residue);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InterruptedScheduledPayload_WithMutatedPreparedPrefix_FailsClosed()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_prefix_binding_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                4096);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(
                    SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }

            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            byte[] mutated = File.ReadAllBytes(ledgerPath);
            Assert.NotEmpty(mutated);
            mutated[0] ^= 1;
            File.WriteAllBytes(ledgerPath, mutated);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void OpenForAppend_RejectsRawHeaderMutationDuringLeaseAcquisition()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_header_toctou_adapter/v1");
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }
            byte[] before = File.ReadAllBytes(Path.Combine(root, "run.json"));
            IRunStoreFileSystem mutating = new LeaseMutatingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                () => File.WriteAllBytes(Path.Combine(root, "run.json"), [(byte)' ', .. before]));

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root, mutating));
            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Continuation_BindsFullSourceSegmentLengthsAndChecksums()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_continuation_length_adapter/v1");
            SnowGlobeWorld world = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore first = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), first, 1);

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                0);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.OpenForAppend(root, faulting);
            try { await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), interrupted, 2)); }
            finally { interrupted.Dispose(); }

            int sourceLedgerLength = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length;
            int sourceMarkerLength = File.ReadAllBytes(Path.Combine(root, "commits.jsonl")).Length;
            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            RunStoreContinuationMarker continuation = JsonSerializer.Deserialize<RunStoreContinuationMarker>(
                File.ReadAllLines(Path.Combine(root, "commits.0001.jsonl")).Single(), JsonOptions)!;

            Assert.Equal(sourceLedgerLength, continuation.SourceLedgerLength);
            Assert.Equal(sourceMarkerLength, continuation.SourceMarkerLength);
            Assert.Equal(Digest(File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"))), continuation.SourceLedgerChecksum);
            Assert.Equal(Digest(File.ReadAllBytes(Path.Combine(root, "commits.jsonl"))), continuation.SourceMarkerChecksum);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FullCommitMarkerWrittenBeforeInjectedError_IsReadBackAsCommittedWithoutRecoverySegment()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_commit_readback_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.CommitMarker,
                int.MaxValue);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(live, new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }

            Assert.Equal(1, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root)).Tick);
            using (SnowGlobeRunStore.OpenForAppend(root)) { }
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
            Assert.False(File.Exists(Path.Combine(root, "commits.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SecondInterruptedScheduledWriteAfterRecovery_FailsClosedAtTheRecoveryBound()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_recovery_bound_adapter/v1");
            IRunStoreFileSystem firstFault = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                0);
            SnowGlobeRunStore? first = SnowGlobeRunStore.CreateNew(root, identity, firstFault);
            try
            {
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), first, 1));
            }
            finally { first.Dispose(); }
            using (SnowGlobeRunStore.OpenForAppend(root)) { }

            IRunStoreFileSystem secondFault = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                0);
            SnowGlobeRunStore? second = SnowGlobeRunStore.OpenForAppend(root, secondFault);
            try
            {
                SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(resumed, new IdleAdapter(), second, 1));
            }
            finally { second.Dispose(); }

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("sequence")]
    [InlineData("canonical_action")]
    [InlineData("unknown_property")]
    public async Task FramingAwareLedgerMutation_ReachesNamedInnerValidation(string mutation)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_inner_validation_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, 1);

            string[] lines = File.ReadAllLines(Path.Combine(root, "ledger.jsonl"));
            SnowGlobeLedgerRecord original = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(lines[0], JsonOptions)!;
            if (mutation == "unknown_property")
            {
                lines[0] = lines[0].Replace("{\"sequence\":", "{\"unknown\":0,\"sequence\":", StringComparison.Ordinal);
            }
            else
            {
                SnowGlobeLedgerRecord unsigned = mutation switch
                {
                    "sequence" => original with { Sequence = 1, Checksum = string.Empty },
                    "canonical_action" => original with { Action = "idle", Checksum = string.Empty },
                    _ => throw new ArgumentOutOfRangeException(nameof(mutation))
                };
                lines[0] = JsonSerializer.Serialize(unsigned with { Checksum = LedgerChecksum(unsigned) }, JsonOptions);
            }
            RewriteSingleFramePayload(root, Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsChecksumValidBrokenCommitChain()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_broken_chain_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, 1);

            string markerPath = Path.Combine(root, "commits.jsonl");
            string[] lines = File.ReadAllLines(markerPath);
            RunStorePrepareMarker prepare = JsonSerializer.Deserialize<RunStorePrepareMarker>(lines[0], JsonOptions)!;
            RunStoreCommitMarker commit = JsonSerializer.Deserialize<RunStoreCommitMarker>(lines[1], JsonOptions)!;
            string falseChain = new('0', 64);
            RunStorePrepareMarker unsignedPrepare = prepare with { PreviousCommitChecksum = falseChain, Checksum = string.Empty };
            prepare = unsignedPrepare with { Checksum = PrepareChecksum(unsignedPrepare) };
            RunStoreCommitMarker unsignedCommit = commit with
            {
                PreviousCommitChecksum = falseChain,
                PrepareChecksum = prepare.Checksum,
                Checksum = string.Empty
            };
            commit = unsignedCommit with { Checksum = CommitChecksum(unsignedCommit) };
            File.WriteAllBytes(markerPath, [.. SerializeLine(prepare), .. SerializeLine(commit)]);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsChecksumValidForkedContinuationSource()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_forked_continuation_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                0);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            try
            {
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(
                    SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }
            using (SnowGlobeRunStore.OpenForAppend(root)) { }

            string markerPath = Path.Combine(root, "commits.0001.jsonl");
            RunStoreContinuationMarker marker = JsonSerializer.Deserialize<RunStoreContinuationMarker>(File.ReadAllLines(markerPath).Single(), JsonOptions)!;
            RunStoreContinuationMarker unsigned = marker with { SourcePrepareChecksum = new string('0', 64), Checksum = string.Empty };
            marker = unsigned with { Checksum = ContinuationChecksum(unsigned) };
            File.WriteAllBytes(markerPath, SerializeLine(marker));

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Reader_RejectsFramingAndChecksumValidParticipant129()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_participant_capacity_adapter/v1");
            SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                for (int index = 0; index < SnowGlobeRunStore.MaximumParticipantEvaluations; index++)
                {
                    SnowGlobeParticipantCommand command = new(
                        "participant-01", $"key-{index:D3}", 1, initial.StateDigest(), initial.EventDigest(),
                        "agent-00", SnowGlobeActionKind.Idle, 0);
                    Assert.Equal("stale_tick", store.EvaluateAndAppendParticipantCommand(command).RejectionReason);
                }
            }

            SnowGlobeParticipantEvaluationRecord template = JsonSerializer.Deserialize<SnowGlobeParticipantEvaluationRecord>(
                File.ReadLines(Path.Combine(root, "ledger.jsonl")).First(), JsonOptions)!;
            SnowGlobeParticipantEvaluationRecord unsigned = template with
            {
                Sequence = SnowGlobeRunStore.MaximumParticipantEvaluations,
                IdempotencyKey = "overflow",
                Checksum = string.Empty
            };
            SnowGlobeParticipantEvaluationRecord extra = unsigned with { Checksum = SnowGlobeRunStore.ParticipantChecksum(unsigned) };
            AppendFramedFrame(root, 0, RunStoreFrameKind.ParticipantEvaluation, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(extra, JsonOptions) + "\n"), 1);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsFramingValidRecord4097AcrossContinuationSegments()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_global_capacity_adapter/v1");
            SnowGlobeWorld world = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), store, 123);

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ScheduledPayload,
                0);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.OpenForAppend(root, faulting);
            try { await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(world, new IdleAdapter(), interrupted, 124)); }
            finally { interrupted.Dispose(); }

            using (SnowGlobeRunStore recovered = SnowGlobeRunStore.OpenForAppend(root))
            {
                SnowGlobeWorld prior = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                await SnowGlobePersistedRun.RunAsync(prior, new IdleAdapter(), recovered, 124);
            }
            Assert.Equal(4092, SnowGlobeRunStore.Read(root).EntryCount);

            byte[] payload = Encoding.UTF8.GetBytes("{}\n{}\n{}\n{}\n{}\n");
            AppendFramedFrame(root, 1, RunStoreFrameKind.ScheduledTick, payload, 5);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void InterruptedParticipantEvidence_IsRejectedAndNeverContinued()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_participant_tail_adapter/v1");
            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.ParticipantPayload,
                12);
            SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity, faulting);
            SnowGlobeParticipantCommand command = new(
                "participant-01", "participant-tail", initial.Tick, initial.StateDigest(), initial.EventDigest(),
                "agent-00", SnowGlobeActionKind.Idle, 0);

            Assert.Throws<IOException>(() => store.EvaluateAndAppendParticipantCommand(command));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
            Assert.False(File.Exists(Path.Combine(root, "ledger.0001.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("committed_corruption")]
    [InlineData("broken_link")]
    [InlineData("fork")]
    [InlineData("unknown_tail")]
    [InlineData("marker_bound")]
    public async Task Reader_RejectsCommittedCorruptionBrokenChainsForksAndBounds(string mutation)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_chain_rejection_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, 1);

            string ledger = Path.Combine(root, "ledger.jsonl");
            string markers = Path.Combine(root, "commits.jsonl");
            switch (mutation)
            {
                case "committed_corruption":
                    byte[] bytes = File.ReadAllBytes(ledger);
                    bytes[bytes.Length / 2] ^= 1;
                    File.WriteAllBytes(ledger, bytes);
                    break;
                case "broken_link":
                    File.WriteAllText(markers, File.ReadAllText(markers).Replace("previous_commit_checksum\":\"", "previous_commit_checksum\":\"0", StringComparison.Ordinal));
                    break;
                case "fork":
                    File.WriteAllBytes(Path.Combine(root, "ledger.0001.jsonl"), Array.Empty<byte>());
                    File.WriteAllText(Path.Combine(root, "commits.0001.jsonl"), File.ReadAllLines(markers)[0] + "\n");
                    break;
                case "unknown_tail":
                    File.WriteAllText(Path.Combine(root, "unexpected.tail"), "tail");
                    break;
                case "marker_bound":
                    File.AppendAllText(markers, new string('x', RunStoreV4Storage.MaximumMarkerLogBytes + 1));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task UninterruptedResumeAndReplay_HaveEqualTerminalDigestsAcrossRecovery()
    {
        string uninterruptedRoot = NewTemporaryDirectory();
        string recoveredRoot = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("runstore_v4_digest_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(uninterruptedRoot, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, 2);

            IRunStoreFileSystem faulting = new FaultInjectingRunStoreFileSystem(
                PhysicalRunStoreFileSystem.Instance,
                RunStoreWriteKind.CommitMarker,
                0);
            SnowGlobeRunStore? interrupted = SnowGlobeRunStore.CreateNew(recoveredRoot, identity, faulting);
            try
            {
                await Assert.ThrowsAsync<IOException>(() => SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), interrupted, 1));
            }
            finally { interrupted.Dispose(); }
            using (SnowGlobeRunStore resumedStore = SnowGlobeRunStore.OpenForAppend(recoveredRoot))
            {
                SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(recoveredRoot));
                await SnowGlobePersistedRun.RunAsync(resumed, new IdleAdapter(), resumedStore, 2);
            }

            SnowGlobeRunLedger uninterrupted = SnowGlobeRunStore.Read(uninterruptedRoot);
            SnowGlobeRunLedger recovered = SnowGlobeRunStore.Read(recoveredRoot);
            SnowGlobeWorld uninterruptedWorld = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(uninterrupted);
            SnowGlobeWorld recoveredWorld = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(recovered);
            SnowGlobeWorld replayedWorld = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(recovered, identity);

            Assert.Equal(uninterruptedWorld.StateDigest(), recoveredWorld.StateDigest());
            Assert.Equal(uninterruptedWorld.EventDigest(), recoveredWorld.EventDigest());
            Assert.Equal(recoveredWorld.StateDigest(), replayedWorld.StateDigest());
            Assert.Equal(recoveredWorld.EventDigest(), replayedWorld.EventDigest());
        }
        finally
        {
            Directory.Delete(uninterruptedRoot, recursive: true);
            Directory.Delete(recoveredRoot, recursive: true);
        }
    }

    [Fact]
    public void FrozenV3Run_ReadsParticipantEvidenceButAppendIsRejectedWithoutMutation()
    {
        string source = NewTemporaryDirectory();
        string frozen = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity current = SnowGlobePersistedRun.Identity("frozen_v3_adapter/v1");
            SnowGlobeWorld initial = SnowGlobeWorld.Create(current.Seed, current.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(source, current))
            {
                SnowGlobeParticipantCommand command = new(
                    "participant-01", "frozen-v3", initial.Tick, initial.StateDigest(), initial.EventDigest(),
                    "agent-00", SnowGlobeActionKind.Idle, 0);
                Assert.True(store.EvaluateAndAppendParticipantCommand(command).Accepted);
            }

            SnowGlobeRunIdentity v3 = current with { SchemaVersion = SnowGlobeRunStore.PreviousSchemaVersion };
            byte[] header = JsonSerializer.SerializeToUtf8Bytes(v3, JsonOptions);
            string headerChecksum = Digest(header);
            SnowGlobeParticipantEvaluationRecord original = JsonSerializer.Deserialize<SnowGlobeParticipantEvaluationRecord>(File.ReadAllLines(Path.Combine(source, "ledger.jsonl")).Single(), JsonOptions)!;
            SnowGlobeParticipantEvaluationRecord unsigned = original with { HeaderChecksum = headerChecksum, Checksum = string.Empty };
            SnowGlobeParticipantEvaluationRecord signed = unsigned with { Checksum = ParticipantChecksum(unsigned) };
            File.WriteAllBytes(Path.Combine(frozen, "run.json"), header);
            File.WriteAllText(Path.Combine(frozen, "ledger.jsonl"), JsonSerializer.Serialize(signed, JsonOptions) + "\n");

            byte[] headerBefore = File.ReadAllBytes(Path.Combine(frozen, "run.json"));
            byte[] ledgerBefore = File.ReadAllBytes(Path.Combine(frozen, "ledger.jsonl"));
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(frozen);

            Assert.Equal(SnowGlobeRunStore.PreviousSchemaVersion, ledger.Identity.SchemaVersion);
            Assert.Single(ledger.ParticipantEvaluationRecords);
            Assert.Single(SnowGlobePersistedRun.Reconstruct(ledger).World.Events);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(frozen));
            Assert.False(File.Exists(Path.Combine(frozen, ".writer.lock")));
            Assert.Equal(headerBefore, File.ReadAllBytes(Path.Combine(frozen, "run.json")));
            Assert.Equal(ledgerBefore, File.ReadAllBytes(Path.Combine(frozen, "ledger.jsonl")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            Directory.Delete(frozen, recursive: true);
        }
    }

    private static void RewriteSingleFramePayload(string root, byte[] payload)
    {
        string markerPath = Path.Combine(root, "commits.jsonl");
        string[] markerLines = File.ReadAllLines(markerPath);
        Assert.Equal(2, markerLines.Length);
        RunStorePrepareMarker originalPrepare = JsonSerializer.Deserialize<RunStorePrepareMarker>(markerLines[0], JsonOptions)!;
        RunStoreCommitMarker originalCommit = JsonSerializer.Deserialize<RunStoreCommitMarker>(markerLines[1], JsonOptions)!;
        RunStorePrepareMarker unsignedPrepare = originalPrepare with
        {
            PayloadLength = payload.Length,
            PayloadChecksum = Digest(payload),
            PayloadPrefixManifest = PayloadPrefixManifest(payload),
            Checksum = string.Empty
        };
        RunStorePrepareMarker prepare = unsignedPrepare with { Checksum = PrepareChecksum(unsignedPrepare) };
        RunStoreCommitMarker unsignedCommit = originalCommit with
        {
            PrepareChecksum = prepare.Checksum,
            PayloadChecksum = prepare.PayloadChecksum,
            LedgerEndOffset = payload.Length,
            Checksum = string.Empty
        };
        RunStoreCommitMarker commit = unsignedCommit with { Checksum = CommitChecksum(unsignedCommit) };
        File.WriteAllBytes(Path.Combine(root, "ledger.jsonl"), payload);
        File.WriteAllBytes(markerPath, [.. SerializeLine(prepare), .. SerializeLine(commit)]);
    }

    private static void AppendFramedFrame(string root, int segmentIndex, RunStoreFrameKind kind, byte[] payload, int entryCount)
    {
        string ledgerPath = RunStoreV4Storage.LedgerPath(root, segmentIndex);
        string markerPath = RunStoreV4Storage.MarkerPath(root, segmentIndex);
        string[] markerLines = File.ReadAllLines(markerPath);
        RunStoreCommitMarker previous = JsonSerializer.Deserialize<RunStoreCommitMarker>(markerLines[^1], JsonOptions)!;
        int firstSequence = SnowGlobeRunStore.Read(root).EntryCount;
        RunStorePrepareMarker unsignedPrepare = new(
            "prepare", RunStoreV4Storage.MarkerSchema, segmentIndex, previous.FrameIndex + 1, previous.Checksum,
            kind.ToString(), firstSequence, entryCount, payload.Length, Digest(payload),
            kind == RunStoreFrameKind.ScheduledTick ? PayloadPrefixManifest(payload) : string.Empty,
            string.Empty);
        RunStorePrepareMarker prepare = unsignedPrepare with { Checksum = PrepareChecksum(unsignedPrepare) };
        int ledgerEndOffset = checked(File.ReadAllBytes(ledgerPath).Length + payload.Length);
        RunStoreCommitMarker unsignedCommit = new(
            "commit", RunStoreV4Storage.MarkerSchema, segmentIndex, prepare.FrameIndex, previous.Checksum,
            prepare.Checksum, prepare.PayloadChecksum, ledgerEndOffset, string.Empty);
        RunStoreCommitMarker commit = unsignedCommit with { Checksum = CommitChecksum(unsignedCommit) };
        AppendBytes(markerPath, SerializeLine(prepare));
        AppendBytes(ledgerPath, payload);
        AppendBytes(markerPath, SerializeLine(commit));
    }

    private static void AppendBytes(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
    }

    private static byte[] SerializeLine<T>(T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return [.. json, (byte)'\n'];
    }

    private static string LedgerChecksum(SnowGlobeLedgerRecord record) => Digest(Encoding.UTF8.GetBytes(
        $"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}|{record.HeaderChecksum}"));
    private static string PayloadPrefixManifest(byte[] payload)
    {
        int[] ends = RunStoreV4Storage.ScheduledPayloadChunkEnds(payload);
        return string.Join(';', ends[..^1].Select(end => $"{end}:{Digest(payload.AsSpan(0, end))}"));
    }
    private static string PrepareChecksum(RunStorePrepareMarker marker) => Digest(Encoding.UTF8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.FrameIndex}|{marker.PreviousCommitChecksum}|{marker.FrameKind}|{marker.FirstSequence}|{marker.EntryCount}|{marker.PayloadLength}|{marker.PayloadChecksum}|{marker.PayloadPrefixManifest}"));
    private static string CommitChecksum(RunStoreCommitMarker marker) => Digest(Encoding.UTF8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.FrameIndex}|{marker.PreviousCommitChecksum}|{marker.PrepareChecksum}|{marker.PayloadChecksum}|{marker.LedgerEndOffset}"));
    private static string ContinuationChecksum(RunStoreContinuationMarker marker) => Digest(Encoding.UTF8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.PreviousCommitChecksum}|{marker.SourceSegmentIndex}|{marker.SourceFrameIndex}|{marker.SourcePrepareChecksum}|{marker.SourceLedgerLength}|{marker.SourceLedgerChecksum}|{marker.SourceMarkerLength}|{marker.SourceMarkerChecksum}|{marker.Disposition}"));
    private static string ParticipantChecksum(SnowGlobeParticipantEvaluationRecord record) => Digest(Encoding.UTF8.GetBytes(
        $"{record.Sequence}|{record.Kind}|{record.Tick}|{record.ParticipantId}|{record.IdempotencyKey}|{record.ExpectedTick}|{record.ExpectedStateDigest}|{record.ExpectedEventDigest}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.AcceptedEventSequence}|{record.AcceptedStructureId}|{record.ResultingStateDigest}|{record.ResultingEventDigest}|{record.HeaderChecksum}"));
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-runstore-v4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class IdleAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle, 0));
        }
    }

    private sealed class LeaseMutatingRunStoreFileSystem(IRunStoreFileSystem inner, Action mutate) : IRunStoreFileSystem
    {
        private int _mutated;

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
        public bool FileExists(string path) => inner.FileExists(path);
        public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);
        public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.CreateFile(path, bytes, kind);
        public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind) => inner.AppendFile(path, bytes, kind);

        public IDisposable AcquireExclusiveLease(string path)
        {
            IDisposable lease = inner.AcquireExclusiveLease(path);
            if (Interlocked.CompareExchange(ref _mutated, 1, 0) == 0) mutate();
            return lease;
        }
    }
}
