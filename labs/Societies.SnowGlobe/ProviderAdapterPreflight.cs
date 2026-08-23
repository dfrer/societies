using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

/// <summary>Canonical identity for one registry-owned, fixed provider profile.</summary>
public sealed record ProviderProfileIdentity
{
    private const string Prefix = "provider-profile-sha256-";

    public ProviderProfileIdentity(string value)
    {
        if (value is null || value.Length != Prefix.Length + 64 || !value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Skip(Prefix.Length).Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("Provider profile identity is invalid.", nameof(value));
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

/// <summary>Caller-selected limits. A fixed profile supplies the upper bounds.</summary>
public sealed record ProviderExecutionBounds(
    int MaximumRequestBytes,
    int MaximumResponseBytes,
    int MaximumJsonDepth,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    int TimeoutMilliseconds,
    int LeaseLifetimeMilliseconds);

/// <summary>
/// Immutable fixture-only future transport contract. It is not a transport implementation.
/// A future Adapter must verify the exact effective URI and model, deny redirects, retries,
/// proxy, cookies and ambient authentication, bound reads and parser depth, classify submission
/// and charge conservatively, and dispose response ownership on every path.
/// </summary>
public sealed class FixedProviderProfile
{
    internal FixedProviderProfile(
        string schema,
        string scheme,
        string host,
        int port,
        string routePath,
        string providerIdentity,
        string modelIdentity,
        string modelRevisionIdentity,
        string promptRevisionIdentity,
        string outputSchemaIdentity,
        string accountAudienceIdentity,
        string authenticationSchemeIdentity,
        ProviderExecutionBounds limits)
    {
        Schema = schema;
        Scheme = scheme;
        Host = host;
        Port = port;
        RoutePath = routePath;
        EffectiveUri = $"{scheme}://{host}:{port}{routePath}";
        ProviderIdentity = providerIdentity;
        ModelIdentity = modelIdentity;
        ModelRevisionIdentity = modelRevisionIdentity;
        PromptRevisionIdentity = promptRevisionIdentity;
        OutputSchemaIdentity = outputSchemaIdentity;
        AccountAudienceIdentity = accountAudienceIdentity;
        AuthenticationSchemeIdentity = authenticationSchemeIdentity;
        Limits = limits;
        CanonicalDescriptor = string.Join('\n', new[]
        {
            schema, scheme, host, port.ToString(System.Globalization.CultureInfo.InvariantCulture), routePath, EffectiveUri,
            providerIdentity, modelIdentity, modelRevisionIdentity, promptRevisionIdentity, outputSchemaIdentity,
            accountAudienceIdentity, authenticationSchemeIdentity,
            limits.MaximumRequestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.MaximumResponseBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.MaximumJsonDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.MaximumInputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.MaximumOutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.TimeoutMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            limits.LeaseLifetimeMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "redirects=false", "automatic-retries=false", "proxy=false", "cookies=false", "ambient-auth=false",
            "exact-model=true", "exact-usage=true", "bounded-read=true", "response-disposal=true", "offline-fixture=true"
        });
        FullProfileDigest = ProviderPreflightCanonical.Digest(CanonicalDescriptor);
        Identity = new ProviderProfileIdentity("provider-profile-sha256-" + FullProfileDigest);
        Validate();
    }

    public string Schema { get; }
    public ProviderProfileIdentity Identity { get; }
    public string Scheme { get; }
    public string Host { get; }
    public int Port { get; }
    public string RoutePath { get; }
    public string EffectiveUri { get; }
    public string ProviderIdentity { get; }
    public string ModelIdentity { get; }
    public string ModelRevisionIdentity { get; }
    public string PromptRevisionIdentity { get; }
    public string OutputSchemaIdentity { get; }
    public string AccountAudienceIdentity { get; }
    public string AuthenticationSchemeIdentity { get; }
    public ProviderExecutionBounds Limits { get; }
    public bool RedirectsAllowed => false;
    public bool AutomaticRetriesAllowed => false;
    public bool ProxyAllowed => false;
    public bool CookiesAllowed => false;
    public bool AmbientAuthenticationAllowed => false;
    public bool ExactModelRequired => true;
    public bool ExactUsageRequired => true;
    public bool ResponseDisposalRequired => true;
    public bool IsOfflineFixture => true;
    public string CanonicalDescriptor { get; }
    public string FullProfileDigest { get; }

    private void Validate()
    {
        if (!string.Equals(Schema, "snow_globe_fixed_provider_profile/v1", StringComparison.Ordinal)
            || !string.Equals(Scheme, "https", StringComparison.Ordinal)
            || Port != 443
            || !string.Equals(Host, "snow-globe-provider.fixture.invalid", StringComparison.Ordinal)
            || !Host.EndsWith(".invalid", StringComparison.Ordinal)
            || !RoutePath.StartsWith("/", StringComparison.Ordinal)
            || RoutePath.Contains('?', StringComparison.Ordinal)
            || RoutePath.Contains('#', StringComparison.Ordinal)
            || !string.Equals(EffectiveUri, $"https://{Host}:443{RoutePath}", StringComparison.Ordinal)
            || !ProviderPreflightCanonical.IsIdentity(ProviderIdentity)
            || !ProviderPreflightCanonical.IsIdentity(ModelIdentity)
            || !ProviderPreflightCanonical.IsContentAddress(ModelRevisionIdentity)
            || !ProviderPreflightCanonical.IsIdentity(PromptRevisionIdentity)
            || !ProviderPreflightCanonical.IsIdentity(OutputSchemaIdentity)
            || !ProviderPreflightCanonical.IsIdentity(AccountAudienceIdentity)
            || !ProviderPreflightCanonical.IsIdentity(AuthenticationSchemeIdentity))
            throw new InvalidDataException("Fixed offline provider profile is invalid.");
        ProviderPreflightCanonical.ValidateBounds(Limits, Limits);
    }
}

/// <summary>Only approved offline fixtures can create a fixed profile.</summary>
public static class FixedProviderProfileRegistry
{
    private static readonly FixedProviderProfile Fixture = new(
        "snow_globe_fixed_provider_profile/v1",
        "https",
        "snow-globe-provider.fixture.invalid",
        443,
        "/v1/offline-proposals",
        "offline-fixture-provider/v1",
        "offline-fixture-model/v1",
        "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        "offline-prompt-revision/v1",
        "snow-globe-proposal/v1",
        "offline-fixture-account-audience/v1",
        "fixture-bearer-lease/v1",
        new ProviderExecutionBounds(16 * 1024, 16 * 1024, 8, 4096, 512, 30_000, 5_000));

    public static FixedProviderProfile ApprovedOfflineFixture => Fixture;

    public static FixedProviderProfile Resolve(ProviderProfileIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity != Fixture.Identity) throw new ProviderPreflightException(ProviderPreflightReasonCode.ProfileMismatch);
        return Fixture;
    }
}

/// <summary>No endpoint, route, transport header, authentication selector, model selector, pricing, or retry control crosses this Interface.</summary>
public sealed record ProviderPreflightAuthorization(
    ProviderProfileIdentity ProfileIdentity,
    string ModelPolicyDigest,
    string FinancialJournalChecksum,
    ByokAccountBindingIdentity AccountBinding,
    string JobDigest,
    ProviderExecutionBounds HardBounds,
    string AuthorizationNonce);

/// <summary>Exact, credential-free request passed to the external credential-lease port.</summary>
public sealed record CredentialLeaseRequest(
    ByokAccountBindingIdentity AccountBinding,
    string ProfileDigest,
    string AccountAudienceIdentity,
    string ScopeIdentity,
    string ModelPolicyDigest,
    string FinancialJournalChecksum,
    string JobDigest,
    string AuthorizationNonce,
    long IssuedAtMilliseconds,
    long ExpiresAtMilliseconds,
    int LifetimeMilliseconds,
    string RequestDigest);

public interface IProviderPreflightClock
{
    long NowMilliseconds { get; }
}

public sealed class OfflineProviderPreflightClock : IProviderPreflightClock
{
    private long _now;
    public OfflineProviderPreflightClock(long nowMilliseconds)
    {
        if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
        _now = nowMilliseconds;
    }
    public long NowMilliseconds => Interlocked.Read(ref _now);
    public void Advance(int milliseconds)
    {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        Interlocked.Add(ref _now, milliseconds);
    }
}

/// <summary>
/// True-external port. Implementations are trusted credential infrastructure: they may create an
/// owned lease, but must not retain, copy, stringify, or log material after ownership transfer.
/// Those implementation obligations require separate review; this Interface cannot enforce them.
/// The lab supplies only the reviewed offline fake Adapter.
/// </summary>
public interface ICredentialLeaseSource
{
    string Identity { get; }
    ValueTask<CredentialLease> AcquireOnceAsync(CredentialLeaseRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Owns one exact mutable buffer transferred by trusted credential infrastructure. No public member
/// reads material back after construction. The internal callback can observe the buffer, so the
/// runtime cannot prevent a future trusted Adapter from copying it; the reviewed fake/probe does not.
/// Every lease terminal or misuse path clears the exact lease-owned buffer.
/// </summary>
public sealed class CredentialLease : IDisposable
{
    public const int MaximumOwnedMaterialBytes = 16 * 1024;
    private readonly byte[] _ownedBuffer;
    private readonly long _expiresAtMilliseconds;
    private readonly Action<bool> _zeroObserver;
    private int _state;
    private int _zeroed;

    /// <summary>
    /// Transfers exclusive ownership of the exact mutable buffer. The trusted source must relinquish
    /// all use after this call and clean the buffer itself if construction fails.
    /// </summary>
    public CredentialLease(byte[] ownedBuffer, long expiresAtMilliseconds)
        : this(ownedBuffer, expiresAtMilliseconds, static _ => { }) { }

    internal CredentialLease(byte[] ownedBuffer, long expiresAtMilliseconds, Action<bool> zeroObserver)
    {
        ArgumentNullException.ThrowIfNull(ownedBuffer);
        if (ownedBuffer.Length is 0 or > MaximumOwnedMaterialBytes)
            throw new ArgumentOutOfRangeException(nameof(ownedBuffer));
        if (expiresAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
        _ownedBuffer = ownedBuffer;
        _expiresAtMilliseconds = expiresAtMilliseconds;
        _zeroObserver = zeroObserver ?? throw new ArgumentNullException(nameof(zeroObserver));
    }

    internal async ValueTask<T> ExecuteOnceAsync<T>(
        long nowMilliseconds,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            ZeroExactBuffer();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseMisuse);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nowMilliseconds >= _expiresAtMilliseconds)
                throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseExpired);
            if (_ownedBuffer.Length == 0)
                throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseInvalid);
            long remainingMilliseconds = _expiresAtMilliseconds - nowMilliseconds;
            using CancellationTokenSource leaseLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            leaseLifetime.CancelAfter(TimeSpan.FromMilliseconds(remainingMilliseconds));
            return await operation(_ownedBuffer.AsMemory(), leaseLifetime.Token).ConfigureAwait(false);
        }
        finally
        {
            ZeroExactBuffer();
            Interlocked.Exchange(ref _state, 2);
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _state, 2);
        ZeroExactBuffer();
    }

