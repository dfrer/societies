using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>A detached source-parser slot; null preserves an exact no-proposal outcome.</summary>
public sealed record CognitionQualityNormalizedProposal(string ScenarioId, SnowGlobeActionProposal? Proposal);

/// <summary>Provider-neutral, raw-free projection of the exact ordered proposal batch used for comparison.</summary>
public sealed class CognitionQualityNormalizedProposalEvidence
{
    private readonly byte[] _canonicalUtf8;
    private readonly CognitionQualityNormalizedProposal[] _proposals;
    private readonly string[] _claimLimitationCodes;

    internal CognitionQualityNormalizedProposalEvidence(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string sourceEvidenceSchemaVersion,
        string sourceEvidenceDigestSha256,
        IReadOnlyList<CognitionQualityNormalizedProposal> proposals,
        IReadOnlyList<string> claimLimitationCodes)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _proposals = proposals.Select(Detach).ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        SourceEvidenceSchemaVersion = sourceEvidenceSchemaVersion;
        SourceEvidenceDigestSha256 = sourceEvidenceDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => CognitionQualityNormalizedProposalEvidenceCodec.SchemaVersion;
    public string Status => "complete";
    public string SourceEvidenceSchemaVersion { get; }
    public string SourceEvidenceDigestSha256 { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public IReadOnlyList<CognitionQualityNormalizedProposal> Proposals => Array.AsReadOnly(_proposals.Select(Detach).ToArray());
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();

    private static CognitionQualityNormalizedProposal Detach(CognitionQualityNormalizedProposal value) =>
        new(value.ScenarioId, value.Proposal is null ? null : value.Proposal with { });
}

/// <summary>Closed, non-echoing failure surface for untrusted normalized-proposal evidence.</summary>
public sealed class CognitionQualityNormalizedProposalEvidenceException : Exception
{
    internal CognitionQualityNormalizedProposalEvidenceException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "evidence_size_invalid" or "evidence_utf8_invalid" or "evidence_json_invalid" or
        "evidence_shape_invalid" or "evidence_noncanonical" or "evidence_value_invalid" or
        "evidence_digest_invalid" or "evidence_binding_invalid" or "proposal_count_invalid" or
        "proposal_order_invalid" or "proposal_shape_invalid" or "proposal_contract_invalid" or
        "evidence_payload_digest_invalid" => code,
        _ => "evidence_validation_failed"
    };
}

