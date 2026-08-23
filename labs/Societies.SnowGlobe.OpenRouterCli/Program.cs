using System.Globalization;
using Societies.SnowGlobe;

return await OpenRouterCliApplication.RunEntrypointAsync(
    args, Console.Out, Console.Error, CancellationToken.None);

internal static class OpenRouterCliApplication
{
    private const int ExitAccepted = 0;
    private const int ExitUnexpected = 1;
    private const int ExitArguments = 2;
    private const int ExitTerminal = 3;
    internal const string StateAnchorProvisionCommand = "state-anchor-provision-once";
    internal const string StateAnchorProvisionAcknowledgement =
        "--acknowledge-create-fixed-v2-state-anchor-and-initialize-offline";
    private static int _productionDispatchCount;
    private static int _stateAnchorProvisioningInvocationCount;
    internal static int ProductionDispatchCount => Volatile.Read(ref _productionDispatchCount);
    internal static int StateAnchorProvisioningInvocationCount =>
        Volatile.Read(ref _stateAnchorProvisioningInvocationCount);
    private static readonly string[] ForbiddenTokens =
    [
        "--api-key", "--key", "--token", "--secret", "--password", "--credential",
        "live", "execute", "retry", "--live", "--execute", "--retry", "--fallback", "--proxy"
    ];

