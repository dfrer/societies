using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

internal enum RunStoreWriteKind
{
    Header,
    Ledger,
    MarkerLog,
    PrepareMarker,
    ScheduledPayload,
    ParticipantPayload,
    CommitMarker,
    ContinuationMarker
}

internal interface IRunStoreFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    IReadOnlyList<string> EnumerateEntryNames(string directory);
    bool FileExists(string path);
    byte[] ReadFile(string path, int maximumBytes, string description);
    void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind);
    void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind);
    IDisposable AcquireExclusiveLease(string path);
}

internal sealed class PhysicalRunStoreFileSystem : IRunStoreFileSystem
{
    internal static PhysicalRunStoreFileSystem Instance { get; } = new();

    private PhysicalRunStoreFileSystem() { }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public IReadOnlyList<string> EnumerateEntryNames(string directory) =>
        Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray()!;
    public bool FileExists(string path) => File.Exists(path);

    public byte[] ReadFile(string path, int maximumBytes, string description)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] bounded = new byte[checked(maximumBytes + 1)];
        int total = 0;
        while (total < bounded.Length)
        {
            int read = stream.Read(bounded, total, bounded.Length - total);
            if (read == 0) break;
            total += read;
        }
        if (total > maximumBytes) throw new InvalidDataException($"{description} exceeds the bounded byte limit.");
        return bounded[..total];
    }

    public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind)
    {
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        if (kind != RunStoreWriteKind.ScheduledPayload)
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
            return;
        }

        // Scheduled recovery recognizes only prepared, checksum-bound prefixes of complete JSONL
        // records. Write at most four such chunks as independent durable units. A real partial
        // chunk/record is deliberately treated as corruption; power-loss atomicity inside one
        // filesystem write is outside this experiment's claim.
        int start = 0;
        foreach (int end in RunStoreV4Storage.ScheduledPayloadChunkEnds(bytes))
        {
            stream.Write(bytes[start..end]);
            stream.Flush(flushToDisk: true);
            start = end;
        }
        if (start != bytes.Length) throw new InvalidDataException("Scheduled payload is not complete JSONL.");
    }

    public IDisposable AcquireExclusiveLease(string path) =>
        new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}

/// <summary>One-shot deterministic write interruption used only through the internal filesystem seam.</summary>
internal sealed class FaultInjectingRunStoreFileSystem(
    IRunStoreFileSystem inner,
    RunStoreWriteKind targetKind,
    int bytesBeforeFailure) : IRunStoreFileSystem
{
    private int _injected;

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);
    public void CreateDirectory(string path) => inner.CreateDirectory(path);
    public IReadOnlyList<string> EnumerateEntryNames(string directory) => inner.EnumerateEntryNames(directory);
    public bool FileExists(string path) => inner.FileExists(path);
    public byte[] ReadFile(string path, int maximumBytes, string description) => inner.ReadFile(path, maximumBytes, description);
    public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);

    public void CreateFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind)
    {
        if (ShouldInject(kind))
        {
            int count = Math.Clamp(bytesBeforeFailure, 0, bytes.Length);
            if (count != 0) inner.CreateFile(path, bytes[..count], kind);
            throw new IOException($"deterministic_{kind.ToString().ToLowerInvariant()}_write_interruption");
        }
        inner.CreateFile(path, bytes, kind);
    }

    public void AppendFile(string path, ReadOnlySpan<byte> bytes, RunStoreWriteKind kind)
    {
        if (ShouldInject(kind))
        {
            int count = Math.Clamp(bytesBeforeFailure, 0, bytes.Length);
            if (kind == RunStoreWriteKind.ScheduledPayload)
            {
                // The physical adapter independently flushes complete records. Model a managed
                // interruption only between those units, never as invented unauthenticated bytes.
                count = RunStoreV4Storage.ScheduledPayloadChunkEnds(bytes).Where(end => end <= count).DefaultIfEmpty(0).Max();
            }
            if (count != 0) inner.AppendFile(path, bytes[..count], kind);
            throw new IOException($"deterministic_{kind.ToString().ToLowerInvariant()}_write_interruption");
        }
        inner.AppendFile(path, bytes, kind);
    }

    private bool ShouldInject(RunStoreWriteKind kind) =>
        kind == targetKind && Interlocked.CompareExchange(ref _injected, 1, 0) == 0;
}

internal enum RunStoreFrameKind { ScheduledTick, ParticipantEvaluation }
internal enum RunStoreRecoveryDisposition { AbandonIncompleteScheduledTick, RecoverCompleteScheduledTick }

internal sealed record RunStorePendingFrame(
    int SegmentIndex,
    RunStorePrepareMarker Prepare,
    byte[] Payload,
    byte[] CommitTail,
    RunStoreRecoveryDisposition Disposition,
    IReadOnlyList<SnowGlobeLedgerRecord> ScheduledRecords,
    int SourceLedgerLength,
    string SourceLedgerChecksum,
    int SourceMarkerLength,
    string SourceMarkerChecksum);

internal sealed record RunStoreV4State(
    SnowGlobeRunLedger Ledger,
    string HeaderChecksum,
    string ChainChecksum,
    string EvidenceChecksum,
    int CurrentSegmentIndex,
    int CurrentLedgerLength,
    int NextFrameIndex,
    int RecoveryCount,
    RunStorePendingFrame? PendingFrame);

