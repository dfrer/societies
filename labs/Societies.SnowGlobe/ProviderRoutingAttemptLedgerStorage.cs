using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

internal interface IProviderRoutingAttemptLedgerStorageFault
{
    int BytesToWrite(string boundary, int totalBytes) => totalBytes;
    void BeforeClaimPreparation() { }
    void BeforeClaimTombstone() { }
    void BeforeRecoveryUnknownWrite() { }
    void AfterWriteBeforeFlush(string boundary) { }
    void AfterFlushBeforeReadback(string boundary) { }
    void BeforeReadback(string boundary) { }
}

internal sealed class InMemoryProviderRoutingAttemptLedgerStorage
    : IProviderRoutingAttemptLedgerStorage
{
    private readonly object _gate = new();
    private readonly IProviderRoutingAttemptIntegrityAnchor _anchor;
    private readonly Dictionary<string, AttemptFiles> _attempts = new(StringComparer.Ordinal);

    internal InMemoryProviderRoutingAttemptLedgerStorage(
        IProviderRoutingAttemptIntegrityAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(anchor.IdentitySha256))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        _anchor = anchor;
    }

    public string IntegrityAnchorIdentitySha256 => _anchor.IdentitySha256;

    public void CreateNew(string attemptId, ReadOnlySpan<byte> initialRecordCanonicalUtf8)
    {
        lock (_gate)
        {
            if (_attempts.ContainsKey(attemptId))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_already_exists");
            ProviderRoutingAttemptRecord initial = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                initialRecordCanonicalUtf8, _anchor);
            if (initial.AttemptId != attemptId || initial.State != ProviderRoutingAttemptState.NotStarted)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
            _attempts.Add(attemptId, new(initialRecordCanonicalUtf8.ToArray()));
        }
    }

    public byte[] ReadCurrent(string attemptId)
    {
        lock (_gate)
        {
            if (!_attempts.TryGetValue(attemptId, out AttemptFiles? files))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_missing");
            return Resolve(files).ToArray();
        }
    }

    public byte[] ClaimOnce(
        string attemptId,
        string expectedRecordDigestSha256,
        ReadOnlySpan<byte> tombstoneCanonicalUtf8,
        ReadOnlySpan<byte> dispatchRecordCanonicalUtf8,
        ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
    {
        lock (_gate)
        {
            byte[]? retainedTombstone = null;
            byte[]? retainedDispatch = null;
            bool terminalMaterialCreated = false;
            try
            {
                if (!_attempts.TryGetValue(attemptId, out AttemptFiles? files))
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_missing");
                ProviderRoutingAttemptRecord current = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    Resolve(files), _anchor);
                if (current.State != ProviderRoutingAttemptState.NotStarted)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_already_terminal");
                if (current.CanonicalDigestSha256 != expectedRecordDigestSha256)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_expected_record_mismatch");
                ProviderRoutingAttemptClaimTombstone tombstone =
                    ProviderRoutingAttemptLedgerCodec.ValidateTombstone(tombstoneCanonicalUtf8, _anchor);
                ProviderRoutingAttemptRecord dispatch = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    dispatchRecordCanonicalUtf8, _anchor);
                ProviderRoutingAttemptRecord unknown = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    unknownRecordCanonicalUtf8, _anchor);
                ValidatePrepared(current, tombstone, dispatch, unknown);
                retainedTombstone = tombstoneCanonicalUtf8.ToArray();
                retainedDispatch = dispatchRecordCanonicalUtf8.ToArray();
                files.Tombstone = retainedTombstone;
                retainedTombstone = null;
                terminalMaterialCreated = true;
                files.Dispatch = retainedDispatch;
                retainedDispatch = null;
                return files.Dispatch.ToArray();
            }
            catch (ProviderRoutingAttemptLedgerException exception) when (
                IsKnownClaimFailure(exception.Code))
            {
                throw;
            }
            catch (ProviderRoutingAttemptStorageClaimException) { throw; }
            catch
            {
                throw new ProviderRoutingAttemptStorageClaimException(terminalMaterialCreated
                    ? ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown
                    : ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone);
            }
            finally
            {
                if (retainedTombstone is not null)
                    CryptographicOperations.ZeroMemory(retainedTombstone);
                if (retainedDispatch is not null)
                    CryptographicOperations.ZeroMemory(retainedDispatch);
            }
        }
    }

    private static bool IsKnownClaimFailure(string code) => code is
        "attempt_missing" or "attempt_already_terminal" or
        "attempt_expected_record_mismatch" or "attempt_poisoned";

    private ReadOnlySpan<byte> Resolve(AttemptFiles files)
    {
        ProviderRoutingAttemptRecord initial = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
            files.Initial, _anchor);
        if (files.Tombstone is null)
        {
            if (files.Dispatch is not null || files.Unknown is not null)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            return files.Initial;
        }
        ProviderRoutingAttemptClaimTombstone tombstone =
            ProviderRoutingAttemptLedgerCodec.ValidateTombstone(files.Tombstone, _anchor);
        if (tombstone.AttemptId != initial.AttemptId
            || tombstone.ExpectedRecordDigestSha256 != initial.CanonicalDigestSha256)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
        if (files.Dispatch is not null && files.Unknown is not null)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
        if (files.Dispatch is not null)
        {
            ProviderRoutingAttemptRecord dispatch = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                files.Dispatch, _anchor);
            ValidateTerminal(initial, tombstone, dispatch, ProviderRoutingAttemptState.DispatchStarted);
            return files.Dispatch;
        }
        if (files.Unknown is null)
        {
            ProviderRoutingAttemptRecord unknown =
                ProviderRoutingAttemptLedgerCodec.CreateUnknownFromTombstone(initial, tombstone, _anchor);
            files.Unknown = unknown.CanonicalUtf8.ToArray();
        }
        ProviderRoutingAttemptRecord retainedUnknown = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
            files.Unknown, _anchor);
        ValidateTerminal(initial, tombstone, retainedUnknown, ProviderRoutingAttemptState.SubmissionUnknown);
        return files.Unknown;
    }

    private static void ValidatePrepared(
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingAttemptClaimTombstone tombstone,
        ProviderRoutingAttemptRecord dispatch,
        ProviderRoutingAttemptRecord unknown)
    {
        ValidateTerminal(initial, tombstone, dispatch, ProviderRoutingAttemptState.DispatchStarted);
        ValidateTerminal(initial, tombstone, unknown, ProviderRoutingAttemptState.SubmissionUnknown);
        if (dispatch.RoutingDecisionDigestSha256 != unknown.RoutingDecisionDigestSha256
            || dispatch.SelectedProvider != unknown.SelectedProvider
            || dispatch.ClaimedAtUnixMilliseconds != unknown.ClaimedAtUnixMilliseconds)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
    }

    internal static void ValidateTerminal(
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingAttemptClaimTombstone tombstone,
        ProviderRoutingAttemptRecord terminal,
        ProviderRoutingAttemptState expectedState)
    {
        string provider = terminal.SelectedProvider switch
        {
            ProviderRoutingSelectedProvider.OpenRouter => "openrouter",
            ProviderRoutingSelectedProvider.Ollama => "ollama",
            _ => "invalid"
        };
        if (terminal.State != expectedState
            || terminal.AttemptId != initial.AttemptId
            || terminal.PreviousRecordDigestSha256 != initial.CanonicalDigestSha256
            || tombstone.AttemptId != initial.AttemptId
            || tombstone.ExpectedRecordDigestSha256 != initial.CanonicalDigestSha256
            || terminal.RoutingDecisionDigestSha256 != tombstone.RoutingDecisionDigestSha256
            || terminal.ComparisonArtifactDigestSha256 != initial.ComparisonArtifactDigestSha256
            || terminal.ReadinessAssessmentDigestSha256 != initial.ReadinessAssessmentDigestSha256
            || terminal.ReadinessAssessmentSchemaVersion != initial.ReadinessAssessmentSchemaVersion
            || terminal.OpenRouterReadinessCode != initial.OpenRouterReadinessCode
            || terminal.OllamaReadinessCode != initial.OllamaReadinessCode
            || terminal.IntentCode != initial.IntentCode
            || provider != tombstone.SelectedProviderCode
            || terminal.ClaimedAtUnixMilliseconds != tombstone.ClaimedAtUnixMilliseconds)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
    }

    private sealed class AttemptFiles(byte[] initial)
    {
        internal byte[] Initial { get; } = initial;
        internal byte[]? Tombstone { get; set; }
        internal byte[]? Dispatch { get; set; }
        internal byte[]? Unknown { get; set; }
    }
}