    private void ZeroExactBuffer()
    {
        CryptographicOperations.ZeroMemory(_ownedBuffer);
        if (Interlocked.Exchange(ref _zeroed, 1) == 0)
            _zeroObserver(_ownedBuffer.All(value => value == 0));
    }
}

public enum OfflineCredentialLeaseBehavior { Normal, Expired, DisposedBeforeReturn, AcquireException, CancelDuringAcquire }

/// <summary>Offline fake only. Its fixed bytes are an invalid fixture, never a real key or token.</summary>
public sealed class FakeCredentialLeaseSource : ICredentialLeaseSource
{
    public const string PrimaryIdentity = "offline-fake-lease-source/v1";
    public const string SecondaryIdentity = "offline-fake-lease-source-secondary/v1";
    private readonly OfflineCredentialLeaseBehavior _behavior;
    private int _calls;
    private int _zeroObservations;
    private int _lastLeaseZeroed;
    private CredentialLeaseRequest? _lastRequest;
    private readonly object _nonceGate = new();
    private readonly HashSet<string> _consumedNonces = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _acquisitionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeCredentialLeaseSource(string identity = PrimaryIdentity, OfflineCredentialLeaseBehavior behavior = OfflineCredentialLeaseBehavior.Normal)
    {
        if (!ProviderPreflightCanonical.IsIdentity(identity)) throw new ArgumentException("Lease source identity is invalid.", nameof(identity));
        Identity = identity;
        _behavior = behavior;
    }

