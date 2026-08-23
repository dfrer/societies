namespace Societies.SnowGlobe;

/// <summary>
/// Mutable local session over one v4/v5 append-only run. The durable ledger is authoritative; the
/// owned world is replaced from an independent strict read/reconstruction after every mutation.
/// </summary>
public sealed class SnowGlobePersistedSession : IDisposable
{
    private const string CoherenceFailure = "session_coherence_lost";

    private readonly SnowGlobeRunStore _store;
    private readonly ISnowGlobeIdentifiedInferenceAdapter _inference;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly object _disposeCancellationGate = new();
    private SnowGlobeWorld _world;
    private string _stateDigest = string.Empty;
    private string _eventDigest = string.Empty;
    private int _knownTick;
    private int _knownEventCount;
    private long _knownRevision;
    private int _fullHistoryDigestRefreshes;
    private long _projectedEventEntries;
    private bool _isPaused;
    private bool _failedClosed;
    private int _disposeRequested;
    private int _disposeCompleted;

    private SnowGlobePersistedSession(
        SnowGlobeRunStore store,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        SnowGlobeInternalRunReconstruction reconstruction,
        bool isPaused)
    {
        _store = store;
        _inference = inference;
        _world = reconstruction.Public.World;
        _isPaused = isPaused;
        CacheWorldIdentity();
    }

    public SnowGlobeRunIdentity Identity => _store.Identity;
    public string DirectoryPath => _store.DirectoryPath;
    public bool IsPaused => _isPaused;
    public bool IsFailedClosed => _failedClosed;
    public bool IsDisposed => Volatile.Read(ref _disposeCompleted) != 0;

    /// <summary>Internal deterministic seam used only to corrupt post-append evidence in focused tests.</summary>
    internal Action<SnowGlobeWorld>? AfterDurableMutationForTesting { get; set; }
    internal Action? BeforeLedgerAppendFlushForTesting
    {
        set => _store.BeforeLedgerAppendFlushForTesting = value;
    }
    internal void ExhaustRunStoreCapacityForTesting() => _store.ExhaustCapacityForTesting();

    public static SnowGlobePersistedSession CreateNew(
        string directory,
        SnowGlobeRunIdentity identity,
        ISnowGlobeIdentifiedInferenceAdapter inference) =>
        CreateNewCore(directory, identity, inference, isPaused: false);

    public static SnowGlobePersistedSession CreateNew(
        string directory,
        SnowGlobeRunIdentity identity,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool isPaused) =>
        CreateNewCore(directory, identity, inference, isPaused);

