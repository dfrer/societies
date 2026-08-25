using Societies.SnowGlobe;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.SnowGlobe.RecordingCli.Tests;

public sealed class ProviderReadinessOneShotCliTests : IDisposable
{
    private const long ObservedAt = 1_787_000_000_000;
    private static readonly PinnedRuntimeObservation Runtime = new(4242, 638916336000000000);
    private readonly string _fileRoot = Path.Combine(
        Path.GetTempPath(), "societies-provider-readiness-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ClaimContractIsDeterministicCanonicalAndBindsEveryFixedArtifact()
    {
        Assert.Equal("7e715b26045ee68d5ad0d44acf4532f4ad3213882b49c859fb952d816731e786",
            ProviderReadinessOneShotClaimCodec.ContractDigestSha256);
        Assert.Equal(40, ProviderReadinessOneShotClaimCodec.SourceCommit.Length);
        Assert.All(ProviderReadinessOneShotClaimCodec.SourceCommit,
            character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
        Assert.Equal("artifacts/snowglobe/provider-readiness/one-shot-consumed-v1.json",
            ProviderReadinessOneShotClaimCodec.ClaimPath);
        Assert.Equal("artifacts/snowglobe/provider-readiness/evidence-v1/openrouter-observation-v1.json",
            ProviderReadinessOneShotClaimCodec.OpenRouterObservationPath);
        Assert.Equal("artifacts/snowglobe/provider-readiness/evidence-v1/ollama-observation-v1.json",
            ProviderReadinessOneShotClaimCodec.OllamaObservationPath);
        Assert.Equal("artifacts/snowglobe/provider-readiness/evidence-v1/routing-readiness-assessment-v2.json",
            ProviderReadinessOneShotClaimCodec.AssessmentPath);

        byte[] first = ProviderReadinessOneShotClaimCodec.CanonicalUtf8.ToArray();
        byte[] second = ProviderReadinessOneShotClaimCodec.CanonicalUtf8.ToArray();
        Assert.Equal(first, second);
        Assert.Contains($"\"source_commit\":\"{ProviderReadinessOneShotClaimCodec.SourceCommit}\"",
            System.Text.Encoding.UTF8.GetString(first), StringComparison.Ordinal);
        Assert.Equal(ProviderReadinessOneShotClaimCodec.Validate(first),
            ProviderReadinessOneShotClaimCodec.Validate(second));
        first[0] ^= 0x01;
        Assert.Equal("artifact_claim_invalid", Assert.Throws<ProviderReadinessOneShotException>(() =>
            ProviderReadinessOneShotClaimCodec.Validate(first)).Code);
    }

    [Fact]
    public void HistoricalClaimValidationRetainsCanonicalPriorSourceCommit()
    {
        string priorSourceCommit = new('1', 40);
        byte[] historical = CreateClaimForSourceCommit(priorSourceCommit);

        string retainedSourceCommit = ProviderReadinessOneShotClaimCodec.Validate(historical);

        Assert.Equal(priorSourceCommit, retainedSourceCommit);
    }

    [Theory]
    [InlineData("111111111111111111111111111111111111111")]
    [InlineData("11111111111111111111111111111111111111111")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggg")]
    public void HistoricalClaimValidationRejectsRedigestedMalformedSourceCommit(string malformed)
    {
        byte[] canonical = CreateClaimForSourceCommit(malformed);

        Assert.Equal("artifact_claim_invalid", Assert.Throws<ProviderReadinessOneShotException>(() =>
            ProviderReadinessOneShotClaimCodec.Validate(canonical)).Code);
    }

    [Fact]
    public void CurrentBuildClaimGateRejectsOtherwiseCanonicalHistoricalClaim()
    {
        CreateFileRepository();
        string priorSourceCommit = new('1', 40);
        Assert.NotEqual(ProviderReadinessOneShotClaimCodec.SourceCommit, priorSourceCommit);
        FileProviderReadinessOneShotArtifactStore store = new(_fileRoot);

        ProviderReadinessOneShotException failure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.ClaimOnce(CreateClaimForSourceCommit(priorSourceCommit)));

        Assert.Equal("source_revision_mismatch", failure.Code);
        Assert.False(Directory.Exists(Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "provider-readiness")));
    }

    [Fact]
    public void CurrentBuildClaimGateRejectsInPlaceHistoricalRelabelBeforeEvidenceRead()
    {
        CreateFileRepository();
        FileProviderReadinessOneShotArtifactStore store = new(_fileRoot);
        store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8);
        string claimPath = Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "provider-readiness", "one-shot-consumed-v1.json");
        File.WriteAllBytes(claimPath, CreateClaimForSourceCommit(new string('1', 40)));

