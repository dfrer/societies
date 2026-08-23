namespace Societies.SnowGlobe;

public sealed record OpenRouterPremiumJournalHeader(
    string SchemaVersion,
    string JournalIdentity,
    string RunIdentity,
    string ProfileDigestSha256,
    string CatalogEvidenceDigestSha256,
    string EndpointEvidenceDigestSha256,
    string PromptSetDigestSha256,
    string AccountBindingIdentity,
    int MaximumSlots,
    long PerSlotCostCeilingMicrousd,
    long AggregateCostCeilingMicrousd,
    string HeaderChecksumSha256)
{
    public const string CurrentSchemaVersion = "snow_globe_openrouter_premium_journal/v1";

    public static OpenRouterPremiumJournalHeader Create(
        string journalIdentity,
        string runIdentity,
        OpenRouterPremiumProfile profile,
        ByokAccountBindingIdentity accountBinding)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(accountBinding);
        OpenRouterPremiumJournalHeader header = new(
            CurrentSchemaVersion, journalIdentity, runIdentity, profile.ProfileDigestSha256,
            OpenRouterPremiumProfile.CatalogEvidenceDigestSha256, OpenRouterPremiumProfile.EndpointEvidenceDigestSha256,
            profile.PromptSetDigestSha256, accountBinding.Value, profile.Bounds.RequiredScenarioCount,
            profile.Bounds.PerSlotCostCeilingMicrousd, profile.Bounds.AggregateCostCeilingMicrousd, string.Empty);
        header.Validate(includeChecksum: false);
        return header with { HeaderChecksumSha256 = Checksum(header) };
    }

    public void Validate(bool includeChecksum = true)
    {
        if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal)
            || !OpenRouterPremiumCanonical.IsIdentity(JournalIdentity)
            || !OpenRouterPremiumCanonical.IsIdentity(RunIdentity)
            || !OpenRouterPremiumCanonical.IsDigest(ProfileDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(CatalogEvidenceDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(EndpointEvidenceDigestSha256)
            || !OpenRouterPremiumCanonical.IsDigest(PromptSetDigestSha256)
            || !AccountBindingIdentity.StartsWith("byok-account-sha256-", StringComparison.Ordinal)
            || AccountBindingIdentity.Length != "byok-account-sha256-".Length + 64
            || !OpenRouterPremiumCanonical.IsDigest(AccountBindingIdentity["byok-account-sha256-".Length..])
            || MaximumSlots != 12 || PerSlotCostCeilingMicrousd <= 0
            || AggregateCostCeilingMicrousd != PerSlotCostCeilingMicrousd * MaximumSlots)
            throw new InvalidDataException("OpenRouter premium journal header is invalid.");
        if (includeChecksum && !string.Equals(HeaderChecksumSha256, Checksum(this), StringComparison.Ordinal))
            throw new InvalidDataException("OpenRouter premium journal header checksum mismatch.");
    }

    private static string Checksum(OpenRouterPremiumJournalHeader value) => OpenRouterPremiumCanonical.Digest(string.Join('|',
        value.SchemaVersion, value.JournalIdentity, value.RunIdentity, value.ProfileDigestSha256,
        value.CatalogEvidenceDigestSha256, value.EndpointEvidenceDigestSha256, value.PromptSetDigestSha256,
        value.AccountBindingIdentity, value.MaximumSlots, value.PerSlotCostCeilingMicrousd,
        value.AggregateCostCeilingMicrousd));
}

public sealed record OpenRouterPremiumSlotReceipt(
    int SlotIndex,
    string ScenarioId,
    string PromptDigestSha256,
    string RequestDigestSha256,
    string ResponseDigestSha256,
    SubmissionState SubmissionState,
    ChargeState ChargeState,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long SettledMicrousd,
    SnowGlobeActionProposal? Proposal,
    string OutcomeCode)
{
    internal OpenRouterPremiumSlotReceipt Detached() => this with { Proposal = Proposal is null ? null : Proposal with { } };
}

public sealed record OpenRouterPremiumJournalSlotSnapshot(
    int SlotIndex,
    string ScenarioId,
    string PromptDigestSha256,
    string RequestDigestSha256,
    long Version,
    long ReservedMicrousd,
    SubmissionState SubmissionState,
    ChargeState ChargeState,
    OpenRouterPremiumSlotReceipt? Receipt);

public sealed record OpenRouterPremiumJournalSnapshot(
    OpenRouterPremiumJournalHeader Header,
    IReadOnlyList<OpenRouterPremiumJournalSlotSnapshot> Slots,
    long ReservedExposureMicrousd,
    long SettledMicrousd,
    IReadOnlyList<string> Trace);

