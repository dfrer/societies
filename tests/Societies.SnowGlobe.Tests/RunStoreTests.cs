using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class RunStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightAgentRun_UninterruptedCheckpointResumeAndRecordedResponseReplay_HaveExactDigests(bool resilience)
    {
        string root = NewTemporaryDirectory();
        try
        {
            ISnowGlobeInferenceAdapter adapter = resilience ? new SnowGlobeResilienceFallbackAdapter() : new ScriptedInferenceAdapter();
            string adapterIdentity = resilience ? SnowGlobeResilienceFallbackAdapter.Identity : "snow_globe_scripted_adapter/v1";
            int ticks = resilience ? 1 : SnowGlobeScenario.FixedTicks;
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity(adapterIdentity);
            SnowGlobeWorld uninterruptedWorld = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            SnowGlobeRunResult uninterrupted = await new SequentialInferenceScheduler(adapter).RunAsync(uninterruptedWorld, ticks);

            string fullPath = Path.Combine(root, "full");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(fullPath, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), adapter, store, ticks);
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(fullPath);
            SnowGlobeWorld checkpointed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);
            SnowGlobeWorld replayed = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            SnowGlobeRunResult replay = await new SequentialInferenceScheduler(new SnowGlobeReplayAdapter(ledger)).RunAsync(replayed, ticks);

            string resumePath = Path.Combine(root, "resume");
            int interruptionTick = Math.Min(2, ticks);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(resumePath, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), adapter, store, interruptionTick);
            SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(resumePath));
            using (SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(resumePath))
                await SnowGlobePersistedRun.RunAsync(resumed, adapter, store, ticks);

            Assert.Equal(uninterrupted.StateDigest, checkpointed.StateDigest());
            Assert.Equal(uninterrupted.EventDigest, checkpointed.EventDigest());
            Assert.Equal(uninterrupted.StateDigest, replayed.StateDigest());
            Assert.Equal(uninterrupted.EventDigest, replay.EventDigest);
            Assert.Equal(uninterrupted.StateDigest, resumed.StateDigest());
            Assert.Equal(uninterrupted.EventDigest, resumed.EventDigest());
            Assert.Contains(ledger.Records, record => record.Kind == SnowGlobeLedgerKind.Checkpoint);
            Assert.Equal(SnowGlobePersistedRun.PromptIdentity, ledger.Identity.PromptIdentity);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(Corruption.Truncate)]
    [InlineData(Corruption.Checksum)]
    [InlineData(Corruption.OutOfOrderSequence)]
    [InlineData(Corruption.DuplicateSequence)]
    [InlineData(Corruption.UnknownAction)]
    [InlineData(Corruption.SchemaMismatch)]
    public async Task Reader_FailsClosedOnCorruptOrIncompatibleArtifacts(Corruption corruption)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new ScriptedInferenceAdapter(), store, 1);
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string headerPath = Path.Combine(root, "run.json");
            string[] lines = File.ReadAllLines(ledgerPath);
            switch (corruption)
            {
                case Corruption.Truncate: File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n{"); break;
                case Corruption.Checksum: File.WriteAllText(ledgerPath, string.Join("\n", lines).Replace("checksum\":\"", "checksum\":\"bad", StringComparison.Ordinal) + "\n"); break;
                case Corruption.OutOfOrderSequence: File.WriteAllText(ledgerPath, string.Join("\n", new[] { lines[1], lines[0] }.Concat(lines.Skip(2))) + "\n"); break;
                case Corruption.DuplicateSequence: File.WriteAllText(ledgerPath, lines[0] + "\n" + string.Join("\n", lines) + "\n"); break;
                case Corruption.UnknownAction: File.WriteAllText(ledgerPath, string.Join("\n", lines).Replace("GatherWood", "NotAnAction", StringComparison.Ordinal) + "\n"); break;
                case Corruption.SchemaMismatch: File.WriteAllText(headerPath, File.ReadAllText(headerPath).Replace(SnowGlobeRunStore.SchemaVersion, "snow_globe_run_store/v999", StringComparison.Ordinal)); break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ReaderAndReconstructor_NeverRewriteOriginalArtifacts()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new ScriptedInferenceAdapter(), store, 1);
            byte[] header = File.ReadAllBytes(Path.Combine(root, "run.json"));
            byte[] ledger = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            _ = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            Assert.Equal(header, File.ReadAllBytes(Path.Combine(root, "run.json")));
            Assert.Equal(ledger, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-runstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    public enum Corruption { Truncate, Checksum, OutOfOrderSequence, DuplicateSequence, UnknownAction, SchemaMismatch }
}
