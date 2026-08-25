using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Societies.SnowGlobe;

internal sealed record OllamaTagsExpectedModel(
    string RuntimeModelReference,
    string ArtifactDigestSha256,
    long ArtifactSizeBytes,
    string ArtifactFormat,
    string ModelFamily,
    string? ParameterSize,
    string QuantizationLevel,
    int MinimumContextLength);

internal sealed record OllamaTagsValidatedModel(
    string RuntimeModelReference,
    string ArtifactDigestSha256,
    long ArtifactSizeBytes,
    string ArtifactFormat,
    string ModelFamily,
    string ParameterSize,
    string QuantizationLevel);

/// <summary>Pure strict validator for the non-generating Ollama GET /api/tags response.</summary>
internal static class OllamaTagsMetadataCodec
{
    internal const string SchemaVersion = "ollama_api_tags_metadata/v1";
    internal const int MaximumJsonDepth = 5;
    internal const int MaximumJsonTokens = 16_384;
    internal const int MaximumStringBytes = 8_192;
    internal const string ContractDescriptor =
        "ollama_api_tags_metadata/v1|strict_utf8_json|unknown_fields=reject|duplicate_properties=reject" +
        "|required_root=models|required_model=name,model,modified_at,size,digest,details,capabilities" +
        "|required_details=parent_model,format,family,families,parameter_size,quantization_level,context_length,embedding_length" +
        "|model_alias=name_equals_model|selected=exactly_one|digest_alias=reject" +
        "|capabilities=unique_bounded_completion_required|families=nonempty_unique_expected_required" +
        "|json_depth=5|json_tokens=16384|string_bytes=8192";
    internal static string ContractDigestSha256 { get; } =
        CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(ContractDescriptor));
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumJsonDepth,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static OllamaTagsValidatedModel Validate(
        ReadOnlySpan<byte> canonicalResponseUtf8,
        OllamaTagsExpectedModel expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        InspectStructure(canonicalResponseUtf8);
        TagsResponse? tags;
        try { tags = JsonSerializer.Deserialize<TagsResponse>(canonicalResponseUtf8, Options); }
        catch (JsonException exception) { throw new LocalModelBenchmarkException("tags_json_invalid", exception); }
        if (tags?.Models is null || tags.Models.Count == 0)
            throw new LocalModelBenchmarkException("tags_models_missing");

        List<TagModel> exact = [];
        foreach (TagModel? model in tags.Models)
        {
            if (model is not null
                && (model.Name == expected.RuntimeModelReference || model.Model == expected.RuntimeModelReference)
                && (model.Name != expected.RuntimeModelReference || model.Model != expected.RuntimeModelReference))
                throw new LocalModelBenchmarkException("runtime_model_alias_rejected");
            ValidateShape(model, expected.MinimumContextLength,
                model?.Name == expected.RuntimeModelReference);
            if (model!.Digest == expected.ArtifactDigestSha256
                && model.Name != expected.RuntimeModelReference)
                throw new LocalModelBenchmarkException("runtime_digest_alias_rejected");
            if (model.Name == expected.RuntimeModelReference) exact.Add(model);
        }
        if (exact.Count != 1)
            throw new LocalModelBenchmarkException(exact.Count == 0
                ? "runtime_model_missing" : "runtime_model_duplicate");

        TagModel selected = exact[0];
        TagDetails details = selected.Details!;
        if (selected.Digest != expected.ArtifactDigestSha256
            || selected.Size != expected.ArtifactSizeBytes
            || details.Format != expected.ArtifactFormat
            || details.Family != expected.ModelFamily
            || expected.ParameterSize is not null && details.ParameterSize != expected.ParameterSize
            || details.QuantizationLevel != expected.QuantizationLevel
            || details.Families is null || details.Families.Count == 0
            || details.Families.Distinct(StringComparer.Ordinal).Count() != details.Families.Count
            || !details.Families.Contains(expected.ModelFamily, StringComparer.Ordinal))
            throw new LocalModelBenchmarkException("runtime_provenance_mismatch");

        return new(expected.RuntimeModelReference, expected.ArtifactDigestSha256,
            expected.ArtifactSizeBytes, expected.ArtifactFormat, expected.ModelFamily,
            details.ParameterSize!, expected.QuantizationLevel);
    }

    private static void ValidateShape(TagModel? model, int minimumContextLength, bool selected)
    {
        if (model is null
            || !OllamaBenchmarkContract.IsRuntimeReference(model.Name)
            || OllamaBenchmarkContract.IsCloudRuntimeReference(model.Name)
            || model.Name != model.Model
            || string.IsNullOrEmpty(model.ModifiedAt) || model.ModifiedAt.Length > 64
            || !DateTimeOffset.TryParse(model.ModifiedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _)
            || model.Size <= 0
            || !OllamaBenchmarkContract.IsDigest(model.Digest)
            || model.Capabilities is null || model.Capabilities.Count is 0 or > 16
            || model.Capabilities.Distinct(StringComparer.Ordinal).Count() != model.Capabilities.Count
            || model.Capabilities.Any(capability =>
                !OllamaBenchmarkContract.IsSubstantiveMetadata(capability, allowUppercase: false))
            || selected && !model.Capabilities.Contains("completion", StringComparer.Ordinal)
            || model.Details is null
            || model.Details.ParentModel != string.Empty
            || model.Details.Format != "gguf"
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.Family, allowUppercase: false)
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.ParameterSize, allowUppercase: true)
            || !OllamaBenchmarkContract.IsSubstantiveMetadata(model.Details.QuantizationLevel, allowUppercase: true)
            || model.Details.ContextLength <= 0
            || selected && model.Details.ContextLength < minimumContextLength
            || model.Details.EmbeddingLength <= 0
            || model.Details.Families is null || model.Details.Families.Count == 0
            || model.Details.Families.Any(family =>
                !OllamaBenchmarkContract.IsSubstantiveMetadata(family, allowUppercase: false)))
            throw new LocalModelBenchmarkException("tags_model_invalid");
    }

    private static void InspectStructure(ReadOnlySpan<byte> bytes)
    {
        Stack<HashSet<string>> objects = new();
        try
        {
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions
            {
                MaxDepth = MaximumJsonDepth + 1,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            int tokens = 0;
            while (reader.Read())
            {
                if (++tokens > MaximumJsonTokens)
                    throw new LocalModelBenchmarkException("tags_json_invalid");
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray
                    && reader.CurrentDepth >= MaximumJsonDepth)
                    throw new LocalModelBenchmarkException("tags_json_too_deep");
                if (reader.TokenType == JsonTokenType.StartObject)
                    objects.Push(new HashSet<string>(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.PropertyName
                    && (objects.Count == 0 || !objects.Peek().Add(reader.GetString()!)))
                    throw new LocalModelBenchmarkException("tags_json_duplicate_property");
                else if (reader.TokenType == JsonTokenType.EndObject)
                    objects.Pop();
                if (reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String)
                {
                    long length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                    if (length > MaximumStringBytes)
                        throw new LocalModelBenchmarkException("tags_json_invalid");
                }
            }
            if (objects.Count != 0) throw new LocalModelBenchmarkException("tags_json_invalid");
        }
        catch (LocalModelBenchmarkException) { throw; }
        catch (JsonException exception) { throw new LocalModelBenchmarkException("tags_json_invalid", exception); }
    }

    private sealed record TagsResponse
    {
        [JsonRequired] public IReadOnlyList<TagModel?>? Models { get; init; }
    }

    private sealed record TagModel
    {
        [JsonRequired] public string? Name { get; init; }
        [JsonRequired] public string? Model { get; init; }
        [JsonRequired] public string? ModifiedAt { get; init; }
        [JsonRequired] public long Size { get; init; }
        [JsonRequired] public string? Digest { get; init; }
        [JsonRequired] public TagDetails? Details { get; init; }
        [JsonRequired] public IReadOnlyList<string>? Capabilities { get; init; }
    }

    private sealed record TagDetails
    {
        [JsonRequired] public string? ParentModel { get; init; }
        [JsonRequired] public string? Format { get; init; }
        [JsonRequired] public string? Family { get; init; }
        [JsonRequired] public IReadOnlyList<string>? Families { get; init; }
        [JsonRequired] public string? ParameterSize { get; init; }
        [JsonRequired] public string? QuantizationLevel { get; init; }
        [JsonRequired] public long ContextLength { get; init; }
        [JsonRequired] public long EmbeddingLength { get; init; }
    }
}