public interface IOpenRouterPremiumJournal
{
    string Identity { get; }
    bool ProvidesDurableFlush { get; }
    OpenRouterPremiumJournalHeader Header { get; }
    long Admit(int slotIndex, string scenarioId, string promptDigestSha256, string requestDigestSha256, long reservedMicrousd);
    long CompleteBeforeDispatch(OpenRouterPremiumSlotReceipt receipt, long expectedVersion);
    long MarkDispatchUnknown(int slotIndex, string requestDigestSha256, long expectedVersion);
    long Complete(OpenRouterPremiumSlotReceipt receipt, long expectedVersion);
    OpenRouterPremiumJournalSnapshot Snapshot();
}

/// <summary>
/// Offline journal fake using the same conservative transitions required of a future durable
/// implementation. Live transports are rejected unless the journal reports durable flush support.
/// </summary>
public sealed class InMemoryOpenRouterPremiumJournal : IOpenRouterPremiumJournal
{
    private readonly object _gate = new();
    private readonly List<MutableSlot> _slots = [];
    private readonly List<string> _trace = [];

    public InMemoryOpenRouterPremiumJournal(OpenRouterPremiumJournalHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        header.Validate();
        Header = header with { };
    }

    public string Identity => Header.JournalIdentity;
    public bool ProvidesDurableFlush => false;
    public OpenRouterPremiumJournalHeader Header { get; }

    public long Admit(int slotIndex, string scenarioId, string promptDigestSha256, string requestDigestSha256, long reservedMicrousd)
    {
        lock (_gate)
        {
            ValidateSlotIdentity(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256);
            if (slotIndex != _slots.Count + 1 || slotIndex > Header.MaximumSlots)
                throw new OpenRouterPremiumEvidenceException("journal_sequence_invalid");
            if (reservedMicrousd != Header.PerSlotCostCeilingMicrousd)
                throw new OpenRouterPremiumEvidenceException("journal_authority_invalid");
            long exposure = checked(_slots.Sum(value => value.ChargeState is ChargeState.Reserved or ChargeState.Unknown ? value.ReservedMicrousd : value.Receipt?.SettledMicrousd ?? 0) + reservedMicrousd);
            if (exposure > Header.AggregateCostCeilingMicrousd)
                throw new OpenRouterPremiumEvidenceException("journal_authority_denied");
            _slots.Add(new MutableSlot(slotIndex, scenarioId, promptDigestSha256, requestDigestSha256, 1, reservedMicrousd,
                SubmissionState.Dispatching, ChargeState.Reserved));
            _trace.Add($"{scenarioId}/admitted");
            return 1;
        }
    }

    public long MarkDispatchUnknown(int slotIndex, string requestDigestSha256, long expectedVersion)
    {
        lock (_gate)
        {
            MutableSlot slot = Existing(slotIndex, requestDigestSha256);
            if (slot.Version != expectedVersion || slot.SubmissionState != SubmissionState.Dispatching || slot.ChargeState != ChargeState.Reserved)
                throw new OpenRouterPremiumEvidenceException("journal_transition_conflict");
            slot.Version++;
            slot.SubmissionState = SubmissionState.SubmissionUnknown;
            slot.ChargeState = ChargeState.Unknown;
            _trace.Add($"{slot.ScenarioId}/dispatch_unknown");
            return slot.Version;
        }
    }

    public long CompleteBeforeDispatch(OpenRouterPremiumSlotReceipt receipt, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        lock (_gate)
        {
            ValidateReceipt(receipt);
            MutableSlot slot = Existing(receipt.SlotIndex, receipt.RequestDigestSha256);
            if (slot.Version != expectedVersion || slot.Receipt is not null
                || slot.SubmissionState != SubmissionState.Dispatching || slot.ChargeState != ChargeState.Reserved
                || receipt.SubmissionState != SubmissionState.DefinitelyNotSubmitted || receipt.ChargeState != ChargeState.Released
                || !string.Equals(slot.ScenarioId, receipt.ScenarioId, StringComparison.Ordinal)
                || !string.Equals(slot.PromptDigestSha256, receipt.PromptDigestSha256, StringComparison.Ordinal))
                throw new OpenRouterPremiumEvidenceException("journal_transition_conflict");
            slot.Version++;
            slot.SubmissionState = receipt.SubmissionState;
            slot.ChargeState = receipt.ChargeState;
            slot.Receipt = receipt.Detached();
            _trace.Add($"{slot.ScenarioId}/completed/{receipt.OutcomeCode}");
            return slot.Version;
        }
    }

