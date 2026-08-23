using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Societies.SnowGlobe;

public sealed class OpenRouterPremiumProductionException : Exception
{
    public OpenRouterPremiumProductionException(string code) : base(Validate(code)) => Code = code;
    public string Code { get; }

    private static string Validate(string value)
    {
        if (!OpenRouterPremiumCanonical.IsIdentity(value) || value.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
}

public sealed record OpenRouterPremiumProductionPreflightResult(
    string AuthorizationDigestSha256,
    string PreflightArtifactDigestSha256,
    string AccountBindingIdentity,
    long ExpiresAtUnixMilliseconds,
    int MaximumRequests,
    long AggregateCostCeilingMicrousd);

public sealed record OpenRouterPremiumProductionRunResult(
    string Status,
    int ExchangeCount,
    long TotalSettledMicrousd,
    string? TerminalCode,
    string EvidenceArtifactDigestSha256);

public sealed record OpenRouterPremiumProductionValidationResult(
    string Status,
    int ExchangeCount,
    long TotalSettledMicrousd,
    string EvidenceArtifactDigestSha256,
    string ValidationReceiptDigestSha256);

internal interface IOpenRouterPremiumProductionClock : IOpenRouterPremiumClock
{
}

internal sealed class SystemOpenRouterPremiumProductionClock : IOpenRouterPremiumProductionClock
{
    internal static SystemOpenRouterPremiumProductionClock Instance { get; } = new();
    private SystemOpenRouterPremiumProductionClock() { }
    public long NowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

internal interface IOpenRouterPremiumProductionProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(ReadOnlySpan<byte> ciphertext);
}

internal interface IOpenRouterPremiumProductionMetadataVerifier
{
    ValueTask<OpenRouterPremiumVerifiedMetadata> VerifyOnceAsync(CancellationToken cancellationToken);
}

internal sealed class OpenRouterPremiumVerifiedMetadata : IDisposable
{
    private byte[] _ownedCanonicalBundle;
    private int _state;

    internal OpenRouterPremiumVerifiedMetadata(byte[] ownedCanonicalBundle)
    {
        ArgumentNullException.ThrowIfNull(ownedCanonicalBundle);
        if (ownedCanonicalBundle.Length is < 1 or > 16 * 1024)
            throw new OpenRouterPremiumProductionException("metadata_evidence_invalid");
        _ownedCanonicalBundle = ownedCanonicalBundle;
    }

    internal byte[] TransferOwnedCanonicalBundle()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new OpenRouterPremiumProductionException("metadata_evidence_consumed");
        byte[] result = _ownedCanonicalBundle;
        _ownedCanonicalBundle = [];
        return result;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, 2) == 2) return;
        CryptographicOperations.ZeroMemory(_ownedCanonicalBundle);
        _ownedCanonicalBundle = [];
    }
}

/// <summary>
/// Fail-closed production authority. The frozen repository evidence has no reviewed official
/// authenticated endpoint whose response binds an OpenRouter credential to a stable account
/// subject. It therefore performs no metadata traffic and cannot issue an activation bundle.
/// </summary>
internal sealed class OpenRouterPremiumUnavailableAccountMetadataVerifier : IOpenRouterPremiumProductionMetadataVerifier
{
    internal static OpenRouterPremiumUnavailableAccountMetadataVerifier Instance { get; } = new();
    private OpenRouterPremiumUnavailableAccountMetadataVerifier() { }
    public ValueTask<OpenRouterPremiumVerifiedMetadata> VerifyOnceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new OpenRouterPremiumProductionException("account_subject_endpoint_unavailable");
    }
}

internal interface IOpenRouterPremiumCredentialStore
{
    void Write(string accountBindingIdentity, byte[] secretMaterial);
    OpenRouterPremiumStoredCredential Read();
    void BindAccount(string derivedAccountBindingIdentity);
    void Delete();
}

internal sealed class OpenRouterPremiumStoredCredential : IDisposable
{
    private byte[] _ownedMaterial;
    private readonly Action<bool>? _zeroObserver;
    private int _state;

    internal OpenRouterPremiumStoredCredential(string accountBindingIdentity, byte[] ownedMaterial, Action<bool>? zeroObserver = null)
    {
        if (accountBindingIdentity != OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity)
            _ = new ByokAccountBindingIdentity(accountBindingIdentity);
        ArgumentNullException.ThrowIfNull(ownedMaterial);
        AccountBindingIdentity = accountBindingIdentity;
        _ownedMaterial = ownedMaterial;
        _zeroObserver = zeroObserver;
    }

    internal string AccountBindingIdentity { get; }
    internal Action<bool> ZeroObserver => _zeroObserver ?? (static _ => { });

    internal byte[] TransferOwnedMaterial()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new OpenRouterPremiumProductionException("credential_record_consumed");
        byte[] material = _ownedMaterial;
        _ownedMaterial = [];
        return material;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, 2) == 2) return;
        if (_ownedMaterial.Length > 0)
        {
            CryptographicOperations.ZeroMemory(_ownedMaterial);
            _zeroObserver?.Invoke(_ownedMaterial.All(static value => value == 0));
        }
        _ownedMaterial = [];
    }
}

internal sealed class OpenRouterPremiumWindowsCredentialStore : IOpenRouterPremiumCredentialStore
{
    internal const string TargetIdentity = "Societies/SnowGlobe/OpenRouterPremium/openrouter-api-account/v1";
    internal const string PendingAccountBindingIdentity = "pending-account-binding/openrouter/v1";
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int NotFound = 1168;

