using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Societies.SnowGlobe.Tests")]

namespace Societies.SnowGlobe;

/// <summary>
/// A bounded, value-only request to advance paused simulation time. The nullable count makes
/// malformed headless command decoding explicit rather than silently choosing a default.
/// </summary>
public sealed record SnowGlobeObserverStepCommand(int? TickCount);

public sealed record SnowGlobeObserverAgentSnapshot(string AgentId, int HomeSlot, int CompletedActions);

public sealed record SnowGlobeObserverStructureSnapshot(string StructureId, SnowGlobeStructureKind Kind, int Durability);

/// <summary>
/// A canonical committed-event projection. It deliberately has no reference to a live world event.
/// </summary>
public sealed record SnowGlobeObserverEventSnapshot(
    int Tick,
    int Sequence,
    string AgentId,
    SnowGlobeActionKind Action,
    int Quantity,
    string? StructureId,
    string Canonical);

/// <summary>
/// A detached read model for a headless observer. Collections are read-only projections and their
/// elements are immutable value records, so callers cannot mutate or retain live world state.
/// </summary>
public sealed record SnowGlobeObserverSnapshot(
    bool IsPaused,
    int Tick,
    int AvailableWood,
    int AvailableStone,
    int StockpileWood,
    int StockpileStone,
    IReadOnlyList<SnowGlobeObserverAgentSnapshot> Agents,
    IReadOnlyList<SnowGlobeObserverStructureSnapshot> Structures,
    int EventHistoryCount,
    int EventCursor,
    int? NextEventCursor,
    IReadOnlyList<SnowGlobeObserverEventSnapshot> CanonicalEvents,
    string StateDigest,
    string EventDigest);

public sealed record SnowGlobeObserverControlResult(
    bool Applied,
    string? RejectionReason,
    SnowGlobeObserverSnapshot? Snapshot);

public sealed record SnowGlobeObserverInspectionResult(
    bool Accepted,
    string? RejectionReason,
    SnowGlobeObserverSnapshot? Snapshot);

/// <summary>Bounded-observer cost counters for focused regression tests and headless diagnostics.</summary>
public sealed record SnowGlobeObserverDiagnostics(int FullHistoryDigestRefreshes, long ProjectedEventEntries);

/// <summary>
/// Thin local-only controller for observing a Snow Globe run. It owns no world facts and advances
/// exclusively through the existing sequential scheduler, preserving its value-only inference,
/// validation, ordinal commit, and tick-boundary semantics.
/// </summary>
public sealed partial class SnowGlobeObserverShell
{
    public const int MaximumStepTicks = 64;
    public const int MaximumInspectionEventWindow = 32;

    private readonly SnowGlobeWorld _world;
    private readonly SequentialInferenceScheduler _scheduler;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private string _stateDigest;
    private string _eventDigest;
    private int _knownTick;
    private int _knownEventCount;
    private long _knownRevision;
    private int _fullHistoryDigestRefreshes;
    private long _projectedEventEntries;
    private bool _isPaused;
    private bool _ownershipLost;

