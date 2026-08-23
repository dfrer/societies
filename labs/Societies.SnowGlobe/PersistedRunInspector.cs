namespace Societies.SnowGlobe;

/// <summary>Bounded v4 recovery disposition visible to an offline persisted-run reader.</summary>
public enum SnowGlobePersistedRunRecoveryDisposition
{
    NoDurableRecovery,
    AbandonedIncompleteScheduledTick,
    AdoptedCompleteScheduledTick
}

/// <summary>
/// Immutable, raw-free v4 recovery-continuation provenance. Nullable source fields are absent
/// when no durable continuation exists; a pending tail alone never produces source evidence.
/// </summary>
public sealed record SnowGlobePersistedRunRecoveryProvenanceReceipt(
    string ReceiptSchemaIdentity,
    SnowGlobePersistedRunRecoveryDisposition Disposition,
    string RunIdentityChecksum,
    string EvidenceChecksum,
    int CommittedTick,
    int CommittedEventCount,
    string CommittedStateDigest,
    string CommittedEventDigest,
    int? SourceSegmentIndex,
    int? SourceFrameIndex,
    string? SourcePrepareChecksum,
    int? SourceLedgerLength,
    string? SourceLedgerChecksum,
    int? SourceMarkerLength,
    string? SourceMarkerChecksum,
    string? ContinuationChecksum);

/// <summary>
/// Result of the separate v4 recovery-provenance read. Legacy v2/v3 evidence can be accepted,
/// but intentionally has no v4 receipt.
/// </summary>
public sealed record SnowGlobePersistedRunRecoveryProvenanceInspectionResult(
    bool Accepted,
    string? RejectionReason,
    SnowGlobePersistedRunRecoveryProvenanceReceipt? Receipt);

/// <summary>The durable control state with which a valid v5 persisted session will reopen.</summary>
public enum SnowGlobePersistedSessionControlState
{
    Running,
    Paused
}

/// <summary>
/// Immutable, raw-free v5 durable session-control receipt. The receipt binds the reconstructed
/// control state to the exact stable evidence image and committed deterministic world identity.
/// </summary>
public sealed record SnowGlobePersistedSessionControlStatusReceipt(
    string ReceiptSchemaIdentity,
    SnowGlobePersistedSessionControlState State,
    string RunIdentityChecksum,
    string EvidenceChecksum,
    int CommittedTick,
    int CommittedEventCount,
    string CommittedStateDigest,
    string CommittedEventDigest);

/// <summary>
/// Result of the separate v5 durable session-control read. Accepted v2/v3/v4 inputs deliberately
/// have no receipt because they do not carry this v5 durable-control contract.
/// </summary>
public sealed record SnowGlobePersistedSessionControlStatusInspectionResult(
    bool Accepted,
    string? RejectionReason,
    SnowGlobePersistedSessionControlStatusReceipt? Receipt);

/// <summary>
/// Read-only, offline projection of an already-persisted run. This module never acquires writer
/// ownership, repairs pending frames, or returns a durable pause claim; <see cref="SnowGlobeObserverSnapshot.IsPaused"/>
/// is true only because the returned projection is inert.
/// </summary>
public static class SnowGlobePersistedRunInspector
{
    private const string RecoveryReceiptSchemaIdentity = "snow_globe_persisted_run_recovery_provenance_receipt/v1";
    private const string DurableControlStatusReceiptSchemaIdentity = "snow_globe_persisted_session_control_status_receipt/v1";

