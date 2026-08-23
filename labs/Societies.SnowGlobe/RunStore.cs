using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Versioned local identity for one normalized, deterministic lab run.</summary>
public sealed record SnowGlobeRunIdentity(
    string SchemaVersion,
    string RulesIdentity,
    string PromptIdentity,
    string AdapterIdentity,
    int Seed,
    int AgentCount,
    string? ParticipantCommandIdentity = null);
public enum SnowGlobeLedgerKind
{
    Response = 0,
    Proposal = 1,
    Commit = 2,
    Event = 3,
    Checkpoint = 4,
    ParticipantEvaluation = 5,
    PauseTransition = 6
}
public sealed record SnowGlobeLedgerRecord(int Sequence, SnowGlobeLedgerKind Kind, int Tick, string AgentId, string Action, int Quantity, bool? Accepted, string? RejectionReason, string? StructureId, string? StateDigest, string? EventDigest, string Checksum, string HeaderChecksum = "");
public sealed record SnowGlobeParticipantEvaluationRecord(
    int Sequence,
    SnowGlobeLedgerKind Kind,
    int Tick,
    string ParticipantId,
    string IdempotencyKey,
    int ExpectedTick,
    string ExpectedStateDigest,
    string ExpectedEventDigest,
    string AgentId,
    string Action,
    int Quantity,
    bool Accepted,
    string? RejectionReason,
    int? AcceptedEventSequence,
    string? AcceptedStructureId,
    string ResultingStateDigest,
    string ResultingEventDigest,
    string Checksum,
    string HeaderChecksum);
public sealed record SnowGlobeRunLedger(
    SnowGlobeRunIdentity Identity,
    IReadOnlyList<SnowGlobeLedgerRecord> Records,
    IReadOnlyList<SnowGlobeParticipantEvaluationRecord>? ParticipantEvaluations = null)
{
    public IReadOnlyList<SnowGlobeParticipantEvaluationRecord> ParticipantEvaluationRecords =>
        ParticipantEvaluations ?? Array.Empty<SnowGlobeParticipantEvaluationRecord>();
    public int EntryCount => Records.Count + ParticipantEvaluationRecords.Count;
}

/// <summary>
/// Internal read result that binds a detached ledger to the exact raw artifacts consumed while
/// reading it. It deliberately does not expose a filesystem abstraction to production callers.
/// </summary>
internal sealed record RunStoreReadEvidence(
    SnowGlobeRunLedger Ledger,
    string EvidenceChecksum,
    string? V4HeaderChecksum,
    RunStoreDurableRecovery? DurableRecovery);

internal enum RunStorePauseAppendResult
{
    Appended,
    AlreadyInTargetState,
    OperationInProgress,
    CapacityExhausted
}

