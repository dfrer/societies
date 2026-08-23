using System.Text;
using System.Text.Json;
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
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }

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
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }

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
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity, faulting))
            {
                await Assert.ThrowsAsync<IOException>(async () =>
                    await SnowGlobePersistedRun.RunAsync(
                        SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, 1));
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
    public void CorruptRun_ReturnsNoSnapshotOrParserText()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = NewIdentity();
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }
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
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }

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
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }
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
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }
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

    private static async Task WriteTicksAsync(string root, SnowGlobeRunIdentity identity, int ticks)
    {
        using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity);
        await SnowGlobePersistedRun.RunAsync(
            SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new IdleAdapter(), store, ticks);
    }

    private static SnowGlobeRunIdentity NewIdentity() =>
        SnowGlobePersistedRun.Identity("persisted_run_inspector_adapter/v1");

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

    private sealed class IdleAdapter : ISnowGlobeInferenceAdapter
    {
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
