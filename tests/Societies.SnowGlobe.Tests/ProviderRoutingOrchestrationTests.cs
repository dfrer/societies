using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ProviderRoutingOrchestrationTests
{
    private const long Now = 1_800_000_000_000;
    private static readonly byte[] Comparison = ReadComparison();

    [Fact]
    public async Task PreferredOnlinePreparesOpenRouterOnlyAfterDurableClaim()
    {
        Assert.Equal("16550d06f0eee280f4618c76bf8ff556320dc0d0198c4b15bcd8021ed29ac230",
            ProviderRoutingOrchestrationModule.ContractDigestSha256);
        using TestIntegrityAnchor anchor = new(0x61);
        ProviderRoutingAttemptLedgerModule ledger = new(
            new InMemoryProviderRoutingAttemptLedgerStorage(anchor), anchor,
            new SequenceAttemptIdSource(new string('a', 64)));
        ProviderRoutingOrchestrationModule module = new(ledger);
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);

        ProviderRoutingOrchestrationResult result = module.Prepare(new(
            Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
            ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal("prepared", result.Status);
        Assert.Equal(ProviderRoutingSelectedProvider.OpenRouter, result.SelectedProvider);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            ledger.Inspect(result.AttemptId).State);
        Assert.Equal(result.CanonicalDigestSha256,
            module.Validate(result.CanonicalUtf8).CanonicalDigestSha256);
        Assert.Equal("orchestration_already_used",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
    }

    [Fact]
    public async Task LocalOnlyPreparesOllamaThroughTheSameDurablePath()
    {
        using TestIntegrityAnchor anchor = new(0x62);
        ProviderRoutingAttemptLedgerModule ledger = NewLedger(anchor, new string('b', 64));
        ProviderRoutingOrchestrationModule module = new(ledger);
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);

        ProviderRoutingOrchestrationResult result = module.Prepare(new(
            Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
            ProviderRoutingIntent.LocalOnly, Now));

        Assert.Equal("prepared", result.Status);
        Assert.Equal(ProviderRoutingSelectedProvider.Ollama, result.SelectedProvider);
        Assert.Equal("explicit_local_only", result.ReasonCode);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            ledger.Inspect(result.AttemptId).State);
    }

    [Fact]
    public void MissingReadinessProducesExplicitNonPreparedAuthenticatedAttempt()
    {
        using TestIntegrityAnchor anchor = new(0x63);
        ProviderRoutingAttemptLedgerModule ledger = NewLedger(anchor, new string('c', 64));
        ProviderRoutingOrchestrationModule module = new(ledger);

        ProviderRoutingOrchestrationResult result = module.Prepare(new(
            Comparison, null, null, ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal("not_prepared", result.Status);
        Assert.Null(result.SelectedProvider);
        Assert.Null(result.ClaimedAttemptRecordDigestSha256);
        Assert.Equal("openrouter_readiness_unknown", result.ReasonCode);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            ledger.Inspect(result.AttemptId).State);
        Assert.Equal(result.CanonicalDigestSha256,
            module.Validate(result.CanonicalUtf8).CanonicalDigestSha256);
    }

    [Fact]
    public async Task WrongProviderMalformedAndExpiredObservationsNeverSelect()
    {
        using TestIntegrityAnchor wrongAnchor = new(0x64);
        ProviderRoutingOrchestrationModule wrong = new(NewLedger(
            wrongAnchor, new string('d', 64)));
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);
        ProviderRoutingOrchestrationResult wrongResult = wrong.Prepare(new(
            Comparison, ollama.CanonicalUtf8, openRouter.CanonicalUtf8,
            ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("not_prepared", wrongResult.Status);
        Assert.Null(wrongResult.SelectedProvider);

        using TestIntegrityAnchor malformedAnchor = new(0x65);
        ProviderRoutingOrchestrationModule malformed = new(NewLedger(
            malformedAnchor, new string('e', 64)));
        byte[] secretMalformed = Encoding.UTF8.GetBytes(
            "{\"raw_response\":\"ORCHESTRATION_SECRET_SENTINEL\"");
        ProviderRoutingOrchestrationResult malformedResult = malformed.Prepare(new(
            Comparison, secretMalformed, null,
            ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("not_prepared", malformedResult.Status);
        Assert.DoesNotContain("ORCHESTRATION_SECRET_SENTINEL", malformedResult.CanonicalJson,
            StringComparison.Ordinal);

        long expiredAt = Now - ProviderReadinessObservationModule.ObservationLifetimeMilliseconds - 1;
        ProviderReadinessObservation expired = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter, expiredAt);
        using TestIntegrityAnchor expiredAnchor = new(0x66);
        ProviderRoutingOrchestrationModule expiredModule = new(NewLedger(
            expiredAnchor, new string('f', 64)));
        ProviderRoutingOrchestrationResult expiredResult = expiredModule.Prepare(new(
            Comparison, expired.CanonicalUtf8, ollama.CanonicalUtf8,
            ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("not_prepared", expiredResult.Status);
        Assert.Null(expiredResult.SelectedProvider);
    }

    [Fact]
    public void MissingMalformedUnsupportedAndTiedComparisonsFailBeforeAttemptCreation()
    {
        using TestIntegrityAnchor anchor = new(0x67);
        CountingStorage storage = new(anchor);

        foreach (ReadOnlyMemory<byte>? comparison in new ReadOnlyMemory<byte>?[]
        {
            null,
            "{bad"u8.ToArray(),
            AlternateAcceptedComparison(),
            TiedComparison()
        })
        {
            ProviderRoutingOrchestrationModule module = new(new ProviderRoutingAttemptLedgerModule(
                storage, anchor, new SequenceAttemptIdSource(new string('1', 64))));
            Assert.Equal("orchestration_comparison_unaccepted",
                Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Prepare(new(
                    comparison, null, null, ProviderRoutingIntent.PreferredOnline, Now))).Code);
        }
        Assert.Equal(0, storage.CreateCount);
    }

    [Fact]
    public async Task CallerEvidenceIsReadOnceAndOversizedEvidenceIsNotRead()
    {
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);
        using ChangingMemoryManager comparison = new(Comparison);
        using ChangingMemoryManager openRouterMemory = new(openRouter.CanonicalUtf8.ToArray());
        using ChangingMemoryManager ollamaMemory = new(ollama.CanonicalUtf8.ToArray());
        using TestIntegrityAnchor anchor = new(0x68);
        ProviderRoutingOrchestrationModule module = new(NewLedger(anchor, new string('2', 64)));

        ProviderRoutingOrchestrationResult result = module.Prepare(new(
            comparison.CreateReadOnlyMemory(), openRouterMemory.CreateReadOnlyMemory(),
            ollamaMemory.CreateReadOnlyMemory(), ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal("prepared", result.Status);
        Assert.Equal(1, comparison.GetSpanCallCount);
        Assert.Equal(1, openRouterMemory.GetSpanCallCount);
        Assert.Equal(1, ollamaMemory.GetSpanCallCount);

        using ThrowingMemoryManager oversized = new(
            CognitionQualityComparisonModule.MaximumArtifactBytes + 1);
        using TestIntegrityAnchor oversizedAnchor = new(0x69);
        ProviderRoutingOrchestrationModule oversizedModule = new(NewLedger(
            oversizedAnchor, new string('3', 64)));
        Assert.Equal("orchestration_input_size_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => oversizedModule.Prepare(new(
                oversized.CreateReadOnlyMemory(), null, null,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(0, oversized.GetSpanCallCount);
    }

    [Fact]
    public void InvalidInputsAndEveryPreparationOutcomeConsumeTheOneShotModule()
    {
        using TestIntegrityAnchor invalidAnchor = new(0x70);
        ProviderRoutingOrchestrationModule invalid = new(NewLedger(
            invalidAnchor, new string('4', 64)));
        Assert.Equal("orchestration_input_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => invalid.Prepare(new(
                Comparison, null, null, (ProviderRoutingIntent)999, Now))).Code);
        Assert.Equal("orchestration_already_used",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => invalid.Prepare(new(
                Comparison, null, null, ProviderRoutingIntent.PreferredOnline, Now))).Code);

        using TestIntegrityAnchor noSelectionAnchor = new(0x71);
        ProviderRoutingOrchestrationModule noSelection = new(NewLedger(
            noSelectionAnchor, new string('5', 64)));
        _ = noSelection.Prepare(new(Comparison, null, null,
            ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("orchestration_already_used",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => noSelection.Prepare(new(
                Comparison, null, null, ProviderRoutingIntent.PreferredOnline, Now))).Code);
    }

    [Fact]
    public async Task CreatePreClaimStaleAndAmbiguousLedgerFailuresStayClosedWithoutSecondAttempt()
    {
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);

        using TestIntegrityAnchor createAnchor = new(0x72);
        FaultingStorage createStorage = new(createAnchor) { FailCreate = true };
        ProviderRoutingOrchestrationModule createModule = new(new ProviderRoutingAttemptLedgerModule(
            createStorage, createAnchor, new SequenceAttemptIdSource(new string('6', 64))));
        Assert.Equal("orchestration_ledger_create_failed",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => createModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(1, createStorage.CreateCount);

        using TestIntegrityAnchor preAnchor = new(0x73);
        FaultingStorage preStorage = new(preAnchor) { ClaimMode = ClaimFaultMode.PreTombstone };
        ProviderRoutingAttemptLedgerModule preLedger = new(preStorage, preAnchor,
            new SequenceAttemptIdSource(new string('7', 64)));
        ProviderRoutingOrchestrationModule preModule = new(preLedger);
        Assert.Equal("orchestration_ledger_claim_failed",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => preModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            preLedger.Inspect(new string('7', 64)).State);
        Assert.Equal("orchestration_already_used",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => preModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);

        using TestIntegrityAnchor staleAnchor = new(0x74);
        FaultingStorage staleStorage = new(staleAnchor) { ClaimMode = ClaimFaultMode.Stale };
        ProviderRoutingAttemptLedgerModule staleLedger = new(staleStorage, staleAnchor,
            new SequenceAttemptIdSource(new string('8', 64)));
        ProviderRoutingOrchestrationModule staleModule = new(staleLedger);
        Assert.Equal("orchestration_ledger_claim_failed",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => staleModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            staleLedger.Inspect(new string('8', 64)).State);

        using TestIntegrityAnchor ambiguousAnchor = new(0x75);
        FaultingStorage ambiguousStorage = new(ambiguousAnchor)
        {
            ClaimMode = ClaimFaultMode.TerminalAmbiguous
        };
        ProviderRoutingAttemptLedgerModule ambiguousLedger = new(ambiguousStorage, ambiguousAnchor,
            new SequenceAttemptIdSource(new string('9', 64)));
        ProviderRoutingOrchestrationModule ambiguousModule = new(ambiguousLedger);
        Assert.Equal("orchestration_claim_terminal_or_ambiguous",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => ambiguousModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            ambiguousLedger.Inspect(new string('9', 64)).State);
        Assert.Equal(1, ambiguousStorage.CreateCount);
        Assert.Equal(1, ambiguousStorage.ClaimCount);

        using TestIntegrityAnchor corruptAnchor = new(0x79);
        ProviderRoutingAttemptLedgerModule corruptLedger = NewLedger(
            corruptAnchor, new string('d', 64));
        PostClaimValiditySplicingLedger corrupting = new(corruptLedger, corruptAnchor);
        ProviderRoutingOrchestrationModule corruptModule = new(corrupting);
        Assert.Equal("orchestration_claim_terminal_or_ambiguous",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => corruptModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            corruptLedger.Inspect(new string('d', 64)).State);

        using TestIntegrityAnchor embeddingAnchor = new(0x7a);
        ProviderRoutingAttemptLedgerModule embeddingLedger = NewLedger(
            embeddingAnchor, new string('e', 64));
        ProviderRoutingOrchestrationModule embeddingModule = new(
            new PostClaimCanonicalEmbeddingFailureLedger(embeddingLedger));
        Assert.Equal("orchestration_claim_terminal_or_ambiguous",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => embeddingModule.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            embeddingLedger.Inspect(new string('e', 64)).State);
    }

    [Fact]
    public async Task DeterministicDependenciesProduceByteIdenticalResults()
    {
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);
        ProviderRoutingOrchestrationResult[] results = new ProviderRoutingOrchestrationResult[2];
        for (int index = 0; index < results.Length; index++)
        {
            using TestIntegrityAnchor anchor = new(0x76);
            ProviderRoutingOrchestrationModule module = new(NewLedger(
                anchor, new string('a', 64)));
            results[index] = module.Prepare(new(
                Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
                ProviderRoutingIntent.PreferredOnline, Now));
        }
        Assert.Equal(results[0].CanonicalJson, results[1].CanonicalJson);
        Assert.Equal(results[0].CanonicalDigestSha256, results[1].CanonicalDigestSha256);
    }

    [Fact]
    public async Task CanonicalValidationRejectsTamperDuplicateDeepOversizedAndChangingMemory()
    {
        using TestIntegrityAnchor anchor = new(0x77);
        ProviderRoutingAttemptLedgerModule ledger = NewLedger(anchor, new string('b', 64));
        ProviderRoutingOrchestrationModule module = new(ledger);
        ProviderReadinessObservation openRouter = await ReadyObservation(
            ProviderReadinessProvider.OpenRouter);
        ProviderReadinessObservation ollama = await ReadyObservation(
            ProviderReadinessProvider.Ollama);
        ProviderRoutingOrchestrationResult result = module.Prepare(new(
            Comparison, openRouter.CanonicalUtf8, ollama.CanonicalUtf8,
            ProviderRoutingIntent.PreferredOnline, Now));

        byte[] changed = result.CanonicalUtf8.ToArray();
        changed[^2] ^= 1;
        Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Validate(changed));
        byte[] rebound = ForgeResult(result, root => root["selected_provider"] = "ollama");
        Assert.Equal("orchestration_result_binding_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Validate(rebound)).Code);
        JsonObject resultRoot = JsonNode.Parse(result.CanonicalUtf8.Span)!.AsObject();
        byte[] claimedBytes = JsonSerializer.SerializeToUtf8Bytes(
            resultRoot["claimed_attempt_record"]);
        ProviderRoutingAttemptRecord claimed = ledger.Validate(claimedBytes);
        ProviderRoutingAttemptRecord validitySplice = ForgeValidityDates(anchor, claimed);
        byte[] splicedResult = ForgeResult(result, root =>
            root["claimed_attempt_record"] = JsonNode.Parse(validitySplice.CanonicalUtf8.Span));
        Assert.Equal("orchestration_result_binding_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() =>
                module.Validate(splicedResult)).Code);
        byte[] duplicate = Encoding.UTF8.GetBytes(result.CanonicalJson.Replace(
            "{\"schema_version\"",
            "{\"schema_version\":\"duplicate\",\"schema_version\"",
            StringComparison.Ordinal));
        Assert.Equal("orchestration_result_shape_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Validate(duplicate)).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 20) + new string(']', 20));
        Assert.Equal("orchestration_result_json_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Validate(deep)).Code);
        Assert.Equal("orchestration_result_size_invalid",
            Assert.Throws<ProviderRoutingOrchestrationException>(() => module.Validate(
                new byte[ProviderRoutingOrchestrationModule.MaximumResultBytes + 1])).Code);

        using ChangingMemoryManager manager = new(result.CanonicalUtf8.ToArray());
        Assert.Equal(result.CanonicalDigestSha256,
            module.Validate(manager.CreateReadOnlyMemory()).CanonicalDigestSha256);
        Assert.Equal(1, manager.GetSpanCallCount);

        ReadOnlyMemory<byte> detached = result.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', result.CanonicalUtf8.Span[0]);
    }

    [Fact]
    public void PublicInterfaceAndCanonicalEvidenceLeakNoExecutionAuthorityOrRawInputs()
    {
        Type module = typeof(ProviderRoutingOrchestrationModule);
        Assert.Empty(module.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[] { "Prepare", "Validate" }, module.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).Order(StringComparer.Ordinal));
        string surface = string.Join('|', new[]
        {
            typeof(ProviderRoutingOrchestrationModule),
            typeof(ProviderRoutingOrchestrationInput),
            typeof(ProviderRoutingOrchestrationResult)
        }.SelectMany(type => type.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(member => member.ToString()));
        foreach (string forbidden in new[]
        {
            "Adapter", "Delegate", "Transport", "Credential", "Endpoint", "RequestBody",
            "Execute", "Generate", "Payment", "Retry", "Fallback", "World", "File", "Path",
            "Root", "Http", "Socket", "Process", "Stream", "Task"
        }) Assert.DoesNotContain(forbidden, surface, StringComparison.OrdinalIgnoreCase);

        using TestIntegrityAnchor anchor = new(0x78);
        ProviderRoutingOrchestrationResult result = new ProviderRoutingOrchestrationModule(
            NewLedger(anchor, new string('c', 64))).Prepare(new(
                Comparison, null, null, ProviderRoutingIntent.PreferredOnline, Now));
        foreach (string forbidden in new[]
        {
            "Bearer ", "credential_value", "account_id", "raw_response", "raw_metadata", "prompt",
            "proposal", "reasoning", "request_body", "response_body", "endpoint", "host_path",
            "process_id", "dynamic_error", "ORCHESTRATION_SECRET_SENTINEL"
        }) Assert.DoesNotContain(forbidden, result.CanonicalJson, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderRoutingAttemptLedgerModule NewLedger(
        IProviderRoutingAttemptIntegrityAnchor anchor,
        string attemptId) => new(
            new InMemoryProviderRoutingAttemptLedgerStorage(anchor), anchor,
            new SequenceAttemptIdSource(attemptId));

    private static ValueTask<ProviderReadinessObservation> ReadyObservation(
        ProviderReadinessProvider provider,
        long observedAt = Now) => ProviderReadinessObservationModule.ObserveAsync(
            new ReadinessAdapter(provider,
                provider == ProviderReadinessProvider.OpenRouter
                    ? ProviderReadinessAdapterResult.Ready(3,
                        OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                        OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                        "same_account_bound")
                    : ProviderReadinessAdapterResult.Ready(1,
                        OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                        OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                        "not_applicable")),
            new ReadinessClock(observedAt), CancellationToken.None);

    private static byte[] AlternateAcceptedComparison()
    {
        CognitionQualityComparisonArtifact accepted =
            CognitionQualityComparisonModule.Validate(Comparison);
        CognitionQualityProviderComparison ollama = accepted.Providers[0];
        CognitionQualityProviderComparison openRouter = accepted.Providers[1];
        string changedDigest = ollama.SourceArtifactDigestSha256![0] == '0'
            ? "1" + ollama.SourceArtifactDigestSha256[1..]
            : "0" + ollama.SourceArtifactDigestSha256[1..];
        return CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(changedDigest,
                ollama.NormalizedProposalEvidence!),
            new CognitionQualityComparisonInput(openRouter.SourceArtifactDigestSha256!,
                openRouter.NormalizedProposalEvidence!)).CanonicalUtf8.ToArray();
    }

    private static byte[] TiedComparison()
    {
        CognitionQualityProviderComparison evidence =
            CognitionQualityComparisonModule.Validate(Comparison).Providers[0];
        return CognitionQualityComparisonModule.Compare(
            new CognitionQualityComparisonInput(new string('a', 64),
                evidence.NormalizedProposalEvidence!),
            new CognitionQualityComparisonInput(new string('b', 64),
                evidence.NormalizedProposalEvidence!)).CanonicalUtf8.ToArray();
    }

    private static byte[] ForgeResult(
        ProviderRoutingOrchestrationResult result,
        Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(result.CanonicalUtf8.Span)!.AsObject();
        mutation(root);
        root.Remove("orchestration_payload_digest_sha256");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(root);
        root["orchestration_payload_digest_sha256"] = Convert.ToHexString(
            SHA256.HashData(payload)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(payload);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static ProviderRoutingAttemptRecord ForgeValidityDates(
        IProviderRoutingAttemptIntegrityAnchor anchor,
        ProviderRoutingAttemptRecord claimed) => ProviderRoutingAttemptLedgerCodec.CreateRecord(
            anchor, claimed.State, claimed.AttemptId, claimed.Sequence,
            claimed.PreviousRecordDigestSha256,
            claimed.CreatedAtUnixMilliseconds - 1,
            claimed.ExpiresAtUnixMilliseconds + 1,
            claimed.ComparisonArtifactDigestSha256,
            claimed.ReadinessAssessmentDigestSha256,
            claimed.ReadinessAssessmentSchemaVersion,
            claimed.OpenRouterReadinessCode,
            claimed.OllamaReadinessCode,
            claimed.IntentCode,
            claimed.SelectedProvider,
            claimed.RoutingDecisionDigestSha256,
            claimed.ClaimedAtUnixMilliseconds,
            claimed.TerminalReasonCode);

    private static byte[] ReadComparison()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string path = Path.Combine(current.FullName, "artifacts", "snowglobe",
                "cognition-quality", "provider-comparison-v1.json");
            if (File.Exists(path)) return File.ReadAllBytes(path);
            current = current.Parent;
        }
        throw new FileNotFoundException("Accepted comparison fixture not found.");
    }

    private sealed class SequenceAttemptIdSource(string value) : IProviderRoutingAttemptIdSource
    {
        public string NextAttemptId() => value;
    }

    private sealed class ReadinessClock(long now) : IProviderReadinessClock
    {
        public long NowMilliseconds => now;
    }

    private sealed class ReadinessAdapter(
        ProviderReadinessProvider provider,
        ProviderReadinessAdapterResult result) : IProviderReadinessObservationAdapter
    {
        public ProviderReadinessProvider Provider => provider;
        public ValueTask<ProviderReadinessAdapterResult> ObserveOnceAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private enum ClaimFaultMode
    {
        None,
        PreTombstone,
        Stale,
        TerminalAmbiguous
    }

    private sealed class CountingStorage : IProviderRoutingAttemptLedgerStorage
    {
        private readonly InMemoryProviderRoutingAttemptLedgerStorage _inner;
        internal CountingStorage(IProviderRoutingAttemptIntegrityAnchor anchor)
        {
            _inner = new(anchor);
            IntegrityAnchorIdentitySha256 = anchor.IdentitySha256;
        }
        internal int CreateCount { get; private set; }
        internal int ClaimCount { get; private set; }
        public string IntegrityAnchorIdentitySha256 { get; }
        public void CreateNew(
            string attemptId,
            ReadOnlySpan<byte> initialRecordCanonicalUtf8)
        {
            CreateCount++;
            _inner.CreateNew(attemptId, initialRecordCanonicalUtf8);
        }
        public byte[] ReadCurrent(string attemptId) => _inner.ReadCurrent(attemptId);
        public byte[] ClaimOnce(
            string attemptId,
            string expectedRecordDigestSha256,
            ReadOnlySpan<byte> tombstoneCanonicalUtf8,
            ReadOnlySpan<byte> dispatchRecordCanonicalUtf8,
            ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
        {
            ClaimCount++;
            return _inner.ClaimOnce(attemptId, expectedRecordDigestSha256,
                tombstoneCanonicalUtf8, dispatchRecordCanonicalUtf8,
                unknownRecordCanonicalUtf8);
        }
    }

    private sealed class FaultingStorage : IProviderRoutingAttemptLedgerStorage
    {
        private readonly InMemoryProviderRoutingAttemptLedgerStorage _inner;
        internal FaultingStorage(IProviderRoutingAttemptIntegrityAnchor anchor)
        {
            _inner = new(anchor);
            IntegrityAnchorIdentitySha256 = anchor.IdentitySha256;
        }
        internal bool FailCreate { get; set; }
        internal ClaimFaultMode ClaimMode { get; set; }
        internal int CreateCount { get; private set; }
        internal int ClaimCount { get; private set; }
        public string IntegrityAnchorIdentitySha256 { get; }
        public void CreateNew(string attemptId, ReadOnlySpan<byte> initialRecordCanonicalUtf8)
        {
            CreateCount++;
            if (FailCreate)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_unavailable");
            _inner.CreateNew(attemptId, initialRecordCanonicalUtf8);
        }
        public byte[] ReadCurrent(string attemptId) => _inner.ReadCurrent(attemptId);
        public byte[] ClaimOnce(
            string attemptId,
            string expectedRecordDigestSha256,
            ReadOnlySpan<byte> tombstoneCanonicalUtf8,
            ReadOnlySpan<byte> dispatchRecordCanonicalUtf8,
            ReadOnlySpan<byte> unknownRecordCanonicalUtf8)
        {
            ClaimCount++;
            if (ClaimMode == ClaimFaultMode.PreTombstone)
                throw new ProviderRoutingAttemptStorageClaimException(
                    ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone);
            if (ClaimMode == ClaimFaultMode.Stale)
                throw ProviderRoutingAttemptLedgerModule.Failure(
                    "attempt_expected_record_mismatch");
            byte[] retained = _inner.ClaimOnce(attemptId, expectedRecordDigestSha256,
                tombstoneCanonicalUtf8, dispatchRecordCanonicalUtf8,
                unknownRecordCanonicalUtf8);
            if (ClaimMode != ClaimFaultMode.TerminalAmbiguous) return retained;
            CryptographicOperations.ZeroMemory(retained);
            throw new ProviderRoutingAttemptStorageClaimException(
                ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown);
        }
    }

    private sealed class PostClaimValiditySplicingLedger(
        ProviderRoutingAttemptLedgerModule inner,
        IProviderRoutingAttemptIntegrityAnchor anchor) : IProviderRoutingOrchestrationLedger
    {
        public ProviderRoutingAttemptRecord Create(ProviderRoutingAttemptCreateInput input) =>
            inner.Create(input);

        public ProviderRoutingAttemptRecord ClaimDispatch(ProviderRoutingDispatchClaimInput input)
        {
            ProviderRoutingAttemptRecord claimed = inner.ClaimDispatch(input);
            return ForgeValidityDates(anchor, claimed);
        }

        public ProviderRoutingAttemptRecord Validate(ReadOnlyMemory<byte> canonicalUtf8) =>
            inner.Validate(canonicalUtf8);
    }

    private sealed class PostClaimCanonicalEmbeddingFailureLedger(
        ProviderRoutingAttemptLedgerModule inner) : IProviderRoutingOrchestrationLedger
    {
        private ProviderRoutingAttemptRecord? _forged;

        public ProviderRoutingAttemptRecord Create(ProviderRoutingAttemptCreateInput input) =>
            inner.Create(input);

        public ProviderRoutingAttemptRecord ClaimDispatch(ProviderRoutingDispatchClaimInput input)
        {
            ProviderRoutingAttemptRecord claimed = inner.ClaimDispatch(input);
            _forged = new ProviderRoutingAttemptRecord(
                [(byte)'{'], claimed.State, claimed.AttemptId, claimed.Sequence,
                claimed.PreviousRecordDigestSha256, claimed.CreatedAtUnixMilliseconds,
                claimed.ExpiresAtUnixMilliseconds, claimed.ComparisonArtifactDigestSha256,
                claimed.ReadinessAssessmentDigestSha256,
                claimed.ReadinessAssessmentSchemaVersion, claimed.OpenRouterReadinessCode,
                claimed.OllamaReadinessCode, claimed.IntentCode, claimed.SelectedProvider,
                claimed.RoutingDecisionDigestSha256, claimed.ClaimedAtUnixMilliseconds,
                claimed.TerminalReasonCode, claimed.IntegrityAnchorIdentitySha256,
                claimed.ClaimLimitationCodes, claimed.RecordPayloadDigestSha256,
                claimed.RecordAuthenticatorSha256);
            return _forged;
        }

        public ProviderRoutingAttemptRecord Validate(ReadOnlyMemory<byte> canonicalUtf8) =>
            _forged is not null && canonicalUtf8.Span.SequenceEqual(_forged.CanonicalUtf8.Span)
                ? _forged
                : inner.Validate(canonicalUtf8);
    }

    private sealed class ChangingMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _later;
        private int _getSpanCallCount;
        internal ChangingMemoryManager(byte[] first)
        {
            _first = first.ToArray();
            _later = first.ToArray();
            _later[^1] ^= 1;
        }
        internal int GetSpanCallCount => _getSpanCallCount;
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_first.Length);
        public override Span<byte> GetSpan() =>
            Interlocked.Increment(ref _getSpanCallCount) == 1 ? _first : _later;
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_later);
        }
    }

    private sealed class ThrowingMemoryManager : MemoryManager<byte>
    {
        private readonly int _length;
        internal ThrowingMemoryManager(int length) => _length = length;
        internal int GetSpanCallCount { get; private set; }
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_length);
        public override Span<byte> GetSpan()
        {
            GetSpanCallCount++;
            throw new InvalidOperationException("must_not_read");
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }

    private sealed class TestIntegrityAnchor : IProviderRoutingAttemptIntegrityAnchor, IDisposable
    {
        private byte[]? _key;
        internal TestIntegrityAnchor(byte value)
        {
            _key = Enumerable.Repeat(value, 32).ToArray();
            IdentitySha256 = Convert.ToHexString(SHA256.HashData(_key)).ToLowerInvariant();
        }
        public string IdentitySha256 { get; }
        public string Authenticate(ReadOnlySpan<byte> canonicalBytes) => Convert.ToHexString(
            HMACSHA256.HashData(_key ?? throw new ObjectDisposedException(nameof(TestIntegrityAnchor)),
                canonicalBytes)).ToLowerInvariant();
        public bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256)
        {
            byte[] expected = Convert.FromHexString(Authenticate(canonicalBytes));
            byte[] actual;
            try { actual = Convert.FromHexString(authenticatorSha256); }
            catch { CryptographicOperations.ZeroMemory(expected); return false; }
            try { return CryptographicOperations.FixedTimeEquals(expected, actual); }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        public void Dispose()
        {
            byte[]? key = Interlocked.Exchange(ref _key, null);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
        }
    }
}