    internal static async ValueTask<int> RunEntrypointAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        if (args.Length > 0 && string.Equals(args[0], "plan", StringComparison.Ordinal))
            return RunOfflinePlan(args, output, error);
        if (args.Length > 0 && string.Equals(args[0], StateAnchorProvisionCommand, StringComparison.Ordinal))
            return RunStateAnchorProvisioning(args,
                ProductionOpenRouterStateAnchorAdministrationFactory.CreateFactory,
                output, error);
        Interlocked.Increment(ref _productionDispatchCount);
        return await RunAsync(args, ProductionOpenRouterCliModuleFactory.CreateFactory,
            output, error, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<int> RunAsync(string[] args, Func<IOpenRouterCliModuleFactory> factoryFactory,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(factoryFactory);
        ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 0 || args.Any(value => ForbiddenTokens.Contains(value, StringComparer.OrdinalIgnoreCase)))
            return Fail(error, ExitArguments, "arguments_invalid");
        try
        {
            switch (args[0])
            {
                case "credential-store":
                    if (args.Length != 1) return Fail(error, ExitArguments, "arguments_invalid");
                    char[] owned = ConsoleSecretReader.ReadOwned();
                    try
                    {
                        _ = CreateCredentialModule(factoryFactory).StoreCredential(owned);
                        output.WriteLine("CREDENTIAL_STORED account_binding=pending_authenticated_preflight target_pinned=true secret_logged=false environment_used=false");
                        return ExitAccepted;
                    }
                    finally { Array.Clear(owned); }

                case "credential-delete":
                    if (args.Length != 2 || args[1] != "--acknowledge-delete-pinned-openrouter-credential")
                        return Fail(error, ExitArguments, "arguments_invalid");
                    _ = CreateCredentialModule(factoryFactory).DeleteCredential();
                    output.WriteLine("CREDENTIAL_DELETED target_pinned=true");
                    return ExitAccepted;

                case "preflight":
                    if (args.Length != 1) return Fail(error, ExitArguments, "arguments_invalid");
                    OpenRouterCliPreflightResult preflight = await CreateStateModule(factoryFactory).PreflightAsync(cancellationToken).ConfigureAwait(false);
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"PREFLIGHT_ACCEPTED authorization_digest_sha256={preflight.AuthorizationDigestSha256} preflight_artifact_digest_sha256={preflight.PreflightArtifactDigestSha256} account_binding={preflight.AccountBindingIdentity} expires_at_unix_ms={preflight.ExpiresAtUnixMilliseconds} maximum_requests=12 aggregate_cost_ceiling_microusd=18000 live_authorized_by_config=false additional_attempt_authorized=false"));
                    return ExitAccepted;

                case "record-once":
                    if (args.Length != 4 || args[1] != "--confirm-authorization-sha256" || !IsDigest(args[2])
                        || args[3] != "--acknowledge-openrouter-paid-one-shot-18000-microusd")
                        return Fail(error, ExitArguments, "arguments_invalid");
                    OpenRouterCliRunResult run = await CreateStateModule(factoryFactory).RecordOnceAsync(args[2], cancellationToken).ConfigureAwait(false);
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"RECORD_ONCE_RESULT status={run.Status} exchange_count={run.ExchangeCount} total_settled_microusd={run.TotalSettledMicrousd} terminal={run.TerminalCode ?? "none"} evidence_artifact_digest_sha256={run.EvidenceArtifactDigestSha256} additional_attempt_authorized=false"));
                    return run.Status == "complete" ? ExitAccepted : ExitTerminal;

                case "validate":
                    if (args.Length != 3 || args[1] != "--confirm-authorization-sha256" || !IsDigest(args[2]))
                        return Fail(error, ExitArguments, "arguments_invalid");
                    OpenRouterCliValidationResult validation = CreateStateModule(factoryFactory).ValidateOnce(args[2]);
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"VALIDATION_ACCEPTED status={validation.Status} exchange_count={validation.ExchangeCount} total_settled_microusd={validation.TotalSettledMicrousd} evidence_artifact_digest_sha256={validation.EvidenceArtifactDigestSha256} validation_receipt_digest_sha256={validation.ValidationReceiptDigestSha256} additional_attempt_authorized=false"));
                    return ExitAccepted;

                default:
                    return Fail(error, ExitArguments, "arguments_invalid");
            }
        }
        catch (OpenRouterPremiumProductionException exception) { return Fail(error, ExitUnexpected, exception.Code); }
        catch (OpenRouterPremiumEvidenceException exception) { return Fail(error, ExitUnexpected, exception.Code); }
        catch (ProviderPreflightException exception) { return Fail(error, ExitUnexpected, ToCode(exception.ReasonCode)); }
        catch (OperationCanceledException) { return Fail(error, ExitUnexpected, "operation_cancelled"); }
        catch { return Fail(error, ExitUnexpected, "invocation_failed"); }
    }

    private static int RunOfflinePlan(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1) return Fail(error, ExitArguments, "arguments_invalid");
        try
        {
            output.WriteLine(OpenRouterPremiumPaidRunReadinessManifest.Create().ToCliLine());
            return ExitAccepted;
        }
        catch (OpenRouterPremiumEvidenceException exception) { return Fail(error, ExitUnexpected, exception.Code); }
        catch { return Fail(error, ExitUnexpected, "invocation_failed"); }
    }

    internal static int RunStateAnchorProvisioning(
        string[] args,
        Func<IOpenRouterCliStateAnchorAdministrationFactory> factoryFactory,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(factoryFactory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length != 2
            || !string.Equals(args[0], StateAnchorProvisionCommand, StringComparison.Ordinal)
            || !string.Equals(args[1], StateAnchorProvisionAcknowledgement, StringComparison.Ordinal))
            return Fail(error, ExitArguments, "arguments_invalid");

        Interlocked.Increment(ref _stateAnchorProvisioningInvocationCount);
        try
        {
            IOpenRouterCliStateAnchorAdministrationFactory factory = factoryFactory()
                ?? throw new InvalidOperationException("production_factory_unavailable");
            IOpenRouterCliStateAnchorAdministrationModule module =
                factory.CreateStateAnchorAdministration()
                ?? throw new InvalidOperationException("production_module_unavailable");
            OpenRouterPremiumV2StateAnchorProvisioningResult result =
                module.ProvisionOnceAndInitializeOffline();
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"STATE_ANCHOR_PROVISIONED anchor_identity_sha256={result.AnchorIdentitySha256} state_contract_digest_sha256={result.StateContractDigestSha256} state_root_policy={result.StateRootPolicy} openrouter_credential_read={result.OpenRouterCredentialRead.ToString().ToLowerInvariant()} provider_access={result.ProviderAccess.ToString().ToLowerInvariant()} additional_attempt_authorized={result.AdditionalAttemptAuthorized.ToString().ToLowerInvariant()}"));
            return ExitAccepted;
        }
        catch (OpenRouterPremiumProductionException exception)
        {
            return Fail(error, ExitUnexpected, exception.Code);
        }
        catch
        {
            return Fail(error, ExitUnexpected, "invocation_failed");
        }
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static IOpenRouterCliCredentialModule CreateCredentialModule(
        Func<IOpenRouterCliModuleFactory> factoryFactory) =>
        (factoryFactory() ?? throw new InvalidOperationException("production_factory_unavailable")).CreateCredential()
        ?? throw new InvalidOperationException("production_module_unavailable");
    private static IOpenRouterCliStateModule CreateStateModule(
        Func<IOpenRouterCliModuleFactory> factoryFactory) =>
        (factoryFactory() ?? throw new InvalidOperationException("production_factory_unavailable")).CreateState()
        ?? throw new InvalidOperationException("production_module_unavailable");
    private static string ToCode(ProviderPreflightReasonCode code) => "credential_lease_" + code.ToString().ToLowerInvariant();
    private static int Fail(TextWriter error, int exit, string code) { error.WriteLine($"OPENROUTER_FAILED code={code}"); return exit; }
}