    public long Complete(OpenRouterPremiumSlotReceipt receipt, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        lock (_gate)
        {
            ValidateReceipt(receipt);
            MutableSlot slot = Existing(receipt.SlotIndex, receipt.RequestDigestSha256);
            if (slot.Version != expectedVersion || slot.Receipt is not null
                || slot.SubmissionState != SubmissionState.SubmissionUnknown || slot.ChargeState != ChargeState.Unknown
                || !string.Equals(slot.ScenarioId, receipt.ScenarioId, StringComparison.Ordinal)
                || !string.Equals(slot.PromptDigestSha256, receipt.PromptDigestSha256, StringComparison.Ordinal))
                throw new OpenRouterPremiumEvidenceException("journal_transition_conflict");
            slot.Version++;
            slot.SubmissionState = receipt.SubmissionState;
            slot.ChargeState = receipt.ChargeState;
            slot.Receipt = receipt.Detached();
            _trace.Add($"{slot.ScenarioId}/completed/{receipt.OutcomeCode}");
            return slot.Version;
        }
    }

    public OpenRouterPremiumJournalSnapshot Snapshot()
    {
        lock (_gate)
        {
            OpenRouterPremiumJournalSlotSnapshot[] slots = _slots.Select(value => new OpenRouterPremiumJournalSlotSnapshot(
                value.SlotIndex, value.ScenarioId, value.PromptDigestSha256, value.RequestDigestSha256,
                value.Version, value.ReservedMicrousd, value.SubmissionState, value.ChargeState,
                value.Receipt?.Detached())).ToArray();
            long exposure = slots.Sum(value => value.ChargeState is ChargeState.Reserved or ChargeState.Unknown ? value.ReservedMicrousd : 0);
            long settled = slots.Sum(value => value.Receipt?.SettledMicrousd ?? 0);
            return new(Header with { }, Array.AsReadOnly(slots), exposure, settled, Array.AsReadOnly(_trace.ToArray()));
        }
    }

    private MutableSlot Existing(int slotIndex, string requestDigestSha256)
    {
        if (slotIndex < 1 || slotIndex > _slots.Count || !OpenRouterPremiumCanonical.IsDigest(requestDigestSha256))
            throw new OpenRouterPremiumEvidenceException("journal_transition_conflict");
        MutableSlot slot = _slots[slotIndex - 1];
        if (!string.Equals(slot.RequestDigestSha256, requestDigestSha256, StringComparison.Ordinal))
            throw new OpenRouterPremiumEvidenceException("journal_transition_conflict");
        return slot;
    }

    private static void ValidateSlotIdentity(int slotIndex, string scenarioId, string promptDigestSha256, string requestDigestSha256)
    {
        if (slotIndex is < 1 or > 12 || !string.Equals(scenarioId, $"cq{slotIndex}", StringComparison.Ordinal)
            || !OpenRouterPremiumCanonical.IsDigest(promptDigestSha256) || !OpenRouterPremiumCanonical.IsDigest(requestDigestSha256))
            throw new OpenRouterPremiumEvidenceException("journal_binding_invalid");
    }

    private void ValidateReceipt(OpenRouterPremiumSlotReceipt value)
    {
        ValidateSlotIdentity(value.SlotIndex, value.ScenarioId, value.PromptDigestSha256, value.RequestDigestSha256);
        if (!OpenRouterPremiumCanonical.IsDigest(value.ResponseDigestSha256)
            || !OpenRouterPremiumCanonical.IsIdentity(value.OutcomeCode)
            || value.PromptTokens < 0 || value.PromptTokens > OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumInputTokens
            || value.CompletionTokens < 0 || value.CompletionTokens > OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumOutputTokens
            || value.TotalTokens != value.PromptTokens + value.CompletionTokens
            || value.SettledMicrousd < 0 || value.SettledMicrousd > Header.PerSlotCostCeilingMicrousd
            || (value.ChargeState == ChargeState.Settled && value.SubmissionState != SubmissionState.ResponseReceived)
            || (value.ChargeState == ChargeState.Released && value.SubmissionState != SubmissionState.DefinitelyNotSubmitted)
            || (value.ChargeState == ChargeState.Unknown && value.SubmissionState != SubmissionState.SubmissionUnknown))
            throw new OpenRouterPremiumEvidenceException("journal_receipt_invalid");
    }

    private sealed class MutableSlot(
        int slotIndex,
        string scenarioId,
        string promptDigestSha256,
        string requestDigestSha256,
        long version,
        long reservedMicrousd,
        SubmissionState submissionState,
        ChargeState chargeState)
    {
        internal int SlotIndex { get; } = slotIndex;
        internal string ScenarioId { get; } = scenarioId;
        internal string PromptDigestSha256 { get; } = promptDigestSha256;
        internal string RequestDigestSha256 { get; } = requestDigestSha256;
        internal long Version { get; set; } = version;
        internal long ReservedMicrousd { get; } = reservedMicrousd;
        internal SubmissionState SubmissionState { get; set; } = submissionState;
        internal ChargeState ChargeState { get; set; } = chargeState;
        internal OpenRouterPremiumSlotReceipt? Receipt { get; set; }
    }
}
