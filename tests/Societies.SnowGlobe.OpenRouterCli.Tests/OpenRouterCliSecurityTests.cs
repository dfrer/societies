using Societies.SnowGlobe;
using System.Reflection;
using System.Runtime.Versioning;
using System.Net;
using Xunit;

[assembly: SupportedOSPlatform("windows")]

namespace Societies.SnowGlobe.OpenRouterCli.Tests;

public sealed class OpenRouterCliSecurityTests
{
    [Fact]
    public async Task ArgumentsRejectSecretSmugglingAndUnboundedLiveAliases()
    {
        FakeCliModule module = new(); FakeCliFactory factory = new(module);
        foreach (string[] arguments in new[]
        {
            new[] { "record-once", "--api-key", "secret-sentinel" },
            new[] { "record-once", "--token", "secret-sentinel" },
            new[] { "record-once", "--confirm-authorization-sha256", Digest('a'), "--acknowledge-openrouter-paid-one-shot-18000-microusd", "extra" },
            new[] { "preflight", "--activation-bundle", @"C:\caller-authored.json" },
            new[] { "credential-store", "--account-binding", "byok-account-sha256-" + new string('a', 64) },
            new[] { "validate" }, new[] { "validate", "--confirm-authorization-sha256", "BAD" },
            new[] { "live" }, new[] { "execute" }, new[] { "retry" }, new[] { "record-once", "--acknowledge-openrouter-paid-one-shot-18000-microusd" }
        })
        {
            StringWriter output = new(); StringWriter error = new();
            int exit = await OpenRouterCliApplication.RunAsync(arguments, () => factory, output, error, CancellationToken.None);
            Assert.Equal(2, exit);
            Assert.DoesNotContain("secret-sentinel", output.ToString() + error.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(0, module.Calls);
        Assert.Equal(0, factory.CreateCalls);
    }

    [Fact]
    public async Task ValidateRequiresAndForwardsTheExactAuthorizationDigest()
    {
        FakeCliModule module = new(); FakeCliFactory factory = new(module);
        StringWriter output = new(); StringWriter error = new();
        int exit = await OpenRouterCliApplication.RunAsync(
            ["validate", "--confirm-authorization-sha256", Digest('c')],
            () => factory, output, error, CancellationToken.None);
        Assert.Equal(0, exit);
        Assert.Equal(Digest('c'), module.ValidationConfirmation);
        Assert.Equal(1, factory.StateCreateCalls);
        Assert.Equal(0, factory.CredentialCreateCalls);
    }

    [Fact]
    public async Task CredentialAdministrationBypassesTheStateModule()
    {
        FakeCliModule module = new(); FakeCliFactory factory = new(module);
        int exit = await OpenRouterCliApplication.RunAsync(
            ["credential-delete", "--acknowledge-delete-pinned-openrouter-credential"],
            () => factory, new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal(0, exit);
        Assert.Equal(1, factory.CredentialCreateCalls);
        Assert.Equal(0, factory.StateCreateCalls);
    }

    [Fact]
    public async Task RecordOnceRequiresExactAcknowledgementAndAuthorizationDigest()
    {
        FakeCliModule module = new(); StringWriter output = new(); StringWriter error = new();
        int exit = await OpenRouterCliApplication.RunAsync(
            ["record-once", "--confirm-authorization-sha256", Digest('a'), "--acknowledge-openrouter-paid-one-shot-18000-microusd"],
            () => new FakeCliFactory(module), output, error, CancellationToken.None);
        Assert.Equal(0, exit);
        Assert.Equal(Digest('a'), module.Confirmation);
        Assert.Contains("additional_attempt_authorized=false", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanEmitsTheFrozenRawFreeContractWithoutProductionDispatch()
    {
        int dispatchesBefore = OpenRouterCliApplication.ProductionDispatchCount;
        StringWriter output = new(); StringWriter error = new();

        int exit = await OpenRouterCliApplication.RunEntrypointAsync(
            ["plan"], output, error, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(dispatchesBefore, OpenRouterCliApplication.ProductionDispatchCount);
        Assert.Empty(error.ToString());
        Assert.Equal(OpenRouterPremiumPaidRunReadinessManifest.Create().ToCliLine()
            + Environment.NewLine, output.ToString());
        Assert.DoesNotContain("credential", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization_digest", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plan", "extra")]
    [InlineData("plan", "--api-key")]
    public async Task PlanRejectsExtraArgumentsWithoutCreatingAProductionModule(params string[] arguments)
    {
        int dispatchesBefore = OpenRouterCliApplication.ProductionDispatchCount;
        StringWriter output = new(); StringWriter error = new();

        int exit = await OpenRouterCliApplication.RunEntrypointAsync(
            arguments, output, error, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Equal(dispatchesBefore, OpenRouterCliApplication.ProductionDispatchCount);
        Assert.Empty(output.ToString());
        Assert.Equal("OPENROUTER_FAILED code=arguments_invalid" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public void ProductionHttpFactoryRetainsClosedTransportControls()
    {
        using OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateProduction();
        Assert.False(exchange.RedirectsAllowed);
        Assert.False(exchange.AutomaticRetriesAllowed);
        Assert.False(exchange.ProxyAllowed);
        Assert.False(exchange.CookiesAllowed);
        Assert.False(exchange.AmbientAuthenticationAllowed);
        Assert.False(exchange.AutomaticDecompressionAllowed);
        using SocketsHttpHandler handler = OpenRouterPremiumHardenedHttp.CreateSocketsHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Null(handler.Proxy);
        Assert.False(handler.UseCookies);
        Assert.Equal(0, handler.CookieContainer.Count);
        Assert.Null(handler.Credentials);
        Assert.False(handler.PreAuthenticate);
        Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression);
        Assert.Equal(8, handler.MaxResponseHeadersLength);
        Assert.Equal(TimeSpan.FromSeconds(5), handler.ConnectTimeout);
        Assert.Equal(1, handler.MaxConnectionsPerServer);
        Assert.Equal(TimeSpan.FromSeconds(60), handler.PooledConnectionLifetime);
        Assert.Equal(TimeSpan.FromSeconds(5), handler.PooledConnectionIdleTimeout);
        Assert.Equal(0, handler.MaxResponseDrainSize);
        Assert.Equal(TimeSpan.Zero, handler.ResponseDrainTimeout);
        Assert.Null(handler.ActivityHeadersPropagator);
        Assert.Equal(OpenRouterPremiumHardenedHttp.PolicyDigestSha256, OpenRouterPremiumHttpExchange.HardenedHttpPolicyDigestSha256);
        Assert.Equal(OpenRouterPremiumHardenedHttp.PolicyDigestSha256, OpenRouterPremiumHttpMetadataVerifier.HardenedHttpPolicyDigestSha256);
    }

    [Fact]
    public async Task CredentialLeaseCancelsAtItsOwnLifetimeAndClearsTheExactOwnedBuffer()
    {
        byte[] owned = Enumerable.Repeat((byte)0x5a, 32).ToArray();
        bool observedZero = false;
        using CredentialLease lease = new(owned, 1_025, value => observedZero = value);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await lease.ExecuteOnceAsync(
            1_000, async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            }, CancellationToken.None));

        Assert.True(observedZero);
        Assert.All(owned, value => Assert.Equal(0, value));
    }

    [Fact]
    public void ProductionPreflightPublicSurfaceAcceptsNoCallerEvidenceOrEligibilityBooleans()
    {
        MethodInfo preflight = Assert.Single(typeof(OpenRouterPremiumProductionBridge).GetMethods(
            BindingFlags.Public | BindingFlags.Instance), method => method.Name == nameof(OpenRouterPremiumProductionBridge.PreflightAsync));
        ParameterInfo parameter = Assert.Single(preflight.GetParameters());
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.DoesNotContain(typeof(OpenRouterPremiumProductionBridge).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(method => method.GetParameters()),
            value => value.ParameterType == typeof(bool)
                || value.Name!.Contains("account", StringComparison.OrdinalIgnoreCase)
                || value.Name.Contains("catalog", StringComparison.OrdinalIgnoreCase)
                || value.Name.Contains("bundle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void V2StateModuleHasExactlyThreeOperationsAndNoCredentialOrDiscoverySurface()
    {
        MethodInfo[] methods = typeof(OpenRouterPremiumV2ProductionBridge).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "PreflightAsync", "RecordOnceAsync", "ValidateOnce" },
            methods.Select(method => method.Name));
        Assert.DoesNotContain(methods, method => new[] { "credential", "delete", "list", "latest",
            "scan", "repair", "retry", "resume", "migrate", "import", "archive" }
            .Any(token => method.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(typeof(OpenRouterPremiumProductionCredentialAdministration)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => field.FieldType == typeof(IOpenRouterPremiumStateTrustAnchorLeaseSource)
                || field.FieldType == typeof(IOpenRouterPremiumV2StateStoreFactory));
    }

    [Fact]
    public async Task StateAnchorProvisioningInvalidArgumentsFailBeforeEveryProductionFactory()
    {
        Assert.Equal("state-anchor-provision-once", OpenRouterCliApplication.StateAnchorProvisionCommand);
        Assert.Equal("--acknowledge-create-fixed-v2-state-anchor-and-initialize-offline",
            OpenRouterCliApplication.StateAnchorProvisionAcknowledgement);
        int administrationFactoriesBefore =
            ProductionOpenRouterStateAnchorAdministrationFactory.ConstructionCount;
        int stateFactoriesBefore = ProductionOpenRouterCliModuleFactory.ConstructionCount;
        int dispatchesBefore = OpenRouterCliApplication.ProductionDispatchCount;
        int invocationsBefore = OpenRouterCliApplication.StateAnchorProvisioningInvocationCount;

        foreach (string[] arguments in new[]
        {
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand },
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand, "--wrong-acknowledgement" },
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand,
                OpenRouterCliApplication.StateAnchorProvisionAcknowledgement, "extra" },
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand, "--root", @"C:\secret-root" },
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand, "--path", @"C:\secret-path" },
            new[] { OpenRouterCliApplication.StateAnchorProvisionCommand,
                OpenRouterCliApplication.StateAnchorProvisionAcknowledgement, "--api-key", "secret-sentinel" }
        })
        {
            StringWriter output = new();
            StringWriter error = new();
            int exit = await OpenRouterCliApplication.RunEntrypointAsync(
                arguments, output, error, CancellationToken.None);
            Assert.Equal(2, exit);
            Assert.Empty(output.ToString());
            Assert.Equal("OPENROUTER_FAILED code=arguments_invalid" + Environment.NewLine,
                error.ToString());
            Assert.DoesNotContain("secret", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(administrationFactoriesBefore,
            ProductionOpenRouterStateAnchorAdministrationFactory.ConstructionCount);
        Assert.Equal(stateFactoriesBefore, ProductionOpenRouterCliModuleFactory.ConstructionCount);
        Assert.Equal(dispatchesBefore, OpenRouterCliApplication.ProductionDispatchCount);
        Assert.Equal(invocationsBefore,
            OpenRouterCliApplication.StateAnchorProvisioningInvocationCount);
    }

    [Fact]
    public void StateAnchorProvisioningSuccessIsBoundedRawFreeAndUsesOnlyDedicatedAdministration()
    {
        OpenRouterPremiumV2StateAnchorProvisioningResult result = new(
            Digest('a'), Digest('b'), "fixed_local_app_data_v2_no_v1_observation",
            OpenRouterCredentialRead: false, ProviderAccess: false,
            AdditionalAttemptAuthorized: false);
        FakeStateAnchorAdministrationModule module = new(result);
        FakeStateAnchorAdministrationFactory factory = new(module);
        int invocationsBefore = OpenRouterCliApplication.StateAnchorProvisioningInvocationCount;
        int productionDispatchesBefore = OpenRouterCliApplication.ProductionDispatchCount;
        StringWriter output = new();
        StringWriter error = new();

        int exit = OpenRouterCliApplication.RunStateAnchorProvisioning(
            [OpenRouterCliApplication.StateAnchorProvisionCommand,
                OpenRouterCliApplication.StateAnchorProvisionAcknowledgement],
            () => factory, output, error);

        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        Assert.Equal(1, factory.CreateCalls);
        Assert.Equal(1, module.Calls);
        Assert.Equal(invocationsBefore + 1,
            OpenRouterCliApplication.StateAnchorProvisioningInvocationCount);
        Assert.Equal(productionDispatchesBefore, OpenRouterCliApplication.ProductionDispatchCount);
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"STATE_ANCHOR_PROVISIONED anchor_identity_sha256={Digest('a')} state_contract_digest_sha256={Digest('b')} state_root_policy=fixed_local_app_data_v2_no_v1_observation openrouter_credential_read=false provider_access=false additional_attempt_authorized=false{Environment.NewLine}"),
            output.ToString());
        Assert.DoesNotContain(OpenRouterPremiumWindowsStateTrustAnchorSource.TargetIdentity,
            output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-sentinel", output.ToString(), StringComparison.Ordinal);
        Assert.True(output.ToString().Length < 512);
    }

    [Fact]
    public void StateAnchorProvisionedInitializationFailureHasDistinctRawFreeTerminalCode()
    {
        FakeStateAnchorAdministrationModule module = new(
            new OpenRouterPremiumProductionException(
                "state_anchor_provisioned_initialization_failed"));
        FakeStateAnchorAdministrationFactory factory = new(module);
        StringWriter output = new();
        StringWriter error = new();

        int exit = OpenRouterCliApplication.RunStateAnchorProvisioning(
            [OpenRouterCliApplication.StateAnchorProvisionCommand,
                OpenRouterCliApplication.StateAnchorProvisionAcknowledgement],
            () => factory, output, error);

        Assert.Equal(1, exit);
        Assert.Empty(output.ToString());
        Assert.Equal("OPENROUTER_FAILED code=state_anchor_provisioned_initialization_failed"
            + Environment.NewLine, error.ToString());
        Assert.Equal(1, factory.CreateCalls);
        Assert.Equal(1, module.Calls);
    }

    private static string Digest(char value) => new(value, 64);

    private sealed class FakeCliFactory(FakeCliModule module) : IOpenRouterCliModuleFactory
    {
        public int CreateCalls { get; private set; }
        public int CredentialCreateCalls { get; private set; }
        public int StateCreateCalls { get; private set; }
        public IOpenRouterCliCredentialModule CreateCredential()
        {
            CreateCalls++; CredentialCreateCalls++; return module;
        }
        public IOpenRouterCliStateModule CreateState()
        {
            CreateCalls++; StateCreateCalls++; return module;
        }
    }

    private sealed class FakeCliModule : IOpenRouterCliCredentialModule, IOpenRouterCliStateModule
    {
        public int Calls { get; private set; }
        public string? Confirmation { get; private set; }
        public string? ValidationConfirmation { get; private set; }
        public OpenRouterCliCredentialResult StoreCredential(char[] ownedSecret) => throw new NotSupportedException();
        public OpenRouterCliCredentialResult DeleteCredential() { Calls++; return new(); }
        public ValueTask<OpenRouterCliPreflightResult> PreflightAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<OpenRouterCliRunResult> RecordOnceAsync(string authorizationDigestSha256, CancellationToken cancellationToken)
        {
            Calls++; Confirmation = authorizationDigestSha256;
            return ValueTask.FromResult(new OpenRouterCliRunResult("complete", 12, 1, null, Digest('b')));
        }
        public OpenRouterCliValidationResult ValidateOnce(string authorizationDigestSha256)
        {
            Calls++; ValidationConfirmation = authorizationDigestSha256;
            return new("complete", 12, 1, Digest('b'), Digest('d'));
        }
    }

    private sealed class FakeStateAnchorAdministrationFactory(
        FakeStateAnchorAdministrationModule module)
        : IOpenRouterCliStateAnchorAdministrationFactory
    {
        public int CreateCalls { get; private set; }
        public IOpenRouterCliStateAnchorAdministrationModule CreateStateAnchorAdministration()
        {
            CreateCalls++;
            return module;
        }
    }

    private sealed class FakeStateAnchorAdministrationModule
        : IOpenRouterCliStateAnchorAdministrationModule
    {
        private readonly OpenRouterPremiumV2StateAnchorProvisioningResult? _result;
        private readonly Exception? _error;
        public FakeStateAnchorAdministrationModule(
            OpenRouterPremiumV2StateAnchorProvisioningResult result) => _result = result;
        public FakeStateAnchorAdministrationModule(Exception error) => _error = error;
        public int Calls { get; private set; }

        public OpenRouterPremiumV2StateAnchorProvisioningResult ProvisionOnceAndInitializeOffline()
        {
            Calls++;
            if (_error is not null) throw _error;
            return _result!;
        }
    }
}
