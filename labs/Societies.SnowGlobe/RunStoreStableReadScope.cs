using System.Text.Json;

namespace Societies.SnowGlobe;

internal enum RunStoreReadScopeFailure
{
    Invalid,
    Unavailable,
    Unstable
}

internal sealed class RunStoreReadScopeException(
    RunStoreReadScopeFailure failure,
    Exception innerException) : IOException("Run-store inspection read scope failed.", innerException)
{
    internal RunStoreReadScopeFailure Failure { get; } = failure;
}

/// <summary>
/// Pins one finite run-store layout to already-open read handles. Strict RunStore readers remain
/// the schema authority; this module owns only lexical path policy, link rejection, handle lifetime,
/// and repeatable bounded reads from offset zero.
/// </summary>
internal sealed class RunStoreStableReadScope : IDisposable
{
    private enum ArtifactLayout { Legacy, Framed, ContinuedFramed }

    private const string HeaderFileName = "run.json";
    private const string LedgerFileName = "ledger.jsonl";
    private const string MarkerFileName = "commits.jsonl";
    private const string ContinuationLedgerFileName = "ledger.0001.jsonl";
    private const string ContinuationMarkerFileName = "commits.0001.jsonl";
    private const string WriterLockFileName = ".writer.lock";

    private static readonly string[] LegacyLayout = [LedgerFileName, HeaderFileName];
    private static readonly string[] FramedLayout = [MarkerFileName, LedgerFileName, HeaderFileName];
    private static readonly string[] ContinuedFramedLayout =
        [ContinuationMarkerFileName, MarkerFileName, ContinuationLedgerFileName, LedgerFileName, HeaderFileName];

    private readonly IRunStoreReadFileSystem _source;
    private readonly string[] _entryNames;
    private readonly ArtifactLayout _layout;
    private readonly Dictionary<string, IRunStoreReadHandle> _handles;
    private readonly PinnedReadFileSystem _pinnedFiles;
    private bool _disposed;

    private RunStoreStableReadScope(
        string directoryPath,
        IRunStoreReadFileSystem source,
        string[] entryNames,
        ArtifactLayout layout,
        Dictionary<string, IRunStoreReadHandle> handles,
        byte[] initialHeaderBytes)
    {
        DirectoryPath = directoryPath;
        _source = source;
        _entryNames = entryNames;
        _layout = layout;
        _handles = handles;
        _pinnedFiles = new PinnedReadFileSystem(directoryPath, entryNames, handles, initialHeaderBytes);
    }

    internal string DirectoryPath { get; }

