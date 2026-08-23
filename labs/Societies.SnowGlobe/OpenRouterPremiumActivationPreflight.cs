using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed class OpenRouterPremiumActivationPreflightCapability
{
    private int _consumed;
    internal OpenRouterPremiumActivationPreflightCapability(OpenRouterPremiumActivationBundle bundle)
    {
        Bundle = bundle;
        BundleDigestSha256 = bundle.BundleDigestSha256;
    }

    public string BundleDigestSha256 { get; }
    internal OpenRouterPremiumActivationBundle Bundle { get; }
    internal bool TryConsume() => Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;
}

public sealed class OpenRouterPremiumPreflightTrustAttestation
{
    internal OpenRouterPremiumPreflightTrustAttestation(
        string authorityIdentity,
        string bundleDigestSha256,
        string accountBindingIdentity,
        string credentialSourceIdentity)
    {
        AuthorityIdentity = authorityIdentity;
        BundleDigestSha256 = bundleDigestSha256;
        AccountBindingIdentity = accountBindingIdentity;
        CredentialSourceIdentity = credentialSourceIdentity;
        EvidenceDigestSha256 = OpenRouterPremiumCanonical.Digest(string.Join('|', authorityIdentity, bundleDigestSha256,
            accountBindingIdentity, credentialSourceIdentity, "contains_secret_bytes=false"));
    }

    public string AuthorityIdentity { get; }
    public string BundleDigestSha256 { get; }
    public string AccountBindingIdentity { get; }
    public string CredentialSourceIdentity { get; }
    public string EvidenceDigestSha256 { get; }
}

