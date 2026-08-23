using System.Globalization;
using System.Security.Cryptography;
using Societies.SnowGlobe;

return await OllamaRecordingCliApplication.RunAsync(
    args,
    ProductionOllamaRecordingCliModuleFactory.Instance,
    Console.Out,
    Console.Error,
    CancellationToken.None);

internal static class RecordingProgram
{
    internal static ValueTask<int> RunAsync(string[] args, TextWriter output, TextWriter error) =>
        OllamaRecordingCliApplication.RunAsync(args, ProductionOllamaRecordingCliModuleFactory.Instance, output, error, CancellationToken.None);
}

internal static class OllamaRecordingCliApplication
{
    private const int ExitAccepted = 0;
    private const int ExitUnexpected = 1;
    private const int ExitArguments = 2;
    private const int ExitTerminalArtifact = 3;
    private const int ExitPreExecutionRejected = 4;
    private const int ExitCompositionIndeterminate = 5;
    private static readonly string[] ForbiddenLiveTokens = ["record", "live", "execute", "--record", "--live", "--execute"];

    internal static async ValueTask<int> RunAsync(string[] args, IOllamaRecordingCliModuleFactory moduleFactory, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(moduleFactory); ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        if (args.Any(token => ForbiddenLiveTokens.Contains(token, StringComparer.Ordinal))) return Fail(error, ExitArguments, "live_mode_not_available");
        if (args.Length == 0 || args[0] is not ("preflight" or "validate" or "record-once")) return Fail(error, ExitArguments, "arguments_invalid");
        if (args[0] == "record-once")
        {
            if (!TryParseRecordOnce(args.AsSpan(1), out RecordOnceArguments parsed)) return Fail(error, ExitArguments, "arguments_invalid");
            return await RunRecordOnceAsync(parsed, moduleFactory, output, error, cancellationToken).ConfigureAwait(false);
        }

        if (!TryParsePairedOptions(args.AsSpan(1), out Dictionary<string, string> options)) return Fail(error, ExitArguments, "arguments_invalid");
        try
        {
            if (args[0] == "preflight")
            {
                if (options.Count != 4 || !options.TryGetValue("--repository-root", out string? root) || !options.TryGetValue("--pid", out string? pidText)
                    || !options.TryGetValue("--start-utc-ticks", out string? ticksText) || !options.TryGetValue("--nonce", out string? nonce)
                    || !int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
                    || !long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)) return Fail(error, ExitArguments, "arguments_invalid");
                OllamaRecordingCliPreparedPlan plan = moduleFactory.Create(root).Prepare(new PinnedRuntimeObservation(pid, ticks), nonce);
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"PREFLIGHT_ACCEPTED plan_digest_sha256={plan.PlanDigestSha256} artifact={plan.RelativeArtifactPath} io_performed=false live_authorized=false additional_attempt_authorized=false"));
                return ExitAccepted;
            }

            if (options.Count != 1 || !options.TryGetValue("--repository-root", out string? validateRoot)) return Fail(error, ExitArguments, "arguments_invalid");
            OllamaRecordingCliValidationSummary artifact = moduleFactory.Create(validateRoot).ValidateArtifact();
            output.WriteLine($"VALIDATION_ACCEPTED artifact_digest_sha256={artifact.ArtifactDigestSha256} outcome={artifact.OutcomeCode} structurally_complete=true additional_attempt_authorized=false");
            return ExitAccepted;
        }
        catch (OllamaRecordingCompositionException exception) { return Fail(error, ExitUnexpected, exception.Code); }
        catch (OllamaRecordingExecutionArtifactException exception) { return Fail(error, ExitUnexpected, exception.Code); }
        catch { return Fail(error, ExitUnexpected, "invocation_failure"); }
    }

    private static async ValueTask<int> RunRecordOnceAsync(RecordOnceArguments parsed, IOllamaRecordingCliModuleFactory moduleFactory, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        IOllamaRecordingCliModule module; OllamaRecordingCliPreparedPlan plan;
        try
        {
            module = moduleFactory.Create(parsed.RepositoryRoot);
            plan = module.Prepare(new PinnedRuntimeObservation(parsed.ProcessId, parsed.ProcessStartUtcTicks), parsed.AuthorizationNonce);
        }
        catch (OllamaRecordingCompositionException exception) { return Fail(error, ExitPreExecutionRejected, exception.Code); }
        catch { return Fail(error, ExitUnexpected, "invocation_failure"); }

        if (!FixedTimeDigestEquals(plan.PlanDigestSha256, parsed.ConfirmedPlanDigestSha256)) return Fail(error, ExitPreExecutionRejected, "plan_confirmation_mismatch");
        if (cancellationToken.IsCancellationRequested) return Fail(error, ExitPreExecutionRejected, "operation_cancelled");

        OllamaRecordingCliExecutionSummary result;
        try { result = await module.ExecuteOnceAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Fail(error, ExitCompositionIndeterminate, "composition_indeterminate"); }
        catch (OllamaRecordingCompositionException exception) { return Fail(error, ExitCompositionIndeterminate, exception.Code); }
        catch (OllamaRecordingExecutionArtifactException exception) { return Fail(error, ExitCompositionIndeterminate, exception.Code); }
        catch { return Fail(error, ExitCompositionIndeterminate, "composition_indeterminate"); }

        if (!IsClosedExecutionSummary(result)) return Fail(error, ExitCompositionIndeterminate, "recording_result_invalid");
        if (!result.ArtifactPublished) return Fail(error, result.OutcomeCode is "Cancelled" or "AuthorizationRejected" ? ExitPreExecutionRejected : ExitCompositionIndeterminate, "recording_artifact_not_published");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"RECORD_ONCE_RESULT outcome={result.OutcomeCode} failure={result.FailureCode} completed={FormatNullable(result.CompletedSlotCount)} submission={result.TerminalSubmissionState ?? "none"} status={FormatNullable(result.TerminalStatusCode)} charge={result.TerminalChargeState ?? "none"} checkpoint={result.TerminalCheckpointCode ?? "none"} policy={result.TerminalPolicyCode ?? "none"} additional_attempt_authorized=false artifact_digest_sha256={result.ArtifactDigestSha256} receipt_digest_sha256={result.ReceiptDigestSha256 ?? "none"}"));
        return result.OutcomeCode switch { "Complete" => ExitAccepted, "Failed" or "Cancelled" or "TimedOut" => ExitTerminalArtifact, "AuthorizationRejected" => ExitPreExecutionRejected, _ => ExitCompositionIndeterminate };
    }

    private static bool TryParseRecordOnce(ReadOnlySpan<string> args, out RecordOnceArguments parsed)
    {
        parsed = default;
        if (args.Length != 11) return false;
        Dictionary<string, string> options = new(StringComparer.Ordinal); bool acknowledged = false;
        for (int index = 0; index < args.Length;)
        {
            string name = args[index];
            if (name == "--acknowledge-live-local-loopback")
            {
                if (acknowledged) return false;
                acknowledged = true; index++; continue;
            }
            if (name is not ("--repository-root" or "--pid" or "--start-utc-ticks" or "--nonce" or "--confirm-plan-sha256")
                || index + 1 >= args.Length || string.IsNullOrEmpty(args[index + 1]) || !options.TryAdd(name, args[index + 1])) return false;
            index += 2;
        }
        if (!acknowledged || options.Count != 5 || !options.TryGetValue("--repository-root", out string? root) || !options.TryGetValue("--pid", out string? pidText)
            || !options.TryGetValue("--start-utc-ticks", out string? ticksText) || !options.TryGetValue("--nonce", out string? nonce)
            || !options.TryGetValue("--confirm-plan-sha256", out string? confirmation) || !IsCanonicalRepositoryRoot(root)
            || !int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0
            || !long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) || ticks <= 0 || ticks > DateTime.MaxValue.Ticks
            || !IsCanonicalIdentity(nonce) || !IsLowerDigest(confirmation)) return false;
        parsed = new RecordOnceArguments(root, pid, ticks, nonce, confirmation); return true;
    }

    private static bool TryParsePairedOptions(ReadOnlySpan<string> args, out Dictionary<string, string> options)
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

    private static bool IsClosedExecutionSummary(OllamaRecordingCliExecutionSummary result)
    {
        if (result is null
            || result.ArtifactPublished != (result.ArtifactDigestSha256 is not null)
            || result.ArtifactDigestSha256 is not null && !IsLowerDigest(result.ArtifactDigestSha256)
            || result.ReceiptPresent != (result.ReceiptDigestSha256 is not null)
            || result.ReceiptDigestSha256 is not null && !IsLowerDigest(result.ReceiptDigestSha256)) return false;

        if (!result.ArtifactPublished)
        {
            return result.OutcomeCode == "Cancelled" && result.FailureCode == "Cancelled"
                && !result.RecordingResultPresent && result.RecordingOutcomeCode is null && result.RecordingFailureCode is null
                && result.CompletedSlotCount is null && result.TerminalSlotOrdinal is null && result.TerminalSubmissionState is null
                && result.TerminalStatusCode is null && result.TerminalChargeState is null
                && !result.ReceiptPresent && result.ReceiptDigestSha256 is null
                && !result.TerminalReceiptRowPresent && !result.TerminalWrapperDigestPresent
                && !result.NestedEvidenceDigestPresent && result.TerminalCheckpointCode is null && result.TerminalPolicyCode is null;
        }

        if (result.RecordingResultPresent ? result.TerminalChargeState != "NotApplicable" : result.TerminalChargeState is not null)
            return false;
        return OllamaRecordingTerminalCoherenceModule.TryParseAndValidate(
            result.OutcomeCode, result.FailureCode, result.RecordingResultPresent,
            result.RecordingOutcomeCode, result.RecordingFailureCode, result.CompletedSlotCount,
            result.TerminalSlotOrdinal, result.TerminalSubmissionState, result.TerminalStatusCode,
            result.ReceiptPresent, result.TerminalReceiptRowPresent, result.TerminalWrapperDigestPresent,
            result.NestedEvidenceDigestPresent, result.TerminalCheckpointCode, result.TerminalPolicyCode);
    }

    private static bool FixedTimeDigestEquals(string actual, string confirmation)
    {
        if (!IsLowerDigest(actual) || !IsLowerDigest(confirmation)) return false;
        byte[] actualBytes = Convert.FromHexString(actual); byte[] confirmationBytes = Convert.FromHexString(confirmation);
        try { return CryptographicOperations.FixedTimeEquals(actualBytes, confirmationBytes); }
        finally { CryptographicOperations.ZeroMemory(actualBytes); CryptographicOperations.ZeroMemory(confirmationBytes); }
    }

    private static bool IsCanonicalRepositoryRoot(string value)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) return false;
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)); } catch { return false; }
        if (!string.Equals(value, full, StringComparison.OrdinalIgnoreCase) || full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal) || full.StartsWith(@"\??\", StringComparison.Ordinal)) return false;
        string? root = Path.GetPathRoot(full);
        if (root is null || root.Length != 3 || !char.IsAsciiLetter(root[0]) || root[1] != ':' || root[2] != '\\' || full.Length <= root.Length) return false;
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (string segment in full[root.Length..].Split('\\'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment[^1] is ' ' or '.' || segment.IndexOfAny(invalid) >= 0 || segment.Any(static value => value < ' ')) return false;
            string stem = segment.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9') return false;
        }
        return true;
    }

    private static bool IsCanonicalIdentity(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value[0] == '/' || value[^1] == '/') return false;
        foreach (char current in value) if (!((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current is '_' or '-' or '.' or '/')) return false;
        return !value.Contains("//", StringComparison.Ordinal);
    }

    private static bool IsLowerDigest(string? value) => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string FormatNullable(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "none";
    private static int Fail(TextWriter error, int exitCode, string code) { error.WriteLine($"RECORDING_FAILED code={code}"); return exitCode; }
    private readonly record struct RecordOnceArguments(string RepositoryRoot, int ProcessId, long ProcessStartUtcTicks, string AuthorizationNonce, string ConfirmedPlanDigestSha256);
}

