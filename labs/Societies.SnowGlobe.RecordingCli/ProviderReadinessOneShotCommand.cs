using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

internal sealed class ProviderReadinessOneShotException : Exception
{
    internal ProviderReadinessOneShotException(string code) : base(Close(code)) => Code = Close(code);
    internal ProviderReadinessOneShotException(string code, Exception? _) : this(code) { }

    internal string Code { get; }

    private static string Close(string code) => code switch
    {
        "runtime_identity_invalid" or "invocation_already_consumed" or "invocation_claim_ambiguous" or
        "comparison_evidence_rejected" or "observation_validation_failed" or "observation_stale" or
        "assessment_validation_failed" or "artifact_path_rejected" or "artifact_path_reparse_rejected" or
        "artifact_hardlink_rejected" or "artifact_target_exists" or "artifact_store_platform_unsupported" or
        "artifact_publication_partial" or "artifact_publication_ambiguous" or "artifact_read_failed" or
        "artifact_claim_invalid" or "source_revision_unavailable" or "source_revision_mismatch" or
        "invocation_failed" => code,
        _ => "invocation_failed"
    };
}

internal interface IProviderReadinessOneShotAdapterFactory
{
    IProviderReadinessObservationAdapter CreateOpenRouter();
    IProviderReadinessObservationAdapter CreateOllama(PinnedRuntimeObservation runtime);
}

internal interface IProviderReadinessOneShotArtifactStore
{
    void ClaimOnce(ReadOnlyMemory<byte> canonicalClaim);
    byte[] ReadAcceptedComparisonArtifact();
    void PublishAtomically(ProviderReadinessOneShotArtifacts artifacts);
}

internal sealed class ProviderReadinessOneShotArtifacts
{
    private readonly byte[] _openRouter;
    private readonly byte[] _ollama;
    private readonly byte[] _assessment;

    internal ProviderReadinessOneShotArtifacts(
        ReadOnlyMemory<byte> openRouterObservationCanonicalUtf8,
        ReadOnlyMemory<byte> ollamaObservationCanonicalUtf8,
        ReadOnlyMemory<byte> assessmentCanonicalUtf8)
    {
        if (openRouterObservationCanonicalUtf8.Length is < 1 or > ProviderReadinessObservationModule.MaximumObservationBytes
            || ollamaObservationCanonicalUtf8.Length is < 1 or > ProviderReadinessObservationModule.MaximumObservationBytes
            || assessmentCanonicalUtf8.Length is < 1 or > ProviderRoutingReadinessEvidenceModule.MaximumCurrentAssessmentBytes)
            throw new ProviderReadinessOneShotException("artifact_publication_ambiguous");
        _openRouter = openRouterObservationCanonicalUtf8.Span.ToArray();
        _ollama = ollamaObservationCanonicalUtf8.Span.ToArray();
        _assessment = assessmentCanonicalUtf8.Span.ToArray();
    }

    internal ReadOnlyMemory<byte> OpenRouterObservationCanonicalUtf8 => _openRouter.ToArray();
    internal ReadOnlyMemory<byte> OllamaObservationCanonicalUtf8 => _ollama.ToArray();
    internal ReadOnlyMemory<byte> AssessmentCanonicalUtf8 => _assessment.ToArray();

    internal ProviderReadinessOneShotArtifacts Copy() => new(_openRouter, _ollama, _assessment);
}

internal sealed record ProviderReadinessOneShotResult(
    string Status,
    int OpenRouterRequestCount,
    int OllamaRequestCount,
    string OpenRouterArtifactDigestSha256,
    string OllamaArtifactDigestSha256,
    string AssessmentArtifactDigestSha256,
    string AssessmentStatus,
    string PrimaryAttemptCurrentState,
    string RoutingInputIssuanceStatus,
    bool RoutingPolicyInputPresent,
    bool AdditionalAttemptAuthorized);

/// <summary>
/// One governed, sequential observation cycle. This command creates evidence only and has no
/// routing, provider-generation, payment, retry, fallback, or world authority.
/// </summary>
internal sealed class ProviderReadinessOneShotCommand
{
    private readonly IProviderReadinessOneShotArtifactStore _store;
    private readonly IProviderReadinessOneShotAdapterFactory _adapters;
    private readonly IProviderReadinessClock _clock;