public sealed class OpenRouterPremiumActivationPreflightArtifact
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _blockerCodes;

    internal OpenRouterPremiumActivationPreflightArtifact(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string decision,
        bool eligible,
        IEnumerable<string> blockerCodes,
        string canonicalModelSlug,
        string frozenCatalogEvidenceDigestSha256,
        bool apiModelIdVerified,
        bool pricingOverrideAuthenticatedCurrent,
        bool pricingOverrideReachableForProfile,
        string accountBindingIdentity,
        string credentialSourceIdentity,
        int maximumRequests,
        long aggregateCostCeilingMicrousd,
        string? durableConsumptionEvidenceDigestSha256,
        long evaluatedAtUnixMilliseconds,
        bool trustContextValidated)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _blockerCodes = blockerCodes.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        ClaimedDecision = decision;
        ClaimedEligible = eligible;
        TrustContextValidated = trustContextValidated;
        Decision = trustContextValidated ? decision : "detached_structure_valid_only";
        Eligible = trustContextValidated && eligible;
        CanonicalModelSlug = canonicalModelSlug;
        FrozenCatalogEvidenceDigestSha256 = frozenCatalogEvidenceDigestSha256;
        ApiModelIdVerified = apiModelIdVerified;
        PricingOverrideAuthenticatedCurrent = pricingOverrideAuthenticatedCurrent;
        PricingOverrideReachableForProfile = pricingOverrideReachableForProfile;
        AccountBindingIdentity = accountBindingIdentity;
        CredentialSourceIdentity = credentialSourceIdentity;
        MaximumRequests = maximumRequests;
        AggregateCostCeilingMicrousd = aggregateCostCeilingMicrousd;
        DurableConsumptionEvidenceDigestSha256 = durableConsumptionEvidenceDigestSha256;
        EvaluatedAtUnixMilliseconds = evaluatedAtUnixMilliseconds;
        CanonicalDigestSha256 = OpenRouterPremiumCanonical.Digest(_canonicalUtf8);
    }

    public string SchemaVersion => OpenRouterPremiumActivationPreflightArtifactModule.SchemaVersion;
    public string Decision { get; }
    public bool Eligible { get; }
    public string ClaimedDecision { get; }
    public bool ClaimedEligible { get; }
    public bool TrustContextValidated { get; }
    public bool LiveTrafficEnabled => false;
    public IReadOnlyList<string> BlockerCodes => Array.AsReadOnly(_blockerCodes.ToArray());
    public string CanonicalModelSlug { get; }
    public string FrozenCatalogEvidenceDigestSha256 { get; }
    public bool ApiModelIdVerified { get; }
    public bool PricingOverrideAuthenticatedCurrent { get; }
    public bool PricingOverrideReachableForProfile { get; }
    public string AccountBindingIdentity { get; }
    public string CredentialSourceIdentity { get; }
    public int MaximumRequests { get; }
    public long AggregateCostCeilingMicrousd { get; }
    public string? DurableConsumptionEvidenceDigestSha256 { get; }
    public long EvaluatedAtUnixMilliseconds { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

/// <summary>
/// Pure activation decision over caller-supplied, canonical, bounded attestations plus an already
/// opened durable journal. It has no credential, environment, DNS, HTTP, provider, or payment port
/// and never changes the profile's closed live-traffic gate.
/// </summary>
public static class OpenRouterPremiumActivationPreflightModule
{
    public const string ApprovedCredentialSourceIdentity = "trusted-credential-source/openrouter-one-shot-v1";
    public const string TrustedAttestorIdentity = "trusted-credential-attestor/local-owner-v1";

    public static OpenRouterPremiumActivationPreflightCapability Authorize(ReadOnlyMemory<byte> canonicalEvidenceBundleUtf8)
    {
        OpenRouterPremiumActivationBundle bundle = OpenRouterPremiumActivationPreflightCodec.Parse(canonicalEvidenceBundleUtf8);
        return new(bundle);
    }

    public static OpenRouterPremiumActivationPreflightArtifact EvaluateOnce(
        OpenRouterPremiumActivationPreflightCapability capability,
        FileOpenRouterPremiumJournal durableJournal,
        OpenRouterPremiumPreflightTrustAttestation trustAttestation,
        long evaluatedAtUnixMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!capability.TryConsume()) throw new OpenRouterPremiumEvidenceException("preflight_capability_consumed");
        if (!OpenRouterPremiumActivationNonceRegistry.TryConsume(capability.Bundle.AuthorizationNonce))
            throw new OpenRouterPremiumEvidenceException("preflight_nonce_consumed");
        if (cancellationToken.IsCancellationRequested) throw new OpenRouterPremiumEvidenceException("preflight_cancelled");
        ArgumentNullException.ThrowIfNull(durableJournal);
        ArgumentNullException.ThrowIfNull(trustAttestation);

        OpenRouterPremiumActivationBundle bundle = capability.Bundle;
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        List<string> blockers = [];

        if (!bundle.Catalog.SourceAuthenticated) blockers.Add("catalog_authentication_unverified");
        if (!bundle.Catalog.ApiModelIdVerified) blockers.Add("api_model_id_unverified");
        if (!bundle.Catalog.ZdrRouteVerified) blockers.Add("zdr_route_unverified");
        if (!IsExactUtcTimestamp(bundle.EvidenceObservedAtUtc) || bundle.Attestation.AttestedAtUtc != bundle.EvidenceObservedAtUtc
            || !IsBoundedAttestationWindow(bundle.Attestation.AttestedAtUtc, bundle.Attestation.ExpiresAtUtc))
            blockers.Add("catalog_evidence_date_invalid");
        if (TryParseAttestationWindow(bundle.Attestation.AttestedAtUtc, bundle.Attestation.ExpiresAtUtc,
                out long attestedAtUnixMilliseconds, out long expiresAtUnixMilliseconds))
        {
            if (evaluatedAtUnixMilliseconds < attestedAtUnixMilliseconds)
                blockers.Add("credential_attestation_not_yet_valid");
            else if (evaluatedAtUnixMilliseconds >= expiresAtUnixMilliseconds)
                blockers.Add("credential_attestation_expired");
        }
        if (bundle.Catalog.SourceUri != OpenRouterPremiumHttpMetadataVerifier.ModelsUri
            || bundle.Catalog.SourceMethod != "GET" || bundle.Catalog.AuthenticationScheme != "Bearer"
            || bundle.Catalog.ResponseRoot != "data" || bundle.Catalog.ResponseStatus != 200)
            blockers.Add("catalog_source_invalid");
        if (bundle.Catalog.ModelId != OpenRouterPremiumProfile.ModelIdentity
            || bundle.Catalog.CanonicalSlug != OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity)
            blockers.Add("api_model_id_mismatch");
        if (bundle.Catalog.ProviderSlug != OpenRouterPremiumProfile.ProviderSlug)
            blockers.Add("provider_route_mismatch");
        if (bundle.Catalog.ContextLengthTokens != OpenRouterPremiumProfile.ContextLengthTokens
            || bundle.Catalog.PromptUsdPerToken != OpenRouterPremiumProfile.PromptUsdPerToken
            || bundle.Catalog.CompletionUsdPerToken != OpenRouterPremiumProfile.CompletionUsdPerToken)
            blockers.Add("catalog_price_mismatch");
        if (bundle.Catalog.PricingOverrideMinimumPromptTokens != OpenRouterPremiumProfile.PricingOverrideMinimumPromptTokens
            || bundle.Catalog.PricingOverridePromptUsdPerToken != OpenRouterPremiumProfile.PricingOverridePromptUsdPerToken
            || bundle.Catalog.PricingOverrideCompletionUsdPerToken != OpenRouterPremiumProfile.PricingOverrideCompletionUsdPerToken
            || bundle.Catalog.PricingOverrideMinimumPromptTokens <= profile.Bounds.MaximumInputTokens)
            blockers.Add("catalog_pricing_schedule_mismatch");
        if (bundle.Catalog.FrozenCatalogEvidenceDigestSha256 != OpenRouterPremiumProfile.CatalogEvidenceDigestSha256
            || !bundle.Catalog.PricingOverrideAuthenticatedCurrent
            || bundle.Catalog.PricingOverrideReachableForProfile)
            blockers.Add("pricing_override_provenance_invalid");
        string[] requiredParameters = ["max_completion_tokens", "reasoning", "response_format", "structured_outputs"];
        if (!bundle.Catalog.SupportedParameters.SequenceEqual(requiredParameters, StringComparer.Ordinal))
            blockers.Add("required_parameters_unverified");
        if (bundle.MaximumRequests != profile.Bounds.RequiredScenarioCount)
            blockers.Add("request_count_not_frozen");
        if (bundle.AggregateCostCeilingMicrousd != profile.Bounds.AggregateCostCeilingMicrousd
            || bundle.AggregateCostCeilingMicrousd != 18_000)
            blockers.Add("cost_ceiling_not_frozen");
        if (bundle.CredentialSourceIdentity != ApprovedCredentialSourceIdentity)
            blockers.Add("credential_source_not_approved");
        if (trustAttestation.AuthorityIdentity != TrustedAttestorIdentity
            || trustAttestation.BundleDigestSha256 != capability.BundleDigestSha256
            || trustAttestation.AccountBindingIdentity != bundle.AccountBindingIdentity
            || trustAttestation.CredentialSourceIdentity != bundle.CredentialSourceIdentity
            || trustAttestation.EvidenceDigestSha256 != OpenRouterPremiumCanonical.Digest(string.Join('|',
                trustAttestation.AuthorityIdentity, trustAttestation.BundleDigestSha256,
                trustAttestation.AccountBindingIdentity, trustAttestation.CredentialSourceIdentity,
                "contains_secret_bytes=false")))
            blockers.Add("credential_trust_not_registered");
        if (bundle.Attestation.SchemaVersion != "snow_globe_openrouter_credential_source_attestation/v1"
            || bundle.Attestation.IssuerIdentity != TrustedAttestorIdentity
            || !bundle.Attestation.SubjectBindingVerified || !bundle.Attestation.SourceControlVerified
            || !bundle.Attestation.SingleUseLeaseSupported || bundle.Attestation.SecretBytesIncluded
            || bundle.Attestation.Purpose != "openrouter.chat.completions/one-shot-12"
            || bundle.Attestation.SourceIdentity != bundle.CredentialSourceIdentity
            || bundle.Attestation.AccountBindingIdentity != bundle.AccountBindingIdentity)
            blockers.Add("credential_attestation_untrusted");

        OpenRouterPremiumJournalHeader header = durableJournal.Header;
        OpenRouterPremiumDurableRestartEvidence restart = durableJournal.RestartEvidence;
        if (!durableJournal.ProvidesDurableFlush || !restart.FlushToDiskRequested)
            blockers.Add("durable_flush_unverified");
        if (!restart.RestartVerified) blockers.Add("durable_restart_unverified");
        if (header.ProfileDigestSha256 != profile.ProfileDigestSha256
            || header.CatalogEvidenceDigestSha256 != OpenRouterPremiumProfile.CatalogEvidenceDigestSha256
            || header.EndpointEvidenceDigestSha256 != OpenRouterPremiumProfile.EndpointEvidenceDigestSha256
            || header.PromptSetDigestSha256 != profile.PromptSetDigestSha256
            || header.AccountBindingIdentity != bundle.AccountBindingIdentity
            || header.MaximumSlots != 12 || header.PerSlotCostCeilingMicrousd != 1_500
            || header.AggregateCostCeilingMicrousd != 18_000
            || restart.JournalHeaderChecksumSha256 != header.HeaderChecksumSha256)
            blockers.Add("journal_binding_invalid");

        try
        {
            OpenRouterPremiumJournalSnapshot snapshot = durableJournal.Snapshot();
            if (snapshot.Slots.Count != 0 || snapshot.ReservedExposureMicrousd != 0 || snapshot.SettledMicrousd != 0)
                blockers.Add("journal_not_empty");
            if (snapshot.Slots.Count == 0 && restart.RecordCount != 0)
                blockers.Add("journal_preflight_already_consumed");
            if (restart.RecordCount == 0 && restart.FinalRecordChecksumSha256 != FileOpenRouterPremiumJournalCodec.ZeroDigest)
                blockers.Add("durable_restart_evidence_invalid");
            if (restart.SnapshotDigestSha256 != FileOpenRouterPremiumJournal.SnapshotDigest(snapshot)
                || restart.EvidenceDigestSha256 != RestartEvidenceDigest(restart))
                blockers.Add("durable_restart_evidence_invalid");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            blockers.Add("durable_journal_unavailable");
        }

        blockers = blockers.Distinct(StringComparer.Ordinal).ToList();
        bool eligible = blockers.Count == 0;
        OpenRouterPremiumDurablePreflightConsumption? consumption = null;
        if (eligible)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OpenRouterPremiumEvidenceException("preflight_cancelled");
                consumption = durableJournal.ConsumeEligiblePreflightOnce(
                    OpenRouterPremiumCanonical.Digest(bundle.AuthorizationNonce), capability.BundleDigestSha256,
                    trustAttestation.EvidenceDigestSha256);
            }
            catch (OpenRouterPremiumEvidenceException exception) when (exception.Code is "preflight_journal_consumed" or "preflight_journal_not_empty")
            {
                blockers.Add("journal_changed_during_preflight");
                eligible = false;
            }
        }
        return OpenRouterPremiumActivationPreflightArtifactModule.Create(bundle, header, restart, trustAttestation,
            consumption, blockers, eligible, evaluatedAtUnixMilliseconds);
    }

    private static bool IsExactUtcTimestamp(string value) => DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'",
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)
        && parsed.Offset == TimeSpan.Zero;

    private static bool IsBoundedAttestationWindow(string start, string end) =>
        DateTimeOffset.TryParseExact(start, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsedStart)
        && DateTimeOffset.TryParseExact(end, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsedEnd)
        && parsedEnd - parsedStart >= TimeSpan.FromMinutes(5)
        && parsedEnd - parsedStart <= TimeSpan.FromDays(1);

    private static bool TryParseAttestationWindow(string start, string end, out long startMilliseconds, out long endMilliseconds)
    {
        bool validStart = DateTimeOffset.TryParseExact(start, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsedStart);
        bool validEnd = DateTimeOffset.TryParseExact(end, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsedEnd);
        bool valid = validStart && validEnd;
        startMilliseconds = valid ? parsedStart.ToUnixTimeMilliseconds() : 0;
        endMilliseconds = valid ? parsedEnd.ToUnixTimeMilliseconds() : 0;
        return valid;
    }

    private static string RestartEvidenceDigest(OpenRouterPremiumDurableRestartEvidence evidence)
    {
        string payload = string.Join('|', evidence.SchemaVersion, evidence.JournalHeaderChecksumSha256,
            evidence.SnapshotDigestSha256, evidence.RecordCount, evidence.FinalRecordChecksumSha256,
            $"flush_to_disk_requested={evidence.FlushToDiskRequested.ToString().ToLowerInvariant()}",
            $"restart_verified={evidence.RestartVerified.ToString().ToLowerInvariant()}");
        return OpenRouterPremiumCanonical.Digest(payload);
    }
}

