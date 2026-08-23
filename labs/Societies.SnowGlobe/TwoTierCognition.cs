using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Societies.SnowGlobe;

/// <summary>Whether a proposal is obtained by the baseline local adapter or the bounded premium lane.</summary>
public enum CognitionLane { Local, Premium }
public enum SubmissionState { NotApplicable, Dispatching, ResponseReceived, DefinitelyNotSubmitted, SubmissionUnknown }
public enum ChargeState { NotApplicable, Reserved, Released, Settled, Unknown }
public enum PremiumResponseStatus { Success, Rejected, TimedOut, Malformed }

/// <summary>
/// Immutable, pinned policy used for a single premium execution. It contains no endpoint, secret,
/// arbitrary prompt, retry control, or runtime-selected model.
/// </summary>
public sealed record ModelPolicySnapshot(
    string PolicyId,
    string ProviderHost,
    string Route,
    string PremiumModelIdentity,
    string PremiumModelRevisionIdentity,
    string PromptRevision,
    string ProposalSchemaVersion,
    string LocalAdapterIdentity,
    string Currency,
    long InputMicrousdPerMillionTokens,
    long OutputMicrousdPerMillionTokens,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    long CostCeilingMicrousd,
    int TimeoutMilliseconds,
    bool RedirectsAllowed = false,
    bool AutomaticRetriesAllowed = false)
{
    public string Digest => CognitionCanonical.Digest(CognitionCanonical.Policy(this));

    public void Validate()
    {
        RequireCanonicalIdentity(PolicyId, nameof(PolicyId));
        RequireHost(ProviderHost);
        RequireCanonicalIdentity(Route, nameof(Route));
        RequireCanonicalIdentity(PremiumModelIdentity, nameof(PremiumModelIdentity));
        RequireContentAddress(PremiumModelRevisionIdentity, nameof(PremiumModelRevisionIdentity));
        RequireCanonicalIdentity(PromptRevision, nameof(PromptRevision));
        RequireCanonicalIdentity(ProposalSchemaVersion, nameof(ProposalSchemaVersion));
        RequireCanonicalIdentity(LocalAdapterIdentity, nameof(LocalAdapterIdentity));
        if (!string.Equals(Currency, "usd", StringComparison.Ordinal)) throw new ArgumentException("Only usd is accepted.", nameof(Currency));
        if (RedirectsAllowed || AutomaticRetriesAllowed) throw new ArgumentException("Premium policy must deny redirects and automatic retries.");
        if (InputMicrousdPerMillionTokens < 0 || OutputMicrousdPerMillionTokens < 0 || MaximumInputTokens < 0 || MaximumOutputTokens < 0 || CostCeilingMicrousd < 0 || TimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(CostCeilingMicrousd));
        long maximum = CalculateCostMicrousd(MaximumInputTokens, MaximumOutputTokens);
        if (maximum != CostCeilingMicrousd) throw new ArgumentException("Cost ceiling must equal the pinned maximum token cost.", nameof(CostCeilingMicrousd));
    }

    public long CalculateCostMicrousd(int inputTokens, int outputTokens)
    {
        if (inputTokens < 0 || outputTokens < 0) throw new ArgumentOutOfRangeException(inputTokens < 0 ? nameof(inputTokens) : nameof(outputTokens));
        try
        {
            checked
            {
                return (inputTokens * InputMicrousdPerMillionTokens + 999_999L) / 1_000_000L
                    + (outputTokens * OutputMicrousdPerMillionTokens + 999_999L) / 1_000_000L;
            }
        }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(inputTokens), "Pinned token pricing overflows micro-USD."); }
    }

    private static void RequireHost(string value)
    {
        RequireCanonicalIdentity(value, nameof(ProviderHost));
        if (value.Contains("/", StringComparison.Ordinal)
            || value.StartsWith(".", StringComparison.Ordinal)
            || value.EndsWith(".", StringComparison.Ordinal)
            || value.Split('.').Any(label => label.Length is 0 or > 63 || label.StartsWith("-", StringComparison.Ordinal) || label.EndsWith("-", StringComparison.Ordinal)))
            throw new ArgumentException("Provider host must be a fixed canonical host name.", nameof(ProviderHost));
    }

    private static void RequireCanonicalIdentity(string value, string parameter)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(value))
            throw new ArgumentException("Policy values must be bounded canonical identities.", parameter);
    }

    private static void RequireContentAddress(string value, string parameter)
    {
        if (value is null || value.Length != 71 || !value.StartsWith("sha256-", StringComparison.Ordinal)
            || value.Skip(7).Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("Premium model revision identity must be sha256- followed by 64 lowercase hexadecimal characters.", parameter);
    }
}