    /// <summary>
    /// Reads a stable, exact-identity persisted run into a detached observer snapshot. The bounded
    /// event page uses the same 32-event contract as the local observer shell.
    /// </summary>
    public static SnowGlobeObserverInspectionResult Inspect(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        int eventCursor = 0) => Inspect(directory, expectedIdentity, eventCursor, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobeObserverInspectionResult Inspect(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        int eventCursor,
        IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(files);
        ValidateExpectedIdentity(expectedIdentity);

        if (eventCursor < 0) return Reject("event_cursor_invalid");

        RunStoreReadEvidence first;
        try
        {
            first = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return Reject(ReadFailureReason(exception));
        }

        if (!ExactIdentity(first.Ledger.Identity, expectedIdentity)) return Reject("run_identity_mismatch");

        RunStoreReadEvidence second;
        try
        {
            second = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // The first validated image cannot be projected once the second read is no longer
            // coherent. Do not reveal whether the competing writer left a valid or invalid tail.
            return Reject("run_store_unstable");
        }

        if (!SnowGlobeRunStore.FixedEquals(first.EvidenceChecksum, second.EvidenceChecksum)
            || !ExactIdentity(first.Ledger.Identity, second.Ledger.Identity))
        {
            return Reject("run_store_unstable");
        }

        if (!ExactIdentity(second.Ledger.Identity, expectedIdentity)) return Reject("run_identity_mismatch");

        try
        {
            SnowGlobeWorld world = SnowGlobePersistedRun.Reconstruct(second.Ledger).World;
            SnowGlobeWorldIdentity identity = world.CaptureIdentity();
            if (eventCursor > identity.EventCount) return Reject("event_cursor_invalid");

            SnowGlobeObserverSnapshot? snapshot = SnowGlobeObserverShell.CreateDetachedSnapshot(
                world,
                isPaused: true,
                eventCursor,
                identity.StateDigest,
                identity.EventDigest,
                identity.Revision,
                out _);
            return snapshot is null
                ? Reject("inspection_coherence_lost")
                : new SnowGlobeObserverInspectionResult(true, null, snapshot);
        }
        catch (Exception)
        {
            return Reject("inspection_coherence_lost");
        }
    }

    /// <summary>
    /// Reads a stable, exact-identity v4 run and returns only bounded provenance for an already
    /// durable recovery continuation. This never opens, repairs, or infers a continuation; v2/v3
    /// runs remain accepted read-only inputs with a null receipt.
    /// </summary>
    public static SnowGlobePersistedRunRecoveryProvenanceInspectionResult InspectRecoveryProvenance(
        string directory,
        SnowGlobeRunIdentity expectedIdentity) => InspectRecoveryProvenance(
            directory, expectedIdentity, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobePersistedRunRecoveryProvenanceInspectionResult InspectRecoveryProvenance(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(files);
        ValidateExpectedIdentity(expectedIdentity);

        RunStoreReadEvidence first;
        try
        {
            first = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return RejectRecovery(ReadFailureReason(exception));
        }

        if (!ExactIdentity(first.Ledger.Identity, expectedIdentity)) return RejectRecovery("run_identity_mismatch");

        RunStoreReadEvidence second;
        try
        {
            second = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return RejectRecovery("run_store_unstable");
        }

        if (!SnowGlobeRunStore.FixedEquals(first.EvidenceChecksum, second.EvidenceChecksum)
            || !ExactIdentity(first.Ledger.Identity, second.Ledger.Identity))
        {
            return RejectRecovery("run_store_unstable");
        }

        if (!ExactIdentity(second.Ledger.Identity, expectedIdentity)) return RejectRecovery("run_identity_mismatch");
        if (second.Ledger.Identity.SchemaVersion != SnowGlobeRunStore.V4SchemaVersion)
            return new SnowGlobePersistedRunRecoveryProvenanceInspectionResult(true, null, null);

        try
        {
            SnowGlobeWorldIdentity identity = SnowGlobePersistedRun.Reconstruct(second.Ledger).World.CaptureIdentity();
            return new SnowGlobePersistedRunRecoveryProvenanceInspectionResult(
                true,
                null,
                CreateRecoveryReceipt(second, identity));
        }
        catch (Exception)
        {
            return RejectRecovery("inspection_coherence_lost");
        }
    }

    /// <summary>
    /// Reads a stable, exact-identity v5 run and reports only the durable control state that a
    /// persisted session will reopen with. This is distinct from <see cref="Inspect"/>, whose
    /// snapshot is deliberately inert. V2/v3/v4 runs remain accepted read-only inputs with a null receipt.
    /// </summary>
    public static SnowGlobePersistedSessionControlStatusInspectionResult InspectDurableControlStatus(
        string directory,
        SnowGlobeRunIdentity expectedIdentity) => InspectDurableControlStatus(
            directory, expectedIdentity, PhysicalRunStoreFileSystem.Instance);

    internal static SnowGlobePersistedSessionControlStatusInspectionResult InspectDurableControlStatus(
        string directory,
        SnowGlobeRunIdentity expectedIdentity,
        IRunStoreFileSystem files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(files);
        ValidateExpectedIdentity(expectedIdentity);

        RunStoreReadEvidence first;
        try
        {
            first = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return RejectDurableControlStatus(ReadFailureReason(exception));
        }

        if (!ExactIdentity(first.Ledger.Identity, expectedIdentity)) return RejectDurableControlStatus("run_identity_mismatch");

        RunStoreReadEvidence second;
        try
        {
            second = SnowGlobeRunStore.ReadWithEvidence(directory, files);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return RejectDurableControlStatus("run_store_unstable");
        }

        if (!SnowGlobeRunStore.FixedEquals(first.EvidenceChecksum, second.EvidenceChecksum)
            || !ExactIdentity(first.Ledger.Identity, second.Ledger.Identity))
        {
            return RejectDurableControlStatus("run_store_unstable");
        }

        if (!ExactIdentity(second.Ledger.Identity, expectedIdentity)) return RejectDurableControlStatus("run_identity_mismatch");
        if (second.Ledger.Identity.SchemaVersion != SnowGlobeRunStore.SchemaVersion)
            return new SnowGlobePersistedSessionControlStatusInspectionResult(true, null, null);

        try
        {
            SnowGlobeInternalRunReconstruction reconstruction = SnowGlobePersistedRun.ReconstructInternal(second.Ledger);
            SnowGlobeWorldIdentity identity = reconstruction.Public.World.CaptureIdentity();
            SnowGlobePersistedSessionControlState state = reconstruction.IsDurablyPaused
                ? SnowGlobePersistedSessionControlState.Paused
                : SnowGlobePersistedSessionControlState.Running;
            return new SnowGlobePersistedSessionControlStatusInspectionResult(
                true,
                null,
                new SnowGlobePersistedSessionControlStatusReceipt(
                    DurableControlStatusReceiptSchemaIdentity,
                    state,
                    second.V4HeaderChecksum ?? throw new InvalidDataException("V5 durable control status requires header evidence."),
                    second.EvidenceChecksum,
                    identity.Tick,
                    identity.EventCount,
                    identity.StateDigest,
                    identity.EventDigest));
        }
        catch (Exception)
        {
            return RejectDurableControlStatus("inspection_coherence_lost");
        }
    }

    private static void ValidateExpectedIdentity(SnowGlobeRunIdentity expectedIdentity)
    {
        try
        {
            SnowGlobeRunStore.ValidateSupportedIdentity(expectedIdentity);
        }
        catch (InvalidDataException exception)
        {
            throw new ArgumentException("Expected run identity is unsupported or incomplete.", nameof(expectedIdentity), exception);
        }
    }

    private static bool ExactIdentity(SnowGlobeRunIdentity actual, SnowGlobeRunIdentity expected) =>
        actual.SchemaVersion == expected.SchemaVersion
        && actual.RulesIdentity == expected.RulesIdentity
        && actual.PromptIdentity == expected.PromptIdentity
        && actual.AdapterIdentity == expected.AdapterIdentity
        && actual.Seed == expected.Seed
        && actual.AgentCount == expected.AgentCount
        && actual.ParticipantCommandIdentity == expected.ParticipantCommandIdentity;

    private static string ReadFailureReason(Exception exception) => exception switch
    {
        IOException or UnauthorizedAccessException => "run_store_unavailable",
        InvalidDataException => "run_store_invalid",
        _ => "run_store_unavailable"
    };

    private static SnowGlobeObserverInspectionResult Reject(string reason) => new(false, reason, null);

    private static SnowGlobePersistedRunRecoveryProvenanceReceipt CreateRecoveryReceipt(
        RunStoreReadEvidence evidence,
        SnowGlobeWorldIdentity committedIdentity)
    {
        if (evidence.V4HeaderChecksum is null)
            throw new InvalidDataException("V4 recovery provenance requires v4 header evidence.");

        if (evidence.DurableRecovery is null)
        {
            return new SnowGlobePersistedRunRecoveryProvenanceReceipt(
                RecoveryReceiptSchemaIdentity,
                SnowGlobePersistedRunRecoveryDisposition.NoDurableRecovery,
                evidence.V4HeaderChecksum,
                evidence.EvidenceChecksum,
                committedIdentity.Tick,
                committedIdentity.EventCount,
                committedIdentity.StateDigest,
                committedIdentity.EventDigest,
                null, null, null, null, null, null, null, null);
        }

        RunStoreDurableRecovery recovery = evidence.DurableRecovery;
        SnowGlobePersistedRunRecoveryDisposition disposition = recovery.Disposition switch
        {
            RunStoreRecoveryDisposition.AbandonIncompleteScheduledTick => SnowGlobePersistedRunRecoveryDisposition.AbandonedIncompleteScheduledTick,
            RunStoreRecoveryDisposition.RecoverCompleteScheduledTick => SnowGlobePersistedRunRecoveryDisposition.AdoptedCompleteScheduledTick,
            _ => throw new InvalidDataException("V4 recovery provenance disposition is invalid.")
        };
        return new SnowGlobePersistedRunRecoveryProvenanceReceipt(
            RecoveryReceiptSchemaIdentity,
            disposition,
            evidence.V4HeaderChecksum,
            evidence.EvidenceChecksum,
            committedIdentity.Tick,
            committedIdentity.EventCount,
            committedIdentity.StateDigest,
            committedIdentity.EventDigest,
            recovery.SourceSegmentIndex,
            recovery.SourceFrameIndex,
            recovery.SourcePrepareChecksum,
            recovery.SourceLedgerLength,
            recovery.SourceLedgerChecksum,
            recovery.SourceMarkerLength,
            recovery.SourceMarkerChecksum,
            recovery.ContinuationChecksum);
    }

    private static SnowGlobePersistedRunRecoveryProvenanceInspectionResult RejectRecovery(string reason) => new(false, reason, null);

    private static SnowGlobePersistedSessionControlStatusInspectionResult RejectDurableControlStatus(string reason) => new(false, reason, null);
}