    internal ProviderReadinessOneShotCommand(
        IProviderReadinessOneShotArtifactStore store,
        IProviderReadinessOneShotAdapterFactory adapters,
        IProviderReadinessClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    internal async ValueTask<ProviderReadinessOneShotResult> ExecuteOnceAsync(
        PinnedRuntimeObservation runtime,
        CancellationToken cancellationToken)
    {
        ValidateRuntime(runtime);
        Claim();

        byte[] comparison = ReadAndValidateComparison();
        try
        {
            ProviderReadinessObservation openRouter = await ObserveOpenRouterAsync(cancellationToken)
                .ConfigureAwait(false);
            ProviderReadinessObservation ollama = await ObserveOllamaAsync(runtime, cancellationToken)
                .ConfigureAwait(false);

            long assessedAt = _clock.NowMilliseconds;
            ValidateCurrentObservation(openRouter, assessedAt);
            ValidateCurrentObservation(ollama, assessedAt);

            ProviderRoutingCurrentReadinessAssessment assessment;
            try
            {
                ProviderRoutingReadinessEvidenceInput historical = new(comparison);
                assessment = ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                    historical,
                    openRouter.CanonicalUtf8,
                    ollama.CanonicalUtf8,
                    assessedAt);
                assessment = ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                    assessment.CanonicalUtf8,
                    assessedAt);
            }
            catch (ProviderRoutingReadinessEvidenceException exception)
            {
                throw new ProviderReadinessOneShotException("assessment_validation_failed", exception);
            }

            if (assessment.PrimaryAttemptCurrentState != "unknown"
                || assessment.RoutingInputIssuanceStatus != "not_issued"
                || assessment.RoutingPolicyInput is not null)
                throw new ProviderReadinessOneShotException("assessment_validation_failed");

            ProviderReadinessOneShotArtifacts artifacts = new(
                openRouter.CanonicalUtf8,
                ollama.CanonicalUtf8,
                assessment.CanonicalUtf8);
            Publish(artifacts);

            return new ProviderReadinessOneShotResult(
                "published",
                openRouter.RequestCount,
                ollama.RequestCount,
                openRouter.CanonicalDigestSha256,
                ollama.CanonicalDigestSha256,
                assessment.CanonicalDigestSha256,
                assessment.Status,
                assessment.PrimaryAttemptCurrentState,
                assessment.RoutingInputIssuanceStatus,
                assessment.RoutingPolicyInput is not null,
                false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(comparison);
        }
    }

    private void Claim()
    {
        try
        {
            _ = ProviderReadinessOneShotClaimCodec.ValidateForCurrentBuild(
                ProviderReadinessOneShotClaimCodec.CanonicalUtf8);
            _store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8);
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("invocation_claim_ambiguous", exception);
        }
    }

    private byte[] ReadAndValidateComparison()
    {
        byte[] comparison;
        try
        {
            comparison = _store.ReadAcceptedComparisonArtifact();
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("comparison_evidence_rejected", exception);
        }

        try
        {
            ProviderRoutingReadinessAssessment historical = ProviderRoutingReadinessEvidenceModule.Assess(
                new ProviderRoutingReadinessEvidenceInput(comparison));
            if (historical.ComparisonEvidence.Status != "accepted"
                || historical.SelectionEvidence != "accepted_openrouter_default"
                || historical.ComparisonEvidence.ArtifactDigestSha256
                    != ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256)
                throw new ProviderReadinessOneShotException("comparison_evidence_rejected");
            return comparison;
        }
        catch (ProviderReadinessOneShotException)
        {
            CryptographicOperations.ZeroMemory(comparison);
            throw;
        }
        catch (Exception exception)
        {
            CryptographicOperations.ZeroMemory(comparison);
            throw new ProviderReadinessOneShotException("comparison_evidence_rejected", exception);
        }
    }

    private async ValueTask<ProviderReadinessObservation> ObserveOpenRouterAsync(
        CancellationToken cancellationToken)
    {
        IProviderReadinessObservationAdapter adapter;
        try { adapter = _adapters.CreateOpenRouter(); }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("observation_validation_failed", exception);
        }
        return await ObserveAndDisposeAsync(adapter, ProviderReadinessProvider.OpenRouter, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<ProviderReadinessObservation> ObserveOllamaAsync(
        PinnedRuntimeObservation runtime,
        CancellationToken cancellationToken)
    {
        IProviderReadinessObservationAdapter adapter;
        try { adapter = _adapters.CreateOllama(runtime); }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("observation_validation_failed", exception);
        }
        return await ObserveAndDisposeAsync(adapter, ProviderReadinessProvider.Ollama, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<ProviderReadinessObservation> ObserveAndDisposeAsync(
        IProviderReadinessObservationAdapter adapter,
        ProviderReadinessProvider expectedProvider,
        CancellationToken cancellationToken)
    {
        if (adapter is null || adapter.Provider != expectedProvider)
        {
            (adapter as IDisposable)?.Dispose();
            throw new ProviderReadinessOneShotException("observation_validation_failed");
        }

        try
        {
            ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
                adapter, _clock, cancellationToken).ConfigureAwait(false);
            if (observation.Provider != ProviderName(expectedProvider))
                throw new ProviderReadinessOneShotException("observation_validation_failed");
            return observation;
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("observation_validation_failed", exception);
        }
        finally
        {
            try { (adapter as IDisposable)?.Dispose(); }
            catch { }
        }
    }

    private static void ValidateCurrentObservation(
        ProviderReadinessObservation observation,
        long assessedAt)
    {
        try
        {
            ProviderReadinessObservation validated = ProviderReadinessObservationModule.Validate(
                observation.CanonicalUtf8,
                assessedAt);
            if (validated.CanonicalDigestSha256 != observation.CanonicalDigestSha256)
                throw new ProviderReadinessOneShotException("observation_validation_failed");
        }
        catch (ProviderReadinessObservationException exception) when (exception.Code is "observation_expired" or "observation_time_invalid")
        {
            throw new ProviderReadinessOneShotException("observation_stale", exception);
        }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("observation_validation_failed", exception);
        }
    }

    private void Publish(ProviderReadinessOneShotArtifacts artifacts)
    {
        try { _store.PublishAtomically(artifacts); }
        catch (ProviderReadinessOneShotException) { throw; }
        catch (Exception exception)
        {
            throw new ProviderReadinessOneShotException("artifact_publication_ambiguous", exception);
        }
    }

    private static void ValidateRuntime(PinnedRuntimeObservation? runtime)
    {
        if (runtime is null
            || runtime.ProcessId <= 0
            || runtime.ProcessStartUtcTicks <= 0
            || runtime.ProcessStartUtcTicks > DateTime.MaxValue.Ticks)
            throw new ProviderReadinessOneShotException("runtime_identity_invalid");
    }

    private static string ProviderName(ProviderReadinessProvider provider) => provider switch
    {
        ProviderReadinessProvider.OpenRouter => "openrouter",
        ProviderReadinessProvider.Ollama => "ollama",
        _ => throw new ProviderReadinessOneShotException("observation_validation_failed")
    };
}

