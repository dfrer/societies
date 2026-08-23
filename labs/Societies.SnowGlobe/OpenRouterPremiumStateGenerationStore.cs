using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

internal interface IOpenRouterPremiumGenerationIdSource
{
    string NextGenerationHex();
}

internal interface IOpenRouterPremiumStateFaultInjector
{
    void AfterDurableBoundary(string boundary);
}

internal interface IOpenRouterPremiumStateIoObserver
{
    void OnExactPath(string path);
    void OnDirectoryEnumeration();
}

internal interface IOpenRouterPremiumStateTrustAnchor
{
    // The implementation and key persistence are intentionally external to v2 state. This store
    // cannot provide same-user/admin or whole-volume rollback protection from local files alone.
    string IdentitySha256 { get; }
    string Authenticate(ReadOnlySpan<byte> canonicalBytes);
    bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256);
}

internal sealed class RandomOpenRouterPremiumGenerationIdSource : IOpenRouterPremiumGenerationIdSource
{
    internal static RandomOpenRouterPremiumGenerationIdSource Instance { get; } = new();
    private RandomOpenRouterPremiumGenerationIdSource() { }
    public string NextGenerationHex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

internal sealed class OpenRouterPremiumStateGenerationStore
{
    private const string AuthenticatedStateEnvelopeSchema = "snow_globe_openrouter_authenticated_state/v2";
    internal const string GenerationManifestSchema = "snow_globe_openrouter_generation_manifest/v2";
    internal const string PreflightStartedSchema = "snow_globe_openrouter_preflight_started/v2";
    internal const string AuthorityLocatorSchema = "snow_globe_openrouter_authority_locator/v2";
    internal const string ExecutionClaimSchema = "snow_globe_openrouter_execution_consumed/v2";
    internal const string ValidationClaimSchema = "snow_globe_openrouter_validation_consumed/v2";
    internal const string EvidenceBindingSchema = "snow_globe_openrouter_evidence_binding/v2";
    internal const string ValidationReceiptSchema = "snow_globe_openrouter_validation_receipt/v2";
    internal const string ReceiptBindingSchema = "snow_globe_openrouter_validation_receipt_binding/v2";
    private const int MaximumManifestPayloadBytes = 4 * 1024;
    private const int MaximumLocatorPayloadBytes = 16 * 1024;
    private const int MaximumClaimPayloadBytes = 4 * 1024;
    internal const int MaximumManifestBytes = 8 * 1024;
    internal const int MaximumLocatorBytes = 32 * 1024;
    internal const int MaximumClaimBytes = 8 * 1024;
    internal const string BoundaryGenerationDirectory = "generation_directory";
    internal const string BoundaryGenerationManifest = "generation_manifest";
    internal const string BoundaryPreflightStarted = "preflight_started";
    internal const string BoundaryActivationBundle = "activation_bundle";
    internal const string BoundaryJournal = "journal";
    internal const string BoundaryJournalPublicationFreeze = "journal_publication_freeze";
    internal const string BoundaryPreflightArtifact = "preflight_artifact";
    internal const string BoundaryRuntimeAuthorization = "runtime_authorization";
    internal const string BoundaryAuthorityLocator = "authority_locator";
    internal const string BoundaryExecutionClaim = "execution_claim";
    internal const string BoundaryEvidence = "evidence";
    internal const string BoundaryEvidenceBinding = "evidence_binding";
    internal const string BoundaryValidationClaim = "validation_claim";
    internal const string BoundaryValidationReceipt = "validation_receipt";
    internal const string BoundaryReceiptBinding = "validation_receipt_binding";
    internal static IReadOnlyList<string> DurableBoundaryNames { get; } =
    [
        BoundaryGenerationDirectory, BoundaryGenerationManifest, BoundaryPreflightStarted,
        BoundaryActivationBundle, BoundaryJournal, BoundaryJournalPublicationFreeze, BoundaryPreflightArtifact,
        BoundaryRuntimeAuthorization, BoundaryAuthorityLocator
    ];

    private const string GenerationsDirectoryName = "generations";
    private const string AuthoritiesDirectoryName = "authorities";
    private const string ExecutionClaimsDirectoryName = "execution-consumed";
    private const string ValidationClaimsDirectoryName = "validation-consumed";
    private const string WriterLockFileName = "root-writer.lock";
    private const string ManifestFileName = "generation-manifest.json";
    private const string PreflightStartedFileName = "preflight-started.json";
    private const string ActivationBundleFileName = "activation-bundle.json";
    private const string PreflightArtifactFileName = "preflight-artifact.json";
    private const string RuntimeAuthorizationFileName = "runtime-authorization.dpapi";
    private const string JournalDirectoryName = "journal";
    private const string EvidenceFileName = "live-evidence.json";
    private const string EvidenceBindingFileName = "live-evidence.binding.json";
    private const string ValidationReceiptFileName = "validation-receipt.json";
    private const string ReceiptBindingFileName = "validation-receipt.binding.json";
    private static readonly byte[] WriterLockBytes = Encoding.ASCII.GetBytes(
        "{\"schema_version\":\"snow_globe_openrouter_root_writer_lock/v2\",\"lock_identity\":\"openrouter-premium-state-generation-writer/v2\"}");
    internal static readonly string StateContractDigestSha256 = OpenRouterPremiumCanonical.Digest(string.Join('|',
        "snow_globe_openrouter_authenticated_append_only_state_contract/v2", GenerationManifestSchema,
        PreflightStartedSchema, AuthorityLocatorSchema, ExecutionClaimSchema, ValidationClaimSchema,
        EvidenceBindingSchema, ReceiptBindingSchema,
        ValidationReceiptSchema,
        "generations/g2-<sha256>", "authorities/<sha256>.json",
        "execution-consumed/<sha256>.json", "validation-consumed/<sha256>.json",
        AuthenticatedStateEnvelopeSchema, "caller-held-trust-anchor",
        "restart-requires-same-external-trust-anchor", "cooperative-create-new-append-only",
        "same-user-or-admin-tamper-protected-without-external-anchor=false",
        "whole-volume-rollback-protected=false",
        "claimed-execution-journal-reopen-after-durable-claim-only",
        "final-journal-and-evidence-authenticated-together",
        "create-new", "write-through", "readback", "no-enumeration", "no-repair", "no-retry"));

    private readonly string _root;
    private readonly string _generations;
    private readonly string _authorities;
    private readonly string _executionClaims;
    private readonly string _validationClaims;
    private readonly IOpenRouterPremiumGenerationIdSource _ids;
    private readonly IOpenRouterPremiumStateTrustAnchor _trustAnchor;
    private readonly IOpenRouterPremiumStateFaultInjector _faults;
    private readonly IOpenRouterPremiumStateIoObserver _observer;
    private readonly FileOpenRouterPremiumDirectoryIdentity _rootIdentity;
    private readonly FileOpenRouterPremiumDirectoryIdentity _generationsIdentity;
    private readonly FileOpenRouterPremiumDirectoryIdentity _authoritiesIdentity;
    private readonly FileOpenRouterPremiumDirectoryIdentity _executionClaimsIdentity;
    private readonly FileOpenRouterPremiumDirectoryIdentity _validationClaimsIdentity;