    public string Identity { get; }
    public int CallCount => Volatile.Read(ref _calls);
    public int ZeroObservationCount => Volatile.Read(ref _zeroObservations);
    public bool LastLeaseZeroed => Volatile.Read(ref _lastLeaseZeroed) == 1;
    public CredentialLeaseRequest? LastRequest => Volatile.Read(ref _lastRequest);
    public Task AcquisitionStarted => _acquisitionStarted.Task;

    public async ValueTask<CredentialLease> AcquireOnceAsync(CredentialLeaseRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _calls);
        Volatile.Write(ref _lastRequest, request with { });
        lock (_nonceGate)
        {
            if (!_consumedNonces.Add(request.AuthorizationNonce))
                throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseMisuse);
        }
        // Non-text fixture bytes avoid any credential-to-string conversion.
        byte[] owned = { 0xF0, 0x0D, 0xBA, 0xAD, 0xC0, 0xDE, 0xA5, 0x5A, 0x11, 0x22, 0x33, 0x44 };
        _acquisitionStarted.TrySetResult();
        if (_behavior == OfflineCredentialLeaseBehavior.AcquireException)
        {
            ZeroSourceOwnedBuffer(owned);
            throw new InvalidOperationException("offline_fake_acquire_failure");
        }
        if (_behavior == OfflineCredentialLeaseBehavior.CancelDuringAcquire)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                ZeroSourceOwnedBuffer(owned);
                throw;
            }
        }

        long expiry = _behavior == OfflineCredentialLeaseBehavior.Expired
            ? request.IssuedAtMilliseconds
            : request.ExpiresAtMilliseconds;
        CredentialLease lease = new(owned, expiry, zeroed =>
        {
            ObserveZeroing(zeroed);
        });
        if (_behavior == OfflineCredentialLeaseBehavior.DisposedBeforeReturn) lease.Dispose();
        return lease;
    }

    private void ZeroSourceOwnedBuffer(byte[] owned)
    {
        CryptographicOperations.ZeroMemory(owned);
        ObserveZeroing(owned.All(value => value == 0));
    }

    private void ObserveZeroing(bool zeroed)
    {
        Interlocked.Increment(ref _zeroObservations);
        Volatile.Write(ref _lastLeaseZeroed, zeroed ? 1 : 0);
    }
}