    /// <summary>
    /// Callers transfer exclusive mutation ownership of <paramref name="world"/> to this shell.
    /// Direct world mutations are not synchronized; detected interference permanently fails closed.
    /// </summary>
    public SnowGlobeObserverShell(SnowGlobeWorld world, SequentialInferenceScheduler scheduler)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        SnowGlobeWorldIdentity identity = _world.CaptureIdentity();
        _stateDigest = identity.StateDigest;
        _eventDigest = identity.EventDigest;
        _knownTick = identity.Tick;
        _knownEventCount = identity.EventCount;
        _knownRevision = identity.Revision;
        _fullHistoryDigestRefreshes = 1;
    }

    public Task<SnowGlobeObserverControlResult> PauseAsync() => SetPauseStateAsync(isPaused: true);

    public Task<SnowGlobeObserverControlResult> ResumeAsync() => SetPauseStateAsync(isPaused: false);

    public SnowGlobeObserverDiagnostics GetDiagnostics() => new(_fullHistoryDigestRefreshes, _projectedEventEntries);

    public bool HasExclusiveWorldOwnership => !_ownershipLost && IsWorldOwned();

    /// <summary>Internal deterministic seam for injecting post-apply interference in focused tests.</summary>
    internal Action? AfterLiveApplyForTesting { get; set; }

    /// <summary>Internal deterministic seam for injecting interference after a participant candidate applies.</summary>
    internal Action? AfterParticipantCandidateApplyForTesting { get; set; }

    /// <summary>Internal deterministic seam for injecting interference immediately before the atomic participant commit.</summary>
    internal Action? BeforeParticipantConditionalCommitForTesting { get; set; }

    /// <summary>
    /// The regular scheduler entry point. A paused shell refuses ordinary advances without changing
    /// the world; paused simulation time may move only through an explicit step command.
    /// </summary>
    public async Task<SnowGlobeObserverControlResult> AdvanceAsync(int ticks = 1)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy))
        {
            return busy!;
        }

        try
        {
            if (!IsWorldOwned())
            {
                return OwnershipLost();
            }

            if (_isPaused)
            {
                return Reject("paused");
            }

            string? rejection = ValidateTickCount(ticks);
            if (rejection is not null)
            {
                return Reject(rejection);
            }

            return await RunTransactionalAsync(ticks).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// Executes a bounded integral number of complete scheduler ticks only while paused.
    /// </summary>
    public async Task<SnowGlobeObserverControlResult> StepAsync(SnowGlobeObserverStepCommand? command)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy))
        {
            return busy!;
        }

        try
        {
            if (!IsWorldOwned())
            {
                return OwnershipLost();
            }

            if (command?.TickCount is not int ticks)
            {
                return Reject("step_command_malformed");
            }

            string? rejection = ValidateTickCount(ticks);
            if (rejection is not null)
            {
                return Reject(rejection);
            }

            if (!_isPaused)
            {
                return Reject("must_be_paused");
            }

            return await RunTransactionalAsync(ticks).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// Returns a point-in-time detached projection, or fails closed if an asynchronous scheduler
    /// operation is between a tick's observations and its ordinal commits.
    /// </summary>
    public SnowGlobeObserverInspectionResult Inspect(int eventCursor = 0)
    {
        if (!_operationGate.Wait(0))
        {
            return new SnowGlobeObserverInspectionResult(false, "operation_in_progress", null);
        }

        try
        {
            if (!IsWorldOwned())
            {
                return new SnowGlobeObserverInspectionResult(false, "world_ownership_lost", null);
            }

            if (eventCursor < 0 || eventCursor > _knownEventCount)
            {
                return new SnowGlobeObserverInspectionResult(false, "event_cursor_invalid", null);
            }

            return CreateSnapshot(eventCursor) is { } snapshot
                ? new SnowGlobeObserverInspectionResult(true, null, snapshot)
                : new SnowGlobeObserverInspectionResult(false, "world_ownership_lost", null);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private Task<SnowGlobeObserverControlResult> SetPauseStateAsync(bool isPaused)
    {
        if (!TryEnterOperation(out SnowGlobeObserverControlResult? busy))
        {
            return Task.FromResult(busy!);
        }

        try
        {
            if (!IsWorldOwned())
            {
                return Task.FromResult(OwnershipLost());
            }

            _isPaused = isPaused;
            return Task.FromResult(Accept());
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private bool TryEnterOperation(out SnowGlobeObserverControlResult? busy)
    {
        if (_operationGate.Wait(0))
        {
            busy = null;
            return true;
        }

        busy = new SnowGlobeObserverControlResult(false, "operation_in_progress", null);
        return false;
    }

    private static string? ValidateTickCount(int ticks) => ticks switch
    {
        <= 0 => "step_count_must_be_positive",
        > MaximumStepTicks => "step_count_exceeds_bound",
        _ => null
    };

    private async Task<SnowGlobeObserverControlResult> RunTransactionalAsync(int ticks)
    {
        try
        {
            SnowGlobeWorld candidate = ReconstructCandidate();
            int eventCountBefore = candidate.Events.Count;
            await _scheduler.RunAsync(candidate, ticks).ConfigureAwait(false);

            SnowGlobeEvent[] candidateDelta = candidate.Events.Skip(eventCountBefore).ToArray();
            SnowGlobeWorld verification = ReconstructCandidate();
            ApplyCommittedCandidateDelta(verification, candidateDelta, candidate.Tick);
            string candidateStateDigest = candidate.StateDigest();
            string candidateEventDigest = candidate.EventDigest();
            if (!string.Equals(candidateStateDigest, verification.StateDigest(), StringComparison.Ordinal)
                || !string.Equals(candidateEventDigest, verification.EventDigest(), StringComparison.Ordinal)
                || !IsWorldOwned())
            {
                return !IsWorldOwned() ? OwnershipLost() : Reject("candidate_replay_mismatch");
            }

            try
            {
                ApplyCommittedCandidateDelta(_world, candidateDelta, candidate.Tick);
            }
            catch (Exception)
            {
                return OwnershipLost();
            }

            long expectedLiveRevision = checked(_knownRevision + candidateDelta.Length + (candidate.Tick - _knownTick));
            AfterLiveApplyForTesting?.Invoke();
            if (!_world.MatchesRevision(expectedLiveRevision))
            {
                return OwnershipLost();
            }

            CacheCandidateIdentity(candidate.Tick, candidate.Events.Count, candidateStateDigest, candidateEventDigest, expectedLiveRevision);
            return Accept();
        }
        catch (Exception)
        {
            return Reject("scheduler_failure");
        }
    }

    private SnowGlobeWorld ReconstructCandidate()
    {
        SnowGlobeWorldReplaySnapshot source = _world.CaptureReplaySnapshot();
        int initialWood = source.AvailableWood;
        int initialStone = source.AvailableStone;
        for (int index = 0; index < source.Events.Count; index++)
        {
            SnowGlobeEvent entry = source.Events[index];
            if (entry.Action == SnowGlobeActionKind.GatherWood) initialWood = checked(initialWood + entry.Quantity);
            if (entry.Action == SnowGlobeActionKind.GatherStone) initialStone = checked(initialStone + entry.Quantity);
        }
        SnowGlobeWorld candidate = SnowGlobeWorld.Create(source.Seed, source.AgentCount, initialWood, initialStone);
        for (int index = 0; index < source.Events.Count; index++)
        {
            candidate.Replay(source.Events[index]);
        }

        while (candidate.Tick < source.Tick)
        {
            candidate.AdvanceTick();
        }

        if (candidate.Tick != source.Tick
            || !string.Equals(candidate.StateDigest(), _stateDigest, StringComparison.Ordinal)
            || !string.Equals(candidate.EventDigest(), _eventDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Live world cannot be reconstructed through its canonical event history.");
        }

        return candidate;
    }

    private static void ApplyCommittedCandidateDelta(SnowGlobeWorld target, IEnumerable<SnowGlobeEvent> delta, int targetTick)
    {
        foreach (SnowGlobeEvent entry in delta)
        {
            target.Replay(entry);
        }

        while (target.Tick < targetTick)
        {
            target.AdvanceTick();
        }

        if (target.Tick != targetTick)
        {
            throw new InvalidOperationException("Candidate replay attempted to move time backward.");
        }
    }

    private bool IsWorldOwned() => !_ownershipLost && _world.MatchesRevision(_knownRevision);

    private void CacheCandidateIdentity(int tick, int eventCount, string stateDigest, string eventDigest, long revision)
    {
        _stateDigest = stateDigest;
        _eventDigest = eventDigest;
        _knownTick = tick;
        _knownEventCount = eventCount;
        _knownRevision = revision;
        _fullHistoryDigestRefreshes++;
    }

    private SnowGlobeObserverControlResult OwnershipLost()
    {
        _ownershipLost = true;
        return new SnowGlobeObserverControlResult(false, "world_ownership_lost", null);
    }

    private SnowGlobeObserverControlResult Accept() => CreateSnapshot() is { } snapshot
        ? new SnowGlobeObserverControlResult(true, null, snapshot)
        : OwnershipLost();

    private SnowGlobeObserverControlResult Reject(string reason) => IsWorldOwned()
        ? CreateSnapshot() is { } snapshot
            ? new SnowGlobeObserverControlResult(false, reason, snapshot)
            : OwnershipLost()
        : OwnershipLost();

    private SnowGlobeObserverSnapshot? CreateSnapshot(int eventCursor = 0)
    {
        SnowGlobeObserverSnapshot? snapshot = CreateDetachedSnapshot(
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

    internal static SnowGlobeObserverSnapshot? CreateDetachedSnapshot(
        SnowGlobeWorld world,
        bool isPaused,
        int eventCursor,
        string stateDigest,
        string eventDigest,
        long expectedRevision,
        out int projectedEventEntries)
    {
        SnowGlobeWorldObserverProjection? projection = world.CaptureObserverProjection(expectedRevision, eventCursor, MaximumInspectionEventWindow);
        if (projection is null)
        {
            projectedEventEntries = 0;
            return null;
        }
        List<SnowGlobeObserverAgentSnapshot> agents = projection.Agents
            .Select(agent => new SnowGlobeObserverAgentSnapshot(agent.AgentId, agent.HomeSlot, agent.CompletedActions))
            .ToList();
        List<SnowGlobeObserverStructureSnapshot> structures = projection.Structures
            .Select(structure => new SnowGlobeObserverStructureSnapshot(structure.StructureId, structure.Kind, structure.Durability))
            .ToList();
        List<SnowGlobeObserverEventSnapshot> events = new(projection.Events.Count);
        for (int index = 0; index < projection.Events.Count; index++)
        {
            SnowGlobeEvent entry = projection.Events[index];
            events.Add(new SnowGlobeObserverEventSnapshot(
                entry.Tick,
                entry.Sequence,
                entry.AgentId,
                entry.Action,
                entry.Quantity,
                entry.StructureId,
                $"{entry.Tick}|{entry.Sequence}|{entry.AgentId}|{entry.Action}|{entry.Quantity}|{entry.StructureId ?? string.Empty}"));
        }
        projectedEventEntries = projection.Events.Count;
        return new SnowGlobeObserverSnapshot(
            isPaused,
            projection.Tick,
            projection.AvailableWood,
            projection.AvailableStone,
            projection.StockpileWood,
            projection.StockpileStone,
            new ReadOnlyCollection<SnowGlobeObserverAgentSnapshot>(agents),
            new ReadOnlyCollection<SnowGlobeObserverStructureSnapshot>(structures),
            projection.EventHistoryCount,
            eventCursor,
            eventCursor + events.Count < projection.EventHistoryCount ? eventCursor + events.Count : null,
            new ReadOnlyCollection<SnowGlobeObserverEventSnapshot>(events),
            stateDigest,
            eventDigest);
    }
}
