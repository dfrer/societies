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
public sealed class SnowGlobeObserverShell
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
        _stateDigest = _world.StateDigest();
        _eventDigest = _world.EventDigest();
        _knownTick = _world.Tick;
        _knownEventCount = _world.Events.Count;
        _fullHistoryDigestRefreshes = 1;
    }

    public Task<SnowGlobeObserverControlResult> PauseAsync() => SetPauseStateAsync(isPaused: true);

    public Task<SnowGlobeObserverControlResult> ResumeAsync() => SetPauseStateAsync(isPaused: false);

    public SnowGlobeObserverDiagnostics GetDiagnostics() => new(_fullHistoryDigestRefreshes, _projectedEventEntries);

    public bool HasExclusiveWorldOwnership => !_ownershipLost && IsWorldOwned();

    /// <summary>Internal deterministic seam for injecting post-apply interference in focused tests.</summary>
    internal Action? AfterLiveApplyForTesting { get; set; }

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

            if (eventCursor < 0 || eventCursor > _world.Events.Count)
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

            AfterLiveApplyForTesting?.Invoke();
            if (!LiveWorldMatchesCandidate(candidate, candidateStateDigest, candidateEventDigest))
            {
                return OwnershipLost();
            }

            CacheCandidateIdentity(candidate.Tick, candidate.Events.Count, candidateStateDigest, candidateEventDigest);
            return Accept();
        }
        catch (Exception)
        {
            return Reject("scheduler_failure");
        }
    }

    private SnowGlobeWorld ReconstructCandidate()
    {
        int initialWood = _world.AvailableWood;
        int initialStone = _world.AvailableStone;
        for (int index = 0; index < _world.Events.Count; index++)
        {
            SnowGlobeEvent entry = _world.Events[index];
            if (entry.Action == SnowGlobeActionKind.GatherWood) initialWood = checked(initialWood + entry.Quantity);
            if (entry.Action == SnowGlobeActionKind.GatherStone) initialStone = checked(initialStone + entry.Quantity);
        }
        SnowGlobeWorld candidate = SnowGlobeWorld.Create(_world.Seed, _world.Agents.Count, initialWood, initialStone);
        for (int index = 0; index < _world.Events.Count; index++)
        {
            candidate.Replay(_world.Events[index]);
        }

        while (candidate.Tick < _world.Tick)
        {
            candidate.AdvanceTick();
        }

        if (candidate.Tick != _world.Tick
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

    private bool IsWorldOwned() => !_ownershipLost && _world.Tick == _knownTick && _world.Events.Count == _knownEventCount;

    private bool LiveWorldMatchesCandidate(SnowGlobeWorld candidate, string expectedStateDigest, string expectedEventDigest) =>
        _world.Tick == candidate.Tick
        && _world.Events.Count == candidate.Events.Count
        && string.Equals(_world.StateDigest(), expectedStateDigest, StringComparison.Ordinal)
        && string.Equals(_world.EventDigest(), expectedEventDigest, StringComparison.Ordinal);

    private void CacheCandidateIdentity(int tick, int eventCount, string stateDigest, string eventDigest)
    {
        _stateDigest = stateDigest;
        _eventDigest = eventDigest;
        _knownTick = tick;
        _knownEventCount = eventCount;
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
        if (!IsWorldOwned()) return null;
        List<SnowGlobeObserverAgentSnapshot> agents = _world.Agents
            .OrderBy(agent => agent.AgentId, StringComparer.Ordinal)
            .Select(agent => new SnowGlobeObserverAgentSnapshot(agent.AgentId, agent.HomeSlot, agent.CompletedActions))
            .ToList();
        List<SnowGlobeObserverStructureSnapshot> structures = _world.Structures
            .OrderBy(structure => structure.StructureId, StringComparer.Ordinal)
            .Select(structure => new SnowGlobeObserverStructureSnapshot(structure.StructureId, structure.Kind, structure.Durability))
            .ToList();
        int eventHistoryCount = _world.Events.Count;
        int pageCount = Math.Min(MaximumInspectionEventWindow, eventHistoryCount - eventCursor);
        List<SnowGlobeObserverEventSnapshot> events = new(pageCount);
        for (int index = eventCursor; index < eventCursor + pageCount; index++)
        {
            SnowGlobeEvent entry = _world.Events[index];
            events.Add(new SnowGlobeObserverEventSnapshot(
                entry.Tick,
                entry.Sequence,
                entry.AgentId,
                entry.Action,
                entry.Quantity,
                entry.StructureId,
                $"{entry.Tick}|{entry.Sequence}|{entry.AgentId}|{entry.Action}|{entry.Quantity}|{entry.StructureId ?? string.Empty}"));
        }
        _projectedEventEntries += pageCount;

        if (!IsWorldOwned()) return null;
        return new SnowGlobeObserverSnapshot(
            _isPaused,
            _world.Tick,
            _world.AvailableWood,
            _world.AvailableStone,
            _world.StockpileWood,
            _world.StockpileStone,
            new ReadOnlyCollection<SnowGlobeObserverAgentSnapshot>(agents),
            new ReadOnlyCollection<SnowGlobeObserverStructureSnapshot>(structures),
            eventHistoryCount,
            eventCursor,
            eventCursor + events.Count < eventHistoryCount ? eventCursor + events.Count : null,
            new ReadOnlyCollection<SnowGlobeObserverEventSnapshot>(events),
            _stateDigest,
            _eventDigest);
    }
}