public static class OpenRouterPremiumActivationPreflightArtifactModule
{
    public const string SchemaVersion = "snow_globe_openrouter_premium_activation_preflight/v2";
    public const int MaximumArtifactBytes = 16 * 1024;
    private static readonly string[] ClaimLimitations =
    [
        "attestation_identity_is_approval_input_not_cryptographic_signature",
        "catalog_snapshot_requires_registered_authenticated_metadata_trust",
        "pricing_override_is_frozen_content_addressed_evidence_not_authenticated_current_api",
        "pricing_override_is_unreachable_under_profile_input_bound",
        "flush_request_and_restart_readback_do_not_prove_physical_media_durability",
        "preflight_eligibility_does_not_enable_live_traffic_or_authorize_spend"
    ];
    private static readonly string[] BlockerOrder =
    [
        "catalog_authentication_unverified", "api_model_id_unverified", "zdr_route_unverified",
        "catalog_evidence_date_invalid", "credential_attestation_not_yet_valid", "credential_attestation_expired",
        "catalog_source_invalid", "api_model_id_mismatch", "provider_route_mismatch",
        "catalog_price_mismatch", "catalog_pricing_schedule_mismatch", "pricing_override_provenance_invalid", "required_parameters_unverified", "request_count_not_frozen", "cost_ceiling_not_frozen",
        "credential_source_not_approved", "credential_trust_not_registered", "credential_attestation_untrusted",
        "durable_flush_unverified", "durable_restart_unverified", "journal_binding_invalid", "journal_not_empty",
        "journal_preflight_already_consumed", "durable_restart_evidence_invalid", "durable_journal_unavailable",
        "journal_changed_during_preflight"
    ];
    private static readonly HashSet<string> AllowedBlockerCodes = new(BlockerOrder, StringComparer.Ordinal);