/// <summary>Value-only immutable job; no provider URL, prompt, credential, header, price, or retry option crosses this seam.</summary>
public sealed record PremiumCognitionJob(
    string IdempotencyKey,
    string JobDigest,
    string PolicyDigest,
    string PremiumModelIdentity,
    string PremiumModelRevisionIdentity,
    SnowGlobeObservation Observation);
public sealed record CostReservation(long ReservedMicrousd, long SettledMicrousd);
public sealed record InferenceReceipt(
    string IdempotencyKey,
    string JobDigest,
    string PolicyDigest,
    string FinancialJournalIdentity,
    CognitionLane RequestedLane,
    string PremiumModelIdentity,
    string PremiumModelRevisionIdentity,
    SubmissionState SubmissionState,
    ChargeState ChargeState,
    CostReservation Reservation,
    string ReasonCode,
    string PrimaryOutcomeCode,
    SnowGlobeActionProposal Proposal);

/// <summary>Read-only evidence surface. Returned values are detached copies.</summary>
public interface ICognitionReceiptInspector
{
    IReadOnlyList<InferenceReceipt> SnapshotReceipts();
    IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs();
    IReadOnlyList<string> SnapshotTrace();
}

/// <summary>Offline-only external seam. A future transport adapter must be separately reviewed.</summary>
public interface IPremiumCognitionProvider
{
    ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken);
}

public sealed record PremiumCognitionProviderResult(
    SubmissionState SubmissionState,
    PremiumResponseStatus Status,
    string EffectiveHost,
    string EffectiveRoute,
    string EffectiveModelIdentity,
    string EffectiveModelRevisionIdentity,
    bool Redirected,
    int InputTokens,
    int OutputTokens,
    SnowGlobeActionProposal? Proposal,
    string ReasonCode = "provider_rejected");

/// <summary>Deterministic injected fake. It performs no I/O and records a bounded trace for tests.</summary>
public sealed class FakePremiumCognitionProvider : IPremiumCognitionProvider
{
    private readonly Func<PremiumCognitionJob, PremiumCognitionProviderResult> _handler;
    private readonly object _gate = new();
    private int _submissionCount;
    private readonly List<PremiumCognitionJob> _jobs = new();

    public FakePremiumCognitionProvider(Func<PremiumCognitionJob, PremiumCognitionProviderResult> handler) => _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    public int SubmissionCount { get { lock (_gate) return _submissionCount; } }
    public IReadOnlyList<PremiumCognitionJob> SnapshotJobs() { lock (_gate) return _jobs.Select(CloneJob).ToArray(); }
    public ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken)
    {
        lock (_gate) { _submissionCount++; _jobs.Add(CloneJob(job)); }
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_handler(CloneJob(job)));
    }
    private static PremiumCognitionJob CloneJob(PremiumCognitionJob job) => job with { Observation = job.Observation with { } };
}

/// <summary>
/// Deep module behind the existing simulation adapter interface. It never validates or mutates a world;
/// successful and fallback proposals still go through the existing deterministic world validator.
/// </summary>
public sealed class SnowGlobeTwoTierCognitionModule : ISnowGlobeIdentifiedInferenceAdapter, ICognitionReceiptInspector
{
    private static readonly HashSet<string> AllowlistedReasons = new(StringComparer.Ordinal)
    { "premium_success", "local_success", "local_fallback", "local_invalid", "local_error", "deterministic_idle", "capacity_denied", "job_cap_denied", "run_ceiling_denied", "account_ceiling_denied", "open_reservation_cap_denied", "record_headroom_denied", "provider_rejected", "provider_timeout", "provider_malformed", "provider_unknown", "policy_rejected" };
    private readonly CognitionLane _lane;
    private readonly ModelPolicySnapshot _policy;
    private readonly ISnowGlobeIdentifiedInferenceAdapter _local;
    private readonly IPremiumCognitionProvider? _premium;
    private readonly IFinancialJournal _journal;
    private readonly string _financialJournalIdentity;
    private readonly object _flightGate = new();
    private readonly Dictionary<string, Flight> _flights = new(StringComparer.Ordinal);
    private readonly AsyncLocal<bool> _executing = new();

