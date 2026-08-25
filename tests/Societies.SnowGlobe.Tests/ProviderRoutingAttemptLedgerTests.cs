using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class ProviderRoutingAttemptLedgerTests
{
    private const long Now = 1_800_000_000_000;
    private static readonly byte[] Comparison = ReadComparison();

    [Fact]
    public void InMemoryCreateAndClaimAreAuthenticatedCanonicalAndTerminal()
    {
        using TestIntegrityAnchor anchor = new(0x41);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('a')));

        ProviderRoutingAttemptRecord created = module.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal(ProviderRoutingAttemptState.NotStarted, created.State);
        Assert.Equal("99694dd77536b92b537d3f95417138b35982d7304c9c20f10f48da5c9d5c2e47",
            ProviderRoutingAttemptLedgerModule.ContractDigestSha256);
        Assert.Equal(Hex('a'), created.AttemptId);
        Assert.Equal(0, created.Sequence);
        Assert.Null(created.PreviousRecordDigestSha256);
        Assert.Equal(ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            created.ComparisonArtifactDigestSha256);
        Assert.Equal(ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion,
            created.ReadinessAssessmentSchemaVersion);
        Assert.Equal("preferred_online", created.IntentCode);
        Assert.Null(created.SelectedProvider);
        Assert.InRange(created.CanonicalUtf8.Length, 1,
            ProviderRoutingAttemptLedgerModule.MaximumRecordBytes);
        Assert.Equal(created.CanonicalDigestSha256,
            module.Validate(created.CanonicalUtf8).CanonicalDigestSha256);

        ProviderRoutingDecision decision = Decision(
            ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter);
        ProviderRoutingAttemptRecord claimed = module.ClaimDispatch(new(
            created.AttemptId,
            created.CanonicalDigestSha256,
            ProviderRoutingSelectedProvider.OpenRouter,
            decision.CanonicalUtf8,
            Now + 1));

        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted, claimed.State);
        Assert.Equal(1, claimed.Sequence);
        Assert.Equal(created.CanonicalDigestSha256, claimed.PreviousRecordDigestSha256);
        Assert.Equal(ProviderRoutingSelectedProvider.OpenRouter, claimed.SelectedProvider);
        Assert.Equal(decision.CanonicalDigestSha256, claimed.RoutingDecisionDigestSha256);
        Assert.Equal("dispatch_claimed", claimed.TerminalReasonCode);
        Assert.Equal(claimed.CanonicalDigestSha256,
            module.Inspect(claimed.AttemptId).CanonicalDigestSha256);
    }

    [Fact]
    public void DuplicateRepeatedStaleWrongBindingAndExpiryFailClosed()
    {
        using TestIntegrityAnchor anchor = new(0x42);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('b'), Hex('b'), Hex('c'), Hex('d')));
        ProviderRoutingAttemptRecord created = module.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal("attempt_already_exists", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Create(new(CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now))).Code);
        Assert.Equal("attempt_expected_record_mismatch", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, Hex('f'), ProviderRoutingSelectedProvider.OpenRouter,
                Decision(ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal("attempt_claim_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal("attempt_claim_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.OpenRouter,
                Decision(ProviderRoutingIntent.LocalOnly, ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                Now + 1))).Code);

        ProviderRoutingAttemptRecord claimed = module.ClaimDispatch(new(
            created.AttemptId, created.CanonicalDigestSha256, ProviderRoutingSelectedProvider.OpenRouter,
            Decision(ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
            Now + 1));
        Assert.Equal("attempt_already_terminal", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, claimed.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.OpenRouter,
                Decision(ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                Now + 2))).Code);

        ProviderRoutingAttemptRecord expiring = module.Create(new(
            CurrentAssessment(Now + 10), ProviderRoutingIntent.LocalOnly, Now + 10));
        Assert.Equal("attempt_expired", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(expiring.AttemptId, expiring.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.LocalOnly, ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                expiring.ExpiresAtUnixMilliseconds + 1))).Code);

        Assert.Equal("attempt_assessment_expired", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Create(new(CurrentAssessment(Now + 20), ProviderRoutingIntent.LocalOnly,
                Now + 20 + ProviderReadinessObservationModule.ObservationLifetimeMilliseconds + 1))).Code);
    }

    [Fact]
    public async Task ClaimRequiresExactAssessmentAndDecisionReadinessCoherence()
    {
        using TestIntegrityAnchor anchor = new(0x57);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('1'), Hex('2'), Hex('3'), Hex('4')));

        ProviderRoutingAttemptRecord unknown = module.Create(new(
            UnknownAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("attempt_claim_binding_invalid",
            Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                unknown.AttemptId, unknown.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.OpenRouter,
                Decision(ProviderRoutingIntent.PreferredOnline,
                    ProviderReadiness.Ready, ProviderReadiness.Ready).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            module.Inspect(unknown.AttemptId).State);

        ReadOnlyMemory<byte> readyNotReadyAssessment = await CurrentAssessment(
            Now, ProviderReadiness.Ready, ProviderReadiness.NotReady);
        ProviderRoutingAttemptRecord swapped = module.Create(new(
            readyNotReadyAssessment, ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("attempt_claim_binding_invalid",
            Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                swapped.AttemptId, swapped.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.PreferredOnline,
                    ProviderReadiness.NotReady, ProviderReadiness.Ready).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            module.Inspect(swapped.AttemptId).State);

        ReadOnlyMemory<byte> readyAssessment = await CurrentAssessment(
            Now, ProviderReadiness.Ready, ProviderReadiness.Ready);
        ProviderRoutingAttemptRecord online = module.Create(new(
            readyAssessment, ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("ready", online.OpenRouterReadinessCode);
        Assert.Equal("ready", online.OllamaReadinessCode);
        ProviderRoutingAttemptRecord validatedOnline = module.Validate(online.CanonicalUtf8);
        Assert.Equal(online.OpenRouterReadinessCode, validatedOnline.OpenRouterReadinessCode);
        Assert.Equal(online.OllamaReadinessCode, validatedOnline.OllamaReadinessCode);
        ProviderRoutingAttemptRecord onlineClaim = module.ClaimDispatch(new(
            online.AttemptId, online.CanonicalDigestSha256,
            ProviderRoutingSelectedProvider.OpenRouter,
            Decision(ProviderRoutingIntent.PreferredOnline,
                ProviderReadiness.Ready, ProviderReadiness.Ready).CanonicalUtf8,
            Now + 1));
        Assert.Equal(online.OpenRouterReadinessCode, onlineClaim.OpenRouterReadinessCode);
        Assert.Equal(online.OllamaReadinessCode, onlineClaim.OllamaReadinessCode);

        ReadOnlyMemory<byte> fallbackAssessment = await CurrentAssessment(
            Now, ProviderReadiness.NotReady, ProviderReadiness.Ready);
        ProviderRoutingAttemptRecord fallback = module.Create(new(
            fallbackAssessment, ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal("not_ready", fallback.OpenRouterReadinessCode);
        Assert.Equal("ready", fallback.OllamaReadinessCode);
        ProviderRoutingAttemptRecord fallbackClaim = module.ClaimDispatch(new(
            fallback.AttemptId, fallback.CanonicalDigestSha256,
            ProviderRoutingSelectedProvider.Ollama,
            Decision(ProviderRoutingIntent.PreferredOnline,
                ProviderReadiness.NotReady, ProviderReadiness.Ready).CanonicalUtf8,
            Now + 1));
        Assert.Equal(fallback.OpenRouterReadinessCode, fallbackClaim.OpenRouterReadinessCode);
        Assert.Equal(fallback.OllamaReadinessCode, fallbackClaim.OllamaReadinessCode);
    }

    [Fact]
    public async Task ConcurrentClaimsHaveOneWinnerAndNeverReturnToNotStarted()
    {
        using TestIntegrityAnchor anchor = new(0x43);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('c')));
        ProviderRoutingAttemptRecord created = module.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));
        ProviderRoutingDecision decision = Decision(
            ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter);
        using ManualResetEventSlim start = new(false);
        int successes = 0;
        string[] errors = new string[2];

        Task[] claims = Enumerable.Range(0, 2).Select(index => Task.Run(() =>
        {
            start.Wait();
            try
            {
                _ = module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                    ProviderRoutingSelectedProvider.OpenRouter, decision.CanonicalUtf8, Now + 1));
                Interlocked.Increment(ref successes);
            }
            catch (ProviderRoutingAttemptLedgerException exception) { errors[index] = exception.Code; }
        })).ToArray();
        start.Set();
        await Task.WhenAll(claims);

        Assert.Equal(1, successes);
        Assert.Contains("attempt_already_terminal", errors);
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            module.Inspect(created.AttemptId).State);
    }

    [Fact]
    public void CallerMemoryIsReadOnceAndResultsAreDetachedAndRepeatable()
    {
        byte[] assessment = CurrentAssessment(Now).ToArray();
        byte[] decision = Decision(
            ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8.ToArray();
        using ChangingMemoryManager assessmentMemory = new(assessment);
        using ChangingMemoryManager decisionMemory = new(decision);
        using TestIntegrityAnchor anchor = new(0x44);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('d')));

        ProviderRoutingAttemptRecord created = module.Create(new(
            assessmentMemory.CreateReadOnlyMemory(), ProviderRoutingIntent.PreferredOnline, Now));
        ProviderRoutingAttemptRecord claimed = module.ClaimDispatch(new(
            created.AttemptId, created.CanonicalDigestSha256, ProviderRoutingSelectedProvider.OpenRouter,
            decisionMemory.CreateReadOnlyMemory(), Now + 1));

        Assert.Equal(1, assessmentMemory.GetSpanCallCount);
        Assert.Equal(1, decisionMemory.GetSpanCallCount);
        ReadOnlyMemory<byte> detached = claimed.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(detached, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', claimed.CanonicalUtf8.Span[0]);

        using TestIntegrityAnchor anchor2 = new(0x44);
        ProviderRoutingAttemptLedgerModule second = new(
            new InMemoryProviderRoutingAttemptLedgerStorage(anchor2), anchor2,
            new SequenceAttemptIdSource(Hex('d')));
        ProviderRoutingAttemptRecord repeated = second.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));
        Assert.Equal(created.CanonicalJson, repeated.CanonicalJson);
        ProviderRoutingAttemptRecord repeatedClaim = second.ClaimDispatch(new(
            repeated.AttemptId, repeated.CanonicalDigestSha256,
            ProviderRoutingSelectedProvider.OpenRouter,
            Decision(ProviderRoutingIntent.PreferredOnline,
                ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
            Now + 1));
        Assert.Equal(claimed.CanonicalJson, repeatedClaim.CanonicalJson);
    }

    [Fact]
    public void InvalidAssessmentsDecisionsEnumsAndMissingAttemptsFailClosed()
    {
        using TestIntegrityAnchor anchor = new(0x53);
        ProviderRoutingAttemptLedgerModule module = new(
            new InMemoryProviderRoutingAttemptLedgerStorage(anchor), anchor,
            new SequenceAttemptIdSource(Hex('d')));
        Assert.Equal("attempt_assessment_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Create(new("{bad"u8.ToArray(), ProviderRoutingIntent.LocalOnly, Now))).Code);
        Assert.Equal("attempt_assessment_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Create(new(new byte[ProviderRoutingReadinessEvidenceModule.MaximumCurrentAssessmentBytes + 1],
                ProviderRoutingIntent.LocalOnly, Now))).Code);
        Assert.Equal("attempt_input_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Create(new(CurrentAssessment(Now), (ProviderRoutingIntent)999, Now))).Code);

        ProviderRoutingAttemptRecord created = module.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now));
        Assert.Equal("attempt_claim_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                (ProviderRoutingSelectedProvider)999, "{bad"u8.ToArray(), Now + 1))).Code);
        Assert.Equal("attempt_claim_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama, "{bad"u8.ToArray(), Now + 1))).Code);
        Assert.Equal("attempt_missing", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.ClaimDispatch(new(Hex('e'), created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.LocalOnly,
                    ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal("attempt_missing", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Inspect(Hex('e'))).Code);
        Assert.Equal("attempt_id_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Inspect("../missing")).Code);
    }

    [Fact]
    public void UnexpectedStorageFailuresRemainClosedAndNeverEchoDynamicDetails()
    {
        using TestIntegrityAnchor anchor = new(0x54);
        ExplodingStorage createFailure = new(anchor) { FailCreate = true };
        ProviderRoutingAttemptLedgerModule createModule = new(createFailure, anchor,
            new SequenceAttemptIdSource(Hex('e')));
        ProviderRoutingAttemptLedgerException create =
            Assert.Throws<ProviderRoutingAttemptLedgerException>(() => createModule.Create(new(
                CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now)));
        Assert.Equal("attempt_storage_unavailable", create.Code);
        Assert.DoesNotContain("HOST_SECRET_PATH", create.Message, StringComparison.Ordinal);

        ExplodingStorage claimFailure = new(anchor);
        ProviderRoutingAttemptLedgerModule claimModule = new(claimFailure, anchor,
            new SequenceAttemptIdSource(Hex('f')));
        ProviderRoutingAttemptRecord created = claimModule.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now));
        claimFailure.FailClaim = true;
        ProviderRoutingAttemptLedgerException claim =
            Assert.Throws<ProviderRoutingAttemptLedgerException>(() => claimModule.ClaimDispatch(new(
                created.AttemptId, created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.LocalOnly,
                    ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                Now + 1)));
        Assert.Equal("attempt_storage_unavailable", claim.Code);
        Assert.DoesNotContain("HOST_SECRET_PATH", claim.Message, StringComparison.Ordinal);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            claimModule.Inspect(created.AttemptId).State);
        claimFailure.FailClaim = false;
        Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
            claimModule.ClaimDispatch(new(
                created.AttemptId, created.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.LocalOnly,
                    ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                Now + 2)).State);

        ExplodingStorage seamViolation = new(anchor)
        {
            FailClaim = true,
            ClassifyClaimFailures = false
        };
        ProviderRoutingAttemptLedgerModule violationModule = new(seamViolation, anchor,
            new SequenceAttemptIdSource(Hex('0')));
        ProviderRoutingAttemptRecord violationCreated = violationModule.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now));
        Assert.Equal("attempt_ledger_failed",
            Assert.Throws<ProviderRoutingAttemptLedgerException>(() => violationModule.ClaimDispatch(new(
                violationCreated.AttemptId, violationCreated.CanonicalDigestSha256,
                ProviderRoutingSelectedProvider.Ollama,
                Decision(ProviderRoutingIntent.LocalOnly,
                    ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                Now + 1))).Code);
        Assert.Equal(ProviderRoutingAttemptState.NotStarted,
            violationModule.Inspect(violationCreated.AttemptId).State);
    }

    [Fact]
    public void CanonicalValidationRejectsMalformedDuplicateDeepOversizedAndTamperedRecords()
    {
        using TestIntegrityAnchor anchor = new(0x45);
        InMemoryProviderRoutingAttemptLedgerStorage storage = new(anchor);
        ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
            new SequenceAttemptIdSource(Hex('e')));
        ProviderRoutingAttemptRecord created = module.Create(new(
            CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));

        Assert.Equal("attempt_record_json_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate("{bad"u8.ToArray())).Code);
        byte[] duplicate = Encoding.UTF8.GetBytes(created.CanonicalJson.Replace(
            "{\"schema_version\"", "{\"schema_version\":\"duplicate\",\"schema_version\"",
            StringComparison.Ordinal));
        Assert.Equal("attempt_record_shape_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(duplicate)).Code);
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 20) + new string(']', 20));
        Assert.Equal("attempt_record_json_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(deep)).Code);
        Assert.Equal("attempt_record_size_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(new byte[ProviderRoutingAttemptLedgerModule.MaximumRecordBytes + 1])).Code);

        byte[] changed = created.CanonicalUtf8.ToArray();
        changed[^2] ^= 1;
        Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.Validate(changed));
        byte[] truncated = created.CanonicalUtf8[..^1].ToArray();
        Assert.Equal("attempt_record_json_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(truncated)).Code);
        byte[] rebound = ForgeRecord(created, root => root["comparison_artifact_digest_sha256"] = Hex('9'));
        Assert.Equal("attempt_record_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(rebound)).Code);
        byte[] relabeledReadiness = ForgeRecord(created,
            root => root["openrouter_readiness"] = "not_ready");
        Assert.Equal("attempt_record_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            module.Validate(relabeledReadiness)).Code);

        using TestIntegrityAnchor wrongAnchor = new(0x46);
        ProviderRoutingAttemptLedgerModule wrong = new(
            new InMemoryProviderRoutingAttemptLedgerStorage(wrongAnchor), wrongAnchor,
            new SequenceAttemptIdSource(Hex('f')));
        Assert.Equal("attempt_record_binding_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
            wrong.Validate(created.CanonicalUtf8)).Code);
    }

    [Fact]
    public void PublicSurfaceIsEvidenceOnlyRawFreeAndHasNoExecutionOrWorldControls()
    {
        Type module = typeof(ProviderRoutingAttemptLedgerModule);
        Assert.Empty(module.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(new[] { "ClaimDispatch", "Create", "Inspect", "Validate" }, module.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        string publicSurface = string.Join('|', module.Assembly.GetExportedTypes()
            .Where(type => type.Name.Contains("ProviderRoutingAttempt", StringComparison.Ordinal))
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(member => member.Name));
        foreach (string forbidden in new[]
        {
            "Adapter", "Transport", "Credential", "Endpoint", "RequestBody", "Execute", "Generate",
            "Payment", "Retry", "Fallback", "World", "File", "Path", "Root", "Task"
        }) Assert.DoesNotContain(forbidden, publicSurface, StringComparison.OrdinalIgnoreCase);

        using TestIntegrityAnchor anchor = new(0x47);
        ProviderRoutingAttemptLedgerModule instance = new(
            new InMemoryProviderRoutingAttemptLedgerStorage(anchor), anchor,
            new SequenceAttemptIdSource(Hex('1')));
        string json = instance.Create(new(CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now)).CanonicalJson;
        foreach (string forbidden in new[]
        {
            "Bearer ", "credential", "account_id", "raw_response", "prompt", "proposal", "reasoning",
            "request_body", "response_body", "host_path", "process_id", "dynamic_error"
        }) Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowsFileAdapterSurvivesRestartRefusesSecondLeaseAndIsAppendOnly()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x48);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage first = new(root, anchor))
            {
                ProviderRoutingAttemptLedgerModule module = new(first, anchor,
                    new SequenceAttemptIdSource(Hex('2')));
                created = module.Create(new(CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));
                Assert.ThrowsAny<Exception>(() => new FileProviderRoutingAttemptLedgerStorage(root, anchor));
            }

            using FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor);
            ProviderRoutingAttemptLedgerModule restarted = new(reopened, anchor,
                new SequenceAttemptIdSource(Hex('3')));
            Assert.Equal(ProviderRoutingAttemptState.NotStarted,
                restarted.Inspect(created.AttemptId).State);
            ProviderRoutingAttemptRecord claimed = restarted.ClaimDispatch(new(
                created.AttemptId, created.CanonicalDigestSha256, ProviderRoutingSelectedProvider.OpenRouter,
                Decision(ProviderRoutingIntent.PreferredOnline, ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                Now + 1));
            Assert.Equal(ProviderRoutingAttemptState.DispatchStarted, claimed.State);
            Assert.Equal(claimed.CanonicalDigestSha256,
                restarted.Inspect(created.AttemptId).CanonicalDigestSha256);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(root, "attempts", created.AttemptId)).Length);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.DispatchRecordWriteBoundary, true)]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.DispatchRecordFlushBoundary, false)]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.DispatchRecordReadbackBoundary, false)]
    public void AmbiguousDispatchPersistenceRecoversAsTerminalUnknown(string boundary, bool partial)
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x49);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor,
                new BoundaryFault(boundary, partial)))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('4')));
                created = module.Create(new(CurrentAssessment(Now), ProviderRoutingIntent.PreferredOnline, Now));
                Assert.Equal("attempt_claim_outcome_ambiguous",
                    Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                        created.AttemptId, created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 1))).Code);
            }

            using FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor);
            ProviderRoutingAttemptLedgerModule restarted = new(reopened, anchor,
                new SequenceAttemptIdSource(Hex('5')));
            ProviderRoutingAttemptRecord current = restarted.Inspect(created.AttemptId);
            Assert.NotEqual(ProviderRoutingAttemptState.NotStarted, current.State);
            Assert.Contains(current.State,
                new[] { ProviderRoutingAttemptState.DispatchStarted, ProviderRoutingAttemptState.SubmissionUnknown });
            Assert.Equal("attempt_already_terminal", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                restarted.ClaimDispatch(new(created.AttemptId, current.CanonicalDigestSha256,
                    ProviderRoutingSelectedProvider.OpenRouter,
                    Decision(ProviderRoutingIntent.PreferredOnline,
                        ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                    Now + 2))).Code);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.ClaimTombstoneWriteBoundary, true, "attempt_poisoned")]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.ClaimTombstoneWriteBoundary, false, "attempt_claim_outcome_ambiguous")]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.ClaimTombstoneFlushBoundary, false, "attempt_claim_outcome_ambiguous")]
    [InlineData(FileProviderRoutingAttemptLedgerStorage.ClaimTombstoneReadbackBoundary, false, "attempt_claim_outcome_ambiguous")]
    public void AmbiguousTombstonePersistenceNeverRecoversNotStarted(
        string boundary,
        bool partial,
        string expectedCode)
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x51);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor,
                new BoundaryFault(boundary, partial)))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('7')));
                created = module.Create(new(CurrentAssessment(Now),
                    ProviderRoutingIntent.PreferredOnline, Now));
                Assert.Equal(expectedCode, Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                    module.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 1))).Code);
            }

            using FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor);
            ProviderRoutingAttemptLedgerModule restarted = new(reopened, anchor,
                new SequenceAttemptIdSource(Hex('8')));
            if (partial)
                Assert.Equal("attempt_poisoned", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                    restarted.Inspect(created.AttemptId)).Code);
            else
                Assert.Equal(ProviderRoutingAttemptState.SubmissionUnknown,
                    restarted.Inspect(created.AttemptId).State);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void AuthenticatedAbaAndPreviousDigestBreaksFailClosedAcrossRestart()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x52);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('9')));
                created = module.Create(new(CurrentAssessment(Now),
                    ProviderRoutingIntent.PreferredOnline, Now));
            }

            ProviderRoutingAttemptRecord aba = ProviderRoutingAttemptLedgerCodec.CreateRecord(
                anchor, ProviderRoutingAttemptState.NotStarted, created.AttemptId, 0, null,
                created.CreatedAtUnixMilliseconds + 1, created.ExpiresAtUnixMilliseconds,
                created.ComparisonArtifactDigestSha256, Hex('a'),
                created.ReadinessAssessmentSchemaVersion, created.OpenRouterReadinessCode,
                created.OllamaReadinessCode, created.IntentCode, null, null, null, "none");
            string initialPath = Path.Combine(root, "attempts", created.AttemptId, "record-00000000.json");
            File.WriteAllBytes(initialPath, aba.CanonicalUtf8.ToArray());
            using (FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor))
            {
                ProviderRoutingAttemptLedgerModule module = new(reopened, anchor,
                    new SequenceAttemptIdSource(Hex('a')));
                Assert.Equal("attempt_expected_record_mismatch",
                    Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                        created.AttemptId, created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 2))).Code);
            }

            string secondRoot = NewRoot();
            try
            {
                ProviderRoutingAttemptRecord initial;
                ProviderRoutingAttemptRecord claimed;
                using (FileProviderRoutingAttemptLedgerStorage storage = new(secondRoot, anchor))
                {
                    ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                        new SequenceAttemptIdSource(Hex('b')));
                    initial = module.Create(new(CurrentAssessment(Now),
                        ProviderRoutingIntent.PreferredOnline, Now));
                    claimed = module.ClaimDispatch(new(initial.AttemptId, initial.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 1));
                }
                ProviderRoutingAttemptRecord broken = ProviderRoutingAttemptLedgerCodec.CreateRecord(
                    anchor, ProviderRoutingAttemptState.DispatchStarted, initial.AttemptId, 1,
                    Hex('f'), initial.CreatedAtUnixMilliseconds, initial.ExpiresAtUnixMilliseconds,
                    initial.ComparisonArtifactDigestSha256, initial.ReadinessAssessmentDigestSha256,
                    initial.ReadinessAssessmentSchemaVersion, initial.OpenRouterReadinessCode,
                    initial.OllamaReadinessCode, initial.IntentCode,
                    claimed.SelectedProvider, claimed.RoutingDecisionDigestSha256,
                    claimed.ClaimedAtUnixMilliseconds, "dispatch_claimed");
                File.WriteAllBytes(Path.Combine(secondRoot, "attempts", initial.AttemptId,
                    "record-00000001-dispatch-started.json"), broken.CanonicalUtf8.ToArray());
                using FileProviderRoutingAttemptLedgerStorage restarted = new(secondRoot, anchor);
                ProviderRoutingAttemptRecord recovered = new ProviderRoutingAttemptLedgerModule(
                    restarted, anchor, new SequenceAttemptIdSource(Hex('c')))
                    .Inspect(initial.AttemptId);
                Assert.Equal(ProviderRoutingAttemptState.SubmissionUnknown, recovered.State);
                Assert.Equal(initial.CanonicalDigestSha256, recovered.PreviousRecordDigestSha256);
            }
            finally { Delete(secondRoot); }
        }
        finally { Delete(root); }
    }

    [Fact]
    public void WindowsFileAdapterRejectsTraversalHardlinksAndReparseRootsWhereAvailable()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x50);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('6')));
                created = module.Create(new(CurrentAssessment(Now), ProviderRoutingIntent.LocalOnly, Now));
                Assert.Equal("attempt_id_invalid", Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                    module.ClaimDispatch(new("..\\escape", created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.Ollama,
                        Decision(ProviderRoutingIntent.LocalOnly, ProviderRoutingSelectedProvider.Ollama).CanonicalUtf8,
                        Now + 1))).Code);
            }

            string initial = Path.Combine(root, "attempts", created.AttemptId, "record-00000000.json");
            string extra = Path.Combine(root, "attempts", created.AttemptId, "hardlink.json");
            Assert.True(CreateHardLink(extra, initial, IntPtr.Zero));
            using (FileProviderRoutingAttemptLedgerStorage hardened = new(root, anchor))
            {
                ProviderRoutingAttemptLedgerModule module = new(hardened, anchor,
                    new SequenceAttemptIdSource(Hex('d')));
                Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                    module.Inspect(created.AttemptId));
            }

            string target = NewRoot();
            string link = Path.Combine(Path.GetDirectoryName(target)!, Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateSymbolicLink(link, target);
                Assert.ThrowsAny<Exception>(() => new FileProviderRoutingAttemptLedgerStorage(link, anchor));
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            finally
            {
                if (Directory.Exists(link)) Directory.Delete(link);
                Delete(target);
            }
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitePreTombstonePreparationAndLeaseFailuresRemainRetryable(
        bool beforePreparation)
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x55);
            ProviderRoutingAttemptRecord created;
            IProviderRoutingAttemptLedgerStorageFault fault = beforePreparation
                ? new PreClaimPreparationFault()
                : new PreTombstoneLeaseFault();
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor, fault))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('e')));
                created = module.Create(new(CurrentAssessment(Now),
                    ProviderRoutingIntent.PreferredOnline, Now));
                Assert.Equal("attempt_storage_unavailable",
                    Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                        created.AttemptId, created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 1))).Code);
                Assert.Equal(ProviderRoutingAttemptState.NotStarted,
                    module.Inspect(created.AttemptId).State);
            }

            using FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor);
            ProviderRoutingAttemptLedgerModule restarted = new(reopened, anchor,
                new SequenceAttemptIdSource(Hex('f')));
            Assert.Equal(ProviderRoutingAttemptState.NotStarted,
                restarted.Inspect(created.AttemptId).State);
            Assert.Equal(ProviderRoutingAttemptState.DispatchStarted,
                restarted.ClaimDispatch(new(created.AttemptId, created.CanonicalDigestSha256,
                    ProviderRoutingSelectedProvider.OpenRouter,
                    Decision(ProviderRoutingIntent.PreferredOnline,
                        ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                    Now + 2)).State);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void RecoveryWriteFailureIsPoisonedUntilAHealthyRestartRecoversUnknown()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            using TestIntegrityAnchor anchor = new(0x56);
            ProviderRoutingAttemptRecord created;
            using (FileProviderRoutingAttemptLedgerStorage storage = new(root, anchor,
                new DispatchPartialAndRecoveryFault()))
            {
                ProviderRoutingAttemptLedgerModule module = new(storage, anchor,
                    new SequenceAttemptIdSource(Hex('1')));
                created = module.Create(new(CurrentAssessment(Now),
                    ProviderRoutingIntent.PreferredOnline, Now));
                Assert.Equal("attempt_poisoned",
                    Assert.Throws<ProviderRoutingAttemptLedgerException>(() => module.ClaimDispatch(new(
                        created.AttemptId, created.CanonicalDigestSha256,
                        ProviderRoutingSelectedProvider.OpenRouter,
                        Decision(ProviderRoutingIntent.PreferredOnline,
                            ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                        Now + 1))).Code);
                Assert.Equal("attempt_poisoned",
                    Assert.Throws<ProviderRoutingAttemptLedgerException>(() =>
                        module.Inspect(created.AttemptId)).Code);
            }

            using FileProviderRoutingAttemptLedgerStorage reopened = new(root, anchor);
            ProviderRoutingAttemptLedgerModule restarted = new(reopened, anchor,
                new SequenceAttemptIdSource(Hex('2')));
            Assert.Equal(ProviderRoutingAttemptState.SubmissionUnknown,
                restarted.Inspect(created.AttemptId).State);
            Assert.Equal("attempt_already_terminal",
                Assert.Throws<ProviderRoutingAttemptLedgerException>(() => restarted.ClaimDispatch(new(
                    created.AttemptId, created.CanonicalDigestSha256,
                    ProviderRoutingSelectedProvider.OpenRouter,
                    Decision(ProviderRoutingIntent.PreferredOnline,
                        ProviderRoutingSelectedProvider.OpenRouter).CanonicalUtf8,
                    Now + 2))).Code);
        }
        finally { Delete(root); }
    }

    private static ReadOnlyMemory<byte> UnknownAssessment(long now) =>
        ProviderRoutingReadinessEvidenceModule.AssessCurrent(
            new ProviderRoutingReadinessEvidenceInput(Comparison), null, null, now).CanonicalUtf8;

    private static ReadOnlyMemory<byte> CurrentAssessment(long now) =>
        CurrentAssessment(now, ProviderReadiness.Ready, ProviderReadiness.Ready)
            .GetAwaiter().GetResult();

    private static ProviderRoutingDecision Decision(
        ProviderRoutingIntent intent,
        ProviderRoutingSelectedProvider provider)
    {
        ProviderRoutingPolicyInput input = provider switch
        {
            ProviderRoutingSelectedProvider.OpenRouter => new(intent,
                ProviderReadiness.Ready, ProviderReadiness.Ready,
                ProviderPrimaryAttemptState.NotStarted),
            ProviderRoutingSelectedProvider.Ollama => new(intent,
                ProviderReadiness.Ready, ProviderReadiness.Ready,
                ProviderPrimaryAttemptState.NotStarted),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        ProviderRoutingDecision decision = ProviderRoutingPolicyModule.Decide(input, Comparison);
        Assert.Equal(provider, decision.SelectedProvider);
        return decision;
    }

    private static ProviderRoutingDecision Decision(
        ProviderRoutingIntent intent,
        ProviderReadiness openRouterReadiness,
        ProviderReadiness ollamaReadiness) => ProviderRoutingPolicyModule.Decide(
            new ProviderRoutingPolicyInput(intent, openRouterReadiness, ollamaReadiness,
                ProviderPrimaryAttemptState.NotStarted), Comparison);

    private static async ValueTask<ReadOnlyMemory<byte>> CurrentAssessment(
        long now,
        ProviderReadiness openRouterReadiness,
        ProviderReadiness ollamaReadiness)
    {
        ProviderReadinessObservation openRouter = await Observation(
            ProviderReadinessProvider.OpenRouter, openRouterReadiness, now);
        ProviderReadinessObservation ollama = await Observation(
            ProviderReadinessProvider.Ollama, ollamaReadiness, now);
        return ProviderRoutingReadinessEvidenceModule.AssessCurrent(
            new ProviderRoutingReadinessEvidenceInput(Comparison),
            openRouter.CanonicalUtf8, ollama.CanonicalUtf8, now).CanonicalUtf8;
    }

    private static ValueTask<ProviderReadinessObservation> Observation(
        ProviderReadinessProvider provider,
        ProviderReadiness readiness,
        long now)
    {
        ProviderReadinessAdapterResult result = (provider, readiness) switch
        {
            (ProviderReadinessProvider.OpenRouter, ProviderReadiness.Ready) =>
                ProviderReadinessAdapterResult.Ready(3,
                    OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                    "same_account_bound"),
            (ProviderReadinessProvider.OpenRouter, ProviderReadiness.NotReady) =>
                ProviderReadinessAdapterResult.Unavailable("credential_unavailable", 0,
                    OpenRouterAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OpenRouterAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                    "not_performed"),
            (ProviderReadinessProvider.Ollama, ProviderReadiness.Ready) =>
                ProviderReadinessAdapterResult.Ready(1,
                    OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                    "not_applicable"),
            (ProviderReadinessProvider.Ollama, ProviderReadiness.NotReady) =>
                ProviderReadinessAdapterResult.Unavailable("model_metadata_rejected", 1,
                    OllamaAuthenticatedReadinessAdapter.SourceSchemaVersion,
                    OllamaAuthenticatedReadinessAdapter.SourceContractDigestSha256,
                    "not_applicable"),
            _ => throw new ArgumentOutOfRangeException(nameof(readiness))
        };
        return ProviderReadinessObservationModule.ObserveAsync(
            new ReadinessAdapter(provider, result), new ReadinessClock(now), CancellationToken.None);
    }

    private static byte[] ForgeRecord(ProviderRoutingAttemptRecord record, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(record.CanonicalJson)!.AsObject();
        mutation(root);
        root.Remove("record_payload_digest_sha256");
        root.Remove("record_authenticator_sha256");
        byte[] payload = Encoding.UTF8.GetBytes(root.ToJsonString());
        root["record_payload_digest_sha256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(payload);
        root["record_authenticator_sha256"] = record.RecordAuthenticatorSha256;
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] ReadComparison()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string path = Path.Combine(current.FullName, "artifacts", "snowglobe", "cognition-quality",
                "provider-comparison-v1.json");
            if (File.Exists(path)) return File.ReadAllBytes(path);
            current = current.Parent;
        }
        throw new FileNotFoundException("Accepted comparison fixture not found.");
    }

    private static string Hex(char value) => new(value, 64);
    private static string NewRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "snow-globe-routing-attempt-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class SequenceAttemptIdSource(params string[] values) : IProviderRoutingAttemptIdSource
    {
        private readonly Queue<string> _values = new(values);
        public string NextAttemptId() => _values.Dequeue();
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

    private sealed class TestIntegrityAnchor : IProviderRoutingAttemptIntegrityAnchor, IDisposable
    {
        private byte[]? _key;
        internal TestIntegrityAnchor(byte value)
        {
            _key = Enumerable.Repeat(value, 32).ToArray();
            IdentitySha256 = Digest(_key);
        }
        public string IdentitySha256 { get; }
        public string Authenticate(ReadOnlySpan<byte> canonicalBytes)
        {
            byte[] key = _key ?? throw new ObjectDisposedException(nameof(TestIntegrityAnchor));
            return Convert.ToHexString(HMACSHA256.HashData(key, canonicalBytes)).ToLowerInvariant();
        }
        public bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256)
        {
            if (authenticatorSha256 is not { Length: 64 }) return false;
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

    private sealed class BoundaryFault(string boundary, bool partial)
        : IProviderRoutingAttemptLedgerStorageFault
    {
        private int _fired;
        public int BytesToWrite(string currentBoundary, int totalBytes)
        {
            if (partial && currentBoundary == boundary && Interlocked.Exchange(ref _fired, 1) == 0)
                return Math.Max(1, totalBytes / 2);
            return totalBytes;
        }
        public void AfterWriteBeforeFlush(string currentBoundary)
        {
            if (!partial && currentBoundary == boundary &&
                boundary.EndsWith("after_write_before_flush", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref _fired, 1) == 0) throw new IOException("injected");
        }
        public void AfterFlushBeforeReadback(string currentBoundary)
        {
            if (!partial && currentBoundary == boundary &&
                boundary.EndsWith("after_flush_before_readback", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref _fired, 1) == 0) throw new IOException("injected");
        }
        public void BeforeReadback(string currentBoundary)
        {
            if (!partial && currentBoundary == boundary &&
                boundary.EndsWith("before_readback", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref _fired, 1) == 0) throw new IOException("injected");
        }
    }

    private sealed class PreTombstoneLeaseFault : IProviderRoutingAttemptLedgerStorageFault
    {
        private int _fired;
        public void BeforeClaimTombstone()
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0)
                throw new ObjectDisposedException("injected-lease");
        }
    }

    private sealed class PreClaimPreparationFault : IProviderRoutingAttemptLedgerStorageFault
    {
        private int _fired;
        public void BeforeClaimPreparation()
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0)
                throw new IOException("injected-read-identity");
        }
    }

    private sealed class DispatchPartialAndRecoveryFault : IProviderRoutingAttemptLedgerStorageFault
    {
        private int _partial;
        public int BytesToWrite(string boundary, int totalBytes)
        {
            if (boundary == FileProviderRoutingAttemptLedgerStorage.DispatchRecordWriteBoundary
                && Interlocked.Exchange(ref _partial, 1) == 0)
                return Math.Max(1, totalBytes / 2);
            return totalBytes;
        }
        public void BeforeRecoveryUnknownWrite() => throw new IOException("injected-recovery");
    }

    private sealed class ExplodingStorage : IProviderRoutingAttemptLedgerStorage
    {
        private readonly InMemoryProviderRoutingAttemptLedgerStorage _inner;
        internal ExplodingStorage(IProviderRoutingAttemptIntegrityAnchor anchor)
        {
            _inner = new(anchor);
            IntegrityAnchorIdentitySha256 = anchor.IdentitySha256;
        }
        internal bool FailCreate { get; set; }
        internal bool FailClaim { get; set; }
        internal bool ClassifyClaimFailures { get; set; } = true;
        public string IntegrityAnchorIdentitySha256 { get; }
        public void CreateNew(string attemptId, ReadOnlySpan<byte> initialRecordCanonicalUtf8)
        {
            if (FailCreate) throw new InvalidOperationException("HOST_SECRET_PATH");
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
            if (FailClaim)
            {
                if (ClassifyClaimFailures)
                    throw new ProviderRoutingAttemptStorageClaimException(
                        ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone);
                throw new InvalidOperationException("HOST_SECRET_PATH");
            }
            return _inner.ClaimOnce(attemptId, expectedRecordDigestSha256,
                tombstoneCanonicalUtf8, dispatchRecordCanonicalUtf8, unknownRecordCanonicalUtf8);
        }
    }

    private sealed class ChangingMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _second;
        internal ChangingMemoryManager(byte[] first)
        {
            _first = first.ToArray();
            _second = first.ToArray();
            _second[^1] ^= 1;
        }
        internal int GetSpanCallCount { get; private set; }
        internal ReadOnlyMemory<byte> CreateReadOnlyMemory() => CreateMemory(_first.Length);
        public override Span<byte> GetSpan()
        {
            GetSpanCallCount++;
            return GetSpanCallCount == 1 ? _first : _second;
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_second);
        }
    }

    private static string Digest(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFileName, string existingFileName,
        IntPtr securityAttributes);
}
