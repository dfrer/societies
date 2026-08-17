namespace Societies.SnowGlobe;

/// <summary>
/// Mutable local session over one v3 append-only run. The durable ledger is authoritative; the
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
        SnowGlobeRunReconstruction reconstruction,
        bool isPaused)
    {
        _store = store;
        _inference = inference;
        _world = reconstruction.World;
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

    public static SnowGlobePersistedSession CreateNew(
        string directory,
        SnowGlobeRunIdentity identity,
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool isPaused = false)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(inference);
        ValidateAdapterProvenance(identity, inference);
        SnowGlobeRunStore store = SnowGlobeRunStore.CreateNew(directory, identity);
        try
        {
            SnowGlobeRunReconstruction reconstruction = ReadAndReconstruct(store, identity);
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
        ISnowGlobeIdentifiedInferenceAdapter inference,
        bool isPaused = false)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(inference);
        ValidateAdapterProvenance(expectedIdentity, inference);

        // The preflight is read-only. In particular, v2 is rejected before OpenForAppend can
        // acquire/create a writer lease artifact.
        SnowGlobeRunLedger preflight = SnowGlobeRunStore.Read(directory);
        if (preflight.Identity.SchemaVersion != SnowGlobeRunStore.SchemaVersion)
            throw new InvalidDataException("Legacy v2 run stores are read-only and cannot back a mutable session.");
        if (preflight.Identity != expectedIdentity)
            throw new InvalidDataException("Recorded run identity does not match the exact expected session identity.");

        SnowGlobeRunStore store = SnowGlobeRunStore.OpenForAppend(directory);
        try
        {
            SnowGlobeRunReconstruction reconstruction = ReadAndReconstruct(store, expectedIdentity);
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
            SnowGlobeRunReconstruction before;
            try
            {
                beforeLedger = SnowGlobeRunStore.Read(_store.DirectoryPath);
                before = SnowGlobePersistedRun.Reconstruct(beforeLedger, _store.Identity);
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
                ReplaceWorld(before.World);
                return SnowGlobeRunStore.SameParticipantCommand(existing, validCommand)
                    ? before.ParticipantReceipts[key]
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
                SnowGlobeRunReconstruction after = SnowGlobePersistedRun.Reconstruct(afterLedger, _store.Identity);
                bool wasDurable = after.ParticipantReceipts.TryGetValue(key, out SnowGlobeParticipantCommandReceipt? durableReceipt);
                if (wasDurable)
                {
                    if (durableReceipt != receipt) throw new InvalidDataException("Durable participant receipt differs from the returned evaluation.");
                }
                else if (afterLedger.EntryCount != beforeLedger.EntryCount)
                {
                    throw new InvalidDataException("Participant evaluation changed the ledger without a reconstructable receipt.");
                }
                ReplaceWorld(after.World);
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
            _isPaused = paused;
            return Task.FromResult(ControlSuccess());
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
            SnowGlobeRunReconstruction reconstruction = ReadAndReconstruct(_store, _store.Identity);
            SnowGlobeWorldIdentity durableIdentity = reconstruction.World.CaptureIdentity();
            if (!WorldIdentitiesMatch(liveIdentity, durableIdentity))
                throw new InvalidDataException("Live scheduled result differs from durable reconstruction.");
            ReplaceWorld(reconstruction.World);
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
            SnowGlobeRunReconstruction reconstruction = ReadAndReconstruct(_store, _store.Identity);
            ReplaceWorld(reconstruction.World);
            return ControlFailure(reason);
        }
        catch
        {
            FailClosed();
            return ControlFailure(CoherenceFailure, includeSnapshot: false);
        }
    }

    private static SnowGlobeRunReconstruction ReadAndReconstruct(
        SnowGlobeRunStore store,
        SnowGlobeRunIdentity expectedIdentity)
    {
        SnowGlobeRunLedger ledger = SnowGlobeRunStore.Read(store.DirectoryPath);
        if (ledger.Identity != store.Identity || ledger.Identity != expectedIdentity)
            throw new InvalidDataException("Run-store identity changed while the mutable session owned it.");
        return SnowGlobePersistedRun.Reconstruct(ledger, expectedIdentity);
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
