using System.Collections.ObjectModel;
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

/// <summary>In-memory, lock-protected journal. It intentionally has no persistence or file I/O.</summary>
public sealed class InMemoryCognitionJobJournal : ICognitionReceiptInspector
{
    private readonly object _gate = new();
    private readonly int _premiumCapacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _trace = new();
    public InMemoryCognitionJobJournal(int premiumCapacity = 64)
    {
        if (premiumCapacity < 0) throw new ArgumentOutOfRangeException(nameof(premiumCapacity));
        _premiumCapacity = premiumCapacity;
    }

    internal JournalAdmission Admit(string key, string digest, CognitionLane lane, PremiumCognitionJob? job, long ceiling)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out Entry? existing))
            {
                if (!string.Equals(existing.Digest, digest, StringComparison.Ordinal)) return JournalAdmission.Conflict;
                return JournalAdmission.Replay(existing.Completion.Task);
            }
            if (lane == CognitionLane.Premium && _entries.Values.Count(entry => entry.Lane == CognitionLane.Premium && !entry.Completion.Task.IsCompleted) >= _premiumCapacity)
            {
                Entry denied = new(key, digest, lane, job, new TaskCompletionSource<InferenceReceipt>(TaskCreationOptions.RunContinuationsAsynchronously));
                _entries.Add(key, denied); _trace.Add("capacity_denied");
                return JournalAdmission.Capacity(denied);
            }
            Entry entry = new(key, digest, lane, job, new TaskCompletionSource<InferenceReceipt>(TaskCreationOptions.RunContinuationsAsynchronously));
            _entries.Add(key, entry);
            if (lane == CognitionLane.Premium) { entry.Reserved = ceiling; entry.Charge = ChargeState.Reserved; entry.Submission = SubmissionState.SubmissionUnknown; _trace.Add("reserved"); _trace.Add("submission_unknown"); }
            else _trace.Add("local_admitted");
            return JournalAdmission.Owner(entry);
        }
    }

    internal void Complete(Entry entry, InferenceReceipt receipt)
    {
        lock (_gate)
        {
            entry.Submission = receipt.SubmissionState; entry.Charge = receipt.ChargeState; entry.Settled = receipt.Reservation.SettledMicrousd;
            _trace.Add("completed/" + receipt.ReasonCode);
            entry.Completion.TrySetResult(CloneReceipt(receipt));
        }
    }
    public IReadOnlyList<InferenceReceipt> SnapshotReceipts()
    {
        lock (_gate) return _entries.Values.Where(entry => entry.Completion.Task.IsCompletedSuccessfully).Select(entry => CloneReceipt(entry.Completion.Task.Result)).ToArray();
    }
    public IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs()
    {
        lock (_gate) return _entries.Values.Where(entry => entry.Job is not null).Select(entry => CloneJob(entry.Job!)).ToArray();
    }
    public IReadOnlyList<string> SnapshotTrace() { lock (_gate) return new ReadOnlyCollection<string>(_trace.ToArray()); }
    internal sealed class Entry
    {
        public Entry(string key, string digest, CognitionLane lane, PremiumCognitionJob? job, TaskCompletionSource<InferenceReceipt> completion) { Key = key; Digest = digest; Lane = lane; Job = job; Completion = completion; }
        public string Key { get; } public string Digest { get; } public CognitionLane Lane { get; } public PremiumCognitionJob? Job { get; }
        public TaskCompletionSource<InferenceReceipt> Completion { get; } public long Reserved { get; set; } public long Settled { get; set; }
        public SubmissionState Submission { get; set; } public ChargeState Charge { get; set; }
    }
    internal readonly record struct JournalAdmission(Entry? Entry, Task<InferenceReceipt>? ReplayTask, bool IsConflict, bool IsCapacity)
    {
        public static JournalAdmission Owner(Entry entry) => new(entry, null, false, false);
        public static JournalAdmission Replay(Task<InferenceReceipt> receipt) => new(null, receipt, false, false);
        public static JournalAdmission Capacity(Entry entry) => new(entry, null, false, true);
        public static JournalAdmission Conflict => new(null, null, true, false);
    }
    internal static InferenceReceipt CloneReceipt(InferenceReceipt value) => value with { Reservation = value.Reservation with { }, Proposal = value.Proposal with { } };
    internal static PremiumCognitionJob CloneJob(PremiumCognitionJob value) => value with { Observation = value.Observation with { } };
}

