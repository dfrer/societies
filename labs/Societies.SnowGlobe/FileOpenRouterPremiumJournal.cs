using System.Buffers;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

public interface IFileOpenRouterPremiumJournalAppendFault
{
    void BeforeWrite(int sequence);
    void AfterWriteBeforeFlush(int sequence);
}

public sealed record OpenRouterPremiumDurableRestartEvidence(
    string SchemaVersion,
    string JournalHeaderChecksumSha256,
    string SnapshotDigestSha256,
    int RecordCount,
    string FinalRecordChecksumSha256,
    bool FlushToDiskRequested,
    bool RestartVerified,
    string EvidenceDigestSha256)
{
    public const string CurrentSchemaVersion = "snow_globe_openrouter_premium_durable_restart_evidence/v1";
}

internal sealed record OpenRouterPremiumDurablePreflightConsumption(
    string SchemaVersion,
    string AuthorizationNonceDigestSha256,
    string BundleDigestSha256,
    string TrustEvidenceDigestSha256,
    string JournalHeaderChecksumSha256,
    string RecordChecksumSha256,
    bool FlushToDiskRequested,
    string EvidenceDigestSha256)
{
    public const string CurrentSchemaVersion = "snow_globe_openrouter_premium_preflight_consumption/v1";
}

/// <summary>
/// Strict, raw-free, single-writer JSON/JSONL journal Adapter. It never reads a credential and has
/// no provider/network authority. Each transition is validated against a detached replay before a
/// canonical record is appended and synchronously flushed to disk.
/// </summary>
public sealed class FileOpenRouterPremiumJournal : IOpenRouterPremiumJournal, IDisposable
{
    public const string HeaderFileName = "header.json";
    public const string RecordsFileName = "records.jsonl";
    public const string WriterLeaseFileName = "writer.lock";
    public const int MaximumTotalBytes = 256 * 1024;
    private const int MaximumHeaderBytes = 4 * 1024;
    private const int MaximumRecordBytes = 8 * 1024;
    private const int MaximumRecords = 48;
    private static readonly byte[] PublicationFreezeMarker = Encoding.ASCII.GetBytes(
        "{\"schema_version\":\"snow_globe_openrouter_premium_journal_writer_freeze/v1\",\"append_closed\":true}");

    private readonly object _gate = new();
    private readonly string _directoryPath;
    private readonly SafeFileHandle _directoryHandle;
    private readonly FileStream _writerLease;
    private readonly FileStream _headerStream;
    private readonly FileStream _recordsStream;
    private readonly IFileOpenRouterPremiumJournalAppendFault? _fault;
    private readonly List<JournalCommand> _commands;
    private InMemoryOpenRouterPremiumJournal _inner;
    private string _finalRecordChecksumSha256;
    private bool _poisoned;
    private bool _disposed;
    private bool _callerDisposed;
    private readonly bool _publicationControlled;

    private FileOpenRouterPremiumJournal(
        string directory,
        OpenRouterPremiumJournalHeader header,
        InMemoryOpenRouterPremiumJournal inner,
        List<JournalCommand> commands,
        string finalRecordChecksumSha256,
        SafeFileHandle directoryHandle,
        FileStream writerLease,
        FileStream headerStream,
        FileStream recordsStream,
        IFileOpenRouterPremiumJournalAppendFault? fault,
        bool restartVerified,
        bool publicationControlled)
    {
        _directoryPath = Path.GetFullPath(directory);
        Header = header with { };
        _inner = inner;
        _commands = commands;
        _finalRecordChecksumSha256 = finalRecordChecksumSha256;
        _directoryHandle = directoryHandle;
        _writerLease = writerLease;
        _headerStream = headerStream;
        _recordsStream = recordsStream;
        _fault = fault;
        _publicationControlled = publicationControlled;
        RestartEvidence = CreateRestartEvidence(restartVerified);
    }

