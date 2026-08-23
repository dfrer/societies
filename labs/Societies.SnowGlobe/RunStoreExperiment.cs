using System.Collections.ObjectModel;

namespace Societies.SnowGlobe;

public sealed record SnowGlobePersistedRunResult(SnowGlobeWorld World, SnowGlobeRunResult Result);
public sealed record SnowGlobeParticipantCommandKey(string ParticipantId, string IdempotencyKey);
public sealed record SnowGlobeRunReconstruction(
    SnowGlobeWorld World,
    IReadOnlyDictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt> ParticipantReceipts);

/// <summary>
/// The persistence experiment's deterministic execution surface. It owns validation and commits;
/// adapters can only return value proposals and recorded responses remain non-authoritative.
/// Managed adapter, cancellation, and validation interruptions before the batch append clear the
/// pending frame and remain resumable. Low-level partial I/O and process crashes are not promised
/// atomic or resumable; an observed append/flush failure poisons the live writer.
/// </summary>
public static class SnowGlobePersistedRun
{
    public const string RulesIdentity = "snow_globe_domain_rules/v1";
    public const string PromptIdentity = "normalized_values_only/no_participant_text/v1";

    public static SnowGlobeRunIdentity Identity(string adapterIdentity, int seed = SnowGlobeScenario.FixedSeed, int agentCount = SnowGlobeScenario.FixedAgentCount) =>
        new(SnowGlobeRunStore.SchemaVersion, RulesIdentity, PromptIdentity, adapterIdentity, seed, agentCount, SnowGlobeRunStore.ParticipantCommandIdentity);

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
        // Managed failures before a tick's batch append retain the last completed checkpoint;
        // partial I/O and process crashes are outside that resumability guarantee.
        if (targetTick < world.Tick || checkpointInterval != 1) throw new ArgumentOutOfRangeException(nameof(checkpointInterval));
        using IDisposable operationLease = await store.AcquireOperationLeaseAsync(cancellationToken);
        SnowGlobeRunMetrics metrics = new();
        while (world.Tick < targetTick)
        {
            string[] scheduledAgents = world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal).ToArray();
            store.BindAndReserveWholeTick(world, scheduledAgents);
            bool completed = false;
            try
            {
                foreach (string agentId in scheduledAgents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SnowGlobeObservation observation = world.Observe(agentId);
                    SnowGlobeActionProposal response = await inference.ProposeAsync(observation, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                world.AdvanceTick();
                metrics.Ticks++;
                store.AppendCheckpoint(world);
                store.CompleteWholeTick(world);
                completed = true;
            }
            finally
            {
                if (!completed) store.AbortWholeTick();
            }
        }
        await store.WaitBeforeOperationLeaseReleaseForTestingAsync().ConfigureAwait(false);
        return new SnowGlobePersistedRunResult(world, new SnowGlobeRunResult(world.StateDigest(), world.EventDigest(), metrics));
    }

    public static SnowGlobeWorld ResumeAtLatestCheckpoint(SnowGlobeRunLedger ledger, SnowGlobeRunIdentity? expectedIdentity = null) =>
        Reconstruct(ledger, expectedIdentity).World;

