using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

internal sealed class OllamaRecordingArtifactStoreException : Exception
{
    internal OllamaRecordingArtifactStoreException(string code) : base(CloseCode(code)) => Code = CloseCode(code);
    internal OllamaRecordingArtifactStoreException(string code, Exception? _) : this(code) { }
    internal string Code { get; }

    private static string CloseCode(string code) => code switch
    {
        "repository_root_not_verified" or "artifact_path_outside_bound" or "artifact_read_bound_invalid" or
        "artifact_path_inspection_failed" or "artifact_directory_missing" or "artifact_directory_invalid" or
        "artifact_path_reparse_point_rejected" or "artifact_directory_lease_failed" or
        "artifact_directory_lease_count_invalid" or "artifact_directory_identity_unavailable" or
        "artifact_directory_identity_mismatch" or "artifact_file_identity_unavailable" or
        "artifact_file_identity_mismatch" or "artifact_file_identity_changed" or "artifact_hardlink_rejected" or
        "artifact_already_exists" or "artifact_reservation_failed" or "artifact_not_found" or
        "artifact_size_invalid" or "artifact_size_changed" or "artifact_read_failed" or
        "artifact_reservation_reused" or "artifact_reservation_disposed" or
        "artifact_durable_readback_mismatch" or "artifact_publication_indeterminate" or
        "artifact_reservation_dispose_failed" or "artifact_store_platform_unsupported" => code,
        _ => "artifact_store_failed"
    };
}

internal interface IOllamaRecordingArtifactStore
{
    IOllamaRecordingArtifactReservation Reserve(string absoluteRepositoryRoot, string relativeArtifactPath);
    byte[] ReadBounded(string absoluteRepositoryRoot, string relativeArtifactPath, int maximumBytes);
}

internal interface IOllamaRecordingArtifactReservation : IDisposable
{
    byte[] PublishAndReadBack(ReadOnlyMemory<byte> canonicalUtf8, int maximumBytes);
}

/// <summary>Production fixed-path CreateNew store with pinned Windows directory identities.</summary>
internal sealed class FileOllamaRecordingArtifactStore : IOllamaRecordingArtifactStore
{
    private readonly Action? _afterDirectoryLeaseAcquiredForTesting;
    private readonly Action? _afterFinalFileHandlePinnedForTesting;
    private readonly Action? _afterReservationStreamDisposedForTesting;
    private readonly Action? _afterReservationLeaseDisposedForTesting;
    internal FileOllamaRecordingArtifactStore() { }
    internal FileOllamaRecordingArtifactStore(Action afterDirectoryLeaseAcquiredForTesting) => _afterDirectoryLeaseAcquiredForTesting = afterDirectoryLeaseAcquiredForTesting ?? throw new ArgumentNullException(nameof(afterDirectoryLeaseAcquiredForTesting));
    internal FileOllamaRecordingArtifactStore(Action? afterDirectoryLeaseAcquiredForTesting, Action? afterFinalFileHandlePinnedForTesting)
    { _afterDirectoryLeaseAcquiredForTesting = afterDirectoryLeaseAcquiredForTesting; _afterFinalFileHandlePinnedForTesting = afterFinalFileHandlePinnedForTesting; }
    internal FileOllamaRecordingArtifactStore(
        Action? afterDirectoryLeaseAcquiredForTesting,
        Action? afterFinalFileHandlePinnedForTesting,
        Action? afterReservationStreamDisposedForTesting,
        Action? afterReservationLeaseDisposedForTesting)
    {
        _afterDirectoryLeaseAcquiredForTesting = afterDirectoryLeaseAcquiredForTesting;
        _afterFinalFileHandlePinnedForTesting = afterFinalFileHandlePinnedForTesting;
        _afterReservationStreamDisposedForTesting = afterReservationStreamDisposedForTesting;
        _afterReservationLeaseDisposedForTesting = afterReservationLeaseDisposedForTesting;
    }