internal static class ProviderReadinessOneShotClaimCodec
{
    internal const string SchemaVersion = "snow_globe_provider_readiness_one_shot_claim/v1";
    internal const string ContractSchemaVersion = "snow_globe_provider_readiness_one_shot_contract/v1";
    internal const int MaximumBytes = 4 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootNames =
    [
        "schema_version", "status", "contract_schema_version", "contract_digest_sha256", "source_commit",
        "publication_directory", "openrouter_observation_path", "ollama_observation_path",
        "assessment_path", "additional_attempt_authorized", "raw_provider_data_retained",
        "claim_payload_digest_sha256"
    ];

    internal const string PublicationDirectory =
        "artifacts/snowglobe/provider-readiness/evidence-v1";
    internal const string OpenRouterObservationPath =
        PublicationDirectory + "/openrouter-observation-v1.json";
    internal const string OllamaObservationPath =
        PublicationDirectory + "/ollama-observation-v1.json";
    internal const string AssessmentPath =
        PublicationDirectory + "/routing-readiness-assessment-v2.json";
    internal const string ClaimPath =
        "artifacts/snowglobe/provider-readiness/one-shot-consumed-v1.json";

    internal static string ContractDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        ContractSchemaVersion +
        "|command=provider-readiness-record-once-v1|one_shot_create_new_terminal_claim_before_adapters" +
        "|providers=openrouter_then_ollama_sequential_exact_existing_adapters" +
        "|openrouter_observation_schema=" + ProviderReadinessObservationModule.SchemaVersion +
        "|observation_contract_digest=" + ProviderReadinessObservationModule.ContractDigestSha256 +
        "|assessment_schema=" + ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion +
        "|assessment_contract_digest=" + ProviderRoutingReadinessEvidenceModule.CurrentContractDigestSha256 +
        "|accepted_comparison_digest=" + ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256 +
        "|source_commit=exact_lower_git_sha1_from_command_assembly_informational_version" +
        "|historical_claim_validation=canonical_payload_accepts_any_exact_lowercase_40_hex_source_commit" +
        "|new_invocation_claim=retained_source_commit_must_equal_current_command_assembly_source_commit" +
        "|publication=three_create_new_files_in_fixed_pending_directory_then_same_volume_atomic_directory_rename" +
        "|claim_path=" + ClaimPath +
        "|publication_directory=" + PublicationDirectory +
        "|openrouter_path=" + OpenRouterObservationPath +
        "|ollama_path=" + OllamaObservationPath +
        "|assessment_path=" + AssessmentPath +
        "|raw_provider_data=forbidden|retry_fallback_parallel_generation_payment_routing_world_authority=none"));

    private static readonly byte[] Canonical = Build(SourceCommit);
    internal static ReadOnlyMemory<byte> CanonicalUtf8 => Canonical.ToArray();
    internal static string SourceCommit => ReadSourceCommit();

    internal static string Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumBytes)
            throw new ProviderReadinessOneShotException("artifact_claim_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            try { _ = StrictUtf8.GetString(snapshot); }
            catch (DecoderFallbackException exception)
            {
                throw new ProviderReadinessOneShotException("artifact_claim_invalid", exception);
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(snapshot, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3
                });
            }
            catch (JsonException exception)
            {
                throw new ProviderReadinessOneShotException("artifact_claim_invalid", exception);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.EnumerateObject().Select(property => property.Name).SequenceEqual(RootNames, StringComparer.Ordinal))
                    throw new ProviderReadinessOneShotException("artifact_claim_invalid");
                RequireString(root, "schema_version", SchemaVersion);
                RequireString(root, "status", "invocation_consumed");
                RequireString(root, "contract_schema_version", ContractSchemaVersion);
                RequireString(root, "contract_digest_sha256", ContractDigestSha256);
                string sourceCommit = RequireSourceCommit(root);
                RequireString(root, "publication_directory", PublicationDirectory);
                RequireString(root, "openrouter_observation_path", OpenRouterObservationPath);
                RequireString(root, "ollama_observation_path", OllamaObservationPath);
                RequireString(root, "assessment_path", AssessmentPath);
                if (root.GetProperty("additional_attempt_authorized").ValueKind != JsonValueKind.False
                    || root.GetProperty("raw_provider_data_retained").ValueKind != JsonValueKind.False)
                    throw new ProviderReadinessOneShotException("artifact_claim_invalid");
                _ = RequireDigest(root, "claim_payload_digest_sha256");
                byte[] expected = Build(sourceCommit);
                try
                {
                    if (!snapshot.AsSpan().SequenceEqual(expected))
                        throw new ProviderReadinessOneShotException("artifact_claim_invalid");
                    return sourceCommit;
                }
                finally { CryptographicOperations.ZeroMemory(expected); }
            }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    internal static string ValidateForCurrentBuild(ReadOnlyMemory<byte> canonicalUtf8)
    {
        string retainedSourceCommit = Validate(canonicalUtf8);
        if (!string.Equals(retainedSourceCommit, SourceCommit, StringComparison.Ordinal))
            throw new ProviderReadinessOneShotException("source_revision_mismatch");
        return retainedSourceCommit;
    }

    private static byte[] Build(string sourceCommit)
    {
        ValidateSourceCommit(sourceCommit);
        byte[] payload;
        using (MemoryStream stream = new())
        {
            using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false });
            writer.WriteStartObject();
            WriteFields(writer, sourceCommit);
            writer.WriteEndObject();
            writer.Flush();
            payload = stream.ToArray();
        }

        string digest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        using MemoryStream final = new();
        using (Utf8JsonWriter writer = new(final, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            WriteFields(writer, sourceCommit);
            writer.WriteString("claim_payload_digest_sha256", digest);
            writer.WriteEndObject();
            writer.Flush();
        }
        return final.ToArray();
    }

    private static void WriteFields(Utf8JsonWriter writer, string sourceCommit)
    {
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("status", "invocation_consumed");
        writer.WriteString("contract_schema_version", ContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", ContractDigestSha256);
        writer.WriteString("source_commit", sourceCommit);
        writer.WriteString("publication_directory", PublicationDirectory);
        writer.WriteString("openrouter_observation_path", OpenRouterObservationPath);
        writer.WriteString("ollama_observation_path", OllamaObservationPath);
        writer.WriteString("assessment_path", AssessmentPath);
        writer.WriteBoolean("additional_attempt_authorized", false);
        writer.WriteBoolean("raw_provider_data_retained", false);
    }

    private static void RequireString(JsonElement root, string name, string expected)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || value.GetString() != expected)
            throw new ProviderReadinessOneShotException("artifact_claim_invalid");
    }

    private static string RequireDigest(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        string? digest = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (digest is not { Length: 64 }
            || digest.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ProviderReadinessOneShotException("artifact_claim_invalid");
        return digest;
    }

    private static string RequireSourceCommit(JsonElement root)
    {
        JsonElement value = root.GetProperty("source_commit");
        string? sourceCommit = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        ValidateSourceCommit(sourceCommit);
        return sourceCommit!;
    }

    private static void ValidateSourceCommit(string? sourceCommit)
    {
        if (sourceCommit is not { Length: 40 }
            || sourceCommit.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ProviderReadinessOneShotException("artifact_claim_invalid");
    }

    private static string ReadSourceCommit()
    {
        string? informationalVersion = typeof(ProviderReadinessOneShotClaimCodec).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        int delimiter = informationalVersion?.LastIndexOf('+') ?? -1;
        string? commit = delimiter >= 0 ? informationalVersion![(delimiter + 1)..] : null;
        if (commit is not { Length: 40 }
            || commit.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ProviderReadinessOneShotException("source_revision_unavailable");
        return commit;
    }
}
