using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

public enum SnowGlobeActionKind
{
    Idle,
    GatherWood,
    GatherStone,
    BuildShelter,
    BuildStorage,
    MaintainShelter
}

public enum SnowGlobeStructureKind
{
    Shelter,
    Storage
}

/// <summary>
/// Persisted actor facts. Inference implementations never receive this mutable record.
/// </summary>
public sealed class SnowGlobeAgentRecord
{
    public SnowGlobeAgentRecord(string agentId, int homeSlot)
    {
        AgentId = agentId;
        HomeSlot = homeSlot;
    }

    public string AgentId { get; }
    public int HomeSlot { get; }
    public int CompletedActions { get; internal set; }
}

public sealed class SnowGlobeStructure
{
    public SnowGlobeStructure(string structureId, SnowGlobeStructureKind kind, int durability)
    {
        StructureId = structureId;
        Kind = kind;
        Durability = durability;
    }

    public string StructureId { get; }
    public SnowGlobeStructureKind Kind { get; }
    public int Durability { get; internal set; }
}

public sealed record SnowGlobeEvent(
    int Tick,
    int Sequence,
    string AgentId,
    SnowGlobeActionKind Action,
    int Quantity,
    string? StructureId);

public sealed record SnowGlobeActionProposal(
    string AgentId,
    SnowGlobeActionKind Action,
    int Quantity = 0);

public sealed record SnowGlobeCommitResult(bool Accepted, string? RejectionReason)
{
    public static SnowGlobeCommitResult Accept() => new(true, null);
    public static SnowGlobeCommitResult Reject(string reason) => new(false, reason);
}

/// <summary>Value-only result for an atomic expected-identity commit attempt.</summary>
internal sealed record SnowGlobeExpectedCommitResult(bool IdentityMatched, SnowGlobeCommitResult CommitResult, SnowGlobeEvent? CommittedEvent, long ResultingRevision);

/// <summary>Value-only coherent world identity used only within the lab boundary.</summary>
internal sealed record SnowGlobeWorldIdentity(int Tick, int EventCount, long Revision, string StateDigest, string EventDigest);

/// <summary>Value-only replay source captured under the world's mutation lock.</summary>
internal sealed record SnowGlobeWorldReplaySnapshot(int Seed, int AgentCount, int Tick, long Revision, int AvailableWood, int AvailableStone, IReadOnlyList<SnowGlobeEvent> Events);

internal sealed record SnowGlobeWorldObserverAgentValue(string AgentId, int HomeSlot, int CompletedActions);

internal sealed record SnowGlobeWorldObserverStructureValue(string StructureId, SnowGlobeStructureKind Kind, int Durability);

/// <summary>A bounded value-only observer read captured under the mutation lock.</summary>
internal sealed record SnowGlobeWorldObserverProjection(
    int Tick,
    long Revision,
    int AvailableWood,
    int AvailableStone,
    int StockpileWood,
    int StockpileStone,
    int EventHistoryCount,
    IReadOnlyList<SnowGlobeWorldObserverAgentValue> Agents,
    IReadOnlyList<SnowGlobeWorldObserverStructureValue> Structures,
    IReadOnlyList<SnowGlobeEvent> Events);

public sealed class SnowGlobeWorld
{
    private const int ShelterWoodCost = 12;
    private const int ShelterStoneCost = 6;
    private const int StorageWoodCost = 8;
    private const int StorageStoneCost = 4;
    private const int ShelterDurability = 10;
    private const int StorageDurability = 8;
    private const int MaxShelterDurability = 12;

    private readonly SortedDictionary<string, SnowGlobeAgentRecord> _agents = new(StringComparer.Ordinal);
    private readonly List<SnowGlobeStructure> _structures = new();
    private readonly List<SnowGlobeEvent> _events = new();
    private readonly object _mutationGate = new();
    private int _nextStructureNumber = 1;
    private long _mutationRevision;

    private SnowGlobeWorld(int seed, int availableWood, int availableStone)
    {
        Seed = seed;
        AvailableWood = availableWood;
        AvailableStone = availableStone;
    }

