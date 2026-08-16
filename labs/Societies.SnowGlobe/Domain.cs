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
    private int _nextStructureNumber = 1;

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
    public IReadOnlyCollection<SnowGlobeAgentRecord> Agents => _agents.Values;
    public IReadOnlyList<SnowGlobeStructure> Structures => _structures;
    public IReadOnlyList<SnowGlobeEvent> Events => _events;

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

    public void AdvanceTick() => Tick++;

    public SnowGlobeObservation Observe(string agentId)
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

    public SnowGlobeCommitResult ValidateAndCommit(SnowGlobeActionProposal proposal)
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
        return SnowGlobeCommitResult.Accept();
    }

    public void Replay(SnowGlobeEvent recorded)
    {
        if (recorded.Sequence != _events.Count)
        {
            throw new InvalidOperationException("Replay events must have contiguous deterministic sequence values.");
        }

        while (Tick < recorded.Tick)
        {
            AdvanceTick();
        }

        if (Tick != recorded.Tick)
        {
            throw new InvalidOperationException("Replay cannot move world time backward.");
        }

        SnowGlobeCommitResult result = ValidateAndCommit(new SnowGlobeActionProposal(recorded.AgentId, recorded.Action, recorded.Quantity));
        if (!result.Accepted || !string.Equals(_events[^1].StructureId, recorded.StructureId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Replay event does not match the deterministic world contract.");
        }
    }

    public string StateDigest() => Sha256(CanonicalState());

    public string EventDigest() => Sha256(string.Join("\n", _events.Select(CanonicalEvent)));

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

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
