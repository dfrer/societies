using System.Security.Cryptography;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Societies.SnowGlobe.RecordingCli.Tests")]
[assembly: InternalsVisibleTo("Societies.SnowGlobe.RecordingCli")]

namespace Societies.SnowGlobe;

/// <summary>Caller-observed process identity only; Prepare performs no process or file inspection.</summary>
public sealed record PinnedRuntimeObservation(int ProcessId, long ProcessStartUtcTicks);

public sealed class OllamaRecordingCompositionException : Exception
{
    internal OllamaRecordingCompositionException(string code) : base(CloseCode(code)) => Code = CloseCode(code);
    public string Code { get; }
    private static string CloseCode(string code) => code switch
    {
        "repository_root_lexically_invalid" or "runtime_observation_invalid" or "authorization_nonce_invalid" or
        "plan_invalid" or "prompt_publication_invalid" or "artifact_path_outside_bound" or "artifact_read_bound_invalid" or
        "repository_root_not_verified" or "artifact_path_inspection_failed" or "artifact_directory_missing" or
        "artifact_directory_invalid" or "artifact_path_reparse_point_rejected" or "artifact_directory_lease_failed" or
        "artifact_directory_lease_count_invalid" or "artifact_directory_identity_unavailable" or "artifact_directory_identity_mismatch" or
        "artifact_already_exists" or "artifact_reservation_failed" or "artifact_not_found" or "artifact_size_invalid" or
        "artifact_size_changed" or "artifact_read_failed" or "artifact_reservation_reused" or "artifact_reservation_disposed" or
        "artifact_durable_readback_mismatch" or "artifact_publication_indeterminate" or "artifact_store_platform_unsupported" or
        "artifact_reservation_dispose_failed" or "artifact_repository_root_binding_invalid" or "artifact_validation_failed" or
        "composition_execution_indeterminate" or "authorization_rejected" => code,
        _ => "composition_execution_indeterminate"
    };
}

/// <summary>Object-bound, immutable, raw-free public plan. Its private nonce is consumed only by Execute.</summary>
public sealed class OllamaRecordingCompositionPlan
{
    private readonly SnowGlobeOllamaRecordingCompositionModule _owner;
    private readonly string _authorizationNonce;
    private int _consumed;

    internal OllamaRecordingCompositionPlan(
        SnowGlobeOllamaRecordingCompositionModule owner,
        CognitionQualityPromptEnvelopePublication publication,
        CognitionQualityExecutionProvenance provenance,
        OllamaLoopbackRuntimeBinding runtimeBinding,
        string authorizationNonce,
        string authorizationNonceDigestSha256,
        string repositoryRootDigestSha256,
        string planDigestSha256)
    {
        _owner = owner;
        _authorizationNonce = authorizationNonce;
        Publication = publication;
        Provenance = provenance;
        RuntimeBinding = runtimeBinding;
        AuthorizationNonceDigestSha256 = authorizationNonceDigestSha256;
        RepositoryRootDigestSha256 = repositoryRootDigestSha256;
        PlanDigestSha256 = planDigestSha256;
        PromptPublicationDigestSha256 = publication.CanonicalDigestSha256;
        PromptSetDigestSha256 = publication.PromptSetDigestSha256;
        ProvenanceDigestSha256 = provenance.ProvenanceDigestSha256;
        RuntimeBindingDigestSha256 = SnowGlobePinnedOllamaRecordingModule.DigestRuntimeBinding(runtimeBinding);
        RuntimeExecutablePathDigestSha256 = CognitionQualityRecordingSessionCanonical.Digest(runtimeBinding.CanonicalExecutablePath);
    }

