using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Versioned local identity for one normalized, deterministic lab run.</summary>
public sealed record SnowGlobeRunIdentity(string SchemaVersion, string RulesIdentity, string PromptIdentity, string AdapterIdentity, int Seed, int AgentCount);
public enum SnowGlobeLedgerKind { Response, Proposal, Commit, Event, Checkpoint }
public sealed record SnowGlobeLedgerRecord(int Sequence, SnowGlobeLedgerKind Kind, int Tick, string AgentId, string Action, int Quantity, bool? Accepted, string? RejectionReason, string? StructureId, string? StateDigest, string? EventDigest, string Checksum, string HeaderChecksum = "");
public sealed record SnowGlobeRunLedger(SnowGlobeRunIdentity Identity, IReadOnlyList<SnowGlobeLedgerRecord> Records);

/// <summary>Bounded, append-only evidence. Readers never modify artifacts; a durable lock file is a lease, not ownership evidence.</summary>
public sealed class SnowGlobeRunStore : IDisposable
{
    public const string SchemaVersion = "snow_globe_run_store/v2";
    public const int MaximumLedgerRecords = 4096;
    public const int MaximumFieldLength = 128;
    public const int MaximumHeaderBytes = 4096;
    public const int MaximumLedgerRecordBytes = 2048;
    public const int MaximumLedgerBytes = MaximumLedgerRecords * (MaximumLedgerRecordBytes + 1);
    private const string HeaderFileName = "run.json";
    private const string LedgerFileName = "ledger.jsonl";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };
    private static readonly HashSet<string> HeaderProperties = new(StringComparer.Ordinal) { "schema_version", "rules_identity", "prompt_identity", "adapter_identity", "seed", "agent_count" };
    private static readonly HashSet<string> RecordProperties = new(StringComparer.Ordinal) { "sequence", "kind", "tick", "agent_id", "action", "quantity", "accepted", "rejection_reason", "structure_id", "state_digest", "event_digest", "checksum", "header_checksum" };
    private readonly string _directory;
    private readonly SnowGlobeRunIdentity _identity;
    private readonly string _headerChecksum;
    private readonly FileStream _lockStream;
    private readonly SemaphoreSlim _operationLease = new(1, 1);
    private readonly object _appendGate = new();
    private int _nextSequence;
    private int _expectedTick;
    private string _expectedStateDigest;
    private string _expectedEventDigest;
    private bool _disposed;

    private SnowGlobeRunStore(string directory, SnowGlobeRunIdentity identity, string headerChecksum, FileStream lockStream, int nextSequence, SnowGlobeWorld expectedWorld)
    {
        _directory = directory;
        _identity = identity;
        _headerChecksum = headerChecksum;
        _lockStream = lockStream;
        _nextSequence = nextSequence;
        _expectedTick = expectedWorld.Tick;
        _expectedStateDigest = expectedWorld.StateDigest();
        _expectedEventDigest = expectedWorld.EventDigest();
    }

    public SnowGlobeRunIdentity Identity => _identity;
    public string DirectoryPath => _directory;

    public static SnowGlobeRunStore CreateNew(string directory, SnowGlobeRunIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ValidateIdentity(identity);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new InvalidOperationException("Run store directory must be empty; existing artifacts are never replaced.");
        Directory.CreateDirectory(directory);
        FileStream lease = AcquireWriterLock(directory);
        try
        {
            byte[] header = JsonSerializer.SerializeToUtf8Bytes(identity, JsonOptions);
            if (header.Length > MaximumHeaderBytes) throw new InvalidDataException("Run identity exceeds the bounded header limit.");
            using FileStream stream = new(Path.Combine(directory, HeaderFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            stream.Write(header, 0, header.Length);
            stream.Flush(flushToDisk: true);
            return new SnowGlobeRunStore(directory, identity, Digest(header), lease, 0, SnowGlobeWorld.Create(identity.Seed, identity.AgentCount));
        }
        catch { lease.Dispose(); throw; }
    }

    public static SnowGlobeRunStore OpenForAppend(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        FileStream lease = AcquireWriterLock(directory);
        try
        {
            SnowGlobeRunLedger ledger = Read(directory);
            SnowGlobeWorld resumed = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);
            return new SnowGlobeRunStore(directory, ledger.Identity, CanonicalIdentityChecksum(ledger.Identity), lease, ledger.Records.Count, resumed);
        }
        catch { lease.Dispose(); throw; }
    }

    public static SnowGlobeRunLedger Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string headerPath = Path.Combine(directory, HeaderFileName);
        string ledgerPath = Path.Combine(directory, LedgerFileName);
        if (!File.Exists(headerPath) || !File.Exists(ledgerPath)) throw new InvalidDataException("Run store artifacts are incomplete.");
        byte[] headerBytes = ReadBoundedFile(headerPath, MaximumHeaderBytes, "Run identity");
        SnowGlobeRunIdentity identity = DeserializeStrict<SnowGlobeRunIdentity>(headerBytes, HeaderProperties, "Run identity");
        ValidateIdentity(identity);
        string headerChecksum = CanonicalIdentityChecksum(identity);
        EnsureFileLength(ledgerPath, MaximumLedgerBytes, "Ledger");
        List<SnowGlobeLedgerRecord> records = new();
        int expectedSequence = 0;
        foreach (byte[] line in ReadLedgerLines(ledgerPath))
        {
            if (records.Count >= MaximumLedgerRecords) throw new InvalidDataException("Ledger exceeds bounded record limit.");
            SnowGlobeLedgerRecord record = DeserializeStrict<SnowGlobeLedgerRecord>(line, RecordProperties, "Ledger record");
            if (record.Sequence != expectedSequence++) throw new InvalidDataException("Ledger sequence is out of order or duplicated.");
            ValidateRecord(record);
            if (!FixedEquals(record.HeaderChecksum, headerChecksum) || !FixedEquals(record.Checksum, Checksum(record with { Checksum = string.Empty }))) throw new InvalidDataException("Ledger record integrity mismatch.");
            records.Add(record);
        }
        SnowGlobeRunLedger ledger = new(identity, records.AsReadOnly());
        _ = SnowGlobePersistedRun.ResumeAtLatestCheckpoint(ledger);
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

    internal void BindAndReserveWholeTick(SnowGlobeWorld world, IReadOnlyList<string> scheduledAgents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduledAgents);
        if (world.Seed != _identity.Seed || world.Tick != _expectedTick || !FixedEquals(world.StateDigest(), _expectedStateDigest) || !FixedEquals(world.EventDigest(), _expectedEventDigest)) throw new InvalidDataException("World does not match the store's latest checkpoint continuity state.");
        string[] expectedAgents = SnowGlobeWorld.Create(_identity.Seed, _identity.AgentCount).Agents.Select(agent => agent.AgentId).OrderBy(agentId => agentId, StringComparer.Ordinal).ToArray();
        if (!scheduledAgents.SequenceEqual(expectedAgents, StringComparer.Ordinal)) throw new InvalidDataException("World agent schedule does not match the run identity.");
        ReserveWholeTick(scheduledAgents.Count);
    }

    internal void CompleteWholeTick(SnowGlobeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Tick != _expectedTick + 1) throw new InvalidDataException("Completed tick does not advance the bound world exactly once.");
        _expectedTick = world.Tick;
        _expectedStateDigest = world.StateDigest();
        _expectedEventDigest = world.EventDigest();
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
            if (_nextSequence >= MaximumLedgerRecords) throw new InvalidDataException("Ledger exceeds bounded record limit.");
            SnowGlobeLedgerRecord unsigned = new(_nextSequence, kind, tick, agentId, action, quantity, accepted, rejectionReason, structureId, stateDigest, eventDigest, string.Empty, _headerChecksum);
            SnowGlobeLedgerRecord record = unsigned with { Checksum = Checksum(unsigned) };
            ValidateRecord(record);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
            if (line.Length > MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
            using FileStream ledger = new(Path.Combine(_directory, LedgerFileName), FileMode.Append, FileAccess.Write, FileShare.Read);
            ledger.Write(line, 0, line.Length);
            ledger.WriteByte((byte)'\n');
            ledger.Flush(flushToDisk: true);
            _nextSequence++;
        }
    }

    private static FileStream AcquireWriterLock(string directory) => new(Path.Combine(directory, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static byte[] ReadBoundedFile(string path, int maximumBytes, string description)
    {
        // The single read handle denies concurrent writers and probes one byte past the cap, avoiding a length-check/read race.
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] boundedBuffer = new byte[checked(maximumBytes + 1)];
        int total = 0;
        while (total < boundedBuffer.Length)
        {
            int read = stream.Read(boundedBuffer, total, boundedBuffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        if (total > maximumBytes) throw new InvalidDataException($"{description} exceeds the bounded byte limit.");
        return boundedBuffer[..total];
    }
    private static void EnsureFileLength(string path, int maximumBytes, string description)
    {
        long length = new FileInfo(path).Length;
        if (length > maximumBytes) throw new InvalidDataException($"{description} exceeds the bounded byte limit.");
    }
    private static IEnumerable<byte[]> ReadLedgerLines(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        List<byte> line = new(MaximumLedgerRecordBytes);
        int value;
        while ((value = stream.ReadByte()) >= 0)
        {
            if (value == '\n')
            {
                if (line.Count == 0) throw new InvalidDataException("Ledger truncation or blank record detected.");
                yield return line.ToArray();
                line.Clear();
                continue;
            }
            if (value == '\r' || line.Count >= MaximumLedgerRecordBytes) throw new InvalidDataException("Ledger record exceeds bounded byte limit.");
            line.Add((byte)value);
        }
        if (line.Count != 0) throw new InvalidDataException("Ledger truncation: final record is missing its terminating line feed.");
    }
    private static T DeserializeStrict<T>(byte[] utf8, HashSet<string> allowedProperties, string description)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{description} must be a JSON object.");
            HashSet<string> encountered = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!encountered.Add(property.Name) || !allowedProperties.Contains(property.Name)) throw new InvalidDataException($"{description} contains an unknown or duplicate property.");
                if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) throw new InvalidDataException($"{description} contains an unsupported nested value.");
            }
            if (!encountered.SetEquals(allowedProperties)) throw new InvalidDataException($"{description} is incomplete.");
            return JsonSerializer.Deserialize<T>(utf8, JsonOptions) ?? throw new InvalidDataException($"{description} is missing.");
        }
        catch (JsonException exception) { throw new InvalidDataException($"{description} is malformed.", exception); }
    }
    private static void ValidateIdentity(SnowGlobeRunIdentity identity)
    {
        if (identity.SchemaVersion != SchemaVersion || identity.RulesIdentity != SnowGlobePersistedRun.RulesIdentity || identity.PromptIdentity != SnowGlobePersistedRun.PromptIdentity || string.IsNullOrWhiteSpace(identity.AdapterIdentity) || identity.AgentCount is < 1 or > 64 || !IsBounded(identity.RulesIdentity) || !IsBounded(identity.PromptIdentity) || !IsBounded(identity.AdapterIdentity)) throw new InvalidDataException("Run identity is unsupported or incomplete.");
    }
    internal static void ValidateSupportedIdentity(SnowGlobeRunIdentity identity) => ValidateIdentity(identity);
    private static void ValidateRecord(SnowGlobeLedgerRecord record)
    {
        if (!Enum.IsDefined(record.Kind) || record.Sequence < 0 || record.Tick < 0 || !IsDigest(record.Checksum) || !IsDigest(record.HeaderChecksum)) throw new InvalidDataException("Ledger record has invalid sequence or integrity fields.");
        if (record.Kind is SnowGlobeLedgerKind.Response or SnowGlobeLedgerKind.Proposal or SnowGlobeLedgerKind.Commit or SnowGlobeLedgerKind.Event)
        {
            if (string.IsNullOrWhiteSpace(record.AgentId) || !IsBounded(record.AgentId) || !IsBounded(record.Action) || !TryParseCanonicalAction(record.Action, out _)) throw new InvalidDataException("Ledger record contains an unknown action or missing agent.");
        }
        if ((record.Kind is SnowGlobeLedgerKind.Response or SnowGlobeLedgerKind.Proposal) && record.Accepted is not null || record.Kind == SnowGlobeLedgerKind.Commit && record.Accepted is null || record.Kind == SnowGlobeLedgerKind.Event && record.Accepted != true) throw new InvalidDataException("Ledger record has an invalid commit disposition.");
        if (!IsBounded(record.RejectionReason) || !IsBounded(record.StructureId)) throw new InvalidDataException("Ledger record exceeds field bounds.");
        if (record.Kind == SnowGlobeLedgerKind.Checkpoint && (!IsDigest(record.StateDigest) || !IsDigest(record.EventDigest))) throw new InvalidDataException("Checkpoint digest is missing.");
    }
    internal static bool TryParseCanonicalAction(string? value, out SnowGlobeActionKind action)
    {
        action = default;
        return value is not null && Enum.TryParse(value, ignoreCase: false, out action) && Enum.IsDefined(action) && string.Equals(value, Enum.GetName(action), StringComparison.Ordinal);
    }
    private static bool IsBounded(string? value) => value is null || value.Length <= MaximumFieldLength;
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static string CanonicalIdentityChecksum(SnowGlobeRunIdentity identity) => Digest(JsonSerializer.SerializeToUtf8Bytes(identity, JsonOptions));
    private static string Checksum(SnowGlobeLedgerRecord record) => Digest(Utf8.GetBytes($"{record.Sequence}|{record.Kind}|{record.Tick}|{record.AgentId}|{record.Action}|{record.Quantity}|{record.Accepted}|{record.RejectionReason}|{record.StructureId}|{record.StateDigest}|{record.EventDigest}|{record.HeaderChecksum}"));
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Utf8.GetBytes(left), Utf8.GetBytes(right));
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(SnowGlobeRunStore)); }
    public void Dispose() { if (_disposed) return; _disposed = true; _operationLease.Dispose(); _lockStream.Dispose(); }

    private sealed class OperationLease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;
        public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}

/// <summary>Recorded normalized responses are replay inputs, not world authority; the scheduler still validates every proposal.</summary>
public sealed class SnowGlobeReplayAdapter : ISnowGlobeInferenceAdapter
{
    public const string Identity = "snow_globe_recorded_response_adapter/v1";
    private readonly Queue<SnowGlobeLedgerRecord> _responses;
    public SnowGlobeReplayAdapter(SnowGlobeRunLedger ledger, string expectedAdapterIdentity, int startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (string.IsNullOrWhiteSpace(expectedAdapterIdentity) || !string.Equals(ledger.Identity.AdapterIdentity, expectedAdapterIdentity, StringComparison.Ordinal)) throw new InvalidDataException("Recorded responses require an exact expected adapter identity.");
        SnowGlobeRunStore.ValidateSupportedIdentity(ledger.Identity);
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
