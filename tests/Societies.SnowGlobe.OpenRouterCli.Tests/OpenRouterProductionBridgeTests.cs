using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.OpenRouterCli.Tests;

public sealed class OpenRouterProductionBridgeTests
{
    private const long Now = 1_777_118_400_000L; // 2026-04-26 placeholder overwritten by ParsedNow.
    private static readonly long ParsedNow = DateTimeOffset.ParseExact("2026-08-21T12:00:00Z", "yyyy-MM-dd'T'HH:mm:ss'Z'",
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToUnixTimeMilliseconds();

    [Fact]
    public async Task CredentialMissingMalformedAndAccountMismatchFailBeforeDurableAuthority()
    {
        foreach ((string kind, string expected) in new[]
                 { ("missing", "credential_missing"), ("malformed", "credential_malformed"), ("mismatch", "credential_account_mismatch") })
        {
            string root = Temp(); string account = Account('a');
            try
            {
                FakeCredentialStore store = kind switch
                {
                    "missing" => FakeCredentialStore.Missing(),
                    "malformed" => new(account, [1, 2, 3]),
                    _ => new(Account('b'), Secret())
                };
                OpenRouterPremiumProductionBridge bridge = Bridge(root, store, new FakeProtector(), new FakeClock(ParsedNow),
                    new ScriptedHandler(ScriptedResponse.Success), account);
                OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () => await bridge.PreflightAsync());
                Assert.Equal(expected, error.Code);
                Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.PreflightFailedFileName)));
                Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName)));
            }
            finally { Delete(root); }
        }
    }

    [Fact]
    public void CredentialProvisioningClearsCallerOwnedCharactersOnSuccessAndFailure()
    {
        string root = Temp(); string account = Account('a');
        try
        {
            FakeCredentialStore store = new(account, Secret());
            OpenRouterPremiumProductionBridge bridge = Bridge(root, store, new FakeProtector(), new FakeClock(ParsedNow),
                new ScriptedHandler(ScriptedResponse.Success));
            char[] valid = Encoding.ASCII.GetString(Secret()).ToCharArray();
            bridge.StoreCredential(valid);
            Assert.All(valid, value => Assert.Equal('\0', value));
            char[] invalid = "not-a-key".ToCharArray();
            Assert.Equal("credential_malformed", Assert.Throws<OpenRouterPremiumProductionException>(() => bridge.StoreCredential(invalid)).Code);
            Assert.All(invalid, value => Assert.Equal('\0', value));
            Assert.DoesNotContain("sk-or-v1", store.LastOperationSummary, StringComparison.Ordinal);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void PartialNativeCredentialCopyFailureClearsTheEntireOwnedManagedBuffer()
    {
        byte[]? observed = null;
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            OpenRouterPremiumWindowsCredentialStore.CopyOwnedCredential(
                OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity,
                new IntPtr(1),
                64,
                (_, destination, _) =>
                {
                    observed = destination;
                    destination.AsSpan(0, 17).Fill(0x5a);
                    throw new InvalidOperationException("injected_partial_copy_failure");
                }));

        Assert.Equal("injected_partial_copy_failure", error.Message);
        Assert.NotNull(observed);
        Assert.All(observed!, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void DpapiSecondPinFailureClearsOwnedAuthorizationAndDisposesTheFirstPin()
    {
        byte[] callerOwned = Enumerable.Repeat((byte)0x5a, 64).ToArray();
        FaultingDpapiOperations operations = new(DpapiFailure.SecondPin);

        Assert.Equal("injected_second_pin_failure", Assert.Throws<InvalidOperationException>(() =>
            OpenRouterPremiumDpapiProtector.TransformForOfflineTests(callerOwned, protect: true, operations)).Message);

        Assert.NotNull(operations.OwnedAuthorization);
        Assert.All(operations.OwnedAuthorization!, value => Assert.Equal((byte)0, value));
        Assert.All(callerOwned, value => Assert.Equal((byte)0x5a, value));
        Assert.Single(operations.Pins);
        Assert.True(operations.Pins[0].Disposed);
        Assert.Equal(0, operations.NativeFreeCalls);
    }

    [Fact]
    public void DpapiPartialOutputCopyFailureClearsManagedOutputPinsAndNativeBlob()
    {
        byte[] callerOwned = Enumerable.Repeat((byte)0x5a, 64).ToArray();
        FaultingDpapiOperations operations = new(DpapiFailure.PartialOutputCopy);

        Assert.Equal("injected_partial_output_copy_failure", Assert.Throws<InvalidOperationException>(() =>
            OpenRouterPremiumDpapiProtector.TransformForOfflineTests(callerOwned, protect: true, operations)).Message);

        Assert.NotNull(operations.OwnedAuthorization);
        Assert.All(operations.OwnedAuthorization!, value => Assert.Equal((byte)0, value));
        Assert.NotNull(operations.PartialOutput);
        Assert.All(operations.PartialOutput!, value => Assert.Equal((byte)0, value));
        Assert.Equal(2, operations.Pins.Count);
        Assert.All(operations.Pins, pin => Assert.True(pin.Disposed));
        Assert.Equal(1, operations.NativeFreeCalls);
    }

    [Fact]
    public async Task EligiblePreflightRunsExactlyTwelveSequentialRequestsAndValidatesExactlyOnce()
    {
        string root = Temp(); string account = Account('a'); ScriptedHandler handler = new(ScriptedResponse.Success);
        FakeCredentialStore store = new(account, Secret()); FakeProtector protector = new(); FakeClock clock = new(ParsedNow);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, store, protector, clock, handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            Assert.Equal(12, preflight.MaximumRequests); Assert.Equal(18_000, preflight.AggregateCostCeilingMicrousd);
            Assert.Equal(ParsedNow + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds,
                preflight.ExpiresAtUnixMilliseconds);

            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256);
            Assert.Equal("complete", run.Status); Assert.Equal(12, run.ExchangeCount); Assert.Equal(528, run.TotalSettledMicrousd);
            Assert.Equal(12, handler.CallCount); Assert.Equal(1, handler.MaximumActive);
            Assert.All(handler.RequestUris, uri => Assert.Equal(OpenRouterPremiumProfile.EffectiveUri, uri));
            Assert.All(handler.AuthorizationSchemes, value => Assert.Equal("Bearer", value));
            Assert.True(store.ZeroObservationCount >= 13); Assert.True(store.AllObservedZero);

            OpenRouterPremiumProductionValidationResult validation = bridge.ValidateOnce();
            Assert.Equal(run.EvidenceArtifactDigestSha256, validation.EvidenceArtifactDigestSha256);
            Assert.Equal("validation_not_available", Assert.Throws<OpenRouterPremiumProductionException>(() => bridge.ValidateOnce()).Code);
            int calls = handler.CallCount;
            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(calls, handler.CallCount);

            OpenRouterPremiumProductionBridge restarted = Bridge(root, store, protector, clock, handler);
            await Assert.ThrowsAnyAsync<Exception>(async () => await restarted.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(calls, handler.CallCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task RuntimeWindowAdmitsTwelveSequentialObservedBoundaryDurations()
    {
        string root = Temp(); string account = Account('a'); FakeClock clock = new(ParsedNow);
        ScriptedHandler handler = new(ScriptedResponse.Success, () => clock.Advance(5_240));
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), clock, handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();

            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256);

            Assert.Equal("complete", run.Status);
            Assert.Equal(12, run.ExchangeCount);
            Assert.Equal(12, handler.CallCount);
            Assert.Equal(ParsedNow + (12 * 5_240), clock.NowMilliseconds);
            Assert.True(preflight.ExpiresAtUnixMilliseconds - ParsedNow
                >= (12 * OpenRouterPremiumProfileRegistry.Selected.Bounds.CredentialLeaseLifetimeMilliseconds)
                    + OpenRouterPremiumProfile.RuntimeAuthorizationOverheadMilliseconds);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task RuntimeAuthorizationCannotOutliveAuthenticatedKeyWindow()
    {
        string root = Temp(); string account = Account('a');
        FakeClock delayedClock = new(ParsedNow + 61_000);
        try
        {
            OpenRouterPremiumProductionBridge bridge = new(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), delayedClock,
                new FakeMetadataVerifier(Bundle(account, "2026-08-21T12:05:00Z")),
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(
                async () => await bridge.PreflightAsync());

            Assert.Equal("key_expiry_window_invalid", error.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(ScriptedResponse.Http503, "http_503_terminal")]
    [InlineData(ScriptedResponse.CostAboveCeiling, "provider_response_rejected_response_cost_invalid")]
    [InlineData(ScriptedResponse.MalformedSchema, "provider_response_rejected_response_object_binding_invalid")]
    [InlineData(ScriptedResponse.InvalidChoiceIndex, "provider_response_rejected_response_choice_index_invalid")]
    [InlineData(ScriptedResponse.MissingFinishReason, "provider_response_rejected_response_finish_reason_missing")]
    [InlineData(ScriptedResponse.WrongTypeFinishReason, "provider_response_rejected_response_finish_reason_type_invalid")]
    [InlineData(ScriptedResponse.NonStopFinishReason, "provider_response_rejected_response_finish_reason_not_stop")]
    [InlineData(ScriptedResponse.ChoiceErrorPresent, "provider_response_rejected_response_choice_error_present")]
    [InlineData(ScriptedResponse.WrongTypeNativeFinishReason, "provider_response_rejected_response_native_finish_reason_type_invalid")]
    [InlineData(ScriptedResponse.NullNativeFinishReason, "provider_response_rejected_response_native_finish_reason_type_invalid")]
    [InlineData(ScriptedResponse.NonNullLogprobs, "provider_response_rejected_response_logprobs_non_null")]
    [InlineData(ScriptedResponse.NonNullRefusal, "provider_response_rejected_response_refusal_non_null")]
    [InlineData(ScriptedResponse.InvalidRouting, "provider_response_rejected_response_routing_invalid")]
    [InlineData(ScriptedResponse.InvalidJson, "provider_response_rejected_response_json_invalid")]
    [InlineData(ScriptedResponse.InvalidShape, "provider_response_rejected_response_shape_invalid")]
    [InlineData(ScriptedResponse.InvalidUsage, "provider_response_rejected_response_usage_invalid")]
    [InlineData(ScriptedResponse.UsageByokTrue, "provider_response_rejected_response_usage_invalid")]
    [InlineData(ScriptedResponse.InvalidProposal, "provider_response_rejected_proposal_content_invalid")]
    [InlineData(ScriptedResponse.InvalidContentType, "provider_exchange_unknown")]
    [InlineData(ScriptedResponse.UnexpectedExchangeFailure, "provider_exchange_unknown")]
    public async Task StatusParserAndPreParserFailuresAreTerminalUnknownRawFreeAndNeverRetried(
        ScriptedResponse response,
        string expectedTerminal)
    {
        string root = Temp(); string account = Account('a'); ScriptedHandler handler = new(response);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256);
            Assert.Equal("terminal", run.Status); Assert.Equal(1, run.ExchangeCount); Assert.Equal(0, run.TotalSettledMicrousd);
            Assert.Equal(expectedTerminal, run.TerminalCode); Assert.Equal(1, handler.CallCount);
            Assert.True(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.ExecutionIndeterminateFileName)));
            byte[] evidenceBytes = File.ReadAllBytes(Path.Combine(root, OpenRouterPremiumProductionFiles.EvidenceArtifactFileName));
            OpenRouterPremiumEvidenceArtifact detached = OpenRouterPremiumEvidenceArtifactModule.Validate(evidenceBytes);
            OpenRouterPremiumSlotReceipt slot = Assert.Single(detached.Slots);
            Assert.Equal(expectedTerminal, detached.TerminalCode);
            Assert.Equal(expectedTerminal, slot.OutcomeCode);
            Assert.Equal(ChargeState.Unknown, slot.ChargeState);
            Assert.Equal(SubmissionState.SubmissionUnknown, slot.SubmissionState);
            Assert.Equal(0, slot.PromptTokens); Assert.Equal(0, slot.CompletionTokens);
            Assert.Equal(0, slot.TotalTokens); Assert.Equal(0, slot.SettledMicrousd);
            Assert.Null(slot.Proposal);
            if (expectedTerminal.StartsWith("provider_response_rejected_", StringComparison.Ordinal)
                || expectedTerminal == "http_503_terminal")
                Assert.Equal(handler.LastResponseDigestSha256, slot.ResponseDigestSha256);
            else
                Assert.Equal(new string('0', 64), slot.ResponseDigestSha256);
            Assert.DoesNotContain("agent-00", detached.CanonicalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-provider-sentinel-must-not-leak", detached.CanonicalJson, StringComparison.Ordinal);
            CryptographicOperations.ZeroMemory(evidenceBytes);
            using (FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(
                Path.Combine(root, OpenRouterPremiumProductionFiles.JournalDirectoryName)))
            {
                OpenRouterPremiumJournalSlotSnapshot persisted = Assert.Single(reopened.Snapshot().Slots);
                Assert.Equal(expectedTerminal, persisted.Receipt!.OutcomeCode);
                Assert.Contains($"cq1/completed/{expectedTerminal}", reopened.Snapshot().Trace);
            }
            OpenRouterPremiumProductionValidationResult validation = bridge.ValidateOnce();
            Assert.Equal("terminal", validation.Status); Assert.Equal(1, validation.ExchangeCount);
            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(1, handler.CallCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task BoundedNonAuthoritativeNativeFinishMetadataCompletesWithoutRetryFallbackOrRetention()
    {
        string root = Temp();
        ScriptedHandler handler = new(ScriptedResponse.NonStopNativeFinishReason);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root,
                new FakeCredentialStore(Account('a'), Secret()), new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();

            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256);

            Assert.Equal("complete", run.Status);
            Assert.Equal(12, run.ExchangeCount);
            Assert.Equal(528, run.TotalSettledMicrousd);
            Assert.Null(run.TerminalCode);
            Assert.Equal(12, handler.CallCount);
            Assert.Equal(1, handler.MaximumActive);
            byte[] evidenceBytes = File.ReadAllBytes(Path.Combine(root, OpenRouterPremiumProductionFiles.EvidenceArtifactFileName));
            try
            {
                OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceArtifactModule.Validate(evidenceBytes);
                Assert.Equal("complete", artifact.Status);
                Assert.DoesNotContain("bounded-provider-metadata", artifact.CanonicalJson, StringComparison.Ordinal);
            }
            finally { CryptographicOperations.ZeroMemory(evidenceBytes); }
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task DocumentedBoundedUsageAdditionsCompleteWithoutRetryFallbackOrRawRetention()
    {
        string root = Temp();
        ScriptedHandler handler = new(ScriptedResponse.DocumentedUsageAdditions);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root,
                new FakeCredentialStore(Account('a'), Secret()), new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();

            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256);

            Assert.Equal("complete", run.Status);
            Assert.Equal(12, run.ExchangeCount);
            Assert.Equal(528, run.TotalSettledMicrousd);
            Assert.Null(run.TerminalCode);
            Assert.Equal(12, handler.CallCount);
            Assert.Equal(1, handler.MaximumActive);
            byte[] evidenceBytes = File.ReadAllBytes(Path.Combine(root, OpenRouterPremiumProductionFiles.EvidenceArtifactFileName));
            try
            {
                OpenRouterPremiumEvidenceArtifact artifact = OpenRouterPremiumEvidenceArtifactModule.Validate(evidenceBytes);
                Assert.Equal("complete", artifact.Status);
                Assert.DoesNotContain("server_tool_use_details", artifact.CanonicalJson, StringComparison.Ordinal);
                Assert.DoesNotContain("upstream_inference", artifact.CanonicalJson, StringComparison.Ordinal);
            }
            finally { CryptographicOperations.ZeroMemory(evidenceBytes); }
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task PublicationCollisionCreatesIndeterminateTombstoneAndPreventsSecondRun()
    {
        string root = Temp(); string account = Account('a'); ScriptedHandler handler = new(ScriptedResponse.Success);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            File.WriteAllText(Path.Combine(root, OpenRouterPremiumProductionFiles.EvidenceArtifactFileName), "collision", Encoding.ASCII);
            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(12, handler.CallCount);
            Assert.True(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.ExecutionIndeterminateFileName)));
            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(12, handler.CallCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task AuthorizationConfirmationMismatchAndExpiryNeverReachCredentialOrHttp()
    {
        foreach (bool expiry in new[] { false, true })
        {
            string root = Temp(); string account = Account('a'); ScriptedHandler handler = new(ScriptedResponse.Success);
            FakeCredentialStore store = new(account, Secret()); FakeClock clock = new(ParsedNow);
            try
            {
                OpenRouterPremiumProductionBridge bridge = Bridge(root, store, new FakeProtector(), clock, handler);
                OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
                int reads = store.ReadCount;
                string confirmation = preflight.AuthorizationDigestSha256;
                if (expiry) clock.Advance(OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds);
                else confirmation = new string('f', 64);
                OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () =>
                    await bridge.RecordOnceAsync(confirmation));
                Assert.Equal(expiry ? "authorization_expired" : "authorization_confirmation_mismatch", error.Code);
                Assert.Equal(0, handler.CallCount); Assert.Equal(reads, store.ReadCount);
                Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.ExecutionConsumedFileName)));
            }
            finally { Delete(root); }
        }
    }

    [Fact]
    public async Task HardLinkedAuthorizationArtifactIsRejectedBeforeConsumptionOrHttp()
    {
        string root = Temp(); string aliases = Temp(); string account = Account('a'); ScriptedHandler handler = new(ScriptedResponse.Success);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            CreateHardLinkExact(Path.Combine(aliases, "authorization.alias"),
                Path.Combine(root, OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName));
            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(0, handler.CallCount);
            Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.ExecutionConsumedFileName)));
        }
        finally { Delete(root); Delete(aliases); }
    }

    [Fact]
    public async Task ReplacedStateRootIsRejectedBeforeAuthorizationReadOrHttp()
    {
        string root = Temp(); string moved = root + "-moved"; string account = Account('a');
        ScriptedHandler handler = new(ScriptedResponse.Success);
        try
        {
            OpenRouterPremiumProductionBridge bridge = Bridge(root, new FakeCredentialStore(account, Secret()),
                new FakeProtector(), new FakeClock(ParsedNow), handler);
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            Directory.Move(root, moved); Directory.CreateDirectory(root);

            await Assert.ThrowsAnyAsync<Exception>(async () => await bridge.RecordOnceAsync(preflight.AuthorizationDigestSha256));
            Assert.Equal(0, handler.CallCount);
            Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.ExecutionConsumedFileName)));
        }
        finally { Delete(root); Delete(moved); }
    }

    [Fact]
    public async Task AuthenticatedMetadataVerifierUsesExactlyThreeSequentialGetsAndDerivesOpaqueAuthority()
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.OfficialCurrentKeyExample);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        byte[] bundleBytes;
        using (OpenRouterPremiumVerifiedMetadata verified = await verifier.VerifyOnceAsync(CancellationToken.None))
            bundleBytes = verified.TransferOwnedCanonicalBundle();
        try
        {
            OpenRouterPremiumActivationBundle bundle = OpenRouterPremiumActivationPreflightCodec.Parse(bundleBytes);
            string expectedAccount = "byok-account-sha256-" + OpenRouterPremiumCanonical.Digest(
                "openrouter-account-key-subject/v1|creator-user-offline|snowglobe-one-shot");
            Assert.Equal(expectedAccount, bundle.AccountBindingIdentity);
            Assert.Equal(expectedAccount, store.AccountForMetadata);
            Assert.Equal(OpenRouterPremiumProfile.ModelIdentity, bundle.Catalog.ModelId);
            Assert.Equal(OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity, bundle.Catalog.CanonicalSlug);
            Assert.True(bundle.Catalog.ApiModelIdVerified);
            Assert.Equal(OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, bundle.Catalog.FrozenCatalogEvidenceDigestSha256);
            Assert.True(bundle.Catalog.PricingOverrideAuthenticatedCurrent);
            Assert.False(bundle.Catalog.PricingOverrideReachableForProfile);
            Assert.Contains("pricing_override_provenance=authenticated_current_api_and_frozen_model_page",
                OpenRouterPremiumHttpMetadataVerifier.OfficialContractDescriptor, StringComparison.Ordinal);
            Assert.Contains("pricing_override_authenticated_current=true",
                OpenRouterPremiumHttpMetadataVerifier.OfficialContractDescriptor, StringComparison.Ordinal);
            Assert.DoesNotContain("frozen_not_authenticated_current_api",
                OpenRouterPremiumHttpMetadataVerifier.OfficialContractDescriptor, StringComparison.Ordinal);
            Assert.Equal("2026-08-21T13:00:00Z", bundle.Attestation.ExpiresAtUtc);
            string rawFree = Encoding.UTF8.GetString(bundleBytes);
            Assert.DoesNotContain("creator-user-offline", rawFree, StringComparison.Ordinal);
            Assert.DoesNotContain("snowglobe-one-shot", rawFree, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-or-v1", rawFree, StringComparison.Ordinal);
        }
        finally { CryptographicOperations.ZeroMemory(bundleBytes); }

        Assert.Equal(3, handler.CallCount);
        Assert.Equal(new[]
        {
            OpenRouterPremiumHttpMetadataVerifier.CurrentKeyUri,
            OpenRouterPremiumHttpMetadataVerifier.ModelsUri,
            OpenRouterPremiumHttpMetadataVerifier.ZdrEndpointsUri
        }, handler.RequestUris);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
        Assert.All(handler.AuthorizationSchemes, scheme => Assert.Equal("Bearer", scheme));
        Assert.All(handler.AuthorizationParameters, parameter => Assert.StartsWith("sk-or-v1-", parameter, StringComparison.Ordinal));
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(1, store.ZeroObservationCount);
        Assert.True(store.AllObservedZero);
        Assert.False(verifier.RedirectsAllowed);
        Assert.False(verifier.AutomaticRetriesAllowed);
        Assert.False(verifier.ProxyAllowed);
        Assert.False(verifier.CookiesAllowed);
        Assert.False(verifier.AmbientAuthenticationAllowed);
        Assert.False(verifier.AutomaticDecompressionAllowed);
    }

    [Fact]
    public async Task AuthenticatedMetadataVerifierUsesOneCredentialSnapshotAcrossConcurrentReplacement()
    {
        byte[] original = Secret();
        byte[] replacement = Encoding.ASCII.GetBytes("sk-or-v1-" + new string('b', 48));
        FakeCredentialStore store = new(
            OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, original);
        ScriptedMetadataHandler handler = new(MetadataScenario.OfficialCurrentKeyExample, call =>
        {
            if (call == 1)
                store.Write(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, replacement);
        });
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        using OpenRouterPremiumVerifiedMetadata verified = await verifier.VerifyOnceAsync(CancellationToken.None);

        Assert.Equal(1, store.ReadCount);
        Assert.Single(handler.AuthorizationParameters.Distinct(StringComparer.Ordinal));
        Assert.Equal(Encoding.ASCII.GetString(original), handler.AuthorizationParameters[0]);
        Assert.True(store.SecretMatches(original));
        Assert.Equal(1, store.ZeroObservationCount);
        Assert.True(store.AllObservedZero);
        CryptographicOperations.ZeroMemory(original);
        CryptographicOperations.ZeroMemory(replacement);
    }

    [Fact]
    public async Task AuthenticatedReadinessAdapterUsesVerifierThreeGetPathAndPublishesRawFreeFact()
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.OfficialCurrentKeyExample);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);
        OpenRouterAuthenticatedReadinessAdapter adapter = new(verifier);

        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FakeReadinessClock(ParsedNow), CancellationToken.None);

        Assert.Equal("ready", observation.Readiness);
        Assert.Equal(3, observation.RequestCount);
        Assert.Equal(0, observation.GenerationRequestCount);
        Assert.Equal("same_account_bound", observation.AccountBindingStatus);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(new[]
        {
            OpenRouterPremiumHttpMetadataVerifier.CurrentKeyUri,
            OpenRouterPremiumHttpMetadataVerifier.ModelsUri,
            OpenRouterPremiumHttpMetadataVerifier.ZdrEndpointsUri
        }, handler.RequestUris);
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
        Assert.DoesNotContain("creator-user-offline", observation.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("snowglobe-one-shot", observation.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-or-v1", observation.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedReadinessAdapterIsOneShotAndRepeatNeverDispatches()
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.OfficialCurrentKeyExample);
        OpenRouterAuthenticatedReadinessAdapter adapter = new(
            OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
                store, new FakeClock(ParsedNow), () => handler));

        ProviderReadinessObservation first = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FakeReadinessClock(ParsedNow), CancellationToken.None);
        ProviderReadinessObservation repeated = await ProviderReadinessObservationModule.ObserveAsync(
            adapter, new FakeReadinessClock(ParsedNow), CancellationToken.None);

        Assert.Equal("ready", first.Readiness);
        Assert.Equal("unknown", repeated.Readiness);
        Assert.Equal("observation_failed", repeated.DiagnosticCode);
        Assert.Equal(0, repeated.RequestCount);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(1, store.ReadCount);
    }

    [Fact]
    public async Task AuthenticatedReadinessAdapterStopsAtFirstMetadataFailureAndDoesNotBindAccount()
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.ModelsHttp503);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        ProviderReadinessObservation observation = await ProviderReadinessObservationModule.ObserveAsync(
            new OpenRouterAuthenticatedReadinessAdapter(verifier),
            new FakeReadinessClock(ParsedNow),
            CancellationToken.None);

        Assert.Equal("unavailable", observation.Readiness);
        Assert.Equal("provider_unavailable", observation.DiagnosticCode);
        Assert.Equal(2, observation.RequestCount);
        Assert.Equal(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, store.AccountForMetadata);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task UnlimitedKeyMetadataIsAcceptedWhileApplicationCostCeilingRemainsFixed()
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.UnlimitedKey);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        using OpenRouterPremiumVerifiedMetadata verified = await verifier.VerifyOnceAsync(CancellationToken.None);
        byte[] bundleBytes = verified.TransferOwnedCanonicalBundle();
        try
        {
            OpenRouterPremiumActivationBundle bundle = OpenRouterPremiumActivationPreflightCodec.Parse(bundleBytes);
            Assert.Equal(12, bundle.MaximumRequests);
            Assert.Equal(18_000, bundle.AggregateCostCeilingMicrousd);
            Assert.Equal("2026-08-22T12:00:00Z", bundle.Attestation.ExpiresAtUtc);
            Assert.Equal(3, handler.CallCount);
        }
        finally { CryptographicOperations.ZeroMemory(bundleBytes); }
    }

    [Fact]
    public async Task CancellationAfterThirdMetadataGetPreventsAccountBindingAndEveryDurableArtifact()
    {
        string root = Temp(); using CancellationTokenSource cancelled = new();
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.Success, call => { if (call == 3) cancelled.Cancel(); });
        try
        {
            OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
                store, new FakeClock(ParsedNow), () => handler);
            OpenRouterPremiumProductionBridge bridge = new(root, store, new FakeProtector(), new FakeClock(ParsedNow), verifier,
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await bridge.PreflightAsync(cancelled.Token));
            Assert.Equal(3, handler.CallCount);
            Assert.Equal(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, store.AccountForMetadata);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task CancellationReturnedWithVerifiedMetadataPreventsFirstDurableMutation()
    {
        string root = Temp(); string account = Account('a'); using CancellationTokenSource cancelled = new();
        FakeCredentialStore store = new(account, Secret());
        try
        {
            OpenRouterPremiumProductionBridge bridge = new(root, store, new FakeProtector(), new FakeClock(ParsedNow),
                new FakeMetadataVerifier(Bundle(account), cancelled.Cancel),
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await bridge.PreflightAsync(cancelled.Token));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task CancellationBeforeDpapiIssuancePreventsRuntimeAuthority()
    {
        string root = Temp(); string account = Account('a'); using CancellationTokenSource cancelled = new();
        FakeCredentialStore store = new(account, Secret()); FakeProtector protector = new();
        try
        {
            OpenRouterPremiumProductionBridge bridge = new(root, store, protector,
                new CancellingClock(ParsedNow, cancelled.Cancel), new FakeMetadataVerifier(Bundle(account)),
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await bridge.PreflightAsync(cancelled.Token));
            Assert.Equal(0, protector.ProtectCalls);
            Assert.False(File.Exists(Path.Combine(root, OpenRouterPremiumProductionFiles.RuntimeAuthorizationFileName)));
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(MetadataScenario.MissingExpiry)]
    [InlineData(MetadataScenario.MalformedExpiry)]
    [InlineData(MetadataScenario.NonUtcExpiry)]
    [InlineData(MetadataScenario.WrongKindExpiry)]
    public async Task ProviderExpiryMustBeNullOrPresentExactUtcAndFailureCreatesNoAuthority(
        MetadataScenario scenario)
    {
        string root = Temp();
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        FakeProtector protector = new(); ScriptedMetadataHandler handler = new(scenario);
        try
        {
            OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
                store, new FakeClock(ParsedNow), () => handler);
            OpenRouterPremiumProductionBridge bridge = new(root, store, protector, new FakeClock(ParsedNow), verifier,
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () =>
                await bridge.PreflightAsync(CancellationToken.None));
            Assert.Equal("key_expiry_window_invalid", error.Code);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, store.AccountForMetadata);
            Assert.Equal(0, protector.ProtectCalls);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task ProviderOneHourExpiryIsBoundExactlyWhileRuntimeAuthorityRemainsFourMinutes()
    {
        string root = Temp();
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.Success);
        try
        {
            OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
                store, new FakeClock(ParsedNow), () => handler);
            OpenRouterPremiumProductionBridge bridge = new(root, store, new FakeProtector(), new FakeClock(ParsedNow), verifier,
                () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(new ScriptedHandler(ScriptedResponse.Success)));

            OpenRouterPremiumProductionPreflightResult result = await bridge.PreflightAsync(CancellationToken.None);
            Assert.Equal(ParsedNow + OpenRouterPremiumProfile.RuntimeAuthorizationLifetimeMilliseconds,
                result.ExpiresAtUnixMilliseconds);
            Assert.Equal(3, handler.CallCount);

            byte[] activationBytes = File.ReadAllBytes(Path.Combine(root, OpenRouterPremiumProductionFiles.ActivationBundleFileName));
            try
            {
                OpenRouterPremiumActivationBundle activation = OpenRouterPremiumActivationPreflightCodec.Parse(activationBytes);
                Assert.Equal("2026-08-21T12:00:00Z", activation.Attestation.AttestedAtUtc);
                Assert.Equal("2026-08-21T13:00:00Z", activation.Attestation.ExpiresAtUtc);
            }
            finally { CryptographicOperations.ZeroMemory(activationBytes); }
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(MetadataScenario.MissingAccountSubject, "metadata_shape_invalid", 1)]
    [InlineData(MetadataScenario.InsufficientLimit, "key_authority_insufficient", 1)]
    [InlineData(MetadataScenario.InsufficientHardLimit, "key_authority_insufficient", 1)]
    [InlineData(MetadataScenario.PartiallyUnlimitedKey, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.InvalidLimitReset, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.ManagementKey, "key_authority_insufficient", 1)]
    [InlineData(MetadataScenario.ExpiringKey, "key_expiry_window_invalid", 1)]
    [InlineData(MetadataScenario.KeyHttp503, "metadata_key_http_status_terminal", 1)]
    [InlineData(MetadataScenario.ModelsHttp503, "metadata_models_http_status_terminal", 2)]
    [InlineData(MetadataScenario.ZdrHttp503, "metadata_zdr_http_status_terminal", 3)]
    [InlineData(MetadataScenario.EffectiveUriMismatch, "metadata_effective_uri_mismatch", 1)]
    [InlineData(MetadataScenario.Oversized, "metadata_response_too_large", 1)]
    [InlineData(MetadataScenario.DeepJson, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.DuplicateKeyProperty, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.UnknownKeyProperty, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.NegativeByokUsage, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.WrongKindByokUsage, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.OverflowByokUsage, "key_metadata_invalid", 1)]
    [InlineData(MetadataScenario.UndatedCatalogSlug, "model_metadata_mismatch", 2)]
    [InlineData(MetadataScenario.ModelMissingReasoningParameter, "metadata_parameters_invalid", 2)]
    [InlineData(MetadataScenario.WrongZdrProvider, "zdr_route_mismatch", 3)]
    [InlineData(MetadataScenario.OnlyHigherPricedZdrVariants, "zdr_route_mismatch", 3)]
    [InlineData(MetadataScenario.ZdrMissingReasoningParameter, "metadata_parameters_invalid", 3)]
    public async Task MetadataFailuresAreTerminalWithoutRetryOrAccountBinding(
        MetadataScenario scenario, string expectedCode, int expectedCalls)
    {
        FakeCredentialStore store = new(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, Secret());
        ScriptedMetadataHandler handler = new(scenario);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () =>
            await verifier.VerifyOnceAsync(CancellationToken.None));
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedCalls, handler.CallCount);
        Assert.Equal(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, store.AccountForMetadata);
    }

    [Theory]
    [InlineData(true, "credential_missing")]
    [InlineData(false, "credential_malformed")]
    public async Task MetadataCredentialFailureOccursBeforeAnyHttp(bool missing, string expectedCode)
    {
        FakeCredentialStore store = missing
            ? FakeCredentialStore.Missing()
            : new FakeCredentialStore(OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity, [1, 2, 3]);
        ScriptedMetadataHandler handler = new(MetadataScenario.Success);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () =>
            await verifier.VerifyOnceAsync(CancellationToken.None));
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticatedAccountMismatchFailsAfterMetadataWithoutRebindingCredential()
    {
        string existing = Account('b');
        FakeCredentialStore store = new(existing, Secret());
        ScriptedMetadataHandler handler = new(MetadataScenario.Success);
        OpenRouterPremiumHttpMetadataVerifier verifier = OpenRouterPremiumHttpMetadataVerifier.CreateForOfflineTests(
            store, new FakeClock(ParsedNow), () => handler);

        OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(async () =>
            await verifier.VerifyOnceAsync(CancellationToken.None));
        Assert.Equal("credential_account_mismatch", error.Code);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(existing, store.AccountForMetadata);
    }

    private static OpenRouterPremiumProductionBridge Bridge(string root, FakeCredentialStore store,
        FakeProtector protector, FakeClock clock, ScriptedHandler handler, string? metadataAccount = null) =>
        new(root, store, protector, clock, new FakeMetadataVerifier(Bundle(metadataAccount ?? store.AccountForMetadata)),
            () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(handler));

    [Theory]
    [InlineData("state_trust_anchor_missing")]
    [InlineData("state_trust_anchor_invalid")]
    public async Task V2MissingOrInvalidAnchorFailsBeforeRootMetadataCredentialOrExchange(string code)
    {
        string local = Temp();
        string root = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        FakeCredentialStore credentials = new(Account('a'), Secret());
        int metadataFactories = 0; int exchanges = 0;
        OpenRouterPremiumV2ProductionBridge bridge = new(local, root,
            new FailingAnchorSource(code), OpenRouterPremiumV2StateStoreFactory.Instance,
            credentials, new FakeProtector(), new FakeClock(ParsedNow),
            () => { metadataFactories++; return new FakeMetadataVerifier(Bundle(Account('a'))); },
            () => { exchanges++; return OpenRouterPremiumHttpExchange.CreateForOfflineTests(
                new ScriptedHandler(ScriptedResponse.Success)); });
        try
        {
            OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(
                async () => await bridge.PreflightAsync());
            Assert.Equal(code, error.Code);
            Assert.False(Directory.Exists(root));
            Assert.Equal(0, credentials.ReadCount);
            Assert.Equal(0, metadataFactories);
            Assert.Equal(0, exchanges);
        }
        finally { Delete(local); }
    }

    [Fact]
    public async Task V2PreexistingReparseAncestorCannotRedirectAnyRootMutation()
    {
        string local = Temp(); string outside = Temp();
        string societies = Path.Combine(local, "Societies");
        string root = Path.Combine(societies, "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        Directory.CreateSymbolicLink(societies, outside);
        List<string> order = [];
        FakeAnchorSource anchors = new(order);
        FakeCredentialStore credentials = new(Account('a'), Secret());
        int metadataFactories = 0; int exchanges = 0;
        OpenRouterPremiumV2ProductionBridge bridge = new(local, root, anchors,
            OpenRouterPremiumV2StateStoreFactory.Instance, credentials,
            new FakeProtector(), new FakeClock(ParsedNow),
            () => { metadataFactories++; return new FakeMetadataVerifier(Bundle(Account('a'))); },
            () => { exchanges++; return OpenRouterPremiumHttpExchange.CreateForOfflineTests(
                new ScriptedHandler(ScriptedResponse.Success)); });
        try
        {
            OpenRouterPremiumProductionException error = await Assert.ThrowsAsync<OpenRouterPremiumProductionException>(
                async () => await bridge.PreflightAsync());
            Assert.Equal("state_root_invalid", error.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
            Assert.Equal(1, anchors.OpenCalls);
            Assert.Equal(1, anchors.DisposeCalls);
            Assert.Equal(0, credentials.ReadCount);
            Assert.Equal(0, metadataFactories);
            Assert.Equal(0, exchanges);
        }
        finally
        {
            try { if (Directory.Exists(societies)) Directory.Delete(societies); } catch { }
            Delete(local); Delete(outside);
        }
    }

    [Fact]
    public void V2PinnedSegmentRejectsInPlaceReparseMutationHandleBeforeNextChildCreation()
    {
        const int ErrorSharingViolation = 32;
        string local = Temp(); string outside = Temp();
        string societies = Path.Combine(local, "Societies");
        string root = Path.Combine(societies, "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        Directory.CreateDirectory(societies);
        bool mutationHandleWasInvalid = false;
        int mutationOpenError = 0;
        int mutationAttempts = 0;
        OpenRouterPremiumV2StateStoreFactory factory = new(path =>
        {
            if (!string.Equals(path, societies, StringComparison.Ordinal)) return;
            using SafeFileHandle mutationHandle = CreateFileForMutationAttempt(path,
                0x40000000u, // GENERIC_WRITE is required by FSCTL_SET_REPARSE_POINT.
                FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, 3,
                0x02000000u | 0x00200000u, IntPtr.Zero);
            mutationHandleWasInvalid = mutationHandle.IsInvalid;
            mutationOpenError = Marshal.GetLastWin32Error();
            mutationAttempts++;
        });
        FakeAnchorSource anchors = new([]);
        try
        {
            using IOpenRouterPremiumStateTrustAnchorLease anchor = anchors.OpenExisting();
            _ = factory.Open(local, root, anchor);
            Assert.Equal(1, mutationAttempts);
            Assert.True(mutationHandleWasInvalid);
            Assert.Equal(ErrorSharingViolation, mutationOpenError);
            Assert.True(Directory.Exists(Path.Combine(societies, "SnowGlobe")));
            Assert.False(Directory.Exists(Path.Combine(outside, "SnowGlobe")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
        }
        finally { Delete(local); Delete(outside); }
    }

    [Fact]
    public async Task V2CompositionUsesFixedRootAndCompletesOfflinePreflightRecordValidation()
    {
        string local = Temp();
        string root = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        string v1Sentinel = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v1");
        Directory.CreateDirectory(v1Sentinel);
        string sentinel = Path.Combine(v1Sentinel, "never-observed.sentinel");
        File.WriteAllText(sentinel, "v1-must-remain-untouched");
        List<string> order = [];
        FakeAnchorSource anchors = new(order);
        TrackingV2StoreFactory stores = new(order);
        FakeCredentialStore credentials = new(Account('a'), Secret());
        string? authority = null;
        FakeProtector protector = new(() => Assert.True(File.Exists(Path.Combine(root,
            "execution-consumed", authority + ".json"))));
        FakeClock clock = new(ParsedNow);
        ScriptedHandler handler = new(ScriptedResponse.Success);
        OpenRouterPremiumV2ProductionBridge bridge = new(local, root, anchors, stores,
            credentials, protector, clock,
            () =>
            {
                order.Add("metadata");
                Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "generations"),
                    "preflight-started.json", SearchOption.AllDirectories));
                return new FakeMetadataVerifier(Bundle(Account('a')));
            },
            () =>
            {
                Assert.True(File.Exists(Path.Combine(root, "execution-consumed", authority + ".json")));
                return OpenRouterPremiumHttpExchange.CreateForOfflineTests(handler);
            });
        try
        {
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            authority = preflight.AuthorizationDigestSha256;
            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(
                preflight.AuthorizationDigestSha256);
            OpenRouterPremiumProductionValidationResult validation = bridge.ValidateOnce(
                preflight.AuthorizationDigestSha256);

            Assert.Equal("complete", run.Status);
            Assert.Equal(12, run.ExchangeCount);
            Assert.Equal(run.EvidenceArtifactDigestSha256, validation.EvidenceArtifactDigestSha256);
            Assert.Equal(12, handler.CallCount);
            Assert.Equal(3, anchors.OpenCalls);
            Assert.Equal(3, anchors.DisposeCalls);
            Assert.Equal(3, stores.OpenCalls);
            Assert.Equal(new[] { "anchor", "root", "metadata" }, order.Take(3));
            Assert.All(stores.Roots, value => Assert.Equal(root, value));
            Assert.True(File.Exists(sentinel));
            Assert.Equal("v1-must-remain-untouched", File.ReadAllText(sentinel));
            byte[] secret = Secret();
            try
            {
                Assert.All(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories), file =>
                    Assert.Equal(-1, File.ReadAllBytes(file).AsSpan().IndexOf(secret)));
            }
            finally { CryptographicOperations.ZeroMemory(secret); }
        }
        finally { Delete(local); }
    }

    [Fact]
    public async Task V2CompositionRetainsTheRawFreeParserDiagnosticThroughRunAndValidation()
    {
        const string diagnostic = "provider_response_rejected_response_finish_reason_not_stop";
        string local = Temp();
        string root = Path.Combine(local, "Societies", "SnowGlobe", "OpenRouterPremiumOneShot", "v2");
        List<string> order = [];
        FakeAnchorSource anchors = new(order);
        TrackingV2StoreFactory stores = new(order);
        ScriptedHandler handler = new(ScriptedResponse.NonStopFinishReason);
        OpenRouterPremiumV2ProductionBridge bridge = new(local, root, anchors, stores,
            new FakeCredentialStore(Account('a'), Secret()), new FakeProtector(), new FakeClock(ParsedNow),
            () => new FakeMetadataVerifier(Bundle(Account('a'))),
            () => OpenRouterPremiumHttpExchange.CreateForOfflineTests(handler));
        try
        {
            OpenRouterPremiumProductionPreflightResult preflight = await bridge.PreflightAsync();
            OpenRouterPremiumProductionRunResult run = await bridge.RecordOnceAsync(
                preflight.AuthorizationDigestSha256);
            OpenRouterPremiumProductionValidationResult validation = bridge.ValidateOnce(
                preflight.AuthorizationDigestSha256);

            Assert.Equal("terminal", run.Status);
            Assert.Equal(1, run.ExchangeCount);
            Assert.Equal(0, run.TotalSettledMicrousd);
            Assert.Equal(diagnostic, run.TerminalCode);
            Assert.Equal("terminal", validation.Status);
            Assert.Equal(1, validation.ExchangeCount);
            Assert.Equal(run.EvidenceArtifactDigestSha256, validation.EvidenceArtifactDigestSha256);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(3, anchors.OpenCalls);
            Assert.Equal(3, anchors.DisposeCalls);
            Assert.Equal(3, stores.OpenCalls);
        }
        finally { Delete(local); }
    }

    private static byte[] Bundle(string account, string expiresAtUtc = "2026-08-22T12:00:00Z")
    {
        const string observed = "2026-08-21T12:00:00Z";
        OpenRouterPremiumCatalogSnapshot catalog = new(OpenRouterPremiumHttpMetadataVerifier.ModelsUri, "GET", "Bearer", true, 200, "data",
            OpenRouterPremiumProfile.ModelIdentity, OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity, OpenRouterPremiumProfile.ContextLengthTokens,
            "0.0000002", "0.0000012", 272_000, "0.0000004", "0.0000018",
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, true, false,
            ["max_completion_tokens", "reasoning", "response_format", "structured_outputs"], OpenRouterPremiumProfile.ProviderSlug, true, true);
        OpenRouterPremiumCredentialSourceAttestation attestation = new("snow_globe_openrouter_credential_source_attestation/v1",
            OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
            OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity, account,
            true, true, true, false, "openrouter.chat.completions/one-shot-12", observed, expiresAtUtc,
            "production-attestation-test-v1");
        OpenRouterPremiumActivationBundle bundle = new(OpenRouterPremiumActivationPreflightCodec.SchemaVersion, observed, account,
            OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity,
            "production-preflight-" + Guid.NewGuid().ToString("N"), 12, 18_000, catalog, attestation, string.Empty);
        return OpenRouterPremiumActivationPreflightCodec.Write(bundle);
    }

    private static byte[] Secret() => Encoding.ASCII.GetBytes("sk-or-v1-" + new string('a', 64));
    private static string Account(char value) => "byok-account-sha256-" + new string(value, 64);
    private static string Temp() { string path = Path.Combine(Path.GetTempPath(), "societies-openrouter-production-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Delete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { } }
    private static void CreateHardLinkExact(string alias, string existing)
    {
        if (!CreateHardLink(alias, existing, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileForMutationAttempt(
        string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    private sealed class FakeClock(long now) : IOpenRouterPremiumProductionClock
    {
        private long _now = now;
        public long NowMilliseconds => Interlocked.Read(ref _now);
        public void Advance(long milliseconds) => Interlocked.Add(ref _now, milliseconds);
    }

    private sealed class CancellingClock(long now, Action cancel) : IOpenRouterPremiumProductionClock
    {
        public long NowMilliseconds { get { cancel(); return now; } }
    }

    private sealed class FakeReadinessClock(long now) : IProviderReadinessClock
    {
        public long NowMilliseconds => now;
    }

    private sealed class FakeProtector(Action? beforeUnprotect = null) : IOpenRouterPremiumProductionProtector
    {
        internal int ProtectCalls { get; private set; }
        public byte[] Protect(ReadOnlySpan<byte> plaintext) { ProtectCalls++; return plaintext.ToArray(); }
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
        {
            beforeUnprotect?.Invoke(); return ciphertext.ToArray();
        }
    }

    private sealed class FakeMetadataVerifier(byte[] bundle, Action? beforeReturn = null) : IOpenRouterPremiumProductionMetadataVerifier
    {
        public ValueTask<OpenRouterPremiumVerifiedMetadata> VerifyOnceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); beforeReturn?.Invoke();
            return ValueTask.FromResult(new OpenRouterPremiumVerifiedMetadata(bundle.ToArray()));
        }
    }

    private sealed class FailingAnchorSource(string code) : IOpenRouterPremiumStateTrustAnchorLeaseSource
    {
        public IOpenRouterPremiumStateTrustAnchorLease OpenExisting() =>
            throw new OpenRouterPremiumProductionException(code);
    }

    private sealed class FakeAnchorSource(List<string> order) : IOpenRouterPremiumStateTrustAnchorLeaseSource
    {
        public int OpenCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public IOpenRouterPremiumStateTrustAnchorLease OpenExisting()
        {
            OpenCalls++; order.Add("anchor");
            byte[] key = Enumerable.Repeat((byte)0x61, 32).ToArray();
            try { return new OpenRouterPremiumWindowsStateTrustAnchor(key, _ => DisposeCalls++); }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
    }

    private sealed class TrackingV2StoreFactory(List<string> order) : IOpenRouterPremiumV2StateStoreFactory
    {
        public int OpenCalls { get; private set; }
        public List<string> Roots { get; } = [];
        public OpenRouterPremiumStateGenerationStore Open(string localApplicationDataRoot,
            string fixedV2Root, IOpenRouterPremiumStateTrustAnchor trustAnchor)
        {
            OpenCalls++; Roots.Add(fixedV2Root); order.Add("root");
            return OpenRouterPremiumV2StateStoreFactory.Instance.Open(
                localApplicationDataRoot, fixedV2Root, trustAnchor);
        }
    }

    private enum DpapiFailure { SecondPin, PartialOutputCopy }

    private sealed class FaultingDpapiOperations(DpapiFailure failure) : IOpenRouterPremiumDpapiOperations
    {
        private int _pinCalls;
        internal byte[]? OwnedAuthorization { get; private set; }
        internal byte[]? PartialOutput { get; private set; }
        internal List<FakeDpapiPin> Pins { get; } = [];
        internal int NativeFreeCalls { get; private set; }

        public IOpenRouterPremiumDpapiPin Pin(byte[] buffer)
        {
            _pinCalls++;
            if (_pinCalls == 1) OwnedAuthorization = buffer;
            if (failure == DpapiFailure.SecondPin && _pinCalls == 2)
                throw new InvalidOperationException("injected_second_pin_failure");
            FakeDpapiPin pin = new(new IntPtr(_pinCalls)); Pins.Add(pin); return pin;
        }

        public OpenRouterPremiumDpapiNativeResult Transform(
            bool protect, IntPtr input, int inputLength, IntPtr entropy, int entropyLength) =>
            new(true, new IntPtr(0x1234), 64);

        public void Copy(IntPtr source, byte[] destination, int length)
        {
            PartialOutput = destination;
            destination.AsSpan(0, 19).Fill(0x6b);
            throw new InvalidOperationException("injected_partial_output_copy_failure");
        }

        public void ZeroAndFree(IntPtr source, int length) => NativeFreeCalls++;
    }

    private sealed class FakeDpapiPin(IntPtr address) : IOpenRouterPremiumDpapiPin
    {
        public IntPtr Address { get; } = address;
        internal bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeCredentialStore : IOpenRouterPremiumCredentialStore
    {
        private string _account; private byte[] _secret; private readonly bool _missing;
        internal FakeCredentialStore(string account, byte[] secret, bool missing = false) { _account = account; _secret = secret.ToArray(); _missing = missing; }
        internal static FakeCredentialStore Missing() => new(Account('a'), Secret(), true);
        internal int ReadCount { get; private set; }
        internal string AccountForMetadata => _account;
        internal int ZeroObservationCount { get; private set; }
        internal bool AllObservedZero { get; private set; } = true;
        internal string LastOperationSummary { get; private set; } = "none";
        internal bool SecretMatches(ReadOnlySpan<byte> expected) => _secret.AsSpan().SequenceEqual(expected);
        public void Write(string accountBindingIdentity, byte[] secretMaterial)
        {
            _account = accountBindingIdentity; CryptographicOperations.ZeroMemory(_secret); _secret = secretMaterial.ToArray();
            LastOperationSummary = "credential_written_raw_free";
        }
        public OpenRouterPremiumStoredCredential Read()
        {
            ReadCount++;
            if (_missing) throw new OpenRouterPremiumProductionException("credential_missing");
            return new(_account, _secret.ToArray(), zeroed => { ZeroObservationCount++; AllObservedZero &= zeroed; });
        }
        public void BindAccount(
            string derivedAccountBindingIdentity,
            string snapshotAccountBindingIdentity,
            byte[] snapshotSecretMaterial)
        {
            if (snapshotAccountBindingIdentity != OpenRouterPremiumWindowsCredentialStore.PendingAccountBindingIdentity
                && snapshotAccountBindingIdentity != derivedAccountBindingIdentity)
                throw new OpenRouterPremiumProductionException("credential_account_mismatch");
            Write(derivedAccountBindingIdentity, snapshotSecretMaterial);
        }
        public void Delete()
        {
            CryptographicOperations.ZeroMemory(_secret); _secret = [];
        }
    }

    public enum ScriptedResponse
    {
        Success,
        Http503,
        CostAboveCeiling,
        MalformedSchema,
        InvalidChoiceIndex,
        MissingFinishReason,
        WrongTypeFinishReason,
        NonStopFinishReason,
        ChoiceErrorPresent,
        WrongTypeNativeFinishReason,
        NullNativeFinishReason,
        NonStopNativeFinishReason,
        NonNullLogprobs,
        NonNullRefusal,
        InvalidRouting,
        InvalidJson,
        InvalidShape,
        InvalidUsage,
        UsageByokTrue,
        DocumentedUsageAdditions,
        InvalidProposal,
        InvalidContentType,
        UnexpectedExchangeFailure
    }

    public enum MetadataScenario
    {
        Success, OfficialCurrentKeyExample, UnlimitedKey, PartiallyUnlimitedKey, InvalidLimitReset, MissingAccountSubject,
        InsufficientLimit, InsufficientHardLimit, ManagementKey, ExpiringKey,
        KeyHttp503, ModelsHttp503, ZdrHttp503,
        EffectiveUriMismatch, Oversized, DeepJson, DuplicateKeyProperty, UnknownKeyProperty,
        NegativeByokUsage, WrongKindByokUsage, OverflowByokUsage,
        MissingExpiry, NullExpiry, MalformedExpiry, NonUtcExpiry, WrongKindExpiry,
        UndatedCatalogSlug, ModelMissingReasoningParameter, WrongZdrProvider,
        OnlyHigherPricedZdrVariants, ZdrMissingReasoningParameter
    }

    private sealed class ScriptedMetadataHandler(MetadataScenario scenario, Action<int>? onCall = null) : HttpMessageHandler
    {
        internal int CallCount { get; private set; }
        internal List<string> RequestUris { get; } = [];
        internal List<HttpMethod> Methods { get; } = [];
        internal List<string?> AuthorizationSchemes { get; } = [];
        internal List<string?> AuthorizationParameters { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++; RequestUris.Add(request.RequestUri!.AbsoluteUri); Methods.Add(request.Method);
            AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
            AuthorizationParameters.Add(request.Headers.Authorization?.Parameter);
            bool terminalStatus = scenario == MetadataScenario.KeyHttp503 && CallCount == 1
                || scenario == MetadataScenario.ModelsHttp503 && CallCount == 2
                || scenario == MetadataScenario.ZdrHttp503 && CallCount == 3;
            HttpStatusCode status = terminalStatus
                ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            byte[] body = CallCount switch
            {
                1 => KeyBody(scenario),
                2 => ModelsBody(scenario),
                3 => ZdrBody(scenario),
                _ => throw new InvalidOperationException("metadata request count exceeded")
            };
            HttpRequestMessage effective = scenario == MetadataScenario.EffectiveUriMismatch && CallCount == 1
                ? new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/") : request;
            HttpResponseMessage response = new(status)
            {
                RequestMessage = effective,
                Content = new ByteArrayContent(body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            onCall?.Invoke(CallCount);
            return Task.FromResult(response);
        }

        private static byte[] KeyBody(MetadataScenario scenario) => scenario switch
        {
            MetadataScenario.Oversized => new byte[OpenRouterPremiumHttpMetadataVerifier.MaximumKeyResponseBytes + 1],
            MetadataScenario.DeepJson => Encoding.UTF8.GetBytes("{\"data\":{\"creator_user_id\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":0}}}}}}}}"),
            MetadataScenario.DuplicateKeyProperty => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace("\"label\":", "\"label\":\"duplicate\",\"label\":", StringComparison.Ordinal)),
            MetadataScenario.UnknownKeyProperty => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace("\"label\":", "\"unknown\":0,\"label\":", StringComparison.Ordinal)),
            MetadataScenario.OfficialCurrentKeyExample => Encoding.UTF8.GetBytes(OfficialCurrentKeyJson()),
            MetadataScenario.NegativeByokUsage => Encoding.UTF8.GetBytes(OfficialCurrentKeyJson()
                .Replace("\"byok_usage\":0.001", "\"byok_usage\":-0.001", StringComparison.Ordinal)),
            MetadataScenario.WrongKindByokUsage => Encoding.UTF8.GetBytes(OfficialCurrentKeyJson()
                .Replace("\"byok_usage\":0.001", "\"byok_usage\":\"0.001\"", StringComparison.Ordinal)),
            MetadataScenario.OverflowByokUsage => Encoding.UTF8.GetBytes(OfficialCurrentKeyJson()
                .Replace("\"byok_usage\":0.001", "\"byok_usage\":79228162514264337593543950336", StringComparison.Ordinal)),
            MetadataScenario.MissingAccountSubject => Encoding.UTF8.GetBytes("{\"data\":{\"label\":\"snowglobe-one-shot\",\"limit\":0.018,\"limit_remaining\":0.018,\"usage\":0,\"expires_at\":\"2026-08-21T13:00:00Z\",\"is_management_key\":false}}"),
            MetadataScenario.InsufficientLimit => Encoding.UTF8.GetBytes(KeyJson("0.017", false, "2026-08-21T13:00:00Z")),
            MetadataScenario.InsufficientHardLimit => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z", "0.017")),
            MetadataScenario.UnlimitedKey => Encoding.UTF8.GetBytes(UnlimitedKeyJson()),
            MetadataScenario.PartiallyUnlimitedKey => Encoding.UTF8.GetBytes(KeyJson("null", false, "2026-08-21T13:00:00Z")),
            MetadataScenario.InvalidLimitReset => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace("}}", ",\"limit_reset\":\"hourly\"}}", StringComparison.Ordinal)),
            MetadataScenario.ManagementKey => Encoding.UTF8.GetBytes(KeyJson("0.018", true, "2026-08-21T13:00:00Z")),
            MetadataScenario.ExpiringKey => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T12:04:59Z")),
            MetadataScenario.MissingExpiry => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace(",\"expires_at\":\"2026-08-21T13:00:00Z\"", string.Empty, StringComparison.Ordinal)),
            MetadataScenario.NullExpiry => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace("\"expires_at\":\"2026-08-21T13:00:00Z\"", "\"expires_at\":null", StringComparison.Ordinal)),
            MetadataScenario.MalformedExpiry => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "not-a-date")),
            MetadataScenario.NonUtcExpiry => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T06:00:00-07:00")),
            MetadataScenario.WrongKindExpiry => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z")
                .Replace("\"expires_at\":\"2026-08-21T13:00:00Z\"", "\"expires_at\":1777122000", StringComparison.Ordinal)),
            _ => Encoding.UTF8.GetBytes(KeyJson("0.018", false, "2026-08-21T13:00:00Z"))
        };

        private static string KeyJson(string remaining, bool management, string expiry, string limit = "0.018") =>
            "{\"data\":{\"creator_user_id\":\"creator-user-offline\",\"label\":\"snowglobe-one-shot\",\"limit\":" + limit + ",\"limit_remaining\":"
            + remaining + ",\"usage\":0,\"expires_at\":\"" + expiry + "\",\"is_management_key\":"
            + management.ToString().ToLowerInvariant() + "}}";

        private static string UnlimitedKeyJson() =>
            "{\"data\":{\"creator_user_id\":\"creator-user-offline\",\"label\":\"snowglobe-one-shot\","
            + "\"limit\":null,\"limit_remaining\":null,\"limit_reset\":null,\"usage\":0,\"expires_at\":null,"
            + "\"is_management_key\":false}}";

        private static string OfficialCurrentKeyJson() =>
            "{\"data\":{\"creator_user_id\":\"creator-user-offline\",\"label\":\"snowglobe-one-shot\"," +
            "\"limit\":0.018,\"limit_remaining\":0.018,\"usage\":0,\"usage_daily\":0,\"usage_weekly\":0,\"usage_monthly\":0," +
            "\"byok_usage\":0.001,\"byok_usage_daily\":0.0001,\"byok_usage_weekly\":0.0002,\"byok_usage_monthly\":0.0003," +
            "\"expires_at\":\"2026-08-21T13:00:00Z\",\"limit_reset\":\"monthly\",\"is_management_key\":false,\"is_free_tier\":false," +
            "\"is_provisioning_key\":false,\"include_byok_in_limit\":true,\"created_at\":\"2026-08-21T12:00:00Z\"," +
            "\"updated_at\":\"2026-08-21T12:00:00Z\",\"rate_limit\":{\"requests\":200,\"interval\":\"10s\",\"note\":\"offline fixture\"}}}";

        private static byte[] ModelsBody(MetadataScenario scenario)
        {
            string canonical = scenario == MetadataScenario.UndatedCatalogSlug
                ? OpenRouterPremiumProfile.CanonicalModelSlug : OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity;
            string body = "{\"data\":[{\"id\":\"" + OpenRouterPremiumProfile.ModelIdentity
                + "\",\"canonical_slug\":\"" + canonical + "\",\"name\":\"GPT-5.6 Luna\",\"created\":1786000000,"
                + "\"description\":\"offline fixture\",\"expiration_date\":null,\"knowledge_cutoff\":null,"
                + "\"links\":{\"details\":\"/api/v1/models/openai/gpt-5.6-luna/endpoints\"},\"supported_voices\":null,"
                + "\"context_length\":1050000,\"benchmarks\":{},\"reasoning\":{},"
                + "\"pricing\":{\"prompt\":\"0.0000002\",\"completion\":\"0.0000012\","
                + "\"overrides\":[{\"min_prompt_tokens\":272000,\"prompt\":\"0.0000004\",\"completion\":\"0.0000018\","
                + "\"input_cache_read\":\"0.00000004\",\"input_cache_write\":\"0.0000005\"}]},"
                + "\"supported_parameters\":[\"max_completion_tokens\",\"reasoning\",\"response_format\",\"structured_outputs\"]}],"
                + "\"links\":{\"next\":null},\"total_count\":1}";
            if (scenario == MetadataScenario.ModelMissingReasoningParameter)
                body = body.Replace("\"max_completion_tokens\",\"reasoning\",", "\"max_completion_tokens\",", StringComparison.Ordinal);
            return Encoding.UTF8.GetBytes(body);
        }

        private static byte[] ZdrBody(MetadataScenario scenario)
        {
            string provider = scenario == MetadataScenario.WrongZdrProvider ? "Other" : "Azure";
            string exact = scenario == MetadataScenario.OnlyHigherPricedZdrVariants ? string.Empty
                : "{\"model_id\":\"" + OpenRouterPremiumProfile.ModelIdentity
                    + "\",\"provider_name\":\"" + provider + "\",\"tag\":\"azure\",\"context_length\":1050000,"
                    + "\"latency_last_30m\":{\"p50\":0.25,\"p75\":0.35,\"p90\":0.48,\"p99\":0.85},"
                    + "\"throughput_last_30m\":{\"p50\":45.2,\"p75\":38.5,\"p90\":28.3,\"p99\":15.1},"
                    + "\"supports_implicit_caching\":true,\"supports_voice_cloning\":false,\"uptime_last_1d\":99.8,\"uptime_last_30m\":99.5,\"uptime_last_5m\":100,"
                    + "\"pricing\":{\"prompt\":\"0.0000002\",\"completion\":\"0.0000012\"},"
                    + "\"supported_parameters\":[\"max_completion_tokens\",\"reasoning\",\"response_format\",\"structured_outputs\"]}";
            string separator = exact.Length == 0 ? string.Empty : ",";
            string variants = "{\"model_id\":\"" + OpenRouterPremiumProfile.ModelIdentity
                + "\",\"provider_name\":\"Azure\",\"tag\":\"azure/eu\",\"context_length\":1050000,"
                + "\"latency_last_30m\":{\"p50\":0.25},\"throughput_last_30m\":{\"p50\":45.2},"
                + "\"supports_implicit_caching\":true,\"supports_voice_cloning\":false,\"uptime_last_1d\":99.8,\"uptime_last_30m\":99.5,\"uptime_last_5m\":100,"
                + "\"pricing\":{\"prompt\":\"0.00000022\",\"completion\":\"0.00000132\"},"
                + "\"supported_parameters\":[\"max_completion_tokens\",\"reasoning\",\"response_format\",\"structured_outputs\"]},"
                + "{\"model_id\":\"" + OpenRouterPremiumProfile.ModelIdentity
                + "\",\"provider_name\":\"Azure\",\"tag\":\"azure/us\",\"context_length\":1050000,"
                + "\"latency_last_30m\":{\"p50\":0.25},\"throughput_last_30m\":{\"p50\":45.2},"
                + "\"supports_implicit_caching\":true,\"supports_voice_cloning\":false,\"uptime_last_1d\":99.8,\"uptime_last_30m\":99.5,\"uptime_last_5m\":100,"
                + "\"pricing\":{\"prompt\":\"0.00000022\",\"completion\":\"0.00000132\"},"
                + "\"supported_parameters\":[\"max_completion_tokens\",\"reasoning\",\"response_format\",\"structured_outputs\"]}";
            string body = "{\"data\":[" + exact + separator + variants + "]}";
            if (scenario == MetadataScenario.ZdrMissingReasoningParameter)
                body = body.Replace("\"max_completion_tokens\",\"reasoning\",", "\"max_completion_tokens\",", StringComparison.Ordinal);
            return Encoding.UTF8.GetBytes(body);
        }
    }

    private sealed class ScriptedHandler(ScriptedResponse response, Action? afterResponse = null) : HttpMessageHandler
    {
        private int _active;
        internal int CallCount { get; private set; }
        internal int MaximumActive { get; private set; }
        internal string? LastResponseDigestSha256 { get; private set; }
        internal List<string> RequestUris { get; } = [];
        internal List<string?> AuthorizationSchemes { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref _active); MaximumActive = Math.Max(MaximumActive, active);
            try
            {
                CallCount++; RequestUris.Add(request.RequestUri!.AbsoluteUri); AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
                if (response == ScriptedResponse.UnexpectedExchangeFailure)
                    throw new InvalidOperationException("raw-provider-sentinel-must-not-leak");
                byte[] body; HttpStatusCode status;
                switch (response)
                {
                    case ScriptedResponse.Http503:
                        status = HttpStatusCode.ServiceUnavailable;
                        body = Encoding.UTF8.GetBytes("{\"error\":{\"code\":503,\"message\":\"terminal\"}}");
                        break;
                    case ScriptedResponse.CostAboveCeiling:
                        status = HttpStatusCode.OK; body = SuccessBody("0.100000"); break;
                    case ScriptedResponse.MalformedSchema:
                        status = HttpStatusCode.OK; body = Encoding.UTF8.GetBytes("{\"id\":\"bad\"}"); break;
                    case ScriptedResponse.InvalidChoiceIndex:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"index\":0", "\"index\":1", StringComparison.Ordinal)); break;
                    case ScriptedResponse.MissingFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            ",\"finish_reason\":\"stop\"", string.Empty, StringComparison.Ordinal)); break;
                    case ScriptedResponse.WrongTypeFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":42", StringComparison.Ordinal)); break;
                    case ScriptedResponse.NonStopFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"raw-provider-sentinel-must-not-leak\"", StringComparison.Ordinal)); break;
                    case ScriptedResponse.ChoiceErrorPresent:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"error\":{\"message\":\"raw-provider-sentinel-must-not-leak\"}", StringComparison.Ordinal)); break;
                    case ScriptedResponse.WrongTypeNativeFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"native_finish_reason\":42", StringComparison.Ordinal)); break;
                    case ScriptedResponse.NullNativeFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"native_finish_reason\":null", StringComparison.Ordinal)); break;
                    case ScriptedResponse.NonStopNativeFinishReason:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"native_finish_reason\":\"bounded-provider-metadata\"", StringComparison.Ordinal)); break;
                    case ScriptedResponse.NonNullLogprobs:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"logprobs\":{\"marker\":\"raw-provider-sentinel-must-not-leak\"}", StringComparison.Ordinal)); break;
                    case ScriptedResponse.NonNullRefusal:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"message\":{\"role\":\"assistant\"", "\"message\":{\"role\":\"assistant\",\"refusal\":\"raw-provider-sentinel-must-not-leak\"", StringComparison.Ordinal)); break;
                    case ScriptedResponse.InvalidRouting:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"attempt\":1", "\"attempt\":2", StringComparison.Ordinal)); break;
                    case ScriptedResponse.InvalidJson:
                        status = HttpStatusCode.OK; body = Encoding.UTF8.GetBytes("{\"id\":@}"); break;
                    case ScriptedResponse.InvalidShape:
                        status = HttpStatusCode.OK; body = Encoding.UTF8.GetBytes("[]"); break;
                    case ScriptedResponse.InvalidUsage:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"total_tokens\":120", "\"total_tokens\":121", StringComparison.Ordinal)); break;
                    case ScriptedResponse.UsageByokTrue:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"cost\":0.000044", "\"cost\":0.000044,\"is_byok\":true", StringComparison.Ordinal)); break;
                    case ScriptedResponse.DocumentedUsageAdditions:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "\"cost\":0.000044",
                            "\"cost\":0.000044,\"is_byok\":false," +
                            "\"server_tool_use_details\":{\"tool_calls_executed\":0,\"tool_calls_requested\":0}," +
                            "\"cost_details\":{\"upstream_inference_cost\":0.000040," +
                            "\"upstream_inference_prompt_cost\":0.000010,\"upstream_inference_completions_cost\":0.000020}",
                            StringComparison.Ordinal)); break;
                    case ScriptedResponse.InvalidProposal:
                        status = HttpStatusCode.OK; body = MutatedSuccess(value => value.Replace(
                            "GatherWood", "BreakWorld", StringComparison.Ordinal)); break;
                    default:
                        status = HttpStatusCode.OK; body = SuccessBody("0.000044"); break;
                }
                LastResponseDigestSha256 = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
                HttpResponseMessage result = new(status)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }
                };
                if (response == ScriptedResponse.InvalidContentType)
                    result.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                afterResponse?.Invoke();
                return Task.FromResult(result);
            }
            finally { Interlocked.Decrement(ref _active); }
        }

        private static byte[] SuccessBody(string cost) => Encoding.UTF8.GetBytes(
            "{\"id\":\"gen-offline\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"openai/gpt-5.6-luna\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"agent_id\\\":\\\"agent-00\\\",\\\"action\\\":\\\"GatherWood\\\",\\\"quantity\\\":12}\"}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":20,\"total_tokens\":120,\"cost\":" + cost + "},\"openrouter_metadata\":{\"requested\":\"openai/gpt-5.6-luna\",\"strategy\":\"direct\",\"attempt\":1,\"is_byok\":false,\"endpoints\":{\"total\":1,\"available\":[{\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"selected\":true}]},\"attempts\":[{\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"status\":200}],\"pipeline\":[]}}");

        private static byte[] MutatedSuccess(Func<string, string> mutate) =>
            Encoding.UTF8.GetBytes(mutate(Encoding.UTF8.GetString(SuccessBody("0.000044"))));
    }
}
