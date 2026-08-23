using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Immutable, offline v1 scenarios and the complete scoring contract.</summary>
public static class CognitionQualityCorpusV1
{
    public const string CorpusSchemaVersion = "snow_globe_cognition_quality_corpus/v1";
    public const string ScoringSchemaVersion = "snow_globe_cognition_quality_scoring/v1";
    public const string ReportSchemaVersion = "snow_globe_cognition_quality_report/v1";
    public const string ValidatorIdentity = "snow_globe_validate_and_commit_v1";
    public const int ScenarioCount = 12;
    public const int CategoryCount = 4;
    public const int MaximumCorpusBytes = 16 * 1024;
    public const int MaximumPoints = ScenarioCount * CognitionQuality.PointsPerScenario;

    internal static IReadOnlyList<string> CategoryIds => Array.AsReadOnly(CategoryOrder.ToArray());
    internal static IReadOnlyList<string> Dispositions => Array.AsReadOnly(DispositionOrder.ToArray());
    internal static IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(Limitations.ToArray());
    internal static string ScoringDigestSha256 => CanonicalScoringDigest;

    // These are guards, not sources of truth. The digest is always derived from canonical contract bytes first.
    internal const string ExpectedScoringDigestSha256 = "043dc7f01ae544d4698e9c8b44c0f2c27b9f0a66fdba3a1e2249b868a64c35b0";
    internal const string ExpectedManifestDigestSha256 = "4de8c4a993b58875f27c5867c29a54679de789dacb03d2b4d8099e26340f1f8f";

    private const int ShelterWoodCost = 12;
    private const int ShelterStoneCost = 6;
    private const int StorageWoodCost = 8;
    private const int StorageStoneCost = 4;
    private const int MaximumProposalQuantity = 64;
    private static readonly string[] CategoryOrder = ["shelter_acquisition", "shelter_construction", "storage_progression", "safe_restraint"];
    private static readonly string[] DispositionOrder = ["no_proposal", "contract_invalid", "domain_rejected", "feasible_suboptimal", "maximum_utility"];
    private static readonly string[] Limitations = ["offline_fixed_corpus", "observable_action_utility_only", "no_unobserved_durability_preference", "no_comparative_selection"];
    private static readonly FrozenScenario[] Scenarios = CreateScenarios();
    private static readonly byte[] CanonicalScoringContract = WriteScoringContract(Scenarios);
    private static readonly string CanonicalScoringDigest = CognitionQualityHash.Sha256(CanonicalScoringContract);
    private static readonly byte[] CanonicalManifest = WriteManifest(Scenarios, CanonicalScoringDigest);
    private static readonly string CanonicalManifestDigest = CognitionQualityHash.Sha256(CanonicalManifest);

    public static CognitionQualityCorpusSnapshot CreateSnapshot()
    {
        if (CanonicalManifest.Length is 0 or > MaximumCorpusBytes
            || !string.Equals(CanonicalScoringDigest, ExpectedScoringDigestSha256, StringComparison.Ordinal)
            || !string.Equals(CanonicalManifestDigest, ExpectedManifestDigestSha256, StringComparison.Ordinal))
        {
            throw new CognitionQualityException(CognitionQualityErrors.CorpusSnapshotInvalid);
        }
        return new CognitionQualityCorpusSnapshot(CanonicalManifest, Scenarios.Select(item => item.Public).ToArray());
    }

    internal static void ValidateSnapshot(CognitionQualityCorpusSnapshot snapshot)
    {
        if (snapshot.CanonicalUtf8.Length > MaximumCorpusBytes
            || !snapshot.CanonicalUtf8.Span.SequenceEqual(CanonicalManifest)
            || !string.Equals(snapshot.CanonicalDigestSha256, CanonicalManifestDigest, StringComparison.Ordinal)
            || snapshot.Scenarios.Count != ScenarioCount
            || !snapshot.Scenarios.SequenceEqual(Scenarios.Select(item => item.Public)))
        {
            throw new CognitionQualityException(CognitionQualityErrors.CorpusSnapshotInvalid);
        }
    }