    public string Identity => Header.JournalIdentity;
    public bool ProvidesDurableFlush => true;
    public OpenRouterPremiumJournalHeader Header { get; }
    public OpenRouterPremiumDurableRestartEvidence RestartEvidence { get; private set; }
    public bool IsPoisoned { get { lock (_gate) return _poisoned; } }
    internal string RecordsFileDigestSha256
    {
        get
        {
            lock (_gate)
            {
                ThrowIfUnavailable();
                byte[] bytes = ReadBounded(_recordsStream, MaximumTotalBytes, allowEmpty: true);
                try { return OpenRouterPremiumCanonical.Digest(bytes); }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                    _recordsStream.Seek(0, SeekOrigin.End);
                }
            }
        }
    }

    public static FileOpenRouterPremiumJournal CreateNew(
        string directory,
        OpenRouterPremiumJournalHeader header,
        IFileOpenRouterPremiumJournalAppendFault? fault = null) =>
        CreateNewCore(directory, header, fault, publicationControlled: false);

    internal static FileOpenRouterPremiumJournal CreateNewForPublication(
        string directory,
        OpenRouterPremiumJournalHeader header) =>
        CreateNewCore(directory, header, fault: null, publicationControlled: true);

    private static FileOpenRouterPremiumJournal CreateNewCore(
        string directory,
        OpenRouterPremiumJournalHeader header,
        IFileOpenRouterPremiumJournalAppendFault? fault,
        bool publicationControlled)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(header);
        header.Validate();
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        string root = Path.GetFullPath(directory);
        if (publicationControlled)
        {
            if (!CreateDirectoryW(root, IntPtr.Zero))
            {
                _ = new Win32Exception(Marshal.GetLastWin32Error());
                throw new IOException("OpenRouter premium publication journal directory creation failed.");
            }
        }
        else Directory.CreateDirectory(root);
        byte[] headerBytes = FileOpenRouterPremiumJournalCodec.WriteHeader(header);
        if (headerBytes.Length is < 1 or > MaximumHeaderBytes)
            throw new InvalidDataException("OpenRouter premium journal header is oversized.");
        SafeFileHandle? directoryHandle = null;
        FileStream? lease = null;
        FileStream? headerLock = null;
        FileStream? records = null;
        try
        {
            directoryHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(root);
            if (!publicationControlled && Directory.EnumerateFileSystemEntries(root).Any())
                throw new IOException("OpenRouter premium journal directory must be empty.");
            lease = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, WriterLeaseFileName),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.WriteThrough);
            headerLock = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, HeaderFileName),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            headerLock.Write(headerBytes); headerLock.Flush(flushToDisk: true); headerLock.Position = 0;
            records = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, RecordsFileName),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            records.Flush(flushToDisk: true);
            FileOpenRouterPremiumIdentity.VerifyStableSet(root, directoryHandle, lease, headerLock, records);
            return new FileOpenRouterPremiumJournal(root, header, new(header), [], FileOpenRouterPremiumJournalCodec.ZeroDigest,
                directoryHandle, lease, headerLock, records, fault, restartVerified: false,
                publicationControlled: publicationControlled);
        }
        catch
        {
            records?.Dispose();
            headerLock?.Dispose();
            lease?.Dispose();
            directoryHandle?.Dispose();
            throw;
        }
    }

    public static FileOpenRouterPremiumJournal OpenForAppend(
        string directory,
        IFileOpenRouterPremiumJournalAppendFault? fault = null)
        => OpenExisting(directory, fault, claimedGrant: null, finalReadback: false,
            publicationControlled: false);

    internal static FileOpenRouterPremiumJournal OpenForClaimedExecution(
        OpenRouterPremiumStateGenerationStore.ClaimedExecutionJournalGrant claimedGrant,
        bool finalReadback = false) =>
        OpenExisting(claimedGrant.JournalDirectory, fault: null, claimedGrant, finalReadback,
            publicationControlled: false);

    private static FileOpenRouterPremiumJournal OpenExisting(
        string directory,
        IFileOpenRouterPremiumJournalAppendFault? fault,
        OpenRouterPremiumStateGenerationStore.ClaimedExecutionJournalGrant? claimedGrant,
        bool finalReadback,
        bool publicationControlled)
    {
        string root = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        claimedGrant?.BeginOpen(root, finalReadback);
        SafeFileHandle? directoryHandle = null;
        FileStream? lease = null;
        FileStream? headerLock = null;
        FileStream? records = null;
        try
        {
            directoryHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(root);
            if (claimedGrant is not null)
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(root,
                    claimedGrant.JournalDirectoryIdentity, directoryHandle);
            lease = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, WriterLeaseFileName),
                FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.WriteThrough);
            if (claimedGrant is null && lease.Length != 0)
                throw new InvalidDataException("OpenRouter premium journal append access was closed for publication.");
            if (claimedGrant is not null)
            {
                RequireIdentity(lease, claimedGrant.WriterLeaseFileIdentity);
                byte[] marker = ReadBounded(lease, PublicationFreezeMarker.Length, allowEmpty: false);
                try
                {
                    if (!marker.AsSpan().SequenceEqual(PublicationFreezeMarker)
                        || !FixedDigestEquals(OpenRouterPremiumCanonical.Digest(marker),
                            claimedGrant.WriterLeaseDigestSha256))
                        throw new InvalidDataException("OpenRouter premium journal publication freeze marker is invalid.");
                }
                finally { CryptographicOperations.ZeroMemory(marker); }
            }
            headerLock = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, HeaderFileName),
                FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
            records = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(root, RecordsFileName),
                FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            if (claimedGrant is not null)
            {
                RequireIdentity(headerLock, claimedGrant.HeaderFileIdentity);
                RequireIdentity(records, claimedGrant.RecordsFileIdentity);
                byte[] headerBytes = ReadBounded(headerLock, MaximumHeaderBytes, allowEmpty: false);
                byte[] recordsBytes = ReadBounded(records, MaximumTotalBytes, allowEmpty: true);
                try
                {
                    if (!FixedDigestEquals(OpenRouterPremiumCanonical.Digest(headerBytes),
                            claimedGrant.HeaderDigestSha256)
                        || (!finalReadback && !FixedDigestEquals(OpenRouterPremiumCanonical.Digest(recordsBytes),
                            claimedGrant.InitialRecordsDigestSha256)))
                        throw new InvalidDataException("OpenRouter premium claimed journal binding is invalid.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(headerBytes);
                    CryptographicOperations.ZeroMemory(recordsBytes);
                }
            }
            FileOpenRouterPremiumIdentity.VerifyStableSet(root, directoryHandle, lease, headerLock, records);
            LoadedJournal loaded = FileOpenRouterPremiumJournalCodec.Load(root, headerLock, records,
                rejectUnexpectedEntries: claimedGrant is null && !publicationControlled);
            FileOpenRouterPremiumIdentity.VerifyStableSet(root, directoryHandle, lease, headerLock, records);
            records.Seek(0, SeekOrigin.End);
            return new FileOpenRouterPremiumJournal(root, loaded.Header, loaded.Inner, loaded.Commands,
                loaded.FinalRecordChecksumSha256, directoryHandle, lease, headerLock, records, fault,
                restartVerified: true, publicationControlled: publicationControlled);
        }
        catch
        {
            records?.Dispose();
            headerLock?.Dispose();
            lease?.Dispose();
            directoryHandle?.Dispose();
            throw;
        }
    }

    internal FileOpenRouterPremiumJournal RestartForPublication()
    {
        string path;
        lock (_gate)
        {
            if (!_publicationControlled || _disposed)
                throw new ObjectDisposedException(nameof(FileOpenRouterPremiumJournal));
            path = _directoryPath;
            DisposeCore();
        }
        return OpenExisting(path, fault: null, claimedGrant: null, finalReadback: false,
            publicationControlled: true);
    }

    private static void RequireIdentity(FileStream stream, FileOpenRouterPremiumFileIdentity expected)
    {
        if (FileOpenRouterPremiumIdentity.CaptureFileIdentity(stream) != expected)
            throw new InvalidDataException("OpenRouter premium claimed journal identity changed.");
    }

    private static byte[] ReadBounded(FileStream stream, int maximum, bool allowEmpty)
    {
        if (stream.Length > maximum || (!allowEmpty && stream.Length < 1))
            throw new InvalidDataException("OpenRouter premium claimed journal file size is invalid.");
        stream.Position = 0;
        byte[] bytes = new byte[checked((int)stream.Length)];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        stream.Position = 0;
        return bytes;
    }

    private static bool FixedDigestEquals(string left, string right)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(left) || !OpenRouterPremiumCanonical.IsDigest(right)) return false;
        byte[] first = Convert.FromHexString(left);
        byte[] second = Convert.FromHexString(right);
        try { return CryptographicOperations.FixedTimeEquals(first, second); }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string pathName, IntPtr securityAttributes);

    public long Admit(int slotIndex, string scenarioId, string promptDigestSha256, string requestDigestSha256, long reservedMicrousd) =>
        Append(new AdmitCommand(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256, reservedMicrousd));

    public long CompleteBeforeDispatch(OpenRouterPremiumSlotReceipt receipt, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Append(new CompleteBeforeDispatchCommand(receipt.Detached(), expectedVersion));
    }

    public long MarkDispatchUnknown(int slotIndex, string requestDigestSha256, long expectedVersion) =>
        Append(new MarkDispatchUnknownCommand(slotIndex, requestDigestSha256, expectedVersion));

    public long Complete(OpenRouterPremiumSlotReceipt receipt, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Append(new CompleteCommand(receipt.Detached(), expectedVersion));
    }

    internal OpenRouterPremiumDurablePreflightConsumption ConsumeEligiblePreflightOnce(
        string authorizationNonceDigestSha256,
        string bundleDigestSha256,
        string trustEvidenceDigestSha256)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(authorizationNonceDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(bundleDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(trustEvidenceDigestSha256))
            throw new OpenRouterPremiumEvidenceException("preflight_consumption_binding_invalid");
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (_commands.OfType<ConsumeEligiblePreflightCommand>().Any())
                throw new OpenRouterPremiumEvidenceException("preflight_journal_consumed");
            if (_commands.Count != 0 || _inner.Snapshot().Slots.Count != 0)
                throw new OpenRouterPremiumEvidenceException("preflight_journal_not_empty");
            ConsumeEligiblePreflightCommand command = new(authorizationNonceDigestSha256, bundleDigestSha256, trustEvidenceDigestSha256);
            _ = Append(command);
            string payload = string.Join('|', OpenRouterPremiumDurablePreflightConsumption.CurrentSchemaVersion,
                authorizationNonceDigestSha256, bundleDigestSha256, trustEvidenceDigestSha256,
                Header.HeaderChecksumSha256, _finalRecordChecksumSha256, "flush_to_disk_requested=true");
            return new(OpenRouterPremiumDurablePreflightConsumption.CurrentSchemaVersion, authorizationNonceDigestSha256,
                bundleDigestSha256, trustEvidenceDigestSha256, Header.HeaderChecksumSha256, _finalRecordChecksumSha256,
                true, OpenRouterPremiumCanonical.Digest(payload));
        }
    }

    public OpenRouterPremiumJournalSnapshot Snapshot()
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            return _inner.Snapshot();
        }
    }

    internal void FreezeForPublication()
    {
        lock (_gate)
        {
            if (!_publicationControlled || _disposed)
                throw new ObjectDisposedException(nameof(FileOpenRouterPremiumJournal));
            FileOpenRouterPremiumIdentity.VerifyStableSet(
                _directoryPath, _directoryHandle, _writerLease, _headerStream, _recordsStream);
            if (_writerLease.Length != 0)
                throw new InvalidDataException("OpenRouter premium journal publication freeze marker already exists.");
            _writerLease.Position = 0;
            _writerLease.Write(PublicationFreezeMarker);
            _writerLease.Flush(flushToDisk: true);
            _writerLease.Position = 0;
            FileOpenRouterPremiumIdentity.VerifyStableSet(
                _directoryPath, _directoryHandle, _writerLease, _headerStream, _recordsStream);
            DisposeCore();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed || _callerDisposed) return;
            _callerDisposed = true;
            if (_publicationControlled) return;
            DisposeCore();
        }
    }

    internal void AbortPublication()
    {
        lock (_gate)
        {
            if (_disposed) return;
            DisposeCore();
        }
    }

    private void DisposeCore()
    {
        _disposed = true;
        try { _recordsStream.Dispose(); }
        finally
        {
            try { _headerStream.Dispose(); }
            finally
            {
                try { _writerLease.Dispose(); }
                finally
                {
                    try { _directoryHandle.Dispose(); }
                    finally { _inner = new(Header); }
                }
            }
        }
    }

    private long Append(JournalCommand command)
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (_commands.Count >= MaximumRecords)
                throw new OpenRouterPremiumEvidenceException("journal_record_capacity_exhausted");
            InMemoryOpenRouterPremiumJournal candidate = Replay(Header, _commands);
            long version = command.Apply(candidate);
            int sequence = _commands.Count + 1;
            byte[] record = FileOpenRouterPremiumJournalCodec.WriteRecord(
                sequence, _finalRecordChecksumSha256, Header.HeaderChecksumSha256, command, version);
            if (record.Length - 1 > MaximumRecordBytes)
                throw new OpenRouterPremiumEvidenceException("journal_record_oversized");
            if (checked(_recordsStream.Length + record.Length) > MaximumTotalBytes)
                throw new OpenRouterPremiumEvidenceException("journal_total_capacity_exhausted");
            string checksum = FileOpenRouterPremiumJournalCodec.RecordChecksum(record.AsSpan(0, record.Length - 1));
            try
            {
                _fault?.BeforeWrite(sequence);
                _recordsStream.Write(record);
                _fault?.AfterWriteBeforeFlush(sequence);
                _recordsStream.Flush(flushToDisk: true);
                _inner = candidate;
                _commands.Add(command.Detached());
                _finalRecordChecksumSha256 = checksum;
                RestartEvidence = CreateRestartEvidence(restartVerified: false);
                return version;
            }
            catch
            {
                _poisoned = true;
                throw;
            }
        }
    }

    private OpenRouterPremiumDurableRestartEvidence CreateRestartEvidence(bool restartVerified)
    {
        OpenRouterPremiumJournalSnapshot snapshot = _inner.Snapshot();
        string snapshotDigest = SnapshotDigest(snapshot);
        string payload = string.Join('|', OpenRouterPremiumDurableRestartEvidence.CurrentSchemaVersion,
            Header.HeaderChecksumSha256, snapshotDigest, _commands.Count, _finalRecordChecksumSha256,
            "flush_to_disk_requested=true", $"restart_verified={restartVerified.ToString().ToLowerInvariant()}");
        return new(OpenRouterPremiumDurableRestartEvidence.CurrentSchemaVersion, Header.HeaderChecksumSha256,
            snapshotDigest, _commands.Count, _finalRecordChecksumSha256, true, restartVerified,
            OpenRouterPremiumCanonical.Digest(payload));
    }

    internal static string SnapshotDigest(OpenRouterPremiumJournalSnapshot snapshot)
    {
        StringBuilder builder = new();
        builder.Append(snapshot.Header.HeaderChecksumSha256).Append('|')
            .Append(snapshot.ReservedExposureMicrousd).Append('|').Append(snapshot.SettledMicrousd);
        foreach (OpenRouterPremiumJournalSlotSnapshot slot in snapshot.Slots)
        {
            builder.Append('|').Append(slot.SlotIndex).Append('|').Append(slot.ScenarioId).Append('|')
                .Append(slot.PromptDigestSha256).Append('|').Append(slot.RequestDigestSha256).Append('|')
                .Append(slot.Version).Append('|').Append(slot.ReservedMicrousd).Append('|')
                .Append(slot.SubmissionState).Append('|').Append(slot.ChargeState);
            if (slot.Receipt is not null) builder.Append('|').Append(FileOpenRouterPremiumJournalCodec.ReceiptDescriptor(slot.Receipt));
        }
        return OpenRouterPremiumCanonical.Digest(builder.ToString());
    }

    internal static InMemoryOpenRouterPremiumJournal Replay(OpenRouterPremiumJournalHeader header, IEnumerable<JournalCommand> commands)
    {
        InMemoryOpenRouterPremiumJournal candidate = new(header);
        foreach (JournalCommand existing in commands) _ = existing.Apply(candidate);
        return candidate;
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed || _callerDisposed) throw new ObjectDisposedException(nameof(FileOpenRouterPremiumJournal));
        if (_poisoned) throw new InvalidOperationException("OpenRouter premium journal writer is poisoned after an uncertain append/flush outcome.");
        FileOpenRouterPremiumIdentity.VerifyStableSet(
            _directoryPath, _directoryHandle, _writerLease, _headerStream, _recordsStream);
    }
}