        ProviderReadinessOneShotException failure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.ReadAcceptedComparisonArtifact());

        Assert.Equal("source_revision_mismatch", failure.Code);
    }

    [Fact]
    public async Task RealCommandInvokesBothAdaptersSequentiallyAndPublishesValidatedRawFreeEvidence()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        FakeAdapterFactory adapters = new(order,
            OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

        ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);

        Assert.Equal("published", result.Status);
        Assert.Equal(["claim", "comparison", "openrouter_factory", "openrouter", "ollama_factory", "ollama", "publish"], order);
        Assert.Equal(3, result.OpenRouterRequestCount);
        Assert.Equal(1, result.OllamaRequestCount);
        Assert.Equal("insufficient_current_attempt_evidence", result.AssessmentStatus);
        Assert.Equal("unknown", result.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", result.RoutingInputIssuanceStatus);
        Assert.False(result.RoutingPolicyInputPresent);
        Assert.False(result.AdditionalAttemptAuthorized);
        Assert.NotNull(store.Published);

        ProviderReadinessObservation openRouter = ProviderReadinessObservationModule.Validate(
            store.Published!.OpenRouterObservationCanonicalUtf8, ObservedAt);
        ProviderReadinessObservation ollama = ProviderReadinessObservationModule.Validate(
            store.Published.OllamaObservationCanonicalUtf8, ObservedAt);
        ProviderRoutingCurrentReadinessAssessment assessment = ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
            store.Published.AssessmentCanonicalUtf8, ObservedAt);
        Assert.Equal("openrouter", openRouter.Provider);
        Assert.Equal("ollama", ollama.Provider);
        Assert.Equal("unknown", assessment.PrimaryAttemptCurrentState);
        Assert.Equal("not_issued", assessment.RoutingInputIssuanceStatus);
        Assert.Null(assessment.RoutingPolicyInput);

        string retained = string.Concat(
            openRouter.CanonicalJson, ollama.CanonicalJson, assessment.CanonicalJson);
        Assert.DoesNotContain("secret", retained, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("response_body", retained, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Runtime.ProcessId.ToString(), retained, StringComparison.Ordinal);
        Assert.DoesNotContain(Runtime.ProcessStartUtcTicks.ToString(), retained, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealCommandRunsConcreteOneShotAdaptersThroughFakeProviderAndRuntimeSeams()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        ConcreteAdapterFactory adapters = new(order, Runtime);
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

        ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);

        Assert.Equal("published", result.Status);
        Assert.Equal(3, result.OpenRouterRequestCount);
        Assert.Equal(1, result.OllamaRequestCount);
        Assert.Equal(1, adapters.OpenRouterVerifierCalls);
        Assert.Equal(1, adapters.OllamaHandlerCalls);
        Assert.Equal(HttpMethod.Get, adapters.OllamaMethods.Single());
        Assert.Equal("http://127.0.0.1:11435/api/tags", adapters.OllamaUris.Single());
        Assert.Null(adapters.OllamaAuthorizationSchemes.Single());
        Assert.False(adapters.OllamaHadContent.Single());
        Assert.Equal(new[]
        {
            OllamaLoopbackRuntimeCheckPoint.BeforeDispatch,
            OllamaLoopbackRuntimeCheckPoint.AfterResponseHeaders,
            OllamaLoopbackRuntimeCheckPoint.AfterExchange
        }, adapters.RuntimeCheckPoints);
        Assert.True(order.IndexOf("openrouter_metadata") < order.IndexOf("ollama_http"));
        Assert.Equal(1, store.PublishCount);
    }

    [Fact]
    public async Task PreCancelledRealCompositionPublishesTerminalEvidenceWithoutProviderDispatch()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        ConcreteAdapterFactory adapters = new(order, Runtime, honorPreCancellation: true);
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(
            Runtime, cancellation.Token);

        Assert.Equal("published", result.Status);
        Assert.Equal(0, result.OpenRouterRequestCount);
        Assert.Equal(0, result.OllamaRequestCount);
        Assert.Equal(1, adapters.OpenRouterVerifierCalls);
        Assert.Equal(0, adapters.OpenRouterProviderDispatchCount);
        Assert.Equal(0, adapters.OllamaHandlerCalls);
        Assert.Equal([OllamaLoopbackRuntimeCheckPoint.BeforeDispatch], adapters.RuntimeCheckPoints);
        Assert.Equal(1, store.ClaimCount);
        Assert.Equal(1, store.PublishCount);
        ProviderReadinessObservation openRouter = ProviderReadinessObservationModule.Validate(
            store.Published!.OpenRouterObservationCanonicalUtf8, ObservedAt);
        ProviderReadinessObservation ollama = ProviderReadinessObservationModule.Validate(
            store.Published.OllamaObservationCanonicalUtf8, ObservedAt);
        Assert.Equal("operation_cancelled", openRouter.DiagnosticCode);
        Assert.Equal("operation_cancelled", ollama.DiagnosticCode);
    }

    [Fact]
    public async Task ExistingClaimBlocksSecondInvocationBeforeComparisonOrAdapters()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));
        _ = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal("invocation_already_consumed", failure.Code);
        Assert.Equal(1, adapters.OpenRouterCreated);
        Assert.Equal(1, adapters.OllamaCreated);
        Assert.Equal(1, store.PublishCount);
    }

    [Fact]
    public async Task MalformedComparisonConsumesClaimButNeverConstructsAProvider()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, "{}"u8.ToArray());
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal("comparison_evidence_rejected", failure.Code);
        Assert.Equal(["claim", "comparison"], order);
        Assert.Equal(0, adapters.OpenRouterCreated);
        Assert.Equal(0, adapters.OllamaCreated);
        Assert.Equal(0, store.PublishCount);
        Assert.Equal(1, store.ClaimCount);
    }

    [Fact]
    public async Task MissingCredentialAndUnavailableOllamaRemainCanonicalEvidenceNotRetries()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        FakeAdapterFactory adapters = new(order,
            OpenRouterResult.CredentialUnavailable(), OllamaResult.ProviderUnavailable());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

        ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);

        Assert.Equal("published", result.Status);
        Assert.Equal(0, result.OpenRouterRequestCount);
        Assert.Equal(1, result.OllamaRequestCount);
        Assert.Equal(1, adapters.OpenRouterCalls);
        Assert.Equal(1, adapters.OllamaCalls);
        Assert.Equal(1, store.PublishCount);
    }

    [Fact]
    public async Task StaleFirstObservationFailsClosedWithoutPublishingOrRetrying()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        SequenceClock clock = new(ObservedAt, ObservedAt + 1, ObservedAt + 60_001);
        ProviderReadinessOneShotCommand command = new(store, adapters, clock);

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal("observation_stale", failure.Code);
        Assert.Equal(1, adapters.OpenRouterCalls);
        Assert.Equal(1, adapters.OllamaCalls);
        Assert.Equal(0, store.PublishCount);
        Assert.Equal(1, store.ClaimCount);
    }

    [Theory]
    [InlineData("artifact_publication_partial")]
    [InlineData("artifact_publication_ambiguous")]
    public async Task PartialOrAmbiguousPublicationIsTerminalAndCannotBeRetried(string code)
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison()) { PublishFailureCode = code };
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));
        ProviderReadinessOneShotException second = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal(code, failure.Code);
        Assert.Equal("invocation_already_consumed", second.Code);
        Assert.Equal(1, store.PublishCount);
        Assert.Equal(1, adapters.OpenRouterCalls);
        Assert.Equal(1, adapters.OllamaCalls);
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task CliRejectsEveryNonExactInterfaceBeforeFactoryConstruction(string[] args)
    {
        CountingCommandFactory factory = new(null);
        StringWriter output = new();
        StringWriter error = new();

        int exit = await ProviderReadinessOneShotCliApplication.RunAsync(
            args, factory, output, error, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Equal(0, factory.CreateCount);
        Assert.Empty(output.ToString());
        Assert.Equal("PROVIDER_READINESS_FAILED code=arguments_invalid" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task CliSummaryIsClosedFixedPathAndSecretFree()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));
        CountingCommandFactory factory = new(command);
        StringWriter output = new();
        StringWriter error = new();

        int exit = await ProviderReadinessOneShotCliApplication.RunAsync(
            ValidArgs(), factory, output, error, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        string line = Assert.Single(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("PROVIDER_READINESS_PUBLISHED status=published openrouter_requests=3 ollama_requests=1", line, StringComparison.Ordinal);
        Assert.Contains("assessment_status=insufficient_current_attempt_evidence", line, StringComparison.Ordinal);
        Assert.Contains("primary_attempt=unknown routing=not_issued routing_input_present=false", line, StringComparison.Ordinal);
        Assert.Contains("openrouter_path=artifacts/snowglobe/provider-readiness/evidence-v1/openrouter-observation-v1.json", line, StringComparison.Ordinal);
        Assert.Contains("ollama_path=artifacts/snowglobe/provider-readiness/evidence-v1/ollama-observation-v1.json", line, StringComparison.Ordinal);
        Assert.Contains("assessment_path=artifacts/snowglobe/provider-readiness/evidence-v1/routing-readiness-assessment-v2.json", line, StringComparison.Ordinal);
        Assert.Contains("additional_attempt_authorized=false", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Runtime.ProcessId.ToString(), line, StringComparison.Ordinal);
        Assert.DoesNotContain(Runtime.ProcessStartUtcTicks.ToString(), line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.OrdinalIgnoreCase);
        Assert.True(line.Length < 1024);
    }

    [Fact]
    public async Task ExecutableEntrypointRoutesVersionedCommandBeforeLegacyParserWithoutProductionConstruction()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exit = await RecordingProgram.RunAsync(
            [
                "provider-readiness-record-once-v1",
                "--ollama-pid", "0",
                "--ollama-start-utc-ticks", Runtime.ProcessStartUtcTicks.ToString()
            ],
            output,
            error);

        Assert.Equal(2, exit);
        Assert.Empty(output.ToString());
        Assert.Equal("PROVIDER_READINESS_FAILED code=arguments_invalid" + Environment.NewLine,
            error.ToString());
    }

    [Fact]
    public async Task FileStoreUsesDurableClaimAndAtomicFixedDirectoryPublication()
    {
        CreateFileRepository();
        ProviderReadinessOneShotArtifacts artifacts = await CreateArtifactsAsync();
        List<string> checkpoints = [];
        FileProviderReadinessOneShotArtifactStore store = new(_fileRoot, checkpoints.Add);

        store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8);
        byte[] comparison = store.ReadAcceptedComparisonArtifact();
        try { Assert.Equal(AcceptedComparison(), comparison); }
        finally { Array.Clear(comparison); }
        try { store.PublishAtomically(artifacts); }
        catch (ProviderReadinessOneShotException exception)
        {
            Assert.Fail($"{exception.Code}; checkpoints={string.Join(',', checkpoints)}");
        }

        string readiness = Path.Combine(_fileRoot, "artifacts", "snowglobe", "provider-readiness");
        string published = Path.Combine(readiness, "evidence-v1");
        Assert.True(File.Exists(Path.Combine(readiness, "one-shot-consumed-v1.json")));
        Assert.False(Directory.Exists(Path.Combine(readiness, ".evidence-v1.pending")));
        Assert.Equal(artifacts.OpenRouterObservationCanonicalUtf8.ToArray(),
            File.ReadAllBytes(Path.Combine(published, "openrouter-observation-v1.json")));
        Assert.Equal(artifacts.OllamaObservationCanonicalUtf8.ToArray(),
            File.ReadAllBytes(Path.Combine(published, "ollama-observation-v1.json")));
        Assert.Equal(artifacts.AssessmentCanonicalUtf8.ToArray(),
            File.ReadAllBytes(Path.Combine(published, "routing-readiness-assessment-v2.json")));

        FileProviderReadinessOneShotArtifactStore second = new(_fileRoot);
        ProviderReadinessOneShotException consumed = Assert.Throws<ProviderReadinessOneShotException>(() =>
            second.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8));
        Assert.Equal("invocation_already_consumed", consumed.Code);
    }

    [Fact]
    public void FileStoreRefusesExistingPendingOrPublishedTargetsBeforeProviderEvidenceRead()
    {
        foreach (string target in new[] { ".evidence-v1.pending", "evidence-v1" })
        {
            ResetFileRoot();
            CreateFileRepository();
            string readiness = Path.Combine(_fileRoot, "artifacts", "snowglobe", "provider-readiness");
            Directory.CreateDirectory(Path.Combine(readiness, target));
            FileProviderReadinessOneShotArtifactStore store = new(_fileRoot);

            ProviderReadinessOneShotException failure = Assert.Throws<ProviderReadinessOneShotException>(() =>
                store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8));

            Assert.Equal("artifact_target_exists", failure.Code);
            Assert.False(File.Exists(Path.Combine(readiness, "one-shot-consumed-v1.json")));
        }
    }

    [Theory]
    [InlineData("openrouter_written", "artifact_publication_partial", true, false)]
    [InlineData("directory_renamed", "artifact_publication_ambiguous", false, true)]
    public async Task FileStoreFaultLeavesTerminalClaimAndBlocksEveryRerun(
        string checkpoint,
        string expectedCode,
        bool pendingExists,
        bool publishedExists)
    {
        CreateFileRepository();
        ProviderReadinessOneShotArtifacts artifacts = await CreateArtifactsAsync();
        FileProviderReadinessOneShotArtifactStore store = new(
            _fileRoot,
            current =>
            {
                if (current == checkpoint) throw new IOException("raw-secret-publication-fault");
            });
        store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8);

        ProviderReadinessOneShotException failure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.PublishAtomically(artifacts));

        Assert.Equal(expectedCode, failure.Code);
        string readiness = Path.Combine(_fileRoot, "artifacts", "snowglobe", "provider-readiness");
        Assert.True(File.Exists(Path.Combine(readiness, "one-shot-consumed-v1.json")));
        Assert.Equal(pendingExists, Directory.Exists(Path.Combine(readiness, ".evidence-v1.pending")));
        Assert.Equal(publishedExists, Directory.Exists(Path.Combine(readiness, "evidence-v1")));
        FileProviderReadinessOneShotArtifactStore second = new(_fileRoot);
        Assert.Equal("invocation_already_consumed", Assert.Throws<ProviderReadinessOneShotException>(() =>
            second.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8)).Code);
    }

    [Fact]
    public void FileStoreRejectsReparseReadinessRootWithoutFollowingIt()
    {
        CreateFileRepository();
        string outside = Path.Combine(_fileRoot, "outside");
        Directory.CreateDirectory(outside);
        string readiness = Path.Combine(_fileRoot, "artifacts", "snowglobe", "provider-readiness");
        try { Directory.CreateSymbolicLink(readiness, outside); }
        catch (UnauthorizedAccessException) { return; }
        FileProviderReadinessOneShotArtifactStore store = new(_fileRoot);

        ProviderReadinessOneShotException failure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8));

        Assert.Equal("artifact_path_reparse_rejected", failure.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        Directory.Delete(readiness);
    }

    [Fact]
    public async Task FileStoreRejectsHardLinkedClaimBeforeComparisonOrPublication()
    {
        CreateFileRepository();
        FileProviderReadinessOneShotArtifactStore store = new(_fileRoot);
        store.ClaimOnce(ProviderReadinessOneShotClaimCodec.CanonicalUtf8);
        string claim = Path.Combine(_fileRoot, "artifacts", "snowglobe", "provider-readiness", "one-shot-consumed-v1.json");
        string alias = Path.Combine(_fileRoot, "claim.alias");
        CreateHardLinkExact(alias, claim);

        ProviderReadinessOneShotException readFailure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.ReadAcceptedComparisonArtifact());
        Assert.Equal("invocation_claim_ambiguous", readFailure.Code);

        ProviderReadinessOneShotArtifacts artifacts = await CreateArtifactsAsync();
        ProviderReadinessOneShotException publishFailure = Assert.Throws<ProviderReadinessOneShotException>(() =>
            store.PublishAtomically(artifacts));
        Assert.Equal("invocation_claim_ambiguous", publishFailure.Code);
    }

    [Fact]
    public async Task HardLinkedComparisonIsRejectedBeforeEitherAdapterIsConstructed()
    {
        CreateFileRepository();
        string comparison = Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "cognition-quality", "provider-comparison-v1.json");
        CreateHardLinkExact(Path.Combine(_fileRoot, "comparison.alias"), comparison);
        List<string> order = [];
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(
            new FileProviderReadinessOneShotArtifactStore(_fileRoot),
            adapters,
            new FixedClock(ObservedAt));

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal("comparison_evidence_rejected", failure.Code);
        Assert.Equal(0, adapters.OpenRouterCreated);
        Assert.Equal(0, adapters.OllamaCreated);
        Assert.True(File.Exists(Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "provider-readiness", "one-shot-consumed-v1.json")));
    }

    [Fact]
    public async Task ReparseComparisonIsRejectedBeforeEitherAdapterIsConstructed()
    {
        CreateFileRepository();
        string comparison = Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "cognition-quality", "provider-comparison-v1.json");
        string outside = Path.Combine(_fileRoot, "comparison.outside.json");
        File.Move(comparison, outside);
        try { File.CreateSymbolicLink(comparison, outside); }
        catch (UnauthorizedAccessException)
        {
            File.Move(outside, comparison);
            return;
        }
        List<string> order = [];
        FakeAdapterFactory adapters = new(order, OpenRouterResult.Ready(), OllamaResult.Ready());
        ProviderReadinessOneShotCommand command = new(
            new FileProviderReadinessOneShotArtifactStore(_fileRoot),
            adapters,
            new FixedClock(ObservedAt));

        ProviderReadinessOneShotException failure = await Assert.ThrowsAsync<ProviderReadinessOneShotException>(
            async () => await command.ExecuteOnceAsync(Runtime, CancellationToken.None));

        Assert.Equal("comparison_evidence_rejected", failure.Code);
        Assert.Equal(0, adapters.OpenRouterCreated);
        Assert.Equal(0, adapters.OllamaCreated);
        Assert.True(File.Exists(Path.Combine(
            _fileRoot, "artifacts", "snowglobe", "provider-readiness", "one-shot-consumed-v1.json")));
        File.Delete(comparison);
    }

    [Fact]
    public async Task CancellationAndTimeoutAreRetainedAsUnknownWithoutASecondCall()
    {
        foreach ((ProviderReadinessAdapterResult openRouter, ProviderReadinessAdapterResult ollama) in new[]
        {
            (OpenRouterResult.Cancelled(), OllamaResult.Cancelled()),
            (OpenRouterResult.Timeout(), OllamaResult.Timeout())
        })
        {
            List<string> order = [];
            FakeArtifactStore store = new(order, AcceptedComparison());
            FakeAdapterFactory adapters = new(order, openRouter, ollama);
            ProviderReadinessOneShotCommand command = new(store, adapters, new FixedClock(ObservedAt));

            ProviderReadinessOneShotResult result = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);

            Assert.Equal("published", result.Status);
            Assert.Equal(1, adapters.OpenRouterCalls);
            Assert.Equal(1, adapters.OllamaCalls);
            Assert.Equal(1, store.PublishCount);
            ProviderReadinessObservation openRouterObservation = ProviderReadinessObservationModule.Validate(
                store.Published!.OpenRouterObservationCanonicalUtf8, ObservedAt);
            ProviderReadinessObservation ollamaObservation = ProviderReadinessObservationModule.Validate(
                store.Published.OllamaObservationCanonicalUtf8, ObservedAt);
            Assert.Equal("unknown", openRouterObservation.Readiness);
            Assert.Equal("unknown", ollamaObservation.Readiness);
        }
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "provider-readiness-record-once-v1" }];
        yield return [new[] { "provider-readiness-record-once-v1", "--ollama-pid", "4242", "--ollama-start-utc-ticks", Runtime.ProcessStartUtcTicks.ToString(), "extra" }];
        yield return [new[] { "provider-readiness-record-once-v1", "--ollama-pid", "0", "--ollama-start-utc-ticks", Runtime.ProcessStartUtcTicks.ToString() }];
        yield return [new[] { "provider-readiness-record-once-v1", "--ollama-pid", "4242", "--ollama-start-utc-ticks", "0" }];
        yield return [new[] { "provider-readiness-record-once-v1", "--api-key", "secret", "--ollama-start-utc-ticks", Runtime.ProcessStartUtcTicks.ToString() }];
        yield return [new[] { "provider-readiness-record-once-v1", "--repository-root", @"C:\outside", "--ollama-pid", "4242" }];
    }

    private static string[] ValidArgs() =>
    [
        "provider-readiness-record-once-v1",
        "--ollama-pid", Runtime.ProcessId.ToString(),
        "--ollama-start-utc-ticks", Runtime.ProcessStartUtcTicks.ToString()
    ];

    private static byte[] AcceptedComparison()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CURRENT_BUILD.md")))
            current = current.Parent;
        Assert.NotNull(current);
        return File.ReadAllBytes(Path.Combine(current!.FullName,
            "artifacts", "snowglobe", "cognition-quality", "provider-comparison-v1.json"));
    }

    private static byte[] CreateClaimForSourceCommit(string sourceCommit)
    {
        JsonObject root = JsonNode.Parse(ProviderReadinessOneShotClaimCodec.CanonicalUtf8.Span)!
            .AsObject();
        root["source_commit"] = sourceCommit;
        Assert.True(root.Remove("claim_payload_digest_sha256"));
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        string digest = CognitionQualityHash.Sha256(payload);
        Array.Clear(payload);
        root["claim_payload_digest_sha256"] = digest;
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private async Task<ProviderReadinessOneShotArtifacts> CreateArtifactsAsync()
    {
        List<string> order = [];
        FakeArtifactStore store = new(order, AcceptedComparison());
        ProviderReadinessOneShotCommand command = new(
            store,
            new FakeAdapterFactory(order, OpenRouterResult.Ready(), OllamaResult.Ready()),
            new FixedClock(ObservedAt));
        _ = await command.ExecuteOnceAsync(Runtime, CancellationToken.None);
        return store.Published!;
    }

    private void CreateFileRepository()
    {
        Directory.CreateDirectory(_fileRoot);
        File.WriteAllText(Path.Combine(_fileRoot, ".git"), "gitdir: .git-worktree");
        File.WriteAllText(Path.Combine(_fileRoot, "CURRENT_BUILD.md"), "test repository marker");
        string lab = Path.Combine(_fileRoot, "labs", "Societies.SnowGlobe");
        Directory.CreateDirectory(lab);
        File.WriteAllText(Path.Combine(lab, "Societies.SnowGlobe.csproj"), "<Project />");
        string cognition = Path.Combine(_fileRoot, "artifacts", "snowglobe", "cognition-quality");
        Directory.CreateDirectory(cognition);
        File.WriteAllBytes(Path.Combine(cognition, "provider-comparison-v1.json"), AcceptedComparison());
    }

    private void ResetFileRoot()
    {
        if (Directory.Exists(_fileRoot)) Directory.Delete(_fileRoot, recursive: true);
    }

    public void Dispose()
    {
        try { ResetFileRoot(); }
        catch { }
    }

    private static void CreateHardLinkExact(string alias, string existing)
    {
        if (!CreateHardLink(alias, existing, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);

    private sealed class FixedClock(long now) : IProviderReadinessClock
    {
        public long NowMilliseconds => now;
    }

    private sealed class SequenceClock(params long[] values) : IProviderReadinessClock
    {
        private int _index;
        public long NowMilliseconds => values[Math.Min(Interlocked.Increment(ref _index) - 1, values.Length - 1)];
    }

    private sealed class FakeArtifactStore(List<string> order, byte[] comparison)
        : IProviderReadinessOneShotArtifactStore
    {
        private bool _claimed;
        public int ClaimCount { get; private set; }
        public int PublishCount { get; private set; }
        public string? PublishFailureCode { get; init; }
        public ProviderReadinessOneShotArtifacts? Published { get; private set; }

        public void ClaimOnce(ReadOnlyMemory<byte> canonicalClaim)
        {
            order.Add("claim");
            if (_claimed) throw new ProviderReadinessOneShotException("invocation_already_consumed");
            _claimed = true;
            ClaimCount++;
            _ = ProviderReadinessOneShotClaimCodec.Validate(canonicalClaim);
        }

        public byte[] ReadAcceptedComparisonArtifact()
        {
            order.Add("comparison");
            return comparison.ToArray();
        }

        public void PublishAtomically(ProviderReadinessOneShotArtifacts artifacts)
        {
            order.Add("publish");
            PublishCount++;
            if (PublishFailureCode is not null)
                throw new ProviderReadinessOneShotException(PublishFailureCode);
            Published = artifacts.Copy();
        }
    }

    private sealed class FakeAdapterFactory(
        List<string> order,
        ProviderReadinessAdapterResult openRouterResult,
        ProviderReadinessAdapterResult ollamaResult)
        : IProviderReadinessOneShotAdapterFactory
    {
        public int OpenRouterCreated { get; private set; }
        public int OllamaCreated { get; private set; }
        public int OpenRouterCalls { get; private set; }
        public int OllamaCalls { get; private set; }

        public IProviderReadinessObservationAdapter CreateOpenRouter()
        {
            order.Add("openrouter_factory");
            OpenRouterCreated++;
            return new FakeAdapter(ProviderReadinessProvider.OpenRouter, openRouterResult, () =>
            {
                order.Add("openrouter");
                OpenRouterCalls++;
            });
        }

        public IProviderReadinessObservationAdapter CreateOllama(PinnedRuntimeObservation runtime)
        {
            Assert.Equal(Runtime, runtime);
            order.Add("ollama_factory");
            OllamaCreated++;
            return new FakeAdapter(ProviderReadinessProvider.Ollama, ollamaResult, () =>
            {
                order.Add("ollama");
                OllamaCalls++;
            });
        }
    }

    private sealed class FakeAdapter(
        ProviderReadinessProvider provider,
        ProviderReadinessAdapterResult result,
        Action called) : IProviderReadinessObservationAdapter
    {
        public ProviderReadinessProvider Provider => provider;
        public ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(CancellationToken cancellationToken)
        {
            called();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ConcreteAdapterFactory(
        List<string> order,
        PinnedRuntimeObservation runtime,
        bool honorPreCancellation = false) : IProviderReadinessOneShotAdapterFactory
    {
        private readonly ConcreteOpenRouterVerifier _openRouter = new(order, honorPreCancellation);
        private readonly RecordingTagsHandler _handler = new(order, ValidTags());
        private readonly RecordingRuntimeVerifier _runtime = new();

        internal int OpenRouterVerifierCalls => _openRouter.CallCount;
        internal int OpenRouterProviderDispatchCount => _openRouter.ProviderDispatchCount;
        internal int OllamaHandlerCalls => _handler.CallCount;
        internal IReadOnlyList<HttpMethod> OllamaMethods => _handler.Methods;
        internal IReadOnlyList<string> OllamaUris => _handler.Uris;
        internal IReadOnlyList<string?> OllamaAuthorizationSchemes => _handler.AuthorizationSchemes;
        internal IReadOnlyList<bool> OllamaHadContent => _handler.HadContent;
        internal IReadOnlyList<OllamaLoopbackRuntimeCheckPoint> RuntimeCheckPoints => _runtime.CheckPoints;

        public IProviderReadinessObservationAdapter CreateOpenRouter()
        {
            order.Add("openrouter_factory");
            return new OpenRouterAuthenticatedReadinessAdapter(_openRouter);
        }

        public IProviderReadinessObservationAdapter CreateOllama(PinnedRuntimeObservation supplied)
        {
            Assert.Equal(runtime, supplied);
            order.Add("ollama_factory");
            OllamaLoopbackRuntimeBinding binding = new(
                supplied.ProcessId,
                supplied.ProcessStartUtcTicks,
                SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath,
                SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256,
                SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity,
                supplied.ProcessId);
            return new OllamaAuthenticatedReadinessAdapter(binding, _runtime, _handler);
        }
    }

    private sealed class ConcreteOpenRouterVerifier(List<string> order, bool honorPreCancellation)
        : IOpenRouterAuthenticatedReadinessMetadataVerifier
    {
        internal int CallCount { get; private set; }
        internal int ProviderDispatchCount { get; private set; }

        public ValueTask<OpenRouterAuthenticatedMetadataReadinessResult> ObserveReadinessOnceAsync(
            CancellationToken cancellationToken)
        {
            order.Add("openrouter_metadata");
            CallCount++;
            if (honorPreCancellation && cancellationToken.IsCancellationRequested)
                return ValueTask.FromResult(new OpenRouterAuthenticatedMetadataReadinessResult(
                    Ready: false,
                    DiagnosticCode: "operation_cancelled",
                    RequestCount: 0,
                    SameAccountBound: false));
            ProviderDispatchCount++;
            return ValueTask.FromResult(new OpenRouterAuthenticatedMetadataReadinessResult(
                Ready: true,
                DiagnosticCode: "ready",
                RequestCount: 3,
                SameAccountBound: true));
        }
    }

    private sealed class RecordingRuntimeVerifier : IOllamaLoopbackRuntimeVerifier
    {
        internal List<OllamaLoopbackRuntimeCheckPoint> CheckPoints { get; } = [];

        public OllamaLoopbackRuntimeVerification Verify(
            OllamaLoopbackRuntimeBinding binding,
            OllamaLoopbackRuntimeCheckPoint checkPoint,
            OllamaLoopbackConnectionIdentity? connection)
        {
            CheckPoints.Add(checkPoint);
            return OllamaLoopbackRuntimeVerification.Pass;
        }
    }

    private sealed class RecordingTagsHandler(List<string> order, byte[] body) : HttpMessageHandler
    {
        internal int CallCount { get; private set; }
        internal List<HttpMethod> Methods { get; } = [];
        internal List<string> Uris { get; } = [];
        internal List<string?> AuthorizationSchemes { get; } = [];
        internal List<bool> HadContent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            order.Add("ollama_http");
            CallCount++;
            Methods.Add(request.Method);
            Uris.Add(request.RequestUri!.AbsoluteUri);
            AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
            HadContent.Add(request.Content is not null);
            ByteArrayContent content = new(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version11,
                RequestMessage = request,
                Content = content
            });
        }
    }

    private static byte[] ValidTags() => JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
    {
        ["models"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["name"] = SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
                ["model"] = SnowGlobePinnedOllamaRecordingModule.RuntimeModelReference,
                ["modified_at"] = "2026-08-24T12:00:00Z",
                ["size"] = SnowGlobePinnedOllamaRecordingModule.ArtifactSizeBytes,
                ["digest"] = SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
                ["details"] = new Dictionary<string, object?>
                {
                    ["parent_model"] = string.Empty,
                    ["format"] = SnowGlobePinnedOllamaRecordingModule.ArtifactFormat,
                    ["family"] = SnowGlobePinnedOllamaRecordingModule.ModelFamily,
                    ["families"] = new[] { SnowGlobePinnedOllamaRecordingModule.ModelFamily },
                    ["parameter_size"] = "4.7B",
                    ["quantization_level"] = SnowGlobePinnedOllamaRecordingModule.QuantizationLevel,
                    ["context_length"] = 262_144,
                    ["embedding_length"] = 2_560
                },
                ["capabilities"] = new[] { "completion" }
            }
        }
    });

    private static class OpenRouterResult
    {
        internal static ProviderReadinessAdapterResult Ready() => ProviderReadinessAdapterResult.Ready(
            3, OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256, "same_account_bound");
        internal static ProviderReadinessAdapterResult CredentialUnavailable() => ProviderReadinessAdapterResult.Unavailable(
            "credential_unavailable", 0, OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_performed");
        internal static ProviderReadinessAdapterResult Cancelled() => ProviderReadinessAdapterResult.Unknown(
            "operation_cancelled", 0, OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_performed");
        internal static ProviderReadinessAdapterResult Timeout() => ProviderReadinessAdapterResult.Unknown(
            "observation_timeout", 1, OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_performed");
    }

    private static class OllamaResult
    {
        internal static ProviderReadinessAdapterResult Ready() => ProviderReadinessAdapterResult.Ready(
            1, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable");
        internal static ProviderReadinessAdapterResult ProviderUnavailable() => ProviderReadinessAdapterResult.Unavailable(
            "provider_unavailable", 1, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable");
        internal static ProviderReadinessAdapterResult Cancelled() => ProviderReadinessAdapterResult.Unknown(
            "operation_cancelled", 0, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable");
        internal static ProviderReadinessAdapterResult Timeout() => ProviderReadinessAdapterResult.Unknown(
            "observation_timeout", 1, OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
            OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256, "not_applicable");
    }

    private sealed class CountingCommandFactory(ProviderReadinessOneShotCommand? command)
        : IProviderReadinessOneShotCommandFactory
    {
        public int CreateCount { get; private set; }
        public ProviderReadinessOneShotCommand Create()
        {
            CreateCount++;
            return command ?? throw new InvalidOperationException("factory_not_expected");
        }
    }
}