    public SnowGlobeTwoTierCognitionModule(CognitionLane lane, string financialRunIdentity, ModelPolicySnapshot policy, ISnowGlobeIdentifiedInferenceAdapter localFallback, InMemoryCognitionJobJournal journal, IPremiumCognitionProvider? premiumProvider = null)
        : this(lane, BindLegacyJournal(lane, financialRunIdentity, policy, journal), policy, localFallback, (IFinancialJournal)journal, premiumProvider) { }

    public SnowGlobeTwoTierCognitionModule(CognitionLane lane, FinancialJournalHeader expectedJournalHeader, ModelPolicySnapshot policy, ISnowGlobeIdentifiedInferenceAdapter localFallback, IFinancialJournal journal, IPremiumCognitionProvider? premiumProvider = null)
    {
        if (!Enum.IsDefined(lane)) throw new ArgumentOutOfRangeException(nameof(lane));
        ArgumentNullException.ThrowIfNull(expectedJournalHeader); expectedJournalHeader.Validate();
        ArgumentNullException.ThrowIfNull(policy); policy.Validate();
        ArgumentNullException.ThrowIfNull(localFallback); ArgumentNullException.ThrowIfNull(journal);
        if (!SnowGlobeInferenceIdentity.IsCanonical(localFallback.AdapterIdentity)) throw new ArgumentException("Local adapter identity is not canonical.", nameof(localFallback));
        if (!string.Equals(policy.LocalAdapterIdentity, localFallback.AdapterIdentity, StringComparison.Ordinal)) throw new ArgumentException("Policy must bind the exact local fallback adapter.", nameof(policy));
        if (lane == CognitionLane.Premium && premiumProvider is null) throw new ArgumentNullException(nameof(premiumProvider));
        FinancialJournalHeader header = journal.Read(new FinancialJournalPageQuery(0, 0)).Header;
        if (!header.Equals(expectedJournalHeader) || header.Lane != lane
            || !string.Equals(header.PolicyDigest, policy.Digest, StringComparison.Ordinal)
            || !string.Equals(header.PremiumModelRevisionIdentity, policy.PremiumModelRevisionIdentity, StringComparison.Ordinal))
            throw new ArgumentException("Financial Journal immutable header does not match the cognition module.", nameof(journal));
        _lane = lane; _financialJournalIdentity = header.FinancialJournalIdentity; _policy = policy; _local = localFallback; _journal = journal; _premium = premiumProvider;
        AdapterIdentity = $"snow_globe_two_tier/{LaneName(lane)}/{policy.Digest}/{header.FinancialJournalIdentity}";
        if (!SnowGlobeInferenceIdentity.IsCanonical(AdapterIdentity)) throw new InvalidOperationException("Derived adapter identity is not canonical.");
    }
    public string AdapterIdentity { get; }