    public void Write(string accountBindingIdentity, byte[] secretMaterial)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        if (accountBindingIdentity != PendingAccountBindingIdentity)
            _ = new ByokAccountBindingIdentity(accountBindingIdentity);
        ArgumentNullException.ThrowIfNull(secretMaterial);
        if (!OpenRouterPremiumCredentialMaterial.IsValid(secretMaterial))
            throw new OpenRouterPremiumProductionException("credential_malformed");
        GCHandle pinned = GCHandle.Alloc(secretMaterial, GCHandleType.Pinned);
        try
        {
            NativeCredential value = new()
            {
                Type = GenericCredential,
                TargetName = TargetIdentity,
                CredentialBlobSize = checked((uint)secretMaterial.Length),
                CredentialBlob = pinned.AddrOfPinnedObject(),
                Persist = PersistLocalMachine,
                UserName = accountBindingIdentity
            };
            if (!CredWriteW(ref value, 0)) throw Last("credential_store_write_failed");
        }
        finally { pinned.Free(); }
    }

    public OpenRouterPremiumStoredCredential Read()
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        if (!CredReadW(TargetIdentity, GenericCredential, 0, out IntPtr pointer))
        {
            if (Marshal.GetLastWin32Error() == NotFound)
                throw new OpenRouterPremiumProductionException("credential_missing");
            throw Last("credential_store_read_failed");
        }
        try
        {
            NativeCredential value = Marshal.PtrToStructure<NativeCredential>(pointer);
            string account = value.UserName ?? string.Empty;
            if (value.CredentialBlob == IntPtr.Zero || value.CredentialBlobSize is 0 or > 512)
                throw new OpenRouterPremiumProductionException("credential_malformed");
            return CopyOwnedCredential(account, value.CredentialBlob, checked((int)value.CredentialBlobSize));
        }
        finally { CredFree(pointer); }
    }

    internal static OpenRouterPremiumStoredCredential CopyOwnedCredential(
        string accountBindingIdentity,
        IntPtr source,
        int length,
        Action<IntPtr, byte[], int>? copy = null)
    {
        if (source == IntPtr.Zero || length is < 1 or > 512)
            throw new OpenRouterPremiumProductionException("credential_malformed");

        byte[] owned = new byte[length];
        try
        {
            if (copy is null) Marshal.Copy(source, owned, 0, owned.Length);
            else copy(source, owned, owned.Length);
            return new OpenRouterPremiumStoredCredential(accountBindingIdentity, owned);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(owned);
            throw;
        }
    }

    public void BindAccount(string derivedAccountBindingIdentity)
    {
        _ = new ByokAccountBindingIdentity(derivedAccountBindingIdentity);
        using OpenRouterPremiumStoredCredential credential = Read();
        if (credential.AccountBindingIdentity != PendingAccountBindingIdentity
            && credential.AccountBindingIdentity != derivedAccountBindingIdentity)
            throw new OpenRouterPremiumProductionException("credential_account_mismatch");
        byte[] material = credential.TransferOwnedMaterial();
        try { Write(derivedAccountBindingIdentity, material); }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    public void Delete()
    {
        using OpenRouterPremiumStoredCredential credential = Read();
        if (!CredDeleteW(TargetIdentity, GenericCredential, 0)) throw Last("credential_store_delete_failed");
    }

    private static OpenRouterPremiumProductionException Last(string code)
    {
        _ = new Win32Exception(Marshal.GetLastWin32Error());
        return new OpenRouterPremiumProductionException(code);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal IntPtr CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

internal interface IOpenRouterPremiumDpapiPin : IDisposable
{
    IntPtr Address { get; }
}

internal readonly record struct OpenRouterPremiumDpapiNativeResult(bool Success, IntPtr Data, int Size);

internal interface IOpenRouterPremiumDpapiOperations
{
    IOpenRouterPremiumDpapiPin Pin(byte[] buffer);
    OpenRouterPremiumDpapiNativeResult Transform(
        bool protect, IntPtr input, int inputLength, IntPtr entropy, int entropyLength);
    void Copy(IntPtr source, byte[] destination, int length);
    void ZeroAndFree(IntPtr source, int length);
}

internal sealed class OpenRouterPremiumDpapiProtector : IOpenRouterPremiumProductionProtector
{
    private const uint UiForbidden = 0x1;
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.ASCII.GetBytes(
        "Societies/SnowGlobe/OpenRouterPremium/runtime-authorization/v1"));
    private static readonly IOpenRouterPremiumDpapiOperations ProductionOperations = new NativeDpapiOperations();

    public byte[] Protect(ReadOnlySpan<byte> plaintext) => Transform(plaintext, protect: true);
    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext) => Transform(ciphertext, protect: false);

    private static byte[] Transform(ReadOnlySpan<byte> input, bool protect) =>
        Transform(input, protect, ProductionOperations);

    internal static byte[] TransformForOfflineTests(
        ReadOnlySpan<byte> input, bool protect, IOpenRouterPremiumDpapiOperations operations) =>
        Transform(input, protect, operations);

    private static byte[] Transform(
        ReadOnlySpan<byte> input, bool protect, IOpenRouterPremiumDpapiOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        if (input.Length is < 1 or > 32 * 1024)
            throw new OpenRouterPremiumProductionException("authorization_protection_failed");
        byte[]? owned = null;
        byte[]? output = null;
        IOpenRouterPremiumDpapiPin? inputPin = null;
        IOpenRouterPremiumDpapiPin? entropyPin = null;
        OpenRouterPremiumDpapiNativeResult native = default;
        try
        {
            owned = input.ToArray();
            inputPin = operations.Pin(owned);
            entropyPin = operations.Pin(Entropy);
            native = operations.Transform(protect, inputPin.Address, owned.Length, entropyPin.Address, Entropy.Length);
            if (!native.Success || native.Data == IntPtr.Zero || native.Size is < 1 or > 64 * 1024)
                throw new OpenRouterPremiumProductionException("authorization_protection_failed");
            output = new byte[native.Size];
            operations.Copy(native.Data, output, output.Length);
            byte[] transferred = output;
            output = null;
            return transferred;
        }
        finally
        {
            if (output is not null) CryptographicOperations.ZeroMemory(output);
            if (owned is not null) CryptographicOperations.ZeroMemory(owned);
            try
            {
                entropyPin?.Dispose();
            }
            finally
            {
                try { inputPin?.Dispose(); }
                finally
                {
                    if (native.Data != IntPtr.Zero) operations.ZeroAndFree(native.Data, native.Size);
                }
            }
        }
    }

    private sealed class NativeDpapiPin : IOpenRouterPremiumDpapiPin
    {
        private GCHandle _handle;
        private NativeDpapiPin(GCHandle handle) => _handle = handle;

        internal static NativeDpapiPin Create(byte[] buffer)
        {
            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                return new NativeDpapiPin(handle);
            }
            catch
            {
                if (handle.IsAllocated) handle.Free();
                throw;
            }
        }

        public IntPtr Address => _handle.IsAllocated
            ? _handle.AddrOfPinnedObject()
            : throw new ObjectDisposedException(nameof(NativeDpapiPin));

        public void Dispose()
        {
            if (_handle.IsAllocated) _handle.Free();
        }
    }

    private sealed class NativeDpapiOperations : IOpenRouterPremiumDpapiOperations
    {
        public IOpenRouterPremiumDpapiPin Pin(byte[] buffer) => NativeDpapiPin.Create(buffer);

        public OpenRouterPremiumDpapiNativeResult Transform(
            bool protect, IntPtr input, int inputLength, IntPtr entropy, int entropyLength)
        {
            DataBlob inputBlob = new() { Size = inputLength, Data = input };
            DataBlob entropyBlob = new() { Size = entropyLength, Data = entropy };
            DataBlob outputBlob = default;
            try
            {
                bool success = protect
                    ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, out outputBlob)
                    : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, out outputBlob);
                return new OpenRouterPremiumDpapiNativeResult(success, outputBlob.Data, outputBlob.Size);
            }
            catch
            {
                if (outputBlob.Data != IntPtr.Zero) ZeroAndFree(outputBlob.Data, outputBlob.Size);
                throw;
            }
        }

        public void Copy(IntPtr source, byte[] destination, int length) =>
            Marshal.Copy(source, destination, 0, length);

        public void ZeroAndFree(IntPtr source, int length)
        {
            try { RtlZeroMemory(source, (nuint)Math.Max(0, length)); }
            finally { _ = LocalFree(source); }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { internal int Size; internal IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
    private static extern void RtlZeroMemory(IntPtr destination, nuint length);
}

internal static class OpenRouterPremiumCredentialMaterial
{
    internal static bool IsValid(ReadOnlySpan<byte> value)
    {
        if (value.Length is < 32 or > 512 || !value.StartsWith("sk-or-v1-"u8)) return false;
        foreach (byte current in value)
            if (!(current is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z'
                or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'_' or (byte)'.')) return false;
        return true;
    }
}

internal sealed class OpenRouterPremiumProductionLeaseSource : ICredentialLeaseSource
{
    private readonly IOpenRouterPremiumCredentialStore _store;
    private readonly string _account;
    private readonly string _capabilityDigest;
    private readonly string _journalChecksum;
    private readonly long _authorizationIssuedAt;
    private readonly long _authorizationExpiresAt;
    private readonly HashSet<string> _nonces = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    internal OpenRouterPremiumProductionLeaseSource(IOpenRouterPremiumCredentialStore store, string account,
        string capabilityDigest, string journalChecksum, long authorizationIssuedAt, long authorizationExpiresAt)
    {
        _store = store;
        _account = account;
        _capabilityDigest = capabilityDigest;
        _journalChecksum = journalChecksum;
        _authorizationIssuedAt = authorizationIssuedAt;
        _authorizationExpiresAt = authorizationExpiresAt;
    }

    public string Identity => OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity;

    public ValueTask<CredentialLease> AcquireOnceAsync(CredentialLeaseRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.AccountBinding.Value != _account
            || request.ProfileDigest != OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256
            || request.AccountAudienceIdentity != "openrouter-api-account/v1"
            || request.ScopeIdentity != "openrouter.chat.completions"
            || request.ModelPolicyDigest != _capabilityDigest
            || request.FinancialJournalChecksum != _journalChecksum
            || request.LifetimeMilliseconds != OpenRouterPremiumProfile.CredentialLeaseLifetimeMilliseconds
            || request.ExpiresAtMilliseconds - request.IssuedAtMilliseconds != OpenRouterPremiumProfile.CredentialLeaseLifetimeMilliseconds
            || request.IssuedAtMilliseconds < _authorizationIssuedAt
            || request.ExpiresAtMilliseconds > _authorizationExpiresAt
            || !request.AuthorizationNonce.StartsWith("openrouter-runtime-", StringComparison.Ordinal)
            || request.JobDigest is not { Length: 64 })
            throw new ProviderPreflightException(ProviderPreflightReasonCode.BindingMismatch);
        string expected = OpenRouterPremiumCanonical.Digest(string.Join('|', request.AccountBinding.Value,
            request.ProfileDigest, request.AccountAudienceIdentity, request.ScopeIdentity, request.ModelPolicyDigest,
            request.FinancialJournalChecksum, request.JobDigest, request.AuthorizationNonce,
            request.IssuedAtMilliseconds, request.ExpiresAtMilliseconds, request.LifetimeMilliseconds));
        if (request.RequestDigest != expected)
            throw new ProviderPreflightException(ProviderPreflightReasonCode.BindingMismatch);
        lock (_gate) if (!_nonces.Add(request.AuthorizationNonce))
            throw new ProviderPreflightException(ProviderPreflightReasonCode.LeaseMisuse);

        OpenRouterPremiumStoredCredential? credential = null;
        byte[]? material = null;
        try
        {
            credential = _store.Read();
            if (credential.AccountBindingIdentity != _account)
                throw new OpenRouterPremiumProductionException("credential_account_mismatch");
            material = credential.TransferOwnedMaterial();
            if (!OpenRouterPremiumCredentialMaterial.IsValid(material))
                throw new OpenRouterPremiumProductionException("credential_malformed");
            CredentialLease lease = new(material, request.ExpiresAtMilliseconds, credential.ZeroObserver);
            material = null;
            return ValueTask.FromResult(lease);
        }
        finally
        {
            if (material is not null) CryptographicOperations.ZeroMemory(material);
            credential?.Dispose();
        }
    }
}

