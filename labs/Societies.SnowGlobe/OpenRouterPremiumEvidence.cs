using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed class OpenRouterPremiumEvidenceException : Exception
{
    public OpenRouterPremiumEvidenceException(string code) : base(Validate(code)) => Code = code;
    internal OpenRouterPremiumEvidenceException(OpenRouterPremiumResponseParserRejectionCode parserRejectionCode)
        : this(ValidateParserRejectionCode(parserRejectionCode)) => ParserRejectionCode = parserRejectionCode;
    public string Code { get; }
    internal OpenRouterPremiumResponseParserRejectionCode? ParserRejectionCode { get; }

    private static string Validate(string code)
    {
        if (!OpenRouterPremiumCanonical.IsIdentity(code) || code.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(code));
        return code;
    }

    private static string ValidateParserRejectionCode(OpenRouterPremiumResponseParserRejectionCode code)
    {
        if (!Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(code));
        return code.ToString();
    }
}

public interface IOpenRouterPremiumClock { long NowMilliseconds { get; } }

public sealed class OfflineOpenRouterPremiumClock : IOpenRouterPremiumClock
{
    private long _now;
    public OfflineOpenRouterPremiumClock(long nowMilliseconds)
    {
        if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
        _now = nowMilliseconds;
    }
    public long NowMilliseconds => Interlocked.Read(ref _now);
    public void Advance(int milliseconds)
    {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        Interlocked.Add(ref _now, milliseconds);
    }
}

public sealed record OpenRouterPremiumAuthorization(
    OpenRouterPremiumProfileIdentity ProfileIdentity,
    string CatalogEvidenceDigestSha256,
    string EndpointEvidenceDigestSha256,
    ByokAccountBindingIdentity AccountBinding,
    string FinancialJournalIdentity,
    string FinancialJournalHeaderChecksumSha256,
    string ExchangeIdentity,
    string ExchangeContractDigestSha256,
    string CredentialLeaseSourceIdentity,
    string AuthorizationNonce,
    long IssuedAtMilliseconds,
    long ExpiresAtMilliseconds);

public sealed class OpenRouterPremiumExecutionCapability
{
    private int _state;

    internal OpenRouterPremiumExecutionCapability(
        OpenRouterPremiumAuthorization authorization,
        OpenRouterPremiumProfile profile,
        CognitionQualityPromptEnvelopePublication publication)
    {
        Authorization = authorization with
        {
            ProfileIdentity = new OpenRouterPremiumProfileIdentity(authorization.ProfileIdentity.Value),
            AccountBinding = new ByokAccountBindingIdentity(authorization.AccountBinding.Value)
        };
        Profile = profile;
        Publication = new CognitionQualityPromptEnvelopePublication(
            publication.CanonicalUtf8.ToArray(), publication.PayloadDigestSha256, publication.PromptRevision,
            publication.Slots, publication.ClaimLimitationCodes);
        CapabilityDigestSha256 = OpenRouterPremiumCanonical.Digest(string.Join('|',
            profile.ProfileDigestSha256, authorization.CatalogEvidenceDigestSha256,
            authorization.EndpointEvidenceDigestSha256, authorization.AccountBinding.Value,
            authorization.FinancialJournalIdentity, authorization.FinancialJournalHeaderChecksumSha256,
            authorization.ExchangeIdentity, authorization.ExchangeContractDigestSha256,
            authorization.CredentialLeaseSourceIdentity,
            authorization.AuthorizationNonce, authorization.IssuedAtMilliseconds, authorization.ExpiresAtMilliseconds,
            publication.CanonicalDigestSha256, publication.PromptSetDigestSha256,
            profile.CorpusDigestSha256, profile.ScoringDigestSha256, OpenRouterPremiumProfile.ProposalSchemaVersion,
            OpenRouterPremiumCanonical.Bounds(profile.Bounds)));
    }

    public string CapabilityDigestSha256 { get; }
    public string ProfileDigestSha256 => Profile.ProfileDigestSha256;
    public long IssuedAtMilliseconds => Authorization.IssuedAtMilliseconds;
    public long ExpiresAtMilliseconds => Authorization.ExpiresAtMilliseconds;
    internal OpenRouterPremiumAuthorization Authorization { get; }
    internal OpenRouterPremiumProfile Profile { get; }
    internal CognitionQualityPromptEnvelopePublication Publication { get; }
    internal bool TryConsume() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
}

public sealed class OpenRouterPremiumEvidenceArtifact
{
    private readonly byte[] _canonicalUtf8;
    private readonly OpenRouterPremiumSlotReceipt[] _slots;