    public IOllamaRecordingArtifactReservation Reserve(string absoluteRepositoryRoot, string relativeArtifactPath)
    {
        ValidateFixedRelativePath(relativeArtifactPath);
        RecordingDirectoryLease lease = RecordingDirectoryLease.Acquire(absoluteRepositoryRoot, createMissing: true);
        try
        {
            _afterDirectoryLeaseAcquiredForTesting?.Invoke();
            string path = Path.Combine(absoluteRepositoryRoot, relativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            FileAttributes? attributes = TryGetAttributes(path);
            if (attributes.HasValue)
            {
                if (attributes.Value.HasFlag(FileAttributes.ReparsePoint)) throw Failure("artifact_path_reparse_point_rejected");
                throw Failure("artifact_already_exists");
            }
            FileStream stream;
            try
            {
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            }
            catch (IOException) { throw Failure("artifact_reservation_failed"); }
            catch (UnauthorizedAccessException) { throw Failure("artifact_reservation_failed"); }
            return new FileReservation(lease, stream, _afterReservationStreamDisposedForTesting, _afterReservationLeaseDisposedForTesting);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public byte[] ReadBounded(string absoluteRepositoryRoot, string relativeArtifactPath, int maximumBytes)
    {
        ValidateFixedRelativePath(relativeArtifactPath);
        if (maximumBytes is < 1 or > OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes) throw Failure("artifact_read_bound_invalid");
        using RecordingDirectoryLease lease = RecordingDirectoryLease.Acquire(absoluteRepositoryRoot, createMissing: false);
        _afterDirectoryLeaseAcquiredForTesting?.Invoke();
        string path = Path.Combine(absoluteRepositoryRoot, relativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            return lease.ReadPinnedArtifact(path, maximumBytes, _afterFinalFileHandlePinnedForTesting);
        }
        catch (OllamaRecordingArtifactStoreException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException) { throw Failure("artifact_read_failed"); }
    }

    internal static void VerifyRepositoryRoot(string root)
    {
        VerifyDirectory(root, "repository_root_not_verified");
        VerifyGitMarker(Path.Combine(root, ".git"));
        VerifyMarkerFile(root, "CURRENT_BUILD.md");
        VerifyMarkerFile(root, "labs", "Societies.SnowGlobe", "Societies.SnowGlobe.csproj");
        VerifyDirectory(root, "repository_root_not_verified");
    }

    private static void VerifyGitMarker(string path)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue || attributes.Value.HasFlag(FileAttributes.ReparsePoint)) throw Failure("repository_root_not_verified");
        if (attributes.Value.HasFlag(FileAttributes.Directory)) return;
        long length = new FileInfo(path).Length;
        if (length is < 1 or > 4096) throw Failure("repository_root_not_verified");
    }

    private static void VerifyMarkerFile(string root, params string[] segments)
    {
        string current = root;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            current = Path.Combine(current, segments[index]);
            VerifyDirectory(current, "repository_root_not_verified");
        }
        string file = Path.Combine(current, segments[^1]);
        FileAttributes? attributes = TryGetAttributes(file);
        if (!attributes.HasValue || attributes.Value.HasFlag(FileAttributes.Directory) || attributes.Value.HasFlag(FileAttributes.ReparsePoint) || new FileInfo(file).Length < 1) throw Failure("repository_root_not_verified");
    }

