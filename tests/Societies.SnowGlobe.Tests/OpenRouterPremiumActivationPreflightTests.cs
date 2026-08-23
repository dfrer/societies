using System.Text;
using System.Reflection;
using System.Globalization;
using System.Security.Cryptography;
using System.Buffers;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumActivationPreflightTests
{
    private static int _nonce;

    [Fact]
    public void ExactApprovedEvidenceAndRestartedDurableJournal_AreEligibleButDoNotEnableTraffic()
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs();
            OpenRouterPremiumJournalHeader header = Header(inputs.Account);
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, header))
            {
                Assert.True(created.ProvidesDurableFlush);
                Assert.False(created.RestartEvidence.RestartVerified);
            }

            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            Assert.True(reopened.RestartEvidence.RestartVerified);
            OpenRouterPremiumActivationPreflightCapability capability =
                OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle);
            Assert.Equal(272_000, capability.Bundle.Catalog.PricingOverrideMinimumPromptTokens);
            Assert.True(capability.Bundle.Catalog.ApiModelIdVerified);
            Assert.Equal(OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, capability.Bundle.Catalog.FrozenCatalogEvidenceDigestSha256);
            Assert.True(capability.Bundle.Catalog.PricingOverrideAuthenticatedCurrent);
            Assert.False(capability.Bundle.Catalog.PricingOverrideReachableForProfile);
            Assert.True(capability.Bundle.Catalog.PricingOverrideMinimumPromptTokens
                > OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumInputTokens);
            OpenRouterPremiumActivationPreflightArtifact artifact =
                OpenRouterPremiumActivationPreflightModule.EvaluateOnce(capability, reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds);

            Assert.True(artifact.Eligible);
            Assert.Equal("eligible_for_separately_authorized_one_shot", artifact.Decision);
            Assert.False(artifact.LiveTrafficEnabled);
            Assert.Empty(artifact.BlockerCodes);
            Assert.Equal(12, artifact.MaximumRequests);
            Assert.Equal(18_000, artifact.AggregateCostCeilingMicrousd);
            Assert.Equal(OpenRouterPremiumProfile.CanonicalModelSlug, artifact.CanonicalModelSlug);
            Assert.Equal(inputs.Account.Value, artifact.AccountBindingIdentity);
            Assert.Equal(inputs.SourceIdentity, artifact.CredentialSourceIdentity);
            Assert.Matches("^[0-9a-f]{64}$", artifact.DurableConsumptionEvidenceDigestSha256);
            Assert.Equal(1, reopened.RestartEvidence.RecordCount);
            Assert.False(reopened.RestartEvidence.RestartVerified);
            Assert.Empty(reopened.Snapshot().Slots);
            Assert.DoesNotContain(root, artifact.CanonicalJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-sentinel", artifact.CanonicalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("credential_utf8", artifact.CanonicalJson, StringComparison.Ordinal);
            Assert.Equal(artifact.CanonicalDigestSha256,
                OpenRouterPremiumActivationPreflightArtifactModule.Validate(artifact.CanonicalUtf8).CanonicalDigestSha256);
            string canonical = artifact.CanonicalJson;
            AssertArtifactRejected(canonical.Replace("\"schema_version\":", "\"unknown\":0,\"schema_version\":", StringComparison.Ordinal));
            AssertArtifactRejected(canonical.Replace("\"schema_version\":", "\"schema_version\":\"duplicate\",\"schema_version\":", StringComparison.Ordinal));
            AssertArtifactRejected(canonical + "\n");
            AssertArtifactRejected(new string(' ', OpenRouterPremiumActivationPreflightArtifactModule.MaximumArtifactBytes + 1));
            Assert.False(OpenRouterPremiumProfileRegistry.Selected.LiveTrafficEnabled);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void UnauthenticatedOrUnverifiedCurrentEvidence_FailsClosedWithExactReasons()
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs(authenticated: false, callable: false, zdr: false);
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);

            OpenRouterPremiumActivationPreflightArtifact artifact = OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle), reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds);

            Assert.False(artifact.Eligible);
            Assert.Equal("ineligible", artifact.Decision);
            Assert.Equal(new[]
            {
                "catalog_authentication_unverified",
                "api_model_id_unverified",
                "zdr_route_unverified"
            }, artifact.BlockerCodes);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("account")]
    [InlineData("source")]
    [InlineData("ceiling")]
    [InlineData("count")]
    [InlineData("date")]
    [InlineData("price")]
    [InlineData("slug")]
    [InlineData("provider")]
    [InlineData("attestation")]
    [InlineData("threshold")]
    [InlineData("override_prompt")]
    [InlineData("override_completion")]
    [InlineData("frozen_evidence")]
    [InlineData("override_auth_stale")]
    [InlineData("override_reachable")]
    public void SemanticMismatchMatrix_IsCanonicalButIneligible(string mutation)
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs(mutation: mutation);
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumActivationPreflightArtifact artifact = OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle), reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds);
            Assert.False(artifact.Eligible);
            Assert.NotEmpty(artifact.BlockerCodes);
            Assert.False(artifact.LiveTrafficEnabled);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void BundleParser_RejectsUnknownDuplicateOversizedDeepAndNoncanonicalInput()
    {
        TestInputs inputs = Inputs();
        byte[] canonical = inputs.Bundle.ToArray();
        string json = Encoding.UTF8.GetString(canonical);

        AssertRejected(json.Replace("\"schema_version\":", "\"unknown\":0,\"schema_version\":", StringComparison.Ordinal));
        AssertRejected(json.Replace("\"schema_version\":", "\"schema_version\":\"duplicate\",\"schema_version\":", StringComparison.Ordinal));
        AssertRejected(json.Replace("\"pricing_override_min_prompt_tokens\":272000,", string.Empty, StringComparison.Ordinal));
        AssertRejected(new string(' ', 16 * 1024) + json);
        AssertRejected(json.Replace("\"catalog\":{", "\"catalog\":{\"nested\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":0}}}}},", StringComparison.Ordinal));
        AssertRejected(json + "\n");
    }

    [Fact]
    public void CancellationAndReplay_ConsumeBeforeJournalInspection()
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumActivationPreflightCapability capability = OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle);
            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();

            Assert.Equal("preflight_cancelled", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                OpenRouterPremiumActivationPreflightModule.EvaluateOnce(capability, reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds, cancelled.Token)).Code);
            Assert.Equal("preflight_capability_consumed", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                OpenRouterPremiumActivationPreflightModule.EvaluateOnce(capability, reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds)).Code);

            OpenRouterPremiumActivationPreflightCapability duplicate = OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle);
            Assert.Equal("preflight_nonce_consumed", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                OpenRouterPremiumActivationPreflightModule.EvaluateOnce(duplicate, reopened, inputs.Trust, inputs.EvaluationTimeUnixMilliseconds)).Code);
            Assert.Empty(reopened.Snapshot().Slots);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void NewOrMismatchedOrNonemptyJournal_IsIneligibleWithoutMutation()
    {
        string freshRoot = Temp();
        string mismatchRoot = Temp();
        string nonemptyRoot = Temp();
        try
        {
            TestInputs freshInputs = Inputs();
            using FileOpenRouterPremiumJournal fresh = FileOpenRouterPremiumJournal.CreateNew(freshRoot, Header(freshInputs.Account));
            Assert.Contains("durable_restart_unverified", Evaluate(freshInputs, fresh).BlockerCodes);

            TestInputs mismatchInputs = Inputs();
            ByokAccountBindingIdentity other = new("byok-account-sha256-" + new string('f', 64));
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(mismatchRoot, Header(other))) { }
            using FileOpenRouterPremiumJournal mismatch = FileOpenRouterPremiumJournal.OpenForAppend(mismatchRoot);
            Assert.Contains("journal_binding_invalid", Evaluate(mismatchInputs, mismatch).BlockerCodes);

            TestInputs nonemptyInputs = Inputs();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(nonemptyRoot, Header(nonemptyInputs.Account)))
                created.Admit(1, "cq1", new string('a', 64), new string('b', 64), 1_500);
            using FileOpenRouterPremiumJournal nonempty = FileOpenRouterPremiumJournal.OpenForAppend(nonemptyRoot);
            int before = nonempty.Snapshot().Slots.Count;
            Assert.Contains("journal_not_empty", Evaluate(nonemptyInputs, nonempty).BlockerCodes);
            Assert.Equal(before, nonempty.Snapshot().Slots.Count);
        }
        finally { Delete(freshRoot); Delete(mismatchRoot); Delete(nonemptyRoot); }
    }

    [Fact]
    public void TrustAttestationHasNoPublicConstructorAndEvaluateRequiresIt()
    {
        Assert.Empty(typeof(OpenRouterPremiumPreflightTrustAttestation).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        MethodInfo evaluate = Assert.Single(typeof(OpenRouterPremiumActivationPreflightModule).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == nameof(OpenRouterPremiumActivationPreflightModule.EvaluateOnce));
        Assert.Contains(evaluate.GetParameters(), parameter => parameter.ParameterType == typeof(OpenRouterPremiumPreflightTrustAttestation));
        Assert.DoesNotContain(typeof(OpenRouterPremiumPreflightTrustAttestation).GetProperties(), property =>
            property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("credentialbytes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrustTokenMutationFailsClosedWithoutJournalMutation()
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumPreflightTrustAttestation wrong = new(
                OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
                new string('0', 64), inputs.Account.Value, inputs.SourceIdentity);
            OpenRouterPremiumActivationPreflightArtifact artifact = OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle), reopened, wrong, inputs.EvaluationTimeUnixMilliseconds);
            Assert.False(artifact.Eligible);
            Assert.Contains("credential_trust_not_registered", artifact.BlockerCodes);
            Assert.Empty(reopened.Snapshot().Slots);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void EligibleConsumptionSurvivesRestartAndDeniesFreshNonceReplay()
    {
        string root = Temp();
        try
        {
            TestInputs first = Inputs();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(first.Account))) { }
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.OpenForAppend(root))
                Assert.True(Evaluate(first, journal).Eligible);

            TestInputs replay = Inputs();
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumActivationPreflightArtifact denied = Evaluate(replay, reopened);
            Assert.False(denied.Eligible);
            Assert.Contains("journal_preflight_already_consumed", denied.BlockerCodes);
            Assert.Null(denied.DurableConsumptionEvidenceDigestSha256);
            Assert.Equal(1, reopened.RestartEvidence.RecordCount);
            Assert.Empty(reopened.Snapshot().Slots);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData(-1, false, "credential_attestation_not_yet_valid")]
    [InlineData(0, true, null)]
    [InlineData(86_399_999, true, null)]
    [InlineData(86_400_000, false, "credential_attestation_expired")]
    public void EvaluationTimeUsesInclusiveStartExclusiveExpiryBoundary(long offsetMilliseconds, bool expectedEligible, string? blocker)
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs();
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            long evaluatedAt = checked(inputs.AttestedAtUnixMilliseconds + offsetMilliseconds);
            OpenRouterPremiumActivationPreflightArtifact artifact = OpenRouterPremiumActivationPreflightModule.EvaluateOnce(
                OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle), reopened, inputs.Trust, evaluatedAt);
            Assert.Equal(expectedEligible, artifact.Eligible);
            if (blocker is null) Assert.DoesNotContain(artifact.BlockerCodes, code => code.Contains("attestation_", StringComparison.Ordinal));
            else Assert.Contains(blocker, artifact.BlockerCodes);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void AuthenticatedEvidenceAcrossUtcMidnightWithinAttestationWindow_IsEligible()
    {
        string root = Temp();
        try
        {
            TestInputs inputs = Inputs(observedAtUtc: "2026-08-22T02:24:21Z");
            using (FileOpenRouterPremiumJournal created = FileOpenRouterPremiumJournal.CreateNew(root, Header(inputs.Account))) { }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);

            OpenRouterPremiumActivationPreflightArtifact artifact = Evaluate(inputs, reopened);

            Assert.True(artifact.Eligible);
            Assert.DoesNotContain("catalog_evidence_date_invalid", artifact.BlockerCodes);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void DetachedCanonicalForgeryIsStructuralOnlyAndCannotConferEligibility()
    {
        byte[] forged = ForgeEligibleArtifact();
        OpenRouterPremiumActivationPreflightArtifact detached = OpenRouterPremiumActivationPreflightArtifactModule.Validate(forged);
        Assert.True(detached.ClaimedEligible);
        Assert.False(detached.Eligible);
        Assert.False(detached.TrustContextValidated);
        Assert.Equal("detached_structure_valid_only", detached.Decision);
        Assert.Equal("eligible_for_separately_authorized_one_shot", detached.ClaimedDecision);
    }

    private static OpenRouterPremiumActivationPreflightArtifact Evaluate(TestInputs inputs, FileOpenRouterPremiumJournal journal) =>
        OpenRouterPremiumActivationPreflightModule.EvaluateOnce(OpenRouterPremiumActivationPreflightModule.Authorize(inputs.Bundle), journal,
            inputs.Trust, inputs.EvaluationTimeUnixMilliseconds);

    private static void AssertRejected(string value) => Assert.Equal("preflight_bundle_rejected",
        Assert.Throws<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumActivationPreflightModule.Authorize(Encoding.UTF8.GetBytes(value))).Code);

    private static void AssertArtifactRejected(string value) => Assert.Equal("preflight_artifact_rejected",
        Assert.Throws<OpenRouterPremiumEvidenceException>(() => OpenRouterPremiumActivationPreflightArtifactModule.Validate(Encoding.UTF8.GetBytes(value))).Code);

    private static OpenRouterPremiumJournalHeader Header(ByokAccountBindingIdentity account) =>
        OpenRouterPremiumJournalHeader.Create("openrouter-premium-journal/preflight", "openrouter-premium-run/preflight",
            OpenRouterPremiumProfileRegistry.Selected, account);

    private static TestInputs Inputs(
        bool authenticated = true,
        bool callable = true,
        bool zdr = true,
        string? mutation = null,
        string? observedAtUtc = null)
    {
        ByokAccountBindingIdentity account = new("byok-account-sha256-" + new string('a', 64));
        string source = "trusted-credential-source/openrouter-one-shot-v1";
        string bundleAccount = mutation == "account" ? "byok-account-sha256-" + new string('b', 64) : account.Value;
        string credentialSource = mutation == "source" ? "trusted-credential-source/other-v1" : source;
        long ceiling = mutation == "ceiling" ? 18_001 : 18_000;
        int requests = mutation == "count" ? 13 : 12;
        string observed = mutation == "date" ? "2026-08-19T23:59:59Z" : observedAtUtc ?? "2026-08-21T12:00:00Z";
        string promptPrice = mutation == "price" ? "0.0000003" : "0.0000002";
        string slug = mutation == "slug" ? OpenRouterPremiumProfile.CanonicalModelSlug : OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity;
        string provider = mutation == "provider" ? "other" : "azure";
        bool sourceTrusted = mutation != "attestation";
        int threshold = mutation == "threshold" ? 4_096 : 272_000;
        string overridePromptPrice = mutation == "override_prompt" ? "0.0000003" : "0.0000004";
        string overrideCompletionPrice = mutation == "override_completion" ? "0.0000017" : "0.0000018";
        string frozenEvidence = mutation == "frozen_evidence" ? new string('0', 64) : OpenRouterPremiumProfile.CatalogEvidenceDigestSha256;
        bool overrideAuthenticatedCurrent = mutation != "override_auth_stale";
        bool overrideReachable = mutation == "override_reachable";
        string nonce = $"openrouter-premium-preflight/test-{Interlocked.Increment(ref _nonce)}";
        byte[] bundle = TestBundleCodec.CreateCanonicalBundle(
            observed, bundleAccount, credentialSource, sourceTrusted, authenticated, callable, zdr,
            slug, provider, promptPrice, "0.0000012", threshold, overridePromptPrice, overrideCompletionPrice,
            frozenEvidence, overrideAuthenticatedCurrent, overrideReachable,
            requests, ceiling, nonce);
        string bundleDigest = Convert.ToHexString(SHA256.HashData(bundle)).ToLowerInvariant();
        OpenRouterPremiumPreflightTrustAttestation trust = new(OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity,
            bundleDigest, bundleAccount, credentialSource);
        long attestedAt = DateTimeOffset.ParseExact(observed, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToUnixTimeMilliseconds();
        string evaluationTimestamp = observedAtUtc ?? "2026-08-21T12:00:00Z";
        long evaluationTime = DateTimeOffset.ParseExact(evaluationTimestamp, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToUnixTimeMilliseconds();
        return new(bundle, account, source, trust, attestedAt, evaluationTime);
    }

    private static string Temp()
    {
        string path = Path.Combine(Path.GetTempPath(), "societies-openrouter-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed record TestInputs(byte[] Bundle, ByokAccountBindingIdentity Account, string SourceIdentity,
        OpenRouterPremiumPreflightTrustAttestation Trust, long AttestedAtUnixMilliseconds, long EvaluationTimeUnixMilliseconds);

    private static class TestBundleCodec
    {
        internal static byte[] CreateCanonicalBundle(
            string observedAtUtc, string accountBindingIdentity, string sourceIdentity, bool sourceTrusted,
            bool authenticated, bool callable, bool zdr, string canonicalSlug, string providerSlug,
            string promptPrice, string completionPrice, int pricingOverrideMinimumPromptTokens,
            string pricingOverridePromptPrice, string pricingOverrideCompletionPrice,
            string frozenCatalogEvidenceDigestSha256, bool pricingOverrideAuthenticatedCurrent,
            bool pricingOverrideReachableForProfile,
            int maximumRequests, long aggregateCeilingMicrousd,
            string authorizationNonce)
        {
            OpenRouterPremiumCatalogSnapshot catalog = new(OpenRouterPremiumHttpMetadataVerifier.ModelsUri, "GET", "Bearer", authenticated, 200, "data",
                OpenRouterPremiumProfile.ModelIdentity, canonicalSlug, OpenRouterPremiumProfile.ContextLengthTokens, promptPrice, completionPrice,
                pricingOverrideMinimumPromptTokens, pricingOverridePromptPrice, pricingOverrideCompletionPrice,
                frozenCatalogEvidenceDigestSha256, pricingOverrideAuthenticatedCurrent, pricingOverrideReachableForProfile,
                ["max_completion_tokens", "reasoning", "response_format", "structured_outputs"], providerSlug, callable, zdr);
            DateTimeOffset observed = DateTimeOffset.ParseExact(observedAtUtc, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            OpenRouterPremiumCredentialSourceAttestation attestation = new("snow_globe_openrouter_credential_source_attestation/v1",
                sourceTrusted ? OpenRouterPremiumActivationPreflightModule.TrustedAttestorIdentity : "untrusted-credential-attestor/test-v1",
                sourceIdentity, accountBindingIdentity, true, true, true, false, "openrouter.chat.completions/one-shot-12",
                observedAtUtc, observed.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                "openrouter-premium-attestation/test-v1");
            OpenRouterPremiumActivationBundle bundle = new(OpenRouterPremiumActivationPreflightCodec.SchemaVersion, observedAtUtc, accountBindingIdentity,
                sourceIdentity, authorizationNonce, maximumRequests, aggregateCeilingMicrousd, catalog, attestation, string.Empty);
            return OpenRouterPremiumActivationPreflightCodec.Write(bundle);
        }
    }


    private static byte[] ForgeEligibleArtifact()
    {
        byte[] payload = WriteForgedArtifact(null);
        string payloadDigest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return WriteForgedArtifact(payloadDigest);
    }

    private static byte[] WriteForgedArtifact(string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion);
        writer.WriteString("decision", "eligible_for_separately_authorized_one_shot");
        writer.WriteBoolean("eligible", true); writer.WriteBoolean("live_traffic_enabled", false);
        writer.WriteString("profile_digest_sha256", OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256);
        writer.WriteString("canonical_model_slug", OpenRouterPremiumProfile.CanonicalModelSlug);
        writer.WriteBoolean("api_model_id_verified", true);
        writer.WriteString("provider_slug", OpenRouterPremiumProfile.ProviderSlug);
        writer.WriteString("evidence_observed_at_utc", "2026-08-21T12:00:00Z");
        writer.WriteNumber("evaluated_at_unix_milliseconds", DateTimeOffset.Parse("2026-08-21T12:00:00Z", CultureInfo.InvariantCulture).ToUnixTimeMilliseconds());
        writer.WriteString("catalog_snapshot_digest_sha256", new string('1', 64));
        writer.WriteString("frozen_catalog_evidence_digest_sha256", OpenRouterPremiumProfile.CatalogEvidenceDigestSha256);
        writer.WriteBoolean("pricing_override_authenticated_current", true);
        writer.WriteBoolean("pricing_override_reachable_for_profile", false);
        writer.WriteString("credential_attestation_digest_sha256", new string('2', 64));
        writer.WriteString("account_binding_identity", "byok-account-sha256-" + new string('a', 64));
        writer.WriteString("credential_source_identity", OpenRouterPremiumActivationPreflightModule.ApprovedCredentialSourceIdentity);
        writer.WriteString("journal_identity", "openrouter-premium-journal/forged");
        writer.WriteString("journal_header_checksum_sha256", new string('3', 64));
        writer.WriteString("durable_restart_evidence_digest_sha256", new string('4', 64));
        writer.WriteString("registered_trust_evidence_digest_sha256", new string('5', 64));
        writer.WriteString("authorization_nonce_digest_sha256", new string('6', 64));
        writer.WriteString("durable_preflight_consumption_evidence_digest_sha256", new string('7', 64));
        writer.WriteNumber("maximum_requests", 12); writer.WriteNumber("per_slot_cost_ceiling_microusd", 1_500);
        writer.WriteNumber("aggregate_cost_ceiling_microusd", 18_000);
        writer.WritePropertyName("blocker_codes"); writer.WriteStartArray(); writer.WriteEndArray();
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray();
        writer.WriteStringValue("attestation_identity_is_approval_input_not_cryptographic_signature");
        writer.WriteStringValue("catalog_snapshot_requires_registered_authenticated_metadata_trust");
        writer.WriteStringValue("pricing_override_is_frozen_content_addressed_evidence_not_authenticated_current_api");
        writer.WriteStringValue("pricing_override_is_unreachable_under_profile_input_bound");
        writer.WriteStringValue("flush_request_and_restart_readback_do_not_prove_physical_media_durability");
        writer.WriteStringValue("preflight_eligibility_does_not_enable_live_traffic_or_authorize_spend");
        writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("preflight_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }
}
