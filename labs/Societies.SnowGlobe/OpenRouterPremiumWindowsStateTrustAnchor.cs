using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

internal interface IOpenRouterPremiumStateTrustAnchorLease : IOpenRouterPremiumStateTrustAnchor, IDisposable
{
}

internal interface IOpenRouterPremiumStateTrustAnchorLeaseSource
{
    IOpenRouterPremiumStateTrustAnchorLease OpenExisting();
}

internal interface IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend
{
    byte[] ReadExisting(string targetIdentity);
}

internal interface IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend
{
    void WriteOnce(OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata metadata,
        ReadOnlySpan<byte> key);
}

internal sealed record OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata(
    string TargetIdentity,
    string Comment,
    string UserName,
    uint Type,
    uint Persist,
    uint Flags)
{
    internal const uint GenericCredential = 1;
    internal const uint LocalMachinePersistence = 2;

    internal static OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata Fixed { get; } = new(
        OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
        "Societies Snow Globe OpenRouter Premium v2 state HMAC anchor",
        "Societies.SnowGlobe.OpenRouterPremiumStateAnchorV2",
        GenericCredential,
        LocalMachinePersistence,
        0);

    internal bool IsFixed => string.Equals(TargetIdentity,
        OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity, StringComparison.Ordinal)
        && string.Equals(Comment, Fixed.Comment, StringComparison.Ordinal)
        && string.Equals(UserName, Fixed.UserName, StringComparison.Ordinal)
        && Type == GenericCredential
        && Persist == LocalMachinePersistence
        && Flags == 0;
}

/// <summary>
/// Opens the fixed, separately provisioned Windows Credential Manager state anchor read-only.
/// This source has no provision, generation, replacement, deletion, rotation, repair, import,
/// enumeration, or scanning operation.
/// </summary>
internal sealed class OpenRouterPremiumWindowsStateTrustAnchorSource : IOpenRouterPremiumStateTrustAnchorLeaseSource
{
    internal const string TargetIdentity = "Societies/SnowGlobe/OpenRouterPremium/state-hmac-anchor/v2";
    private readonly IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend _backend;
    private readonly Action<bool>? _leaseZeroObserver;

    internal OpenRouterPremiumWindowsStateTrustAnchorSource(
        IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend? backend = null,
        Action<bool>? leaseZeroObserver = null)
    {
        _backend = backend ?? OpenRouterPremiumWindowsStateTrustAnchorNativeBackend.Instance;
        _leaseZeroObserver = leaseZeroObserver;
    }

    public IOpenRouterPremiumStateTrustAnchorLease OpenExisting()
    {
        byte[]? owned = null;
        try
        {
            owned = _backend.ReadExisting(TargetIdentity);
            if (owned is not { Length: OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes })
                throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid");
            return new OpenRouterPremiumWindowsStateTrustAnchor(owned, _leaseZeroObserver);
        }
        finally
        {
            if (owned is not null) CryptographicOperations.ZeroMemory(owned);
        }
    }
}

internal sealed class OpenRouterPremiumWindowsStateTrustAnchor : IOpenRouterPremiumStateTrustAnchorLease
{
    internal const int KeyBytes = 32;
    private const int MaximumAuthenticatedBytes = 64 * 1024;
    private static readonly byte[] IdentityDomain = Encoding.ASCII.GetBytes(
        "Societies/SnowGlobe/OpenRouterPremium/state-hmac-anchor/v2/identity\0");
    private static readonly byte[] AuthenticationDomain = Encoding.ASCII.GetBytes(
        "Societies/SnowGlobe/OpenRouterPremium/state-hmac-anchor/v2/authenticate\0");

    private readonly object _gate = new();
    private readonly Action<bool>? _zeroObserver;
    private byte[]? _key;

    internal OpenRouterPremiumWindowsStateTrustAnchor(ReadOnlySpan<byte> key, Action<bool>? zeroObserver = null)
    {
        if (key.Length != KeyBytes)
            throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid");
        _key = key.ToArray();
        _zeroObserver = zeroObserver;
        try { IdentitySha256 = Compute(IdentityDomain, ReadOnlySpan<byte>.Empty); }
        catch
        {
            byte[] owned = _key;
            _key = null;
            CryptographicOperations.ZeroMemory(owned);
            _zeroObserver?.Invoke(owned.All(static value => value == 0));
            throw;
        }
    }

    public string IdentitySha256 { get; }

    public string Authenticate(ReadOnlySpan<byte> canonicalBytes)
    {
        if (canonicalBytes.Length is < 1 or > MaximumAuthenticatedBytes)
            throw new OpenRouterPremiumProductionException("state_authentication_input_invalid");
        lock (_gate) return Compute(AuthenticationDomain, canonicalBytes);
    }

