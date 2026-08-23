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
            SnowGlobeWorld world = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            await SnowGlobePersistedRun.RunAsync(world, new YieldingIdleAdapter(), store, 124);
            byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            Assert.Equal(4092, SnowGlobeRunStore.Read(root).EntryCount);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SnowGlobePersistedRun.RunAsync(world, new YieldingIdleAdapter(), store, 125));
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
            byte[] ledger = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SnowGlobePersistedRun.RunAsync(world, new ScriptedInferenceAdapter(), store, targetTick));
            Assert.Empty(ledger);
            Assert.Equal(ledger, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
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

    [Fact]
    public void FrozenV2Run_ReadsAndReconstructsButAppendIsRejectedWithoutMutation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            WriteFrozenV2OneTick(root);
            byte[] headerBefore = File.ReadAllBytes(Path.Combine(root, "run.json"));
            byte[] ledgerBefore = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeWorld world = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);

            Assert.Equal(SnowGlobeRunStore.LegacySchemaVersion, ledger.Identity.SchemaVersion);
            Assert.Null(ledger.Identity.ParticipantCommandIdentity);
            Assert.Empty(ledger.ParticipantEvaluationRecords);
            Assert.Equal(1, world.Tick);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.OpenForAppend(root));
            Assert.False(File.Exists(Path.Combine(root, ".writer.lock")));
            Assert.Equal(headerBefore, File.ReadAllBytes(Path.Combine(root, "run.json")));
            Assert.Equal(ledgerBefore, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void V3CreateReadAndReconstruct_UsesExactParticipantIdentityAndFlatEvaluation()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("participant_store_adapter/v1");
            SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            SnowGlobeParticipantCommand command = ParticipantCommand(initial, "participant-01", "command-001", SnowGlobeActionKind.Idle);
            SnowGlobeParticipantCommandReceipt receipt;
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                receipt = store.EvaluateAndAppendParticipantCommand(command);

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(ledger);
            SnowGlobeParticipantEvaluationRecord evaluation = Assert.Single(ledger.ParticipantEvaluationRecords);
            using JsonDocument header = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "run.json")));
            HashSet<string> headerNames = header.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(SnowGlobeRunStore.SchemaVersion, ledger.Identity.SchemaVersion);
            Assert.Equal(SnowGlobeRunStore.ParticipantCommandIdentity, ledger.Identity.ParticipantCommandIdentity);
            Assert.True(headerNames.SetEquals(new[] { "schema_version", "rules_identity", "prompt_identity", "adapter_identity", "seed", "agent_count", "participant_command_identity" }));
            Assert.Equal(SnowGlobeLedgerKind.ParticipantEvaluation, evaluation.Kind);
            Assert.True(receipt.Accepted);
            Assert.Equal(0, receipt.ResultingEventSequence);
            Assert.Equal(receipt, reconstruction.ParticipantReceipts[new SnowGlobeParticipantCommandKey("participant-01", "command-001")]);
            Assert.Equal(receipt.ResultingStateDigest, reconstruction.World.StateDigest());
            Assert.Equal(receipt.ResultingEventDigest, reconstruction.World.EventDigest());

            string ledgerText = File.ReadAllText(Path.Combine(root, "ledger.jsonl"));
            Assert.DoesNotContain("\"response\"", ledgerText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"prompt\"", ledgerText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"message\"", ledgerText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"participant_text\"", ledgerText, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("\n", ledgerText, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task EmptyV3CreateDisposeReadOpenAndZeroTickRun_AreValidWithoutWrites()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("empty_store_adapter/v1");
            using (SnowGlobeRunStore.CreateNew(root, identity)) { }
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            Assert.True(File.Exists(ledgerPath));
            Assert.Empty(File.ReadAllBytes(ledgerPath));

            SnowGlobeRunLedger empty = SnowGlobeRunStore.Read(root);
            Assert.Equal(0, empty.EntryCount);
            Assert.Equal(0, SnowGlobePersistedRun.ResumeAtLatestCheckpoint(empty).Tick);
            using (SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root))
            {
                SnowGlobeWorld world = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
                SnowGlobePersistedRunResult zeroTick = await SnowGlobePersistedRun.RunAsync(world, new YieldingIdleAdapter(), reopened, 0);
                Assert.Equal(0, zeroTick.World.Tick);
            }
            Assert.Empty(File.ReadAllBytes(ledgerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(OperationLeaseWindow.BeforeFirstTick)]
    [InlineData(OperationLeaseWindow.BetweenTicks)]
    [InlineData(OperationLeaseWindow.PostFinalBeforeRelease)]
    public async Task ParticipantEvaluation_CannotAppendWhileRunAsyncOwnsOperationLease(OperationLeaseWindow window)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("operation_lease_adapter/v1");
            SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity);
            TaskCompletionSource<bool> entered = NewSignal();
            TaskCompletionSource<bool> release = NewSignal();
            ISnowGlobeInferenceAdapter adapter;
            int targetTick;
            if (window == OperationLeaseWindow.PostFinalBeforeRelease)
            {
                adapter = new YieldingIdleAdapter();
                targetTick = 1;
                store.BeforeOperationLeaseReleaseForTesting = async () =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                };
            }
            else
            {
                int blockCall = window == OperationLeaseWindow.BeforeFirstTick ? 0 : identity.AgentCount;
                adapter = new BlockingInferenceAdapter(blockCall, entered, release);
                targetTick = window == OperationLeaseWindow.BeforeFirstTick ? 1 : 2;
            }

            Task<SnowGlobePersistedRunResult> running = SnowGlobePersistedRun.RunAsync(live, adapter, store, targetTick);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
            SnowGlobeParticipantCommand command = ParticipantCommand(live, "participant-01", $"blocked-{window.ToString().ToLowerInvariant()}", SnowGlobeActionKind.Idle);

            SnowGlobeParticipantCommandReceipt blocked = store.EvaluateAndAppendParticipantCommand(command);

            Assert.Equal("operation_in_progress", blocked.RejectionReason);
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            release.TrySetResult(true);
            await running;
            store.BeforeOperationLeaseReleaseForTesting = null;
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(ScheduledFailure.Adapter, 0)]
    [InlineData(ScheduledFailure.Adapter, 4)]
    [InlineData(ScheduledFailure.Adapter, 7)]
    [InlineData(ScheduledFailure.Cancellation, 0)]
    [InlineData(ScheduledFailure.Cancellation, 4)]
    [InlineData(ScheduledFailure.Cancellation, 7)]
    [InlineData(ScheduledFailure.Validation, 0)]
    [InlineData(ScheduledFailure.Validation, 4)]
    [InlineData(ScheduledFailure.Validation, 7)]
    public async Task FailedScheduledTick_LeavesLastCheckpointBytesAndStoreReusable(ScheduledFailure failure, int failingOrdinal)
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("framed_failure_adapter/v1");
            SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                await SnowGlobePersistedRun.RunAsync(live, new YieldingIdleAdapter(), store, 1);
                byte[] checkpointBytes = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
                SnowGlobeWorld checkpoint = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                Assert.Equal(1, checkpoint.Tick);

                using CancellationTokenSource cancellation = new();
                ISnowGlobeInferenceAdapter failing = new FailingInferenceAdapter(failure, failingOrdinal, cancellation);
                if (failure == ScheduledFailure.Cancellation)
                {
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                        await SnowGlobePersistedRun.RunAsync(live, failing, store, 2, cancellationToken: cancellation.Token));
                }
                else
                {
                    await Assert.ThrowsAnyAsync<Exception>(async () =>
                        await SnowGlobePersistedRun.RunAsync(live, failing, store, 2));
                }

                Assert.Equal(checkpointBytes, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
                SnowGlobeWorld resumedAfterFailure = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                Assert.Equal(checkpoint.StateDigest(), resumedAfterFailure.StateDigest());
                Assert.Equal(checkpoint.EventDigest(), resumedAfterFailure.EventDigest());

                await SnowGlobePersistedRun.RunAsync(resumedAfterFailure, new YieldingIdleAdapter(), store, 2);
                Assert.Equal(2, resumedAfterFailure.Tick);
            }

            using SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root);
            SnowGlobeWorld reopenedWorld = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            Assert.Equal(2, reopenedWorld.Tick);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(AppendFaultPath.ScheduledBatch)]
    [InlineData(AppendFaultPath.ParticipantEvaluation)]
    public async Task DeterministicPreWriteAppendFault_PoisonsLiveWriterAcrossBothApisAndReopensFromUnchangedBytes(AppendFaultPath path)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunStore? store = null;
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("append_fault_adapter/v1");
            store = SnowGlobeRunStore.CreateNew(root, identity);
            SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            await SnowGlobePersistedRun.RunAsync(live, new YieldingIdleAdapter(), store, 1);
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            byte[] checkpointBytes = File.ReadAllBytes(ledgerPath);
            SnowGlobeWorld checkpoint = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            SnowGlobeParticipantCommand command = ParticipantCommand(checkpoint, "participant-01", "faulted-command", SnowGlobeActionKind.Idle);
            store.BeforeLedgerAppendFlushForTesting = () => throw new IOException("deterministic_pre_write_append_fault");

            if (path == AppendFaultPath.ScheduledBatch)
            {
                await Assert.ThrowsAsync<IOException>(async () =>
                    await SnowGlobePersistedRun.RunAsync(checkpoint, new YieldingIdleAdapter(), store, 2));
            }
            else
            {
                Assert.Throws<IOException>(() => store.EvaluateAndAppendParticipantCommand(command));
            }

            Assert.Equal(checkpointBytes, File.ReadAllBytes(ledgerPath));
            SnowGlobeWorld stable = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await SnowGlobePersistedRun.RunAsync(stable, new YieldingIdleAdapter(), store, 2));
            Assert.Throws<InvalidDataException>(() => store.EvaluateAndAppendParticipantCommand(command));
            Assert.Equal(checkpointBytes, File.ReadAllBytes(ledgerPath));

            store.Dispose();
            store = null;
            SnowGlobeWorld reopenedWorld = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
            Assert.Equal(1, reopenedWorld.Tick);
            using SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root);
            if (path == AppendFaultPath.ScheduledBatch)
            {
                await SnowGlobePersistedRun.RunAsync(reopenedWorld, new YieldingIdleAdapter(), reopened, 2);
                Assert.Equal(2, reopenedWorld.Tick);
            }
            else
            {
                Assert.True(reopened.EvaluateAndAppendParticipantCommand(ParticipantCommand(reopenedWorld, "participant-01", "faulted-command", SnowGlobeActionKind.Idle)).Accepted);
            }
        }
        finally
        {
            store?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReconstructedParticipantReceiptIndex_IsAReadOnlyCopy()
    {
        string root = WriteOneParticipantEvaluation(stale: false);
        try
        {
            SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root));
            IDictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt> downcast =
                Assert.IsAssignableFrom<IDictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt>>(reconstruction.ParticipantReceipts);
            SnowGlobeParticipantCommandKey injected = new("participant-02", "injected");
            SnowGlobeParticipantCommandReceipt receipt = reconstruction.ParticipantReceipts.Values.Single();

            Assert.Throws<NotSupportedException>(() => downcast.Add(injected, receipt));
            Assert.DoesNotContain(injected, reconstruction.ParticipantReceipts.Keys);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    public static IEnumerable<object[]> StandardShapeTamperCases =>
        Enum.GetValues<StandardShapeTamper>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(StandardShapeTamperCases))]
    public async Task Reader_RejectsChecksumValidDataInEveryKindSpecificIrrelevantField(StandardShapeTamper tamper)
    {
        string root = await WriteOneTickAsync();
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string[] lines = File.ReadAllLines(ledgerPath);
            SnowGlobeLedgerKind kind = StandardShapeKind(tamper);
            int lineIndex = Array.FindIndex(lines, line =>
                JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(line, JsonOptions)!.Kind == kind);
            Assert.True(lineIndex >= 0);
            SnowGlobeLedgerRecord original = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(lines[lineIndex], JsonOptions)!;
            SnowGlobeLedgerRecord unsigned = MutateStandardShape(original, tamper) with { Checksum = string.Empty };
            SnowGlobeLedgerRecord signed = unsigned with { Checksum = Checksum(unsigned) };
            lines[lineIndex] = JsonSerializer.Serialize(signed, JsonOptions);
            File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n");

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reader_RejectsChecksumValidNonAllowlistedRejectedCommitReason()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("rejected_reason_adapter/v1");
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
                await SnowGlobePersistedRun.RunAsync(SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), new RejectingInferenceAdapter(), store, 1);

            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string[] lines = File.ReadAllLines(ledgerPath);
            int commitIndex = Array.FindIndex(lines, line =>
            {
                SnowGlobeLedgerRecord record = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(line, JsonOptions)!;
                return record.Kind == SnowGlobeLedgerKind.Commit && record.Accepted == false;
            });
            Assert.True(commitIndex >= 0);
            SnowGlobeLedgerRecord rejected = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(lines[commitIndex], JsonOptions)!;
            SnowGlobeLedgerRecord unsigned = rejected with { RejectionReason = "arbitrary_text", Checksum = string.Empty };
            lines[commitIndex] = JsonSerializer.Serialize(unsigned with { Checksum = Checksum(unsigned) }, JsonOptions);
            File.WriteAllText(ledgerPath, string.Join("\n", lines) + "\n");

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ParticipantEvaluations_BeforeAndBetweenScheduledTicks_PreserveOneEventSequence()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("mixed_participant_adapter/v1");
            SnowGlobeWorld live = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                SnowGlobeParticipantCommand before = ParticipantCommand(live, "participant-01", "before", SnowGlobeActionKind.Idle);
                Assert.True(store.EvaluateAndAppendParticipantCommand(before).Accepted);
                Assert.True(live.ValidateAndCommit(new SnowGlobeActionProposal(before.TargetAgentId!, before.Action!.Value, before.Quantity!.Value)).Accepted);
                await SnowGlobePersistedRun.RunAsync(live, new ScriptedInferenceAdapter(), store, 1);

                SnowGlobeParticipantCommand between = ParticipantCommand(live, "participant-01", "between", SnowGlobeActionKind.Idle);
                Assert.True(store.EvaluateAndAppendParticipantCommand(between).Accepted);
                Assert.True(live.ValidateAndCommit(new SnowGlobeActionProposal(between.TargetAgentId!, between.Action!.Value, between.Quantity!.Value)).Accepted);
                await SnowGlobePersistedRun.RunAsync(live, new ScriptedInferenceAdapter(), store, 2);
            }

            SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(root);
            SnowGlobeWorld reconstructed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);
            SnowGlobeParticipantEvaluationRecord[] evaluations = ledger.ParticipantEvaluationRecords.OrderBy(entry => entry.Sequence).ToArray();
            SnowGlobeLedgerRecord firstCheckpoint = ledger.Records.First(record => record.Kind == SnowGlobeLedgerKind.Checkpoint);

            Assert.Equal(2, evaluations.Length);
            Assert.Equal(0, evaluations[0].Sequence);
            Assert.True(evaluations[1].Sequence > firstCheckpoint.Sequence);
            Assert.Equal(live.StateDigest(), reconstructed.StateDigest());
            Assert.Equal(live.EventDigest(), reconstructed.EventDigest());
            Assert.Equal(Enumerable.Range(0, reconstructed.Events.Count), reconstructed.Events.Select(entry => entry.Sequence));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AcceptedStaleAndDomainReject_ResumeExactlyAndRebuildScopedIdempotency()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("participant_resume_adapter/v1");
            SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            SnowGlobeParticipantCommand accepted = ParticipantCommand(initial, "participant-01", "accepted", SnowGlobeActionKind.Idle);
            SnowGlobeParticipantCommandReceipt acceptedReceipt;
            SnowGlobeParticipantCommand stale;
            SnowGlobeParticipantCommandReceipt staleReceipt;
            SnowGlobeParticipantCommand domainReject;
            SnowGlobeParticipantCommandReceipt domainReceipt;
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                acceptedReceipt = store.EvaluateAndAppendParticipantCommand(accepted);
                stale = new SnowGlobeParticipantCommand("participant-01", "stale", 1, acceptedReceipt.ResultingStateDigest, acceptedReceipt.ResultingEventDigest, "agent-00", SnowGlobeActionKind.Idle, 0);
                staleReceipt = store.EvaluateAndAppendParticipantCommand(stale);
                domainReject = new SnowGlobeParticipantCommand("participant-01", "domain", 0, acceptedReceipt.ResultingStateDigest, acceptedReceipt.ResultingEventDigest, "agent-00", SnowGlobeActionKind.BuildShelter, 0);
                domainReceipt = store.EvaluateAndAppendParticipantCommand(domainReject);

                int bytesBeforeDuplicate = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length;
                Assert.Equal(acceptedReceipt, store.EvaluateAndAppendParticipantCommand(accepted));
                SnowGlobeParticipantCommandReceipt conflict = store.EvaluateAndAppendParticipantCommand(accepted with { Quantity = 1 });
                Assert.Equal("command_id_conflict", conflict.RejectionReason);
                Assert.Equal(bytesBeforeDuplicate, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")).Length);
            }

            Assert.True(acceptedReceipt.Accepted);
            Assert.Equal("stale_tick", staleReceipt.RejectionReason);
            Assert.Equal(0, staleReceipt.ResultingEventSequence);
            Assert.Equal("insufficient_resources_or_invalid_action", domainReceipt.RejectionReason);
            Assert.Equal(0, domainReceipt.ResultingEventSequence);

            using (SnowGlobeRunStore reopened = SnowGlobeRunStore.OpenForAppend(root))
            {
                Assert.Equal(staleReceipt, reopened.EvaluateAndAppendParticipantCommand(stale));
                SnowGlobeParticipantCommand otherParticipant = accepted with
                {
                    ParticipantId = "participant-02",
                    ExpectedStateDigest = acceptedReceipt.ResultingStateDigest,
                    ExpectedEventDigest = acceptedReceipt.ResultingEventDigest
                };
                Assert.True(reopened.EvaluateAndAppendParticipantCommand(otherParticipant).Accepted);
            }

            SnowGlobeRunReconstruction reconstruction = SnowGlobePersistedRun.Reconstruct(SnowGlobeRunStore.Read(root));
            Assert.Equal(4, reconstruction.ParticipantReceipts.Count);
            Assert.Equal(staleReceipt, reconstruction.ParticipantReceipts[new SnowGlobeParticipantCommandKey("participant-01", "stale")]);
            Assert.Contains(new SnowGlobeParticipantCommandKey("participant-02", "accepted"), reconstruction.ParticipantReceipts.Keys);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Reconstructor_RejectsParticipantEvaluationInsideAnAgentSchedule()
    {
        string root = WriteOneParticipantEvaluation(stale: false);
        try
        {
            SnowGlobeRunLedger ledger;
            SnowGlobeWorld live;
            using (SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(root))
            {
                live = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(SnowGlobeRunStore.Read(root));
                await SnowGlobePersistedRun.RunAsync(live, new ScriptedInferenceAdapter(), store, 1);
            }
            ledger = SnowGlobeRunStore.Read(root);

            SnowGlobeParticipantEvaluationRecord evaluation = ledger.ParticipantEvaluationRecords.Single() with { Sequence = 1 };
            List<SnowGlobeLedgerRecord> records = ledger.Records
                .Select(record => record.Sequence == 1 ? record with { Sequence = 0 } : record)
                .ToList();
            SnowGlobeRunLedger illegal = new(ledger.Identity, records, new[] { evaluation });

            Assert.Throws<InvalidDataException>(() => SnowGlobePersistedRun.Reconstruct(illegal));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(ParticipantTamper.Disposition)]
    [InlineData(ParticipantTamper.EventSequence)]
    [InlineData(ParticipantTamper.ResultingStateDigest)]
    [InlineData(ParticipantTamper.ExpectedAnchor)]
    [InlineData(ParticipantTamper.CanonicalAction)]
    [InlineData(ParticipantTamper.ArbitraryReason)]
    [InlineData(ParticipantTamper.AcceptedStructureText)]
    public void ReaderAndReconstructor_RejectChecksumValidParticipantSemanticTamper(ParticipantTamper tamper)
    {
        string root = WriteOneParticipantEvaluation(stale: tamper == ParticipantTamper.ArbitraryReason);
        try
        {
            SnowGlobeParticipantEvaluationRecord original = ReadOnlyParticipantRecord(root);
            SnowGlobeParticipantEvaluationRecord mutated = tamper switch
            {
                ParticipantTamper.Disposition => original with { Accepted = false, RejectionReason = "stale_tick", AcceptedEventSequence = null, AcceptedStructureId = null },
                ParticipantTamper.EventSequence => original with { AcceptedEventSequence = original.AcceptedEventSequence + 1 },
                ParticipantTamper.ResultingStateDigest => original with { ResultingStateDigest = new string('0', 64) },
                ParticipantTamper.ExpectedAnchor => original with { ExpectedStateDigest = new string('0', 64) },
                ParticipantTamper.CanonicalAction => original with { Action = "idle" },
                ParticipantTamper.ArbitraryReason => original with { RejectionReason = "arbitrary_text" },
                ParticipantTamper.AcceptedStructureText => original with { AcceptedStructureId = "arbitrary text" },
                _ => throw new ArgumentOutOfRangeException(nameof(tamper))
            };
            WriteOnlyParticipantRecord(root, mutated);
            byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("deep")]
    [InlineData("oversized")]
    [InlineData("missing_lf")]
    public void ParticipantReader_RejectsStrictShapeBoundAndTerminalLineFeedCorruptionWithoutRewrite(string mutation)
    {
        string root = WriteOneParticipantEvaluation(stale: false);
        try
        {
            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            string line = File.ReadAllLines(ledgerPath).Single();
            byte[] corrupted = mutation switch
            {
                "unknown" => Encoding.UTF8.GetBytes(line[..^1] + ",\"unknown\":0}\n"),
                "duplicate" => Encoding.UTF8.GetBytes(line[..^1] + ",\"participant_id\":\"participant-01\"}\n"),
                "deep" => Encoding.UTF8.GetBytes(line[..^1] + ",\"unknown\":[[[[[[[[[[0]]]]]]]]]]}\n"),
                "oversized" => Encoding.UTF8.GetBytes(new string(' ', SnowGlobeRunStore.MaximumLedgerRecordBytes) + line + "\n"),
                "missing_lf" => Encoding.UTF8.GetBytes(line),
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
            File.WriteAllBytes(ledgerPath, corrupted);

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Equal(corrupted, File.ReadAllBytes(ledgerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ParticipantCapacity_IsTransientAndReaderRejectsChecksumValid129EntryCorruption()
    {
        string root = NewTemporaryDirectory();
        try
        {
            SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("participant_capacity_adapter/v1");
            SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
            using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
            {
                for (int index = 0; index < SnowGlobeRunStore.MaximumParticipantEvaluations; index++)
                {
                    SnowGlobeParticipantCommand stale = ParticipantCommand(initial, "participant-01", $"key-{index:D3}", SnowGlobeActionKind.Idle) with { ExpectedTick = 1 };
                    Assert.Equal("stale_tick", store.EvaluateAndAppendParticipantCommand(stale).RejectionReason);
                }
                byte[] before = File.ReadAllBytes(Path.Combine(root, "ledger.jsonl"));
                SnowGlobeParticipantCommand overflow = ParticipantCommand(initial, "participant-01", "overflow", SnowGlobeActionKind.Idle) with { ExpectedTick = 1 };
                Assert.Equal("idempotency_store_saturated", store.EvaluateAndAppendParticipantCommand(overflow).RejectionReason);
                Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "ledger.jsonl")));
            }

            string ledgerPath = Path.Combine(root, "ledger.jsonl");
            SnowGlobeParticipantEvaluationRecord template = JsonSerializer.Deserialize<SnowGlobeParticipantEvaluationRecord>(File.ReadLines(ledgerPath).First(), JsonOptions)!;
            SnowGlobeParticipantEvaluationRecord extra = template with
            {
                Sequence = SnowGlobeRunStore.MaximumParticipantEvaluations,
                IdempotencyKey = "corrupt-129",
                Checksum = string.Empty
            };
            extra = extra with { Checksum = ParticipantChecksum(extra) };
            File.AppendAllText(ledgerPath, JsonSerializer.Serialize(extra, JsonOptions) + "\n");

            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Reader_RejectsWrongParticipantCommandIdentityWithoutRewrite()
    {
        string root = WriteOneParticipantEvaluation(stale: false);
        try
        {
            string headerPath = Path.Combine(root, "run.json");
            byte[] mutated = Encoding.UTF8.GetBytes(File.ReadAllText(headerPath).Replace(SnowGlobeRunStore.ParticipantCommandIdentity, "snow_globe_participant_command/v999", StringComparison.Ordinal));
            File.WriteAllBytes(headerPath, mutated);
            Assert.Throws<InvalidDataException>(() => SnowGlobeRunStore.Read(root));
            Assert.Equal(mutated, File.ReadAllBytes(headerPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static SnowGlobeParticipantCommand ParticipantCommand(
        SnowGlobeWorld world,
        string participantId,
        string idempotencyKey,
        SnowGlobeActionKind action,
        int quantity = 0,
        string agentId = "agent-00") =>
        new(participantId, idempotencyKey, world.Tick, world.StateDigest(), world.EventDigest(), agentId, action, quantity);

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static SnowGlobeLedgerKind StandardShapeKind(StandardShapeTamper tamper) => tamper switch
    {
        StandardShapeTamper.ResponseAccepted
            or StandardShapeTamper.ResponseRejectionReason
            or StandardShapeTamper.ResponseStructureId
            or StandardShapeTamper.ResponseStateDigest
            or StandardShapeTamper.ResponseEventDigest => SnowGlobeLedgerKind.Response,
        StandardShapeTamper.ProposalAccepted
            or StandardShapeTamper.ProposalRejectionReason
            or StandardShapeTamper.ProposalStructureId
            or StandardShapeTamper.ProposalStateDigest
            or StandardShapeTamper.ProposalEventDigest => SnowGlobeLedgerKind.Proposal,
        StandardShapeTamper.CommitRejectionReason
            or StandardShapeTamper.CommitStructureId
            or StandardShapeTamper.CommitStateDigest
            or StandardShapeTamper.CommitEventDigest => SnowGlobeLedgerKind.Commit,
        StandardShapeTamper.EventAccepted
            or StandardShapeTamper.EventRejectionReason
            or StandardShapeTamper.EventStateDigest
            or StandardShapeTamper.EventEventDigest => SnowGlobeLedgerKind.Event,
        StandardShapeTamper.CheckpointAgentId
            or StandardShapeTamper.CheckpointAction
            or StandardShapeTamper.CheckpointQuantity
            or StandardShapeTamper.CheckpointAccepted
            or StandardShapeTamper.CheckpointRejectionReason
            or StandardShapeTamper.CheckpointStructureId => SnowGlobeLedgerKind.Checkpoint,
        _ => throw new ArgumentOutOfRangeException(nameof(tamper))
    };

    private static SnowGlobeLedgerRecord MutateStandardShape(SnowGlobeLedgerRecord record, StandardShapeTamper tamper) => tamper switch
    {
        StandardShapeTamper.ResponseAccepted => record with { Accepted = true },
        StandardShapeTamper.ResponseRejectionReason => record with { RejectionReason = "arbitrary_text" },
        StandardShapeTamper.ResponseStructureId => record with { StructureId = "arbitrary_text" },
        StandardShapeTamper.ResponseStateDigest => record with { StateDigest = "arbitrary_text" },
        StandardShapeTamper.ResponseEventDigest => record with { EventDigest = "arbitrary_text" },
        StandardShapeTamper.ProposalAccepted => record with { Accepted = true },
        StandardShapeTamper.ProposalRejectionReason => record with { RejectionReason = "arbitrary_text" },
        StandardShapeTamper.ProposalStructureId => record with { StructureId = "arbitrary_text" },
        StandardShapeTamper.ProposalStateDigest => record with { StateDigest = "arbitrary_text" },
        StandardShapeTamper.ProposalEventDigest => record with { EventDigest = "arbitrary_text" },
        StandardShapeTamper.CommitRejectionReason => record with { RejectionReason = "arbitrary_text" },
        StandardShapeTamper.CommitStructureId => record with { StructureId = "arbitrary_text" },
        StandardShapeTamper.CommitStateDigest => record with { StateDigest = "arbitrary_text" },
        StandardShapeTamper.CommitEventDigest => record with { EventDigest = "arbitrary_text" },
        StandardShapeTamper.EventAccepted => record with { Accepted = false },
        StandardShapeTamper.EventRejectionReason => record with { RejectionReason = "arbitrary_text" },
        StandardShapeTamper.EventStateDigest => record with { StateDigest = "arbitrary_text" },
        StandardShapeTamper.EventEventDigest => record with { EventDigest = "arbitrary_text" },
        StandardShapeTamper.CheckpointAgentId => record with { AgentId = "arbitrary_text" },
        StandardShapeTamper.CheckpointAction => record with { Action = "arbitrary_text" },
        StandardShapeTamper.CheckpointQuantity => record with { Quantity = 1 },
        StandardShapeTamper.CheckpointAccepted => record with { Accepted = true },
        StandardShapeTamper.CheckpointRejectionReason => record with { RejectionReason = "arbitrary_text" },
        StandardShapeTamper.CheckpointStructureId => record with { StructureId = "arbitrary_text" },
        _ => throw new ArgumentOutOfRangeException(nameof(tamper))
    };

    private static string WriteOneParticipantEvaluation(bool stale)
    {
        string root = NewTemporaryDirectory();
        SnowGlobeRunIdentity identity = SnowGlobePersistedRun.Identity("participant_fixture_adapter/v1");
        SnowGlobeWorld initial = SnowGlobeWorld.Create(identity.Seed, identity.AgentCount);
        using (SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(root, identity))
        {
            SnowGlobeParticipantCommand command = ParticipantCommand(initial, "participant-01", "command-001", SnowGlobeActionKind.Idle);
            if (stale) command = command with { ExpectedTick = command.ExpectedTick + 1 };
            _ = store.EvaluateAndAppendParticipantCommand(command);
        }
        return root;
    }

    private static SnowGlobeParticipantEvaluationRecord ReadOnlyParticipantRecord(string root) =>
        JsonSerializer.Deserialize<SnowGlobeParticipantEvaluationRecord>(File.ReadLines(Path.Combine(root, "ledger.jsonl")).Single(), JsonOptions)!;

    private static void WriteOnlyParticipantRecord(string root, SnowGlobeParticipantEvaluationRecord record)
    {
        SnowGlobeParticipantEvaluationRecord unsigned = record with { Checksum = string.Empty };
        SnowGlobeParticipantEvaluationRecord signed = unsigned with { Checksum = ParticipantChecksum(unsigned) };
        File.WriteAllText(Path.Combine(root, "ledger.jsonl"), JsonSerializer.Serialize(signed, JsonOptions) + "\n");
    }

    private static void WriteFrozenV2OneTick(string root)
    {
        FrozenV2Identity header = new(
            SnowGlobeRunStore.LegacySchemaVersion,
            SnowGlobePersistedRun.RulesIdentity,
            SnowGlobePersistedRun.PromptIdentity,
            "frozen_v2_adapter/v1",
            SnowGlobeScenario.FixedSeed,
            SnowGlobeScenario.FixedAgentCount);
        byte[] headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
        string headerChecksum = Convert.ToHexString(SHA256.HashData(headerBytes)).ToLowerInvariant();
        File.WriteAllBytes(Path.Combine(root, "run.json"), headerBytes);

        SnowGlobeWorld world = SnowGlobeWorld.Create(header.Seed, header.AgentCount);
        List<SnowGlobeLedgerRecord> records = new();
        foreach (string agentId in world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal))
        {
            Add(SnowGlobeLedgerKind.Response, world.Tick, agentId, SnowGlobeActionKind.Idle.ToString(), 0, null, null, null, null, null);
            Add(SnowGlobeLedgerKind.Proposal, world.Tick, agentId, SnowGlobeActionKind.Idle.ToString(), 0, null, null, null, null, null);
            SnowGlobeCommitResult commit = world.ValidateAndCommit(new SnowGlobeActionProposal(agentId, SnowGlobeActionKind.Idle));
            Add(SnowGlobeLedgerKind.Commit, world.Tick, agentId, SnowGlobeActionKind.Idle.ToString(), 0, commit.Accepted, commit.RejectionReason, null, null, null);
            SnowGlobeEvent accepted = world.Events[^1];
            Add(SnowGlobeLedgerKind.Event, world.Tick, agentId, SnowGlobeActionKind.Idle.ToString(), 0, true, null, accepted.StructureId, null, null);
        }
        world.AdvanceTick();
        Add(SnowGlobeLedgerKind.Checkpoint, world.Tick, string.Empty, string.Empty, 0, null, null, null, world.StateDigest(), world.EventDigest());
        File.WriteAllText(Path.Combine(root, "ledger.jsonl"), string.Join("\n", records.Select(record => JsonSerializer.Serialize(record, JsonOptions))) + "\n");

        void Add(SnowGlobeLedgerKind kind, int tick, string agentId, string action, int quantity, bool? accepted, string? reason, string? structureId, string? stateDigest, string? eventDigest)
        {
            SnowGlobeLedgerRecord unsigned = new(records.Count, kind, tick, agentId, action, quantity, accepted, reason, structureId, stateDigest, eventDigest, string.Empty, headerChecksum);
            records.Add(unsigned with { Checksum = Checksum(unsigned) });
        }
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
    private static string ParticipantChecksum(SnowGlobeParticipantEvaluationRecord record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{record.Sequence}|{record.Kind}|{record.Tick}|{record.ParticipantId}|{record.IdempotencyKey}|{record.ExpectedTick}|{record.ExpectedStateDigest}|{record.ExpectedEventDigest}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.AcceptedEventSequence}|{record.AcceptedStructureId}|{record.ResultingStateDigest}|{record.ResultingEventDigest}|{record.HeaderChecksum}"))).ToLowerInvariant();
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

    private sealed class BlockingInferenceAdapter(
        int blockCall,
        TaskCompletionSource<bool> entered,
        TaskCompletionSource<bool> release) : ISnowGlobeInferenceAdapter
    {
        private int _calls;

        public async ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _calls) - 1;
            if (call == blockCall)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }
            return new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle);
        }
    }

    private sealed class RejectingInferenceAdapter : ISnowGlobeInferenceAdapter
    {
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.BuildShelter));
        }
    }

    private sealed class FailingInferenceAdapter(
        ScheduledFailure failure,
        int failingOrdinal,
        CancellationTokenSource cancellation) : ISnowGlobeInferenceAdapter
    {
        private int _calls;

        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
        {
            int call = _calls++;
            if (call != failingOrdinal) return ValueTask.FromResult(new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
            return failure switch
            {
                ScheduledFailure.Adapter => throw new InvalidOperationException("deterministic_adapter_failure"),
                ScheduledFailure.Cancellation => Cancel(cancellation, cancellationToken),
                ScheduledFailure.Validation => ValueTask.FromResult(new SnowGlobeActionProposal("arbitrary text", SnowGlobeActionKind.Idle)),
                _ => throw new ArgumentOutOfRangeException(nameof(failure))
            };
        }

        private static ValueTask<SnowGlobeActionProposal> Cancel(CancellationTokenSource cancellation, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation token did not cancel.");
        }
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-snowglobe-runstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private sealed record FrozenV2Identity(string SchemaVersion, string RulesIdentity, string PromptIdentity, string AdapterIdentity, int Seed, int AgentCount);
    public enum Corruption { Truncate, Checksum, OutOfOrderSequence, DuplicateSequence, UnknownAction, SchemaMismatch }
    public enum ParticipantTamper { Disposition, EventSequence, ResultingStateDigest, ExpectedAnchor, CanonicalAction, ArbitraryReason, AcceptedStructureText }
    public enum OperationLeaseWindow { BeforeFirstTick, BetweenTicks, PostFinalBeforeRelease }
    public enum ScheduledFailure { Adapter, Cancellation, Validation }
    public enum AppendFaultPath { ScheduledBatch, ParticipantEvaluation }
    public enum StandardShapeTamper
    {
        ResponseAccepted,
        ResponseRejectionReason,
        ResponseStructureId,
        ResponseStateDigest,
        ResponseEventDigest,
        ProposalAccepted,
        ProposalRejectionReason,
        ProposalStructureId,
        ProposalStateDigest,
        ProposalEventDigest,
        CommitRejectionReason,
        CommitStructureId,
        CommitStateDigest,
        CommitEventDigest,
        EventAccepted,
        EventRejectionReason,
        EventStateDigest,
        EventEventDigest,
        CheckpointAgentId,
        CheckpointAction,
        CheckpointQuantity,
        CheckpointAccepted,
        CheckpointRejectionReason,
        CheckpointStructureId
    }
    public enum ScheduleMutation { Incomplete, Duplicate, Reordered }
    public enum IdentityMutation { V1Schema, Rules, Prompt }
    public enum WorldMismatch { Seed, AgentCount, Tick, Digest }
}