internal interface IOllamaRecordingCliModuleFactory { IOllamaRecordingCliModule Create(string absoluteRepositoryRoot); }
internal interface IOllamaRecordingCliModule
{
    OllamaRecordingCliPreparedPlan Prepare(PinnedRuntimeObservation runtime, string authorizationNonce);
    ValueTask<OllamaRecordingCliExecutionSummary> ExecuteOnceAsync(CancellationToken cancellationToken);
    OllamaRecordingCliValidationSummary ValidateArtifact();
}
internal sealed record OllamaRecordingCliPreparedPlan(string PlanDigestSha256, string RelativeArtifactPath);
internal sealed record OllamaRecordingCliValidationSummary(string ArtifactDigestSha256, string OutcomeCode);
internal sealed record OllamaRecordingCliExecutionSummary(
    string OutcomeCode,
    string FailureCode,
    bool RecordingResultPresent,
    string? RecordingOutcomeCode,
    string? RecordingFailureCode,
    int? CompletedSlotCount,
    int? TerminalSlotOrdinal,
    string? TerminalSubmissionState,
    int? TerminalStatusCode,
    string? TerminalChargeState,
    bool ArtifactPublished,
    string? ArtifactDigestSha256,
    bool ReceiptPresent,
    string? ReceiptDigestSha256,
    bool TerminalReceiptRowPresent,
    bool TerminalWrapperDigestPresent,
    bool NestedEvidenceDigestPresent,
    string? TerminalCheckpointCode,
    string? TerminalPolicyCode);