/// <summary>
/// Windows-only internal file Adapter over a caller-injected verified absolute root and
/// provider-neutral integrity anchor. It exposes no default or real retained root factory.
/// </summary>
internal sealed class FileProviderRoutingAttemptLedgerStorage
    : IProviderRoutingAttemptLedgerStorage, IDisposable
{
    internal const string DispatchRecordWriteBoundary = "dispatch_record_after_write_before_flush";
    internal const string DispatchRecordFlushBoundary = "dispatch_record_after_flush_before_readback";
    internal const string DispatchRecordReadbackBoundary = "dispatch_record_before_readback";
    internal const string ClaimTombstoneWriteBoundary = "claim_tombstone_after_write_before_flush";
    internal const string ClaimTombstoneFlushBoundary = "claim_tombstone_after_flush_before_readback";
    internal const string ClaimTombstoneReadbackBoundary = "claim_tombstone_before_readback";

    private const string AttemptsDirectoryName = "attempts";
    private const string WriterLockFileName = "writer.lock";
    private const string InitialFileName = "record-00000000.json";
    private const string TombstoneFileName = "dispatch-claim-tombstone.json";
    private const string DispatchFileName = "record-00000001-dispatch-started.json";
    private const string UnknownFileName = "record-00000001-submission-unknown.json";
    private static readonly byte[] WriterLockBytes = Encoding.ASCII.GetBytes(
        "{\"schema_version\":\"snow_globe_provider_routing_attempt_writer_lock/v1\",\"exclusive_writer\":true}");

    private readonly object _gate = new();
    private readonly string _root;
    private readonly string _attempts;
    private readonly IProviderRoutingAttemptIntegrityAnchor _anchor;
    private readonly IProviderRoutingAttemptLedgerStorageFault _fault;
    private readonly FileOpenRouterPremiumDirectoryIdentity _rootIdentity;
    private readonly FileOpenRouterPremiumDirectoryIdentity _attemptsIdentity;
    private readonly SafeFileHandle _rootHandle;
    private readonly SafeFileHandle _attemptsHandle;
    private readonly FileStream _writerLease;
    private bool _disposed;

    internal FileProviderRoutingAttemptLedgerStorage(
        string verifiedAbsoluteRoot,
        IProviderRoutingAttemptIntegrityAnchor anchor,
        IProviderRoutingAttemptLedgerStorageFault? fault = null)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        ArgumentNullException.ThrowIfNull(verifiedAbsoluteRoot);
        ArgumentNullException.ThrowIfNull(anchor);
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(anchor.IdentitySha256))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        _anchor = anchor;
        _fault = fault ?? NoStorageFault.Instance;
        _root = ValidateRoot(verifiedAbsoluteRoot);
        _attempts = Path.Combine(_root, AttemptsDirectoryName);
        SafeFileHandle? rootHandle = null;
        SafeFileHandle? attemptsHandle = null;
        FileStream? writerLease = null;
        try
        {
            using (SafeFileHandle mutationLease = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_root))
            {
                RequireCanonicalPath(_root);
                _rootIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_root);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, mutationLease);
                if (!Directory.Exists(_attempts)) Directory.CreateDirectory(_attempts);
                _attemptsIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_attempts);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_attempts, _attemptsIdentity);
            }
            rootHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_root);
            attemptsHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_attempts);
            string lockPath = Path.Combine(_root, WriterLockFileName);
            bool create = !File.Exists(lockPath);
            try
            {
                writerLease = FileOpenRouterPremiumIdentity.OpenFileNoFollow(lockPath,
                    create ? FileMode.CreateNew : FileMode.Open,
                    FileAccess.ReadWrite, FileShare.Read, 256, FileOptions.WriteThrough);
            }
            catch (IOException) when (create)
            {
                writerLease = FileOpenRouterPremiumIdentity.OpenFileNoFollow(lockPath,
                    FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 256, FileOptions.WriteThrough);
            }
            if (create)
            {
                writerLease.Write(WriterLockBytes);
                writerLease.Flush(flushToDisk: true);
            }
            writerLease.Position = 0;
            byte[] retainedLock = ReadStreamBounded(writerLease, WriterLockBytes.Length);
            try
            {
                if (!retainedLock.AsSpan().SequenceEqual(WriterLockBytes))
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(retainedLock); }
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, rootHandle);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_attempts, _attemptsIdentity, attemptsHandle);
            FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
                _root, rootHandle, WriterLockFileName, writerLease);
            _rootHandle = rootHandle;
            _attemptsHandle = attemptsHandle;
            _writerLease = writerLease;
        }
        catch
        {
            writerLease?.Dispose();
            attemptsHandle?.Dispose();
            rootHandle?.Dispose();
            throw;
        }
    }

    public string IntegrityAnchorIdentitySha256 => _anchor.IdentitySha256;

    public void CreateNew(string attemptId, ReadOnlySpan<byte> initialRecordCanonicalUtf8)
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            RequireAttemptId(attemptId);
            ProviderRoutingAttemptRecord initial = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                initialRecordCanonicalUtf8, _anchor);
            if (initial.State != ProviderRoutingAttemptState.NotStarted || initial.AttemptId != attemptId)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
            string attemptPath = Path.Combine(_attempts, attemptId);
            using SafeFileHandle attemptsLease = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_attempts);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_attempts, _attemptsIdentity, attemptsLease);
            if (Directory.Exists(attemptPath) || File.Exists(attemptPath))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_already_exists");
            try { Directory.CreateDirectory(attemptPath); }
            catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable"); }
            FileOpenRouterPremiumDirectoryIdentity attemptIdentity;
            try { attemptIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(attemptPath); }
            catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable"); }
            using SafeFileHandle attemptHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(attemptPath);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(attemptPath, attemptIdentity, attemptHandle);
            WriteNew(attemptPath, attemptIdentity, attemptHandle, InitialFileName,
                initialRecordCanonicalUtf8, "initial_record_after_write_before_flush",
                "initial_record_after_flush_before_readback", "initial_record_before_readback",
                bytes =>
                {
                    ProviderRoutingAttemptRecord read = ProviderRoutingAttemptLedgerCodec.ValidateRecord(bytes, _anchor);
                    if (read.State != ProviderRoutingAttemptState.NotStarted || read.AttemptId != attemptId)
                        throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
                });
            VerifyDirectories();
        }
    }

    public byte[] ReadCurrent(string attemptId)
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            RequireAttemptId(attemptId);
            return ResolveCurrent(attemptId);
        }
    }

    public byte[] ClaimOnce(
        string attemptId,
        string expectedRecordDigestSha256,
        ReadOnlySpan<byte> tombstoneCanonicalUtf8,
        ReadOnlySpan<byte> dispatchRecordCanonicalUtf8,
        ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
    {
        lock (_gate)
        {
            byte[] currentBytes = [];
            bool terminalMaterialMayExist = false;
            try
            {
                ThrowIfUnavailable();
                RequireAttemptId(attemptId);
                _fault.BeforeClaimPreparation();
                currentBytes = ResolveCurrent(attemptId);
                ProviderRoutingAttemptRecord current = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    currentBytes, _anchor);
                if (current.State != ProviderRoutingAttemptState.NotStarted)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_already_terminal");
                if (current.CanonicalDigestSha256 != expectedRecordDigestSha256)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_expected_record_mismatch");
                ProviderRoutingAttemptClaimTombstone tombstone =
                    ProviderRoutingAttemptLedgerCodec.ValidateTombstone(tombstoneCanonicalUtf8, _anchor);
                ProviderRoutingAttemptRecord dispatch = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    dispatchRecordCanonicalUtf8, _anchor);
                ProviderRoutingAttemptRecord unknown = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                    unknownRecordCanonicalUtf8, _anchor);
                InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                    current, tombstone, dispatch, ProviderRoutingAttemptState.DispatchStarted);
                InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                    current, tombstone, unknown, ProviderRoutingAttemptState.SubmissionUnknown);

                AttemptDirectory attempt = OpenAttempt(attemptId);
                using (attempt)
                {
                    try
                    {
                        _fault.BeforeClaimTombstone();
                        WriteNew(attempt.Path, attempt.Identity, attempt.Handle, TombstoneFileName,
                            tombstoneCanonicalUtf8, ClaimTombstoneWriteBoundary,
                            ClaimTombstoneFlushBoundary, ClaimTombstoneReadbackBoundary,
                            bytes => _ = ProviderRoutingAttemptLedgerCodec.ValidateTombstone(bytes, _anchor));
                        terminalMaterialMayExist = true;
                        WriteNew(attempt.Path, attempt.Identity, attempt.Handle, DispatchFileName,
                            dispatchRecordCanonicalUtf8, DispatchRecordWriteBoundary,
                            DispatchRecordFlushBoundary, DispatchRecordReadbackBoundary,
                            bytes =>
                            {
                                ProviderRoutingAttemptRecord retained =
                                    ProviderRoutingAttemptLedgerCodec.ValidateRecord(bytes, _anchor);
                                InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                                    current, tombstone, retained,
                                    ProviderRoutingAttemptState.DispatchStarted);
                            });
                        byte[] retained = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                            DispatchFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
                        ProviderRoutingAttemptRecord validated =
                            ProviderRoutingAttemptLedgerCodec.ValidateRecord(retained, _anchor);
                        InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                            current, tombstone, validated, ProviderRoutingAttemptState.DispatchStarted);
                        VerifyDirectories();
                        return retained;
                    }
                    catch (ProviderRoutingAttemptLedgerException exception) when (
                        exception.Code is "attempt_already_terminal" or "attempt_expected_record_mismatch")
                    {
                        throw;
                    }
                    catch
                    {
                        bool retainedTerminalMaterialMayExist;
                        try
                        {
                            retainedTerminalMaterialMayExist = terminalMaterialMayExist
                                || ExactEntryExists(attempt.Path, TombstoneFileName);
                        }
                        catch
                        {
                            throw new ProviderRoutingAttemptStorageClaimException(
                                ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown);
                        }
                        if (retainedTerminalMaterialMayExist)
                        {
                            try
                            {
                                RecoverUnknownIfRequired(
                                    attempt, current, tombstone, unknownRecordCanonicalUtf8);
                            }
                            catch (ProviderRoutingAttemptLedgerException) { throw; }
                            catch
                            {
                                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
                            }
                            throw new ProviderRoutingAttemptStorageClaimException(
                                ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown);
                        }
                        throw new ProviderRoutingAttemptStorageClaimException(
                            ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone);
                    }
                }
            }
            catch (ProviderRoutingAttemptLedgerException exception) when (
                IsKnownClaimFailure(exception.Code))
            {
                throw;
            }
            catch (ProviderRoutingAttemptStorageClaimException) { throw; }
            catch
            {
                throw new ProviderRoutingAttemptStorageClaimException(terminalMaterialMayExist
                    ? ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown
                    : ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone);
            }
            finally { CryptographicOperations.ZeroMemory(currentBytes); }
        }
    }

    private static bool IsKnownClaimFailure(string code) => code is
        "attempt_missing" or "attempt_already_terminal" or
        "attempt_expected_record_mismatch" or "attempt_poisoned";

    private byte[] ResolveCurrent(string attemptId)
    {
        using AttemptDirectory attempt = OpenAttempt(attemptId);
        byte[] initialBytes = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
            InitialFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
        try
        {
            ProviderRoutingAttemptRecord initial = ProviderRoutingAttemptLedgerCodec.ValidateRecord(
                initialBytes, _anchor);
            if (initial.AttemptId != attemptId || initial.State != ProviderRoutingAttemptState.NotStarted)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            bool tombstoneExists = ExactEntryExists(attempt.Path, TombstoneFileName);
            bool dispatchExists = ExactEntryExists(attempt.Path, DispatchFileName);
            bool unknownExists = ExactEntryExists(attempt.Path, UnknownFileName);
            if (!tombstoneExists)
            {
                if (dispatchExists || unknownExists)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
                return initialBytes.ToArray();
            }

            byte[] tombstoneBytes = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                TombstoneFileName, ProviderRoutingAttemptLedgerCodec.MaximumTombstoneBytes);
            try
            {
                ProviderRoutingAttemptClaimTombstone tombstone;
                try { tombstone = ProviderRoutingAttemptLedgerCodec.ValidateTombstone(tombstoneBytes, _anchor); }
                catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned"); }
                if (tombstone.AttemptId != initial.AttemptId
                    || tombstone.ExpectedRecordDigestSha256 != initial.CanonicalDigestSha256)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
                if (dispatchExists && !unknownExists)
                {
                    byte[] dispatchBytes = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                        DispatchFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
                    try
                    {
                        ProviderRoutingAttemptRecord dispatch =
                            ProviderRoutingAttemptLedgerCodec.ValidateRecord(dispatchBytes, _anchor);
                        InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                            initial, tombstone, dispatch, ProviderRoutingAttemptState.DispatchStarted);
                        return dispatchBytes.ToArray();
                    }
                    catch
                    {
                        ProviderRoutingAttemptRecord unknown =
                            ProviderRoutingAttemptLedgerCodec.CreateUnknownFromTombstone(initial, tombstone, _anchor);
                        byte[] unknownBytes = unknown.CanonicalUtf8.ToArray();
                        try
                        {
                            if (!unknownExists)
                                WriteRecoveryUnknown(attempt, initial, tombstone, unknownBytes);
                            return ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                                UnknownFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
                        }
                        finally { CryptographicOperations.ZeroMemory(unknownBytes); }
                    }
                    finally { CryptographicOperations.ZeroMemory(dispatchBytes); }
                }
                if (!unknownExists)
                {
                    ProviderRoutingAttemptRecord unknown =
                        ProviderRoutingAttemptLedgerCodec.CreateUnknownFromTombstone(initial, tombstone, _anchor);
                    byte[] unknownBytes = unknown.CanonicalUtf8.ToArray();
                    try { WriteRecoveryUnknown(attempt, initial, tombstone, unknownBytes); }
                    finally { CryptographicOperations.ZeroMemory(unknownBytes); }
                }
                byte[] retainedUnknownBytes = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                    UnknownFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
                try
                {
                    ProviderRoutingAttemptRecord retainedUnknown =
                        ProviderRoutingAttemptLedgerCodec.ValidateRecord(retainedUnknownBytes, _anchor);
                    InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                        initial, tombstone, retainedUnknown, ProviderRoutingAttemptState.SubmissionUnknown);
                    return retainedUnknownBytes.ToArray();
                }
                finally { CryptographicOperations.ZeroMemory(retainedUnknownBytes); }
            }
            finally { CryptographicOperations.ZeroMemory(tombstoneBytes); }
        }
        finally { CryptographicOperations.ZeroMemory(initialBytes); }
    }

    private void RecoverUnknownIfRequired(
        AttemptDirectory attempt,
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingAttemptClaimTombstone expectedTombstone,
        ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
    {
        byte[] tombstoneBytes;
        try
        {
            tombstoneBytes = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                TombstoneFileName, ProviderRoutingAttemptLedgerCodec.MaximumTombstoneBytes);
        }
        catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned"); }
        try
        {
            ProviderRoutingAttemptClaimTombstone retained;
            try
            {
                retained = ProviderRoutingAttemptLedgerCodec.ValidateTombstone(tombstoneBytes, _anchor);
            }
            catch
            {
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            }
            if (!retained.CanonicalUtf8.Span.SequenceEqual(expectedTombstone.CanonicalUtf8.Span))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            if (ExactEntryExists(attempt.Path, DispatchFileName))
            {
                byte[] dispatch = ReadExact(attempt.Path, attempt.Identity, attempt.Handle,
                    DispatchFileName, ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
                try
                {
                    ProviderRoutingAttemptRecord record =
                        ProviderRoutingAttemptLedgerCodec.ValidateRecord(dispatch, _anchor);
                    InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                        initial, retained, record, ProviderRoutingAttemptState.DispatchStarted);
                    return;
                }
                catch { }
                finally { CryptographicOperations.ZeroMemory(dispatch); }
            }
            if (!ExactEntryExists(attempt.Path, UnknownFileName))
                WriteRecoveryUnknown(attempt, initial, retained, unknownRecordCanonicalUtf8);
        }
        finally { CryptographicOperations.ZeroMemory(tombstoneBytes); }
    }

    private void WriteRecoveryUnknown(
        AttemptDirectory attempt,
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingAttemptClaimTombstone tombstone,
        ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
    {
        try { _fault.BeforeRecoveryUnknownWrite(); }
        catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned"); }
        WriteNew(attempt.Path, attempt.Identity, attempt.Handle, UnknownFileName,
            unknownRecordCanonicalUtf8, "unknown_record_after_write_before_flush",
            "unknown_record_after_flush_before_readback", "unknown_record_before_readback",
            bytes =>
            {
                ProviderRoutingAttemptRecord unknown =
                    ProviderRoutingAttemptLedgerCodec.ValidateRecord(bytes, _anchor);
                InMemoryProviderRoutingAttemptLedgerStorage.ValidateTerminal(
                    initial, tombstone, unknown, ProviderRoutingAttemptState.SubmissionUnknown);
            }, applyFault: false);
    }

    private AttemptDirectory OpenAttempt(string attemptId)
    {
        VerifyDirectories();
        string path = Path.Combine(_attempts, attemptId);
        if (!Directory.Exists(path))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_missing");
        try
        {
            FileOpenRouterPremiumDirectoryIdentity identity =
                FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(path);
            SafeFileHandle handle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(path);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(path, identity, handle);
            return new(path, identity, handle);
        }
        catch (ProviderRoutingAttemptLedgerException) { throw; }
        catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable"); }
    }

    private void WriteNew(
        string directory,
        FileOpenRouterPremiumDirectoryIdentity identity,
        SafeFileHandle directoryHandle,
        string name,
        ReadOnlySpan<byte> bytes,
        string writeBoundary,
        string flushBoundary,
        string readbackBoundary,
        Action<byte[]> validate,
        bool applyFault = true)
    {
        if (bytes.Length is < 1 or > ProviderRoutingAttemptLedgerModule.MaximumRecordBytes)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, identity, directoryHandle);
        string path = Path.Combine(directory, name);
        using FileStream output = FileOpenRouterPremiumIdentity.OpenFileNoFollow(path,
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, name, output);
        int requested = applyFault ? _fault.BytesToWrite(writeBoundary, bytes.Length) : bytes.Length;
        if (requested < 0 || requested > bytes.Length)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        output.Write(bytes[..requested]);
        if (applyFault) _fault.AfterWriteBeforeFlush(writeBoundary);
        if (requested != bytes.Length) throw new IOException("partial write");
        output.Flush(flushToDisk: true);
        if (applyFault) _fault.AfterFlushBeforeReadback(flushBoundary);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, name, output);
        if (applyFault) _fault.BeforeReadback(readbackBoundary);
        byte[] retained = ReadStreamBounded(output, bytes.Length);
        try
        {
            if (!retained.AsSpan().SequenceEqual(bytes))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable");
            validate(retained);
        }
        finally { CryptographicOperations.ZeroMemory(retained); }
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, name, output);
    }

    private byte[] ReadExact(
        string directory,
        FileOpenRouterPremiumDirectoryIdentity identity,
        SafeFileHandle directoryHandle,
        string name,
        int maximum)
    {
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, identity, directoryHandle);
        using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(
            Path.Combine(directory, name), FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.SequentialScan);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, name, input);
        byte[] bytes = ReadStreamBounded(input, maximum);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, name, input);
        return bytes;
    }

    private static byte[] ReadStreamBounded(FileStream stream, int maximum)
    {
        if (stream.Length is < 1 || stream.Length > maximum)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable");
        stream.Position = 0;
        byte[] bytes = new byte[checked((int)stream.Length)];
        int offset = 0;
        try
        {
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable");
                offset += read;
            }
            if (stream.ReadByte() != -1)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable");
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private bool ExactEntryExists(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        FileAttributes? attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable"); }
        if (attributes.Value.HasFlag(FileAttributes.Directory)
            || attributes.Value.HasFlag(FileAttributes.ReparsePoint))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
        return true;
    }

    private void VerifyDirectories()
    {
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, _rootHandle);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_attempts, _attemptsIdentity, _attemptsHandle);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(
            _root, _rootHandle, WriterLockFileName, _writerLease);
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileProviderRoutingAttemptLedgerStorage));
        VerifyDirectories();
    }

    private static void RequireAttemptId(string attemptId)
    {
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(attemptId))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_id_invalid");
    }

    private static string ValidateRoot(string root)
    {
        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        string full;
        try { full = Path.GetFullPath(root); }
        catch { throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid"); }
        if (!string.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        return full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static void RequireCanonicalPath(string path)
    {
        string canonical = FileOpenRouterPremiumIdentity.GetCanonicalDirectoryPath(path)
            .TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(canonical, path.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _writerLease.Dispose();
            _attemptsHandle.Dispose();
            _rootHandle.Dispose();
        }
    }

    private sealed record AttemptDirectory(
        string Path,
        FileOpenRouterPremiumDirectoryIdentity Identity,
        SafeFileHandle Handle) : IDisposable
    {
        public void Dispose() => Handle.Dispose();
    }

    private sealed class NoStorageFault : IProviderRoutingAttemptLedgerStorageFault
    {
        internal static NoStorageFault Instance { get; } = new();
        private NoStorageFault() { }
    }
}