    public async ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateObservation(observation);
        if (_executing.Value) throw new InvalidOperationException("Reentrant cognition execution is not allowed.");
        cancellationToken.ThrowIfCancellationRequested();
        string key = CognitionCanonical.Digest($"{AdapterIdentity}|{observation.Tick}|{observation.AgentId}");
        string digest = CognitionCanonical.Digest($"{AdapterIdentity}|{CognitionCanonical.Observation(observation)}");
        Flight flight;
        bool owner = false;
        lock (_flightGate)
        {
            if (_flights.TryGetValue(key, out flight!))
            {
                if (!string.Equals(flight.JobDigest, digest, StringComparison.Ordinal)) throw new InvalidOperationException("Cognition idempotency key was reused with different observations.");
            }
            else { flight = new Flight(digest); _flights.Add(key, flight); owner = true; }
        }
        if (owner) _ = RunFlightAsync(flight, observation with { }, key, digest, cancellationToken);
        return (await flight.Completion.Task.ConfigureAwait(false)) with { };
    }

    private async Task RunFlightAsync(Flight flight, SnowGlobeObservation observation, string key, string digest, CancellationToken cancellationToken)
    {
        _executing.Value = true;
        try { flight.Completion.TrySetResult(await ExecuteAsync(observation, key, digest, cancellationToken).ConfigureAwait(false)); }
        catch (OperationCanceledException exception)
        {
            flight.Completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception) { flight.Completion.TrySetException(exception); }
        finally { _executing.Value = false; }
    }

    private static FinancialJournalHeader BindLegacyJournal(CognitionLane lane, string financialRunIdentity, ModelPolicySnapshot policy, InMemoryCognitionJobJournal journal)
    {
        ArgumentNullException.ThrowIfNull(policy); ArgumentNullException.ThrowIfNull(journal);
        if (!Enum.IsDefined(lane)) throw new ArgumentOutOfRangeException(nameof(lane));
        if (!SnowGlobeInferenceIdentity.IsCanonical(financialRunIdentity)) throw new ArgumentException("Financial run identity is not canonical.", nameof(financialRunIdentity));
        policy.Validate();
        string opaqueBinding = "byok-account-sha256-" + CognitionCanonical.Digest("legacy-in-memory|" + financialRunIdentity);
        FinancialJournalHeader header = FinancialJournalHeader.Create(financialRunIdentity, financialRunIdentity, lane, policy.Digest,
            policy.PremiumModelRevisionIdentity, lane == CognitionLane.Premium ? new ByokAccountBindingIdentity(opaqueBinding) : null,
            long.MaxValue, long.MaxValue, FinancialJournalBounds.MaximumRecords, journal.LegacyPremiumCapacity);
        journal.BindLegacy(header);
        return header;
    }

    private async ValueTask<SnowGlobeActionProposal> ExecuteAsync(SnowGlobeObservation observation, string key, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PremiumCognitionJob? job = _lane == CognitionLane.Premium
            ? new PremiumCognitionJob(key, digest, _policy.Digest, _policy.PremiumModelIdentity, _policy.PremiumModelRevisionIdentity, observation with { })
            : null;
        FinancialJournalApplyResult admission = _journal.Apply(new AdmitAndReserveFinancialJournalCommand(key, digest, job, _lane == CognitionLane.Premium ? _policy.CostCeilingMicrousd : 0));
        if (admission.Status == FinancialJournalApplyStatus.Conflict) throw new InvalidOperationException("Cognition idempotency key was reused with different observations.");
        if (admission.Status == FinancialJournalApplyStatus.Replay) return admission.Receipt!.Proposal with { };
        if (admission.Status == FinancialJournalApplyStatus.Pending) return await AwaitPendingAsync(key, digest).ConfigureAwait(false);

        InferenceReceipt receipt;
        if (admission.Status == FinancialJournalApplyStatus.SubmissionUnknown)
            receipt = await FallbackAsync(observation, key, digest, SubmissionState.SubmissionUnknown, ChargeState.Unknown, _policy.CostCeilingMicrousd, "provider_unknown").ConfigureAwait(false);
        else if (admission.Status == FinancialJournalApplyStatus.RecordCapacityExhausted)
        {
            // No durable idempotency key can be created once even a denial+completion pair cannot fit.
            // This module instance still caches the deterministic Idle flight and performs no provider/local call.
            return new SnowGlobeActionProposal(observation.AgentId, SnowGlobeActionKind.Idle);
        }
        else if (IsDenial(admission.Status))
        {
            string primary = admission.Status == FinancialJournalApplyStatus.OpenReservationCapDenied ? "capacity_denied" : admission.OutcomeCode;
            receipt = _lane == CognitionLane.Local
                ? Idle(observation.AgentId, key, digest, SubmissionState.NotApplicable, ChargeState.NotApplicable, 0, "deterministic_idle", primary)
                : await FallbackAsync(observation, key, digest, SubmissionState.DefinitelyNotSubmitted, ChargeState.NotApplicable, 0, primary).ConfigureAwait(false);
        }
        else if (_lane == CognitionLane.Local)
            receipt = await LocalOnlyAsync(observation, key, digest, cancellationToken).ConfigureAwait(false);
        else
        {
            FinancialJournalApplyResult marked = _journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(key, digest, admission.Version));
            if (marked.Status != FinancialJournalApplyStatus.DispatchMarked) throw new InvalidOperationException("Premium dispatch marker was not durably accepted.");
            receipt = await PremiumAsync(job!, observation, key, digest, cancellationToken).ConfigureAwait(false);
            admission = marked;
        }

        FinancialJournalApplyResult completed = _journal.Apply(new CompleteFinancialJournalCommand(key, digest, admission.Version, receipt));
        if (completed.Status is not (FinancialJournalApplyStatus.Completed or FinancialJournalApplyStatus.Replay))
            throw new InvalidOperationException("Financial Journal completion was not accepted.");
        return (completed.Receipt ?? receipt).Proposal with { };
    }

    private async ValueTask<SnowGlobeActionProposal> AwaitPendingAsync(string key, string digest)
    {
        while (true)
        {
            FinancialJournalEntrySnapshot? entry = ReadAllEntries().FirstOrDefault(value => string.Equals(value.IdempotencyKey, key, StringComparison.Ordinal));
            if (entry is null || !string.Equals(entry.JobDigest, digest, StringComparison.Ordinal)) throw new InvalidOperationException("Pending Financial Journal entry disappeared or changed.");
            if (entry.Receipt is not null) return entry.Receipt.Proposal with { };
            await Task.Delay(1).ConfigureAwait(false);
        }
    }

    private static bool IsDenial(FinancialJournalApplyStatus status) => status is FinancialJournalApplyStatus.JobCapDenied
        or FinancialJournalApplyStatus.RunCeilingDenied or FinancialJournalApplyStatus.AccountCeilingDenied
        or FinancialJournalApplyStatus.OpenReservationCapDenied or FinancialJournalApplyStatus.RecordHeadroomDenied;

    private async ValueTask<InferenceReceipt> LocalOnlyAsync(SnowGlobeObservation observation, string key, string digest, CancellationToken cancellationToken)
    {
        try
        {
            SnowGlobeActionProposal proposal = await _local.ProposeAsync(observation, cancellationToken).ConfigureAwait(false);
            return IsProposalValid(observation, proposal)
                ? Receipt(key, digest, SubmissionState.NotApplicable, ChargeState.NotApplicable, 0, 0, "local_success", "local_success", proposal)
                : Idle(observation.AgentId, key, digest, SubmissionState.NotApplicable, ChargeState.NotApplicable, 0, "deterministic_idle", "local_invalid");
        }
        catch { return Idle(observation.AgentId, key, digest, SubmissionState.NotApplicable, ChargeState.NotApplicable, 0, "deterministic_idle", "local_error"); }
    }

    private async ValueTask<InferenceReceipt> PremiumAsync(PremiumCognitionJob job, SnowGlobeObservation observation, string key, string digest, CancellationToken cancellationToken)
    {
        PremiumCognitionProviderResult? result;
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_policy.TimeoutMilliseconds);
            Task<PremiumCognitionProviderResult> providerTask = _premium!.SubmitOnceAsync(job, timeout.Token).AsTask();
            result = await providerTask.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch { return await FallbackAsync(observation, key, digest, SubmissionState.SubmissionUnknown, ChargeState.Unknown, _policy.CostCeilingMicrousd, "provider_unknown").ConfigureAwait(false); }

        if (result is null)
            return await FallbackAsync(observation, key, digest, SubmissionState.SubmissionUnknown, ChargeState.Unknown, _policy.CostCeilingMicrousd, "provider_unknown").ConfigureAwait(false);
        if (IsDefinitelyNotSubmitted(result))
            return await FallbackAsync(observation, key, digest, SubmissionState.DefinitelyNotSubmitted, ChargeState.Released, _policy.CostCeilingMicrousd, "provider_rejected").ConfigureAwait(false);
        if (result.SubmissionState != SubmissionState.ResponseReceived)
            return await FallbackAsync(observation, key, digest, SubmissionState.SubmissionUnknown, ChargeState.Unknown, _policy.CostCeilingMicrousd, ReasonFor(result)).ConfigureAwait(false);
        if (!IsPremiumResultValid(result, observation, out long settled))
            return await FallbackAsync(observation, key, digest, SubmissionState.ResponseReceived, ChargeState.Unknown, _policy.CostCeilingMicrousd, ReasonFor(result)).ConfigureAwait(false);
        return Receipt(key, digest, SubmissionState.ResponseReceived, ChargeState.Settled, _policy.CostCeilingMicrousd, settled, "premium_success", "premium_success", result.Proposal!);
    }

    private async ValueTask<InferenceReceipt> FallbackAsync(SnowGlobeObservation observation, string key, string digest, SubmissionState submission, ChargeState charge, long reserved, string primaryReason)
    {
        try
        {
            SnowGlobeActionProposal proposal = await _local.ProposeAsync(observation, CancellationToken.None).ConfigureAwait(false);
            if (IsProposalValid(observation, proposal)) return Receipt(key, digest, submission, charge, reserved, 0, "local_fallback", primaryReason, proposal);
        }
        catch { }
        return Idle(observation.AgentId, key, digest, submission, charge, reserved, "deterministic_idle", primaryReason);
    }

    private bool IsPremiumResultValid(PremiumCognitionProviderResult result, SnowGlobeObservation observation, out long settled)
    {
        settled = 0;
        if (result.Redirected || result.Status != PremiumResponseStatus.Success
            || !string.Equals(result.EffectiveHost, _policy.ProviderHost, StringComparison.Ordinal)
            || !string.Equals(result.EffectiveRoute, _policy.Route, StringComparison.Ordinal)
            || !string.Equals(result.EffectiveModelIdentity, _policy.PremiumModelIdentity, StringComparison.Ordinal)
            || !string.Equals(result.EffectiveModelRevisionIdentity, _policy.PremiumModelRevisionIdentity, StringComparison.Ordinal)
            || result.InputTokens < 0 || result.OutputTokens < 0 || result.InputTokens > _policy.MaximumInputTokens || result.OutputTokens > _policy.MaximumOutputTokens
            || result.Proposal is null || !IsProposalValid(observation, result.Proposal)) return false;
        try { settled = _policy.CalculateCostMicrousd(result.InputTokens, result.OutputTokens); return settled <= _policy.CostCeilingMicrousd; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
    private bool IsDefinitelyNotSubmitted(PremiumCognitionProviderResult result) =>
        result.SubmissionState == SubmissionState.DefinitelyNotSubmitted
        && result.Status == PremiumResponseStatus.Rejected
        && !result.Redirected
        && string.Equals(result.EffectiveHost, _policy.ProviderHost, StringComparison.Ordinal)
        && string.Equals(result.EffectiveRoute, _policy.Route, StringComparison.Ordinal)
        && string.Equals(result.EffectiveModelIdentity, _policy.PremiumModelIdentity, StringComparison.Ordinal)
        && string.Equals(result.EffectiveModelRevisionIdentity, _policy.PremiumModelRevisionIdentity, StringComparison.Ordinal)
        && result.InputTokens == 0
        && result.OutputTokens == 0
        && result.Proposal is null;

    private static void ValidateObservation(SnowGlobeObservation observation)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(observation.AgentId)) throw new ArgumentException("Observation agent identity is not canonical.", nameof(observation));
        if (observation.HomeSlot < 0 || observation.Tick < 0 || observation.AvailableWood < 0 || observation.AvailableStone < 0
            || observation.StockpileWood < 0 || observation.StockpileStone < 0 || observation.ShelterCount < 0 || observation.StorageCount < 0)
            throw new ArgumentOutOfRangeException(nameof(observation), "Observation values cannot be negative.");
    }
    private static bool IsProposalValid(SnowGlobeObservation observation, SnowGlobeActionProposal proposal)
    {
        if (!string.Equals(observation.AgentId, proposal.AgentId, StringComparison.Ordinal) || !Enum.IsDefined(proposal.Action)) return false;
        return proposal.Action switch
        {
            SnowGlobeActionKind.Idle or SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage => proposal.Quantity == 0,
            SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone or SnowGlobeActionKind.MaintainShelter => proposal.Quantity is > 0 and <= 64,
            _ => false
        };
    }
    private static string ReasonFor(PremiumCognitionProviderResult result) => result.Status switch
    {
        PremiumResponseStatus.TimedOut => "provider_timeout", PremiumResponseStatus.Malformed => "provider_malformed", _ => "provider_rejected"
    };
    private InferenceReceipt Receipt(string key, string digest, SubmissionState submission, ChargeState charge, long reserved, long settled, string reason, string primaryOutcome, SnowGlobeActionProposal proposal)
    {
        if (!AllowlistedReasons.Contains(reason) || !AllowlistedReasons.Contains(primaryOutcome)) throw new InvalidOperationException("Receipt outcome code is not allowlisted.");
        return new(
            key,
            digest,
            _policy.Digest,
            _financialJournalIdentity,
            _lane,
            _policy.PremiumModelIdentity,
            _policy.PremiumModelRevisionIdentity,
            submission,
            charge,
            new CostReservation(reserved, settled),
            reason,
            primaryOutcome,
            proposal with { });
    }
    private InferenceReceipt Idle(string agentId, string key, string digest, SubmissionState submission, ChargeState charge, long reserved, string reason, string primaryOutcome) =>
        Receipt(key, digest, submission, charge, reserved, 0, reason, primaryOutcome, new SnowGlobeActionProposal(agentId, SnowGlobeActionKind.Idle));
    public IReadOnlyList<InferenceReceipt> SnapshotReceipts() => ReadAllEntries().Where(entry => entry.Receipt is not null).Select(entry => FinancialJournalState.CloneReceipt(entry.Receipt!)).ToArray();
    public IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs() => ReadAllEntries().Where(entry => entry.PremiumJob is not null).Select(entry => FinancialJournalState.CloneJob(entry.PremiumJob!)).ToArray();
    public IReadOnlyList<string> SnapshotTrace() => _journal.Read(new FinancialJournalPageQuery(0, 0)).Trace;
    private IReadOnlyList<FinancialJournalEntrySnapshot> ReadAllEntries() => _journal.Read(new FinancialJournalSnapshotQuery()).Entries;
    private sealed class Flight
    {
        internal Flight(string jobDigest) { JobDigest = jobDigest; }
        internal string JobDigest { get; }
        internal TaskCompletionSource<SnowGlobeActionProposal> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private static string LaneName(CognitionLane lane) => lane switch
    {
        CognitionLane.Local => "local",
        CognitionLane.Premium => "premium",
        _ => throw new ArgumentOutOfRangeException(nameof(lane))
    };
}

internal static class CognitionCanonical
{
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    internal static string Observation(SnowGlobeObservation value) => string.Join("|", Field(value.AgentId), Integer(value.HomeSlot), Integer(value.Tick), Integer(value.AvailableWood), Integer(value.AvailableStone), Integer(value.StockpileWood), Integer(value.StockpileStone), Integer(value.ShelterCount), Integer(value.StorageCount));
    internal static string Policy(ModelPolicySnapshot value) => string.Join("|", Field(value.PolicyId), Field(value.ProviderHost), Field(value.Route), Field(value.PremiumModelIdentity), Field(value.PremiumModelRevisionIdentity), Field(value.PromptRevision), Field(value.ProposalSchemaVersion), Field(value.LocalAdapterIdentity), Field(value.Currency), Integer(value.InputMicrousdPerMillionTokens), Integer(value.OutputMicrousdPerMillionTokens), Integer(value.MaximumInputTokens), Integer(value.MaximumOutputTokens), Integer(value.CostCeilingMicrousd), Integer(value.TimeoutMilliseconds), value.RedirectsAllowed ? "1" : "0", value.AutomaticRetriesAllowed ? "1" : "0");
    private static string Field(string value) => $"{Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture)}:{value}";
    private static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);
}