/// <summary>Bounded, append-only evidence. Readers never modify artifacts; a durable lock file is a lease, not ownership evidence.</summary>
public sealed class SnowGlobeRunStore : IDisposable
{
    public const string SchemaVersion = "snow_globe_run_store/v5";
    public const string PreviousSchemaVersion = "snow_globe_run_store/v3";
    public const string V4SchemaVersion = "snow_globe_run_store/v4";
    public const string LegacySchemaVersion = "snow_globe_run_store/v2";
    public const string ParticipantCommandIdentity = "snow_globe_participant_command/v1";
    public const int MaximumParticipantEvaluations = 128;
    public const int MaximumLedgerRecords = 4096;
    public const int MaximumFieldLength = 128;
    public const int MaximumParticipantIdLength = 64;
    public const int MaximumIdempotencyKeyLength = 64;
    public const int MaximumHeaderBytes = 4096;
    public const int MaximumLedgerRecordBytes = 2048;
    public const int MaximumLedgerBytes = MaximumLedgerRecords * (MaximumLedgerRecordBytes + 1);
    private const string HeaderFileName = "run.json";
    private const string LedgerFileName = "ledger.jsonl";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };
    private static readonly HashSet<string> V2HeaderProperties = new(StringComparer.Ordinal) { "schema_version", "rules_identity", "prompt_identity", "adapter_identity", "seed", "agent_count" };
    private static readonly HashSet<string> V3HeaderProperties = new(V2HeaderProperties, StringComparer.Ordinal) { "participant_command_identity" };
    private static readonly HashSet<string> RecordProperties = new(StringComparer.Ordinal) { "sequence", "kind", "tick", "agent_id", "action", "quantity", "accepted", "rejection_reason", "structure_id", "state_digest", "event_digest", "checksum", "header_checksum" };
    private static readonly HashSet<string> ParticipantEvaluationProperties = new(StringComparer.Ordinal)
    {
        "sequence", "kind", "tick", "participant_id", "idempotency_key", "expected_tick", "expected_state_digest", "expected_event_digest",
        "agent_id", "action", "quantity", "accepted", "rejection_reason", "accepted_event_sequence", "accepted_structure_id",
        "resulting_state_digest", "resulting_event_digest", "checksum", "header_checksum"
    };
    private static readonly HashSet<string> DomainRejectionReasons = new(StringComparer.Ordinal)
    {
        "unknown_agent",
        "unknown_action",
        "quantity_must_be_positive",
        "construction_quantity_must_be_zero",
        "shelter_missing",
        "insufficient_resources_or_invalid_action"
    };
    private static readonly HashSet<string> ParticipantRejectionReasons = new(DomainRejectionReasons, StringComparer.Ordinal)
    {
        "stale_tick",
        "stale_state_digest",
        "stale_event_digest"
    };
    private readonly string _directory;
    private readonly IRunStoreFileSystem _files;
    private readonly SnowGlobeRunIdentity _identity;
    private readonly string _headerChecksum;
    private readonly IDisposable _lockStream;
    private readonly RunStoreV4Storage _storage;
    private readonly SemaphoreSlim _operationLease = new(1, 1);
    private readonly object _appendGate = new();
    private readonly List<SnowGlobeLedgerRecord> _records;
    private readonly List<SnowGlobeParticipantEvaluationRecord> _participantEvaluations;
    private readonly Dictionary<(string ParticipantId, string IdempotencyKey), SnowGlobeParticipantEvaluationRecord> _participantIndex;
    private readonly Dictionary<(string ParticipantId, string IdempotencyKey), SnowGlobeParticipantCommandReceipt> _participantReceipts;
    private int _nextSequence;
    private int _expectedTick;
    private int _expectedEventCount;
    private string _expectedStateDigest;
    private string _expectedEventDigest;
    private List<SnowGlobeLedgerRecord>? _scheduledTickFrame;
    private bool _agentScheduleInProgress;
    private bool _poisoned;
    private bool _disposed;

    internal Func<Task>? BeforeOperationLeaseReleaseForTesting { get; set; }
    internal Action? BeforeLedgerAppendFlushForTesting { get; set; }
    internal bool IsPoisoned => Volatile.Read(ref _poisoned);

    private SnowGlobeRunStore(
        string directory,
        IRunStoreFileSystem files,
        SnowGlobeRunLedger ledger,
        string headerChecksum,
        IDisposable lockStream,
        SnowGlobeWorld expectedWorld,
        RunStoreV4State storageState)
    {
        _directory = directory;
        _files = files;
        _identity = ledger.Identity;
        _headerChecksum = headerChecksum;
        _lockStream = lockStream;
        _storage = new RunStoreV4Storage(directory, files, storageState);
        _records = ledger.Records.ToList();
        _participantEvaluations = ledger.ParticipantEvaluationRecords.ToList();
        _participantIndex = _participantEvaluations.ToDictionary(entry => (entry.ParticipantId, entry.IdempotencyKey), entry => entry);
        _participantReceipts = SnowGlobePersistedRun.Reconstruct(ledger).ParticipantReceipts.ToDictionary(
            entry => (entry.Key.ParticipantId, entry.Key.IdempotencyKey),
            entry => entry.Value);
        _nextSequence = ledger.EntryCount;
        _expectedTick = expectedWorld.Tick;
        _expectedEventCount = expectedWorld.Events.Count;
        _expectedStateDigest = expectedWorld.StateDigest();
        _expectedEventDigest = expectedWorld.EventDigest();
    }

    public SnowGlobeRunIdentity Identity => _identity;
    public string DirectoryPath => _directory;

    public static SnowGlobeRunStore CreateNew(string directory, SnowGlobeRunIdentity identity)
        => CreateNew(directory, identity, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobeRunStore CreateNew(string directory, SnowGlobeRunIdentity identity, IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(files);
        ValidateIdentity(identity, requireCurrent: true);
        return CreateEmptyFramedStore(directory, identity, files);
    }

    /// <summary>Internal compatibility-fixture seam; public CreateNew remains v5-only.</summary>
    internal static SnowGlobeRunStore CreateV4Fixture(string directory, SnowGlobeRunIdentity identity) =>
        CreateV4Fixture(directory, identity, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobeRunStore CreateV4Fixture(
        string directory,
        SnowGlobeRunIdentity identity,
        IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(files);
        ValidateIdentity(identity, requireCurrent: false);
        if (identity.SchemaVersion != V4SchemaVersion)
            throw new InvalidDataException("The compatibility fixture seam accepts only an explicit v4 identity.");
        return CreateEmptyFramedStore(directory, identity, files);
    }

    private static SnowGlobeRunStore CreateEmptyFramedStore(
        string directory,
        SnowGlobeRunIdentity identity,
        IRunStoreFileSystem files)
    {
        if (files.DirectoryExists(directory) && files.EnumerateEntryNames(directory).Count != 0) throw new InvalidOperationException("Run store directory must be empty; existing artifacts are never replaced.");
        files.CreateDirectory(directory);
        IDisposable lease = AcquireWriterLock(directory, files);
        try
        {
            byte[] header = CanonicalIdentityBytes(identity);
            if (header.Length > MaximumHeaderBytes) throw new InvalidDataException("Run identity exceeds the bounded header limit.");
            files.CreateFile(Path.Combine(directory, HeaderFileName), header, RunStoreWriteKind.Header);
            RunStoreV4Storage.CreateEmpty(directory, files);
            RunStoreV4State state = ReadV4(directory, files, identity, Digest(header), header);
            return new SnowGlobeRunStore(directory, files, state.Ledger, state.HeaderChecksum, lease, SnowGlobeWorld.Create(identity.Seed, identity.AgentCount), state);
        }
        catch { lease.Dispose(); throw; }
    }

    public static SnowGlobeRunStore OpenForAppend(string directory)
        => OpenForAppend(directory, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobeRunStore OpenForAppend(string directory, IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(files);
        // Read and reject legacy stores before acquiring a lease, because acquiring a lease can create a file.
        RunStoreV4State preflight = ReadStateForAppend(directory, files);
        IDisposable lease = AcquireWriterLock(directory, files);
        try
        {
            RunStoreV4State state = ReadStateForAppend(directory, files);
            if (!FixedEquals(state.EvidenceChecksum, preflight.EvidenceChecksum)) throw new InvalidDataException("Run store changed while append ownership was acquired.");
            if (state.PendingFrame is not null)
            {
                RunStoreV4Storage.WriteContinuation(directory, files, state);
                state = ReadStateForAppend(directory, files);
            }
            SnowGlobeWorld resumed = SnowGlobePersistedRun.Reconstruct(state.Ledger).World;
            return new SnowGlobeRunStore(directory, files, state.Ledger, state.HeaderChecksum, lease, resumed, state);
        }
        catch { lease.Dispose(); throw; }
    }

    public static SnowGlobeRunLedger Read(string directory)
        => ReadWithEvidence(directory, PhysicalRunStoreFileSystem.Instance).Ledger;

    internal static SnowGlobeRunLedger Read(string directory, IRunStoreReadFileSystem files)
        => ReadWithEvidence(directory, files).Ledger;

    internal static RunStoreReadEvidence ReadWithEvidence(string directory, IRunStoreReadFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(files);
        string headerPath = Path.Combine(directory, HeaderFileName);
        string ledgerPath = Path.Combine(directory, LedgerFileName);
        if (!files.FileExists(headerPath) || !files.FileExists(ledgerPath)) throw new InvalidDataException("Run store artifacts are incomplete.");
        byte[] headerBytes = files.ReadFile(headerPath, MaximumHeaderBytes, "Run identity");
        SnowGlobeRunIdentity identity = ReadIdentityForInspection(headerBytes);
        string headerChecksum = CanonicalIdentityChecksum(identity);
        if (identity.SchemaVersion is V4SchemaVersion or SchemaVersion)
        {
            RunStoreV4State state = ReadV4(directory, files, identity, headerChecksum, headerBytes);
            return new RunStoreReadEvidence(state.Ledger, state.EvidenceChecksum, state.HeaderChecksum, state.DurableRecovery);
        }

        SnowGlobeRunLedger ledger = ReadLegacy(directory, files, identity, headerChecksum, out byte[] ledgerBytes);
        return new RunStoreReadEvidence(ledger, LegacyRawEvidenceChecksum(headerBytes, ledgerBytes), null, null);
    }

    internal static SnowGlobeRunIdentity ReadIdentityForInspection(byte[] headerBytes)
    {
        SnowGlobeRunIdentity identity = DeserializeIdentityStrict(headerBytes);
        ValidateIdentity(identity, requireCurrent: false);
        return identity;
    }

    private static RunStoreV4State ReadStateForAppend(string directory, IRunStoreFileSystem files)
    {
        string headerPath = Path.Combine(directory, HeaderFileName);
        if (!files.FileExists(headerPath)) throw new InvalidDataException("Run store artifacts are incomplete.");
        byte[] headerBytes = files.ReadFile(headerPath, MaximumHeaderBytes, "Run identity");
        SnowGlobeRunIdentity identity = DeserializeIdentityStrict(headerBytes);
        ValidateIdentity(identity, requireCurrent: false);
        if (identity.SchemaVersion is not V4SchemaVersion and not SchemaVersion) throw new InvalidDataException("Legacy v2/v3 run stores are read-only and are never upgraded in place.");
        return ReadV4(directory, files, identity, CanonicalIdentityChecksum(identity), headerBytes);
    }

    private static RunStoreV4State ReadV4(string directory, IRunStoreReadFileSystem files, SnowGlobeRunIdentity identity, string headerChecksum, ReadOnlySpan<byte> rawHeader) =>
        RunStoreV4Storage.Read(directory, files, identity, headerChecksum, rawHeader);

    private static SnowGlobeRunLedger ReadLegacy(string directory, IRunStoreReadFileSystem files, SnowGlobeRunIdentity identity, string headerChecksum, out byte[] ledgerBytes)
    {
        HashSet<string> allowedArtifacts = new(StringComparer.Ordinal) { HeaderFileName, LedgerFileName, ".writer.lock" };
        if (files.EnumerateEntryNames(directory).Any(name => !allowedArtifacts.Contains(name)))
            throw new InvalidDataException("Legacy run store contains unknown or extra artifacts.");
        string ledgerPath = Path.Combine(directory, LedgerFileName);
        ledgerBytes = files.ReadFile(ledgerPath, MaximumLedgerBytes, "Ledger");
        List<SnowGlobeLedgerRecord> records = new();
        List<SnowGlobeParticipantEvaluationRecord> participantEvaluations = new();
        int expectedSequence = 0;
        foreach (byte[] line in ReadLedgerLines(ledgerBytes))
        {
            if (expectedSequence >= MaximumLedgerRecords) throw new InvalidDataException("Ledger exceeds bounded record limit.");
            ParseAndValidateEntry(line, identity, headerChecksum, expectedSequence++, out SnowGlobeLedgerRecord? record, out SnowGlobeParticipantEvaluationRecord? participant);
            if (record is not null) records.Add(record);
            else if (participant is not null) participantEvaluations.Add(participant);
        }
        SnowGlobeRunLedger ledger = new(identity, records.AsReadOnly(), participantEvaluations.AsReadOnly());
        _ = SnowGlobePersistedRun.Reconstruct(ledger);
        return ledger;
    }

    /// <summary>Reserves the maximum possible record count for a complete tick before its first response is persisted.</summary>
    public void ReserveWholeTick(int agentCount)
    {
        ThrowIfDisposed();
        if (agentCount != _identity.AgentCount) throw new InvalidDataException("Tick agent schedule does not match the run identity.");
        int maximumTickRecords = checked(agentCount * 4 + 1);
        if (_nextSequence > MaximumLedgerRecords - maximumTickRecords) throw new InvalidDataException("Ledger lacks capacity for a complete tick.");
    }

    internal async ValueTask<IDisposable> AcquireOperationLeaseAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLease.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ThrowIfDisposed(); return new OperationLease(_operationLease); }
        catch { _operationLease.Release(); throw; }
    }

    private bool TryAcquireOperationLease(out IDisposable? lease)
    {
        ThrowIfDisposed();
        if (!_operationLease.Wait(0))
        {
            lease = null;
            return false;
        }
        try
        {
            ThrowIfDisposed();
            lease = new OperationLease(_operationLease);
            return true;
        }
        catch
        {
            _operationLease.Release();
            throw;
        }
    }

    internal Task WaitBeforeOperationLeaseReleaseForTestingAsync() =>
        BeforeOperationLeaseReleaseForTesting?.Invoke() ?? Task.CompletedTask;

    internal void ExhaustCapacityForTesting()
    {
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (_agentScheduleInProgress) throw new InvalidOperationException("Cannot exhaust capacity during a scheduled frame.");
            _nextSequence = MaximumLedgerRecords;
        }
    }

    internal void BindAndReserveWholeTick(SnowGlobeWorld world, IReadOnlyList<string> scheduledAgents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduledAgents);
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (_agentScheduleInProgress) throw new InvalidDataException("A scheduled tick is already in progress.");
            if (world.Seed != _identity.Seed || world.Tick != _expectedTick || !FixedEquals(world.StateDigest(), _expectedStateDigest) || !FixedEquals(world.EventDigest(), _expectedEventDigest)) throw new InvalidDataException("World does not match the store's latest continuity state.");
            string[] expectedAgents = SnowGlobeWorld.Create(_identity.Seed, _identity.AgentCount).Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal).ToArray();
            if (!scheduledAgents.SequenceEqual(expectedAgents, StringComparer.Ordinal)) throw new InvalidDataException("World agent schedule does not match the run identity.");
            ReserveWholeTick(scheduledAgents.Count);
            _scheduledTickFrame = new List<SnowGlobeLedgerRecord>(checked(scheduledAgents.Count * 4 + 1));
            _agentScheduleInProgress = true;
        }
    }

    internal void CompleteWholeTick(SnowGlobeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (!_agentScheduleInProgress || _scheduledTickFrame is null || world.Tick != _expectedTick + 1) throw new InvalidDataException("Completed tick does not advance the bound world exactly once.");
            SnowGlobeRunLedger prospectiveLedger = new(
                _identity,
                _records.Concat(_scheduledTickFrame).ToArray(),
                _participantEvaluations.AsReadOnly());
            SnowGlobeWorld reconstructed = SnowGlobePersistedRun.Reconstruct(prospectiveLedger).World;
            if (reconstructed.Tick != world.Tick
                || reconstructed.Events.Count != world.Events.Count
                || !FixedEquals(reconstructed.StateDigest(), world.StateDigest())
                || !FixedEquals(reconstructed.EventDigest(), world.EventDigest()))
            {
                throw new InvalidDataException("Completed tick frame does not match deterministic world reconstruction.");
            }

            byte[][] lines = _scheduledTickFrame.Select(record => JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions)).ToArray();
            foreach (byte[] line in lines)
            {
                if (line.Length > MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
            }
            int batchLength = lines.Aggregate(0, (length, line) => checked(length + line.Length + 1));
            byte[] batch = new byte[batchLength];
            int offset = 0;
            foreach (byte[] line in lines)
            {
                line.CopyTo(batch, offset);
                offset += line.Length;
                batch[offset++] = (byte)'\n';
            }
            try
            {
                BeforeLedgerAppendFlushForTesting?.Invoke();
                _storage.AppendFrame(RunStoreFrameKind.ScheduledTick, batch, _nextSequence, _scheduledTickFrame.Count);
            }
            catch
            {
                // A process crash or low-level partial append is not claimed atomic. Poison the writer so it cannot
                // continue from uncertain bytes; a later strict reader/reopen must decide whether the artifact is valid.
                _poisoned = true;
                throw;
            }

            _records.AddRange(_scheduledTickFrame);
            _nextSequence += _scheduledTickFrame.Count;
            _expectedTick = world.Tick;
            _expectedEventCount = world.Events.Count;
            _expectedStateDigest = world.StateDigest();
            _expectedEventDigest = world.EventDigest();
            _scheduledTickFrame = null;
            _agentScheduleInProgress = false;
        }
    }

    internal void AbortWholeTick()
    {
        lock (_appendGate)
        {
            _scheduledTickFrame = null;
            _agentScheduleInProgress = false;
        }
    }

    /// <summary>
    /// Deterministically evaluates and appends one already-admitted, well-formed participant command at a tick boundary.
    /// Admission failures belong to the caller and are never ledger records. This advances store continuity without
    /// mutating a caller-owned live world.
    /// </summary>
    public SnowGlobeParticipantCommandReceipt EvaluateAndAppendParticipantCommand(SnowGlobeParticipantCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!TryAcquireOperationLease(out IDisposable? operationLease))
        {
            lock (_appendGate) return TransientReceipt(command, "operation_in_progress");
        }
        using (operationLease)
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (_identity.SchemaVersion is not V4SchemaVersion and not SchemaVersion) throw new InvalidDataException("Participant evaluations cannot be appended to a legacy v2/v3 run store.");
            if (_agentScheduleInProgress) return TransientReceipt(command, "operation_in_progress");
            ValidateParticipantCommand(command);

            (string ParticipantId, string IdempotencyKey) key = (command.ParticipantId!, command.IdempotencyKey!);
            if (_participantIndex.TryGetValue(key, out SnowGlobeParticipantEvaluationRecord? existing))
            {
                return SameParticipantCommand(existing, command)
                    ? _participantReceipts[key]
                    : TransientReceipt(command, "command_id_conflict");
            }
            SnowGlobeInternalRunReconstruction durable = SnowGlobePersistedRun.ReconstructInternal(CurrentLedger());
            if (_identity.SchemaVersion == SchemaVersion && !durable.IsDurablyPaused)
                return TransientReceipt(command, "must_be_paused");
            if (_participantEvaluations.Count >= MaximumParticipantEvaluations) return TransientReceipt(command, "idempotency_store_saturated");
            if (_nextSequence >= MaximumLedgerRecords) return TransientReceipt(command, "run_store_capacity_exhausted");
            SnowGlobeWorld candidate = durable.Public.World;
            if (candidate.Tick != _expectedTick || candidate.Events.Count != _expectedEventCount || !FixedEquals(candidate.StateDigest(), _expectedStateDigest) || !FixedEquals(candidate.EventDigest(), _expectedEventDigest)) throw new InvalidDataException("Reconstructed ledger continuity does not match the append cursor.");

            bool accepted = false;
            string? reason;
            int? acceptedEventSequence = null;
            string? acceptedStructureId = null;
            if (command.ExpectedTick != candidate.Tick) reason = "stale_tick";
            else if (!FixedEquals(command.ExpectedStateDigest!, candidate.StateDigest())) reason = "stale_state_digest";
            else if (!FixedEquals(command.ExpectedEventDigest!, candidate.EventDigest())) reason = "stale_event_digest";
            else
            {
                SnowGlobeCommitResult result = candidate.ValidateAndCommit(new SnowGlobeActionProposal(command.TargetAgentId!, command.Action!.Value, command.Quantity!.Value));
                accepted = result.Accepted;
                reason = result.RejectionReason;
                if (accepted)
                {
                    SnowGlobeEvent acceptedEvent = candidate.Events[^1];
                    acceptedEventSequence = acceptedEvent.Sequence;
                    acceptedStructureId = acceptedEvent.StructureId;
                }
            }

            SnowGlobeParticipantEvaluationRecord unsigned = new(
                _nextSequence,
                SnowGlobeLedgerKind.ParticipantEvaluation,
                candidate.Tick,
                command.ParticipantId!,
                command.IdempotencyKey!,
                command.ExpectedTick!.Value,
                command.ExpectedStateDigest!,
                command.ExpectedEventDigest!,
                command.TargetAgentId!,
                command.Action!.Value.ToString(),
                command.Quantity!.Value,
                accepted,
                reason,
                acceptedEventSequence,
                acceptedStructureId,
                candidate.StateDigest(),
                candidate.EventDigest(),
                string.Empty,
                _headerChecksum);
            SnowGlobeParticipantEvaluationRecord evaluation = unsigned with { Checksum = ParticipantChecksum(unsigned) };
            ValidateParticipantRecord(evaluation);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(evaluation, JsonOptions);
            AppendCompleteLine(line, RunStoreFrameKind.ParticipantEvaluation);

            _participantEvaluations.Add(evaluation);
            _participantIndex.Add(key, evaluation);
            SnowGlobeParticipantCommandReceipt receipt = Receipt(evaluation, candidate.Events.Count - 1);
            _participantReceipts.Add(key, receipt);
            _nextSequence++;
            _expectedEventCount = candidate.Events.Count;
            _expectedStateDigest = evaluation.ResultingStateDigest;
            _expectedEventDigest = evaluation.ResultingEventDigest;
            return receipt;
        }
    }

    /// <summary>Appends one v5-only durable pause transition at a ledger/frame boundary.</summary>
    internal RunStorePauseAppendResult AppendPauseTransition(bool paused)
    {
        if (!TryAcquireOperationLease(out IDisposable? operationLease))
            return RunStorePauseAppendResult.OperationInProgress;
        using (operationLease)
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (_identity.SchemaVersion != SchemaVersion)
                throw new InvalidDataException("Durable pause transitions require a v5 run store.");
            if (_agentScheduleInProgress) return RunStorePauseAppendResult.OperationInProgress;

            SnowGlobeInternalRunReconstruction durable = SnowGlobePersistedRun.ReconstructInternal(CurrentLedger(), _identity);
            SnowGlobeWorld world = durable.Public.World;
            if (world.Tick != _expectedTick
                || world.Events.Count != _expectedEventCount
                || !FixedEquals(world.StateDigest(), _expectedStateDigest)
                || !FixedEquals(world.EventDigest(), _expectedEventDigest))
            {
                throw new InvalidDataException("Reconstructed ledger continuity does not match the append cursor.");
            }
            if (durable.IsDurablyPaused == paused) return RunStorePauseAppendResult.AlreadyInTargetState;
            if (_nextSequence >= MaximumLedgerRecords) return RunStorePauseAppendResult.CapacityExhausted;

            SnowGlobeLedgerRecord unsigned = new(
                _nextSequence,
                SnowGlobeLedgerKind.PauseTransition,
                world.Tick,
                string.Empty,
                paused ? "Pause" : "Resume",
                0,
                null,
                null,
                null,
                world.StateDigest(),
                world.EventDigest(),
                string.Empty,
                _headerChecksum);
            SnowGlobeLedgerRecord transition = unsigned with { Checksum = Checksum(unsigned) };
            ValidateRecord(transition);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(transition, JsonOptions);
            AppendCompleteLine(line, RunStoreFrameKind.PauseTransition);

            _records.Add(transition);
            _nextSequence++;
            return RunStorePauseAppendResult.Appended;
        }
    }

    public void AppendResponse(SnowGlobeObservation observation, SnowGlobeActionProposal response) => Append(SnowGlobeLedgerKind.Response, observation.Tick, response.AgentId, response.Action, response.Quantity, null, null, null, null, null);
    public void AppendProposal(int tick, SnowGlobeActionProposal proposal) => Append(SnowGlobeLedgerKind.Proposal, tick, proposal.AgentId, proposal.Action, proposal.Quantity, null, null, null, null, null);
    public void AppendCommit(int tick, SnowGlobeActionProposal proposal, SnowGlobeCommitResult result) => Append(SnowGlobeLedgerKind.Commit, tick, proposal.AgentId, proposal.Action, proposal.Quantity, result.Accepted, result.RejectionReason, null, null, null);
    public void AppendEvent(SnowGlobeEvent entry) => Append(SnowGlobeLedgerKind.Event, entry.Tick, entry.AgentId, entry.Action, entry.Quantity, true, null, entry.StructureId, null, null);
    public void AppendCheckpoint(SnowGlobeWorld world) => Append(SnowGlobeLedgerKind.Checkpoint, world.Tick, string.Empty, string.Empty, 0, null, null, null, world.StateDigest(), world.EventDigest());

    private void Append(SnowGlobeLedgerKind kind, int tick, string agentId, SnowGlobeActionKind action, int quantity, bool? accepted, string? rejectionReason, string? structureId, string? stateDigest, string? eventDigest) => Append(kind, tick, agentId, action.ToString(), quantity, accepted, rejectionReason, structureId, stateDigest, eventDigest);
    private void Append(SnowGlobeLedgerKind kind, int tick, string agentId, string action, int quantity, bool? accepted, string? rejectionReason, string? structureId, string? stateDigest, string? eventDigest)
    {
        lock (_appendGate)
        {
            ThrowIfDisposed();
            if (!_agentScheduleInProgress || _scheduledTickFrame is null) throw new InvalidOperationException("Scheduled ledger records require an active whole-tick frame.");
            int sequence = checked(_nextSequence + _scheduledTickFrame.Count);
            if (sequence >= MaximumLedgerRecords) throw new InvalidDataException("Ledger exceeds bounded record limit.");
            SnowGlobeLedgerRecord unsigned = new(sequence, kind, tick, agentId, action, quantity, accepted, rejectionReason, structureId, stateDigest, eventDigest, string.Empty, _headerChecksum);
            SnowGlobeLedgerRecord record = unsigned with { Checksum = Checksum(unsigned) };
            ValidateRecord(record);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
            if (line.Length > MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
            _scheduledTickFrame.Add(record);
        }
    }

    private void AppendCompleteLine(byte[] line, RunStoreFrameKind frameKind)
    {
        if (line.Length > MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
        try
        {
            BeforeLedgerAppendFlushForTesting?.Invoke();
            byte[] payload = new byte[line.Length + 1];
            line.CopyTo(payload, 0);
            payload[^1] = (byte)'\n';
            _storage.AppendFrame(frameKind, payload, _nextSequence, 1);
        }
        catch
        {
            _poisoned = true;
            throw;
        }
    }

    private SnowGlobeRunLedger CurrentLedger() => new(_identity, _records.AsReadOnly(), _participantEvaluations.AsReadOnly());

    private SnowGlobeParticipantCommandReceipt TransientReceipt(SnowGlobeParticipantCommand command, string reason) => new(
        false,
        reason,
        false,
        IsCanonicalOpaqueId(command.ParticipantId, MaximumParticipantIdLength) ? command.ParticipantId : null,
        IsCanonicalOpaqueId(command.IdempotencyKey, MaximumIdempotencyKeyLength) ? command.IdempotencyKey : null,
        _expectedTick,
        _expectedEventCount - 1,
        _expectedStateDigest,
        _expectedEventDigest);

    private static SnowGlobeParticipantCommandReceipt Receipt(SnowGlobeParticipantEvaluationRecord evaluation, int currentEventSequence) => new(
        evaluation.Accepted,
        evaluation.RejectionReason,
        false,
        evaluation.ParticipantId,
        evaluation.IdempotencyKey,
        evaluation.Tick,
        evaluation.Accepted ? evaluation.AcceptedEventSequence : currentEventSequence,
        evaluation.ResultingStateDigest,
        evaluation.ResultingEventDigest);

    internal static bool SameParticipantCommand(SnowGlobeParticipantEvaluationRecord existing, SnowGlobeParticipantCommand command) =>
        existing.ParticipantId == command.ParticipantId
        && existing.IdempotencyKey == command.IdempotencyKey
        && existing.ExpectedTick == command.ExpectedTick
        && existing.ExpectedStateDigest == command.ExpectedStateDigest
        && existing.ExpectedEventDigest == command.ExpectedEventDigest
        && existing.AgentId == command.TargetAgentId
        && command.Action is SnowGlobeActionKind action
        && existing.Action == action.ToString()
        && existing.Quantity == command.Quantity;

    private static IDisposable AcquireWriterLock(string directory, IRunStoreFileSystem files) =>
        files.AcquireExclusiveLease(Path.Combine(directory, ".writer.lock"));

    private static IEnumerable<byte[]> ReadLedgerLines(byte[] bytes)
    {
        List<byte> line = new(MaximumLedgerRecordBytes);
        foreach (byte value in bytes)
        {
            if (value == '\n')
            {
                if (line.Count == 0) throw new InvalidDataException("Ledger truncation or blank record detected.");
                yield return line.ToArray();
                line.Clear();
                continue;
            }
            if (value == '\r' || line.Count >= MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
            line.Add(value);
        }
        if (line.Count != 0) throw new InvalidDataException("Ledger truncation: final record is missing its terminating line feed.");
    }
    private static SnowGlobeRunIdentity DeserializeIdentityStrict(byte[] utf8)
    {
        using JsonDocument document = ParseFlatObject(utf8, "Run identity");
        if (!document.RootElement.TryGetProperty("schema_version", out JsonElement schema) || schema.ValueKind != JsonValueKind.String) throw new InvalidDataException("Run identity schema is missing.");
        string? schemaVersion = schema.GetString();
        if (schemaVersion == LegacySchemaVersion)
        {
            ValidateProperties(document.RootElement, V2HeaderProperties, "Run identity");
            V2RunIdentity parsed = JsonSerializer.Deserialize<V2RunIdentity>(utf8, JsonOptions) ?? throw new InvalidDataException("Run identity is missing.");
            return new(parsed.SchemaVersion, parsed.RulesIdentity, parsed.PromptIdentity, parsed.AdapterIdentity, parsed.Seed, parsed.AgentCount);
        }
        if (schemaVersion is not PreviousSchemaVersion and not V4SchemaVersion and not SchemaVersion) throw new InvalidDataException("Run identity schema is unsupported.");
        ValidateProperties(document.RootElement, V3HeaderProperties, "Run identity");
        return JsonSerializer.Deserialize<SnowGlobeRunIdentity>(utf8, JsonOptions) ?? throw new InvalidDataException("Run identity is missing.");
    }

    internal static SnowGlobeLedgerKind ReadEntryKind(byte[] utf8)
    {
        using JsonDocument document = ParseFlatObject(utf8, "Ledger record");
        if (!document.RootElement.TryGetProperty("kind", out JsonElement kind) || kind.ValueKind != JsonValueKind.Number || !kind.TryGetInt32(out int value) || !Enum.IsDefined((SnowGlobeLedgerKind)value)) throw new InvalidDataException("Ledger record kind is invalid.");
        return (SnowGlobeLedgerKind)value;
    }

    internal static void ParseAndValidateEntry(
        byte[] line,
        SnowGlobeRunIdentity identity,
        string headerChecksum,
        int expectedSequence,
        out SnowGlobeLedgerRecord? record,
        out SnowGlobeParticipantEvaluationRecord? participant)
    {
        if (expectedSequence < 0 || expectedSequence >= MaximumLedgerRecords)
            throw new InvalidDataException("Ledger exceeds bounded record limit.");
        SnowGlobeLedgerKind kind = ReadEntryKind(line);
        if (kind == SnowGlobeLedgerKind.ParticipantEvaluation)
        {
            if (identity.SchemaVersion == LegacySchemaVersion) throw new InvalidDataException("Legacy v2 ledgers cannot contain participant evaluations.");
            SnowGlobeParticipantEvaluationRecord evaluation = DeserializeStrict<SnowGlobeParticipantEvaluationRecord>(line, ParticipantEvaluationProperties, "Participant evaluation");
            if (evaluation.Sequence != expectedSequence) throw new InvalidDataException("Ledger sequence is out of order or duplicated.");
            ValidateParticipantRecord(evaluation);
            if (!FixedEquals(evaluation.HeaderChecksum, headerChecksum) || !FixedEquals(evaluation.Checksum, ParticipantChecksum(evaluation with { Checksum = string.Empty }))) throw new InvalidDataException("Participant evaluation integrity mismatch.");
            record = null;
            participant = evaluation;
            return;
        }

        SnowGlobeLedgerRecord parsed = DeserializeStrict<SnowGlobeLedgerRecord>(line, RecordProperties, "Ledger record");
        if (parsed.Sequence != expectedSequence) throw new InvalidDataException("Ledger sequence is out of order or duplicated.");
        if (parsed.Kind == SnowGlobeLedgerKind.PauseTransition && identity.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Durable pause evidence is valid only in v5 run stores.");
        ValidateRecord(parsed);
        if (!FixedEquals(parsed.HeaderChecksum, headerChecksum) || !FixedEquals(parsed.Checksum, Checksum(parsed with { Checksum = string.Empty }))) throw new InvalidDataException("Ledger record integrity mismatch.");
        record = parsed;
        participant = null;
    }

    private static T DeserializeStrict<T>(byte[] utf8, HashSet<string> allowedProperties, string description)
    {
        try
        {
            using JsonDocument document = ParseFlatObject(utf8, description);
            ValidateProperties(document.RootElement, allowedProperties, description);
            return JsonSerializer.Deserialize<T>(utf8, JsonOptions) ?? throw new InvalidDataException($"{description} is missing.");
        }
        catch (JsonException exception) { throw new InvalidDataException($"{description} is malformed.", exception); }
    }

    private static JsonDocument ParseFlatObject(byte[] utf8, string description)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(utf8, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new InvalidDataException($"{description} must be a JSON object.");
            }
            return document;
        }
        catch (JsonException exception) { throw new InvalidDataException($"{description} is malformed.", exception); }
    }

    private static void ValidateProperties(JsonElement element, HashSet<string> allowedProperties, string description)
    {
        HashSet<string> encountered = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!encountered.Add(property.Name) || !allowedProperties.Contains(property.Name)) throw new InvalidDataException($"{description} contains an unknown or duplicate property.");
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) throw new InvalidDataException($"{description} contains an unsupported nested value.");
        }
        if (!encountered.SetEquals(allowedProperties)) throw new InvalidDataException($"{description} is incomplete.");
    }

    private static void ValidateIdentity(SnowGlobeRunIdentity identity, bool requireCurrent)
    {
        bool common = identity.RulesIdentity == SnowGlobePersistedRun.RulesIdentity
            && identity.PromptIdentity == SnowGlobePersistedRun.PromptIdentity
            && SnowGlobeInferenceIdentity.IsCanonical(identity.AdapterIdentity)
            && identity.AgentCount is >= 1 and <= 64
            && IsBounded(identity.RulesIdentity)
            && IsBounded(identity.PromptIdentity)
            && IsBounded(identity.AdapterIdentity);
        bool supportedVersion = identity.SchemaVersion switch
        {
            SchemaVersion => identity.ParticipantCommandIdentity == ParticipantCommandIdentity,
            V4SchemaVersion when !requireCurrent => identity.ParticipantCommandIdentity == ParticipantCommandIdentity,
            PreviousSchemaVersion when !requireCurrent => identity.ParticipantCommandIdentity == ParticipantCommandIdentity,
            LegacySchemaVersion when !requireCurrent => identity.ParticipantCommandIdentity is null,
            _ => false
        };
        if (!common || !supportedVersion) throw new InvalidDataException("Run identity is unsupported or incomplete.");
    }

    internal static void ValidateSupportedIdentity(SnowGlobeRunIdentity identity) => ValidateIdentity(identity, requireCurrent: false);

    private static void ValidateRecord(SnowGlobeLedgerRecord record)
    {
        if (!Enum.IsDefined(record.Kind) || record.Kind == SnowGlobeLedgerKind.ParticipantEvaluation || record.Sequence < 0 || record.Tick < 0 || !IsDigest(record.Checksum) || !IsDigest(record.HeaderChecksum)) throw new InvalidDataException("Ledger record has invalid sequence, kind, or integrity fields.");
        if (record.Kind is SnowGlobeLedgerKind.Response or SnowGlobeLedgerKind.Proposal or SnowGlobeLedgerKind.Commit or SnowGlobeLedgerKind.Event
            && (!IsCanonicalOpaqueId(record.AgentId, MaximumFieldLength) || !TryParseCanonicalAction(record.Action, out _)))
        {
            throw new InvalidDataException("Ledger record contains a non-canonical action or agent.");
        }

        bool validShape = record.Kind switch
        {
            SnowGlobeLedgerKind.Response or SnowGlobeLedgerKind.Proposal =>
                record.Accepted is null
                && record.RejectionReason is null
                && record.StructureId is null
                && record.StateDigest is null
                && record.EventDigest is null,
            SnowGlobeLedgerKind.Commit =>
                record.Accepted is not null
                && (record.Accepted.Value ? record.RejectionReason is null : IsAllowedReason(record.RejectionReason, DomainRejectionReasons))
                && record.StructureId is null
                && record.StateDigest is null
                && record.EventDigest is null,
            SnowGlobeLedgerKind.Event =>
                record.Accepted == true
                && record.RejectionReason is null
                && (record.StructureId is null || IsCanonicalOpaqueId(record.StructureId, MaximumFieldLength))
                && record.StateDigest is null
                && record.EventDigest is null,
            SnowGlobeLedgerKind.Checkpoint =>
                record.AgentId == string.Empty
                && record.Action == string.Empty
                && record.Quantity == 0
                && record.Accepted is null
                && record.RejectionReason is null
                && record.StructureId is null
                && IsDigest(record.StateDigest)
                && IsDigest(record.EventDigest),
            SnowGlobeLedgerKind.PauseTransition =>
                record.AgentId == string.Empty
                && record.Action is "Pause" or "Resume"
                && record.Quantity == 0
                && record.Accepted is null
                && record.RejectionReason is null
                && record.StructureId is null
                && IsDigest(record.StateDigest)
                && IsDigest(record.EventDigest),
            _ => false
        };
        if (!validShape) throw new InvalidDataException("Ledger record fields do not match the exact kind-specific shape.");
    }

    internal static void ValidatePauseTransitionRecord(SnowGlobeLedgerRecord record, SnowGlobeRunIdentity identity)
    {
        if (identity.SchemaVersion != SchemaVersion || record.Kind != SnowGlobeLedgerKind.PauseTransition)
            throw new InvalidDataException("Durable pause evidence is valid only in v5 run stores.");
        ValidateRecord(record);
        string expectedHeaderChecksum = CanonicalIdentityChecksum(identity);
        if (!FixedEquals(record.HeaderChecksum, expectedHeaderChecksum)
            || !FixedEquals(record.Checksum, Checksum(record with { Checksum = string.Empty })))
        {
            throw new InvalidDataException("Durable pause transition integrity mismatch.");
        }
    }

    internal static void ValidateParticipantRecord(SnowGlobeParticipantEvaluationRecord record)
    {
        if (record.Kind != SnowGlobeLedgerKind.ParticipantEvaluation || record.Sequence < 0 || record.Tick < 0 || record.ExpectedTick < 0 || !IsDigest(record.Checksum) || !IsDigest(record.HeaderChecksum)) throw new InvalidDataException("Participant evaluation has invalid identity or integrity fields.");
        if (!IsCanonicalOpaqueId(record.ParticipantId, MaximumParticipantIdLength) || !IsCanonicalOpaqueId(record.IdempotencyKey, MaximumIdempotencyKeyLength) || !IsCanonicalOpaqueId(record.AgentId, MaximumParticipantIdLength)) throw new InvalidDataException("Participant evaluation contains a non-canonical identity.");
        if (!TryParseCanonicalAction(record.Action, out _) || !IsDigest(record.ExpectedStateDigest) || !IsDigest(record.ExpectedEventDigest) || !IsDigest(record.ResultingStateDigest) || !IsDigest(record.ResultingEventDigest)) throw new InvalidDataException("Participant evaluation contains a non-canonical action or digest.");
        if (record.Accepted)
        {
            if (record.RejectionReason is not null
                || record.AcceptedEventSequence is null or < 0
                || record.AcceptedStructureId is not null && !IsCanonicalOpaqueId(record.AcceptedStructureId, MaximumFieldLength))
            {
                throw new InvalidDataException("Accepted participant evaluation has an invalid disposition.");
            }
        }
        else if (!IsAllowedReason(record.RejectionReason, ParticipantRejectionReasons) || record.AcceptedEventSequence is not null || record.AcceptedStructureId is not null)
        {
            throw new InvalidDataException("Rejected participant evaluation has an invalid disposition.");
        }
    }

    private static void ValidateParticipantCommand(SnowGlobeParticipantCommand command)
    {
        if (!IsCanonicalOpaqueId(command.ParticipantId, MaximumParticipantIdLength)) throw new InvalidDataException("Participant id is invalid.");
        if (!IsCanonicalOpaqueId(command.IdempotencyKey, MaximumIdempotencyKeyLength)) throw new InvalidDataException("Participant idempotency key is invalid.");
        if (command.ExpectedTick is null or < 0 || !IsDigest(command.ExpectedStateDigest) || !IsDigest(command.ExpectedEventDigest)) throw new InvalidDataException("Participant command anchors are invalid.");
        if (!IsCanonicalOpaqueId(command.TargetAgentId, MaximumParticipantIdLength) || command.Action is not SnowGlobeActionKind action || !Enum.IsDefined(action) || command.Quantity is null) throw new InvalidDataException("Participant command action is invalid.");
    }
    internal static bool TryParseCanonicalAction(string? value, out SnowGlobeActionKind action)
    {
        action = default;
        return value is not null && Enum.TryParse(value, ignoreCase: false, out action) && Enum.IsDefined(action) && string.Equals(value, Enum.GetName(action), StringComparison.Ordinal);
    }
    internal static bool IsCanonicalOpaqueId(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current is '-' or '_')) return false;
        }
        return value[0] is >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsAllowedReason(string? value, HashSet<string> allowlist) => value is not null && allowlist.Contains(value);
    private static bool IsBounded(string? value) => value is null || value.Length <= MaximumFieldLength;
    internal static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static string CanonicalIdentityChecksum(SnowGlobeRunIdentity identity) => Digest(CanonicalIdentityBytes(identity));

    private static string LegacyRawEvidenceChecksum(ReadOnlySpan<byte> header, ReadOnlySpan<byte> ledger)
    {
        using IncrementalHash evidence = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        evidence.AppendData("snow_globe_run_store_legacy_raw_evidence/v1"u8);
        AppendFramedRawEvidence(evidence, header);
        AppendFramedRawEvidence(evidence, ledger);
        return Convert.ToHexString(evidence.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendFramedRawEvidence(IncrementalHash evidence, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, bytes.Length);
        evidence.AppendData(length);
        evidence.AppendData(bytes);
    }

    private static byte[] CanonicalIdentityBytes(SnowGlobeRunIdentity identity) => identity.SchemaVersion == LegacySchemaVersion
        ? JsonSerializer.SerializeToUtf8Bytes(new V2RunIdentity(identity.SchemaVersion, identity.RulesIdentity, identity.PromptIdentity, identity.AdapterIdentity, identity.Seed, identity.AgentCount), JsonOptions)
        : JsonSerializer.SerializeToUtf8Bytes(identity, JsonOptions);
    private static string Checksum(SnowGlobeLedgerRecord record) => Digest(Utf8.GetBytes($"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}|{record.HeaderChecksum}"));
    internal static string ParticipantChecksum(SnowGlobeParticipantEvaluationRecord record) => Digest(Utf8.GetBytes(
        $"{record.Sequence}|{record.Kind}|{record.Tick}|{record.ParticipantId}|{record.IdempotencyKey}|{record.ExpectedTick}|{record.ExpectedStateDigest}|{record.ExpectedEventDigest}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.AcceptedEventSequence}|{record.AcceptedStructureId}|{record.ResultingStateDigest}|{record.ResultingEventDigest}|{record.HeaderChecksum}"));
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Utf8.GetBytes(left), Utf8.GetBytes(right));
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SnowGlobeRunStore));
        if (_poisoned) throw new InvalidDataException("Run-store writer is poisoned after an uncertain low-level append failure.");
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _operationLease.Dispose(); _lockStream.Dispose(); }

    private sealed record V2RunIdentity(string SchemaVersion, string RulesIdentity, string PromptIdentity, string AdapterIdentity, int Seed, int AgentCount);

    private sealed class OperationLease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;
        public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}

