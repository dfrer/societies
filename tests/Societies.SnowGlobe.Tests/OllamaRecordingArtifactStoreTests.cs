using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OllamaRecordingArtifactStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "societies-recording-store-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReserveCreatesFixedAncestorsAndDurableExactReadbackWithoutOverwrite()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); FileOllamaRecordingArtifactStore store = new(); byte[] payload = "{\"test\":true}"u8.ToArray();
        using (IOllamaRecordingArtifactReservation reservation = store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath))
            Assert.Equal(payload, reservation.PublishAndReadBack(payload, OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes));
        Assert.Equal(payload, store.ReadBounded(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, 1024));
        OllamaRecordingArtifactStoreException collision = Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath));
        Assert.Equal("artifact_already_exists", collision.Code);
    }

    [Fact]
    public void EmptyReservationIsPreservedAsTombstoneAndNeverDeletedOrRetried()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); FileOllamaRecordingArtifactStore store = new();
        using (store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath)) { }
        string path = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path)); Assert.Equal(0, new FileInfo(path).Length);
        Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RootMarkersAndFixedPathFailClosedBeforeOutsideWrite()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_root); FileOllamaRecordingArtifactStore store = new();
        Assert.Equal("repository_root_not_verified", Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath)).Code);
        CreateRepositoryMarkers();
        Assert.Equal("artifact_path_outside_bound", Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.Reserve(_root, "..\\outside.json")).Code);
        Assert.False(File.Exists(Path.Combine(_root, "outside.json")));
    }

    [Fact]
    public void ReparseAncestorIsRejectedWhereWindowsAllowsCreation()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); string target = Path.Combine(_root, "target"); Directory.CreateDirectory(target);
        string artifacts = Path.Combine(_root, "artifacts");
        try { Directory.CreateSymbolicLink(artifacts, target); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException) { return; }
        FileOllamaRecordingArtifactStore store = new();
        OllamaRecordingArtifactStoreException rejected = Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath));
        Assert.Contains(rejected.Code, new[] { "artifact_path_reparse_point_rejected", "artifact_directory_invalid", "artifact_directory_identity_mismatch" });
        Assert.Empty(Directory.EnumerateFiles(target));
    }

    [Fact]
    public void HeldDirectoryIdentitiesBlockAncestorSwapAndInPlaceReplacementThroughPublication()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); string artifacts = Path.Combine(_root, "artifacts"); string moved = Path.Combine(_root, "moved-artifacts"); string localModel = Path.Combine(artifacts, "snowglobe", "local-model");
        bool ancestorSwapBlocked = false; bool inPlaceReplacementBlocked = false;
        FileOllamaRecordingArtifactStore store = new(() =>
        {
            try { Directory.Move(artifacts, moved); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { ancestorSwapBlocked = true; }
            try { Directory.Delete(localModel); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { inPlaceReplacementBlocked = true; }
        });
        using IOllamaRecordingArtifactReservation reservation = store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath);
        Assert.True(ancestorSwapBlocked); Assert.True(inPlaceReplacementBlocked);
        byte[] bytes = "{}"u8.ToArray(); Assert.Equal(bytes, reservation.PublishAndReadBack(bytes, 1024));
        Assert.False(Directory.Exists(moved)); Assert.True(File.Exists(Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void BoundedReadRejectsFinalFileHardlinkReplacementBeforeOutsideBytesAreRead()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); string fixedPath = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(fixedPath)!);
        string outside = Path.Combine(_root, "outside-sensitive.json"); File.WriteAllText(outside, "outside-sensitive-bytes");
        if (!CreateHardLink(fixedPath, outside, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        FileOllamaRecordingArtifactStore store = new();
        Assert.Throws<OllamaRecordingArtifactStoreException>(() => store.ReadBounded(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, 1024));
    }

    [Fact]
    public void PinnedFinalHandleBlocksRenameAndReparseSwapAndReadsOnlyAdmittedBytes()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers();
        byte[] admitted = "admitted-artifact"u8.ToArray();
        string fixedPath = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        string movedPath = Path.Combine(Path.GetDirectoryName(fixedPath)!, "moved.json");
        string outsidePath = Path.Combine(_root, "outside-sensitive.json");
        File.WriteAllText(outsidePath, "outside-sensitive-bytes");
        FileOllamaRecordingArtifactStore writer = new();
        using (IOllamaRecordingArtifactReservation reservation = writer.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath))
            _ = reservation.PublishAndReadBack(admitted, 1024);

        bool replacementBlocked = false;
        FileOllamaRecordingArtifactStore reader = new(null, () =>
        {
            try
            {
                File.Move(fixedPath, movedPath);
                File.CreateSymbolicLink(fixedPath, outsidePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                replacementBlocked = true;
            }
        });

        Assert.Equal(admitted, reader.ReadBounded(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, 1024));
        Assert.True(replacementBlocked);
        Assert.False(File.Exists(movedPath));
        Assert.Equal("outside-sensitive-bytes", File.ReadAllText(outsidePath));
    }

    [Fact]
    public void StreamDisposeFailureStillReleasesRealDirectoryLeaseExactlyOnceWithCodeOnlyException()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers();
        int leaseDisposeCount = 0; const string secret = @"C:\attacker\raw-os-message";
        FileOllamaRecordingArtifactStore store = new(null, null,
            () => throw new IOException(secret),
            () => Interlocked.Increment(ref leaseDisposeCount));
        IOllamaRecordingArtifactReservation reservation = store.Reserve(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath);
        OllamaRecordingArtifactStoreException failure = Assert.Throws<OllamaRecordingArtifactStoreException>(() => reservation.Dispose());
        reservation.Dispose();
        Assert.Equal("artifact_reservation_dispose_failed", failure.Code); Assert.Null(failure.InnerException); Assert.Equal(failure.Code, failure.Message);
        Assert.DoesNotContain(secret, failure.ToString(), StringComparison.Ordinal); Assert.Equal(1, leaseDisposeCount);

        string artifacts = Path.Combine(_root, "artifacts"); string moved = Path.Combine(_root, "released-artifacts");
        Directory.Move(artifacts, moved);
        Assert.True(Directory.Exists(moved));
    }

    private void CreateRepositoryMarkers()
    {
        Directory.CreateDirectory(_root); File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: offline-test"); File.WriteAllText(Path.Combine(_root, "CURRENT_BUILD.md"), "# offline test");
        string lab = Path.Combine(_root, "labs", "Societies.SnowGlobe"); Directory.CreateDirectory(lab); File.WriteAllText(Path.Combine(lab, "Societies.SnowGlobe.csproj"), "<Project />");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