public enum ProviderPreflightReasonCode
{
    InvalidIdentity,
    InvalidDigest,
    InvalidBounds,
    ProfileMismatch,
    BindingMismatch,
    LeaseSourceMismatch,
    CapabilityReused,
    CapabilityExpired,
    Cancelled,
    LeaseAcquisitionFailed,
    LeaseExpired,
    LeaseInvalid,
    LeaseMisuse,
    ProbeFailed
}

public sealed class ProviderPreflightException : Exception
{
    public ProviderPreflightException(ProviderPreflightReasonCode reasonCode) : base(reasonCode.ToString()) => ReasonCode = reasonCode;
    public ProviderPreflightReasonCode ReasonCode { get; }
}

/// <summary>Immutable, single-use offline capability. It is neither network authorization nor a dispatch method.</summary>
public sealed class ProviderExecutionCapability
{
    private int _state;
    internal ProviderExecutionCapability(ProviderPreflightAuthorization authorization, FixedProviderProfile profile, ICredentialLeaseSource leaseSource, IProviderPreflightClock clock, long issuedAt, long expiresAt)
    {
        Authorization = authorization with { HardBounds = authorization.HardBounds with { } };
        Profile = profile;
        LeaseSource = leaseSource;
        Clock = clock;
        LeaseSourceIdentity = leaseSource.Identity;
        IssuedAtMilliseconds = issuedAt;
        ExpiresAtMilliseconds = expiresAt;
        CapabilityDigest = ProviderPreflightCanonical.Digest(string.Join('|',
            authorization.ProfileIdentity.Value, profile.FullProfileDigest, authorization.ModelPolicyDigest,
            authorization.FinancialJournalChecksum, authorization.AccountBinding.Value, authorization.JobDigest,
            ProviderPreflightCanonical.Bounds(authorization.HardBounds), authorization.AuthorizationNonce,
            LeaseSourceIdentity, issuedAt, expiresAt));
    }

