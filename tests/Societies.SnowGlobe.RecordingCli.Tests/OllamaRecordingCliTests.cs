using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.RecordingCli.Tests;

public sealed class OllamaRecordingCliTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "societies-recording-cli-" + Guid.NewGuid().ToString("N"));
    private static readonly long StartTicks = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public async Task PreflightIsRawFreeAndCreatesNoFilesOrDirectories()
    {
        string nonce = "cli-preflight-secret-v1"; StringWriter output = new(); StringWriter error = new();
        int exit = await RecordingProgram.RunAsync(["preflight", "--repository-root", _root, "--pid", "777", "--start-utc-ticks", StartTicks.ToString(), "--nonce", nonce], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString()); Assert.Contains("PREFLIGHT_ACCEPTED", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("io_performed=false", output.ToString(), StringComparison.Ordinal); Assert.Contains("live_authorized=false", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(nonce, output.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(_root, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task RecordOnceConfirmationMismatchIsClosedBeforeExecution()
    {
        StringWriter output = new(); StringWriter error = new();
        int exit = await RecordingProgram.RunAsync([
            "record-once", "--repository-root", _root, "--pid", "777",
            "--start-utc-ticks", StartTicks.ToString(), "--nonce", "cli-record-once-v1",
            "--confirm-plan-sha256", new string('0', 64), "--acknowledge-live-local-loopback"
        ], output, error);
        Assert.Equal(4, exit); Assert.Empty(output.ToString());
        Assert.Equal("RECORDING_FAILED code=plan_confirmation_mismatch" + Environment.NewLine, error.ToString());
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task ConfirmationMismatchUsesPurePrepareOnceAndNeverExecutes()
    {
        CountingModule module = new(); CountingFactory factory = new(module);
        string nonce = "cli-confirmation-secret-v1"; StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(new string('0', 64), nonce), factory, output, error, CancellationToken.None);
        Assert.Equal(4, exit); Assert.Equal(1, factory.CreateCount); Assert.Equal(1, module.PrepareCount); Assert.Equal(0, module.ExecuteCount);
        Assert.Equal(_root, factory.LastRoot); Assert.Equal(nonce, module.LastNonce); Assert.Empty(output.ToString());
        Assert.Equal("RECORDING_FAILED code=plan_confirmation_mismatch" + Environment.NewLine, error.ToString());
        Assert.DoesNotContain(nonce, error.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Complete", "None", 12, "ResponseReceived", 200, "NotApplicable", true, 0)]
    [InlineData("Failed", "CapabilityExpired", 0, "DefinitelyNotSubmitted", null, "NotApplicable", false, 3)]
    [InlineData("Failed", "RuntimeBindingInvalid", 0, "DefinitelyNotSubmitted", null, "NotApplicable", true, 3)]
    [InlineData("Failed", "RuntimeChanged", 2, "DefinitelyNotSubmitted", null, "NotApplicable", true, 3)]
    [InlineData("Failed", "TransportPoisoned", 3, "DefinitelyNotSubmitted", null, "NotApplicable", true, 3)]
    [InlineData("Failed", "TransportFailure", 4, "SubmissionUnknown", null, "NotApplicable", true, 3)]
    [InlineData("Failed", "HttpResponseRejected", 5, "ResponseReceived", 503, "NotApplicable", true, 3)]
    [InlineData("Failed", "ResponseBodyRejected", 6, "ResponseReceived", 200, "NotApplicable", true, 3)]
    [InlineData("Failed", "WrapperRejected", 7, "ResponseReceived", 200, "NotApplicable", true, 3)]
    [InlineData("Failed", "EvidenceRejected", 12, "ResponseReceived", 200, "NotApplicable", true, 3)]
    [InlineData("Cancelled", "Cancelled", 0, "DefinitelyNotSubmitted", null, "NotApplicable", false, 3)]
    [InlineData("TimedOut", "TimedOut", 8, "ResponseReceived", 504, "NotApplicable", true, 3)]
    [InlineData("AuthorizationRejected", "AuthorizationRejected", null, null, null, null, false, 4)]
    [InlineData("CompositionFailed", "CompositionFailed", null, null, null, null, false, 5)]
    public async Task ExactConfirmationExecutesOnceAndMapsEveryClosedOutcome(
        string outcome, string failure, int? completed, string? submission, int? status, string? charge, bool receiptPresent, int expectedExit)
    {
        CountingModule module = new()
        {
            Summary = Summary(outcome, failure, completed, submission, status, charge, artifactPublished: true, receiptPresent)
        };
        CountingFactory factory = new(module); StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);
        Assert.Equal(expectedExit, exit); Assert.Equal(1, factory.CreateCount); Assert.Equal(1, module.PrepareCount); Assert.Equal(1, module.ExecuteCount);
        Assert.Empty(error.ToString()); string line = output.ToString(); Assert.Single(ReadLines(line));
        Assert.Contains($"outcome={outcome}", line, StringComparison.Ordinal); Assert.Contains($"failure={failure}", line, StringComparison.Ordinal);
        Assert.Contains($"completed={(completed?.ToString() ?? "none")}", line, StringComparison.Ordinal);
        Assert.Contains($"submission={submission ?? "none"}", line, StringComparison.Ordinal);
        Assert.Contains($"status={(status?.ToString() ?? "none")}", line, StringComparison.Ordinal);
        Assert.Contains($"charge={charge ?? "none"}", line, StringComparison.Ordinal);
        (string? checkpoint, string? policy) = TerminalEvidence(outcome, failure, submission, status);
        Assert.Contains($"checkpoint={checkpoint ?? "none"}", line, StringComparison.Ordinal);
        Assert.Contains($"policy={policy ?? "none"}", line, StringComparison.Ordinal);
        Assert.Contains("additional_attempt_authorized=false", line, StringComparison.Ordinal);
        Assert.Contains($"artifact_digest_sha256={ArtifactDigest}", line, StringComparison.Ordinal);
        Assert.Contains($"receipt_digest_sha256={(receiptPresent ? ReceiptDigest : "none")}", line, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, line, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("cli-record-once-v1", line, StringComparison.Ordinal);
        Assert.True(line.Length < 768);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(429)]
    public async Task RuntimeChangedResponseReceivedPublishedSummaryMapsTerminalOnce(int status)
    {
        CountingModule module = new()
        {
            Summary = Summary("Failed", "RuntimeChanged", 2, "ResponseReceived", status, "NotApplicable", artifactPublished: true, receiptPresent: true)
        };
        CountingFactory factory = new(module); StringWriter output = new(); StringWriter error = new();

        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);

        Assert.Equal(3, exit); Assert.Equal(1, factory.CreateCount); Assert.Equal(1, module.PrepareCount); Assert.Equal(1, module.ExecuteCount);
        Assert.Empty(error.ToString()); string line = Assert.Single(ReadLines(output.ToString()));
        Assert.Contains($"outcome=Failed failure=RuntimeChanged completed=2 submission=ResponseReceived status={status}", line, StringComparison.Ordinal);
        Assert.Contains("additional_attempt_authorized=false", line, StringComparison.Ordinal); Assert.True(line.Length < 768);
    }

    [Theory]
    [InlineData("HttpResponseRejected")]
    [InlineData("TransportFailure")]
    public async Task Http200ResponseTerminalPublishedSummaryMapsTerminalOnce(string failure)
    {
        CountingModule module = new()
        {
            Summary = Summary("Failed", failure, 0, "ResponseReceived", 200, "NotApplicable", artifactPublished: true, receiptPresent: true)
        };
        CountingFactory factory = new(module); StringWriter output = new(); StringWriter error = new();

        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);

        Assert.Equal(3, exit); Assert.Equal(1, factory.CreateCount); Assert.Equal(1, module.PrepareCount); Assert.Equal(1, module.ExecuteCount);
        Assert.Empty(error.ToString()); string line = Assert.Single(ReadLines(output.ToString()));
        Assert.Contains($"outcome=Failed failure={failure} completed=0 submission=ResponseReceived status=200", line, StringComparison.Ordinal);
        Assert.Contains("additional_attempt_authorized=false", line, StringComparison.Ordinal); Assert.True(line.Length < 768);
    }

    [Fact]
    public async Task PreCancelledTokenStopsBeforeExecution()
    {
        CountingModule module = new(); CountingFactory factory = new(module);
        using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, cancellation.Token);
        Assert.Equal(4, exit); Assert.Equal(0, module.ExecuteCount); Assert.False(module.EnteredExecution);
        Assert.Empty(output.ToString()); Assert.Equal("RECORDING_FAILED code=operation_cancelled" + Environment.NewLine, error.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExceptionAfterExecutionEntryIsIndeterminateAndNeverRetried(bool cancellationException)
    {
        string sentinel = "entered-execution-secret-C:/outside/nonce";
        Exception exception = cancellationException ? new OperationCanceledException(sentinel) : new InvalidOperationException(sentinel);
        CountingModule module = new() { ExecuteException = exception }; CountingFactory factory = new(module);
        StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);
        Assert.Equal(5, exit); Assert.True(module.EnteredExecution); Assert.Equal(1, module.ExecuteCount);
        Assert.Empty(output.ToString()); Assert.Equal("RECORDING_FAILED code=composition_indeterminate" + Environment.NewLine, error.ToString());
        Assert.DoesNotContain(sentinel, error.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExceptionsAndMissingArtifactNeverCauseASecondAttemptOrEchoRawDetails()
    {
        string sentinel = "raw-exception-C:/outside/cli-record-once-v1";
        foreach ((Exception? exception, OllamaRecordingCliExecutionSummary summary, int expectedExit, string expectedCode) in new (Exception?, OllamaRecordingCliExecutionSummary, int, string)[]
        {
            (new OllamaRecordingCompositionException("artifact_publication_indeterminate"), Summary(), 5, "artifact_publication_indeterminate"),
            (null, Summary("Failed", "TransportFailure", 1, "SubmissionUnknown", null, "NotApplicable", artifactPublished: false, receiptPresent: false), 5, "recording_result_invalid")
        })
        {
            CountingModule module = new() { Summary = summary, ExecuteException = exception }; CountingFactory factory = new(module);
            StringWriter output = new(); StringWriter error = new();
            int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);
            Assert.Equal(expectedExit, exit); Assert.Equal(1, module.ExecuteCount); Assert.Empty(output.ToString());
            Assert.Equal($"RECORDING_FAILED code={expectedCode}" + Environment.NewLine, error.ToString());
            Assert.DoesNotContain(sentinel, error.ToString(), StringComparison.Ordinal); Assert.True(error.ToString().Length < 160);
        }
    }

    [Fact]
    public async Task AuthorizationRejectionDuringPrepareIsClosedBeforeExecution()
    {
        CountingModule module = new() { PrepareException = new OllamaRecordingCompositionException("authorization_rejected") };
        CountingFactory factory = new(module); StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);
        Assert.Equal(4, exit); Assert.Equal(1, module.PrepareCount); Assert.Equal(0, module.ExecuteCount); Assert.Empty(output.ToString());
        Assert.Equal("RECORDING_FAILED code=authorization_rejected" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task IntegrityShapedButSemanticallyImpossibleSummariesAreRejected()
    {
        OllamaRecordingCliExecutionSummary[] invalid =
        [
            Summary("Complete", "None", 11, "ResponseReceived", 200, "NotApplicable", true, true),
            Summary("Complete", "None", 12, "SubmissionUnknown", null, "NotApplicable", true, true),
            Summary("Failed", "EvidenceRejected", 11, "ResponseReceived", 200, "NotApplicable", true, true),
            Summary("Cancelled", "Cancelled", 12, "ResponseReceived", 200, "NotApplicable", true, true),
            Summary("TimedOut", "TimedOut", 2, "DefinitelyNotSubmitted", 200, "NotApplicable", true, true),
            Summary("AuthorizationRejected", "AuthorizationRejected", 0, null, null, null, true, false),
            Summary("CompositionFailed", "CompositionFailed", null, null, null, null, true, true),
            Summary("Failed", "TransportFailure", 1, "SubmissionUnknown", null, "Charged", true, true),
            Summary("Failed", "RuntimeChanged", 1, "SubmissionUnknown", null, "NotApplicable", true, true),
            Summary("Failed", "RuntimeChanged", 1, "DefinitelyNotSubmitted", 200, "NotApplicable", true, true),
            Summary("Failed", "RuntimeChanged", 1, "ResponseReceived", null, "NotApplicable", true, true),
            Summary("Failed", "TransportPoisoned", 1, "ResponseReceived", 200, "NotApplicable", true, true),
            Summary() with { TerminalCheckpointCode = "ResponseHeaders" },
            Summary() with { TerminalPolicyCode = "ContentType" },
            Summary("Failed", "HttpResponseRejected", 1, "ResponseReceived", 200, "NotApplicable", true, true) with { TerminalPolicyCode = "WrapperShape" },
            Summary("Failed", "TransportFailure", 1, "ResponseReceived", 200, "NotApplicable", true, true) with { TerminalCheckpointCode = "ResponseHeaders" },
            Summary() with { ArtifactDigestSha256 = new string('A', 64) }
        ];
        foreach (OllamaRecordingCliExecutionSummary summary in invalid)
        {
            CountingModule module = new() { Summary = summary }; CountingFactory factory = new(module);
            StringWriter output = new(); StringWriter error = new();
            int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest), factory, output, error, CancellationToken.None);
            Assert.Equal(5, exit); Assert.Equal(1, module.ExecuteCount); Assert.Empty(output.ToString());
            Assert.Equal("RECORDING_FAILED code=recording_result_invalid" + Environment.NewLine, error.ToString());
        }
    }

    [Fact]
    public async Task EveryRecordOnceArgumentFailureOccursBeforeModuleConstruction()
    {
        List<string[]> invalid = [];
        string[] uppercaseDigest = RecordArgs(PlanDigest.ToUpperInvariant()); invalid.Add(uppercaseDigest);
        invalid.Add(RecordArgs(new string('a', 63)));
        invalid.Add([.. RecordArgs(PlanDigest), "--acknowledge-live-local-loopback"]);
        invalid.Add([.. RecordArgs(PlanDigest), "unexpected-ack-value"]);
        invalid.Add(RecordArgs(PlanDigest)[..^1]);
        string[] duplicateOption = RecordArgs(PlanDigest); duplicateOption[Array.IndexOf(duplicateOption, "--pid")] = "--repository-root"; invalid.Add(duplicateOption);
        string[] unknownOption = RecordArgs(PlanDigest); unknownOption[Array.IndexOf(unknownOption, "--pid")] = "--model"; invalid.Add(unknownOption);
        string[] zeroPid = RecordArgs(PlanDigest); zeroPid[Array.IndexOf(zeroPid, "--pid") + 1] = "0"; invalid.Add(zeroPid);
        string[] zeroTicks = RecordArgs(PlanDigest); zeroTicks[Array.IndexOf(zeroTicks, "--start-utc-ticks") + 1] = "0"; invalid.Add(zeroTicks);
        string[] badNonce = RecordArgs(PlanDigest); badNonce[Array.IndexOf(badNonce, "--nonce") + 1] = "NOT-CANONICAL"; invalid.Add(badNonce);
        string[] relativeRoot = RecordArgs(PlanDigest); relativeRoot[Array.IndexOf(relativeRoot, "--repository-root") + 1] = "relative-root"; invalid.Add(relativeRoot);

        foreach (string[] args in invalid)
        {
            CountingFactory factory = new(new()); StringWriter output = new(); StringWriter error = new();
            int exit = await OllamaRecordingCliApplication.RunAsync(args, factory, output, error, CancellationToken.None);
            Assert.Equal(2, exit); Assert.Equal(0, factory.CreateCount); Assert.Empty(output.ToString());
            Assert.Equal("RECORDING_FAILED code=arguments_invalid" + Environment.NewLine, error.ToString());
        }
    }

    [Theory]
    [InlineData("--model")]
    [InlineData("--endpoint")]
    [InlineData("--runtime-path")]
    [InlineData("--runtime-hash")]
    [InlineData("--header")]
    [InlineData("--timeout")]
    [InlineData("--retry")]
    [InlineData("--output-path")]
    [InlineData("--alternate")]
    [InlineData("--download")]
    [InlineData("--start")]
    [InlineData("--stop")]
    public async Task AuthorityExpandingOptionsAreRejectedBeforeConstruction(string option)
    {
        string[] args = RecordArgs(PlanDigest); args[Array.IndexOf(args, "--pid")] = option;
        CountingFactory factory = new(new()); StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync(args, factory, output, error, CancellationToken.None);
        Assert.Equal(2, exit); Assert.Equal(0, factory.CreateCount); Assert.Empty(output.ToString());
        Assert.Equal("RECORDING_FAILED code=arguments_invalid" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task ConcurrentInvocationsHaveIndependentOneShotState()
    {
        Task<(int Exit, CountingFactory Factory, CountingModule Module, string Output, string Error)>[] tasks = Enumerable.Range(0, 24).Select(async index =>
        {
            CountingModule module = new(); CountingFactory factory = new(module); StringWriter output = new(); StringWriter error = new();
            int exit = await OllamaRecordingCliApplication.RunAsync(RecordArgs(PlanDigest, $"cli-record-once-{index}"), factory, output, error, CancellationToken.None);
            return (exit, factory, module, output.ToString(), error.ToString());
        }).ToArray();
        foreach (var result in await Task.WhenAll(tasks))
        {
            Assert.Equal(0, result.Exit); Assert.Equal(1, result.Factory.CreateCount); Assert.Equal(1, result.Module.PrepareCount); Assert.Equal(1, result.Module.ExecuteCount);
            Assert.Empty(result.Error); Assert.Single(ReadLines(result.Output));
        }
    }

    [Fact]
    public void CliAssemblyHasOneCompositionExecuteCallsiteAndNoLiveHelpersOrRetryWaits()
    {
        Assembly assembly = typeof(OllamaRecordingCliApplication).Assembly;
        string[] references = assembly.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain("System.Net.Http", references); Assert.DoesNotContain("System.Diagnostics.Process", references); Assert.DoesNotContain("System.Net.Sockets", references);
        MethodBase[] called = assembly.GetTypes()
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)))
            .SelectMany(CalledMethods).ToArray();
        Assert.Single(called.Where(static method => method.DeclaringType == typeof(SnowGlobeOllamaRecordingCompositionModule)
            && method.Name == nameof(SnowGlobeOllamaRecordingCompositionModule.ExecuteAndPublishOnceAsync)));
        Assert.DoesNotContain(called, static method =>
        {
            string type = method.DeclaringType?.FullName ?? string.Empty;
            return type == "System.Diagnostics.Process" || type.StartsWith("System.Net.Http.", StringComparison.Ordinal)
                || type.StartsWith("System.Net.Sockets.", StringComparison.Ordinal)
                || type == "System.Threading.Thread" && method.Name == "Sleep"
                || type == "System.Threading.Tasks.Task" && method.Name == "Delay";
        });
        Assert.DoesNotContain(assembly.GetTypes().SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)),
            static method => method.Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Download", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("record")]
    [InlineData("live")]
    [InlineData("execute")]
    [InlineData("--record")]
    public async Task EveryLiveTokenFailsBeforeConstruction(string token)
    {
        CountingFactory factory = new(new()); StringWriter output = new(); StringWriter error = new();
        int exit = await OllamaRecordingCliApplication.RunAsync([token, "--repository-root", _root], factory, output, error, CancellationToken.None);
        Assert.Equal(2, exit); Assert.Equal(0, factory.CreateCount); Assert.Empty(output.ToString());
        Assert.Equal("RECORDING_FAILED code=live_mode_not_available" + Environment.NewLine, error.ToString()); Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task UnknownDuplicateAndUnapprovedOptionsFailClosedWithoutEcho()
    {
        string secret = "do-not-echo-v1";
        foreach (string[] args in new[]
        {
            new[] { "unknown", "--nonce", secret },
            new[] { "preflight", "--repository-root", _root, "--repository-root", _root, "--pid", "1", "--start-utc-ticks", "1", "--nonce", secret },
            new[] { "preflight", "--repository-root", _root, "--model", secret },
            new[] { "validate", "--repository-root", _root, "--output-path", secret }
        })
        {
            StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(args, output, error);
            Assert.Equal(2, exit); Assert.Empty(output.ToString()); Assert.Contains("arguments_invalid", error.ToString(), StringComparison.Ordinal); Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        }
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task ValidateReadsOnlyFixedBoundedArtifactAndReportsDigest()
    {
        if (!OperatingSystem.IsWindows()) return;
        OllamaRecordingExecutionArtifact artifact = await CreateArtifact(); CreateRepositoryMarkers();
        string path = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, artifact.CanonicalUtf8.ToArray());
        StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(["validate", "--repository-root", _root], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString()); Assert.Contains(artifact.CanonicalDigestSha256, output.ToString(), StringComparison.Ordinal); Assert.Contains("structurally_complete=true", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRejectsMalformedArtifactWithoutLiveFallback()
    {
        if (!OperatingSystem.IsWindows()) return;
        CreateRepositoryMarkers(); string path = Path.Combine(_root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "{}");
        StringWriter output = new(); StringWriter error = new(); int exit = await RecordingProgram.RunAsync(["validate", "--repository-root", _root], output, error);
        Assert.Equal(1, exit); Assert.Empty(output.ToString()); Assert.StartsWith("RECORDING_FAILED code=artifact_", error.ToString(), StringComparison.Ordinal);
    }

    private async Task<OllamaRecordingExecutionArtifact> CreateArtifact()
    {
        InMemoryOllamaRecordingArtifactStore store = new(); Factory factory = new(); SnowGlobePinnedOllamaRecordingModule inner = new(new Clock(), factory);
        SnowGlobeOllamaRecordingCompositionModule module = new(_root, inner, store);
        return (await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "cli-artifact-v1"))).Artifact!;
    }

    private void CreateRepositoryMarkers()
    {
        Directory.CreateDirectory(_root); File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: offline-test"); File.WriteAllText(Path.Combine(_root, "CURRENT_BUILD.md"), "# offline test");
        string lab = Path.Combine(_root, "labs", "Societies.SnowGlobe"); Directory.CreateDirectory(lab); File.WriteAllText(Path.Combine(lab, "Societies.SnowGlobe.csproj"), "<Project />");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private const string PlanDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ArtifactDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ReceiptDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private string[] RecordArgs(string digest, string nonce = "cli-record-once-v1") =>
    [
        "record-once", "--repository-root", _root, "--pid", "777", "--start-utc-ticks", StartTicks.ToString(),
        "--nonce", nonce, "--confirm-plan-sha256", digest, "--acknowledge-live-local-loopback"
    ];

    private static OllamaRecordingCliExecutionSummary Summary(
        string outcome = "Complete", string failure = "None", int? completed = 12, string? submission = "ResponseReceived",
        int? status = 200, string? charge = "NotApplicable", bool artifactPublished = true, bool receiptPresent = true)
    {
        bool recordingPresent = outcome is not ("AuthorizationRejected" or "CompositionFailed");
        string? recordingOutcome = recordingPresent ? outcome : null;
        string? recordingFailure = recordingPresent ? outcome is "Complete" or "Cancelled" or "TimedOut" ? "None" : failure : null;
        int? terminalSlot = !recordingPresent || outcome == "Complete" || failure == "CapabilityExpired"
            ? null
            : failure == "EvidenceRejected" ? 12 : completed + 1;
        bool terminalRow = receiptPresent && outcome != "Complete" && failure is not ("EvidenceRejected" or "RuntimeBindingInvalid")
            && !(outcome is "Cancelled" or "TimedOut" && submission == "DefinitelyNotSubmitted");
        bool wrapper = terminalRow && failure == "WrapperRejected";
        bool nestedEvidence = outcome == "Complete";
        (string? checkpoint, string? policy) = TerminalEvidence(outcome, failure, submission, status);
        return new(outcome, failure, recordingPresent, recordingOutcome, recordingFailure, completed, terminalSlot,
            submission, status, charge, artifactPublished, artifactPublished ? ArtifactDigest : null,
            receiptPresent, receiptPresent ? ReceiptDigest : null, terminalRow, wrapper, nestedEvidence, checkpoint, policy);
    }

    private static (string? Checkpoint, string? Policy) TerminalEvidence(string outcome, string failure, string? submission, int? status) =>
        (outcome, failure, submission, status) switch
        {
            ("Complete", _, _, _) => ("None", "None"),
            ("AuthorizationRejected", _, _, _) => ("Authorization", "Authorization"),
            ("CompositionFailed", _, _, _) => ("Composition", "UnexpectedException"),
            (_, "CapabilityExpired", _, _) => ("BeforeDispatch", "Capability"),
            (_, "RuntimeBindingInvalid", _, _) => ("BeforeDispatch", "RuntimeBinding"),
            (_, "RuntimeChanged", "ResponseReceived", _) => ("ResponseHeaders", "RuntimeOwnership"),
            (_, "RuntimeChanged", _, _) => ("BeforeDispatch", "RuntimeOwnership"),
            (_, "TransportPoisoned", _, _) => ("BeforeDispatch", "TransportState"),
            (_, "TransportFailure", "ResponseReceived", 200) => ("ResponseBody", "BodyRead"),
            (_, "TransportFailure", _, _) => ("RequestDispatch", "TransportIo"),
            (_, "HttpResponseRejected", _, 200) => ("ResponseHeaders", "ContentType"),
            (_, "HttpResponseRejected", _, _) => ("ResponseHeaders", "HttpStatus"),
            (_, "ResponseBodyRejected", _, _) => ("ResponseBody", "BodyRead"),
            (_, "WrapperRejected", _, _) => ("WrapperDecode", "WrapperShape"),
            (_, "EvidenceRejected", _, _) => ("EvidenceConstruction", "EvidenceShape"),
            ("Cancelled", _, "ResponseReceived", 200) => ("ResponseBody", "Cancellation"),
            ("Cancelled", _, "ResponseReceived", _) => ("ResponseHeaders", "Cancellation"),
            ("Cancelled", _, "SubmissionUnknown", _) => ("RequestDispatch", "Cancellation"),
            ("Cancelled", _, _, _) => ("BeforeDispatch", "Cancellation"),
            ("TimedOut", _, "ResponseReceived", 200) => ("ResponseBody", "Timeout"),
            ("TimedOut", _, "ResponseReceived", _) => ("ResponseHeaders", "Timeout"),
            ("TimedOut", _, "SubmissionUnknown", _) => ("RequestDispatch", "Timeout"),
            ("TimedOut", _, _, _) => ("BeforeDispatch", "Timeout"),
            _ => (null, null)
        };

    private static string[] ReadLines(string value) => value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<MethodBase> CalledMethods(MethodBase caller)
    {
        byte[]? il = caller.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        int offset = 0;
        while (offset < il.Length)
        {
            short value = il[offset++] == 0xfe ? unchecked((short)(0xfe00 | il[offset++])) : il[offset - 1];
            if (!IlOpCodes.TryGetValue(value, out OpCode opCode)) yield break;
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                int token = BitConverter.ToInt32(il, offset);
                MethodBase? called = null;
                try { called = caller.Module.ResolveMethod(token, caller.DeclaringType?.GetGenericArguments(), caller.IsGenericMethod ? caller.GetGenericArguments() : null); }
                catch (ArgumentException) { }
                if (called is not null) yield return called;
            }
            offset += OperandSize(opCode.OperandType, il, offset);
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int offset) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod
            or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, offset) * 4,
        _ => throw new InvalidOperationException("unsupported_il_operand")
    };

    private static readonly IReadOnlyDictionary<short, OpCode> IlOpCodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(static field => field.FieldType == typeof(OpCode)).Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static code => code.Value);

    private sealed class CountingFactory(CountingModule module) : IOllamaRecordingCliModuleFactory
    {
        public int CreateCount;
        public string? LastRoot { get; private set; }
        public IOllamaRecordingCliModule Create(string absoluteRepositoryRoot)
        {
            Interlocked.Increment(ref CreateCount); LastRoot = absoluteRepositoryRoot; return module;
        }
    }

    private sealed class CountingModule : IOllamaRecordingCliModule
    {
        public int PrepareCount;
        public int ExecuteCount;
        public bool EnteredExecution { get; private set; }
        public string? LastNonce { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public Exception? PrepareException { get; init; }
        public Exception? ExecuteException { get; init; }
        public OllamaRecordingCliExecutionSummary Summary { get; init; } = Summary();
        public OllamaRecordingCliPreparedPlan Prepare(PinnedRuntimeObservation runtime, string authorizationNonce)
        {
            Interlocked.Increment(ref PrepareCount); LastNonce = authorizationNonce;
            if (PrepareException is not null) throw PrepareException;
            return new(PlanDigest, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath);
        }
        public ValueTask<OllamaRecordingCliExecutionSummary> ExecuteOnceAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ExecuteCount); EnteredExecution = true; LastCancellationToken = cancellationToken;
            return ExecuteException is null ? ValueTask.FromResult(Summary) : ValueTask.FromException<OllamaRecordingCliExecutionSummary>(ExecuteException);
        }
        public OllamaRecordingCliValidationSummary ValidateArtifact() => new(ArtifactDigest, "Complete");
    }

    private sealed class Clock : ICognitionQualityRecordingSessionClock { public long NowMilliseconds => 1; }
    private sealed class Factory : IOllamaLoopbackRecordingTransportFactory
    {
        public IOfflineOllamaRecordingTransportPort Create(OllamaLoopbackRuntimeBinding binding) => new InMemoryOfflineOllamaRecordingTransportAdapter(Enumerable.Range(1, 12).Select(Wrapper).ToArray());
        private static byte[] Wrapper(int index)
        {
            string action = index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" }; int quantity = index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
            string proposal = $"{{\"agent_id\":\"agent-00\",\"action\":\"{action}\",\"quantity\":{quantity}}}";
            return Encoding.UTF8.GetBytes($"{{\"model\":\"qwen3.5:4b\",\"created_at\":\"2026-08-18T12:00:00Z\",\"response\":{JsonSerializer.Serialize(proposal)},\"done\":true,\"done_reason\":\"stop\",\"context\":[1,2],\"total_duration\":1000000,\"load_duration\":0,\"prompt_eval_count\":10,\"prompt_eval_duration\":500000,\"eval_count\":20,\"eval_duration\":500000}}");
        }
    }
}