internal sealed class ProductionOllamaRecordingCliModuleFactory : IOllamaRecordingCliModuleFactory
{
    internal static ProductionOllamaRecordingCliModuleFactory Instance { get; } = new();
    private ProductionOllamaRecordingCliModuleFactory() { }
    public IOllamaRecordingCliModule Create(string absoluteRepositoryRoot) => new ProductionOllamaRecordingCliModule(absoluteRepositoryRoot);
}

internal sealed class ProductionOllamaRecordingCliModule : IOllamaRecordingCliModule
{
    private readonly SnowGlobeOllamaRecordingCompositionModule _module;
    private OllamaRecordingCompositionPlan? _plan;
    private int _executed;
    internal ProductionOllamaRecordingCliModule(string absoluteRepositoryRoot) => _module = new(absoluteRepositoryRoot);
    public OllamaRecordingCliPreparedPlan Prepare(PinnedRuntimeObservation runtime, string authorizationNonce)
    {
        if (_plan is not null) throw new InvalidOperationException("cli_plan_already_prepared");
        _plan = _module.Prepare(runtime, authorizationNonce); return new OllamaRecordingCliPreparedPlan(_plan.PlanDigestSha256, _plan.RelativeArtifactPath);
    }
    public async ValueTask<OllamaRecordingCliExecutionSummary> ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        if (_plan is null || Interlocked.Exchange(ref _executed, 1) != 0) throw new InvalidOperationException("cli_execution_not_available");
        OllamaRecordingCompositionResult result = await _module.ExecuteAndPublishOnceAsync(_plan, cancellationToken).ConfigureAwait(false); OllamaRecordingExecutionArtifact? artifact = result.Artifact;
        return new OllamaRecordingCliExecutionSummary(
            result.OutcomeCode, result.FailureCode, artifact?.RecordingResultPresent ?? false,
            artifact?.RecordingOutcomeCode, artifact?.RecordingFailureCode, artifact?.CompletedSlotCount,
            artifact?.TerminalSlotOrdinal, artifact?.TerminalSubmissionState, artifact?.TerminalStatusCode,
            artifact?.TerminalChargeState?.ToString(), result.ArtifactPublished, artifact?.CanonicalDigestSha256,
            artifact?.ReceiptPresent ?? false, artifact?.ReceiptDigestSha256,
            artifact?.TerminalReceiptRowPresent ?? false, artifact?.TerminalWrapperDigestPresent ?? false,
            artifact?.NestedRecordingEvidenceDigestSha256 is not null, artifact?.TerminalCheckpointCode, artifact?.TerminalPolicyCode);
    }
    public OllamaRecordingCliValidationSummary ValidateArtifact()
    {
        OllamaRecordingExecutionArtifact artifact = _module.ValidateArtifact(); return new OllamaRecordingCliValidationSummary(artifact.CanonicalDigestSha256, artifact.OutcomeCode);
    }
}