    internal static byte[] WriteSubmissionEnvelope(IReadOnlyList<CognitionQualitySubmission> submissions)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", "snow_globe_cognition_submission/v1");
            writer.WritePropertyName("submissions");
            writer.WriteStartArray();
            foreach (CognitionQualitySubmission submission in submissions)
            {
                writer.WriteStartObject();
                writer.WriteString("scenario_id", submission.ScenarioId);
                if (submission.Proposal is null)
                {
                    writer.WriteNull("proposal");
                }
                else
                {
                    writer.WritePropertyName("proposal");
                    writer.WriteStartObject();
                    writer.WriteString("agent_id", submission.Proposal.AgentId);
                    writer.WriteNumber("action", (int)submission.Proposal.Action);
                    writer.WriteNumber("quantity", submission.Proposal.Quantity);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    internal static CognitionQualityScenarioResult Score(string scenarioId, SnowGlobeActionProposal? proposal)
    {
        FrozenScenario scenario = Scenarios.Single(item => string.Equals(item.Public.ScenarioId, scenarioId, StringComparison.Ordinal));
        if (proposal is null) return Result(scenario, 0, "no_proposal", ["proposal_missing"]);
        if (!IsContractValid(scenario.Public.Observation, proposal)) return Result(scenario, 0, "contract_invalid", ["proposal_contract_invalid"]);

        SnowGlobeWorld world = CreateWorld(scenario);
        if (!string.Equals(world.StateDigest(), scenario.Public.StateDigestSha256, StringComparison.Ordinal)
            || !string.Equals(world.EventDigest(), scenario.Public.EventDigestSha256, StringComparison.Ordinal)
            || world.Observe("agent-00") != scenario.Public.Observation)
        {
            throw new CognitionQualityException(CognitionQualityErrors.CorpusSnapshotInvalid);
        }
        if (!world.ValidateAndCommit(proposal).Accepted) return Result(scenario, 0, "domain_rejected", ["proposal_domain_rejected"]);

        if (proposal.Action == scenario.PreferredAction)
        {
            int points = scenario.PreferredAction is SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone
                ? checked(25 + 75 * Math.Min(proposal.Quantity, scenario.RequiredProgress) / scenario.RequiredProgress)
                : CognitionQuality.PointsPerScenario;
            return points == CognitionQuality.PointsPerScenario
                ? Result(scenario, points, "maximum_utility", ["observable_target_met"])
                : Result(scenario, points, "feasible_suboptimal", ["partial_observable_progress"]);
        }

        int suboptimal = proposal.Action == SnowGlobeActionKind.Idle && scenario.PreferredAction != SnowGlobeActionKind.Idle ? 25 : 10;
        return Result(scenario, suboptimal, "feasible_suboptimal", proposal.Action == SnowGlobeActionKind.Idle ? ["observable_progress_available"] : ["lower_observable_utility"]);
    }

    private static CognitionQualityScenarioResult Result(FrozenScenario scenario, int points, string disposition, IReadOnlyList<string> codes) =>
        new(scenario.Public.ScenarioId, scenario.Public.CategoryId, points, checked(points * 100), disposition, codes);

    private static bool IsContractValid(SnowGlobeObservation observation, SnowGlobeActionProposal proposal) =>
        string.Equals(proposal.AgentId, observation.AgentId, StringComparison.Ordinal)
        && Enum.IsDefined(proposal.Action)
        && proposal.Action switch
        {
            SnowGlobeActionKind.Idle or SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage => proposal.Quantity == 0,
            SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone or SnowGlobeActionKind.MaintainShelter => proposal.Quantity is > 0 and <= MaximumProposalQuantity,
            _ => false
        };

    private static FrozenScenario[] CreateScenarios()
    {
        ScenarioDefinition[] definitions =
        [
            new("cq1", CategoryOrder[0], 64, 32, [], SnowGlobeActionKind.GatherWood, 12),
            new("cq2", CategoryOrder[0], 12, 32, [GatherWood(8)], SnowGlobeActionKind.GatherStone, 6),
            new("cq3", CategoryOrder[0], 12, 6, [GatherWood(12), GatherStone(4)], SnowGlobeActionKind.GatherStone, 2),
            new("cq4", CategoryOrder[1], 12, 6, [GatherWood(12), GatherStone(6)], SnowGlobeActionKind.BuildShelter, 0),
            new("cq5", CategoryOrder[1], 20, 10, [GatherWood(20), GatherStone(10)], SnowGlobeActionKind.BuildShelter, 0),
            new("cq6", CategoryOrder[1], 20, 10, [GatherWood(8), GatherStone(4), BuildStorage(), GatherWood(12), GatherStone(6)], SnowGlobeActionKind.BuildShelter, 0),
            new("cq7", CategoryOrder[2], 20, 10, [GatherWood(12), GatherStone(6), BuildShelter(), GatherStone(4)], SnowGlobeActionKind.GatherWood, 8),
            new("cq8", CategoryOrder[2], 20, 10, [GatherWood(12), GatherStone(6), BuildShelter(), GatherWood(8), GatherStone(4)], SnowGlobeActionKind.BuildStorage, 0),
            new("cq9", CategoryOrder[2], 28, 14, [GatherWood(12), GatherStone(6), BuildShelter(), GatherWood(16), GatherStone(8)], SnowGlobeActionKind.BuildStorage, 0),
            new("cq10", CategoryOrder[3], 0, 0, [], SnowGlobeActionKind.Idle, 0),
            new("cq11", CategoryOrder[3], 20, 10, [GatherWood(12), GatherStone(6), BuildShelter(), GatherWood(8), GatherStone(4), BuildStorage()], SnowGlobeActionKind.Idle, 0),
            new("cq12", CategoryOrder[3], 26, 13, [GatherWood(12), GatherStone(6), BuildShelter(), GatherWood(8), GatherStone(4), BuildStorage()], SnowGlobeActionKind.Idle, 0)
        ];
        return definitions.Select(CreateScenario).ToArray();
    }

    private static FrozenScenario CreateScenario(ScenarioDefinition definition)
    {
        SnowGlobeWorld world = CreateWorld(definition);
        SnowGlobeObservation observation = world.Observe("agent-00");
        CognitionQualityScenario value = new(definition.Id, definition.Category, observation, world.StateDigest(), world.EventDigest(), CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(CanonicalObservation(observation))));
        return new FrozenScenario(value, definition.AvailableWood, definition.AvailableStone, definition.Setup.ToArray(), definition.PreferredAction, definition.RequiredProgress);
    }

    private static SnowGlobeWorld CreateWorld(FrozenScenario scenario) => CreateWorld(new ScenarioDefinition("", "", scenario.AvailableWood, scenario.AvailableStone, scenario.Setup, scenario.PreferredAction, scenario.RequiredProgress));
    private static SnowGlobeWorld CreateWorld(ScenarioDefinition definition)
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(SnowGlobeScenario.FixedSeed, 1, definition.AvailableWood, definition.AvailableStone);
        foreach (SnowGlobeActionProposal proposal in definition.Setup)
        {
            if (!world.ValidateAndCommit(proposal).Accepted) throw new InvalidOperationException("Frozen setup is not feasible.");
        }
        return world;
    }