    internal CognitionQualityPromptEnvelopePublication Publication { get; }
    internal CognitionQualityExecutionProvenance Provenance { get; }
    internal OllamaLoopbackRuntimeBinding RuntimeBinding { get; }
    internal string AuthorizationNonce => _authorizationNonce;
    internal PlanConsumeResult TryConsume(SnowGlobeOllamaRecordingCompositionModule owner)
    {
        if (Interlocked.CompareExchange(ref _consumed, 1, 0) != 0) return PlanConsumeResult.Reused;
        return ReferenceEquals(_owner, owner) ? PlanConsumeResult.Consumed : PlanConsumeResult.BindingMismatch;
    }

    public string SchemaVersion => SnowGlobeOllamaRecordingCompositionModule.PlanSchemaVersion;
    public string RelativeArtifactPath => OllamaRecordingExecutionArtifactModule.RelativeArtifactPath;
    public string RegisteredCellDigestSha256 => SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256;
    public string ProfileDigestSha256 => SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256;
    public string AdapterIdentity => SnowGlobePinnedOllamaRecordingModule.AdapterIdentity;
    public string AdapterContractDigestSha256 => SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256;
    public string CodecContractDigestSha256 => SnowGlobePinnedOllamaRecordingModule.CodecContractDigestSha256;
    public string TransportContractDigestSha256 => OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256;
    public string PromptPublicationDigestSha256 { get; }
    public string PromptSetDigestSha256 { get; }
    public string ProvenanceDigestSha256 { get; }
    public string RuntimeBindingDigestSha256 { get; }
    public string RuntimeExecutablePathDigestSha256 { get; }
    public string RuntimeExecutableSha256 => SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256;
    public string AuthorizationNonceDigestSha256 { get; }
    public string RepositoryRootDigestSha256 { get; }
    public string PlanDigestSha256 { get; }
    public int RuntimeProcessId => RuntimeBinding.ProcessId;
    public long RuntimeProcessStartUtcTicks => RuntimeBinding.ProcessStartUtcTicks;
    public int EndpointOwnerProcessId => RuntimeBinding.EndpointOwnerProcessId;
    public bool IsConsumed => Volatile.Read(ref _consumed) != 0;
    public bool AdditionalAttemptAuthorized => false;
}

internal enum PlanConsumeResult { Consumed, Reused, BindingMismatch }

internal enum OllamaRecordingCompositionOutcomeCode { Complete, Failed, Cancelled, TimedOut, AuthorizationRejected, CompositionFailed }
internal enum OllamaRecordingCompositionFailureCode
{
    None, Cancelled, TimedOut, AuthorizationRejected, CompositionFailed,
    CapabilityExpired, RuntimeBindingInvalid, RuntimeChanged, TransportPoisoned, TransportFailure,
    HttpResponseRejected, ResponseBodyRejected, WrapperRejected, EvidenceRejected
}

/// <summary>Detached raw-free composition result; it never exposes prompts, proposals, or nested evidence.</summary>
public sealed class OllamaRecordingCompositionResult
{
    internal OllamaRecordingCompositionResult(string outcomeCode, string failureCode, OllamaRecordingExecutionArtifact? artifact)
    { OutcomeCode = outcomeCode; FailureCode = failureCode; Artifact = artifact; }
    public string OutcomeCode { get; }
    public string FailureCode { get; }
    public OllamaRecordingExecutionArtifact? Artifact { get; }
    public CognitionQualityScoreSummary? ScoreSummary => Artifact?.ScoreSummary;
    public string? ScoreSummaryDigestSha256 => Artifact?.ScoreSummaryDigestSha256;
    public bool AdditionalAttemptAuthorized => false;
    public bool ArtifactPublished => Artifact is not null;
    public bool HasRawRecordingEvidence => false;
}