    public int Seed { get; }
    public int Tick { get; private set; }
    public int AvailableWood { get; private set; }
    public int AvailableStone { get; private set; }
    public int StockpileWood { get; private set; }
    public int StockpileStone { get; private set; }
    /// <summary>Detached values; callers cannot downcast to or mutate backing world records.</summary>
    public IReadOnlyCollection<SnowGlobeAgentRecord> Agents
    {
        get
        {
            lock (_mutationGate)
            {
                return new ReadOnlyCollection<SnowGlobeAgentRecord>(_agents.Values
                    .Select(agent => new SnowGlobeAgentRecord(agent.AgentId, agent.HomeSlot) { CompletedActions = agent.CompletedActions })
                    .ToList());
            }
        }
    }

    /// <summary>Detached values; callers cannot downcast to or mutate backing world records.</summary>
    public IReadOnlyList<SnowGlobeStructure> Structures
    {
        get
        {
            lock (_mutationGate)
            {
                return new ReadOnlyCollection<SnowGlobeStructure>(_structures
                    .Select(structure => new SnowGlobeStructure(structure.StructureId, structure.Kind, structure.Durability))
                    .ToList());
            }
        }
    }

    /// <summary>Detached immutable events captured under the mutation lock.</summary>
    public IReadOnlyList<SnowGlobeEvent> Events
    {
        get
        {
            lock (_mutationGate)
            {
                return new ReadOnlyCollection<SnowGlobeEvent>(_events.ToList());
            }
        }
    }

    public static SnowGlobeWorld Create(int seed, int agentCount, int availableWood = 64, int availableStone = 32)
    {
        if (agentCount <= 0 || agentCount > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(agentCount), "The lab supports 1 through 64 agents.");
        }

        SnowGlobeWorld world = new(seed, availableWood, availableStone);
        for (int index = 0; index < agentCount; index++)
        {
            string agentId = $"agent-{index:D2}";
            world._agents.Add(agentId, new SnowGlobeAgentRecord(agentId, index));
        }