    private static void VerifyDirectory(string path, string code)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue || !attributes.Value.HasFlag(FileAttributes.Directory) || attributes.Value.HasFlag(FileAttributes.ReparsePoint)) throw Failure(code);
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw Failure("artifact_path_inspection_failed"); }
    }

    private static void ValidateFixedRelativePath(string relativeArtifactPath)
    {
        if (!string.Equals(relativeArtifactPath, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, StringComparison.Ordinal)) throw Failure("artifact_path_outside_bound");
    }

    private static OllamaRecordingArtifactStoreException Failure(string code) => new(code);

    private sealed class FileReservation : IOllamaRecordingArtifactReservation
    {
        private readonly RecordingDirectoryLease _lease;
        private readonly FileStream _stream;
        private readonly Action? _afterStreamDisposedForTesting;
        private readonly Action? _afterLeaseDisposedForTesting;
        private int _published;
        private int _disposed;

        internal FileReservation(RecordingDirectoryLease lease, FileStream stream, Action? afterStreamDisposedForTesting, Action? afterLeaseDisposedForTesting)
        { _lease = lease; _stream = stream; _afterStreamDisposedForTesting = afterStreamDisposedForTesting; _afterLeaseDisposedForTesting = afterLeaseDisposedForTesting; }

        public byte[] PublishAndReadBack(ReadOnlyMemory<byte> canonicalUtf8, int maximumBytes)
        {
            if (Interlocked.Exchange(ref _published, 1) != 0) throw Failure("artifact_reservation_reused");
            if (Volatile.Read(ref _disposed) != 0) throw Failure("artifact_reservation_disposed");
            if (canonicalUtf8.Length is < 1 || canonicalUtf8.Length > maximumBytes) throw Failure("artifact_size_invalid");
            try
            {
                _stream.Write(canonicalUtf8.Span);
                _stream.Flush(flushToDisk: true);
                _stream.Position = 0;
                byte[] readback = new byte[canonicalUtf8.Length];
                _stream.ReadExactly(readback);
                if (_stream.ReadByte() != -1 || !readback.AsSpan().SequenceEqual(canonicalUtf8.Span))
                {
                    Array.Clear(readback);
                    throw Failure("artifact_durable_readback_mismatch");
                }
                return readback;
            }
            catch (OllamaRecordingArtifactStoreException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException) { throw Failure("artifact_publication_indeterminate"); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            OllamaRecordingArtifactStoreException? failure = null;
            try { _stream.Dispose(); _afterStreamDisposedForTesting?.Invoke(); }
            catch { failure = Failure("artifact_reservation_dispose_failed"); }
            finally
            {
                try { _lease.Dispose(); _afterLeaseDisposedForTesting?.Invoke(); }
                catch { failure ??= Failure("artifact_reservation_dispose_failed"); }
            }
            if (failure is not null) throw failure;
        }
    }

    /// <summary>Holds root and fixed artifact ancestors without FILE_SHARE_DELETE until publication ends.</summary>
    private sealed class RecordingDirectoryLease : IDisposable
    {
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint GenericRead = 0x80000000;
        private const uint OpenExisting = 3;
        private const int MaximumFinalPathCharacters = 32_768;
        private static readonly string[] Segments = ["artifacts", "snowglobe", "local-model"];
        private readonly SafeFileHandle[] _handles;
        private int _disposed;

        private RecordingDirectoryLease(SafeFileHandle[] handles) => _handles = handles;

        internal static RecordingDirectoryLease Acquire(string root, bool createMissing)
        {
            if (!OperatingSystem.IsWindows()) throw Failure("artifact_store_platform_unsupported");
            List<SafeFileHandle> handles = new(4);
            try
            {
                handles.Add(OpenVerifiedDirectory(root));
                VerifyRepositoryRoot(root);
                string current = root;
                foreach (string segment in Segments)
                {
                    current = Path.Combine(current, segment);
                    if (createMissing) CreateLeafIfMissing(current);
                    else VerifyDirectory(current, "artifact_directory_missing");
                    handles.Add(OpenVerifiedDirectory(current));
                }
                if (handles.Count != 4) throw Failure("artifact_directory_lease_count_invalid");
                return new RecordingDirectoryLease(handles.ToArray());
            }
            catch
            {
                for (int index = handles.Count - 1; index >= 0; index--) handles[index].Dispose();
                throw;
            }
        }

        internal byte[] ReadPinnedArtifact(string expectedPath, int maximumBytes, Action? afterHandlePinned)
        {
            SafeFileHandle handle = CreateFile(expectedPath, GenericRead, FileShare.Read, IntPtr.Zero,
                OpenExisting, FileFlagOpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw Failure(error is 2 or 3 ? "artifact_not_found" : "artifact_read_failed");
            }

            try
            {
                ByHandleFileInformation before = VerifyPinnedArtifactIdentity(handle, expectedPath, maximumBytes);
                afterHandlePinned?.Invoke();
                long length = CombineSize(before);
                using FileStream stream = new(handle, FileAccess.Read, 4096, isAsync: false);
                byte[] bytes = new byte[checked((int)length)];
                try
                {
                    stream.ReadExactly(bytes);
                    if (stream.ReadByte() != -1) throw Failure("artifact_size_changed");
                    ByHandleFileInformation after = VerifyPinnedArtifactIdentity(stream.SafeFileHandle, expectedPath, maximumBytes);
                    if (!SameFileIdentity(before, after)) throw Failure("artifact_file_identity_changed");
                    return bytes;
                }
                catch
                {
                    Array.Clear(bytes);
                    throw;
                }
            }
            catch (OllamaRecordingArtifactStoreException) { handle.Dispose(); throw; }
            catch { handle.Dispose(); throw Failure("artifact_read_failed"); }
        }

        private static ByHandleFileInformation VerifyPinnedArtifactIdentity(SafeFileHandle handle, string expectedPath, int maximumBytes)
        {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) throw NativeFailure("artifact_file_identity_unavailable");
            FileAttributes attributes = (FileAttributes)information.FileAttributes;
            if (attributes.HasFlag(FileAttributes.Directory)) throw Failure("artifact_not_found");
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw Failure("artifact_path_reparse_point_rejected");
            if (information.NumberOfLinks != 1) throw Failure("artifact_hardlink_rejected");
            long length = CombineSize(information);
            if (length is < 1 || length > maximumBytes) throw Failure("artifact_size_invalid");

            string actual = GetFinalPath(handle, "artifact_file_identity_unavailable");
            if (!string.Equals(Path.GetFullPath(expectedPath), Path.GetFullPath(actual), StringComparison.OrdinalIgnoreCase))
                throw Failure("artifact_file_identity_mismatch");
            return information;
        }

        private static bool SameFileIdentity(ByHandleFileInformation left, ByHandleFileInformation right) =>
            left.VolumeSerialNumber == right.VolumeSerialNumber
            && left.FileIndexHigh == right.FileIndexHigh
            && left.FileIndexLow == right.FileIndexLow
            && left.FileSizeHigh == right.FileSizeHigh
            && left.FileSizeLow == right.FileSizeLow
            && left.FileAttributes == right.FileAttributes
            && left.NumberOfLinks == right.NumberOfLinks;

        private static long CombineSize(ByHandleFileInformation information) =>
            checked(((long)information.FileSizeHigh << 32) | information.FileSizeLow);

        private static string GetFinalPath(SafeFileHandle handle, string code)
        {
            StringBuilder buffer = new(MaximumFinalPathCharacters);
            uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0 || length >= buffer.Capacity) throw NativeFailure(code);
            return NormalizeFinalPath(buffer.ToString());
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            for (int index = _handles.Length - 1; index >= 0; index--)
            {
                try { _handles[index].Dispose(); }
                catch { }
            }
        }

        private static void CreateLeafIfMissing(string path)
        {
            FileAttributes? attributes = TryGetAttributes(path);
            if (!attributes.HasValue)
            {
                try { Directory.CreateDirectory(path); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw Failure("artifact_directory_invalid"); }
            }
            VerifyDirectory(path, "artifact_directory_invalid");
        }

        private static SafeFileHandle OpenVerifiedDirectory(string expectedPath)
        {
            SafeFileHandle handle = CreateFile(expectedPath, GenericRead, FileShare.Read, IntPtr.Zero, OpenExisting, FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose(); throw Failure("artifact_directory_lease_failed");
            }
            try
            {
                if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) throw NativeFailure("artifact_directory_lease_failed");
                FileAttributes attributes = (FileAttributes)information.FileAttributes;
                if (!attributes.HasFlag(FileAttributes.Directory)) throw Failure("artifact_directory_invalid");
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw Failure("artifact_path_reparse_point_rejected");
                string actual = GetFinalPath(handle, "artifact_directory_identity_unavailable");
                if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual)), StringComparison.OrdinalIgnoreCase)) throw Failure("artifact_directory_identity_mismatch");
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }

        private static string NormalizeFinalPath(string path)
        {
            const string unc = @"\\?\UNC\"; const string extended = @"\\?\";
            if (path.StartsWith(unc, StringComparison.OrdinalIgnoreCase)) return @"\\" + path[unc.Length..];
            return path.StartsWith(extended, StringComparison.OrdinalIgnoreCase) ? path[extended.Length..] : path;
        }

        private static OllamaRecordingArtifactStoreException NativeFailure(string code) => Failure(code);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation fileInformation);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder filePath, uint filePathCharacters, uint flags);
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
    }
}