internal sealed record RunStorePrepareMarker(
    string RecordType,
    string MarkerSchema,
    int SegmentIndex,
    int FrameIndex,
    string PreviousCommitChecksum,
    string FrameKind,
    int FirstSequence,
    int EntryCount,
    int PayloadLength,
    string PayloadChecksum,
    string PayloadPrefixManifest,
    string Checksum);

internal sealed record RunStoreCommitMarker(
    string RecordType,
    string MarkerSchema,
    int SegmentIndex,
    int FrameIndex,
    string PreviousCommitChecksum,
    string PrepareChecksum,
    string PayloadChecksum,
    int LedgerEndOffset,
    string Checksum);

internal sealed record RunStoreContinuationMarker(
    string RecordType,
    string MarkerSchema,
    int SegmentIndex,
    string PreviousCommitChecksum,
    int SourceSegmentIndex,
    int SourceFrameIndex,
    string SourcePrepareChecksum,
    int SourceLedgerLength,
    string SourceLedgerChecksum,
    int SourceMarkerLength,
    string SourceMarkerChecksum,
    string Disposition,
    string Checksum);

/// <summary>
/// Version-four framing implementation. Ledger payloads remain ordinary JSONL; prepare/commit markers
/// bind each append to the prior committed chain. Recovery writes only a new continuation segment.
/// </summary>
internal sealed class RunStoreV4Storage
{
    internal const string MarkerSchema = "snow_globe_run_store_marker/v1";
    internal const int MaximumMarkerBytes = 1024;
    internal const int MaximumRecoveryCount = 1;
    internal const int MaximumSegments = MaximumRecoveryCount + 1;
    internal const int MaximumScheduledPayloadBytes = 64 * 4 * (SnowGlobeRunStore.MaximumLedgerRecordBytes + 1) + SnowGlobeRunStore.MaximumLedgerRecordBytes + 1;
    internal const int MaximumMarkerLogBytes = (SnowGlobeRunStore.MaximumLedgerRecords * 2 + 1) * (MaximumMarkerBytes + 1);

    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 4, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };
    private static readonly HashSet<string> PrepareProperties = new(StringComparer.Ordinal)
    {
        "record_type", "marker_schema", "segment_index", "frame_index", "previous_commit_checksum", "frame_kind",
        "first_sequence", "entry_count", "payload_length", "payload_checksum", "payload_prefix_manifest", "checksum"
    };
    private static readonly HashSet<string> CommitProperties = new(StringComparer.Ordinal)
    {
        "record_type", "marker_schema", "segment_index", "frame_index", "previous_commit_checksum", "prepare_checksum",
        "payload_checksum", "ledger_end_offset", "checksum"
    };
    private static readonly HashSet<string> ContinuationProperties = new(StringComparer.Ordinal)
    {
        "record_type", "marker_schema", "segment_index", "previous_commit_checksum", "source_segment_index", "source_frame_index",
        "source_prepare_checksum", "source_ledger_length", "source_ledger_checksum", "source_marker_length", "source_marker_checksum",
        "disposition", "checksum"
    };

    private readonly string _directory;
    private readonly IRunStoreFileSystem _files;
    private string _chainChecksum;
    private int _segmentIndex;
    private int _ledgerLength;
    private int _nextFrameIndex;

    internal RunStoreV4Storage(string directory, IRunStoreFileSystem files, RunStoreV4State state)
    {
        _directory = directory;
        _files = files;
        _chainChecksum = state.ChainChecksum;
        _segmentIndex = state.CurrentSegmentIndex;
        _ledgerLength = state.CurrentLedgerLength;
        _nextFrameIndex = state.NextFrameIndex;
    }

    internal static void CreateEmpty(string directory, IRunStoreFileSystem files)
    {
        files.CreateFile(LedgerPath(directory, 0), ReadOnlySpan<byte>.Empty, RunStoreWriteKind.Ledger);
        files.CreateFile(MarkerPath(directory, 0), ReadOnlySpan<byte>.Empty, RunStoreWriteKind.MarkerLog);
    }

    internal void AppendFrame(RunStoreFrameKind kind, ReadOnlySpan<byte> payload, int firstSequence, int entryCount)
    {
        int maximumEntries = kind == RunStoreFrameKind.ParticipantEvaluation ? 1 : 64 * 4 + 1;
        int maximumPayload = kind == RunStoreFrameKind.ParticipantEvaluation
            ? SnowGlobeRunStore.MaximumLedgerRecordBytes + 1
            : MaximumScheduledPayloadBytes;
        if (entryCount < 1 || entryCount > maximumEntries || firstSequence < 0
            || firstSequence > SnowGlobeRunStore.MaximumLedgerRecords - entryCount
            || payload.Length < 1 || payload.Length > maximumPayload || payload[^1] != (byte)'\n')
            throw new InvalidDataException("Run-store frame exceeds its bounded shape.");

        string payloadChecksum = Digest(payload);
        string payloadPrefixManifest = kind == RunStoreFrameKind.ScheduledTick ? BuildPayloadPrefixManifest(payload) : string.Empty;
        RunStorePrepareMarker unsignedPrepare = new(
            "prepare", MarkerSchema, _segmentIndex, _nextFrameIndex, _chainChecksum,
            kind.ToString(), firstSequence, entryCount, payload.Length, payloadChecksum, payloadPrefixManifest, string.Empty);
        RunStorePrepareMarker prepare = unsignedPrepare with { Checksum = PrepareChecksum(unsignedPrepare) };
        byte[] prepareLine = SerializeLine(prepare);
        _files.AppendFile(MarkerPath(_directory, _segmentIndex), prepareLine, RunStoreWriteKind.PrepareMarker);

        RunStoreWriteKind payloadKind = kind == RunStoreFrameKind.ScheduledTick
            ? RunStoreWriteKind.ScheduledPayload
            : RunStoreWriteKind.ParticipantPayload;
        _files.AppendFile(LedgerPath(_directory, _segmentIndex), payload, payloadKind);

        int ledgerEnd = checked(_ledgerLength + payload.Length);
        RunStoreCommitMarker unsignedCommit = new(
            "commit", MarkerSchema, _segmentIndex, _nextFrameIndex, _chainChecksum,
            prepare.Checksum, payloadChecksum, ledgerEnd, string.Empty);
        RunStoreCommitMarker commit = unsignedCommit with { Checksum = CommitChecksum(unsignedCommit) };
        _files.AppendFile(MarkerPath(_directory, _segmentIndex), SerializeLine(commit), RunStoreWriteKind.CommitMarker);

        _chainChecksum = commit.Checksum;
        _ledgerLength = ledgerEnd;
        _nextFrameIndex++;
    }

    internal static RunStoreV4State Read(
        string directory,
        IRunStoreFileSystem files,
        SnowGlobeRunIdentity identity,
        string headerChecksum,
        ReadOnlySpan<byte> rawHeader)
    {
        ValidateLayout(directory, files);
        List<SnowGlobeLedgerRecord> records = new();
        List<SnowGlobeParticipantEvaluationRecord> participants = new();
        string chain = headerChecksum;
        int expectedSequence = 0;
        int nextFrame = 0;
        int recoveryCount = 0;
        RunStorePendingFrame? pending = null;
        int currentSegment = 0;
        int currentLedgerLength = 0;
        using IncrementalHash evidence = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        evidence.AppendData(Utf8.GetBytes($"header|{rawHeader.Length}|"));
        evidence.AppendData(rawHeader);

        for (int segmentIndex = 0; segmentIndex < MaximumSegments; segmentIndex++)
        {
            string ledgerPath = LedgerPath(directory, segmentIndex);
            string markerPath = MarkerPath(directory, segmentIndex);
            if (!files.FileExists(ledgerPath) && !files.FileExists(markerPath)) break;
            if (!files.FileExists(ledgerPath) || !files.FileExists(markerPath)) throw new InvalidDataException("Run-store segment artifacts are incomplete.");

            byte[] ledgerBytes = files.ReadFile(ledgerPath, SnowGlobeRunStore.MaximumLedgerBytes + MaximumScheduledPayloadBytes, "Run-store ledger segment");
            byte[] markerBytes = files.ReadFile(markerPath, MaximumMarkerLogBytes, "Run-store marker segment");
            AppendEvidence(evidence, segmentIndex, ledgerBytes, markerBytes);
            List<byte[]> markerLines = CompleteLines(markerBytes, out byte[] markerTail, "Run-store marker");
            int markerIndex = 0;
            int ledgerOffset = 0;

            if (segmentIndex > 0)
            {
                if (pending is null || recoveryCount >= MaximumRecoveryCount || markerIndex >= markerLines.Count)
                    throw new InvalidDataException("Run-store continuation is not linked to one recoverable tail.");
                RunStoreContinuationMarker continuation = DeserializeContinuation(markerLines[markerIndex++]);
                ValidateContinuation(continuation, segmentIndex, chain, pending, ledgerBytes.Length, markerBytes.Length);
                ApplyPending(pending, records, ref expectedSequence);
                chain = continuation.Checksum;
                nextFrame = checked(pending.Prepare.FrameIndex + 1);
                recoveryCount++;
                pending = null;
            }

            while (markerIndex < markerLines.Count)
            {
                RunStorePrepareMarker prepare = DeserializePrepare(markerLines[markerIndex++]);
                ValidatePrepare(prepare, segmentIndex, nextFrame, chain, expectedSequence, identity.AgentCount);
                if (markerIndex >= markerLines.Count)
                {
                    pending = ResolvePending(
                        segmentIndex, prepare, ledgerBytes, ledgerOffset, markerTail, markerBytes,
                        identity, records, participants, expectedSequence);
                    ledgerOffset = ledgerBytes.Length;
                    break;
                }

                RunStoreCommitMarker commit = DeserializeCommit(markerLines[markerIndex++]);
                ValidateCommit(commit, prepare, chain, ledgerOffset, ledgerBytes.Length);
                ReadCommittedPayload(ledgerBytes.AsSpan(ledgerOffset, commit.LedgerEndOffset - ledgerOffset), prepare, identity, headerChecksum, records, participants, ref expectedSequence);
                ledgerOffset = commit.LedgerEndOffset;
                chain = commit.Checksum;
                nextFrame++;
            }

            if (pending is null && markerTail.Length != 0)
                throw new InvalidDataException("Run-store marker tail is unknown or corrupt.");
            if (pending is null && ledgerOffset != ledgerBytes.Length)
                throw new InvalidDataException("Run-store ledger contains an unknown or extra tail.");
            if (pending is not null)
            {
                if (segmentIndex + 1 < MaximumSegments && files.FileExists(LedgerPath(directory, segmentIndex + 1)))
                {
                    currentSegment = segmentIndex;
                    currentLedgerLength = ledgerBytes.Length;
                    continue;
                }
                if (segmentIndex != LastExistingSegment(directory, files)) throw new InvalidDataException("Run-store recoverable tail is not final.");
            }

            currentSegment = segmentIndex;
            currentLedgerLength = ledgerBytes.Length;
        }

        if (pending is not null && recoveryCount >= MaximumRecoveryCount)
            throw new InvalidDataException("Run store exceeds the single bounded recovery continuation.");

        if (expectedSequence > SnowGlobeRunStore.MaximumLedgerRecords
            || expectedSequence != records.Count + participants.Count)
            throw new InvalidDataException("Run store exceeds the global bounded record limit.");
        SnowGlobeRunLedger ledger = new(identity, records.AsReadOnly(), participants.AsReadOnly());
        _ = SnowGlobePersistedRun.Reconstruct(ledger);
        return new RunStoreV4State(
            ledger, headerChecksum, chain, Convert.ToHexString(evidence.GetHashAndReset()).ToLowerInvariant(),
            currentSegment, currentLedgerLength, nextFrame, recoveryCount, pending);
    }

    internal static void WriteContinuation(string directory, IRunStoreFileSystem files, RunStoreV4State state)
    {
        RunStorePendingFrame pending = state.PendingFrame ?? throw new InvalidOperationException("No recoverable tail exists.");
        if (state.RecoveryCount >= MaximumRecoveryCount) throw new InvalidDataException("Run store exceeds the single bounded recovery continuation.");
        int segmentIndex = checked(pending.SegmentIndex + 1);
        if (segmentIndex >= MaximumSegments) throw new InvalidDataException("Run store exceeds the bounded segment count.");
        string ledgerPath = LedgerPath(directory, segmentIndex);
        string markerPath = MarkerPath(directory, segmentIndex);
        if (files.FileExists(ledgerPath) || files.FileExists(markerPath)) throw new InvalidDataException("Run-store continuation target already exists.");

        files.CreateFile(ledgerPath, ReadOnlySpan<byte>.Empty, RunStoreWriteKind.Ledger);
        RunStoreContinuationMarker unsigned = new(
            "continuation", MarkerSchema, segmentIndex, state.ChainChecksum,
            pending.SegmentIndex, pending.Prepare.FrameIndex, pending.Prepare.Checksum,
            pending.SourceLedgerLength, pending.SourceLedgerChecksum,
            pending.SourceMarkerLength, pending.SourceMarkerChecksum,
            pending.Disposition.ToString(), string.Empty);
        RunStoreContinuationMarker marker = unsigned with { Checksum = ContinuationChecksum(unsigned) };
        files.CreateFile(markerPath, SerializeLine(marker), RunStoreWriteKind.ContinuationMarker);
    }

    private static RunStorePendingFrame ResolvePending(
        int segmentIndex,
        RunStorePrepareMarker prepare,
        byte[] ledgerBytes,
        int ledgerOffset,
        byte[] commitTail,
        byte[] markerBytes,
        SnowGlobeRunIdentity identity,
        List<SnowGlobeLedgerRecord> committedRecords,
        List<SnowGlobeParticipantEvaluationRecord> committedParticipants,
        int expectedSequence)
    {
        if (prepare.FrameKind == RunStoreFrameKind.ParticipantEvaluation.ToString())
            throw new InvalidDataException("Uncommitted participant-command evidence is never repaired or ignored.");
        if (prepare.FrameKind != RunStoreFrameKind.ScheduledTick.ToString())
            throw new InvalidDataException("Run-store pending frame kind is unsupported.");
        int actualLength = ledgerBytes.Length - ledgerOffset;
        if (actualLength < 0 || actualLength > prepare.PayloadLength || actualLength > MaximumScheduledPayloadBytes)
            throw new InvalidDataException("Run-store pending payload exceeds its prepared bound.");
        byte[] payload = ledgerBytes[ledgerOffset..];

        RunStoreCommitMarker expectedUnsigned = new(
            "commit", MarkerSchema, segmentIndex, prepare.FrameIndex, prepare.PreviousCommitChecksum,
            prepare.Checksum, prepare.PayloadChecksum, checked(ledgerOffset + prepare.PayloadLength), string.Empty);
        RunStoreCommitMarker expectedCommit = expectedUnsigned with { Checksum = CommitChecksum(expectedUnsigned) };
        byte[] expectedCommitLine = SerializeLine(expectedCommit);
        if (commitTail.Length != 0 && !expectedCommitLine.AsSpan().StartsWith(commitTail))
            throw new InvalidDataException("Run-store uncertain commit marker is not a canonical prefix.");

        List<SnowGlobeLedgerRecord> scheduled = new();
        RunStoreRecoveryDisposition disposition;
        if (actualLength == prepare.PayloadLength)
        {
            if (!FixedEquals(Digest(payload), prepare.PayloadChecksum)) throw new InvalidDataException("Run-store pending payload integrity mismatch.");
            int sequence = expectedSequence;
            SnowGlobeWorld before = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, committedRecords.AsReadOnly(), committedParticipants.AsReadOnly())).World;
            List<SnowGlobeLedgerRecord> prospectiveRecords = committedRecords.ToList();
            List<SnowGlobeParticipantEvaluationRecord> prospectiveParticipants = committedParticipants.ToList();
            ReadCommittedPayload(payload, prepare, identity, SnowGlobeRunStore.CanonicalIdentityChecksum(identity), prospectiveRecords, prospectiveParticipants, ref sequence);
            if (prospectiveParticipants.Count != committedParticipants.Count) throw new InvalidDataException("Scheduled recovery contains participant evidence.");
            scheduled.AddRange(prospectiveRecords.Skip(committedRecords.Count));
            SnowGlobeWorld after = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, prospectiveRecords.AsReadOnly(), prospectiveParticipants.AsReadOnly())).World;
            if (after.Tick != before.Tick + 1) throw new InvalidDataException("Recoverable scheduled frame does not advance exactly one tick.");
            disposition = RunStoreRecoveryDisposition.RecoverCompleteScheduledTick;
        }
        else
        {
            if (actualLength != 0)
            {
                IReadOnlyDictionary<int, string> prefixes = ParsePayloadPrefixManifest(prepare.PayloadPrefixManifest, prepare.PayloadLength);
                if (!prefixes.TryGetValue(actualLength, out string? expectedPrefixChecksum)
                    || !FixedEquals(Digest(payload), expectedPrefixChecksum))
                    throw new InvalidDataException("Run-store pending payload is not a prepared durable prefix.");
            }
            ValidateScheduledPrefix(payload, prepare, identity, committedRecords, committedParticipants, expectedSequence);
            disposition = RunStoreRecoveryDisposition.AbandonIncompleteScheduledTick;
        }

        return new RunStorePendingFrame(
            segmentIndex, prepare, payload, commitTail, disposition, scheduled.AsReadOnly(),
            ledgerBytes.Length, Digest(ledgerBytes), markerBytes.Length, Digest(markerBytes));
    }

    private static void ApplyPending(RunStorePendingFrame pending, List<SnowGlobeLedgerRecord> records, ref int expectedSequence)
    {
        if (pending.Disposition != RunStoreRecoveryDisposition.RecoverCompleteScheduledTick) return;
        if (expectedSequence > SnowGlobeRunStore.MaximumLedgerRecords - pending.ScheduledRecords.Count)
            throw new InvalidDataException("Recovered scheduled frame exceeds the global bounded record limit.");
        foreach (SnowGlobeLedgerRecord record in pending.ScheduledRecords)
        {
            if (record.Sequence != expectedSequence++) throw new InvalidDataException("Recovered scheduled frame sequence is discontinuous.");
            records.Add(record);
        }
    }

    private static void ValidateScheduledPrefix(
        byte[] payload,
        RunStorePrepareMarker prepare,
        SnowGlobeRunIdentity identity,
        List<SnowGlobeLedgerRecord> committedRecords,
        List<SnowGlobeParticipantEvaluationRecord> committedParticipants,
        int expectedSequence)
    {
        List<byte[]> completeLines = CompleteLines(payload, out byte[] partialLine, "Pending scheduled payload");
        if (completeLines.Count >= prepare.EntryCount) throw new InvalidDataException("Incomplete scheduled payload contains an unknown or extra tail.");
        if (partialLine.Length != 0)
            throw new InvalidDataException("Pending scheduled payload contains an unauthenticated partial record.");
        List<SnowGlobeLedgerRecord> prefix = new();
        foreach (byte[] line in completeLines)
        {
            SnowGlobeRunStore.ParseAndValidateEntry(line, identity, SnowGlobeRunStore.CanonicalIdentityChecksum(identity), expectedSequence++, out SnowGlobeLedgerRecord? record, out SnowGlobeParticipantEvaluationRecord? participant);
            if (participant is not null || record is null) throw new InvalidDataException("Pending scheduled payload contains participant evidence.");
            prefix.Add(record);
        }
        ValidateScheduledRecordPrefix(identity, committedRecords, committedParticipants, prefix);
    }

    private static void ValidateScheduledRecordPrefix(
        SnowGlobeRunIdentity identity,
        List<SnowGlobeLedgerRecord> committedRecords,
        List<SnowGlobeParticipantEvaluationRecord> committedParticipants,
        List<SnowGlobeLedgerRecord> prefix)
    {
        SnowGlobeWorld world = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, committedRecords.AsReadOnly(), committedParticipants.AsReadOnly())).World;
        string[] agents = world.Agents.Select(agent => agent.AgentId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        int index = 0;
        foreach (string agent in agents)
        {
            if (index == prefix.Count) return;
            SnowGlobeLedgerRecord response = prefix[index++];
            if (response.Kind != SnowGlobeLedgerKind.Response || response.Tick != world.Tick || response.AgentId != agent) throw new InvalidDataException("Pending scheduled payload is not an ordinal tick prefix.");
            if (index == prefix.Count) return;
            SnowGlobeLedgerRecord proposal = prefix[index++];
            if (proposal.Kind != SnowGlobeLedgerKind.Proposal || !SameAction(response, proposal)) throw new InvalidDataException("Pending scheduled proposal diverges from its response.");
            if (index == prefix.Count) return;
            SnowGlobeLedgerRecord commit = prefix[index++];
            if (commit.Kind != SnowGlobeLedgerKind.Commit || !SameAction(proposal, commit) || commit.Accepted is null) throw new InvalidDataException("Pending scheduled commit diverges from its proposal.");
            if (commit.Accepted.Value)
            {
                if (index == prefix.Count) return;
                SnowGlobeLedgerRecord entry = prefix[index++];
                if (entry.Kind != SnowGlobeLedgerKind.Event || !SameAction(commit, entry)) throw new InvalidDataException("Pending scheduled event diverges from its commit.");
            }
        }
        if (index == prefix.Count) return;
        SnowGlobeLedgerRecord checkpoint = prefix[index++];
        if (checkpoint.Kind != SnowGlobeLedgerKind.Checkpoint || checkpoint.Tick != world.Tick + 1 || index != prefix.Count)
            throw new InvalidDataException("Pending scheduled payload extends beyond one tick.");
    }

    private static bool SameAction(SnowGlobeLedgerRecord left, SnowGlobeLedgerRecord right) =>
        left.Tick == right.Tick && left.AgentId == right.AgentId && left.Action == right.Action && left.Quantity == right.Quantity;

    private static void ReadCommittedPayload(
        ReadOnlySpan<byte> payload,
        RunStorePrepareMarker prepare,
        SnowGlobeRunIdentity identity,
        string headerChecksum,
        List<SnowGlobeLedgerRecord> records,
        List<SnowGlobeParticipantEvaluationRecord> participants,
        ref int expectedSequence)
    {
        if (expectedSequence < 0 || prepare.EntryCount < 1
            || expectedSequence > SnowGlobeRunStore.MaximumLedgerRecords - prepare.EntryCount)
            throw new InvalidDataException("Run-store frame exceeds the global bounded record limit.");
        int recordCountBefore = records.Count;
        int participantCountBefore = participants.Count;
        SnowGlobeWorld before = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, records.AsReadOnly(), participants.AsReadOnly())).World;
        if (payload.Length != prepare.PayloadLength || !FixedEquals(Digest(payload), prepare.PayloadChecksum))
            throw new InvalidDataException("Committed run-store payload integrity mismatch.");
        string expectedPrefixManifest = prepare.FrameKind == RunStoreFrameKind.ScheduledTick.ToString()
            ? BuildPayloadPrefixManifest(payload)
            : string.Empty;
        if (!string.Equals(prepare.PayloadPrefixManifest, expectedPrefixManifest, StringComparison.Ordinal))
            throw new InvalidDataException("Committed run-store payload prefix manifest mismatch.");
        List<byte[]> lines = CompleteLines(payload.ToArray(), out byte[] tail, "Committed run-store payload");
        if (tail.Length != 0 || lines.Count != prepare.EntryCount) throw new InvalidDataException("Committed run-store payload framing mismatch.");
        foreach (byte[] line in lines)
        {
            SnowGlobeRunStore.ParseAndValidateEntry(line, identity, headerChecksum, expectedSequence++, out SnowGlobeLedgerRecord? record, out SnowGlobeParticipantEvaluationRecord? participant);
            if (record is not null) records.Add(record);
            else if (participant is not null)
            {
                if (participants.Count >= SnowGlobeRunStore.MaximumParticipantEvaluations)
                    throw new InvalidDataException("Participant evaluation index exceeds its bounded capacity.");
                participants.Add(participant);
            }
        }
        if (prepare.FrameKind == RunStoreFrameKind.ParticipantEvaluation.ToString())
        {
            if (lines.Count != 1 || records.Count != recordCountBefore || participants.Count != participantCountBefore + 1 || participants[^1].Sequence != prepare.FirstSequence)
                throw new InvalidDataException("Participant frame does not contain exactly one participant evaluation.");
            SnowGlobeWorld after = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, records.AsReadOnly(), participants.AsReadOnly())).World;
            if (after.Tick != before.Tick) throw new InvalidDataException("Participant frame advanced the scheduled tick cursor.");
        }
        else if (prepare.FrameKind == RunStoreFrameKind.ScheduledTick.ToString())
        {
            if (participants.Count != participantCountBefore || records.Count != recordCountBefore + lines.Count
                || lines.Any(line => SnowGlobeRunStore.ReadEntryKind(line) == SnowGlobeLedgerKind.ParticipantEvaluation))
                throw new InvalidDataException("Scheduled frame contains participant evidence.");
            SnowGlobeWorld after = SnowGlobePersistedRun.Reconstruct(new SnowGlobeRunLedger(identity, records.AsReadOnly(), participants.AsReadOnly())).World;
            if (after.Tick != before.Tick + 1) throw new InvalidDataException("Committed scheduled frame does not advance exactly one tick.");
        }
        else throw new InvalidDataException("Run-store frame kind is unsupported.");
    }

    private static void ValidatePrepare(RunStorePrepareMarker marker, int segmentIndex, int frameIndex, string chain, int firstSequence, int agentCount)
    {
        if (marker.RecordType != "prepare" || marker.MarkerSchema != MarkerSchema || marker.SegmentIndex != segmentIndex || marker.FrameIndex != frameIndex
            || marker.FirstSequence != firstSequence || marker.EntryCount < 1 || marker.PayloadLength < 1
            || !FixedEquals(marker.PreviousCommitChecksum, chain) || !FixedEquals(marker.Checksum, PrepareChecksum(marker with { Checksum = string.Empty }))
            || !SnowGlobeRunStore.IsDigest(marker.PayloadChecksum))
            throw new InvalidDataException("Run-store prepare marker is invalid or breaks the commit chain.");
        if (marker.FrameKind == RunStoreFrameKind.ParticipantEvaluation.ToString())
        {
            if (marker.EntryCount != 1 || marker.PayloadLength > SnowGlobeRunStore.MaximumLedgerRecordBytes + 1
                || marker.PayloadPrefixManifest.Length != 0) throw new InvalidDataException("Participant frame exceeds its bounded shape.");
        }
        else if (marker.FrameKind == RunStoreFrameKind.ScheduledTick.ToString())
        {
            if (marker.EntryCount > checked(agentCount * 4 + 1) || marker.PayloadLength > MaximumScheduledPayloadBytes) throw new InvalidDataException("Scheduled frame exceeds its bounded shape.");
            _ = ParsePayloadPrefixManifest(marker.PayloadPrefixManifest, marker.PayloadLength);
        }
        else throw new InvalidDataException("Run-store prepare marker frame kind is unknown.");
        if (firstSequence < 0 || marker.EntryCount > SnowGlobeRunStore.MaximumLedgerRecords - firstSequence)
            throw new InvalidDataException("Run-store prepare marker exceeds the global bounded record limit.");
    }

    private static void ValidateCommit(RunStoreCommitMarker marker, RunStorePrepareMarker prepare, string chain, int ledgerOffset, int ledgerLength)
    {
        if (marker.RecordType != "commit" || marker.MarkerSchema != MarkerSchema || marker.SegmentIndex != prepare.SegmentIndex || marker.FrameIndex != prepare.FrameIndex
            || !FixedEquals(marker.PreviousCommitChecksum, chain) || !FixedEquals(marker.PrepareChecksum, prepare.Checksum)
            || !FixedEquals(marker.PayloadChecksum, prepare.PayloadChecksum) || marker.LedgerEndOffset != checked(ledgerOffset + prepare.PayloadLength)
            || marker.LedgerEndOffset > ledgerLength || !FixedEquals(marker.Checksum, CommitChecksum(marker with { Checksum = string.Empty })))
            throw new InvalidDataException("Run-store commit marker is invalid or breaks the commit chain.");
    }

    private static void ValidateContinuation(
        RunStoreContinuationMarker marker,
        int segmentIndex,
        string chain,
        RunStorePendingFrame pending,
        int continuationLedgerLength,
        int continuationMarkerLength)
    {
        if (marker.RecordType != "continuation" || marker.MarkerSchema != MarkerSchema || marker.SegmentIndex != segmentIndex
            || continuationLedgerLength < 0 || continuationMarkerLength < 1
            || !FixedEquals(marker.PreviousCommitChecksum, chain) || marker.SourceSegmentIndex != pending.SegmentIndex
            || marker.SourceFrameIndex != pending.Prepare.FrameIndex || !FixedEquals(marker.SourcePrepareChecksum, pending.Prepare.Checksum)
            || marker.SourceLedgerLength != pending.SourceLedgerLength || !FixedEquals(marker.SourceLedgerChecksum, pending.SourceLedgerChecksum)
            || marker.SourceMarkerLength != pending.SourceMarkerLength || !FixedEquals(marker.SourceMarkerChecksum, pending.SourceMarkerChecksum)
            || marker.Disposition != pending.Disposition.ToString()
            || !FixedEquals(marker.Checksum, ContinuationChecksum(marker with { Checksum = string.Empty })))
            throw new InvalidDataException("Run-store continuation marker is invalid or forked.");
    }

    private static RunStorePrepareMarker DeserializePrepare(byte[] line) => DeserializeStrict<RunStorePrepareMarker>(line, PrepareProperties, "Run-store prepare marker");
    private static RunStoreCommitMarker DeserializeCommit(byte[] line) => DeserializeStrict<RunStoreCommitMarker>(line, CommitProperties, "Run-store commit marker");
    private static RunStoreContinuationMarker DeserializeContinuation(byte[] line) => DeserializeStrict<RunStoreContinuationMarker>(line, ContinuationProperties, "Run-store continuation marker");

    private static T DeserializeStrict<T>(byte[] line, HashSet<string> properties, string description)
    {
        if (line.Length == 0 || line.Length > MaximumMarkerBytes || line.Contains((byte)'\r')) throw new InvalidDataException($"{description} exceeds its bounded framing.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(line, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{description} must be an object.");
            HashSet<string> found = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!found.Add(property.Name) || !properties.Contains(property.Name) || property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                    throw new InvalidDataException($"{description} contains an unknown, duplicate, or nested property.");
            }
            if (!found.SetEquals(properties)) throw new InvalidDataException($"{description} is incomplete.");
            return JsonSerializer.Deserialize<T>(line, JsonOptions) ?? throw new InvalidDataException($"{description} is missing.");
        }
        catch (JsonException exception) { throw new InvalidDataException($"{description} is malformed.", exception); }
    }

    private static List<byte[]> CompleteLines(byte[] bytes, out byte[] tail, string description)
    {
        List<byte[]> lines = new();
        int start = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == '\r') throw new InvalidDataException($"{description} contains carriage-return framing.");
            if (bytes[index] != '\n') continue;
            if (index == start) throw new InvalidDataException($"{description} contains a blank line.");
            int count = index - start;
            if (count > Math.Max(MaximumMarkerBytes, SnowGlobeRunStore.MaximumLedgerRecordBytes)) throw new InvalidDataException($"{description} line exceeds its bounded byte limit.");
            lines.Add(bytes.AsSpan(start, count).ToArray());
            start = index + 1;
        }
        tail = bytes[start..];
        return lines;
    }

    internal static int[] ScheduledPayloadChunkEnds(ReadOnlySpan<byte> payload)
    {
        List<int> recordEnds = new();
        for (int index = 0; index < payload.Length; index++)
            if (payload[index] == (byte)'\n') recordEnds.Add(index + 1);
        if (recordEnds.Count == 0 || recordEnds[^1] != payload.Length)
            throw new InvalidDataException("Scheduled payload is not complete JSONL.");

        const int maximumChunks = 4;
        int chunkCount = Math.Min(maximumChunks, recordEnds.Count);
        int[] ends = new int[chunkCount];
        for (int chunk = 1; chunk <= chunkCount; chunk++)
        {
            int recordCount = checked((recordEnds.Count * chunk + chunkCount - 1) / chunkCount);
            ends[chunk - 1] = recordEnds[recordCount - 1];
        }
        return ends;
    }

    private static string BuildPayloadPrefixManifest(ReadOnlySpan<byte> payload)
    {
        int[] ends = ScheduledPayloadChunkEnds(payload);
        StringBuilder manifest = new();
        for (int index = 0; index < ends.Length - 1; index++)
        {
            if (index != 0) manifest.Append(';');
            int end = ends[index];
            manifest.Append(end).Append(':').Append(Digest(payload[..end]));
        }
        return manifest.ToString();
    }

    private static IReadOnlyDictionary<int, string> ParsePayloadPrefixManifest(string manifest, int payloadLength)
    {
        Dictionary<int, string> prefixes = new();
        if (manifest.Length == 0) return prefixes;
        string[] entries = manifest.Split(';', StringSplitOptions.None);
        if (entries.Length > 3) throw new InvalidDataException("Scheduled payload prefix manifest exceeds its bounded shape.");
        int previousLength = 0;
        foreach (string entry in entries)
        {
            string[] fields = entry.Split(':', StringSplitOptions.None);
            if (fields.Length != 2 || !int.TryParse(fields[0], out int length)
                || length <= previousLength || length >= payloadLength || !SnowGlobeRunStore.IsDigest(fields[1]))
                throw new InvalidDataException("Scheduled payload prefix manifest is invalid.");
            prefixes.Add(length, fields[1]);
            previousLength = length;
        }
        return prefixes;
    }

    private static void ValidateLayout(string directory, IRunStoreFileSystem files)
    {
        HashSet<string> allowed = new(StringComparer.Ordinal) { "run.json", ".writer.lock" };
        for (int index = 0; index < MaximumSegments; index++)
        {
            allowed.Add(Path.GetFileName(LedgerPath(directory, index)));
            allowed.Add(Path.GetFileName(MarkerPath(directory, index)));
        }
        string[] unknown = files.EnumerateEntryNames(directory).Where(name => !allowed.Contains(name)).ToArray();
        if (unknown.Length != 0) throw new InvalidDataException("Run store contains unknown or extra artifacts.");
        if (!files.FileExists(LedgerPath(directory, 0)) || !files.FileExists(MarkerPath(directory, 0)))
            throw new InvalidDataException("Run-store v4 artifacts are incomplete.");
        bool hasSecondLedger = files.FileExists(LedgerPath(directory, 1));
        bool hasSecondMarker = files.FileExists(MarkerPath(directory, 1));
        if (hasSecondLedger != hasSecondMarker) throw new InvalidDataException("Run-store continuation artifacts are incomplete.");
    }

    private static int LastExistingSegment(string directory, IRunStoreFileSystem files) =>
        files.FileExists(LedgerPath(directory, 1)) ? 1 : 0;

    private static void AppendEvidence(IncrementalHash evidence, int segmentIndex, byte[] ledger, byte[] markers)
    {
        evidence.AppendData(Utf8.GetBytes($"{segmentIndex}|{ledger.Length}|{markers.Length}|"));
        evidence.AppendData(ledger);
        evidence.AppendData(markers);
    }

    private static byte[] SerializeLine<T>(T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (json.Length == 0 || json.Length > MaximumMarkerBytes) throw new InvalidDataException("Run-store marker exceeds its bounded byte limit.");
        byte[] line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    private static string PrepareChecksum(RunStorePrepareMarker marker) => Digest(Utf8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.FrameIndex}|{marker.PreviousCommitChecksum}|{marker.FrameKind}|{marker.FirstSequence}|{marker.EntryCount}|{marker.PayloadLength}|{marker.PayloadChecksum}|{marker.PayloadPrefixManifest}"));
    private static string CommitChecksum(RunStoreCommitMarker marker) => Digest(Utf8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.FrameIndex}|{marker.PreviousCommitChecksum}|{marker.PrepareChecksum}|{marker.PayloadChecksum}|{marker.LedgerEndOffset}"));
    private static string ContinuationChecksum(RunStoreContinuationMarker marker) => Digest(Utf8.GetBytes(
        $"{marker.RecordType}|{marker.MarkerSchema}|{marker.SegmentIndex}|{marker.PreviousCommitChecksum}|{marker.SourceSegmentIndex}|{marker.SourceFrameIndex}|{marker.SourcePrepareChecksum}|{marker.SourceLedgerLength}|{marker.SourceLedgerChecksum}|{marker.SourceMarkerLength}|{marker.SourceMarkerChecksum}|{marker.Disposition}"));
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Utf8.GetBytes(left), Utf8.GetBytes(right));

    internal static string LedgerPath(string directory, int segmentIndex) => segmentIndex == 0
        ? Path.Combine(directory, "ledger.jsonl")
        : Path.Combine(directory, $"ledger.{segmentIndex:D4}.jsonl");
    internal static string MarkerPath(string directory, int segmentIndex) => segmentIndex == 0
        ? Path.Combine(directory, "commits.jsonl")
        : Path.Combine(directory, $"commits.{segmentIndex:D4}.jsonl");
}
