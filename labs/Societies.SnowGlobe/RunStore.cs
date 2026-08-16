using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>
/// Versioned, append-only local evidence for one deterministic lab run. This deliberately records
/// normalized values only: no prompts, participant text, credentials, or provider payloads.
/// </summary>
public sealed record SnowGlobeRunIdentity(
    string SchemaVersion,
    string RulesIdentity,
    string PromptIdentity,
    string AdapterIdentity,
    int Seed,
    int AgentCount);

public enum SnowGlobeLedgerKind { Response, Proposal, Commit, Event, Checkpoint }

public sealed record SnowGlobeLedgerRecord(
    int Sequence,
    SnowGlobeLedgerKind Kind,
    int Tick,
    string AgentId,
    string Action,
    int Quantity,
    bool? Accepted,
    string? RejectionReason,
    string? StructureId,
    string? StateDigest,
    string? EventDigest,
    string Checksum);

public sealed record SnowGlobeRunLedger(SnowGlobeRunIdentity Identity, IReadOnlyList<SnowGlobeLedgerRecord> Records);

public sealed class SnowGlobeRunStore : IDisposable
{
    public const string SchemaVersion = "snow_globe_run_store/v1";
    public const int MaximumLedgerRecords = 4096;
    public const int MaximumFieldLength = 128;
    private const string HeaderFileName = "run.json";
    private const string LedgerFileName = "ledger.jsonl";
    private readonly string _directory;
    private readonly SnowGlobeRunIdentity _identity;
    private readonly FileStream? _lockStream;
    private int _nextSequence;
    private bool _disposed;

    private SnowGlobeRunStore(string directory, SnowGlobeRunIdentity identity, FileStream? lockStream, int nextSequence)
    {
        _directory = directory;
        _identity = identity;
        _lockStream = lockStream;
        _nextSequence = nextSequence;
    }

    public SnowGlobeRunIdentity Identity => _identity;
    public string DirectoryPath => _directory;