    private static byte[] WriteScoringContract(IReadOnlyList<FrozenScenario> scenarios)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", ScoringSchemaVersion);
            writer.WriteString("validator_identity", ValidatorIdentity);
            writer.WriteNumber("points_per_scenario", CognitionQuality.PointsPerScenario);
            writer.WriteNumber("maximum_points", MaximumPoints);
            writer.WriteNumber("maximum_proposal_quantity", MaximumProposalQuantity);
            writer.WriteNumber("maximum_submission_bytes", CognitionQuality.MaximumSubmissionBytes);
            writer.WritePropertyName("category_order"); WriteStringArray(writer, CategoryOrder);
            writer.WritePropertyName("disposition_order"); WriteStringArray(writer, DispositionOrder);
            writer.WritePropertyName("action_costs");
            writer.WriteStartObject();
            writer.WriteNumber("shelter_wood", ShelterWoodCost); writer.WriteNumber("shelter_stone", ShelterStoneCost);
            writer.WriteNumber("storage_wood", StorageWoodCost); writer.WriteNumber("storage_stone", StorageStoneCost);
            writer.WriteEndObject();
            writer.WritePropertyName("rules");
            writer.WriteStartObject();
            writer.WriteString("observation_agent_equality", "ordinal_exact");
            writer.WriteString("submitted_agent_normalization", "ascii_lowercase_letters_digits_hyphen;code_units=1..64;utf8_bytes=1..64");
            writer.WriteString("idle_build_quantity", "zero");
            writer.WriteString("gather_maintain_quantity", "integer_1..64");
            writer.WriteString("no_proposal", "points=0;disposition=no_proposal");
            writer.WriteString("contract_invalid", "points=0;disposition=contract_invalid");
            writer.WriteString("domain_rejected", "points=0;disposition=domain_rejected;authority=snow_globe_validate_and_commit_v1");
            writer.WriteString("preferred_build_idle", "points=100;disposition=maximum_utility");
            writer.WriteString("preferred_gather_partial", "points=25+floor(75*min(quantity,required_progress)/required_progress);points<100;disposition=feasible_suboptimal");
            writer.WriteString("preferred_gather_maximum", "points=100;disposition=maximum_utility");
            writer.WriteString("idle_when_progress_available", "points=25;disposition=feasible_suboptimal");
            writer.WriteString("other_feasible", "points=10;disposition=feasible_suboptimal");
            writer.WriteString("survival_order", "shelter_then_storage_then_idle");
            writer.WriteString("deficit_order", "checked_cross_products_tie_wood_first_unavailable_other");
            writer.WriteString("restraint", "both_structures_or_no_observable_progress_idle");
            writer.WriteString("maintenance", "not_preferred_durability_unobservable");
            writer.WriteEndObject();
            writer.WritePropertyName("defined_actions");
            writer.WriteStartArray();
            foreach (SnowGlobeActionKind action in Enum.GetValues<SnowGlobeActionKind>())
            {
                writer.WriteStartObject(); writer.WriteNumber("numeric", (int)action); writer.WriteString("name", action.ToString()); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("scenarios");
            writer.WriteStartArray();
            foreach (FrozenScenario scenario in scenarios)
            {
                writer.WriteStartObject();
                writer.WriteString("scenario_id", scenario.Public.ScenarioId);
                writer.WriteString("category_id", scenario.Public.CategoryId);
                writer.WriteNumber("available_wood", scenario.AvailableWood);
                writer.WriteNumber("available_stone", scenario.AvailableStone);
                writer.WritePropertyName("setup_proposals");
                writer.WriteStartArray();
                foreach (SnowGlobeActionProposal proposal in scenario.Setup) WriteProposal(writer, proposal);
                writer.WriteEndArray();
                writer.WriteNumber("preferred_action", (int)scenario.PreferredAction);
                writer.WriteNumber("required_progress", scenario.RequiredProgress);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] WriteManifest(IReadOnlyList<FrozenScenario> scenarios, string scoringDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", CorpusSchemaVersion);
            writer.WriteString("validator_identity", ValidatorIdentity);
            writer.WriteString("scoring_digest_sha256", scoringDigest);
            writer.WriteNumber("scenario_count", ScenarioCount);
            writer.WritePropertyName("categories"); WriteStringArray(writer, CategoryOrder);
            writer.WritePropertyName("scenarios"); writer.WriteStartArray();
            foreach (FrozenScenario scenario in scenarios)
            {
                CognitionQualityScenario value = scenario.Public;
                writer.WriteStartObject();
                writer.WriteString("scenario_id", value.ScenarioId); writer.WriteString("category_id", value.CategoryId);
                writer.WritePropertyName("observation"); WriteObservation(writer, value.Observation);
                writer.WriteString("observation_digest_sha256", value.ObservationDigestSha256);
                writer.WriteString("state_digest_sha256", value.StateDigestSha256);
                writer.WriteString("event_digest_sha256", value.EventDigestSha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteProposal(Utf8JsonWriter writer, SnowGlobeActionProposal proposal)
    {
        writer.WriteStartObject(); writer.WriteString("agent_id", proposal.AgentId); writer.WriteNumber("action", (int)proposal.Action); writer.WriteNumber("quantity", proposal.Quantity); writer.WriteEndObject();
    }
    private static void WriteStringArray(Utf8JsonWriter writer, IEnumerable<string> values) { writer.WriteStartArray(); foreach (string value in values) writer.WriteStringValue(value); writer.WriteEndArray(); }
    private static void WriteObservation(Utf8JsonWriter writer, SnowGlobeObservation value)
    {
        writer.WriteStartObject();
        writer.WriteString("agent_id", value.AgentId); writer.WriteNumber("home_slot", value.HomeSlot); writer.WriteNumber("tick", value.Tick);
        writer.WriteNumber("available_wood", value.AvailableWood); writer.WriteNumber("available_stone", value.AvailableStone);
        writer.WriteNumber("stockpile_wood", value.StockpileWood); writer.WriteNumber("stockpile_stone", value.StockpileStone);
        writer.WriteNumber("shelter_count", value.ShelterCount); writer.WriteNumber("storage_count", value.StorageCount); writer.WriteEndObject();
    }
    private static string CanonicalObservation(SnowGlobeObservation value) => string.Join("|", value.AgentId, value.HomeSlot, value.Tick, value.AvailableWood, value.AvailableStone, value.StockpileWood, value.StockpileStone, value.ShelterCount, value.StorageCount);
    private static SnowGlobeActionProposal GatherWood(int quantity) => new("agent-00", SnowGlobeActionKind.GatherWood, quantity);
    private static SnowGlobeActionProposal GatherStone(int quantity) => new("agent-00", SnowGlobeActionKind.GatherStone, quantity);
    private static SnowGlobeActionProposal BuildShelter() => new("agent-00", SnowGlobeActionKind.BuildShelter);
    private static SnowGlobeActionProposal BuildStorage() => new("agent-00", SnowGlobeActionKind.BuildStorage);
    private sealed record ScenarioDefinition(string Id, string Category, int AvailableWood, int AvailableStone, SnowGlobeActionProposal[] Setup, SnowGlobeActionKind PreferredAction, int RequiredProgress);
    private sealed record FrozenScenario(CognitionQualityScenario Public, int AvailableWood, int AvailableStone, SnowGlobeActionProposal[] Setup, SnowGlobeActionKind PreferredAction, int RequiredProgress);
}