internal static class FileOpenRouterPremiumIdentity
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint CreateNew = 1;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;

    internal static void RequireSupportedPlatform()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "The durable OpenRouter premium journal requires Windows handle-identity enforcement.");
    }

    internal static SafeFileHandle OpenDirectoryPinned(string path)
    {
        SafeFileHandle handle = CreateFileW(path, FileReadAttributes,
            FileShare.Read | FileShare.Write, IntPtr.Zero, OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) throw LastWin32("open journal directory");
        try
        {
            FileIdentity identity = Inspect(handle, expectDirectory: true);
            if ((identity.Attributes & FileAttributeReparsePoint) != 0)
                throw new InvalidDataException("OpenRouter premium journal directory must not be a reparse point.");
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    // Initialization code that will create a child beneath this directory needs a
    // stronger lease than the ordinary identity pin. Denying write sharing prevents
    // another same-user handle from acquiring GENERIC_WRITE for
    // FSCTL_SET_REPARSE_POINT while the verified parent is being used.
    internal static SafeFileHandle OpenDirectoryMutationLease(string path)
    {
        SafeFileHandle handle = CreateFileW(path, GenericRead,
            FileShare.Read, IntPtr.Zero, OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) throw LastWin32("open journal directory mutation lease");
        try
        {
            FileIdentity identity = Inspect(handle, expectDirectory: true);
            if ((identity.Attributes & FileAttributeReparsePoint) != 0)
                throw new InvalidDataException("OpenRouter premium journal directory must not be a reparse point.");
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static FileOpenRouterPremiumDirectoryIdentity CaptureDirectoryIdentity(string path)
    {
        using SafeFileHandle handle = OpenDirectoryPinned(path);
        FileIdentity identity = Inspect(handle, expectDirectory: true);
        return new(identity.VolumeSerialNumber, identity.FileIndexHigh, identity.FileIndexLow);
    }

    internal static FileOpenRouterPremiumFileIdentity CaptureSingleFileIdentity(string root, string fileName)
    {
        RequireSupportedPlatform();
        if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName))
            throw new InvalidDataException("OpenRouter premium artifact name is invalid.");
        using SafeFileHandle directory = OpenDirectoryPinned(root);
        using FileStream stream = OpenFileNoFollow(
            Path.Combine(root, fileName), FileMode.Open, FileAccess.Read,
            FileShare.Read, 1, FileOptions.SequentialScan);
        VerifyStableSingleFile(root, directory, fileName, stream);
        FileIdentity identity = InspectRegularSingleLink(stream.SafeFileHandle);
        return new(identity.VolumeSerialNumber, identity.FileIndexHigh, identity.FileIndexLow);
    }

    internal static FileOpenRouterPremiumFileIdentity CaptureFileIdentity(FileStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        FileIdentity identity = InspectRegularSingleLink(stream.SafeFileHandle);
        return new(identity.VolumeSerialNumber, identity.FileIndexHigh, identity.FileIndexLow);
    }

    internal static void VerifySingleFileIdentity(
        string root,
        string fileName,
        FileOpenRouterPremiumFileIdentity expected)
    {
        FileOpenRouterPremiumFileIdentity actual = CaptureSingleFileIdentity(root, fileName);
        if (actual != expected)
            throw new InvalidDataException("OpenRouter premium artifact file identity changed.");
    }

    internal static string GetCanonicalDirectoryPath(string path)
    {
        using SafeFileHandle handle = OpenDirectoryPinned(path);
        char[] buffer = new char[32_768];
        uint length = GetFinalPathNameByHandleW(handle, buffer, checked((uint)buffer.Length), 0);
        if (length == 0 || length >= buffer.Length)
            throw LastWin32("resolve journal directory path");
        string value = new(buffer, 0, checked((int)length));
        const string extendedPrefix = @"\\?\";
        if (!value.StartsWith(extendedPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("OpenRouter premium directory canonical path is invalid.");
        return value[extendedPrefix.Length..];
    }

    internal static void VerifyStableDirectory(string path, FileOpenRouterPremiumDirectoryIdentity expected)
    {
        using SafeFileHandle handle = OpenDirectoryPinned(path);
        VerifyStableDirectory(path, expected, handle);
    }

    internal static void VerifyStableDirectory(string path, FileOpenRouterPremiumDirectoryIdentity expected,
        SafeFileHandle pinnedHandle)
    {
        FileIdentity pinned = Inspect(pinnedHandle, expectDirectory: true);
        if (expected.VolumeSerialNumber != pinned.VolumeSerialNumber
            || expected.FileIndexHigh != pinned.FileIndexHigh
            || expected.FileIndexLow != pinned.FileIndexLow)
            throw new InvalidDataException("OpenRouter premium artifact directory handle identity changed.");
        using SafeFileHandle handle = OpenDirectoryPinned(path);
        FileIdentity actual = Inspect(handle, expectDirectory: true);
        EnsureSameIdentity(pinned, actual, "artifact directory");
    }

    internal static FileStream OpenFileNoFollow(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options)
    {
        if (mode is not (FileMode.CreateNew or FileMode.Open))
            throw new ArgumentOutOfRangeException(nameof(mode));
        uint desiredAccess = access switch
        {
            FileAccess.Read => GenericRead,
            FileAccess.Write => GenericWrite,
            FileAccess.ReadWrite => GenericRead | GenericWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(access))
        };
        uint disposition = mode == FileMode.CreateNew ? CreateNew : OpenExisting;
        uint flags = FileFlagOpenReparsePoint | unchecked((uint)options);
        SafeFileHandle handle = CreateFileW(path, desiredAccess, share, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid) throw LastWin32($"open journal file {Path.GetFileName(path)}");
        try
        {
            _ = InspectRegularSingleLink(handle);
            return new FileStream(handle, access, bufferSize, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static void VerifyStableSet(
        string root,
        SafeFileHandle directoryHandle,
        FileStream writerLease,
        FileStream headerStream,
        FileStream recordsStream)
    {
        RequireSupportedPlatform();
        FileIdentity pinnedDirectory = Inspect(directoryHandle, expectDirectory: true);
        using (SafeFileHandle reopenedDirectory = OpenDirectoryPinned(root))
        {
            EnsureSameIdentity(pinnedDirectory, Inspect(reopenedDirectory, expectDirectory: true), "journal directory");
        }

        VerifyPath(root, FileOpenRouterPremiumJournal.WriterLeaseFileName, writerLease.SafeFileHandle);
        VerifyPath(root, FileOpenRouterPremiumJournal.HeaderFileName, headerStream.SafeFileHandle);
        VerifyPath(root, FileOpenRouterPremiumJournal.RecordsFileName, recordsStream.SafeFileHandle);
    }

    internal static void VerifyStableSingleFile(
        string root,
        SafeFileHandle directoryHandle,
        string fileName,
        FileStream stream)
    {
        RequireSupportedPlatform();
        if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName))
            throw new InvalidDataException("OpenRouter premium artifact name is invalid.");
        FileIdentity pinnedDirectory = Inspect(directoryHandle, expectDirectory: true);
        using (SafeFileHandle reopenedDirectory = OpenDirectoryPinned(root))
        {
            EnsureSameIdentity(pinnedDirectory, Inspect(reopenedDirectory, expectDirectory: true), "artifact directory");
        }
        VerifyPath(root, fileName, stream.SafeFileHandle);
    }

    private static void VerifyPath(string root, string fileName, SafeFileHandle pinnedHandle)
    {
        FileIdentity pinned = InspectRegularSingleLink(pinnedHandle);
        using FileStream reopened = OpenFileNoFollow(
            Path.Combine(root, fileName), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, 1, FileOptions.SequentialScan);
        FileIdentity path = InspectRegularSingleLink(reopened.SafeFileHandle);
        EnsureSameIdentity(pinned, path, fileName);
    }

    private static FileIdentity InspectRegularSingleLink(SafeFileHandle handle)
    {
        FileIdentity identity = Inspect(handle, expectDirectory: false);
        if ((identity.Attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
            throw new InvalidDataException("OpenRouter premium journal artifact must be a regular non-reparse file.");
        if (identity.LinkCount != 1)
            throw new InvalidDataException("OpenRouter premium journal artifacts must have exactly one hard link.");
        return identity;
    }

    private static FileIdentity Inspect(SafeFileHandle handle, bool expectDirectory)
    {
        if (handle.IsInvalid || handle.IsClosed)
            throw new InvalidDataException("OpenRouter premium journal handle is unavailable.");
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation info))
            throw LastWin32("inspect journal handle");
        bool isDirectory = (info.FileAttributes & FileAttributeDirectory) != 0;
        if (isDirectory != expectDirectory)
            throw new InvalidDataException(expectDirectory
                ? "OpenRouter premium journal root is not a directory."
                : "OpenRouter premium journal artifact is not a regular file.");
        return new(info.VolumeSerialNumber, info.FileIndexHigh, info.FileIndexLow,
            info.NumberOfLinks, info.FileAttributes);
    }

    private static void EnsureSameIdentity(FileIdentity expected, FileIdentity actual, string name)
    {
        if (expected.VolumeSerialNumber != actual.VolumeSerialNumber
            || expected.FileIndexHigh != actual.FileIndexHigh
            || expected.FileIndexLow != actual.FileIndexLow)
            throw new InvalidDataException($"OpenRouter premium {name} path identity changed.");
    }

    private static IOException LastWin32(string operation) =>
        new($"Unable to {operation}.", new Win32Exception(Marshal.GetLastWin32Error()));

    private readonly record struct FileIdentity(
        uint VolumeSerialNumber,
        uint FileIndexHigh,
        uint FileIndexLow,
        uint LinkCount,
        uint Attributes);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] path,
        uint pathLength,
        uint flags);
}

internal readonly record struct FileOpenRouterPremiumDirectoryIdentity(
    uint VolumeSerialNumber,
    uint FileIndexHigh,
    uint FileIndexLow)
{
    internal string CanonicalIdentity => FormattableString.Invariant(
        $"{VolumeSerialNumber:x8}-{FileIndexHigh:x8}-{FileIndexLow:x8}");

    internal static FileOpenRouterPremiumDirectoryIdentity Parse(string value)
    {
        FileOpenRouterPremiumFileIdentity parsed = FileOpenRouterPremiumFileIdentity.Parse(value);
        return new(parsed.VolumeSerialNumber, parsed.FileIndexHigh, parsed.FileIndexLow);
    }
}

internal readonly record struct FileOpenRouterPremiumFileIdentity(
    uint VolumeSerialNumber,
    uint FileIndexHigh,
    uint FileIndexLow)
{
    internal string CanonicalIdentity => FormattableString.Invariant(
        $"{VolumeSerialNumber:x8}-{FileIndexHigh:x8}-{FileIndexLow:x8}");

    internal static FileOpenRouterPremiumFileIdentity Parse(string value)
    {
        if (value is not { Length: 26 } || value[8] != '-' || value[17] != '-')
            throw new InvalidDataException("OpenRouter premium artifact file identity is invalid.");
        if (!uint.TryParse(value.AsSpan(0, 8), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint volume)
            || !uint.TryParse(value.AsSpan(9, 8), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint high)
            || !uint.TryParse(value.AsSpan(18, 8), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint low)
            || value.Any(static character => character is >= 'A' and <= 'F'))
            throw new InvalidDataException("OpenRouter premium artifact file identity is invalid.");
        return new(volume, high, low);
    }
}

internal abstract record JournalCommand
{
    internal abstract string Kind { get; }
    internal abstract long Apply(InMemoryOpenRouterPremiumJournal journal);
    internal abstract JournalCommand Detached();
}

internal sealed record AdmitCommand(int SlotIndex, string ScenarioId, string PromptDigestSha256, string RequestDigestSha256, long ReservedMicrousd) : JournalCommand
{
    internal override string Kind => "admit";
    internal override long Apply(InMemoryOpenRouterPremiumJournal journal) => journal.Admit(SlotIndex, ScenarioId, PromptDigestSha256, RequestDigestSha256, ReservedMicrousd);
    internal override JournalCommand Detached() => this with { };
}

internal sealed record MarkDispatchUnknownCommand(int SlotIndex, string RequestDigestSha256, long ExpectedVersion) : JournalCommand
{
    internal override string Kind => "dispatch_unknown";
    internal override long Apply(InMemoryOpenRouterPremiumJournal journal) => journal.MarkDispatchUnknown(SlotIndex, RequestDigestSha256, ExpectedVersion);
    internal override JournalCommand Detached() => this with { };
}

internal sealed record CompleteBeforeDispatchCommand(OpenRouterPremiumSlotReceipt Receipt, long ExpectedVersion) : JournalCommand
{
    internal override string Kind => "complete_before_dispatch";
    internal override long Apply(InMemoryOpenRouterPremiumJournal journal) => journal.CompleteBeforeDispatch(Receipt, ExpectedVersion);
    internal override JournalCommand Detached() => new CompleteBeforeDispatchCommand(Receipt.Detached(), ExpectedVersion);
}

internal sealed record CompleteCommand(OpenRouterPremiumSlotReceipt Receipt, long ExpectedVersion) : JournalCommand
{
    internal override string Kind => "complete";
    internal override long Apply(InMemoryOpenRouterPremiumJournal journal) => journal.Complete(Receipt, ExpectedVersion);
    internal override JournalCommand Detached() => new CompleteCommand(Receipt.Detached(), ExpectedVersion);
}

internal sealed record ConsumeEligiblePreflightCommand(
    string AuthorizationNonceDigestSha256,
    string BundleDigestSha256,
    string TrustEvidenceDigestSha256) : JournalCommand
{
    internal override string Kind => "consume_eligible_preflight";
    internal override long Apply(InMemoryOpenRouterPremiumJournal journal) => 1;
    internal override JournalCommand Detached() => this with { };
}

internal sealed record LoadedJournal(
    OpenRouterPremiumJournalHeader Header,
    InMemoryOpenRouterPremiumJournal Inner,
    List<JournalCommand> Commands,
    string FinalRecordChecksumSha256);

internal static class FileOpenRouterPremiumJournalCodec
{
    internal const string ZeroDigest = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string RecordSchemaVersion = "snow_globe_openrouter_premium_journal_record/v1";
    private const int MaximumJsonDepth = 8;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] WriteHeader(OpenRouterPremiumJournalHeader header)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", header.SchemaVersion);
        writer.WriteString("journal_identity", header.JournalIdentity);
        writer.WriteString("run_identity", header.RunIdentity);
        writer.WriteString("profile_digest_sha256", header.ProfileDigestSha256);
        writer.WriteString("catalog_evidence_digest_sha256", header.CatalogEvidenceDigestSha256);
        writer.WriteString("endpoint_evidence_digest_sha256", header.EndpointEvidenceDigestSha256);
        writer.WriteString("prompt_set_digest_sha256", header.PromptSetDigestSha256);
        writer.WriteString("account_binding_identity", header.AccountBindingIdentity);
        writer.WriteNumber("maximum_slots", header.MaximumSlots);
        writer.WriteNumber("per_slot_cost_ceiling_microusd", header.PerSlotCostCeilingMicrousd);
        writer.WriteNumber("aggregate_cost_ceiling_microusd", header.AggregateCostCeilingMicrousd);
        writer.WriteString("header_checksum_sha256", header.HeaderChecksumSha256);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static LoadedJournal Load(string root, FileStream headerStream, FileStream recordsStream,
        bool rejectUnexpectedEntries = true)
    {
        if (rejectUnexpectedEntries)
        {
            string[] names = Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName)
                .OrderBy(v => v, StringComparer.Ordinal).ToArray()!;
            string[] expected = [HeaderFileName(), RecordsFileName(), WriterLeaseFileName()];
            Array.Sort(expected, StringComparer.Ordinal);
            if (!names.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException("OpenRouter premium journal directory contents are invalid.");
        }
        byte[] headerBytes = ReadBounded(headerStream, 4 * 1024);
        OpenRouterPremiumJournalHeader header = ParseHeader(headerBytes);
        byte[] recordBytes = ReadBounded(recordsStream, FileOpenRouterPremiumJournal.MaximumTotalBytes);
        if (HasBom(recordBytes)) throw new InvalidDataException("OpenRouter premium journal records must be UTF-8 without BOM.");
        List<JournalCommand> commands = [];
        string previous = ZeroDigest;
        if (recordBytes.Length > 0)
        {
            if (recordBytes[^1] != (byte)'\n') throw new InvalidDataException("OpenRouter premium journal record tail is unterminated.");
            int start = 0;
            while (start < recordBytes.Length)
            {
                int end = Array.IndexOf(recordBytes, (byte)'\n', start);
                int length = end - start;
                if (end < 0 || length is < 1 or > 8 * 1024 || commands.Count >= 48)
                    throw new InvalidDataException("OpenRouter premium journal record bounds are invalid.");
                ParsedRecord parsed = ParseRecord(recordBytes.AsSpan(start, length), commands.Count + 1, previous, header.HeaderChecksumSha256);
                if (parsed.Command is ConsumeEligiblePreflightCommand
                    && (commands.Count != 0 || commands.OfType<ConsumeEligiblePreflightCommand>().Any()))
                    throw new InvalidDataException("OpenRouter premium journal preflight consumption is out of sequence.");
                InMemoryOpenRouterPremiumJournal candidate = FileOpenRouterPremiumJournal.Replay(header, commands);
                long actualVersion = parsed.Command.Apply(candidate);
                if (actualVersion != parsed.Version)
                    throw new InvalidDataException("OpenRouter premium journal record transition is incoherent.");
                commands.Add(parsed.Command.Detached());
                previous = parsed.ChecksumSha256;
                start = end + 1;
            }
        }
        return new(header, FileOpenRouterPremiumJournal.Replay(header, commands), commands, previous);
    }

    internal static byte[] WriteRecord(int sequence, string previousChecksum, string headerChecksum, JournalCommand command, long version)
    {
        byte[] payload = WriteRecordCore(sequence, previousChecksum, headerChecksum, command, version, null);
        string checksum = OpenRouterPremiumCanonical.Digest(payload);
        byte[] canonical = WriteRecordCore(sequence, previousChecksum, headerChecksum, command, version, checksum);
        byte[] result = new byte[canonical.Length + 1];
        canonical.CopyTo(result, 0); result[^1] = (byte)'\n';
        return result;
    }

    internal static string RecordChecksum(ReadOnlySpan<byte> record)
    {
        using JsonDocument document = ParseDocument(record);
        return RequiredString(document.RootElement, "record_checksum_sha256");
    }

    internal static string ReceiptDescriptor(OpenRouterPremiumSlotReceipt receipt) => string.Join('|', receipt.SlotIndex,
        receipt.ScenarioId, receipt.PromptDigestSha256, receipt.RequestDigestSha256, receipt.ResponseDigestSha256,
        receipt.SubmissionState, receipt.ChargeState, receipt.PromptTokens, receipt.CompletionTokens, receipt.TotalTokens,
        receipt.SettledMicrousd, receipt.Proposal?.AgentId ?? "null", receipt.Proposal?.Action.ToString() ?? "null",
        receipt.Proposal?.Quantity.ToString() ?? "null", receipt.OutcomeCode);

    private static byte[] WriteRecordCore(int sequence, string previousChecksum, string headerChecksum, JournalCommand command, long version, string? checksum)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", RecordSchemaVersion);
        writer.WriteNumber("sequence", sequence);
        writer.WriteString("kind", command.Kind);
        writer.WriteString("previous_checksum_sha256", previousChecksum);
        writer.WriteString("header_checksum_sha256", headerChecksum);
        writer.WriteNumber("version", version);
        writer.WritePropertyName("payload"); WriteCommand(writer, command);
        if (checksum is not null) writer.WriteString("record_checksum_sha256", checksum);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteCommand(Utf8JsonWriter writer, JournalCommand command)
    {
        writer.WriteStartObject();
        switch (command)
        {
            case AdmitCommand admit:
                writer.WriteNumber("slot_index", admit.SlotIndex); writer.WriteString("scenario_id", admit.ScenarioId);
                writer.WriteString("prompt_digest_sha256", admit.PromptDigestSha256); writer.WriteString("request_digest_sha256", admit.RequestDigestSha256);
                writer.WriteNumber("reserved_microusd", admit.ReservedMicrousd); break;
            case MarkDispatchUnknownCommand mark:
                writer.WriteNumber("slot_index", mark.SlotIndex); writer.WriteString("request_digest_sha256", mark.RequestDigestSha256);
                writer.WriteNumber("expected_version", mark.ExpectedVersion); break;
            case CompleteBeforeDispatchCommand complete:
                writer.WriteNumber("expected_version", complete.ExpectedVersion); writer.WritePropertyName("receipt"); WriteReceipt(writer, complete.Receipt); break;
            case CompleteCommand complete:
                writer.WriteNumber("expected_version", complete.ExpectedVersion); writer.WritePropertyName("receipt"); WriteReceipt(writer, complete.Receipt); break;
            case ConsumeEligiblePreflightCommand preflight:
                writer.WriteString("authorization_nonce_digest_sha256", preflight.AuthorizationNonceDigestSha256);
                writer.WriteString("bundle_digest_sha256", preflight.BundleDigestSha256);
                writer.WriteString("trust_evidence_digest_sha256", preflight.TrustEvidenceDigestSha256); break;
            default: throw new InvalidDataException("Unknown OpenRouter premium journal command.");
        }
        writer.WriteEndObject();
    }

    private static void WriteReceipt(Utf8JsonWriter writer, OpenRouterPremiumSlotReceipt receipt)
    {
        writer.WriteStartObject();
        writer.WriteNumber("slot_index", receipt.SlotIndex); writer.WriteString("scenario_id", receipt.ScenarioId);
        writer.WriteString("prompt_digest_sha256", receipt.PromptDigestSha256); writer.WriteString("request_digest_sha256", receipt.RequestDigestSha256);
        writer.WriteString("response_digest_sha256", receipt.ResponseDigestSha256); writer.WriteString("submission_state", receipt.SubmissionState.ToString());
        writer.WriteString("charge_state", receipt.ChargeState.ToString()); writer.WriteNumber("prompt_tokens", receipt.PromptTokens);
        writer.WriteNumber("completion_tokens", receipt.CompletionTokens); writer.WriteNumber("total_tokens", receipt.TotalTokens);
        writer.WriteNumber("settled_microusd", receipt.SettledMicrousd);
        writer.WritePropertyName("proposal");
        if (receipt.Proposal is null) writer.WriteNullValue();
        else
        {
            writer.WriteStartObject(); writer.WriteString("agent_id", receipt.Proposal.AgentId);
            writer.WriteString("action", receipt.Proposal.Action.ToString()); writer.WriteNumber("quantity", receipt.Proposal.Quantity); writer.WriteEndObject();
        }
        writer.WriteString("outcome_code", receipt.OutcomeCode); writer.WriteEndObject();
    }

    private static OpenRouterPremiumJournalHeader ParseHeader(byte[] bytes)
    {
        if (HasBom(bytes)) throw new InvalidDataException("OpenRouter premium journal header must be UTF-8 without BOM.");
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_version", "journal_identity", "run_identity", "profile_digest_sha256", "catalog_evidence_digest_sha256",
            "endpoint_evidence_digest_sha256", "prompt_set_digest_sha256", "account_binding_identity", "maximum_slots",
            "per_slot_cost_ceiling_microusd", "aggregate_cost_ceiling_microusd", "header_checksum_sha256");
        string account = RequiredString(root, "account_binding_identity"); _ = new ByokAccountBindingIdentity(account);
        OpenRouterPremiumJournalHeader header = new(RequiredString(root, "schema_version"), RequiredString(root, "journal_identity"),
            RequiredString(root, "run_identity"), RequiredString(root, "profile_digest_sha256"), RequiredString(root, "catalog_evidence_digest_sha256"),
            RequiredString(root, "endpoint_evidence_digest_sha256"), RequiredString(root, "prompt_set_digest_sha256"), account,
            RequiredInt32(root, "maximum_slots"), RequiredInt64(root, "per_slot_cost_ceiling_microusd"),
            RequiredInt64(root, "aggregate_cost_ceiling_microusd"), RequiredString(root, "header_checksum_sha256"));
        header.Validate();
        if (!bytes.AsSpan().SequenceEqual(WriteHeader(header))) throw new InvalidDataException("OpenRouter premium journal header is not canonical.");
        return header;
    }

    private static ParsedRecord ParseRecord(ReadOnlySpan<byte> bytes, int sequence, string previous, string headerChecksum)
    {
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_version", "sequence", "kind", "previous_checksum_sha256", "header_checksum_sha256", "version", "payload", "record_checksum_sha256");
        if (RequiredString(root, "schema_version") != RecordSchemaVersion || RequiredInt32(root, "sequence") != sequence
            || RequiredString(root, "previous_checksum_sha256") != previous || RequiredString(root, "header_checksum_sha256") != headerChecksum)
            throw new InvalidDataException("OpenRouter premium journal record chain is invalid.");
        string checksum = RequiredString(root, "record_checksum_sha256");
        if (!OpenRouterPremiumCanonical.IsDigest(checksum)) throw new InvalidDataException("OpenRouter premium journal record checksum is invalid.");
        JournalCommand command = ParseCommand(RequiredString(root, "kind"), root.GetProperty("payload"));
        long version = RequiredInt64(root, "version");
        byte[] canonical = WriteRecord(sequence, previous, headerChecksum, command, version);
        if (!bytes.SequenceEqual(canonical.AsSpan(0, canonical.Length - 1)))
            throw new InvalidDataException("OpenRouter premium journal record is not canonical or checksum-valid.");
        return new(command, version, checksum);
    }

    private static JournalCommand ParseCommand(string kind, JsonElement payload) => kind switch
    {
        "admit" => ParseAdmit(payload),
        "dispatch_unknown" => ParseMark(payload),
        "complete_before_dispatch" => ParseCompleteBefore(payload),
        "complete" => ParseComplete(payload),
        "consume_eligible_preflight" => ParsePreflightConsumption(payload),
        _ => throw new InvalidDataException("Unknown OpenRouter premium journal record kind.")
    };

    private static JournalCommand ParseAdmit(JsonElement value)
    {
        Exact(value, "slot_index", "scenario_id", "prompt_digest_sha256", "request_digest_sha256", "reserved_microusd");
        return new AdmitCommand(RequiredInt32(value, "slot_index"), RequiredString(value, "scenario_id"), RequiredString(value, "prompt_digest_sha256"),
            RequiredString(value, "request_digest_sha256"), RequiredInt64(value, "reserved_microusd"));
    }

    private static JournalCommand ParseMark(JsonElement value)
    {
        Exact(value, "slot_index", "request_digest_sha256", "expected_version");
        return new MarkDispatchUnknownCommand(RequiredInt32(value, "slot_index"), RequiredString(value, "request_digest_sha256"), RequiredInt64(value, "expected_version"));
    }

    private static JournalCommand ParseCompleteBefore(JsonElement value)
    {
        Exact(value, "expected_version", "receipt"); return new CompleteBeforeDispatchCommand(ParseReceipt(value.GetProperty("receipt")), RequiredInt64(value, "expected_version"));
    }

    private static JournalCommand ParseComplete(JsonElement value)
    {
        Exact(value, "expected_version", "receipt"); return new CompleteCommand(ParseReceipt(value.GetProperty("receipt")), RequiredInt64(value, "expected_version"));
    }

    private static JournalCommand ParsePreflightConsumption(JsonElement value)
    {
        Exact(value, "authorization_nonce_digest_sha256", "bundle_digest_sha256", "trust_evidence_digest_sha256");
        string nonce = RequiredString(value, "authorization_nonce_digest_sha256");
        string bundle = RequiredString(value, "bundle_digest_sha256");
        string trust = RequiredString(value, "trust_evidence_digest_sha256");
        if (!OpenRouterPremiumCanonical.IsDigest(nonce) || !OpenRouterPremiumCanonical.IsDigest(bundle)
            || !OpenRouterPremiumCanonical.IsDigest(trust))
            throw new InvalidDataException("OpenRouter premium journal preflight consumption binding is invalid.");
        return new ConsumeEligiblePreflightCommand(nonce, bundle, trust);
    }

    private static OpenRouterPremiumSlotReceipt ParseReceipt(JsonElement value)
    {
        Exact(value, "slot_index", "scenario_id", "prompt_digest_sha256", "request_digest_sha256", "response_digest_sha256", "submission_state",
            "charge_state", "prompt_tokens", "completion_tokens", "total_tokens", "settled_microusd", "proposal", "outcome_code");
        if (!Enum.TryParse(RequiredString(value, "submission_state"), false, out SubmissionState submission) || !Enum.IsDefined(submission)
            || !Enum.TryParse(RequiredString(value, "charge_state"), false, out ChargeState charge) || !Enum.IsDefined(charge))
            throw new InvalidDataException("OpenRouter premium journal receipt enum is invalid.");
        SnowGlobeActionProposal? proposal = null;
        JsonElement proposalValue = value.GetProperty("proposal");
        if (proposalValue.ValueKind == JsonValueKind.Object)
        {
            Exact(proposalValue, "agent_id", "action", "quantity");
            if (!Enum.TryParse(RequiredString(proposalValue, "action"), false, out SnowGlobeActionKind action) || !Enum.IsDefined(action))
                throw new InvalidDataException("OpenRouter premium journal proposal action is invalid.");
            proposal = new(RequiredString(proposalValue, "agent_id"), action, RequiredInt32(proposalValue, "quantity"));
        }
        else if (proposalValue.ValueKind != JsonValueKind.Null) throw new InvalidDataException("OpenRouter premium journal proposal is invalid.");
        return new(RequiredInt32(value, "slot_index"), RequiredString(value, "scenario_id"), RequiredString(value, "prompt_digest_sha256"),
            RequiredString(value, "request_digest_sha256"), RequiredString(value, "response_digest_sha256"), submission, charge,
            RequiredInt32(value, "prompt_tokens"), RequiredInt32(value, "completion_tokens"), RequiredInt32(value, "total_tokens"),
            RequiredInt64(value, "settled_microusd"), proposal, RequiredString(value, "outcome_code"));
    }

    private static JsonDocument ParseDocument(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = StrictUtf8.GetString(bytes);
            RejectDuplicates(bytes);
            JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            { MaxDepth = MaximumJsonDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            { document.Dispose(); throw new InvalidDataException("OpenRouter premium journal JSON root must be an object."); }
            return document;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        { throw new InvalidDataException("OpenRouter premium journal JSON is invalid.", exception); }
    }

    private static void RejectDuplicates(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = MaximumJsonDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !stack.Peek().Add(reader.GetString()!))
                throw new InvalidDataException("OpenRouter premium journal JSON contains a duplicate property.");
        }
    }

    private static void Exact(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("OpenRouter premium journal JSON value must be an object.");
        HashSet<string> names = value.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (names.Count != expected.Length || expected.Any(name => !names.Contains(name)))
            throw new InvalidDataException("OpenRouter premium journal JSON has missing or unknown properties.");
    }

    private static string RequiredString(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String
        ? value.GetProperty(name).GetString()! : throw new InvalidDataException($"{name} must be a string.");
    private static int RequiredInt32(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt32(out int parsed)
        ? parsed : throw new InvalidDataException($"{name} must be a 32-bit integer.");
    private static long RequiredInt64(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt64(out long parsed)
        ? parsed : throw new InvalidDataException($"{name} must be an integer.");

    private static byte[] ReadBounded(FileStream stream, int maximum)
    {
        stream.Position = 0;
        long length = stream.Length;
        if (length > maximum) throw new InvalidDataException("OpenRouter premium journal artifact is oversized.");
        byte[] bytes = new byte[checked((int)length)];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new InvalidDataException("OpenRouter premium journal artifact changed while being read.");
            offset += read;
        }
        if (stream.Length != length) throw new InvalidDataException("OpenRouter premium journal artifact changed while being read.");
        return bytes;
    }

    private static bool HasBom(ReadOnlySpan<byte> bytes) => bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
    private static string HeaderFileName() => FileOpenRouterPremiumJournal.HeaderFileName;
    private static string RecordsFileName() => FileOpenRouterPremiumJournal.RecordsFileName;
    private static string WriterLeaseFileName() => FileOpenRouterPremiumJournal.WriterLeaseFileName;
    private sealed record ParsedRecord(JournalCommand Command, long Version, string ChecksumSha256);
}