    internal ProviderPreflightAuthorization Authorization { get; }
    internal FixedProviderProfile Profile { get; }
    internal ICredentialLeaseSource LeaseSource { get; }
    internal IProviderPreflightClock Clock { get; }
    internal string LeaseSourceIdentity { get; }
    internal bool TryConsume() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
    internal void Invalidate() => Interlocked.CompareExchange(ref _state, 1, 0);
    public ProviderProfileIdentity ProfileIdentity => Authorization.ProfileIdentity;
    public string CapabilityDigest { get; }
    public long IssuedAtMilliseconds { get; }
    public long ExpiresAtMilliseconds { get; }
    public bool IsNetworkAuthorization => false;
    public bool CanDispatch => false;
}

public enum OfflineProviderProbeBehavior
{
    Success,
    CallbackException,
    CallbackCancellation,
    ReentrantLeaseUse
}

public enum ProviderProbeOutcomeCode { OfflineSuccess }

public enum ProviderSubmissionClassification { ResponseReceived, DefinitelyNotSubmitted, SubmissionUnknown }
public enum ProviderChargeClassification { OfflineNotApplicable, Unknown }

/// <summary>Detached metrics-only fake evidence. The proposal remains untrusted and has no world authority.</summary>
public sealed record ProviderExecutionEvidence(
    string CapabilityDigest,
    ProviderProfileIdentity ProfileIdentity,
    string ProfileDigest,
    string ModelPolicyDigest,
    string FinancialJournalChecksum,
    ByokAccountBindingIdentity AccountBinding,
    string JobDigest,
    string LeaseSourceIdentity,
    ProviderExecutionBounds Bounds,
    ProviderProbeOutcomeCode OutcomeCode,
    ProviderSubmissionClassification SubmissionClassification,
    ProviderChargeClassification ChargeClassification,
    int RequestBytes,
    int ResponseBytes,
    int JsonDepth,
    int InputTokens,
    int OutputTokens,
    bool ResponseDisposed,
    bool HasWorldAuthority,
    SnowGlobeActionProposal? Proposal);

public static class ProviderAdapterPreflight
{
    public const int MinimumRequestBytes = 128;
    public const int MinimumResponseBytes = 128;
    public const int MinimumJsonDepth = 2;
    public const int MinimumInputTokens = 16;
    public const int MinimumOutputTokens = 8;

    public static ProviderExecutionCapability Authorize(
        ProviderPreflightAuthorization authorization,
        ICredentialLeaseSource leaseSource,
        IProviderPreflightClock clock)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(leaseSource);
        ArgumentNullException.ThrowIfNull(clock);
        FixedProviderProfile profile = FixedProviderProfileRegistry.Resolve(authorization.ProfileIdentity);
        ProviderPreflightCanonical.ValidateAuthorization(authorization, profile);
        if (!ProviderPreflightCanonical.IsIdentity(leaseSource.Identity))
            throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidIdentity);
        long issuedAt = clock.NowMilliseconds;
        long expiresAt;
        try { expiresAt = checked(issuedAt + authorization.HardBounds.LeaseLifetimeMilliseconds); }
        catch (OverflowException) { throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidBounds); }
        return new ProviderExecutionCapability(authorization, profile, leaseSource, clock, issuedAt, expiresAt);
    }
}

/// <summary>The only capability consumer in this slice; it performs no I/O and never returns credential-derived data.</summary>
public sealed class FakeProviderExecutionProbe
{
    private readonly OfflineProviderProbeBehavior _behavior;
    private int _calls;

    public FakeProviderExecutionProbe(OfflineProviderProbeBehavior behavior = OfflineProviderProbeBehavior.Success) => _behavior = behavior;
    public int CallCount => Volatile.Read(ref _calls);