internal static class ConsoleSecretReader
{
    internal static char[] ReadOwned()
    {
        if (Console.IsInputRedirected) throw new OpenRouterPremiumProductionException("interactive_secret_required");
        char[] buffer = new char[512]; int count = 0;
        try
        {
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace) { if (count > 0) buffer[--count] = '\0'; continue; }
                if (key.KeyChar == '\0' || char.IsControl(key.KeyChar) || count >= buffer.Length)
                    throw new OpenRouterPremiumProductionException("credential_malformed");
                buffer[count++] = key.KeyChar;
            }
            if (count == 0) throw new OpenRouterPremiumProductionException("credential_malformed");
            return buffer.AsSpan(0, count).ToArray();
        }
        finally { Array.Clear(buffer); }
    }
}

internal interface IOpenRouterCliModuleFactory
{
    IOpenRouterCliCredentialModule CreateCredential();
    IOpenRouterCliStateModule CreateState();
}
internal interface IOpenRouterCliCredentialModule
{
    OpenRouterCliCredentialResult StoreCredential(char[] ownedSecret);
    OpenRouterCliCredentialResult DeleteCredential();
}
internal interface IOpenRouterCliStateModule
{
    ValueTask<OpenRouterCliPreflightResult> PreflightAsync(CancellationToken cancellationToken);
    ValueTask<OpenRouterCliRunResult> RecordOnceAsync(string authorizationDigestSha256, CancellationToken cancellationToken);
    OpenRouterCliValidationResult ValidateOnce(string authorizationDigestSha256);
}

internal interface IOpenRouterCliStateAnchorAdministrationFactory
{
    IOpenRouterCliStateAnchorAdministrationModule CreateStateAnchorAdministration();
}

internal interface IOpenRouterCliStateAnchorAdministrationModule
{
    OpenRouterPremiumV2StateAnchorProvisioningResult ProvisionOnceAndInitializeOffline();
}

internal sealed record OpenRouterCliCredentialResult;
internal sealed record OpenRouterCliPreflightResult(string AuthorizationDigestSha256, string PreflightArtifactDigestSha256,
    string AccountBindingIdentity, long ExpiresAtUnixMilliseconds);
internal sealed record OpenRouterCliRunResult(string Status, int ExchangeCount, long TotalSettledMicrousd,
    string? TerminalCode, string EvidenceArtifactDigestSha256);
internal sealed record OpenRouterCliValidationResult(string Status, int ExchangeCount, long TotalSettledMicrousd,
    string EvidenceArtifactDigestSha256, string ValidationReceiptDigestSha256);

