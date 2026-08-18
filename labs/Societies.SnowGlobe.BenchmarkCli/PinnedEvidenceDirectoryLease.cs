using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Societies.SnowGlobe;

namespace Societies.SnowGlobe.BenchmarkCli;

/// <summary>
/// Pins the exact Windows directory objects forming the evidence path. Omitting FILE_SHARE_DELETE
/// prevents a same-user rename/delete/reparse swap until the evidence file is durably flushed.
/// </summary>
internal sealed class PinnedEvidenceDirectoryLease : IDisposable
{
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint GenericRead = 0x80000000;
    private const uint OpenExisting = 3;
    private const int MaximumFinalPathCharacters = 32_768;
    private static readonly string[] DirectorySegments = { "artifacts", "snowglobe", "local-model" };
    private readonly SafeFileHandle[] _handles;
    private bool _disposed;

    private PinnedEvidenceDirectoryLease(string evidencePath, SafeFileHandle[] handles)
    {
        EvidencePath = evidencePath;
        _handles = handles;
    }

    internal string EvidencePath { get; }

    internal static PinnedEvidenceDirectoryLease Acquire(string evidencePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new LocalModelBenchmarkException("evidence_directory_lease_platform_unsupported");
        }

        string fullPath = Path.GetFullPath(evidencePath);
        string expectedSuffix = Path.Combine(
            DirectorySegments[0],
            DirectorySegments[1],
            DirectorySegments[2],
            Path.GetFileName(PinnedBenchmarkContract.RelativeEvidencePath));
        if (!fullPath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalModelBenchmarkException("evidence_path_outside_bound");
        }

        DirectoryInfo? localModel = Directory.GetParent(fullPath);
        DirectoryInfo? snowglobe = localModel?.Parent;
        DirectoryInfo? artifacts = snowglobe?.Parent;
        DirectoryInfo? root = artifacts?.Parent;
        if (root is null
            || !string.Equals(localModel!.Name, DirectorySegments[2], StringComparison.Ordinal)
            || !string.Equals(snowglobe!.Name, DirectorySegments[1], StringComparison.Ordinal)
            || !string.Equals(artifacts!.Name, DirectorySegments[0], StringComparison.Ordinal)
            || !string.Equals(
                fullPath,
                Path.GetFullPath(Path.Combine(root.FullName, PinnedBenchmarkContract.RelativeEvidencePath)),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalModelBenchmarkException("evidence_path_outside_bound");
        }

        // The contract has exactly four directory objects: root plus three bounded descendants.
        List<SafeFileHandle> handles = new(capacity: 4);
        try
        {
            handles.Add(OpenVerifiedDirectory(root.FullName));
            PinnedBenchmarkContract.VerifyRepositoryRoot(root.FullName);

            string current = root.FullName;
            foreach (string segment in DirectorySegments)
            {
                current = Path.Combine(current, segment);
                CreateDirectoryLeafIfMissing(current);
                handles.Add(OpenVerifiedDirectory(current));
            }

            if (handles.Count != 4)
            {
                throw new LocalModelBenchmarkException("evidence_directory_lease_count_invalid");
            }

            return new PinnedEvidenceDirectoryLease(fullPath, handles.ToArray());
        }
        catch
        {
            for (int index = handles.Count - 1; index >= 0; index--)
            {
                handles[index].Dispose();
            }
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int index = _handles.Length - 1; index >= 0; index--)
        {
            _handles[index].Dispose();
        }
    }

    private static void CreateDirectoryLeafIfMissing(string path)
    {
        FileAttributes? attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            attributes = null;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LocalModelBenchmarkException("evidence_directory_invalid", exception);
        }

        if (!attributes.HasValue)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new LocalModelBenchmarkException("evidence_directory_invalid", exception);
            }
        }
    }

    private static SafeFileHandle OpenVerifiedDirectory(string expectedPath)
    {
        SafeFileHandle handle = CreateFile(
            expectedPath,
            // FILE_SHARE_DELETE is deliberately omitted below. Generic read is enough to deny
            // concurrent directory writes while remaining compatible with ordinary worktree
            // readers that do not share DELETE access.
            GenericRead,
            FileShare.Read,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new LocalModelBenchmarkException(
                "evidence_directory_lease_failed",
                new Win32Exception(error));
        }

        try
        {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
            {
                throw CreateNativeFailure("evidence_directory_lease_failed");
            }
            FileAttributes attributes = (FileAttributes)information.FileAttributes;
            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                throw new LocalModelBenchmarkException("evidence_directory_invalid");
            }
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new LocalModelBenchmarkException("evidence_path_reparse_point_rejected");
            }

            StringBuilder finalPathBuffer = new(MaximumFinalPathCharacters);
            uint finalPathLength = GetFinalPathNameByHandle(
                handle,
                finalPathBuffer,
                (uint)finalPathBuffer.Capacity,
                flags: 0);
            if (finalPathLength == 0 || finalPathLength >= finalPathBuffer.Capacity)
            {
                throw CreateNativeFailure("evidence_directory_identity_unavailable");
            }

            string actualPath = NormalizeFinalPath(finalPathBuffer.ToString());
            if (!string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedPath)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(actualPath)),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new LocalModelBenchmarkException("evidence_directory_identity_mismatch");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static string NormalizeFinalPath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string extendedPrefix = @"\\?\";
        if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[uncPrefix.Length..];
        }
        return path.StartsWith(extendedPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[extendedPrefix.Length..]
            : path;
    }

    private static LocalModelBenchmarkException CreateNativeFailure(string code) =>
        new(code, new Win32Exception(Marshal.GetLastWin32Error()));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
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
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathCharacters,
        uint flags);

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