    private static SnowGlobePersistedSession CreateNewCore(
        string directory,
        SnowGlobeRunIdentity identity,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool isPaused)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(inference);
        ValidateAdapterProvenance(identity, inference);
        SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(directory, identity);
        try
        {
            SnowGlobeInternalRunReconstruction reconstruction = ReadAndReconstructInternal(store, identity);
            if (isPaused)
            {
                if (store.AppendPauseTransition(paused: true) != RunStorePauseAppendResult.Appended)
                    throw new InvalidDataException("Initial durable pause transition could not be appended.");
                reconstruction = ReadAndReconstructInternal(store, identity);
                if (!reconstruction.IsDurablyPaused)
                    throw new InvalidDataException("Initial durable pause transition did not reconstruct.");
            }
            return new SnowGlobePersistedSession(store, inference, reconstruction, isPaused);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public static SnowGlobePersistedSession Reopen(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        ISnowGlobeIdentifiedInferenceAdapter inference) =>
        ReopenCore(directory, expectedIdentity, inference, v4PauseOverride: null);

    public static SnowGlobePersistedSession Reopen(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool isPaused) =>
        ReopenCore(directory, expectedIdentity, inference, isPaused);

    private static SnowGlobePersistedSession ReopenCore(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool? v4PauseOverride)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(inference);
        ValidateAdapterProvenance(expectedIdentity, inference);

        // The preflight is read-only. In particular, legacy v2/v3 is rejected before OpenForAppend can
        // acquire/create a writer lease artifact.
        SnowGlobeRunLedger preflight = SnowGlobeRunStore.Read(directory);
        if (preflight.Identity != expectedIdentity)
            throw new InvalidDataException("Recorded run identity does not match the exact expected session identity.");
        if (preflight.Identity.SchemaVersion is SnowGlobeRunStore.LegacySchemaVersion or SnowGlobeRunStore.PreviousSchemaVersion)
            throw new InvalidDataException("Legacy v2/v3 run stores are read-only and cannot back a mutable session.");
        if (preflight.Identity.SchemaVersion == SnowGlobeRunStore.SchemaVersion && v4PauseOverride.HasValue)
            throw new InvalidDataException("V5 persisted sessions derive pause state from durable evidence; the four-argument reopen overload is v4-only.");
        if (preflight.Identity.SchemaVersion is not SnowGlobeRunStore.V4SchemaVersion and not SnowGlobeRunStore.SchemaVersion)
            throw new InvalidDataException("Run-store schema cannot back a mutable session.");

        SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(directory);
        try
        {
            SnowGlobeInternalRunReconstruction reconstruction = ReadAndReconstructInternal(store, expectedIdentity);
            bool isPaused = expectedIdentity.SchemaVersion == SnowGlobeRunStore.SchemaVersion
                ? reconstruction.IsDurablyPaused
                : v4PauseOverride ?? false;
            return new SnowGlobePersistedSession(store, inference, reconstruction, isPaused);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public Task<SnowGlobeObserverControlResult> PauseAsync() => SetPausedAsync(true);

    public Task<SnowGlobeObserverControlResult> ResumeAsync() => SetPausedAsync(false);

    public async Task<SnowGlobeObserverControlResult> AdvanceAsync(
        int ticks = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy)) return busy!;
        try
        {
            if (_failedClosed) return ControlFailure(CoherenceFailure, includeSnapshot: false);
            if (_isPaused) return ControlFailure("paused");
            string? invalid = ValidateTickCount(ticks);
            if (invalid is not null) return ControlFailure(invalid);
            return await RunScheduledAsync(ticks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ExitOperation();
        }
    }

    public async Task<SnowGlobeObserverControlResult> StepAsync(
        SnowGlobeObserverStepCommand? command,
        CancellationToken cancellationToken = default)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy)) return busy!;
        try
        {
            if (_failedClosed) return ControlFailure(CoherenceFailure, includeSnapshot: false);
            if (command?.TickCount is not int ticks) return ControlFailure("step_command_malformed");
            string? invalid = ValidateTickCount(ticks);
            if (invalid is not null) return ControlFailure(invalid);
            if (!_isPaused) return ControlFailure("must_be_paused");
            return await RunScheduledAsync(ticks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ExitOperation();
        }
    }

    public SnowGlobeObserverInspectionResult Inspect(int eventCursor = 0)
    {
        if (!_operationGate.Wait(0))
            return new SnowGlobeObserverInspectionResult(false, "operation_in_progress", null);
        try
        {
            ThrowIfDisposed();
            if (_failedClosed)
                return new SnowGlobeObserverInspectionResult(false, CoherenceFailure, null);
            if (eventCursor < 0 || eventCursor > _knownEventCount)
                return new SnowGlobeObserverInspectionResult(false, "event_cursor_invalid", null);
            SnowGlobeObserverSnapshot? snapshot = CreateSnapshot(eventCursor);
            if (snapshot is null)
            {
                FailClosed();
                return new SnowGlobeObserverInspectionResult(false, CoherenceFailure, null);
            }
            return new SnowGlobeObserverInspectionResult(true, null, snapshot);
        }
        finally
        {
            ExitOperation();
        }
    }

    public SnowGlobeObserverDiagnostics GetDiagnostics() =>
        new(_fullHistoryDigestRefreshes, _projectedEventEntries);

    public async Task<SnowGlobeParticipantCommandReceipt> SubmitParticipantCommandAsync(
        SnowGlobeParticipantCommand? command,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!_operationGate.Wait(0))
            return ParticipantIdentityFreeFailure("operation_in_progress");

        try
        {
            ThrowIfDisposed();
            if (_failedClosed) return ParticipantFailure(command, CoherenceFailure, includeIdentity: false);
            if (cancellationToken.IsCancellationRequested)
                return ParticipantFailure(command, "operation_cancelled");
            string? malformed = SnowGlobeObserverShell.ValidateParticipantCommandShape(command);
            if (malformed is not null) return ParticipantFailure(command, malformed);
            SnowGlobeParticipantCommand validCommand = command!;

            SnowGlobeRunLedger beforeLedger;
            SnowGlobeInternalRunReconstruction before;
            try
            {
                beforeLedger = SnowGlobeRunStore.Read(_store.DirectoryPath);
                before = SnowGlobePersistedRun.ReconstructInternal(beforeLedger, _store.Identity);
                if (_store.Identity.SchemaVersion == SnowGlobeRunStore.SchemaVersion
                    && before.IsDurablyPaused != _isPaused)
                {
                    throw new InvalidDataException("Session pause state differs from durable v5 evidence.");
                }
            }
            catch
            {
                FailClosed();
                return ParticipantFailure(validCommand, CoherenceFailure, includeIdentity: false);
            }

            SnowGlobeParticipantCommandKey key = new(validCommand.ParticipantId!, validCommand.IdempotencyKey!);
            SnowGlobeParticipantEvaluationRecord? existing = beforeLedger.ParticipantEvaluationRecords
                .SingleOrDefault(entry => entry.ParticipantId == key.ParticipantId && entry.IdempotencyKey == key.IdempotencyKey);
            if (existing is not null)
            {
                ReplaceWorld(before.Public.World);
                return SnowGlobeRunStore.SameParticipantCommand(existing, validCommand)
                    ? before.Public.ParticipantReceipts[key]
                    : ParticipantFailure(validCommand, "command_id_conflict");
            }

            if (!_isPaused) return ParticipantFailure(validCommand, "must_be_paused");
            if (cancellationToken.IsCancellationRequested)
                return ParticipantFailure(validCommand, "operation_cancelled");

            SnowGlobeParticipantCommandReceipt receipt;
            try
            {
                receipt = _store.EvaluateAndAppendParticipantCommand(validCommand);
            }
            catch
            {
                FailClosed();
                return ParticipantFailure(validCommand, CoherenceFailure, includeIdentity: false);
            }

            try
            {
                AfterDurableMutationForTesting?.Invoke(_world);
                SnowGlobeRunLedger afterLedger = SnowGlobeRunStore.Read(_store.DirectoryPath);
                SnowGlobeInternalRunReconstruction after = SnowGlobePersistedRun.ReconstructInternal(afterLedger, _store.Identity);
                if (_store.Identity.SchemaVersion == SnowGlobeRunStore.SchemaVersion
                    && after.IsDurablyPaused != _isPaused)
                {
                    throw new InvalidDataException("Participant append changed durable pause state.");
                }
                bool wasDurable = after.Public.ParticipantReceipts.TryGetValue(key, out SnowGlobeParticipantCommandReceipt? durableReceipt);
                if (wasDurable)
                {
                    if (durableReceipt != receipt) throw new InvalidDataException("Durable participant receipt differs from the returned evaluation.");
                }
                else if (afterLedger.EntryCount != beforeLedger.EntryCount)
                {
                    throw new InvalidDataException("Participant evaluation changed the ledger without a reconstructable receipt.");
                }
                ReplaceWorld(after.Public.World);
                return receipt;
            }
            catch
            {
                FailClosed();
                return ParticipantFailure(validCommand, CoherenceFailure, includeIdentity: false);
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    private Task<SnowGlobeObserverControlResult> SetPausedAsync(bool paused)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy))
            return Task.FromResult(busy!);
        try
        {
            if (_failedClosed)
                return Task.FromResult(ControlFailure(CoherenceFailure, includeSnapshot: false));
            if (_isPaused == paused)
                return Task.FromResult(ControlSuccess());

            if (_store.Identity.SchemaVersion == SnowGlobeRunStore.V4SchemaVersion)
            {
                _isPaused = paused;
                return Task.FromResult(ControlSuccess());
            }

            RunStorePauseAppendResult appendResult;
            try
            {
                appendResult = _store.AppendPauseTransition(paused);
            }
            catch
            {
                FailClosed();
                return Task.FromResult(ControlFailure(CoherenceFailure, includeSnapshot: false));
            }

            if (appendResult == RunStorePauseAppendResult.OperationInProgress)
                return Task.FromResult(ControlFailure("operation_in_progress"));
            if (appendResult == RunStorePauseAppendResult.CapacityExhausted)
                return Task.FromResult(ControlFailure("run_store_capacity_exhausted"));
            if (appendResult != RunStorePauseAppendResult.Appended)
            {
                FailClosed();
                return Task.FromResult(ControlFailure(CoherenceFailure, includeSnapshot: false));
            }

            try
            {
                AfterDurableMutationForTesting?.Invoke(_world);
                SnowGlobeWorldIdentity beforeIdentity = _world.CaptureIdentity();
                SnowGlobeInternalRunReconstruction reconstruction = ReadAndReconstructInternal(_store, _store.Identity);
                if (reconstruction.IsDurablyPaused != paused
                    || !WorldIdentitiesMatch(beforeIdentity, reconstruction.Public.World.CaptureIdentity()))
                {
                    throw new InvalidDataException("Durable pause transition changed world authority or did not reconstruct.");
                }
                ReplaceWorld(reconstruction.Public.World);
                _isPaused = paused;
                return Task.FromResult(ControlSuccess());
            }
            catch
            {
                FailClosed();
                return Task.FromResult(ControlFailure(CoherenceFailure, includeSnapshot: false));
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    private async Task<SnowGlobeObserverControlResult> RunScheduledAsync(
        int ticks,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);
        try
        {
            int targetTick = checked(_knownTick + ticks);
            await SnowGlobePersistedRun.RunAsync(
                _world,
                _inference,
                _store,
                targetTick,
                cancellationToken: linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            return HandleScheduledFailure("operation_cancelled");
        }
        catch
        {
            return HandleScheduledFailure("scheduler_failure");
        }

        try
        {
            AfterDurableMutationForTesting?.Invoke(_world);
            SnowGlobeWorldIdentity liveIdentity = _world.CaptureIdentity();
            SnowGlobeInternalRunReconstruction reconstruction = ReadAndReconstructInternal(_store, _store.Identity);
            SnowGlobeWorldIdentity durableIdentity = reconstruction.Public.World.CaptureIdentity();
            if (!WorldIdentitiesMatch(liveIdentity, durableIdentity))
                throw new InvalidDataException("Live scheduled result differs from durable reconstruction.");
            if (_store.Identity.SchemaVersion == SnowGlobeRunStore.SchemaVersion
                && reconstruction.IsDurablyPaused != _isPaused)
            {
                throw new InvalidDataException("Scheduled operation changed durable pause state.");
            }
            ReplaceWorld(reconstruction.Public.World);
            return ControlSuccess();
        }
        catch
        {
            FailClosed();
            return ControlFailure(CoherenceFailure, includeSnapshot: false);
        }
    }

    private SnowGlobeObserverControlResult HandleScheduledFailure(string reason)
    {
        if (_store.IsPoisoned)
        {
            FailClosed();
            return ControlFailure(CoherenceFailure, includeSnapshot: false);
        }
        return RestoreAfterManagedFailure(reason);
    }

    private SnowGlobeObserverControlResult RestoreAfterManagedFailure(string reason)
    {
        try
        {
            SnowGlobeInternalRunReconstruction reconstruction = ReadAndReconstructInternal(_store, _store.Identity);
            if (_store.Identity.SchemaVersion == SnowGlobeRunStore.SchemaVersion
                && reconstruction.IsDurablyPaused != _isPaused)
            {
                throw new InvalidDataException("Managed failure changed durable pause state.");
            }
            ReplaceWorld(reconstruction.Public.World);
            return ControlFailure(reason);
        }
        catch
        {
            FailClosed();
            return ControlFailure(CoherenceFailure, includeSnapshot: false);
        }
    }

    private static SnowGlobeInternalRunReconstruction ReadAndReconstructInternal(
        SnowGlobeRunStore store,
        SnowGlobeRunIdentity expectedIdentity)
    {
        SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(store.DirectoryPath);
        if (ledger.Identity != store.Identity || ledger.Identity != expectedIdentity)
            throw new InvalidDataException("Run-store identity changed while the mutable session owned it.");
        return SnowGlobePersistedRun.ReconstructInternal(ledger, expectedIdentity);
    }

    private void ReplaceWorld(SnowGlobeWorld world)
    {
        _world = world;
        CacheWorldIdentity();
    }

    private void CacheWorldIdentity()
    {
        SnowGlobeWorldIdentity identity = _world.CaptureIdentity();
        _knownTick = identity.Tick;
        _knownEventCount = identity.EventCount;
        _knownRevision = identity.Revision;
        _stateDigest = identity.StateDigest;
        _eventDigest = identity.EventDigest;
        _fullHistoryDigestRefreshes++;
    }

    private static bool WorldIdentitiesMatch(SnowGlobeWorldIdentity left, SnowGlobeWorldIdentity right) =>
        left.Tick == right.Tick
        && left.EventCount == right.EventCount
        && left.Revision == right.Revision
        && SnowGlobeRunStore.FixedEquals(left.StateDigest, right.StateDigest)
        && SnowGlobeRunStore.FixedEquals(left.EventDigest, right.EventDigest);

    private SnowGlobeObserverSnapshot? CreateSnapshot(int eventCursor = 0)
    {
        SnowGlobeObserverSnapshot? snapshot = SnowGlobeObserverShell.CreateDetachedSnapshot(
            _world,
            _isPaused,
            eventCursor,
            _stateDigest,
            _eventDigest,
            _knownRevision,
            out int projectedEventEntries);
        _projectedEventEntries += projectedEventEntries;
        return snapshot;
    }

    private SnowGlobeObserverControlResult ControlSuccess() =>
        CreateSnapshot() is SnowGlobeObserverSnapshot snapshot
            ? new SnowGlobeObserverControlResult(true, null, snapshot)
            : FailClosedControlResult();

    private SnowGlobeObserverControlResult ControlFailure(string reason, bool includeSnapshot = true)
    {
        if (!includeSnapshot) return new SnowGlobeObserverControlResult(false, reason, null);
        SnowGlobeObserverSnapshot? snapshot = CreateSnapshot();
        return snapshot is not null
            ? new SnowGlobeObserverControlResult(false, reason, snapshot)
            : FailClosedControlResult();
    }

    private SnowGlobeObserverControlResult FailClosedControlResult()
    {
        FailClosed();
        return new SnowGlobeObserverControlResult(false, CoherenceFailure, null);
    }

    private SnowGlobeParticipantCommandReceipt ParticipantFailure(
        SnowGlobeParticipantCommand? command,
        string reason,
        bool includeIdentity = true)
    {
        (string? participantId, string? idempotencyKey) = SnowGlobeObserverShell.SafeReceiptIdentity(command);
        return new SnowGlobeParticipantCommandReceipt(
            false,
            reason,
            false,
            participantId,
            idempotencyKey,
            includeIdentity ? _knownTick : null,
            includeIdentity ? _knownEventCount - 1 : null,
            includeIdentity ? _stateDigest : null,
            includeIdentity ? _eventDigest : null);
    }

    private static SnowGlobeParticipantCommandReceipt ParticipantIdentityFreeFailure(string reason) =>
        new(false, reason, false, null, null, null, null, null, null);

    private bool TryEnterOperation(out SnowGlobeObserverControlResult? busy)
    {
        if (_operationGate.Wait(0))
        {
            try
            {
                ThrowIfDisposed();
                busy = null;
                return true;
            }
            catch
            {
                ExitOperation();
                throw;
            }
        }
        busy = new SnowGlobeObserverControlResult(false, "operation_in_progress", null);
        return false;
    }

    private static void ValidateAdapterProvenance(
        SnowGlobeRunIdentity identity,
        ISnowGlobeIdentifiedInferenceAdapter inference)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(inference.AdapterIdentity))
            throw new InvalidDataException("Inference adapter identity is not canonical and bounded.");
        if (!string.Equals(identity.AdapterIdentity, inference.AdapterIdentity, StringComparison.Ordinal))
            throw new InvalidDataException("Inference adapter identity does not exactly match the run identity.");
    }

    private static string? ValidateTickCount(int ticks) => ticks switch
    {
        <= 0 => "step_count_must_be_positive",
        > SnowGlobeObserverShell.MaximumStepTicks => "step_count_exceeds_bound",
        _ => null
    };

    private void FailClosed() => _failedClosed = true;

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeRequested) != 0)
            throw new ObjectDisposedException(nameof(SnowGlobePersistedSession));
    }

    private void ExitOperation()
    {
        try
        {
            if (Volatile.Read(ref _disposeRequested) != 0) DisposeCoreUnderGate();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void DisposeCoreUnderGate()
    {
        if (Volatile.Read(ref _disposeCompleted) != 0) return;
        try
        {
            _store.Dispose();
        }
        finally
        {
            lock (_disposeCancellationGate) _disposeCancellation.Dispose();
            Volatile.Write(ref _disposeCompleted, 1);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) == 0)
        {
            lock (_disposeCancellationGate)
            {
                try { _disposeCancellation.Cancel(); }
                catch (AggregateException) { }
                catch (ObjectDisposedException) { }
            }
        }
        if (IsDisposed || !_operationGate.Wait(0)) return;
        try
        {
            DisposeCoreUnderGate();
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