/// <summary>Offline test Adapter; it never touches the file system.</summary>
internal sealed class InMemoryOllamaRecordingArtifactStore : IOllamaRecordingArtifactStore
{
    private byte[]? _bytes;
    private int _reserveCount;
    private int _publishCount;
    private int _readCount;
    internal int ReserveCount => Volatile.Read(ref _reserveCount);
    internal int PublishCount => Volatile.Read(ref _publishCount);
    internal int ReadCount => Volatile.Read(ref _readCount);
    internal ReadOnlyMemory<byte>? Bytes => _bytes?.ToArray();

    public IOllamaRecordingArtifactReservation Reserve(string absoluteRepositoryRoot, string relativeArtifactPath)
    {
        Interlocked.Increment(ref _reserveCount);
        if (_bytes is not null) throw new OllamaRecordingArtifactStoreException("artifact_already_exists");
        return new Reservation(this);
    }

    public byte[] ReadBounded(string absoluteRepositoryRoot, string relativeArtifactPath, int maximumBytes)
    {
        Interlocked.Increment(ref _readCount);
        byte[] bytes = _bytes?.ToArray() ?? throw new OllamaRecordingArtifactStoreException("artifact_not_found");
        if (bytes.Length > maximumBytes) throw new OllamaRecordingArtifactStoreException("artifact_size_invalid");
        return bytes;
    }

    private sealed class Reservation : IOllamaRecordingArtifactReservation
    {
        private readonly InMemoryOllamaRecordingArtifactStore _owner;
        private int _used;
        internal Reservation(InMemoryOllamaRecordingArtifactStore owner) => _owner = owner;
        public byte[] PublishAndReadBack(ReadOnlyMemory<byte> canonicalUtf8, int maximumBytes)
        {
            if (Interlocked.Exchange(ref _used, 1) != 0) throw new OllamaRecordingArtifactStoreException("artifact_reservation_reused");
            if (canonicalUtf8.Length > maximumBytes) throw new OllamaRecordingArtifactStoreException("artifact_size_invalid");
            Interlocked.Increment(ref _owner._publishCount);
            byte[] bytes = canonicalUtf8.ToArray();
            if (Interlocked.CompareExchange(ref _owner._bytes, bytes, null) is not null) throw new OllamaRecordingArtifactStoreException("artifact_already_exists");
            return bytes.ToArray();
        }
        public void Dispose() { }
    }
}