        return world;
    }

    public void AdvanceTick()
    {
        lock (_mutationGate)
        {
            AdvanceTickLocked();
        }
    }

    public SnowGlobeObservation Observe(string agentId)
    {
        lock (_mutationGate)
        {
            if (!_agents.TryGetValue(agentId, out SnowGlobeAgentRecord? agent))
            {
                throw new InvalidOperationException($"Unknown agent '{agentId}'.");
            }

            return new SnowGlobeObservation(
                agent.AgentId,
                agent.HomeSlot,
                Tick,
                AvailableWood,
                AvailableStone,
                StockpileWood,
                StockpileStone,
                _structures.Count(structure => structure.Kind == SnowGlobeStructureKind.Shelter),
                _structures.Count(structure => structure.Kind == SnowGlobeStructureKind.Storage));
        }
    }

    public SnowGlobeCommitResult ValidateAndCommit(SnowGlobeActionProposal proposal)
    {
        lock (_mutationGate)
        {
            return ValidateAndCommitLocked(proposal);
        }
    }

    /// <summary>
    /// Atomically proves the exact caller-supplied identity then runs the unchanged deterministic
    /// validator and commit. No world reference or mutable state crosses this boundary.
    /// </summary>
    internal SnowGlobeExpectedCommitResult ValidateAndCommitIfIdentityMatches(
        int expectedTick,
        string expectedStateDigest,
        string expectedEventDigest,
        SnowGlobeActionProposal proposal)
    {
        lock (_mutationGate)
        {
            bool identityMatched = Tick == expectedTick
                && string.Equals(StateDigestLocked(), expectedStateDigest, StringComparison.Ordinal)
                && string.Equals(EventDigestLocked(), expectedEventDigest, StringComparison.Ordinal);
            if (!identityMatched)
            {
                return new SnowGlobeExpectedCommitResult(false, SnowGlobeCommitResult.Reject("expected_identity_mismatch"), null, _mutationRevision);
            }

            SnowGlobeCommitResult result = ValidateAndCommitLocked(proposal);
            return new SnowGlobeExpectedCommitResult(true, result, result.Accepted ? _events[^1] : null, _mutationRevision);
        }
    }

    internal SnowGlobeWorldIdentity CaptureIdentity()
    {
        lock (_mutationGate)
        {
            return new SnowGlobeWorldIdentity(Tick, _events.Count, _mutationRevision, StateDigestLocked(), EventDigestLocked());
        }
    }

    internal SnowGlobeWorldReplaySnapshot CaptureReplaySnapshot()
    {
        lock (_mutationGate)
        {
            return new SnowGlobeWorldReplaySnapshot(Seed, _agents.Count, Tick, _mutationRevision, AvailableWood, AvailableStone, _events.ToArray());
        }
    }

    internal bool MatchesRevision(long expectedRevision)
    {
        lock (_mutationGate)
        {
            return _mutationRevision == expectedRevision;
        }
    }

    internal SnowGlobeWorldObserverProjection? CaptureObserverProjection(long expectedRevision, int eventCursor, int maximumWindow)
    {
        lock (_mutationGate)
        {
            if (_mutationRevision != expectedRevision || eventCursor < 0 || eventCursor > _events.Count || maximumWindow < 0)
            {
                return null;
            }

            int pageCount = Math.Min(maximumWindow, _events.Count - eventCursor);
            List<SnowGlobeEvent> page = new(pageCount);
            for (int index = eventCursor; index < eventCursor + pageCount; index++) page.Add(_events[index]);
            return new SnowGlobeWorldObserverProjection(
                Tick,
                _mutationRevision,
                AvailableWood,
                AvailableStone,
                StockpileWood,
                StockpileStone,
                _events.Count,
                new ReadOnlyCollection<SnowGlobeWorldObserverAgentValue>(_agents.Values
                    .Select(agent => new SnowGlobeWorldObserverAgentValue(agent.AgentId, agent.HomeSlot, agent.CompletedActions)).ToList()),
                new ReadOnlyCollection<SnowGlobeWorldObserverStructureValue>(_structures
                    .OrderBy(structure => structure.StructureId, StringComparer.Ordinal)
                    .Select(structure => new SnowGlobeWorldObserverStructureValue(structure.StructureId, structure.Kind, structure.Durability)).ToList()),
                new ReadOnlyCollection<SnowGlobeEvent>(page));
        }
    }

    private SnowGlobeCommitResult ValidateAndCommitLocked(SnowGlobeActionProposal proposal)
    {
        if (!_agents.TryGetValue(proposal.AgentId, out SnowGlobeAgentRecord? agent))
        {
            return SnowGlobeCommitResult.Reject("unknown_agent");
        }

        SnowGlobeCommitResult validation = Validate(proposal);
        if (!validation.Accepted)
        {
            return validation;
        }

        string? structureId = null;
        switch (proposal.Action)
        {
            case SnowGlobeActionKind.Idle:
                break;
            case SnowGlobeActionKind.GatherWood:
                AvailableWood -= proposal.Quantity;
                StockpileWood += proposal.Quantity;
                break;
            case SnowGlobeActionKind.GatherStone:
                AvailableStone -= proposal.Quantity;
                StockpileStone += proposal.Quantity;
                break;
            case SnowGlobeActionKind.BuildShelter:
                StockpileWood -= ShelterWoodCost;
                StockpileStone -= ShelterStoneCost;
                structureId = CreateStructure(SnowGlobeStructureKind.Shelter, ShelterDurability);
                break;
            case SnowGlobeActionKind.BuildStorage:
                StockpileWood -= StorageWoodCost;
                StockpileStone -= StorageStoneCost;
                structureId = CreateStructure(SnowGlobeStructureKind.Storage, StorageDurability);
                break;
            case SnowGlobeActionKind.MaintainShelter:
                StockpileWood -= proposal.Quantity;
                SnowGlobeStructure shelter = _structures.First(structure => structure.Kind == SnowGlobeStructureKind.Shelter);
                shelter.Durability = Math.Min(MaxShelterDurability, shelter.Durability + proposal.Quantity);
                structureId = shelter.StructureId;
                break;
            default:
                return SnowGlobeCommitResult.Reject("unknown_action");
        }

        agent.CompletedActions++;
        _events.Add(new SnowGlobeEvent(Tick, _events.Count, proposal.AgentId, proposal.Action, proposal.Quantity, structureId));
        _mutationRevision++;
        return SnowGlobeCommitResult.Accept();
    }

    public void Replay(SnowGlobeEvent recorded)
    {
        lock (_mutationGate)
        {
            if (recorded.Sequence != _events.Count)
            {
                throw new InvalidOperationException("Replay events must have contiguous deterministic sequence values.");
            }

            while (Tick < recorded.Tick)
            {
                AdvanceTickLocked();
            }

            if (Tick != recorded.Tick)
            {
                throw new InvalidOperationException("Replay cannot move world time backward.");
            }

            SnowGlobeCommitResult result = ValidateAndCommitLocked(new SnowGlobeActionProposal(recorded.AgentId, recorded.Action, recorded.Quantity));
            if (!result.Accepted || !string.Equals(_events[^1].StructureId, recorded.StructureId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Replay event does not match the deterministic world contract.");
            }
        }
    }

    public string StateDigest()
    {
        lock (_mutationGate)
        {
            return StateDigestLocked();
        }
    }

    public string EventDigest()
    {
        lock (_mutationGate)
        {
            return EventDigestLocked();
        }
    }

    private SnowGlobeCommitResult Validate(SnowGlobeActionProposal proposal)
    {
        return proposal.Action switch
        {
            SnowGlobeActionKind.Idle when proposal.Quantity == 0 => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.GatherWood when proposal.Quantity > 0 && proposal.Quantity <= AvailableWood => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.GatherStone when proposal.Quantity > 0 && proposal.Quantity <= AvailableStone => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.BuildShelter when proposal.Quantity == 0 && StockpileWood >= ShelterWoodCost && StockpileStone >= ShelterStoneCost => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.BuildStorage when proposal.Quantity == 0 && StockpileWood >= StorageWoodCost && StockpileStone >= StorageStoneCost => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.MaintainShelter when proposal.Quantity > 0 && StockpileWood >= proposal.Quantity && _structures.Any(structure => structure.Kind == SnowGlobeStructureKind.Shelter) => SnowGlobeCommitResult.Accept(),
            SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone when proposal.Quantity <= 0 => SnowGlobeCommitResult.Reject("quantity_must_be_positive"),
            SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage when proposal.Quantity != 0 => SnowGlobeCommitResult.Reject("construction_quantity_must_be_zero"),
            SnowGlobeActionKind.MaintainShelter when !_structures.Any(structure => structure.Kind == SnowGlobeStructureKind.Shelter) => SnowGlobeCommitResult.Reject("shelter_missing"),
            _ => SnowGlobeCommitResult.Reject("insufficient_resources_or_invalid_action")
        };
    }

    private string CreateStructure(SnowGlobeStructureKind kind, int durability)
    {
        string structureId = $"{kind.ToString().ToLowerInvariant()}-{_nextStructureNumber:D3}";
        _nextStructureNumber++;
        _structures.Add(new SnowGlobeStructure(structureId, kind, durability));
        return structureId;
    }

    private string CanonicalState()
    {
        StringBuilder builder = new();
        builder.Append($"seed={Seed}|tick={Tick}|wood={AvailableWood}|stone={AvailableStone}|stockWood={StockpileWood}|stockStone={StockpileStone}");
        foreach (SnowGlobeAgentRecord agent in _agents.Values)
        {
            builder.Append($"|agent={agent.AgentId}:{agent.HomeSlot}:{agent.CompletedActions}");
        }

        foreach (SnowGlobeStructure structure in _structures.OrderBy(structure => structure.StructureId, StringComparer.Ordinal))
        {
            builder.Append($"|structure={structure.StructureId}:{structure.Kind}:{structure.Durability}");
        }

        return builder.ToString();
    }

    private static string CanonicalEvent(SnowGlobeEvent item) =>
        $"{item.Tick}|{item.Sequence}|{item.AgentId}|{item.Action}|{item.Quantity}|{item.StructureId ?? string.Empty}";

    private string StateDigestLocked() => Sha256(CanonicalState());

    private string EventDigestLocked() => Sha256(string.Join("\n", _events.Select(CanonicalEvent)));

    private void AdvanceTickLocked()
    {
        Tick++;
        _mutationRevision++;
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