    internal OpenRouterPremiumStateGenerationStore(
        string root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor,
        IOpenRouterPremiumGenerationIdSource? generationIds = null,
        IOpenRouterPremiumStateFaultInjector? faults = null,
        IOpenRouterPremiumStateIoObserver? observer = null)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        ArgumentNullException.ThrowIfNull(trustAnchor);
        ValidateTrustAnchor(trustAnchor);
        _root = ValidateRoot(root);
        _trustAnchor = trustAnchor;
        _ids = generationIds ?? RandomOpenRouterPremiumGenerationIdSource.Instance;
        _faults = faults ?? NoFaults.Instance;
        _observer = observer ?? NoIoObserver.Instance;
        Observe(_root);
        if (!Directory.Exists(_root)) throw new OpenRouterPremiumProductionException("state_root_invalid");
        using SafeFileHandle rootHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_root);
        RequireCanonicalDirectoryPath(_root);
        _rootIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_root);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, rootHandle);
        _generations = Path.Combine(_root, GenerationsDirectoryName);
        _authorities = Path.Combine(_root, AuthoritiesDirectoryName);
        _executionClaims = Path.Combine(_root, ExecutionClaimsDirectoryName);
        _validationClaims = Path.Combine(_root, ValidationClaimsDirectoryName);
        _generationsIdentity = CreateOrVerifyDirectory(_generations, rootHandle);
        _authoritiesIdentity = CreateOrVerifyDirectory(_authorities, rootHandle);
        _executionClaimsIdentity = CreateOrVerifyDirectory(_executionClaims, rootHandle);
        _validationClaimsIdentity = CreateOrVerifyDirectory(_validationClaims, rootHandle);
        InitializeWriterLock();
        RootPathDigestSha256 = OpenRouterPremiumCanonical.Digest("snow-globe-openrouter-v2-root|" + _root);
    }

    internal string RootPathDigestSha256 { get; }

    internal OpenRouterPremiumGenerationWriter BeginPreflight(long startedAtUnixMilliseconds)
    {
        if (startedAtUnixMilliseconds < 0)
            throw new OpenRouterPremiumProductionException("generation_time_invalid");
        RootWriterLease lease = AcquireWriterLease();
        try
        {
            string generationHex = _ids.NextGenerationHex();
            if (!IsDigest(generationHex))
                throw new OpenRouterPremiumProductionException("generation_id_invalid");
            string generationId = "g2-" + generationHex;
            string generationPath = Path.Combine(_generations, generationId);
            Observe(generationPath);
            CreateGenerationDirectoryOnce(generationPath);
            _faults.AfterDurableBoundary(BoundaryGenerationDirectory);
            FileOpenRouterPremiumDirectoryIdentity generationIdentity;
            try { generationIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(generationPath); }
            catch { throw new OpenRouterPremiumProductionException("generation_identity_invalid"); }
            lease.PinGeneration(generationPath, generationIdentity);
            VerifyOperationDirectories(lease);
            GenerationManifest manifest = new(GenerationManifestSchema, generationId,
                StateContractDigestSha256, RootPathDigestSha256,
                OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256, _trustAnchor.IdentitySha256,
                true, true, false, startedAtUnixMilliseconds, false, false);
            byte[] manifestBytes = manifest.Write();
            try { WriteProtectedNew(generationPath, generationIdentity, ManifestFileName,
                "generation_manifest", manifestBytes, MaximumManifestPayloadBytes, MaximumManifestBytes); }
            finally { CryptographicOperations.ZeroMemory(manifestBytes); }
            _faults.AfterDurableBoundary(BoundaryGenerationManifest);
            VerifyOperationDirectories(lease);
            string manifestDigest = DigestExactFile(generationPath, generationIdentity, ManifestFileName,
                MaximumManifestBytes, out FileOpenRouterPremiumFileIdentity manifestFileIdentity);
            PreflightStarted started = new(PreflightStartedSchema, generationId, manifestDigest,
                StateContractDigestSha256, RootPathDigestSha256, startedAtUnixMilliseconds, false, false);
            byte[] startedBytes = started.Write();
            try { WriteProtectedNew(generationPath, generationIdentity, PreflightStartedFileName,
                "preflight_started", startedBytes, MaximumClaimPayloadBytes, MaximumClaimBytes); }
            finally { CryptographicOperations.ZeroMemory(startedBytes); }
            _faults.AfterDurableBoundary(BoundaryPreflightStarted);
            VerifyOperationDirectories(lease);
            string startedDigest = DigestExactFile(generationPath, generationIdentity, PreflightStartedFileName,
                MaximumClaimBytes, out FileOpenRouterPremiumFileIdentity startedFileIdentity);
            GenerationWriteContext context = new(lease, generationId, generationPath, generationIdentity,
                manifestDigest, manifestFileIdentity, startedDigest, startedFileIdentity,
                startedAtUnixMilliseconds);
            return new(lease, generationId, manifestDigest, startedAtUnixMilliseconds,
                bytes => WriteGenerationArtifact(context, ActivationBundleFileName, bytes, 16 * 1024,
                    BoundaryActivationBundle),
                header => CreateGenerationJournal(context, header),
                bytes => WriteGenerationArtifact(context, PreflightArtifactFileName, bytes,
                    OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes,
                    BoundaryPreflightArtifact),
                () => _faults.AfterDurableBoundary(BoundaryJournalPublicationFreeze),
                bytes => PublishGenerationAuthorization(context, bytes));
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal OpenRouterPremiumResolvedGeneration ResolveAuthority(string authorizationDigestSha256)
    {
        RequireDigest(authorizationDigestSha256, "authorization_confirmation_invalid");
        using RootWriterLease lease = AcquireWriterLease();
        using ResolvedGeneration resolved = ResolveCommittedGeneration(lease, authorizationDigestSha256);
        return resolved.Detached();
    }

    internal OpenRouterPremiumExecutionGeneration OpenForExecution(
        string authorizationDigestSha256,
        long claimedAtUnixMilliseconds)
    {
        RequireDigest(authorizationDigestSha256, "authorization_confirmation_invalid");
        if (claimedAtUnixMilliseconds < 0)
            throw new OpenRouterPremiumProductionException("generation_time_invalid");
        RootWriterLease lease = AcquireWriterLease();
        try
        {
            string claimName = authorizationDigestSha256 + ".json";
            if (ExactFileExists(_executionClaims, _executionClaimsIdentity, claimName))
                throw ExistingExecutionClaim(lease, authorizationDigestSha256, claimName);
            using ResolvedGeneration resolved = ResolveCommittedGeneration(lease, authorizationDigestSha256);
            RootClaim claim = new(ExecutionClaimSchema, authorizationDigestSha256,
                resolved.Locator.GenerationId, resolved.Locator.GenerationManifestDigestSha256,
                StateContractDigestSha256, RootPathDigestSha256, null, claimedAtUnixMilliseconds, false);
            byte[] bytes = claim.Write();
            try { WriteProtectedNew(_executionClaims, _executionClaimsIdentity, claimName,
                "execution_claim", bytes, MaximumClaimPayloadBytes, MaximumClaimBytes); }
            catch (IOException) { throw new OpenRouterPremiumProductionException("execution_already_consumed"); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            VerifyOperationDirectories(lease);
            _faults.AfterDurableBoundary(BoundaryExecutionClaim);
            GenerationMutationContext context = new(lease, resolved.Locator, resolved.GenerationPath,
                resolved.GenerationIdentity, claimedAtUnixMilliseconds);
            ClaimedExecutionJournalGrant journalGrant = CreateClaimedExecutionJournalGrant(resolved);
            FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.OpenForClaimedExecution(journalGrant);
            try
            {
                return new(lease, resolved.Locator.GenerationId, resolved.Locator.AuthorizationDigestSha256,
                    resolved.Locator.GenerationManifestDigestSha256, claimedAtUnixMilliseconds,
                    resolved.RuntimeAuthorizationBytes, resolved.PreflightArtifactBytes, journal, journalGrant,
                    (evidence, finalJournal) => PublishEvidence(context, evidence, finalJournal));
            }
            catch { journal.Dispose(); throw; }
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal OpenRouterPremiumValidationGeneration OpenForValidation(
        string authorizationDigestSha256,
        long claimedAtUnixMilliseconds)
    {
        RequireDigest(authorizationDigestSha256, "authorization_confirmation_invalid");
        if (claimedAtUnixMilliseconds < 0)
            throw new OpenRouterPremiumProductionException("generation_time_invalid");
        RootWriterLease lease = AcquireWriterLease();
        byte[]? evidenceToClear = null;
        ResolvedGeneration? resolvedToDispose = null;
        try
        {
            string executionName = authorizationDigestSha256 + ".json";
            if (!ExactFileExists(_executionClaims, _executionClaimsIdentity, executionName))
                throw new OpenRouterPremiumProductionException("validation_not_available");
            RootClaim executionClaim;
            try { executionClaim = ReadRootClaim(_executionClaims, _executionClaimsIdentity, executionName, ExecutionClaimSchema); }
            catch { throw new OpenRouterPremiumProductionException("execution_consumed_indeterminate"); }
            string validationName = authorizationDigestSha256 + ".json";
            if (ExactFileExists(_validationClaims, _validationClaimsIdentity, validationName))
            {
                try
                {
                    using ResolvedGeneration alreadyConsumed = ResolveCommittedGeneration(lease, authorizationDigestSha256,
                        verifyInitialJournal: false);
                    throw ExistingValidationClaim(authorizationDigestSha256, validationName, alreadyConsumed);
                }
                catch (OpenRouterPremiumProductionException exception) when (exception.Code is
                    "validation_already_consumed" or "validation_consumed_failed")
                {
                    throw;
                }
                catch { throw new OpenRouterPremiumProductionException("validation_consumed_failed"); }
            }
            ResolvedGeneration resolved;
            byte[] evidence;
            EvidenceBinding evidenceBinding;
            try
            {
                resolved = ResolveCommittedGeneration(lease, authorizationDigestSha256, verifyInitialJournal: false);
                resolvedToDispose = resolved;
                RequireClaimBindings(executionClaim, resolved.Locator);
                evidence = ReadExact(resolved.GenerationPath, resolved.GenerationIdentity,
                    EvidenceFileName, OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes);
                evidenceToClear = evidence;
                evidenceBinding = ReadEvidenceBinding(resolved, evidence);
            }
            catch { throw new OpenRouterPremiumProductionException("execution_consumed_indeterminate"); }
            RootClaim validationClaim = new(ValidationClaimSchema, authorizationDigestSha256,
                resolved.Locator.GenerationId, resolved.Locator.GenerationManifestDigestSha256,
                StateContractDigestSha256, RootPathDigestSha256,
                evidenceBinding.EvidenceDigestSha256, claimedAtUnixMilliseconds, false);
            byte[] claimBytes = validationClaim.Write();
            try { WriteProtectedNew(_validationClaims, _validationClaimsIdentity, validationName,
                "validation_claim", claimBytes, MaximumClaimPayloadBytes, MaximumClaimBytes); }
            catch (IOException)
            {
                CryptographicOperations.ZeroMemory(evidence);
                throw new OpenRouterPremiumProductionException("validation_already_consumed");
            }
            finally { CryptographicOperations.ZeroMemory(claimBytes); }
            VerifyOperationDirectories(lease);
            _faults.AfterDurableBoundary(BoundaryValidationClaim);
            OpenRouterPremiumEvidenceArtifact validatedEvidence =
                OpenRouterPremiumEvidenceArtifactModule.Validate(evidence);
            if (validatedEvidence.CanonicalDigestSha256 != evidenceBinding.EvidenceDigestSha256)
                throw new OpenRouterPremiumProductionException("evidence_binding_invalid");
            ValidationReceipt receipt = new(ValidationReceiptSchema,
                resolved.Locator.AuthorizationDigestSha256, resolved.Locator.GenerationId,
                resolved.Locator.GenerationManifestDigestSha256, StateContractDigestSha256,
                RootPathDigestSha256, validatedEvidence.CanonicalDigestSha256,
                validatedEvidence.Status, validatedEvidence.ExchangeCount,
                validatedEvidence.TotalSettledMicrousd, claimedAtUnixMilliseconds, false);
            byte[] receiptBytes = receipt.Write();
            GenerationMutationContext context = new(lease, resolved.Locator, resolved.GenerationPath,
                resolved.GenerationIdentity, claimedAtUnixMilliseconds);
            OpenRouterPremiumValidationGeneration result;
            try
            {
                result = new(lease, resolved.Locator.GenerationId,
                    resolved.Locator.AuthorizationDigestSha256, resolved.Locator.GenerationManifestDigestSha256,
                    evidence, evidenceBinding.EvidenceDigestSha256, receiptBytes,
                    claimedAtUnixMilliseconds,
                    canonicalReceipt => PublishReceipt(context, evidenceBinding.EvidenceDigestSha256, canonicalReceipt));
            }
            finally { CryptographicOperations.ZeroMemory(receiptBytes); }
            evidenceToClear = null;
            return result;
        }
        catch
        {
            if (evidenceToClear is not null) CryptographicOperations.ZeroMemory(evidenceToClear);
            lease.Dispose();
            throw;
        }
        finally { resolvedToDispose?.Dispose(); }
    }

    private OpenRouterPremiumProductionException ExistingExecutionClaim(RootWriterLease lease, string authority, string claimName)
    {
        try
        {
            RootClaim claim = ReadRootClaim(_executionClaims, _executionClaimsIdentity, claimName, ExecutionClaimSchema);
            AuthorityLocator locator = ReadLocator(authority);
            RequireClaimBindings(claim, locator);
            string generationPath = ExactGenerationPath(locator.GenerationId);
            FileOpenRouterPremiumDirectoryIdentity generationIdentity = VerifyGenerationDirectory(generationPath, locator);
            lease.PinGeneration(generationPath, generationIdentity);
            byte[] evidence = ReadExact(generationPath, generationIdentity,
                EvidenceFileName, OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes);
            using ResolvedGeneration detached = new(locator, generationPath, generationIdentity, [], [], []);
            try { _ = ReadEvidenceBinding(detached, evidence); }
            finally { CryptographicOperations.ZeroMemory(evidence); }
            return new OpenRouterPremiumProductionException("execution_already_consumed");
        }
        catch (OpenRouterPremiumProductionException exception) when (exception.Code == "execution_already_consumed")
        {
            return exception;
        }
        catch { return new OpenRouterPremiumProductionException("execution_consumed_indeterminate"); }
    }

    private OpenRouterPremiumProductionException ExistingValidationClaim(
        string authority,
        string claimName,
        ResolvedGeneration resolved)
    {
        try
        {
            RootClaim claim = ReadRootClaim(_validationClaims, _validationClaimsIdentity, claimName, ValidationClaimSchema);
            RequireClaimBindings(claim, resolved.Locator);
            byte[] receipt = ReadExact(resolved.GenerationPath, resolved.GenerationIdentity,
                ValidationReceiptFileName, 16 * 1024);
            try { _ = ReadReceiptBinding(resolved, receipt, claim.EvidenceDigestSha256!); }
            finally { CryptographicOperations.ZeroMemory(receipt); }
            return new OpenRouterPremiumProductionException("validation_already_consumed");
        }
        catch (OpenRouterPremiumProductionException exception) when (exception.Code == "validation_already_consumed")
        {
            return exception;
        }
        catch { return new OpenRouterPremiumProductionException("validation_consumed_failed"); }
    }

    private ResolvedGeneration ResolveCommittedGeneration(RootWriterLease lease, string authority,
        bool verifyInitialJournal = true)
    {
        AuthorityLocator locator = ReadLocator(authority);
        string generationPath = ExactGenerationPath(locator.GenerationId);
        FileOpenRouterPremiumDirectoryIdentity generationIdentity = VerifyGenerationDirectory(generationPath, locator);
        lease.PinGeneration(generationPath, generationIdentity);
        byte[] manifestBytes = ReadExact(generationPath, generationIdentity, ManifestFileName, MaximumManifestBytes,
            FileOpenRouterPremiumFileIdentity.Parse(locator.GenerationManifestFileIdentity));
        byte[] startedBytes = [];
        byte[] manifestPayload = [];
        byte[] startedPayload = [];
        byte[] preflightBytes = [];
        byte[] authorizationBytes = [];
        try
        {
            manifestPayload = AuthenticatedState.Unwrap(manifestBytes, "generation_manifest",
                MaximumManifestBytes, MaximumManifestPayloadBytes, _trustAnchor);
            GenerationManifest manifest = GenerationManifest.Parse(manifestPayload);
            if (manifest.GenerationId != locator.GenerationId
                || manifest.StateContractDigestSha256 != StateContractDigestSha256
                || manifest.RootPathDigestSha256 != RootPathDigestSha256
                || manifest.ProfileDigestSha256 != OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256
                || manifest.ExternalTrustAnchorIdentitySha256 != _trustAnchor.IdentitySha256
                || OpenRouterPremiumCanonical.Digest(manifestBytes) != locator.GenerationManifestDigestSha256)
                throw new OpenRouterPremiumProductionException("generation_binding_invalid");
            startedBytes = ReadLocatorArtifact(generationPath, generationIdentity, PreflightStartedFileName,
                MaximumClaimBytes, locator.PreflightStartedDigestSha256, locator.PreflightStartedFileIdentity);
            startedPayload = AuthenticatedState.Unwrap(startedBytes, "preflight_started",
                MaximumClaimBytes, MaximumClaimPayloadBytes, _trustAnchor);
            PreflightStarted started = PreflightStarted.Parse(startedPayload);
            if (started.GenerationId != locator.GenerationId
                || started.GenerationManifestDigestSha256 != locator.GenerationManifestDigestSha256
                || started.StateContractDigestSha256 != StateContractDigestSha256
                || started.RootPathDigestSha256 != RootPathDigestSha256)
                throw new OpenRouterPremiumProductionException("generation_binding_invalid");
            VerifyLocatorArtifact(generationPath, generationIdentity, ActivationBundleFileName, 16 * 1024,
                locator.ActivationBundleDigestSha256, locator.ActivationBundleFileIdentity);
            VerifyLocatorArtifact(generationPath, generationIdentity, Path.Combine(JournalDirectoryName,
                    FileOpenRouterPremiumJournal.HeaderFileName), 4 * 1024,
                locator.JournalHeaderDigestSha256, locator.JournalHeaderFileIdentity);
            if (verifyInitialJournal)
            {
                VerifyLocatorArtifact(generationPath, generationIdentity, Path.Combine(JournalDirectoryName,
                        FileOpenRouterPremiumJournal.RecordsFileName), 256 * 1024,
                    locator.JournalRecordsDigestSha256, locator.JournalRecordsFileIdentity, allowEmpty: true);
            }
            else
            {
                VerifyLocatorArtifactIdentity(generationPath, generationIdentity,
                    Path.Combine(JournalDirectoryName, FileOpenRouterPremiumJournal.RecordsFileName),
                    locator.JournalRecordsFileIdentity);
            }
            VerifyLocatorArtifact(generationPath, generationIdentity, Path.Combine(JournalDirectoryName,
                    FileOpenRouterPremiumJournal.WriterLeaseFileName), 256,
                locator.JournalWriterLeaseDigestSha256, locator.JournalWriterLeaseFileIdentity, allowEmpty: true);
            preflightBytes = ReadLocatorArtifact(generationPath, generationIdentity, PreflightArtifactFileName,
                OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes,
                locator.PreflightArtifactDigestSha256, locator.PreflightArtifactFileIdentity);
            authorizationBytes = ReadLocatorArtifact(generationPath, generationIdentity, RuntimeAuthorizationFileName,
                64 * 1024, locator.RuntimeAuthorizationDigestSha256, locator.RuntimeAuthorizationFileIdentity);
            VerifyOperationDirectories(lease);
            return new(locator, generationPath, generationIdentity, manifestBytes.ToArray(),
                preflightBytes.ToArray(), authorizationBytes.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(manifestBytes);
            CryptographicOperations.ZeroMemory(startedBytes);
            CryptographicOperations.ZeroMemory(manifestPayload);
            CryptographicOperations.ZeroMemory(startedPayload);
            CryptographicOperations.ZeroMemory(preflightBytes);
            CryptographicOperations.ZeroMemory(authorizationBytes);
        }
    }

    private AuthorityLocator ReadLocator(string authority)
    {
        string name = authority + ".json";
        byte[] bytes;
        try { bytes = ReadExact(_authorities, _authoritiesIdentity, name, MaximumLocatorBytes); }
        catch { throw new OpenRouterPremiumProductionException("authority_locator_invalid"); }
        byte[] payload = [];
        try
        {
            payload = AuthenticatedState.Unwrap(bytes, "authority_locator", MaximumLocatorBytes,
                MaximumLocatorPayloadBytes, _trustAnchor);
            AuthorityLocator locator = AuthorityLocator.Parse(payload);
            if (locator.AuthorizationDigestSha256 != authority
                || locator.RuntimeAuthorizationDigestSha256 != authority
                || locator.StateContractDigestSha256 != StateContractDigestSha256
                || locator.RootPathDigestSha256 != RootPathDigestSha256)
                throw new OpenRouterPremiumProductionException("authority_locator_invalid");
            return locator;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private FileOpenRouterPremiumDirectoryIdentity VerifyGenerationDirectory(
        string generationPath,
        AuthorityLocator locator)
    {
        Observe(generationPath);
        FileOpenRouterPremiumDirectoryIdentity actual = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(generationPath);
        if (actual != FileOpenRouterPremiumDirectoryIdentity.Parse(locator.GenerationDirectoryIdentity))
            throw new OpenRouterPremiumProductionException("generation_identity_changed");
        return actual;
    }

    private void VerifyLocatorArtifact(string generationPath,
        FileOpenRouterPremiumDirectoryIdentity generationIdentity, string relativeName, int maximum,
        string expectedDigest, string expectedIdentity, bool allowEmpty = false)
    {
        byte[] bytes = ReadRelativeExact(generationPath, generationIdentity, relativeName, maximum,
            FileOpenRouterPremiumFileIdentity.Parse(expectedIdentity), allowEmpty);
        try
        {
            if (!FixedDigestEquals(OpenRouterPremiumCanonical.Digest(bytes), expectedDigest))
                throw new OpenRouterPremiumProductionException("generation_binding_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private void VerifyLocatorArtifactIdentity(string generationPath,
        FileOpenRouterPremiumDirectoryIdentity generationIdentity, string relativeName, string expectedIdentity)
    {
        byte[] bytes = ReadRelativeExact(generationPath, generationIdentity, relativeName, 256 * 1024,
            FileOpenRouterPremiumFileIdentity.Parse(expectedIdentity), allowEmpty: true);
        CryptographicOperations.ZeroMemory(bytes);
    }

    private byte[] ReadLocatorArtifact(string generationPath,
        FileOpenRouterPremiumDirectoryIdentity generationIdentity, string relativeName, int maximum,
        string expectedDigest, string expectedIdentity)
    {
        byte[] bytes = ReadRelativeExact(generationPath, generationIdentity, relativeName, maximum,
            FileOpenRouterPremiumFileIdentity.Parse(expectedIdentity));
        if (!FixedDigestEquals(OpenRouterPremiumCanonical.Digest(bytes), expectedDigest))
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new OpenRouterPremiumProductionException("generation_binding_invalid");
        }
        return bytes;
    }

    private EvidenceBinding ReadEvidenceBinding(ResolvedGeneration resolved, byte[] evidence)
    {
        byte[] bindingBytes = ReadExact(resolved.GenerationPath, resolved.GenerationIdentity,
            EvidenceBindingFileName, MaximumClaimBytes);
        byte[] bindingPayload = [];
        try
        {
            bindingPayload = AuthenticatedState.Unwrap(bindingBytes, "evidence_binding",
                MaximumClaimBytes, MaximumClaimPayloadBytes, _trustAnchor);
            EvidenceBinding binding = EvidenceBinding.Parse(bindingPayload);
            FileOpenRouterPremiumFileIdentity evidenceIdentity = FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(
                resolved.GenerationPath, EvidenceFileName);
            if (binding.AuthorizationDigestSha256 != resolved.Locator.AuthorizationDigestSha256
                || binding.GenerationId != resolved.Locator.GenerationId
                || binding.GenerationManifestDigestSha256 != resolved.Locator.GenerationManifestDigestSha256
                || binding.StateContractDigestSha256 != StateContractDigestSha256
                || binding.RootPathDigestSha256 != RootPathDigestSha256
                || binding.EvidenceDigestSha256 != OpenRouterPremiumCanonical.Digest(evidence)
                || binding.EvidenceFileIdentity != evidenceIdentity.CanonicalIdentity
                || binding.FinalJournalRecordsFileIdentity != resolved.Locator.JournalRecordsFileIdentity)
                throw new OpenRouterPremiumProductionException("evidence_binding_invalid");
            ClaimedExecutionJournalGrant journalGrant = CreateClaimedExecutionJournalGrant(resolved,
                finalReadbackOnly: true);
            using FileOpenRouterPremiumJournal journal =
                FileOpenRouterPremiumJournal.OpenForClaimedExecution(journalGrant, finalReadback: true);
            OpenRouterPremiumDurableRestartEvidence restart = journal.RestartEvidence;
            OpenRouterPremiumJournalSnapshot snapshot = journal.Snapshot();
            if (!restart.RestartVerified || !restart.FlushToDiskRequested
                || binding.FinalJournalDirectoryIdentity != journalGrant.JournalDirectoryIdentity.CanonicalIdentity
                || !FixedDigestEquals(binding.FinalJournalRecordsDigestSha256, journal.RecordsFileDigestSha256)
                || binding.FinalJournalRecordCount != restart.RecordCount
                || !FixedDigestEquals(binding.FinalJournalRecordChecksumSha256, restart.FinalRecordChecksumSha256)
                || !FixedDigestEquals(binding.FinalJournalSnapshotDigestSha256,
                    FileOpenRouterPremiumJournal.SnapshotDigest(snapshot))
                || !FixedDigestEquals(binding.FinalJournalRestartEvidenceDigestSha256,
                    restart.EvidenceDigestSha256))
                throw new OpenRouterPremiumProductionException("evidence_binding_invalid");
            return binding;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bindingBytes);
            CryptographicOperations.ZeroMemory(bindingPayload);
        }
    }

    private ReceiptBinding ReadReceiptBinding(ResolvedGeneration resolved, byte[] receipt, string evidenceDigest)
    {
        byte[] bindingBytes = ReadExact(resolved.GenerationPath, resolved.GenerationIdentity,
            ReceiptBindingFileName, MaximumClaimBytes);
        byte[] bindingPayload = [];
        try
        {
            bindingPayload = AuthenticatedState.Unwrap(bindingBytes, "receipt_binding",
                MaximumClaimBytes, MaximumClaimPayloadBytes, _trustAnchor);
            ReceiptBinding binding = ReceiptBinding.Parse(bindingPayload);
            ValidationReceipt validationReceipt = ValidationReceipt.Parse(receipt);
            FileOpenRouterPremiumFileIdentity receiptIdentity = FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(
                resolved.GenerationPath, ValidationReceiptFileName);
            if (binding.AuthorizationDigestSha256 != resolved.Locator.AuthorizationDigestSha256
                || binding.GenerationId != resolved.Locator.GenerationId
                || binding.GenerationManifestDigestSha256 != resolved.Locator.GenerationManifestDigestSha256
                || binding.StateContractDigestSha256 != StateContractDigestSha256
                || binding.RootPathDigestSha256 != RootPathDigestSha256
                || binding.EvidenceDigestSha256 != evidenceDigest
                || binding.ReceiptDigestSha256 != OpenRouterPremiumCanonical.Digest(receipt)
                || binding.ReceiptFileIdentity != receiptIdentity.CanonicalIdentity)
                throw new OpenRouterPremiumProductionException("validation_receipt_binding_invalid");
            if (validationReceipt.AuthorizationDigestSha256 != resolved.Locator.AuthorizationDigestSha256
                || validationReceipt.GenerationId != resolved.Locator.GenerationId
                || validationReceipt.GenerationManifestDigestSha256 != resolved.Locator.GenerationManifestDigestSha256
                || validationReceipt.StateContractDigestSha256 != StateContractDigestSha256
                || validationReceipt.RootPathDigestSha256 != RootPathDigestSha256
                || validationReceipt.EvidenceArtifactDigestSha256 != evidenceDigest)
                throw new OpenRouterPremiumProductionException("validation_receipt_binding_invalid");
            return binding;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bindingBytes);
            CryptographicOperations.ZeroMemory(bindingPayload);
        }
    }

    private static void RequireClaimBindings(RootClaim claim, AuthorityLocator locator)
    {
        if (claim.AuthorizationDigestSha256 != locator.AuthorizationDigestSha256
            || claim.GenerationId != locator.GenerationId
            || claim.GenerationManifestDigestSha256 != locator.GenerationManifestDigestSha256
            || claim.StateContractDigestSha256 != locator.StateContractDigestSha256
            || claim.RootPathDigestSha256 != locator.RootPathDigestSha256)
            throw new OpenRouterPremiumProductionException("consumption_claim_invalid");
    }

    private RootClaim ReadRootClaim(string directory,
        FileOpenRouterPremiumDirectoryIdentity directoryIdentity, string name, string schema)
    {
        byte[] bytes = ReadExact(directory, directoryIdentity, name, MaximumClaimBytes);
        byte[] payload = [];
        try
        {
            string kind = schema == ExecutionClaimSchema ? "execution_claim" : "validation_claim";
            payload = AuthenticatedState.Unwrap(bytes, kind, MaximumClaimBytes,
                MaximumClaimPayloadBytes, _trustAnchor);
            RootClaim claim = RootClaim.Parse(payload);
            if (claim.SchemaVersion != schema) throw new InvalidDataException();
            return claim;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private string ExactGenerationPath(string generationId)
    {
        if (!IsGenerationId(generationId))
            throw new OpenRouterPremiumProductionException("generation_id_invalid");
        return Path.Combine(_generations, generationId);
    }

    private RootWriterLease AcquireWriterLease()
    {
        VerifyFixedDirectories();
        string path = Path.Combine(_root, WriterLockFileName);
        Observe(path);
        SafeFileHandle? rootHandle = null;
        SafeFileHandle? generationsHandle = null;
        SafeFileHandle? authoritiesHandle = null;
        SafeFileHandle? executionClaimsHandle = null;
        SafeFileHandle? validationClaimsHandle = null;
        try
        {
            FileStream stream = FileOpenRouterPremiumIdentity.OpenFileNoFollow(path, FileMode.Open,
                FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            try
            {
                rootHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_root);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, rootHandle);
                generationsHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_generations);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_generations, _generationsIdentity, generationsHandle);
                authoritiesHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_authorities);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_authorities, _authoritiesIdentity, authoritiesHandle);
                executionClaimsHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_executionClaims);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_executionClaims, _executionClaimsIdentity, executionClaimsHandle);
                validationClaimsHandle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(_validationClaims);
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_validationClaims, _validationClaimsIdentity, validationClaimsHandle);
                byte[] bytes = ReadPinned(stream, MaximumClaimBytes);
                byte[] payload = [];
                try
                {
                    payload = AuthenticatedState.Unwrap(bytes, "root_writer_lock", MaximumClaimBytes,
                        WriterLockBytes.Length, _trustAnchor);
                    if (!payload.AsSpan().SequenceEqual(WriterLockBytes))
                        throw new OpenRouterPremiumProductionException("state_writer_lock_invalid");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                    CryptographicOperations.ZeroMemory(payload);
                }
                FileOpenRouterPremiumIdentity.VerifyStableSingleFile(_root, rootHandle, WriterLockFileName, stream);
                VerifyFixedDirectories();
                RootWriterLease lease = new(stream, rootHandle, generationsHandle, authoritiesHandle,
                    executionClaimsHandle, validationClaimsHandle);
                rootHandle = null; generationsHandle = null; authoritiesHandle = null;
                executionClaimsHandle = null; validationClaimsHandle = null;
                return lease;
            }
            catch { stream.Dispose(); throw; }
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch (IOException) { throw new OpenRouterPremiumProductionException("state_writer_busy"); }
        finally
        {
            rootHandle?.Dispose(); generationsHandle?.Dispose(); authoritiesHandle?.Dispose();
            executionClaimsHandle?.Dispose(); validationClaimsHandle?.Dispose();
        }
    }

    private void InitializeWriterLock()
    {
        string path = Path.Combine(_root, WriterLockFileName);
        Observe(path);
        try
        {
            WriteProtectedNew(_root, _rootIdentity, WriterLockFileName, "root_writer_lock",
                WriterLockBytes, WriterLockBytes.Length, MaximumClaimBytes);
        }
        catch (IOException)
        {
            byte[] existing;
            try { existing = ReadExact(_root, _rootIdentity, WriterLockFileName, MaximumClaimBytes); }
            catch { throw new OpenRouterPremiumProductionException("state_writer_lock_invalid"); }
            byte[] payload = [];
            try
            {
                try
                {
                    payload = AuthenticatedState.Unwrap(existing, "root_writer_lock", MaximumClaimBytes,
                        WriterLockBytes.Length, _trustAnchor);
                }
                catch { throw new OpenRouterPremiumProductionException("state_writer_lock_invalid"); }
                if (!payload.AsSpan().SequenceEqual(WriterLockBytes))
                    throw new OpenRouterPremiumProductionException("state_writer_lock_invalid");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(existing);
                CryptographicOperations.ZeroMemory(payload);
            }
        }
    }

    private void VerifyFixedDirectories()
    {
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_generations, _generationsIdentity);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_authorities, _authoritiesIdentity);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_executionClaims, _executionClaimsIdentity);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_validationClaims, _validationClaimsIdentity);
    }

    private void VerifyOperationDirectories(RootWriterLease lease)
    {
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, lease.RootHandle);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_generations, _generationsIdentity, lease.GenerationsHandle);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_authorities, _authoritiesIdentity, lease.AuthoritiesHandle);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_executionClaims, _executionClaimsIdentity, lease.ExecutionClaimsHandle);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_validationClaims, _validationClaimsIdentity, lease.ValidationClaimsHandle);
        lease.VerifyGeneration();
    }

    private FileOpenRouterPremiumDirectoryIdentity CreateOrVerifyDirectory(
        string path, SafeFileHandle pinnedRoot)
    {
        try
        {
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, pinnedRoot);
            Observe(path);
            if (!Directory.Exists(path))
            {
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, pinnedRoot);
                if (!CreateDirectoryW(path, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    _ = new Win32Exception(error);
                    throw new OpenRouterPremiumProductionException(error is 80 or 183
                        ? "state_directory_creation_race" : "state_directory_creation_failed");
                }
            }
            using SafeFileHandle handle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(path);
            RequireCanonicalDirectoryPath(path);
            FileOpenRouterPremiumDirectoryIdentity identity =
                FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(path);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(path, identity, handle);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity, pinnedRoot);
            return identity;
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException("state_directory_invalid"); }
    }

    private static string ValidateRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)
            || root.StartsWith(@"\\", StringComparison.Ordinal)
            || root.StartsWith(@"\\?\", StringComparison.Ordinal)
            || root.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        string full;
        try { full = Path.GetFullPath(root); }
        catch { throw new OpenRouterPremiumProductionException("state_root_invalid"); }
        if (!string.Equals(full, root, StringComparison.Ordinal)
            || Path.GetPathRoot(full) == full || full.AsSpan(2).Contains(':')
            || full.EndsWith(Path.DirectorySeparatorChar))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        return full;
    }

    private static void RequireCanonicalDirectoryPath(string path)
    {
        string canonical = FileOpenRouterPremiumIdentity.GetCanonicalDirectoryPath(path);
        if (!string.Equals(canonical, path, StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
    }

    private void CreateGenerationDirectoryOnce(string path)
    {
        if (!CreateDirectoryW(path, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            if (error is 80 or 183)
                throw new OpenRouterPremiumProductionException("generation_collision");
            _ = new Win32Exception(error);
            throw new OpenRouterPremiumProductionException("generation_create_failed");
        }
        try { RequireCanonicalDirectoryPath(path); }
        catch { throw new OpenRouterPremiumProductionException("generation_identity_invalid"); }
    }

    private void WriteProtectedNew(string directory, FileOpenRouterPremiumDirectoryIdentity directoryIdentity,
        string fileName, string artifactKind, byte[] payload, int maximumPayload, int maximumEnvelope)
    {
        byte[] envelope = AuthenticatedState.Wrap(artifactKind, payload, maximumPayload, _trustAnchor);
        try { WriteNew(directory, directoryIdentity, fileName, envelope, maximumEnvelope); }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    private void WriteNew(string directory, FileOpenRouterPremiumDirectoryIdentity directoryIdentity,
        string fileName, ReadOnlySpan<byte> bytes, int maximum)
    {
        if (bytes.Length < 1 || bytes.Length > maximum)
            throw new OpenRouterPremiumProductionException("artifact_size_invalid");
        RequireExactLeafName(fileName);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, directoryIdentity);
        string path = Path.Combine(directory, fileName);
        Observe(path);
        using SafeFileHandle directoryHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(directory);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, directoryIdentity, directoryHandle);
        using (FileStream output = FileOpenRouterPremiumIdentity.OpenFileNoFollow(path, FileMode.CreateNew,
                   FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))
        {
            FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, fileName, output);
            output.Write(bytes);
            output.Flush(flushToDisk: true);
            output.Position = 0;
            FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, fileName, output);
        }
        byte[] readback = ReadExact(directory, directoryIdentity, fileName, maximum);
        try
        {
            if (!readback.AsSpan().SequenceEqual(bytes))
                throw new OpenRouterPremiumProductionException("artifact_readback_invalid");
        }
        finally { CryptographicOperations.ZeroMemory(readback); }
    }

    private byte[] ReadExact(string directory, FileOpenRouterPremiumDirectoryIdentity directoryIdentity,
        string fileName, int maximum, FileOpenRouterPremiumFileIdentity? expectedIdentity = null,
        bool allowEmpty = false)
    {
        RequireExactLeafName(fileName);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, directoryIdentity);
        string path = Path.Combine(directory, fileName);
        Observe(path);
        using SafeFileHandle directoryHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(directory);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, directoryIdentity, directoryHandle);
        using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, fileName, input);
        if (input.Length > maximum || (!allowEmpty && input.Length < 1))
            throw new OpenRouterPremiumProductionException("artifact_size_invalid");
        byte[] bytes = new byte[input.Length];
        input.ReadExactly(bytes);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(directory, directoryHandle, fileName, input);
        if (expectedIdentity.HasValue)
            FileOpenRouterPremiumIdentity.VerifySingleFileIdentity(directory, fileName, expectedIdentity.Value);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, directoryIdentity);
        return bytes;
    }

    private byte[] ReadRelativeExact(string generationPath,
        FileOpenRouterPremiumDirectoryIdentity generationIdentity, string relativeName, int maximum,
        FileOpenRouterPremiumFileIdentity expectedIdentity, bool allowEmpty = false)
    {
        string[] parts = relativeName.Split(Path.DirectorySeparatorChar);
        if (parts.Length == 1)
            return ReadExact(generationPath, generationIdentity, parts[0], maximum, expectedIdentity, allowEmpty);
        if (parts.Length != 2 || parts[0] != JournalDirectoryName)
            throw new OpenRouterPremiumProductionException("artifact_path_invalid");
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(generationPath, generationIdentity);
        string journal = Path.Combine(generationPath, JournalDirectoryName);
        FileOpenRouterPremiumDirectoryIdentity journalIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(journal);
        using SafeFileHandle journalHandle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(journal);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(journal, journalIdentity, journalHandle);
        byte[] bytes = ReadExact(journal, journalIdentity, parts[1], maximum, expectedIdentity, allowEmpty);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(generationPath, generationIdentity);
        return bytes;
    }

    private string DigestExactFile(string directory, FileOpenRouterPremiumDirectoryIdentity directoryIdentity,
        string fileName, int maximum, out FileOpenRouterPremiumFileIdentity identity)
    {
        byte[] bytes = ReadExact(directory, directoryIdentity, fileName, maximum);
        try
        {
            identity = FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(directory, fileName);
            return OpenRouterPremiumCanonical.Digest(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private bool ExactFileExists(string directory, FileOpenRouterPremiumDirectoryIdentity identity, string fileName)
    {
        RequireExactLeafName(fileName);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, identity);
        string path = Path.Combine(directory, fileName);
        Observe(path);
        bool exists = File.Exists(path);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(directory, identity);
        return exists;
    }

    private void Observe(string path) => _observer.OnExactPath(path);

    private static byte[] ReadPinned(FileStream stream, int maximumLength)
    {
        if (stream.Length < 1 || stream.Length > maximumLength)
            throw new OpenRouterPremiumProductionException("state_writer_lock_invalid");
        byte[] bytes = new byte[stream.Length];
        stream.Position = 0;
        stream.ReadExactly(bytes);
        stream.Position = 0;
        return bytes;
    }

    private static void RequireExactLeafName(string value)
    {
        if (string.IsNullOrEmpty(value) || value != Path.GetFileName(value)
            || value.Contains(':', StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("artifact_path_invalid");
    }

    private static void RequireDigest(string value, string code)
    {
        if (!IsDigest(value)) throw new OpenRouterPremiumProductionException(code);
    }

    private static void ValidateTrustAnchor(IOpenRouterPremiumStateTrustAnchor trustAnchor)
    {
        byte[] probe = Encoding.ASCII.GetBytes("snow-globe-openrouter-state-trust-anchor-probe/v2");
        try
        {
            string authenticator = trustAnchor.Authenticate(probe);
            if (!IsDigest(trustAnchor.IdentitySha256) || !IsDigest(authenticator)
                || !trustAnchor.Verify(probe, authenticator))
                throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid");
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid"); }
        finally { CryptographicOperations.ZeroMemory(probe); }
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsGenerationId(string? value) => value is { Length: 67 }
        && value.StartsWith("g2-", StringComparison.Ordinal) && IsDigest(value[3..]);

    private static bool FixedDigestEquals(string left, string right)
    {
        if (!IsDigest(left) || !IsDigest(right)) return false;
        byte[] a = Convert.FromHexString(left); byte[] b = Convert.FromHexString(right);
        try { return CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string path, IntPtr securityAttributes);

    private sealed class RootWriterLease(
        FileStream stream,
        SafeFileHandle rootHandle,
        SafeFileHandle generationsHandle,
        SafeFileHandle authoritiesHandle,
        SafeFileHandle executionClaimsHandle,
        SafeFileHandle validationClaimsHandle) : IDisposable
    {
        private FileStream? _stream = stream;
        private SafeFileHandle? _rootHandle = rootHandle;
        private SafeFileHandle? _generationsHandle = generationsHandle;
        private SafeFileHandle? _authoritiesHandle = authoritiesHandle;
        private SafeFileHandle? _executionClaimsHandle = executionClaimsHandle;
        private SafeFileHandle? _validationClaimsHandle = validationClaimsHandle;
        private SafeFileHandle? _generationHandle;
        private string? _generationPath;
        private FileOpenRouterPremiumDirectoryIdentity? _generationIdentity;

        internal SafeFileHandle RootHandle => Require(_rootHandle);
        internal SafeFileHandle GenerationsHandle => Require(_generationsHandle);
        internal SafeFileHandle AuthoritiesHandle => Require(_authoritiesHandle);
        internal SafeFileHandle ExecutionClaimsHandle => Require(_executionClaimsHandle);
        internal SafeFileHandle ValidationClaimsHandle => Require(_validationClaimsHandle);

        internal void PinGeneration(string path, FileOpenRouterPremiumDirectoryIdentity identity)
        {
            if (_stream is null) throw new ObjectDisposedException(nameof(RootWriterLease));
            if (_generationHandle is not null) throw new InvalidOperationException("A generation is already pinned.");
            SafeFileHandle handle = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(path);
            try
            {
                FileOpenRouterPremiumIdentity.VerifyStableDirectory(path, identity, handle);
                _generationHandle = handle;
                _generationPath = path;
                _generationIdentity = identity;
            }
            catch { handle.Dispose(); throw; }
        }

        internal void VerifyGeneration()
        {
            if (_generationHandle is null) return;
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(_generationPath!, _generationIdentity!.Value,
                _generationHandle);
        }

        private static SafeFileHandle Require(SafeFileHandle? handle) =>
            handle ?? throw new ObjectDisposedException(nameof(RootWriterLease));

        public void Dispose()
        {
            FileStream? owned = Interlocked.Exchange(ref _stream, null);
            if (owned is null) return;
            Interlocked.Exchange(ref _generationHandle, null)?.Dispose();
            Interlocked.Exchange(ref _validationClaimsHandle, null)?.Dispose();
            Interlocked.Exchange(ref _executionClaimsHandle, null)?.Dispose();
            Interlocked.Exchange(ref _authoritiesHandle, null)?.Dispose();
            Interlocked.Exchange(ref _generationsHandle, null)?.Dispose();
            Interlocked.Exchange(ref _rootHandle, null)?.Dispose();
            _generationPath = null;
            _generationIdentity = null;
            owned.Dispose();
        }
    }

    private sealed class NoFaults : IOpenRouterPremiumStateFaultInjector
    {
        internal static NoFaults Instance { get; } = new();
        public void AfterDurableBoundary(string boundary) { }
    }

    private sealed class NoIoObserver : IOpenRouterPremiumStateIoObserver
    {
        internal static NoIoObserver Instance { get; } = new();
        public void OnExactPath(string path) { }
        public void OnDirectoryEnumeration() => throw new InvalidOperationException("Directory enumeration is forbidden.");
    }

    internal sealed class ResolvedGeneration : IDisposable
    {
        internal ResolvedGeneration(AuthorityLocator locator, string generationPath,
            FileOpenRouterPremiumDirectoryIdentity generationIdentity, byte[] manifestBytes,
            byte[] preflightArtifactBytes, byte[] runtimeAuthorizationBytes)
        {
            Locator = locator; GenerationPath = generationPath; GenerationIdentity = generationIdentity;
            ManifestBytes = manifestBytes; PreflightArtifactBytes = preflightArtifactBytes;
            RuntimeAuthorizationBytes = runtimeAuthorizationBytes;
        }

        internal AuthorityLocator Locator { get; }
        internal string GenerationPath { get; }
        internal FileOpenRouterPremiumDirectoryIdentity GenerationIdentity { get; }
        internal byte[] ManifestBytes { get; private set; }
        internal byte[] PreflightArtifactBytes { get; private set; }
        internal byte[] RuntimeAuthorizationBytes { get; private set; }

        internal OpenRouterPremiumResolvedGeneration Detached() => new(Locator.GenerationId,
            Locator.AuthorizationDigestSha256, Locator.GenerationManifestDigestSha256,
            ManifestBytes, PreflightArtifactBytes, RuntimeAuthorizationBytes);

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(ManifestBytes);
            CryptographicOperations.ZeroMemory(PreflightArtifactBytes);
            CryptographicOperations.ZeroMemory(RuntimeAuthorizationBytes);
            ManifestBytes = []; PreflightArtifactBytes = []; RuntimeAuthorizationBytes = [];
        }
    }

    internal sealed record GenerationManifest(string SchemaVersion, string GenerationId,
        string StateContractDigestSha256, string RootPathDigestSha256, string ProfileDigestSha256,
        string ExternalTrustAnchorIdentitySha256, bool RequiresSameExternalTrustAnchorForRestart,
        bool CooperativeCreateNewAppendOnly, bool SameUserOrAdminTamperProtectedWithoutExternalAnchor,
        long CreatedAtUnixMilliseconds,
        bool AdditionalAttemptAuthorized, bool WholeVolumeRollbackProtected)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteString("profile_digest_sha256", ProfileDigestSha256);
            writer.WriteString("external_trust_anchor_identity_sha256", ExternalTrustAnchorIdentitySha256);
            writer.WriteBoolean("requires_same_external_trust_anchor_for_restart", RequiresSameExternalTrustAnchorForRestart);
            writer.WriteBoolean("cooperative_create_new_append_only", CooperativeCreateNewAppendOnly);
            writer.WriteBoolean("same_user_or_admin_tamper_protected_without_external_anchor", SameUserOrAdminTamperProtectedWithoutExternalAnchor);
            writer.WriteNumber("created_at_unix_milliseconds", CreatedAtUnixMilliseconds);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
            writer.WriteBoolean("whole_volume_rollback_protected", WholeVolumeRollbackProtected);
        });

        internal static GenerationManifest Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumManifestPayloadBytes,
            4, ["schema_version", "generation_id", "state_contract_digest_sha256", "root_path_digest_sha256",
                "profile_digest_sha256", "external_trust_anchor_identity_sha256",
                "requires_same_external_trust_anchor_for_restart", "cooperative_create_new_append_only",
                "same_user_or_admin_tamper_protected_without_external_anchor", "created_at_unix_milliseconds",
                "additional_attempt_authorized", "whole_volume_rollback_protected"], root =>
            {
                GenerationManifest value = new(StateJson.String(root, "schema_version"),
                    StateJson.String(root, "generation_id"), StateJson.Digest(root, "state_contract_digest_sha256"),
                    StateJson.Digest(root, "root_path_digest_sha256"), StateJson.Digest(root, "profile_digest_sha256"),
                    StateJson.Digest(root, "external_trust_anchor_identity_sha256"),
                    StateJson.Boolean(root, "requires_same_external_trust_anchor_for_restart"),
                    StateJson.Boolean(root, "cooperative_create_new_append_only"),
                    StateJson.Boolean(root, "same_user_or_admin_tamper_protected_without_external_anchor"),
                    StateJson.Int64(root, "created_at_unix_milliseconds"),
                    StateJson.Boolean(root, "additional_attempt_authorized"), StateJson.Boolean(root, "whole_volume_rollback_protected"));
                if (value.SchemaVersion != GenerationManifestSchema || !IsGenerationId(value.GenerationId)
                    || value.CreatedAtUnixMilliseconds < 0 || value.AdditionalAttemptAuthorized
                    || !value.RequiresSameExternalTrustAnchorForRestart || !value.CooperativeCreateNewAppendOnly
                    || value.SameUserOrAdminTamperProtectedWithoutExternalAnchor
                    || value.WholeVolumeRollbackProtected)
                    throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record PreflightStarted(string SchemaVersion, string GenerationId,
        string GenerationManifestDigestSha256, string StateContractDigestSha256,
        string RootPathDigestSha256, long StartedAtUnixMilliseconds,
        bool MetadataRequestsStarted, bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteNumber("started_at_unix_milliseconds", StartedAtUnixMilliseconds);
            writer.WriteBoolean("metadata_requests_started", MetadataRequestsStarted);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static PreflightStarted Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumClaimPayloadBytes, 4,
            ["schema_version", "generation_id", "generation_manifest_digest_sha256", "state_contract_digest_sha256",
             "root_path_digest_sha256", "started_at_unix_milliseconds", "metadata_requests_started",
             "additional_attempt_authorized"], root =>
            {
                PreflightStarted value = new(StateJson.String(root, "schema_version"),
                    StateJson.String(root, "generation_id"), StateJson.Digest(root, "generation_manifest_digest_sha256"),
                    StateJson.Digest(root, "state_contract_digest_sha256"), StateJson.Digest(root, "root_path_digest_sha256"),
                    StateJson.Int64(root, "started_at_unix_milliseconds"), StateJson.Boolean(root, "metadata_requests_started"),
                    StateJson.Boolean(root, "additional_attempt_authorized"));
                if (value.SchemaVersion != PreflightStartedSchema || !IsGenerationId(value.GenerationId)
                    || value.StartedAtUnixMilliseconds < 0 || value.MetadataRequestsStarted
                    || value.AdditionalAttemptAuthorized) throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record RootClaim(string SchemaVersion, string AuthorizationDigestSha256,
        string GenerationId, string GenerationManifestDigestSha256,
        string StateContractDigestSha256, string RootPathDigestSha256,
        string? EvidenceDigestSha256, long ClaimedAtUnixMilliseconds,
        bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("authorization_digest_sha256", AuthorizationDigestSha256);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            if (EvidenceDigestSha256 is null) writer.WriteNull("evidence_digest_sha256");
            else writer.WriteString("evidence_digest_sha256", EvidenceDigestSha256);
            writer.WriteNumber("claimed_at_unix_milliseconds", ClaimedAtUnixMilliseconds);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static RootClaim Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumClaimPayloadBytes, 4,
            ["schema_version", "authorization_digest_sha256", "generation_id", "generation_manifest_digest_sha256",
             "state_contract_digest_sha256", "root_path_digest_sha256", "evidence_digest_sha256",
             "claimed_at_unix_milliseconds", "additional_attempt_authorized"], root =>
            {
                string schema = StateJson.String(root, "schema_version");
                JsonElement evidence = root.GetProperty("evidence_digest_sha256");
                string? evidenceDigest = evidence.ValueKind == JsonValueKind.Null ? null : StateJson.Digest(root, "evidence_digest_sha256");
                RootClaim value = new(schema, StateJson.Digest(root, "authorization_digest_sha256"),
                    StateJson.String(root, "generation_id"), StateJson.Digest(root, "generation_manifest_digest_sha256"),
                    StateJson.Digest(root, "state_contract_digest_sha256"), StateJson.Digest(root, "root_path_digest_sha256"),
                    evidenceDigest, StateJson.Int64(root, "claimed_at_unix_milliseconds"),
                    StateJson.Boolean(root, "additional_attempt_authorized"));
                if (schema is not (ExecutionClaimSchema or ValidationClaimSchema) || !IsGenerationId(value.GenerationId)
                    || value.ClaimedAtUnixMilliseconds < 0 || value.AdditionalAttemptAuthorized
                    || (schema == ExecutionClaimSchema && evidenceDigest is not null)
                    || (schema == ValidationClaimSchema && evidenceDigest is null))
                    throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record EvidenceBinding(string SchemaVersion, string AuthorizationDigestSha256,
        string GenerationId, string GenerationManifestDigestSha256, string StateContractDigestSha256,
        string RootPathDigestSha256, string EvidenceDigestSha256, string EvidenceFileIdentity,
        string FinalJournalDirectoryIdentity, string FinalJournalRecordsDigestSha256,
        string FinalJournalRecordsFileIdentity, int FinalJournalRecordCount,
        string FinalJournalRecordChecksumSha256, string FinalJournalSnapshotDigestSha256,
        string FinalJournalRestartEvidenceDigestSha256,
        long PublishedAtUnixMilliseconds, bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("authorization_digest_sha256", AuthorizationDigestSha256);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteString("evidence_digest_sha256", EvidenceDigestSha256);
            writer.WriteString("evidence_file_identity", EvidenceFileIdentity);
            writer.WriteString("final_journal_directory_identity", FinalJournalDirectoryIdentity);
            writer.WriteString("final_journal_records_digest_sha256", FinalJournalRecordsDigestSha256);
            writer.WriteString("final_journal_records_file_identity", FinalJournalRecordsFileIdentity);
            writer.WriteNumber("final_journal_record_count", FinalJournalRecordCount);
            writer.WriteString("final_journal_record_checksum_sha256", FinalJournalRecordChecksumSha256);
            writer.WriteString("final_journal_snapshot_digest_sha256", FinalJournalSnapshotDigestSha256);
            writer.WriteString("final_journal_restart_evidence_digest_sha256", FinalJournalRestartEvidenceDigestSha256);
            writer.WriteNumber("published_at_unix_milliseconds", PublishedAtUnixMilliseconds);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static EvidenceBinding Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumClaimPayloadBytes, 4,
            ["schema_version", "authorization_digest_sha256", "generation_id", "generation_manifest_digest_sha256",
             "state_contract_digest_sha256", "root_path_digest_sha256", "evidence_digest_sha256",
             "evidence_file_identity", "final_journal_directory_identity", "final_journal_records_digest_sha256",
             "final_journal_records_file_identity", "final_journal_record_count",
             "final_journal_record_checksum_sha256", "final_journal_snapshot_digest_sha256",
             "final_journal_restart_evidence_digest_sha256", "published_at_unix_milliseconds",
             "additional_attempt_authorized"], root =>
            {
                EvidenceBinding value = new(StateJson.String(root, "schema_version"),
                    StateJson.Digest(root, "authorization_digest_sha256"), StateJson.String(root, "generation_id"),
                    StateJson.Digest(root, "generation_manifest_digest_sha256"), StateJson.Digest(root, "state_contract_digest_sha256"),
                    StateJson.Digest(root, "root_path_digest_sha256"), StateJson.Digest(root, "evidence_digest_sha256"),
                    StateJson.String(root, "evidence_file_identity"),
                    StateJson.String(root, "final_journal_directory_identity"),
                    StateJson.Digest(root, "final_journal_records_digest_sha256"),
                    StateJson.String(root, "final_journal_records_file_identity"),
                    StateJson.Int32(root, "final_journal_record_count"),
                    StateJson.Digest(root, "final_journal_record_checksum_sha256"),
                    StateJson.Digest(root, "final_journal_snapshot_digest_sha256"),
                    StateJson.Digest(root, "final_journal_restart_evidence_digest_sha256"),
                    StateJson.Int64(root, "published_at_unix_milliseconds"),
                    StateJson.Boolean(root, "additional_attempt_authorized"));
                _ = FileOpenRouterPremiumFileIdentity.Parse(value.EvidenceFileIdentity);
                _ = FileOpenRouterPremiumDirectoryIdentity.Parse(value.FinalJournalDirectoryIdentity);
                _ = FileOpenRouterPremiumFileIdentity.Parse(value.FinalJournalRecordsFileIdentity);
                if (value.SchemaVersion != EvidenceBindingSchema || !IsGenerationId(value.GenerationId)
                    || value.FinalJournalRecordCount is < 1 or > 48
                    || value.PublishedAtUnixMilliseconds < 0 || value.AdditionalAttemptAuthorized) throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record ReceiptBinding(string SchemaVersion, string AuthorizationDigestSha256,
        string GenerationId, string GenerationManifestDigestSha256, string StateContractDigestSha256,
        string RootPathDigestSha256, string EvidenceDigestSha256, string ReceiptDigestSha256,
        string ReceiptFileIdentity, long PublishedAtUnixMilliseconds, bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("authorization_digest_sha256", AuthorizationDigestSha256);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteString("evidence_digest_sha256", EvidenceDigestSha256);
            writer.WriteString("receipt_digest_sha256", ReceiptDigestSha256);
            writer.WriteString("receipt_file_identity", ReceiptFileIdentity);
            writer.WriteNumber("published_at_unix_milliseconds", PublishedAtUnixMilliseconds);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static ReceiptBinding Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumClaimPayloadBytes, 4,
            ["schema_version", "authorization_digest_sha256", "generation_id", "generation_manifest_digest_sha256",
             "state_contract_digest_sha256", "root_path_digest_sha256", "evidence_digest_sha256",
             "receipt_digest_sha256", "receipt_file_identity", "published_at_unix_milliseconds", "additional_attempt_authorized"], root =>
            {
                ReceiptBinding value = new(StateJson.String(root, "schema_version"),
                    StateJson.Digest(root, "authorization_digest_sha256"), StateJson.String(root, "generation_id"),
                    StateJson.Digest(root, "generation_manifest_digest_sha256"), StateJson.Digest(root, "state_contract_digest_sha256"),
                    StateJson.Digest(root, "root_path_digest_sha256"), StateJson.Digest(root, "evidence_digest_sha256"),
                    StateJson.Digest(root, "receipt_digest_sha256"), StateJson.String(root, "receipt_file_identity"),
                    StateJson.Int64(root, "published_at_unix_milliseconds"), StateJson.Boolean(root, "additional_attempt_authorized"));
                _ = FileOpenRouterPremiumFileIdentity.Parse(value.ReceiptFileIdentity);
                if (value.SchemaVersion != ReceiptBindingSchema || !IsGenerationId(value.GenerationId)
                    || value.PublishedAtUnixMilliseconds < 0 || value.AdditionalAttemptAuthorized) throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record ValidationReceipt(string SchemaVersion, string AuthorizationDigestSha256,
        string GenerationId, string GenerationManifestDigestSha256, string StateContractDigestSha256,
        string RootPathDigestSha256, string EvidenceArtifactDigestSha256, string Status,
        int ExchangeCount, long TotalSettledMicrousd, long ValidatedAtUnixMilliseconds,
        bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("authorization_digest_sha256", AuthorizationDigestSha256);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteString("evidence_artifact_digest_sha256", EvidenceArtifactDigestSha256);
            writer.WriteString("status", Status);
            writer.WriteNumber("exchange_count", ExchangeCount);
            writer.WriteNumber("total_settled_microusd", TotalSettledMicrousd);
            writer.WriteNumber("validated_at_unix_milliseconds", ValidatedAtUnixMilliseconds);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static ValidationReceipt Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumClaimPayloadBytes, 4,
            ["schema_version", "authorization_digest_sha256", "generation_id", "generation_manifest_digest_sha256",
             "state_contract_digest_sha256", "root_path_digest_sha256", "evidence_artifact_digest_sha256", "status",
             "exchange_count", "total_settled_microusd", "validated_at_unix_milliseconds", "additional_attempt_authorized"], root =>
            {
                ValidationReceipt value = new(StateJson.String(root, "schema_version"),
                    StateJson.Digest(root, "authorization_digest_sha256"), StateJson.String(root, "generation_id"),
                    StateJson.Digest(root, "generation_manifest_digest_sha256"), StateJson.Digest(root, "state_contract_digest_sha256"),
                    StateJson.Digest(root, "root_path_digest_sha256"), StateJson.Digest(root, "evidence_artifact_digest_sha256"),
                    StateJson.String(root, "status"), StateJson.Int32(root, "exchange_count"),
                    StateJson.Int64(root, "total_settled_microusd"), StateJson.Int64(root, "validated_at_unix_milliseconds"),
                    StateJson.Boolean(root, "additional_attempt_authorized"));
                if (value.SchemaVersion != ValidationReceiptSchema || !IsGenerationId(value.GenerationId)
                    || value.Status is not ("complete" or "terminal") || value.ExchangeCount is < 0 or > 12
                    || value.TotalSettledMicrousd is < 0 or > 18_000 || value.ValidatedAtUnixMilliseconds < 0
                    || value.AdditionalAttemptAuthorized) throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    internal sealed record AuthorityLocator(string SchemaVersion, string AuthorizationDigestSha256,
        string GenerationId, string GenerationDirectoryIdentity, string GenerationManifestDigestSha256,
        string GenerationManifestFileIdentity, string PreflightStartedDigestSha256,
        string PreflightStartedFileIdentity, string StateContractDigestSha256, string RootPathDigestSha256,
        string ActivationBundleDigestSha256, string ActivationBundleFileIdentity,
        string JournalHeaderDigestSha256, string JournalHeaderFileIdentity,
        string JournalRecordsDigestSha256, string JournalRecordsFileIdentity,
        string JournalWriterLeaseDigestSha256, string JournalWriterLeaseFileIdentity,
        string PreflightArtifactDigestSha256, string PreflightArtifactFileIdentity,
        string RuntimeAuthorizationDigestSha256, string RuntimeAuthorizationFileIdentity,
        bool AdditionalAttemptAuthorized)
    {
        internal byte[] Write() => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", SchemaVersion);
            writer.WriteString("authorization_digest_sha256", AuthorizationDigestSha256);
            writer.WriteString("generation_id", GenerationId);
            writer.WriteString("generation_directory_identity", GenerationDirectoryIdentity);
            writer.WriteString("generation_manifest_digest_sha256", GenerationManifestDigestSha256);
            writer.WriteString("generation_manifest_file_identity", GenerationManifestFileIdentity);
            writer.WriteString("preflight_started_digest_sha256", PreflightStartedDigestSha256);
            writer.WriteString("preflight_started_file_identity", PreflightStartedFileIdentity);
            writer.WriteString("state_contract_digest_sha256", StateContractDigestSha256);
            writer.WriteString("root_path_digest_sha256", RootPathDigestSha256);
            writer.WriteString("activation_bundle_digest_sha256", ActivationBundleDigestSha256);
            writer.WriteString("activation_bundle_file_identity", ActivationBundleFileIdentity);
            writer.WriteString("journal_header_digest_sha256", JournalHeaderDigestSha256);
            writer.WriteString("journal_header_file_identity", JournalHeaderFileIdentity);
            writer.WriteString("journal_records_digest_sha256", JournalRecordsDigestSha256);
            writer.WriteString("journal_records_file_identity", JournalRecordsFileIdentity);
            writer.WriteString("journal_writer_lease_digest_sha256", JournalWriterLeaseDigestSha256);
            writer.WriteString("journal_writer_lease_file_identity", JournalWriterLeaseFileIdentity);
            writer.WriteString("preflight_artifact_digest_sha256", PreflightArtifactDigestSha256);
            writer.WriteString("preflight_artifact_file_identity", PreflightArtifactFileIdentity);
            writer.WriteString("runtime_authorization_digest_sha256", RuntimeAuthorizationDigestSha256);
            writer.WriteString("runtime_authorization_file_identity", RuntimeAuthorizationFileIdentity);
            writer.WriteBoolean("additional_attempt_authorized", AdditionalAttemptAuthorized);
        });

        internal static AuthorityLocator Parse(byte[] bytes) => StateJson.ParseCanonical(bytes, MaximumLocatorPayloadBytes, 4,
            ["schema_version", "authorization_digest_sha256", "generation_id", "generation_directory_identity",
             "generation_manifest_digest_sha256", "generation_manifest_file_identity", "preflight_started_digest_sha256",
             "preflight_started_file_identity", "state_contract_digest_sha256",
             "root_path_digest_sha256", "activation_bundle_digest_sha256", "activation_bundle_file_identity",
             "journal_header_digest_sha256", "journal_header_file_identity", "journal_records_digest_sha256",
             "journal_records_file_identity", "journal_writer_lease_digest_sha256", "journal_writer_lease_file_identity",
             "preflight_artifact_digest_sha256", "preflight_artifact_file_identity",
             "runtime_authorization_digest_sha256", "runtime_authorization_file_identity", "additional_attempt_authorized"], root =>
            {
                AuthorityLocator value = new(StateJson.String(root, "schema_version"), StateJson.Digest(root, "authorization_digest_sha256"),
                    StateJson.String(root, "generation_id"), StateJson.String(root, "generation_directory_identity"),
                    StateJson.Digest(root, "generation_manifest_digest_sha256"), StateJson.String(root, "generation_manifest_file_identity"),
                    StateJson.Digest(root, "preflight_started_digest_sha256"), StateJson.String(root, "preflight_started_file_identity"),
                    StateJson.Digest(root, "state_contract_digest_sha256"), StateJson.Digest(root, "root_path_digest_sha256"),
                    StateJson.Digest(root, "activation_bundle_digest_sha256"), StateJson.String(root, "activation_bundle_file_identity"),
                    StateJson.Digest(root, "journal_header_digest_sha256"), StateJson.String(root, "journal_header_file_identity"),
                    StateJson.Digest(root, "journal_records_digest_sha256"), StateJson.String(root, "journal_records_file_identity"),
                    StateJson.Digest(root, "journal_writer_lease_digest_sha256"), StateJson.String(root, "journal_writer_lease_file_identity"),
                    StateJson.Digest(root, "preflight_artifact_digest_sha256"), StateJson.String(root, "preflight_artifact_file_identity"),
                    StateJson.Digest(root, "runtime_authorization_digest_sha256"), StateJson.String(root, "runtime_authorization_file_identity"),
                    StateJson.Boolean(root, "additional_attempt_authorized"));
                _ = FileOpenRouterPremiumDirectoryIdentity.Parse(value.GenerationDirectoryIdentity);
                foreach (string identity in new[] { value.GenerationManifestFileIdentity, value.PreflightStartedFileIdentity,
                    value.ActivationBundleFileIdentity,
                    value.JournalHeaderFileIdentity, value.JournalRecordsFileIdentity, value.JournalWriterLeaseFileIdentity,
                    value.PreflightArtifactFileIdentity, value.RuntimeAuthorizationFileIdentity })
                    _ = FileOpenRouterPremiumFileIdentity.Parse(identity);
                if (value.SchemaVersion != AuthorityLocatorSchema || !IsGenerationId(value.GenerationId)
                    || value.AdditionalAttemptAuthorized) throw new InvalidDataException();
                return value;
            }, value => value.Write());
    }

    private static class StateJson
    {
        internal static byte[] Write(Action<Utf8JsonWriter> properties)
        {
            ArrayBufferWriter<byte> buffer = new();
            using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
            writer.WriteStartObject(); properties(writer); writer.WriteEndObject(); writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        internal static T ParseCanonical<T>(byte[] bytes, int maximum, int maximumDepth,
            string[] exactProperties, Func<JsonElement, T> parse, Func<T, byte[]> write)
        {
            if (bytes.Length < 1 || bytes.Length > maximum) throw new InvalidDataException();
            try
            {
                _ = new UTF8Encoding(false, true).GetString(bytes);
                using JsonDocument document = JsonDocument.Parse(bytes,
                    new JsonDocumentOptions { MaxDepth = maximumDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
                string[] actual = root.EnumerateObject().Select(property => property.Name).ToArray();
                if (!actual.SequenceEqual(exactProperties, StringComparer.Ordinal)
                    || actual.Distinct(StringComparer.Ordinal).Count() != exactProperties.Length)
                    throw new InvalidDataException();
                T value = parse(root);
                byte[] canonical = write(value);
                try { if (!canonical.AsSpan().SequenceEqual(bytes)) throw new InvalidDataException(); }
                finally { CryptographicOperations.ZeroMemory(canonical); }
                return value;
            }
            catch (InvalidDataException) { throw; }
            catch { throw new InvalidDataException(); }
        }

        internal static string String(JsonElement root, string name, int maximumLength = 256)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException();
            string? text = value.GetString();
            if (string.IsNullOrEmpty(text) || text.Length > maximumLength || text.Any(char.IsControl)) throw new InvalidDataException();
            return text;
        }

        internal static string Digest(JsonElement root, string name)
        {
            string value = String(root, name);
            if (!IsDigest(value)) throw new InvalidDataException();
            return value;
        }

        internal static long Int64(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result)) throw new InvalidDataException();
            return result;
        }

        internal static int Int32(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result)) throw new InvalidDataException();
            return result;
        }

        internal static bool Boolean(JsonElement root, string name)
        {
            JsonElement value = root.GetProperty(name);
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException();
            return value.GetBoolean();
        }
    }

    private static class AuthenticatedState
    {
        private const int MaximumEnvelopeDepth = 4;

        internal static byte[] Wrap(string artifactKind, byte[] payload,
            int maximumPayloadBytes, IOpenRouterPremiumStateTrustAnchor trustAnchor)
        {
            ValidateArtifactKind(artifactKind);
            if (payload.Length < 1 || payload.Length > maximumPayloadBytes) throw new InvalidDataException();
            string payloadBase64 = Convert.ToBase64String(payload);
            string payloadDigest = OpenRouterPremiumCanonical.Digest(payload);
            byte[] authenticated = WriteAuthenticatedFields(artifactKind, trustAnchor.IdentitySha256,
                payloadDigest, payloadBase64);
            string authenticator;
            try { authenticator = trustAnchor.Authenticate(authenticated); }
            finally { CryptographicOperations.ZeroMemory(authenticated); }
            return StateJson.Write(writer =>
            {
                writer.WriteString("schema_version", AuthenticatedStateEnvelopeSchema);
                writer.WriteString("artifact_kind", artifactKind);
                writer.WriteString("trust_anchor_identity_sha256", trustAnchor.IdentitySha256);
                writer.WriteString("payload_digest_sha256", payloadDigest);
                writer.WriteString("payload_base64", payloadBase64);
                writer.WriteString("authenticator_sha256", authenticator);
            });
        }

        internal static byte[] Unwrap(byte[] envelope, string expectedArtifactKind,
            int maximumEnvelopeBytes, int maximumPayloadBytes,
            IOpenRouterPremiumStateTrustAnchor trustAnchor)
        {
            AuthenticatedEnvelope parsed = StateJson.ParseCanonical(envelope, maximumEnvelopeBytes,
                MaximumEnvelopeDepth,
                ["schema_version", "artifact_kind", "trust_anchor_identity_sha256",
                 "payload_digest_sha256", "payload_base64", "authenticator_sha256"], root =>
                {
                    AuthenticatedEnvelope value = new(StateJson.String(root, "schema_version"),
                        StateJson.String(root, "artifact_kind"), StateJson.Digest(root, "trust_anchor_identity_sha256"),
                        StateJson.Digest(root, "payload_digest_sha256"),
                        StateJson.String(root, "payload_base64", maximumPayloadBytes * 2),
                        StateJson.Digest(root, "authenticator_sha256"));
                    ValidateArtifactKind(value.ArtifactKind);
                    if (value.SchemaVersion != AuthenticatedStateEnvelopeSchema) throw new InvalidDataException();
                    return value;
                }, value => value.Write());
            if (parsed.ArtifactKind != expectedArtifactKind
                || !FixedDigestEquals(parsed.TrustAnchorIdentitySha256, trustAnchor.IdentitySha256))
                throw new InvalidDataException();
            byte[] authenticated = WriteAuthenticatedFields(parsed.ArtifactKind,
                parsed.TrustAnchorIdentitySha256, parsed.PayloadDigestSha256, parsed.PayloadBase64);
            try
            {
                if (!trustAnchor.Verify(authenticated, parsed.AuthenticatorSha256)) throw new InvalidDataException();
            }
            finally { CryptographicOperations.ZeroMemory(authenticated); }
            byte[] payload;
            try { payload = Convert.FromBase64String(parsed.PayloadBase64); }
            catch { throw new InvalidDataException(); }
            if (payload.Length < 1 || payload.Length > maximumPayloadBytes
                || !FixedDigestEquals(OpenRouterPremiumCanonical.Digest(payload), parsed.PayloadDigestSha256))
            {
                CryptographicOperations.ZeroMemory(payload);
                throw new InvalidDataException();
            }
            return payload;
        }

        private static byte[] WriteAuthenticatedFields(string artifactKind, string trustAnchorIdentitySha256,
            string payloadDigestSha256, string payloadBase64) => StateJson.Write(writer =>
        {
            writer.WriteString("schema_version", AuthenticatedStateEnvelopeSchema);
            writer.WriteString("artifact_kind", artifactKind);
            writer.WriteString("trust_anchor_identity_sha256", trustAnchorIdentitySha256);
            writer.WriteString("payload_digest_sha256", payloadDigestSha256);
            writer.WriteString("payload_base64", payloadBase64);
        });

        private static void ValidateArtifactKind(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 80
                || value.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
                throw new InvalidDataException();
        }

        private sealed record AuthenticatedEnvelope(string SchemaVersion, string ArtifactKind,
            string TrustAnchorIdentitySha256, string PayloadDigestSha256,
            string PayloadBase64, string AuthenticatorSha256)
        {
            internal byte[] Write() => StateJson.Write(writer =>
            {
                writer.WriteString("schema_version", SchemaVersion);
                writer.WriteString("artifact_kind", ArtifactKind);
                writer.WriteString("trust_anchor_identity_sha256", TrustAnchorIdentitySha256);
                writer.WriteString("payload_digest_sha256", PayloadDigestSha256);
                writer.WriteString("payload_base64", PayloadBase64);
                writer.WriteString("authenticator_sha256", AuthenticatorSha256);
            });
        }
    }

    private void WriteGenerationArtifact(GenerationWriteContext context, string name,
        byte[] bytes, int maximum, string boundary)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        VerifyOperationDirectories(context.Lease);
        WriteNew(context.GenerationPath, context.GenerationIdentity, name, bytes, maximum);
        VerifyOperationDirectories(context.Lease);
        _faults.AfterDurableBoundary(boundary);
    }

    private FileOpenRouterPremiumJournal CreateGenerationJournal(
        GenerationWriteContext context,
        OpenRouterPremiumJournalHeader header)
    {
        VerifyOperationDirectories(context.Lease);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(context.GenerationPath, context.GenerationIdentity);
        FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNewForPublication(
            Path.Combine(context.GenerationPath, JournalDirectoryName), header);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(context.GenerationPath, context.GenerationIdentity);
        try { _faults.AfterDurableBoundary(BoundaryJournal); }
        catch { journal.AbortPublication(); throw; }
        VerifyOperationDirectories(context.Lease);
        return journal;
    }

    private string PublishGenerationAuthorization(GenerationWriteContext context, byte[] authorizationBytes)
    {
        ArgumentNullException.ThrowIfNull(authorizationBytes);
        VerifyOperationDirectories(context.Lease);
        WriteNew(context.GenerationPath, context.GenerationIdentity, RuntimeAuthorizationFileName,
            authorizationBytes, 64 * 1024);
        _faults.AfterDurableBoundary(BoundaryRuntimeAuthorization);
        AuthorityLocator locator = PublishLocator(context, authorizationBytes);
        VerifyOperationDirectories(context.Lease);
        return locator.AuthorizationDigestSha256;
    }

    private AuthorityLocator PublishLocator(GenerationWriteContext context, byte[] authorizationBytes)
    {
        VerifyOperationDirectories(context.Lease);
        string generationPath = context.GenerationPath;
        FileOpenRouterPremiumDirectoryIdentity generationIdentity = context.GenerationIdentity;
        ArtifactIdentity activation = Artifact(generationPath, generationIdentity, ActivationBundleFileName, 16 * 1024);
        ArtifactIdentity preflight = Artifact(generationPath, generationIdentity, PreflightArtifactFileName,
            OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes);
        ArtifactIdentity runtime = Artifact(generationPath, generationIdentity, RuntimeAuthorizationFileName, 64 * 1024);
        string journal = Path.Combine(generationPath, JournalDirectoryName);
        FileOpenRouterPremiumDirectoryIdentity journalIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(journal);
        ArtifactIdentity header = Artifact(journal, journalIdentity, FileOpenRouterPremiumJournal.HeaderFileName, 4 * 1024);
        ArtifactIdentity records = Artifact(journal, journalIdentity, FileOpenRouterPremiumJournal.RecordsFileName, 256 * 1024, allowEmpty: true);
        ArtifactIdentity lease = Artifact(journal, journalIdentity, FileOpenRouterPremiumJournal.WriterLeaseFileName, 256, allowEmpty: true);
        string authority = OpenRouterPremiumCanonical.Digest(authorizationBytes);
        if (authority != runtime.DigestSha256) throw new OpenRouterPremiumProductionException("authorization_digest_invalid");
        AuthorityLocator locator = new(AuthorityLocatorSchema, authority, context.GenerationId,
            generationIdentity.CanonicalIdentity, context.ManifestDigestSha256, context.ManifestFileIdentity.CanonicalIdentity,
            context.PreflightStartedDigestSha256, context.PreflightStartedFileIdentity.CanonicalIdentity,
            StateContractDigestSha256, RootPathDigestSha256,
            activation.DigestSha256, activation.FileIdentity.CanonicalIdentity,
            header.DigestSha256, header.FileIdentity.CanonicalIdentity,
            records.DigestSha256, records.FileIdentity.CanonicalIdentity,
            lease.DigestSha256, lease.FileIdentity.CanonicalIdentity,
            preflight.DigestSha256, preflight.FileIdentity.CanonicalIdentity,
            runtime.DigestSha256, runtime.FileIdentity.CanonicalIdentity, false);
        byte[] locatorBytes = locator.Write();
        try
        {
            try { WriteProtectedNew(_authorities, _authoritiesIdentity, authority + ".json",
                "authority_locator", locatorBytes, MaximumLocatorPayloadBytes, MaximumLocatorBytes); }
            catch (IOException) { throw new OpenRouterPremiumProductionException("authority_locator_collision"); }
        }
        finally { CryptographicOperations.ZeroMemory(locatorBytes); }
        _faults.AfterDurableBoundary(BoundaryAuthorityLocator);
        return locator;
    }

    private ArtifactIdentity Artifact(string directory, FileOpenRouterPremiumDirectoryIdentity directoryIdentity,
        string name, int maximum, bool allowEmpty = false)
    {
        byte[] bytes = ReadExact(directory, directoryIdentity, name, maximum, allowEmpty: allowEmpty);
        try
        {
            return new(OpenRouterPremiumCanonical.Digest(bytes),
                FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(directory, name));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed record ArtifactIdentity(string DigestSha256, FileOpenRouterPremiumFileIdentity FileIdentity);

    private ClaimedExecutionJournalGrant CreateClaimedExecutionJournalGrant(ResolvedGeneration resolved,
        bool finalReadbackOnly = false)
    {
        string journalDirectory = Path.Combine(resolved.GenerationPath, JournalDirectoryName);
        FileOpenRouterPremiumDirectoryIdentity journalDirectoryIdentity =
            FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(journalDirectory);
        return new ClaimedExecutionJournalGrant(journalDirectory, journalDirectoryIdentity,
            resolved.Locator.JournalHeaderDigestSha256,
            FileOpenRouterPremiumFileIdentity.Parse(resolved.Locator.JournalHeaderFileIdentity),
            resolved.Locator.JournalRecordsDigestSha256,
            FileOpenRouterPremiumFileIdentity.Parse(resolved.Locator.JournalRecordsFileIdentity),
            resolved.Locator.JournalWriterLeaseDigestSha256,
            FileOpenRouterPremiumFileIdentity.Parse(resolved.Locator.JournalWriterLeaseFileIdentity),
            finalReadbackOnly);
    }

    private void PublishEvidence(GenerationMutationContext context, byte[] evidence,
        FinalJournalBinding finalJournal)
    {
        VerifyOperationDirectories(context.Lease);
        OpenRouterPremiumEvidenceArtifact validated = OpenRouterPremiumEvidenceArtifactModule.Validate(evidence);
        if (!validated.CanonicalUtf8.Span.SequenceEqual(evidence))
            throw new OpenRouterPremiumProductionException("evidence_artifact_invalid");
        RequireEvidenceBoundToGeneration(context, validated, evidence, finalJournal);
        WriteNew(context.GenerationPath, context.GenerationIdentity, EvidenceFileName, evidence,
            OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes);
        _faults.AfterDurableBoundary(BoundaryEvidence);
        FileOpenRouterPremiumFileIdentity identity = FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(
            context.GenerationPath, EvidenceFileName);
        EvidenceBinding binding = new(EvidenceBindingSchema, context.Locator.AuthorizationDigestSha256,
            context.Locator.GenerationId, context.Locator.GenerationManifestDigestSha256,
            StateContractDigestSha256, RootPathDigestSha256, OpenRouterPremiumCanonical.Digest(evidence),
            identity.CanonicalIdentity, finalJournal.JournalDirectoryIdentity.CanonicalIdentity,
            finalJournal.RecordsDigestSha256, finalJournal.RecordsFileIdentity.CanonicalIdentity,
            finalJournal.RecordCount, finalJournal.FinalRecordChecksumSha256,
            finalJournal.SnapshotDigestSha256, finalJournal.RestartEvidenceDigestSha256,
            context.ClaimedAtUnixMilliseconds, false);
        byte[] bytes = binding.Write();
        try { WriteProtectedNew(context.GenerationPath, context.GenerationIdentity, EvidenceBindingFileName,
            "evidence_binding", bytes, MaximumClaimPayloadBytes, MaximumClaimBytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        _faults.AfterDurableBoundary(BoundaryEvidenceBinding);
        VerifyOperationDirectories(context.Lease);
    }

    private void RequireEvidenceBoundToGeneration(GenerationMutationContext context,
        OpenRouterPremiumEvidenceArtifact validated, byte[] evidence, FinalJournalBinding finalJournal)
    {
        byte[] headerBytes = ReadLocatorArtifact(context.GenerationPath, context.GenerationIdentity,
            Path.Combine(JournalDirectoryName, FileOpenRouterPremiumJournal.HeaderFileName), 4 * 1024,
            context.Locator.JournalHeaderDigestSha256, context.Locator.JournalHeaderFileIdentity);
        try
        {
            using JsonDocument headerDocument = JsonDocument.Parse(headerBytes,
                new JsonDocumentOptions { MaxDepth = 4, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            using JsonDocument evidenceDocument = JsonDocument.Parse(evidence,
                new JsonDocumentOptions { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            JsonElement header = headerDocument.RootElement;
            JsonElement artifact = evidenceDocument.RootElement;
            foreach ((string headerName, string evidenceName) in new[]
                     {
                         ("journal_identity", "journal_identity"),
                         ("run_identity", "run_identity"),
                         ("profile_digest_sha256", "profile_digest_sha256"),
                         ("account_binding_identity", "account_binding_identity"),
                         ("header_checksum_sha256", "journal_header_checksum_sha256")
                     })
            {
                if (header.GetProperty(headerName).GetString() != artifact.GetProperty(evidenceName).GetString())
                    throw new OpenRouterPremiumProductionException("evidence_generation_binding_invalid");
            }
            OpenRouterPremiumJournalSnapshot snapshot = finalJournal.Snapshot;
            if (snapshot.Slots.Count != validated.Slots.Count
                || snapshot.SettledMicrousd != validated.TotalSettledMicrousd
                || snapshot.Slots.Any(static slot => slot.Receipt is null))
                throw new OpenRouterPremiumProductionException("evidence_generation_binding_invalid");
            for (int index = 0; index < snapshot.Slots.Count; index++)
            {
                if (FileOpenRouterPremiumJournalCodec.ReceiptDescriptor(snapshot.Slots[index].Receipt!)
                    != FileOpenRouterPremiumJournalCodec.ReceiptDescriptor(validated.Slots[index]))
                    throw new OpenRouterPremiumProductionException("evidence_generation_binding_invalid");
            }
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException("evidence_generation_binding_invalid"); }
        finally { CryptographicOperations.ZeroMemory(headerBytes); }
    }

    private void PublishReceipt(GenerationMutationContext context, string evidenceDigestSha256, byte[] receipt)
    {
        VerifyOperationDirectories(context.Lease);
        ValidationReceipt parsed = ValidationReceipt.Parse(receipt);
        if (parsed.AuthorizationDigestSha256 != context.Locator.AuthorizationDigestSha256
            || parsed.GenerationId != context.Locator.GenerationId
            || parsed.GenerationManifestDigestSha256 != context.Locator.GenerationManifestDigestSha256
            || parsed.StateContractDigestSha256 != StateContractDigestSha256
            || parsed.RootPathDigestSha256 != RootPathDigestSha256
            || parsed.EvidenceArtifactDigestSha256 != evidenceDigestSha256
            || parsed.ValidatedAtUnixMilliseconds != context.ClaimedAtUnixMilliseconds)
            throw new OpenRouterPremiumProductionException("validation_receipt_invalid");
        WriteNew(context.GenerationPath, context.GenerationIdentity,
            ValidationReceiptFileName, receipt, MaximumClaimPayloadBytes);
        _faults.AfterDurableBoundary(BoundaryValidationReceipt);
        FileOpenRouterPremiumFileIdentity identity = FileOpenRouterPremiumIdentity.CaptureSingleFileIdentity(
            context.GenerationPath, ValidationReceiptFileName);
        ReceiptBinding binding = new(ReceiptBindingSchema, context.Locator.AuthorizationDigestSha256,
            context.Locator.GenerationId, context.Locator.GenerationManifestDigestSha256,
            StateContractDigestSha256, RootPathDigestSha256, evidenceDigestSha256,
            OpenRouterPremiumCanonical.Digest(receipt), identity.CanonicalIdentity,
            context.ClaimedAtUnixMilliseconds, false);
        byte[] bytes = binding.Write();
        try { WriteProtectedNew(context.GenerationPath, context.GenerationIdentity, ReceiptBindingFileName,
            "receipt_binding", bytes, MaximumClaimPayloadBytes, MaximumClaimBytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        _faults.AfterDurableBoundary(BoundaryReceiptBinding);
        VerifyOperationDirectories(context.Lease);
    }

    private sealed record GenerationWriteContext(RootWriterLease Lease, string GenerationId, string GenerationPath,
        FileOpenRouterPremiumDirectoryIdentity GenerationIdentity, string ManifestDigestSha256,
        FileOpenRouterPremiumFileIdentity ManifestFileIdentity, string PreflightStartedDigestSha256,
        FileOpenRouterPremiumFileIdentity PreflightStartedFileIdentity, long StartedAtUnixMilliseconds);

    private sealed record GenerationMutationContext(RootWriterLease Lease, AuthorityLocator Locator, string GenerationPath,
        FileOpenRouterPremiumDirectoryIdentity GenerationIdentity, long ClaimedAtUnixMilliseconds);

    internal sealed class ClaimedExecutionJournalGrant
    {
        private readonly string _canonicalJournalDirectory;
        private readonly bool _finalReadbackOnly;
        private int _openPhase;

        internal ClaimedExecutionJournalGrant(string journalDirectory,
            FileOpenRouterPremiumDirectoryIdentity journalDirectoryIdentity,
            string headerDigestSha256, FileOpenRouterPremiumFileIdentity headerFileIdentity,
            string initialRecordsDigestSha256, FileOpenRouterPremiumFileIdentity recordsFileIdentity,
            string writerLeaseDigestSha256, FileOpenRouterPremiumFileIdentity writerLeaseFileIdentity,
            bool finalReadbackOnly)
        {
            _canonicalJournalDirectory = journalDirectory;
            JournalDirectoryIdentity = journalDirectoryIdentity;
            HeaderDigestSha256 = headerDigestSha256;
            HeaderFileIdentity = headerFileIdentity;
            InitialRecordsDigestSha256 = initialRecordsDigestSha256;
            RecordsFileIdentity = recordsFileIdentity;
            WriterLeaseDigestSha256 = writerLeaseDigestSha256;
            WriterLeaseFileIdentity = writerLeaseFileIdentity;
            _finalReadbackOnly = finalReadbackOnly;
        }

        internal string JournalDirectory => _canonicalJournalDirectory;
        internal FileOpenRouterPremiumDirectoryIdentity JournalDirectoryIdentity { get; }
        internal string HeaderDigestSha256 { get; }
        internal FileOpenRouterPremiumFileIdentity HeaderFileIdentity { get; }
        internal string InitialRecordsDigestSha256 { get; }
        internal FileOpenRouterPremiumFileIdentity RecordsFileIdentity { get; }
        internal string WriterLeaseDigestSha256 { get; }
        internal FileOpenRouterPremiumFileIdentity WriterLeaseFileIdentity { get; }

        internal void BeginOpen(string journalDirectory, bool finalReadback)
        {
            if (!string.Equals(journalDirectory, _canonicalJournalDirectory, StringComparison.Ordinal))
                throw new InvalidDataException("OpenRouter premium claimed journal path changed.");
            int expected = _finalReadbackOnly ? 0 : finalReadback ? 1 : 0;
            if (finalReadback != (_finalReadbackOnly || expected == 1)
                || Interlocked.CompareExchange(ref _openPhase, expected + 1, expected) != expected)
                throw new InvalidDataException("OpenRouter premium claimed journal grant was consumed.");
        }
    }

    internal sealed record FinalJournalBinding(
        FileOpenRouterPremiumDirectoryIdentity JournalDirectoryIdentity,
        string RecordsDigestSha256,
        FileOpenRouterPremiumFileIdentity RecordsFileIdentity,
        int RecordCount,
        string FinalRecordChecksumSha256,
        string SnapshotDigestSha256,
        string RestartEvidenceDigestSha256,
        OpenRouterPremiumJournalSnapshot Snapshot);
}

internal sealed class OpenRouterPremiumGenerationWriter : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _lease;
    private readonly Action<byte[]> _writeActivation;
    private readonly Func<OpenRouterPremiumJournalHeader, FileOpenRouterPremiumJournal> _createJournal;
    private readonly Action<byte[]> _writePreflight;
    private readonly Action _afterJournalPublicationFreeze;
    private readonly Func<byte[], string> _publishAuthorization;
    private FileOpenRouterPremiumJournal? _journal;
    private int _stage;

    internal OpenRouterPremiumGenerationWriter(IDisposable lease, string generationId,
        string manifestDigestSha256, long startedAtUnixMilliseconds,
        Action<byte[]> writeActivation,
        Func<OpenRouterPremiumJournalHeader, FileOpenRouterPremiumJournal> createJournal,
        Action<byte[]> writePreflight,
        Action afterJournalPublicationFreeze,
        Func<byte[], string> publishAuthorization)
    {
        _lease = lease; GenerationId = generationId; ManifestDigestSha256 = manifestDigestSha256;
        StartedAtUnixMilliseconds = startedAtUnixMilliseconds; _writeActivation = writeActivation;
        _createJournal = createJournal; _writePreflight = writePreflight;
        _afterJournalPublicationFreeze = afterJournalPublicationFreeze;
        _publishAuthorization = publishAuthorization;
    }

    internal string GenerationId { get; }
    internal string ManifestDigestSha256 { get; }
    internal long StartedAtUnixMilliseconds { get; }

    internal void WriteActivationBundle(byte[] bytes)
    {
        lock (_gate)
        {
            RequireStage(0);
            try { _writeActivation(bytes); _stage = 1; }
            catch { _stage = -1; throw; }
        }
    }

    internal FileOpenRouterPremiumJournal CreateJournal(OpenRouterPremiumJournalHeader header)
    {
        lock (_gate)
        {
            RequireStage(1);
            try
            {
                FileOpenRouterPremiumJournal journal = _createJournal(header);
                _journal = journal;
                _stage = 2;
                return journal;
            }
            catch { _stage = -1; throw; }
        }
    }

    internal FileOpenRouterPremiumJournal RestartJournalForPreflight()
    {
        lock (_gate)
        {
            RequireStage(2);
            if (_journal is null)
                throw new OpenRouterPremiumProductionException("generation_journal_unavailable");
            try
            {
                _journal = _journal.RestartForPublication();
                return _journal;
            }
            catch { _stage = -1; throw; }
        }
    }

    internal void WritePreflightArtifact(byte[] bytes)
    {
        lock (_gate)
        {
            RequireStage(2);
            try { _writePreflight(bytes); _stage = 3; }
            catch { _stage = -1; throw; }
        }
    }

    internal string PublishAuthorization(byte[] ciphertext)
    {
        lock (_gate)
        {
            RequireStage(3);
            try
            {
                if (_journal is null)
                    throw new OpenRouterPremiumProductionException("generation_journal_unavailable");
                _journal.FreezeForPublication();
                _journal = null;
                _afterJournalPublicationFreeze();
                string authority = _publishAuthorization(ciphertext);
                _stage = 5;
                return authority;
            }
            catch { _stage = -1; throw; }
        }
    }

    private void RequireStage(int expected)
    {
        if (_lease is null) throw new ObjectDisposedException(nameof(OpenRouterPremiumGenerationWriter));
        if (_stage != expected) throw new OpenRouterPremiumProductionException("generation_stage_invalid");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _journal?.AbortPublication();
            _journal = null;
            IDisposable? lease = _lease;
            _lease = null;
            lease?.Dispose();
        }
    }
}

internal sealed class OpenRouterPremiumResolvedGeneration : IDisposable
{
    private readonly object _gate = new();
    private byte[] _manifest;
    private byte[] _preflight;
    private byte[] _authorization;

    internal OpenRouterPremiumResolvedGeneration(string generationId, string authorizationDigestSha256,
        string generationManifestDigestSha256, byte[] manifest, byte[] preflight, byte[] authorization)
    {
        GenerationId = generationId; AuthorizationDigestSha256 = authorizationDigestSha256;
        GenerationManifestDigestSha256 = generationManifestDigestSha256;
        _manifest = manifest.ToArray(); _preflight = preflight.ToArray(); _authorization = authorization.ToArray();
    }

    internal string GenerationId { get; }
    internal string AuthorizationDigestSha256 { get; }
    internal string GenerationManifestDigestSha256 { get; }
    internal ReadOnlyMemory<byte> ManifestBytes { get { lock (_gate) return _manifest.ToArray(); } }
    internal ReadOnlyMemory<byte> PreflightArtifactBytes { get { lock (_gate) return _preflight.ToArray(); } }
    internal ReadOnlyMemory<byte> RuntimeAuthorizationBytes { get { lock (_gate) return _authorization.ToArray(); } }

    public void Dispose()
    {
        lock (_gate)
        {
            CryptographicOperations.ZeroMemory(_manifest);
            CryptographicOperations.ZeroMemory(_preflight);
            CryptographicOperations.ZeroMemory(_authorization);
            _manifest = []; _preflight = []; _authorization = [];
        }
    }
}

internal sealed class OpenRouterPremiumExecutionGeneration : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _lease;
    private readonly Action<byte[], OpenRouterPremiumStateGenerationStore.FinalJournalBinding> _publishEvidence;
    private readonly OpenRouterPremiumStateGenerationStore.ClaimedExecutionJournalGrant _journalGrant;
    private FileOpenRouterPremiumJournal? _journal;
    private byte[] _runtimeAuthorization;
    private byte[] _preflightArtifact;
    private int _published;

    internal OpenRouterPremiumExecutionGeneration(IDisposable lease, string generationId,
        string authorizationDigestSha256, string generationManifestDigestSha256,
        long claimedAtUnixMilliseconds, byte[] runtimeAuthorization, byte[] preflightArtifact,
        FileOpenRouterPremiumJournal journal,
        OpenRouterPremiumStateGenerationStore.ClaimedExecutionJournalGrant journalGrant,
        Action<byte[], OpenRouterPremiumStateGenerationStore.FinalJournalBinding> publishEvidence)
    {
        _lease = lease; GenerationId = generationId; AuthorizationDigestSha256 = authorizationDigestSha256;
        GenerationManifestDigestSha256 = generationManifestDigestSha256; ClaimedAtUnixMilliseconds = claimedAtUnixMilliseconds;
        _runtimeAuthorization = runtimeAuthorization.ToArray(); _preflightArtifact = preflightArtifact.ToArray();
        _journal = journal; _journalGrant = journalGrant; _publishEvidence = publishEvidence;
    }

    internal string GenerationId { get; }
    internal string AuthorizationDigestSha256 { get; }
    internal string GenerationManifestDigestSha256 { get; }
    internal long ClaimedAtUnixMilliseconds { get; }
    internal ReadOnlyMemory<byte> RuntimeAuthorizationBytes { get { lock (_gate) return _runtimeAuthorization.ToArray(); } }
    internal ReadOnlyMemory<byte> PreflightArtifactBytes { get { lock (_gate) return _preflightArtifact.ToArray(); } }
    internal FileOpenRouterPremiumJournal Journal
    {
        get
        {
            lock (_gate)
                return _journal ?? throw new OpenRouterPremiumProductionException("execution_journal_unavailable");
        }
    }

    internal void WriteEvidence(byte[] evidence)
    {
        lock (_gate)
        {
            if (_lease is null) throw new ObjectDisposedException(nameof(OpenRouterPremiumExecutionGeneration));
            if (_published != 0) throw new OpenRouterPremiumProductionException("evidence_already_published");
            _published = 1;
            FileOpenRouterPremiumJournal journal = _journal
                ?? throw new OpenRouterPremiumProductionException("execution_journal_unavailable");
            _journal = null;
            journal.Dispose();
            using FileOpenRouterPremiumJournal readback =
                FileOpenRouterPremiumJournal.OpenForClaimedExecution(_journalGrant, finalReadback: true);
            OpenRouterPremiumDurableRestartEvidence restart = readback.RestartEvidence;
            if (!restart.RestartVerified || !restart.FlushToDiskRequested)
                throw new OpenRouterPremiumProductionException("journal_restart_binding_invalid");
            OpenRouterPremiumJournalSnapshot snapshot = readback.Snapshot();
            OpenRouterPremiumStateGenerationStore.FinalJournalBinding final = new(
                _journalGrant.JournalDirectoryIdentity, readback.RecordsFileDigestSha256,
                _journalGrant.RecordsFileIdentity, restart.RecordCount,
                restart.FinalRecordChecksumSha256, restart.SnapshotDigestSha256,
                restart.EvidenceDigestSha256, snapshot);
            _publishEvidence(evidence, final);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CryptographicOperations.ZeroMemory(_runtimeAuthorization);
            CryptographicOperations.ZeroMemory(_preflightArtifact);
            _runtimeAuthorization = []; _preflightArtifact = [];
            _journal?.Dispose();
            _journal = null;
            IDisposable? lease = _lease;
            _lease = null;
            lease?.Dispose();
        }
    }
}

internal sealed class OpenRouterPremiumValidationGeneration : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _lease;
    private readonly Action<byte[]> _publishReceipt;
    private byte[] _evidence;
    private byte[] _canonicalReceipt;
    private int _published;

    internal OpenRouterPremiumValidationGeneration(IDisposable lease, string generationId,
        string authorizationDigestSha256, string generationManifestDigestSha256, byte[] evidence,
        string evidenceDigestSha256, byte[] canonicalReceipt, long claimedAtUnixMilliseconds,
        Action<byte[]> publishReceipt)
    {
        _lease = lease; GenerationId = generationId; AuthorizationDigestSha256 = authorizationDigestSha256;
        GenerationManifestDigestSha256 = generationManifestDigestSha256; EvidenceDigestSha256 = evidenceDigestSha256;
        ClaimedAtUnixMilliseconds = claimedAtUnixMilliseconds; _evidence = evidence;
        _canonicalReceipt = canonicalReceipt.ToArray();
        _publishReceipt = publishReceipt;
    }

    internal string GenerationId { get; }
    internal string AuthorizationDigestSha256 { get; }
    internal string GenerationManifestDigestSha256 { get; }
    internal string EvidenceDigestSha256 { get; }
    internal long ClaimedAtUnixMilliseconds { get; }
    internal ReadOnlyMemory<byte> EvidenceBytes { get { lock (_gate) return _evidence.ToArray(); } }
    internal ReadOnlyMemory<byte> CanonicalReceiptBytes { get { lock (_gate) return _canonicalReceipt.ToArray(); } }

    internal void WriteReceipt()
    {
        lock (_gate)
        {
            if (_lease is null) throw new ObjectDisposedException(nameof(OpenRouterPremiumValidationGeneration));
            if (_published != 0) throw new OpenRouterPremiumProductionException("validation_receipt_already_published");
            _published = 1;
            _publishReceipt(_canonicalReceipt);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CryptographicOperations.ZeroMemory(_evidence); _evidence = [];
            CryptographicOperations.ZeroMemory(_canonicalReceipt); _canonicalReceipt = [];
            IDisposable? lease = _lease;
            _lease = null;
            lease?.Dispose();
        }
    }
}