internal sealed record OpenRouterPremiumRuntimeAuthorization(
    string SchemaVersion,
    string ProfileDigestSha256,
    string CatalogEvidenceDigestSha256,
    string EndpointEvidenceDigestSha256,
    string AccountBindingIdentity,
    string CredentialTargetIdentity,
    string CredentialSourceIdentity,
    string JournalIdentity,
    string JournalHeaderChecksumSha256,
    string PreflightArtifactDigestSha256,
    string PreflightPayloadDigestSha256,
    string PreflightConsumptionDigestSha256,
    string CapabilityDigestSha256,
    string RuntimeNonce,
    long IssuedAtUnixMilliseconds,
    long ExpiresAtUnixMilliseconds,
    int MaximumRequests,
    long AggregateCostCeilingMicrousd)
{
    internal const string CurrentSchemaVersion = "snow_globe_openrouter_production_runtime_authorization/v1";

    internal byte[] Write()
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject(); writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("profile_digest_sha256", ProfileDigestSha256); writer.WriteString("catalog_evidence_digest_sha256", CatalogEvidenceDigestSha256);
        writer.WriteString("endpoint_evidence_digest_sha256", EndpointEvidenceDigestSha256); writer.WriteString("account_binding_identity", AccountBindingIdentity);
        writer.WriteString("credential_target_identity", CredentialTargetIdentity); writer.WriteString("credential_source_identity", CredentialSourceIdentity);
        writer.WriteString("journal_identity", JournalIdentity); writer.WriteString("journal_header_checksum_sha256", JournalHeaderChecksumSha256);
        writer.WriteString("preflight_artifact_digest_sha256", PreflightArtifactDigestSha256); writer.WriteString("preflight_payload_digest_sha256", PreflightPayloadDigestSha256);
        writer.WriteString("preflight_consumption_digest_sha256", PreflightConsumptionDigestSha256); writer.WriteString("capability_digest_sha256", CapabilityDigestSha256);
        writer.WriteString("runtime_nonce", RuntimeNonce); writer.WriteNumber("issued_at_unix_milliseconds", IssuedAtUnixMilliseconds);
        writer.WriteNumber("expires_at_unix_milliseconds", ExpiresAtUnixMilliseconds); writer.WriteNumber("maximum_requests", MaximumRequests);
        writer.WriteNumber("aggregate_cost_ceiling_microusd", AggregateCostCeilingMicrousd); writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static OpenRouterPremiumRuntimeAuthorization Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > 16 * 1024) throw new OpenRouterPremiumProductionException("authorization_invalid");
        try
        {
            using JsonDocument document = PreflightJson.ParseStrictObject(bytes, 3, 64, "authorization_invalid");
            JsonElement root = document.RootElement;
            PreflightJson.Exact(root, "schema_version", "profile_digest_sha256", "catalog_evidence_digest_sha256", "endpoint_evidence_digest_sha256",
                "account_binding_identity", "credential_target_identity", "credential_source_identity", "journal_identity", "journal_header_checksum_sha256",
                "preflight_artifact_digest_sha256", "preflight_payload_digest_sha256", "preflight_consumption_digest_sha256", "capability_digest_sha256",
                "runtime_nonce", "issued_at_unix_milliseconds", "expires_at_unix_milliseconds", "maximum_requests", "aggregate_cost_ceiling_microusd");
            OpenRouterPremiumRuntimeAuthorization value = new(PreflightJson.String(root, "schema_version"), PreflightJson.String(root, "profile_digest_sha256"),
                PreflightJson.String(root, "catalog_evidence_digest_sha256"), PreflightJson.String(root, "endpoint_evidence_digest_sha256"),
                PreflightJson.String(root, "account_binding_identity"), PreflightJson.String(root, "credential_target_identity"),
                PreflightJson.String(root, "credential_source_identity"), PreflightJson.String(root, "journal_identity"),
                PreflightJson.String(root, "journal_header_checksum_sha256"), PreflightJson.String(root, "preflight_artifact_digest_sha256"),
                PreflightJson.String(root, "preflight_payload_digest_sha256"), PreflightJson.String(root, "preflight_consumption_digest_sha256"),
                PreflightJson.String(root, "capability_digest_sha256"), PreflightJson.String(root, "runtime_nonce"),
                PreflightJson.Int64(root, "issued_at_unix_milliseconds"), PreflightJson.Int64(root, "expires_at_unix_milliseconds"),
                PreflightJson.Int32(root, "maximum_requests"), PreflightJson.Int64(root, "aggregate_cost_ceiling_microusd"));
            value.Validate();
            byte[] canonical = value.Write();
            try { if (!canonical.AsSpan().SequenceEqual(bytes)) throw new OpenRouterPremiumProductionException("authorization_invalid"); }
            finally { CryptographicOperations.ZeroMemory(canonical); }
            return value;
        }
        catch (OpenRouterPremiumProductionException) { throw; }
        catch { throw new OpenRouterPremiumProductionException("authorization_invalid"); }
    }

    internal void Validate()
    {
        _ = new ByokAccountBindingIdentity(AccountBindingIdentity);
        foreach (string digest in new[] { ProfileDigestSha256, CatalogEvidenceDigestSha256, EndpointEvidenceDigestSha256,
                     JournalHeaderChecksumSha256, PreflightArtifactDigestSha256, PreflightPayloadDigestSha256,
                     PreflightConsumptionDigestSha256, CapabilityDigestSha256 })
            if (!OpenRouterPremiumCanonical.IsDigest(digest)) throw new OpenRouterPremiumProductionException("authorization_invalid");
        if (SchemaVersion != CurrentSchemaVersion || ProfileDigestSha256 != OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256
            || CatalogEvidenceDigestSha256 != OpenRouterPremiumProfile.CatalogEvidenceDigestSha256
            || EndpointEvidenceDigestSha256 != OpenRouterPremiumProfile.EndpointEvidenceDigestSha256
            || CredentialTargetIdentity != OpenRouterPremiumWindowsCredentialStore.TargetIdentity
            || CredentialSourceIdentity != OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity
            || !OpenRouterPremiumCanonical.IsIdentity(JournalIdentity) || !OpenRouterPremiumCanonical.IsIdentity(RuntimeNonce)
            || ExpiresAtUnixMilliseconds <= IssuedAtUnixMilliseconds
            || ExpiresAtUnixMilliseconds - IssuedAtUnixMilliseconds
                != OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds
            || MaximumRequests != 12 || AggregateCostCeilingMicrousd != 18_000)
            throw new OpenRouterPremiumProductionException("authorization_invalid");
    }
}

