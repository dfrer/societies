using System.Globalization;
using Societies.SnowGlobe;

return await RecordingProgram.RunAsync(args, Console.Out, Console.Error);

internal static class RecordingProgram
{
    private static readonly string[] ForbiddenLiveTokens = ["record", "live", "execute", "--record", "--live", "--execute"];

    internal static ValueTask<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        if (args.Any(token => ForbiddenLiveTokens.Contains(token, StringComparer.Ordinal)))
        {
            error.WriteLine("RECORDING_FAILED code=live_mode_not_available");
            return ValueTask.FromResult(2);
        }
        if (args.Length == 0 || args[0] is not ("preflight" or "validate")) return ArgumentsInvalid(error);

        string command = args[0];
        if (!TryParseOptions(args.AsSpan(1), out Dictionary<string, string> options)) return ArgumentsInvalid(error);
        try
        {
            if (command == "preflight")
            {
                if (options.Count != 4
                    || !options.TryGetValue("--repository-root", out string? root)
                    || !options.TryGetValue("--pid", out string? pidText)
                    || !options.TryGetValue("--start-utc-ticks", out string? ticksText)
                    || !options.TryGetValue("--nonce", out string? nonce)
                    || !int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
                    || !long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)) return ArgumentsInvalid(error);
                SnowGlobeOllamaRecordingCompositionModule module = new(root);
                OllamaRecordingCompositionPlan plan = module.Prepare(new PinnedRuntimeObservation(pid, ticks), nonce);
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"PREFLIGHT_ACCEPTED plan_digest_sha256={plan.PlanDigestSha256} artifact={plan.RelativeArtifactPath} io_performed=false live_authorized=false additional_attempt_authorized=false"));
                return ValueTask.FromResult(0);
            }

            if (options.Count != 1 || !options.TryGetValue("--repository-root", out string? validateRoot)) return ArgumentsInvalid(error);
            SnowGlobeOllamaRecordingCompositionModule validator = new(validateRoot);
            OllamaRecordingExecutionArtifact artifact = validator.ValidateArtifact();
            output.WriteLine($"VALIDATION_ACCEPTED artifact_digest_sha256={artifact.CanonicalDigestSha256} outcome={artifact.OutcomeCode} structurally_complete=true additional_attempt_authorized=false");
            return ValueTask.FromResult(0);
        }
        catch (OllamaRecordingCompositionException exception)
        {
            error.WriteLine($"RECORDING_FAILED code={exception.Code}");
            return ValueTask.FromResult(1);
        }
        catch (OllamaRecordingExecutionArtifactException exception)
        {
            error.WriteLine($"RECORDING_FAILED code={exception.Code}");
            return ValueTask.FromResult(1);
        }
        catch
        {
            error.WriteLine("RECORDING_FAILED code=invocation_failure");
            return ValueTask.FromResult(1);
        }
    }

    private static bool TryParseOptions(ReadOnlySpan<string> args, out Dictionary<string, string> options)
    {
        options = new(StringComparer.Ordinal);
        if (args.Length == 0 || args.Length % 2 != 0) return false;
        for (int index = 0; index < args.Length; index += 2)
        {
            string name = args[index]; string value = args[index + 1];
            if (name is not ("--repository-root" or "--pid" or "--start-utc-ticks" or "--nonce") || string.IsNullOrEmpty(value) || !options.TryAdd(name, value)) return false;
        }
        return true;
    }

    private static ValueTask<int> ArgumentsInvalid(TextWriter error)
    {
        error.WriteLine("RECORDING_FAILED code=arguments_invalid");
        return ValueTask.FromResult(2);
    }
}
