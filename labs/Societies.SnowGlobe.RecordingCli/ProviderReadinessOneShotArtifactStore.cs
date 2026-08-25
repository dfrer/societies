using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

internal static class ProviderReadinessFixedRepositoryLocator
{
    private const int MaximumAncestorCount = 16;

    internal static string FindVerifiedRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int index = 0; current is not null && index < MaximumAncestorCount; index++, current = current.Parent)
        {
            if (!File.Exists(Path.Combine(current.FullName, "CURRENT_BUILD.md"))
                || !File.Exists(Path.Combine(current.FullName, "labs", "Societies.SnowGlobe", "Societies.SnowGlobe.csproj"))
                || !File.Exists(Path.Combine(current.FullName, ".git")) && !Directory.Exists(Path.Combine(current.FullName, ".git")))
                continue;
            try
            {
                string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(current.FullName));
                FileOllamaRecordingArtifactStore.VerifyRepositoryRoot(root);
                return root;
            }
            catch (OllamaRecordingArtifactStoreException) { }
        }
        throw new ProviderReadinessOneShotException("artifact_path_rejected");
    }
}

/// <summary>
/// Fixed-path Windows store. A durable claim is published before provider access. The three evidence
/// files are created under a fixed pending child, then exposed together by a same-volume directory
/// rename. Any uncertainty leaves the claim and therefore permanently closes this governed attempt.
/// </summary>
internal sealed class FileProviderReadinessOneShotArtifactStore
    : IProviderReadinessOneShotArtifactStore
{
    private const string ComparisonRelativePath =
        "artifacts/snowglobe/cognition-quality/provider-comparison-v1.json";
    private const string ReadinessRelativeDirectory =
        "artifacts/snowglobe/provider-readiness";
    private const string PendingDirectoryName = ".evidence-v1.pending";
    private const string PublishedDirectoryName = "evidence-v1";
    private const string ClaimFileName = "one-shot-consumed-v1.json";
    private const string OpenRouterFileName = "openrouter-observation-v1.json";
    private const string OllamaFileName = "ollama-observation-v1.json";
    private const string AssessmentFileName = "routing-readiness-assessment-v2.json";
    private const int ErrorAlreadyExists = 183;

    private readonly string _repositoryRoot;
    private readonly string _readinessRoot;
    private readonly string _pendingRoot;
    private readonly string _publishedRoot;
    private readonly Action<string>? _publicationCheckpointForTesting;
    private FileOpenRouterPremiumDirectoryIdentity? _claimedDirectoryIdentity;
    private FileOpenRouterPremiumFileIdentity? _claimFileIdentity;
    private int _claimed;
    private int _publicationEntered;

    internal FileProviderReadinessOneShotArtifactStore(string absoluteRepositoryRoot)
        : this(absoluteRepositoryRoot, null) { }

    internal FileProviderReadinessOneShotArtifactStore(
        string absoluteRepositoryRoot,
        Action<string>? publicationCheckpointForTesting)
    {
        if (!OperatingSystem.IsWindows())
            throw new ProviderReadinessOneShotException("artifact_store_platform_unsupported");
        if (string.IsNullOrWhiteSpace(absoluteRepositoryRoot)
            || !Path.IsPathFullyQualified(absoluteRepositoryRoot))
            throw new ProviderReadinessOneShotException("artifact_path_rejected");
        try
        {
            _repositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(absoluteRepositoryRoot));
            FileOllamaRecordingArtifactStore.VerifyRepositoryRoot(_repositoryRoot);
            _readinessRoot = FixedPath(ReadinessRelativeDirectory);
            _pendingRoot = Path.Combine(_readinessRoot, PendingDirectoryName);
            _publishedRoot = Path.Combine(_readinessRoot, PublishedDirectoryName);
            _publicationCheckpointForTesting = publicationCheckpointForTesting;
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("artifact_path_rejected", exception);
        }
    }

    public void ClaimOnce(ReadOnlyMemory<byte> canonicalClaim)
    {
        if (Interlocked.Exchange(ref _claimed, 1) != 0)
            throw new ProviderReadinessOneShotException("invocation_already_consumed");
        _ = ProviderReadinessOneShotClaimCodec.ValidateForCurrentBuild(canonicalClaim);

        byte[] owned = canonicalClaim.Span.ToArray();
        try
        {
            using DirectoryHandleSet ancestors = OpenExistingAncestors(
                ["artifacts", "snowglobe"], mutationLeaseOnLeaf: true);
            EnsureReadinessRootCreated();
            using SafeFileHandle readinessMutation =
                FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_readinessRoot);
            VerifyFixedDirectoryPath(_readinessRoot);
            RefuseExistingAttemptTargets();

            string claimPath = Path.Combine(_readinessRoot, ClaimFileName);
            try
            {
                using FileStream claim = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
                    claimPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                claim.Write(owned);
                claim.Flush(flushToDisk: true);
                claim.Position = 0;
                byte[] readback = new byte[owned.Length];
                try
                {
                    claim.ReadExactly(readback);
                    if (claim.ReadByte() != -1 || !readback.AsSpan().SequenceEqual(owned))
                        throw new ProviderReadinessOneShotException("invocation_claim_ambiguous");
                    _ = ProviderReadinessOneShotClaimCodec.ValidateForCurrentBuild(readback);
                    using SafeFileHandle pinnedDirectory =
                        FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_readinessRoot);
                    FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                        _readinessRoot, pinnedDirectory, ClaimFileName, claim);
                    _claimFileIdentity = FileOpenRouterPremiumIdentity.CaptureFileIdentity(claim);
                    _claimedDirectoryIdentity =
                        FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_readinessRoot);
                }
                finally { CryptographicOperations.ZeroMemory(readback); }
            }
            catch (ProviderReadinessOneShotException) { throw; }
            catch (IOException exception) when (PathExistsNoFollow(claimPath))
            {
                throw new ProviderReadinessOneShotException("invocation_already_consumed", exception);
            }
            catch (Exception exception)
            {
                throw new ProviderReadinessOneShotException("invocation_claim_ambiguous", exception);
            }
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("invocation_claim_ambiguous", exception);
        }
        finally { CryptographicOperations.ZeroMemory(owned); }
    }

    public byte[] ReadAcceptedComparisonArtifact()
    {
        RequireClaimed();
        string path = FixedPath(ComparisonRelativePath);
        try
        {
            using DirectoryHandleSet ancestors = OpenExistingAncestors(
                ["artifacts", "snowglobe", "cognition-quality"], mutationLeaseOnLeaf: false);
            VerifyClaimIdentity();
            using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
            long length = input.Length;
            if (length is < 1 or > CognitionQualityComparisonModule.MaximumArtifactBytes)
                throw new ProviderReadinessOneShotException("comparison_evidence_rejected");
            byte[] bytes = new byte[checked((int)length)];
            try
            {
                input.ReadExactly(bytes);
                if (input.ReadByte() != -1)
                    throw new ProviderReadinessOneShotException("comparison_evidence_rejected");
                FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                    Path.GetDirectoryName(path)!, ancestors.Leaf, Path.GetFileName(path), input);
                return bytes;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("comparison_evidence_rejected", exception);
        }
    }

    public void PublishAtomically(ProviderReadinessOneShotArtifacts artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        RequireClaimed();
        if (Interlocked.Exchange(ref _publicationEntered, 1) != 0)
            throw new ProviderReadinessOneShotException("invocation_already_consumed");

        int createdFileCount = 0;
        bool renameEntered = false;
        try
        {
            VerifyClaimIdentity();
            FileOpenRouterPremiumDirectoryIdentity pendingIdentity;
            FileOpenRouterPremiumFileIdentity openRouterIdentity;
            FileOpenRouterPremiumFileIdentity ollamaIdentity;
            FileOpenRouterPremiumFileIdentity assessmentIdentity;
            using (SafeFileHandle readinessMutation =
                FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_readinessRoot))
            {
                RefuseExistingPublicationTargets();
                CreateDirectoryNew(_pendingRoot);
                _publicationCheckpointForTesting?.Invoke("pending_created");

                pendingIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_pendingRoot);
                openRouterIdentity = WriteFixedFile(
                    _pendingRoot,
                    OpenRouterFileName,
                    artifacts.OpenRouterObservationCanonicalUtf8);
                createdFileCount++;
                _publicationCheckpointForTesting?.Invoke("openrouter_written");
                ollamaIdentity = WriteFixedFile(
                    _pendingRoot,
                    OllamaFileName,
                    artifacts.OllamaObservationCanonicalUtf8);
                createdFileCount++;
                _publicationCheckpointForTesting?.Invoke("ollama_written");
                assessmentIdentity = WriteFixedFile(
                    _pendingRoot,
                    AssessmentFileName,
                    artifacts.AssessmentCanonicalUtf8);
                createdFileCount++;
                _publicationCheckpointForTesting?.Invoke("assessment_written");

                VerifyPendingFiles(
                    _pendingRoot,
                    openRouterIdentity,
                    ollamaIdentity,
                    assessmentIdentity,
                    artifacts);
                VerifyClaimIdentity();
            }

            renameEntered = true;
            Directory.Move(_pendingRoot, _publishedRoot);
            _publicationCheckpointForTesting?.Invoke("directory_renamed");

            FileOpenRouterPremiumDirectoryIdentity publishedIdentity =
                FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_publishedRoot);
            if (publishedIdentity != pendingIdentity)
                throw new ProviderReadinessOneShotException("artifact_publication_ambiguous");
            _publicationCheckpointForTesting?.Invoke("published_identity_verified");
            VerifyPendingFiles(
                _publishedRoot,
                openRouterIdentity,
                ollamaIdentity,
                assessmentIdentity,
                artifacts);
            _publicationCheckpointForTesting?.Invoke("published_files_verified");
            VerifyClaimIdentity();
            _publicationCheckpointForTesting?.Invoke("publication_verified");
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException(
                renameEntered ? "artifact_publication_ambiguous"
                    : createdFileCount > 0 ? "artifact_publication_partial"
                    : "artifact_publication_ambiguous",
                exception);
        }
    }

    private FileOpenRouterPremiumFileIdentity WriteFixedFile(
        string directory,
        string fileName,
        ReadOnlyMemory<byte> canonicalUtf8)
    {
        byte[] owned = canonicalUtf8.Span.ToArray();
        try
        {
            using SafeFileHandle directoryMutation =
                FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(directory);
            using FileStream output = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
                Path.Combine(directory, fileName),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                4096,
                FileOptions.WriteThrough);
            output.Write(owned);
            output.Flush(flushToDisk: true);
            output.Position = 0;
            byte[] readback = new byte[owned.Length];
            try
            {
                output.ReadExactly(readback);
                if (output.ReadByte() != -1 || !readback.AsSpan().SequenceEqual(owned))
                    throw new ProviderReadinessOneShotException("artifact_publication_partial");
                using SafeFileHandle pinnedDirectory =
                    FileOpenRouterPremiumIdentity.OpenDirectoryPinned(directory);
                FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                    directory, pinnedDirectory, fileName, output);
                return FileOpenRouterPremiumIdentity.CaptureFileIdentity(output);
            }
            finally { CryptographicOperations.ZeroMemory(readback); }
        }
        finally { CryptographicOperations.ZeroMemory(owned); }
    }

    private static void VerifyPendingFiles(
        string directory,
        FileOpenRouterPremiumFileIdentity openRouterIdentity,
        FileOpenRouterPremiumFileIdentity ollamaIdentity,
        FileOpenRouterPremiumFileIdentity assessmentIdentity,
        ProviderReadinessOneShotArtifacts expected)
    {
        VerifyFixedFile(directory, OpenRouterFileName, openRouterIdentity,
            expected.OpenRouterObservationCanonicalUtf8);
        VerifyFixedFile(directory, OllamaFileName, ollamaIdentity,
            expected.OllamaObservationCanonicalUtf8);
        VerifyFixedFile(directory, AssessmentFileName, assessmentIdentity,
            expected.AssessmentCanonicalUtf8);
        string[] names = Directory.EnumerateFileSystemEntries(directory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        string[] expectedNames = [AssessmentFileName, OllamaFileName, OpenRouterFileName];
        Array.Sort(expectedNames, StringComparer.Ordinal);
        if (!names.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new ProviderReadinessOneShotException("artifact_publication_ambiguous");
    }

    private static void VerifyFixedFile(
        string directory,
        string fileName,
        FileOpenRouterPremiumFileIdentity identity,
        ReadOnlyMemory<byte> expected)
    {
        FileOpenRouterPremiumIdentity.VerifySingleFileIdentity(directory, fileName, identity);
        using SafeFileHandle pinnedDirectory = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(directory);
        using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
            Path.Combine(directory, fileName), FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.SequentialScan);
        if (input.Length != expected.Length)
            throw new ProviderReadinessOneShotException("artifact_publication_ambiguous");
        byte[] readback = new byte[expected.Length];
        try
        {
            input.ReadExactly(readback);
            if (input.ReadByte() != -1 || !readback.AsSpan().SequenceEqual(expected.Span))
                throw new ProviderReadinessOneShotException("artifact_publication_ambiguous");
            FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                directory, pinnedDirectory, fileName, input);
        }
        finally { CryptographicOperations.ZeroMemory(readback); }
    }

    private void VerifyClaimIdentity()
    {
        if (_claimedDirectoryIdentity is not FileOpenRouterPremiumDirectoryIdentity directoryIdentity
            || _claimFileIdentity is not FileOpenRouterPremiumFileIdentity fileIdentity)
            throw new ProviderReadinessOneShotException("invocation_claim_ambiguous");
        try
        {
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_readinessRoot, directoryIdentity);
            FileOpenRouterPremiumIdentity.VerifySingleFileIdentity(
                _readinessRoot, ClaimFileName, fileIdentity);
            byte[] claim = ReadFixedSmallFile(
                _readinessRoot,
                ClaimFileName,
                ProviderReadinessOneShotClaimCodec.MaximumBytes);
            try { _ = ProviderReadinessOneShotClaimCodec.ValidateForCurrentBuild(claim); }
            finally { CryptographicOperations.ZeroMemory(claim); }
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("invocation_claim_ambiguous", exception);
        }
    }

    private static byte[] ReadFixedSmallFile(string directory, string fileName, int maximumBytes)
    {
        using SafeFileHandle pinnedDirectory = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(directory);
        using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
            Path.Combine(directory, fileName), FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.SequentialScan);
        if (input.Length is < 1 || input.Length > maximumBytes)
            throw new ProviderReadinessOneShotException("artifact_read_failed");
        byte[] bytes = new byte[checked((int)input.Length)];
        try
        {
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1)
                throw new ProviderReadinessOneShotException("artifact_read_failed");
            FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                directory, pinnedDirectory, fileName, input);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private void EnsureReadinessRootCreated()
    {
        FileAttributes? attributes = TryGetAttributes(_readinessRoot);
        if (!attributes.HasValue)
            CreateDirectoryNew(_readinessRoot);
        else if (!attributes.Value.HasFlag(FileAttributes.Directory)
            || attributes.Value.HasFlag(FileAttributes.ReparsePoint))
            throw new ProviderReadinessOneShotException("artifact_path_reparse_rejected");
        VerifyFixedDirectoryPath(_readinessRoot);
    }

    private void RefuseExistingAttemptTargets()
    {
        string claim = Path.Combine(_readinessRoot, ClaimFileName);
        foreach (string path in new[] { claim, _pendingRoot, _publishedRoot })
        {
            FileAttributes? attributes = TryGetAttributes(path);
            if (!attributes.HasValue) continue;
            if (attributes.Value.HasFlag(FileAttributes.ReparsePoint))
                throw new ProviderReadinessOneShotException("artifact_path_reparse_rejected");
            if (string.Equals(path, claim, StringComparison.OrdinalIgnoreCase))
                throw new ProviderReadinessOneShotException("invocation_already_consumed");
            throw new ProviderReadinessOneShotException("artifact_target_exists");
        }
    }

    private void RefuseExistingPublicationTargets()
    {
        foreach (string path in new[] { _pendingRoot, _publishedRoot })
        {
            FileAttributes? attributes = TryGetAttributes(path);
            if (!attributes.HasValue) continue;
            if (attributes.Value.HasFlag(FileAttributes.ReparsePoint))
                throw new ProviderReadinessOneShotException("artifact_path_reparse_rejected");
            throw new ProviderReadinessOneShotException("artifact_target_exists");
        }
    }

    private DirectoryHandleSet OpenExistingAncestors(
        IReadOnlyList<string> segments,
        bool mutationLeaseOnLeaf)
    {
        List<SafeFileHandle> handles = [];
        try
        {
            FileOllamaRecordingArtifactStore.VerifyRepositoryRoot(_repositoryRoot);
            handles.Add(FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_repositoryRoot));
            string current = _repositoryRoot;
            for (int index = 0; index < segments.Count; index++)
            {
                current = Path.Combine(current, segments[index]);
                VerifyFixedDirectoryPath(current);
                bool leaf = index == segments.Count - 1;
                handles.Add(leaf && mutationLeaseOnLeaf
                    ? FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(current)
                    : FileOpenRouterPremiumIdentity.OpenDirectoryPinned(current));
            }
            return new DirectoryHandleSet(handles);
        }
        catch
        {
            for (int index = handles.Count - 1; index >= 0; index--)
                handles[index].Dispose();
            throw;
        }
    }

    private string FixedPath(string relativePath)
    {
        string combined = Path.GetFullPath(Path.Combine(
            _repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(_repositoryRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new ProviderReadinessOneShotException("artifact_path_rejected");
        return combined;
    }

    private static void CreateDirectoryNew(string path)
    {
        if (CreateDirectoryW(path, IntPtr.Zero)) return;
        int error = Marshal.GetLastWin32Error();
        throw new ProviderReadinessOneShotException(
            error == ErrorAlreadyExists ? "artifact_target_exists" : "artifact_publication_ambiguous",
            new Win32Exception(error));
    }

    private static void VerifyFixedDirectoryPath(string path)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue || !attributes.Value.HasFlag(FileAttributes.Directory))
            throw new ProviderReadinessOneShotException("artifact_path_rejected");
        if (attributes.Value.HasFlag(FileAttributes.ReparsePoint))
            throw new ProviderReadinessOneShotException("artifact_path_reparse_rejected");
        string actual = Path.TrimEndingDirectorySeparator(
            FileOpenRouterPremiumIdentity.GetCanonicalDirectoryPath(path));
        string expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new ProviderReadinessOneShotException("artifact_path_rejected");
    }

    private static bool PathExistsNoFollow(string path)
    {
        FileAttributes? attributes = TryGetAttributes(path);
        if (!attributes.HasValue) return false;
        if (attributes.Value.HasFlag(FileAttributes.ReparsePoint))
            throw new ProviderReadinessOneShotException("artifact_path_reparse_rejected");
        return true;
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProviderReadinessOneShotException("artifact_path_rejected", exception);
        }
    }

    private void RequireClaimed()
    {
        if (Volatile.Read(ref _claimed) == 0)
            throw new ProviderReadinessOneShotException("invocation_claim_ambiguous");
    }

    private sealed class DirectoryHandleSet(List<SafeFileHandle> handles) : IDisposable
    {
        internal SafeFileHandle Leaf => handles[^1];
        public void Dispose()
        {
            for (int index = handles.Count - 1; index >= 0; index--)
                handles[index].Dispose();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string pathName, IntPtr securityAttributes);
}