internal sealed class OpenRouterPremiumProductionExecutionPermit(OpenRouterPremiumRuntimeAuthorization authorization)
{
    internal bool Validate(OpenRouterPremiumExecutionCapability capability, IOpenRouterPremiumJournal journal, long nowMilliseconds)
    {
        OpenRouterPremiumJournalHeader header = journal.Header;
        return nowMilliseconds >= authorization.IssuedAtUnixMilliseconds && nowMilliseconds < authorization.ExpiresAtUnixMilliseconds
            && capability.CapabilityDigestSha256 == authorization.CapabilityDigestSha256
            && capability.IssuedAtMilliseconds == authorization.IssuedAtUnixMilliseconds
            && capability.ExpiresAtMilliseconds == authorization.ExpiresAtUnixMilliseconds
            && journal.ProvidesDurableFlush
            && journal.Identity == authorization.JournalIdentity
            && header.HeaderChecksumSha256 == authorization.JournalHeaderChecksumSha256
            && header.AccountBindingIdentity == authorization.AccountBindingIdentity
            && header.MaximumSlots == 12 && header.AggregateCostCeilingMicrousd == 18_000;
    }
}

internal sealed class OpenRouterPremiumProductionFiles
{
    internal const string ActivationBundleFileName = "activation-bundle.json";
    internal const string PreflightArtifactFileName = "preflight-artifact.json";
    internal const string RuntimeAuthorizationFileName = "runtime-authorization.dpapi";
    internal const string ExecutionConsumedFileName = "execution-consumed.tombstone";
    internal const string ExecutionIndeterminateFileName = "execution-indeterminate.tombstone";
    internal const string EvidenceArtifactFileName = "live-evidence.json";
    internal const string ValidationConsumedFileName = "validation-consumed.tombstone";
    internal const string ValidationReceiptFileName = "validation-receipt.json";
    internal const string PreflightFailedFileName = "preflight-failed.tombstone";
    internal const string JournalDirectoryName = "journal";
    private readonly string _root;
    private readonly FileOpenRouterPremiumDirectoryIdentity _rootIdentity;