/// <summary>
/// Fixed one-shot qwen recording composition. Construction and Prepare are deterministic zero-I/O;
/// only Execute creates the production store/module, and only ValidateArtifact performs a bounded read.
/// </summary>
public sealed class SnowGlobeOllamaRecordingCompositionModule
{
    public const string PromptRevision = "prompt-v1";
    internal const string PlanSchemaVersion = "snow_globe_ollama_recording_composition_plan/v5";
    internal const string LegacyPlanSchemaVersion = "snow_globe_ollama_recording_composition_plan/v4";
    private readonly string _absoluteRepositoryRoot;
    private readonly string _repositoryRootDigestSha256;
    private readonly SnowGlobePinnedOllamaRecordingModule? _injectedInnerModule;
    private readonly IOllamaRecordingArtifactStore? _injectedArtifactStore;
    private readonly Func<AuthorizedOllamaLoopbackRecordingSession, SnowGlobeOllamaLoopbackRecordingResult, SnowGlobeOllamaLoopbackRecordingResult>? _resultProjectionForTesting;

    public SnowGlobeOllamaRecordingCompositionModule(string absoluteRepositoryRoot)
    {
        (_absoluteRepositoryRoot, _repositoryRootDigestSha256) = CanonicalizeLexicalRepositoryRoot(absoluteRepositoryRoot);
    }

    internal SnowGlobeOllamaRecordingCompositionModule(
        string absoluteRepositoryRoot,
        SnowGlobePinnedOllamaRecordingModule innerModule,
        IOllamaRecordingArtifactStore artifactStore,
        Func<AuthorizedOllamaLoopbackRecordingSession, SnowGlobeOllamaLoopbackRecordingResult, SnowGlobeOllamaLoopbackRecordingResult>? resultProjectionForTesting = null)
    {
        (_absoluteRepositoryRoot, _repositoryRootDigestSha256) = CanonicalizeLexicalRepositoryRoot(absoluteRepositoryRoot);
        _injectedInnerModule = innerModule ?? throw new ArgumentNullException(nameof(innerModule));
        _injectedArtifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        _resultProjectionForTesting = resultProjectionForTesting;
    }

    public OllamaRecordingCompositionPlan Prepare(PinnedRuntimeObservation runtime, string authorizationNonce)
    {
        if (runtime is null || runtime.ProcessId <= 0 || runtime.ProcessStartUtcTicks <= 0 || runtime.ProcessStartUtcTicks > DateTime.MaxValue.Ticks) throw Failure("runtime_observation_invalid");
        if (!SnowGlobeInferenceIdentity.IsCanonical(authorizationNonce)) throw Failure("authorization_nonce_invalid");

        CognitionQualityPromptEnvelopePublication publication;
        try { publication = CognitionQualityPromptEnvelopeBuilderModule.Create(PromptRevision); }
        catch { throw Failure("prompt_publication_invalid"); }
        CognitionQualityExecutionProvenance provenance = CognitionQualityExecutionProvenance.ForLocal(
            SnowGlobePinnedOllamaRecordingModule.NormalizedModelIdentity,
            "sha256-" + SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
            SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256,
            publication.PromptRevision,
            CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion,
            SnowGlobePinnedOllamaRecordingModule.AdapterIdentity);
        OllamaLoopbackRuntimeBinding binding = new(
            runtime.ProcessId,
            runtime.ProcessStartUtcTicks,
            SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath,
            SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
            SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity,
            runtime.ProcessId);
        string nonceDigest = CognitionQualityRecordingSessionCanonical.Digest(authorizationNonce);
        string planDigest = ComputePlanDigest(publication, provenance, binding, _repositoryRootDigestSha256, nonceDigest,
            PlanSchemaVersion, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath);
        return new OllamaRecordingCompositionPlan(this, publication, provenance, binding, authorizationNonce, nonceDigest, _repositoryRootDigestSha256, planDigest);
    }

    internal static string ComputePlanDigest(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, OllamaLoopbackRuntimeBinding binding, string repositoryRootDigestSha256, string nonceDigest) =>
        ComputePlanDigest(publication, provenance, binding, repositoryRootDigestSha256, nonceDigest,
            PlanSchemaVersion, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath);