/// <summary>Strict bounded codec and the only source-evidence-to-proposal projection seam.</summary>
public static class CognitionQualityNormalizedProposalEvidenceCodec
{
    public const string SchemaVersion = "snow_globe_cognition_quality_normalized_proposals/v1";
    public const int MaximumEvidenceBytes = 16 * 1024;
    public const int MaximumJsonDepth = 6;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "source_evidence_schema_version", "source_evidence_digest_sha256",
        "corpus_schema_version", "corpus_digest_sha256", "scoring_digest_sha256", "validator_identity",
        "prompt_publication_digest_sha256", "prompt_set_digest_sha256", "proposal_schema_version",
        "proposals", "claim_limitation_codes", "evidence_payload_digest_sha256"
    ];
    private static readonly string[] ProposalNames = ["scenario_id", "proposal"];
    private static readonly string[] ActionNames = ["agent_id", "action", "quantity"];
    private static readonly string[] Claims =
    [
        "normalized_proposal_slots_only",
        "nullable_no_proposal_preserves_source_parser_outcome",
        "undefined_action_enum_not_representable",
        "source_digest_integrity_not_authenticity",
        "no_raw_prompt_response_or_reasoning_retention",
        "no_secret_or_provider_metadata_retention",
        "no_world_or_simulation_authority"
    ];
    private static readonly string[] SourceSchemas =
    [
        CognitionQualityRecordingEvidenceModule.SchemaVersion,
        OpenRouterPremiumEvidenceArtifactModule.SchemaVersion
    ];

    public static CognitionQualityNormalizedProposalEvidence Create(
        string sourceEvidenceSchemaVersion,
        string sourceEvidenceDigestSha256,
        IReadOnlyList<CognitionQualitySubmission> proposals)
    {
        ArgumentNullException.ThrowIfNull(sourceEvidenceSchemaVersion);
        ArgumentNullException.ThrowIfNull(sourceEvidenceDigestSha256);
        ArgumentNullException.ThrowIfNull(proposals);
        if (!SourceSchemas.Contains(sourceEvidenceSchemaVersion, StringComparer.Ordinal)) throw Failure("evidence_binding_invalid");
        if (!IsDigest(sourceEvidenceDigestSha256)) throw Failure("evidence_digest_invalid");

        CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
        CognitionQualityNormalizedProposal[] snapshot = Snapshot(corpus, proposals);
        byte[] payload = Write(sourceEvidenceSchemaVersion, sourceEvidenceDigestSha256, corpus, snapshot, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(sourceEvidenceSchemaVersion, sourceEvidenceDigestSha256, corpus, snapshot, payloadDigest);
        if (canonical.Length is < 1 or > MaximumEvidenceBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("evidence_size_invalid");
        }
        return new CognitionQualityNormalizedProposalEvidence(canonical, payloadDigest, sourceEvidenceSchemaVersion, sourceEvidenceDigestSha256, snapshot, Claims);
    }

    public static CognitionQualityNormalizedProposalEvidence CreateFromRecording(CognitionQualityRecordingEvidence recordingEvidence)
    {
        ArgumentNullException.ThrowIfNull(recordingEvidence);
        if (!string.Equals(recordingEvidence.SchemaVersion, CognitionQualityRecordingEvidenceModule.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(recordingEvidence.Status, "complete", StringComparison.Ordinal)
            || !string.Equals(recordingEvidence.CanonicalDigestSha256, CognitionQualityHash.Sha256(recordingEvidence.CanonicalUtf8.Span), StringComparison.Ordinal))
            throw Failure("evidence_binding_invalid");
        return Create(recordingEvidence.SchemaVersion, recordingEvidence.CanonicalDigestSha256, recordingEvidence.RecordedResponseRun.ProposalBatch);
    }

    public static CognitionQualityNormalizedProposalEvidence CreateFromOpenRouter(OpenRouterPremiumEvidenceArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        OpenRouterPremiumEvidenceArtifact validated;
        try { validated = OpenRouterPremiumEvidenceArtifactModule.Validate(artifact.CanonicalUtf8); }
        catch (OpenRouterPremiumEvidenceException) { throw Failure("evidence_binding_invalid"); }
        if (!string.Equals(validated.Status, "complete", StringComparison.Ordinal)
            || validated.Slots.Count != CognitionQualityCorpusV1.ScenarioCount
            || validated.Slots.Any(static slot => slot.Proposal is null))
            throw Failure("evidence_binding_invalid");
        CognitionQualitySubmission[] proposals = validated.Slots
            .Select(slot => new CognitionQualitySubmission(slot.ScenarioId, slot.Proposal! with { }))
            .ToArray();
        return Create(OpenRouterPremiumEvidenceArtifactModule.SchemaVersion, validated.CanonicalDigestSha256, proposals);
    }

    public static CognitionQualityNormalizedProposalEvidence Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumEvidenceBytes) throw Failure("evidence_size_invalid");
        try { _ = StrictUtf8.GetString(canonicalUtf8.Span); }
        catch (DecoderFallbackException) { throw Failure("evidence_utf8_invalid"); }

        JsonDocument document;
        try
        {
            RejectDuplicateProperties(canonicalUtf8.Span);
            Utf8JsonReader reader = new(canonicalUtf8.Span, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth
            });
            document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("evidence_json_invalid"); }
        }
        catch (CognitionQualityNormalizedProposalEvidenceException) { throw; }
        catch (JsonException) { throw Failure("evidence_json_invalid"); }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireObjectAndOrder(root, RootNames, "evidence_shape_invalid");
            RequireCanonicalScalars(root);
            RequireString(root, "schema_version", SchemaVersion);
            RequireString(root, "status", "complete");
            string sourceSchema = RequireClosedString(root, "source_evidence_schema_version", SourceSchemas);
            string sourceDigest = RequireDigest(root, "source_evidence_digest_sha256");
            CognitionQualityCorpusSnapshot corpus = CognitionQualityCorpusV1.CreateSnapshot();
            RequireString(root, "corpus_schema_version", CognitionQualityCorpusV1.CorpusSchemaVersion);
            RequireString(root, "corpus_digest_sha256", corpus.CanonicalDigestSha256);
            RequireString(root, "scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256);
            RequireString(root, "validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
            CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create(OpenRouterPremiumProfile.PromptRevision);
            RequireString(root, "prompt_publication_digest_sha256", publication.CanonicalDigestSha256);
            RequireString(root, "prompt_set_digest_sha256", publication.PromptSetDigestSha256);
            RequireString(root, "proposal_schema_version", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion);

            JsonElement proposalsValue = root.GetProperty("proposals");
            if (proposalsValue.ValueKind != JsonValueKind.Array || proposalsValue.GetArrayLength() != CognitionQualityCorpusV1.ScenarioCount)
                throw Failure("proposal_count_invalid");
            List<CognitionQualitySubmission> proposals = new(CognitionQualityCorpusV1.ScenarioCount);
            int index = 0;
            foreach (JsonElement value in proposalsValue.EnumerateArray())
            {
                RequireObjectAndOrder(value, ProposalNames, "proposal_shape_invalid");
                string scenarioId = RequireStringValue(value, "scenario_id");
                if (!string.Equals(scenarioId, corpus.Scenarios[index++].ScenarioId, StringComparison.Ordinal)) throw Failure("proposal_order_invalid");
                JsonElement proposal = value.GetProperty("proposal");
                if (proposal.ValueKind == JsonValueKind.Null)
                {
                    proposals.Add(new CognitionQualitySubmission(scenarioId, null));
                }
                else
                {
                    RequireObjectAndOrder(proposal, ActionNames, "proposal_shape_invalid");
                    string agentId = RequireStringValue(proposal, "agent_id");
                    if (!IsCanonicalAgentId(agentId)) throw Failure("proposal_shape_invalid");
                    int action = RequireInt32(proposal, "action");
                    int quantity = RequireInt32(proposal, "quantity");
                    proposals.Add(new CognitionQualitySubmission(scenarioId, new SnowGlobeActionProposal(agentId, (SnowGlobeActionKind)action, quantity)));
                }
            }
            RequireExactStrings(root.GetProperty("claim_limitation_codes"), Claims, "evidence_value_invalid");
            string payloadDigest = RequireDigest(root, "evidence_payload_digest_sha256");
            byte[] payload = CanonicalizeWithoutLast(root, "evidence_payload_digest_sha256");
            try
            {
                if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal))
                    throw Failure("evidence_payload_digest_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(payload); }

            CognitionQualityNormalizedProposalEvidence recreated = Create(sourceSchema, sourceDigest, proposals);
            if (!recreated.CanonicalUtf8.Span.SequenceEqual(canonicalUtf8.Span)) throw Failure("evidence_noncanonical");
            return recreated;
        }
    }

    private static CognitionQualityNormalizedProposal[] Snapshot(CognitionQualityCorpusSnapshot corpus, IReadOnlyList<CognitionQualitySubmission> proposals)
    {
        if (proposals.Count != CognitionQualityCorpusV1.ScenarioCount) throw Failure("proposal_count_invalid");
        CognitionQualityNormalizedProposal[] snapshot = new CognitionQualityNormalizedProposal[proposals.Count];
        for (int index = 0; index < proposals.Count; index++)
        {
            CognitionQualitySubmission item = proposals[index] ?? throw Failure("proposal_shape_invalid");
            if (!string.Equals(item.ScenarioId, corpus.Scenarios[index].ScenarioId, StringComparison.Ordinal)) throw Failure("proposal_order_invalid");
            if (item.Proposal is not null && !IsCanonicalAgentId(item.Proposal.AgentId)) throw Failure("proposal_shape_invalid");
            if (item.Proposal is not null && !Enum.IsDefined(item.Proposal.Action)) throw Failure("proposal_contract_invalid");
            snapshot[index] = new CognitionQualityNormalizedProposal(
                item.ScenarioId,
                item.Proposal is null ? null : item.Proposal with { });
        }
        return snapshot;
    }

    private static byte[] Write(
        string sourceSchema,
        string sourceDigest,
        CognitionQualityCorpusSnapshot corpus,
        IReadOnlyList<CognitionQualityNormalizedProposal> proposals,
        string? payloadDigest)
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create(OpenRouterPremiumProfile.PromptRevision);
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "complete");
        writer.WriteString("source_evidence_schema_version", sourceSchema);
        writer.WriteString("source_evidence_digest_sha256", sourceDigest);
        writer.WriteString("corpus_schema_version", CognitionQualityCorpusV1.CorpusSchemaVersion);
        writer.WriteString("corpus_digest_sha256", corpus.CanonicalDigestSha256);
        writer.WriteString("scoring_digest_sha256", CognitionQualityCorpusV1.ScoringDigestSha256);
        writer.WriteString("validator_identity", CognitionQualityCorpusV1.ValidatorIdentity);
        writer.WriteString("prompt_publication_digest_sha256", publication.CanonicalDigestSha256);
        writer.WriteString("prompt_set_digest_sha256", publication.PromptSetDigestSha256);
        writer.WriteString("proposal_schema_version", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion);
        writer.WritePropertyName("proposals"); writer.WriteStartArray();
        foreach (CognitionQualityNormalizedProposal item in proposals)
        {
            writer.WriteStartObject(); writer.WriteString("scenario_id", item.ScenarioId); writer.WritePropertyName("proposal");
            if (item.Proposal is null) writer.WriteNullValue();
            else
            {
                writer.WriteStartObject(); writer.WriteString("agent_id", item.Proposal.AgentId); writer.WriteNumber("action", (int)item.Proposal.Action); writer.WriteNumber("quantity", item.Proposal.Quantity); writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray(); foreach (string claim in Claims) writer.WriteStringValue(claim); writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("evidence_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static bool IsCanonicalAgentId(string? value)
    {
        if (value is null || value.Length is < 1 or > 64) return false;
        return value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names, string code)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure(code);
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure(code);
        for (int index = 0; index < names.Count; index++)
            if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal)) throw Failure(code);
    }

    private static void RequireString(JsonElement owner, string name, string expected)
    {
        if (!string.Equals(RequireStringValue(owner, name), expected, StringComparison.Ordinal)) throw Failure("evidence_value_invalid");
    }

    private static string RequireStringValue(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        return value.ValueKind == JsonValueKind.String && value.GetString() is { } text ? text : throw Failure("evidence_value_invalid");
    }

    private static string RequireClosedString(JsonElement owner, string name, IReadOnlyList<string> allowed)
    {
        string value = RequireStringValue(owner, name);
        if (!allowed.Contains(value, StringComparer.Ordinal)) throw Failure("evidence_value_invalid");
        return value;
    }

    private static string RequireDigest(JsonElement owner, string name)
    {
        string value = RequireStringValue(owner, name);
        if (!IsDigest(value)) throw Failure("evidence_digest_invalid");
        return value;
    }

    private static int RequireInt32(JsonElement owner, string name)
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result)) throw Failure("evidence_value_invalid");
        return result;
    }

    private static void RequireExactStrings(JsonElement value, IReadOnlyList<string> expected, string code)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count) throw Failure(code);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index++], StringComparison.Ordinal)) throw Failure(code);
    }

    private static void RequireCanonicalScalars(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { foreach (JsonProperty property in value.EnumerateObject()) RequireCanonicalScalars(property.Value); return; }
        if (value.ValueKind == JsonValueKind.Array) { foreach (JsonElement item in value.EnumerateArray()) RequireCanonicalScalars(item); return; }
        if (value.ValueKind == JsonValueKind.String && !string.Equals(JsonSerializer.Serialize(value.GetString()), value.GetRawText(), StringComparison.Ordinal)) throw Failure("evidence_noncanonical");
        if (value.ValueKind == JsonValueKind.Number && (!value.TryGetInt64(out long integer) || !string.Equals(integer.ToString(System.Globalization.CultureInfo.InvariantCulture), value.GetRawText(), StringComparison.Ordinal))) throw Failure("evidence_noncanonical");
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement value, string lastName)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject(); foreach (JsonProperty property in value.EnumerateObject()) { if (property.NameEquals(lastName)) continue; writer.WritePropertyName(property.Name); property.Value.WriteTo(writer); } writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = MaximumJsonDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && (stack.Count == 0 || !stack.Peek().Add(reader.GetString()!))) throw Failure("evidence_shape_invalid");
        }
    }

    private static CognitionQualityNormalizedProposalEvidenceException Failure(string code) => new(code);
}