/// <summary>
/// Deep module behind the existing simulation adapter interface. It never validates or mutates a world;
/// successful and fallback proposals still go through the existing deterministic world validator.
/// </summary>
public sealed class SnowGlobeTwoTierCognitionModule : ISnowGlobeIdentifiedInferenceAdapter, ICognitionReceiptInspector
{
    private static readonly HashSet<string> AllowlistedReasons = new(StringComparer.Ordinal)
    { "premium_success", "local_success", "local_fallback", "local_invalid", "local_error", "deterministic_idle", "capacity_denied", "provider_rejected", "provider_timeout", "provider_malformed", "provider_unknown", "policy_rejected" };
    private readonly CognitionLane _lane;
    private readonly ModelPolicySnapshot _policy;
    private readonly ISnowGlobeIdentifiedInferenceAdapter _local;
    private readonly IPremiumCognitionProvider? _premium;
    private readonly InMemoryCognitionJobJournal _journal;
    private readonly string _financialRunIdentity;

    public SnowGlobeTwoTierCognitionModule(CognitionLane lane, string financialRunIdentity, ModelPolicySnapshot policy, ISnowGlobeIdentifiedInferenceAdapter localFallback, InMemoryCognitionJobJournal journal, IPremiumCognitionProvider? premiumProvider = null)
    {
        if (!Enum.IsDefined(lane)) throw new ArgumentOutOfRangeException(nameof(lane));
        if (!SnowGlobeInferenceIdentity.IsCanonical(financialRunIdentity)) throw new ArgumentException("Financial run identity is not canonical.", nameof(financialRunIdentity));
        ArgumentNullException.ThrowIfNull(policy); policy.Validate();
        ArgumentNullException.ThrowIfNull(localFallback); ArgumentNullException.ThrowIfNull(journal);
        if (!SnowGlobeInferenceIdentity.IsCanonical(localFallback.AdapterIdentity)) throw new ArgumentException("Local adapter identity is not canonical.", nameof(localFallback));
        if (!string.Equals(policy.LocalAdapterIdentity, localFallback.AdapterIdentity, StringComparison.Ordinal)) throw new ArgumentException("Policy must bind the exact local fallback adapter.", nameof(policy));
        if (lane == CognitionLane.Premium && premiumProvider is null) throw new ArgumentNullException(nameof(premiumProvider));
        _lane = lane; _financialRunIdentity = financialRunIdentity; _policy = policy; _local = localFallback; _journal = journal; _premium = premiumProvider;
        AdapterIdentity = $"snow_globe_two_tier/{LaneName(lane)}/{policy.Digest}/{financialRunIdentity}";
        if (!SnowGlobeInferenceIdentity.IsCanonical(AdapterIdentity)) throw new InvalidOperationException("Derived adapter identity is not canonical.");
    }
    public string AdapterIdentity { get; }

    public async ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateObservation(observation);
        cancellationToken.ThrowIfCancellationRequested();
        string key = CognitionCanonical.Digest($"{AdapterIdentity}|{observation.Tick}|{observation.AgentId}");
        string digest = CognitionCanonical.Digest($"{AdapterIdentity}|{CognitionCanonical.Observation(observation)}");
        PremiumCognitionJob? job = _lane == CognitionLane.Premium
            ? new PremiumCognitionJob(key, digest, _policy.Digest, _policy.PremiumModelIdentity, _policy.PremiumModelRevisionIdentity, observation with { })
            : null;
        InMemoryCognitionJobJournal.JournalAdmission admission = _journal.Admit(key, digest, _lane, job, _policy.CostCeilingMicrousd);
        if (admission.IsConflict) throw new InvalidOperationException("Cognition idempotency key was reused with different observations.");
        if (admission.ReplayTask is not null) return (await admission.ReplayTask.ConfigureAwait(false)).Proposal with { };
        InMemoryCognitionJobJournal.Entry entry = admission.Entry!;
        InferenceReceipt receipt = _lane == CognitionLane.Local
            ? await LocalOnlyAsync(observation, key, digest, cancellationToken).ConfigureAwait(false)
            : admission.IsCapacity
                ? await FallbackAsync(observation, key, digest, SubmissionState.DefinitelyNotSubmitted, ChargeState.NotApplicable, 0, "capacity_denied").ConfigureAwait(false)
                : await PremiumAsync(job!, observation, key, digest, cancellationToken).ConfigureAwait(false);
        _journal.Complete(entry, receipt);
        return receipt.Proposal with { };
    }

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
            _financialRunIdentity,
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
    public IReadOnlyList<InferenceReceipt> SnapshotReceipts() => _journal.SnapshotReceipts();
    public IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs() => _journal.SnapshotPremiumJobs();
    public IReadOnlyList<string> SnapshotTrace() => _journal.SnapshotTrace();
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