    public async ValueTask<ProviderExecutionEvidence> ExecuteOnceAsync(
        ProviderExecutionCapability capability,
        ProviderPreflightAuthorization expectedAuthorization,
        ICredentialLeaseSource leaseSource,
        IProviderPreflightClock clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(expectedAuthorization);
        ArgumentNullException.ThrowIfNull(leaseSource);
        ArgumentNullException.ThrowIfNull(clock);
        ProviderPreflightCanonical.ValidateAuthorization(expectedAuthorization, capability.Profile);

        if (!expectedAuthorization.Equals(capability.Authorization))
        {
            capability.Invalidate();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.BindingMismatch);
        }
        if (!ReferenceEquals(leaseSource, capability.LeaseSource)
            || !string.Equals(leaseSource.Identity, capability.LeaseSourceIdentity, StringComparison.Ordinal))
        {
            capability.Invalidate();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseSourceMismatch);
        }
        if (!ReferenceEquals(clock, capability.Clock))
        {
            capability.Invalidate();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.BindingMismatch);
        }
        if (cancellationToken.IsCancellationRequested)
        {
            capability.Invalidate();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.Cancelled);
        }
        if (clock.NowMilliseconds >= capability.ExpiresAtMilliseconds)
        {
            capability.Invalidate();
            throw new ProviderPreflightException(ProviderPreflightReasonCode.CapabilityExpired);
        }
        if (!capability.TryConsume())
            throw new ProviderPreflightException(ProviderPreflightReasonCode.CapabilityReused);

        Interlocked.Increment(ref _calls);
        CredentialLeaseRequest request = CreateLeaseRequest(capability);
        CredentialLease? lease = null;
        try
        {
            try { lease = await leaseSource.AcquireOnceAsync(request, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw new ProviderPreflightException(ProviderPreflightReasonCode.Cancelled); }
            catch (ProviderPreflightException) { throw; }
            catch { throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseAcquisitionFailed); }

            return await lease.ExecuteOnceAsync(clock.NowMilliseconds, (material, token) =>
                ExecuteFixtureAsync(capability, lease, material, token), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new ProviderPreflightException(ProviderPreflightReasonCode.Cancelled);
        }
        catch (ProviderPreflightException)
        {
            throw;
        }
        catch
        {
            throw new ProviderPreflightException(ProviderPreflightReasonCode.ProbeFailed);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private ValueTask<ProviderExecutionEvidence> ExecuteFixtureAsync(
        ProviderExecutionCapability capability,
        CredentialLease lease,
        ReadOnlyMemory<byte> material,
        CancellationToken cancellationToken)
    {
        if (material.Length == 0) throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseInvalid);
        if (_behavior == OfflineProviderProbeBehavior.CallbackException) throw new InvalidOperationException("offline_fake_callback_failure");
        if (_behavior == OfflineProviderProbeBehavior.CallbackCancellation) throw new OperationCanceledException(cancellationToken);
        if (_behavior == OfflineProviderProbeBehavior.ReentrantLeaseUse)
            return ReenterAsync(capability, lease, cancellationToken);
        return ValueTask.FromResult(CreateEvidence(capability));
    }

    private async ValueTask<ProviderExecutionEvidence> ReenterAsync(ProviderExecutionCapability capability, CredentialLease lease, CancellationToken token)
    {
        await lease.ExecuteOnceAsync(capability.IssuedAtMilliseconds, (_, _) => ValueTask.FromResult(CreateEvidence(capability)), token).ConfigureAwait(false);
        throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseMisuse);
    }

    private ProviderExecutionEvidence CreateEvidence(ProviderExecutionCapability capability)
    {
        ProviderExecutionBounds bounds = capability.Authorization.HardBounds;
        if (_behavior != OfflineProviderProbeBehavior.Success)
            throw new ProviderPreflightException(ProviderPreflightReasonCode.ProbeFailed);
        SnowGlobeActionProposal proposal = new("agent-00", SnowGlobeActionKind.Idle);
        return new ProviderExecutionEvidence(
            capability.CapabilityDigest, capability.ProfileIdentity, capability.Profile.FullProfileDigest,
            capability.Authorization.ModelPolicyDigest, capability.Authorization.FinancialJournalChecksum,
            capability.Authorization.AccountBinding, capability.Authorization.JobDigest, capability.LeaseSourceIdentity,
            bounds with { }, ProviderProbeOutcomeCode.OfflineSuccess, ProviderSubmissionClassification.ResponseReceived,
            ProviderChargeClassification.OfflineNotApplicable, 128, 128, 2,
            Math.Min(16, bounds.MaximumInputTokens), Math.Min(8, bounds.MaximumOutputTokens), true, false,
            proposal with { });
    }

    private static CredentialLeaseRequest CreateLeaseRequest(ProviderExecutionCapability capability)
    {
        ProviderPreflightAuthorization authorization = capability.Authorization;
        string scope = "offline-fixture-proposal-once/v1";
        string digest = ProviderPreflightCanonical.Digest(string.Join('|',
            authorization.AccountBinding.Value, capability.Profile.FullProfileDigest,
            capability.Profile.AccountAudienceIdentity, scope, authorization.ModelPolicyDigest,
            authorization.FinancialJournalChecksum, authorization.JobDigest, authorization.AuthorizationNonce,
            capability.IssuedAtMilliseconds, capability.ExpiresAtMilliseconds,
            authorization.HardBounds.LeaseLifetimeMilliseconds));
        return new CredentialLeaseRequest(authorization.AccountBinding, capability.Profile.FullProfileDigest,
            capability.Profile.AccountAudienceIdentity, scope, authorization.ModelPolicyDigest,
            authorization.FinancialJournalChecksum, authorization.JobDigest, authorization.AuthorizationNonce,
            capability.IssuedAtMilliseconds, capability.ExpiresAtMilliseconds,
            authorization.HardBounds.LeaseLifetimeMilliseconds, digest);
    }
}

