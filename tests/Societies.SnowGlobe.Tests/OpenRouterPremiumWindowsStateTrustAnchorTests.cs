using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumWindowsStateTrustAnchorTests
{
    [Fact]
    public void FixedTargetIsReadExactlyOnceAndBackendOwnedBufferIsCleared()
    {
        byte[] key = Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();
        FakeBackend backend = new(key);
        bool leaseZeroed = false;
        OpenRouterPremiumWindowsStateTrustAnchorSource source = new(backend,
            observed => leaseZeroed = observed);

        using (IOpenRouterPremiumStateTrustAnchorLease lease = source.OpenExisting())
        {
            Assert.Equal(1, backend.ReadCalls);
            Assert.Equal(OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity, backend.Target);
            Assert.All(backend.Returned!, value => Assert.Equal(0, value));
            Assert.True(OpenRouterPremiumCanonical.IsDigest(lease.IdentitySha256));
            Assert.True(lease.Verify("canonical"u8, lease.Authenticate("canonical"u8)));
        }

        Assert.True(leaseZeroed);
    }

    [Fact]
    public void IdentityAndAuthenticationAreStableDomainSeparatedAndWrongKeyFails()
    {
        byte[] firstKey = Enumerable.Repeat((byte)0x41, 32).ToArray();
        byte[] secondKey = Enumerable.Repeat((byte)0x42, 32).ToArray();
        using OpenRouterPremiumWindowsStateTrustAnchor first = new(firstKey);
        using OpenRouterPremiumWindowsStateTrustAnchor same = new(firstKey);
        using OpenRouterPremiumWindowsStateTrustAnchor wrong = new(secondKey);
        byte[] canonical = Encoding.ASCII.GetBytes("fixed-canonical-state");
        try
        {
            string authenticator = first.Authenticate(canonical);
            Assert.Equal(first.IdentitySha256, same.IdentitySha256);
            Assert.Equal(authenticator, same.Authenticate(canonical));
            Assert.NotEqual(first.IdentitySha256, authenticator);
            Assert.False(wrong.Verify(canonical, authenticator));
            Assert.False(first.Verify("other"u8, authenticator));
            Assert.False(first.Verify(canonical, authenticator.ToUpperInvariant()));
            Assert.False(first.Verify(canonical, "not-hex"));
            Assert.False(first.Verify(canonical, new string('0', 63)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(firstKey);
            CryptographicOperations.ZeroMemory(secondKey);
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    [Fact]
    public void InvalidAnchorLengthFailsClosedAndClearsReturnedMaterial()
    {
        FakeBackend backend = new(Enumerable.Repeat((byte)0x5a, 31).ToArray());
        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            () => new OpenRouterPremiumWindowsStateTrustAnchorSource(backend).OpenExisting());
        Assert.Equal("state_trust_anchor_invalid", error.Code);
        Assert.Equal(1, backend.ReadCalls);
        Assert.All(backend.Returned!, value => Assert.Equal(0, value));
    }

    [Fact]
    public void ProductionAnchorSurfaceIsReadOnlyAndHasNoLifecycleOrDiscoveryOperations()
    {
        string[] forbidden = ["write", "delete", "provision", "generate", "replace", "rotate",
            "repair", "import", "scan", "enumerate"];
        MethodInfo method = Assert.Single(typeof(IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal("ReadExisting", method.Name);
        Assert.DoesNotContain(typeof(OpenRouterPremiumWindowsStateTrustAnchorSource)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => forbidden.Any(token => candidate.Name.Contains(token,
                StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(typeof(OpenRouterPremiumWindowsStateTrustAnchorNativeBackend)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => forbidden.Any(token => candidate.Name.Contains(token,
                StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(32, "state_trust_anchor_already_provisioned")]
    [InlineData(31, "state_trust_anchor_invalid")]
    public void ExistingOrMalformedTargetNeverWritesGeneratesOrInitializes(
        int length, string expectedCode)
    {
        ScriptedProvisioningBackend backend = new(Enumerable.Repeat((byte)0x31, length).ToArray());
        FixedKeyGenerator generator = new(Key());
        TrackingInitializer initializer = new(backend.Order);
        OpenRouterPremiumV2StateAnchorAdministration administration = Administration(
            backend, generator, initializer);

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            administration.ProvisionOnceAndInitializeOffline);

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(1, backend.ReadCalls);
        Assert.Equal(0, backend.WriteCalls);
        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, initializer.Calls);
        Assert.All(backend.ReturnedBuffers.SelectMany(static value => value),
            value => Assert.Equal((byte)0, value));
        backend.Clear();
    }

    [Fact]
    public void ProvisioningUsesExactTargetMetadataReadbackAndSameAnchorBeforeOfflineInitialization()
    {
        byte[] expected = Key();
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(expected);
        TrackingInitializer initializer = new(backend.Order, expected);
        bool leaseZeroed = false;
        OpenRouterPremiumV2StateAnchorAdministration administration = Administration(
            backend, generator, initializer, observed => leaseZeroed = observed);

        OpenRouterPremiumV2StateAnchorProvisioningResult result =
            administration.ProvisionOnceAndInitializeOffline();

        Assert.Equal(["read", "write", "read", "initialize"], backend.Order);
        Assert.Equal(2, backend.ReadCalls);
        Assert.Equal(1, backend.WriteCalls);
        Assert.Equal(OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
            backend.ReadTargets[0]);
        Assert.Equal(OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
            backend.ReadTargets[1]);
        Assert.Equal(OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.Fixed,
            backend.Metadata);
        Assert.Equal(OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata.LocalMachinePersistence,
            backend.Metadata!.Persist);
        Assert.Equal(1, initializer.Calls);
        Assert.True(initializer.SameAnchorObserved);
        Assert.True(leaseZeroed);
        Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        Assert.All(backend.ReturnedBuffers.SelectMany(static value => value),
            value => Assert.Equal((byte)0, value));
        Assert.True(OpenRouterPremiumCanonical.IsDigest(result.AnchorIdentitySha256));
        Assert.Equal(OpenRouterPremiumStateGenerationStore.StateContractDigestSha256,
            result.StateContractDigestSha256);
        Assert.Equal("fixed_local_app_data_v2_no_v1_observation", result.StateRootPolicy);
        Assert.False(result.OpenRouterCredentialRead);
        Assert.False(result.ProviderAccess);
        Assert.False(result.AdditionalAttemptAuthorized);
        CryptographicOperations.ZeroMemory(expected);
        backend.Clear();
    }

    [Fact]
    public void WriteFailureIsIndeterminateNeverRetriedAndClearsGeneratedKey()
    {
        ScriptedProvisioningBackend backend = new() { FailWrite = true };
        FixedKeyGenerator generator = new(Key());
        TrackingInitializer initializer = new(backend.Order);

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            Administration(backend, generator, initializer).ProvisionOnceAndInitializeOffline);

        Assert.Equal("state_trust_anchor_provisioning_indeterminate", error.Code);
        Assert.Equal(1, backend.ReadCalls);
        Assert.Equal(1, backend.WriteCalls);
        Assert.Equal(0, initializer.Calls);
        Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        backend.Clear();
    }

    [Fact]
    public void GeneratedAnchorMustBeExactlyThirtyTwoBytesAndIsClearedOnRejection()
    {
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(Enumerable.Repeat((byte)0x52, 31).ToArray());
        TrackingInitializer initializer = new(backend.Order);

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            Administration(backend, generator, initializer).ProvisionOnceAndInitializeOffline);

        Assert.Equal("state_trust_anchor_generation_failed", error.Code);
        Assert.Equal(1, backend.ReadCalls);
        Assert.Equal(0, backend.WriteCalls);
        Assert.Equal(0, initializer.Calls);
        Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        backend.Clear();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadbackFailureOrMismatchIsIndeterminateWithoutRetryOrInitialization(bool readFails)
    {
        ScriptedProvisioningBackend backend = new()
        {
            FailReadback = readFails,
            MismatchReadback = !readFails
        };
        FixedKeyGenerator generator = new(Key());
        TrackingInitializer initializer = new(backend.Order);

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            Administration(backend, generator, initializer).ProvisionOnceAndInitializeOffline);

        Assert.Equal("state_trust_anchor_provisioning_indeterminate", error.Code);
        Assert.Equal(2, backend.ReadCalls);
        Assert.Equal(1, backend.WriteCalls);
        Assert.Equal(0, initializer.Calls);
        Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        Assert.All(backend.ReturnedBuffers.SelectMany(static value => value),
            value => Assert.Equal((byte)0, value));
        Assert.DoesNotContain(typeof(IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("delete", StringComparison.OrdinalIgnoreCase));
        backend.Clear();
    }

    [Fact]
    public void ConfirmedAnchorIsPreservedWhenOfflineInitializationFails()
    {
        byte[] expected = Key();
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(expected);
        TrackingInitializer initializer = new(backend.Order) { Fail = true };
        bool leaseZeroed = false;

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            Administration(backend, generator, initializer,
                observed => leaseZeroed = observed).ProvisionOnceAndInitializeOffline);

        Assert.Equal("state_anchor_provisioned_initialization_failed", error.Code);
        Assert.Equal(1, backend.WriteCalls);
        Assert.Equal(2, backend.ReadCalls);
        Assert.Equal(1, initializer.Calls);
        Assert.Equal(expected, backend.Stored);
        Assert.True(leaseZeroed);
        Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        CryptographicOperations.ZeroMemory(expected);
        backend.Clear();
    }

    [Fact]
    public void BusyCooperativeLockIsTerminalBeforeNativeGenerationOrRootAction()
    {
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(Key());
        TrackingInitializer initializer = new(backend.Order);
        OpenRouterPremiumV2StateAnchorAdministration administration = new(
            backend, backend, new BusyLockSource(), generator, initializer);

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            administration.ProvisionOnceAndInitializeOffline);

        Assert.Equal("state_trust_anchor_writer_busy", error.Code);
        Assert.Equal(0, backend.ReadCalls);
        Assert.Equal(0, backend.WriteCalls);
        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, initializer.Calls);
        Assert.Equal(@"Global\Societies.SnowGlobe.OpenRouterPremium.StateAnchorProvision.v2",
            OpenRouterPremiumStateAnchorProvisioningLockSource.MutexName);
        backend.Clear();
    }

    [Fact]
    public async Task ConcurrentProvisioningHasExactlyOneWriteAndOneSuccess()
    {
        ScriptedProvisioningBackend backend = new();
        SharedZeroWaitLockSource locks = new();
        FixedKeyGenerator generator = new(Key());
        BlockingInitializer initializer = new(backend.Order);
        OpenRouterPremiumV2StateAnchorAdministration administration = new(
            backend, backend, locks, generator, initializer);

        Task<OpenRouterPremiumV2StateAnchorProvisioningResult> first = Task.Run(
            administration.ProvisionOnceAndInitializeOffline);
        Assert.True(initializer.Entered.Wait(TimeSpan.FromSeconds(5)));
        OpenRouterPremiumProductionException second = Assert.Throws<OpenRouterPremiumProductionException>(
            administration.ProvisionOnceAndInitializeOffline);
        initializer.Release.Set();
        OpenRouterPremiumV2StateAnchorProvisioningResult result = await first;

        Assert.Equal("state_trust_anchor_writer_busy", second.Code);
        Assert.True(OpenRouterPremiumCanonical.IsDigest(result.AnchorIdentitySha256));
        Assert.Equal(1, backend.WriteCalls);
        Assert.Equal(2, backend.ReadCalls);
        Assert.Equal(1, initializer.Calls);
        backend.Clear();
    }

    [Fact]
    public async Task ProductionGlobalMutexIsZeroWaitAcrossThreadsAndReusableAfterOwnerDisposes()
    {
        OpenRouterPremiumStateAnchorProvisioningLockSource source =
            OpenRouterPremiumStateAnchorProvisioningLockSource.Instance;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(initialState: false);
        Task holder = Task.Run(() =>
        {
            using IDisposable first = source.TryAcquire();
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            OpenRouterPremiumProductionException second = await Task.Run(() =>
                Assert.Throws<OpenRouterPremiumProductionException>(source.TryAcquire));

            Assert.Equal("state_trust_anchor_writer_busy", second.Code);
            Assert.Equal(@"Global\Societies.SnowGlobe.OpenRouterPremium.StateAnchorProvision.v2",
                OpenRouterPremiumStateAnchorProvisioningLockSource.MutexName);
        }
        finally
        {
            release.Set();
            await holder;
        }

        using IDisposable later = source.TryAcquire();
        Assert.NotNull(later);
    }

    [Fact]
    public void ProductionGlobalMutexAbandonmentIsIndeterminateAndRecoveredOwnershipIsReleased()
    {
        string mutexName = @"Global\Societies.SnowGlobe.OpenRouterPremium.StateAnchorProvision.Test." +
            Guid.NewGuid().ToString("N");
        Mutex? abandoned = null;
        Exception? ownerFailure = null;
        Thread owner = new(() =>
        {
            try
            {
                abandoned = new Mutex(initiallyOwned: false, mutexName);
                Assert.True(abandoned.WaitOne(TimeSpan.FromSeconds(5)));
            }
            catch (Exception exception)
            {
                ownerFailure = exception;
            }
        }) { IsBackground = true };
        OpenRouterPremiumStateAnchorProvisioningLockSource source = new(
            _ => new Mutex(initiallyOwned: false, mutexName));

        try
        {
            owner.Start();
            Assert.True(owner.Join(TimeSpan.FromSeconds(5)));
            Assert.Null(ownerFailure);
            Assert.NotNull(abandoned);

            OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
                source.TryAcquire);

            Assert.Equal("state_trust_anchor_provisioning_indeterminate", error.Code);
            using IDisposable later = source.TryAcquire();
            Assert.NotNull(later);
        }
        finally
        {
            if (owner.IsAlive) _ = owner.Join(TimeSpan.FromSeconds(6));
            abandoned?.Dispose();
        }
    }

    [Fact]
    public void ProductionGlobalMutexCreationOrAccessFailureIsFailClosed()
    {
        OpenRouterPremiumStateAnchorProvisioningLockSource source = new(
            _ => throw new UnauthorizedAccessException("injected_global_mutex_access_denied"));

        OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
            source.TryAcquire);

        Assert.Equal("state_trust_anchor_provisioning_indeterminate", error.Code);
        Assert.DoesNotContain("access_denied", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineInitializationCreatesOnlyTheFixedEmptyV2StoreAndNeverObservesV1()
    {
        string local = Path.Combine(Path.GetTempPath(), "snowglobe-anchor-" + Guid.NewGuid().ToString("N"));
        string v1 = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v1");
        string v2 = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        byte[] expected = Key();
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(expected);
        List<string> pinned = [];
        try
        {
            Directory.CreateDirectory(v1);
            string sentinel = Path.Combine(v1, "never-observe-or-mutate.txt");
            File.WriteAllText(sentinel, "v1-sentinel", Encoding.ASCII);
            DateTime before = File.GetLastWriteTimeUtc(sentinel);
            OpenRouterPremiumV2OfflineStateInitializer initializer = new(local, v2,
                new OpenRouterPremiumV2StateStoreFactory(path => pinned.Add(path)));
            OpenRouterPremiumV2StateAnchorAdministration administration = new(
                backend, backend, new SharedZeroWaitLockSource(), generator, initializer);

            OpenRouterPremiumV2StateAnchorProvisioningResult result =
                administration.ProvisionOnceAndInitializeOffline();

            Assert.Equal("v1-sentinel", File.ReadAllText(sentinel, Encoding.ASCII));
            Assert.Equal(before, File.GetLastWriteTimeUtc(sentinel));
            Assert.DoesNotContain(pinned, path => path.StartsWith(v1, StringComparison.Ordinal));
            Assert.Equal(new[]
            {
                "authorities", "execution-consumed", "generations", "root-writer.lock",
                "validation-consumed"
            }, Directory.EnumerateFileSystemEntries(v2).Select(Path.GetFileName)
                .OrderBy(static value => value, StringComparer.Ordinal));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(v2, "generations")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(v2, "authorities")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(v2, "execution-consumed")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(v2, "validation-consumed")));
            foreach (string file in Directory.EnumerateFiles(v2, "*", SearchOption.AllDirectories))
                Assert.False(ContainsSequence(File.ReadAllBytes(file), expected));
            Assert.False(result.OpenRouterCredentialRead);
            Assert.False(result.ProviderAccess);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            backend.Clear();
            if (Directory.Exists(local)) Directory.Delete(local, recursive: true);
        }
    }

    [Fact]
    public void PreexistingV2RootFailsAfterConfirmedWriteWithoutMutatingRootOrAnchor()
    {
        string local = Path.Combine(Path.GetTempPath(), "snowglobe-anchor-existing-"
            + Guid.NewGuid().ToString("N"));
        string v2 = Path.Combine(local, "Societies", "SnowGlobe",
            "OpenRouterPremiumOneShot", "v2");
        byte[] expected = Key();
        ScriptedProvisioningBackend backend = new();
        FixedKeyGenerator generator = new(expected);
        try
        {
            Directory.CreateDirectory(v2);
            string sentinel = Path.Combine(v2, "preexisting-sentinel.txt");
            File.WriteAllText(sentinel, "preexisting-v2", Encoding.ASCII);
            OpenRouterPremiumV2OfflineStateInitializer initializer = new(local, v2,
                OpenRouterPremiumV2StateStoreFactory.Instance);
            OpenRouterPremiumV2StateAnchorAdministration administration = new(
                backend, backend, new SharedZeroWaitLockSource(), generator, initializer);

            OpenRouterPremiumProductionException error = Assert.Throws<OpenRouterPremiumProductionException>(
                administration.ProvisionOnceAndInitializeOffline);

            Assert.Equal("state_anchor_provisioned_initialization_failed", error.Code);
            Assert.Equal(expected, backend.Stored);
            Assert.Equal("preexisting-v2", File.ReadAllText(sentinel, Encoding.ASCII));
            Assert.Single(Directory.EnumerateFileSystemEntries(v2));
            Assert.All(generator.Owned, value => Assert.Equal((byte)0, value));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            backend.Clear();
            if (Directory.Exists(local)) Directory.Delete(local, recursive: true);
        }
    }

    [Fact]
    public void StateAnchorAdministrationHasNoCredentialMetadataExchangeOrStateOperationSurface()
    {
        string[] forbidden = ["credentialstore", "metadata", "exchange", "preflight", "record", "validate",
            "delete", "rotate", "repair", "retry", "import", "scan", "enumerate"];
        MethodInfo[] methods = typeof(OpenRouterPremiumV2StateAnchorAdministration).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(["ProvisionOnceAndInitializeOffline"], methods.Select(static value => value.Name));
        Assert.DoesNotContain(methods, method => forbidden.Any(token => method.Name.Contains(token,
            StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(typeof(OpenRouterPremiumV2StateAnchorAdministration).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic),
            field => forbidden.Any(token => field.FieldType.Name.Contains(token,
                StringComparison.OrdinalIgnoreCase)));
    }

    private static OpenRouterPremiumV2StateAnchorAdministration Administration(
        ScriptedProvisioningBackend backend,
        FixedKeyGenerator generator,
        TrackingInitializer initializer,
        Action<bool>? leaseZeroObserver = null) => new(
            backend, backend, new SharedZeroWaitLockSource(), generator, initializer,
            leaseZeroObserver);

    private static byte[] Key() => Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (int index = 0; index <= haystack.Length - needle.Length; index++)
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return true;
        return false;
    }

    private sealed class FakeBackend(byte[] material)
        : IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend
    {
        public int ReadCalls { get; private set; }
        public string? Target { get; private set; }
        public byte[]? Returned { get; private set; }

        public byte[] ReadExisting(string targetIdentity)
        {
            ReadCalls++;
            Target = targetIdentity;
            Returned = material.ToArray();
            return Returned;
        }
    }

    private sealed class ScriptedProvisioningBackend(byte[]? initial = null)
        : IOpenRouterPremiumWindowsStateTrustAnchorNativeBackend,
          IOpenRouterPremiumWindowsStateTrustAnchorProvisioningNativeBackend
    {
        private readonly object _gate = new();
        public byte[]? Stored { get; private set; } = initial?.ToArray();
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }
        public bool FailWrite { get; init; }
        public bool FailReadback { get; init; }
        public bool MismatchReadback { get; init; }
        public List<string> Order { get; } = [];
        public List<string> ReadTargets { get; } = [];
        public List<byte[]> ReturnedBuffers { get; } = [];
        public OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata? Metadata { get; private set; }

        public byte[] ReadExisting(string targetIdentity)
        {
            lock (_gate)
            {
                ReadCalls++;
                Order.Add("read");
                ReadTargets.Add(targetIdentity);
                if (ReadCalls > 1 && FailReadback)
                    throw new OpenRouterPremiumProductionException("state_trust_anchor_read_failed");
                if (Stored is null)
                    throw new OpenRouterPremiumProductionException("state_trust_anchor_missing");
                byte[] returned = MismatchReadback && ReadCalls > 1
                    ? Enumerable.Repeat((byte)0x7f, Stored.Length).ToArray()
                    : Stored.ToArray();
                ReturnedBuffers.Add(returned);
                return returned;
            }
        }

        public void WriteOnce(OpenRouterPremiumWindowsStateTrustAnchorCredentialMetadata metadata,
            ReadOnlySpan<byte> key)
        {
            lock (_gate)
            {
                WriteCalls++;
                Order.Add("write");
                Metadata = metadata;
                if (FailWrite)
                    throw new OpenRouterPremiumProductionException("injected_write_failure");
                Stored = key.ToArray();
            }
        }

        public void Clear()
        {
            if (Stored is not null) CryptographicOperations.ZeroMemory(Stored);
            foreach (byte[] returned in ReturnedBuffers) CryptographicOperations.ZeroMemory(returned);
        }
    }

    private sealed class FixedKeyGenerator(byte[] key) : IOpenRouterPremiumStateAnchorKeyGenerator
    {
        public byte[] Owned { get; } = key.ToArray();
        public int Calls { get; private set; }
        public byte[] GenerateOwned()
        {
            Calls++;
            return Owned;
        }
    }

    private sealed class TrackingInitializer : IOpenRouterPremiumV2OfflineStateInitializer
    {
        private readonly List<string> _order;
        private readonly byte[]? _expectedKey;
        public TrackingInitializer(List<string> order, byte[]? expectedKey = null)
        {
            _order = order;
            _expectedKey = expectedKey?.ToArray();
        }
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public bool SameAnchorObserved { get; private set; }

        public void Initialize(IOpenRouterPremiumStateTrustAnchor trustAnchor)
        {
            Calls++;
            _order.Add("initialize");
            if (_expectedKey is not null)
            {
                using OpenRouterPremiumWindowsStateTrustAnchor expected = new(_expectedKey);
                SameAnchorObserved = trustAnchor.IdentitySha256 == expected.IdentitySha256
                    && trustAnchor.Authenticate("same-anchor"u8) == expected.Authenticate("same-anchor"u8);
                CryptographicOperations.ZeroMemory(_expectedKey);
            }
            if (Fail) throw new InvalidOperationException("injected_initialization_failure");
        }
    }

    private sealed class BlockingInitializer(List<string> order)
        : IOpenRouterPremiumV2OfflineStateInitializer
    {
        public ManualResetEventSlim Entered { get; } = new(initialState: false);
        public ManualResetEventSlim Release { get; } = new(initialState: false);
        public int Calls { get; private set; }

        public void Initialize(IOpenRouterPremiumStateTrustAnchor trustAnchor)
        {
            Calls++;
            order.Add("initialize");
            Entered.Set();
            Assert.True(Release.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    private sealed class BusyLockSource : IOpenRouterPremiumStateAnchorProvisioningLockSource
    {
        public IDisposable TryAcquire() => throw new OpenRouterPremiumProductionException(
            "state_trust_anchor_writer_busy");
    }

    private sealed class SharedZeroWaitLockSource : IOpenRouterPremiumStateAnchorProvisioningLockSource
    {
        private int _held;
        public IDisposable TryAcquire() => Interlocked.CompareExchange(ref _held, 1, 0) == 0
            ? new ActionDisposable(() => Volatile.Write(ref _held, 0))
            : throw new OpenRouterPremiumProductionException(
                "state_trust_anchor_writer_busy");
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