    internal OpenRouterPremiumProductionFiles(string root)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
        if (!Path.IsPathFullyQualified(_root) || _root.StartsWith(@"\\?\", StringComparison.Ordinal)
            || _root.StartsWith(@"\\.\", StringComparison.Ordinal) || Path.GetPathRoot(_root) == _root)
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        Directory.CreateDirectory(_root);
        _rootIdentity = FileOpenRouterPremiumIdentity.CaptureDirectoryIdentity(_root);
    }

    internal string JournalPath => Path.Combine(_root, JournalDirectoryName);
    internal bool Exists(string fileName)
    {
        string path = Path.Combine(_root, ExactName(fileName));
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        bool exists = File.Exists(path);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        return exists;
    }

    internal void WriteNew(string fileName, ReadOnlySpan<byte> bytes, int maximum)
    {
        if (bytes.Length is < 1 || bytes.Length > maximum) throw new OpenRouterPremiumProductionException("artifact_size_invalid");
        string name = ExactName(fileName);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        using SafeFileHandle root = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_root);
        using FileStream output = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(_root, name), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        output.Write(bytes); output.Flush(flushToDisk: true); output.Position = 0;
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(_root, root, name, output);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
    }

    internal byte[] Read(string fileName, int maximum)
    {
        string name = ExactName(fileName);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        using SafeFileHandle root = FileOpenRouterPremiumIdentity.OpenDirectoryPinned(_root);
        using FileStream input = FileOpenRouterPremiumIdentity.OpenFileNoFollow(Path.Combine(_root, name), FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(_root, root, name, input);
        if (input.Length is < 1 || input.Length > maximum) throw new OpenRouterPremiumProductionException("artifact_size_invalid");
        byte[] bytes = new byte[input.Length]; input.ReadExactly(bytes);
        FileOpenRouterPremiumIdentity.VerifyStableSingleFile(_root, root, name, input);
        FileOpenRouterPremiumIdentity.VerifyStableDirectory(_root, _rootIdentity);
        return bytes;
    }

    internal void Tombstone(string fileName, string code)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(code + "\n");
        try { WriteNew(fileName, bytes, 256); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string ExactName(string value)
    {
        if (value != Path.GetFileName(value) || string.IsNullOrEmpty(value))
            throw new OpenRouterPremiumProductionException("artifact_path_invalid");
        return value;
    }
}

/// <summary>
/// Windows-only production bridge. Live execution remains unreachable without an eligible durable
/// preflight, a DPAPI CurrentUser-sealed bounded authorization, its exact digest confirmation, and
/// a CreateNew execution tombstone. Managed HTTP may retain framework-owned Bearer copies; only the
/// bridge-owned mutable credential buffers are claimed zeroed.
/// </summary>
public sealed class OpenRouterPremiumProductionBridge
{
    private readonly IOpenRouterPremiumCredentialStore _credentialStore;
    private readonly IOpenRouterPremiumProductionProtector _protector;
    private readonly IOpenRouterPremiumProductionClock _clock;
    private readonly IOpenRouterPremiumProductionMetadataVerifier _metadataVerifier;
    private readonly Func<OpenRouterPremiumHttpExchange> _exchangeFactory;
    private readonly OpenRouterPremiumProductionFiles _files;

    public static OpenRouterPremiumProductionBridge CreateDefault()
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(local)) throw new OpenRouterPremiumProductionException("state_root_unavailable");
        string root = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v1");
        RequireFixedStateTree(local, root);
        OpenRouterPremiumWindowsCredentialStore store = new();
        IOpenRouterPremiumProductionClock clock = SystemOpenRouterPremiumProductionClock.Instance;
        return new(root, store, new OpenRouterPremiumDpapiProtector(), clock,
            OpenRouterPremiumHttpMetadataVerifier.CreateProduction(store, clock),
            OpenRouterPremiumHttpExchange.CreateProduction);
    }

    private static void RequireFixedStateTree(string container, string root)
    {
        string fixedContainer = Path.GetFullPath(container);
        string fixedRoot = Path.GetFullPath(root);
        string relative = Path.GetRelativePath(fixedContainer, fixedRoot);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relative))
            throw new OpenRouterPremiumProductionException("state_root_invalid");
        Directory.CreateDirectory(fixedRoot);
        string current = fixedContainer;
        using (FileOpenRouterPremiumIdentity.OpenDirectoryPinned(current)) { }
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            using (FileOpenRouterPremiumIdentity.OpenDirectoryPinned(current)) { }
        }
    }

    internal OpenRouterPremiumProductionBridge(string root, IOpenRouterPremiumCredentialStore credentialStore,
        IOpenRouterPremiumProductionProtector protector, IOpenRouterPremiumProductionClock clock,
        IOpenRouterPremiumProductionMetadataVerifier metadataVerifier,
        Func<OpenRouterPremiumHttpExchange> exchangeFactory)
    {
        _files = new(root); _credentialStore = credentialStore; _protector = protector; _clock = clock;
        _metadataVerifier = metadataVerifier; _exchangeFactory = exchangeFactory;
    }

    public void StoreCredential(char[] ownedSecret)
    {
        ArgumentNullException.ThrowIfNull(ownedSecret);
        byte[]? bytes = null;
        try
        {
            if (ownedSecret.Length is < 1 or > 512 || ownedSecret.Any(static value => value > 0x7f))
                throw new OpenRouterPremiumProductionException("credential_malformed");
            bytes = ownedSecret.Select(static value => (byte)value).ToArray();
            if (!OpenRouterPremiumCredentialMaterial.IsValid(bytes)) throw new OpenRouterPremiumProductionException("credential_malformed");
            _credentialStore.Write(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, bytes);
        }
        finally
        {
            Array.Clear(ownedSecret);
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void DeleteCredential() => _credentialStore.Delete();

    public async ValueTask<OpenRouterPremiumProductionPreflightResult> PreflightAsync(CancellationToken cancellationToken = default)
    {
        if (_files.Exists(OpenRouterPremiumProductionFiles.PreflightFailedFileName)
            || _files.Exists(OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName)
            || _files.Exists(OpenRouterPremiumProductionFiles.ExecutionConsumedFileName))
            throw new OpenRouterPremiumProductionException("preflight_already_attempted");
        bool durableMutationStarted = false;
        byte[]? activationBundleUtf8 = null;
        try
        {
            using OpenRouterPremiumVerifiedMetadata verified = await _metadataVerifier.VerifyOnceAsync(cancellationToken).ConfigureAwait(false);
            activationBundleUtf8 = verified.TransferOwnedCanonicalBundle();
            OpenRouterPremiumActivationPreflightCapability preflightCapability = OpenRouterPremiumActivationPreflightModule.Authorize(activationBundleUtf8);
            OpenRouterPremiumActivationBundle bundle = preflightCapability.Bundle;
            using (OpenRouterPremiumStoredCredential credential = _credentialStore.Read())
            {
                if (credential.AccountBindingIdentity != bundle.AccountBindingIdentity)
                    throw new OpenRouterPremiumProductionException("credential_account_mismatch");
                byte[] material = credential.TransferOwnedMaterial();
                try { if (!OpenRouterPremiumCredentialMaterial.IsValid(material)) throw new OpenRouterPremiumProductionException("credential_malformed"); }
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
            durableMutationStarted = true;
            _files.WriteNew(OpenRouterPremiumProductionFiles.ActivationBundleFileName, activationBundleUtf8, 16 * 1024);
            OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
            OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
                "openrouter-premium-production-journal/v1", "openrouter-premium-production-run/v1", profile,
                new ByokAccountBindingIdentity(bundle.AccountBindingIdentity));
            cancellationToken.ThrowIfCancellationRequested();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(_files.JournalPath, header)) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(_files.JournalPath);
            OpenRouterPremiumPreflightTrustAttestation trust = new(OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
                preflightCapability.BundleDigestSha256, bundle.AccountBindingIdentity, bundle.CredentialSourceIdentity);
            cancellationToken.ThrowIfCancellationRequested();
            OpenRouterPremiumActivationPreflightArtifact artifact = OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                preflightCapability, reopened, trust, now, cancellationToken);
            if (!artifact.TrustContextValidated || !artifact.Eligible || artifact.BlockerCodes.Count != 0
                || artifact.MaximumRequests != 12 || artifact.AggregateCostCeilingMicrousd != 18_000
                || artifact.DurableConsumptionEvidenceDigestSha256 is null)
                throw new OpenRouterPremiumProductionException("preflight_ineligible");
            cancellationToken.ThrowIfCancellationRequested();
            _files.WriteNew(OpenRouterPremiumProductionFiles.PreflightArtifactFileName, artifact.CanonicalUtf8.Span,
                OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes);

            string runtimeNonce = "openrouter-runtime-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            OpenRouterPremiumAuthorization executionAuthorization = new(profile.Identity,
                OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                new ByokAccountBindingIdentity(bundle.AccountBindingIdentity), header.JournalIdentity, header.HeaderChecksumSha256,
                OpenRouterPremiumHttpExchange.AdapterIdentity, OpenRouterPremiumHttpExchange.AdapterContractDigestSha256,
                OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity, runtimeNonce, now, expires);
            OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(executionAuthorization);
            OpenRouterPremiumRuntimeAuthorization runtime = new(OpenRouterPremiumRuntimeAuthorization.CurrentSchemaVersion,
                profile.ProfileDigestSha256, OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                bundle.AccountBindingIdentity, OpenRouterPremiumWindowsCredentialStore.TargetIdentity,
                OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity, header.JournalIdentity,
                header.HeaderChecksumSha256, artifact.CanonicalDigestSha256, artifact.PayloadDigestSha256,
                artifact.DurableConsumptionEvidenceDigestSha256, capability.CapabilityDigestSha256, runtimeNonce,
                now, expires, 12, 18_000);
            byte[] plaintext = runtime.Write(); byte[]? ciphertext = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ciphertext = _protector.Protect(plaintext);
                cancellationToken.ThrowIfCancellationRequested();
                _files.WriteNew(OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName, ciphertext, 32 * 1024);
                string digest = OpenRouterPremiumCanonical.Digest(ciphertext);
                return new(digest, artifact.CanonicalDigestSha256, bundle.AccountBindingIdentity, expires, 12, 18_000);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                if (ciphertext is not null) CryptographicOperations.ZeroMemory(ciphertext);
            }
        }
        catch
        {
            try { if (durableMutationStarted && !_files.Exists(OpenRouterPremiumProductionFiles.PreflightFailedFileName))
                    _files.Tombstone(OpenRouterPremiumProductionFiles.PreflightFailedFileName, "preflight_failed"); }
            catch { }
            throw;
        }
        finally
        {
            if (activationBundleUtf8 is not null) CryptographicOperations.ZeroMemory(activationBundleUtf8);
        }
    }

    public async ValueTask<OpenRouterPremiumProductionRunResult> RecordOnceAsync(
        string confirmedAuthorizationDigestSha256,
        CancellationToken cancellationToken = default)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(confirmedAuthorizationDigestSha256))
            throw new OpenRouterPremiumProductionException("authorization_confirmation_invalid");
        byte[] ciphertext = _files.Read(OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName, 32 * 1024);
        byte[]? plaintext = null; bool consumed = false;
        try
        {
            if (!FixedDigestEquals(OpenRouterPremiumCanonical.Digest(ciphertext), confirmedAuthorizationDigestSha256))
                throw new OpenRouterPremiumProductionException("authorization_confirmation_mismatch");
            plaintext = _protector.Unprotect(ciphertext);
            OpenRouterPremiumRuntimeAuthorization runtime = OpenRouterPremiumRuntimeAuthorization.Parse(plaintext);
            long now = _clock.NowMilliseconds;
            if (now < runtime.IssuedAtUnixMilliseconds || now >= runtime.ExpiresAtUnixMilliseconds)
                throw new OpenRouterPremiumProductionException("authorization_expired");
            byte[] preflightBytes = _files.Read(OpenRouterPremiumProductionFiles.PreflightArtifactFileName,
                OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes);
            try
            {
                OpenRouterPremiumActivationPreflightArtifact preflight = OpenRouterPremiumActivationPreflightArtifactModule.Validate(preflightBytes);
                if (!preflight.ClaimedEligible || preflight.ClaimedDecision != "eligible_for_separately_authorized_one_shot"
                    || preflight.CanonicalDigestSha256 != runtime.PreflightArtifactDigestSha256
                    || preflight.PayloadDigestSha256 != runtime.PreflightPayloadDigestSha256
                    || preflight.DurableConsumptionEvidenceDigestSha256 != runtime.PreflightConsumptionDigestSha256
                    || preflight.AccountBindingIdentity != runtime.AccountBindingIdentity)
                    throw new OpenRouterPremiumProductionException("preflight_binding_invalid");
            }
            finally { CryptographicOperations.ZeroMemory(preflightBytes); }

            using FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.OpenForAppend(_files.JournalPath);
            OpenRouterPremiumJournalSnapshot snapshot = journal.Snapshot();
            if (!journal.RestartEvidence.RestartVerified || journal.RestartEvidence.RecordCount != 1
                || snapshot.Slots.Count != 0 || snapshot.ReservedExposureMicrousd != 0 || snapshot.SettledMicrousd != 0
                || journal.Header.HeaderChecksumSha256 != runtime.JournalHeaderChecksumSha256
                || journal.Header.AccountBindingIdentity != runtime.AccountBindingIdentity)
                throw new OpenRouterPremiumProductionException("journal_restart_binding_invalid");

            OpenRouterPremiumAuthorization authorization = new(OpenRouterPremiumProfileRegistry.Selected.Identity,
                OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                new ByokAccountBindingIdentity(runtime.AccountBindingIdentity), runtime.JournalIdentity,
                runtime.JournalHeaderChecksumSha256, OpenRouterPremiumHttpExchange.AdapterIdentity,
                OpenRouterPremiumHttpExchange.AdapterContractDigestSha256, runtime.CredentialSourceIdentity,
                runtime.RuntimeNonce, runtime.IssuedAtUnixMilliseconds, runtime.ExpiresAtUnixMilliseconds);
            OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(authorization);
            if (capability.CapabilityDigestSha256 != runtime.CapabilityDigestSha256)
                throw new OpenRouterPremiumProductionException("authorization_binding_invalid");

            _files.Tombstone(OpenRouterPremiumProductionFiles.ExecutionConsumedFileName, "execution_authority_consumed");
            consumed = true;
            OpenRouterPremiumProductionLeaseSource source = new(_credentialStore, runtime.AccountBindingIdentity,
                capability.CapabilityDigestSha256, runtime.JournalHeaderChecksumSha256,
                runtime.IssuedAtUnixMilliseconds, runtime.ExpiresAtUnixMilliseconds);
            using OpenRouterPremiumHttpExchange exchange = _exchangeFactory();
            OpenRouterPremiumEvidenceArtifact artifact = await OpenRouterPremiumEvidenceModule.ExecuteAuthorizedProductionOnceAsync(
                capability, exchange, source, journal, _clock, new OpenRouterPremiumProductionExecutionPermit(runtime), cancellationToken).ConfigureAwait(false);
            if (artifact.Slots.Any(static slot => slot.ChargeState == ChargeState.Unknown
                || slot.SubmissionState == SubmissionState.SubmissionUnknown))
                _files.Tombstone(OpenRouterPremiumProductionFiles.ExecutionIndeterminateFileName, "execution_indeterminate_no_retry");
            _files.WriteNew(OpenRouterPremiumProductionFiles.EvidenceArtifactFileName, artifact.CanonicalUtf8.Span,
                OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes);
            return new(artifact.Status, artifact.ExchangeCount, artifact.TotalSettledMicrousd,
                artifact.TerminalCode, artifact.CanonicalDigestSha256);
        }
        catch
        {
            if (consumed)
            {
                try { if (!_files.Exists(OpenRouterPremiumProductionFiles.ExecutionIndeterminateFileName))
                        _files.Tombstone(OpenRouterPremiumProductionFiles.ExecutionIndeterminateFileName, "execution_indeterminate_no_retry"); }
                catch { }
            }
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ciphertext);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public OpenRouterPremiumProductionValidationResult ValidateOnce()
    {
        if (!_files.Exists(OpenRouterPremiumProductionFiles.ExecutionConsumedFileName)
            || _files.Exists(OpenRouterPremiumProductionFiles.ValidationConsumedFileName))
            throw new OpenRouterPremiumProductionException("validation_not_available");
        byte[] evidence = _files.Read(OpenRouterPremiumProductionFiles.EvidenceArtifactFileName,
            OpenRouterPremiumEvidenceArtifactModule.MaximumArtifactBytes);
        try
        {
            _files.Tombstone(OpenRouterPremiumProductionFiles.ValidationConsumedFileName, "validation_authority_consumed");
            OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceArtifactModule.Validate(evidence);
            byte[] receipt = WriteValidationReceipt(artifact); string digest = OpenRouterPremiumCanonical.Digest(receipt);
            try { _files.WriteNew(OpenRouterPremiumProductionFiles.ValidationReceiptFileName, receipt, 4 * 1024); }
            finally { CryptographicOperations.ZeroMemory(receipt); }
            return new(artifact.Status, artifact.ExchangeCount, artifact.TotalSettledMicrousd,
                artifact.CanonicalDigestSha256, digest);
        }
        finally { CryptographicOperations.ZeroMemory(evidence); }
    }

    private static byte[] WriteValidationReceipt(OpenRouterPremiumEvidenceArtifact artifact)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject(); writer.WriteString("schema_version", "snow_globe_openrouter_production_validation_receipt/v1");
        writer.WriteString("evidence_artifact_digest_sha256", artifact.CanonicalDigestSha256); writer.WriteString("status", artifact.Status);
        writer.WriteNumber("exchange_count", artifact.ExchangeCount); writer.WriteNumber("total_settled_microusd", artifact.TotalSettledMicrousd);
        writer.WriteBoolean("additional_attempt_authorized", false); writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static bool FixedDigestEquals(string left, string right)
    {
        if (!OpenRouterPremiumCanonical.IsDigest(left) || !OpenRouterPremiumCanonical.IsDigest(right)) return false;
        byte[] a = Convert.FromHexString(left); byte[] b = Convert.FromHexString(right);
        try { return CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }
}