    public bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256)
    {
        if (canonicalBytes.Length is < 1 or > MaximumAuthenticatedBytes) return false;
        byte[] expected = [];
        byte[] actual = new byte[KeyBytes];
        bool correctLength = authenticatorSha256 is { Length: KeyBytes * 2 };
        bool parsed = correctLength;
        try
        {
            expected = Convert.FromHexString(Authenticate(canonicalBytes));
            for (int index = 0; index < KeyBytes; index++)
            {
                int high = HexNibble(correctLength ? authenticatorSha256[index * 2] : '\0');
                int low = HexNibble(correctLength ? authenticatorSha256[(index * 2) + 1] : '\0');
                parsed &= high >= 0 & low >= 0;
                actual[index] = (byte)((Math.Max(high, 0) << 4) | Math.Max(low, 0));
            }
            bool equal = CryptographicOperations.FixedTimeEquals(expected, actual);
            return parsed && equal;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static int HexNibble(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        _ => -1
    };

    private string Compute(ReadOnlySpan<byte> domain, ReadOnlySpan<byte> payload)
    {
        byte[]? key = _key;
        if (key is null) throw new ObjectDisposedException(nameof(OpenRouterPremiumWindowsStateTrustAnchor));
        byte[] input = new byte[checked(domain.Length + sizeof(int) + payload.Length)];
        byte[] digest = [];
        try
        {
            domain.CopyTo(input);
            BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(domain.Length, sizeof(int)), payload.Length);
            payload.CopyTo(input.AsSpan(domain.Length + sizeof(int)));
            digest = HMACSHA256.HashData(key, input);
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            byte[]? key = _key;
            _key = null;
            if (key is null) return;
            CryptographicOperations.ZeroMemory(key);
            _zeroObserver?.Invoke(key.All(static value => value == 0));
        }
    }
}

internal sealed class OpenRouterPremiumWindowsStateTrustAnchorNativeBackend
    : IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend
{
    private const int NotFound = 1168;
    internal static OpenRouterPremiumWindowsStateTrustAnchorNativeBackend Instance { get; } = new();
    private OpenRouterPremiumWindowsStateTrustAnchorNativeBackend() { }

    public byte[] ReadExisting(string targetIdentity)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        if (!string.Equals(targetIdentity, OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
                StringComparison.Ordinal))
            throw new OpenRouterPremiumProductionException("state_trust_anchor_target_invalid");
        if (!CredReadW(OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
                OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.GenericCredential,
                0, out IntPtr pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == NotFound)
                throw new OpenRouterPremiumProductionException("state_trust_anchor_missing");
            _ = new Win32Exception(error);
            throw new OpenRouterPremiumProductionException("state_trust_anchor_read_failed");
        }

        IntPtr blob = IntPtr.Zero;
        int size = 0;
        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            blob = credential.CredentialBlob;
            size = checked((int)credential.CredentialBlobSize);
            if (credential.Flags != 0
                || credential.Type != OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.GenericCredential
                || !string.Equals(credential.TargetName,
                    OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.Fixed.TargetIdentity,
                    StringComparison.Ordinal)
                || !string.Equals(credential.Comment,
                    OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.Fixed.Comment,
                    StringComparison.Ordinal)
                || credential.Persist != OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.LocalMachinePersistence
                || credential.AttributeCount != 0
                || credential.Attributes != IntPtr.Zero
                || credential.TargetAlias is not null
                || !string.Equals(credential.UserName,
                    OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.Fixed.UserName,
                    StringComparison.Ordinal)
                || blob == IntPtr.Zero
                || size != OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes)
                throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid");
            byte[] owned = new byte[size];
            try
            {
                Marshal.Copy(blob, owned, 0, owned.Length);
                return owned;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(owned);
                throw;
            }
        }
        finally
        {
            if (blob != IntPtr.Zero && size > 0) RtlZeroMemory(blob, (nuint)size);
            CredFree(pointer);
        }
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
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
    private static extern void RtlZeroMemory(IntPtr destination, nuint length);
}

/// <summary>
/// The only native mutation seam for the separately gated state-anchor administrator.
/// Ordinary state-anchor readers do not implement or reference this interface.
/// </summary>
internal sealed class OpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend
    : IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend
{
    internal static OpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend Instance { get; } = new();
    private OpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend() { }

    public void WriteOnce(OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata metadata,
        ReadOnlySpan<byte> key)
    {
        FileOpenRouterPremiumIdentity.RequireSupportedPlatform();
        if (metadata is null || !metadata.IsFixed)
            throw new OpenRouterPremiumProductionException("state_trust_anchor_metadata_invalid");
        if (key.Length != OpenRouterPremiumWindowsStateTrustAnchor.KeyBytes)
            throw new OpenRouterPremiumProductionException("state_trust_anchor_invalid");

        byte[] owned = key.ToArray();
        GCHandle pin = default;
        try
        {
            pin = GCHandle.Alloc(owned, GCHandleType.Pinned);
            NativeCredential credential = new()
            {
                Flags = metadata.Flags,
                Type = metadata.Type,
                TargetName = metadata.TargetIdentity,
                Comment = metadata.Comment,
                CredentialBlobSize = checked((uint)owned.Length),
                CredentialBlob = pin.AddrOfPinnedObject(),
                Persist = metadata.Persist,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = metadata.UserName
            };
            if (!CredWriteW(ref credential, 0))
            {
                _ = new Win32Exception(Marshal.GetLastWin32Error());
                throw new OpenRouterPremiumProductionException(
                    "state_trust_anchor_provisioning_indeterminate");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(owned);
            if (pin.IsAllocated) pin.Free();
        }
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
}
