using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>One detached, model-visible prompt slot for the frozen offline cognition-quality corpus.</summary>
public sealed class CognitionQualityPromptEnvelopeSlot
{
    private readonly byte[] _promptUtf8;

    internal CognitionQualityPromptEnvelopeSlot(string scenarioId, string observationDigestSha256, byte[] promptUtf8)
    {
        ScenarioId = scenarioId;
        ObservationDigestSha256 = observationDigestSha256;
        _promptUtf8 = promptUtf8.ToArray();
        PromptDigestSha256 = CognitionQualityHash.Sha256(_promptUtf8);
    }

    public string ScenarioId { get; }
    public string ObservationDigestSha256 { get; }
    public int PromptByteCount => _promptUtf8.Length;
    public string PromptDigestSha256 { get; }
    public ReadOnlyMemory<byte> PromptUtf8 => _promptUtf8.ToArray();
    internal byte[] CopyPromptUtf8() => _promptUtf8.ToArray();
}

/// <summary>Detached canonical publication of exact offline prompt bytes; it carries no recorded response bytes.</summary>
public sealed class CognitionQualityPromptEnvelopePublication
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityPromptEnvelopeSlot[] _slots;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityPromptEnvelopePublication(byte[] canonicalUtf8, string payloadDigestSha256, string promptRevision, IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots, IEnumerable<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        PromptRevision = promptRevision;
        _slots = slots.Select(slot => new CognitionQualityPromptEnvelopeSlot(slot.ScenarioId, slot.ObservationDigestSha256, slot.CopyPromptUtf8())).ToArray();
        PromptSetDigestSha256 = ComputePromptSetDigest(_slots);
        _claimLimitationCodes = claimLimitationCodes.ToArray();
    }

    public string SchemaVersion => CognitionQualityPromptEnvelopeBuilderModule.SchemaVersion;
    public string Status => "complete";
    public string Semantics => "offline_canonical_prompt_publication_only";
    public string BuilderIdentity => CognitionQualityPromptEnvelopeBuilderModule.BuilderIdentity;
    public string PromptSchemaVersion => CognitionQualityPromptEnvelopeBuilderModule.PromptSchemaVersion;
    public string PromptRevision { get; }
    public string PromptSetDigestSha256 { get; }
    public IReadOnlyList<CognitionQualityPromptEnvelopeSlot> Slots => Array.AsReadOnly(_slots.Select(slot => new CognitionQualityPromptEnvelopeSlot(slot.ScenarioId, slot.ObservationDigestSha256, slot.CopyPromptUtf8())).ToArray());
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();

    /// <summary>Binds exact ordered caller-supplied response bytes to these frozen slots for the existing offline runner.</summary>
    public IReadOnlyList<CognitionQualityRecordedResponseFixture> BindRecordedResponses(CognitionQualityExecutionProvenance provenance, IReadOnlyList<ReadOnlyMemory<byte>> responses)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(responses);
        if (!string.Equals(provenance.PromptRevision, PromptRevision, StringComparison.Ordinal))
            throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.ProvenancePromptRevisionInvalid);
        if (!string.Equals(provenance.ProposalSchemaVersion, CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, StringComparison.Ordinal))
            throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.ProvenanceProposalSchemaInvalid);
        if (responses.Count != _slots.Length)
            throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.ResponseCountInvalid);

        byte[][] snapshots = new byte[_slots.Length][];
        try
        {
            int aggregate = 0;
            for (int index = 0; index < snapshots.Length; index++)
            {
                byte[] response = responses[index].ToArray();
                if (response.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes)
                {
                    CryptographicOperations.ZeroMemory(response);
                    throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.ResponseSizeInvalid);
                }
                aggregate = checked(aggregate + response.Length);
                if (aggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes)
                {
                    CryptographicOperations.ZeroMemory(response);
                    throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.ResponseAggregateSizeInvalid);
                }
                snapshots[index] = response;
            }

            CognitionQualityRecordedResponseFixture[] fixtures = new CognitionQualityRecordedResponseFixture[_slots.Length];
            for (int index = 0; index < fixtures.Length; index++)
                fixtures[index] = new CognitionQualityRecordedResponseFixture(_slots[index].ScenarioId, _slots[index].ObservationDigestSha256, snapshots[index]);
            return Array.AsReadOnly(fixtures);
        }
        finally
        {
            foreach (byte[]? snapshot in snapshots)
                if (snapshot is not null) CryptographicOperations.ZeroMemory(snapshot);
        }
    }

    private static string ComputePromptSetDigest(IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartArray();
        foreach (CognitionQualityPromptEnvelopeSlot slot in slots)
        {
            writer.WriteStartObject();
            writer.WriteString("scenario_id", slot.ScenarioId);
            writer.WriteString("observation_digest_sha256", slot.ObservationDigestSha256);
            writer.WriteNumber("prompt_byte_count", slot.PromptByteCount);
            writer.WriteString("prompt_digest_sha256", slot.PromptDigestSha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        return CognitionQualityHash.Sha256(buffer.WrittenSpan);
    }
}

/// <summary>Closed error surface for offline prompt publication and recorded-byte binding failures.</summary>
public sealed class CognitionQualityPromptEnvelopeException : Exception
{
    internal CognitionQualityPromptEnvelopeException(string code) : base(code)
    {
        if (!CognitionQualityPromptEnvelopeErrors.IsAllowlisted(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Pure synchronous publisher of canonical model-visible prompts for the frozen twelve-scenario corpus.</summary>
public static class CognitionQualityPromptEnvelopeBuilderModule
{
    public const string SchemaVersion = "snow_globe_cognition_quality_prompt_envelope_publication/v1";
    public const string BuilderIdentity = "snow_globe_cognition_quality_prompt_envelope_builder/v1";
    public const string PromptSchemaVersion = "snow_globe_cognition_quality_prompt/v1";
    public const int MaximumPromptBytes = 2048;
    public const int MaximumAggregatePromptBytes = 12 * MaximumPromptBytes;
    public const int MaximumPublicationBytes = 64 * 1024;

    private static readonly string[] ClaimLimitations =
    [
        "offline_prompt_publication_only",
        "identity_attribution_is_caller_attested",
        "no_execution_attestation",
        "raw_response_not_retained",
        "no_provider_status_retry_or_charge_evidence",
        "offline_fixed_corpus",
        "observable_action_utility_only",
        "no_general_quality_claim",
        "no_cost_claim",
        "no_winner_claim"
    ];

    public static CognitionQualityPromptEnvelopePublication Create(string promptRevision)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(promptRevision))
            throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.PromptRevisionInvalid);

        CognitionQualityCorpusSnapshot corpus;
        try { corpus = CognitionQualityCorpusV1.CreateSnapshot(); }
        catch (CognitionQualityException) { throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.CorpusInvalid); }

        CognitionQualityPromptEnvelopeSlot[] slots = new CognitionQualityPromptEnvelopeSlot[CognitionQualityCorpusV1.ScenarioCount];
        int aggregate = 0;
        foreach ((CognitionQualityScenario scenario, int index) in corpus.Scenarios.Select((scenario, index) => (scenario, index)))
        {
            byte[] prompt = WritePrompt(promptRevision, scenario.Observation);
            if (prompt.Length is < 1 or > MaximumPromptBytes)
            {
                CryptographicOperations.ZeroMemory(prompt);
                throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.PromptSizeInvalid);
            }
            aggregate = checked(aggregate + prompt.Length);
            if (aggregate > MaximumAggregatePromptBytes)
            {
                CryptographicOperations.ZeroMemory(prompt);
                throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.PromptAggregateSizeInvalid);
            }
            slots[index] = new CognitionQualityPromptEnvelopeSlot(scenario.ScenarioId, scenario.ObservationDigestSha256, prompt);
            CryptographicOperations.ZeroMemory(prompt);
        }

        string promptSetDigest = ComputePromptSetDigest(slots);
        byte[] payload = WritePublication(corpus, promptRevision, slots, promptSetDigest, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = WritePublication(corpus, promptRevision, slots, promptSetDigest, payloadDigest);
        if (canonical.Length is < 1 or > MaximumPublicationBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw new CognitionQualityPromptEnvelopeException(CognitionQualityPromptEnvelopeErrors.PublicationSizeInvalid);
        }
        return new CognitionQualityPromptEnvelopePublication(canonical, payloadDigest, promptRevision, slots, ClaimLimitations);
    }

    private static byte[] WritePrompt(string promptRevision, SnowGlobeObservation observation)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", PromptSchemaVersion);
        writer.WriteString("prompt_revision", promptRevision);
        writer.WriteString("task", "Propose one legal action from the current observation. The deterministic validator is the sole authority.");
        writer.WritePropertyName("rules");
        writer.WriteStartArray();
        writer.WriteStringValue("Policy: shelter then storage then Idle.");
        writer.WriteStringValue("Shelter costs 12 wood and 6 stone; storage costs 8 wood and 4 stone.");
        writer.WriteStringValue("Build the affordable current target. Otherwise gather the missing resource using proportional deficit cross-products; tie wood first; if the other resource is unavailable gather wood.");
        writer.WriteStringValue("Gather the exact deficit capped by available resource and 64.");
        writer.WriteStringValue("Idle if both structures exist or no observable progress is possible.");
        writer.WriteStringValue("Durability is unobserved; do not infer a maintenance need.");
        writer.WriteStringValue("Legal actions: Idle, GatherWood, GatherStone, BuildShelter, BuildStorage, MaintainShelter.");
        writer.WriteStringValue("Quantity: Idle, BuildShelter, and BuildStorage require 0; GatherWood, GatherStone, and MaintainShelter require integer 1..64.");
        writer.WriteEndArray();
        writer.WritePropertyName("observation");
        WriteObservation(writer, observation);
        writer.WritePropertyName("response_contract");
        writer.WriteStartObject();
        writer.WriteString("format", "single_json_object");
        writer.WriteString("properties", "agent_id,action,quantity");
        writer.WriteString("agent_id", "exact_observation_agent_id");
        writer.WriteString("action", "one_legal_action_name");
        writer.WriteString("quantity", "integer_per_legal_action_quantity_rule");
        writer.WriteString("json", "{\"agent_id\":\"<exact_observation_agent_id>\",\"action\":\"<legal_action_name>\",\"quantity\":<integer>}");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteObservation(Utf8JsonWriter writer, SnowGlobeObservation value)
    {
        writer.WriteStartObject();
        writer.WriteString("agent_id", value.AgentId);
        writer.WriteNumber("home_slot", value.HomeSlot);
        writer.WriteNumber("tick", value.Tick);
        writer.WriteNumber("available_wood", value.AvailableWood);
        writer.WriteNumber("available_stone", value.AvailableStone);
        writer.WriteNumber("stockpile_wood", value.StockpileWood);
        writer.WriteNumber("stockpile_stone", value.StockpileStone);
        writer.WriteNumber("shelter_count", value.ShelterCount);
        writer.WriteNumber("storage_count", value.StorageCount);
        writer.WriteEndObject();
    }

    private static string ComputePromptSetDigest(IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartArray();
        foreach (CognitionQualityPromptEnvelopeSlot slot in slots)
        {
            writer.WriteStartObject();
            writer.WriteString("scenario_id", slot.ScenarioId);
            writer.WriteString("observation_digest_sha256", slot.ObservationDigestSha256);
            writer.WriteNumber("prompt_byte_count", slot.PromptByteCount);
            writer.WriteString("prompt_digest_sha256", slot.PromptDigestSha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        return CognitionQualityHash.Sha256(buffer.WrittenSpan);
    }

    private static byte[] WritePublication(CognitionQualityCorpusSnapshot corpus, string promptRevision, IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots, string promptSetDigest, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("semantics", "offline_canonical_prompt_publication_only");
        writer.WriteString("builder_identity", BuilderIdentity);
        writer.WriteString("prompt_schema_version", PromptSchemaVersion);
        writer.WriteString("prompt_revision", promptRevision);
        writer.WriteString("corpus_digest_sha256", corpus.CanonicalDigestSha256);
        writer.WriteString("scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256);
        writer.WriteString("validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
        writer.WriteString("runner_identity", CognitionQualityRecordedResponseRunnerModule.RunnerIdentity);
        writer.WriteString("parser_identity", CognitionQualityRecordedResponseRunnerModule.ParserIdentity);
        writer.WriteString("proposal_schema_version", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion);
        writer.WriteNumber("scenario_count", slots.Count);
        writer.WritePropertyName("limits");
        writer.WriteStartObject();
        writer.WriteNumber("maximum_prompt_bytes", MaximumPromptBytes);
        writer.WriteNumber("maximum_aggregate_prompt_bytes", MaximumAggregatePromptBytes);
        writer.WriteNumber("maximum_publication_bytes", MaximumPublicationBytes);
        writer.WriteNumber("maximum_response_bytes", CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes);
        writer.WriteNumber("maximum_aggregate_response_bytes", CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes);
        writer.WriteEndObject();
        writer.WritePropertyName("slots");
        writer.WriteStartArray();
        foreach (CognitionQualityPromptEnvelopeSlot slot in slots)
        {
            byte[] prompt = slot.CopyPromptUtf8();
            try
            {
                writer.WriteStartObject();
                writer.WriteString("scenario_id", slot.ScenarioId);
                writer.WriteString("observation_digest_sha256", slot.ObservationDigestSha256);
                writer.WriteNumber("prompt_byte_count", slot.PromptByteCount);
                writer.WriteString("prompt_digest_sha256", slot.PromptDigestSha256);
                writer.WriteBase64String("prompt_utf8_base64", prompt);
                writer.WriteNumber("response_byte_count", 0);
                writer.WriteNull("response_digest_sha256");
                writer.WriteEndObject();
            }
            finally { CryptographicOperations.ZeroMemory(prompt); }
        }
        writer.WriteEndArray();
        writer.WriteString("prompt_set_digest_sha256", promptSetDigest);
        writer.WritePropertyName("claim_limitation_codes");
        writer.WriteStartArray();
        foreach (string limitation in ClaimLimitations) writer.WriteStringValue(limitation);
        writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("publication_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}

internal static class CognitionQualityPromptEnvelopeErrors
{
    internal const string PromptRevisionInvalid = "prompt_revision_invalid";
    internal const string CorpusInvalid = "corpus_invalid";
    internal const string PromptSizeInvalid = "prompt_size_invalid";
    internal const string PromptAggregateSizeInvalid = "prompt_aggregate_size_invalid";
    internal const string PublicationSizeInvalid = "publication_size_invalid";
    internal const string ProvenancePromptRevisionInvalid = "provenance_prompt_revision_invalid";
    internal const string ProvenanceProposalSchemaInvalid = "provenance_proposal_schema_invalid";
    internal const string ResponseCountInvalid = "response_count_invalid";
    internal const string ResponseSizeInvalid = "response_size_invalid";
    internal const string ResponseAggregateSizeInvalid = "response_aggregate_size_invalid";
    internal static bool IsAllowlisted(string code) => code is PromptRevisionInvalid or CorpusInvalid or PromptSizeInvalid or PromptAggregateSizeInvalid or PublicationSizeInvalid or ProvenancePromptRevisionInvalid or ProvenanceProposalSchemaInvalid or ResponseCountInvalid or ResponseSizeInvalid or ResponseAggregateSizeInvalid;
}
