using System.Text;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumEvidenceTests
{
    private static int _contextCounter;
    [Fact]
    public async Task AuthorizeIsDetachedAndZeroIoThenCapabilityRunsExactlyTwelveSequentialCalls()
    {
        TestContext context = CreateContext();
        OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(context.Authorization);

        Assert.Equal(0, context.Exchange.CallCount);
        Assert.Equal(0, context.Leases.CallCount);
        Assert.Empty(context.Journal.Snapshot().Slots);

        OpenRouterPremiumEvidenceArtifact artifact = await OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            capability, context.Exchange, context.Leases, context.Journal, context.Clock, CancellationToken.None);

        Assert.Equal("complete", artifact.Status);
        Assert.Equal(12, artifact.ExchangeCount);
        Assert.Equal(12, context.Exchange.CallCount);
        Assert.Equal(1, context.Exchange.MaximumConcurrentCalls);
        Assert.Equal(12, context.Leases.CallCount);
        Assert.Equal(12, context.Leases.ZeroObservationCount);
        Assert.True(context.Leases.LastLeaseZeroed);
        Assert.Equal(Enumerable.Range(1, 12).Select(index => $"cq{index}"), artifact.Slots.Select(slot => slot.ScenarioId));
        Assert.All(artifact.Slots, slot =>
        {
            Assert.Equal(SubmissionState.ResponseReceived, slot.SubmissionState);
            Assert.Equal(ChargeState.Settled, slot.ChargeState);
            Assert.DoesNotContain("agent-00", slot.ResponseDigestSha256, StringComparison.Ordinal);
        });
        Assert.DoesNotContain("offline scripted terminal", artifact.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Propose one legal action", artifact.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", artifact.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"\"model_identity\":\"{OpenRouterPremiumProfile.CanonicalModelSlug}\"", artifact.CanonicalJson, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllSlots))]
    public async Task TerminalFailureStopsAtExactSlotAndNeverIssuesThirteenth(int failingSlot)
    {
        TestContext context = CreateContext(failingSlot);
        OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(context.Authorization);

        OpenRouterPremiumEvidenceArtifact artifact = await OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            capability, context.Exchange, context.Leases, context.Journal, context.Clock, CancellationToken.None);

        Assert.Equal("terminal", artifact.Status);
        Assert.Equal(failingSlot, artifact.ExchangeCount);
        Assert.Equal(failingSlot, context.Exchange.CallCount);
        Assert.Equal(failingSlot, context.Leases.CallCount);
        Assert.Equal($"cq{failingSlot}", artifact.Slots[^1].ScenarioId);
        Assert.Equal(SubmissionState.SubmissionUnknown, artifact.Slots[^1].SubmissionState);
        Assert.Equal(ChargeState.Unknown, artifact.Slots[^1].ChargeState);
    }

    [Fact]
    public async Task CapabilityIsConsumedBeforeCancellationExpiryAndBindingChecksAndConcurrentLosersClose()
    {
        TestContext cancelled = CreateContext();
        OpenRouterPremiumExecutionCapability cancelledCapability = OpenRouterPremiumEvidenceModule.Authorize(cancelled.Authorization);
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            cancelledCapability, cancelled.Exchange, cancelled.Leases, cancelled.Journal, cancelled.Clock, cts.Token).AsTask());
        await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            cancelledCapability, cancelled.Exchange, cancelled.Leases, cancelled.Journal, cancelled.Clock, CancellationToken.None).AsTask());

        TestContext expired = CreateContext();
        OpenRouterPremiumExecutionCapability expiredCapability = OpenRouterPremiumEvidenceModule.Authorize(expired.Authorization);
        expired.Clock.Advance(OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds + 1);
        OpenRouterPremiumEvidenceException expiry = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            expiredCapability, expired.Exchange, expired.Leases, expired.Journal, expired.Clock, CancellationToken.None).AsTask());
        Assert.Equal("capability_expired", expiry.Code);

        TestContext concurrent = CreateContext();
        concurrent.Exchange.PauseFirstCall = true;
        OpenRouterPremiumExecutionCapability one = OpenRouterPremiumEvidenceModule.Authorize(concurrent.Authorization);
        Task<OpenRouterPremiumEvidenceArtifact> winner = OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            one, concurrent.Exchange, concurrent.Leases, concurrent.Journal, concurrent.Clock, CancellationToken.None).AsTask();
        await concurrent.Exchange.FirstCallStarted;
        OpenRouterPremiumEvidenceException loser = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            one, concurrent.Exchange, concurrent.Leases, concurrent.Journal, concurrent.Clock, CancellationToken.None).AsTask());
        Assert.Equal("capability_consumed", loser.Code);
        concurrent.Exchange.ReleaseFirstCall();
        Assert.Equal("complete", (await winner).Status);
    }

    [Fact]
    public async Task RawFreeArtifactValidationIsCanonicalDetachedAndRejectsTamper()
    {
        TestContext context = CreateContext();
        OpenRouterPremiumEvidenceArtifact artifact = await OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            OpenRouterPremiumEvidenceModule.Authorize(context.Authorization), context.Exchange, context.Leases, context.Journal, context.Clock, CancellationToken.None);
        byte[] caller = artifact.CanonicalUtf8.ToArray();

        OpenRouterPremiumEvidenceArtifact validated = OpenRouterPremiumEvidenceArtifactModule.Validate(caller);
        caller[0] ^= 1;

        Assert.Equal(artifact.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Equal(artifact.CanonicalJson, validated.CanonicalJson);
        byte[] tampered = artifact.CanonicalUtf8.ToArray();
        tampered[^2] ^= 1;
        Assert.Equal("artifact_rejected", Assert.Throws<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceArtifactModule.Validate(tampered)).Code);
    }

    [Theory]
    [InlineData("provider_response_rejected")]
    [InlineData("provider_response_rejected_response_finish_invalid")]
    [InlineData("provider_response_rejected_response_native_finish_reason_not_stop")]
    public void HistoricalProviderResponseRejectionArtifactsRemainCanonicalAndValid(string outcomeCode)
    {
        TestContext context = CreateContext();
        OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(context.Authorization);
        CognitionQualityPromptEnvelopeSlot prompt = capability.Publication.Slots[0];
        OpenRouterPremiumSlotReceipt historical = new(1, prompt.ScenarioId, prompt.PromptDigestSha256,
            new string('a', 64), new string('b', 64), SubmissionState.SubmissionUnknown, ChargeState.Unknown,
            0, 0, 0, 0, null, outcomeCode);

        OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceArtifactModule.Create(
            capability, context.Journal.Header, context.Exchange.Identity, [historical]);
        byte[] canonical = artifact.CanonicalUtf8.ToArray();
        OpenRouterPremiumEvidenceArtifact validated = OpenRouterPremiumEvidenceArtifactModule.Validate(canonical);

        Assert.Equal(outcomeCode, validated.TerminalCode);
        Assert.Equal(outcomeCode, Assert.Single(validated.Slots).OutcomeCode);
        Assert.Equal(artifact.CanonicalJson, validated.CanonicalJson);
        Assert.Equal(artifact.CanonicalDigestSha256, validated.CanonicalDigestSha256);
    }

    [Fact]
    public void EveryTypedActiveOrHistoricalParserDiagnosticMapsExhaustivelyWhileUntypedCodesStayGeneric()
    {
        OpenRouterPremiumResponseParserRejectionCode[] codes =
            Enum.GetValues<OpenRouterPremiumResponseParserRejectionCode>();
        Assert.Equal(codes.Length, codes.Select(code => code.ToString()).Distinct(StringComparer.Ordinal).Count());
        HashSet<string> codeNames = codes.Select(code => code.ToString()).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("response_finish_invalid", codeNames);
        Assert.All(new[]
        {
            "response_choice_index_invalid",
            "response_finish_reason_missing",
            "response_finish_reason_type_invalid",
            "response_finish_reason_not_stop",
            "response_choice_error_present",
            "response_native_finish_reason_type_invalid",
            "response_native_finish_reason_not_stop",
            "response_logprobs_non_null",
            "response_refusal_non_null"
        }, code => Assert.Contains(code, codeNames));
        foreach (OpenRouterPremiumResponseParserRejectionCode code in codes)
        {
            OpenRouterPremiumEvidenceException exception = new(code);
            Assert.Equal(code.ToString(), exception.Code);
            Assert.Equal("provider_response_rejected_" + code,
                OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(exception));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpenRouterPremiumEvidenceException((OpenRouterPremiumResponseParserRejectionCode)int.MaxValue));

        Assert.Equal("provider_response_rejected",
            OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(
                new OpenRouterPremiumEvidenceException("response_finish_invalid")));
        Assert.Equal("provider_response_rejected",
            OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(
                new OpenRouterPremiumEvidenceException("journal_sequence_invalid")));
    }

    [Fact]
    public async Task LiveHttpIdentityClosesOnUnresolvedCostAndAccountGateBeforeCredentialOrJournalIo()
    {
        TestContext context = CreateContext();
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        OpenRouterPremiumAuthorization authorization = context.Authorization with
        {
            ExchangeIdentity = OpenRouterPremiumHttpExchange.AdapterIdentity,
            ExchangeContractDigestSha256 = OpenRouterPremiumHttpExchange.AdapterContractDigestSha256
        };
        OpenRouterPremiumExecutionCapability capability = OpenRouterPremiumEvidenceModule.Authorize(authorization);
        using OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateProduction();

        OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(capability, exchange, context.Leases, context.Journal, context.Clock, CancellationToken.None).AsTask());

        Assert.Equal("paid_cost_ceiling_and_account_binding_unresolved", exception.Code);
        Assert.Equal(0, context.Leases.CallCount);
        Assert.Empty(context.Journal.Snapshot().Slots);
    }

    [Fact]
    public async Task CallerSpoofedOfflineExchangeIsRejectedBeforeItsPropertiesJournalLeaseOrExchange()
    {
        TestContext context = CreateContext();
        HostileOfflineClaimExchange hostile = new();

        OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
                OpenRouterPremiumEvidenceModule.Authorize(context.Authorization), hostile,
                context.Leases, context.Journal, context.Clock, CancellationToken.None).AsTask());

        Assert.Equal("exchange_not_registered", exception.Code);
        Assert.Equal(0, hostile.PropertyReadCount);
        Assert.Equal(0, hostile.CallCount);
        Assert.Equal(0, context.Leases.CallCount);
        Assert.Empty(context.Journal.Snapshot().Slots);
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("endpoint")]
    [InlineData("journal_checksum")]
    [InlineData("exchange")]
    [InlineData("exchange_contract")]
    [InlineData("lease")]
    [InlineData("nonce")]
    [InlineData("expiry")]
    public void AuthorizationMutationMatrixFailsClosedWithoutIo(string mutation)
    {
        TestContext context = CreateContext();
        OpenRouterPremiumAuthorization authorization = mutation switch
        {
            "catalog" => context.Authorization with { CatalogEvidenceDigestSha256 = new string('0', 64) },
            "endpoint" => context.Authorization with { EndpointEvidenceDigestSha256 = new string('0', 64) },
            "journal_checksum" => context.Authorization with { FinancialJournalHeaderChecksumSha256 = "invalid" },
            "exchange" => context.Authorization with { ExchangeIdentity = "INVALID" },
            "exchange_contract" => context.Authorization with { ExchangeContractDigestSha256 = "invalid" },
            "lease" => context.Authorization with { CredentialLeaseSourceIdentity = "INVALID" },
            "nonce" => context.Authorization with { AuthorizationNonce = new string('a', 129) },
            "expiry" => context.Authorization with
            {
                ExpiresAtMilliseconds = 1_000 + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds + 1
            },
            _ => throw new InvalidOperationException()
        };

        Assert.Throws<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumEvidenceModule.Authorize(authorization));
        Assert.Equal(0, context.Exchange.CallCount);
        Assert.Equal(0, context.Leases.CallCount);
        Assert.Empty(context.Journal.Snapshot().Slots);
    }

    [Fact]
    public async Task DuplicateAuthorizationNonceIsProcessBoundedAndCannotReachSecondLeaseOrExchange()
    {
        TestContext first = CreateContext();
        TestContext second = CreateContext();
        OpenRouterPremiumAuthorization duplicate = second.Authorization with { AuthorizationNonce = first.Authorization.AuthorizationNonce };
        OpenRouterPremiumExecutionCapability firstCapability = OpenRouterPremiumEvidenceModule.Authorize(first.Authorization);
        OpenRouterPremiumExecutionCapability secondCapability = OpenRouterPremiumEvidenceModule.Authorize(duplicate);

        Assert.Equal("complete", (await OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(firstCapability,
            first.Exchange, first.Leases, first.Journal, first.Clock, CancellationToken.None)).Status);
        OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(secondCapability,
                second.Exchange, second.Leases, second.Journal, second.Clock, CancellationToken.None).AsTask());

        Assert.Equal("authorization_nonce_consumed", exception.Code);
        Assert.Equal(0, second.Exchange.CallCount);
        Assert.Equal(0, second.Leases.CallCount);
        Assert.Empty(second.Journal.Snapshot().Slots);
    }

    [Fact]
    public async Task ValidButWrongExchangeContractDigestFailsBeforeCredentialJournalOrExchange()
    {
        TestContext context = CreateContext();
        OpenRouterPremiumAuthorization authorization = context.Authorization with
        {
            ExchangeContractDigestSha256 = new string('0', 64)
        };

        OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(OpenRouterPremiumEvidenceModule.Authorize(authorization),
                context.Exchange, context.Leases, context.Journal, context.Clock, CancellationToken.None).AsTask());

        Assert.Equal("execution_binding_invalid", exception.Code);
        Assert.Equal(0, context.Exchange.CallCount);
        Assert.Equal(0, context.Leases.CallCount);
        Assert.Empty(context.Journal.Snapshot().Slots);
    }

    [Theory]
    [InlineData(OfflineCredentialLeaseBehavior.AcquireException, SubmissionState.DefinitelyNotSubmitted, ChargeState.Released)]
    [InlineData(OfflineCredentialLeaseBehavior.Expired, SubmissionState.DefinitelyNotSubmitted, ChargeState.Released)]
    [InlineData(OfflineCredentialLeaseBehavior.DisposedBeforeReturn, SubmissionState.DefinitelyNotSubmitted, ChargeState.Released)]
    public async Task CredentialTerminalPathsZeroExactLeaseOwnedBufferWithoutExchange(
        OfflineCredentialLeaseBehavior behavior,
        SubmissionState expectedSubmission,
        ChargeState expectedCharge)
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        ByokAccountBindingIdentity account = new("byok-account-sha256-" + new string('b', 64));
        OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
            $"openrouter-premium-journal/credential-{behavior.ToString().ToLowerInvariant()}",
            $"openrouter-premium-run/credential-{behavior.ToString().ToLowerInvariant()}", profile, account);
        InMemoryOpenRouterPremiumJournal journal = new(header);
        FakeCredentialLeaseSource leases = new(behavior: behavior);
        ScriptedOpenRouterPremiumExchange exchange = ScriptedOpenRouterPremiumExchange.CreateSuccessful();
        OfflineOpenRouterPremiumClock clock = new(1_000);
        OpenRouterPremiumAuthorization authorization = new(profile.Identity,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
            account, header.JournalIdentity, header.HeaderChecksumSha256, exchange.Identity,
            exchange.ContractDigestSha256, leases.Identity,
            $"openrouter-premium-authorization/credential-{behavior.ToString().ToLowerInvariant()}-{Interlocked.Increment(ref _contextCounter)}",
            1_000, 1_000 + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds);

        OpenRouterPremiumEvidenceArtifact artifact = await OpenRouterPremiumEvidenceModule.ExecuteOnceAsync(
            OpenRouterPremiumEvidenceModule.Authorize(authorization), exchange, leases, journal, clock, CancellationToken.None);

        Assert.Equal("terminal", artifact.Status);
        Assert.Equal(0, exchange.CallCount);
        Assert.Equal(1, leases.CallCount);
        Assert.True(leases.LastLeaseZeroed);
        Assert.Equal(expectedSubmission, artifact.Slots.Single().SubmissionState);
        Assert.Equal(expectedCharge, artifact.Slots.Single().ChargeState);
        Assert.DoesNotContain("offline_fake_acquire_failure", artifact.CanonicalJson, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> AllSlots() => Enumerable.Range(1, 12).Select(value => new object[] { value });

    private static TestContext CreateContext(int? failingSlot = null)
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        ByokAccountBindingIdentity account = new("byok-account-sha256-" + new string('a', 64));
        OpenRouterPremiumJournalHeader header = OpenRouterPremiumJournalHeader.Create(
            "openrouter-premium-journal/test", "openrouter-premium-run/test", profile, account);
        InMemoryOpenRouterPremiumJournal journal = new(header);
        FakeCredentialLeaseSource leases = new();
        ScriptedOpenRouterPremiumExchange exchange = ScriptedOpenRouterPremiumExchange.CreateSuccessful(failingSlot);
        OfflineOpenRouterPremiumClock clock = new(1_000);
        OpenRouterPremiumAuthorization authorization = new(
            profile.Identity,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256,
            OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
            account,
            header.JournalIdentity,
            header.HeaderChecksumSha256,
            exchange.Identity,
            exchange.ContractDigestSha256,
            leases.Identity,
            $"openrouter-premium-authorization/test-nonce-{Interlocked.Increment(ref _contextCounter)}",
            1_000,
            1_000 + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds);
        return new(authorization, exchange, leases, journal, clock);
    }

    private sealed record TestContext(
        OpenRouterPremiumAuthorization Authorization,
        ScriptedOpenRouterPremiumExchange Exchange,
        FakeCredentialLeaseSource Leases,
        InMemoryOpenRouterPremiumJournal Journal,
        OfflineOpenRouterPremiumClock Clock);

    private sealed class HostileOfflineClaimExchange : IOpenRouterPremiumExchange
    {
        public int PropertyReadCount { get; private set; }
        public int CallCount { get; private set; }
        public string Identity { get { PropertyReadCount++; return ScriptedOpenRouterPremiumExchange.AdapterIdentity; } }
        public string ContractDigestSha256 { get { PropertyReadCount++; return ScriptedOpenRouterPremiumExchange.AdapterContractDigestSha256; } }
        public bool IsOfflineScripted { get { PropertyReadCount++; return true; } }

        public ValueTask<OpenRouterPremiumExchangeResponse> ExchangeOnceAsync(
            OpenRouterPremiumExchangeRequest request,
            ReadOnlyMemory<byte> bearerCredential,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(OpenRouterPremiumExchangeResponse.SubmissionUnknown(OpenRouterPremiumProfile.EffectiveUri));
        }
    }
}