    internal static RunStoreStableReadScope Open(string directory, IRunStoreReadFileSystem source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(source);

        string fullDirectory;
        string[] entryNames;
        ArtifactLayout layout;
        try
        {
            fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            (entryNames, layout) = CaptureAndValidateLayout(fullDirectory, source);
        }
        catch (Exception exception) when (IsInvalidPathOrLayout(exception))
        {
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Invalid, exception);
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Unavailable, exception);
        }

        Dictionary<string, IRunStoreReadHandle> handles = new(StringComparer.Ordinal);
        byte[] initialHeaderBytes;
        string headerPath = DirectPath(fullDirectory, HeaderFileName);
        try
        {
            IRunStoreReadHandle headerHandle = source.OpenReadFile(headerPath);
            handles.Add(headerPath, headerHandle);
            initialHeaderBytes = ReadBounded(
                headerHandle,
                SnowGlobeRunStore.MaximumHeaderBytes,
                "Run identity");
        }
        catch (InvalidDataException exception)
        {
            DisposeHandles(handles.Values);
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Invalid, exception);
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            DisposeHandles(handles.Values);
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Unavailable, exception);
        }
        catch
        {
            DisposeHandles(handles.Values);
            throw;
        }

        try
        {
            SnowGlobeRunIdentity identity = SnowGlobeRunStore.ReadIdentityForInspection(initialHeaderBytes);
            bool legacy = identity.SchemaVersion is SnowGlobeRunStore.LegacySchemaVersion
                or SnowGlobeRunStore.PreviousSchemaVersion;
            if (legacy != (layout == ArtifactLayout.Legacy))
                throw new InvalidDataException("Run-store schema and finite artifact layout disagree.");
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            DisposeHandles(handles.Values);
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Invalid, exception);
        }
        catch
        {
            DisposeHandles(handles.Values);
            throw;
        }

        try
        {
            foreach (string name in ConsumedNames(layout).Where(name => name != HeaderFileName))
            {
                string path = DirectPath(fullDirectory, name);
                handles.Add(path, source.OpenReadFile(path));
            }
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            DisposeHandles(handles.Values);
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Unavailable, exception);
        }
        catch
        {
            DisposeHandles(handles.Values);
            throw;
        }

        RunStoreStableReadScope scope = new(
            fullDirectory,
            source,
            entryNames,
            layout,
            handles,
            initialHeaderBytes);
        try
        {
            scope.Revalidate();
            return scope;
        }
        catch (Exception exception) when (IsValidationFailure(exception))
        {
            scope.Dispose();
            throw new RunStoreReadScopeException(RunStoreReadScopeFailure.Unstable, exception);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    internal RunStoreReadEvidence ReadWithEvidence()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SnowGlobeRunStore.ReadWithEvidence(DirectoryPath, _pinnedFiles);
    }

    internal void Revalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        (string[] names, ArtifactLayout layout) = CaptureAndValidateLayout(DirectoryPath, _source);
        if (_layout != layout || !_entryNames.SequenceEqual(names, StringComparer.Ordinal))
            throw new InvalidDataException("Run-store layout changed during inspection.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeHandles(_handles.Values);
    }

    private static (string[] EntryNames, ArtifactLayout Layout) CaptureAndValidateLayout(
        string fullDirectory,
        IRunStoreReadFileSystem source)
    {
        ValidateDirectoryChain(fullDirectory, source);
        string[] entryNames = source.EnumerateEntryNames(fullDirectory)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (entryNames.Length != entryNames.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException("Run-store layout contains duplicate entry names.");

        string[] consumedNames = entryNames
            .Where(name => !string.Equals(name, WriterLockFileName, StringComparison.Ordinal))
            .ToArray();
        ArtifactLayout layout;
        if (MatchesLayout(consumedNames, LegacyLayout)) layout = ArtifactLayout.Legacy;
        else if (MatchesLayout(consumedNames, FramedLayout)) layout = ArtifactLayout.Framed;
        else if (MatchesLayout(consumedNames, ContinuedFramedLayout)) layout = ArtifactLayout.ContinuedFramed;
        else
        {
            throw new InvalidDataException("Run-store layout is not one finite supported artifact set.");
        }

        foreach (string name in entryNames)
        {
            if (string.IsNullOrEmpty(name) || name is "." or ".." || Path.GetFileName(name) != name)
                throw new InvalidDataException("Run-store entry name is not direct and canonical.");
            string path = DirectPath(fullDirectory, name);
            FileAttributes attributes = source.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Run-store entries cannot be links or reparse points.");
            if ((attributes & FileAttributes.Directory) != 0 || !source.FileExists(path))
                throw new InvalidDataException("Run-store entries must be ordinary files.");
        }

        return (entryNames, layout);
    }

    private static IReadOnlyList<string> ConsumedNames(ArtifactLayout layout) => layout switch
    {
        ArtifactLayout.Legacy => LegacyLayout,
        ArtifactLayout.Framed => FramedLayout,
        ArtifactLayout.ContinuedFramed => ContinuedFramedLayout,
        _ => throw new InvalidDataException("Run-store artifact layout is unsupported.")
    };

    private static void ValidateDirectoryChain(string fullDirectory, IRunStoreReadFileSystem source)
    {
        foreach (string path in BuildLexicalDirectoryChain(fullDirectory))
        {
            FileAttributes attributes = source.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Run-store path cannot traverse a link or reparse point.");
            if ((attributes & FileAttributes.Directory) == 0 || !source.DirectoryExists(path))
                throw new InvalidDataException("Run-store path must contain only existing directories.");
        }
    }

    private static IReadOnlyList<string> BuildLexicalDirectoryChain(string fullDirectory)
    {
        string root = Path.GetPathRoot(fullDirectory)
            ?? throw new InvalidDataException("Run-store directory root is invalid.");
        List<string> chain = [root];
        string relative = Path.GetRelativePath(root, fullDirectory);
        if (relative == ".") return chain;

        string current = root;
        foreach (string component in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (component is "." or "..")
                throw new InvalidDataException("Run-store directory path is not lexically canonical.");
            current = Path.Combine(current, component);
            chain.Add(Path.TrimEndingDirectorySeparator(current));
        }
        if (!string.Equals(chain[^1], fullDirectory, PathComparison))
            throw new InvalidDataException("Run-store directory path is not lexically canonical.");
        return chain;
    }

    private static string DirectPath(string directory, string name)
    {
        string path = Path.GetFullPath(Path.Combine(directory, name));
        if (!string.Equals(Path.GetDirectoryName(path), directory, PathComparison))
            throw new InvalidDataException("Run-store entry escapes its directory.");
        return path;
    }

    private static bool MatchesLayout(string[] actual, string[] expected) =>
        actual.SequenceEqual(expected, StringComparer.Ordinal);

    private static byte[] ReadBounded(
        IRunStoreReadHandle handle,
        int maximumBytes,
        string description)
    {
        byte[] bounded = new byte[checked(maximumBytes + 1)];
        int total = 0;
        while (total < bounded.Length)
        {
            int read = handle.Read(bounded.AsSpan(total), total);
            if (read == 0) break;
            total = checked(total + read);
        }
        if (total > maximumBytes) throw new InvalidDataException($"{description} exceeds the bounded byte limit.");
        return bounded[..total];
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static bool IsInvalidPathOrLayout(Exception exception) =>
        exception is InvalidDataException or ArgumentException or NotSupportedException or PathTooLongException;

    private static bool IsAccessFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private static bool IsValidationFailure(Exception exception) =>
        IsInvalidPathOrLayout(exception) || IsAccessFailure(exception);

    private static void DisposeHandles(IEnumerable<IRunStoreReadHandle> handles)
    {
        foreach (IRunStoreReadHandle handle in handles)
        {
            try { handle.Dispose(); }
            catch { }
        }
    }

    private sealed class PinnedReadFileSystem(
        string directory,
        string[] entryNames,
        IReadOnlyDictionary<string, IRunStoreReadHandle> handles,
        byte[] initialHeaderBytes) : IRunStoreReadFileSystem
    {
        private readonly string _headerPath = DirectPath(directory, HeaderFileName);
        private int _initialHeaderAvailable = 1;

        public bool DirectoryExists(string path) => string.Equals(path, directory, PathComparison);

        public IReadOnlyList<string> EnumerateEntryNames(string requestedDirectory)
        {
            if (!DirectoryExists(requestedDirectory)) throw new DirectoryNotFoundException();
            return entryNames;
        }

        public bool FileExists(string path) => handles.ContainsKey(Path.GetFullPath(path));

        public FileAttributes GetAttributes(string path) => throw new NotSupportedException();

        public IRunStoreReadHandle OpenReadFile(string path) => throw new NotSupportedException();

        public byte[] ReadFile(string path, int maximumBytes, string description)
        {
            if (maximumBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            string fullPath = Path.GetFullPath(path);
            if (!handles.TryGetValue(fullPath, out IRunStoreReadHandle? handle))
                throw new FileNotFoundException("Pinned run-store artifact is unavailable.", fullPath);
            if (string.Equals(fullPath, _headerPath, StringComparison.Ordinal)
                && Interlocked.Exchange(ref _initialHeaderAvailable, 0) == 1)
            {
                if (initialHeaderBytes.Length > maximumBytes)
                    throw new InvalidDataException($"{description} exceeds the bounded byte limit.");
                return initialHeaderBytes.ToArray();
            }
            return ReadBounded(handle, maximumBytes, description);
        }
    }
}