    public static SnowGlobeRunStore CreateNew(string directory, SnowGlobeRunIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ValidateIdentity(identity);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new InvalidOperationException("Run store directory must be empty; existing artifacts are never replaced.");
        Directory.CreateDirectory(directory);
        string headerPath = Path.Combine(directory, HeaderFileName);
        using (FileStream header = new(headerPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (Utf8JsonWriter writer = new(header))
        {
            JsonSerializer.Serialize(writer, identity, JsonOptions);
        }
        return new SnowGlobeRunStore(directory, identity, AcquireWriterLock(directory), 0);
    }

    public static SnowGlobeRunStore OpenForAppend(string directory)
    {
        SnowGlobeRunLedger ledger = Read(directory);
        return new SnowGlobeRunStore(directory, ledger.Identity, AcquireWriterLock(directory), ledger.Records.Count);
    }

    public static SnowGlobeRunLedger Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string headerPath = Path.Combine(directory, HeaderFileName);
        string ledgerPath = Path.Combine(directory, LedgerFileName);
        if (!File.Exists(headerPath) || !File.Exists(ledgerPath)) throw new InvalidDataException("Run store artifacts are incomplete.");
        SnowGlobeRunIdentity identity;
        try { identity = JsonSerializer.Deserialize<SnowGlobeRunIdentity>(File.ReadAllText(headerPath), JsonOptions) ?? throw new InvalidDataException("Run identity is missing."); }
        catch (JsonException exception) { throw new InvalidDataException("Run identity is malformed.", exception); }
        ValidateIdentity(identity);
        List<SnowGlobeLedgerRecord> records = new();
        int expectedSequence = 0;
        foreach (string line in File.ReadLines(ledgerPath))
        {
            if (string.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Ledger truncation or blank record detected.");
            SnowGlobeLedgerRecord record;
            try { record = JsonSerializer.Deserialize<SnowGlobeLedgerRecord>(line, JsonOptions) ?? throw new InvalidDataException("Ledger record is missing."); }
            catch (JsonException exception) { throw new InvalidDataException("Ledger record is malformed or truncated.", exception); }
            if (records.Count >= MaximumLedgerRecords) throw new InvalidDataException("Ledger exceeds bounded record limit.");
            if (record.Sequence != expectedSequence++) throw new InvalidDataException("Ledger sequence is out of order or duplicated.");
            ValidateRecord(record);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(record.Checksum), Encoding.UTF8.GetBytes(Checksum(record with { Checksum = string.Empty }))))
                throw new InvalidDataException("Ledger record checksum mismatch.");
            records.Add(record);
        }
        SnowGlobeRunLedger ledger = new(identity, records.AsReadOnly());
        // A successful read means a run is structurally replayable, not merely parseable.
        _ = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);
        return ledger;
    }

    public void AppendResponse(SnowGlobeObservation observation, SnowGlobeActionProposal response) =>
        Append(SnowGlobeLedgerKind.Response, observation.Tick, response.AgentId, response.Action, response.Quantity, null, null, null, null, null);
    public void AppendProposal(int tick, SnowGlobeActionProposal proposal) =>
        Append(SnowGlobeLedgerKind.Proposal, tick, proposal.AgentId, proposal.Action, proposal.Quantity, null, null, null, null, null);
    public void AppendCommit(int tick, SnowGlobeActionProposal proposal, SnowGlobeCommitResult result) =>
        Append(SnowGlobeLedgerKind.Commit, tick, proposal.AgentId, proposal.Action, proposal.Quantity, result.Accepted, result.RejectionReason, null, null, null);
    public void AppendEvent(SnowGlobeEvent entry) =>
        Append(SnowGlobeLedgerKind.Event, entry.Tick, entry.AgentId, entry.Action, entry.Quantity, true, null, entry.StructureId, null, null);
    public void AppendCheckpoint(SnowGlobeWorld world) =>
        Append(SnowGlobeLedgerKind.Checkpoint, world.Tick, string.Empty, string.Empty, 0, null, null, null, world.StateDigest(), world.EventDigest());

    private void Append(SnowGlobeLedgerKind kind, int tick, string agentId, SnowGlobeActionKind action, int quantity, bool? accepted, string? rejectionReason, string? structureId, string? stateDigest, string? eventDigest) =>
        Append(kind, tick, agentId, action.ToString(), quantity, accepted, rejectionReason, structureId, stateDigest, eventDigest);
    private void Append(SnowGlobeLedgerKind kind, int tick, string agentId, string action, int quantity, bool? accepted, string? rejectionReason, string? structureId, string? stateDigest, string? eventDigest)
    {
        ThrowIfDisposed();
        SnowGlobeLedgerRecord unsigned = new(_nextSequence, kind, tick, agentId, action, quantity, accepted, rejectionReason, structureId, stateDigest, eventDigest, string.Empty);
        SnowGlobeLedgerRecord record = unsigned with { Checksum = Checksum(unsigned) };
        ValidateRecord(record);
        string line = JsonSerializer.Serialize(record, JsonOptions) + "\n";
        using FileStream ledger = new(Path.Combine(_directory, LedgerFileName), FileMode.Append, FileAccess.Write, FileShare.Read);
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        ledger.Write(bytes, 0, bytes.Length);
        ledger.Flush(flushToDisk: true);
        _nextSequence++;
    }

    private static FileStream AcquireWriterLock(string directory) =>
        new(Path.Combine(directory, ".writer.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    private static void ValidateIdentity(SnowGlobeRunIdentity identity)
    {
        if (identity.SchemaVersion != SchemaVersion || string.IsNullOrWhiteSpace(identity.RulesIdentity) || string.IsNullOrWhiteSpace(identity.PromptIdentity)
            || string.IsNullOrWhiteSpace(identity.AdapterIdentity) || identity.AgentCount is < 1 or > 64
            || !IsBounded(identity.RulesIdentity) || !IsBounded(identity.PromptIdentity) || !IsBounded(identity.AdapterIdentity)) throw new InvalidDataException("Run identity is unsupported or incomplete.");
    }
    private static void ValidateRecord(SnowGlobeLedgerRecord record)
    {
        if (!Enum.IsDefined(record.Kind) || record.Sequence < 0 || record.Tick < 0 || !IsDigest(record.Checksum)) throw new InvalidDataException("Ledger record has invalid sequence, tick, or checksum.");
        if (record.Kind is SnowGlobeLedgerKind.Response or SnowGlobeLedgerKind.Proposal or SnowGlobeLedgerKind.Commit or SnowGlobeLedgerKind.Event)
        {
            if (string.IsNullOrWhiteSpace(record.AgentId) || !IsBounded(record.AgentId) || !Enum.TryParse<SnowGlobeActionKind>(record.Action, out _)) throw new InvalidDataException("Ledger record contains an unknown action or missing agent.");
        }
        if (!IsBounded(record.RejectionReason) || !IsBounded(record.StructureId)) throw new InvalidDataException("Ledger record exceeds field bounds.");
        if (record.Kind == SnowGlobeLedgerKind.Checkpoint && (!IsDigest(record.StateDigest) || !IsDigest(record.EventDigest))) throw new InvalidDataException("Checkpoint digest is missing.");
    }
    private static bool IsBounded(string? value) => value is null || value.Length <= MaximumFieldLength;
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Checksum(SnowGlobeLedgerRecord record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}"))).ToLowerInvariant();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(SnowGlobeRunStore)); }
    public void Dispose() { if (_disposed) return; _lockStream?.Dispose(); File.Delete(Path.Combine(_directory, ".writer.lock")); _disposed = true; }
}

/// <summary>Recorded normalized responses are replay inputs, not world authority; the scheduler still validates every proposal.</summary>
public sealed class SnowGlobeReplayAdapter : ISnowGlobeInferenceAdapter
{
    public const string Identity = "snow_globe_recorded_response_adapter/v1";
    private readonly Queue<SnowGlobeLedgerRecord> _responses;
    public SnowGlobeReplayAdapter(SnowGlobeRunLedger ledger, int startTick = 0) => _responses = new Queue<SnowGlobeLedgerRecord>(ledger.Records.Where(record => record.Kind == SnowGlobeLedgerKind.Response && record.Tick >= startTick));
    public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_responses.Count == 0) throw new InvalidDataException("Recorded response ledger is exhausted.");
        SnowGlobeLedgerRecord response = _responses.Dequeue();
        if (response.Tick != observation.Tick || !string.Equals(response.AgentId, observation.AgentId, StringComparison.Ordinal) || !Enum.TryParse(response.Action, out SnowGlobeActionKind action))
            throw new InvalidDataException("Recorded response does not match the deterministic observation order.");
        return ValueTask.FromResult(new SnowGlobeActionProposal(response.AgentId, action, response.Quantity));
    }
}