    internal static string ComputePlanDigest(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, OllamaLoopbackRuntimeBinding binding, string repositoryRootDigestSha256, string nonceDigest, string planSchemaVersion, string relativeArtifactPath) =>
        CognitionQualityRecordingSessionCanonical.Digest(string.Join('|',
            planSchemaVersion,
            relativeArtifactPath,
            SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256,
            SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256,
            SnowGlobePinnedOllamaRecordingModule.AdapterIdentity,
            SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256,
            SnowGlobePinnedOllamaRecordingModule.CodecContractDigestSha256,
            OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256,
            publication.CanonicalDigestSha256,
            publication.PromptSetDigestSha256,
            provenance.ProvenanceDigestSha256,
            SnowGlobePinnedOllamaRecordingModule.DigestRuntimeBinding(binding),
            binding.ProcessId,
            binding.ProcessStartUtcTicks,
            binding.EndpointOwnerProcessId,
            CognitionQualityRecordingSessionCanonical.Digest(binding.CanonicalExecutablePath),
            SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
            nonceDigest,
            repositoryRootDigestSha256));

    public async ValueTask<OllamaRecordingCompositionResult> ExecuteAndPublishOnceAsync(OllamaRecordingCompositionPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan is null) throw Failure("plan_invalid");
        PlanConsumeResult consume = plan.TryConsume(this);
        if (consume == PlanConsumeResult.BindingMismatch) return ClosedResult("CompositionFailed", "BindingMismatch");
        if (consume == PlanConsumeResult.Reused) return ClosedResult("CompositionFailed", "PlanReused");
        if (cancellationToken.IsCancellationRequested) return ClosedResult(OllamaRecordingCompositionOutcomeCode.Cancelled.ToString(), OllamaRecordingCompositionFailureCode.Cancelled.ToString());

