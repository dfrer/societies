using Societies.SnowGlobe;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            SnowGlobeRunResult replay = await new SequentialInferenceScheduler(new SnowGlobeReplayAdapter(ledger, adapterIdentity)).RunAsync(replayed, ticks);

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

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("deep")]
    [InlineData("oversized")]
    public async Task Reader_FailsClosedOnNonCanonicalOrOversizedHeader(string mutation)
    {
        string root = await WriteOneTickAsync();
        try
        {
            string headerPath = Path.Combine(root, "run.json");
            string header = File.ReadAllText(headerPath);
            string mutated = mutation switch
            {
                "unknown" => header[..^1] + ",\"ignored\":0}",
                "duplicate" => header[..^1] + ",\"prompt_identity\":\"" + SnowGlobePersistedRun.PromptIdentity + "\"}",
                "deep" => header[..^1] + ",\"ignored\":[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]}",
                "oversized" => new string(' ', 8192) + header,
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            File.WriteAllText(headerPath, mutated);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task HeaderRead_UsesTheExactBoundAndRejectsTheSingleByteOverflowWithoutRewrite()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string headerPath = Path.Combine(root, "run.json");
            byte[] original = File.ReadAllBytes(headerPath);
            Assert.True(original.Length < SnowGlobeRunStore.MaximumHeaderBytes);
            byte[] exact = Encoding.UTF8.GetBytes(new string(' ', SnowGlobeRunStore.MaximumHeaderBytes - original.Length) + File.ReadAllText(headerPath));
            Assert.Equal(SnowGlobeRunStore.MaximumHeaderBytes, exact.Length);
            File.WriteAllBytes(headerPath, exact);
            _ = SnowGlobeRunStore.Read(root);
            Assert.Equal(exact, File.ReadAllBytes(headerPath));

            byte[] overflow = Encoding.UTF8.GetBytes(" " + Encoding.UTF8.GetString(exact));
            Assert.Equal(SnowGlobeRunStore.MaximumHeaderBytes + 1, overflow.Length);
            File.WriteAllBytes(headerPath, overflow);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Equal(overflow, File.ReadAllBytes(headerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Read_RejectsChecksumValidFinalLedgerRecordWithoutTerminatingLineFeedWithoutRewrite()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            byte[] complete = File.ReadAllBytes(ledgerPath);
            Assert.Equal((byte)'\n', complete[^1]);
            byte[] incomplete = complete[..^1];
            File.WriteAllBytes(ledgerPath, incomplete);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Equal(incomplete, File.ReadAllBytes(ledgerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task OpenForAppend_RejectsFinalLedgerRecordWithoutTerminatingLineFeedWithoutRewrite()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            byte[] complete = File.ReadAllBytes(ledgerPath);
            byte[] incomplete = complete[..^1];
            File.WriteAllBytes(ledgerPath, incomplete);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
            Assert.Equal(incomplete, File.ReadAllBytes(ledgerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Read_AcceptsALedgerWhoseEveryRecordIsLineFeedTerminated()
    {
        string root = await WriteOneTickAsync();
        try
        {
            byte[] ledger = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            Assert.Equal((byte)'\n', ledger[^1]);
            Assert.NotEmpty(SnowGlobeRunStore.Read(root).Records);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("oversized")]
    public async Task Reader_FailsClosedOnNonCanonicalOrOversizedLedgerLine(string mutation)
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string[] lines = File.ReadAllLines(ledgerPath);
            lines[0] = mutation switch
            {
                "unknown" => lines[0][..^1] + ",\"ignored\":0}",
                "duplicate" => lines[0][..^1] + ",\"action\":\"" + ActionFromJson(lines[0]) + "\"}",
                "oversized" => new string(' ', 8192) + lines[0],
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n");
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_FailsClosedOnDeepLedgerJson()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string[] lines = File.ReadAllLines(ledgerPath);
            lines[0] = lines[0][..^1] + ",\"ignored\":[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]}";
            File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n");
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsCanonicalChecksumWithNumericAction()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string[] lines = File.ReadAllLines(ledgerPath);
            SnowGlobeLedgerRecord record = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(lines[0], JsonOptions)!;
            SnowGlobeLedgerRecord numeric = record with { Action = "1", Checksum = string.Empty };
            numeric = numeric with { Checksum = Checksum(numeric) };
            lines[0] = JsonSerializer.Serialize(numeric, JsonOptions);
            File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n");
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsHeaderProvenanceTamperingEvenWhenLedgerChecksumsRemainValid()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string headerPath = Path.Combine(root, "run.json");
            File.WriteAllText(headerPath, File.ReadAllText(headerPath).Replace(SnowGlobePersistedRun.RulesIdentity, "snow_globe_domain_rules/v2", StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task OpenForAppend_RecoversAStaleLockFileAndRejectsALiveWriter()
    {
        string root = await WriteOneTickAsync();
        try
        {
            string lockPath = Path.Combine(root, ".writer.lock");
            File.WriteAllText(lockPath, "stale");
            using (SnowGlobeRunStore recovered = SnowGlobeRunStore.OpenForAppend(root)) { }
            using SnowGlobeRunStore writer = SnowGlobeRunStore.OpenForAppend(root);
            Assert.ThrowsAny<IOException>(() => SnowGlobeRunStore.OpenForAppend(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(ScheduleMutation.Incomplete)]
    [InlineData(ScheduleMutation.Duplicate)]
    [InlineData(ScheduleMutation.Reordered)]
    public async Task Reconstructor_RequiresEveryIdentityAgentExactlyOncePerTick(ScheduleMutation mutation)
    {
        string root = await WriteOneTickAsync();
        try
        {
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            List<SnowGlobeLedgerRecord> records = ledger.Records.ToList();
            int firstTurnLength = records[3].Kind == SnowGlobeLedgerKind.Event ? 4 : 3;
            if (mutation == ScheduleMutation.Incomplete)
                records.RemoveRange(records.Count - firstTurnLength - 1, firstTurnLength);
            else if (mutation == ScheduleMutation.Duplicate)
                ReplaceAgent(records, firstTurnLength, records[0].AgentId);
            else
                SwapTurns(records, 0, firstTurnLength);

            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.ResumeAtLatestCheckpoint(new SnowGlobeRunLedger(ledger.Identity, records)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task WholeTickCapacityFailure_WritesNoPartialTick()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
            using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity);
            for (int index = 0; index < SnowGlobeRunStore.MaximumLedgerRecords - 2; index++)
                store.AppendCheckpoint(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount));
            byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new ScriptedInferenceAdapter(), store, 1));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reconstructor_RejectsUnexpectedRunIdentity()
    {
        string root = await WriteOneTickAsync();
        try
        {
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeRunIdentity unexpected = ledger.Identity with { AdapterIdentity = "different_adapter/v1" };
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger, unexpected));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(IdentityMutation.V1Schema)]
    [InlineData(IdentityMutation.Rules)]
    [InlineData(IdentityMutation.Prompt)]
    public void CreateNew_RejectsUnsupportedV2IdentityTuplesBeforeArtifactsExist(IdentityMutation mutation)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = MutateIdentity(SnowGlobePersistedRun.Identity("provider_neutral_adapter/v1"), mutation);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.CreateNew(root, identity));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(IdentityMutation.V1Schema)]
    [InlineData(IdentityMutation.Rules)]
    [InlineData(IdentityMutation.Prompt)]
    public async Task ReadAndReconstruction_RejectUnsupportedIdentityTuplesByDefault(IdentityMutation mutation)
    {
        string root = await WriteOneTickAsync();
        try
        {
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeRunIdentity unsupported = MutateIdentity(ledger.Identity, mutation);
            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.ResumeAtLatestCheckpoint(new SnowGlobeRunLedger(unsupported, ledger.Records)));
            string headerPath = Path.Combine(root, "run.json");
            string original = File.ReadAllText(headerPath);
            string replacement = mutation switch
            {
                IdentityMutation.V1Schema => original.Replace(SnowGlobeRunStore.SchemaVersion, "snow_globe_run_store/v1", StringComparison.Ordinal),
                IdentityMutation.Rules => original.Replace(SnowGlobePersistedRun.RulesIdentity, "unsupported_rules/v1", StringComparison.Ordinal),
                IdentityMutation.Prompt => original.Replace(SnowGlobePersistedRun.PromptIdentity, "unsupported_prompt/v1", StringComparison.Ordinal),
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            File.WriteAllText(headerPath, replacement);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ReplayAdapter_RequiresAnExactExpectedProviderNeutralIdentity()
    {
        string root = await WriteOneTickAsync();
        try
        {
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            Assert.Throws<InvalidDataException>(() => new SnowGlobeReplayAdapter(ledger, "other_provider_neutral_adapter/v1"));
            SnowGlobeRunLedger unsupportedRules = new(ledger.Identity with { RulesIdentity = "unsupported_rules/v1" }, ledger.Records);
            Assert.Throws<InvalidDataException>(() => new SnowGlobeReplayAdapter(unsupportedRules, ledger.Identity.AdapterIdentity));
            SnowGlobeRunLedger unsupportedPrompt = new(ledger.Identity with { PromptIdentity = "unsupported_prompt/v1" }, ledger.Records);
            Assert.Throws<InvalidDataException>(() => new SnowGlobeReplayAdapter(unsupportedPrompt, ledger.Identity.AdapterIdentity));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(WorldMismatch.Seed)]
    [InlineData(WorldMismatch.AgentCount)]
    [InlineData(WorldMismatch.Tick)]
    [InlineData(WorldMismatch.Digest)]
    public async Task RunAsync_RejectsWorldStoreMismatchesBeforeAnyLedgerWrite(WorldMismatch mismatch)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
            SnowGlobeWorld world = mismatch switch
            {
                WorldMismatch.Seed => SnowGlobeWorld.Create(identity.Seed + 1, identity.AgentCount),
                WorldMismatch.AgentCount => SnowGlobeWorld.Create(identity.Seed, identity.AgentCount + 1),
                _ => SnowGlobeWorld.Create(identity.Seed, identity.AgentCount)
            };
            int targetTick = 1;
            if (mismatch == WorldMismatch.Tick) { world.AdvanceTick(); targetTick = 2; }
            if (mismatch == WorldMismatch.Digest) Assert.True(world.ValidateAndCommit(new SnowGlobeActionProposal(world.Agents.First().AgentId, SnowGlobeActionKind.Idle, 0)).Accepted);
            using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity);
            byte[] header = File.ReadAllBytes(Path.Combine(root, "run.json"));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SnowGlobePersistedRun.RunAsync(world, new ScriptedInferenceAdapter(), store, targetTick));
            Assert.False(File.Exists(Path.Combine(root, "ledger.jsonl")));
            Assert.Equal(header, File.ReadAllBytes(Path.Combine(root, "run.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task OpenForAppend_RejectsAWorldThatDoesNotMatchTheLatestCheckpointBeforeWrites()
    {
        string root = await WriteOneTickAsync();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
            byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            using SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new ScriptedInferenceAdapter(), store, 1));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConcurrentRunCalls_SerializeAsOneCompleteTickWithoutPartialArtifacts()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("concurrent_provider_neutral_adapter/v1");
            SnowGlobeWorld world = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity);
            YieldingIdleAdapter adapter = new();
            await Task.WhenAll(
                SnowGlobePersistedRun.RunAsync(world, adapter, store, 1),
                SnowGlobePersistedRun.RunAsync(world, adapter, store, 1));
            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            Assert.Equal(1, world.Tick);
            Assert.Equal(1, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger).Tick);
            Assert.Equal(ledger.Records.Count, ledger.Records.Select(record => record.Sequence).Distinct().Count());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task<string> WriteOneTickAsync()
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("snow_globe_scripted_adapter/v1");
        using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new ScriptedInferenceAdapter(), store, 1);
        return root;
    }

    private static SnowGlobeRunIdentity MutateIdentity(SnowGlobeRunIdentity identity, IdentityMutation mutation) => mutation switch
    {
        IdentityMutation.V1Schema => identity with { SchemaVersion = "snow_globe_run_store/v1" },
        IdentityMutation.Rules => identity with { RulesIdentity = "unsupported_rules/v1" },
        IdentityMutation.Prompt => identity with { PromptIdentity = "unsupported_prompt/v1" },
        _ => throw new ArgumentOutOfRangeException(nameof(mutation))
    };

    private static void ReplaceAgent(List<SnowGlobeLedgerRecord> records, int start, string agentId)
    {
        for (int index = start; index < start + 3; index++) records[index] = records[index] with { AgentId = agentId };
        if (start + 3 < records.Count && records[start + 3].Kind == SnowGlobeLedgerKind.Event) records[start + 3] = records[start + 3] with { AgentId = agentId };
    }

    private static void SwapTurns(List<SnowGlobeLedgerRecord> records, int firstStart, int secondStart)
    {
        int firstLength = records[firstStart + 3].Kind == SnowGlobeLedgerKind.Event ? 4 : 3;
        int secondLength = records[secondStart + 3].Kind == SnowGlobeLedgerKind.Event ? 4 : 3;
        List<SnowGlobeLedgerRecord> first = records.GetRange(firstStart, firstLength);
        List<SnowGlobeLedgerRecord> second = records.GetRange(secondStart, secondLength);
        records.RemoveRange(firstStart, firstLength + secondLength);
        records.InsertRange(firstStart, second);
        records.InsertRange(firstStart + second.Count, first);
    }

    private static string ActionFromJson(string line) => JsonDocument.Parse(line).RootElement.GetProperty("action").GetString()!;
    private static string Checksum(SnowGlobeLedgerRecord record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}|{record.HeaderChecksum}"))).ToLowerInvariant();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private sealed class YieldingIdleAdapter : ISnowGlobeInferenceAdapter
    {
        public async ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            return new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle, 0);
        }
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-runstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    public enum Corruption { Truncate, Checksum, OutOfOrderSequence, DuplicateSequence, UnknownAction, SchemaMismatch }
    public enum ScheduleMutation { Incomplete, Duplicate, Reordered }
    public enum IdentityMutation { V1Schema, Rules, Prompt }
    public enum WorldMismatch { Seed, AgentCount, Tick, Digest }
}
