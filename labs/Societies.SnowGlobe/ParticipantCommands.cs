namespace Societies.SnowGlobe;

public sealed record SnowGlobeParticipantCommand(
    string? ParticipantId,
    string? IdempotencyKey,
    int? ExpectedTick,
    string? ExpectedStateDigest,
    string? ExpectedEventDigest,
    string? TargetAgentId,
    SnowGlobeActionKind? Action,
    int? Quantity);

/// <summary>A detached receipt for one command evaluation; it never exposes the live world.</summary>
public sealed record SnowGlobeParticipantCommandReceipt(
    bool Accepted,
    string? RejectionReason,
    bool ShellOwnershipLost,
    string? ParticipantId,
    string? IdempotencyKey,
    int? ResultingTick,
    int? ResultingEventSequence,
    string? ResultingStateDigest,
    string? ResultingEventDigest);

public sealed partial class SnowGlobeObserverShell
{
    public const int MaximumParticipantIdLength = 64;
    public const int MaximumParticipantIdempotencyKeyLength = 64;
    public const int MaximumParticipantCommandReceipts = 128;

    private const int DigestLength = 64;
    private readonly Dictionary<string, StoredParticipantReceipt> _participantReceipts = new(StringComparer.Ordinal);

    public Task<SnowGlobeParticipantCommandReceipt> SubmitParticipantCommandAsync(
        SnowGlobeParticipantCommand? command,
        CancellationToken cancellationToken = default)
    {
        // Cancellation and busy admission are deliberately transient, even for a prior command id.
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(ParticipantReject(command, "operation_cancelled"));
        if (!_operationGate.Wait(0)) return Task.FromResult(ParticipantReject(command, "operation_in_progress"));

        try
        {
            string? malformed = ValidateParticipantCommandShape(command);
            if (malformed is not null) return Task.FromResult(ParticipantReject(command, malformed));

            SnowGlobeParticipantCommand validCommand = command!;
            string fingerprint = ParticipantFingerprint(validCommand);
            if (_participantReceipts.TryGetValue(validCommand.IdempotencyKey!, out StoredParticipantReceipt? stored))
            {
                return Task.FromResult(string.Equals(stored.Fingerprint, fingerprint, StringComparison.Ordinal)
                    ? stored.Receipt
                    : ParticipantReject(validCommand, "command_id_conflict"));
            }

            // New durable evaluations require a coherent paused shell and available fixed capacity.
            if (!IsWorldOwned())
            {
                OwnershipLost();
                return Task.FromResult(ParticipantReject(validCommand, "world_ownership_lost"));
            }

            if (!_isPaused) return Task.FromResult(ParticipantReject(validCommand, "must_be_paused"));
            if (_participantReceipts.Count >= MaximumParticipantCommandReceipts) return Task.FromResult(ParticipantReject(validCommand, "idempotency_store_saturated"));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(ParticipantReject(validCommand, "operation_cancelled"));

            // From here the key is admitted for durable semantic evaluation.
            if (validCommand.ExpectedTick != _knownTick) return Task.FromResult(StoreTerminalReceipt(validCommand, fingerprint, ParticipantReject(validCommand, "stale_tick")));
            if (!string.Equals(validCommand.ExpectedStateDigest, _stateDigest, StringComparison.Ordinal)) return Task.FromResult(StoreTerminalReceipt(validCommand, fingerprint, ParticipantReject(validCommand, "stale_state_digest")));
            if (!string.Equals(validCommand.ExpectedEventDigest, _eventDigest, StringComparison.Ordinal)) return Task.FromResult(StoreTerminalReceipt(validCommand, fingerprint, ParticipantReject(validCommand, "stale_event_digest")));

            return Task.FromResult(CommitParticipantCommand(validCommand, fingerprint, cancellationToken));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private SnowGlobeParticipantCommandReceipt CommitParticipantCommand(SnowGlobeParticipantCommand command, string fingerprint, CancellationToken cancellationToken)
    {
        try
        {
            SnowGlobeActionProposal proposal = new(command.TargetAgentId!, command.Action!.Value, command.Quantity!.Value);
            SnowGlobeWorld candidate = ReconstructCandidate();
            int eventCountBefore = candidate.Events.Count;
            SnowGlobeCommitResult candidateResult = candidate.ValidateAndCommit(proposal);
            if (!candidateResult.Accepted)
            {
                return StoreTerminalReceipt(command, fingerprint, ParticipantReject(command, candidateResult.RejectionReason ?? "validator_rejected"));
            }

            SnowGlobeEvent[] candidateDelta = candidate.Events.Skip(eventCountBefore).ToArray();
            if (candidateDelta.Length != 1) return ParticipantReject(command, "candidate_delta_invalid");

            AfterParticipantCandidateApplyForTesting?.Invoke();
            if (!IsWorldOwned())
            {
                OwnershipLost();
                return ParticipantReject(command, "world_ownership_lost");
            }

            if (cancellationToken.IsCancellationRequested) return ParticipantReject(command, "operation_cancelled");

            SnowGlobeWorld verification = ReconstructCandidate();
            SnowGlobeCommitResult verificationResult = verification.ValidateAndCommit(proposal);
            if (!verificationResult.Accepted || !EventsMatch(candidateDelta[0], verification.Events[^1])) return ParticipantReject(command, "candidate_replay_mismatch");

            string candidateStateDigest = candidate.StateDigest();
            string candidateEventDigest = candidate.EventDigest();
            if (!string.Equals(candidateStateDigest, verification.StateDigest(), StringComparison.Ordinal)
                || !string.Equals(candidateEventDigest, verification.EventDigest(), StringComparison.Ordinal))
            {
                return ParticipantReject(command, "candidate_replay_mismatch");
            }

            BeforeParticipantConditionalCommitForTesting?.Invoke();
            SnowGlobeExpectedCommitResult liveAttempt = _world.ValidateAndCommitIfIdentityMatches(
                _knownTick,
                _stateDigest,
                _eventDigest,
                proposal);
            if (!liveAttempt.IdentityMatched)
            {
                OwnershipLost();
                return ParticipantReject(command, "world_ownership_lost");
            }

            if (!liveAttempt.CommitResult.Accepted
                || liveAttempt.CommittedEvent is null
                || !EventsMatch(candidateDelta[0], liveAttempt.CommittedEvent))
            {
                OwnershipLost();
                return ParticipantReject(command, "live_replay_mismatch");
            }

            SnowGlobeParticipantCommandReceipt receipt = new(
                true,
                null,
                false,
                command.ParticipantId,
                command.IdempotencyKey,
                candidate.Tick,
                candidateDelta[0].Sequence,
                candidateStateDigest,
                candidateEventDigest);

            AfterLiveApplyForTesting?.Invoke();
            if (!_world.MatchesRevision(liveAttempt.ResultingRevision))
            {
                OwnershipLost();
                return StoreTerminalReceipt(command, fingerprint, receipt with { ShellOwnershipLost = true });
            }

            CacheCandidateIdentity(candidate.Tick, candidate.Events.Count, candidateStateDigest, candidateEventDigest, liveAttempt.ResultingRevision);
            return StoreTerminalReceipt(command, fingerprint, receipt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ParticipantReject(command, "operation_cancelled");
        }
        catch (Exception)
        {
            return IsWorldOwned() ? ParticipantReject(command, "participant_command_failure") : ParticipantReject(command, "world_ownership_lost");
        }
    }

    private SnowGlobeParticipantCommandReceipt StoreTerminalReceipt(SnowGlobeParticipantCommand command, string fingerprint, SnowGlobeParticipantCommandReceipt receipt)
    {
        _participantReceipts.Add(command.IdempotencyKey!, new StoredParticipantReceipt(fingerprint, receipt));
        return receipt;
    }

    private SnowGlobeParticipantCommandReceipt ParticipantReject(SnowGlobeParticipantCommand? command, string reason)
    {
        (string? participantId, string? idempotencyKey) = SafeReceiptIdentity(command);
        if (!IsWorldOwned()) return new(false, reason, false, participantId, idempotencyKey, null, null, null, null);
        return new(false, reason, false, participantId, idempotencyKey, _knownTick, _knownEventCount - 1, _stateDigest, _eventDigest);
    }

    private static (string? ParticipantId, string? IdempotencyKey) SafeReceiptIdentity(SnowGlobeParticipantCommand? command) =>
        (IsCanonicalOpaqueId(command?.ParticipantId, MaximumParticipantIdLength) ? command!.ParticipantId : null,
         IsCanonicalOpaqueId(command?.IdempotencyKey, MaximumParticipantIdempotencyKeyLength) ? command!.IdempotencyKey : null);

    private static string? ValidateParticipantCommandShape(SnowGlobeParticipantCommand? command)
    {
        if (command is null) return "participant_command_malformed";
        if (!IsCanonicalOpaqueId(command.ParticipantId, MaximumParticipantIdLength)) return "participant_id_invalid";
        if (!IsCanonicalOpaqueId(command.IdempotencyKey, MaximumParticipantIdempotencyKeyLength)) return "idempotency_key_invalid";
        if (command.ExpectedTick is null || command.ExpectedTick < 0) return "expected_tick_invalid";
        if (!IsCanonicalDigest(command.ExpectedStateDigest)) return "expected_state_digest_invalid";
        if (!IsCanonicalDigest(command.ExpectedEventDigest)) return "expected_event_digest_invalid";
        if (!IsCanonicalOpaqueId(command.TargetAgentId, MaximumParticipantIdLength)) return "target_agent_id_invalid";
        if (command.Action is not SnowGlobeActionKind action || !Enum.IsDefined(action)) return "action_invalid";
        return command.Quantity is null ? "quantity_invalid" : null;
    }

    private static bool IsCanonicalOpaqueId(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current is '-' or '_')) return false;
        }
        return value[0] is >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsCanonicalDigest(string? value)
    {
        if (value is null || value.Length != DigestLength) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= '0' && current <= '9') || (current >= 'a' && current <= 'f'))) return false;
        }
        return true;
    }

    private static string ParticipantFingerprint(SnowGlobeParticipantCommand command) => string.Join("|", command.ParticipantId, command.IdempotencyKey, command.ExpectedTick, command.ExpectedStateDigest, command.ExpectedEventDigest, command.TargetAgentId, ((int)command.Action!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture), command.Quantity!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static bool EventsMatch(SnowGlobeEvent expected, SnowGlobeEvent actual) => expected.Tick == actual.Tick && expected.Sequence == actual.Sequence && expected.Action == actual.Action && expected.Quantity == actual.Quantity && string.Equals(expected.AgentId, actual.AgentId, StringComparison.Ordinal) && string.Equals(expected.StructureId, actual.StructureId, StringComparison.Ordinal);

    private sealed record StoredParticipantReceipt(string Fingerprint, SnowGlobeParticipantCommandReceipt Receipt);
}
