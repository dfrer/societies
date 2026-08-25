using System.Globalization;
using Societies.SnowGlobe;

internal interface IProviderReadinessOneShotCommandFactory
{
    ProviderReadinessOneShotCommand Create();
}

internal static class ProviderReadinessOneShotCliApplication
{
    internal const string CommandName = "provider-readiness-record-once-v1";
    private const int ExitAccepted = 0;
    private const int ExitUnexpected = 1;
    private const int ExitArguments = 2;
    private const int ExitTerminal = 3;

    internal static async ValueTask<int> RunAsync(
        string[] args,
        IProviderReadinessOneShotCommandFactory commandFactory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(commandFactory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out PinnedRuntimeObservation? runtime))
            return Fail(error, ExitArguments, "arguments_invalid");

        try
        {
            ProviderReadinessOneShotCommand command = commandFactory.Create()
                ?? throw new InvalidOperationException("production_command_unavailable");
            ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(
                runtime!, cancellationToken).ConfigureAwait(false);
            if (result.Status != "published"
                || result.AssessmentStatus != "insufficient_current_attempt_evidence"
                || result.PrimaryAttemptCurrentState != "unknown"
                || result.RoutingInputIssuanceStatus != "not_issued"
                || result.RoutingPolicyInputPresent
                || result.AdditionalAttemptAuthorized
                || !IsDigest(result.OpenRouterArtifactDigestSha256)
                || !IsDigest(result.OllamaArtifactDigestSha256)
                || !IsDigest(result.AssessmentArtifactDigestSha256)
                || result.OpenRouterRequestCount is < 0 or > 3
                || result.OllamaRequestCount is < 0 or > 1)
                return Fail(error, ExitUnexpected, "invocation_failed");

            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"PROVIDER_READINESS_PUBLISHED status={result.Status}" +
                $" openrouter_requests={result.OpenRouterRequestCount}" +
                $" ollama_requests={result.OllamaRequestCount}" +
                $" openrouter_digest_sha256={result.OpenRouterArtifactDigestSha256}" +
                $" ollama_digest_sha256={result.OllamaArtifactDigestSha256}" +
                $" assessment_digest_sha256={result.AssessmentArtifactDigestSha256}" +
                $" assessment_status={result.AssessmentStatus}" +
                $" primary_attempt={result.PrimaryAttemptCurrentState}" +
                $" routing={result.RoutingInputIssuanceStatus}" +
                $" routing_input_present={result.RoutingPolicyInputPresent.ToString().ToLowerInvariant()}" +
                $" openrouter_path={ProviderReadinessOneShotClaimCodec.OpenRouterObservationPath}" +
                $" ollama_path={ProviderReadinessOneShotClaimCodec.OllamaObservationPath}" +
                $" assessment_path={ProviderReadinessOneShotClaimCodec.AssessmentPath}" +
                $" additional_attempt_authorized={result.AdditionalAttemptAuthorized.ToString().ToLowerInvariant()}"));
            return ExitAccepted;
        }
        catch (ProviderReadinessOneShotException exception)
        {
            return Fail(error, ExitTerminal, exception.Code);
        }
        catch
        {
            return Fail(error, ExitUnexpected, "invocation_failed");
        }
    }

    private static bool TryParse(string[] args, out PinnedRuntimeObservation? runtime)
    {
        runtime = null;
        if (args.Length != 5
            || !string.Equals(args[0], CommandName, StringComparison.Ordinal))
            return false;

        string? pidText = null;
        string? ticksText = null;
        for (int index = 1; index < args.Length; index += 2)
        {
            string name = args[index];
            string value = args[index + 1];
            if (string.IsNullOrEmpty(value)) return false;
            switch (name)
            {
                case "--ollama-pid" when pidText is null:
                    pidText = value;
                    break;
                case "--ollama-start-utc-ticks" when ticksText is null:
                    ticksText = value;
                    break;
                default:
                    return false;
            }
        }

        if (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            || pid <= 0
            || !long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            || ticks <= 0
            || ticks > DateTime.MaxValue.Ticks)
            return false;
        runtime = new PinnedRuntimeObservation(pid, ticks);
        return true;
    }

    private static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static int Fail(TextWriter error, int exitCode, string code)
    {
        error.WriteLine($"PROVIDER_READINESS_FAILED code={code}");
        return exitCode;
    }
}

internal sealed class ProductionProviderReadinessOneShotCommandFactory
    : IProviderReadinessOneShotCommandFactory
{
    internal static ProductionProviderReadinessOneShotCommandFactory Instance { get; } = new();
    private ProductionProviderReadinessOneShotCommandFactory() { }

    public ProviderReadinessOneShotCommand Create()
    {
        string repositoryRoot = ProviderReadinessFixedRepositoryLocator.FindVerifiedRoot();
        return new ProviderReadinessOneShotCommand(
            new FileProviderReadinessOneShotArtifactStore(repositoryRoot),
            ProductionProviderReadinessOneShotAdapterFactory.Instance,
            SystemProviderReadinessClock.Instance);
    }
}

internal sealed class ProductionProviderReadinessOneShotAdapterFactory
    : IProviderReadinessOneShotAdapterFactory
{
    internal static ProductionProviderReadinessOneShotAdapterFactory Instance { get; } = new();
    private ProductionProviderReadinessOneShotAdapterFactory() { }

    public IProviderReadinessObservationAdapter CreateOpenRouter() =>
        OpenRouterAuthenticatedReadinessAdapter.CreateProduction();

    public IProviderReadinessObservationAdapter CreateOllama(PinnedRuntimeObservation runtime) =>
        OllamaAuthenticatedReadinessAdapter.CreateProduction(runtime);
}