    internal OpenRouterPremiumEvidenceArtifact(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string status,
        int exchangeCount,
        int totalPromptTokens,
        int totalCompletionTokens,
        long totalSettledMicrousd,
        string? terminalCode,
        IEnumerable<OpenRouterPremiumSlotReceipt> slots)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        Status = status;
        ExchangeCount = exchangeCount;
        TotalPromptTokens = totalPromptTokens;
        TotalCompletionTokens = totalCompletionTokens;
        TotalSettledMicrousd = totalSettledMicrousd;
        TerminalCode = terminalCode;
        _slots = slots.Select(value => value.Detached()).ToArray();
        CanonicalDigestSha256 = OpenRouterPremiumCanonical.Digest(_canonicalUtf8);
    }

    public string SchemaVersion => OpenRouterPremiumEvidenceArtifactModule.SchemaVersion;
    public string Status { get; }
    public int ExchangeCount { get; }
    public int TotalPromptTokens { get; }
    public int TotalCompletionTokens { get; }
    public long TotalSettledMicrousd { get; }
    public string? TerminalCode { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
    public IReadOnlyList<OpenRouterPremiumSlotReceipt> Slots => Array.AsReadOnly(_slots.Select(value => value.Detached()).ToArray());
}

public static class OpenRouterPremiumEvidenceModule
{
    public static OpenRouterPremiumExecutionCapability Authorize(OpenRouterPremiumAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Resolve(authorization.ProfileIdentity);
        ValidateAuthorization(authorization, profile);
        CognitionQualityPromptEnvelopePublication publication;
        try { publication = CognitionQualityPromptEnvelopeBuilderModule.Create(OpenRouterPremiumProfile.PromptRevision); }
        catch (Exception) { throw new OpenRouterPremiumEvidenceException("prompt_registry_invalid"); }
        if (!string.Equals(publication.CanonicalDigestSha256, profile.PromptPublicationDigestSha256, StringComparison.Ordinal)
            || !string.Equals(publication.PromptSetDigestSha256, profile.PromptSetDigestSha256, StringComparison.Ordinal)
            || publication.Slots.Count != profile.Bounds.RequiredScenarioCount)
            throw new OpenRouterPremiumEvidenceException("prompt_registry_invalid");
        return new OpenRouterPremiumExecutionCapability(authorization, profile, publication);
    }

    public static async ValueTask<OpenRouterPremiumEvidenceArtifact> ExecuteOnceAsync(
        OpenRouterPremiumExecutionCapability capability,
        IOpenRouterPremiumExchange exchange,
        ICredentialLeaseSource credentialLeaseSource,
        IOpenRouterPremiumJournal financialJournal,
        IOpenRouterPremiumClock clock,
        CancellationToken cancellationToken = default) =>
        await ExecuteCoreOnceAsync(capability, exchange, credentialLeaseSource, financialJournal, clock,
            productionPermit: null, cancellationToken).ConfigureAwait(false);

