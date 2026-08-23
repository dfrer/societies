using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

internal interface IOpenRouterPremiumV2StateStoreFactory
{
    OpenRouterPremiumStateGenerationStore Open(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor);
}

internal interface IOpenRouterPremiumV2OfflineEmptyStateStoreFactory
{
    void InitializeNew(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor);
}

internal sealed class OpenRouterPremiumV2StateStoreFactory : IOpenRouterPremiumV2StateStoreFactory,
    IOpenRouterPremiumV2OfflineEmptyStateStoreFactory
{
    internal static OpenRouterPremiumV2StateStoreFactory Instance { get; } = new();
    private readonly Action<string>? _afterDirectoryPinned;

    internal OpenRouterPremiumV2StateStoreFactory(Action<string>? afterDirectoryPinned = null) =>
        _afterDirectoryPinned = afterDirectoryPinned;

    public OpenRouterPremiumStateGenerationStore Open(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor) =>
        OpenCore(localApplicationDataRoot, fixedV2Root, trustAnchor,
            requireNewFixedRoot: false);

    public void InitializeNew(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor) =>
        _ = OpenCore(localApplicationDataRoot, fixedV2Root, trustAnchor,
            requireNewFixedRoot: true);

    private OpenRouterPremiumStateGenerationStore OpenCore(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchor trustAnchor,
        bool requireNewFixedRoot)
    {
        string container = Path.GetFullPath(localApplicationDataRoot);
        string root = Path.GetFullPath(fixedV2Root);
        string relative = Path.GetRelativePath(container, root);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative)
            || !string.Equals(relative,
                Path.Combine("Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v2"),
                StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_root_invalid");

        List<PinnedDirectory> pinned = [];
        try
        {
            string volumeRoot = Path.GetPathRoot(container)
                ?? throw new OpenRouterPremiumProductionException("state_root_invalid");
            if (volumeRoot.StartsWith(@"\\", StringComparison.Ordinal)
                || volumeRoot.StartsWith(@"\\?\", StringComparison.Ordinal)
                || volumeRoot.StartsWith(@"\\.\", StringComparison.Ordinal))
                throw new OpenRouterPremiumProductionException("state_root_invalid");
            string current = volumeRoot;
            PinExistingExact(current, pinned);
            string containerRelative = Path.GetRelativePath(volumeRoot, container);
            foreach (string segment in ExactSegments(containerRelative))
            {
                current = Path.Combine(current, segment);
                PinExistingExact(current, pinned);
            }
            if (!string.Equals(current, container, StringComparison.Ordinal))
                throw new OpenRouterPremiumProductionException("state_root_invalid");

            foreach (string segment in ExactSegments(relative))
            {
                current = Path.Combine(current, segment);
                VerifyPinned(pinned);
                if (Directory.Exists(current))
                {
                    if (requireNewFixedRoot && string.Equals(current, root, StringComparison.Ordinal))
                        throw new OpenRouterPremiumProductionException(
                            "state_root_initialization_not_empty");
                    PinExistingExact(current, pinned);
                    continue;
                }
                if (!CreateDirectoryW(current, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    _ = new Win32Exception(error);
                    throw new OpenRouterPremiumProductionException(error is 80 or 183
                        ? "state_root_creation_race" : "state_root_creation_failed");
                }
                try { PinExistingExact(current, pinned); }
                catch { throw new OpenRouterPremiumProductionException("state_root_creation_race"); }
                VerifyPinned(pinned);
            }
            if (!string.Equals(current, root, StringComparison.Ordinal))
                throw new OpenRouterPremiumProductionException("state_root_invalid");
            return new OpenRouterPremiumStateGenerationStore(root, trustAnchor);
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException("state_root_invalid"); }
        finally
        {
            for (int index = pinned.Count - 1; index >= 0; index--)
                pinned[index].Handle.Dispose();
        }
    }

    private static IEnumerable<string> ExactSegments(string relative)
    {
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or ".." || segment.Contains(Path.AltDirectorySeparatorChar)
                || segment.Contains(':'))
                throw new OpenRouterPremiumProductionException("state_root_invalid");
            yield return segment;
        }
    }

    private void PinExistingExact(string path, List<PinnedDirectory> pinned)
    {
        SafeFileHandle handle = FileOpenRouterPremiumIdentity.OpenDirectoryMutationLease(path);
        try
        {
            string canonical = FileOpenRouterPremiumIdentity.GetCanonicalDirectoryPath(path);
            if (!string.Equals(canonical, path, StringComparison.Ordinal))
                throw new OpenRouterPremiumProductionException("state_root_invalid");
            FileOpenRouterPremiumDirectoryIdentity identity =
                FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(path);
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(path, identity, handle);
            _afterDirectoryPinned?.Invoke(path);
            pinned.Add(new(path, identity, handle));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static void VerifyPinned(IEnumerable<PinnedDirectory> pinned)
    {
        foreach (PinnedDirectory directory in pinned)
            FileOpenRouterPremiumIdentity.VerifyStableDirectory(
                directory.Path, directory.Identity, directory.Handle);
    }

    private sealed record PinnedDirectory(string Path,
        FileOpenRouterPremiumDirectoryIdentity Identity, SafeFileHandle Handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string pathName, IntPtr securityAttributes);
}

/// <summary>
/// Deep v2 state Module. Its only state operations are preflight, exact-digest record-once,
/// and exact-digest validate-once. Construction computes policy and paths without I/O. Every
/// operation opens the required external anchor lease before v2 root creation or access.
/// </summary>
public sealed class OpenRouterPremiumV2ProductionBridge
{
    internal const string StateRootVersion = "v2";
    private readonly string _localApplicationDataRoot;
    private readonly string _fixedV2Root;
    private readonly IOpenRouterPremiumStateTrustAnchorLeaseSource _anchorSource;
    private readonly IOpenRouterPremiumV2StateStoreFactory _storeFactory;
    private readonly IOpenRouterPremiumCredentialStore _credentialStore;
    private readonly IOpenRouterPremiumProductionProtector _protector;
    private readonly IOpenRouterPremiumProductionClock _clock;
    private readonly Func<IOpenRouterPremiumProductionMetadataVerifier> _metadataVerifierFactory;
    private readonly Func<OpenRouterPremiumHttpExchange> _exchangeFactory;

    internal OpenRouterPremiumV2ProductionBridge(
        string localApplicationDataRoot,
        string fixedV2Root,
        IOpenRouterPremiumStateTrustAnchorLeaseSource anchorSource,
        IOpenRouterPremiumV2StateStoreFactory storeFactory,
        IOpenRouterPremiumCredentialStore credentialStore,
        IOpenRouterPremiumProductionProtector protector,
        IOpenRouterPremiumProductionClock clock,
        Func<IOpenRouterPremiumProductionMetadataVerifier> metadataVerifierFactory,
        Func<OpenRouterPremiumHttpExchange> exchangeFactory)
    {
        _localApplicationDataRoot = RequireCanonicalPath(localApplicationDataRoot);
        _fixedV2Root = RequireFixedV2Path(_localApplicationDataRoot, fixedV2Root);
        _anchorSource = anchorSource ?? throw new ArgumentNullException(nameof(anchorSource));
        _storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _metadataVerifierFactory = metadataVerifierFactory ?? throw new ArgumentNullException(nameof(metadataVerifierFactory));
        _exchangeFactory = exchangeFactory ?? throw new ArgumentNullException(nameof(exchangeFactory));
    }

    public static OpenRouterPremiumV2ProductionBridge CreateDefault()
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        (string local, string root) = ComputeDefaultPaths();
        OpenRouterPremiumWindowsCredentialStore credentialStore = new();
        IOpenRouterPremiumProductionClock clock = SystemOpenRouterPremiumProductionClock.Instance;
        return new(local, root, new OpenRouterPremiumWindowsStateTrustAnchorSource(),
            OpenRouterPremiumV2StateStoreFactory.Instance, credentialStore,
            new OpenRouterPremiumDpapiProtector(), clock,
            () => OpenRouterPremiumHttpMetadataVerifier.CreateProduction(credentialStore, clock),
            OpenRouterPremiumHttpExchange.CreateProduction);
    }

    internal static (string LocalApplicationDataRoot, string FixedV2Root) ComputeDefaultPaths(string? local = null)
    {
        string value = local ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(value))
            throw new OpenRouterPremiumProductionException("state_root_unavailable");
        string container = RequireCanonicalPath(value);
        string root = Path.Combine(container, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", StateRootVersion);
        return (container, RequireFixedV2Path(container, root));
    }

    public async ValueTask<OpenRouterPremiumProductionPreflightResult> PreflightAsync(
        CancellationToken cancellationToken = default)
    {
        using IOpenRouterPremiumStateTrustAnchorLease anchor = _anchorSource.OpenExisting();
        OpenRouterPremiumStateGenerationStore store =
            _storeFactory.Open(_localApplicationDataRoot, _fixedV2Root, anchor);
        long startedAt = _clock.NowMilliseconds;
        using OpenRouterPremiumGenerationWriter writer = store.BeginPreflight(startedAt);
        byte[]? activationBundleUtf8 = null;
        try
        {
            IOpenRouterPremiumProductionMetadataVerifier verifier = _metadataVerifierFactory();
            using OpenRouterPremiumVerifiedMetadata verified =
                await verifier.VerifyOnceAsync(cancellationToken).ConfigureAwait(false);
            activationBundleUtf8 = verified.TransferOwnedCanonicalBundle();
            OpenRouterPremiumActivationPreflightCapability preflightCapability =
                OpenRouterPremiumActivationPreflightModule.Authorize(activationBundleUtf8);
            OpenRouterPremiumActivationBundle bundle = preflightCapability.Bundle;
            using (OpenRouterPremiumStoredCredential credential = _credentialStore.Read())
            {
                if (credential.AccountBindingIdentity != bundle.AccountBindingIdentity)
                    throw new OpenRouterPremiumProductionException("credential_account_mismatch");
                byte[] material = credential.TransferOwnedMaterial();
                try
                {
                    if (!OpenRouterPremiumCredentialMaterial.IsValid(material))
                        throw new OpenRouterPremiumProductionException("credential_malformed");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(material);
                    credential.ZeroObserver(material.All(static value => value == 0));
                }
            }

            long now = _clock.NowMilliseconds;
            long expires;
            try { expires = checked(now + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds); }
            catch (OverflowException) { throw new OpenRouterPremiumProductionException("key_expiry_window_invalid"); }
            if (!DateTimeOffset.TryParseExact(bundle.Attestation.ExpiresAtUtc, "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset attestationExpiry)
                || attestationExpiry.Offset != TimeSpan.Zero
                || expires > attestationExpiry.ToUnixTimeMilliseconds())
                throw new OpenRouterPremiumProductionException("key_expiry_window_invalid");

            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteActivationBundle(activationBundleUtf8);
            OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
            OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
                "openrouter-premium-production-journal/v1", "openrouter-premium-production-run/v1", profile,
                new ByokAccountBindingIdentity(bundle.AccountBindingIdentity));
            _ = writer.CreateJournal(header);
            FileOpenRouterPremiumJournal journal = writer.RestartJournalForPreflight();
            OpenRouterPremiumPreflightTrustAttestation trust = new(
                OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
                preflightCapability.BundleDigestSha256, bundle.AccountBindingIdentity,
                bundle.CredentialSourceIdentity);
            OpenRouterPremiumActivationPreflightArtifact artifact =
                OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                    preflightCapability, journal, trust, now, cancellationToken);
            if (!artifact.TrustContextValidated || !artifact.Eligible || artifact.BlockerCodes.Count != 0
                || artifact.MaximumRequests != 12 || artifact.AggregateCostCeilingMicrousd != 18_000
                || artifact.DurableConsumptionEvidenceDigestSha256 is null)
                throw new OpenRouterPremiumProductionException("preflight_ineligible");
            writer.WritePreflightArtifact(artifact.CanonicalUtf8.ToArray());

            string runtimeNonce = "openrouter-runtime-" +
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            OpenRouterPremiumAuthorization executionAuthorization = new(profile.Identity,
                OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
                OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                new ByokAccountBindingIdentity(bundle.AccountBindingIdentity), header.JournalIdentity,
                header.HeaderChecksumSha256, OpenRouterPremiumHttpExchange.AdapterIdentity,
                OpenRouterPremiumHttpExchange.AdapterContractDigestSha256,
                OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity,
                runtimeNonce, now, expires);
            OpenRouterPremiumExecutionCapability capability =
                OpenRouterPremiumEvidenceModule.Authorize(executionAuthorization);
            OpenRouterPremiumRuntimeAuthorization runtime = new(
                OpenRouterPremiumRuntimeAuthorization.CurrentSchemaVersion,
                profile.ProfileDigestSha256, OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
                OpenRouterPremiumProfile.EndpointEvidenceDigestSha256, bundle.AccountBindingIdentity,
                OpenRouterPremiumWindowsCredentialStore.TargetIdentity,
                OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity,
                header.JournalIdentity, header.HeaderChecksumSha256, artifact.CanonicalDigestSha256,
                artifact.PayloadDigestSha256, artifact.DurableConsumptionEvidenceDigestSha256,
                capability.CapabilityDigestSha256, runtimeNonce, now, expires, 12, 18_000);
            byte[] plaintext = runtime.Write();
            byte[]? ciphertext = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ciphertext = _protector.Protect(plaintext);
                cancellationToken.ThrowIfCancellationRequested();
                string digest = writer.PublishAuthorization(ciphertext);
                return new(digest, artifact.CanonicalDigestSha256, bundle.AccountBindingIdentity,
                    expires, 12, 18_000);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                if (ciphertext is not null) CryptographicOperations.ZeroMemory(ciphertext);
            }
        }
        finally
        {
            if (activationBundleUtf8 is not null)
                CryptographicOperations.ZeroMemory(activationBundleUtf8);
        }
    }

    public async ValueTask<OpenRouterPremiumProductionRunResult> RecordOnceAsync(
        string confirmedAuthorizationDigestSha256,
        CancellationToken cancellationToken = default)
    {
        RequireDigest(confirmedAuthorizationDigestSha256);
        using IOpenRouterPremiumStateTrustAnchorLease anchor = _anchorSource.OpenExisting();
        OpenRouterPremiumStateGenerationStore store =
            _storeFactory.Open(_localApplicationDataRoot, _fixedV2Root, anchor);
        using OpenRouterPremiumExecutionGeneration execution =
            store.OpenForExecution(confirmedAuthorizationDigestSha256, _clock.NowMilliseconds);
        byte[] ciphertext = execution.RuntimeAuthorizationBytes.ToArray();
        byte[]? plaintext = null;
        try
        {
            if (!FixedDigestEquals(OpenRouterPremiumCanonical.Digest(ciphertext),
                    confirmedAuthorizationDigestSha256))
                throw new OpenRouterPremiumProductionException("authorization_confirmation_mismatch");
            plaintext = _protector.Unprotect(ciphertext);
            OpenRouterPremiumRuntimeAuthorization runtime = OpenRouterPremiumRuntimeAuthorization.Parse(plaintext);
            long now = _clock.NowMilliseconds;
            if (now < runtime.IssuedAtUnixMilliseconds || now >= runtime.ExpiresAtUnixMilliseconds)
                throw new OpenRouterPremiumProductionException("authorization_expired");
            byte[] preflightBytes = execution.PreflightArtifactBytes.ToArray();
            try
            {
                OpenRouterPremiumActivationPreflightArtifact preflight =
                    OpenRouterPremiumActivationPreflightArtifactModule.Validate(preflightBytes);
                if (!preflight.ClaimedEligible
                    || preflight.ClaimedDecision != "eligible_for_separately_authorized_one_shot"
                    || preflight.CanonicalDigestSha256 != runtime.PreflightArtifactDigestSha256
                    || preflight.PayloadDigestSha256 != runtime.PreflightPayloadDigestSha256
                    || preflight.DurableConsumptionEvidenceDigestSha256 != runtime.PreflightConsumptionDigestSha256
                    || preflight.AccountBindingIdentity != runtime.AccountBindingIdentity)
                    throw new OpenRouterPremiumProductionException("preflight_binding_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(preflightBytes); }

            FileOpenRouterPremiumJournal journal = execution.Journal;
            OpenRouterPremiumJournalSnapshot snapshot = journal.Snapshot();
            if (!journal.RestartEvidence.RestartVerified || journal.RestartEvidence.RecordCount != 1
                || snapshot.Slots.Count != 0 || snapshot.ReservedExposureMicrousd != 0
                || snapshot.SettledMicrousd != 0
                || journal.Header.HeaderChecksumSha256 != runtime.JournalHeaderChecksumSha256
                || journal.Header.AccountBindingIdentity != runtime.AccountBindingIdentity)
                throw new OpenRouterPremiumProductionException("journal_restart_binding_invalid");

            OpenRouterPremiumAuthorization authorization = new(OpenRouterPremiumProfileRegistry.Selected.Identity,
                OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
                OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                new ByokAccountBindingIdentity(runtime.AccountBindingIdentity), runtime.JournalIdentity,
                runtime.JournalHeaderChecksumSha256, OpenRouterPremiumHttpExchange.AdapterIdentity,
                OpenRouterPremiumHttpExchange.AdapterContractDigestSha256, runtime.CredentialSourceIdentity,
                runtime.RuntimeNonce, runtime.IssuedAtUnixMilliseconds, runtime.ExpiresAtUnixMilliseconds);
            OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(authorization);
            if (capability.CapabilityDigestSha256 != runtime.CapabilityDigestSha256)
                throw new OpenRouterPremiumProductionException("authorization_binding_invalid");
            OpenRouterPremiumProductionLeaseSource source = new(_credentialStore,
                runtime.AccountBindingIdentity, capability.CapabilityDigestSha256,
                runtime.JournalHeaderChecksumSha256, runtime.IssuedAtUnixMilliseconds,
                runtime.ExpiresAtUnixMilliseconds);
            using OpenRouterPremiumHttpExchange exchange = _exchangeFactory();
            OpenRouterPremiumEvidenceArtifact artifact =
                await OpenRouterPremiumEvidenceModule.ExecuteAuthorizedProductionOnceAsync(
                    capability, exchange, source, journal, _clock,
                    new OpenRouterPremiumProductionExecutionPermit(runtime), cancellationToken)
                    .ConfigureAwait(false);
            execution.WriteEvidence(artifact.CanonicalUtf8.ToArray());
            return new(artifact.Status, artifact.ExchangeCount, artifact.TotalSettledMicrousd,
                artifact.TerminalCode, artifact.CanonicalDigestSha256);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ciphertext);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public OpenRouterPremiumProductionValidationResult ValidateOnce(
        string confirmedAuthorizationDigestSha256)
    {
        RequireDigest(confirmedAuthorizationDigestSha256);
        using IOpenRouterPremiumStateTrustAnchorLease anchor = _anchorSource.OpenExisting();
        OpenRouterPremiumStateGenerationStore store =
            _storeFactory.Open(_localApplicationDataRoot, _fixedV2Root, anchor);
        using OpenRouterPremiumValidationGeneration validation =
            store.OpenForValidation(confirmedAuthorizationDigestSha256, _clock.NowMilliseconds);
        byte[] evidence = validation.EvidenceBytes.ToArray();
        try
        {
            OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceArtifactModule.Validate(evidence);
            byte[] receipt = validation.CanonicalReceiptBytes.ToArray();
            try
            {
                string receiptDigest = OpenRouterPremiumCanonical.Digest(receipt);
                validation.WriteReceipt();
                return new(artifact.Status, artifact.ExchangeCount, artifact.TotalSettledMicrousd,
                    artifact.CanonicalDigestSha256, receiptDigest);
            }
            finally { CryptographicOperations.ZeroMemory(receipt); }
        }
        finally { CryptographicOperations.ZeroMemory(evidence); }
    }

    private static void RequireDigest(string value)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(value))
            throw new OpenRouterPremiumProductionException("authorization_confirmation_invalid");
    }

    private static string RequireCanonicalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        string full = Path.GetFullPath(value);
        if (!string.Equals(full, value, StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        return full;
    }

    private static string RequireFixedV2Path(string container, string root)
    {
        string full = RequireCanonicalPath(root);
        string expected = Path.Combine(container, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", StateRootVersion);
        if (!string.Equals(full, expected, StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        return full;
    }

    private static bool FixedDigestEquals(string left, string right)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(left) || !OpenRouterPremiumCanonical.IsDigest(right)) return false;
        byte[] first = Convert.FromHexString(left);
        byte[] second = Convert.FromHexString(right);
        try { return CryptographicOperations.FixedTimeEquals(first, second); }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }
}

public sealed class OpenRouterPremiumProductionCredentialAdministration
{
    private readonly IOpenRouterPremiumCredentialStore _credentialStore;

    internal OpenRouterPremiumProductionCredentialAdministration(IOpenRouterPremiumCredentialStore credentialStore) =>
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));

    public static OpenRouterPremiumProductionCredentialAdministration CreateDefault() =>
        new(new OpenRouterPremiumWindowsCredentialStore());

    public void Store(char[] ownedSecret)
    {
        ArgumentNullException.ThrowIfNull(ownedSecret);
        byte[]? bytes = null;
        try
        {
            if (ownedSecret.Length is < 1 or > 512 || ownedSecret.Any(static value => value > 0x7f))
                throw new OpenRouterPremiumProductionException("credential_malformed");
            bytes = ownedSecret.Select(static value => (byte)value).ToArray();
            if (!OpenRouterPremiumCredentialMaterial.IsValid(bytes))
                throw new OpenRouterPremiumProductionException("credential_malformed");
            _credentialStore.Write(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, bytes);
        }
        finally
        {
            Array.Clear(ownedSecret);
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Delete() => _credentialStore.Delete();
}