/// <summary>Recorded normalized responses are replay inputs, not world authority; the scheduler still validates every proposal.</summary>
public sealed class SnowGlobeReplayAdapter : ISnowGlobeIdentifiedInferenceAdapter
{
    public const string Identity = "snow_globe_recorded_response_adapter/v1";
    private readonly Queue<SnowGlobeLedgerRecord> _responses;
    public string AdapterIdentity { get; }
    public SnowGlobeReplayAdapter(SnowGlobeRunLedger ledger, string expectedAdapterIdentity, int startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (string.IsNullOrWhiteSpace(expectedAdapterIdentity) || !string.Equals(ledger.Identity.AdapterIdentity, expectedAdapterIdentity, StringComparison.Ordinal)) throw new InvalidDataException("Recorded responses require an exact expected adapter identity.");
        SnowGlobeRunStore.ValidateSupportedIdentity(ledger.Identity);
        AdapterIdentity = ledger.Identity.AdapterIdentity;
        _responses = new Queue<SnowGlobeLedgerRecord>(ledger.Records.Where(record => record.Kind == SnowGlobeLedgerKind.Response && record.Tick >= startTick));
    }
    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_responses.Count == 0) throw new InvalidDataException("Recorded response ledger is exhausted.");
        SnowGlobeLedgerRecord response = _responses.Dequeue();
        if (response.Tick != observation.Tick || !string.Equals(response.AgentId, observation.AgentId, StringComparison.Ordinal) || !SnowGlobeRunStore.TryParseCanonicalAction(response.Action, out SnowGlobeActionKind action)) throw new InvalidDataException("Recorded response does not match the deterministic observation order.");
        return ValueTask.FromResult(new SnowGlobeActionProposal(response.AgentId, action, response.Quantity));
    }
}