internal static class ProviderPreflightCanonical
{
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    internal static bool IsContentAddress(string? value) => value is { Length: 71 }
        && value.StartsWith("sha256-", StringComparison.Ordinal) && IsDigest(value[7..]);

    internal static bool IsIdentity(string? value) => value is { Length: > 0 and <= 96 }
        && value.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '/' or '.');

    internal static string Bounds(ProviderExecutionBounds value) => string.Join(',',
        value.MaximumRequestBytes, value.MaximumResponseBytes, value.MaximumJsonDepth,
        value.MaximumInputTokens, value.MaximumOutputTokens, value.TimeoutMilliseconds, value.LeaseLifetimeMilliseconds);

    internal static void ValidateAuthorization(ProviderPreflightAuthorization value, FixedProviderProfile profile)
    {
        if (value.ProfileIdentity is null || value.AccountBinding is null || value.HardBounds is null)
            throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidIdentity);
        if (value.ProfileIdentity != profile.Identity)
            throw new ProviderPreflightException(ProviderPreflightReasonCode.ProfileMismatch);
        if (!IsDigest(value.ModelPolicyDigest) || !IsDigest(value.FinancialJournalChecksum) || !IsDigest(value.JobDigest))
            throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidDigest);
        if (!IsIdentity(value.AuthorizationNonce))
            throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidIdentity);
        ValidateBounds(value.HardBounds, profile.Limits);
    }

    internal static void ValidateBounds(ProviderExecutionBounds value, ProviderExecutionBounds maximum)
    {
        if (value.MaximumRequestBytes < ProviderAdapterPreflight.MinimumRequestBytes || value.MaximumRequestBytes > maximum.MaximumRequestBytes
            || value.MaximumResponseBytes < ProviderAdapterPreflight.MinimumResponseBytes || value.MaximumResponseBytes > maximum.MaximumResponseBytes
            || value.MaximumJsonDepth < ProviderAdapterPreflight.MinimumJsonDepth || value.MaximumJsonDepth > maximum.MaximumJsonDepth
            || value.MaximumInputTokens < ProviderAdapterPreflight.MinimumInputTokens || value.MaximumInputTokens > maximum.MaximumInputTokens
            || value.MaximumOutputTokens < ProviderAdapterPreflight.MinimumOutputTokens || value.MaximumOutputTokens > maximum.MaximumOutputTokens
            || value.TimeoutMilliseconds <= 0 || value.TimeoutMilliseconds > maximum.TimeoutMilliseconds
            || value.LeaseLifetimeMilliseconds <= 0 || value.LeaseLifetimeMilliseconds > maximum.LeaseLifetimeMilliseconds)
            throw new ProviderPreflightException(ProviderPreflightReasonCode.InvalidBounds);
    }
}
