namespace Societies.SnowGlobe;

public sealed record SnowGlobePersistedRunResult(SnowGlobeWorld World, SnowGlobeRunResult Result);

/// <summary>
/// The persistence experiment's deterministic execution surface. It owns validation and commits;
/// adapters can only return value proposals and recorded responses remain non-authoritative.
/// </summary>
public static class SnowGlobePersistedRun
{
    public const string RulesIdentity = "snow_globe_domain_rules/v1";
    public const string PromptIdentity = "normalized_values_only/no_participant_text/v1";

    public static SnowGlobeRunIdentity Identity(string adapterIdentity, int seed = SnowGlobeScenario.FixedSeed, int agentCount = SnowGlobeScenario.FixedAgentCount) =>
        new(SnowGlobeRunStore.SchemaVersion, RulesIdentity, PromptIdentity, adapterIdentity, seed, agentCount);

    public static async Task<SnowGlobePersistedRunResult> RunAsync(
        SnowGlobeWorld world,
        ISnowGlobeInferenceAdapter inference,
        SnowGlobeRunStore store,
        int targetTick,
        int checkpointInterval = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inference);
        ArgumentNullException.ThrowIfNull(store);
        // v1 writes a checkpoint at each completed tick so an interruption is always resumable.
        if (targetTick < world.Tick || checkpointInterval != 1) throw new ArgumentOutOfRangeException(nameof(checkpointInterval));
        using IDisposable operationLease = await store.AcquireOperationLeaseAsync(cancellationToken);
        SnowGlobeRunMetrics metrics = new();
        while (world.Tick < targetTick)
        {
            string[] scheduledAgents = world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal).ToArray();
            store.BindAndReserveWholeTick(world, scheduledAgents);
            foreach (string agentId in scheduledAgents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SnowGlobeObservation observation = world.Observe(agentId);
                SnowGlobeActionProposal response = await inference.ProposeAsync(observation, cancellationToken);
                store.AppendResponse(observation, response);
                store.AppendProposal(world.Tick, response);
                SnowGlobeCommitResult commit = world.ValidateAndCommit(response);
                store.AppendCommit(world.Tick, response, commit);
                metrics.SequentialQueueTurns++;
                metrics.InferenceCalls++;
                metrics.ProposalCount++;
                if (commit.Accepted)
                {
                    store.AppendEvent(world.Events[^1]);
                    metrics.AcceptedActions++;
                }
                else metrics.RejectedActions++;
            }
            world.AdvanceTick();
            metrics.Ticks++;
            store.AppendCheckpoint(world);
            store.CompleteWholeTick(world);
        }
        return new SnowGlobePersistedRunResult(world, new SnowGlobeRunResult(world.StateDigest(), world.EventDigest(), metrics));
    }

    public static SnowGlobeWorld ResumeAtLatestCheckpoint(SnowGlobeRunLedger ledger, SnowGlobeRunIdentity? expectedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        SnowGlobeRunStore.ValidateSupportedIdentity(ledger.Identity);
        if (expectedIdentity is not null && ledger.Identity != expectedIdentity) throw new InvalidDataException("Recorded run identity does not match the expected provenance.");
        SnowGlobeWorld world = SnowGlobeWorld.Create(ledger.Identity.Seed, ledger.Identity.AgentCount);
        int index = 0;
        while (index < ledger.Records.Count)
        {
            foreach (string expectedAgentId in world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal))
            {
                SnowGlobeLedgerRecord response = Require(ledger.Records, ref index, SnowGlobeLedgerKind.Response, world.Tick);
                if (!string.Equals(response.AgentId, expectedAgentId, StringComparison.Ordinal)) throw new InvalidDataException("Recorded response violates the identity agent ordinal schedule.");
                SnowGlobeLedgerRecord proposal = Require(ledger.Records, ref index, SnowGlobeLedgerKind.Proposal, world.Tick);
                if (!SameAction(response, proposal)) throw new InvalidDataException("Response and proposal diverge.");
                SnowGlobeLedgerRecord commit = Require(ledger.Records, ref index, SnowGlobeLedgerKind.Commit, world.Tick);
                if (!SameAction(proposal, commit) || commit.Accepted is null) throw new InvalidDataException("Proposal and commit diverge.");
                if (commit.Accepted.Value)
                {
                    SnowGlobeLedgerRecord entry = Require(ledger.Records, ref index, SnowGlobeLedgerKind.Event, world.Tick);
                    if (!SameAction(commit, entry)) throw new InvalidDataException("Commit and event diverge.");
                    SnowGlobeActionKind action = ParseAction(entry);
                    world.Replay(new SnowGlobeEvent(entry.Tick, world.Events.Count, entry.AgentId, action, entry.Quantity, entry.StructureId));
                }
                else
                {
                    SnowGlobeCommitResult result = world.ValidateAndCommit(ToProposal(commit));
                    if (result.Accepted || !string.Equals(result.RejectionReason, commit.RejectionReason, StringComparison.Ordinal)) throw new InvalidDataException("Rejected commit does not match deterministic validation.");
                }
            }
            SnowGlobeLedgerRecord checkpoint = Require(ledger.Records, ref index, SnowGlobeLedgerKind.Checkpoint, world.Tick + 1);
            world.AdvanceTick();
            if (!string.Equals(checkpoint.StateDigest, world.StateDigest(), StringComparison.Ordinal) || !string.Equals(checkpoint.EventDigest, world.EventDigest(), StringComparison.Ordinal)) throw new InvalidDataException("Checkpoint digest mismatch.");
        }
        return world;
    }

    private static SnowGlobeLedgerRecord Require(IReadOnlyList<SnowGlobeLedgerRecord> records, ref int index, SnowGlobeLedgerKind kind, int tick)
    {
        if (index >= records.Count) throw new InvalidDataException("Ledger is truncated.");
        SnowGlobeLedgerRecord record = records[index++];
        if (record.Kind != kind || record.Tick != tick) throw new InvalidDataException("Ledger kind or tick is out of order.");
        return record;
    }
    private static bool SameAction(SnowGlobeLedgerRecord left, SnowGlobeLedgerRecord right) =>
        left.Tick == right.Tick && left.AgentId == right.AgentId && left.Action == right.Action && left.Quantity == right.Quantity;
    private static SnowGlobeActionProposal ToProposal(SnowGlobeLedgerRecord record) => new(record.AgentId, ParseAction(record), record.Quantity);
    private static SnowGlobeActionKind ParseAction(SnowGlobeLedgerRecord record) => SnowGlobeRunStore.TryParseCanonicalAction(record.Action, out SnowGlobeActionKind action) ? action : throw new InvalidDataException("Ledger contains an unknown action.");
}

/// <summary>Eight-agent recorded resilience fixture: ordinal conflicting claims followed by deterministic idle fallback.</summary>
public sealed class SnowGlobeResilienceFallbackAdapter : ISnowGlobeInferenceAdapter
{
    public const string Identity = "snow_globe_resilience_conflicting_claims_fallback/v1";
    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(observation.HomeSlot == 0
            ? new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.GatherWood, 64)
            : new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
    }
}