    public static SnowGlobeRunReconstruction Reconstruct(SnowGlobeRunLedger ledger, SnowGlobeRunIdentity? expectedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        SnowGlobeRunStore.ValidateSupportedIdentity(ledger.Identity);
        if (expectedIdentity is not null && ledger.Identity != expectedIdentity) throw new InvalidDataException("Recorded run identity does not match the expected provenance.");
        SnowGlobeWorld world = SnowGlobeWorld.Create(ledger.Identity.Seed, ledger.Identity.AgentCount);
        IReadOnlyList<OrderedLedgerEntry> entries = OrderedEntries(ledger);
        Dictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt> participantReceipts = new();
        int index = 0;
        while (index < entries.Count)
        {
            while (index < entries.Count && entries[index].ParticipantEvaluation is SnowGlobeParticipantEvaluationRecord evaluation)
            {
                if (ledger.Identity.SchemaVersion != SnowGlobeRunStore.SchemaVersion) throw new InvalidDataException("Legacy v2 reconstruction cannot contain participant evaluations.");
                if (participantReceipts.Count >= SnowGlobeRunStore.MaximumParticipantEvaluations) throw new InvalidDataException("Participant evaluation index exceeds its bounded capacity.");
                SnowGlobeParticipantCommandKey key = new(evaluation.ParticipantId, evaluation.IdempotencyKey);
                if (participantReceipts.ContainsKey(key)) throw new InvalidDataException("Participant evaluation idempotency key is duplicated.");
                participantReceipts.Add(key, ReplayParticipantEvaluation(world, evaluation));
                index++;
            }

            if (index == entries.Count) break;
            foreach (string expectedAgentId in world.Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal))
            {
                SnowGlobeLedgerRecord response = Require(entries, ref index, SnowGlobeLedgerKind.Response, world.Tick);
                if (!string.Equals(response.AgentId, expectedAgentId, StringComparison.Ordinal)) throw new InvalidDataException("Recorded response violates the identity agent ordinal schedule.");
                SnowGlobeLedgerRecord proposal = Require(entries, ref index, SnowGlobeLedgerKind.Proposal, world.Tick);
                if (!SameAction(response, proposal)) throw new InvalidDataException("Response and proposal diverge.");
                SnowGlobeLedgerRecord commit = Require(entries, ref index, SnowGlobeLedgerKind.Commit, world.Tick);
                if (!SameAction(proposal, commit) || commit.Accepted is null) throw new InvalidDataException("Proposal and commit diverge.");
                if (commit.Accepted.Value)
                {
                    SnowGlobeLedgerRecord entry = Require(entries, ref index, SnowGlobeLedgerKind.Event, world.Tick);
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
            SnowGlobeLedgerRecord checkpoint = Require(entries, ref index, SnowGlobeLedgerKind.Checkpoint, world.Tick + 1);
            world.AdvanceTick();
            if (!string.Equals(checkpoint.StateDigest, world.StateDigest(), StringComparison.Ordinal) || !string.Equals(checkpoint.EventDigest, world.EventDigest(), StringComparison.Ordinal)) throw new InvalidDataException("Checkpoint digest mismatch.");
        }
        return new SnowGlobeRunReconstruction(
            world,
            new ReadOnlyDictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt>(
                new Dictionary<SnowGlobeParticipantCommandKey, SnowGlobeParticipantCommandReceipt>(participantReceipts)));
    }

    private static SnowGlobeParticipantCommandReceipt ReplayParticipantEvaluation(SnowGlobeWorld world, SnowGlobeParticipantEvaluationRecord evaluation)
    {
        SnowGlobeRunStore.ValidateParticipantRecord(evaluation);
        if (evaluation.Tick != world.Tick) throw new InvalidDataException("Participant evaluation is not at the current tick boundary.");

        bool accepted = false;
        string? rejectionReason;
        int? acceptedEventSequence = null;
        string? acceptedStructureId = null;
        if (evaluation.ExpectedTick != world.Tick) rejectionReason = "stale_tick";
        else if (!SnowGlobeRunStore.FixedEquals(evaluation.ExpectedStateDigest, world.StateDigest())) rejectionReason = "stale_state_digest";
        else if (!SnowGlobeRunStore.FixedEquals(evaluation.ExpectedEventDigest, world.EventDigest())) rejectionReason = "stale_event_digest";
        else
        {
            SnowGlobeActionKind action = ParseAction(evaluation.Action);
            SnowGlobeCommitResult result = world.ValidateAndCommit(new SnowGlobeActionProposal(evaluation.AgentId, action, evaluation.Quantity));
            accepted = result.Accepted;
            rejectionReason = result.RejectionReason;
            if (accepted)
            {
                SnowGlobeEvent acceptedEvent = world.Events[^1];
                acceptedEventSequence = acceptedEvent.Sequence;
                acceptedStructureId = acceptedEvent.StructureId;
            }
        }

        if (evaluation.Accepted != accepted
            || !string.Equals(evaluation.RejectionReason, rejectionReason, StringComparison.Ordinal)
            || evaluation.AcceptedEventSequence != acceptedEventSequence
            || !string.Equals(evaluation.AcceptedStructureId, acceptedStructureId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Participant evaluation disposition or accepted event does not match deterministic replay.");
        }
        if (!SnowGlobeRunStore.FixedEquals(evaluation.ResultingStateDigest, world.StateDigest())
            || !SnowGlobeRunStore.FixedEquals(evaluation.ResultingEventDigest, world.EventDigest()))
        {
            throw new InvalidDataException("Participant evaluation resulting digest does not match deterministic replay.");
        }

        return new SnowGlobeParticipantCommandReceipt(
            accepted,
            rejectionReason,
            false,
            evaluation.ParticipantId,
            evaluation.IdempotencyKey,
            world.Tick,
            accepted ? acceptedEventSequence : world.Events.Count - 1,
            evaluation.ResultingStateDigest,
            evaluation.ResultingEventDigest);
    }

    private static IReadOnlyList<OrderedLedgerEntry> OrderedEntries(SnowGlobeRunLedger ledger)
    {
        if (ledger.Identity.SchemaVersion == SnowGlobeRunStore.LegacySchemaVersion && ledger.ParticipantEvaluationRecords.Count != 0) throw new InvalidDataException("Legacy v2 ledgers cannot contain participant evaluations.");
        EnsureIncreasing(ledger.Records.Select(record => record.Sequence));
        EnsureIncreasing(ledger.ParticipantEvaluationRecords.Select(record => record.Sequence));
        List<OrderedLedgerEntry> entries = ledger.Records
            .Select(record => new OrderedLedgerEntry(record.Sequence, record, null))
            .Concat(ledger.ParticipantEvaluationRecords.Select(evaluation => new OrderedLedgerEntry(evaluation.Sequence, null, evaluation)))
            .OrderBy(entry => entry.Sequence)
            .ToList();
        for (int sequence = 0; sequence < entries.Count; sequence++)
        {
            if (entries[sequence].Sequence != sequence) throw new InvalidDataException("Ledger sequence is out of order, duplicated, or incomplete.");
        }
        return entries;
    }

    private static void EnsureIncreasing(IEnumerable<int> sequences)
    {
        int previous = -1;
        foreach (int sequence in sequences)
        {
            if (sequence <= previous) throw new InvalidDataException("Ledger collection order does not match sequence order.");
            previous = sequence;
        }
    }

    private static SnowGlobeLedgerRecord Require(IReadOnlyList<OrderedLedgerEntry> entries, ref int index, SnowGlobeLedgerKind kind, int tick)
    {
        if (index >= entries.Count || entries[index].Record is not SnowGlobeLedgerRecord record) throw new InvalidDataException("Ledger is truncated or contains an evaluation inside an agent schedule.");
        index++;
        if (record.Kind != kind || record.Tick != tick) throw new InvalidDataException("Ledger kind or tick is out of order.");
        return record;
    }
    private static bool SameAction(SnowGlobeLedgerRecord left, SnowGlobeLedgerRecord right) =>
        left.Tick == right.Tick && left.AgentId == right.AgentId && left.Action == right.Action && left.Quantity == right.Quantity;
    private static SnowGlobeActionProposal ToProposal(SnowGlobeLedgerRecord record) => new(record.AgentId, ParseAction(record), record.Quantity);
    private static SnowGlobeActionKind ParseAction(SnowGlobeLedgerRecord record) => SnowGlobeRunStore.TryParseCanonicalAction(record.Action, out SnowGlobeActionKind action) ? action : throw new InvalidDataException("Ledger contains an unknown action.");
    private static SnowGlobeActionKind ParseAction(string actionText) => SnowGlobeRunStore.TryParseCanonicalAction(actionText, out SnowGlobeActionKind action) ? action : throw new InvalidDataException("Ledger contains an unknown action.");
    private sealed record OrderedLedgerEntry(int Sequence, SnowGlobeLedgerRecord? Record, SnowGlobeParticipantEvaluationRecord? ParticipantEvaluation);
}

/// <summary>Eight-agent recorded resilience fixture: ordinal conflicting claims followed by deterministic idle fallback.</summary>
public sealed class SnowGlobeResilienceFallbackAdapter : ISnowGlobeIdentifiedInferenceAdapter
{
    public const string Identity = "snow_globe_resilience_conflicting_claims_fallback/v1";
    public string AdapterIdentity => Identity;
    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(observation.HomeSlot == 0
            ? new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.GatherWood, 64)
            : new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle));
    }
}