    internal static async ValueTask<OpenRouterPremiumEvidenceArtifact> ExecuteAuthorizedProductionOnceAsync(
        OpenRouterPremiumExecutionCapability capability,
        OpenRouterPremiumHttpExchange exchange,
        ICredentialLeaseSource credentialLeaseSource,
        FileOpenRouterPremiumJournal financialJournal,
        IOpenRouterPremiumClock clock,
        OpenRouterPremiumProductionExecutionPermit productionPermit,
        CancellationToken cancellationToken = default) =>
        await ExecuteCoreOnceAsync(capability, exchange, credentialLeaseSource, financialJournal, clock,
            productionPermit ?? throw new ArgumentNullException(nameof(productionPermit)), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<OpenRouterPremiumEvidenceArtifact> ExecuteCoreOnceAsync(
        OpenRouterPremiumExecutionCapability capability,
        IOpenRouterPremiumExchange exchange,
        ICredentialLeaseSource credentialLeaseSource,
        IOpenRouterPremiumJournal financialJournal,
        IOpenRouterPremiumClock clock,
        OpenRouterPremiumProductionExecutionPermit? productionPermit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!capability.TryConsume()) throw new OpenRouterPremiumEvidenceException("capability_consumed");
        if (!OpenRouterPremiumNonceRegistry.TryConsume(capability.Authorization.AuthorizationNonce))
            throw new OpenRouterPremiumEvidenceException("authorization_nonce_consumed");
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(credentialLeaseSource);
        ArgumentNullException.ThrowIfNull(financialJournal);
        ArgumentNullException.ThrowIfNull(clock);
        OpenRouterPremiumExchangeRegistration exchangeRegistration = OpenRouterPremiumExchangeRegistry.Resolve(exchange);
        if (cancellationToken.IsCancellationRequested) throw new OpenRouterPremiumEvidenceException("capability_cancelled");
        if (clock.NowMilliseconds >= capability.ExpiresAtMilliseconds) throw new OpenRouterPremiumEvidenceException("capability_expired");
        ValidateExecutionBindings(capability, exchangeRegistration, credentialLeaseSource, financialJournal);
        if (exchangeRegistration.Kind == OpenRouterPremiumExchangeKind.ProductionHttp
            && (productionPermit is null || !productionPermit.Validate(capability, financialJournal, clock.NowMilliseconds)))
            throw new OpenRouterPremiumEvidenceException(OpenRouterPremiumProfile.LiveTrafficBlockerCode);
        if (exchangeRegistration.Kind == OpenRouterPremiumExchangeKind.ProductionHttp && !financialJournal.ProvidesDurableFlush)
            throw new OpenRouterPremiumEvidenceException("durable_journal_required");

        long remainingAuthorizationMilliseconds = capability.ExpiresAtMilliseconds - clock.NowMilliseconds;
        using CancellationTokenSource aggregateTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        aggregateTimeout.CancelAfter((int)Math.Min(capability.Profile.Bounds.TimeoutMilliseconds, remainingAuthorizationMilliseconds));
        CancellationToken executionToken = aggregateTimeout.Token;
        List<OpenRouterPremiumSlotReceipt> receipts = [];
        foreach (CognitionQualityPromptEnvelopeSlot slot in capability.Publication.Slots)
        {
            executionToken.ThrowIfCancellationRequested();
            int slotIndex = receipts.Count + 1;
            if (slotIndex > capability.Profile.Bounds.MaximumExchanges || !string.Equals(slot.ScenarioId, $"cq{slotIndex}", StringComparison.Ordinal))
                throw new OpenRouterPremiumEvidenceException("scenario_sequence_invalid");
            using OpenRouterPremiumExchangeRequest request = OpenRouterPremiumExchangeRequest.CreateForProfile(
                capability.Profile, slot, capability.CapabilityDigestSha256);
            string requestDigest = request.RequestDigestSha256;
            long version = financialJournal.Admit(slotIndex, slot.ScenarioId, slot.PromptDigestSha256, requestDigest,
                capability.Profile.Bounds.PerSlotCostCeilingMicrousd);
            CredentialLease? lease = null;
            try
            {
                CredentialLeaseRequest leaseRequest = BuildLeaseRequest(capability, slot, slotIndex, requestDigest, clock.NowMilliseconds);
                try { lease = await credentialLeaseSource.AcquireOnceAsync(leaseRequest, executionToken).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    OpenRouterPremiumSlotReceipt notSubmitted = PreDispatchFailure(slotIndex, slot, requestDigest, "credential_acquisition_cancelled");
                    financialJournal.CompleteBeforeDispatch(notSubmitted, version);
                    receipts.Add(notSubmitted);
                    break;
                }
                catch (Exception)
                {
                    OpenRouterPremiumSlotReceipt notSubmitted = PreDispatchFailure(slotIndex, slot, requestDigest, "credential_acquisition_failed");
                    financialJournal.CompleteBeforeDispatch(notSubmitted, version);
                    receipts.Add(notSubmitted);
                    break;
                }

                version = financialJournal.MarkDispatchUnknown(slotIndex, requestDigest, version);
                OpenRouterPremiumSlotReceipt receipt;
                try
                {
                    using OpenRouterPremiumExchangeResponse response = await lease.ExecuteOnceAsync(
                        clock.NowMilliseconds,
                        (credential, token) => exchange.ExchangeOnceAsync(request, credential, token),
                        executionToken).ConfigureAwait(false);
                    receipt = ParseResponse(response, capability.Profile, slotIndex, slot, requestDigest);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { receipt = Unknown(slotIndex, slot, requestDigest, "provider_timeout"); }
                catch (OperationCanceledException)
                { receipt = Unknown(slotIndex, slot, requestDigest, "provider_cancelled_unknown"); }
                catch (ProviderPreflightException exception) when (exception.ReasonCode is
                    ProviderPreflightReasonCode.LeaseExpired or ProviderPreflightReasonCode.LeaseInvalid or ProviderPreflightReasonCode.LeaseMisuse)
                { receipt = PreDispatchFailure(slotIndex, slot, requestDigest, "credential_lease_rejected"); }
                catch (Exception)
                { receipt = Unknown(slotIndex, slot, requestDigest, "provider_exchange_unknown"); }
                financialJournal.Complete(receipt, version);
                receipts.Add(receipt);
                if (!string.Equals(receipt.OutcomeCode, "premium_evidence_success", StringComparison.Ordinal)) break;
            }
            finally { lease?.Dispose(); }
        }

        return OpenRouterPremiumEvidenceArtifactModule.Create(capability, financialJournal.Header, exchangeRegistration.Identity, receipts);
    }

    private static OpenRouterPremiumSlotReceipt ParseResponse(
        OpenRouterPremiumExchangeResponse response,
        OpenRouterPremiumProfile profile,
        int slotIndex,
        CognitionQualityPromptEnvelopeSlot slot,
        string requestDigest)
    {
        if (!string.Equals(response.EffectiveUri, OpenRouterPremiumProfile.EffectiveUri, StringComparison.Ordinal)
            || response.ResponseHeaderBytes > profile.Bounds.MaximumResponseHeaderBytes
            || response.ResponseByteCount > profile.Bounds.MaximumResponseBytes)
            return Unknown(slotIndex, slot, requestDigest, "provider_exchange_binding_invalid");
        if (response.StatusCode == 0)
            return Unknown(slotIndex, slot, requestDigest, "provider_submission_unknown");
        try
        {
            return OpenRouterPremiumResponseParser.Parse(response.BodySpan, response.StatusCode, profile,
                slotIndex, slot.ScenarioId, slot.PromptDigestSha256, requestDigest);
        }
        catch (OpenRouterPremiumEvidenceException exception)
        {
            string digest = response.ResponseByteCount == 0 ? new string('0', 64) : OpenRouterPremiumCanonical.Digest(response.BodySpan);
            return OpenRouterPremiumResponseParser.Unknown(slotIndex, slot.ScenarioId, slot.PromptDigestSha256,
                requestDigest, digest, OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(exception));
        }
    }

    private static OpenRouterPremiumSlotReceipt Unknown(int slotIndex, CognitionQualityPromptEnvelopeSlot slot, string requestDigest, string outcome) =>
        OpenRouterPremiumResponseParser.Unknown(slotIndex, slot.ScenarioId, slot.PromptDigestSha256,
            requestDigest, new string('0', 64), outcome);

    private static OpenRouterPremiumSlotReceipt PreDispatchFailure(int index, CognitionQualityPromptEnvelopeSlot slot, string requestDigest, string outcome) =>
        new(index, slot.ScenarioId, slot.PromptDigestSha256, requestDigest, new string('0', 64),
            SubmissionState.DefinitelyNotSubmitted, ChargeState.Released, 0, 0, 0, 0, null, outcome);

    private static CredentialLeaseRequest BuildLeaseRequest(OpenRouterPremiumExecutionCapability capability, CognitionQualityPromptEnvelopeSlot slot, int index, string requestDigest, long now)
    {
        long expiry = checked(now + capability.Profile.Bounds.CredentialLeaseLifetimeMilliseconds);
        string nonce = $"{capability.Authorization.AuthorizationNonce}/cq{index}";
        string leaseDigest = OpenRouterPremiumCanonical.Digest(string.Join('|', capability.Authorization.AccountBinding.Value,
            capability.Profile.ProfileDigestSha256, "openrouter-api-account/v1", "openrouter.chat.completions",
            capability.CapabilityDigestSha256, capability.Authorization.FinancialJournalHeaderChecksumSha256,
            requestDigest, nonce, now, expiry, capability.Profile.Bounds.CredentialLeaseLifetimeMilliseconds));
        return new CredentialLeaseRequest(capability.Authorization.AccountBinding, capability.Profile.ProfileDigestSha256,
            "openrouter-api-account/v1", "openrouter.chat.completions", capability.CapabilityDigestSha256,
            capability.Authorization.FinancialJournalHeaderChecksumSha256, requestDigest, nonce, now, expiry,
            capability.Profile.Bounds.CredentialLeaseLifetimeMilliseconds, leaseDigest);
    }

    private static void ValidateAuthorization(OpenRouterPremiumAuthorization value, OpenRouterPremiumProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value.AccountBinding);
        if (!string.Equals(value.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(value.EndpointEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256, StringComparison.Ordinal)
            || !OpenRouterPremiumCanonical.IsIdentity(value.FinancialJournalIdentity)
            || !OpenRouterPremiumCanonical.IsDigest(value.FinancialJournalHeaderChecksumSha256)
            || !OpenRouterPremiumCanonical.IsIdentity(value.ExchangeIdentity)
            || !OpenRouterPremiumCanonical.IsDigest(value.ExchangeContractDigestSha256)
            || !OpenRouterPremiumCanonical.IsIdentity(value.CredentialLeaseSourceIdentity)
            || !OpenRouterPremiumCanonical.IsIdentity(value.AuthorizationNonce)
            || value.IssuedAtMilliseconds < 0 || value.ExpiresAtMilliseconds <= value.IssuedAtMilliseconds
            || value.ExpiresAtMilliseconds - value.IssuedAtMilliseconds > profile.Bounds.TimeoutMilliseconds)
            throw new OpenRouterPremiumEvidenceException("authorization_invalid");
    }

    private static void ValidateExecutionBindings(
        OpenRouterPremiumExecutionCapability capability,
        OpenRouterPremiumExchangeRegistration exchange,
        ICredentialLeaseSource source,
        IOpenRouterPremiumJournal journal)
    {
        OpenRouterPremiumAuthorization authorization = capability.Authorization;
        OpenRouterPremiumJournalHeader header = journal.Header;
        header.Validate();
        if (!string.Equals(exchange.Identity, authorization.ExchangeIdentity, StringComparison.Ordinal)
            || !string.Equals(exchange.ContractDigestSha256, authorization.ExchangeContractDigestSha256, StringComparison.Ordinal)
            || !string.Equals(source.Identity, authorization.CredentialLeaseSourceIdentity, StringComparison.Ordinal)
            || !string.Equals(journal.Identity, authorization.FinancialJournalIdentity, StringComparison.Ordinal)
            || !string.Equals(header.HeaderChecksumSha256, authorization.FinancialJournalHeaderChecksumSha256, StringComparison.Ordinal)
            || !string.Equals(header.ProfileDigestSha256, capability.Profile.ProfileDigestSha256, StringComparison.Ordinal)
            || !string.Equals(header.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(header.EndpointEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256, StringComparison.Ordinal)
            || !string.Equals(header.PromptSetDigestSha256, capability.Profile.PromptSetDigestSha256, StringComparison.Ordinal)
            || !string.Equals(header.AccountBindingIdentity, authorization.AccountBinding.Value, StringComparison.Ordinal)
            || header.MaximumSlots != capability.Profile.Bounds.RequiredScenarioCount
            || header.PerSlotCostCeilingMicrousd != capability.Profile.Bounds.PerSlotCostCeilingMicrousd
            || header.AggregateCostCeilingMicrousd != capability.Profile.Bounds.AggregateCostCeilingMicrousd)
            throw new OpenRouterPremiumEvidenceException("execution_binding_invalid");
    }
}

public static class OpenRouterPremiumEvidenceArtifactModule
{
    public const string SchemaVersion = "snow_globe_openrouter_premium_evidence_artifact/v1";
    public const int MaximumArtifactBytes = 64 * 1024;

    internal static OpenRouterPremiumEvidenceArtifact Create(
        OpenRouterPremiumExecutionCapability capability,
        OpenRouterPremiumJournalHeader journal,
        string exchangeIdentity,
        IReadOnlyList<OpenRouterPremiumSlotReceipt> slots)
    {
        string status = slots.Count == 12 && slots.All(slot => slot.OutcomeCode == "premium_evidence_success") ? "complete" : "terminal";
        string? terminal = status == "terminal" ? slots.LastOrDefault()?.OutcomeCode ?? "no_exchange" : null;
        byte[] payload = Write(capability.Profile, capability.CapabilityDigestSha256, capability.Authorization.AccountBinding.Value,
            journal, exchangeIdentity, status, terminal, slots, null);
        string payloadDigest = OpenRouterPremiumCanonical.Digest(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(capability.Profile, capability.CapabilityDigestSha256, capability.Authorization.AccountBinding.Value,
            journal, exchangeIdentity, status, terminal, slots, payloadDigest);
        if (canonical.Length is < 1 or > MaximumArtifactBytes) throw new OpenRouterPremiumEvidenceException("artifact_size_invalid");
        return New(canonical, payloadDigest, status, terminal, slots);
    }

    public static OpenRouterPremiumEvidenceArtifact Validate(ReadOnlyMemory<byte> canonicalArtifactUtf8)
    {
        byte[] bytes = canonicalArtifactUtf8.ToArray();
        if (bytes.Length is < 1 or > MaximumArtifactBytes) throw new OpenRouterPremiumEvidenceException("artifact_rejected");
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            RejectDuplicateProperties(bytes);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 25
                || root.GetProperty("schema_version").GetString() != SchemaVersion)
                throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
            if (root.GetProperty("profile_digest_sha256").GetString() != profile.ProfileDigestSha256
                || root.GetProperty("catalog_evidence_digest_sha256").GetString() != OpenRouterPremiumProfile.CatalogEvidenceDigestSha256
                || root.GetProperty("endpoint_evidence_digest_sha256").GetString() != OpenRouterPremiumProfile.EndpointEvidenceDigestSha256
                || root.GetProperty("model_identity").GetString() != OpenRouterPremiumProfile.CanonicalModelSlug
                || root.GetProperty("provider_identity").GetString() != OpenRouterPremiumProfile.ProviderResponseIdentity
                || root.GetProperty("prompt_publication_digest_sha256").GetString() != profile.PromptPublicationDigestSha256
                || root.GetProperty("prompt_set_digest_sha256").GetString() != profile.PromptSetDigestSha256
                || root.GetProperty("corpus_digest_sha256").GetString() != profile.CorpusDigestSha256
                || root.GetProperty("scoring_digest_sha256").GetString() != profile.ScoringDigestSha256
                || root.GetProperty("proposal_schema_version").GetString() != OpenRouterPremiumProfile.ProposalSchemaVersion)
                throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            string status = root.GetProperty("status").GetString()!;
            string? terminal = root.GetProperty("terminal_code").ValueKind == JsonValueKind.Null ? null : root.GetProperty("terminal_code").GetString();
            string capabilityDigest = root.GetProperty("capability_digest_sha256").GetString()!;
            string account = root.GetProperty("account_binding_identity").GetString()!;
            string exchange = root.GetProperty("exchange_identity").GetString()!;
            OpenRouterPremiumJournalHeader journal = new(OpenRouterPremiumJournalHeader.CurrentSchemaVersion,
                root.GetProperty("journal_identity").GetString()!, root.GetProperty("run_identity").GetString()!,
                profile.ProfileDigestSha256, OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
                profile.PromptSetDigestSha256, account, 12, profile.Bounds.PerSlotCostCeilingMicrousd,
                profile.Bounds.AggregateCostCeilingMicrousd, root.GetProperty("journal_header_checksum_sha256").GetString()!);
            journal.Validate();
            List<OpenRouterPremiumSlotReceipt> slots = ParseSlots(root.GetProperty("slots"));
            ValidateArtifactSemantics(root, profile, status, terminal, capabilityDigest, account, exchange, slots);
            string payloadDigest = root.GetProperty("artifact_payload_digest_sha256").GetString()!;
            byte[] payload = Write(profile, capabilityDigest, account, journal, exchange, status, terminal, slots, null);
            string expectedPayload = OpenRouterPremiumCanonical.Digest(payload); CryptographicOperations.ZeroMemory(payload);
            byte[] canonical = Write(profile, capabilityDigest, account, journal, exchange, status, terminal, slots, payloadDigest);
            if (!OpenRouterPremiumCanonical.IsDigest(payloadDigest) || expectedPayload != payloadDigest || !canonical.AsSpan().SequenceEqual(bytes))
                throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            return New(canonical, payloadDigest, status, terminal, slots);
        }
        catch (OpenRouterPremiumEvidenceException) { throw; }
        catch (Exception) { throw new OpenRouterPremiumEvidenceException("artifact_rejected"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static OpenRouterPremiumEvidenceArtifact New(byte[] canonical, string payloadDigest, string status, string? terminal, IReadOnlyList<OpenRouterPremiumSlotReceipt> slots) =>
        new(canonical, payloadDigest, status, slots.Count, slots.Sum(value => value.PromptTokens),
            slots.Sum(value => value.CompletionTokens), slots.Sum(value => value.SettledMicrousd), terminal, slots);

    private static byte[] Write(
        OpenRouterPremiumProfile profile,
        string capabilityDigest,
        string accountBinding,
        OpenRouterPremiumJournalHeader journal,
        string exchangeIdentity,
        string status,
        string? terminal,
        IReadOnlyList<OpenRouterPremiumSlotReceipt> slots,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion); writer.WriteString("status", status);
        writer.WriteString("profile_digest_sha256", profile.ProfileDigestSha256);
        writer.WriteString("catalog_evidence_digest_sha256", OpenRouterPremiumProfile.CatalogEvidenceDigestSha256);
        writer.WriteString("endpoint_evidence_digest_sha256", OpenRouterPremiumProfile.EndpointEvidenceDigestSha256);
        writer.WriteString("model_identity", OpenRouterPremiumProfile.CanonicalModelSlug); writer.WriteString("provider_identity", OpenRouterPremiumProfile.ProviderResponseIdentity);
        writer.WriteString("prompt_publication_digest_sha256", profile.PromptPublicationDigestSha256);
        writer.WriteString("prompt_set_digest_sha256", profile.PromptSetDigestSha256);
        writer.WriteString("corpus_digest_sha256", profile.CorpusDigestSha256); writer.WriteString("scoring_digest_sha256", profile.ScoringDigestSha256);
        writer.WriteString("proposal_schema_version", OpenRouterPremiumProfile.ProposalSchemaVersion); writer.WriteString("capability_digest_sha256", capabilityDigest);
        writer.WriteString("account_binding_identity", accountBinding); writer.WriteString("journal_identity", journal.JournalIdentity);
        writer.WriteString("run_identity", journal.RunIdentity); writer.WriteString("journal_header_checksum_sha256", journal.HeaderChecksumSha256);
        writer.WriteString("exchange_identity", exchangeIdentity); writer.WriteNumber("exchange_count", slots.Count);
        writer.WriteNumber("total_prompt_tokens", slots.Sum(value => value.PromptTokens)); writer.WriteNumber("total_completion_tokens", slots.Sum(value => value.CompletionTokens));
        writer.WriteNumber("total_settled_microusd", slots.Sum(value => value.SettledMicrousd));
        if (terminal is null) writer.WriteNull("terminal_code"); else writer.WriteString("terminal_code", terminal);
        writer.WritePropertyName("slots"); writer.WriteStartArray(); foreach (OpenRouterPremiumSlotReceipt slot in slots) WriteSlot(writer, slot); writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("artifact_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteSlot(Utf8JsonWriter writer, OpenRouterPremiumSlotReceipt slot)
    {
        writer.WriteStartObject(); writer.WriteNumber("slot_index", slot.SlotIndex); writer.WriteString("scenario_id", slot.ScenarioId);
        writer.WriteString("prompt_digest_sha256", slot.PromptDigestSha256); writer.WriteString("request_digest_sha256", slot.RequestDigestSha256);
        writer.WriteString("response_digest_sha256", slot.ResponseDigestSha256); writer.WriteString("submission_state", slot.SubmissionState.ToString());
        writer.WriteString("charge_state", slot.ChargeState.ToString()); writer.WriteNumber("prompt_tokens", slot.PromptTokens);
        writer.WriteNumber("completion_tokens", slot.CompletionTokens); writer.WriteNumber("total_tokens", slot.TotalTokens);
        writer.WriteNumber("settled_microusd", slot.SettledMicrousd); writer.WriteString("outcome_code", slot.OutcomeCode);
        writer.WritePropertyName("proposal");
        if (slot.Proposal is null) writer.WriteNullValue(); else
        {
            writer.WriteStartObject(); writer.WriteString("agent_id", slot.Proposal.AgentId); writer.WriteString("action", slot.Proposal.Action.ToString());
            writer.WriteNumber("quantity", slot.Proposal.Quantity); writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static List<OpenRouterPremiumSlotReceipt> ParseSlots(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 12) throw new OpenRouterPremiumEvidenceException("artifact_rejected");
        List<OpenRouterPremiumSlotReceipt> slots = [];
        foreach (JsonElement value in array.EnumerateArray())
        {
            if (value.EnumerateObject().Count() != 13) throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            int index = value.GetProperty("slot_index").GetInt32(); string scenario = value.GetProperty("scenario_id").GetString()!;
            SnowGlobeActionProposal? proposal = null; JsonElement proposalElement = value.GetProperty("proposal");
            if (proposalElement.ValueKind == JsonValueKind.Object)
            {
                if (!Enum.TryParse(proposalElement.GetProperty("action").GetString(), false, out SnowGlobeActionKind action)) throw new OpenRouterPremiumEvidenceException("artifact_rejected");
                proposal = new(proposalElement.GetProperty("agent_id").GetString()!, action, proposalElement.GetProperty("quantity").GetInt32());
            }
            slots.Add(new(index, scenario, value.GetProperty("prompt_digest_sha256").GetString()!, value.GetProperty("request_digest_sha256").GetString()!,
                value.GetProperty("response_digest_sha256").GetString()!, Enum.Parse<SubmissionState>(value.GetProperty("submission_state").GetString()!, false),
                Enum.Parse<ChargeState>(value.GetProperty("charge_state").GetString()!, false), value.GetProperty("prompt_tokens").GetInt32(),
                value.GetProperty("completion_tokens").GetInt32(), value.GetProperty("total_tokens").GetInt32(), value.GetProperty("settled_microusd").GetInt64(),
                proposal, value.GetProperty("outcome_code").GetString()!));
        }
        return slots;
    }

    private static void ValidateArtifactSemantics(
        JsonElement root,
        OpenRouterPremiumProfile profile,
        string status,
        string? terminal,
        string capabilityDigest,
        string account,
        string exchange,
        IReadOnlyList<OpenRouterPremiumSlotReceipt> slots)
    {
        if (status is not ("complete" or "terminal")
            || !OpenRouterPremiumCanonical.IsDigest(capabilityDigest)
            || !OpenRouterPremiumCanonical.IsIdentity(exchange)
            || root.GetProperty("exchange_count").GetInt32() != slots.Count
            || root.GetProperty("total_prompt_tokens").GetInt32() != slots.Sum(value => value.PromptTokens)
            || root.GetProperty("total_completion_tokens").GetInt32() != slots.Sum(value => value.CompletionTokens)
            || root.GetProperty("total_settled_microusd").GetInt64() != slots.Sum(value => value.SettledMicrousd))
            throw new OpenRouterPremiumEvidenceException("artifact_rejected");
        try { _ = new ByokAccountBindingIdentity(account); }
        catch (ArgumentException) { throw new OpenRouterPremiumEvidenceException("artifact_rejected"); }
        IReadOnlyList<CognitionQualityPromptEnvelopeSlot> prompts = CognitionQualityPromptEnvelopeBuilderModule.Create(OpenRouterPremiumProfile.PromptRevision).Slots;
        for (int index = 0; index < slots.Count; index++)
        {
            OpenRouterPremiumSlotReceipt slot = slots[index];
            CognitionQualityPromptEnvelopeSlot prompt = prompts[index];
            bool financialTuple = (slot.SubmissionState, slot.ChargeState) switch
            {
                (SubmissionState.ResponseReceived, ChargeState.Settled) => true,
                (SubmissionState.DefinitelyNotSubmitted, ChargeState.Released) => true,
                (SubmissionState.SubmissionUnknown, ChargeState.Unknown) => true,
                _ => false
            };
            if (slot.SlotIndex != index + 1 || slot.ScenarioId != $"cq{index + 1}"
                || slot.ScenarioId != prompt.ScenarioId || slot.PromptDigestSha256 != prompt.PromptDigestSha256
                || !OpenRouterPremiumCanonical.IsDigest(slot.RequestDigestSha256)
                || !OpenRouterPremiumCanonical.IsDigest(slot.ResponseDigestSha256)
                || !OpenRouterPremiumCanonical.IsIdentity(slot.OutcomeCode)
                || !financialTuple || slot.PromptTokens < 0 || slot.PromptTokens > profile.Bounds.MaximumInputTokens
                || slot.CompletionTokens < 0 || slot.CompletionTokens > profile.Bounds.MaximumOutputTokens
                || slot.TotalTokens != slot.PromptTokens + slot.CompletionTokens
                || slot.SettledMicrousd < 0 || slot.SettledMicrousd > profile.Bounds.PerSlotCostCeilingMicrousd
                || (slot.OutcomeCode == "premium_evidence_success") != (slot.Proposal is not null)
                || (slot.OutcomeCode == "premium_evidence_success" && (slot.SubmissionState != SubmissionState.ResponseReceived || slot.ChargeState != ChargeState.Settled)))
                throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            if (slot.Proposal is not null)
            {
                CognitionQualityScenario scenario = CognitionQualityCorpusV1.CreateSnapshot().Scenarios[index];
                bool proposalShape = slot.Proposal.Action switch
                {
                    SnowGlobeActionKind.Idle or SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage => slot.Proposal.Quantity == 0,
                    SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone or SnowGlobeActionKind.MaintainShelter => slot.Proposal.Quantity is >= 1 and <= 64,
                    _ => false
                };
                if (!proposalShape || slot.Proposal.AgentId != scenario.Observation.AgentId)
                    throw new OpenRouterPremiumEvidenceException("artifact_rejected");
            }
        }
        bool complete = status == "complete" && terminal is null && slots.Count == 12 && slots.All(value => value.OutcomeCode == "premium_evidence_success");
        bool terminalValid = status == "terminal" && slots.Count is >= 1 and <= 12 && terminal == slots[^1].OutcomeCode
            && slots.Take(slots.Count - 1).All(value => value.OutcomeCode == "premium_evidence_success")
            && slots[^1].OutcomeCode != "premium_evidence_success";
        if (!complete && !terminalValid) throw new OpenRouterPremiumEvidenceException("artifact_rejected");
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !stack.Peek().Add(reader.GetString()!))
                throw new OpenRouterPremiumEvidenceException("artifact_rejected");
        }
    }
}

internal static class OpenRouterPremiumNonceRegistry
{
    private const int MaximumRememberedNonces = 4096;
    private static readonly object Gate = new();
    private static readonly HashSet<string> Consumed = new(StringComparer.Ordinal);

    internal static bool TryConsume(string nonce)
    {
        lock (Gate)
        {
            if (Consumed.Contains(nonce)) return false;
            if (Consumed.Count >= MaximumRememberedNonces) throw new OpenRouterPremiumEvidenceException("authorization_nonce_capacity_exhausted");
            Consumed.Add(nonce);
            return true;
        }
    }
}
