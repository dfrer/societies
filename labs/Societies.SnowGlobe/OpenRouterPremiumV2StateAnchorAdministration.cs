using System.Security.Cryptography;

namespace Societies.SnowGlobe;

internal interface IOpenRouterPremiumStateAnchorKeyGenerator
{
    byte[] GenerateOwned();
}

internal interface IOpenRouterPremiumStateAnchorProvisioningLockSource
{
    IDisposable TryAcquire();
}

internal interface IOpenRouterPremiumV2OfflineStateInitializer
{
    void Initialize(IOpenRouterPremiumStateTrustAnchor trustAnchor);
}

internal sealed class RandomOpenRouterPremiumStateAnchorKeyGenerator
    : IOpenRouterPremiumStateAnchorKeyGenerator
{
    internal static RandomOpenRouterPremiumStateAnchorKeyGenerator Instance { get; } = new();
    private RandomOpenRouterPremiumStateAnchorKeyGenerator() { }

    public byte[] GenerateOwned() =>
        RandomNumberGenerator.GetBytes(OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes);
}

internal sealed class OpenRouterPremiumStateAnchorProvisioningLockSource
    : IOpenRouterPremiumStateAnchorProvisioningLockSource
{
    internal const string MutexName =
        @"Global\Societies.SnowGlobe.OpenRouterPremium.StateAnchorProvision.v2";
    internal static OpenRouterPremiumStateAnchorProvisioningLockSource Instance { get; } = new();
    private readonly Func<string, Mutex> _mutexFactory;

    private OpenRouterPremiumStateAnchorProvisioningLockSource()
        : this(static name => new Mutex(initiallyOwned: false, name)) { }

    internal OpenRouterPremiumStateAnchorProvisioningLockSource(
        Func<string, Mutex> mutexFactory) =>
        _mutexFactory = mutexFactory ?? throw new ArgumentNullException(nameof(mutexFactory));

    public IDisposable TryAcquire()
    {
        Mutex? mutex = null;
        try
        {
            mutex = _mutexFactory(MutexName)
                ?? throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");
            bool acquired;
            try { acquired = mutex.WaitOne(TimeSpan.Zero); }
            catch (AbandonedMutexException)
            {
                try { mutex.ReleaseMutex(); }
                finally { mutex.Dispose(); }
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");
            }
            if (!acquired)
            {
                mutex.Dispose();
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_writer_busy");
            }
            MutexLease lease = new(mutex);
            mutex = null;
            return lease;
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch
        {
            mutex?.Dispose();
            throw new OpenRouterPremiumProductionException(
                "state_trust_anchor_provisioning_indeterminate");
        }
    }

    private sealed class MutexLease(Mutex mutex) : IDisposable
    {
        private Mutex? _mutex = mutex;

        public void Dispose()
        {
            Mutex? owned = Interlocked.Exchange(ref _mutex, null);
            if (owned is null) return;
            try { owned.ReleaseMutex(); }
            finally { owned.Dispose(); }
        }
    }
}

internal sealed class OpenRouterPremiumV2OfflineStateInitializer
    : IOpenRouterPremiumV2OfflineStateInitializer
{
    private readonly string? _localApplicationDataRoot;
    private readonly string? _fixedV2Root;
    private readonly IOpenRouterPremiumV2OfflineEmptyStateStoreFactory _storeFactory;

    internal OpenRouterPremiumV2OfflineStateInitializer(
        string? localApplicationDataRoot = null,
        string? fixedV2Root = null,
        IOpenRouterPremiumV2OfflineEmptyStateStoreFactory? storeFactory = null)
    {
        if ((localApplicationDataRoot is null) != (fixedV2Root is null))
            throw new ArgumentException("Both fixed roots must be supplied together.");
        _localApplicationDataRoot = localApplicationDataRoot;
        _fixedV2Root = fixedV2Root;
        _storeFactory = storeFactory ?? OpenRouterPremiumV2StateStoreFactory.Instance;
    }

    public void Initialize(IOpenRouterPremiumStateTrustAnchor trustAnchor)
    {
        ArgumentNullException.ThrowIfNull(trustAnchor);
        (string local, string root) = _localApplicationDataRoot is null
            ? OpenRouterPremiumV2ProductionBridge.ComputeDefaultPaths()
            : (_localApplicationDataRoot, _fixedV2Root!);
        _storeFactory.InitializeNew(local, root, trustAnchor);
    }
}

public sealed record OpenRouterPremiumV2StateAnchorProvisioningResult(
    string AnchorIdentitySha256,
    string StateContractDigestSha256,
    string StateRootPolicy,
    bool OpenRouterCredentialRead,
    bool ProviderAccess,
    bool AdditionalAttemptAuthorized);

/// <summary>
/// Separate one-shot lifecycle administration for the v2 state anchor. This type does not expose
/// delete, replacement, rotation, repair, import, discovery, retry, or provider operations.
/// The named mutex is cooperative process exclusion only; it does not add owning-user, admin, or
/// whole-volume rollback resistance.
/// </summary>
public sealed class OpenRouterPremiumV2StateAnchorAdministration
{
    public const string StateRootPolicy = "fixed_local_app_data_v2_no_v1_observation";

    private readonly IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend _reader;
    private readonly IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend _writer;
    private readonly IOpenRouterPremiumStateAnchorProvisioningLockSource _lockSource;
    private readonly IOpenRouterPremiumStateAnchorKeyGenerator _keyGenerator;
    private readonly IOpenRouterPremiumV2OfflineStateInitializer _initializer;
    private readonly Action<bool>? _leaseZeroObserver;

    internal OpenRouterPremiumV2StateAnchorAdministration(
        IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend reader,
        IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend writer,
        IOpenRouterPremiumStateAnchorProvisioningLockSource lockSource,
        IOpenRouterPremiumStateAnchorKeyGenerator keyGenerator,
        IOpenRouterPremiumV2OfflineStateInitializer initializer,
        Action<bool>? leaseZeroObserver = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _lockSource = lockSource ?? throw new ArgumentNullException(nameof(lockSource));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        _leaseZeroObserver = leaseZeroObserver;
    }

    public static OpenRouterPremiumV2StateAnchorAdministration CreateDefault() => new(
        OpenRouterPremiumWindowsStateTrustAnchorNativeBackend.Instance,
        OpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend.Instance,
        OpenRouterPremiumStateAnchorProvisioningLockSource.Instance,
        RandomOpenRouterPremiumStateAnchorKeyGenerator.Instance,
        new OpenRouterPremiumV2OfflineStateInitializer());

    public OpenRouterPremiumV2StateAnchorProvisioningResult ProvisionOnceAndInitializeOffline()
    {
        using IDisposable provisioningLock = _lockSource.TryAcquire();
        RequireAbsentBeforeWrite();

        byte[]? generated = null;
        byte[]? readback = null;
        try
        {
            try { generated = _keyGenerator.GenerateOwned(); }
            catch
            {
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_generation_failed");
            }
            if (generated is not { Length: OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes })
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_generation_failed");

            try
            {
                _writer.WriteOnce(
                    OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.Fixed,
                    generated);
            }
            catch
            {
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");
            }

            try { readback = _reader.ReadExisting(
                OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity); }
            catch
            {
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");
            }
            if (readback is not { Length: OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes }
                || !CryptographicOperations.FixedTimeEquals(generated, readback))
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");

            try
            {
                using OpenRouterPremiumWindowsStateTrustAnchor anchor =
                    new(generated, _leaseZeroObserver);
                _initializer.Initialize(anchor);
                return new(anchor.IdentitySha256,
                    OpenRouterPremiumStateGenerationStore.StateContractDigestSha256,
                    StateRootPolicy,
                    OpenRouterCredentialRead: false,
                    ProviderAccess: false,
                    AdditionalAttemptAuthorized: false);
            }
            catch (OpenRouterPremiumProductionException exception)
                when (exception.Code == "state_anchor_provisioned_initialization_failed")
            {
                throw;
            }
            catch
            {
                throw new OpenRouterPremiumProductionException(
                    "state_anchor_provisioned_initialization_failed");
            }
        }
        finally
        {
            if (readback is not null) CryptographicOperations.ZeroMemory(readback);
            if (generated is not null) CryptographicOperations.ZeroMemory(generated);
        }
    }

    private void RequireAbsentBeforeWrite()
    {
        byte[]? existing = null;
        try
        {
            existing = _reader.ReadExisting(
                OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity);
            if (existing is not { Length: OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes })
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_invalid");
            throw new OpenRouterPremiumProductionException(
                "state_trust_anchor_already_provisioned");
        }
        catch (OpenRouterPremiumProductionException exception)
            when (exception.Code == "state_trust_anchor_missing")
        {
        }
        finally
        {
            if (existing is not null) CryptographicOperations.ZeroMemory(existing);
        }
    }
}