    internal static OpenRouterPremiumActivationPreflightArtifact Create(
        OpenRouterPremiumActivationBundle bundle,
        OpenRouterPremiumJournalHeader header,
        OpenRouterPremiumDurableRestartEvidence restart,
        OpenRouterPremiumPreflightTrustAttestation trustAttestation,
        OpenRouterPremiumDurablePreflightConsumption? consumption,
        IReadOnlyList<string> blockers,
        bool eligible,
        long evaluatedAtUnixMilliseconds)
    {
        string decision = eligible ? "eligible_for_separately_authorized_one_shot" : "ineligible";
        string catalogDigest = OpenRouterPremiumActivationPreflightCodec.CatalogDigest(bundle.Catalog);
        string attestationDigest = OpenRouterPremiumActivationPreflightCodec.AttestationDigest(bundle.Attestation);
        string nonceDigest = OpenRouterPremiumCanonical.Digest(bundle.AuthorizationNonce);
        byte[] payload = Write(decision, eligible, blockers, bundle, header, restart, trustAttestation.EvidenceDigestSha256,
            consumption?.EvidenceDigestSha256, catalogDigest, attestationDigest, nonceDigest, evaluatedAtUnixMilliseconds, null);
        string payloadDigest = OpenRouterPremiumCanonical.Digest(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(decision, eligible, blockers, bundle, header, restart, trustAttestation.EvidenceDigestSha256,
            consumption?.EvidenceDigestSha256, catalogDigest, attestationDigest, nonceDigest, evaluatedAtUnixMilliseconds, payloadDigest);
        if (canonical.Length is < 1 or > MaximumArtifactBytes) throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
        return New(canonical, payloadDigest, decision, eligible, blockers, bundle,
            consumption?.EvidenceDigestSha256, evaluatedAtUnixMilliseconds);
    }

    public static OpenRouterPremiumActivationPreflightArtifact Validate(ReadOnlyMemory<byte> canonicalArtifactUtf8)
    {
        byte[] bytes = canonicalArtifactUtf8.ToArray();
        if (bytes.Length is < 1 or > MaximumArtifactBytes) throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
        try
        {
            using JsonDocument document = PreflightJson.ParseStrictObject(bytes, 4, 128, "preflight_artifact_rejected");
            JsonElement root = document.RootElement;
            PreflightJson.Exact(root, "schema_version", "decision", "eligible", "live_traffic_enabled", "profile_digest_sha256",
                "canonical_model_slug", "api_model_id_verified", "provider_slug", "evidence_observed_at_utc", "evaluated_at_unix_milliseconds",
                "catalog_snapshot_digest_sha256",
                "frozen_catalog_evidence_digest_sha256", "pricing_override_authenticated_current", "pricing_override_reachable_for_profile",
                "credential_attestation_digest_sha256", "account_binding_identity", "credential_source_identity", "journal_identity",
                "journal_header_checksum_sha256", "durable_restart_evidence_digest_sha256", "registered_trust_evidence_digest_sha256", "authorization_nonce_digest_sha256",
                "durable_preflight_consumption_evidence_digest_sha256",
                "maximum_requests", "per_slot_cost_ceiling_microusd", "aggregate_cost_ceiling_microusd", "blocker_codes",
                "claim_limitation_codes", "preflight_payload_digest_sha256");
            string decision = PreflightJson.String(root, "decision"); bool eligible = PreflightJson.Boolean(root, "eligible");
            long evaluatedAtUnixMilliseconds = PreflightJson.Int64(root, "evaluated_at_unix_milliseconds");
            if (PreflightJson.Boolean(root, "live_traffic_enabled") || root.GetProperty("schema_version").GetString() != SchemaVersion
                || root.GetProperty("profile_digest_sha256").GetString() != OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256
                || root.GetProperty("canonical_model_slug").GetString() != OpenRouterPremiumProfile.CanonicalModelSlug
                || !PreflightJson.Boolean(root, "api_model_id_verified")
                || root.GetProperty("frozen_catalog_evidence_digest_sha256").GetString() != OpenRouterPremiumProfile.CatalogEvidenceDigestSha256
                || !PreflightJson.Boolean(root, "pricing_override_authenticated_current")
                || PreflightJson.Boolean(root, "pricing_override_reachable_for_profile")
                || root.GetProperty("provider_slug").GetString() != OpenRouterPremiumProfile.ProviderSlug)
                throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            string[] blockers = PreflightJson.StringArray(root.GetProperty("blocker_codes"), 32);
            string[] limitations = PreflightJson.StringArray(root.GetProperty("claim_limitation_codes"), ClaimLimitations.Length);
            if (!limitations.SequenceEqual(ClaimLimitations, StringComparer.Ordinal)
                || eligible != (blockers.Length == 0) || decision != (eligible ? "eligible_for_separately_authorized_one_shot" : "ineligible")
                || blockers.Distinct(StringComparer.Ordinal).Count() != blockers.Length
                || blockers.Any(blocker => !AllowedBlockerCodes.Contains(blocker))
                || !blockers.SequenceEqual(BlockerOrder.Where(blockers.Contains), StringComparer.Ordinal))
                throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            foreach (string digestName in new[] { "catalog_snapshot_digest_sha256", "frozen_catalog_evidence_digest_sha256", "credential_attestation_digest_sha256",
                         "journal_header_checksum_sha256", "durable_restart_evidence_digest_sha256", "registered_trust_evidence_digest_sha256",
                         "authorization_nonce_digest_sha256", "preflight_payload_digest_sha256" })
                if (!OpenRouterPremiumCanonical.IsDigest(PreflightJson.String(root, digestName)))
                    throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            JsonElement consumption = root.GetProperty("durable_preflight_consumption_evidence_digest_sha256");
            string? consumptionDigest = consumption.ValueKind == JsonValueKind.Null ? null
                : consumption.ValueKind == JsonValueKind.String ? consumption.GetString()
                : throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            if ((eligible && !OpenRouterPremiumCanonical.IsDigest(consumptionDigest)) || (!eligible && consumptionDigest is not null))
                throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            string account = PreflightJson.String(root, "account_binding_identity"); _ = new ByokAccountBindingIdentity(account);
            string source = PreflightJson.String(root, "credential_source_identity");
            if (!OpenRouterPremiumCanonical.IsIdentity(source) || PreflightJson.Int32(root, "maximum_requests") != 12
                || PreflightJson.Int64(root, "per_slot_cost_ceiling_microusd") != 1_500
                || PreflightJson.Int64(root, "aggregate_cost_ceiling_microusd") != 18_000)
                throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            string payloadDigest = PreflightJson.String(root, "preflight_payload_digest_sha256");
            byte[] payload = WriteFromArtifact(root, blockers, limitations, includeDigest: false);
            string expectedPayload = OpenRouterPremiumCanonical.Digest(payload); CryptographicOperations.ZeroMemory(payload);
            byte[] canonical = WriteFromArtifact(root, blockers, limitations, includeDigest: true);
            if (payloadDigest != expectedPayload || !canonical.AsSpan().SequenceEqual(bytes))
                throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected");
            return new(canonical, payloadDigest, decision, eligible, blockers, OpenRouterPremiumProfile.CanonicalModelSlug,
                OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, true, false, false,
                account, source, 12, 18_000, consumptionDigest, evaluatedAtUnixMilliseconds, trustContextValidated: false);
        }
        catch (OpenRouterPremiumEvidenceException) { throw; }
        catch (Exception) { throw new OpenRouterPremiumEvidenceException("preflight_artifact_rejected"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static OpenRouterPremiumActivationPreflightArtifact New(byte[] canonical, string payloadDigest, string decision,
        bool eligible, IReadOnlyList<string> blockers, OpenRouterPremiumActivationBundle bundle,
        string? consumptionDigest, long evaluatedAtUnixMilliseconds) =>
        new(canonical, payloadDigest, decision, eligible, blockers, OpenRouterPremiumProfile.CanonicalModelSlug,
            bundle.Catalog.FrozenCatalogEvidenceDigestSha256, bundle.Catalog.ApiModelIdVerified,
            bundle.Catalog.PricingOverrideAuthenticatedCurrent, bundle.Catalog.PricingOverrideReachableForProfile,
            bundle.AccountBindingIdentity, bundle.CredentialSourceIdentity, 12, 18_000, consumptionDigest,
            evaluatedAtUnixMilliseconds, trustContextValidated: true);

    private static byte[] Write(
        string decision, bool eligible, IReadOnlyList<string> blockers, OpenRouterPremiumActivationBundle bundle,
        OpenRouterPremiumJournalHeader header, OpenRouterPremiumDurableRestartEvidence restart,
        string trustEvidenceDigest, string? consumptionEvidenceDigest, string catalogDigest, string attestationDigest,
        string nonceDigest, long evaluatedAtUnixMilliseconds, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject(); writer.WriteString("schema_version", SchemaVersion); writer.WriteString("decision", decision);
        writer.WriteBoolean("eligible", eligible); writer.WriteBoolean("live_traffic_enabled", false);
        writer.WriteString("profile_digest_sha256", OpenRouterPremiumProfileRegistry.Selected.ProfileDigestSha256);
        writer.WriteString("canonical_model_slug", OpenRouterPremiumProfile.CanonicalModelSlug);
        writer.WriteBoolean("api_model_id_verified", bundle.Catalog.ApiModelIdVerified);
        writer.WriteString("provider_slug", OpenRouterPremiumProfile.ProviderSlug);
        writer.WriteString("evidence_observed_at_utc", bundle.EvidenceObservedAtUtc);
        writer.WriteNumber("evaluated_at_unix_milliseconds", evaluatedAtUnixMilliseconds);
        writer.WriteString("catalog_snapshot_digest_sha256", catalogDigest);
        writer.WriteString("frozen_catalog_evidence_digest_sha256", bundle.Catalog.FrozenCatalogEvidenceDigestSha256);
        writer.WriteBoolean("pricing_override_authenticated_current", bundle.Catalog.PricingOverrideAuthenticatedCurrent);
        writer.WriteBoolean("pricing_override_reachable_for_profile", bundle.Catalog.PricingOverrideReachableForProfile);
        writer.WriteString("credential_attestation_digest_sha256", attestationDigest); writer.WriteString("account_binding_identity", bundle.AccountBindingIdentity);
        writer.WriteString("credential_source_identity", bundle.CredentialSourceIdentity); writer.WriteString("journal_identity", header.JournalIdentity);
        writer.WriteString("journal_header_checksum_sha256", header.HeaderChecksumSha256); writer.WriteString("durable_restart_evidence_digest_sha256", restart.EvidenceDigestSha256);
        writer.WriteString("registered_trust_evidence_digest_sha256", trustEvidenceDigest); writer.WriteString("authorization_nonce_digest_sha256", nonceDigest);
        if (consumptionEvidenceDigest is null) writer.WriteNull("durable_preflight_consumption_evidence_digest_sha256");
        else writer.WriteString("durable_preflight_consumption_evidence_digest_sha256", consumptionEvidenceDigest);
        writer.WriteNumber("maximum_requests", 12);
        writer.WriteNumber("per_slot_cost_ceiling_microusd", 1_500); writer.WriteNumber("aggregate_cost_ceiling_microusd", 18_000);
        writer.WritePropertyName("blocker_codes"); WriteStrings(writer, blockers); writer.WritePropertyName("claim_limitation_codes"); WriteStrings(writer, ClaimLimitations);
        if (payloadDigest is not null) writer.WriteString("preflight_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static byte[] WriteFromArtifact(JsonElement root, IReadOnlyList<string> blockers, IReadOnlyList<string> limitations, bool includeDigest)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.NameEquals("preflight_payload_digest_sha256")) continue;
            writer.WritePropertyName(property.Name);
            if (property.NameEquals("blocker_codes")) WriteStrings(writer, blockers);
            else if (property.NameEquals("claim_limitation_codes")) WriteStrings(writer, limitations);
            else property.Value.WriteTo(writer);
        }
        if (includeDigest) writer.WriteString("preflight_payload_digest_sha256", PreflightJson.String(root, "preflight_payload_digest_sha256"));
        writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, IEnumerable<string> values)
    { writer.WriteStartArray(); foreach (string value in values) writer.WriteStringValue(value); writer.WriteEndArray(); }
}

internal sealed record OpenRouterPremiumCatalogSnapshot(
    string SourceUri, string SourceMethod, string AuthenticationScheme, bool SourceAuthenticated, int ResponseStatus, string ResponseRoot,
    string ModelId, string CanonicalSlug, int ContextLengthTokens, string PromptUsdPerToken,
    string CompletionUsdPerToken, int PricingOverrideMinimumPromptTokens,
    string PricingOverridePromptUsdPerToken, string PricingOverrideCompletionUsdPerToken,
    string FrozenCatalogEvidenceDigestSha256, bool PricingOverrideAuthenticatedCurrent,
    bool PricingOverrideReachableForProfile,
    string[] SupportedParameters, string ProviderSlug,
    bool ApiModelIdVerified, bool ZdrRouteVerified);

internal sealed record OpenRouterPremiumCredentialSourceAttestation(
    string SchemaVersion, string IssuerIdentity, string SourceIdentity, string AccountBindingIdentity,
    bool SubjectBindingVerified, bool SourceControlVerified, bool SingleUseLeaseSupported,
    bool SecretBytesIncluded, string Purpose, string AttestedAtUtc, string ExpiresAtUtc, string AttestationNonce);

internal sealed record OpenRouterPremiumActivationBundle(
    string SchemaVersion, string EvidenceObservedAtUtc, string AccountBindingIdentity,
    string CredentialSourceIdentity, string AuthorizationNonce, int MaximumRequests,
    long AggregateCostCeilingMicrousd, OpenRouterPremiumCatalogSnapshot Catalog,
    OpenRouterPremiumCredentialSourceAttestation Attestation, string BundleDigestSha256);

internal static class OpenRouterPremiumActivationPreflightCodec
{
    internal const string SchemaVersion = "snow_globe_openrouter_premium_activation_bundle/v2";
    private const int MaximumBundleBytes = 16 * 1024;

    internal static OpenRouterPremiumActivationBundle Parse(ReadOnlyMemory<byte> canonicalUtf8)
    {
        byte[] bytes = canonicalUtf8.ToArray();
        if (bytes.Length is < 1 or > MaximumBundleBytes) throw new OpenRouterPremiumEvidenceException("preflight_bundle_rejected");
        try
        {
            using JsonDocument document = PreflightJson.ParseStrictObject(bytes, 6, 160, "preflight_bundle_rejected");
            JsonElement root = document.RootElement;
            PreflightJson.Exact(root, "schema_version", "evidence_observed_at_utc", "account_binding_identity", "credential_source_identity",
                "authorization_nonce", "maximum_requests", "aggregate_cost_ceiling_microusd", "catalog", "credential_source_attestation");
            JsonElement catalogValue = root.GetProperty("catalog");
            PreflightJson.Exact(catalogValue, "source_uri", "source_method", "authentication_scheme", "source_authenticated", "response_status", "response_root", "model_id", "canonical_slug",
                "context_length_tokens", "prompt_usd_per_token", "completion_usd_per_token", "pricing_override_min_prompt_tokens",
                "pricing_override_prompt_usd_per_token", "pricing_override_completion_usd_per_token", "frozen_catalog_evidence_digest_sha256",
                "pricing_override_authenticated_current", "pricing_override_reachable_for_profile", "supported_parameters", "provider_slug",
                "api_model_id_verified", "zdr_route_verified");
            JsonElement attestationValue = root.GetProperty("credential_source_attestation");
            PreflightJson.Exact(attestationValue, "schema_version", "issuer_identity", "source_identity", "account_binding_identity", "subject_binding_verified",
                "source_control_verified", "single_use_lease_supported", "secret_bytes_included", "purpose", "attested_at_utc", "expires_at_utc", "attestation_nonce");
            string account = PreflightJson.String(root, "account_binding_identity"); _ = new ByokAccountBindingIdentity(account);
            string source = PreflightJson.String(root, "credential_source_identity"); string nonce = PreflightJson.String(root, "authorization_nonce");
            if (!OpenRouterPremiumCanonical.IsIdentity(source) || !OpenRouterPremiumCanonical.IsIdentity(nonce))
                throw new OpenRouterPremiumEvidenceException("preflight_bundle_rejected");
            OpenRouterPremiumCatalogSnapshot catalog = new(PreflightJson.String(catalogValue, "source_uri"), PreflightJson.String(catalogValue, "source_method"),
                PreflightJson.String(catalogValue, "authentication_scheme"), PreflightJson.Boolean(catalogValue, "source_authenticated"),
                PreflightJson.Int32(catalogValue, "response_status"), PreflightJson.String(catalogValue, "response_root"),
                PreflightJson.String(catalogValue, "model_id"), PreflightJson.String(catalogValue, "canonical_slug"),
                PreflightJson.Int32(catalogValue, "context_length_tokens"), PreflightJson.String(catalogValue, "prompt_usd_per_token"),
                PreflightJson.String(catalogValue, "completion_usd_per_token"), PreflightJson.Int32(catalogValue, "pricing_override_min_prompt_tokens"),
                PreflightJson.String(catalogValue, "pricing_override_prompt_usd_per_token"),
                PreflightJson.String(catalogValue, "pricing_override_completion_usd_per_token"),
                PreflightJson.String(catalogValue, "frozen_catalog_evidence_digest_sha256"),
                PreflightJson.Boolean(catalogValue, "pricing_override_authenticated_current"),
                PreflightJson.Boolean(catalogValue, "pricing_override_reachable_for_profile"),
                PreflightJson.StringArray(catalogValue.GetProperty("supported_parameters"), 16),
                PreflightJson.String(catalogValue, "provider_slug"), PreflightJson.Boolean(catalogValue, "api_model_id_verified"),
                PreflightJson.Boolean(catalogValue, "zdr_route_verified"));
            OpenRouterPremiumCredentialSourceAttestation attestation = new(PreflightJson.String(attestationValue, "schema_version"),
                PreflightJson.String(attestationValue, "issuer_identity"), PreflightJson.String(attestationValue, "source_identity"),
                PreflightJson.String(attestationValue, "account_binding_identity"), PreflightJson.Boolean(attestationValue, "subject_binding_verified"),
                PreflightJson.Boolean(attestationValue, "source_control_verified"), PreflightJson.Boolean(attestationValue, "single_use_lease_supported"),
                PreflightJson.Boolean(attestationValue, "secret_bytes_included"), PreflightJson.String(attestationValue, "purpose"),
                PreflightJson.String(attestationValue, "attested_at_utc"), PreflightJson.String(attestationValue, "expires_at_utc"),
                PreflightJson.String(attestationValue, "attestation_nonce"));
            if (!OpenRouterPremiumCanonical.IsIdentity(attestation.IssuerIdentity) || !OpenRouterPremiumCanonical.IsIdentity(attestation.SourceIdentity)
                || !OpenRouterPremiumCanonical.IsIdentity(attestation.AttestationNonce))
                throw new OpenRouterPremiumEvidenceException("preflight_bundle_rejected");
            _ = new ByokAccountBindingIdentity(attestation.AccountBindingIdentity);
            OpenRouterPremiumActivationBundle bundle = new(PreflightJson.String(root, "schema_version"), PreflightJson.String(root, "evidence_observed_at_utc"),
                account, source, nonce, PreflightJson.Int32(root, "maximum_requests"), PreflightJson.Int64(root, "aggregate_cost_ceiling_microusd"),
                catalog, attestation, OpenRouterPremiumCanonical.Digest(bytes));
            byte[] expected = Write(bundle);
            if (bundle.SchemaVersion != SchemaVersion || !bytes.AsSpan().SequenceEqual(expected))
                throw new OpenRouterPremiumEvidenceException("preflight_bundle_rejected");
            return bundle;
        }
        catch (OpenRouterPremiumEvidenceException) { throw; }
        catch (Exception) { throw new OpenRouterPremiumEvidenceException("preflight_bundle_rejected"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static byte[] Write(OpenRouterPremiumActivationBundle bundle)
    {
        ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject(); writer.WriteString("schema_version", bundle.SchemaVersion); writer.WriteString("evidence_observed_at_utc", bundle.EvidenceObservedAtUtc);
        writer.WriteString("account_binding_identity", bundle.AccountBindingIdentity); writer.WriteString("credential_source_identity", bundle.CredentialSourceIdentity);
        writer.WriteString("authorization_nonce", bundle.AuthorizationNonce); writer.WriteNumber("maximum_requests", bundle.MaximumRequests);
        writer.WriteNumber("aggregate_cost_ceiling_microusd", bundle.AggregateCostCeilingMicrousd); writer.WritePropertyName("catalog"); WriteCatalog(writer, bundle.Catalog);
        writer.WritePropertyName("credential_source_attestation"); WriteAttestation(writer, bundle.Attestation); writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static string CatalogDigest(OpenRouterPremiumCatalogSnapshot value)
    { ArrayBufferWriter<byte> b = new(); using Utf8JsonWriter w = new(b); WriteCatalog(w, value); w.Flush(); return OpenRouterPremiumCanonical.Digest(b.WrittenSpan); }
    internal static string AttestationDigest(OpenRouterPremiumCredentialSourceAttestation value)
    { ArrayBufferWriter<byte> b = new(); using Utf8JsonWriter w = new(b); WriteAttestation(w, value); w.Flush(); return OpenRouterPremiumCanonical.Digest(b.WrittenSpan); }

    private static void WriteCatalog(Utf8JsonWriter writer, OpenRouterPremiumCatalogSnapshot value)
    {
        writer.WriteStartObject(); writer.WriteString("source_uri", value.SourceUri); writer.WriteString("source_method", value.SourceMethod);
        writer.WriteString("authentication_scheme", value.AuthenticationScheme); writer.WriteBoolean("source_authenticated", value.SourceAuthenticated);
        writer.WriteNumber("response_status", value.ResponseStatus); writer.WriteString("response_root", value.ResponseRoot);
        writer.WriteString("model_id", value.ModelId); writer.WriteString("canonical_slug", value.CanonicalSlug); writer.WriteNumber("context_length_tokens", value.ContextLengthTokens);
        writer.WriteString("prompt_usd_per_token", value.PromptUsdPerToken); writer.WriteString("completion_usd_per_token", value.CompletionUsdPerToken);
        writer.WriteNumber("pricing_override_min_prompt_tokens", value.PricingOverrideMinimumPromptTokens);
        writer.WriteString("pricing_override_prompt_usd_per_token", value.PricingOverridePromptUsdPerToken);
        writer.WriteString("pricing_override_completion_usd_per_token", value.PricingOverrideCompletionUsdPerToken);
        writer.WriteString("frozen_catalog_evidence_digest_sha256", value.FrozenCatalogEvidenceDigestSha256);
        writer.WriteBoolean("pricing_override_authenticated_current", value.PricingOverrideAuthenticatedCurrent);
        writer.WriteBoolean("pricing_override_reachable_for_profile", value.PricingOverrideReachableForProfile);
        writer.WritePropertyName("supported_parameters"); writer.WriteStartArray(); foreach (string item in value.SupportedParameters) writer.WriteStringValue(item); writer.WriteEndArray();
        writer.WriteString("provider_slug", value.ProviderSlug); writer.WriteBoolean("api_model_id_verified", value.ApiModelIdVerified);
        writer.WriteBoolean("zdr_route_verified", value.ZdrRouteVerified); writer.WriteEndObject();
    }

    private static void WriteAttestation(Utf8JsonWriter writer, OpenRouterPremiumCredentialSourceAttestation value)
    {
        writer.WriteStartObject(); writer.WriteString("schema_version", value.SchemaVersion); writer.WriteString("issuer_identity", value.IssuerIdentity);
        writer.WriteString("source_identity", value.SourceIdentity); writer.WriteString("account_binding_identity", value.AccountBindingIdentity);
        writer.WriteBoolean("subject_binding_verified", value.SubjectBindingVerified); writer.WriteBoolean("source_control_verified", value.SourceControlVerified);
        writer.WriteBoolean("single_use_lease_supported", value.SingleUseLeaseSupported); writer.WriteBoolean("secret_bytes_included", value.SecretBytesIncluded);
        writer.WriteString("purpose", value.Purpose); writer.WriteString("attested_at_utc", value.AttestedAtUtc); writer.WriteString("expires_at_utc", value.ExpiresAtUtc);
        writer.WriteString("attestation_nonce", value.AttestationNonce); writer.WriteEndObject();
    }
}

internal static class PreflightJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static JsonDocument ParseStrictObject(ReadOnlySpan<byte> bytes, int maxDepth, int maxTokens, string code)
    {
        try
        {
            _ = StrictUtf8.GetString(bytes); int tokens = 0;
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = maxDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            Stack<HashSet<string>> stack = new();
            while (reader.Read())
            {
                if (++tokens > maxTokens) throw new OpenRouterPremiumEvidenceException(code);
                if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && !stack.Peek().Add(reader.GetString()!)) throw new OpenRouterPremiumEvidenceException(code);
                if (reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String
                    && (reader.HasValueSequence ? reader.ValueSequence.Length > 256 : reader.ValueSpan.Length > 256))
                    throw new OpenRouterPremiumEvidenceException(code);
            }
            JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = maxDepth, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (document.RootElement.ValueKind != JsonValueKind.Object) { document.Dispose(); throw new OpenRouterPremiumEvidenceException(code); }
            return document;
        }
        catch (OpenRouterPremiumEvidenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException) { throw new OpenRouterPremiumEvidenceException(code); }
    }

    internal static void Exact(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        string[] names = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (!names.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidDataException();
    }
    internal static string String(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String ? value.GetProperty(name).GetString()! : throw new InvalidDataException();
    internal static bool Boolean(JsonElement value, string name) => value.GetProperty(name).ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetProperty(name).GetBoolean() : throw new InvalidDataException();
    internal static int Int32(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt32(out int parsed) ? parsed : throw new InvalidDataException();
    internal static long Int64(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt64(out long parsed) ? parsed : throw new InvalidDataException();
    internal static string[] StringArray(JsonElement value, int maximum)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum) throw new InvalidDataException();
        return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : throw new InvalidDataException()).ToArray();
    }
}

internal static class OpenRouterPremiumActivationNonceRegistry
{
    private const int MaximumRememberedNonces = 4096;
    private static readonly object Gate = new();
    private static readonly HashSet<string> Consumed = new(StringComparer.Ordinal);
    internal static bool TryConsume(string nonce)
    {
        lock (Gate)
        {
            if (Consumed.Contains(nonce)) return false;
            if (Consumed.Count >= MaximumRememberedNonces) throw new OpenRouterPremiumEvidenceException("preflight_nonce_capacity_exhausted");
            Consumed.Add(nonce); return true;
        }
    }
}