        IOllamaRecordingArtifactStore store = _injectedArtifactStore ?? new FileOllamaRecordingArtifactStore();
        IOllamaRecordingArtifactReservation reservation;
        try { reservation = store.Reserve(_absoluteRepositoryRoot, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath); }
        catch (OllamaRecordingArtifactStoreException exception) { throw Failure(exception.Code); }
        catch { throw Failure("composition_execution_indeterminate"); }
        OllamaRecordingCompositionResult? completedResult = null;
        OllamaRecordingCompositionException? closedFailure = null;
        try
        {
            completedResult = await ExecuteReservedAsync(plan, cancellationToken, reservation).ConfigureAwait(false);
        }
        catch (OllamaRecordingCompositionException exception) { closedFailure = Failure(exception.Code); }
        catch (OllamaRecordingExecutionArtifactException exception) { closedFailure = Failure(exception.Code); }
        catch { closedFailure = Failure("composition_execution_indeterminate"); }
        try { reservation.Dispose(); }
        catch { closedFailure ??= Failure("artifact_reservation_dispose_failed"); }
        if (closedFailure is not null) throw closedFailure;
        return completedResult ?? throw Failure("composition_execution_indeterminate");
    }

    private async ValueTask<OllamaRecordingCompositionResult> ExecuteReservedAsync(OllamaRecordingCompositionPlan plan, CancellationToken cancellationToken, IOllamaRecordingArtifactReservation reservation)
    {
        OllamaRecordingArtifactSnapshot? snapshot = null;
        try
        {
            SnowGlobePinnedOllamaRecordingModule inner = _injectedInnerModule ?? new SnowGlobePinnedOllamaRecordingModule();
            AuthorizedOllamaLoopbackRecordingSession? session = null;
            try
            {
                session = inner.Authorize(
                    plan.Publication,
                    plan.RuntimeBinding,
                    new OllamaLoopbackRecordingAuthorization(plan.AuthorizationNonce));
            }
            catch (OllamaLoopbackRecordingAuthorizationException)
            {
                snapshot = CompositionFailureSnapshot(OllamaRecordingCompositionOutcomeCode.AuthorizationRejected, OllamaRecordingCompositionFailureCode.AuthorizationRejected);
            }
            catch (ArgumentException)
            {
                snapshot = CompositionFailureSnapshot(OllamaRecordingCompositionOutcomeCode.AuthorizationRejected, OllamaRecordingCompositionFailureCode.AuthorizationRejected);
            }

            if (session is not null)
            {
                await using (session)
                {
                    SnowGlobeOllamaLoopbackRecordingResult result = await session.RecordOnceAsync(cancellationToken).ConfigureAwait(false);
                    if (_resultProjectionForTesting is not null)
                        result = _resultProjectionForTesting(session, result) ?? throw Failure("composition_execution_indeterminate");
                    ReadOnlyMemory<byte>? receiptBytes = result.Receipt?.CanonicalUtf8;
                    (OllamaRecordingCompositionOutcomeCode compositionOutcome, OllamaRecordingCompositionFailureCode compositionFailure) = DeriveComposition(result);
                    snapshot = new OllamaRecordingArtifactSnapshot(
                        compositionOutcome.ToString(), compositionFailure.ToString(), true,
                        result.OutcomeCode.ToString(), result.FailureCode.ToString(), result.CompletedSlotCount,
                        result.TerminalSlotOrdinal, result.TerminalSubmissionState.ToString(), ChargeState.NotApplicable.ToString(), result.TerminalStatusCode,
                        result.TerminalCheckpointCode, result.TerminalPolicyCode,
                        receiptBytes, result.Receipt?.CanonicalDigestSha256, result.Receipt?.NestedRecordingEvidenceDigestSha256,
                        result.ScoreSummary?.CanonicalUtf8, result.ScoreSummaryDigestSha256);
                }
            }
        }
        catch
        {
            snapshot = CompositionFailureSnapshot(
                OllamaRecordingCompositionOutcomeCode.CompositionFailed,
                OllamaRecordingCompositionFailureCode.CompositionFailed);
        }

        snapshot ??= CompositionFailureSnapshot(
            OllamaRecordingCompositionOutcomeCode.CompositionFailed,
            OllamaRecordingCompositionFailureCode.CompositionFailed);
        OllamaRecordingExecutionArtifact artifact = OllamaRecordingExecutionArtifactModule.Create(plan, snapshot);
        byte[] readback;
        try { readback = reservation.PublishAndReadBack(artifact.CanonicalUtf8, OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes); }
        catch (OllamaRecordingArtifactStoreException exception) { throw Failure(exception.Code); }
        try
        {
            OllamaRecordingExecutionArtifact durable = OllamaRecordingExecutionArtifactModule.Validate(readback, plan.RepositoryRootDigestSha256);
            if (!readback.AsSpan().SequenceEqual(artifact.CanonicalUtf8.Span) || !string.Equals(durable.CanonicalDigestSha256, artifact.CanonicalDigestSha256, StringComparison.Ordinal)) throw Failure("artifact_durable_readback_mismatch");
            return new OllamaRecordingCompositionResult(durable.OutcomeCode, durable.FailureCode, durable);
        }
        finally { CryptographicOperations.ZeroMemory(readback); }
    }

    public OllamaRecordingExecutionArtifact ValidateArtifact()
    {
        IOllamaRecordingArtifactStore store = _injectedArtifactStore ?? new FileOllamaRecordingArtifactStore();
        byte[] bytes;
        try { bytes = store.ReadBounded(_absoluteRepositoryRoot, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath, OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes); }
        catch (OllamaRecordingArtifactStoreException exception) { throw Failure(exception.Code); }
        catch { throw Failure("composition_execution_indeterminate"); }
        try { return OllamaRecordingExecutionArtifactModule.Validate(bytes, _repositoryRootDigestSha256); }
        catch (OllamaRecordingExecutionArtifactException exception)
        {
            throw Failure(exception.Code == "artifact_repository_root_binding_invalid"
                ? exception.Code
                : "artifact_validation_failed");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static (OllamaRecordingCompositionOutcomeCode Outcome, OllamaRecordingCompositionFailureCode Failure) DeriveComposition(SnowGlobeOllamaLoopbackRecordingResult result) => result.OutcomeCode switch
    {
        SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete when result.FailureCode == SnowGlobeOllamaLoopbackRecordingFailureCode.None => (OllamaRecordingCompositionOutcomeCode.Complete, OllamaRecordingCompositionFailureCode.None),
        SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled when result.FailureCode == SnowGlobeOllamaLoopbackRecordingFailureCode.None => (OllamaRecordingCompositionOutcomeCode.Cancelled, OllamaRecordingCompositionFailureCode.Cancelled),
        SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut when result.FailureCode == SnowGlobeOllamaLoopbackRecordingFailureCode.None => (OllamaRecordingCompositionOutcomeCode.TimedOut, OllamaRecordingCompositionFailureCode.TimedOut),
        SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed when Enum.TryParse(result.FailureCode.ToString(), out OllamaRecordingCompositionFailureCode failure) && failure is not (OllamaRecordingCompositionFailureCode.None or OllamaRecordingCompositionFailureCode.Cancelled or OllamaRecordingCompositionFailureCode.TimedOut or OllamaRecordingCompositionFailureCode.AuthorizationRejected or OllamaRecordingCompositionFailureCode.CompositionFailed) => (OllamaRecordingCompositionOutcomeCode.Failed, failure),
        _ => throw Failure("composition_execution_indeterminate")
    };

    private static OllamaRecordingArtifactSnapshot CompositionFailureSnapshot(OllamaRecordingCompositionOutcomeCode outcome, OllamaRecordingCompositionFailureCode failure) => new(
        outcome.ToString(), failure.ToString(), false, null, null, null, null, null, null, null,
        outcome == OllamaRecordingCompositionOutcomeCode.AuthorizationRejected
            ? OllamaRecordingTerminalCheckpointCode.Authorization.ToString()
            : OllamaRecordingTerminalCheckpointCode.Composition.ToString(),
        outcome == OllamaRecordingCompositionOutcomeCode.AuthorizationRejected
            ? OllamaRecordingTerminalPolicyCode.Authorization.ToString()
            : OllamaRecordingTerminalPolicyCode.UnexpectedException.ToString(),
        null, null, null, null, null);
    private static OllamaRecordingCompositionResult ClosedResult(string outcome, string failure) => new(outcome, failure, null);

    private static (string Path, string DigestSha256) CanonicalizeLexicalRepositoryRoot(string value)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) throw Failure("repository_root_lexically_invalid");
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)); }
        catch (Exception) { throw Failure("repository_root_lexically_invalid"); }
        if (!string.Equals(value, full, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal)
            || full.StartsWith(@"\??\", StringComparison.Ordinal)
            || !IsCanonicalLocalDrivePath(full))
            throw Failure("repository_root_lexically_invalid");
        string canonicalForDigest = full.ToUpperInvariant();
        return (full, CognitionQualityRecordingSessionCanonical.Digest("repository-root-windows-v1|" + canonicalForDigest));
    }

    private static bool IsCanonicalLocalDrivePath(string full)
    {
        string? root = Path.GetPathRoot(full);
        if (root is null || root.Length != 3 || !char.IsAsciiLetter(root[0]) || root[1] != ':' || root[2] != '\\' || full.Length <= root.Length)
            return false;
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (string segment in full[root.Length..].Split('\\'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment[^1] is ' ' or '.'
                || segment.IndexOfAny(invalid) >= 0 || segment.Any(static value => value < ' '))
                return false;
            string deviceStem = segment.Split('.')[0];
            if (deviceStem.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || deviceStem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || deviceStem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || deviceStem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || deviceStem.Length == 4 && (deviceStem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceStem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                    && deviceStem[3] is >= '1' and <= '9')
                return false;
        }
        return true;
    }

    private static OllamaRecordingCompositionException Failure(string code) => new(code);
}
