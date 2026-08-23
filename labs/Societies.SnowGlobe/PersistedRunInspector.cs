namespace Societies.SnowGlobe;

/// <summary>
/// Read-only, offline projection of an already-persisted run. This module never acquires writer
/// ownership, repairs pending frames, or returns a durable pause claim; <see cref="SnowGlobeObserverSnapshot.IsPaused"/>
/// is true only because the returned projection is inert.
/// </summary>
public static class SnowGlobePersistedRunInspector
{
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
}