internal sealed class ProductionOpenRouterCliModuleFactory : IOpenRouterCliModuleFactory
{
    private static int _constructionCount;
    internal static int ConstructionCount => Volatile.Read(ref _constructionCount);
    internal static IOpenRouterCliModuleFactory CreateFactory() => new ProductionOpenRouterCliModuleFactory();

    private ProductionOpenRouterCliModuleFactory() => Interlocked.Increment(ref _constructionCount);
    public IOpenRouterCliCredentialModule CreateCredential() => new ProductionOpenRouterCredentialCliModule(
        OpenRouterPremiumProductionCredentialAdministration.CreateDefault());
    public IOpenRouterCliStateModule CreateState() => new ProductionOpenRouterStateCliModule(
        OpenRouterPremiumV2ProductionBridge.CreateDefault());
}

internal sealed class ProductionOpenRouterStateAnchorAdministrationFactory
    : IOpenRouterCliStateAnchorAdministrationFactory
{
    private static int _constructionCount;
    internal static int ConstructionCount => Volatile.Read(ref _constructionCount);
    internal static IOpenRouterCliStateAnchorAdministrationFactory CreateFactory() =>
        new ProductionOpenRouterStateAnchorAdministrationFactory();

    private ProductionOpenRouterStateAnchorAdministrationFactory() =>
        Interlocked.Increment(ref _constructionCount);

    public IOpenRouterCliStateAnchorAdministrationModule CreateStateAnchorAdministration() =>
        new ProductionOpenRouterStateAnchorAdministrationModule(
            OpenRouterPremiumV2StateAnchorAdministration.CreateDefault());
}

internal sealed class ProductionOpenRouterStateAnchorAdministrationModule(
    OpenRouterPremiumV2StateAnchorAdministration administration)
    : IOpenRouterCliStateAnchorAdministrationModule
{
    public OpenRouterPremiumV2StateAnchorProvisioningResult ProvisionOnceAndInitializeOffline() =>
        administration.ProvisionOnceAndInitializeOffline();
}

internal sealed class ProductionOpenRouterCredentialCliModule(
    OpenRouterPremiumProductionCredentialAdministration administration) : IOpenRouterCliCredentialModule
{
    public OpenRouterCliCredentialResult StoreCredential(char[] ownedSecret)
    {
        administration.Store(ownedSecret); return new();
    }
    public OpenRouterCliCredentialResult DeleteCredential()
    {
        administration.Delete(); return new();
    }
}

internal sealed class ProductionOpenRouterStateCliModule(OpenRouterPremiumV2ProductionBridge bridge)
    : IOpenRouterCliStateModule
{
    public async ValueTask<OpenRouterCliPreflightResult> PreflightAsync(CancellationToken cancellationToken)
    {
        OpenRouterPremiumProductionPreflightResult result = await bridge.PreflightAsync(cancellationToken).ConfigureAwait(false);
        return new(result.AuthorizationDigestSha256, result.PreflightArtifactDigestSha256,
            result.AccountBindingIdentity, result.ExpiresAtUnixMilliseconds);
    }
    public async ValueTask<OpenRouterCliRunResult> RecordOnceAsync(string authorizationDigestSha256, CancellationToken cancellationToken)
    {
        OpenRouterPremiumProductionRunResult result = await bridge.RecordOnceAsync(authorizationDigestSha256, cancellationToken).ConfigureAwait(false);
        return new(result.Status, result.ExchangeCount, result.TotalSettledMicrousd, result.TerminalCode, result.EvidenceArtifactDigestSha256);
    }
    public OpenRouterCliValidationResult ValidateOnce(string authorizationDigestSha256)
    {
        OpenRouterPremiumProductionValidationResult result = bridge.ValidateOnce(authorizationDigestSha256);
        return new(result.Status, result.ExchangeCount, result.TotalSettledMicrousd,
            result.EvidenceArtifactDigestSha256, result.ValidationReceiptDigestSha256);
    }
}
